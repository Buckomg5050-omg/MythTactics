// Full content for Assets/Scripts/Abilities/AbilitySO.cs

using UnityEngine;
using MythTactics.Combat; // This should now work correctly

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

    // public Sprite abilityIcon; // Placeholder for UI icon

    [Header("Costs")]
    [Tooltip("Action Points (AP) required to use this ability.")]
    public int apCost = 1; // GDD 3.1: Basic Skill/Spell: 1 AP

    [Tooltip("Mana Points (MP) required to use this ability.")]
    public int mpCost = 0;

    [Tooltip("Stamina Points (SP) required to use this ability.")]
    public int spCost = 0;

    [Header("Targeting")]
    [Tooltip("Who or what this ability can target.")]
    public AbilityTargetType targetType = AbilityTargetType.EnemyUnit; // Uses enum from MythTactics.Combat

    [Tooltip("The range of the ability in tiles. 0 for self-cast, 1 for adjacent, etc.")]
    public int range = 1;

    [Header("Effects")]
    [Tooltip("The primary type of effect this ability produces (e.g., Damage, Heal).")]
    public AbilityEffectType effectType = AbilityEffectType.Damage; // Uses enum from MythTactics.Combat

    [Tooltip("The base power of the ability (e.g., base damage for a damage spell, base heal amount). Modifiers may apply.")]
    public int basePower = 10;

    [Tooltip("The type of damage dealt by this ability, if applicable (e.g., Physical, Magical, Fire).")]
    public DamageType damageType = DamageType.Magical; // Uses enum from MythTactics.Combat

    // ... (other commented out future fields)
}