// ClassDataSO.cs
using UnityEngine;
// using System.Collections.Generic; // If you add lists for skills, etc.

// Assuming UnitDataTypes.SerializablePrimaryStatGain is defined elsewhere
// If not, and it's simple, it could be nested or defined in a common types file.
// For now, I'll assume it exists as you've indicated.
// namespace UnitDataTypes // Example namespace
// {
//     [System.Serializable]
//     public class SerializablePrimaryStatGain 
//     {
//         public int coreGain;
//         public int echoGain;
//         // ... and so on for other primary stats
//     }
// }


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

    // MOVED baseMP and baseSP here for better grouping with other resource-related base stats
    [Header("Base Resource Contributions")] // NEW or MOVED HEADER
    [Tooltip("Base Mana Points (MP) contribution from this class. GDD 2.3.")]
    public int baseMPContribution = 0; // NEW FIELD

    [Tooltip("Base Stamina Points (SP) contribution from this class. GDD 2.3.")]
    public int baseSPContribution = 0; // NEW FIELD

    [Header("Combat Stats")]
    [Tooltip("The base range (in tiles) for this class's standard attack. GDD 7.1.1 (simplified for basic attack).")]
    public int baseAttackRange = 1; // Default to 1 (melee)

    [Header("Attribute Growth Per Level (GDD 2.2)")]
    [Tooltip("Defines how primary attributes increase automatically per level for this class.")]
    public UnitDataTypes.SerializablePrimaryStatGain primaryStatGains;

    // Add other class-specific properties here:
    // e.g., Skill Trees, Equipment Proficiencies, Allowed Weapon Types, etc.
    // public List<AbilitySO> startingClassAbilities;
    // public List<EquipmentType> proficientEquipment;
}