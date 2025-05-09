// PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign the GridManager from the scene.")]
    public GridManager gridManager; // Should be GridManager.Instance after GridManager is updated
    [Tooltip("Assign the GridTester from the scene to get player unit ref.")]
    public GridTester gridTester; // Keep for now, but _selectedUnit will be primary focus
    private Pathfinder _pathfinder;

    private const int MOVE_ACTION_COST = 1; // GDD 3.1: Move Action costs 1 AP

    // InputState might need to evolve if we have more complex actions, but good for now.
    private enum InputState { WaitingForInput, UnitSelected, UnitMoving } // Renamed None to WaitingForInput for clarity
    private InputState _currentState = InputState.WaitingForInput;

    private Unit _selectedUnit = null;
    // private Vector2Int? _endTilePos = null; // Not strictly needed if path is stored directly

    private List<Tile> _highlightedReachableTiles = new List<Tile>();
    private List<Tile> _highlightedPathTiles = new List<Tile>(); // For path preview
    // private Tile _highlightedStartTile = null; // The selected unit's tile will be highlighted by its own state

    private PlayerControls _playerControls;
    private Camera _mainCamera;

    void Awake()
    {
        _mainCamera = Camera.main;
        _playerControls = new PlayerControls();

        // GridManager should be accessed via its static Instance after the update
        if (GridManager.Instance == null) { DebugHelper.LogError("InputHandler: GridManager.Instance is missing! Ensure GridManager is in scene and has Awake setup.", this); this.enabled = false; return; }
        if (gridTester == null) { DebugHelper.LogError("InputHandler: GridTester missing (for player unit)! Ensure it's assigned.", this); this.enabled = false; return; }
        
        _pathfinder = new Pathfinder(GridManager.Instance); // Use instance
    }

    void Start()
    {
        // Pathfinder initialized in Awake
        StartCoroutine(AssignPlayerUnitReference()); // Still useful to ensure player unit is ready
    }

     IEnumerator AssignPlayerUnitReference()
    {
        // This coroutine might become less critical if PlayerInputHandler directly finds player units
        // or if a UnitManager system handles player unit registration.
        // For now, it ensures GridTester has spawned the unit.
        yield return null; // Wait a frame for GridTester's Start/Awake
        while (gridTester.PlayerUnitInstance == null) {
            DebugHelper.LogWarning("InputHandler waiting for Player Unit from GridTester...", this);
            yield return new WaitForSeconds(0.2f);
        }
        if (gridTester.PlayerUnitInstance != null) {
             DebugHelper.Log("InputHandler ready, found Player Unit reference via GridTester.", this);
        } else {
            DebugHelper.LogError("InputHandler could not get Player Unit reference from GridTester!", this);
             this.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_playerControls == null) _playerControls = new PlayerControls();
        _playerControls.Gameplay.Enable();
        _playerControls.Gameplay.Click.performed += HandleClick;
    }

    private void OnDisable()
    {
        if (_playerControls == null) return;
        _playerControls.Gameplay.Click.performed -= HandleClick;
        _playerControls.Gameplay.Disable();
    }

    private void HandleClick(InputAction.CallbackContext context)
    {
        if (_currentState == InputState.UnitMoving || GridManager.Instance == null || _pathfinder == null || _mainCamera == null) return;

        Vector2 screenPosition = _playerControls.Gameplay.Point.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
        Plane xyPlane = new Plane(Vector3.forward, Vector3.zero);

        if (!xyPlane.Raycast(ray, out float distance)) { ClearSelectionAndHighlights(); return; }

        Vector3 worldPoint = ray.GetPoint(distance);
        Vector2Int clickedGridPos = GridManager.Instance.WorldToGrid(worldPoint);
        Tile clickedTile = GridManager.Instance.GetTile(clickedGridPos);

        switch (_currentState)
        {
            case InputState.WaitingForInput:
                if (clickedTile != null && clickedTile.IsOccupied && clickedTile.occupyingUnit != null && clickedTile.occupyingUnit.CompareTag("Player")) // Assuming player units have "Player" tag
                {
                    ClearSelectionAndHighlights(); // Clear any previous state
                    _selectedUnit = clickedTile.occupyingUnit;
                    
                    if (_selectedUnit.CanAffordAction(MOVE_ACTION_COST))
                    {
                        _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); // Highlight selected unit's tile
                        int unitMoveRange = _selectedUnit.CalculatedMoveRange;
                        ShowReachableRange(_selectedUnit.CurrentTile.gridPosition, unitMoveRange, _selectedUnit);
                        _currentState = InputState.UnitSelected;
                        DebugHelper.Log($"Selected {_selectedUnit.unitName} at: {clickedGridPos} (Move: {unitMoveRange}, AP: {_selectedUnit.currentActionPoints}/{_selectedUnit.maxActionPoints})", this);
                    }
                    else
                    {
                        DebugHelper.Log($"{_selectedUnit.unitName} selected, but has insufficient AP ({_selectedUnit.currentActionPoints}) to move (cost: {MOVE_ACTION_COST}).", this);
                        _selectedUnit = null; // Deselect if cannot act
                    }
                }
                else { ClearSelectionAndHighlights(); } // Clicked empty tile or non-player unit
                break;

            case InputState.UnitSelected:
                 if (_selectedUnit != null && clickedTile != null)
                 {
                    if (clickedTile == _selectedUnit.CurrentTile) { // Clicked on the already selected unit
                        DebugHelper.Log("Clicked selected unit's tile again. Deselecting.", this);
                        ClearSelectionAndHighlights(); 
                        break;
                    }

                    if (_highlightedReachableTiles.Contains(clickedTile))
                    {
                        if (clickedTile.IsOccupied) // Target tile is occupied by another unit
                        {
                             DebugHelper.Log($"Cannot move to occupied tile {clickedGridPos} (Occupant: {clickedTile.occupyingUnit?.unitName}). Resetting.", this);
                             ClearSelectionAndHighlights(); // Or just clear path/reachable and wait for new target.
                             break;
                        }
                        
                        // Confirmed move target
                        List<Tile> path = _pathfinder.FindPath(_selectedUnit.CurrentTile.gridPosition, clickedTile.gridPosition, _selectedUnit);

                        if (path != null && path.Count > 0) {
                            // AP check is implicitly done by _selectedUnit.CanAffordAction(MOVE_ACTION_COST) in MoveUnitAlongPath
                            // but a quick check here can avoid unnecessary path highlighting if AP was spent by another means (future feature)
                            if (!_selectedUnit.CanAffordAction(MOVE_ACTION_COST))
                            {
                                DebugHelper.LogWarning($"{_selectedUnit.unitName} could not afford move at path confirmation. AP: {_selectedUnit.currentActionPoints}", this);
                                ClearSelectionAndHighlights();
                                break;
                            }
                            ClearReachableHighlight(false); // Clear reachable, but keep selected unit's tile highlighted
                            ShowPathHighlight(path); // Highlight the chosen path
                            _currentState = InputState.UnitMoving; // Set state to prevent further clicks while moving
                            DebugHelper.Log($"Path confirmed to {clickedTile.gridPosition}. Requesting move.", this);
                            StartCoroutine(MoveUnitAlongPath(_selectedUnit, path));
                        }
                        else {
                             DebugHelper.LogWarning($"No path calculated to {clickedTile.gridPosition} (was in reachable, but pathing failed). Resetting.", this);
                             ClearSelectionAndHighlights();
                        }
                    }
                    else { 
                        DebugHelper.Log($"Clicked tile {clickedGridPos} is not in reachable range. Deselecting unit.", this); 
                        ClearSelectionAndHighlights(); 
                    }
                 }
                 else { 
                    DebugHelper.Log("Clicked outside grid or invalid state while unit selected. Deselecting.", this); 
                    ClearSelectionAndHighlights(); 
                }
                break;
        }
    }

    private void ShowReachableRange(Vector2Int startPos, int range, Unit requestingUnit)
    {
        ClearReachableHighlight(true); // Clear previous reachable tiles fully
        if (_pathfinder == null || requestingUnit == null) return;

        List<Tile> reachable = _pathfinder.GetReachableTiles(startPos, range, requestingUnit);
        _highlightedReachableTiles.AddRange(reachable); // Add to the list for tracking
        DebugHelper.Log($"Found {reachable.Count} tiles reachable from {startPos} with range {range} for {requestingUnit.unitName}.", this);

        foreach (Tile tile in _highlightedReachableTiles) {
             if (tile != null && tile != requestingUnit.CurrentTile) { // Don't highlight the start tile as "reachable" in the same way
                tile.SetHighlight(TileHighlightState.MovementRange);
             }
        }
    }

    private void ShowPathHighlight(List<Tile> path) // Renamed from ShowPath
    {
        ClearPathHighlight(); // Clear previous path highlights

        if (path == null || path.Count == 0) return;

        foreach (Tile tileInPath in path) {
            // Don't highlight the start tile as path, it's already 'SelectedUnit'
            if (tileInPath != null && (_selectedUnit == null || tileInPath != _selectedUnit.CurrentTile)) 
            {
                tileInPath.SetHighlight(TileHighlightState.Path); // Using the new Path state
                _highlightedPathTiles.Add(tileInPath); 
            }
        }
    }
    
    private void ClearReachableHighlight(bool clearList)
    {
        foreach (Tile tile in _highlightedReachableTiles) {
            if (tile != null && (_selectedUnit == null || tile != _selectedUnit.CurrentTile)) { // Avoid unhighlighting selected unit's tile if it's in the list
                tile.SetHighlight(TileHighlightState.None); 
            }
        }
        if (clearList) _highlightedReachableTiles.Clear();
    }

    private void ClearPathHighlight()
    {
        foreach (Tile tile in _highlightedPathTiles)
        {
            if (tile != null)
            {
                // If the tile was also in reachable range, revert it to that highlight, otherwise to None.
                if (_highlightedReachableTiles.Contains(tile) && tile != (_selectedUnit?.CurrentTile))
                {
                    tile.SetHighlight(TileHighlightState.MovementRange);
                }
                else if (_selectedUnit == null || tile != _selectedUnit.CurrentTile) // Don't clear selected unit tile
                {
                    tile.SetHighlight(TileHighlightState.None);
                }
            }
        }
        _highlightedPathTiles.Clear();
    }

    private void ClearSelectionAndHighlights()
    {
        ClearReachableHighlight(true);
        ClearPathHighlight();

        if (_selectedUnit != null && _selectedUnit.CurrentTile != null) {
            _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.None); 
        }

        _selectedUnit = null;
        _currentState = InputState.WaitingForInput;
        // DebugHelper.Log("Selection and all highlights cleared. State reset.", this);
    }

     private IEnumerator MoveUnitAlongPath(Unit unitToMove, List<Tile> path)
    {
        if (unitToMove == null || path == null || path.Count == 0) {
            DebugHelper.LogWarning("MoveUnitAlongPath: Invalid unit or path.", this);
            ClearSelectionAndHighlights(); // Reset state
            _currentState = InputState.WaitingForInput; // Ensure state returns to waiting
            yield break; 
        }

        // Final AP check and spend
        if (!unitToMove.CanAffordAction(MOVE_ACTION_COST))
        {
            DebugHelper.LogWarning($"{unitToMove.unitName} cannot afford AP ({MOVE_ACTION_COST}) for move at commitment point. AP: {unitToMove.currentActionPoints}", this);
            ClearSelectionAndHighlights(); // Clear selection, path, and reset state
            _currentState = InputState.WaitingForInput;
            yield break;
        }
        unitToMove.SpendActionPoints(MOVE_ACTION_COST);

        // _isUnitMoving flag for other systems if needed, _currentState handles input blocking
        DebugHelper.Log($"Starting movement for {unitToMove.unitName} (AP spent, now {unitToMove.currentActionPoints})...", this);

        yield return StartCoroutine(unitToMove.MoveOnPath(path)); // This is the Unit's own movement coroutine

        DebugHelper.Log($"{unitToMove.unitName} finished movement. AP: {unitToMove.currentActionPoints}/{unitToMove.maxActionPoints}", this);
        
        // After movement, clear path highlight
        ClearPathHighlight();

        // If unit is still selected (it should be) and can still perform a move:
        if (_selectedUnit != null && _selectedUnit == unitToMove) 
        {
            if (_selectedUnit.CanAffordAction(MOVE_ACTION_COST))
            {
                // Refresh reachable range from new position
                ShowReachableRange(_selectedUnit.CurrentTile.gridPosition, _selectedUnit.CalculatedMoveRange, _selectedUnit);
                _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); // Re-highlight selected tile
                _currentState = InputState.UnitSelected; // Ready for another command (e.g. another move or action)
                DebugHelper.Log($"{_selectedUnit.unitName} can act again. AP: {_selectedUnit.currentActionPoints}", this);
            }
            else
            {
                // Unit has moved and has no more AP for another move.
                DebugHelper.Log($"{_selectedUnit.unitName} finished move and has no more move AP. AP: {_selectedUnit.currentActionPoints}", this);
                _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); // Keep unit selected but no range.
                _currentState = InputState.UnitSelected; // Still selected, but ShowReachableRange won't trigger if AP check fails on next selection logic
                                                         // Or, decide to auto-deselect: ClearSelectionAndHighlights();
            }
        }
        else
        {
            // If selected unit changed or became null during move (shouldn't happen with current logic)
            ClearSelectionAndHighlights();
        }
    }
}