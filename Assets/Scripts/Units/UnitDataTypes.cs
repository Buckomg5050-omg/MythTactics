using UnityEngine;

[System.Serializable]
public class UnitDataTypes
{
    [System.Serializable]
    public class SerializablePrimaryStatGain
    {
        public int coreGain = 0;
        public int echoGain = 0;
        public int pulseGain = 0;
        public int sparkGain = 0;
        public int glimmerGain = 0;
        public int auraGain = 0;
    }
}