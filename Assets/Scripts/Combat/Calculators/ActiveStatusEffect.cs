// ActiveStatusEffect.cs
using UnityEngine;
using MythTactics.Combat; // For StatType, if not globally accessible

public class ActiveStatusEffect
{
    public EffectSO BaseEffect { get; private set; }
    public Unit Caster { get; private set; }

    public int RemainingDuration { get; set; }
    public int CurrentStacks { get; set; }

    private bool _isPermanent;
    private int _snapshottedCasterScalingStatValue = 0; // To store the caster's stat value for scaling tick actions

    public ActiveStatusEffect(EffectSO baseEffect, Unit caster)
    {
        BaseEffect = baseEffect;
        Caster = caster;

        if (BaseEffect == null)
        {
            Debug.LogError("ActiveStatusEffect created with a null BaseEffect SO!");
            return;
        }

        CurrentStacks = 1;

        if (BaseEffect.durationType == EffectDurationType.Permanent)
        {
            _isPermanent = true;
            RemainingDuration = int.MaxValue;
        }
        else
        {
            _isPermanent = false;
            RemainingDuration = BaseEffect.duration;
        }

        // --- NEW: Snapshot caster's stat for scaling tick actions ---
        if (BaseEffect.tickActionScalesWithCasterStat && Caster != null && Caster.Stats != null)
        {
            // Get the caster's *effective* primary attribute value for the specified scaling stat
            UnitPrimaryAttributes casterEffectiveAttrs = Caster.Stats.EffectiveAttributes;
            switch (BaseEffect.tickScalingStat)
            {
                case StatType.Core:
                    _snapshottedCasterScalingStatValue = casterEffectiveAttrs.Core;
                    break;
                case StatType.Echo:
                    _snapshottedCasterScalingStatValue = casterEffectiveAttrs.Echo;
                    break;
                case StatType.Pulse:
                    _snapshottedCasterScalingStatValue = casterEffectiveAttrs.Pulse;
                    break;
                case StatType.Spark:
                    _snapshottedCasterScalingStatValue = casterEffectiveAttrs.Spark;
                    break;
                case StatType.Glimmer:
                    _snapshottedCasterScalingStatValue = casterEffectiveAttrs.Glimmer;
                    break;
                case StatType.Aura:
                    _snapshottedCasterScalingStatValue = casterEffectiveAttrs.Aura;
                    break;
                default:
                    Debug.LogWarning($"EffectSO '{BaseEffect.effectName}' is set to scale with an unsupported or non-primary StatType '{BaseEffect.tickScalingStat}'. Defaulting snapshot to 0.", BaseEffect);
                    _snapshottedCasterScalingStatValue = 0;
                    break;
            }
            DebugHelper.Log($"Effect '{BaseEffect.effectName}' snapshotted caster ({Caster.unitName}) {BaseEffect.tickScalingStat}: {_snapshottedCasterScalingStatValue}", Caster);
        }
        else if (BaseEffect.tickActionScalesWithCasterStat)
        {
            Debug.LogWarning($"EffectSO '{BaseEffect.effectName}' is set to scale, but caster or caster.Stats is null. Snapshotting 0.", BaseEffect);
             _snapshottedCasterScalingStatValue = 0;
        }
    }

    public bool TickDuration()
    {
        if (_isPermanent) return false;
        if (RemainingDuration > 0) RemainingDuration--;
        return RemainingDuration <= 0;
    }

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
    }

    public bool AddStack()
    {
        if (BaseEffect == null) return false;
        bool stacked = false;
        if (CurrentStacks < BaseEffect.maxStacks)
        {
            CurrentStacks++;
            stacked = true;
        }

        // Always refresh duration when stacking behavior involves increasing or refreshing
        if (BaseEffect.stackingBehavior == EffectStackingBehavior.IncreaseStacks ||
            BaseEffect.stackingBehavior == EffectStackingBehavior.RefreshDuration)
        {
            RefreshDuration();
        }
        return stacked; // Return true if a new stack was actually added
    }

    // --- NEW: Getter for the snapshotted stat value ---
    public int GetSnapshottedCasterScalingStatValue()
    {
        return _snapshottedCasterScalingStatValue;
    }
}