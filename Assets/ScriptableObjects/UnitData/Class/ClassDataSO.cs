// ClassDataSO.cs
using UnityEngine;
// MODIFIED: Removed 'using UnitDataTypes;'

[CreateAssetMenu(fileName = "NewClassData", menuName = "MythTactics/Unit Class Data")]
public class ClassDataSO : ScriptableObject
{
    [Header("Identification")]
    public string className = "Unnamed Class";
    [TextArea(3, 5)]
    public string description = "Class description.";

    [Header("Base Stat Contributions")]
    [Tooltip("Base movement points contribution from this class (added to race base). GDD 1.2.")]
    public int baseMovementContribution = 3;

    [Tooltip("Flat speed bonus granted by this class. GDD 1.1.")]
    public int classSpeedBonus = 0;

    [Header("Base Resource Contributions")]
    [Tooltip("Base Vitality Points (VP) contribution from this class. GDD 2.3.")]
    public int baseVPContribution = 0;

    [Tooltip("Base Mana Points (MP) contribution from this class. GDD 2.3.")]
    public int baseMPContribution = 0;

    [Tooltip("Base Stamina Points (SP) contribution from this class. GDD 2.3.")]
    public int baseSPContribution = 0;

    [Tooltip("Base Focus Points (FP) contribution from this class. GDD 2.3.")]
    public int baseFPContribution = 0;

    [Tooltip("Base Influence Points (IP) contribution from this class. GDD 2.3.")]
    public int baseIPContribution = 0;

    [Header("Combat Stats")]
    [Tooltip("The base range (in tiles) for this class's standard attack. GDD 7.1.1 (simplified for basic attack).")]
    public int baseAttackRange = 1;

    [Header("Attribute Growth Per Level (GDD 2.2)")]
    [Tooltip("Defines how primary attributes increase automatically per level for this class.")]
    public SerializablePrimaryStatGain primaryStatGains; // This should now resolve correctly
}