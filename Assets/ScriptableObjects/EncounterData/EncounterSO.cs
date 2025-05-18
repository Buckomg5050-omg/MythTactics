// EncounterSO.cs
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct UnitSpawnData
{
    public UnitTemplateSO unitTemplate;
    public int level;
    public Vector2Int gridPosition;
    // public FactionType factionOverride; // Optional if template faction isn't enough
    // public AIBehaviorProfile aiProfileOverride; // Optional
}

[CreateAssetMenu(fileName = "NewEncounter", menuName = "MythTactics/Encounter Configuration")]
public class EncounterSO : ScriptableObject
{
    [Header("Encounter Description")]
    public string encounterName = "Unnamed Encounter";
    [TextArea]
    public string description = "Details about this encounter.";

    [Header("Player Units")]
    [Tooltip("Define player units for this encounter. These might be overridden by a party management system later.")]
    public List<UnitSpawnData> playerUnitsToSpawn;

    [Header("Enemy Units")]
    public List<UnitSpawnData> enemyUnitsToSpawn;

    // Future:
    // public List<UnitSpawnData> allyUnitsToSpawn;
    // public List<UnitSpawnData> neutralUnitsToSpawn;
    // public string victoryCondition; // e.g., "DefeatAllEnemies", "SurviveXRounds"
    // public string defeatCondition; // e.g., "PlayerPartyWiped"
    // public MusicTrackSO battleMusic;
    // public RewardTableSO rewardsOnVictory;
}