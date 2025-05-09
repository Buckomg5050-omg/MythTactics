// WeaponSO.cs
// Located in: Assets/Scripts/ScriptableObjects/Items/
using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "MythTactics/Items/Weapon")]
public class WeaponSO : ScriptableObject // Or potentially an ItemSO base class later
{
    [Header("Weapon Stats")]
    [Tooltip("The base damage of this weapon before attribute modifiers.")]
    public int baseDamage = 5;

    // Future properties for a weapon:
    // public string weaponName = "Generic Weapon";
    // public Sprite icon;
    // public int range = 1; // If weapon dictates range over class default
    // public enum WeaponType { Sword, Axe, Bow, Staff, Unarmed } // For skill compatibility, animations etc.
    // public WeaponType weaponType = WeaponType.Sword;
    // public int baseAccuracy = 75;
    // public List<StatModifier> statModifiers; // e.g., +2 Core, -1 Speed
    // public AbilitySO grantedAbility; // e.g., a special attack
}