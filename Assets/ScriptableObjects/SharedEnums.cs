// SharedEnums.cs

// This enum defines the different categories of abilities.
public enum AbilityType
{
    Skill,      // General learned techniques, martial arts, rogue tricks, etc.
    Spell,      // Magical incantations, arcane powers, divine blessings.
    Passive,    // Always active, not directly used (for future use).
    BasicAttack // A special type for standard weapon attacks if they are represented as AbilitySOs.
}

// Defines types of armor.
public enum ArmorType
{
    None,       // No armor / Clothing
    Light,      // e.g., Leather, Padded
    Medium,     // e.g., Chainmail, Scale
    Heavy,      // e.g., Plate
    Shield      // For off-hand defensive items
}

// Defines general categories of items.
public enum ItemType
{
    Consumable,   // e.g., Potions, Grenades, Shards
    Weapon,       // (Covered by WeaponSO, but ItemSO could represent a weapon in inventory)
    Armor,        // (Covered by ArmorSO, but ItemSO could represent armor in inventory)
    KeyItem,      // Quest items, unique progression items
    Miscellaneous // Junk, crafting materials, currency (if not a separate system)
}

// Defines types of weapons.
public enum WeaponType
{
    Unarmed,    // Default or for fist-fighters
    Sword,
    Axe,
    Mace,
    Dagger,
    Spear,
    Bow,
    Crossbow,
    Staff,      // Can be for melee or magical implements
    Wand,       // Typically magical
    Shield,     // While primarily defensive, some shields can be used offensively or grant stats/abilities
    Tome,       // Magical implement
    Instrument  // For bard-like classes
}

// Defines the metaphysical or elemental leaning of a unit or ability.
public enum AlignmentType
{
    None,       // No specific alignment / Unaligned
    Light,
    Dark
}

// Defines the allegiance or team of a unit.
public enum FactionType
{
    Player,     // Controlled by the human player
    Enemy,      // Hostile to Player faction
    Ally,       // AI controlled, friendly to Player faction
    Neutral     // Not initially aligned with/against anyone
}

// Defines different types of terrain on the map.
public enum TerrainType
{
    Plains,
    Forest,
    Hills,
    RockyGround,
    Swamp,
    ShallowWater,
    MountainPeak,
    DeepWater,
    Boundary
}

// Add other global enums here in the future.