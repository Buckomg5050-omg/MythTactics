using System;
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

    public TileTypeSO PlainTileType => _plainTileType;

    public event Action<Tile> OnTileChanged;
    public event Action OnGridRebuilt;

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
                int heightLevel;
                if (x == 0 || x == _width - 1 || y == 0 || y == _height - 1)
                {
                    tileType = _waterTileType;
                    heightLevel = 0;
                }
                else if ((x + y) % 3 == 0)
                {
                    tileType = _forestTileType;
                    heightLevel = 1;
                }
                else if ((x + y) % 5 == 0)
                {
                    tileType = _mountainTileType;
                    heightLevel = 2;
                }
                else
                {
                    tileType = _plainTileType;
                    heightLevel = 0;
                }

                tile.Initialize(new Vector2Int(x, y), tileType, heightLevel);
                _tiles[x, y] = tile;

                OnTileChanged?.Invoke(tile);
            }
        }

        OnGridRebuilt?.Invoke();
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
                if (IsInBounds(pos) && ManhattanDistance(pos, center) <= range)
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

    public void SetTileType(Vector2Int pos, TileTypeSO tileType)
    {
        Tile tile = GetTile(pos.x, pos.y);
        if (tile != null)
        {
            tile.SetTileType(tileType);
            // Reset height for Plain tiles
            if (tileType == _plainTileType)
                tile.SetHeightLevel(0);
            OnTileChanged?.Invoke(tile);
        }
    }

    public void SetTileHeight(Vector2Int pos, int heightLevel)
    {
        Tile tile = GetTile(pos.x, pos.y);
        if (tile != null)
        {
            tile.SetHeightLevel(heightLevel);
            OnTileChanged?.Invoke(tile);
        }
    }
}