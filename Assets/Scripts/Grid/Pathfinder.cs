// Pathfinder.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Pathfinder
{
    private GridManager _gridManager;
    private List<PathNode> _openList;
    private HashSet<Vector2Int> _closedSet;
    private PathNode[,] _pathNodeGrid; // Stores the PathNode data for the current search

    // Nested struct for pathfinding nodes
    private struct PathNode
    {
        public Vector2Int gridPosition;
        public int gCost; // Cost from start to this node
        public int hCost; // Heuristic cost from this node to end
        public Vector2Int parentPosition; // Position of the node this one came from
        public bool isWalkable; // Is this tile generally walkable (ignoring temp occupancy for init)

        public int FCost => gCost + hCost; // Total estimated cost

        public PathNode(Vector2Int pos, bool walkableInitial)
        {
            gridPosition = pos;
            isWalkable = walkableInitial; // Initial walkability based on terrain
            gCost = int.MaxValue;
            hCost = 0;
            parentPosition = new Vector2Int(-1, -1); // Using -1,-1 to denote no parent
        }

        public void ResetCosts()
        {
            gCost = int.MaxValue;
            hCost = 0;
            parentPosition = new Vector2Int(-1, -1);
        }
    }

    public Pathfinder(GridManager gridManager)
    {
        if (gridManager == null)
        {
            Debug.LogError("[Pathfinder] GridManager constructor parameter is null!");
            // Potentially throw an exception or handle this more gracefully
            return;
        }
        _gridManager = gridManager;
        _openList = new List<PathNode>();
        _closedSet = new HashSet<Vector2Int>();
        // _pathNodeGrid is initialized at the start of each pathfinding call
    }

    public List<Tile> FindPath(Vector2Int startPosPlayable, Vector2Int endPosPlayable, Unit requestingUnit = null, bool findAdjacentToTargetInstead = false)
    {
        List<Tile> finalPath = new List<Tile>();
        if (_gridManager == null) { Debug.LogError("Pathfinder Error: GridManager is null."); return finalPath; }

        if (!_gridManager.IsInPlayableBounds(startPosPlayable) || !_gridManager.IsInPlayableBounds(endPosPlayable))
        {
            DebugHelper.LogWarning($"Pathfinder Error: Start ({startPosPlayable}) or End ({endPosPlayable}) is out of playable bounds.", _gridManager);
            return finalPath;
        }

        InitializePathNodeGrid(requestingUnit); // Initialize/reset grid for this search

        PathNode startNode = _pathNodeGrid[startPosPlayable.x, startPosPlayable.y];
        // Target's actual tile. We might path to a neighbor of this.
        Tile targetActualTile = _gridManager.GetTile(endPosPlayable); 

        if (!startNode.isWalkable) // isWalkable here considers terrain cost for the unit
        {
            DebugHelper.LogWarning($"Pathfinder Error: Start position {startPosPlayable} is not walkable for {requestingUnit?.unitName}.", _gridManager);
            return finalPath;
        }

        // If not finding adjacent, the end tile itself must be a valid destination
        if (!findAdjacentToTargetInstead)
        {
            PathNode endNodeCheck = _pathNodeGrid[endPosPlayable.x, endPosPlayable.y];
            if (!endNodeCheck.isWalkable)
            {
                DebugHelper.LogWarning($"Pathfinder Error: Target destination {endPosPlayable} is not walkable for {requestingUnit?.unitName}.", _gridManager);
                return finalPath;
            }
            if (targetActualTile != null && targetActualTile.IsOccupied && targetActualTile.occupyingUnit != requestingUnit)
            {
                DebugHelper.Log($"Pathfinder Error: Target destination {endPosPlayable} is occupied by {targetActualTile.occupyingUnit?.unitName}.", _gridManager);
                return finalPath;
            }
        }

        _openList.Clear();
        _closedSet.Clear();

        startNode.gCost = 0;
        startNode.hCost = CalculateManhattanDistance(startPosPlayable, endPosPlayable);
        _pathNodeGrid[startPosPlayable.x, startPosPlayable.y] = startNode; // Update grid with modified startNode
        _openList.Add(startNode);

        int iterations = 0;
        int maxIterations = _gridManager.playableWidth * _gridManager.playableHeight + 100; // Safety break

        while (_openList.Count > 0)
        {
            iterations++;
            if (iterations > maxIterations)
            {
                DebugHelper.LogError("Pathfinder Error: Max iterations exceeded in FindPath.", _gridManager);
                return finalPath; // Return empty path
            }

            // Get node with lowest F cost, then H cost as tie-breaker
            _openList = _openList.OrderBy(node => node.FCost).ThenBy(node => node.hCost).ToList();
            PathNode currentNode = _openList[0];
            _openList.RemoveAt(0);
            _closedSet.Add(currentNode.gridPosition);

            // --- Goal Check ---
            if (findAdjacentToTargetInstead)
            {
                // Is currentNode adjacent to the original endPosPlayable?
                if (CalculateManhattanDistance(currentNode.gridPosition, endPosPlayable) == 1)
                {
                    // And is it a valid tile to stand on? (walkable, not occupied by others)
                    Tile currentTileAsDestination = _gridManager.GetTile(currentNode.gridPosition);
                    if (currentNode.isWalkable && 
                        (currentTileAsDestination == null || !currentTileAsDestination.IsOccupied || currentTileAsDestination.occupyingUnit == requestingUnit))
                    {
                        DebugHelper.Log($"Pathfinder (Adjacent): Path found to {currentNode.gridPosition} (adjacent to {endPosPlayable}).", _gridManager);
                        return ReconstructPath(_pathNodeGrid[currentNode.gridPosition.x, currentNode.gridPosition.y], startNode);
                    }
                }
            }
            else // Standard pathfinding: goal is endPosPlayable itself
            {
                if (currentNode.gridPosition == endPosPlayable)
                {
                    DebugHelper.Log($"Pathfinder (Direct): Path found to {endPosPlayable}.", _gridManager);
                    return ReconstructPath(_pathNodeGrid[currentNode.gridPosition.x, currentNode.gridPosition.y], startNode);
                }
            }

            // --- Process Neighbors ---
            List<Tile> neighborGridTiles = _gridManager.GetNeighbors(currentNode.gridPosition, false); // Get 4-directional neighbors
            foreach (Tile neighborTile in neighborGridTiles)
            {
                Vector2Int neighborPos = neighborTile.gridPosition;
                if (_closedSet.Contains(neighborPos)) continue; // Already processed

                PathNode neighborNode = _pathNodeGrid[neighborPos.x, neighborPos.y];

                if (!neighborNode.isWalkable) // Check walkability for the requesting unit (terrain cost)
                {
                    _closedSet.Add(neighborPos); // Mark as unwalkable for this search
                    continue;
                }

                // Occupancy check: Can't move onto a tile occupied by another unit.
                // If finding adjacent, the actual target tile (endPosPlayable) can be occupied by the target.
                // Any *other* tile on the path, including the chosen adjacent spot, cannot be occupied by a *different* unit.
                bool isOccupiedByOtherUnit = neighborTile.IsOccupied && neighborTile.occupyingUnit != requestingUnit;
                
                if (findAdjacentToTargetInstead)
                {
                    // If this neighbor IS the target's actual tile, allow considering it (it might be occupied by target)
                    // But if it's any OTHER tile and occupied by someone else, it's a no-go.
                    if (neighborPos != endPosPlayable && isOccupiedByOtherUnit)
                    {
                        _closedSet.Add(neighborPos); continue;
                    }
                    // If neighborPos == endPosPlayable, we don't block it based on occupancy here,
                    // because we are not trying to *land* on it, just path *near* it.
                    // The goal check (is current node adjacent and standable) handles the final spot.
                }
                else // Direct pathing
                {
                    if (isOccupiedByOtherUnit) // Cannot move onto tile occupied by another if pathing directly
                    {
                         _closedSet.Add(neighborPos); continue;
                    }
                }


                int moveCostToNeighbor = neighborTile.GetMovementCost(requestingUnit);
                // This check should be redundant if InitializePathNodeGrid sets isWalkable correctly based on cost
                // if (moveCostToNeighbor >= Tile.IMPASSABLE_COST) { _closedSet.Add(neighborPos); continue; }


                int tentativeGCost = _pathNodeGrid[currentNode.gridPosition.x, currentNode.gridPosition.y].gCost + moveCostToNeighbor;

                if (tentativeGCost < neighborNode.gCost) // Found a cheaper path to this neighbor
                {
                    neighborNode.parentPosition = currentNode.gridPosition;
                    neighborNode.gCost = tentativeGCost;
                    // H-cost is always to the original target tile, even if pathing adjacent
                    neighborNode.hCost = CalculateManhattanDistance(neighborPos, endPosPlayable); 
                    
                    _pathNodeGrid[neighborPos.x, neighborPos.y] = neighborNode; // Update the grid with new costs/parent

                    if (!_openList.Any(n => n.gridPosition == neighborPos)) // Add to open list if not already there
                    {
                        _openList.Add(neighborNode);
                    }
                }
            }
        }

        DebugHelper.LogWarning($"Pathfinder: No path found from {startPosPlayable} to " +
                               $"{(findAdjacentToTargetInstead ? "adjacent of " : "")}{endPosPlayable} " +
                               $"for unit {requestingUnit?.unitName}.", _gridManager);
        return finalPath; // Return empty list if no path found
    }

    public List<Tile> GetReachableTiles(Vector2Int startPosPlayable, int movementPoints, Unit requestingUnit = null)
    {
        List<Tile> reachableTiles = new List<Tile>();
        if (_gridManager == null) { Debug.LogError("Pathfinder Error: GridManager is null."); return reachableTiles; }
        if (movementPoints < 0) return reachableTiles;
        if (!_gridManager.IsInPlayableBounds(startPosPlayable))
        {
            DebugHelper.LogWarning($"GetReachableTiles: Start position {startPosPlayable} is out of bounds.", _gridManager);
            return reachableTiles;
        }

        InitializePathNodeGrid(requestingUnit);
        PathNode startNode = _pathNodeGrid[startPosPlayable.x, startPosPlayable.y];

        if (!startNode.isWalkable)
        {
            DebugHelper.LogWarning($"GetReachableTiles: Start position {startPosPlayable} is not walkable for {requestingUnit?.unitName}.", _gridManager);
            return reachableTiles;
        }

        _openList.Clear();
        _closedSet.Clear();

        startNode.gCost = 0;
        startNode.hCost = 0; // Not used for Dijkstra-like flood fill
        _pathNodeGrid[startPosPlayable.x, startPosPlayable.y] = startNode;
        _openList.Add(startNode);

        int iterations = 0;
        int maxIterations = _gridManager.playableWidth * _gridManager.playableHeight + 100;

        while (_openList.Count > 0)
        {
            iterations++;
            if (iterations > maxIterations) { DebugHelper.LogError("Pathfinder Error: Max iterations in GetReachableTiles.", _gridManager); break; }

            _openList = _openList.OrderBy(node => node.gCost).ToList(); // Dijkstra: process closest first
            PathNode currentNode = _openList[0];
            _openList.RemoveAt(0);

            // If current node's cost exceeds movement points, skip (already handled by check before adding)
            // but as a safeguard:
            if (currentNode.gCost > movementPoints) continue;

            _closedSet.Add(currentNode.gridPosition);
            Tile currentTileFromGrid = _gridManager.GetTile(currentNode.gridPosition);
            if (currentTileFromGrid != null)
            {
                // Add to reachable if not occupied by another unit (or if it's the start tile itself)
                 if (!currentTileFromGrid.IsOccupied || currentTileFromGrid.occupyingUnit == requestingUnit || currentTileFromGrid.occupyingUnit == null)
                {
                    reachableTiles.Add(currentTileFromGrid);
                }
            }

            List<Tile> neighborGridTiles = _gridManager.GetNeighbors(currentNode.gridPosition, false);
            foreach (Tile neighborTile in neighborGridTiles)
            {
                Vector2Int neighborPos = neighborTile.gridPosition;
                if (_closedSet.Contains(neighborPos)) continue;

                PathNode neighborNode = _pathNodeGrid[neighborPos.x, neighborPos.y];
                if (!neighborNode.isWalkable) { _closedSet.Add(neighborPos); continue; }

                // Cannot move onto tiles occupied by other units
                if (neighborTile.IsOccupied && neighborTile.occupyingUnit != requestingUnit && neighborTile.occupyingUnit != null)
                {
                    _closedSet.Add(neighborPos); continue;
                }

                int moveCostToNeighbor = neighborTile.GetMovementCost(requestingUnit);
                // if (moveCostToNeighbor >= Tile.IMPASSABLE_COST) { _closedSet.Add(neighborPos); continue; } // Redundant if isWalkable is correct

                int tentativeGCost = _pathNodeGrid[currentNode.gridPosition.x, currentNode.gridPosition.y].gCost + moveCostToNeighbor;

                if (tentativeGCost <= movementPoints) // Check if within movement range
                {
                    if (tentativeGCost < neighborNode.gCost) // If new path is cheaper
                    {
                        neighborNode.gCost = tentativeGCost;
                        _pathNodeGrid[neighborPos.x, neighborPos.y] = neighborNode; // Update G cost in grid
                        
                        if (!_openList.Any(n => n.gridPosition == neighborPos))
                        {
                            _openList.Add(neighborNode);
                        }
                    }
                }
            }
        }
        return reachableTiles;
    }

    private void InitializePathNodeGrid(Unit requestingUnit = null)
    {
        int width = _gridManager.playableWidth;
        int height = _gridManager.playableHeight;
        _pathNodeGrid = new PathNode[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile tile = _gridManager.GetTile(x, y);
                bool walkable = false;
                if (tile != null)
                {
                    // A tile's initial walkability for pathfinding node grid depends on terrain cost for the unit
                    walkable = tile.GetMovementCost(requestingUnit) < Tile.IMPASSABLE_COST;
                }
                _pathNodeGrid[x, y] = new PathNode(new Vector2Int(x, y), walkable);
                // PathNode struct constructor already sets gCost to int.MaxValue and parent to -1,-1
            }
        }
    }

    // CalculateManhattanDistance is used internally and by BasicAIHandler
    public int CalculateManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private List<Tile> ReconstructPath(PathNode endNode, PathNode startNode)
    {
        List<Tile> path = new List<Tile>();
        PathNode currentNode = endNode;
        int safetyCounter = 0;
        int maxPathLength = _gridManager.playableWidth * _gridManager.playableHeight;

        while (currentNode.gridPosition != startNode.gridPosition)
        {
            safetyCounter++;
            if (safetyCounter > maxPathLength)
            {
                DebugHelper.LogError("Pathfinder Error: Max path length exceeded in ReconstructPath. Possible loop or broken parent chain.", _gridManager);
                return new List<Tile>(); // Return empty path
            }

            Tile tileOnPath = _gridManager.GetTile(currentNode.gridPosition);
            if (tileOnPath != null)
            {
                path.Add(tileOnPath);
            }
            else
            {
                DebugHelper.LogError($"Pathfinder Error: Null tile encountered at {currentNode.gridPosition} during path reconstruction.", _gridManager);
                return new List<Tile>(); // Critical error
            }

            // Check for valid parent
            if (currentNode.parentPosition.x == -1 || currentNode.parentPosition.y == -1) {
                 DebugHelper.LogError($"Pathfinder Error: Node {currentNode.gridPosition} in path reconstruction has no parent before reaching start {startNode.gridPosition}.", _gridManager);
                 return new List<Tile>(); // Broken path
            }
            if (!_gridManager.IsInPlayableBounds(currentNode.parentPosition)) {
                 DebugHelper.LogError($"Pathfinder Error: Parent {currentNode.parentPosition} of node {currentNode.gridPosition} is out of bounds.", _gridManager);
                 return new List<Tile>(); // Broken path
            }

            currentNode = _pathNodeGrid[currentNode.parentPosition.x, currentNode.parentPosition.y];
        }
        // Path is reconstructed from end to start, so reverse it.
        path.Reverse();
        return path;
    }
}