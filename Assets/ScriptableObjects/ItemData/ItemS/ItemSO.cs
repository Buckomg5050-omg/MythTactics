using UnityEngine;
// If EffectSO is in a namespace, you might need: using YourNamespace.Effects;

// Enum to define different types of items
public enum ItemType
{
    Consumable,   // e.g., Potions, Grenades
    Weapon,       // For equipping, like your WeaponSO
    Armor,        // For equipping, like your ArmorSO
    KeyItem,      // Quest items, etc.
    Miscellaneous // Junk, crafting materials later, etc.
}

[CreateAssetMenu(fileName = "NewItem", menuName = "MythTactics/Items/Generic Item")]
public class ItemSO : ScriptableObject
{
    [Header("Basic Information")]
    public string itemName = "New Item";
    public Sprite itemIcon; // Assign this in the Inspector for each item
    [TextArea(3, 5)]
    public string description = "Item description.";
    public ItemType itemType = ItemType.Consumable;

    [Header("Combat Usage (Primarily for Consumables)")]
    [Tooltip("Can this item be used directly by a unit during combat?")]
    public bool isUsableInCombat = true;
    [Tooltip("AP cost to use this item in combat. Relevant if isUsableInCombat is true.")]
    public int apCostToUse = 1; // GDD: "Use Standard Item: 1 AP"
    [Tooltip("If true, this item will be removed from the unit's inventory after use. Primarily for consumables.")] // MODIFIED: Added Tooltip
    public bool consumeOnUse = true; // MODIFIED: Added this field, default true for consumables

    [Header("Effects")]
    [Tooltip("The primary effect this item applies when used (e.g., heal, buff). Assign an EffectSO here.")]
    public EffectSO effectToApplyOnUse;

    // Future considerations we can add later:
    // public int maxStackSize = 99; // For stackable consumables
    // public int value; // Gold value for buying/selling
    // public AbilitySO grantsAbility; // For items that grant a temporary or permanent ability
}