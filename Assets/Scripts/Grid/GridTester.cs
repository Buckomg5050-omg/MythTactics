// GridTester.cs
using UnityEngine;
using System.Collections.Generic; // For List<T>

public class GridTester : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign the GridManager instance from your scene here.")]
    public GridManager gridManager;

    private Pathfinder _pathfinder; // Pathfinder instance

    [Header("Pathfinding Test Parameters")]
    public Vector2Int pathTestStart = new Vector2Int(1, 1);
    public Vector2Int pathTestEnd = new Vector2Int(20, 20);

    [Header("Test Occupancy Setup (Optional)")]
    [Tooltip("Drag a simple prefab here to represent a blocking unit for tests.")]
    public GameObject dummyUnitPrefab; // e.g., a simple Sprite or Cube
    public Vector2Int blockingUnit1Pos = new Vector2Int(5, 5);
    public Vector2Int blockingUnit2Pos = new Vector2Int(10, 10);

    private List<Unit> _dummyUnits = new List<Unit>(); // To keep track of spawned dummy units

    void Start()
    {
        if (gridManager == null)
        {
            DebugHelper.LogError("GridTester: GridManager reference not set! Cannot run tests.", this);
            return;
        }

        // Instantiate Pathfinder
        _pathfinder = new Pathfinder(gridManager);

        StartCoroutine(RunGridTests());
    }

    System.Collections.IEnumerator RunGridTests()
    {
        yield return null; // Wait one frame for GridManager to initialize

        DebugHelper.Log("===== GridTester: Starting All Grid Tests =====", this);

        // Setup dummy units for occupancy tests BEFORE pathfinding tests
        SetupDummyUnitsForTest(); 
        yield return null; // Wait a frame for units to potentially register their positions if they did it in Start/Awake

        TestGridToWorld();
        TestWorldToGrid();
        TestGetNeighbors();
        TestGetTilesInRange();
        TestPathfinding(); // New test call

        DebugHelper.Log("===== GridTester: Finished All Grid Tests =====", this);

        // Clean up dummy units after all tests are done
        CleanupDummyUnits();
    }

    void SetupDummyUnitsForTest()
    {
        if (dummyUnitPrefab == null) {
            // DebugHelper.Log("GridTester: No dummyUnitPrefab assigned, skipping occupancy setup for pathfinding test.", this);
            return;
        }

        DebugHelper.Log("--- GridTester: Setting up dummy units for occupancy test ---", this);
        Vector2Int[] positions = { blockingUnit1Pos, blockingUnit2Pos };
        int unitCounter = 1;

        foreach(Vector2Int pos in positions)
        {
            if (gridManager.IsInPlayableBounds(pos))
            {
                Tile tileToOccupy = gridManager.GetTile(pos);
                if (tileToOccupy != null && !tileToOccupy.IsOccupied)
                {
                    GameObject unitGO = Instantiate(dummyUnitPrefab, gridManager.GridToWorld(pos), Quaternion.identity);
                    unitGO.name = $"DummyBlocker_{unitCounter++}";
                    Unit unitComponent = unitGO.GetComponent<Unit>();
                    if (unitComponent == null) unitComponent = unitGO.AddComponent<Unit>(); // Add Unit script if prefab doesn't have it
                    
                    unitComponent.unitName = unitGO.name;
                    unitComponent.SetCurrentTile(tileToOccupy); // Unit updates its own tile
                    tileToOccupy.SetOccupyingUnit(unitComponent); // Tile is marked as occupied

                    _dummyUnits.Add(unitComponent);
                    DebugHelper.Log($"Placed {unitComponent.unitName} at {pos} on tile {tileToOccupy.name}", this);
                }
                else if (tileToOccupy != null && tileToOccupy.IsOccupied)
                {
                    DebugHelper.LogWarning($"Dummy unit setup: Tile {pos} is already occupied by {tileToOccupy.occupyingUnit.unitName}. Skipping dummy unit.", this);
                }
            }
            else
            {
                DebugHelper.LogWarning($"Dummy unit setup: Position {pos} is out of bounds.", this);
            }
        }
    }

    void CleanupDummyUnits()
    {
        if (_dummyUnits.Count > 0)
        {
            DebugHelper.Log("--- GridTester: Cleaning up dummy units ---", this);
            foreach (Unit unit in _dummyUnits)
            {
                if (unit != null)
                {
                    if (unit.CurrentTile != null)
                    {
                        unit.CurrentTile.ClearOccupyingUnit();
                    }
                    Destroy(unit.gameObject);
                }
            }
            _dummyUnits.Clear();
        }
    }


    void TestGridToWorld() // Existing test
    {
        DebugHelper.Log("--- GridTester: Testing GridToWorld ---", this);
        Vector2Int testPlayablePos_g2w_1 = new Vector2Int(0, 0);
        Vector3 worldPos_g2w_1 = gridManager.GridToWorld(testPlayablePos_g2w_1);
        DebugHelper.Log($"GridToWorld: Playable tile {testPlayablePos_g2w_1} is at world {worldPos_g2w_1}", this);
        Tile actualTile_g2w_1 = gridManager.GetTile(testPlayablePos_g2w_1);
        if (actualTile_g2w_1 != null) DebugHelper.Log($"GridToWorld: Actual tile at {testPlayablePos_g2w_1} transform is {actualTile_g2w_1.transform.position}", this);
        
        Vector2Int testPlayablePos_g2w_2 = new Vector2Int(gridManager.playableWidth - 1, gridManager.playableHeight - 1);
        Vector3 worldPos_g2w_2 = gridManager.GridToWorld(testPlayablePos_g2w_2);
        DebugHelper.Log($"GridToWorld: Playable tile {testPlayablePos_g2w_2} is at world {worldPos_g2w_2}", this);
        Tile actualTile_g2w_2 = gridManager.GetTile(testPlayablePos_g2w_2);
        if (actualTile_g2w_2 != null) DebugHelper.Log($"GridToWorld: Actual tile at {testPlayablePos_g2w_2} transform is {actualTile_g2w_2.transform.position}", this);
        DebugHelper.Log("--- GridTester: Finished GridToWorld Test ---", this);
    }

    void TestWorldToGrid() // Existing test
    {
        DebugHelper.Log("--- GridTester: Testing WorldToGrid ---", this);
        Tile tileForTest1_w2g = gridManager.GetTile(0,0);
        if (tileForTest1_w2g != null)
        {
            Vector3 testWorldPos1 = tileForTest1_w2g.transform.position;
            Vector2Int gridResult1 = gridManager.WorldToGrid(testWorldPos1);
            DebugHelper.Log($"WorldToGrid: World pos {testWorldPos1} -> Grid {gridResult1}", this);
        } else { DebugHelper.LogWarning("W2G Test1: Tile (0,0) not found.", this); }
        
        Tile tileForTest2_w2g = gridManager.GetTile(0,0);
        if (tileForTest2_w2g != null)
        {
            Vector3 testWorldPos2 = tileForTest2_w2g.transform.position + new Vector3(0.3f, -0.2f, 0f);
            Vector2Int gridResult2 = gridManager.WorldToGrid(testWorldPos2);
            DebugHelper.Log($"WorldToGrid: World pos {testWorldPos2} -> Grid {gridResult2}", this);
        } else { DebugHelper.LogWarning("W2G Test2: Tile (0,0) not found.", this); }
        
        if (gridManager.AllTiles != null)
        {
            int totalTestWidth = gridManager.playableWidth + 2;
            int totalTestHeight = gridManager.playableHeight + 2;
            float xGridOffset_test = -(totalTestWidth / 2.0f) + 0.5f;
            float yGridOffset_test = -(totalTestHeight / 2.0f) + 0.5f;
            Vector3 boundaryWorldPos = new Vector3(0 + xGridOffset_test, 0 + yGridOffset_test, 0);
            Vector2Int gridResult3 = gridManager.WorldToGrid(boundaryWorldPos);
            DebugHelper.Log($"WorldToGrid: World pos {boundaryWorldPos} (boundary _tiles[0,0]) -> Grid {gridResult3}", this);
        } else { DebugHelper.LogWarning("W2G Test3: Grid not initialized for boundary.", this); }
        
        Vector3 outsideWorldPos_w2g = new Vector3(100f, 100f, 0f);
        Vector2Int gridResult4_w2g = gridManager.WorldToGrid(outsideWorldPos_w2g);
        DebugHelper.Log($"WorldToGrid: World pos {outsideWorldPos_w2g} (far outside) -> Grid {gridResult4_w2g}", this);
        DebugHelper.Log("--- GridTester: Finished WorldToGrid Test ---", this);
    }

    void TestGetNeighbors() // Existing test
    {
        DebugHelper.Log("--- GridTester: Testing GetNeighbors ---", this);
        Vector2Int centerPos_gn = new Vector2Int(gridManager.playableWidth / 2, gridManager.playableHeight / 2);
        if (gridManager.IsInPlayableBounds(centerPos_gn))
        {
            DebugHelper.Log($"Neighbors for {centerPos_gn} (4-dir):", this);
            List<Tile> n4 = gridManager.GetNeighbors(centerPos_gn);
            foreach (Tile n in n4) DebugHelper.Log($"- GN4 {n.gridPosition} ({n.currentTerrainType})", this);
            DebugHelper.Log($"Neighbors for {centerPos_gn} (8-dir):", this);
            List<Tile> n8 = gridManager.GetNeighbors(centerPos_gn, true);
            foreach (Tile n in n8) DebugHelper.Log($"- GN8 {n.gridPosition} ({n.currentTerrainType})", this);
        } else { DebugHelper.LogWarning($"GN Test1: Center {centerPos_gn} out of bounds.", this); }
        
        Vector2Int cornerPos_gn = new Vector2Int(0,0);
        if (gridManager.IsInPlayableBounds(cornerPos_gn))
        {
            DebugHelper.Log($"Neighbors for {cornerPos_gn} (4-dir):", this);
            List<Tile> cn4 = gridManager.GetNeighbors(cornerPos_gn);
            foreach (Tile n in cn4) DebugHelper.Log($"- GN4C {n.gridPosition} ({n.currentTerrainType})", this);
        } else { DebugHelper.LogWarning($"GN Test2: Corner {cornerPos_gn} out of bounds.", this); }
        
        Vector2Int edgePos_gn = new Vector2Int(0, gridManager.playableHeight / 2);
         if (gridManager.IsInPlayableBounds(edgePos_gn))
        {
            DebugHelper.Log($"Neighbors for {edgePos_gn} (8-dir):", this);
            List<Tile> en8 = gridManager.GetNeighbors(edgePos_gn, true);
            foreach (Tile n in en8) DebugHelper.Log($"- GN8E {n.gridPosition} ({n.currentTerrainType})", this);
        } else { DebugHelper.LogWarning($"GN Test3: Edge {edgePos_gn} out of bounds.", this); }
        DebugHelper.Log("--- GridTester: Finished GetNeighbors Test ---", this);
    }

    void TestGetTilesInRange() // Existing test
    {
        DebugHelper.Log("--- GridTester: Testing GetTilesInRange ---", this);
        Vector2Int centerPos_gtir = new Vector2Int(gridManager.playableWidth / 2, gridManager.playableHeight / 2);
        int r1 = 2;
        if(gridManager.IsInPlayableBounds(centerPos_gtir))
        {
            DebugHelper.Log($"Tiles in range {r1} from {centerPos_gtir}:", this);
            List<Tile> tir1 = gridManager.GetTilesInRange(centerPos_gtir, r1);
            DebugHelper.Log($"Found {tir1.Count} tiles in range {r1} from {centerPos_gtir}:", this);
            foreach (Tile t in tir1) DebugHelper.Log($"- Tile {t.gridPosition}, Dist: {Mathf.Abs(t.gridPosition.x-centerPos_gtir.x)+Mathf.Abs(t.gridPosition.y-centerPos_gtir.y)} ({t.currentTerrainType})", this);
        } else { DebugHelper.LogWarning($"GTiR Test1: Center {centerPos_gtir} out of bounds.", this); }

        Vector2Int cornerPos_gtir = new Vector2Int(0,0);
        int r2 = 1;
        if(gridManager.IsInPlayableBounds(cornerPos_gtir))
        {
            DebugHelper.Log($"Tiles in range {r2} from {cornerPos_gtir}:", this);
            List<Tile> tir2 = gridManager.GetTilesInRange(cornerPos_gtir, r2);
            DebugHelper.Log($"Found {tir2.Count} tiles in range {r2} from {cornerPos_gtir}:", this);
            foreach (Tile t in tir2) DebugHelper.Log($"- Tile {t.gridPosition}, Dist: {Mathf.Abs(t.gridPosition.x-cornerPos_gtir.x)+Mathf.Abs(t.gridPosition.y-cornerPos_gtir.y)} ({t.currentTerrainType})", this);
        } else { DebugHelper.LogWarning($"GTiR Test2: Corner {cornerPos_gtir} out of bounds.", this); }
        DebugHelper.Log("--- GridTester: Finished GetTilesInRange Test ---", this);
    }

    // ----- NEW METHOD for Pathfinding Test -----
    void TestPathfinding()
    {
        if (_pathfinder == null)
        {
            DebugHelper.LogError("GridTester: Pathfinder not initialized! Cannot run pathfinding test.", this);
            return;
        }
        DebugHelper.Log("--- GridTester: Testing Pathfinding ---", this);

        // Test 1: Simple path, no obstacles initially (unless dummy units are placed on it)
        DebugHelper.Log($"Pathfinding from {pathTestStart} to {pathTestEnd}", this);
        List<Tile> path1 = _pathfinder.FindPath(pathTestStart, pathTestEnd);
        if (path1 != null && path1.Count > 0)
        {
            DebugHelper.Log($"Path found with {path1.Count} steps:", this);
            string pathString = $"Start({pathTestStart}) -> ";
            foreach (Tile tileInPath in path1)
            {
                pathString += $"{tileInPath.gridPosition} -> ";
                // You could also highlight these tiles visually for debugging
                // if (tileInPath != null) tileInPath.SetHighlight(TileHighlightState.MovementRange); // Example highlight
            }
            pathString += $"End({pathTestEnd})"; // Note: path1 usually doesn't include start, and includes end.
            DebugHelper.Log(pathString, this);
        }
        else
        {
            DebugHelper.LogWarning($"No path found from {pathTestStart} to {pathTestEnd}.", this);
        }

        // Test 2: Path to an unreachable or non-walkable tile (e.g., try pathing into a boundary if we could select one)
        // For now, let's try pathing to an occupied tile that's NOT one of our dummy units.
        // This requires a tile to be pre-occupied by something else.
        // If we place a dummy unit at pathTestEnd for instance, it should fail.
        // Let's re-test pathTestStart to pathTestEnd. If one of the dummy units is on pathTestEnd, it should now fail.
        if (gridManager.GetTile(pathTestEnd) != null && gridManager.GetTile(pathTestEnd).IsOccupied)
        {
            DebugHelper.Log($"Pathfinding from {pathTestStart} to {pathTestEnd} (expecting to fail if end is occupied by dummy):", this);
            List<Tile> path2 = _pathfinder.FindPath(pathTestStart, pathTestEnd);
             if (path2 != null && path2.Count > 0)
             {
                DebugHelper.LogWarning($"Path found to occupied {pathTestEnd} unexpectedly!", this);
             }
             else
             {
                DebugHelper.Log($"Correctly no path found to occupied {pathTestEnd}.", this);
             }
        }


        // Test 3: Path that might be blocked by one of the dummy units
        // E.g. Start (1,1), End (6,6), Blocker at (5,5)
        Vector2Int pathTestStartBlocked = new Vector2Int(1,1);
        Vector2Int pathTestEndBlocked = new Vector2Int(6,6); // Make sure this is different from blockingUnit1Pos if it's (5,5)
        bool isBlockingUnit1At_5_5 = false;
        foreach(var unit in _dummyUnits) {
            if (unit.CurrentTile != null && unit.CurrentTile.gridPosition == new Vector2Int(5,5)) {
                isBlockingUnit1At_5_5 = true;
                break;
            }
        }

        if (isBlockingUnit1At_5_5 && gridManager.IsInPlayableBounds(pathTestStartBlocked) && gridManager.IsInPlayableBounds(pathTestEndBlocked))
        {
            DebugHelper.Log($"Pathfinding from {pathTestStartBlocked} to {pathTestEndBlocked} (expecting path around (5,5) if occupied):", this);
            List<Tile> path3 = _pathfinder.FindPath(pathTestStartBlocked, pathTestEndBlocked);
            if (path3 != null && path3.Count > 0)
            {
                DebugHelper.Log($"Path found with {path3.Count} steps (blocked route):", this);
                string pathStringBlocked = $"Start({pathTestStartBlocked}) -> ";
                foreach (Tile tileInPath in path3)
                {
                    pathStringBlocked += $"{tileInPath.gridPosition} -> ";
                }
                pathStringBlocked += $"End({pathTestEndBlocked})";
                DebugHelper.Log(pathStringBlocked, this);
            }
            else
            {
                DebugHelper.LogWarning($"No path found from {pathTestStartBlocked} to {pathTestEndBlocked} (blocked route).", this);
            }
        } else if (!isBlockingUnit1At_5_5) {
            DebugHelper.Log("Skipping blocked path test as dummy unit not at (5,5) or not set up.", this);
        }


        DebugHelper.Log("--- GridTester: Finished Pathfinding Test ---", this);
    }
}