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
    // public UnitPrimaryAttributes baseAttributeModifiers; // Example if you had complex modifiers
    [Tooltip("Base movement points contribution from this race. GDD 1.2.")]
    public int baseMovementContribution = 0; // Example: Human might be +0, Elf +1, Dwarf -1 etc from a common base.

    [Tooltip("Flat speed bonus granted by this race. GDD 1.1.")]
    public int raceSpeedBonus = 0; // e.g. Humans 50, Elves 60, Dwarves 40

    // Add other race-specific properties here:
    // e.g., Resistances, Innate Abilities, Visual Prefab Variants, etc.
    // public Sprite racePortrait;
    // public List<AbilitySO> innateAbilities;
}