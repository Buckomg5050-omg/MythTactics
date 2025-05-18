// WeaponSO.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "MythTactics/Items/Weapon")]
public class WeaponSO : ScriptableObject
{
    [Header("== IDENTIFICATION & LORE ==")] // MODIFIED: Header
    [Tooltip("The in-game display name of this weapon.")]
    public string weaponName = "Generic Weapon";

    [Tooltip("Icon for UI representation of the weapon.")]
    public Sprite icon;

    [Tooltip("A short mechanical description of the weapon's primary function or key stats. Displayed in tooltips or brief summaries.")]
    [TextArea(2, 4)] // MODIFIED: Adjusted TextArea size
    public string shortDescription = "A standard weapon.";

    [Tooltip("Longer flavor text or lore associated with this weapon. Can be shown in detailed item views.")]
    [TextArea(4, 8)] // MODIFIED: Added Lore Text, larger TextArea
    public string loreText = "Many such weapons exist, each with its own minor history.";


    [Header("== TYPE & CATEGORY ==")] // MODIFIED: New Header
    [Tooltip("The general category of this weapon. Can influence animations, proficiency, skills.")]
    public WeaponType weaponType = WeaponType.Sword;


    [Header("== CORE COMBAT STATS ==")]
    [Tooltip("The base damage of this weapon before attribute modifiers.")]
    public int baseDamage = 5;

    [Tooltip("The attack range of this weapon in tiles. Overrides class default attack range if this value is > 0.")]
    public int range = 1;

    [Tooltip("The base accuracy of this weapon (0-100). Used in hit chance calculation.")]
    [Range(0, 100)]
    public int baseAccuracy = 75;


    [Header("== DAMAGE PROPERTIES ==")]
    [Tooltip("If true, this weapon's damage bypasses defender's PDR/resistances (True Damage).")]
    public bool dealsTrueDamage = false;


    [Header("== ADDITIONAL EFFECTS & MODIFIERS ==")]
    [Tooltip("Stat modifiers passively granted when this weapon is equipped (e.g., +1 Core, -5 Speed).")]
    public List<StatModifier> statModifiers = new List<StatModifier>();

    [Tooltip("An ability that is granted to the wielder when this weapon is equipped. (Optional)")]
    public AbilitySO grantedAbility;

    // Future:
    // public int handsRequired = 1; // 1-handed, 2-handed
    // public List<DamageType> alternateDamageTypes; // e.g. a flaming sword deals Physical and Fire
    // public SoundEffectSO attackSound;
    // public GameObject attackVFX;
    // public int itemLevelRequirement; // Level needed to equip
    // public List<ClassDataSO> classRestrictions; // Classes that can/cannot use it
}