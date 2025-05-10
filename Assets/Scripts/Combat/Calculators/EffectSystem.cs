// EffectSystem.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EffectSystem : MonoBehaviour // Or a static class if you prefer
{
    public static EffectSystem Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // Optional: if you want it to persist across scenes
        }
        else
        {
            Debug.LogWarning("Duplicate EffectSystem instance found. Destroying this one.", this);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Applies an effect to a target unit.
    /// Handles stacking, duration refresh, and adding the effect to the unit.
    /// </summary>
    public void ApplyEffect(Unit targetUnit, EffectSO effectToApply, Unit caster)
    {
        if (targetUnit == null || !targetUnit.IsAlive || effectToApply == null || targetUnit.Stats == null)
        {
            DebugHelper.LogWarning($"EffectSystem.ApplyEffect: Invalid target, effect, or target missing Stats. Target: {targetUnit?.unitName}, Effect: {effectToApply?.effectName}", targetUnit);
            return;
        }

        // TODO: Check for immunities based on effectToApply.effectTypeTags and targetUnit properties/effects

        ActiveStatusEffect existingEffect = targetUnit.Stats.ActiveEffects.FirstOrDefault(ae => ae.BaseEffect == effectToApply);

        if (existingEffect != null)
        {
            // Effect already exists, handle stacking behavior
            switch (effectToApply.stackingBehavior)
            {
                case EffectStackingBehavior.None:
                    // Typically, don't reapply or refresh unless new duration is longer (not implemented here for simplicity)
                    DebugHelper.Log($"Effect '{effectToApply.effectName}' (Stacking: None) already on {targetUnit.unitName}. No change.", targetUnit);
                    return; // Or refresh duration if new one is better, GDD dependent

                case EffectStackingBehavior.RefreshDuration:
                    existingEffect.RefreshDuration();
                    DebugHelper.Log($"Effect '{effectToApply.effectName}' on {targetUnit.unitName} duration refreshed to {existingEffect.RemainingDuration}.", targetUnit);
                    targetUnit.Stats.RecalculateAffectedStats(); // In case max stacks influences something indirectly
                    break;

                case EffectStackingBehavior.IncreaseStacks:
                    if (existingEffect.AddStack()) // AddStack handles refreshing duration
                    {
                        DebugHelper.Log($"Effect '{effectToApply.effectName}' on {targetUnit.unitName} stacked to {existingEffect.CurrentStacks}. Duration refreshed.", targetUnit);
                    }
                    else
                    {
                        // Already at max stacks, but AddStack would have refreshed duration if applicable
                        DebugHelper.Log($"Effect '{effectToApply.effectName}' on {targetUnit.unitName} at max stacks ({existingEffect.CurrentStacks}). Duration refreshed if applicable.", targetUnit);
                    }
                    targetUnit.Stats.RecalculateAffectedStats(); // Stats might change with stacks
                    break;

                case EffectStackingBehavior.AddNewInstance:
                    // This case means we don't care about 'existingEffect' for stacking, just add a new one.
                    // The 'existingEffect' variable might be misleading here if we truly add a new one.
                    // This behavior is rare and can lead to many instances of the same visual effect.
                    // For now, let's treat it like RefreshDuration to avoid complexity unless specified otherwise by GDD.
                    // If true AddNewInstance is needed, the check for existingEffect needs to be more nuanced
                    // or we allow multiple ActiveStatusEffect with the same BaseEffect SO.
                    ActiveStatusEffect newSeparateInstance = new ActiveStatusEffect(effectToApply, caster);
                    targetUnit.Stats.AddEffect(newSeparateInstance); // AddEffect in UnitStats handles the list
                    // RecalculateAffectedStats is called by UnitStats.AddEffect
                    break;

                default:
                    DebugHelper.LogWarning($"Unknown stacking behavior: {effectToApply.stackingBehavior} for effect {effectToApply.effectName}", effectToApply);
                    return;
            }
        }
        else
        {
            // Effect does not exist on target, add new instance
            ActiveStatusEffect newEffectInstance = new ActiveStatusEffect(effectToApply, caster);
            targetUnit.Stats.AddEffect(newEffectInstance); // AddEffect in UnitStats handles the list & recalculation
            // DebugHelper.Log already in UnitStats.AddEffect
        }
    }

    /// <summary>
    /// Removes a specific instance of an active effect from a unit.
    /// </summary>
    public void RemoveEffectInstance(Unit targetUnit, ActiveStatusEffect effectInstance)
    {
        if (targetUnit == null || effectInstance == null || targetUnit.Stats == null) return;
        targetUnit.Stats.RemoveEffect(effectInstance); // RemoveEffect in UnitStats handles the list & recalculation
    }

    /// <summary>
    /// Removes all stacks of a specific EffectSO from a unit.
    /// </summary>
    public void RemoveAllStacksOfEffect(Unit targetUnit, EffectSO effectSOToRemove)
    {
        if (targetUnit == null || effectSOToRemove == null || targetUnit.Stats == null) return;

        // Find all instances matching the EffectSO (though with current stacking, there's usually one)
        List<ActiveStatusEffect> effectsToRemove = targetUnit.Stats.ActiveEffects
                                                    .Where(ae => ae.BaseEffect == effectSOToRemove)
                                                    .ToList(); // ToList to avoid modification during iteration

        if (effectsToRemove.Count > 0)
        {
            foreach (var activeEffect in effectsToRemove)
            {
                targetUnit.Stats.RemoveEffect(activeEffect);
            }
            DebugHelper.Log($"All stacks of '{effectSOToRemove.effectName}' removed from {targetUnit.unitName}.", targetUnit);
            // RecalculateAffectedStats is called by UnitStats.RemoveEffect
        }
    }


    /// <summary>
    /// Ticks down durations for all effects on a unit. Removes expired effects.
    /// Should be called by TurnManager at an appropriate phase (e.g., end of unit's turn).
    /// </summary>
    public void TickUnitEffects(Unit unit)
    {
        if (unit == null || !unit.IsAlive || unit.Stats == null) return;

        List<ActiveStatusEffect> effectsToRemove = new List<ActiveStatusEffect>();
        bool statsChanged = false;

        // Iterate backwards if removing while iterating, or use a temporary list.
        // Using ToList() creates a copy to iterate over, allowing modification of the original list.
        foreach (ActiveStatusEffect effect in unit.Stats.ActiveEffects.ToList())
        {
            // TODO: Implement "OnTurnStart/OnTurnEnd" trigger processing here if the effect has such triggers
            // E.g., if (effect.BaseEffect.HasTrigger(EffectTriggerTime.EndOfTurn)) { ProcessEffectTrigger(unit, effect); }

            if (effect.TickDuration()) // TickDuration returns true if expired
            {
                effectsToRemove.Add(effect);
                statsChanged = true; // Assume stats might change if an effect expires
            }
        }

        foreach (ActiveStatusEffect expiredEffect in effectsToRemove)
        {
            unit.Stats.RemoveEffect(expiredEffect); // RemoveEffect in UnitStats logs and recalculates
        }

        if (statsChanged && effectsToRemove.Count == 0)
        {
            // This case can happen if an effect has a per-turn action but doesn't expire,
            // and that action modified stats. Or if an effect ticks but doesn't expire yet.
            // For now, RecalculateAffectedStats is primarily called on add/remove.
            // If per-tick effects directly modify base stats not covered by StatModifier system,
            // then a call here might be needed.
            // unit.Stats.RecalculateAffectedStats();
        }
    }
}