// Pathfinder.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // For OrderBy

public class Pathfinder
{
    private GridManager _gridManager;
    private List<PathNode> _openList;
    private HashSet<Vector2Int> _closedSet;
    private PathNode[,] _pathNodeGrid;

    public Pathfinder(GridManager gridManager)
    {
        if (gridManager == null) { Debug.LogError("[Pathfinder] GridManager null!"); _gridManager = null; return; }
        _gridManager = gridManager;
        _openList = new List<PathNode>();
        _closedSet = new HashSet<Vector2Int>();
    }

    public List<Tile> FindPath(Vector2Int startPosPlayable, Vector2Int endPosPlayable, Unit requestingUnit = null)
    {
        List<Tile> finalPath = new List<Tile>();
        if (_gridManager == null) { Debug.LogError("PF Error: No GridManager."); return finalPath; }
        if (!_gridManager.IsInPlayableBounds(startPosPlayable) || !_gridManager.IsInPlayableBounds(endPosPlayable)) { DebugHelper.LogWarning($"PF Error: Start/End {startPosPlayable}->{endPosPlayable} out of bounds.", _gridManager); return finalPath; }

        InitializePathNodeGrid();

        PathNode startNode = GetPathNode(startPosPlayable.x, startPosPlayable.y);
        PathNode endNodeOriginalQuery = GetPathNode(endPosPlayable.x, endPosPlayable.y);

        if (!startNode.isWalkable) { DebugHelper.LogWarning($"PF Error: Start {startPosPlayable} not walkable.", _gridManager); return finalPath; }
        if (!endNodeOriginalQuery.isWalkable) { DebugHelper.LogWarning($"PF Error: End {endPosPlayable} not walkable.", _gridManager); return finalPath; }

        Tile endTile = _gridManager.GetTile(endPosPlayable);
        if (endTile != null && endTile.IsOccupied && endTile.occupyingUnit != requestingUnit) { DebugHelper.Log($"PF Error: End tile {endPosPlayable} occupied by {endTile.occupyingUnit?.name}.", _gridManager); return finalPath; }

        _openList.Clear();
        _closedSet.Clear();

        startNode.gCost = 0;
        startNode.hCost = CalculateManhattanDistance(startPosPlayable, endPosPlayable);
        _pathNodeGrid[startPosPlayable.x, startPosPlayable.y] = startNode;
        _openList.Add(startNode);

        int emergencyExit = 0;
        int maxIterations = _gridManager.playableWidth * _gridManager.playableHeight * 2;

        while (_openList.Count > 0)
        {
            emergencyExit++;
            if (emergencyExit > maxIterations) { DebugHelper.LogError("PF Error: Max iterations exceeded in FindPath.", _gridManager); return finalPath; }

            _openList = _openList.OrderBy(node => node.FCost).ThenBy(node => node.hCost).ToList(); // Corrected LINQ usage
            PathNode currentNode = _openList[0];
            
            if (currentNode.gridPosition == endPosPlayable) {
                PathNode endNodeFromGrid = _pathNodeGrid[endPosPlayable.x, endPosPlayable.y];
                return ReconstructPath(endNodeFromGrid, startNode);
            }

            _openList.RemoveAt(0);
            _closedSet.Add(currentNode.gridPosition);

            List<Tile> neighborTiles = _gridManager.GetNeighbors(currentNode.gridPosition);
            foreach (Tile neighborTile in neighborTiles)
            {
                Vector2Int neighborPos = neighborTile.gridPosition;
                if (_closedSet.Contains(neighborPos)) continue;

                PathNode neighborNodeData = GetPathNode(neighborPos.x, neighborPos.y);

                if (!neighborNodeData.isWalkable) { _closedSet.Add(neighborPos); continue; }
                if (neighborPos != endPosPlayable && neighborTile.IsOccupied && neighborTile.occupyingUnit != requestingUnit) { _closedSet.Add(neighborPos); continue; }

                int movementCostToNeighbor = neighborTile.GetMovementCost();
                if (movementCostToNeighbor >= 255) { _closedSet.Add(neighborPos); continue; }

                int tentativeGCost = currentNode.gCost + movementCostToNeighbor;

                bool wasInOpenList = _openList.Any(n => n.gridPosition == neighborPos);

                if (tentativeGCost < neighborNodeData.gCost || !wasInOpenList)
                {
                    neighborNodeData.gCost = tentativeGCost;
                    neighborNodeData.hCost = CalculateManhattanDistance(neighborPos, endPosPlayable);
                    neighborNodeData.parentPosition = currentNode.gridPosition;
                    _pathNodeGrid[neighborPos.x, neighborPos.y] = neighborNodeData;

                    if (!wasInOpenList) _openList.Add(neighborNodeData);
                    // If it was in open list, its data is updated, and sort will re-position it.
                }
            }
        }
        DebugHelper.Log($"Pathfinder: No path found from {startPosPlayable} to {endPosPlayable}.", _gridManager);
        return finalPath;
    }

