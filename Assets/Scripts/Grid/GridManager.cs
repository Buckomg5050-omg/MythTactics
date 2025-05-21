// GridManager.cs
using UnityEngine;
using System.Collections.Generic; // For List<T> and Dictionary

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Map Configuration")]
    [Tooltip("The MapDataSO asset defining the current map's layout and properties.")]
    public MapDataSO currentMapData;

    [Header("Tile Setup (Prefabs & Fallbacks)")]
    [Tooltip("The prefab to use for instantiating each tile.")]
    public GameObject tilePrefab;
    [Tooltip("Fallback default tile type if MapDataSO doesn't specify one (should not happen with proper MapDataSO).")]
    public TileTypeSO fallbackDefaultPlayableTileType;
    [Tooltip("Fallback boundary tile type if MapDataSO doesn't specify one (should not happen with proper MapDataSO).")]
    public TileTypeSO fallbackBoundaryTileType;

    [Tooltip("Transform to parent all instantiated tile GameObjects under. If null, a new container will be created.")]
    public Transform tileContainer;

    private Tile[,] _tiles; // Internal grid array including boundaries
    public Tile[,] AllTilesInternal => _tiles;

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

        InitializeGrid();
    }

    void InitializeGrid()
    {
        if (currentMapData == null)
        {
            Debug.LogError("[GridManager] CurrentMapData SO not assigned! Cannot initialize grid.", this);
            enabled = false;
            return;
        }
        if (tilePrefab == null) { Debug.LogError("GridManager: TilePrefab not assigned!", this); enabled = false; return; } // Changed to Debug.LogError

        TileTypeSO boundaryTypeToUse = currentMapData.boundaryTile ?? fallbackBoundaryTileType;
        TileTypeSO defaultPlayableTypeToUse = currentMapData.defaultPlayableTile ?? fallbackDefaultPlayableTileType;

        if (boundaryTypeToUse == null) { Debug.LogError("GridManager: No BoundaryTileType available (neither in MapDataSO nor as fallback)! Cannot initialize boundary.", this); enabled = false; return; } // Changed to Debug.LogError
        if (defaultPlayableTypeToUse == null) { Debug.LogWarning("GridManager: No DefaultPlayableTileType available. Playable area might be incomplete if not all tiles are specified in MapDataSO.", this); }


        _totalWidth = PlayableWidth + 2;
        _totalHeight = PlayableHeight + 2;
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

        float xGridWorldOffset = -(PlayableWidth / 2.0f);
        float yGridWorldOffset = -(PlayableHeight / 2.0f);

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

        for (int arrayX = 0; arrayX < _totalWidth; arrayX++)
        {
            for (int arrayY = 0; arrayY < _totalHeight; arrayY++)
            {
                Vector3 worldPos = new Vector3(
                    (arrayX - 1) + xGridWorldOffset + 0.5f,
                    (arrayY - 1) + yGridWorldOffset + 0.5f,
                    0
                );

                GameObject tileGO = Instantiate(tilePrefab, worldPos, Quaternion.identity, tileContainer);
                Tile tileComp = tileGO.GetComponent<Tile>();

                if (tileComp != null)
                {
                    bool isBoundary = (arrayX == 0 || arrayX == _totalWidth - 1 || arrayY == 0 || arrayY == _totalHeight - 1);
                    Vector2Int playableCoords = new Vector2Int(arrayX - 1, arrayY - 1);

                    TileTypeSO tileSOToUse = null;
                    int heightToUse = 0;

                    if (isBoundary)
                    {
                        tileSOToUse = boundaryTypeToUse;
                    }
                    else
                    {
                        if (specificTileDict.TryGetValue(playableCoords, out MapTileData specificData))
                        {
                            tileSOToUse = specificData.tileType;
                            heightToUse = specificData.heightLevel;
                        }
                        else
                        {
                            tileSOToUse = defaultPlayableTypeToUse;
                        }
                    }

                    if (tileSOToUse != null)
                    {
                        tileComp.Initialize(playableCoords, tileSOToUse, heightToUse);
                    }
                    else
                    {
                        Debug.LogError($"GridManager: Could not determine TileTypeSO for tile at playable position {playableCoords} for map '{currentMapData.name}'. Using fallback or destroying.", this);
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
                    Debug.LogError($"GridManager: TilePrefab '{tilePrefab.name}' is missing Tile component.", this); // Changed to Debug.LogError
                    Destroy(tileGO);
                }
            }
        }
        Debug.Log($"GridManager: Initialized grid from MapDataSO '{currentMapData.name}' ({PlayableWidth}x{PlayableHeight} playable area).", this); // Changed from DebugHelper.Log

        _pathfinder = new Pathfinder(this);
        if (_pathfinder == null)
        {
            Debug.LogError("GridManager: Failed to initialize Pathfinder instance!", this); // Changed from DebugHelper.Log
        }
    }

    public Tile GetTile(int playableX, int playableY)
    {
        if (IsInPlayableBounds(playableX, playableY))
        {
            return _tiles[playableX + 1, playableY + 1];
        }
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
        
        float xGridWorldOffset = -(PlayableWidth / 2.0f);
        float yGridWorldOffset = -(PlayableHeight / 2.0f);

        return new Vector3(
            playableGridPos.x + xGridWorldOffset + 0.5f,
            playableGridPos.y + yGridWorldOffset + 0.5f,
            0
        );
    }

    // MODIFIED WorldToGrid method
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        if (_tiles == null || currentMapData == null) { Debug.LogWarning("GridManager.WorldToGrid: Grid not initialized or no map data.", this); return new Vector2Int(-999, -999); }

        float xGridWorldOriginOffset = -(PlayableWidth / 2.0f); 
        float yGridWorldOriginOffset = -(PlayableHeight / 2.0f);

        float relativeXToGridOrigin = worldPos.x - xGridWorldOriginOffset;
        float relativeYToGridOrigin = worldPos.y - yGridWorldOriginOffset;

        int playableX = Mathf.FloorToInt(relativeXToGridOrigin);
        int playableY = Mathf.FloorToInt(relativeYToGridOrigin);
        
        return new Vector2Int(playableX, playableY);
    }
    // END MODIFIED WorldToGrid method

    public List<Tile> GetNeighbors(Vector2Int playableGridPos, bool includeDiagonals = false)
    {
        List<Tile> neighbors = new List<Tile>();
        if (!IsInPlayableBounds(playableGridPos)) return neighbors;

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

    public List<Tile> GetTilesInRange(Vector2Int playableCenter, int range)
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