// RaceDataSO.cs
using UnityEngine;
// using System.Collections.Generic; // For List if used later

[CreateAssetMenu(fileName = "NewRaceData", menuName = "MythTactics/Unit Race Data")]
public class RaceDataSO : ScriptableObject
{
    [Header("Identification")]
    public string raceName = "Unnamed Race";
    [TextArea(3, 5)]
    public string description = "Race description.";

    [Header("Base Stat Contributions")]
    // public UnitPrimaryAttributes baseAttributeModifiers; // Example if you had complex modifiers
    [Tooltip("Base movement points contribution from this race. GDD 1.2.")]
    public int baseMovementContribution = 0;

    [Tooltip("Flat speed bonus granted by this race. GDD 1.1.")]
    public int raceSpeedBonus = 0;

    [Header("Base Resource Contributions")] // NEW HEADER
    [Tooltip("Base Mana Points (MP) contribution from this race. GDD 2.3.")]
    public int baseMPContribution = 0; // NEW FIELD

    [Tooltip("Base Stamina Points (SP) contribution from this race. GDD 2.3.")]
    public int baseSPContribution = 0; // NEW FIELD

    // Add other race-specific properties here:
    // e.g., Resistances, Innate Abilities, Visual Prefab Variants, etc.
    // public Sprite racePortrait;
    // public List<AbilitySO> innateAbilities;
}