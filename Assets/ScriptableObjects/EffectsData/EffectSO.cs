// EffectSO.cs
using UnityEngine;
using System.Collections.Generic;
using MythTactics.Combat;

public enum EffectDurationType
{
    Rounds,     // Ticks down per round
    Permanent,
    Instant     // MODIFIED: Added Instant type
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

// MODIFIED: New enum for direct on-apply effects
public enum DirectEffectType
{
    None,
    HealVitality,
    RestoreMana,
    DamageVitality // For instant damage like a grenade, if needed later
    // Add more as needed: RestoreStamina, RestoreFocus, etc.
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
    [Tooltip("Number of rounds/turns the effect lasts. Not used if durationType is Permanent or Instant.")] // MODIFIED Tooltip
    public int duration = 3;
    [Tooltip("Maximum number of times this effect can stack on a single unit.")]
    public int maxStacks = 1;
    public EffectStackingBehavior stackingBehavior = EffectStackingBehavior.RefreshDuration;

    [Header("Core Mechanics (Stat Modifiers - for Persistent Effects)")] // MODIFIED Header
    [Tooltip("Modifiers to primary or derived stats. Primarily for effects with duration.")]
    public List<StatModifier> statModifiers;

    [Tooltip("Tags to categorize the effect (e.g., Buff, Debuff, Poison, Magic, Physical).")]
    public List<string> effectTypeTags;

    // --- NEW: On Apply Direct Effect Properties ---
    [Header("On Apply Direct Effect (for Instant or Initial Impact)")]
    [Tooltip("The direct type of effect to apply immediately when this EffectSO is applied.")]
    public DirectEffectType onApplyDirectEffectType = DirectEffectType.None;

    [Tooltip("Base power for the direct on-apply effect (e.g., heal amount, mana restore amount).")]
    public int onApplyDirectEffectBasePower = 0;

    [Tooltip("If true, the on-apply direct effect's power will scale with a stat from the caster.")]
    public bool onApplyDirectEffectScalesWithCasterStat = false;

    [Tooltip("The primary attribute of the caster that this on-apply direct effect scales with. Only used if scalesWithCasterStat is true.")]
    public StatType onApplyDirectEffectScalingStat = StatType.Spark;

    [Tooltip("The factor by which the caster's stat contributes to the on-apply direct effect power. Only used if scalesWithCasterStat is true.")]
    public float onApplyDirectEffectScalingFactor = 0.25f;

    [Tooltip("DamageType for direct on-apply effect, if type is DamageVitality.")]
    public DamageType onApplyDirectEffectDamageType = DamageType.True;


    [Header("Tick Action (DoT/HoT - for Effects with Duration)")] // MODIFIED Header
    [Tooltip("Action to perform each time the effect ticks (usually at start/end of affected unit's turn). Only for effects with duration.")]
    public EffectTickActionType tickActionType = EffectTickActionType.None;

    [Tooltip("Base power for the tick action (e.g., damage amount or heal amount per tick).")]
    public int tickActionBasePower = 0;

    [Tooltip("DamageType for tick action, if tickActionType is Damage.")]
    public DamageType tickActionDamageType = DamageType.True;

    [Header("Tick Action Scaling")]
    [Tooltip("If true, the tick action's power will scale with a stat from the caster.")]
    public bool tickActionScalesWithCasterStat = false;

    [Tooltip("The primary attribute of the caster that this tick action scales with. Only used if tickActionScalesWithCasterStat is true.")]
    public StatType tickScalingStat = StatType.Spark;

    [Tooltip("The factor by which the caster's stat contributes to the tick power. Only used if tickActionScalesWithCasterStat is true.")]
    public float tickScalingFactor = 0.25f;
}