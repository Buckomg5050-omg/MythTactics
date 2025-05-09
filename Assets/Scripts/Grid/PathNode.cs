// PathNode.cs
using UnityEngine;

public struct PathNode
{
    public Vector2Int gridPosition;
    public int gCost;
    public int hCost;
    public int FCost => gCost + hCost;
    public Vector2Int parentPosition;
    public bool isWalkable;

    public PathNode(Vector2Int pos, bool walkable)
    {
        gridPosition = pos;
        isWalkable = walkable;
        gCost = int.MaxValue;
        hCost = 0;
        parentPosition = new Vector2Int(-1, -1);
    }
}