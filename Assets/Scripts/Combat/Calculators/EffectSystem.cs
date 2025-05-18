// EffectSystem.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MythTactics.Combat; // For Unit, EffectSO, ActiveStatusEffect, DamageType etc.

public class EffectSystem : MonoBehaviour
{
    public static EffectSystem Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Duplicate EffectSystem instance found. Destroying this one.", this);
            Destroy(gameObject);
        }
    }

    private int CalculateDirectEffectPower(EffectSO effect, Unit caster)
    {
        int power = effect.onApplyDirectEffectBasePower;
        if (effect.onApplyDirectEffectScalesWithCasterStat && caster != null && caster.Stats != null)
        {
            int casterStatValue = 0;
            UnitPrimaryAttributes casterAttributes = caster.Stats.EffectiveAttributes;

            switch (effect.onApplyDirectEffectScalingStat)
            {
                case StatType.Core: casterStatValue = casterAttributes.Core; break;
                case StatType.Echo: casterStatValue = casterAttributes.Echo; break;
                case StatType.Pulse: casterStatValue = casterAttributes.Pulse; break;
                case StatType.Spark: casterStatValue = casterAttributes.Spark; break;
                case StatType.Glimmer: casterStatValue = casterAttributes.Glimmer; break;
                case StatType.Aura: casterStatValue = casterAttributes.Aura; break;
                default:
                    break;
            }
            power += Mathf.FloorToInt(casterStatValue * effect.onApplyDirectEffectScalingFactor);
        }
        return Mathf.Max(0, power);
    }


        public void ApplyEffect(Unit targetUnit, EffectSO effectToApply, Unit caster)
    {
        if (targetUnit == null || !targetUnit.IsAlive || effectToApply == null || targetUnit.Stats == null)
        {
            return;
        }

        string casterNameForLog = caster != null ? caster.unitName : "Effect Source";

        // --- Handle On-Apply Direct Effect FIRST ---
        if (effectToApply.onApplyDirectEffectType != DirectEffectType.None)
        {
            int directPower = CalculateDirectEffectPower(effectToApply, caster);

            bool shouldApplyDirectEffect = false;
            switch (effectToApply.onApplyDirectEffectType)
            {
                case DirectEffectType.HealVitality:
                case DirectEffectType.RestoreMana:
                    // For beneficial effects, apply if power > 0
                    shouldApplyDirectEffect = directPower > 0;
                    break;
                case DirectEffectType.DamageVitality:
                    // For damage, apply if power is non-zero (could be negative if something buffs damage to < 0, though usually positive)
                    // Or, if you intend for 0 damage to still "hit" for other purposes (e.g. on-hit effects), adjust this.
                    // For now, let's assume actual damage means power > 0.
                    shouldApplyDirectEffect = directPower != 0; // If directPower is damage, it should be positive here
                    break;
                // Add other cases as needed
            }

            if (shouldApplyDirectEffect)
            {
                switch (effectToApply.onApplyDirectEffectType)
                {
                    case DirectEffectType.HealVitality:
                        int oldVP = targetUnit.Stats.currentVitalityPoints;
                        targetUnit.Stats.ModifyVitality(directPower); // ModifyVitality handles clamping
                        int healedAmount = targetUnit.Stats.currentVitalityPoints - oldVP;
                        if (healedAmount != 0) // Log if there was any change
                        { 
                            CombatLogger.LogEvent($"{targetUnit.unitName} {(healedAmount > 0 ? "healed" : "took")} {Mathf.Abs(healedAmount)} VP from '{effectToApply.effectName}' (used by {casterNameForLog}).", healedAmount > 0 ? Color.green : Color.yellow, LogMessageType.StatusChange);
                        }
                        break;
                    case DirectEffectType.RestoreMana:
                        int oldMana = targetUnit.Stats.currentManaPoints;
                        targetUnit.Stats.currentManaPoints = Mathf.Clamp(targetUnit.Stats.currentManaPoints + directPower, 0, targetUnit.Stats.MaxManaPoints);
                        int restoredMana = targetUnit.Stats.currentManaPoints - oldMana;
                        if (restoredMana != 0) 
                        {
                            CombatLogger.LogEvent($"{targetUnit.unitName} {(restoredMana > 0 ? "restored" : "lost")} {Mathf.Abs(restoredMana)} MP from '{effectToApply.effectName}' (used by {casterNameForLog}).", restoredMana > 0 ? Color.cyan : new Color(0.5f, 0.8f, 0.8f), LogMessageType.StatusChange);
                        }
                        break;
                    case DirectEffectType.DamageVitality:
                        int oldVPDamage = targetUnit.Stats.currentVitalityPoints;
                        targetUnit.Stats.ModifyVitality(-directPower); // Negative for damage
                        int damageTaken = oldVPDamage - targetUnit.Stats.currentVitalityPoints;
                         if (damageTaken != 0) 
                        { 
                             CombatLogger.LogEvent($"{targetUnit.unitName} took {damageTaken} {effectToApply.onApplyDirectEffectDamageType} damage from '{effectToApply.effectName}' (used by {casterNameForLog}).", Color.red, LogMessageType.CombatAction);
                        }
                        if (!targetUnit.IsAlive)
                        {
                             CombatLogger.LogEvent($"{targetUnit.unitName} defeated by '{effectToApply.effectName}' from {casterNameForLog}!", Color.red, LogMessageType.CombatAction);
                        }
                        break;
                }
            }
            else // ADDED: Debug log for when direct effect isn't applied due to power
            {
                if (effectToApply.onApplyDirectEffectType != DirectEffectType.None) // Only log if it was meant to be a direct effect
                {
                    Debug.LogWarning($"EffectSystem.ApplyEffect: Direct effect '{effectToApply.onApplyDirectEffectType}' for '{effectToApply.effectName}' had calculated power {directPower}, so it was not applied based on conditions.", effectToApply);
                }
            }
        }

        // If the effect is purely instant and has no duration, stat modifiers, or ticks, we might not need to add it as an ActiveStatusEffect.
        if (effectToApply.durationType == EffectDurationType.Instant &&
            (effectToApply.statModifiers == null || effectToApply.statModifiers.Count == 0) &&
            effectToApply.tickActionType == EffectTickActionType.None)
        {
            targetUnit.Stats.RecalculateAffectedStats(); 
            return; 
        }

        // ... (rest of the method for persistent effects remains the same) ...
        ActiveStatusEffect existingEffect = targetUnit.Stats.ActiveEffects.FirstOrDefault(ae => ae.BaseEffect == effectToApply);
        
        if (existingEffect != null)
        {
            switch (effectToApply.stackingBehavior)
            {
                case EffectStackingBehavior.None:
                    return;
                case EffectStackingBehavior.RefreshDuration:
                    existingEffect.RefreshDuration();
                    CombatLogger.LogStatusApplied(targetUnit, $"{effectToApply.effectName} duration refreshed", caster);
                    targetUnit.Stats.RecalculateAffectedStats();
                    break;
                case EffectStackingBehavior.IncreaseStacks:
                    bool stacked = existingEffect.AddStack();
                    if (stacked)
                    {
                        CombatLogger.LogStatusApplied(targetUnit, $"{effectToApply.effectName} stacked to {existingEffect.CurrentStacks}", caster);
                    }
                    else
                    {
                        CombatLogger.LogStatusApplied(targetUnit, $"{effectToApply.effectName} (at max stacks) duration refreshed", caster);
                    }
                    targetUnit.Stats.RecalculateAffectedStats();
                    break;
                case EffectStackingBehavior.AddNewInstance:
                    ActiveStatusEffect newSeparateInstance = new ActiveStatusEffect(effectToApply, caster);
                    targetUnit.Stats.AddEffect(newSeparateInstance);
                    break;
                default:
                    DebugHelper.LogWarning($"Unknown stacking behavior: {effectToApply.stackingBehavior} for effect {effectToApply.effectName}", effectToApply);
                    return;
            }
        }
        else
        {
            ActiveStatusEffect newEffectInstance = new ActiveStatusEffect(effectToApply, caster);
            targetUnit.Stats.AddEffect(newEffectInstance);
        }
    }

    public void RemoveEffectInstance(Unit targetUnit, ActiveStatusEffect effectInstance)
    {
        if (targetUnit == null || effectInstance == null || targetUnit.Stats == null) return;
        targetUnit.Stats.RemoveEffect(effectInstance);
    }

    public void RemoveAllStacksOfEffect(Unit targetUnit, EffectSO effectSOToRemove)
    {
        if (targetUnit == null || effectSOToRemove == null || targetUnit.Stats == null) return;
        List<ActiveStatusEffect> effectsToRemove = targetUnit.Stats.ActiveEffects
                                                    .Where(ae => ae.BaseEffect == effectSOToRemove)
                                                    .ToList();
        if (effectsToRemove.Count > 0)
        {
            foreach (var activeEffect in effectsToRemove)
            {
                targetUnit.Stats.RemoveEffect(activeEffect);
            }
        }
    }

    public void TickUnitEffects(Unit unit)
    {
        if (unit == null || !unit.IsAlive || unit.Stats == null) return;

        List<ActiveStatusEffect> effectsToExpire = new List<ActiveStatusEffect>();
        
        foreach (ActiveStatusEffect effect in unit.Stats.ActiveEffects.ToList()) 
        {
            if (!unit.IsAlive) break; 

            EffectSO baseEffect = effect.BaseEffect;
            if (baseEffect == null) continue;

            if (baseEffect.tickActionType != EffectTickActionType.None)
            {
                int tickPower = baseEffect.tickActionBasePower; 
                if (baseEffect.tickActionScalesWithCasterStat)
                {
                    int snapshottedStatValue = effect.GetSnapshottedCasterScalingStatValue();
                    int bonusFromStat = Mathf.FloorToInt(snapshottedStatValue * baseEffect.tickScalingFactor);
                    tickPower += bonusFromStat;
                }
                int totalTickPower = tickPower * effect.CurrentStacks; 
                totalTickPower = Mathf.Max(0, totalTickPower); 

                if (totalTickPower > 0) 
                {
                    string casterName = effect.Caster != null ? effect.Caster.unitName : "Unknown Source";
                    if (baseEffect.tickActionType == EffectTickActionType.Damage)
                    {
                        // Log the damage taken from the tick
                        CombatLogger.LogEvent($"{unit.unitName} takes {totalTickPower} {baseEffect.tickActionDamageType} damage from '{baseEffect.effectName}' (tick from {casterName}).", Color.magenta, LogMessageType.StatusChange); // Or CombatAction
                        unit.Stats.ModifyVitality(-totalTickPower); 
                        if (!unit.IsAlive)
                        {
                            CombatLogger.LogEvent($"{unit.unitName} defeated by '{baseEffect.effectName}' from {casterName}!", Color.red, LogMessageType.CombatAction);
                        }
                    }
                    else if (baseEffect.tickActionType == EffectTickActionType.Heal)
                    {
                        // Log the healing from the tick
                        CombatLogger.LogEvent($"{unit.unitName} heals {totalTickPower} VP from '{baseEffect.effectName}' (tick from {casterName}).", Color.green, LogMessageType.StatusChange);
                        unit.Stats.ModifyVitality(totalTickPower);
                    }
                }
            }

            if (baseEffect.durationType != EffectDurationType.Instant)
            {
                if (effect.TickDuration()) 
                {
                    effectsToExpire.Add(effect);
                }
            }
        }

        if (effectsToExpire.Count > 0)
        {
            foreach (ActiveStatusEffect expiredEffect in effectsToExpire)
            {
                unit.Stats.RemoveEffect(expiredEffect);
            }
        }
    }
}