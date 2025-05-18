// EncounterManager.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EncounterManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Encounter SO defining the units and setup for this battle.")]
    public EncounterSO encounterConfiguration;
    public TurnManager turnManager; // Assign in Inspector

    private List<Unit> _spawnedPlayerUnits = new List<Unit>();
    private List<Unit> _spawnedEnemyUnits = new List<Unit>();
    // private List<Unit> _spawnedAllyUnits = new List<Unit>(); // For future

    void Start()
    {
        if (encounterConfiguration == null)
        {
            Debug.LogError("[EncounterManager] No EncounterConfiguration SO assigned! Cannot start battle.", this);
            enabled = false;
            return;
        }
        if (turnManager == null)
        {
            Debug.LogError("[EncounterManager] TurnManager not assigned! Cannot start battle.", this);
            enabled = false;
            return;
        }
        if (GridManager.Instance == null)
        {
            Debug.LogError("[EncounterManager] GridManager not found! Cannot place units.", this);
            enabled = false;
            return;
        }

        StartCoroutine(SetupEncounter());
    }

    private IEnumerator SetupEncounter()
    {
        Debug.Log($"[EncounterManager] Setting up encounter: {encounterConfiguration.encounterName}");

        // Clear any previous combat units from TurnManager (important for scene reloads/retries)
        turnManager.ClearCombatUnits(); // Assumes TurnManager has such a method

        // Spawn Player Units
        foreach (UnitSpawnData spawnData in encounterConfiguration.playerUnitsToSpawn)
        {
            SpawnUnit(spawnData, FactionType.Player);
            yield return null; // Small delay per spawn if needed, or do all at once
        }

        // Spawn Enemy Units
        foreach (UnitSpawnData spawnData in encounterConfiguration.enemyUnitsToSpawn)
        {
            SpawnUnit(spawnData, FactionType.Enemy);
            yield return null;
        }

        // TODO: Spawn Ally/Neutral units if defined

        Debug.Log("[EncounterManager] All units spawned. Initializing combat.");
        // Ensure all spawned units are registered with TurnManager (SpawnUnit handles this)
        // TurnManager should be ready to start.
        turnManager.StartCombat();
        Debug.Log($"[EncounterManager] Combat started for: {encounterConfiguration.encounterName}");
    }

    private void SpawnUnit(UnitSpawnData spawnData, FactionType defaultFactionForList)
    {
        if (spawnData.unitTemplate == null)
        {
            Debug.LogWarning("[EncounterManager] UnitSpawnData has null unitTemplate. Skipping this unit.", this);
            return;
        }

        Tile spawnTile = GridManager.Instance.GetTile(spawnData.gridPosition);
        if (spawnTile == null || !GridManager.Instance.IsInPlayableBounds(spawnData.gridPosition))
        {
            Debug.LogWarning($"[EncounterManager] Cannot spawn {spawnData.unitTemplate.unitName} at {spawnData.gridPosition}: Tile invalid or out of bounds. Skipping.", this);
            return;
        }
        if (spawnTile.IsOccupied)
        {
            Debug.LogWarning($"[EncounterManager] Cannot spawn {spawnData.unitTemplate.unitName} at {spawnData.gridPosition}: Tile already occupied by {spawnTile.occupyingUnit?.unitName}. Skipping.", this);
            return;
        }

        Vector3 worldPosition = GridManager.Instance.GridToWorld(spawnData.gridPosition);
        // Adjust Y based on prefab or generic height if needed, GridToWorld should handle this ideally.

        Unit spawnedUnit = UnitFactory.CreateUnit(
            spawnData.unitTemplate,
            spawnData.level,
            worldPosition,
            Quaternion.identity,
            null, // Or a "Units" parent transform
            defaultFactionForList // Use the faction context of the list (player/enemy)
                                  // Could also add factionOverride in UnitSpawnData for more complex scenarios
        );

        if (spawnedUnit != null)
        {
            if (spawnedUnit.Movement != null)
            {
                spawnedUnit.Movement.SetCurrentTileForced(spawnTile); // Place unit on grid
            }
            else
            {
                Debug.LogError($"[EncounterManager] Spawned unit {spawnedUnit.unitName} is missing UnitMovement component!", spawnedUnit);
            }
            
            turnManager.RegisterUnit(spawnedUnit); // Register with TurnManager

            // Add to local lists for tracking if needed
            if (spawnedUnit.CurrentFaction == FactionType.Player) _spawnedPlayerUnits.Add(spawnedUnit);
            else if (spawnedUnit.CurrentFaction == FactionType.Enemy) _spawnedEnemyUnits.Add(spawnedUnit);
            // else if (spawnedUnit.CurrentFaction == FactionType.Ally) _spawnedAllyUnits.Add(spawnedUnit);

            Debug.Log($"[EncounterManager] Spawned {spawnedUnit.unitName} (Lvl {spawnedUnit.level}, Faction: {spawnedUnit.CurrentFaction}) at {spawnData.gridPosition}", spawnedUnit);
        }
        else
        {
             Debug.LogError($"[EncounterManager] Failed to spawn unit using template {spawnData.unitTemplate.name} at {spawnData.gridPosition}", this);
        }
    }
}