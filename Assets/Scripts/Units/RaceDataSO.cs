// RaceDataSO.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewRaceData", menuName = "MythTactics/Unit Data/Race Data")]
public class RaceDataSO : ScriptableObject
{
    [Header("Identification")]
    public string raceName = "Default Race";
    [TextArea(3,5)] public string description = "Race description.";

    [Header("Attribute Modifiers")]
    [Tooltip("Flat modifiers applied ON TOP of base attributes at level 1.")]
    public int coreModifier = 0;
    public int echoModifier = 0;
    public int pulseModifier = 0;
    public int sparkModifier = 0;
    public int glimmerModifier = 0;
    public int auraModifier = 0;

    [Header("Derived Stat Bonuses")]
    [Tooltip("Flat bonus added to Effective Speed calculation (GDD 1.1).")]
    public int raceSpeedBonus = 0;

    [Tooltip("Contribution to the Base Movement range (GDD 1.2). To be combined with Class value.")]
    public int baseMovementContribution = 0;
}