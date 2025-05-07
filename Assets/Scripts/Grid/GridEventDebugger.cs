using System.Collections.Generic;
using UnityEngine;

public class GridEventDebugger : MonoBehaviour
{
    private GridManager _gridManager;
    private UnitManager _unitManager;
    private Pathfinder _pathfinder;

    private void Awake()
    {
        _gridManager = GetComponent<GridManager>();
        _unitManager = GetComponent<UnitManager>();
        _pathfinder = GetComponent<Pathfinder>();
        _gridManager.OnTileChanged += OnTileChanged;
        _gridManager.OnGridRebuilt += OnGridRebuilt;

        for (int x = 0; x < 10; x++)
        {
            for (int y = 0; y < 10; y++)
            {
                Tile tile = _gridManager.GetTile(x, y);
                if (tile != null)
                    tile.OnTileStateChanged += OnTileStateChanged;
            }
        }
    }

    private void OnDestroy()
    {
        if (_gridManager != null)
        {
            _gridManager.OnTileChanged -= OnTileChanged;
            _gridManager.OnGridRebuilt -= OnGridRebuilt;

            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    Tile tile = _gridManager.GetTile(x, y);
                    if (tile != null)
                        tile.OnTileStateChanged -= OnTileStateChanged;
                }
            }
        }
    }

    private void OnTileChanged(Tile tile)
    {
        Debug.Log($"Tile changed at {tile.GridPosition}: {tile.TileType.DisplayName}, Height: {tile.HeightLevel}");
    }

    private void OnGridRebuilt()
    {
        Debug.Log("Grid rebuilt.");
    }

    private void OnTileStateChanged(Tile tile, string propertyName, object oldValue, object newValue)
    {
        Debug.Log($"Tile {tile.GridPosition} state changed: {propertyName} from {oldValue} to {newValue}");
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left click
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int gridPos = _gridManager.WorldToGrid(mousePos);
            Tile tile = _gridManager.GetTile(gridPos.x, gridPos.y);
            if (tile != null)
            {
                Unit unit = _unitManager.GetUnitAt(gridPos);
                if (unit != null)
                {
                    _unitManager.SelectUnit(unit);
                }
                else
                {
                    _unitManager.DeselectUnit();
                    tile.SetHighlight(TileHighlight.Selected);
                }
            }
        }

        if (Input.GetMouseButtonDown(1)) // Right click
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int gridPos = _gridManager.WorldToGrid(mousePos);
            Tile targetTile = _gridManager.GetTile(gridPos.x, gridPos.y);
            if (targetTile != null && _unitManager.SelectedUnit != null)
            {
                List<Tile> path = _pathfinder.FindPath(_unitManager.SelectedUnit.CurrentTile.GridPosition, gridPos, _unitManager.SelectedUnit);
                if (path != null)
                {
                    _unitManager.SelectedUnit.MoveAlongPath(path, () => Debug.Log("Unit movement complete."));
                }
                else
                {
                    Debug.LogWarning($"No valid path from {_unitManager.SelectedUnit.CurrentTile.GridPosition} to {gridPos}.");
                }
            }
            else if (targetTile != null)
            {
                targetTile.SetHighlight(TileHighlight.None);
            }
        }

        if (Input.GetKeyDown(KeyCode.A)) // Attack
        {
            if (_unitManager.SelectedUnit != null)
            {
                List<Unit> targets = _unitManager.GetUnitsInRange(_unitManager.SelectedUnit, _unitManager.SelectedUnit.AttackRange);
                if (targets.Count > 0)
                {
                    _unitManager.SelectedUnit.Attack(targets[0]);
                }
                else
                {
                    Debug.Log("No units in attack range.");
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            _gridManager.SetTileType(new Vector2Int(5, 5), _gridManager.PlainTileType);
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            _gridManager.SetTileHeight(new Vector2Int(5, 5), _gridManager.GetTile(5, 5).HeightLevel + 1);
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            Tile tile1 = _gridManager.GetTile(5, 5);
            Tile tile2 = _gridManager.GetTile(6, 6);
            if (tile1 != null && tile2 != null)
            {
                float bonus = tile1.GetCombatBonus(tile2);
                Debug.Log($"Combat bonus from {tile1.GridPosition} (Height {tile1.HeightLevel}) to {tile2.GridPosition} (Height {tile2.HeightLevel}): {bonus:P0}");
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            TestPathfinding();
            TestCoordinateSystem();
        }
    }

    private void TestPathfinding()
    {
        // Find any registered unit
        Unit unit = null;
        foreach (var u in FindObjectsOfType<Unit>())
        {
            if (_unitManager.GetUnitAt(u.CurrentTile.GridPosition) != null)
            {
                unit = u;
                break;
            }
        }

        if (unit == null)
        {
            Debug.LogWarning("No units found for pathfinding test.");
            return;
        }

        Vector2Int start = unit.CurrentTile.GridPosition;
        Vector2Int end = new Vector2Int(8, 8);
        List<Tile> path = _pathfinder.FindPath(start, end, unit);
        if (path != null)
        {
            Debug.Log("Path found:");
            foreach (Tile tile in path)
            {
                Debug.Log($"Tile: {tile.GridPosition}, Type: {tile.TileType.DisplayName}, Height: {tile.HeightLevel}");
            }
            // Simulate movement to test attacking
            unit.MoveAlongPath(path, () =>
            {
                Debug.Log("Test movement complete.");
                List<Unit> targets = _unitManager.GetUnitsInRange(unit, unit.AttackRange);
                if (targets.Count > 0)
                {
                    unit.Attack(targets[0]);
                }
            });
        }
        else
        {
            Debug.LogWarning($"No path found from {start} to {end}.");
        }
    }

    private void TestCoordinateSystem()
    {
        Vector3 worldPos = new Vector3(2.5f, 3.5f, 0);
        Vector2Int gridPos = _gridManager.WorldToGrid(worldPos);
        Debug.Log($"World {worldPos} -> Grid {gridPos}");

        Vector3 convertedBack = _gridManager.GridToWorld(gridPos);
        Debug.Log($"Grid {gridPos} -> World {convertedBack}");

        List<Vector2Int> neighbors = _gridManager.GetNeighbors(new Vector2Int(5, 5), false);
        Debug.Log("Neighbors of (5,5):");
        foreach (var neighbor in neighbors)
        {
            Debug.Log($"Neighbor: {neighbor}");
        }

        List<Tile> tilesInRange = _gridManager.GetTilesInRange(new Vector2Int(5, 5), 2);
        Debug.Log($"Tiles in range 2 from (5,5): {tilesInRange.Count}");
    }
}