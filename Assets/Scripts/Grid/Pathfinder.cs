// Pathfinder.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Pathfinder
{
    private GridManager _gridManager;
    private List<PathNode> _openList;
    private HashSet<Vector2Int> _closedSet;
    private PathNode[,] _pathNodeGrid; 

    private struct PathNode
    {
        public Vector2Int gridPosition;
        public int gCost; 
        public int hCost; 
        public Vector2Int parentPosition; 
        public bool isWalkable; 

        public int FCost => gCost + hCost; 

        public PathNode(Vector2Int pos, bool walkableInitial)
        {
            gridPosition = pos;
            isWalkable = walkableInitial; 
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
            return;
        }
        _gridManager = gridManager;
        _openList = new List<PathNode>();
        _closedSet = new HashSet<Vector2Int>();
    }

    public List<Tile> FindPath(Vector2Int startPosPlayable, Vector2Int endPosPlayable, Unit requestingUnit = null, bool findAdjacentToTargetInstead = false)
    {
        // REMOVED: Initial verbose Debug.Log for FindPath parameters

        List<Tile> finalPath = new List<Tile>();
        if (_gridManager == null) { Debug.LogError("Pathfinder Error: GridManager is null."); return finalPath; }

        if (!_gridManager.IsInPlayableBounds(startPosPlayable) || !_gridManager.IsInPlayableBounds(endPosPlayable))
        {
            DebugHelper.LogWarning($"Pathfinder Error: Start ({startPosPlayable}) or End ({endPosPlayable}) is out of playable bounds.", _gridManager);
            return finalPath;
        }

        InitializePathNodeGrid(requestingUnit); 

        PathNode startNode = _pathNodeGrid[startPosPlayable.x, startPosPlayable.y];
        Tile targetActualTile = _gridManager.GetTile(endPosPlayable); 

        if (!startNode.isWalkable) 
        {
            DebugHelper.LogWarning($"Pathfinder Error: Start position {startPosPlayable} is not walkable for {requestingUnit?.unitName}.", _gridManager);
            return finalPath;
        }

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
        _pathNodeGrid[startPosPlayable.x, startPosPlayable.y] = startNode; 
        _openList.Add(startNode);

        int iterations = 0;
        int maxIterations = _gridManager.PlayableWidth * _gridManager.PlayableHeight + 100; 

        while (_openList.Count > 0)
        {
            iterations++;
            if (iterations > maxIterations)
            {
                DebugHelper.LogError("Pathfinder Error: Max iterations exceeded in FindPath.", _gridManager);
                return finalPath; 
            }

            _openList = _openList.OrderBy(node => node.FCost).ThenBy(node => node.hCost).ToList();
            PathNode currentNode = _openList[0];
            _openList.RemoveAt(0);
            _closedSet.Add(currentNode.gridPosition);

            if (findAdjacentToTargetInstead)
            {
                if (CalculateManhattanDistance(currentNode.gridPosition, endPosPlayable) == 1)
                {
                    Tile currentTileAsDestination = _gridManager.GetTile(currentNode.gridPosition);
                    if (currentNode.isWalkable && 
                        (currentTileAsDestination == null || !currentTileAsDestination.IsOccupied || currentTileAsDestination.occupyingUnit == requestingUnit))
                    {
                        // COMMENTED OUT for noise reduction during path preview
                        // DebugHelper.Log($"Pathfinder (Adjacent): Path found to {currentNode.gridPosition} (adjacent to {endPosPlayable}). Iterations: {iterations}", _gridManager);
                        return ReconstructPath(_pathNodeGrid[currentNode.gridPosition.x, currentNode.gridPosition.y], startNode);
                    }
                }
            }
            else 
            {
                if (currentNode.gridPosition == endPosPlayable)
                {
                    // COMMENTED OUT for noise reduction during path preview
                    // DebugHelper.Log($"Pathfinder (Direct): Path found to {endPosPlayable}. Iterations: {iterations}", _gridManager);
                    return ReconstructPath(_pathNodeGrid[currentNode.gridPosition.x, currentNode.gridPosition.y], startNode);
                }
            }

            List<Tile> neighborGridTiles = _gridManager.GetNeighbors(currentNode.gridPosition, false); 
            foreach (Tile neighborTile in neighborGridTiles)
            {
                Vector2Int neighborPos = neighborTile.gridPosition;
                if (_closedSet.Contains(neighborPos)) continue; 

                PathNode neighborNode = _pathNodeGrid[neighborPos.x, neighborPos.y];

                if (!neighborNode.isWalkable) 
                {
                    _closedSet.Add(neighborPos); 
                    continue;
                }
                
                bool isOccupiedByOtherUnit = neighborTile.IsOccupied && neighborTile.occupyingUnit != requestingUnit;
                
                if (findAdjacentToTargetInstead)
                {
                    if (neighborPos != endPosPlayable && isOccupiedByOtherUnit)
                    {
                        _closedSet.Add(neighborPos); continue;
                    }
                }
                else 
                {
                    if (isOccupiedByOtherUnit) 
                    {
                         _closedSet.Add(neighborPos); continue;
                    }
                }

                int moveCostToNeighbor = neighborTile.GetMovementCost(requestingUnit);
                int tentativeGCost = _pathNodeGrid[currentNode.gridPosition.x, currentNode.gridPosition.y].gCost + moveCostToNeighbor;

                if (tentativeGCost < neighborNode.gCost) 
                {
                    neighborNode.parentPosition = currentNode.gridPosition;
                    neighborNode.gCost = tentativeGCost;
                    neighborNode.hCost = CalculateManhattanDistance(neighborPos, endPosPlayable); 
                    _pathNodeGrid[neighborPos.x, neighborPos.y] = neighborNode; 

                    if (!_openList.Any(n => n.gridPosition == neighborPos)) 
                    {
                        _openList.Add(neighborNode);
                    }
                }
            }
        }
        // Keep this one as it's a significant outcome
        DebugHelper.LogWarning($"Pathfinder: No path found from {startPosPlayable} to " +
                               $"{(findAdjacentToTargetInstead ? "adjacent of " : "")}{endPosPlayable} " +
                               $"for unit {requestingUnit?.unitName}. Iterations: {iterations}", _gridManager);
        return finalPath; 
    }

    public List<Tile> GetReachableTiles(Vector2Int startPosPlayable, int movementPoints, Unit requestingUnit = null)
    {
        // Debug.Log($"Pathfinder.GetReachableTiles: StartPos={startPosPlayable}, MaxMove={movementPoints}, Unit={requestingUnit?.unitName ?? "N/A"}", _gridManager); // Already logged by PIH.ShowReachableRange

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
        startNode.hCost = 0; 
        _pathNodeGrid[startPosPlayable.x, startPosPlayable.y] = startNode;
        _openList.Add(startNode);

        int iterations = 0;
        int maxIterations = _gridManager.PlayableWidth * _gridManager.PlayableHeight + 100;

        while (_openList.Count > 0)
        {
            iterations++;
            if (iterations > maxIterations) { DebugHelper.LogError("Pathfinder Error: Max iterations in GetReachableTiles.", _gridManager); break; }

            _openList = _openList.OrderBy(node => node.gCost).ToList(); 
            PathNode currentNode = _openList[0];
            _openList.RemoveAt(0);

            if (currentNode.gCost > movementPoints) continue;

            _closedSet.Add(currentNode.gridPosition);
            Tile currentTileFromGrid = _gridManager.GetTile(currentNode.gridPosition);
            if (currentTileFromGrid != null)
            {
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

                if (neighborTile.IsOccupied && neighborTile.occupyingUnit != requestingUnit && neighborTile.occupyingUnit != null)
                {
                    _closedSet.Add(neighborPos); continue;
                }

                int moveCostToNeighbor = neighborTile.GetMovementCost(requestingUnit);
                int tentativeGCost = _pathNodeGrid[currentNode.gridPosition.x, currentNode.gridPosition.y].gCost + moveCostToNeighbor;

                if (tentativeGCost <= movementPoints) 
                {
                    if (tentativeGCost < neighborNode.gCost) 
                    {
                        neighborNode.gCost = tentativeGCost;
                        _pathNodeGrid[neighborPos.x, neighborPos.y] = neighborNode; 
                        
                        if (!_openList.Any(n => n.gridPosition == neighborPos))
                        {
                            _openList.Add(neighborNode);
                        }
                    }
                }
            }
        }
        // This is logged by PIH.ShowReachableRange
        // Debug.Log($"Pathfinder.GetReachableTiles: Returning {reachableTiles.Count} tiles. Iterations: {iterations}", _gridManager);
        return reachableTiles;
    }

    private void InitializePathNodeGrid(Unit requestingUnit = null)
    {
        int width = _gridManager.PlayableWidth;   
        int height = _gridManager.PlayableHeight; 
        _pathNodeGrid = new PathNode[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile tile = _gridManager.GetTile(x, y); 
                bool walkable = false;
                if (tile != null)
                {
                    walkable = tile.GetMovementCost(requestingUnit) < Tile.IMPASSABLE_COST;
                }
                _pathNodeGrid[x, y] = new PathNode(new Vector2Int(x, y), walkable);
            }
        }
    }
    
    public int CalculateManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private List<Tile> ReconstructPath(PathNode endNode, PathNode startNode)
    {
        List<Tile> path = new List<Tile>();
        PathNode currentNode = endNode;
        int safetyCounter = 0;
        int maxPathLength = _gridManager.PlayableWidth * _gridManager.PlayableHeight; 

        while (currentNode.gridPosition != startNode.gridPosition)
        {
            safetyCounter++;
            if (safetyCounter > maxPathLength)
            {
                DebugHelper.LogError("Pathfinder Error: Max path length exceeded in ReconstructPath.", _gridManager);
                return new List<Tile>(); 
            }

            Tile tileOnPath = _gridManager.GetTile(currentNode.gridPosition);
            if (tileOnPath != null)
            {
                path.Add(tileOnPath);
            }
            else
            {
                DebugHelper.LogError($"Pathfinder Error: Null tile encountered at {currentNode.gridPosition} during path reconstruction.", _gridManager);
                return new List<Tile>(); 
            }

            if (currentNode.parentPosition.x == -1 || currentNode.parentPosition.y == -1) {
                 DebugHelper.LogError($"Pathfinder Error: Node {currentNode.gridPosition} in path reconstruction has no parent before reaching start {startNode.gridPosition}.", _gridManager);
                 return new List<Tile>(); 
            }
            if (!_gridManager.IsInPlayableBounds(currentNode.parentPosition)) {
                 DebugHelper.LogError($"Pathfinder Error: Parent {currentNode.parentPosition} of node {currentNode.gridPosition} is out of bounds.", _gridManager);
                 return new List<Tile>(); 
            }

            currentNode = _pathNodeGrid[currentNode.parentPosition.x, currentNode.parentPosition.y];
        }
        path.Reverse();
        return path;
    }
}