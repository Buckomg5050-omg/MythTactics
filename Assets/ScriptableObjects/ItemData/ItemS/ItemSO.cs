// ItemSO.cs
using UnityEngine;
// Make sure EffectSO is accessible
// Make sure ItemType enum is accessible (it will be if ItemType.cs is created as above)

[CreateAssetMenu(fileName = "NewItem", menuName = "MythTactics/Items/Generic Item")]
public class ItemSO : ScriptableObject
{
    [Header("== IDENTIFICATION & LORE ==")]
    [Tooltip("The in-game display name of this item.")]
    public string itemName = "New Item";

    [Tooltip("Icon for UI representation of the item.")]
    public Sprite itemIcon;

    [Tooltip("A short mechanical description of what the item does when used or its passive benefits if equippable (though dedicated equipment SOs are preferred for equip).")]
    [TextArea(2, 4)]
    public string shortDescription = "A useful item.";

    [Tooltip("Longer flavor text or lore associated with this item.")]
    [TextArea(4, 8)]
    public string loreText = "Its origins are mundane, yet its utility is undeniable.";

    [Header("== TYPE & USAGE ==")]
    [Tooltip("The general category of this item.")]
    public ItemType itemType = ItemType.Consumable; // This line was causing the error

    [Tooltip("Can this item be used directly by a unit during combat? (Primarily for consumables or activatable items).")]
    public bool isUsableInCombat = true;

    [Tooltip("AP cost to use this item in combat. Relevant if isUsableInCombat is true.")]
    public int apCostToUse = 1;

    [Tooltip("If true, this item will be removed from the unit's inventory after use. Primarily for consumables.")]
    public bool consumeOnUse = true;


    [Header("== APPLIED EFFECT (If Usable) ==")]
    [Tooltip("The primary effect this item applies when used (e.g., heal, buff). Assign an EffectSO here. Primarily for consumables.")]
    public EffectSO effectToApplyOnUse;
}