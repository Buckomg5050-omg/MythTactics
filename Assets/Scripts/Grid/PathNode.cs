// PathNode.cs
using UnityEngine; // For Vector2Int

/// <summary>
/// Represents a node in the pathfinding grid for A* calculations.
/// Contains G, H, F costs, its position, and a reference to its parent in the path.
/// As per GDD 5.1.3.
/// </summary>
public struct PathNode
{
    public Vector2Int gridPosition; // The logical grid position of this node/tile

    public int gCost; // Cost from the start node to this node
    public int hCost; // Heuristic: Estimated cost from this node to the end node
    public int FCost => gCost + hCost; // Total estimated cost (G + H)

    public Vector2Int parentPosition; // Grid position of the node that leads to this one in the path
    public bool isWalkable;       // Pre-calculated walkability (considers terrain, but not dynamic occupancy initially)

    /// <summary>
    /// Constructor for a PathNode.
    /// </summary>
    /// <param name="pos">Grid position of the node.</param>
    /// <param name="walkable">Is this node generally walkable (based on terrain type)?</param>
    public PathNode(Vector2Int pos, bool walkable)
    {
        gridPosition = pos;
        isWalkable = walkable;
        gCost = int.MaxValue; // Initialize with a high cost
        hCost = 0;
        parentPosition = new Vector2Int(-1, -1); // Indicates no parent initially
    }

    // It can be useful to compare nodes, primarily for priority queue in A*
    // However, standard List.Sort or LINQ.OrderBy can also be used.
    // If using a custom priority queue, it might need an IComparable implementation.
    // For now, we'll rely on sorting external collections.
}