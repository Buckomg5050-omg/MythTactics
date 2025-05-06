using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid size")]
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 10;

    [Header("References")]
    [SerializeField] private Tile tilePrefab;
    [SerializeField] private Unit unitPrefab;

    private Tile[,] _tiles;

    private void Awake()
    {
        GenerateGrid();
    }

    private void GenerateGrid()
    {
        _tiles = new Tile[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var tile = Instantiate(tilePrefab,
                                       new Vector3(x, y),
                                       Quaternion.identity,
                                       transform);

                tile.Init(new Vector2Int(x, y));
                _tiles[x, y] = tile;
            }
        }

        // place a single test unit at (0,0)
        var u = Instantiate(unitPrefab);
        u.Init(new Vector2Int(0, 0));
    }

    public Tile GetTile(int x, int y) => _tiles[x, y];

    public List<Tile> GetTilesInRange(Vector2Int center, int range)
    {
        List<Tile> result = new();

        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                Vector2Int pos = center + new Vector2Int(x, y);

                if (IsInBounds(pos) && Mathf.Abs(x) + Mathf.Abs(y) <= range)
                    result.Add(_tiles[pos.x, pos.y]);
            }
        }

        return result;
    }

    public Tile GetTileSafe(Vector2Int pos)
{
    if (pos.x < 0 || pos.x >= _tiles.GetLength(0) || pos.y < 0 || pos.y >= _tiles.GetLength(1))
        return null;

    return _tiles[pos.x, pos.y];
}
    private bool IsInBounds(Vector2Int pos) =>
        pos.x >= 0 && pos.x < _tiles.GetLength(0) && pos.y >= 0 && pos.y < _tiles.GetLength(1);
}
