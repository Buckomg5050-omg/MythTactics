// EffectSO.cs
using UnityEngine;
using System.Collections.Generic;
using MythTactics.Combat;

public enum EffectDurationType
{
    Rounds,
    Permanent,
}

public enum EffectStackingBehavior
{
    None,
    RefreshDuration,
    AddNewInstance,
    IncreaseStacks,
}

public enum EffectTickActionType
{
    None,
    Damage,
    Heal
}

[CreateAssetMenu(fileName = "NewEffect", menuName = "MythTactics/Effect")]
public class EffectSO : ScriptableObject
{
    [Header("Identification")]
    public string effectID;
    public string effectName = "New Effect";
    [TextArea(2, 4)]
    public string description = "Effect description.";
    public Sprite icon;

    [Header("Duration & Stacking")]
    public EffectDurationType durationType = EffectDurationType.Rounds;
    [Tooltip("Number of rounds/turns the effect lasts. Not used if durationType is Permanent.")]
    public int duration = 3;
    [Tooltip("Maximum number of times this effect can stack on a single unit.")]
    public int maxStacks = 1;
    public EffectStackingBehavior stackingBehavior = EffectStackingBehavior.RefreshDuration;

    [Header("Core Mechanics")]
    [Tooltip("Modifiers to primary or derived stats.")]
    public List<StatModifier> statModifiers;

    [Tooltip("Tags to categorize the effect (e.g., Buff, Debuff, Poison, Magic, Physical).")]
    public List<string> effectTypeTags;

    [Header("Tick Action (DoT/HoT)")]
    [Tooltip("Action to perform each time the effect ticks (usually at start/end of affected unit's turn).")]
    public EffectTickActionType tickActionType = EffectTickActionType.None;

    [Tooltip("Base power for the tick action (e.g., damage amount or heal amount per tick).")]
    public int tickActionBasePower = 0;

    [Tooltip("DamageType for tick action, if tickActionType is Damage.")]
    public DamageType tickActionDamageType = DamageType.True;

    // --- NEW: Tick Action Scaling Properties ---
    [Header("Tick Action Scaling")]
    [Tooltip("If true, the tick action's power will scale with a stat from the caster.")]
    public bool tickActionScalesWithCasterStat = false;

    [Tooltip("The primary attribute of the caster that this tick action scales with (e.g., Spark for magical DoTs, Core for physical DoTs). Only used if tickActionScalesWithCasterStat is true.")]
    public StatType tickScalingStat = StatType.Spark; // Default to Spark, designer can change per effect

    [Tooltip("The factor by which the caster's stat contributes to the tick power (e.g., 0.25 for Stat/4). Only used if tickActionScalesWithCasterStat is true.")]
    public float tickScalingFactor = 0.25f; // e.g., GDD's Floor(Stat / 4) implies a factor of 0.25f
}