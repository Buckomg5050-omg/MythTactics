using System.Collections.Generic;
using UnityEngine;

public class Pathfinder : MonoBehaviour
{
    private GridManager _gridManager;

    private void Awake()
    {
        _gridManager = GetComponent<GridManager>();
    }

    public List<Tile> FindPath(Vector2Int start, Vector2Int end)
    {
        if (!_gridManager.GetTile(start.x, start.y) || !_gridManager.GetTile(end.x, end.y))
            return null;

        var openSet = new PriorityQueue<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, int> { [start] = 0 };
        var fScore = new Dictionary<Vector2Int, int> { [start] = _gridManager.ManhattanDistance(start, end) };

        openSet.Enqueue(start, fScore[start]);

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();

            if (current == end)
                return ReconstructPath(cameFrom, current);

            foreach (Vector2Int neighbor in _gridManager.GetNeighbors(current, false))
            {
                Tile neighborTile = _gridManager.GetTile(neighbor.x, neighbor.y);
                if (neighborTile == null || !neighborTile.CanBeOccupied(null))
                    continue;

                int tentativeGScore = gScore[current] + neighborTile.MovementCost;

                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + _gridManager.ManhattanDistance(neighbor, end);

                    if (!openSet.Contains(neighbor))
                        openSet.Enqueue(neighbor, fScore[neighbor]);
                }
            }
        }

        return null;
    }

    private List<Tile> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Tile> path = new List<Tile> { _gridManager.GetTile(current.x, current.y) };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(_gridManager.GetTile(current.x, current.y));
        }
        path.Reverse();
        return path;
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