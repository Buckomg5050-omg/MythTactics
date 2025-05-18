// UnitTemplateSO.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewUnitTemplate", menuName = "MythTactics/Unit Template")]
public class UnitTemplateSO : ScriptableObject
{
    [Header("== CORE IDENTITY ==")]
    [Tooltip("The display name for units created from this template.")]
    public string unitName = "New Unit";

    [Tooltip("The visual prefab for this unit. Should be your GenericUnit_Prefab.")]
    public GameObject unitPrefab;

    [Header("== VISUALS ==")]
    [Tooltip("Portrait sprite used in UI elements (Turn Order, Info Panels, etc.). Can also be used as a fallback for the in-world sprite if 'In World Combat Sprite' is not set.")]
    public Sprite portrait;

    [Tooltip("The primary sprite used for the unit in the game world during combat. If null, the 'Portrait' sprite will be attempted. If both are null, the prefab's default sprite is used.")]
    public Sprite inWorldCombatSprite; // MODIFIED: Added this new field

    [Header("== FACTION & ALIGNMENT ==")]
    [Tooltip("Default faction this unit belongs to (Player, Enemy, Ally, Neutral).")]
    public FactionType defaultFaction = FactionType.Player;

    [Tooltip("The metaphysical alignment of this unit (None, Light, Dark).")]
    public AlignmentType alignment = AlignmentType.None;

    [Header("== RACE & CLASS ==")]
    [Tooltip("Race data defining racial traits and base contributions.")]
    public RaceDataSO raceData;

    [Tooltip("Class data defining class traits, stat growth, and base contributions.")]
    public ClassDataSO classData;

    [Header("== BASE STATS & LEVEL 1 SETUP ==")]
    [Tooltip("The fundamental primary attribute values for this unit archetype at Level 1.")]
    public UnitPrimaryAttributes baseLevel1Attributes = new UnitPrimaryAttributes();

    [Tooltip("The XP value this unit grants when defeated. Primarily for enemy units.")]
    public int defaultXpValueOnDefeat = 10;

    [Header("== STARTING EQUIPMENT ==")]
    public WeaponSO startingWeapon;
    public ArmorSO startingBodyArmor;

    [Header("== STARTING ABILITIES & ITEMS ==")]
    [Tooltip("Abilities known by the unit from the start.")]
    public List<AbilitySO> startingAbilities = new List<AbilitySO>();

    [Tooltip("Items in the unit's inventory from the start.")]
    public List<ItemSO> startingInventory = new List<ItemSO>();

    [Header("== AI CONFIGURATION (If not Player Faction) ==")]
    [Tooltip("The general behavior profile for AI-controlled units of this template.")]
    public AIBehaviorProfile aiProfile = AIBehaviorProfile.Balanced;

    [Tooltip("If true, allows minor randomization of primary attributes for AI instances created from this template.")]
    public bool allowsStatVariationForAI = false;

    [Range(0f, 0.3f)]
    [Tooltip("Percentage for stat variation (e.g., 0.1 for +/- 10% of baseLevel1Attributes). Only used if allowsStatVariationForAI is true and unit is not Player faction.")]
    public float aiStatVariationPercent = 0.1f;
}