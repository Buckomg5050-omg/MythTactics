// UnitTemplateSO.cs
using UnityEngine;
using System.Collections.Generic;

// Make sure your enum files (FactionType.cs, AlignmentType.cs, AIBehaviorProfile.cs)
// are accessible, either by being in the global namespace or by adding 'using' directives
// if you've placed them within a specific namespace. For this example, I'm assuming global.

// Also, UnitPrimaryAttributes struct needs to be accessible.

[CreateAssetMenu(fileName = "NewUnitTemplate", menuName = "MythTactics/Unit Template")]
public class UnitTemplateSO : ScriptableObject
{
    [Header("== CORE IDENTITY ==")]
    [Tooltip("The display name for units created from this template.")]
    public string unitName = "New Unit";

    [Tooltip("The visual prefab for this unit. Should have a Unit component and other necessary Unity components (SpriteRenderer, Animator, etc.).")]
    public GameObject unitPrefab;

    [Tooltip("Portrait sprite used in UI elements like Turn Order, Info Panels, etc.")]
    public Sprite portrait;

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
    [Tooltip("The fundamental primary attribute values for this unit archetype at Level 1. These are set BEFORE race/class base values are added and before any racial/class attribute modifiers might apply if those were a feature.")]
    public UnitPrimaryAttributes baseLevel1Attributes = new UnitPrimaryAttributes();
    // Note: UnitStats.Initialize combines these with Race/Class contributions.

    [Tooltip("The XP value this unit grants when defeated. Primarily for enemy units.")]
    public int defaultXpValueOnDefeat = 10;

    // public int startingLevel = 1; // If units can start at > Lvl 1 via template. For now, assume Lvl 1.

    [Header("== STARTING EQUIPMENT ==")]
    public WeaponSO startingWeapon;
    public ArmorSO startingBodyArmor;
    // public ArmorSO startingShield; // Example for future expansion
    // public AccessorySO startingAccessory1; // Example

    [Header("== STARTING ABILITIES & ITEMS ==")]
    [Tooltip("Abilities known by the unit from the start.")]
    public List<AbilitySO> startingAbilities = new List<AbilitySO>();

    [Tooltip("Items in the unit's inventory from the start.")]
    public List<ItemSO> startingInventory = new List<ItemSO>();
    // Consider adding quantity if items can stack in templates, e.g., List<ItemStack> where ItemStack is a struct { ItemSO item; int quantity; }

    [Header("== AI CONFIGURATION (If not Player Faction) ==")]
    [Tooltip("The general behavior profile for AI-controlled units of this template.")]
    public AIBehaviorProfile aiProfile = AIBehaviorProfile.Balanced;

    [Tooltip("If true, allows minor randomization of primary attributes for AI instances created from this template.")]
    public bool allowsStatVariationForAI = false;

    [Range(0f, 0.3f)] // Max 30% variation
    [Tooltip("Percentage for stat variation (e.g., 0.1 for +/- 10% of baseLevel1Attributes). Only used if allowsStatVariationForAI is true and unit is not Player faction.")]
    public float aiStatVariationPercent = 0.1f;

    // For more advanced AI (GDD 6.1.2) - We can add WeightedAbilityEntry struct/class later
    // public List<WeightedAbilityEntry> enemySkillPool;
}