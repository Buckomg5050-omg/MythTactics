// GridTester.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Required for List

public class GridTester : MonoBehaviour
{
    [Header("References (Optional for On-Demand Tests)")]
    public TurnManager turnManager;

    [Header("Test Parameters (For On-Demand Grid Logic Tests)")]
    public Vector2Int logPathTestStart = new Vector2Int(10, 10);
    public Vector2Int logPathTestEnd = new Vector2Int(20, 20);
    public int logReachableTestRange = 3;
    public Unit unitForReachableTest;

    [Header("On-Demand Test Triggers")]
    public bool runTestsOnEnable = false;

    void OnEnable()
    {
        if (runTestsOnEnable)
        {
            RunAllGridLogicTests();
        }
    }

    [ContextMenu("Execute All Grid Logic Tests Now")]
    public void RunAllGridLogicTests()
    {
        if (GridManager.Instance == null) { DebugHelper.LogError("GridTester: GridManager.Instance missing! Cannot run grid logic tests.", this); return; }
        if (GridManager.Instance.PathfinderInstance == null && (ShouldRunPathfindingTests() || ShouldRunReachableTilesTest()))
        { DebugHelper.LogWarning("GridTester: GridManager.Instance.PathfinderInstance is null! Pathfinding/Reachable tests might fail or be skipped.", this); }

        DebugHelper.Log("===== GridTester: EXECUTING ON-DEMAND GRID LOGIC TESTS =====", this);
        TestGridToWorld();
        TestWorldToGrid();
        TestGetNeighbors();
        TestGetTilesInRange();

        if (ShouldRunPathfindingTests()) TestPathfinding(); else DebugHelper.Log("GridTester: Skipping Pathfinding test (Pathfinder might be null).", this);
        if (ShouldRunReachableTilesTest()) TestGetReachableTiles(); else DebugHelper.Log("GridTester: Skipping Reachable Tiles test (Pathfinder or test unit might be null).", this);

        DebugHelper.Log("===== GridTester: FINISHED ON-DEMAND GRID LOGIC TESTS =====", this);
    }

    private bool ShouldRunPathfindingTests()
    {
        return GridManager.Instance != null && GridManager.Instance.PathfinderInstance != null;
    }
    private bool ShouldRunReachableTilesTest()
    {
        return GridManager.Instance != null && GridManager.Instance.PathfinderInstance != null && unitForReachableTest != null;
    }

