// GridTester.cs
using UnityEngine;
using System.Collections; // Required for Coroutine and WaitForSeconds
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
    public int reachableTestRange = 3; // Movement points for reachable test

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
        _pathfinder = new Pathfinder(gridManager);
        StartCoroutine(RunGridTests());
    }

    System.Collections.IEnumerator RunGridTests()
    {
        yield return null; // Wait one frame for GridManager to initialize

        DebugHelper.Log("===== GridTester: Starting All Grid Tests =====", this);

        ClearAllHighlights(); // Clear previous highlights before setup
        SetupDummyUnitsForTest();
        yield return null; // Wait a frame

        // Run non-visual tests first
        TestGridToWorld();
        TestWorldToGrid();
        TestGetNeighbors();
        TestGetTilesInRange(); // The basic range test

        // --- Pathfinding tests with Visualization ---
        DebugHelper.Log("--- Running Path Test (Visual) ---", this);
        ClearAllHighlights(); // Clear before path test
        TestPathfinding(); // This will now highlight the path
        DebugHelper.Log("Pausing 2s to show PATH highlight...", this);
        yield return new WaitForSeconds(2.0f); // Pause to see the path highlight

        DebugHelper.Log("--- Running Reachable Test (Visual) ---", this);
        ClearAllHighlights(); // Clear before reachable test
        TestGetReachableTiles(); // This will now highlight reachable tiles
        DebugHelper.Log("Pausing 3s to show REACHABLE highlight...", this);
        yield return new WaitForSeconds(3.0f); // Pause to see the reachable highlight

        ClearAllHighlights(); // Final clear after tests

        DebugHelper.Log("===== GridTester: Finished All Grid Tests =====", this);

        CleanupDummyUnits();
    }

    void SetupDummyUnitsForTest()
    {
        if (dummyUnitPrefab == null) return;
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
                    unitComp.SetCurrentTile(tile);
                    tile.SetOccupyingUnit(unitComp);
                    _dummyUnits.Add(unitComp);
                    DebugHelper.Log($"Placed {unitComp.unitName} at {pos}", this);
                } else if (tile != null) { DebugHelper.LogWarning($"Dummy setup: Tile {pos} already occupied by {tile.occupyingUnit?.unitName}.", this); }
            } else { DebugHelper.LogWarning($"Dummy setup: Pos {pos} out of bounds.", this); }
        }
    }

    void CleanupDummyUnits()
    {
        if (_dummyUnits.Count > 0) {
            DebugHelper.Log("--- GridTester: Cleaning up dummy units ---", this);
            foreach (Unit unit in _dummyUnits) {
                if (unit != null) {
                    if (unit.CurrentTile != null) unit.CurrentTile.ClearOccupyingUnit();
                    Destroy(unit.gameObject); } }
            _dummyUnits.Clear(); }
    }


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
        Vector2Int p1 = new Vector2Int(gridManager.playableWidth/2, gridManager.playableHeight/2); int r1=2; if(gridManager.IsInPlayableBounds(p1)) { List<Tile> res1=gridManager.GetTilesInRange(p1,r1); DebugHelper.Log($"TilesInRange {p1} R={r1}: Found {res1.Count}", this); res1.ForEach(t=>DebugHelper.Log($"- {t.gridPosition}",this));} else DebugHelper.LogWarning($"GTIR T1 OOB {p1}");
        Vector2Int p2 = new Vector2Int(0,0); int r2=1; if(gridManager.IsInPlayableBounds(p2)) { List<Tile> res2=gridManager.GetTilesInRange(p2,r2); DebugHelper.Log($"TilesInRange {p2} R={r2}: Found {res2.Count}", this); res2.ForEach(t=>DebugHelper.Log($"- {t.gridPosition}",this));} else DebugHelper.LogWarning($"GTIR T2 OOB {p2}");
        DebugHelper.Log("--- GridTester: Finished GTIR Test ---", this);
    }
    void TestPathfinding() {
        if (_pathfinder == null) { DebugHelper.LogError("PF Test Error: Pathfinder null.", this); return; }
        DebugHelper.Log("--- GridTester: Testing Pathfinding ---", this);
        DebugHelper.Log($"Pathfinding {pathTestStart} -> {pathTestEnd}", this);
        List<Tile> path1 = _pathfinder.FindPath(pathTestStart, pathTestEnd);
        if (path1 != null && path1.Count > 0) {
            DebugHelper.Log($"Path found ({path1.Count} steps):", this); string s1 = $"S({pathTestStart})->";
            foreach (Tile t in path1) { s1 += $"{t.gridPosition}->"; if (t!=null) t.SetHighlight(TileHighlightState.AttackRange); } // Highlight Path
            s1 += $"E({pathTestEnd})"; DebugHelper.Log(s1, this);
        } else { DebugHelper.LogWarning($"No path found {pathTestStart} -> {pathTestEnd}.", this); }

        Tile endTile = gridManager.GetTile(pathTestEnd);
        if (endTile != null && endTile.IsOccupied) {
            DebugHelper.Log($"Pathfinding {pathTestStart} -> {pathTestEnd} (Expect fail: Occupied End)", this); List<Tile> path2 = _pathfinder.FindPath(pathTestStart, pathTestEnd);
            if (path2 != null && path2.Count > 0) { DebugHelper.LogWarning($"Path found to occupied {pathTestEnd}!", this);} else { DebugHelper.Log($"Correctly no path to occupied {pathTestEnd}.", this); }
        }

        Vector2Int blockStart = new Vector2Int(1,1); Vector2Int blockEnd = new Vector2Int(6,6); Vector2Int blockPos = new Vector2Int(5,5); bool blockerPresent = false; foreach(var u in _dummyUnits) if(u.CurrentTile?.gridPosition == blockPos) blockerPresent = true;
        if (blockerPresent && gridManager.IsInPlayableBounds(blockStart) && gridManager.IsInPlayableBounds(blockEnd)) {
             DebugHelper.Log($"Pathfinding {blockStart} -> {blockEnd} (Expect route around {blockPos})", this);
             List<Tile> path3 = _pathfinder.FindPath(blockStart, blockEnd);
              if (path3 != null && path3.Count > 0) { DebugHelper.Log($"Path found ({path3.Count} steps) (Blocked):", this); string s3 = $"S({blockStart})->"; foreach (Tile t in path3) { s3 += $"{t.gridPosition}->"; if (t!=null) t.SetHighlight(TileHighlightState.AttackRange); } s3 += $"E({blockEnd})"; DebugHelper.Log(s3, this); } else { DebugHelper.LogWarning($"No path found {blockStart} -> {blockEnd} (Blocked).", this); }
        } else if (!blockerPresent) DebugHelper.Log($"Skipping blocked path test ({blockPos} not occupied).", this);
        DebugHelper.Log("--- GridTester: Finished PF Test ---", this);
    }

    void TestGetReachableTiles()
    {
        if (_pathfinder == null) { DebugHelper.LogError("Reachable Test Error: Pathfinder null.", this); return; }
        DebugHelper.Log("--- GridTester: Testing GetReachableTiles ---", this);

        Vector2Int center = new Vector2Int(gridManager.playableWidth/2, gridManager.playableHeight/2); int range1 = reachableTestRange;
        if (gridManager.IsInPlayableBounds(center)) {
            DebugHelper.Log($"Reachable from {center} with {range1} move:", this);
            List<Tile> reach1 = _pathfinder.GetReachableTiles(center, range1);
            DebugHelper.Log($"Found {reach1.Count} reachable tiles:", this);
            int count = 0; foreach(Tile t in reach1) { if (t!=null) t.SetHighlight(TileHighlightState.MovementRange); if(count < 10 || count > reach1.Count-5) DebugHelper.Log($"- {t?.gridPosition}", this); else if (count==10) DebugHelper.Log("...", this); count++;} // Highlight Reachable
            bool blockerFound = false; if (_dummyUnits.Count > 0 && _dummyUnits[0].CurrentTile != null) { Vector2Int bp = _dummyUnits[0].CurrentTile.gridPosition; foreach(var t in reach1) if(t.gridPosition == bp) blockerFound=true; if(blockerFound) DebugHelper.LogWarning($"Reachable Test1: Blocker {bp} found in list!", this); else DebugHelper.Log($"Reachable Test1: Blocker {bp} correctly NOT found.", this);}
        } else DebugHelper.LogWarning($"Reachable T1 OOB {center}");

        Vector2Int corner = new Vector2Int(0,0); int range2 = 2;
        if (gridManager.IsInPlayableBounds(corner)) {
            DebugHelper.Log($"Reachable from {corner} with {range2} move:", this);
            List<Tile> reach2 = _pathfinder.GetReachableTiles(corner, range2);
            DebugHelper.Log($"Found {reach2.Count} reachable tiles:", this);
            foreach(Tile t in reach2) { if (t!=null) t.SetHighlight(TileHighlightState.MovementRange); DebugHelper.Log($"- {t?.gridPosition}", this);} // Highlight Reachable
        } else DebugHelper.LogWarning($"Reachable T2 OOB {corner}");
        DebugHelper.Log("--- GridTester: Finished Reachable Test ---", this);
    }

     void ClearAllHighlights()
    {
        if (gridManager == null || gridManager.AllTiles == null) return;
        // Don't log clearing every time, can be noisy
        // DebugHelper.Log("--- GridTester: Clearing all tile highlights ---", this);
        for (int x = 0; x < gridManager.playableWidth; x++) {
            for (int y = 0; y < gridManager.playableHeight; y++) {
                Tile tile = gridManager.GetTile(x, y);
                if (tile != null) tile.SetHighlight(TileHighlightState.None);
            }
        }
    }

} // End of GridTester class