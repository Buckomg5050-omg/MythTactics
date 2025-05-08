// Pathfinder.cs
using UnityEngine;
using System.Collections.Generic; // For List
using System.Linq; // For OrderBy in simple Open List handling

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
        if (!_gridManager.IsInPlayableBounds(startPosPlayable) || !_gridManager.IsInPlayableBounds(endPosPlayable)) { DebugHelper.LogWarning($"PF Error: Start/End out of bounds.", _gridManager); return finalPath; }

        InitializePathNodeGrid();

        PathNode startNode = GetPathNode(startPosPlayable.x, startPosPlayable.y);
        PathNode endNodeOriginalQuery = GetPathNode(endPosPlayable.x, endPosPlayable.y);

        if (!startNode.isWalkable) { DebugHelper.LogWarning($"PF Error: Start not walkable.", _gridManager); return finalPath; }
        if (!endNodeOriginalQuery.isWalkable) { DebugHelper.LogWarning($"PF Error: End not walkable.", _gridManager); return finalPath; }

        Tile endTile = _gridManager.GetTile(endPosPlayable);
        if (endTile != null && endTile.IsOccupied && endTile.occupyingUnit != requestingUnit) { DebugHelper.Log($"PF Error: End tile occupied.", _gridManager); return finalPath; }

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

            // Simple Open List handling: Find lowest F-cost node
            _openList = _openList.OrderBy(node => node.FCost).ThenBy(node => node.hCost).ToList();
            PathNode currentNode = _openList[0];

            if (currentNode.gridPosition == endPosPlayable)
            {
                PathNode endNodeFromGrid = _pathNodeGrid[endPosPlayable.x, endPosPlayable.y];
                return ReconstructPath(endNodeFromGrid, startNode);
            }

            _openList.RemoveAt(0); // Remove the node with lowest F-cost
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

                 // Check if the neighbor is already in the open list
                PathNode existingNeighborInOpen = default;
                bool wasInOpenList = false;
                foreach(var node in _openList) {
                    if (node.gridPosition == neighborPos) {
                        existingNeighborInOpen = node;
                        wasInOpenList = true;
                        break;
                    }
                }


                // If this path is better than any previous path, OR if it's not in Open List yet
                if (tentativeGCost < neighborNodeData.gCost || !wasInOpenList)
                {
                    neighborNodeData.gCost = tentativeGCost;
                    neighborNodeData.hCost = CalculateManhattanDistance(neighborPos, endPosPlayable);
                    neighborNodeData.parentPosition = currentNode.gridPosition;
                    _pathNodeGrid[neighborPos.x, neighborPos.y] = neighborNodeData; // Update the grid copy

                    if (!wasInOpenList)
                    {
                        _openList.Add(neighborNodeData);
                    }
                    // No need to explicitly update if already in list, the re-sort handles it.
                    // Re-sorting happens at the start of the next loop iteration.
                }
            }
            // Re-sort is handled by LINQ OrderBy at the start of the loop
        }

        DebugHelper.Log($"Pathfinder: No path found from {startPosPlayable} to {endPosPlayable}.", _gridManager);
        return finalPath;
    } // End of FindPath


    /// <summary>
    /// Finds all playable tiles reachable from a start position within a given movement point budget.
    /// Considers terrain movement costs and basic unit occupancy.
    /// </summary>
    /// <param name="startPosPlayable">The starting playable grid coordinate.</param>
    /// <param name="movementPoints">The maximum movement points allowed.</param>
    /// <param name="requestingUnit">The unit requesting the check (can be null, affects occupancy checks).</param>
    /// <returns>A List of Tile objects representing all reachable tiles (inclusive of start tile if movementPoints >= 0).
    /// Returns an empty list if start position is invalid or movementPoints is negative.</returns>
    public List<Tile> GetReachableTiles(Vector2Int startPosPlayable, int movementPoints, Unit requestingUnit = null)
    {
        List<Tile> reachableTiles = new List<Tile>();
        if (_gridManager == null) { Debug.LogError("Pathfinder Error: No GridManager."); return reachableTiles; }
        if (movementPoints < 0) { return reachableTiles; } // Cannot reach anywhere with negative movement
        if (!_gridManager.IsInPlayableBounds(startPosPlayable)) { DebugHelper.LogWarning($"GetReachableTiles: Start pos {startPosPlayable} out of bounds.", _gridManager); return reachableTiles; }

        InitializePathNodeGrid(); // Get terrain walkability

        PathNode startNode = GetPathNode(startPosPlayable.x, startPosPlayable.y);
        if (!startNode.isWalkable) { DebugHelper.LogWarning($"GetReachableTiles: Start pos {startPosPlayable} not walkable.", _gridManager); return reachableTiles; }

        _openList.Clear();
        _closedSet.Clear();

        startNode.gCost = 0; // Cost to reach start is 0
        // H cost is irrelevant for finding reachable tiles, but A* structure uses it. Set to 0.
        startNode.hCost = 0;
        _pathNodeGrid[startPosPlayable.x, startPosPlayable.y] = startNode; // Update grid

        _openList.Add(startNode);

        int emergencyExit = 0;
        int maxIterations = _gridManager.playableWidth * _gridManager.playableHeight * 2;

        while (_openList.Count > 0)
        {
            emergencyExit++;
            if (emergencyExit > maxIterations) { DebugHelper.LogError("Pathfinder Error: Max iterations exceeded in GetReachableTiles.", _gridManager); break; }

            // Get node with lowest G-COST (movement points used)
             _openList = _openList.OrderBy(node => node.gCost).ToList(); // Sort by G-cost
            PathNode currentNode = _openList[0];
            _openList.RemoveAt(0);


            // If the cost to reach this node exceeds the budget, skip it and its neighbors (via this path)
            if (currentNode.gCost > movementPoints)
            {
                continue;
            }

             // Add to reachable list and closed set
            Tile currentTile = _gridManager.GetTile(currentNode.gridPosition);
            if (currentTile != null)
            {
                reachableTiles.Add(currentTile); // Add the tile itself
            }
            _closedSet.Add(currentNode.gridPosition);

            // Explore neighbors
            List<Tile> neighborTiles = _gridManager.GetNeighbors(currentNode.gridPosition);
            foreach (Tile neighborTile in neighborTiles)
            {
                 Vector2Int neighborPos = neighborTile.gridPosition;
                if (_closedSet.Contains(neighborPos)) continue;

                PathNode neighborNodeData = GetPathNode(neighborPos.x, neighborPos.y);

                if (!neighborNodeData.isWalkable) { _closedSet.Add(neighborPos); continue; }

                // Cannot move *into* an occupied tile (unless it's the start tile potentially, if unit hasn't moved yet - handle this nuance?)
                // For finding reachable range, generally cannot end turn on occupied tile.
                if (neighborTile.IsOccupied && neighborTile.occupyingUnit != requestingUnit) { _closedSet.Add(neighborPos); continue; }


                int movementCostToNeighbor = neighborTile.GetMovementCost();
                if (movementCostToNeighbor >= 255) { _closedSet.Add(neighborPos); continue; }


                int tentativeGCost = currentNode.gCost + movementCostToNeighbor;

                 // If the cost to reach neighbor is within budget
                if (tentativeGCost <= movementPoints)
                {
                    // Check if the neighbor is already in the open list
                    PathNode existingNeighborInOpen = default;
                    bool wasInOpenList = false;
                    foreach(var node in _openList) {
                        if (node.gridPosition == neighborPos) {
                            existingNeighborInOpen = node;
                            wasInOpenList = true;
                            break;
                        }
                    }

                    // If this path is better OR if neighbor not yet considered for open list
                    if (tentativeGCost < neighborNodeData.gCost || !wasInOpenList)
                    {
                        neighborNodeData.gCost = tentativeGCost;
                        // H-cost not used for sorting here, parent not strictly needed unless reconstructing paths later
                        // neighborNodeData.hCost = 0;
                        // neighborNodeData.parentPosition = currentNode.gridPosition;

                        _pathNodeGrid[neighborPos.x, neighborPos.y] = neighborNodeData;

                        if (!wasInOpenList)
                        {
                            _openList.Add(neighborNodeData);
                        }
                        // If it was already in the open list with a higher G-cost,
                        // the new lower G-cost will ensure it gets processed correctly
                        // due to the sort at the start of the loop.
                    }
                }
            }
            // Re-sort happens at the start of the next loop iteration
        }
        return reachableTiles;
    } // End of GetReachableTiles


    // --- Helper Methods ---

    private void InitializePathNodeGrid()
    {
        int width = _gridManager.playableWidth;
        int height = _gridManager.playableHeight;
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
        List<Tile> path = new List<Tile>();
        PathNode current = endNodeArg;
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
} // End of Pathfinder class