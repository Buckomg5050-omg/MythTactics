// UnitPrimaryAttributes.cs
using UnityEngine;

/// <summary>
/// Holds the six primary attributes for a unit.
/// Based on GDD Section 2.1.
/// Make it Serializable to potentially view/edit in Inspector or save/load.
/// </summary>
[System.Serializable]
public class UnitPrimaryAttributes
{
    // Consider using Properties with private setters if more complex logic needed later,
    // but public fields are simple for now.
    [Range(1, 100)] // Example range, adjust as needed
    public int Core = 5;    // Strength equivalent
    [Range(1, 100)]
    public int Echo = 5;    // Dexterity equivalent
    [Range(1, 100)]
    public int Pulse = 5;   // Constitution equivalent
    [Range(1, 100)]
    public int Spark = 5;   // Intelligence equivalent
    [Range(1, 100)]
    public int Glimmer = 5; // Wisdom equivalent
    [Range(1, 100)]
    public int Aura = 5;    // Charisma equivalent

    // Constructor for default values (optional)
    public UnitPrimaryAttributes()
    {
        Core = 5;
        Echo = 5;
        Pulse = 5;
        Spark = 5;
        Glimmer = 5;
        Aura = 5;
    }

    // Constructor to set specific values
    public UnitPrimaryAttributes(int core, int echo, int pulse, int spark, int glimmer, int aura)
    {
        Core = core;
        Echo = echo;
        Pulse = pulse;
        Spark = spark;
        Glimmer = glimmer;
        Aura = aura;
    }

    // Maybe add methods later for copying or modification?
    // public UnitPrimaryAttributes DeepCopy() { ... }
}