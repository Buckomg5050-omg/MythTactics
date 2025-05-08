// Pathfinder.cs
using UnityEngine;
using System.Collections.Generic; // For List<PathNode> and List<Tile>

public class Pathfinder
{
    private GridManager _gridManager;
    private List<PathNode> _openList;
    private HashSet<Vector2Int> _closedSet;
    private PathNode[,] _pathNodeGrid;

    public Pathfinder(GridManager gridManager)
    {
        if (gridManager == null)
        {
            Debug.LogError("[Pathfinder] GridManager reference cannot be null!");
            _gridManager = null; 
            return;
        }
        _gridManager = gridManager;
        _openList = new List<PathNode>();
        _closedSet = new HashSet<Vector2Int>();
    }

    public List<Tile> FindPath(Vector2Int startPosPlayable, Vector2Int endPosPlayable, Unit requestingUnit = null)
    {
        List<Tile> finalPath = new List<Tile>();

        if (_gridManager == null) {
            Debug.LogError("Pathfinder cannot operate without a GridManager instance.");
            return finalPath;
        }

        if (!_gridManager.IsInPlayableBounds(startPosPlayable) || !_gridManager.IsInPlayableBounds(endPosPlayable))
        {
            DebugHelper.LogWarning($"Pathfinder: Start ({startPosPlayable}) or End ({endPosPlayable}) pos is out of bounds.", _gridManager);
            return finalPath;
        }

        InitializePathNodeGrid();

        PathNode startNode = GetPathNode(startPosPlayable.x, startPosPlayable.y);
        PathNode endNodeOriginalQuery = GetPathNode(endPosPlayable.x, endPosPlayable.y); // Original end node for comparison

        if (!startNode.isWalkable) 
        {
            DebugHelper.LogWarning($"Pathfinder: Start node {startPosPlayable} is on non-walkable terrain.", _gridManager);
            return finalPath;
        }
        if (!endNodeOriginalQuery.isWalkable) 
        {
             DebugHelper.LogWarning($"Pathfinder: End node {endPosPlayable} is on non-walkable terrain.", _gridManager);
            return finalPath;
        }
        
        Tile endTile = _gridManager.GetTile(endPosPlayable);
        if (endTile != null && endTile.IsOccupied && endTile.occupyingUnit != requestingUnit)
        {
            string requesterName = requestingUnit != null ? requestingUnit.unitName : "Unknown Unit";
            DebugHelper.Log($"Pathfinder: End tile {endPosPlayable} is occupied by {endTile.occupyingUnit.unitName}. Cannot be pathed to by {requesterName}.", _gridManager);
            return finalPath;
        }

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
            if (emergencyExit > maxIterations)
            {
                DebugHelper.LogError("Pathfinder: Exceeded max iterations! Possible infinite loop.", _gridManager);
                return finalPath;
            }

            PathNode currentNode = _openList[0];
            for (int i = 1; i < _openList.Count; i++)
            {
                if (_openList[i].FCost < currentNode.FCost || (_openList[i].FCost == currentNode.FCost && _openList[i].hCost < currentNode.hCost))
                {
                    currentNode = _openList[i];
                }
            }

            // Check if current node is the target
            if (currentNode.gridPosition == endPosPlayable) // Compare with the target gridPosition
            {
                // ----- ADDED DEBUGGING -----
                DebugHelper.Log($"Pathfinder: Path found! CurrentNode ({currentNode.gridPosition}) matches EndNode target ({endPosPlayable}).", _gridManager);
                DebugHelper.Log($"CurrentNode Details: G={currentNode.gCost}, H={currentNode.hCost}, Parent={currentNode.parentPosition}", _gridManager);
                PathNode endNodeFromGrid = _pathNodeGrid[endPosPlayable.x, endPosPlayable.y]; // Get the definitive end node from grid
                DebugHelper.Log($"EndNodeFromGrid Details: G={endNodeFromGrid.gCost}, H={endNodeFromGrid.hCost}, Parent={endNodeFromGrid.parentPosition}", _gridManager);
                // ----- END ADDED DEBUGGING -----
                
                return ReconstructPath(endNodeFromGrid, startNode); // Use the node from _pathNodeGrid for reconstruction
            }

            _openList.Remove(currentNode);
            _closedSet.Add(currentNode.gridPosition);

            List<Tile> neighborTiles = _gridManager.GetNeighbors(currentNode.gridPosition); 
            foreach (Tile neighborTile in neighborTiles)
            {
                Vector2Int neighborPos = neighborTile.gridPosition;
                if (_closedSet.Contains(neighborPos)) continue;

                PathNode neighborNodeData = GetPathNode(neighborPos.x, neighborPos.y);

                if (!neighborNodeData.isWalkable) {
                    _closedSet.Add(neighborPos); 
                    continue;
                }
                
                if (neighborPos != endPosPlayable && neighborTile.IsOccupied && neighborTile.occupyingUnit != requestingUnit)
                {
                    _closedSet.Add(neighborPos); 
                    continue;
                }

                int movementCostToNeighbor = neighborTile.GetMovementCost();
                if (movementCostToNeighbor >= 255) { 
                     _closedSet.Add(neighborPos);
                     continue;
                }

                int tentativeGCost = currentNode.gCost + movementCostToNeighbor;

                bool wasInOpenList = false; 
                int openListIndex = -1; // Not strictly needed with sort, but kept for clarity of logic
                for(int i=0; i < _openList.Count; ++i) {
                    if(_openList[i].gridPosition == neighborPos) {
                        wasInOpenList = true;
                        openListIndex = i;
                        break;
                    }
                }

                if (tentativeGCost < neighborNodeData.gCost || !wasInOpenList)
                {
                    neighborNodeData.gCost = tentativeGCost;
                    neighborNodeData.hCost = CalculateManhattanDistance(neighborPos, endPosPlayable);
                    neighborNodeData.parentPosition = currentNode.gridPosition;
                    
                    _pathNodeGrid[neighborPos.x, neighborPos.y] = neighborNodeData; 

                    if (!wasInOpenList)
                    {
                        _openList.Add(neighborNodeData);
                    }
                }
            }
            _openList.Sort((a, b) => { 
                int fCompare = a.FCost.CompareTo(b.FCost);
                if (fCompare == 0) return a.hCost.CompareTo(b.hCost);
                return fCompare;
            });
        }

