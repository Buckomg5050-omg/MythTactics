// GridManager.cs
using UnityEngine;
// We don't need System.Collections.Generic for this version yet.

public class GridManager : MonoBehaviour
{
    [Header("Grid Dimensions")]
    [Tooltip("The playable width of the grid.")]
    public int playableWidth = 10;

    [Tooltip("The playable height of the grid.")]
    public int playableHeight = 10;

    [Header("Tile Setup")]
    [Tooltip("The prefab to use for instantiating tiles.")]
    public GameObject tilePrefab;

    [Tooltip("Default TileTypeSO to use for tiles if no specific map data is loaded.")]
    public TileTypeSO defaultTileType;

    [Tooltip("Parent transform for instantiated tile GameObjects. Helps keep hierarchy clean.")]
    public Transform tileContainer;

    private Tile[,] _tiles;
    public Tile[,] Tiles => _tiles; // Public getter for the tiles array

    void Start()
    {
        GenerateSimpleGrid();
    }

    void GenerateSimpleGrid()
    {
        if (tilePrefab == null)
        {
            DebugHelper.LogError("TilePrefab is not assigned in GridManager! Cannot generate grid.", this);
            return;
        }
        if (defaultTileType == null)
        {
            DebugHelper.LogError("DefaultTileType is not assigned in GridManager! Assign a TileTypeSO. Cannot generate grid.", this);
            return;
        }

        _tiles = new Tile[playableWidth, playableHeight];

        if (tileContainer == null)
        {
            GameObject containerGO = new GameObject("_TileContainer");
            tileContainer = containerGO.transform;
            tileContainer.SetParent(this.transform);
            DebugHelper.Log("No TileContainer assigned. Created one automatically under GridManager.", this);
        }
        else
        {
            foreach (Transform child in tileContainer)
            {
                Destroy(child.gameObject);
            }
            DebugHelper.Log("Cleared existing tiles from TileContainer.", this);
        }

        float xOffset = -(playableWidth / 2.0f) + 0.5f;
        float yOffset = -(playableHeight / 2.0f) + 0.5f;

        for (int x = 0; x < playableWidth; x++)
        {
            for (int y = 0; y < playableHeight; y++)
            {
                Vector3 worldPosition = new Vector3(x + xOffset, y + yOffset, 0);
                GameObject tileGO = Instantiate(tilePrefab, worldPosition, Quaternion.identity, tileContainer);
                Tile tileComponent = tileGO.GetComponent<Tile>();

                if (tileComponent != null)
                {
                    tileComponent.Initialize(new Vector2Int(x, y), defaultTileType, 0);
                    _tiles[x, y] = tileComponent;
                }
                else
                {
                    DebugHelper.LogError($"TilePrefab is missing the Tile component! Prefab: {tilePrefab.name}. Destroying instantiated object.", tileGO);
                    Destroy(tileGO); // Destroy the problematic GameObject
                }
            }
        }
        DebugHelper.Log($"Generated a {playableWidth}x{playableHeight} grid with '{defaultTileType.displayName}' tiles.", this);
    }

    public Tile GetTile(int x, int y)
    {
        if (IsInBounds(x, y))
        {
            return _tiles[x, y];
        }
        return null;
    }

    public Tile GetTile(Vector2Int gridPos)
    {
        return GetTile(gridPos.x, gridPos.y);
    }

    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < playableWidth && y >= 0 && y < playableHeight;
    }

    public bool IsInBounds(Vector2Int gridPos)
    {
        return IsInBounds(gridPos.x, gridPos.y);
    }
}