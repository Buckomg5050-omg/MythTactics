using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid size")]
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 10;

    [Header("References")]
    [SerializeField] private Tile tilePrefab;

    private Tile[,] _tiles;
    [SerializeField] private Unit unitPrefab;


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
                                       new Vector3(x, y),   // 1 unit = 1 tile
                                       Quaternion.identity,
                                       transform);          // parent under GridManager

                tile.Init(new Vector2Int(x, y));
                _tiles[x, y] = tile;
            }
        }
        // place a single test unit at (0,0)
var u = Instantiate(unitPrefab);
u.Init(new Vector2Int(0, 0));

    }

    public Tile GetTile(int x, int y) => _tiles[x, y];
}