        DebugHelper.Log($"Pathfinder: No path found from {startPosPlayable} to {endPosPlayable} after {emergencyExit-1} nodes.", _gridManager);
        return finalPath;
    }

    private void InitializePathNodeGrid()
    {
        int width = _gridManager.playableWidth;
        int height = _gridManager.playableHeight;
        _pathNodeGrid = new PathNode[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile currentTile = _gridManager.GetTile(x, y);
                bool isWalkable = false;
                if (currentTile != null)
                {
                    isWalkable = currentTile.GetMovementCost() < 255;
                }
                _pathNodeGrid[x, y] = new PathNode(new Vector2Int(x, y), isWalkable);
            }
        }
    }
    
    private PathNode GetPathNode(int x, int y)
    {
        if (x < 0 || x >= _gridManager.playableWidth || y < 0 || y >= _gridManager.playableHeight) {
            Debug.LogError($"GetPathNode: Attempted to access out-of-bounds PathNode at ({x},{y})");
            return new PathNode(new Vector2Int(x,y), false); 
        }
        return _pathNodeGrid[x, y];
    }

    private int CalculateManhattanDistance(Vector2Int posA, Vector2Int posB)
    {
        int dx = Mathf.Abs(posA.x - posB.x);
        int dy = Mathf.Abs(posA.y - posB.y);
        return dx + dy; 
    }

    private List<Tile> ReconstructPath(PathNode endNodeArgument, PathNode startNodeArgument)
    {
        List<Tile> path = new List<Tile>();
        PathNode currentNode = endNodeArgument; 
        int pathSafety = 0;
        int maxPathLength = _gridManager.playableWidth * _gridManager.playableHeight;

        while (currentNode.gridPosition != startNodeArgument.gridPosition)
        {
            pathSafety++;
            if (pathSafety > maxPathLength) {
                DebugHelper.LogError("Pathfinder: ReconstructPath max length! Broken parent chain?", _gridManager);
                return new List<Tile>(); 
            }

            Tile tile = _gridManager.GetTile(currentNode.gridPosition);
            if (tile != null) path.Add(tile);
            else {
                DebugHelper.LogError($"Pathfinder: ReconstructPath couldn't find tile at {currentNode.gridPosition}!", _gridManager);
                return new List<Tile>();
            }
            
            if (currentNode.parentPosition.x != -1 && currentNode.parentPosition.y != -1) 
            {
                if (currentNode.parentPosition.x >= 0 && currentNode.parentPosition.x < _gridManager.playableWidth &&
                    currentNode.parentPosition.y >= 0 && currentNode.parentPosition.y < _gridManager.playableHeight)
                {
                    currentNode = _pathNodeGrid[currentNode.parentPosition.x, currentNode.parentPosition.y]; // Get parent from the grid
                }
                else
                {
                    DebugHelper.LogError($"Pathfinder: ReconstructPath parent {currentNode.parentPosition} is out of bounds!", _gridManager);
                    return new List<Tile>(); 
                }
            }
            else {
                 DebugHelper.LogError($"Pathfinder: ReconstructPath node {currentNode.gridPosition} has no valid parent before start! G={currentNode.gCost}, H={currentNode.hCost}", _gridManager);
                 return new List<Tile>(); 
            }
        }
        path.Reverse();
        return path;
    }
}