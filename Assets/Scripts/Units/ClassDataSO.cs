// ClassDataSO.cs
using UnityEngine;

/// <summary>
/// ScriptableObject defining characteristics and progression for a unit class.
/// Based on GDD 2.2, 1.1, 1.2, 2.3, 6.1.2, 6.2.2.
/// </summary>
[CreateAssetMenu(fileName = "NewClassData", menuName = "MythTactics/Unit Data/Class Data")]
public class ClassDataSO : ScriptableObject
{
    [Header("Identification")]
    public string className = "Default Class";
    [TextArea(3,5)] // Make description box slightly larger in inspector
    public string description = "Class description.";

    [Header("Automatic Attribute Gains Per Level")]
    [Tooltip("How much each primary attribute increases automatically each level (e.g., 0.5 means +1 every 2 levels).")]
    [Range(0f, 2f)] public float coreGainPerLevel = 0.5f;
    [Range(0f, 2f)] public float echoGainPerLevel = 0.5f;
    [Range(0f, 2f)] public float pulseGainPerLevel = 0.5f;
    [Range(0f, 2f)] public float sparkGainPerLevel = 0.5f;
    [Range(0f, 2f)] public float glimmerGainPerLevel = 0.5f;
    [Range(0f, 2f)] public float auraGainPerLevel = 0.2f;

    [Header("Base Resource/Stat Contributions")]
    [Tooltip("Starting VP before Pulse scaling (GDD 2.3)")]
    public int baseVP = 20;
    [Tooltip("Starting MP before Spark scaling (GDD 2.3)")]
    public int baseMP = 10;
    [Tooltip("Starting SP before Core scaling (GDD 2.3)")]
    public int baseSP = 10;
    [Tooltip("Starting FP before Glimmer scaling (GDD 2.3)")]
    public int baseFP = 10;
    [Tooltip("Starting IP before Aura scaling (GDD 2.3)")]
    public int baseIP = 5;

    [Header("Derived Stat Bonuses")]
    [Tooltip("Flat bonus added to Effective Speed calculation (GDD 1.1).")]
    public int classSpeedBonus = 100; // Example base speed contribution

    [Tooltip("Contribution to the Base Movement range (GDD 1.2). To be combined with Race value.")]
    public int baseMovementContribution = 3; // e.g., Warrior=3, Rogue=4, Mage=2

    // --- Future Considerations ---
    // GDD 6.1.2 Mentions:
    // - Skill trees / ability progression rules.
    // - Equipment proficiencies.
    // We can add fields for these later, e.g.:
    // public SkillTreeSO skillTree;
    // public List<EquipmentType> weaponProficiencies;
    // public List<EquipmentType> armorProficiencies;
}