    public List<Tile> GetReachableTiles(Vector2Int startPosPlayable, int movementPoints, Unit requestingUnit = null)
    {
        List<Tile> reachableTiles = new List<Tile>();
        if (_gridManager == null) { Debug.LogError("Pathfinder Error: No GridManager."); return reachableTiles; }
        if (movementPoints < 0) { return reachableTiles; }
        if (!_gridManager.IsInPlayableBounds(startPosPlayable)) { DebugHelper.LogWarning($"GetReachableTiles: Start pos {startPosPlayable} out of bounds.", _gridManager); return reachableTiles; }

        InitializePathNodeGrid();
        PathNode startNode = GetPathNode(startPosPlayable.x, startPosPlayable.y);
        if (!startNode.isWalkable) { DebugHelper.LogWarning($"GetReachableTiles: Start pos {startPosPlayable} not walkable.", _gridManager); return reachableTiles; }

        _openList.Clear();
        _closedSet.Clear();
        startNode.gCost = 0;
        startNode.hCost = 0;
        _pathNodeGrid[startPosPlayable.x, startPosPlayable.y] = startNode;
        _openList.Add(startNode);

        int emergencyExit = 0;
        int maxIterations = _gridManager.playableWidth * _gridManager.playableHeight * 2;

        while (_openList.Count > 0)
        {
            emergencyExit++;
            if (emergencyExit > maxIterations) { DebugHelper.LogError("Pathfinder Error: Max iterations in GetReachableTiles.", _gridManager); break; }

            _openList = _openList.OrderBy(node => node.gCost).ToList(); // Sort by G-cost
            PathNode currentNode = _openList[0];
            _openList.RemoveAt(0);

            if (currentNode.gCost > movementPoints) continue;

            Tile currentTile = _gridManager.GetTile(currentNode.gridPosition);
            if (currentTile != null) reachableTiles.Add(currentTile);
            _closedSet.Add(currentNode.gridPosition);

            List<Tile> neighborTiles = _gridManager.GetNeighbors(currentNode.gridPosition);
            foreach (Tile neighborTile in neighborTiles)
            {
                 Vector2Int neighborPos = neighborTile.gridPosition;
                if (_closedSet.Contains(neighborPos)) continue;
                PathNode neighborNodeData = GetPathNode(neighborPos.x, neighborPos.y);
                if (!neighborNodeData.isWalkable) { _closedSet.Add(neighborPos); continue; }
                if (neighborTile.IsOccupied && neighborTile.occupyingUnit != requestingUnit) { _closedSet.Add(neighborPos); continue; }

                int movementCostToNeighbor = neighborTile.GetMovementCost();
                if (movementCostToNeighbor >= 255) { _closedSet.Add(neighborPos); continue; }

                int tentativeGCost = currentNode.gCost + movementCostToNeighbor;
                if (tentativeGCost <= movementPoints)
                {
                    bool wasInOpenList = _openList.Any(n => n.gridPosition == neighborPos);
                    if (tentativeGCost < neighborNodeData.gCost || !wasInOpenList)
                    {
                        neighborNodeData.gCost = tentativeGCost;
                        _pathNodeGrid[neighborPos.x, neighborPos.y] = neighborNodeData;
                        if (!wasInOpenList) _openList.Add(neighborNodeData);
                    }
                }
            }
        }
        return reachableTiles;
    }

    private void InitializePathNodeGrid()
    {
        int width = _gridManager.playableWidth; int height = _gridManager.playableHeight;
        _pathNodeGrid = new PathNode[width, height];
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                Tile tile = _gridManager.GetTile(x, y);
                bool walkable = (tile != null && tile.GetMovementCost() < 255);
                _pathNodeGrid[x, y] = new PathNode(new Vector2Int(x, y), walkable);
            }
        }
    }

    private PathNode GetPathNode(int x, int y)
    {
        if (x < 0 || x >= _gridManager.playableWidth || y < 0 || y >= _gridManager.playableHeight) {
            Debug.LogError($"GetPathNode OOB: ({x},{y})"); return new PathNode(new Vector2Int(x,y), false); }
        return _pathNodeGrid[x, y];
    }

    private int CalculateManhattanDistance(Vector2Int a, Vector2Int b) { return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); }

    private List<Tile> ReconstructPath(PathNode endNodeArg, PathNode startNodeArg)
    {
        List<Tile> path = new List<Tile>(); PathNode current = endNodeArg;
        int safe = 0, max = _gridManager.playableWidth * _gridManager.playableHeight;
        while (current.gridPosition != startNodeArg.gridPosition) {
            safe++; if (safe > max) { DebugHelper.LogError("ReconstructPath loop!", _gridManager); return new List<Tile>(); }
            Tile tile = _gridManager.GetTile(current.gridPosition);
            if (tile != null) path.Add(tile); else { DebugHelper.LogError($"ReconstructPath null tile at {current.gridPosition}!", _gridManager); return new List<Tile>(); }
            if (current.parentPosition.x != -1 && current.parentPosition.y != -1) {
                if (current.parentPosition.x >= 0 && current.parentPosition.x < _gridManager.playableWidth && current.parentPosition.y >= 0 && current.parentPosition.y < _gridManager.playableHeight) {
                    current = _pathNodeGrid[current.parentPosition.x, current.parentPosition.y]; }
                else { DebugHelper.LogError($"ReconstructPath parent OOB: {current.parentPosition}!", _gridManager); return new List<Tile>(); } }
            else { DebugHelper.LogError($"ReconstructPath node {current.gridPosition} has no valid parent!", _gridManager); return new List<Tile>(); } }
        path.Reverse(); return path;
    }
}