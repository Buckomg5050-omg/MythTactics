// PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // For List

public class PlayerInputHandler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign the GridManager from the scene.")]
    public GridManager gridManager;
    private Pathfinder _pathfinder;

    [Header("Input Settings")]
    [Tooltip("Movement points to use when calculating reachable range on click.")]
    public int defaultMovementRange = 4;

    // State Tracking
    private enum InputState { None, TileSelected, PathDisplayed }
    private InputState _currentState = InputState.None;

    private Vector2Int? _startTilePos = null;
    private Vector2Int? _endTilePos = null;

    private List<Tile> _highlightedReachableTiles = new List<Tile>();
    private List<Tile> _highlightedPathTiles = new List<Tile>();
    private Tile _highlightedStartTile = null;

    private PlayerControls _playerControls;
    private Camera _mainCamera;

    void Awake()
    {
        _mainCamera = Camera.main;
        _playerControls = new PlayerControls();

        if (gridManager == null)
        {
            DebugHelper.LogError("PlayerInputHandler: GridManager reference is not set! Input handler will not function.", this);
            this.enabled = false;
            return;
        }
        _pathfinder = new Pathfinder(gridManager);
    }

    private void OnEnable()
    {
        if (_playerControls == null) return;
        _playerControls.Gameplay.Enable();
        _playerControls.Gameplay.Click.performed += HandleClick;
    }

    private void OnDisable()
{
    if (_playerControls == null) return;

    // Directly unsubscribe and disable. If _playerControls exists, Gameplay should too.
    // The input system handles null checks internally for these operations gracefully.
    _playerControls.Gameplay.Click.performed -= HandleClick;
    _playerControls.Gameplay.Disable();
}

    private void HandleClick(InputAction.CallbackContext context)
    {
        if (gridManager == null || _pathfinder == null || _mainCamera == null) return;

        Vector2 screenPosition = _playerControls.Gameplay.Point.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
        Plane xyPlane = new Plane(Vector3.forward, Vector3.zero);
        float distance;

        if (!xyPlane.Raycast(ray, out distance))
        {
             ClearHighlightsAndSelection();
             DebugHelper.Log("Click did not intersect grid plane.", this);
             return;
        }

        Vector3 worldPoint = ray.GetPoint(distance);
        Vector2Int clickedGridPos = gridManager.WorldToGrid(worldPoint);

        // --- State-Based Click Handling ---
        switch (_currentState)
        {
            case InputState.None:
                // --- First Click: Select Start Tile & Show Range ---
                if (gridManager.IsInPlayableBounds(clickedGridPos))
                {
                    Tile selectedTile = gridManager.GetTile(clickedGridPos);
                    if (selectedTile != null && !selectedTile.IsOccupied) // Can only select unoccupied tiles to start movement
                    {
                        ClearHighlightsAndSelection();

                        _startTilePos = clickedGridPos;
                        _highlightedStartTile = selectedTile;
                        selectedTile.SetHighlight(TileHighlightState.SelectedUnit);

                        ShowReachableRange(clickedGridPos, defaultMovementRange);

                        _currentState = InputState.TileSelected;
                        DebugHelper.Log($"Selected Start Tile: {clickedGridPos}", this);
                    }
                    else if (selectedTile != null && selectedTile.IsOccupied)
                    {
                        DebugHelper.Log($"Clicked on occupied tile {clickedGridPos}. Cannot select as start.", this);
                        ClearHighlightsAndSelection();
                    }
                    else { // Should not happen if IsInPlayableBounds is true
                         DebugHelper.LogWarning($"Clicked valid bounds {clickedGridPos} but got null tile?", this);
                         ClearHighlightsAndSelection();
                    }
                }
                else
                {
                    ClearHighlightsAndSelection();
                    DebugHelper.Log("Clicked outside playable area.", this);
                }
                break;

            case InputState.TileSelected:
                // --- Second Click: Select End Tile & Show Path (if reachable) ---
                if (_startTilePos.HasValue && gridManager.IsInPlayableBounds(clickedGridPos))
                {
                    bool isReachable = false;
                    foreach(Tile reachableTile in _highlightedReachableTiles) {
                        if (reachableTile.gridPosition == clickedGridPos) { isReachable = true; break; }
                    }
                    if (clickedGridPos == _startTilePos.Value) isReachable = true; // Start tile is always "reachable"

                    if (isReachable)
                    {
                        Tile targetTile = gridManager.GetTile(clickedGridPos);
                         // Ensure target tile is not occupied by another unit
                        if(targetTile != null && targetTile.IsOccupied) {
                             DebugHelper.Log($"Cannot end movement on occupied tile {clickedGridPos}", this);
                             // Optional: Instead of full reset, maybe just show feedback and stay in TileSelected state?
                             // For now, reset.
                             ClearHighlightsAndSelection();
                             break; // Exit switch
                        }


                        _endTilePos = clickedGridPos;
                        ClearReachableHighlight();

                        if (_startTilePos.Value != _endTilePos.Value)
                        {
                            DebugHelper.Log($"Target Tile selected: {clickedGridPos}. Finding path...", this);
                            ShowPath(_startTilePos.Value, _endTilePos.Value);
                            _currentState = InputState.PathDisplayed;
                        }
                        else
                        {
                            DebugHelper.Log("Clicked start tile again. Resetting.", this);
                            ClearHighlightsAndSelection();
                        }
                    }
                    else
                    {
                        DebugHelper.Log("Clicked non-reachable tile. Resetting selection.", this);
                        ClearHighlightsAndSelection();
                    }
                }
                else
                {
                     DebugHelper.Log("Clicked outside playable area. Resetting selection.", this);
                     ClearHighlightsAndSelection();
                }
                break;

            case InputState.PathDisplayed:
                // --- Click after Path is Displayed: Reset Everything ---
                 DebugHelper.Log("Clicked after path shown. Resetting state.", this);
                 ClearHighlightsAndSelection();
                break;
        }
    }

    /// <summary>
    /// Calculates and highlights the reachable tiles from a start position.
    /// </summary>
    private void ShowReachableRange(Vector2Int startPos, int range)
    {
         if (_pathfinder == null) return;

        List<Tile> reachable = _pathfinder.GetReachableTiles(startPos, range);
        _highlightedReachableTiles.Clear();

        DebugHelper.Log($"Found {reachable.Count} tiles reachable from {startPos} with range {range}.", this);

        foreach (Tile tile in reachable)
        {
            // Highlight reachable tiles, including start tile this time
            // (but we avoid clearing start tile highlight later)
             if (tile != null)
             {
                if (tile.gridPosition != _startTilePos) // Only add non-start tiles to clear list
                     _highlightedReachableTiles.Add(tile);

                if(tile != _highlightedStartTile) // Don't overwrite start tile's 'Selected' highlight
                     tile.SetHighlight(TileHighlightState.MovementRange);
             }
        }
    }

    /// <summary>
    /// Calculates and highlights the path between two tiles.
    /// </summary>
    private void ShowPath(Vector2Int startPos, Vector2Int endPos)
    {
        if (_pathfinder == null) return;

        List<Tile> path = _pathfinder.FindPath(startPos, endPos);
        _highlightedPathTiles.Clear();

        if (path != null && path.Count > 0)
        {
             DebugHelper.Log($"Path found with {path.Count} steps.", this);
            foreach (Tile tile in path)
            {
                if (tile.gridPosition != startPos) // Don't overwrite start tile highlight
                {
                    tile.SetHighlight(TileHighlightState.AttackRange); // Use AttackRange color for path
                    _highlightedPathTiles.Add(tile);
                }
            }
            // Optionally highlight the very last tile (end tile) differently?
            // if(path.Count > 0) path[path.Count - 1]?.SetHighlight(SomeOtherColor);
        }
        else
        {
             DebugHelper.LogWarning($"ShowPath: No path found from {startPos} to {endPos}.", this);
             // If no path, reset state back to TileSelected? Or None? Let's reset to None.
             ClearHighlightsAndSelection();
        }
    }

    /// <summary>
    /// Clears all visual highlights (Selection, Range, Path) and resets input state.
    /// </summary>
    private void ClearHighlightsAndSelection()
    {
        // Use temporary lists to avoid issues if Clear methods modify the lists being iterated over
        List<Tile> tilesToClear = new List<Tile>();

        // Add selected tile
        if (_highlightedStartTile != null)
        {
            tilesToClear.Add(_highlightedStartTile);
            _highlightedStartTile = null;
        }

        // Add reachable tiles
        tilesToClear.AddRange(_highlightedReachableTiles);
        _highlightedReachableTiles.Clear();

        // Add path tiles
        tilesToClear.AddRange(_highlightedPathTiles);
        _highlightedPathTiles.Clear();

        // Reset highlights, avoiding duplicates
        HashSet<Tile> processedTiles = new HashSet<Tile>();
        foreach (Tile tile in tilesToClear)
        {
            if (tile != null && processedTiles.Add(tile)) // Add returns true if item was new
            {
                tile.SetHighlight(TileHighlightState.None);
            }
        }

        // Reset state variables
        _startTilePos = null;
        _endTilePos = null;
        _currentState = InputState.None;
        // DebugHelper.Log("Cleared Highlights & Selection", this);
    }

    /// <summary>
    /// Clears only the Movement Range highlights. Used when path is selected.
    /// Ensures path tiles or start tile aren't accidentally cleared yet.
    /// </summary>
    private void ClearReachableHighlight()
    {
         List<Tile> tilesToClear = new List<Tile>(_highlightedReachableTiles); // Copy list
        _highlightedReachableTiles.Clear(); // Clear original tracker

        foreach (Tile tile in tilesToClear)
        {
            // Ensure it's not the start tile and not going to be part of the path highlight
            if (tile != null && tile != _highlightedStartTile)
            {
                // Check if this tile will be highlighted by the path later.
                // This is tricky without knowing the path beforehand.
                // A simpler approach: Just clear all non-start tiles. Path highlight will overwrite.
                tile.SetHighlight(TileHighlightState.None);
            }
        }
         // DebugHelper.Log("Cleared Reachable Highlights", this);
    }
}