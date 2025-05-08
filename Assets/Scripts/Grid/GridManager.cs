// GridManager.cs
using UnityEngine;
using System.Collections.Generic; // For List<T>

public class GridManager : MonoBehaviour
{
    [Header("Grid Dimensions")]
    [Tooltip("The playable width of the grid (e.g., 25 for a 25xY grid).")]
    public int playableWidth = 25;

    [Tooltip("The playable height of the grid (e.g., 25 for an Xx25 grid).")]
    public int playableHeight = 25;

    [Header("Tile Setup")]
    [Tooltip("The prefab to use for instantiating all tiles.")]
    public GameObject tilePrefab;

    [Tooltip("Default TileTypeSO for the playable area if no map data is loaded.")]
    public TileTypeSO defaultPlayableTileType;

    [Tooltip("TileTypeSO to use for the impassable boundary tiles.")]
    public TileTypeSO boundaryTileType;

    [Tooltip("Parent transform for instantiated tile GameObjects. Helps keep hierarchy clean.")]
    public Transform tileContainer;

    private Tile[,] _tiles;
    public Tile[,] AllTiles => _tiles;

    private int _totalWidth;
    private int _totalHeight;

    void Start()
    {
        InitializeGrid();

        // --- Test GridToWorld ---
        DebugHelper.Log("--- Testing GridToWorld ---", this);
        Vector2Int testPlayablePos_g2w_1 = new Vector2Int(0, 0);
        Vector3 worldPos_g2w_1 = GridToWorld(testPlayablePos_g2w_1);
        DebugHelper.Log($"GridToWorld: Playable tile {testPlayablePos_g2w_1} is at world position {worldPos_g2w_1}", this);
        Tile actualTile_g2w_1 = GetTile(testPlayablePos_g2w_1);
        if (actualTile_g2w_1 != null) DebugHelper.Log($"GridToWorld: Actual tile at {testPlayablePos_g2w_1} transform is {actualTile_g2w_1.transform.position}", this);
        Vector2Int testPlayablePos_g2w_2 = new Vector2Int(playableWidth - 1, playableHeight - 1);
        Vector3 worldPos_g2w_2 = GridToWorld(testPlayablePos_g2w_2);
        DebugHelper.Log($"GridToWorld: Playable tile {testPlayablePos_g2w_2} is at world position {worldPos_g2w_2}", this);
        Tile actualTile_g2w_2 = GetTile(testPlayablePos_g2w_2);
        if (actualTile_g2w_2 != null) DebugHelper.Log($"GridToWorld: Actual tile at {testPlayablePos_g2w_2} transform is {actualTile_g2w_2.transform.position}", this);
        DebugHelper.Log("--- Finished GridToWorld Test ---", this);

        // --- Test WorldToGrid ---
        DebugHelper.Log("--- Testing WorldToGrid ---", this);
        Tile tileForTest1_w2g = GetTile(0,0);
        if (tileForTest1_w2g != null)
        {
            Vector3 testWorldPos1 = tileForTest1_w2g.transform.position;
            Vector2Int gridResult1 = WorldToGrid(testWorldPos1);
            DebugHelper.Log($"WorldToGrid: World pos {testWorldPos1} (center of tile (0,0)) -> Grid {gridResult1}", this);
        } else { DebugHelper.LogWarning("WorldToGrid Test Case 1: Tile (0,0) not found.", this); }
        Tile tileForTest2_w2g = GetTile(0,0);
        if (tileForTest2_w2g != null)
        {
            Vector3 testWorldPos2 = tileForTest2_w2g.transform.position + new Vector3(0.3f, -0.2f, 0f);
            Vector2Int gridResult2 = WorldToGrid(testWorldPos2);
            DebugHelper.Log($"WorldToGrid: World pos {testWorldPos2} (offset within tile (0,0)) -> Grid {gridResult2}", this);
        } else { DebugHelper.LogWarning("WorldToGrid Test Case 2: Tile (0,0) not found.", this); }
        if (_tiles != null && _totalWidth > 0 && _totalHeight > 0)
        {
            float xGridOffset_test = -(_totalWidth / 2.0f) + 0.5f;
            float yGridOffset_test = -(_totalHeight / 2.0f) + 0.5f;
            Vector3 boundaryWorldPos = new Vector3(0 + xGridOffset_test, 0 + yGridOffset_test, 0);
            Vector2Int gridResult3 = WorldToGrid(boundaryWorldPos);
            DebugHelper.Log($"WorldToGrid: World pos {boundaryWorldPos} (center of boundary _tiles[0,0]) -> Grid {gridResult3}", this);
        } else { DebugHelper.LogWarning("WorldToGrid Test Case 3: Grid not initialized.", this); }
        Vector3 outsideWorldPos_w2g = new Vector3(100f, 100f, 0f);
        Vector2Int gridResult4_w2g = WorldToGrid(outsideWorldPos_w2g);
        DebugHelper.Log($"WorldToGrid: World pos {outsideWorldPos_w2g} (far outside grid) -> Grid {gridResult4_w2g}", this);
        DebugHelper.Log("--- Finished WorldToGrid Test ---", this);

        // --- Test GetNeighbors ---
        DebugHelper.Log("--- Testing GetNeighbors ---", this);
        Vector2Int centerPos_gn = new Vector2Int(playableWidth / 2, playableHeight / 2);
        if (IsInPlayableBounds(centerPos_gn))
        {
            DebugHelper.Log($"Getting neighbors for {centerPos_gn} (4-directional):", this);
            List<Tile> neighbors4Dir = GetNeighbors(centerPos_gn);
            foreach (Tile neighbor in neighbors4Dir) DebugHelper.Log($"- GN4 Neighbor at {neighbor.gridPosition} ({neighbor.currentTerrainType})", this);
            DebugHelper.Log($"Getting neighbors for {centerPos_gn} (8-directional):", this);
            List<Tile> neighbors8Dir = GetNeighbors(centerPos_gn, true);
            foreach (Tile neighbor in neighbors8Dir) DebugHelper.Log($"- GN8 Neighbor at {neighbor.gridPosition} ({neighbor.currentTerrainType})", this);
        } else { DebugHelper.LogWarning($"GetNeighbors Test Case 1: Center pos {centerPos_gn} out of bounds.", this); }
        Vector2Int cornerPos_gn = new Vector2Int(0,0);
        if (IsInPlayableBounds(cornerPos_gn))
        {
            DebugHelper.Log($"Getting neighbors for {cornerPos_gn} (4-directional):", this);
            List<Tile> cornerNeighbors4Dir = GetNeighbors(cornerPos_gn);
            foreach (Tile neighbor in cornerNeighbors4Dir) DebugHelper.Log($"- GN4C Neighbor at {neighbor.gridPosition} ({neighbor.currentTerrainType})", this);
        } else { DebugHelper.LogWarning($"GetNeighbors Test Case 2: Corner pos {cornerPos_gn} out of bounds.", this); }
        Vector2Int edgePos_gn = new Vector2Int(0, playableHeight / 2);
         if (IsInPlayableBounds(edgePos_gn))
        {
            DebugHelper.Log($"Getting neighbors for {edgePos_gn} (8-directional):", this);
            List<Tile> edgeNeighbors8Dir = GetNeighbors(edgePos_gn, true);
            foreach (Tile neighbor in edgeNeighbors8Dir) DebugHelper.Log($"- GN8E Neighbor at {neighbor.gridPosition} ({neighbor.currentTerrainType})", this);
        } else { DebugHelper.LogWarning($"GetNeighbors Test Case 3: Edge pos {edgePos_gn} out of bounds.", this); }
        DebugHelper.Log("--- Finished GetNeighbors Test ---", this);

        // --- Test GetTilesInRange ---
        DebugHelper.Log("--- Testing GetTilesInRange ---", this);
        Vector2Int centerPos_gtir = new Vector2Int(playableWidth / 2, playableHeight / 2); // e.g., (12,12)
        int testRange = 2;
        if(IsInPlayableBounds(centerPos_gtir))
        {
            DebugHelper.Log($"Getting tiles in range {testRange} from {centerPos_gtir}:", this);
            List<Tile> tilesInRange = GetTilesInRange(centerPos_gtir, testRange);
            DebugHelper.Log($"Found {tilesInRange.Count} tiles in range {testRange} from {centerPos_gtir}:", this);
            foreach (Tile tile in tilesInRange)
            {
                int dist = Mathf.Abs(tile.gridPosition.x - centerPos_gtir.x) + Mathf.Abs(tile.gridPosition.y - centerPos_gtir.y);
                DebugHelper.Log($"- Tile at {tile.gridPosition}, Dist: {dist} ({tile.currentTerrainType})", this);
            }
        } else { DebugHelper.LogWarning($"GetTilesInRange Test Case 1: Center pos {centerPos_gtir} out of bounds.", this); }

        Vector2Int cornerPos_gtir = new Vector2Int(0,0);
        testRange = 1;
        if(IsInPlayableBounds(cornerPos_gtir))
        {
            DebugHelper.Log($"Getting tiles in range {testRange} from {cornerPos_gtir}:", this);
            List<Tile> tilesInRangeCorner = GetTilesInRange(cornerPos_gtir, testRange);
            DebugHelper.Log($"Found {tilesInRangeCorner.Count} tiles in range {testRange} from {cornerPos_gtir}:", this);
            foreach (Tile tile in tilesInRangeCorner)
            {
                int dist = Mathf.Abs(tile.gridPosition.x - cornerPos_gtir.x) + Mathf.Abs(tile.gridPosition.y - cornerPos_gtir.y);
                DebugHelper.Log($"- Tile at {tile.gridPosition}, Dist: {dist} ({tile.currentTerrainType})", this);
            }
        } else { DebugHelper.LogWarning($"GetTilesInRange Test Case 2: Corner pos {cornerPos_gtir} out of bounds.", this); }
        DebugHelper.Log("--- Finished GetTilesInRange Test ---", this);

    } // End of Start()