    void TestGridToWorld() {
        DebugHelper.Log("--- GridTester: Testing GridToWorld ---", this);
        Vector2Int pos1 = new Vector2Int(0, 0); Vector3 world1 = GridManager.Instance.GridToWorld(pos1); DebugHelper.Log($"G2W: {pos1} -> {world1}", this); Tile tile1 = GridManager.Instance.GetTile(pos1); if (tile1) DebugHelper.Log($"G2W Actual tile pos: {tile1.transform.position}", this);
        // MODIFIED: Used capitalized PlayableWidth and PlayableHeight properties
        Vector2Int pos2 = new Vector2Int(GridManager.Instance.PlayableWidth-1, GridManager.Instance.PlayableHeight-1); Vector3 world2 = GridManager.Instance.GridToWorld(pos2); DebugHelper.Log($"G2W: {pos2} -> {world2}", this); Tile tile2 = GridManager.Instance.GetTile(pos2); if (tile2) DebugHelper.Log($"G2W Actual tile pos: {tile2.transform.position}", this);
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
        // MODIFIED: Used capitalized PlayableWidth and PlayableHeight properties
        Vector2Int p1 = new Vector2Int(GridManager.Instance.PlayableWidth/2, GridManager.Instance.PlayableHeight/2); if(GridManager.Instance.IsInPlayableBounds(p1)) { DebugHelper.Log($"Neighbors4 for {p1}:", this); GridManager.Instance.GetNeighbors(p1).ForEach(n => DebugHelper.Log($"- {n.gridPosition} ({n.currentTerrainType})", this)); DebugHelper.Log($"Neighbors8 for {p1}:", this); GridManager.Instance.GetNeighbors(p1, true).ForEach(n => DebugHelper.Log($"- {n.gridPosition} ({n.currentTerrainType})", this));} else DebugHelper.LogWarning($"GN T1 OOB {p1}");
        Vector2Int p2 = new Vector2Int(0,0); if(GridManager.Instance.IsInPlayableBounds(p2)) { DebugHelper.Log($"Neighbors4 for {p2}:", this); GridManager.Instance.GetNeighbors(p2).ForEach(n => DebugHelper.Log($"- {n.gridPosition} ({n.currentTerrainType})", this));} else DebugHelper.LogWarning($"GN T2 OOB {p2}");
        DebugHelper.Log("--- GridTester: Finished GN Test ---", this);
    }
    void TestGetTilesInRange() {
        DebugHelper.Log("--- GridTester: Testing GetTilesInRange (Manhattan Distance) ---", this);
        // MODIFIED: Used capitalized PlayableWidth and PlayableHeight properties
        Vector2Int p1 = new Vector2Int(GridManager.Instance.PlayableWidth/2, GridManager.Instance.PlayableHeight/2); int r1=2; if(GridManager.Instance.IsInPlayableBounds(p1)) { List<Tile> res1=GridManager.Instance.GetTilesInRange(p1,r1); DebugHelper.Log($"TilesInRange (Manhattan) {p1} R={r1}: Found {res1.Count}", this); } else DebugHelper.LogWarning($"GTIR T1 OOB {p1}");
        DebugHelper.Log("--- GridTester: Finished GTIR Test ---", this);
    }
    void TestPathfinding() {
        if (GridManager.Instance == null || GridManager.Instance.PathfinderInstance == null) { DebugHelper.LogError("PF Test Error: GridManager or PathfinderInstance null.", this); return; }
        DebugHelper.Log("--- GridTester: Testing Pathfinding Calc ---", this);
        DebugHelper.Log($"Pathfinding {logPathTestStart} -> {logPathTestEnd}", this);
        List<Tile> path1 = GridManager.Instance.PathfinderInstance.FindPath(logPathTestStart, logPathTestEnd, null); 
        if (path1 != null && path1.Count > 0) { DebugHelper.Log($"Path found ({path1.Count} steps): OK", this); } else { DebugHelper.LogWarning($"No path found {logPathTestStart} -> {logPathTestEnd}.", this); }
        DebugHelper.Log("--- GridTester: Finished PF Calc Test ---", this);
    }

    void TestGetReachableTiles()
    {
        if (GridManager.Instance == null || GridManager.Instance.PathfinderInstance == null) { DebugHelper.LogError("Reachable Test Error: GridManager or PathfinderInstance null.", this); return; }
        DebugHelper.Log("--- GridTester: Testing GetReachableTiles Calc (Pathfinder) ---", this);
        // MODIFIED: Used capitalized PlayableWidth and PlayableHeight properties
        Vector2Int center_grt = new Vector2Int(GridManager.Instance.PlayableWidth/2, GridManager.Instance.PlayableHeight/2); 
        int range1_grt = logReachableTestRange; 

        if (GridManager.Instance.IsInPlayableBounds(center_grt)) { 
            if (unitForReachableTest != null && unitForReachableTest.Stats != null && unitForReachableTest.Movement != null) 
            {
                int actualRangeToTest = unitForReachableTest.Movement.CalculatedMoveRange > 0 ? unitForReachableTest.Movement.CalculatedMoveRange : range1_grt;
                List<Tile> reach1 = GridManager.Instance.PathfinderInstance.GetReachableTiles(center_grt, actualRangeToTest, unitForReachableTest);
                DebugHelper.Log($"Reachable (Pathfinder) from {center_grt} with {actualRangeToTest} move points for {unitForReachableTest.unitName}: Found {reach1.Count} tiles.", this); 
            }
            else
            {
                DebugHelper.LogWarning($"Reachable Calc Test: 'unitForReachableTest' not assigned or lacks Stats/Movement. Cannot perform unit-specific reachable test.", this);
            }
        } else DebugHelper.LogWarning($"Reachable Calc T1 OOB {center_grt}");
        DebugHelper.Log("--- GridTester: Finished Reachable Calc Test ---", this);
    }
}