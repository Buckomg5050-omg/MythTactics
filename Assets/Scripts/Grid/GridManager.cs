using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [SerializeField] private int _width = 10;
    [SerializeField] private int _height = 10;
    [SerializeField] private float _tileSize = 1f;
    [SerializeField] private GameObject _tilePrefab;
    [SerializeField] private TileTypeSO _plainTileType;
    [SerializeField] private TileTypeSO _forestTileType;
    [SerializeField] private TileTypeSO _mountainTileType;
    [SerializeField] private TileTypeSO _waterTileType;

    private Tile[,] _tiles;

    private void Awake()
    {
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        _tiles = new Tile[_width, _height];

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                Vector3 worldPos = GridToWorld(new Vector2Int(x, y));
                GameObject tileObj = Instantiate(_tilePrefab, worldPos, Quaternion.identity, transform);
                Tile tile = tileObj.GetComponent<Tile>();

                TileTypeSO tileType;
                if (x == 0 || x == _width - 1 || y == 0 || y == _height - 1)
                    tileType = _waterTileType;
                else if ((x + y) % 3 == 0)
                    tileType = _forestTileType;
                else if ((x + y) % 5 == 0)
                    tileType = _mountainTileType;
                else
                    tileType = _plainTileType;

                tile.Initialize(new Vector2Int(x, y), tileType);
                _tiles[x, y] = tile;
            }
        }
    }
    
    public Tile GetTile(int x, int y)
    {
        if (IsInBounds(new Vector2Int(x, y)))
            return _tiles[x, y];
        Debug.LogWarning($"Tile at ({x}, {y}) is out of bounds.");
        return null;
    }

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return new Vector3(gridPos.x * _tileSize + _tileSize / 2, gridPos.y * _tileSize + _tileSize / 2, 0);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / _tileSize),
            Mathf.FloorToInt(worldPos.y / _tileSize)
        );
    }

    public bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < _width && pos.y >= 0 && pos.y < _height;
    }

    public List<Vector2Int> GetNeighbors(Vector2Int pos, bool includeDiagonals)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>
        {
            new Vector2Int(pos.x + 1, pos.y),
            new Vector2Int(pos.x - 1, pos.y),
            new Vector2Int(pos.x, pos.y + 1),
            new Vector2Int(pos.x, pos.y - 1)
        };

        if (includeDiagonals)
        {
            neighbors.Add(new Vector2Int(pos.x + 1, pos.y + 1));
            neighbors.Add(new Vector2Int(pos.x + 1, pos.y - 1));
            neighbors.Add(new Vector2Int(pos.x - 1, pos.y + 1));
            neighbors.Add(new Vector2Int(pos.x - 1, pos.y - 1));
        }

        return neighbors.FindAll(n => IsInBounds(n));
    }

    public List<Tile> GetTilesInRange(Vector2Int center, int range)
    {
        List<Tile> tilesInRange = new List<Tile>();
        for (int x = center.x - range; x <= center.x + range; x++)
        {
            for (int y = center.y - range; y <= center.y + range; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (IsInBounds(pos) && ManhattanDistance(center, pos) <= range)
                {
                    Tile tile = GetTile(x, y);
                    if (tile != null && tile.CanBeOccupied(null))
                        tilesInRange.Add(tile);
                }
            }
        }
        return tilesInRange;
    }

    public int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    // Temporary debug for testing
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TestPathfinding();
            TestCoordinateSystem();
        }
        TestHighlighting();
    }

    private void TestPathfinding()
    {
        Pathfinder pathfinder = GetComponent<Pathfinder>();
        List<Tile> path = pathfinder.FindPath(new Vector2Int(1, 1), new Vector2Int(8, 8));
        if (path != null)
        {
            Debug.Log("Path found:");
            foreach (Tile tile in path)
            {
                Debug.Log($"Tile: {tile.GridPosition}, Type: {tile.TileType.DisplayName}");
            }
        }
        else
        {
            Debug.Log("No path found.");
        }
    }

    private void TestCoordinateSystem()
    {
        Vector3 worldPos = new Vector3(2.5f, 3.5f, 0);
        Vector2Int gridPos = WorldToGrid(worldPos);
        Debug.Log($"World {worldPos} -> Grid {gridPos}");

        Vector3 convertedBack = GridToWorld(gridPos);
        Debug.Log($"Grid {gridPos} -> World {convertedBack}");

        List<Vector2Int> neighbors = GetNeighbors(new Vector2Int(5, 5), false);
        Debug.Log("Neighbors of (5,5):");
        foreach (var neighbor in neighbors)
        {
            Debug.Log($"Neighbor: {neighbor}");
        }

        List<Tile> tilesInRange = GetTilesInRange(new Vector2Int(5, 5), 2);
        Debug.Log($"Tiles in range 2 from (5,5): {tilesInRange.Count}");
    }
    private void TestHighlighting()
{
    if (Input.GetMouseButtonDown(0)) // Left click
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int gridPos = WorldToGrid(mousePos);
        Tile tile = GetTile(gridPos.x, gridPos.y);
        if (tile != null)
        {
            tile.SetHighlight(TileHighlight.Selected);
            Debug.Log($"Highlighted tile {gridPos} as Selected");
        }
    }
    if (Input.GetMouseButtonDown(1)) // Right click
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int gridPos = WorldToGrid(mousePos);
        Tile tile = GetTile(gridPos.x, gridPos.y);
        if (tile != null)
        {
            tile.SetHighlight(TileHighlight.None);
            Debug.Log($"Cleared highlight on tile {gridPos}");
        }
    }
}
}