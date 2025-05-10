// EffectSystem.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MythTactics.Combat; // For DamageType, ensure this namespace has it or adjust

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
                    if (existingEffect.AddStack())
                    {
                        DebugHelper.Log($"Effect '{effectToApply.effectName}' on {targetUnit.unitName} stacked to {existingEffect.CurrentStacks}. Duration refreshed.", targetUnit);
                    }
                    else
                    {
                        DebugHelper.Log($"Effect '{effectToApply.effectName}' on {targetUnit.unitName} at max stacks ({existingEffect.CurrentStacks}). Duration refreshed if applicable.", targetUnit);
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
        // Iterate over a copy of the list in case effects are removed during processing (e.g., dispel on tick)
        // Though for simple DoT/HoT and duration ticking, direct iteration and adding to effectsToExpire is fine.

        foreach (ActiveStatusEffect effect in unit.Stats.ActiveEffects.ToList()) // ToList() for safe iteration if modification occurs
        {
            if (!unit.IsAlive) break; // Unit might die from a previous tick effect in the same frame

            EffectSO baseEffect = effect.BaseEffect;
            if (baseEffect == null) continue;

            // --- Process Tick Action (DoT/HoT) ---
            if (baseEffect.tickActionType != EffectTickActionType.None)
            {
                // Determine caster for potency scaling, if applicable in the future. For now, use effect's base power.
                // Unit caster = effect.Caster; 
                // For now, tick damage/heal doesn't scale with caster stats, just uses baseEffect.tickActionBasePower.

                int powerPerStack = baseEffect.tickActionBasePower; // Assuming base power is per stack
                int totalTickPower = powerPerStack * effect.CurrentStacks;


                if (baseEffect.tickActionType == EffectTickActionType.Damage)
                {
                    if (totalTickPower > 0) // Only apply if there's actual power
                    {
                        DebugHelper.Log($"{unit.unitName} takes {totalTickPower} {baseEffect.tickActionDamageType} damage from '{baseEffect.effectName}' tick.", unit);
                        // For simplicity, directly modify vitality.
                        // A more robust system might use DamageCalculator or a dedicated method in UnitCombat.
                        // For now, let's assume tick damage is direct and unmitigated unless type is Physical/Magical and handled by TakeDamage.
                        // If tickActionDamageType is True or Poison (and Poison is treated like True), direct modification is okay.
                        // If it were Physical/Magical, it should go through TakeDamage.
                        if (baseEffect.tickActionDamageType == DamageType.True || baseEffect.tickActionDamageType == DamageType.Poison)
                        {
                            unit.Stats.ModifyVitality(-totalTickPower); // Negative for damage
                            if (!unit.IsAlive)
                            {
                                DebugHelper.Log($"{unit.unitName} defeated by tick damage from '{baseEffect.effectName}'.", unit);
                                // If unit dies from DoT, subsequent effects in this loop for this unit might not process if we break.
                                // Or, handle death explicitly (e.g., TurnManager might unregister, PIH might change state).
                                // For now, we'll let the loop continue for other effects, then check IsAlive again.
                            }
                        }
                        else
                        {
                            // If it's meant to be mitigated damage:
                            // This would require a simpler version of PerformAttack/PerformAbility or a direct call to TakeDamage.
                            // Let's assume for now tick damage specified as Physical/Magical still goes via TakeDamage for mitigation.
                            // unit.Combat.TakeDamage(totalTickPower, baseEffect.tickActionDamageType, effect.Caster); // Hypothetical
                            DebugHelper.LogWarning($"Tick damage type {baseEffect.tickActionDamageType} for '{baseEffect.effectName}' not fully handled with mitigation yet. Applying as unmitigated for now.", unit);
                            unit.Stats.ModifyVitality(-totalTickPower);
                        }
                    }
                }
                else if (baseEffect.tickActionType == EffectTickActionType.Heal)
                {
                    if (totalTickPower > 0)
                    {
                        DebugHelper.Log($"{unit.unitName} heals {totalTickPower} VP from '{baseEffect.effectName}' tick.", unit);
                        unit.Stats.ModifyVitality(totalTickPower); // Positive for healing
                    }
                }
            }

            // --- Tick Duration ---
            if (effect.TickDuration()) // TickDuration returns true if expired
            {
                effectsToExpire.Add(effect);
            }
        }

        // Remove expired effects
        if (effectsToExpire.Count > 0)
        {
            foreach (ActiveStatusEffect expiredEffect in effectsToExpire)
            {
                unit.Stats.RemoveEffect(expiredEffect); // This calls RecalculateAffectedStats
            }
        }
        // If no effects expired but tick actions occurred, stats (like current HP) might have changed.
        // RecalculateAffectedStats in UnitStats primarily deals with max values based on StatModifiers.
        // Current HP changes are handled by ModifyVitality directly.
        // If a tick action *itself* applied a new stat-modifying effect, AddEffect would handle recalculation.
    }
}