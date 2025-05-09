// ClassDataSO.cs
using UnityEngine;
// using System.Collections.Generic; // If you add lists for skills, etc.

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

    [Header("Combat Stats")]
    [Tooltip("The base range (in tiles) for this class's standard attack. GDD 7.1.1 (simplified for basic attack).")]
    public int baseAttackRange = 1; // Default to 1 (melee)

    [Header("Attribute Growth Per Level (GDD 2.2)")]
    [Tooltip("Defines how primary attributes increase automatically per level for this class.")]
    public UnitDataTypes.SerializablePrimaryStatGain primaryStatGains; // MODIFIED to use nested class

    // Add other class-specific properties here:
    // e.g., Skill Trees, Equipment Proficiencies, Allowed Weapon Types, etc.
    // public List<AbilitySO> startingClassAbilities;
    // public List<EquipmentType> proficientEquipment;
}