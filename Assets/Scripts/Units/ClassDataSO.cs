// ClassDataSO.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewClassData", menuName = "MythTactics/Unit Data/Class Data")]
public class ClassDataSO : ScriptableObject
{
    [Header("Identification")]
    public string className = "Default Class";
    [TextArea(3,5)]
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
    public int classSpeedBonus = 100;

    [Tooltip("Contribution to the Base Movement range (GDD 1.2). To be combined with Race value.")]
    public int baseMovementContribution = 3;
}