    void InitializeGrid()
    {
        if (tilePrefab == null) { DebugHelper.LogError("TilePrefab not assigned!", this); return; }
        if (defaultPlayableTileType == null) { DebugHelper.LogError("DefaultPlayableTileType not assigned!", this); return; }
        if (boundaryTileType == null) { DebugHelper.LogError("BoundaryTileType not assigned!", this); return; }

        _totalWidth = playableWidth + 2;
        _totalHeight = playableHeight + 2;
        _tiles = new Tile[_totalWidth, _totalHeight];

        if (tileContainer == null)
        {
            GameObject containerGO = new GameObject("_TileContainer");
            tileContainer = containerGO.transform;
            tileContainer.SetParent(this.transform);
            DebugHelper.Log("No TileContainer assigned. Created one automatically.", this);
        }
        else { foreach (Transform child in tileContainer) Destroy(child.gameObject); }

        float xOffset = -(_totalWidth / 2.0f) + 0.5f;
        float yOffset = -(_totalHeight / 2.0f) + 0.5f;

        for (int x = 0; x < _totalWidth; x++)
        {
            for (int y = 0; y < _totalHeight; y++)
            {
                Vector3 worldPos = new Vector3(x + xOffset, y + yOffset, 0);
                GameObject tileGO = Instantiate(tilePrefab, worldPos, Quaternion.identity, tileContainer);
                Tile tileComp = tileGO.GetComponent<Tile>();

                if (tileComp != null)
                {
                    TileTypeSO currentSO = (x == 0 || x == _totalWidth - 1 || y == 0 || y == _totalHeight - 1) ? boundaryTileType : defaultPlayableTileType;
                    Vector2Int logicalPos = (currentSO == boundaryTileType) ? new Vector2Int(x, y) : new Vector2Int(x - 1, y - 1);
                    tileComp.Initialize(logicalPos, currentSO, 0);
                    _tiles[x, y] = tileComp;
                }
                else { DebugHelper.LogError($"TilePrefab missing Tile component on {tileGO.name}!", this); Destroy(tileGO); }
            }
        }
        DebugHelper.Log($"Initialized {playableWidth}x{playableHeight} grid with boundary. Total: {_totalWidth}x{_totalHeight}.", this);
    } // End of InitializeGrid()

