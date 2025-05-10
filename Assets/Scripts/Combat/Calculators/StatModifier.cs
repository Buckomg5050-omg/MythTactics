// StatModifier.cs (or a shared combat data file)
using UnityEngine; // Required if you add [System.Serializable] to structs for Inspector viewing, not strictly needed for pure data.

// Enum for the type of stats that can be modified.
// This should cover primary attributes, derived stats, resistances, etc.
// Add to this as your game develops more modifiable stats.
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
    EffectiveSpeed,            // GDD 1.1 (this one might be tricky if it's re-calculated often)
    ActionCounterRate,         // Alternative way to modify speed directly

    // Hit Chance / Evasion Components (from GDD 2.3)
    WeaponBaseAccuracy,        // Modifies the weapon's contribution
    ArmorBaseEvasion,          // Modifies the armor's contribution
    OverallHitChanceModifier,  // A direct +/- to final Hit%
    OverallEvasionModifier,    // A direct +/- to final Evasion%

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
    // Add more resistances/vulnerabilities as needed

    // Regen Rates (from UnitStats)
    VitalityRegenRate,
    ManaRegenRate,
    StaminaRegenRate,
    FocusRegenRate,
    InfluenceRegenRate,

    // Action Point related
    MaxActionPoints,
    APRegenRate, // If you want to modify AP regen beyond full
}

// Enum for how the modifier is applied.
public enum ModifierType
{
    Flat,          // Adds a flat value (e.g., +5 Core)
    PercentAdd,    // Adds a percentage of the base value (e.g., +10% MaxVP based on original MaxVP). Multiple PercentAdd are additive.
    PercentMult,   // Multiplies the current value by a percentage (e.g., 1.2x for +20%). Multiple PercentMult are multiplicative.
    // Override      // Sets the stat to a specific value (less common for temporary effects)
}

[System.Serializable] // Makes it show up in the Inspector list within EffectSO
public struct StatModifier
{
    public StatType stat;
    public ModifierType type;
    public float value;
    // public int order; // Optional: For controlling order of operations if multiple modifiers of same type apply
    // public object source; // Optional: To track who/what applied this modifier (e.g., the EffectSO itself)

    public StatModifier(StatType stat, ModifierType type, float value)
    {
        this.stat = stat;
        this.type = type;
        this.value = value;
    }
}