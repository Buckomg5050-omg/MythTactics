// StatModifier.cs (or a shared combat data file)
using UnityEngine;

public enum StatType
{
    // Primary Attributes (from GDD 2.1)
    Core,
    Echo,
    Pulse,
    Spark,
    Glimmer,
    Aura,

    // Max Resources (from GDD 2.3)
    MaxVitalityPoints,
    MaxManaPoints,
    MaxStaminaPoints,
    MaxFocusPoints,
    MaxInfluencePoints,

    // Derived Combat Stats (examples, expand based on GDD & future needs)
    PhysicalAttackDamageBonus, // GDD 2.3
    MagicalPotencyBonus,       // GDD 2.3
    MovementRange,             // GDD 1.2, 2.3
    EffectiveSpeed,            // GDD 1.1
    ActionCounterRate,

    // Hit Chance / Evasion Components (from GDD 2.3)
    WeaponBaseAccuracy,
    ArmorBaseEvasion,
    OverallHitChanceModifier,
    OverallEvasionModifier,
    TemporaryEvasionBonus,      // MODIFIED: Added for status effect based evasion bonuses

    // Critical Hit Components (from GDD 3.2)
    PhysicalCritChance,
    MagicalCritChance,
    CriticalDamageMultiplier,

    // Damage Mitigations / Resistances (from GDD 3.2, 7.1.2)
    PhysicalDamageReductionPercent, // PDR%
    MagicalDamageReductionPercent,
    FireResistancePercent,
    ColdResistancePercent,
    LightningResistancePercent,

    // Regen Rates (from UnitStats)
    VitalityRegenRate,
    ManaRegenRate,
    StaminaRegenRate,
    FocusRegenRate,
    InfluenceRegenRate,

    // Action Point related
    MaxActionPoints,
    APRegenRate,
}

public enum ModifierType
{
    Flat,
    PercentAdd,
    PercentMult,
}

[System.Serializable]
public struct StatModifier
{
    public StatType stat;
    public ModifierType type;
    public float value;

    public StatModifier(StatType stat, ModifierType type, float value)
    {
        this.stat = stat;
        this.type = type;
        this.value = value;
    }
}