    public Tile GetTile(int playableX, int playableY)
    {
        if (IsInPlayableBounds(playableX, playableY)) return _tiles[playableX + 1, playableY + 1];
        return null;
    }
    public Tile GetTile(Vector2Int playableGridPos) { return GetTile(playableGridPos.x, playableGridPos.y); }

    public bool IsInPlayableBounds(int playableX, int playableY)
    {
        return playableX >= 0 && playableX < playableWidth && playableY >= 0 && playableY < playableHeight;
    }
    public bool IsInPlayableBounds(Vector2Int playableGridPos) { return IsInPlayableBounds(playableGridPos.x, playableGridPos.y); }

    public Vector3 GridToWorld(Vector2Int playableGridPos)
    {
        if (_tiles == null) { DebugHelper.LogWarning("GridToWorld: Grid not initialized.", this); return Vector3.zero; }
        int arrayX = playableGridPos.x + 1; int arrayY = playableGridPos.y + 1;
        float xGridOffset = -(_totalWidth / 2.0f) + 0.5f; float yGridOffset = -(_totalHeight / 2.0f) + 0.5f;
        return new Vector3(arrayX + xGridOffset, arrayY + yGridOffset, 0);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        if (_tiles == null) { DebugHelper.LogWarning("WorldToGrid: Grid not initialized.", this); return new Vector2Int(-999,-999); }
        float xGridOffset = -(_totalWidth / 2.0f) + 0.5f; float yGridOffset = -(_totalHeight / 2.0f) + 0.5f;
        float cornerX = xGridOffset - 0.5f; float cornerY = yGridOffset - 0.5f;
        float relativeX = worldPos.x - cornerX; float relativeY = worldPos.y - cornerY;
        int arrayX = Mathf.FloorToInt(relativeX); int arrayY = Mathf.FloorToInt(relativeY);

        if (arrayX < 0 || arrayX >= _totalWidth || arrayY < 0 || arrayY >= _totalHeight) return new Vector2Int(-999,-999);
        int playableX = arrayX - 1; int playableY = arrayY - 1;
        if (!IsInPlayableBounds(playableX, playableY)) return new Vector2Int(-998,-998);
        return new Vector2Int(playableX, playableY);
    }

