using System.Collections.Generic;
using UnityEngine;

public class Pathfinder : MonoBehaviour
{
    private GridManager _gridManager;
    private Dictionary<(Vector2Int, Vector2Int), List<Tile>> _pathCache = new Dictionary<(Vector2Int, Vector2Int), List<Tile>>();

    private struct PathNode
    {
        public int F, G, H;
        public Vector2Int Parent;
        public bool Closed;
    }

    private void Awake()
    {
        _gridManager = GetComponent<GridManager>();
    }

    public List<Tile> FindPath(Vector2Int start, Vector2Int end, Unit movingUnit = null)
    {
        var key = (start, end);
        if (_pathCache.ContainsKey(key))
        {
            Debug.Log($"Using cached path from {start} to {end}");
            return new List<Tile>(_pathCache[key]);
        }

        Tile startTile = _gridManager.GetTile(start.x, start.y);
        Tile endTile = _gridManager.GetTile(end.x, end.y);
        if (startTile == null || endTile == null)
        {
            Debug.LogWarning($"Invalid start {start} or end {end} tile for pathfinding.");
            return null;
        }

        if (startTile.IsOccupied && startTile.OccupyingUnit != movingUnit)
        {
            Debug.LogWarning($"Start tile {start} is occupied by another unit.");
            return null;
        }

        if (!endTile.TileType.IsPassable)
        {
            Debug.LogWarning($"End tile {end} is impassable.");
            return null;
        }

        var openSet = new PriorityQueue<Vector2Int>();
        var nodes = new Dictionary<Vector2Int, PathNode>();
        nodes[start] = new PathNode { G = 0, H = _gridManager.ManhattanDistance(start, end), F = _gridManager.ManhattanDistance(start, end) };

        openSet.Enqueue(start, nodes[start].F);

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();
            nodes[current] = new PathNode
            {
                F = nodes[current].F,
                G = nodes[current].G,
                H = nodes[current].H,
                Parent = nodes[current].Parent,
                Closed = true
            };

            if (current == end)
            {
                List<Tile> path = ReconstructPath(nodes, current);
                _pathCache[key] = new List<Tile>(path);
                return path;
            }

            foreach (Vector2Int neighbor in _gridManager.GetNeighbors(current, false))
            {
                Tile neighborTile = _gridManager.GetTile(neighbor.x, neighbor.y);
                if (neighborTile == null || !neighborTile.TileType.IsPassable || (neighborTile.IsOccupied && neighborTile.OccupyingUnit != movingUnit && neighbor != end) || (nodes.ContainsKey(neighbor) && nodes[neighbor].Closed))
                    continue;

                Tile currentTile = _gridManager.GetTile(current.x, current.y);
                int heightDifference = neighborTile.HeightLevel - currentTile.HeightLevel;
                int heightPenalty = heightDifference > 0 ? heightDifference * 2 : 0;
                int movementCost = neighborTile.MovementCost + heightPenalty;

                int tentativeGScore = nodes[current].G + movementCost;

                if (!nodes.ContainsKey(neighbor) || tentativeGScore < nodes[neighbor].G)
                {
                    int hScore = _gridManager.ManhattanDistance(neighbor, end);
                    nodes[neighbor] = new PathNode
                    {
                        G = tentativeGScore,
                        H = hScore,
                        F = tentativeGScore + hScore,
                        Parent = current
                    };

                    if (!openSet.Contains(neighbor))
                        openSet.Enqueue(neighbor, nodes[neighbor].F);
                }
            }
        }

        Debug.LogWarning($"No path found from {start} to {end}.");
        return null;
    }

    private List<Tile> ReconstructPath(Dictionary<Vector2Int, PathNode> nodes, Vector2Int current)
    {
        List<Tile> path = new List<Tile>();
        int totalCost = nodes[current].G;

        while (nodes.ContainsKey(current))
        {
            Tile tile = _gridManager.GetTile(current.x, current.y);
            if (tile != null && tile.TileType.IsPassable)
                path.Add(tile);
            if (current == nodes[current].Parent) break;
            current = nodes[current].Parent;
        }

        path.Reverse();
        Debug.Log($"Path cost: {totalCost}");
        return path;
    }

    public void InvalidateCache()
    {
        _pathCache.Clear();
        Debug.Log("Path cache cleared.");
    }

    private class PriorityQueue<T>
    {
        private List<(T item, int priority)> _elements = new List<(T, int)>();

        public int Count => _elements.Count;

        public void Enqueue(T item, int priority)
        {
            _elements.Add((item, priority));
            _elements.Sort((a, b) => a.priority.CompareTo(b.priority));
        }

        public T Dequeue()
        {
            T item = _elements[0].item;
            _elements.RemoveAt(0);
            return item;
        }

        public bool Contains(T item)
        {
            return _elements.Exists(e => EqualityComparer<T>.Default.Equals(e.item, item));
        }
    }
}