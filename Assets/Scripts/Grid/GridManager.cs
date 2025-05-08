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
    public Tile[,] AllTiles => _tiles; // Provides access to the full tile array, including boundaries

    // These are kept private as they are implementation details for centering and array sizing.
    // Other classes should use playableWidth/Height or query tiles.
    private int _totalWidth;
    private int _totalHeight;

    void Start()
    {
        InitializeGrid();
    } // End of Start()

    void InitializeGrid()
    {
        if (tilePrefab == null) { DebugHelper.LogError("TilePrefab not assigned! Cannot initialize grid.", this); return; }
        if (defaultPlayableTileType == null) { DebugHelper.LogError("DefaultPlayableTileType not assigned! Cannot initialize grid.", this); return; }
        if (boundaryTileType == null) { DebugHelper.LogError("BoundaryTileType not assigned! Cannot initialize grid.", this); return; }

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
        else { foreach (Transform child in tileContainer) Destroy(child.gameObject); } // Clear previous tiles

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
                    bool isBoundary = (x == 0 || x == _totalWidth - 1 || y == 0 || y == _totalHeight - 1);
                    TileTypeSO currentSO = isBoundary ? boundaryTileType : defaultPlayableTileType;
                    Vector2Int logicalPos = isBoundary ? new Vector2Int(x, y) : new Vector2Int(x - 1, y - 1);
                    
                    tileComp.Initialize(logicalPos, currentSO, 0);
                    _tiles[x, y] = tileComp;
                }
                else { DebugHelper.LogError($"TilePrefab is missing the Tile component on {tileGO.name}! Destroying object.", this); Destroy(tileGO); }
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

    public List<Tile> GetTilesInRange(Vector2Int playableCenter, int range)
    {
        List<Tile> tilesInRange = new List<Tile>();
        if (!IsInPlayableBounds(playableCenter) || range < 0) return tilesInRange;

        for (int x = playableCenter.x - range; x <= playableCenter.x + range; x++)
        {
            for (int y = playableCenter.y - range; y <= playableCenter.y + range; y++)
            {
                Vector2Int currentPos = new Vector2Int(x, y);
                if (IsInPlayableBounds(currentPos))
                {
                    int manhattanDistance = Mathf.Abs(currentPos.x - playableCenter.x) + Mathf.Abs(currentPos.y - playableCenter.y);
                    if (manhattanDistance <= range)
                    {
                        Tile tile = GetTile(currentPos);
                        if (tile != null) tilesInRange.Add(tile);
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