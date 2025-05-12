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

    public void ApplyEffect(Unit targetUnit, EffectSO effectToApply, Unit caster)
    {
        if (targetUnit == null || !targetUnit.IsAlive || effectToApply == null || targetUnit.Stats == null)
        {
            // DebugHelper.LogWarning($"EffectSystem.ApplyEffect: Invalid target, effect, or target missing Stats. Target: {targetUnit?.unitName}, Effect: {effectToApply?.effectName}", targetUnit);
            return;
        }

        ActiveStatusEffect existingEffect = targetUnit.Stats.ActiveEffects.FirstOrDefault(ae => ae.BaseEffect == effectToApply);
        string casterName = caster != null ? caster.unitName : "Unknown Source";

        if (existingEffect != null)
        {
            switch (effectToApply.stackingBehavior)
            {
                case EffectStackingBehavior.None:
                    // CombatLogger.LogEvent($"Effect '{effectToApply.effectName}' (Stacking: None) already on {targetUnit.unitName}. No change.", Color.gray); // Optional, can be noisy
                    return;

                case EffectStackingBehavior.RefreshDuration:
                    existingEffect.RefreshDuration();
                    CombatLogger.LogStatusApplied(targetUnit, $"{effectToApply.effectName} duration refreshed", caster); // MODIFIED
                    targetUnit.Stats.RecalculateAffectedStats();
                    break;

                case EffectStackingBehavior.IncreaseStacks:
                    bool stacked = existingEffect.AddStack(); // AddStack handles refreshing duration
                    if (stacked)
                    {
                        CombatLogger.LogStatusApplied(targetUnit, $"{effectToApply.effectName} stacked to {existingEffect.CurrentStacks}", caster); // MODIFIED
                    }
                    else 
                    {
                        CombatLogger.LogStatusApplied(targetUnit, $"{effectToApply.effectName} (at max stacks) duration refreshed", caster); // MODIFIED
                    }
                    targetUnit.Stats.RecalculateAffectedStats(); 
                    break;

                case EffectStackingBehavior.AddNewInstance:
                    ActiveStatusEffect newSeparateInstance = new ActiveStatusEffect(effectToApply, caster);
                    targetUnit.Stats.AddEffect(newSeparateInstance); 
                    // UnitStats.AddEffect will call CombatLogger.LogStatusApplied for the new instance
                    break;

                default:
                    DebugHelper.LogWarning($"Unknown stacking behavior: {effectToApply.stackingBehavior} for effect {effectToApply.effectName}", effectToApply);
                    return;
            }
        }
        else // No existing effect, add new instance
        {
            ActiveStatusEffect newEffectInstance = new ActiveStatusEffect(effectToApply, caster);
            targetUnit.Stats.AddEffect(newEffectInstance);
            // UnitStats.AddEffect should call CombatLogger.LogStatusApplied
        }
    }

    public void RemoveEffectInstance(Unit targetUnit, ActiveStatusEffect effectInstance)
    {
        if (targetUnit == null || effectInstance == null || targetUnit.Stats == null) return;
        // Logging for removal will be handled in UnitStats.RemoveEffect
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
                targetUnit.Stats.RemoveEffect(activeEffect); // This will log individual removals if UnitStats.RemoveEffect does
            }
            // CombatLogger.LogEvent($"All stacks of '{effectSOToRemove.effectName}' removed from {targetUnit.unitName}.", Color.gray); // Optional summary
        }
    }

    public void TickUnitEffects(Unit unit)
    {
        if (unit == null || !unit.IsAlive || unit.Stats == null) return;

        List<ActiveStatusEffect> effectsToExpire = new List<ActiveStatusEffect>();
        
        // Iterate on a copy in case effects are removed during iteration (e.g., by expiring themselves or other effects)
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
                        // CombatLogger.LogDamage(effect.Caster, unit, totalTickPower, baseEffect.tickActionDamageType, false); // False for isCritical
                        // The above LogDamage is good, but might be too verbose if the "takes damage" is also logged.
                        // Let's use a more specific tick log.
                        CombatLogger.LogEvent($"{unit.unitName} takes {totalTickPower} {baseEffect.tickActionDamageType} damage from '{baseEffect.effectName}' (tick from {casterName}).", Color.magenta); // ADDED
                        
                        unit.Stats.ModifyVitality(-totalTickPower); 
                        if (!unit.IsAlive)
                        {
                            CombatLogger.LogEvent($"{unit.unitName} defeated by '{baseEffect.effectName}' from {casterName}!", Color.red); // ADDED
                        }
                    }
                    else if (baseEffect.tickActionType == EffectTickActionType.Heal)
                    {
                        CombatLogger.LogEvent($"{unit.unitName} heals {totalTickPower} VP from '{baseEffect.effectName}' (tick from {casterName}).", Color.green); // ADDED
                        unit.Stats.ModifyVitality(totalTickPower);
                    }
                }
            }

            if (effect.TickDuration()) 
            {
                effectsToExpire.Add(effect);
            }
        }

        if (effectsToExpire.Count > 0)
        {
            foreach (ActiveStatusEffect expiredEffect in effectsToExpire)
            {
                // Logging of effect expiration will be handled by UnitStats.RemoveEffect
                unit.Stats.RemoveEffect(expiredEffect);
            }
        }
    }
}