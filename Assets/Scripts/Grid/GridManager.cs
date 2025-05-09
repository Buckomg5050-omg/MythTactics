// GridManager.cs
using UnityEngine;
using System.Collections.Generic; // For List<T>

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Dimensions")]
    public int playableWidth = 25;
    public int playableHeight = 25;

    [Header("Tile Setup")]
    public GameObject tilePrefab;
    public TileTypeSO defaultPlayableTileType;
    public TileTypeSO boundaryTileType;
    public Transform tileContainer;

    [Header("Specific Terrain Types")]
    public TileTypeSO plainsTileType;
    public TileTypeSO forestTileType;
    public TileTypeSO hillsTileType;

    private Tile[,] _tiles;
    public Tile[,] AllTiles => _tiles; // Read-only access to the raw array if needed

    private int _totalWidth;
    private int _totalHeight;

    // NEW: Pathfinder instance
    private Pathfinder _pathfinder;
    public Pathfinder PathfinderInstance => _pathfinder;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Duplicate GridManager instance found. Destroying this one.", this);
            Destroy(gameObject);
            return;
        }

        InitializeGrid();
    }

    void InitializeGrid()
    {
        if (tilePrefab == null) { DebugHelper.LogError("GridManager: TilePrefab not assigned!", this); return; }
        if (defaultPlayableTileType == null && plainsTileType == null) { DebugHelper.LogWarning("GridManager: Neither DefaultPlayableTileType nor PlainsTileType assigned.", this); }
        if (boundaryTileType == null) { DebugHelper.LogError("GridManager: BoundaryTileType not assigned!", this); return; }

        _totalWidth = playableWidth + 2;
        _totalHeight = playableHeight + 2;
        _tiles = new Tile[_totalWidth, _totalHeight];

        if (tileContainer == null)
        {
            GameObject containerGO = new GameObject("_TileContainer");
            tileContainer = containerGO.transform;
            tileContainer.SetParent(this.transform);
        }
        else
        {
            foreach (Transform child in tileContainer) { if (child != null) Destroy(child.gameObject); }
        }

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
                    Vector2Int logicalPos = new Vector2Int(x - 1, y - 1);
                    TileTypeSO currentSO = null;

                    if (isBoundary) { currentSO = boundaryTileType; }
                    else
                    {
                        // Example terrain placement - replace with your map loading logic eventually
                        if (forestTileType != null && logicalPos.x >= 5 && logicalPos.x < 8 && logicalPos.y >= 5 && logicalPos.y < 8) { currentSO = forestTileType; }
                        else if (hillsTileType != null && logicalPos.x >= 2 && logicalPos.x < 4 && logicalPos.y >= 2 && logicalPos.y < 4) { currentSO = hillsTileType; }
                        else if (plainsTileType != null) { currentSO = plainsTileType; }
                        else if (defaultPlayableTileType != null) { currentSO = defaultPlayableTileType; }
                        else { DebugHelper.LogError($"GridManager: No TileTypeSO for playable tile {logicalPos}.", this); Destroy(tileGO); continue; }
                    }
                    
                    if (currentSO != null) { tileComp.Initialize(logicalPos, currentSO, 0); }
                    else { DebugHelper.LogError($"GridManager: Tile {logicalPos} has null TileTypeSO.", this); Destroy(tileGO); continue; }
                    
                    _tiles[x, y] = tileComp;
                }
                else { DebugHelper.LogError($"GridManager: TilePrefab missing Tile component.", this); Destroy(tileGO); }
            }
        }
        DebugHelper.Log($"GridManager: Initialized {playableWidth}x{playableHeight} grid.", this);

        // NEW: Instantiate Pathfinder after grid is fully initialized
        _pathfinder = new Pathfinder(this);
        if (_pathfinder == null)
        {
            DebugHelper.LogError("GridManager: Failed to initialize Pathfinder!", this);
        }
    }

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
        if (_tiles == null) { DebugHelper.LogWarning("GridManager.GridToWorld: Grid not initialized.", this); return Vector3.zero; }
        int arrayX = playableGridPos.x + 1; int arrayY = playableGridPos.y + 1;
        float xGridOffset = -(_totalWidth / 2.0f) + 0.5f; float yGridOffset = -(_totalHeight / 2.0f) + 0.5f;
        return new Vector3(arrayX + xGridOffset, arrayY + yGridOffset, 0);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        if (_tiles == null) { DebugHelper.LogWarning("GridManager.WorldToGrid: Grid not initialized.", this); return new Vector2Int(-999,-999); }
        float xGridOffset = -(_totalWidth / 2.0f) + 0.5f; float yGridOffset = -(_totalHeight / 2.0f) + 0.5f;
        float gridOriginX = xGridOffset - 0.5f; float gridOriginY = yGridOffset - 0.5f;
        float relativeX = worldPos.x - gridOriginX; float relativeY = worldPos.y - gridOriginY;
        int arrayX = Mathf.FloorToInt(relativeX); int arrayY = Mathf.FloorToInt(relativeY);
        int playableX = arrayX - 1; int playableY = arrayY - 1;
        if (arrayX < 0 || arrayX >= _totalWidth || arrayY < 0 || arrayY >= _totalHeight) return new Vector2Int(-999,-999);
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
            Tile tile = GetTile(playableGridPos + offset);
            if (tile != null) neighbors.Add(tile);
        }
        if (includeDiagonals)
        {
            foreach (Vector2Int offset in diagonalOffsets)
            {
                Tile tile = GetTile(playableGridPos + offset);
                if (tile != null) neighbors.Add(tile);
            }
        }
        return neighbors;
    }

    public List<Tile> GetTilesInRange(Vector2Int playableCenter, int range)
    {
        List<Tile> tilesInRange = new List<Tile>();
        if (!IsInPlayableBounds(playableCenter) || range < 0) return tilesInRange;
        for (int x = playableCenter.x - range; x <= playableCenter.x + range; x++)
        {
            for (int y = playableCenter.y - range; y <= playableCenter.y + range; y++)
            {
                Vector2Int currentPosPlayable = new Vector2Int(x, y);
                if (IsInPlayableBounds(currentPosPlayable))
                {
                    int manhattanDistance = Mathf.Abs(currentPosPlayable.x - playableCenter.x) + Mathf.Abs(currentPosPlayable.y - playableCenter.y);
                    if (manhattanDistance <= range)
                    {
                        Tile tile = GetTile(currentPosPlayable);
                        if (tile != null) tilesInRange.Add(tile);
                    }
                }
            }
        }
        return tilesInRange;
    }
}