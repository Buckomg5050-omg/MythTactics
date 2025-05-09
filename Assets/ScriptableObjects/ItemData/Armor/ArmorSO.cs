// ArmorSO.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewArmor", menuName = "MythTactics/Items/Armor")]
public class ArmorSO : ScriptableObject // Could inherit from a base ItemSO or EquipmentSO later
{
    [Header("Armor Stats")]
    [Tooltip("The name of this armor piece.")]
    public string armorName = "Generic Armor";

    [Tooltip("The base armor value used for PDR calculation.")]
    public int armorValue = 10;

    // Future properties for armor:
    // public Sprite icon;
    // public enum ArmorSlot { Body, Head, Shield }
    // public ArmorSlot slot;
    // public List<StatModifier> statModifiers; // e.g., -1 Speed, +5 MaxVP
    // public int weight;
    // public int magicalResistance; // etc.
}