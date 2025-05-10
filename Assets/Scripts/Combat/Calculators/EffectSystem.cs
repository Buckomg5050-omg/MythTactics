// EffectSystem.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MythTactics.Combat;

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
            DebugHelper.LogWarning($"EffectSystem.ApplyEffect: Invalid target, effect, or target missing Stats. Target: {targetUnit?.unitName}, Effect: {effectToApply?.effectName}", targetUnit);
            return;
        }

        ActiveStatusEffect existingEffect = targetUnit.Stats.ActiveEffects.FirstOrDefault(ae => ae.BaseEffect == effectToApply);

        if (existingEffect != null)
        {
            switch (effectToApply.stackingBehavior)
            {
                case EffectStackingBehavior.None:
                    DebugHelper.Log($"Effect '{effectToApply.effectName}' (Stacking: None) already on {targetUnit.unitName}. No change.", targetUnit);
                    return;

                case EffectStackingBehavior.RefreshDuration:
                    existingEffect.RefreshDuration();
                    DebugHelper.Log($"Effect '{effectToApply.effectName}' on {targetUnit.unitName} duration refreshed to {existingEffect.RemainingDuration}.", targetUnit);
                    targetUnit.Stats.RecalculateAffectedStats();
                    break;

                case EffectStackingBehavior.IncreaseStacks:
                    if (existingEffect.AddStack()) // AddStack handles refreshing duration
                    {
                        DebugHelper.Log($"Effect '{effectToApply.effectName}' on {targetUnit.unitName} stacked to {existingEffect.CurrentStacks}. Duration refreshed.", targetUnit);
                    }
                    else 
                    {
                        // Already at max stacks, but AddStack still refreshed duration if applicable.
                        DebugHelper.Log($"Effect '{effectToApply.effectName}' on {targetUnit.unitName} at max stacks ({existingEffect.CurrentStacks}). Duration refreshed.", targetUnit);
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
            DebugHelper.Log($"All stacks of '{effectSOToRemove.effectName}' removed from {targetUnit.unitName}.", targetUnit);
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

            // --- Process Tick Action (DoT/HoT) ---
            if (baseEffect.tickActionType != EffectTickActionType.None)
            {
                int tickPower = baseEffect.tickActionBasePower; // Start with base power

                // --- NEW: Apply scaling if enabled ---
                if (baseEffect.tickActionScalesWithCasterStat)
                {
                    int snapshottedStatValue = effect.GetSnapshottedCasterScalingStatValue();
                    int bonusFromStat = Mathf.FloorToInt(snapshottedStatValue * baseEffect.tickScalingFactor);
                    tickPower += bonusFromStat;
                    // Log the scaling contribution for clarity during testing
                    // DebugHelper.Log($"Effect '{baseEffect.effectName}' on {unit.unitName}: BaseTickPower={baseEffect.tickActionBasePower}, CasterStat({baseEffect.tickScalingStat})={snapshottedStatValue}, Bonus={bonusFromStat}, Pre-StackPower={tickPower}", unit);
                }
                // --- End of NEW scaling ---

                int totalTickPower = tickPower * effect.CurrentStacks; // Apply stacks to the (potentially scaled) power
                totalTickPower = Mathf.Max(0, totalTickPower); // Ensure power isn't negative

                if (totalTickPower > 0) // Only proceed if there's actual power
                {
                    if (baseEffect.tickActionType == EffectTickActionType.Damage)
                    {
                        DebugHelper.Log($"{unit.unitName} takes {totalTickPower} {baseEffect.tickActionDamageType} damage from '{baseEffect.effectName}' tick. (Base: {baseEffect.tickActionBasePower}, ScaledTickPower: {tickPower}, Stacks: {effect.CurrentStacks})", unit);
                        // For now, direct vitality modification for True/Poison
                        if (baseEffect.tickActionDamageType == DamageType.True || baseEffect.tickActionDamageType == DamageType.Poison)
                        {
                            unit.Stats.ModifyVitality(-totalTickPower); 
                            if (!unit.IsAlive)
                            {
                                DebugHelper.Log($"{unit.unitName} defeated by tick damage from '{baseEffect.effectName}'.", unit);
                            }
                        }
                        else
                        {
                            DebugHelper.LogWarning($"Tick damage type {baseEffect.tickActionDamageType} for '{baseEffect.effectName}' not fully handled with mitigation yet. Applying as unmitigated for now.", unit);
                            unit.Stats.ModifyVitality(-totalTickPower);
                        }
                    }
                    else if (baseEffect.tickActionType == EffectTickActionType.Heal)
                    {
                        DebugHelper.Log($"{unit.unitName} VP before HoT tick: {unit.Stats.currentVitalityPoints}/{unit.Stats.MaxVitalityPoints}", unit);
                        DebugHelper.Log($"{unit.unitName} heals {totalTickPower} VP from '{baseEffect.effectName}' tick. (Base: {baseEffect.tickActionBasePower}, ScaledTickPower: {tickPower}, Stacks: {effect.CurrentStacks})", unit);
                        unit.Stats.ModifyVitality(totalTickPower);
                        DebugHelper.Log($"{unit.unitName} VP after HoT tick: {unit.Stats.currentVitalityPoints}/{unit.Stats.MaxVitalityPoints}", unit);
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
                unit.Stats.RemoveEffect(expiredEffect);
            }
        }
    }
}