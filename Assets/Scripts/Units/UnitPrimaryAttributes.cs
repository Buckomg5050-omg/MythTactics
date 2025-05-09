// UnitPrimaryAttributes.cs
using UnityEngine;

[System.Serializable]
public class UnitPrimaryAttributes
{
    [Range(1, 100)] public int Core = 5;
    [Range(1, 100)] public int Echo = 5;
    [Range(1, 100)] public int Pulse = 5;
    [Range(1, 100)] public int Spark = 5;
    [Range(1, 100)] public int Glimmer = 5;
    [Range(1, 100)] public int Aura = 5;

    public UnitPrimaryAttributes() { } // Default constructor

    public UnitPrimaryAttributes(int core, int echo, int pulse, int spark, int glimmer, int aura)
    {
        Core = core; Echo = echo; Pulse = pulse; Spark = spark; Glimmer = glimmer; Aura = aura;
    }
}