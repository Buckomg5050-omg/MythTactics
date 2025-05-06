using System.Collections.Generic;
using UnityEngine;

public class Pathfinder
{
    private readonly GridManager _grid;

    public Pathfinder(GridManager grid)
    {
        _grid = grid;
    }

    public List<Tile> FindPath(Vector2Int start, Vector2Int goal)
    {
        Dictionary<Vector2Int, Vector2Int> cameFrom = new();
        Dictionary<Vector2Int, int> costSoFar = new();

        PriorityQueue<Vector2Int> frontier = new();
        frontier.Enqueue(start, 0);

        cameFrom[start] = start;
        costSoFar[start] = 0;

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();

            if (current == goal)
                break;

            foreach (var neighbor in GetNeighbors(current))
            {
                int newCost = costSoFar[current] + 1; // constant cost for now

                if (!costSoFar.ContainsKey(neighbor) || newCost < costSoFar[neighbor])
                {
                    costSoFar[neighbor] = newCost;
                    int priority = newCost + ManhattanDistance(neighbor, goal);
                    frontier.Enqueue(neighbor, priority);
                    cameFrom[neighbor] = current;
                }
            }
        }

        if (!cameFrom.ContainsKey(goal))
            return null; // no path

        List<Tile> path = new();
        Vector2Int step = goal;

        while (step != start)
        {
            path.Add(_grid.GetTile(step.x, step.y));
            step = cameFrom[step];
        }

        path.Reverse();
        return path;
    }

    private IEnumerable<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        Vector2Int[] directions = {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        foreach (var dir in directions)
        {
            Vector2Int next = pos + dir;
            if (_grid.GetTileSafe(next) != null)
                yield return next;
        }
    }

    private int ManhattanDistance(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
}
