// RaceDataSO.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewRaceData", menuName = "MythTactics/Unit Race Data")]
public class RaceDataSO : ScriptableObject
{
    [Header("== IDENTIFICATION & LORE ==")]
    public string raceName = "Unnamed Race";

    [Tooltip("A concise overview of the race's key characteristics and typical role.")]
    [TextArea(3, 5)]
    public string description = "Race description."; // Or shortDescription

    [Tooltip("Extended lore, cultural details, and historical background for this race.")]
    [TextArea(4, 8)]
    public string loreText = ""; // NEW FIELD

    [Header("== ALIGNMENT TENDENCIES (Optional) ==")] // NEW HEADER
    [Tooltip("The common or typical alignment tendency for this race. Individual units can vary.")]
    public AlignmentType typicalAlignment = AlignmentType.None; // NEW FIELD

    [Header("== BASE STAT CONTRIBUTIONS (Racial Bonuses) ==")]
    // ... (rest of the fields: baseMovementContribution, raceSpeedBonus, etc. remain the same) ...
    [Tooltip("Base movement points contribution from this race. GDD 1.2.")]
    public int baseMovementContribution = 0;

    [Tooltip("Flat speed bonus granted by this race. GDD 1.1.")]
    public int raceSpeedBonus = 0;

    [Header("Base Resource Contributions")]
    [Tooltip("Base Vitality Points (VP) contribution from this race. GDD 2.3.")]
    public int baseVPContribution = 0;

    [Tooltip("Base Mana Points (MP) contribution from this race. GDD 2.3.")]
    public int baseMPContribution = 0;

    [Tooltip("Base Stamina Points (SP) contribution from this race. GDD 2.3.")]
    public int baseSPContribution = 0;

    [Tooltip("Base Focus Points (FP) contribution from this race. GDD 2.3.")]
    public int baseFPContribution = 0;

    [Tooltip("Base Influence Points (IP) contribution from this race. GDD 2.3.")]
    public int baseIPContribution = 0;
}