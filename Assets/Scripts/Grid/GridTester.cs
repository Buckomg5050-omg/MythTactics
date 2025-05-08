// GridTester.cs
using UnityEngine;
using System.Collections; // For Coroutine
using System.Collections.Generic; // For List<T>

public class GridTester : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign the GridManager instance from your scene here.")]
    public GridManager gridManager;

    private Pathfinder _pathfinder;

    [Header("Player Unit Setup")]
    [Tooltip("Assign the Player Unit Prefab here.")]
    public GameObject playerUnitPrefab;
    public Vector2Int playerStartPos = new Vector2Int(12, 12); // Centered start

    public Unit PlayerUnitInstance { get; private set; }

    [Header("Test Occupancy Setup (Optional)")]
    [Tooltip("Drag a simple prefab here to represent a blocking unit for tests.")]
    public GameObject dummyUnitPrefab;
    public Vector2Int blockingUnit1Pos = new Vector2Int(5, 5);
    public Vector2Int blockingUnit2Pos = new Vector2Int(10, 10); // Blocker for path test

    private List<Unit> _dummyUnits = new List<Unit>();

    [Header("Logging Test Parameters (Optional)")]
    public Vector2Int logPathTestStart = new Vector2Int(1, 1);
    public Vector2Int logPathTestEnd = new Vector2Int(20, 20);
    public int logReachableTestRange = 3;


    void Start()
    {
        if (gridManager == null) { DebugHelper.LogError("GridTester: GridManager missing!", this); return; }
        _pathfinder = new Pathfinder(gridManager);
        StartCoroutine(RunGridTests());
    }

    System.Collections.IEnumerator RunGridTests()
    {
        yield return null;

        DebugHelper.Log("===== GridTester: Starting Spawn & Initial LOGGING Tests =====", this);

        // Spawn blockers FIRST
        SetupDummyUnitsForTest();
        // Then spawn player
        SpawnPlayerUnit();

        yield return null; // Wait a frame

        TestGridToWorld();
        TestWorldToGrid();
        TestGetNeighbors();
        TestGetTilesInRange();
        TestPathfinding();
        TestGetReachableTiles();

        DebugHelper.Log("===== GridTester: Finished Initial LOGGING Tests =====", this);

        // Cleanup only happens when game stops now
    }

    void SetupDummyUnitsForTest()
    {
        if (dummyUnitPrefab == null) {
            DebugHelper.Log("GridTester: No dummyUnitPrefab assigned, skipping dummy blocker setup.", this);
             return;
        }
        DebugHelper.Log("--- GridTester: Setting up dummy units ---", this);
        Vector2Int[] positions = { blockingUnit1Pos, blockingUnit2Pos };
        int unitCounter = 1;
        foreach(Vector2Int pos in positions) {
            if (gridManager.IsInPlayableBounds(pos)) {
                Tile tile = gridManager.GetTile(pos);
                if (tile != null && !tile.IsOccupied) {
                    GameObject unitGO = Instantiate(dummyUnitPrefab, gridManager.GridToWorld(pos), Quaternion.identity);
                    unitGO.name = $"DummyBlocker_{unitCounter++}";
                    Unit unitComp = unitGO.GetComponent<Unit>() ?? unitGO.AddComponent<Unit>();
                    unitComp.unitName = unitGO.name;
                    unitComp.PlaceOnTile(tile); // PlaceOnTile handles occupancy
                    _dummyUnits.Add(unitComp);
                    DebugHelper.Log($"Placed {unitComp.unitName} at {pos}", this);
                } else if (tile != null) { DebugHelper.LogWarning($"Dummy setup: Tile {pos} already occupied by {tile.occupyingUnit?.unitName}.", this); }
            } else { DebugHelper.LogWarning($"Dummy setup: Pos {pos} out of bounds.", this); }
        }
    }

     void SpawnPlayerUnit()
    {
        if (playerUnitPrefab == null) { DebugHelper.LogError("GridTester: playerUnitPrefab not assigned!", this); return; }
        if (!gridManager.IsInPlayableBounds(playerStartPos)) { DebugHelper.LogError($"GridTester: playerStartPos {playerStartPos} out of bounds!", this); return; }

        Tile startTile = gridManager.GetTile(playerStartPos);
        if (startTile == null) { DebugHelper.LogError($"GridTester: Cannot get tile at playerStartPos {playerStartPos}!", this); return; }

         if (startTile.IsOccupied) {
             DebugHelper.LogWarning($"GridTester: playerStartPos {startTile.gridPosition} occupied by {startTile.occupyingUnit?.name}! Check dummy placement.", this);
             // Decide if this should prevent player spawn
        }

        DebugHelper.Log($"--- GridTester: Spawning Player Unit at {playerStartPos} ---", this);
        GameObject unitGO = Instantiate(playerUnitPrefab, gridManager.GridToWorld(playerStartPos), Quaternion.identity);
        unitGO.name = "PlayerUnit_Instance";

        PlayerUnitInstance = unitGO.GetComponent<Unit>();
        if (PlayerUnitInstance == null) { DebugHelper.LogWarning("PlayerUnitPrefab missing Unit comp, adding.", unitGO); PlayerUnitInstance = unitGO.AddComponent<Unit>(); }

        PlayerUnitInstance.unitName = "Player";
        PlayerUnitInstance.PlaceOnTile(startTile);

        DebugHelper.Log($"Spawned {PlayerUnitInstance.unitName} at {startTile.gridPosition}", this);
    }


    // --- Test Methods (Log Only) ---
    // (These methods remain the same as the previous 'cleaned up' version)
    // ... TestGridToWorld ...
    // ... TestWorldToGrid ...
    // ... TestGetNeighbors ...
    // ... TestGetTilesInRange ...
    // ... TestPathfinding ...
    // ... TestGetReachableTiles ...

     void TestGridToWorld() {
        DebugHelper.Log("--- GridTester: Testing GridToWorld ---", this);
        Vector2Int pos1 = new Vector2Int(0, 0); Vector3 world1 = gridManager.GridToWorld(pos1); DebugHelper.Log($"G2W: {pos1} -> {world1}", this); Tile tile1 = gridManager.GetTile(pos1); if (tile1) DebugHelper.Log($"G2W Actual: {tile1.transform.position}", this);
        Vector2Int pos2 = new Vector2Int(gridManager.playableWidth-1, gridManager.playableHeight-1); Vector3 world2 = gridManager.GridToWorld(pos2); DebugHelper.Log($"G2W: {pos2} -> {world2}", this); Tile tile2 = gridManager.GetTile(pos2); if (tile2) DebugHelper.Log($"G2W Actual: {tile2.transform.position}", this);
        DebugHelper.Log("--- GridTester: Finished G2W Test ---", this);
    }
    void TestWorldToGrid() {
        DebugHelper.Log("--- GridTester: Testing WorldToGrid ---", this);
        Tile t1 = gridManager.GetTile(0,0); if(t1) { Vector3 w1 = t1.transform.position; Vector2Int g1 = gridManager.WorldToGrid(w1); DebugHelper.Log($"W2G: {w1} -> {g1}", this); } else DebugHelper.LogWarning("W2G T1 OOB");
        Tile t2 = gridManager.GetTile(0,0); if(t2) { Vector3 w2 = t2.transform.position + new Vector3(0.3f,-0.2f,0); Vector2Int g2 = gridManager.WorldToGrid(w2); DebugHelper.Log($"W2G: {w2} -> {g2}", this); } else DebugHelper.LogWarning("W2G T2 OOB");
        if (gridManager.AllTiles != null) { int tw=gridManager.playableWidth+2; int th=gridManager.playableHeight+2; float ox=-(tw/2f)+0.5f; float oy=-(th/2f)+0.5f; Vector3 w3=new Vector3(ox,oy,0); Vector2Int g3=gridManager.WorldToGrid(w3); DebugHelper.Log($"W2G: {w3} (boundary) -> {g3}", this); } else DebugHelper.LogWarning("W2G T3 Grid Null");
        Vector3 w4 = new Vector3(100,100,0); Vector2Int g4 = gridManager.WorldToGrid(w4); DebugHelper.Log($"W2G: {w4} (outside) -> {g4}", this);
        DebugHelper.Log("--- GridTester: Finished W2G Test ---", this);
    }
    void TestGetNeighbors() {
        DebugHelper.Log("--- GridTester: Testing GetNeighbors ---", this);
        Vector2Int p1 = new Vector2Int(gridManager.playableWidth/2, gridManager.playableHeight/2); if(gridManager.IsInPlayableBounds(p1)) { DebugHelper.Log($"Neighbors4 {p1}:", this); gridManager.GetNeighbors(p1).ForEach(n => DebugHelper.Log($"- {n.gridPosition}", this)); DebugHelper.Log($"Neighbors8 {p1}:", this); gridManager.GetNeighbors(p1, true).ForEach(n => DebugHelper.Log($"- {n.gridPosition}", this));} else DebugHelper.LogWarning($"GN T1 OOB {p1}");
        Vector2Int p2 = new Vector2Int(0,0); if(gridManager.IsInPlayableBounds(p2)) { DebugHelper.Log($"Neighbors4 {p2}:", this); gridManager.GetNeighbors(p2).ForEach(n => DebugHelper.Log($"- {n.gridPosition}", this));} else DebugHelper.LogWarning($"GN T2 OOB {p2}");
        Vector2Int p3 = new Vector2Int(0, gridManager.playableHeight/2); if(gridManager.IsInPlayableBounds(p3)) { DebugHelper.Log($"Neighbors8 {p3}:", this); gridManager.GetNeighbors(p3, true).ForEach(n => DebugHelper.Log($"- {n.gridPosition}", this));} else DebugHelper.LogWarning($"GN T3 OOB {p3}");
        DebugHelper.Log("--- GridTester: Finished GN Test ---", this);
    }
    void TestGetTilesInRange() {
        DebugHelper.Log("--- GridTester: Testing GetTilesInRange ---", this);
        Vector2Int p1 = new Vector2Int(gridManager.playableWidth/2, gridManager.playableHeight/2); int r1=2; if(gridManager.IsInPlayableBounds(p1)) { List<Tile> res1=gridManager.GetTilesInRange(p1,r1); DebugHelper.Log($"TilesInRange {p1} R={r1}: Found {res1.Count}", this); } else DebugHelper.LogWarning($"GTIR T1 OOB {p1}");
        Vector2Int p2 = new Vector2Int(0,0); int r2=1; if(gridManager.IsInPlayableBounds(p2)) { List<Tile> res2=gridManager.GetTilesInRange(p2,r2); DebugHelper.Log($"TilesInRange {p2} R={r2}: Found {res2.Count}", this); } else DebugHelper.LogWarning($"GTIR T2 OOB {p2}");
        DebugHelper.Log("--- GridTester: Finished GTIR Test ---", this);
    }
    void TestPathfinding() {
        if (_pathfinder == null) { DebugHelper.LogError("PF Test Error: Pathfinder null.", this); return; }
        DebugHelper.Log("--- GridTester: Testing Pathfinding Calc ---", this);
        DebugHelper.Log($"Pathfinding {logPathTestStart} -> {logPathTestEnd}", this);
        List<Tile> path1 = _pathfinder.FindPath(logPathTestStart, logPathTestEnd);
        if (path1 != null && path1.Count > 0) { DebugHelper.Log($"Path found ({path1.Count} steps): OK", this); } else { DebugHelper.LogWarning($"No path found {logPathTestStart} -> {logPathTestEnd}.", this); }
        DebugHelper.Log("--- GridTester: Finished PF Calc Test ---", this);
    }
    void TestGetReachableTiles()
    {
        if (_pathfinder == null) { DebugHelper.LogError("Reachable Test Error: Pathfinder null.", this); return; }
        DebugHelper.Log("--- GridTester: Testing GetReachableTiles Calc ---", this);
        Vector2Int center_grt = new Vector2Int(gridManager.playableWidth/2, gridManager.playableHeight/2); int range1_grt = logReachableTestRange;
        if (gridManager.IsInPlayableBounds(center_grt)) { DebugHelper.Log($"Reachable from {center_grt} with {range1_grt} move:", this); List<Tile> reach1 = _pathfinder.GetReachableTiles(center_grt, range1_grt); DebugHelper.Log($"Found {reach1.Count} reachable tiles (Calc Test).", this); } else DebugHelper.LogWarning($"Reachable Calc T1 OOB {center_grt}");
        Vector2Int corner_grt = new Vector2Int(0,0); int range2_grt = 2;
        if (gridManager.IsInPlayableBounds(corner_grt)) { DebugHelper.Log($"Reachable from {corner_grt} with {range2_grt} move:", this); List<Tile> reach2 = _pathfinder.GetReachableTiles(corner_grt, range2_grt); DebugHelper.Log($"Found {reach2.Count} reachable tiles (Calc Test).", this); } else DebugHelper.LogWarning($"Reachable Calc T2 OOB {corner_grt}");
        DebugHelper.Log("--- GridTester: Finished Reachable Calc Test ---", this);
    }

} // End of GridTester class