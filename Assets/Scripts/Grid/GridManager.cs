using UnityEngine;

public class GridManager : MonoBehaviour
{
    [SerializeField] private int _width = 10; // Grid width (tiles)
    [SerializeField] private int _height = 10; // Grid height (tiles)
    [SerializeField] private float _tileSize = 1f; // Size of each tile in world units

    private Tile[,] _tiles; // 2D array to store tiles

    private void Awake()
    {
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        _tiles = new Tile[_width, _height];
        // Placeholder: Tiles will be populated later
    }

    public Tile GetTile(int x, int y)
    {
        if (x >= 0 && x < _width && y >= 0 && y < _height)
        {
            return _tiles[x, y];
        }
        Debug.LogWarning($"Tile at ({x}, {y}) is out of bounds.");
        return null;
    }
}