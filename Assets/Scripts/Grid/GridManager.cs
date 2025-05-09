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

    [Tooltip("Default TileTypeSO for the playable area if no map data is loaded (can be overridden by specific types below).")]
    public TileTypeSO defaultPlayableTileType; // Keep this as a fallback

    [Tooltip("TileTypeSO to use for the impassable boundary tiles.")]
    public TileTypeSO boundaryTileType;

    [Tooltip("Parent transform for instantiated tile GameObjects. Helps keep hierarchy clean.")]
    public Transform tileContainer;

    [Header("Specific Terrain Types")]
    [Tooltip("TileTypeSO for Plains terrain. If null, defaultPlayableTileType will be used more broadly.")]
    public TileTypeSO plainsTileType;

    [Tooltip("TileTypeSO for Forest terrain.")]
    public TileTypeSO forestTileType;

    [Tooltip("TileTypeSO for Hills terrain.")]
    public TileTypeSO hillsTileType;

    // Add more for other types as needed for initial testing (RockyGround, Swamp, etc.)
    // public TileTypeSO rockyGroundTileType;
    // public TileTypeSO swampTileType;

    private Tile[,] _tiles; // Stores all tiles including boundaries, using _totalWidth/_totalHeight
    public Tile[,] AllTiles => _tiles; // Exposes the full grid array if needed by other systems

    private int _totalWidth;  // playableWidth + 2
    private int _totalHeight; // playableHeight + 2

    void Start()
    {
        InitializeGrid();
    }

    void InitializeGrid()
    {
        if (tilePrefab == null) { DebugHelper.LogError("GridManager: TilePrefab not assigned!", this); return; }
        // defaultPlayableTileType is now more of a fallback if specific types aren't set or conditions aren't met.
        // It's good to ensure it's assigned, or handle nulls gracefully in the logic below.
        if (defaultPlayableTileType == null) { DebugHelper.LogWarning("GridManager: DefaultPlayableTileType not assigned. Ensure specific types cover all areas or provide a default.", this); }
        if (boundaryTileType == null) { DebugHelper.LogError("GridManager: BoundaryTileType not assigned!", this); return; }

        _totalWidth = playableWidth + 2;
        _totalHeight = playableHeight + 2;
        _tiles = new Tile[_totalWidth, _totalHeight];

        if (tileContainer == null)
        {
            GameObject containerGO = new GameObject("_TileContainer");
            tileContainer = containerGO.transform;
            tileContainer.SetParent(this.transform);
            DebugHelper.Log("GridManager: No TileContainer assigned. Created one automatically.", this);
        }
        else // Clear existing tiles if any (e.g., on editor recompile or scene reset)
        {
            foreach (Transform child in tileContainer)
            {
                if (child != null && child.gameObject != null) Destroy(child.gameObject);
            }
        }

        // Calculate offsets for centering the grid visually at world (0,0)
        // Assumes tile PPU is such that 1 Unity unit = 1 tile visual size
        float xOffset = -(_totalWidth / 2.0f) + 0.5f;
        float yOffset = -(_totalHeight / 2.0f) + 0.5f;

        for (int x = 0; x < _totalWidth; x++) // Iterate through the total grid array including boundaries
        {
            for (int y = 0; y < _totalHeight; y++)
            {
                Vector3 worldPos = new Vector3(x + xOffset, y + yOffset, 0);
                GameObject tileGO = Instantiate(tilePrefab, worldPos, Quaternion.identity, tileContainer);
                Tile tileComp = tileGO.GetComponent<Tile>();

                if (tileComp != null)
                {
                    // Determine if this tile is part of the outer boundary
                    bool isBoundary = (x == 0 || x == _totalWidth - 1 || y == 0 || y == _totalHeight - 1);
                    
                    // Logical position for playable area (0 to playableWidth-1, 0 to playableHeight-1)
                    // For boundary tiles, logicalPos will be outside this, which is fine for the logic below.
                    Vector2Int logicalPos = new Vector2Int(x - 1, y - 1); 
                    
                    TileTypeSO currentSO = null; // Initialize to null

                    if (isBoundary)
                    {
                        currentSO = boundaryTileType;
                        // For boundary tiles, their logicalPos in Tile.Initialize might be set to their array pos (x,y)
                        // or a special indicator. Current Tile.Initialize takes logicalPos.
                        // The name of the tile will reflect its boundary status.
                        tileComp.Initialize(new Vector2Int(x, y), currentSO, 0); // Use raw array coords for boundary tile IDs maybe?
                                                                                // Or stick to one system: tileComp.Initialize(logicalPos, currentSO, 0);
                                                                                // Let's stick to logicalPos for consistency, Initialize will name it.
                    }
                    else // This is a playable tile
                    {
                        // Example: Create a 3x3 Forest patch around (5,5) in playable coordinates
                        if (forestTileType != null && logicalPos.x >= 5 && logicalPos.x < 8 && logicalPos.y >= 5 && logicalPos.y < 8)
                        {
                            currentSO = forestTileType;
                        }
                        // Example: Create a small Hills patch
                        // else if (hillsTileType != null && logicalPos.x >= 2 && logicalPos.x < 4 && logicalPos.y >= 2 && logicalPos.y < 4)
                        // {
                        //    currentSO = hillsTileType;
                        // }
                        else if (plainsTileType != null) // Default to Plains if available for other playable tiles
                        {
                            currentSO = plainsTileType;
                        }
                        else if (defaultPlayableTileType != null) // Fallback to the original generic default
                        {
                            currentSO = defaultPlayableTileType;
                             DebugHelper.LogWarning($"GridManager: Using fallback defaultPlayableTileType for tile at {logicalPos}. Consider assigning plainsTileType or other specific types.", this);
                        }
                        else
                        {
                            // This case should ideally not be reached if boundary and at least one playable type (plains or default) is set.
                            DebugHelper.LogError($"GridManager: No suitable TileTypeSO found for playable tile at {logicalPos}. Assign plainsTileType or defaultPlayableTileType.", this);
                            // Optionally, destroy tileGO or assign a 'missing' TileTypeSO
                        }
                    }
                    
                    // Initialize the tile (Tile.Initialize expects playable coordinates for its gridPosition field if not boundary)
                    // The logicalPos calculated earlier (x-1, y-1) is correct for playable tiles.
                    // For boundary tiles, their 'gridPosition' in Tile.cs might be less relevant or could use their raw (x,y)
                    // For now, Tile.Initialize takes the logicalPos (which is -1,y or x,-1 etc. for boundaries)
                    // This is okay as IsInPlayableBounds uses 0 to playableWidth-1.
                    // Tile's name will reflect its assigned type.
                    if (currentSO != null)
                    {
                        tileComp.Initialize(logicalPos, currentSO, 0);
                    }
                    else
                    {
                        // If currentSO is still null (error case), initialize with placeholder or log error
                        DebugHelper.LogError($"GridManager: Tile at {logicalPos} ended up with a null TileTypeSO before Initialize. Destroying tile.", this);
                        Destroy(tileGO);
                        continue; // Skip adding to _tiles array
                    }
                    
                    _tiles[x, y] = tileComp; // Store in the main array using totalWidth/Height indices
                }
                else
                {
                    DebugHelper.LogError($"GridManager: TilePrefab '{tilePrefab.name}' is missing the Tile component. Tile at ({x},{y}) not created.", this);
                    Destroy(tileGO); // Cleanup
                }
            }
        }
        DebugHelper.Log($"GridManager: Initialized {playableWidth}x{playableHeight} grid with boundary. Total: {_totalWidth}x{_totalHeight}. Check for Forest patch around (5,5).", this);
    }

    // GetTile expects PLAYABLE coordinates
    public Tile GetTile(int playableX, int playableY)
    {
        if (IsInPlayableBounds(playableX, playableY))
        {
            // Convert playable coordinates to _tiles array indices (+1 for boundary offset)
            return _tiles[playableX + 1, playableY + 1];
        }
        // DebugHelper.LogWarning($"GetTile: Position ({playableX},{playableY}) is outside playable bounds.", this);
        return null;
    }
    public Tile GetTile(Vector2Int playableGridPos)
    {
        return GetTile(playableGridPos.x, playableGridPos.y);
    }

    // IsInPlayableBounds checks against PLAYABLE dimensions
    public bool IsInPlayableBounds(int playableX, int playableY)
    {
        return playableX >= 0 && playableX < playableWidth &&
               playableY >= 0 && playableY < playableHeight;
    }
    public bool IsInPlayableBounds(Vector2Int playableGridPos)
    {
        return IsInPlayableBounds(playableGridPos.x, playableGridPos.y);
    }

    // GridToWorld converts PLAYABLE coordinates to world position
    public Vector3 GridToWorld(Vector2Int playableGridPos)
    {
        if (_tiles == null) { DebugHelper.LogWarning("GridManager.GridToWorld: Grid not initialized (_tiles is null).", this); return Vector3.zero; }
        
        // Convert playable coordinates to _tiles array indices
        int arrayX = playableGridPos.x + 1;
        int arrayY = playableGridPos.y + 1;

        // Validate array indices against total dimensions (includes boundaries)
        if (arrayX < 0 || arrayX >= _totalWidth || arrayY < 0 || arrayY >= _totalHeight)
        {
            // This case should ideally not happen if IsInPlayableBounds was checked before,
            // but GridToWorld might be called with out-of-playable-bounds coords for other reasons.
            // For now, let's assume it's an error if it's outside the _tiles array completely.
            // However, the GDD implies GridToWorld should work for logical grid pos.
            // The current offset calculation assumes we're mapping a cell in the _tiles array.
            // DebugHelper.LogWarning($"GridManager.GridToWorld: Playable pos {playableGridPos} results in array index out of bounds [{arrayX},{arrayY}]. Total: {_totalWidth}x{_totalHeight}", this);
            // Decide behavior: return error, or extrapolate? Current logic centers on _totalWidth/_totalHeight.
        }

        float xGridOffset = -(_totalWidth / 2.0f) + 0.5f;
        float yGridOffset = -(_totalHeight / 2.0f) + 0.5f;
        
        // Calculate world position based on array indices and centering offset
        return new Vector3(arrayX + xGridOffset, arrayY + yGridOffset, 0);
    }

    // WorldToGrid converts world position to PLAYABLE coordinates
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        if (_tiles == null) { DebugHelper.LogWarning("GridManager.WorldToGrid: Grid not initialized (_tiles is null).", this); return new Vector2Int(-999,-999); } // Special value for error

        // Calculate the effective "corner" of the grid in world space (bottom-left of tile [0,0] in the _tiles array)
        float xGridOffset = -(_totalWidth / 2.0f) + 0.5f; // Center of the first tile in _tiles array (index 0)
        float yGridOffset = -(_totalHeight / 2.0f) + 0.5f;
        
        // World coordinate of the bottom-left corner of the tile at _tiles[0,0]
        float gridOriginX = xGridOffset - 0.5f; 
        float gridOriginY = yGridOffset - 0.5f;

        // Calculate position relative to this grid origin
        float relativeX = worldPos.x - gridOriginX;
        float relativeY = worldPos.y - gridOriginY;

        // Determine array indices by flooring (assuming 1 unit per tile)
        int arrayX = Mathf.FloorToInt(relativeX);
        int arrayY = Mathf.FloorToInt(relativeY);

        // Convert array indices (which include boundary) to playable coordinates
        int playableX = arrayX - 1;
        int playableY = arrayY - 1;

        // Check if these playable coordinates are valid
        // if (!IsInPlayableBounds(playableX, playableY))
        // {
            // If not in playable bounds, could return a special value or clamp,
            // or indicate it's a boundary tile if arrayX/Y point to one.
            // For now, if it's outside *playable*, we return an indicator.
            // The GDD says WorldToGrid should account for centered origin and return playable coords.
            // If it falls on a boundary tile, it should probably still resolve to that boundary cell's "playable equivalent" if we had one,
            // or an "out of playable bounds" indicator. The current IsInPlayableBounds check is sufficient.
        //    return new Vector2Int(-998,-998); // Special value for out-of-playable-bounds but within total grid
        // }
        // If it's outside the _tiles array entirely:
        if (arrayX < 0 || arrayX >= _totalWidth || arrayY < 0 || arrayY >= _totalHeight) {
             // DebugHelper.LogWarning($"WorldToGrid: World pos {worldPos} is completely outside the grid area. Array indices [{arrayX},{arrayY}]", this);
            return new Vector2Int(-999,-999); // Fully out of grid
        }

        return new Vector2Int(playableX, playableY); // This might be -1,y or x,-1 etc. if it's a boundary.
                                                    // IsInPlayableBounds should be used by caller to confirm it's playable.
    }


    public List<Tile> GetNeighbors(Vector2Int playableGridPos, bool includeDiagonals = false)
    {
        List<Tile> neighbors = new List<Tile>();
        if (!IsInPlayableBounds(playableGridPos))
        {
            // DebugHelper.LogWarning($"GetNeighbors: Position {playableGridPos} is outside playable bounds.", this);
            return neighbors; // Return empty list if start pos is invalid
        }

        Vector2Int[] directOffsets = {
            new Vector2Int(0, 1),  // North
            new Vector2Int(0, -1), // South
            new Vector2Int(1, 0),  // East
            new Vector2Int(-1, 0)  // West
        };

        Vector2Int[] diagonalOffsets = {
            new Vector2Int(1, 1),   // North-East
            new Vector2Int(1, -1),  // South-East
            new Vector2Int(-1, 1),  // North-West
            new Vector2Int(-1, -1)  // South-West
        };

        foreach (Vector2Int offset in directOffsets)
        {
            Vector2Int neighborPosPlayable = playableGridPos + offset;
            // GetTile will internally check IsInPlayableBounds and return null if outside.
            Tile tile = GetTile(neighborPosPlayable);
            if (tile != null) // Ensures tile is within playable bounds and exists
            {
                neighbors.Add(tile);
            }
        }

        if (includeDiagonals)
        {
            foreach (Vector2Int offset in diagonalOffsets)
            {
                Vector2Int neighborPosPlayable = playableGridPos + offset;
                Tile tile = GetTile(neighborPosPlayable);
                if (tile != null)
                {
                    neighbors.Add(tile);
                }
            }
        }
        return neighbors;
    }

    public List<Tile> GetTilesInRange(Vector2Int playableCenter, int range)
    {
        List<Tile> tilesInRange = new List<Tile>();
        if (!IsInPlayableBounds(playableCenter) || range < 0)
        {
            // DebugHelper.LogWarning($"GetTilesInRange: Center {playableCenter} or range {range} is invalid.", this);
            return tilesInRange;
        }

        for (int x = playableCenter.x - range; x <= playableCenter.x + range; x++)
        {
            for (int y = playableCenter.y - range; y <= playableCenter.y + range; y++)
            {
                Vector2Int currentPosPlayable = new Vector2Int(x, y);
                // No need to check IsInPlayableBounds here if GetTile does it, but it's safer/clearer.
                if (IsInPlayableBounds(currentPosPlayable))
                {
                    int manhattanDistance = Mathf.Abs(currentPosPlayable.x - playableCenter.x) +
                                            Mathf.Abs(currentPosPlayable.y - playableCenter.y);
                    if (manhattanDistance <= range)
                    {
                        Tile tile = GetTile(currentPosPlayable);
                        if (tile != null) // Should always be true if IsInPlayableBounds passed
                        {
                            tilesInRange.Add(tile);
                        }
                    }
                }
            }
        }
        return tilesInRange;
    }
}