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
    private const int WAIT_ACTION_COST = 1; 

    private enum InputState { WaitingForTurn, UnitActionPhase, UnitMoving }
    private InputState _currentState = InputState.WaitingForTurn;

    private Unit _selectedUnit = null; 

    private List<Tile> _highlightedReachableTiles = new List<Tile>();
    private List<Tile> _highlightedPathTiles = new List<Tile>();

    private PlayerControls _playerControls;
    private Camera _mainCamera;

    void Awake()
    {
        _mainCamera = Camera.main;
        _playerControls = new PlayerControls();

        if (GridManager.Instance == null) { DebugHelper.LogError("InputHandler: GridManager.Instance is missing!", this); enabled = false; return; }
        if (TurnManager.Instance == null) { DebugHelper.LogError("InputHandler: TurnManager.Instance is missing!", this); enabled = false; return; }
        if (gridTester == null) { DebugHelper.LogError("InputHandler: GridTester missing!", this); enabled = false; return; }
        
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
            DebugHelper.LogWarning("InputHandler waiting for Player Unit from GridTester (for initial reference)...", this);
            yield return new WaitForSeconds(0.2f);
        }
        if (gridTester.PlayerUnitInstance != null) {
             DebugHelper.Log("InputHandler has initial Player Unit reference via GridTester.", this);
        } else {
            DebugHelper.LogError("InputHandler could not get Player Unit reference from GridTester!", this);
             this.enabled = false;
        }
    }
    
    void Update() 
    {
        if (TurnManager.Instance == null || !TurnManager.Instance.IsCombatActive)
        {
            if(_currentState != InputState.WaitingForTurn) 
            {
                ClearSelectionAndHighlights(); 
            }
            _currentState = InputState.WaitingForTurn;
            // _selectedUnit = null; // This is handled by ClearSelectionAndHighlights
            return;
        }

        Unit currentManagedActiveUnit = TurnManager.Instance.ActiveUnit;

        if (currentManagedActiveUnit != null && currentManagedActiveUnit.CompareTag("Player"))
        {
            if (_selectedUnit != currentManagedActiveUnit) 
            {
                // DebugHelper.Log($"PlayerInputHandler: Turn changed or started for {currentManagedActiveUnit.unitName}.", this);
                ClearSelectionAndHighlights(); 
                _selectedUnit = currentManagedActiveUnit;
                _currentState = InputState.UnitActionPhase;
                if (_selectedUnit.CurrentTile != null)
                {
                    _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
                }
                DebugHelper.Log($"PlayerInputHandler: Now it's {currentManagedActiveUnit.unitName}'s turn! (AP: {currentManagedActiveUnit.currentActionPoints})", this);
                AttemptToShowMoveRangeForSelectedUnit(); 
            }
            // If _selectedUnit is already currentManagedActiveUnit, PIH is already in this unit's action phase.
        }
        else 
        {
            if (_selectedUnit != null) 
            {
                DebugHelper.Log($"PlayerInputHandler: No longer player unit '{_selectedUnit.unitName}'s turn (Active TM Unit: {currentManagedActiveUnit?.unitName ?? "None"}). Clearing PIH selection.", this);
                ClearSelectionAndHighlights(); 
            }
            // If _selectedUnit is already null, state should correctly be WaitingForTurn.
        }
    }

    private void OnEnable()
    {
        if (_playerControls == null) _playerControls = new PlayerControls();
        _playerControls.Gameplay.Enable();
        _playerControls.Gameplay.Click.performed += HandleClickInput;
        _playerControls.Gameplay.Wait.performed += HandleWaitActionInput;
    }

    private void OnDisable()
    {
        if (_playerControls == null) return;
        _playerControls.Gameplay.Click.performed -= HandleClickInput;
        _playerControls.Gameplay.Wait.performed -= HandleWaitActionInput;
        _playerControls.Gameplay.Disable();
    }

    private void HandleClickInput(InputAction.CallbackContext context)
    {
        if (_currentState != InputState.UnitActionPhase || _selectedUnit == null || _mainCamera == null) 
        {
            return;
        }
        
        // DebugHelper.Log($"PlayerInputHandler: HandleClickInput CALLED during {_selectedUnit.unitName}'s action phase.", this);

        Vector2 screenPosition = _playerControls.Gameplay.Point.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
        Plane xyPlane = new Plane(Vector3.forward, Vector3.zero);

        if (!xyPlane.Raycast(ray, out float distance)) { return; }

        Vector3 worldPoint = ray.GetPoint(distance);
        Vector2Int clickedGridPos = GridManager.Instance.WorldToGrid(worldPoint);
        Tile clickedTile = GridManager.Instance.GetTile(clickedGridPos);

        if (clickedTile == null) { return; }

        if (clickedTile == _selectedUnit.CurrentTile) { 
            DebugHelper.Log("Clicked selected unit's tile again. No move action taken.", this);
            return;
        }

        if (_highlightedReachableTiles.Contains(clickedTile)) 
        {
            if (!_selectedUnit.CanAffordAction(MOVE_ACTION_COST))
            {
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford MOVE AP cost ({MOVE_ACTION_COST}). Has {_selectedUnit.currentActionPoints}. Move blocked.", this);
                return; 
            }

            if (clickedTile.IsOccupied) 
            {
                DebugHelper.Log($"Cannot move to occupied tile {clickedGridPos} (Occupant: {clickedTile.occupyingUnit?.unitName}).", this);
                return; 
            }
            
            List<Tile> path = _pathfinder.FindPath(_selectedUnit.CurrentTile.gridPosition, clickedTile.gridPosition, _selectedUnit);

            if (path != null && path.Count > 0) {
                ClearReachableHighlight(false); 
                ShowPathHighlight(path); 
                _currentState = InputState.UnitMoving; 
                DebugHelper.Log($"Path confirmed to {clickedTile.gridPosition}. Requesting move for {_selectedUnit.unitName}.", this);
                StartCoroutine(MoveUnitAlongPath(_selectedUnit, path));
            }
            else {
                DebugHelper.LogWarning($"No path calculated to {clickedTile.gridPosition} for {_selectedUnit.unitName}. It was in reachable, but pathing failed.", this);
                ClearPathHighlight();
            }
        }
        else { 
            DebugHelper.Log($"Clicked tile {clickedGridPos} is not in {_selectedUnit.unitName}'s reachable range. No action taken.", this); 
        }
    }

    private void HandleWaitActionInput(InputAction.CallbackContext context)
    {
        // DebugHelper.Log("PlayerInputHandler: Wait Action Input Received.", this); // Can be verbose
        if (_currentState == InputState.UnitActionPhase && _selectedUnit != null)
        {
            HandleWaitAction();
        }
        else
        {
            // DebugHelper.Log($"Wait action ignored. State: {_currentState}, SelectedUnit: {(_selectedUnit != null ? _selectedUnit.unitName : "null")}", this);
        }
    }

    private void HandleWaitAction()
    {
        if (_selectedUnit == null)
        {
            DebugHelper.LogWarning("Attempted to Wait with no selected unit (HandleWaitAction).", this);
            return;
        }

        if (_selectedUnit.CanAffordAction(WAIT_ACTION_COST))
        {
            _selectedUnit.SpendActionPoints(WAIT_ACTION_COST);
            DebugHelper.Log($"{_selectedUnit.unitName} performs WAIT action. AP remaining: {_selectedUnit.currentActionPoints}", this);
            
            TurnManager.Instance.EndUnitTurn(_selectedUnit); 
        }
        else
        {
            DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford WAIT action (Cost: {WAIT_ACTION_COST}). AP: {_selectedUnit.currentActionPoints}", this);
        }
    }

    private void AttemptToShowMoveRangeForSelectedUnit()
    {
        if (_selectedUnit != null && _selectedUnit.CurrentTile != null && _selectedUnit.CanAffordAction(MOVE_ACTION_COST))
        {
            int unitMoveRange = _selectedUnit.CalculatedMoveRange;
            ShowReachableRange(_selectedUnit.CurrentTile.gridPosition, unitMoveRange, _selectedUnit);
        } else {
            ClearReachableHighlight(true); 
            if(_selectedUnit != null) {
                // DebugHelper.Log($"{_selectedUnit.unitName} cannot afford a move action (AP: {_selectedUnit.currentActionPoints}). Range not shown.", this);
            }
        }
    }

    private void ShowReachableRange(Vector2Int startPos, int range, Unit requestingUnit)
    {
        ClearReachableHighlight(true); 
        if (_pathfinder == null || requestingUnit == null) return;

        List<Tile> reachable = _pathfinder.GetReachableTiles(startPos, range, requestingUnit);
        _highlightedReachableTiles.AddRange(reachable); 
        // DebugHelper.Log($"Found {reachable.Count} tiles reachable from {startPos} with range {range} for {requestingUnit.unitName}.", this);

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
                bool shouldRevertToReachable = _highlightedReachableTiles.Contains(tile) && (_selectedUnit == null || tile != _selectedUnit.CurrentTile);
                if (shouldRevertToReachable)
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
            // Check if it's still this unit's turn before removing its "SelectedUnit" highlight.
            // If TurnManager.ActiveUnit is this _selectedUnit, it should remain highlighted as selected.
            // This check might be redundant if Update() handles setting the highlight for the new ActiveUnit.
            // For now, just clear it if it's not the current active unit.
            if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _selectedUnit)
            {
                 _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.None); 
            }
            // If it IS the active unit, Update() will re-apply SelectedUnit highlight.
        }

        _selectedUnit = null; 
        _currentState = InputState.WaitingForTurn;
    }

     private IEnumerator MoveUnitAlongPath(Unit unitToMove, List<Tile> path)
    {
        if (unitToMove == null || path == null || path.Count == 0) {
            DebugHelper.LogWarning("MoveUnitAlongPath: Invalid unit or path.", this);
            ClearSelectionAndHighlights(); 
            _currentState = InputState.WaitingForTurn; 
            yield break; 
        }

        if (unitToMove != TurnManager.Instance.ActiveUnit) { // Crucial check
            DebugHelper.LogError($"MoveUnitAlongPath: {unitToMove.unitName} tried to move, but it's not the active unit's turn ({TurnManager.Instance.ActiveUnit?.unitName})!", this);
            ClearSelectionAndHighlights();
            _currentState = InputState.WaitingForTurn;
            yield break;
        }

        if (!unitToMove.CanAffordAction(MOVE_ACTION_COST))
        {
            DebugHelper.LogWarning($"{unitToMove.unitName} cannot afford MOVE AP ({MOVE_ACTION_COST}) at commitment. AP: {unitToMove.currentActionPoints}", this);
            _currentState = InputState.UnitActionPhase; 
            AttemptToShowMoveRangeForSelectedUnit(); 
            yield break;
        }
        unitToMove.SpendActionPoints(MOVE_ACTION_COST);

        DebugHelper.Log($"Starting movement for {unitToMove.unitName} (AP spent for move, now {unitToMove.currentActionPoints})...", this);

        yield return StartCoroutine(unitToMove.MoveOnPath(path)); 

        DebugHelper.Log($"{unitToMove.unitName} finished movement. AP: {unitToMove.currentActionPoints}/{unitToMove.maxActionPoints}", this);
        
        ClearPathHighlight();

        if (_selectedUnit != null && _selectedUnit == unitToMove) // Ensure unit is still selected and is the one that moved
        {
            _currentState = InputState.UnitActionPhase; // Return to action phase to check for more actions

            bool canMoveAgain = _selectedUnit.CanAffordAction(MOVE_ACTION_COST);
            bool canWait = _selectedUnit.CanAffordAction(WAIT_ACTION_COST);

            if (canMoveAgain) 
            {
                AttemptToShowMoveRangeForSelectedUnit(); 
                if(_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
                DebugHelper.Log($"{_selectedUnit.unitName} can move again. AP: {_selectedUnit.currentActionPoints}", this);
            }
            else if (canWait) 
            {
                DebugHelper.Log($"{_selectedUnit.unitName} cannot move again, but may have other actions (like Wait). AP: {_selectedUnit.currentActionPoints}", this);
                ClearReachableHighlight(true); 
                if(_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
            }
            else 
            {
                DebugHelper.Log($"{_selectedUnit.unitName} has no AP for further actions (Move/Wait). Ending turn. AP: {_selectedUnit.currentActionPoints}", this);
                ClearReachableHighlight(true); 
                if(_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
                TurnManager.Instance.EndUnitTurn(_selectedUnit); 
            }
        }
        else
        {
            // If selected unit changed or became null mid-move (unlikely with current design, but defensive)
            ClearSelectionAndHighlights();
        }
    }
}