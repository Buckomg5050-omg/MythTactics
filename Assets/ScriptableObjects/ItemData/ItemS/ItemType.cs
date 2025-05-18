// ItemType.cs

public enum ItemType
{
    Consumable,   // e.g., Potions, Grenades, Shards
    Weapon,       // (Covered by WeaponSO, but ItemSO could represent a weapon in inventory)
    Armor,        // (Covered by ArmorSO, but ItemSO could represent armor in inventory)
    KeyItem,      // Quest items, unique progression items
    Miscellaneous // Junk, crafting materials, currency (if not a separate system)
    // Add more as needed, e.g., Accessory, Reagent
}