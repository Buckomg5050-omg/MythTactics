// AbilitySO.cs
using UnityEngine;
using MythTactics.Combat; // For DamageType, AbilityTargetType, AbilityEffectType
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewAbility", menuName = "MythTactics/Ability")]
public class AbilitySO : ScriptableObject
{
    [Header("== IDENTIFICATION & LORE ==")]
    [Tooltip("The display name of the ability.")]
    public string abilityName = "New Ability";

    [Tooltip("Detailed description of what the ability does, its effects, and any special properties. Can include flavor text.")]
    [TextArea(3, 6)] 
    public string description = "Ability Description.";

    public Sprite abilityIcon;

    // ADDED: Ability Type field
    [Header("== TYPE & CATEGORY ==")]
    [Tooltip("The fundamental type of this ability (e.g., Skill, Spell).")]
    public AbilityType abilityType = AbilityType.Skill; // Default to Skill


    [Header("== COSTS & REQUIREMENTS ==")]
    [Tooltip("Action Points (AP) required to use this ability.")]
    public int apCost = 1;
    public int mpCost = 0;
    public int spCost = 0;
    public int fpCost = 0;
    public int ipCost = 0;
    // Future: public int levelRequirement = 1;


    [Header("== TARGETING & RANGE ==")]
    [Tooltip("Who or what this ability can target.")]
    public AbilityTargetType targetType = AbilityTargetType.EnemyUnit;

    [Tooltip("The range of the ability in tiles. 0 for self-cast, 1 for adjacent, etc. For AoEs, this is often the cast range to the center point.")]
    public int range = 1;


    [Header("== ACCURACY & CRITICAL HIT ==")] 
    [Tooltip("Base accuracy of the ability (0-100 if it rolls to hit). Used in hit chance calculation if applicable and 'Always Hits' is false.")]
    [Range(0, 100)]
    public int baseAccuracy = 80;

    [Tooltip("If true, this ability does not perform a hit roll and always connects (e.g., self-buffs, friendly heals, some AoEs). Base Accuracy is ignored.")]
    public bool alwaysHits = false; 

    [Tooltip("Bonus percentage points added to the user's critical hit chance when using this specific ability.")]
    [Range(-100, 100)]
    public int critChanceModifier = 0; 


    [Header("== PRIMARY EFFECT (Damage/Heal/Etc) ==")]
    [Tooltip("The primary type of direct effect this ability produces (e.g., Damage, Heal).")]
    public AbilityEffectType effectType = AbilityEffectType.Damage;

    [Tooltip("The base power of the ability (e.g., base damage, base heal amount). Modifiers may apply from caster stats.")]
    public int basePower = 10;

    [Tooltip("The type of damage dealt by this ability, if applicable (e.g., Physical, Magical, Fire).")]
    public DamageType damageType = DamageType.Magical;

    [Tooltip("If true, this ability's direct damage/effect bypasses most resistances and mitigations.")]
    public bool dealsTrueDamage = false;


    [Header("== APPLIED STATUS EFFECTS ==")]
    [Tooltip("List of status effects this ability applies to the target(s) on successful use (or automatically if 'Always Hits' is true and applicable).")]
    public List<EffectSO> effectsToApplyOnHit = new List<EffectSO>();

    // Future:
    // public AreaOfEffectSO areaOfEffect; 
    // public SoundEffectSO castSound, hitSound, missSound;
    // public VisualEffectSO castVFX, hitVFX, travelVFX;
    // public List<AbilityTag> abilityTags; 
}