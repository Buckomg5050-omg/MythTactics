// GridManager.cs
using UnityEngine;

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

    private Tile[,] _tiles; // This should now correctly find the Tile type from Tile.cs
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
        if (actualTile_g2w_1 != null)
        {
            DebugHelper.Log($"GridToWorld: Actual tile at {testPlayablePos_g2w_1} transform is {actualTile_g2w_1.transform.position}", this);
        }

        Vector2Int testPlayablePos_g2w_2 = new Vector2Int(playableWidth - 1, playableHeight - 1);
        Vector3 worldPos_g2w_2 = GridToWorld(testPlayablePos_g2w_2);
        DebugHelper.Log($"GridToWorld: Playable tile {testPlayablePos_g2w_2} is at world position {worldPos_g2w_2}", this);

        Tile actualTile_g2w_2 = GetTile(testPlayablePos_g2w_2);
        if (actualTile_g2w_2 != null)
        {
            DebugHelper.Log($"GridToWorld: Actual tile at {testPlayablePos_g2w_2} transform is {actualTile_g2w_2.transform.position}", this);
        }
        DebugHelper.Log("--- Finished GridToWorld Test ---", this);


        // --- Test WorldToGrid ---
        DebugHelper.Log("--- Testing WorldToGrid ---", this);

        Tile tileForTest1 = GetTile(0,0);
        if (tileForTest1 != null)
        {
            Vector3 testWorldPos1 = tileForTest1.transform.position;
            Vector2Int gridResult1 = WorldToGrid(testWorldPos1);
            DebugHelper.Log($"WorldToGrid: World pos {testWorldPos1} (center of tile (0,0)) -> Grid {gridResult1}", this);
        } else { DebugHelper.LogWarning("WorldToGrid Test Case 1: Tile (0,0) not found for testing.", this); }

        Tile tileForTest2 = GetTile(0,0);
        if (tileForTest2 != null)
        {
            Vector3 testWorldPos2 = tileForTest2.transform.position + new Vector3(0.3f, -0.2f, 0f);
            Vector2Int gridResult2 = WorldToGrid(testWorldPos2);
            DebugHelper.Log($"WorldToWorldToGrid: World pos {testWorldPos2} (offset within tile (0,0)) -> Grid {gridResult2}", this);
        } else { DebugHelper.LogWarning("WorldToGrid Test Case 2: Tile (0,0) not found for testing.", this); }

        if (_tiles != null && _totalWidth > 0 && _totalHeight > 0)
        {
            float xGridOffset_test = -(_totalWidth / 2.0f) + 0.5f;
            float yGridOffset_test = -(_totalHeight / 2.0f) + 0.5f;
            Vector3 boundaryWorldPos = new Vector3(0 + xGridOffset_test, 0 + yGridOffset_test, 0);
            Vector2Int gridResult3 = WorldToGrid(boundaryWorldPos);
            DebugHelper.Log($"WorldToGrid: World pos {boundaryWorldPos} (center of boundary _tiles[0,0]) -> Grid {gridResult3}", this);
        } else { DebugHelper.LogWarning("WorldToGrid Test Case 3: Grid not initialized for boundary test.", this); }

        Vector3 outsideWorldPos = new Vector3(100f, 100f, 0f);
        Vector2Int gridResult4 = WorldToGrid(outsideWorldPos);
        DebugHelper.Log($"WorldToGrid: World pos {outsideWorldPos} (far outside grid) -> Grid {gridResult4}", this);
        DebugHelper.Log("--- Finished WorldToGrid Test ---", this);
    } // End of Start()

    void InitializeGrid()
    {
        if (tilePrefab == null)
        {
            DebugHelper.LogError("TilePrefab is not assigned in GridManager! Cannot initialize grid.", this);
            return;
        }
        if (defaultPlayableTileType == null)
        {
            DebugHelper.LogError("DefaultPlayableTileType is not assigned! Assign a TileTypeSO (e.g., PlainsTile).", this);
            return;
        }
        if (boundaryTileType == null)
        {
            DebugHelper.LogError("BoundaryTileType is not assigned! Assign your Boundary TileTypeSO.", this);
            return;
        }

        _totalWidth = playableWidth + 2;
        _totalHeight = playableHeight + 2;
        _tiles = new Tile[_totalWidth, _totalHeight];

        if (tileContainer == null)
        {
            GameObject containerGO = new GameObject("_TileContainer");
            tileContainer = containerGO.transform;
            tileContainer.SetParent(this.transform);
            DebugHelper.Log("No TileContainer assigned. Created one automatically under GridManager.", this);
        }
        else
        {
            foreach (Transform child in tileContainer) Destroy(child.gameObject);
        }

        float xOffset = -(_totalWidth / 2.0f) + 0.5f;
        float yOffset = -(_totalHeight / 2.0f) + 0.5f;

        for (int x = 0; x < _totalWidth; x++)
        {
            for (int y = 0; y < _totalHeight; y++)
            {
                Vector3 worldPosition = new Vector3(x + xOffset, y + yOffset, 0);
                GameObject tileGO = Instantiate(tilePrefab, worldPosition, Quaternion.identity, tileContainer);
                Tile tileComponent = tileGO.GetComponent<Tile>();

                if (tileComponent != null)
                {
                    TileTypeSO currentTileSO;
                    Vector2Int logicalGridPos;
                    bool isBoundary = (x == 0 || x == _totalWidth - 1 || y == 0 || y == _totalHeight - 1);

                    if (isBoundary)
                    {
                        currentTileSO = boundaryTileType;
                        logicalGridPos = new Vector2Int(x, y);
                    }
                    else
                    {
                        currentTileSO = defaultPlayableTileType;
                        logicalGridPos = new Vector2Int(x - 1, y - 1);
                    }
                    tileComponent.Initialize(logicalGridPos, currentTileSO, 0);
                    _tiles[x, y] = tileComponent;
                }
                else
                {
                    DebugHelper.LogError($"TilePrefab is missing the Tile component! Prefab: {tilePrefab.name}. Destroying.", tileGO);
                    Destroy(tileGO);
                }
            }
        }
        DebugHelper.Log($"Initialized a {playableWidth}x{playableHeight} playable grid with a 1-tile boundary. Total dimensions: {_totalWidth}x{_totalHeight}.", this);
    } // End of InitializeGrid()

    public Tile GetTile(int playableX, int playableY)
    {
        if (IsInPlayableBounds(playableX, playableY))
        {
            return _tiles[playableX + 1, playableY + 1];
        }
        return null;
    } // End of GetTile(int, int)

    public Tile GetTile(Vector2Int playableGridPos)
    {
        return GetTile(playableGridPos.x, playableGridPos.y);
    } // End of GetTile(Vector2Int)

    public bool IsInPlayableBounds(int playableX, int playableY)
    {
        return playableX >= 0 && playableX < playableWidth &&
               playableY >= 0 && playableY < playableHeight;
    } // End of IsInPlayableBounds(int, int)

    public bool IsInPlayableBounds(Vector2Int playableGridPos)
    {
        return IsInPlayableBounds(playableGridPos.x, playableGridPos.y);
    } // End of IsInPlayableBounds(Vector2Int)

    public Vector3 GridToWorld(Vector2Int playableGridPos)
    {
        if (_tiles == null)
        {
            DebugHelper.LogWarning("GridToWorld called before grid was initialized.", this);
            return Vector3.zero;
        }

        int arrayX = playableGridPos.x + 1;
        int arrayY = playableGridPos.y + 1;

        float xGridOffset = -(_totalWidth / 2.0f) + 0.5f;
        float yGridOffset = -(_totalHeight / 2.0f) + 0.5f;

        return new Vector3(arrayX + xGridOffset, arrayY + yGridOffset, 0);
    } // End of GridToWorld()

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        if (_tiles == null)
        {
            DebugHelper.LogWarning("WorldToGrid called before grid was initialized.", this);
            return new Vector2Int(-999, -999);
        }

        float xGridOffset = -(_totalWidth / 2.0f) + 0.5f;
        float yGridOffset = -(_totalHeight / 2.0f) + 0.5f;

        float gridBottomLeftCornerX = xGridOffset - 0.5f;
        float gridBottomLeftCornerY = yGridOffset - 0.5f;

        float relativeX = worldPos.x - gridBottomLeftCornerX;
        float relativeY = worldPos.y - gridBottomLeftCornerY;

        int arrayX = Mathf.FloorToInt(relativeX);
        int arrayY = Mathf.FloorToInt(relativeY);

        if (arrayX < 0 || arrayX >= _totalWidth || arrayY < 0 || arrayY >= _totalHeight)
        {
            return new Vector2Int(-999, -999);
        }

        int playableX = arrayX - 1;
        int playableY = arrayY - 1;

        if (!IsInPlayableBounds(playableX, playableY))
        {
            return new Vector2Int(-998, -998);
        }

        return new Vector2Int(playableX, playableY);
    } // End of WorldToGrid()

} // End of GridManager class