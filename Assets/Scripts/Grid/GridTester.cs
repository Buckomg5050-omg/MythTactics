// GridTester.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GridTester : MonoBehaviour
{
    [Header("References")]
    public TurnManager turnManager;

    [Header("Player Unit Setup")]
    public GameObject playerUnitPrefab;
    public Vector2Int playerStartPos = new Vector2Int(1, 1);
    public Unit PlayerUnitInstance { get; private set; }

    [Header("Test Occupancy Setup (Optional)")]
    [Tooltip("Assign the prefab for the first dummy/blocking unit.")]
    public GameObject dummyUnitPrefab1;
    public Vector2Int blockingUnit1Pos = new Vector2Int(4, 1);

    [Tooltip("Assign the prefab for the second dummy/blocking unit. Can be the same as Prefab1 if desired.")]
    public GameObject dummyUnitPrefab2;
    public Vector2Int blockingUnit2Pos = new Vector2Int(6, 2);

    private List<Unit> _dummyUnits = new List<Unit>();

    [Header("Logging Test Parameters (Optional)")]
    public Vector2Int logPathTestStart = new Vector2Int(10, 10);
    public Vector2Int logPathTestEnd = new Vector2Int(20, 20);
    public int logReachableTestRange = 3;

    void Start()
    {
        if (GridManager.Instance == null) { DebugHelper.LogError("GridTester: GridManager.Instance missing!", this); enabled = false; return; }
        if (GridManager.Instance.PathfinderInstance == null) { DebugHelper.LogError("GridTester: GridManager.Instance.PathfinderInstance is null!", this); enabled = false; return; }
        if (turnManager == null) { DebugHelper.LogError("GridTester: TurnManager missing! Please assign in Inspector.", this); enabled = false; return; }
        
        StartCoroutine(RunGridTests());
    }

    System.Collections.IEnumerator RunGridTests()
    {
        yield return null; 
        DebugHelper.Log("===== GridTester: Starting Spawn & Initial LOGGING Tests =====", this);
        
        SetupDummyUnitsForTest();
        SpawnPlayerUnit();
        
        yield return null;

        TestGridToWorld();
        TestWorldToGrid();
        TestGetNeighbors();
        TestGetTilesInRange();
        TestPathfinding();
        TestGetReachableTiles(); // Call to the method
        DebugHelper.Log("===== GridTester: Finished Initial LOGGING Tests =====", this);

        if (turnManager != null)
        {
            DebugHelper.Log("===== GridTester: Starting Combat via TurnManager =====", this);
            turnManager.StartCombat();
        }
        else
        {
            DebugHelper.LogError("GridTester: TurnManager reference not set. Cannot start combat.", this);
        }
    }

    void SetupDummyUnitsForTest()
    {
        DebugHelper.Log("--- GridTester: Setting up dummy units ---", this);
        
        if (dummyUnitPrefab1 != null)
        {
            SpawnSpecificDummy(dummyUnitPrefab1, blockingUnit1Pos, "DummyBlocker_1");
        }
        else
        {
            DebugHelper.Log("GridTester: No dummyUnitPrefab1, skipping first blocker setup.", this);
        }

        if (dummyUnitPrefab2 != null)
        {
            SpawnSpecificDummy(dummyUnitPrefab2, blockingUnit2Pos, "DummyBlocker_2");
        }
        else
        {
            DebugHelper.Log("GridTester: No dummyUnitPrefab2, skipping second blocker setup.", this);
        }
    }

    void SpawnSpecificDummy(GameObject prefab, Vector2Int position, string unitInstanceName)
    {
        if (GridManager.Instance.IsInPlayableBounds(position))
        {
            Tile tile = GridManager.Instance.GetTile(position);
            if (tile != null && !tile.IsOccupied)
            {
                GameObject unitGO = Instantiate(prefab, GridManager.Instance.GridToWorld(position), Quaternion.identity);
                unitGO.name = unitInstanceName;
                Unit unitComp = unitGO.GetComponent<Unit>();
                if (unitComp == null)
                {
                     DebugHelper.LogWarning($"GridTester: Prefab {prefab.name} missing Unit comp, adding one.", unitGO);
                     unitComp = unitGO.AddComponent<Unit>();
                }
                
                unitComp.unitName = unitGO.name; 
                unitComp.PlaceOnTile(tile); 
                _dummyUnits.Add(unitComp);

                if (turnManager != null) turnManager.RegisterUnit(unitComp);
                else DebugHelper.LogError($"GridTester: TurnManager null during {unitInstanceName} setup!", this);
                DebugHelper.Log($"Placed and registered {unitComp.unitName} at {position}", this);
            }
            else if (tile != null)
            {
                DebugHelper.LogWarning($"Dummy setup: Tile {position} for {unitInstanceName} occupied by {tile.occupyingUnit?.unitName}.", this);
            }
        }
        else
        {
            DebugHelper.LogWarning($"Dummy setup: Pos {position} for {unitInstanceName} out of bounds.", this);
        }
    }

    void SpawnPlayerUnit()
    {
        if (playerUnitPrefab == null) { DebugHelper.LogError("GridTester: playerUnitPrefab not assigned!", this); return; }
        if (!GridManager.Instance.IsInPlayableBounds(playerStartPos)) { DebugHelper.LogError($"GridTester: playerStartPos {playerStartPos} out of bounds!", this); return; }
        Tile startTile = GridManager.Instance.GetTile(playerStartPos);
        if (startTile == null) { DebugHelper.LogError($"GridTester: Cannot get tile at playerStartPos {playerStartPos}!", this); return; }
        if (startTile.IsOccupied) { DebugHelper.LogWarning($"GridTester: playerStartPos {startTile.gridPosition} occupied by {startTile.occupyingUnit?.name}! Check dummy placement.", this); }

        DebugHelper.Log($"--- GridTester: Spawning Player Unit at {playerStartPos} ---", this);
        GameObject unitGO = Instantiate(playerUnitPrefab, GridManager.Instance.GridToWorld(playerStartPos), Quaternion.identity);
        unitGO.name = "PlayerUnit_Instance";
        PlayerUnitInstance = unitGO.GetComponent<Unit>();
        if (PlayerUnitInstance == null) 
        {
            DebugHelper.LogWarning("PlayerUnitPrefab missing Unit comp, adding one.", unitGO); 
            PlayerUnitInstance = unitGO.AddComponent<Unit>();
        }
        
        PlayerUnitInstance.unitName = "Player"; 
        PlayerUnitInstance.PlaceOnTile(startTile);
        
        if (turnManager != null) turnManager.RegisterUnit(PlayerUnitInstance);
        else DebugHelper.LogError("GridTester: TurnManager null during player spawn!", this);
        
        DebugHelper.Log($"Spawned and registered {PlayerUnitInstance.unitName} at {startTile.gridPosition}", this);
    }

    void TestGridToWorld() {
        DebugHelper.Log("--- GridTester: Testing GridToWorld ---", this);
        Vector2Int pos1 = new Vector2Int(0, 0); Vector3 world1 = GridManager.Instance.GridToWorld(pos1); DebugHelper.Log($"G2W: {pos1} -> {world1}", this); Tile tile1 = GridManager.Instance.GetTile(pos1); if (tile1) DebugHelper.Log($"G2W Actual tile pos: {tile1.transform.position}", this);
        Vector2Int pos2 = new Vector2Int(GridManager.Instance.playableWidth-1, GridManager.Instance.playableHeight-1); Vector3 world2 = GridManager.Instance.GridToWorld(pos2); DebugHelper.Log($"G2W: {pos2} -> {world2}", this); Tile tile2 = GridManager.Instance.GetTile(pos2); if (tile2) DebugHelper.Log($"G2W Actual tile pos: {tile2.transform.position}", this);
        DebugHelper.Log("--- GridTester: Finished G2W Test ---", this);
    }
    void TestWorldToGrid() {
        DebugHelper.Log("--- GridTester: Testing WorldToGrid ---", this);
        Tile t1 = GridManager.Instance.GetTile(0,0); if(t1) { Vector3 w1 = t1.transform.position; Vector2Int g1 = GridManager.Instance.WorldToGrid(w1); DebugHelper.Log($"W2G: {w1} -> {g1}", this); } else DebugHelper.LogWarning("W2G T1: Tile (0,0) not found for test.");
        Tile t2 = GridManager.Instance.GetTile(0,0); if(t2) { Vector3 w2 = t2.transform.position + new Vector3(0.3f,-0.2f,0); Vector2Int g2 = GridManager.Instance.WorldToGrid(w2); DebugHelper.Log($"W2G (offset): {w2} -> {g2}", this); } else DebugHelper.LogWarning("W2G T2: Tile (0,0) not found for offset test.");
        Vector3 w4 = new Vector3(1000,1000,0); Vector2Int g4 = GridManager.Instance.WorldToGrid(w4); DebugHelper.Log($"W2G: {w4} (far outside) -> {g4}", this);
        DebugHelper.Log("--- GridTester: Finished W2G Test ---", this);
    }
    void TestGetNeighbors() {
        DebugHelper.Log("--- GridTester: Testing GetNeighbors ---", this);
        Vector2Int p1 = new Vector2Int(GridManager.Instance.playableWidth/2, GridManager.Instance.playableHeight/2); if(GridManager.Instance.IsInPlayableBounds(p1)) { DebugHelper.Log($"Neighbors4 for {p1}:", this); GridManager.Instance.GetNeighbors(p1).ForEach(n => DebugHelper.Log($"- {n.gridPosition} ({n.currentTerrainType})", this)); DebugHelper.Log($"Neighbors8 for {p1}:", this); GridManager.Instance.GetNeighbors(p1, true).ForEach(n => DebugHelper.Log($"- {n.gridPosition} ({n.currentTerrainType})", this));} else DebugHelper.LogWarning($"GN T1 OOB {p1}");
        Vector2Int p2 = new Vector2Int(0,0); if(GridManager.Instance.IsInPlayableBounds(p2)) { DebugHelper.Log($"Neighbors4 for {p2}:", this); GridManager.Instance.GetNeighbors(p2).ForEach(n => DebugHelper.Log($"- {n.gridPosition} ({n.currentTerrainType})", this));} else DebugHelper.LogWarning($"GN T2 OOB {p2}");
        DebugHelper.Log("--- GridTester: Finished GN Test ---", this);
    }
    void TestGetTilesInRange() {
        DebugHelper.Log("--- GridTester: Testing GetTilesInRange (Manhattan Distance) ---", this);
        Vector2Int p1 = new Vector2Int(GridManager.Instance.playableWidth/2, GridManager.Instance.playableHeight/2); int r1=2; if(GridManager.Instance.IsInPlayableBounds(p1)) { List<Tile> res1=GridManager.Instance.GetTilesInRange(p1,r1); DebugHelper.Log($"TilesInRange (Manhattan) {p1} R={r1}: Found {res1.Count}", this); } else DebugHelper.LogWarning($"GTIR T1 OOB {p1}");
        DebugHelper.Log("--- GridTester: Finished GTIR Test ---", this);
    }
    void TestPathfinding() {
        if (GridManager.Instance.PathfinderInstance == null) { DebugHelper.LogError("PF Test Error: PathfinderInstance null on GridManager.", this); return; }
        DebugHelper.Log("--- GridTester: Testing Pathfinding Calc ---", this);
        DebugHelper.Log($"Pathfinding {logPathTestStart} -> {logPathTestEnd}", this);
        // Assuming FindPath can take null for unit if not considering unit-specific costs for this generic test
        List<Tile> path1 = GridManager.Instance.PathfinderInstance.FindPath(logPathTestStart, logPathTestEnd, null); 
        if (path1 != null && path1.Count > 0) { DebugHelper.Log($"Path found ({path1.Count} steps): OK", this); } else { DebugHelper.LogWarning($"No path found {logPathTestStart} -> {logPathTestEnd}.", this); }
        DebugHelper.Log("--- GridTester: Finished PF Calc Test ---", this);
    }

    // THIS IS THE METHOD WITH THE CORRECTED CALL at line 213 (approximately)
    void TestGetReachableTiles()
    {
        if (GridManager.Instance.PathfinderInstance == null) { DebugHelper.LogError("Reachable Test Error: PathfinderInstance null on GridManager.", this); return; }
        DebugHelper.Log("--- GridTester: Testing GetReachableTiles Calc (Pathfinder) ---", this);
        Vector2Int center_grt = new Vector2Int(GridManager.Instance.playableWidth/2, GridManager.Instance.playableHeight/2); 
        int range1_grt = logReachableTestRange; // This is the movementPoints for the test

        if (GridManager.Instance.IsInPlayableBounds(center_grt)) { 
            if (PlayerUnitInstance != null && PlayerUnitInstance.Stats != null) // Ensure player and its stats are ready
            {
                // CORRECTED Call: GetReachableTiles(Vector2Int startPos, int movementPoints, Unit unit)
                // This should be line 213 or very close to it.
                List<Tile> reach1 = GridManager.Instance.PathfinderInstance.GetReachableTiles(center_grt, range1_grt, PlayerUnitInstance);
                DebugHelper.Log($"Reachable (Pathfinder) from {center_grt} with {range1_grt} move points for {PlayerUnitInstance.unitName}: Found {reach1.Count} tiles.", this); 
            }
            else
            {
                DebugHelper.LogWarning($"Reachable Calc Test: PlayerUnitInstance or its Stats not ready for test. Cannot perform this specific test.", this);
            }
        } else DebugHelper.LogWarning($"Reachable Calc T1 OOB {center_grt}");
        DebugHelper.Log("--- GridTester: Finished Reachable Calc Test ---", this);
    }
}