    public List<Tile> GetNeighbors(Vector2Int playableGridPos, bool includeDiagonals = false)
    {
        List<Tile> neighbors = new List<Tile>();
        if (!IsInPlayableBounds(playableGridPos)) return neighbors;

        Vector2Int[] directOffsets = { new Vector2Int(0,1), new Vector2Int(0,-1), new Vector2Int(1,0), new Vector2Int(-1,0) };
        Vector2Int[] diagonalOffsets = { new Vector2Int(1,1), new Vector2Int(1,-1), new Vector2Int(-1,1), new Vector2Int(-1,-1) };

        foreach (Vector2Int offset in directOffsets)
        {
            Vector2Int neighborPos = playableGridPos + offset;
            if (IsInPlayableBounds(neighborPos)) { Tile tile = GetTile(neighborPos); if (tile != null) neighbors.Add(tile); }
        }
        if (includeDiagonals)
        {
            foreach (Vector2Int offset in diagonalOffsets)
            {
                Vector2Int neighborPos = playableGridPos + offset;
                if (IsInPlayableBounds(neighborPos)) { Tile tile = GetTile(neighborPos); if (tile != null) neighbors.Add(tile); }
            }
        }
        return neighbors;
    } // End of GetNeighbors()

    /// <summary>
    /// Gets all playable tiles within a given Manhattan distance (range) from a center point.
    /// As per GDD 5.1.1. Range is inclusive.
    /// </summary>
    /// <param name="playableCenter">The 0-indexed playable grid coordinate of the center point.</param>
    /// <param name="range">The Manhattan distance range (e.g., range 1 includes immediate neighbors, range 0 includes only the center tile).</param>
    /// <returns>A List of Tile objects within the specified range. Returns an empty list if center is invalid or range is negative.</returns>
    public List<Tile> GetTilesInRange(Vector2Int playableCenter, int range)
    {
        List<Tile> tilesInRange = new List<Tile>();

        if (!IsInPlayableBounds(playableCenter) || range < 0)
        {
            // DebugHelper.LogWarning($"GetTilesInRange called with invalid center {playableCenter} or range {range}.", this);
            return tilesInRange;
        }

        // Iterate over a square area defined by the range
        // Min/Max X: playableCenter.x - range TO playableCenter.x + range
        // Min/Max Y: playableCenter.y - range TO playableCenter.y + range
        for (int x = playableCenter.x - range; x <= playableCenter.x + range; x++)
        {
            for (int y = playableCenter.y - range; y <= playableCenter.y + range; y++)
            {
                Vector2Int currentPos = new Vector2Int(x, y);

                // Only consider tiles within playable bounds
                if (IsInPlayableBounds(currentPos))
                {
                    // Calculate Manhattan distance
                    int manhattanDistance = Mathf.Abs(currentPos.x - playableCenter.x) + Mathf.Abs(currentPos.y - playableCenter.y);

                    if (manhattanDistance <= range)
                    {
                        Tile tile = GetTile(currentPos);
                        if (tile != null) // Should always be true if IsInPlayableBounds passed
                        {
                            tilesInRange.Add(tile);
                        }
                    }
                }
            }
        }
        return tilesInRange;
    } // End of GetTilesInRange()

    // TODO GDD 5.1.1:
    // - Load map data (JSON)
    // - Event System (OnTileDataChanged, OnGridReset)

} // End of GridManager class