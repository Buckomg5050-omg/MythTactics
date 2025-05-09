// ClassDataSO.cs
using UnityEngine;
using System.Collections.Generic; // If you add lists for skills, etc.

[CreateAssetMenu(fileName = "NewClassData", menuName = "MythTactics/Unit Class Data")]
public class ClassDataSO : ScriptableObject
{
    [Header("Identification")]
    public string className = "Unnamed Class";
    [TextArea(3, 5)]
    public string description = "Class description.";

    [Header("Base Stat Contributions")]
    [Tooltip("Base movement points contribution from this class (added to race base). GDD 1.2.")]
    public int baseMovementContribution = 3; // Defaulting to a standard value

    [Tooltip("Flat speed bonus granted by this class. GDD 1.1.")]
    public int classSpeedBonus = 0; // e.g. Warrior +0, Rogue +10, Mage -5

    [Header("Attribute Growth Per Level (GDD 2.2)")]
    [Tooltip("Example: Define how primary attributes increase automatically per level.")]
    public SerializablePrimaryStatGain primaryStatGains; // Requires SerializablePrimaryStatGain class/struct
    // public int secondaryStatGainsPerXLevels = 2; // e.g. +1 to a secondary stat every 2 levels

    // Add other class-specific properties here:
    // e.g., Skill Trees, Equipment Proficiencies, Allowed Weapon Types, etc.
    // public List<AbilitySO> startingClassAbilities;
    // public List<EquipmentType> proficientEquipment;
}

// Example definition for SerializablePrimaryStatGain (if not already defined elsewhere)
// You might want to put this in a separate file or a shared "DataTypes" file.
[System.Serializable]
public class SerializablePrimaryStatGain
{
    public int coreGain = 1;
    public int echoGain = 1;
    public int pulseGain = 1;
    public int sparkGain = 1;
    public int glimmerGain = 1;
    public int auraGain = 1;
}