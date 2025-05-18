// ClassDataSO.cs
using UnityEngine;
using System.Collections.Generic; // MODIFIED: Added this line

// Assuming PrimaryStatGainsDefinition.cs (for SerializablePrimaryStatGain) is accessible
// Assuming AlignmentType and FactionType enums are accessible

[CreateAssetMenu(fileName = "NewClassData", menuName = "MythTactics/Unit Class Data")]
public class ClassDataSO : ScriptableObject
{
    [Header("== IDENTIFICATION & LORE ==")]
    public string className = "Unnamed Class";

    [Tooltip("A concise overview of the class's role, strengths, and typical playstyle.")]
    [TextArea(3, 5)]
    public string description = "Class description."; // Or shortDescription

    [Tooltip("Extended lore, historical significance, or notable organizations/figures related to this class.")]
    [TextArea(4, 8)]
    public string loreText = "";

    [Header("== ALIGNMENT & FACTION AFFINITIES (Optional) ==")]
    [Tooltip("The common or typical alignment tendency for members of this class. Individual units can vary.")]
    public AlignmentType typicalAlignment = AlignmentType.None;

    [Tooltip("Factions this class is commonly associated with or restricted to. (Informational, actual restriction enforced by game logic/scenario).")]
    public List<FactionType> commonFactionAssociations = new List<FactionType>(); // This line needs System.Collections.Generic

    [Header("== BASE STAT CONTRIBUTIONS (Class Base) ==")]
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


    [Header("== COMBAT ROLE ==")]
    [Tooltip("The base range (in tiles) for this class's standard attack if not using a weapon with specified range. GDD 7.1.1 (simplified for basic attack).")]
    public int baseAttackRange = 1;

    [Header("== ATTRIBUTE GROWTH PER LEVEL (GDD 2.2) ==")]
    [Tooltip("Defines how primary attributes increase automatically per level for this class.")]
    public SerializablePrimaryStatGain primaryStatGains;
}