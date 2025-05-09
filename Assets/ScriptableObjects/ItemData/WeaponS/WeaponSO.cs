// WeaponSO.cs
// Located in: Assets/Scripts/ScriptableObjects/Items/
using UnityEngine;
using System.Collections.Generic; // Added for List if StatModifier or AbilitySO were used

[CreateAssetMenu(fileName = "NewWeapon", menuName = "MythTactics/Items/Weapon")]
public class WeaponSO : ScriptableObject // Or potentially an ItemSO base class later
{
    [Header("Weapon Stats")]
    [Tooltip("The base damage of this weapon before attribute modifiers.")]
    public int baseDamage = 5;

    [Tooltip("The attack range of this weapon. Default is 1 for melee. Overrides class default if > 0.")]
    public int range = 1;

    [Tooltip("The base accuracy of this weapon (0-100). Used in hit chance calculation.")]
    public int baseAccuracy = 75; // NEW FIELD

    // Future properties for a weapon:
    // public string weaponName = "Generic Weapon";
    // public Sprite icon;
    // public enum WeaponType { Sword, Axe, Bow, Staff, Unarmed } // For skill compatibility, animations etc.
    // public WeaponType weaponType = WeaponType.Sword;
    // public List<StatModifier> statModifiers; // e.g., +2 Core, -1 Speed
    // public AbilitySO grantedAbility; // e.g., a special attack
}