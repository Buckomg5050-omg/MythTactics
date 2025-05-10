// AbilitySO.cs
using UnityEngine;
using MythTactics.Combat;
using System.Collections.Generic; // Added for List<EffectSO>

/// <summary>
/// ScriptableObject defining the properties of an ability.
/// </summary>
[CreateAssetMenu(fileName = "NewAbility", menuName = "MythTactics/Ability")]
public class AbilitySO : ScriptableObject
{
    [Header("General Info")]
    [Tooltip("The display name of the ability.")]
    public string abilityName = "New Ability";

    [Tooltip("A description of what the ability does.")]
    [TextArea(3, 5)]
    public string description = "Ability Description.";

    // public Sprite abilityIcon;

    [Header("Costs")]
    [Tooltip("Action Points (AP) required to use this ability.")]
    public int apCost = 1;

    [Tooltip("Mana Points (MP) required to use this ability.")]
    public int mpCost = 0;

    [Tooltip("Stamina Points (SP) required to use this ability.")]
    public int spCost = 0;

    [Tooltip("Focus Points (FP) required to use this ability.")]
    public int fpCost = 0;

    [Tooltip("Influence Points (IP) required to use this ability.")]
    public int ipCost = 0;

    [Header("Targeting")]
    [Tooltip("Who or what this ability can target.")]
    public AbilityTargetType targetType = AbilityTargetType.EnemyUnit;

    [Tooltip("The range of the ability in tiles. 0 for self-cast, 1 for adjacent, etc.")]
    public int range = 1;

    [Tooltip("Base accuracy of the ability (0-100). Used in hit chance calculation if applicable.")]
    [Range(0, 100)]
    public int baseAccuracy = 80;

    [Header("Primary Effect")] // Renamed "Effects" to "Primary Effect" for clarity
    [Tooltip("The primary type of effect this ability produces (e.g., Damage, Heal).")]
    public AbilityEffectType effectType = AbilityEffectType.Damage;

    [Tooltip("The base power of the ability (e.g., base damage for a damage spell, base heal amount). Modifiers may apply.")]
    public int basePower = 10;

    [Tooltip("The type of damage dealt by this ability, if applicable (e.g., Physical, Magical, Fire).")]
    public DamageType damageType = DamageType.Magical;

    [Tooltip("If true, this ability's damage bypasses most resistances and mitigations.")]
    public bool dealsTrueDamage = false;

    // NEW: Section for applying status effects
    [Header("Applied Status Effects")]
    [Tooltip("List of status effects this ability applies to the target(s) on successful use.")]
    public List<EffectSO> effectsToApplyOnHit; // Or OnUse for self/ally buffs

    // [Tooltip("Chance to apply each effect (0-100). If not set or list count mismatch, assumes 100% for listed effects.")]
    // public List<int> effectApplicationChances; // Optional: for more complex proc chances per effect
}