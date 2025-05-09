// GridTester.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GridTester : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign the GridManager instance from your scene here.")]
    public GridManager gridManager; // Will be GridManager.Instance
    [Tooltip("Assign the TurnManager instance from your scene here.")]
    public TurnManager turnManager; // Added reference

    private Pathfinder _pathfinder;

    [Header("Player Unit Setup")]
    [Tooltip("Assign the Player Unit Prefab here.")]
    public GameObject playerUnitPrefab;
    public Vector2Int playerStartPos = new Vector2Int(1, 1); // Defaulted to (1,1)

    public Unit PlayerUnitInstance { get; private set; }

    [Header("Test Occupancy Setup (Optional)")]
    [Tooltip("Drag a simple prefab here to represent a blocking unit for tests.")]
    public GameObject dummyUnitPrefab;
    public Vector2Int blockingUnit1Pos = new Vector2Int(4, 1); // Adjusted for test map
    public Vector2Int blockingUnit2Pos = new Vector2Int(6, 2); // Adjusted for test map

    private List<Unit> _dummyUnits = new List<Unit>();

    [Header("Logging Test Parameters (Optional)")]
    public Vector2Int logPathTestStart = new Vector2Int(10, 10); // Mid-grid
    public Vector2Int logPathTestEnd = new Vector2Int(20, 20);   // Far corner
    public int logReachableTestRange = 3;


    void Start()
    {
        // GridManager should be accessed via GridManager.Instance now
        if (GridManager.Instance == null) { DebugHelper.LogError("GridTester: GridManager.Instance missing!", this); enabled = false; return; }
        if (turnManager == null) { DebugHelper.LogError("GridTester: TurnManager missing! Please assign in Inspector.", this); enabled = false; return; }
        
        _pathfinder = new Pathfinder(GridManager.Instance);
        StartCoroutine(RunGridTests());
    }

    System.Collections.IEnumerator RunGridTests()
    {
        yield return null; // Wait a frame for other Awakes/Starts if needed
        DebugHelper.Log("===== GridTester: Starting Spawn & Initial LOGGING Tests =====", this);
        
        SetupDummyUnitsForTest(); // Dummies registered inside this method
        SpawnPlayerUnit();      // Player registered inside this method
        
        yield return null; // Ensure units are fully set up before tests run

        TestGridToWorld();
        TestWorldToGrid();
        TestGetNeighbors();
        TestGetTilesInRange();
        TestPathfinding();
        TestGetReachableTiles();
        DebugHelper.Log("===== GridTester: Finished Initial LOGGING Tests =====", this);

        if (turnManager != null)
        {
            DebugHelper.Log("===== GridTester: Starting Combat via TurnManager =====", this);
            turnManager.StartCombat();
        }
        else
        {
            DebugHelper.LogError("GridTester: TurnManager reference not set at end of RunGridTests. Cannot start combat.", this);
        }
    }

    void SetupDummyUnitsForTest()
    {
        if (dummyUnitPrefab == null) { DebugHelper.Log("GridTester: No dummyUnitPrefab, skipping blocker setup.", this); return; }
        DebugHelper.Log("--- GridTester: Setting up dummy units ---", this);
        Vector2Int[] positions = { blockingUnit1Pos, blockingUnit2Pos };
        int unitCounter = 1;
        foreach(Vector2Int pos in positions) {
            if (GridManager.Instance.IsInPlayableBounds(pos)) {
                Tile tile = GridManager.Instance.GetTile(pos);
                if (tile != null && !tile.IsOccupied) {
                    GameObject unitGO = Instantiate(dummyUnitPrefab, GridManager.Instance.GridToWorld(pos), Quaternion.identity);
                    unitGO.name = $"DummyBlocker_{unitCounter++}";
                    Unit unitComp = unitGO.GetComponent<Unit>() ?? unitGO.AddComponent<Unit>();
                    if (unitComp.currentAttributes == null) unitComp.currentAttributes = new UnitPrimaryAttributes(); // Ensure attributes exist for speed calc
                    unitComp.unitName = unitGO.name;
                    unitComp.PlaceOnTile(tile); // PlaceOnTile now calls ResetActionPoints
                    _dummyUnits.Add(unitComp);
                    if (turnManager != null) turnManager.RegisterUnit(unitComp); // REGISTER DUMMY
                    else DebugHelper.LogError("GridTester: TurnManager null during dummy setup!", this);
                    DebugHelper.Log($"Placed and registered {unitComp.unitName} at {pos}", this);
                } else if (tile != null) { DebugHelper.LogWarning($"Dummy setup: Tile {pos} occupied by {tile.occupyingUnit?.unitName}.", this); }
            } else { DebugHelper.LogWarning($"Dummy setup: Pos {pos} out of bounds.", this); }
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
        if (PlayerUnitInstance == null) { DebugHelper.LogWarning("PlayerUnitPrefab missing Unit comp, adding one.", unitGO); PlayerUnitInstance = unitGO.AddComponent<Unit>(); }
        if (PlayerUnitInstance.currentAttributes == null) PlayerUnitInstance.currentAttributes = new UnitPrimaryAttributes(); // Ensure attributes exist

        PlayerUnitInstance.unitName = "Player"; // Ensure name is set before registration for logs
        PlayerUnitInstance.PlaceOnTile(startTile); // PlaceOnTile calls unit.ResetActionPoints()
        
        if (turnManager != null) turnManager.RegisterUnit(PlayerUnitInstance); // REGISTER PLAYER
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
        // Boundary test might be tricky if WorldToGrid clamps to playable or returns special values.
        // Vector3 w3=new Vector3(GridManager.Instance.GridToWorld(new Vector2Int(-1,-1)).x, GridManager.Instance.GridToWorld(new Vector2Int(-1,-1)).y ,0); Vector2Int g3=GridManager.Instance.WorldToGrid(w3); DebugHelper.Log($"W2G: {w3} (intended boundary) -> {g3}", this); 
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
        if (_pathfinder == null) { DebugHelper.LogError("PF Test Error: Pathfinder null.", this); return; }
        DebugHelper.Log("--- GridTester: Testing Pathfinding Calc ---", this);
        DebugHelper.Log($"Pathfinding {logPathTestStart} -> {logPathTestEnd}", this);
        List<Tile> path1 = _pathfinder.FindPath(logPathTestStart, logPathTestEnd); // No specific unit passed for this general test
        if (path1 != null && path1.Count > 0) { DebugHelper.Log($"Path found ({path1.Count} steps): OK", this); } else { DebugHelper.LogWarning($"No path found {logPathTestStart} -> {logPathTestEnd}.", this); }
        DebugHelper.Log("--- GridTester: Finished PF Calc Test ---", this);
    }
    void TestGetReachableTiles()
    {
        if (_pathfinder == null) { DebugHelper.LogError("Reachable Test Error: Pathfinder null.", this); return; }
        DebugHelper.Log("--- GridTester: Testing GetReachableTiles Calc (Pathfinder) ---", this);
        Vector2Int center_grt = new Vector2Int(GridManager.Instance.playableWidth/2, GridManager.Instance.playableHeight/2); 
        int range1_grt = logReachableTestRange;
        if (GridManager.Instance.IsInPlayableBounds(center_grt)) { 
            List<Tile> reach1 = _pathfinder.GetReachableTiles(center_grt, range1_grt); // No specific unit for this general test
            DebugHelper.Log($"Reachable (Pathfinder) from {center_grt} with {range1_grt} move points: Found {reach1.Count} tiles.", this); 
        } else DebugHelper.LogWarning($"Reachable Calc T1 OOB {center_grt}");
        DebugHelper.Log("--- GridTester: Finished Reachable Calc Test ---", this);
    }
}