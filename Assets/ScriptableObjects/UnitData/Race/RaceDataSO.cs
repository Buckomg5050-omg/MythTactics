// RaceDataSO.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewRaceData", menuName = "MythTactics/Unit Race Data")]
public class RaceDataSO : ScriptableObject
{
    [Header("Identification")]
    public string raceName = "Unnamed Race";
    [TextArea(3, 5)]
    public string description = "Race description.";

    [Header("Base Stat Contributions")]
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

    // NEW FIELDS ADDED
    [Tooltip("Base Focus Points (FP) contribution from this race. GDD 2.3.")]
    public int baseFPContribution = 0;

    [Tooltip("Base Influence Points (IP) contribution from this race. GDD 2.3.")]
    public int baseIPContribution = 0;
}