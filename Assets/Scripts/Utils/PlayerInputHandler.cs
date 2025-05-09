// PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("References")]
    public GridTester gridTester; 
    private Pathfinder _pathfinder;

    private const int MOVE_ACTION_COST = 1;
    private const int WAIT_ACTION_COST = 1; // Cost for the Wait action

    private enum InputState { WaitingForInput, UnitSelected, UnitMoving }
    private InputState _currentState = InputState.WaitingForInput;

    private Unit _selectedUnit = null;

    private List<Tile> _highlightedReachableTiles = new List<Tile>();
    private List<Tile> _highlightedPathTiles = new List<Tile>();

    private PlayerControls _playerControls;
    private Camera _mainCamera;

    void Awake()
    {
        _mainCamera = Camera.main;
        _playerControls = new PlayerControls();

        if (GridManager.Instance == null) { DebugHelper.LogError("InputHandler: GridManager.Instance is missing! Ensure GridManager is in scene and has Awake setup.", this); this.enabled = false; return; }
        if (gridTester == null) { DebugHelper.LogError("InputHandler: GridTester missing (for player unit)! Ensure it's assigned.", this); this.enabled = false; return; }
        
        _pathfinder = new Pathfinder(GridManager.Instance);
    }

    void Start()
    {
        StartCoroutine(AssignPlayerUnitReference());
    }

     IEnumerator AssignPlayerUnitReference()
    {
        yield return null; 
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
        _playerControls.Gameplay.Wait.performed += HandleWaitActionInput; // Subscribe to the new Wait action
    }

    private void OnDisable()
    {
        if (_playerControls == null) return;
        _playerControls.Gameplay.Click.performed -= HandleClick;
        _playerControls.Gameplay.Wait.performed -= HandleWaitActionInput; // Unsubscribe
        _playerControls.Gameplay.Disable();
    }

    private void HandleClick(InputAction.CallbackContext context)
    {
        // DebugHelper.Log("PlayerInputHandler: HandleClick CALLED!", this); 

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
                if (clickedTile != null && clickedTile.IsOccupied && clickedTile.occupyingUnit != null && clickedTile.occupyingUnit.CompareTag("Player")) 
                {
                    ClearSelectionAndHighlights(); 
                    _selectedUnit = clickedTile.occupyingUnit;
                    
                    // Check AP for any action (e.g., move) before fully selecting
                    if (_selectedUnit.CanAffordAction(MOVE_ACTION_COST) || _selectedUnit.CanAffordAction(WAIT_ACTION_COST)) // Can the unit do anything?
                    {
                        _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
                        // Show move range only if unit can afford to move
                        if (_selectedUnit.CanAffordAction(MOVE_ACTION_COST))
                        {
                            int unitMoveRange = _selectedUnit.CalculatedMoveRange;
                            ShowReachableRange(_selectedUnit.CurrentTile.gridPosition, unitMoveRange, _selectedUnit);
                        }
                        _currentState = InputState.UnitSelected;
                        DebugHelper.Log($"Selected {_selectedUnit.unitName} at: {clickedGridPos} (Move: {_selectedUnit.CalculatedMoveRange}, AP: {_selectedUnit.currentActionPoints}/{_selectedUnit.maxActionPoints})", this);
                    }
                    else
                    {
                        DebugHelper.Log($"{_selectedUnit.unitName} selected, but has insufficient AP ({_selectedUnit.currentActionPoints}) for any action.", this);
                        _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); // Show it's selected
                        // Don't transition to UnitSelected state if no actions are possible
                        // Or, allow selection but actions will fail their CanAffordAction checks.
                        // For now, let's allow selection but actions (like showing move range) will be gated by AP.
                        _currentState = InputState.UnitSelected; // Allow selection to see info, wait, etc.
                    }
                }
                else { ClearSelectionAndHighlights(); } 
                break;

            case InputState.UnitSelected:
                 if (_selectedUnit != null && clickedTile != null)
                 {
                    if (clickedTile == _selectedUnit.CurrentTile) { 
                        DebugHelper.Log("Clicked selected unit's tile again. No action, unit remains selected.", this);
                        // No need to deselect here unless specific design calls for it.
                        // ClearSelectionAndHighlights(); 
                        break;
                    }

                    if (_highlightedReachableTiles.Contains(clickedTile)) // Attempting to move
                    {
                        if (!_selectedUnit.CanAffordAction(MOVE_ACTION_COST))
                        {
                            DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford MOVE AP cost ({MOVE_ACTION_COST}). Has {_selectedUnit.currentActionPoints}. Move blocked.", this);
                            // Optionally, provide feedback to player.
                            break; // Break from switch, unit remains selected, range might still be shown.
                        }

                        if (clickedTile.IsOccupied) 
                        {
                             DebugHelper.Log($"Cannot move to occupied tile {clickedGridPos} (Occupant: {clickedTile.occupyingUnit?.unitName}).", this);
                             // ClearPathHighlight(); // Clear just the path if one was shown
                             break; // Unit remains selected, player can choose another target.
                        }
                        
                        List<Tile> path = _pathfinder.FindPath(_selectedUnit.CurrentTile.gridPosition, clickedTile.gridPosition, _selectedUnit);

                        if (path != null && path.Count > 0) {
                            ClearReachableHighlight(false); 
                            ShowPathHighlight(path); 
                            _currentState = InputState.UnitMoving; 
                            DebugHelper.Log($"Path confirmed to {clickedTile.gridPosition}. Requesting move.", this);
                            StartCoroutine(MoveUnitAlongPath(_selectedUnit, path));
                        }
                        else {
                             DebugHelper.LogWarning($"No path calculated to {clickedTile.gridPosition}. It was in reachable, but pathing failed.", this);
                             // Keep unit selected, allow trying another tile.
                             ClearPathHighlight();
                        }
                    }
                    else { // Clicked outside reachable range or on an invalid tile
                        DebugHelper.Log($"Clicked tile {clickedGridPos} is not a valid move target for {_selectedUnit.unitName}. Deselecting unit.", this); 
                        ClearSelectionAndHighlights(); 
                    }
                 }
                 else { 
                    // Clicked off-grid or something unexpected
                    DebugHelper.Log("Clicked outside grid or invalid state while unit selected. Deselecting.", this); 
                    ClearSelectionAndHighlights(); 
                }
                break;
            
            case InputState.UnitMoving:
                DebugHelper.Log("Clicked while unit moving. Input ignored.", this);
                break;
        }
    }

    private void HandleWaitActionInput(InputAction.CallbackContext context)
    {
        DebugHelper.Log("PlayerInputHandler: HandleWaitActionInput CALLED!", this);
        if (_currentState == InputState.UnitSelected && _selectedUnit != null)
        {
            HandleWaitAction();
        }
        else
        {
            DebugHelper.Log("Wait action ignored: No unit selected or unit is currently moving.", this);
        }
    }

    private void HandleWaitAction()
    {
        if (_selectedUnit == null)
        {
            DebugHelper.LogWarning("Attempted to Wait with no selected unit.", this);
            return;
        }

        if (_selectedUnit.CanAffordAction(WAIT_ACTION_COST))
        {
            _selectedUnit.SpendActionPoints(WAIT_ACTION_COST);
            DebugHelper.Log($"{_selectedUnit.unitName} performs WAIT action. AP remaining: {_selectedUnit.currentActionPoints}", this);
            
            // Simulate end of this unit's contribution to player's turn for now
            // Actual turn ending will be handled by TurnManager
            ClearSelectionAndHighlights(); // Deselects unit, clears highlights, returns to WaitingForInput state
            // _currentState = InputState.WaitingForInput; // This is done by ClearSelectionAndHighlights
        }
        else
        {
            DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford WAIT action. AP: {_selectedUnit.currentActionPoints}", this);
            // Optionally, provide feedback to player
        }
    }


    private void ShowReachableRange(Vector2Int startPos, int range, Unit requestingUnit)
    {
        ClearReachableHighlight(true); 
        if (_pathfinder == null || requestingUnit == null) return;

        List<Tile> reachable = _pathfinder.GetReachableTiles(startPos, range, requestingUnit);
        _highlightedReachableTiles.AddRange(reachable); 
        DebugHelper.Log($"Found {reachable.Count} tiles reachable from {startPos} with range {range} for {requestingUnit.unitName}.", this);

        foreach (Tile tile in _highlightedReachableTiles) {
             if (tile != null && tile != requestingUnit.CurrentTile) { 
                tile.SetHighlight(TileHighlightState.MovementRange);
             }
        }
    }

    private void ShowPathHighlight(List<Tile> path) 
    {
        ClearPathHighlight(); 

        if (path == null || path.Count == 0) return;

        foreach (Tile tileInPath in path) {
            if (tileInPath != null && (_selectedUnit == null || tileInPath != _selectedUnit.CurrentTile)) 
            {
                tileInPath.SetHighlight(TileHighlightState.Path); 
                _highlightedPathTiles.Add(tileInPath); 
            }
        }
    }
    
    private void ClearReachableHighlight(bool clearList)
    {
        foreach (Tile tile in _highlightedReachableTiles) {
            if (tile != null && (_selectedUnit == null || tile != _selectedUnit.CurrentTile)) { 
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
                // If the tile was also in reachable range (unlikely for path tiles if reachable is cleared first, but defensive)
                // and it's not the selected unit's current tile.
                if (_highlightedReachableTiles.Contains(tile) && (_selectedUnit == null || tile != _selectedUnit.CurrentTile) )
                {
                    tile.SetHighlight(TileHighlightState.MovementRange);
                }
                else if (_selectedUnit == null || tile != _selectedUnit.CurrentTile) 
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
        // DebugHelper.Log("Selection and all highlights cleared. State reset to WaitingForInput.", this);
    }

     private IEnumerator MoveUnitAlongPath(Unit unitToMove, List<Tile> path)
    {
        if (unitToMove == null || path == null || path.Count == 0) {
            DebugHelper.LogWarning("MoveUnitAlongPath: Invalid unit or path.", this);
            ClearSelectionAndHighlights(); 
            _currentState = InputState.WaitingForInput; 
            yield break; 
        }

        // AP check and spend for MOVE_ACTION_COST
        if (!unitToMove.CanAffordAction(MOVE_ACTION_COST))
        {
            DebugHelper.LogWarning($"{unitToMove.unitName} cannot afford MOVE AP ({MOVE_ACTION_COST}) at commitment. AP: {unitToMove.currentActionPoints}", this);
            ClearSelectionAndHighlights(); 
            _currentState = InputState.WaitingForInput;
            yield break;
        }
        unitToMove.SpendActionPoints(MOVE_ACTION_COST);

        DebugHelper.Log($"Starting movement for {unitToMove.unitName} (AP spent for move, now {unitToMove.currentActionPoints})...", this);

        yield return StartCoroutine(unitToMove.MoveOnPath(path)); 

        DebugHelper.Log($"{unitToMove.unitName} finished movement. AP: {unitToMove.currentActionPoints}/{unitToMove.maxActionPoints}", this);
        
        ClearPathHighlight(); // Path is done.

        if (_selectedUnit != null && _selectedUnit == unitToMove) 
        {
            // Check if unit can perform *any* other action (e.g., another move OR wait)
            bool canMoveAgain = _selectedUnit.CanAffordAction(MOVE_ACTION_COST);
            bool canWait = _selectedUnit.CanAffordAction(WAIT_ACTION_COST);

            if (canMoveAgain) // If it can move again, show new range
            {
                ShowReachableRange(_selectedUnit.CurrentTile.gridPosition, _selectedUnit.CalculatedMoveRange, _selectedUnit);
                _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
                _currentState = InputState.UnitSelected; 
                DebugHelper.Log($"{_selectedUnit.unitName} can move again. AP: {_selectedUnit.currentActionPoints}", this);
            }
            else if (canWait) // Can't move, but maybe can wait or do other 1-AP actions
            {
                DebugHelper.Log($"{_selectedUnit.unitName} cannot move again, but may have other actions (like Wait). AP: {_selectedUnit.currentActionPoints}", this);
                _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
                _currentState = InputState.UnitSelected; // Stay selected for other actions
            }
            else // No AP left for any typical action
            {
                DebugHelper.Log($"{_selectedUnit.unitName} finished move and has no AP for further actions (Move/Wait). AP: {_selectedUnit.currentActionPoints}", this);
                _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); // Keep selected for info
                _currentState = InputState.UnitSelected; // Still selected, but actions will fail AP checks
                // Or to fully end its turn for player: ClearSelectionAndHighlights();
            }
        }
        else
        {
            ClearSelectionAndHighlights();
        }
    }
}