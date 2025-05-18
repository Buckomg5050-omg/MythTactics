// GridManager.cs
using UnityEngine;
using System.Collections.Generic; // For List<T> and Dictionary

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    // MODIFIED: Removed direct dimension fields, they will come from MapDataSO
    // public int playableWidth = 25;
    // public int playableHeight = 25;

    // MODIFIED: Added currentMapData field
    [Header("Map Configuration")]
    [Tooltip("The MapDataSO asset defining the current map's layout and properties.")]
    public MapDataSO currentMapData;

    [Header("Tile Setup (Prefabs & Fallbacks)")]
    [Tooltip("The prefab to use for instantiating each tile.")]
    public GameObject tilePrefab;
    // MODIFIED: Default and boundary types will now ideally come from currentMapData,
    // but these can serve as project-wide fallbacks if MapDataSO fields are null (though MapDataSO should be complete).
    [Tooltip("Fallback default tile type if MapDataSO doesn't specify one (should not happen with proper MapDataSO).")]
    public TileTypeSO fallbackDefaultPlayableTileType;
    [Tooltip("Fallback boundary tile type if MapDataSO doesn't specify one (should not happen with proper MapDataSO).")]
    public TileTypeSO fallbackBoundaryTileType;

    [Tooltip("Transform to parent all instantiated tile GameObjects under. If null, a new container will be created.")]
    public Transform tileContainer;

    // MODIFIED: Removed specific terrain type fields here, as they should be defined as TileTypeSO assets
    // and assigned within the MapDataSO.
    // public TileTypeSO plainsTileType;
    // public TileTypeSO forestTileType;
    // public TileTypeSO hillsTileType;

    private Tile[,] _tiles; // Internal grid array including boundaries
    public Tile[,] AllTilesInternal => _tiles; // Access to the full grid array (including boundaries) for systems that need it.

    // MODIFIED: Public getters for dimensions, now sourced from currentMapData
    public int PlayableWidth => (currentMapData != null) ? currentMapData.playableWidth : 0;
    public int PlayableHeight => (currentMapData != null) ? currentMapData.playableHeight : 0;

    private int _totalWidth;  // Includes boundaries
    private int _totalHeight; // Includes boundaries

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

        InitializeGrid(); // Initialize grid on Awake
    }

    void InitializeGrid()
    {
        if (currentMapData == null)
        {
            Debug.LogError("[GridManager] CurrentMapData SO not assigned! Cannot initialize grid.", this);
            enabled = false; // Disable GridManager if no map data
            return;
        }
        if (tilePrefab == null) { DebugHelper.LogError("GridManager: TilePrefab not assigned!", this); enabled = false; return; }

        TileTypeSO boundaryTypeToUse = currentMapData.boundaryTile ?? fallbackBoundaryTileType;
        TileTypeSO defaultPlayableTypeToUse = currentMapData.defaultPlayableTile ?? fallbackDefaultPlayableTileType;

        if (boundaryTypeToUse == null) { DebugHelper.LogError("GridManager: No BoundaryTileType available (neither in MapDataSO nor as fallback)! Cannot initialize boundary.", this); enabled = false; return; }
        if (defaultPlayableTypeToUse == null) { DebugHelper.LogWarning("GridManager: No DefaultPlayableTileType available. Playable area might be incomplete if not all tiles are specified in MapDataSO.", this); }


        // Use dimensions from MapDataSO
        // playableWidth and playableHeight are now properties reading from currentMapData
        _totalWidth = PlayableWidth + 2;
        _totalHeight = PlayableHeight + 2;
        _tiles = new Tile[_totalWidth, _totalHeight];

        if (tileContainer == null)
        {
            GameObject containerGO = new GameObject("_TileContainer");
            tileContainer = containerGO.transform;
            tileContainer.SetParent(this.transform); // Parent to GridManager itself
        }
        else // Clear existing tiles if container is pre-assigned and has children
        {
            foreach (Transform child in tileContainer) { if (child != null) Destroy(child.gameObject); }
        }

        // Calculate offsets for centering the grid (optional, but common)
        // Assumes tile size is 1x1 world unit. Pivot at center of tile.
        float xGridWorldOffset = -(PlayableWidth / 2.0f); // Centers based on playable area
        float yGridWorldOffset = -(PlayableHeight / 2.0f);

        // Create a dictionary for quick lookup of specific tile data from MapDataSO
        Dictionary<Vector2Int, MapTileData> specificTileDict = new Dictionary<Vector2Int, MapTileData>();
        if (currentMapData.specificTiles != null)
        {
            foreach (var tileData in currentMapData.specificTiles)
            {
                if (!specificTileDict.ContainsKey(tileData.position))
                {
                    specificTileDict.Add(tileData.position, tileData);
                }
                else
                {
                    Debug.LogWarning($"[GridManager] Duplicate specific tile data for position {tileData.position} in MapData '{currentMapData.name}'. Using first entry found.");
                }
            }
        }

        for (int arrayX = 0; arrayX < _totalWidth; arrayX++) // Iterate using total dimensions (incl. boundary)
        {
            for (int arrayY = 0; arrayY < _totalHeight; arrayY++)
            {
                // Calculate world position for the tile's center
                // The +0.5f for x and y assumes tile pivots are at their bottom-left for this calculation, then adjusted by the offset.
                // If tile prefab pivot is center, the world pos calculation is simpler.
                // Let's assume tile prefab pivot is center for now.
                Vector3 worldPos = new Vector3(
                    (arrayX - 1) + xGridWorldOffset + 0.5f, // (arrayX-1) is playableX, then offset
                    (arrayY - 1) + yGridWorldOffset + 0.5f, // (arrayY-1) is playableY, then offset
                    0 // Assuming Z is 0 for tiles
                );


                GameObject tileGO = Instantiate(tilePrefab, worldPos, Quaternion.identity, tileContainer);
                Tile tileComp = tileGO.GetComponent<Tile>();

                if (tileComp != null)
                {
                    bool isBoundary = (arrayX == 0 || arrayX == _totalWidth - 1 || arrayY == 0 || arrayY == _totalHeight - 1);
                    Vector2Int playableCoords = new Vector2Int(arrayX - 1, arrayY - 1); // Convert array index to playable coords

                    TileTypeSO tileSOToUse = null;
                    int heightToUse = 0;

                    if (isBoundary)
                    {
                        tileSOToUse = boundaryTypeToUse;
                        // tileComp.name = $"Tile_Boundary_{playableCoords.x}_{playableCoords.y}"; // Name already set in Tile.Initialize
                    }
                    else // It's a playable tile
                    {
                        if (specificTileDict.TryGetValue(playableCoords, out MapTileData specificData))
                        {
                            tileSOToUse = specificData.tileType;
                            heightToUse = specificData.heightLevel;
                        }
                        else
                        {
                            tileSOToUse = defaultPlayableTypeToUse;
                            // heightToUse defaults to 0 if not specified
                        }
                    }

                    if (tileSOToUse != null)
                    {
                        tileComp.Initialize(playableCoords, tileSOToUse, heightToUse);
                    }
                    else
                    {
                        Debug.LogError($"GridManager: Could not determine TileTypeSO for tile at playable position {playableCoords} for map '{currentMapData.name}'. Using fallback or destroying.", this);
                        // If even defaultPlayableTypeToUse was null, we have an issue.
                        // As a last resort, could destroy or use a super-default error tile.
                        if (fallbackDefaultPlayableTileType != null) {
                            tileComp.Initialize(playableCoords, fallbackDefaultPlayableTileType, heightToUse);
                        } else {
                            Destroy(tileGO); continue;
                        }
                    }
                    _tiles[arrayX, arrayY] = tileComp;
                }
                else
                {
                    DebugHelper.LogError($"GridManager: TilePrefab '{tilePrefab.name}' is missing Tile component.", this);
                    Destroy(tileGO); // Clean up problematic instance
                }
            }
        }
        DebugHelper.Log($"GridManager: Initialized grid from MapDataSO '{currentMapData.name}' ({PlayableWidth}x{PlayableHeight} playable area).", this);

        _pathfinder = new Pathfinder(this);
        if (_pathfinder == null) // Should not happen if constructor doesn't throw
        {
            DebugHelper.LogError("GridManager: Failed to initialize Pathfinder instance!", this);
        }
    }

    public Tile GetTile(int playableX, int playableY)
    {
        if (IsInPlayableBounds(playableX, playableY))
        {
            return _tiles[playableX + 1, playableY + 1]; // Offset by 1 due to boundary
        }
        // Debug.LogWarning($"GetTile: Position ({playableX},{playableY}) is out of playable bounds.");
        return null;
    }
    public Tile GetTile(Vector2Int playableGridPos) { return GetTile(playableGridPos.x, playableGridPos.y); }

    public bool IsInPlayableBounds(int playableX, int playableY)
    {
        return playableX >= 0 && playableX < PlayableWidth && playableY >= 0 && playableY < PlayableHeight;
    }
    public bool IsInPlayableBounds(Vector2Int playableGridPos) { return IsInPlayableBounds(playableGridPos.x, playableGridPos.y); }

    public Vector3 GridToWorld(Vector2Int playableGridPos)
    {
        if (_tiles == null || currentMapData == null) { Debug.LogWarning("GridManager.GridToWorld: Grid not initialized or no map data.", this); return Vector3.zero; }
        
        // Consistent with InitializeGrid's world position calculation
        float xGridWorldOffset = -(PlayableWidth / 2.0f);
        float yGridWorldOffset = -(PlayableHeight / 2.0f);

        // playableGridPos.x is 0 to PlayableWidth-1.
        // Add 0.5f to center it on the tile, assuming tile origin is bottom-left for this calculation step.
        // Then apply the centering offset.
        return new Vector3(
            playableGridPos.x + xGridWorldOffset + 0.5f,
            playableGridPos.y + yGridWorldOffset + 0.5f,
            0
        );
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        if (_tiles == null || currentMapData == null) { Debug.LogWarning("GridManager.WorldToGrid: Grid not initialized or no map data.", this); return new Vector2Int(-999, -999); }

        float xGridWorldOffset = -(PlayableWidth / 2.0f);
        float yGridWorldOffset = -(PlayableHeight / 2.0f);

        // Reverse the GridToWorld logic
        // First, remove the centering offset and the +0.5f adjustment
        float relativeX = worldPos.x - xGridWorldOffset - 0.5f;
        float relativeY = worldPos.y - yGridWorldOffset - 0.5f;

        // Floor to get the grid coordinate
        int playableX = Mathf.FloorToInt(relativeX);
        int playableY = Mathf.FloorToInt(relativeY);

        // The result should be within playable bounds if the worldPos corresponds to a playable tile.
        // No need to check against _totalWidth here, IsInPlayableBounds does that.
        // if (!IsInPlayableBounds(playableX, playableY)) return new Vector2Int(-999,-999); // Out of playable map
        
        return new Vector2Int(playableX, playableY);
    }


    public List<Tile> GetNeighbors(Vector2Int playableGridPos, bool includeDiagonals = false)
    {
        List<Tile> neighbors = new List<Tile>();
        if (!IsInPlayableBounds(playableGridPos)) return neighbors; // Check against playable bounds

        Vector2Int[] directOffsets = {
            new Vector2Int(0, 1), new Vector2Int(1, 0),
            new Vector2Int(0, -1), new Vector2Int(-1, 0)
        };
        Vector2Int[] diagonalOffsets = {
            new Vector2Int(1, 1), new Vector2Int(1, -1),
            new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        foreach (Vector2Int offset in directOffsets)
        {
            Vector2Int neighborPlayablePos = playableGridPos + offset;
            // GetTile already handles bounds checking for playable area
            Tile tile = GetTile(neighborPlayablePos);
            if (tile != null) neighbors.Add(tile);
        }

        if (includeDiagonals)
        {
            foreach (Vector2Int offset in diagonalOffsets)
            {
                Vector2Int neighborPlayablePos = playableGridPos + offset;
                Tile tile = GetTile(neighborPlayablePos);
                if (tile != null) neighbors.Add(tile);
            }
        }
        return neighbors;
    }

    public List<Tile> GetTilesInRange(Vector2Int playableCenter, int range) // Manhattan range
    {
        List<Tile> tilesInRange = new List<Tile>();
        if (!IsInPlayableBounds(playableCenter) || range < 0) return tilesInRange;

        for (int x = playableCenter.x - range; x <= playableCenter.x + range; x++)
        {
            for (int y = playableCenter.y - range; y <= playableCenter.y + range; y++)
            {
                Vector2Int currentPosPlayable = new Vector2Int(x,y);
                if (IsInPlayableBounds(currentPosPlayable))
                {
                    if (CalculateManhattanDistance(playableCenter, currentPosPlayable) <= range)
                    {
                        Tile tile = GetTile(currentPosPlayable);
                        if (tile != null) tilesInRange.Add(tile);
                    }
                }
            }
        }
        return tilesInRange;
    }
    
    public int CalculateManhattanDistance(Vector2Int posA, Vector2Int posB)
    {
        return Mathf.Abs(posA.x - posB.x) + Mathf.Abs(posA.y - posB.y);
    }
}