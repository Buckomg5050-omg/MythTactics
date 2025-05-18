// ArmorSO.cs
using UnityEngine;
using System.Collections.Generic; // Required for List
// Make sure ArmorType.cs is accessible (global or with 'using')
// Make sure StatModifier struct is accessible

[CreateAssetMenu(fileName = "NewArmor", menuName = "MythTactics/Items/Armor")]
public class ArmorSO : ScriptableObject
{
    [Header("== IDENTIFICATION & LORE ==")]
    [Tooltip("The in-game display name of this armor piece.")]
    public string armorName = "Generic Armor";

    [Tooltip("Icon for UI representation of the armor.")]
    public Sprite icon; // MODIFIED: Added

    [Tooltip("A short mechanical description of the armor's primary function or key stats.")]
    [TextArea(2, 4)]
    public string shortDescription = "Standard protection."; // MODIFIED: Added

    [Tooltip("Longer flavor text or lore associated with this armor piece.")]
    [TextArea(4, 8)]
    public string loreText = "Commonly crafted, offering basic defense."; // MODIFIED: Added


    [Header("== TYPE & CATEGORY ==")]
    [Tooltip("The general category of this armor. Can influence movement penalties, proficiencies, etc.")]
    public ArmorType armorType = ArmorType.Light; // MODIFIED: Added
    // public enum ArmorSlot { Body, Head, Feet, Hands, Accessory } // For future equipment slot system
    // public ArmorSlot slot = ArmorSlot.Body;


    [Header("== DEFENSIVE STATS ==")]
    [Tooltip("The base physical armor value used for Physical Damage Reduction (PDR) calculation.")]
    public int armorValue = 10; // Physical defense

    [Tooltip("Base evasion bonus granted by this armor. Affects physical hit chance against the wearer.")]
    public int baseEvasion = 0;

    // Future: public int magicResistance = 0; // Flat magic damage reduction or %


    [Header("== ADDITIONAL MODIFIERS ==")]
    [Tooltip("Stat modifiers passively granted when this armor is equipped (e.g., -1 EffectiveSpeed, +5 MaxVitalityPoints).")]
    public List<StatModifier> statModifiers = new List<StatModifier>(); // MODIFIED: Added

    // Future:
    // public int weight; // Could affect speed or stamina consumption
    // public List<DamageTypeResistance> resistances; // e.g., struct { DamageType type; float percentResistance; }
    // public int itemLevelRequirement;
    // public List<ClassDataSO> classRestrictions;
}