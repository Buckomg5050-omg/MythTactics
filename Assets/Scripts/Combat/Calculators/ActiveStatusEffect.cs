// ActiveStatusEffect.cs
using UnityEngine;

public class ActiveStatusEffect
{
    public EffectSO BaseEffect { get; private set; }
    public Unit Caster { get; private set; } // The unit that applied this effect instance, can be null

    public int RemainingDuration { get; set; } // In rounds/turns, if applicable
    public int CurrentStacks { get; set; }

    private bool _isPermanent;

    public ActiveStatusEffect(EffectSO baseEffect, Unit caster)
    {
        BaseEffect = baseEffect;
        Caster = caster;

        if (BaseEffect == null)
        {
            Debug.LogError("ActiveStatusEffect created with a null BaseEffect SO!");
            // Potentially throw an error or handle this to prevent further issues
            return;
        }

        CurrentStacks = 1; // Effects always start with at least 1 stack

        if (BaseEffect.durationType == EffectDurationType.Permanent)
        {
            _isPermanent = true;
            RemainingDuration = int.MaxValue; // Or a conventional very large number for "permanent"
        }
        else
        {
            _isPermanent = false;
            RemainingDuration = BaseEffect.duration;
        }
    }

    /// <summary>
    /// Ticks down the duration of the effect by one round.
    /// Should be called at the appropriate time (e.g., end of affected unit's turn).
    /// </summary>
    /// <returns>True if the effect has expired after this tick, false otherwise.</returns>
    public bool TickDuration()
    {
        if (_isPermanent)
        {
            return false; // Permanent effects don't expire by ticking
        }

        if (RemainingDuration > 0)
        {
            RemainingDuration--;
        }

        return RemainingDuration <= 0;
    }

    /// <summary>
    /// Refreshes the duration of this effect instance based on its EffectSO.
    /// </summary>
    public void RefreshDuration()
    {
        if (BaseEffect == null) return;

        if (BaseEffect.durationType == EffectDurationType.Rounds)
        {
            RemainingDuration = BaseEffect.duration;
            _isPermanent = false;
        }
        else if (BaseEffect.durationType == EffectDurationType.Permanent)
        {
            RemainingDuration = int.MaxValue;
            _isPermanent = true;
        }
        // Add other duration types if necessary
    }

    /// <summary>
    /// Attempts to add a stack to this effect instance, up to its MaxStacks.
    /// Also refreshes duration if appropriate based on stacking behavior.
    /// </summary>
    /// <returns>True if a stack was successfully added, false otherwise (e.g., already at max stacks).</returns>
    public bool AddStack()
    {
        if (BaseEffect == null) return false;

        if (CurrentStacks < BaseEffect.maxStacks)
        {
            CurrentStacks++;
            // Typically, adding a stack also refreshes duration
            if (BaseEffect.stackingBehavior == EffectStackingBehavior.IncreaseStacks ||
                BaseEffect.stackingBehavior == EffectStackingBehavior.RefreshDuration) // And other relevant behaviors
            {
                RefreshDuration();
            }
            return true;
        }
        else if (CurrentStacks >= BaseEffect.maxStacks &&
                 (BaseEffect.stackingBehavior == EffectStackingBehavior.IncreaseStacks ||
                  BaseEffect.stackingBehavior == EffectStackingBehavior.RefreshDuration))
        {
            // At max stacks, but still refresh duration if behavior allows
            RefreshDuration();
        }
        return false; // Could not add a new stack (was already at max for IncreaseStacks)
    }
}