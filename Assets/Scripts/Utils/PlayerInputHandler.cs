// PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("References")]
    public GridTester gridTester;
    private Pathfinder _pathfinder;

    private const int MOVE_ACTION_COST = 1;
    private const int WAIT_ACTION_COST = 1;
    private const int ATTACK_ACTION_COST = 1;
    private const int PLACEHOLDER_ATTACK_DAMAGE = 15; 

    private enum InputState { WaitingForTurn, UnitActionPhase, SelectingAttackTarget, UnitMoving }
    private InputState _currentState = InputState.WaitingForTurn;

    private Unit _selectedUnit = null;

    private List<Tile> _highlightedReachableTiles = new List<Tile>();
    private List<Tile> _highlightedPathTiles = new List<Tile>();
    private List<Tile> _highlightedAttackRangeTiles = new List<Tile>();

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
        while (gridTester.PlayerUnitInstance == null && Application.isPlaying) { 
            yield return new WaitForSeconds(0.2f);
        }
        if(!Application.isPlaying) yield break; 

        if (gridTester.PlayerUnitInstance != null) {
             DebugHelper.Log("InputHandler has initial Player Unit reference via GridTester.", this);
        } else {
            DebugHelper.LogError("InputHandler could not get Player Unit reference from GridTester post-initialization wait!", this);
             this.enabled = false;
        }
    }

    void Update()
    {
        if (!Application.isPlaying) return; 
        if (TurnManager.Instance == null || !TurnManager.Instance.IsCombatActive)
        {
            if(_currentState != InputState.WaitingForTurn)
            {
                ClearSelectionAndHighlights();
            }
            return;
        }

        Unit currentManagedActiveUnit = TurnManager.Instance.ActiveUnit;

        if (currentManagedActiveUnit != null && currentManagedActiveUnit.IsAlive && currentManagedActiveUnit.CompareTag("Player")) 
        {
            if (_selectedUnit != currentManagedActiveUnit) 
            {
                ClearSelectionAndHighlights(); 
                _selectedUnit = currentManagedActiveUnit;
                _currentState = InputState.UnitActionPhase; 
                if (_selectedUnit.CurrentTile != null)
                {
                    _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
                }
                DebugHelper.Log($"PlayerInputHandler: Now it's {currentManagedActiveUnit.unitName}'s turn! (AP: {currentManagedActiveUnit.currentActionPoints}). State: {_currentState}", this);
                AttemptToShowMoveRangeForSelectedUnit();
            }
            else if (_selectedUnit == currentManagedActiveUnit) // Same unit's turn, just update highlights based on state
            {
                if (_currentState == InputState.UnitActionPhase)
                {
                    AttemptToShowMoveRangeForSelectedUnit(); 
                    // ClearAttackRangeHighlight(true); // Already cleared when transitioning to this state
                }
                else if (_currentState == InputState.SelectingAttackTarget)
                {
                    ClearReachableHighlight(true);
                    // ShowAttackRange(_selectedUnit); // MODIFIED: Removed from here - only show on state transition
                }
                 if (_selectedUnit.CurrentTile != null && _selectedUnit.IsAlive && _selectedUnit.CurrentTile.occupyingUnit == _selectedUnit) { 
                    _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
                }
            }
        }
        else 
        {
            if (_selectedUnit != null) 
            {
                ClearSelectionAndHighlights(); 
            }
        }
    }

    private void OnEnable()
    {
        if (_playerControls == null) _playerControls = new PlayerControls();
        _playerControls.Gameplay.Enable();
        _playerControls.Gameplay.Click.performed += HandleClickInput;
        _playerControls.Gameplay.Wait.performed += HandleWaitActionInput;
        _playerControls.Gameplay.EndTurn.performed += HandleEndTurnActionInput;
        _playerControls.Gameplay.ToggleAttackMode.performed += HandleToggleAttackModeInput;
    }

    private void OnDisable()
    {
        if (_playerControls == null) return;
        _playerControls.Gameplay.Click.performed -= HandleClickInput;
        _playerControls.Gameplay.Wait.performed -= HandleWaitActionInput;
        _playerControls.Gameplay.EndTurn.performed -= HandleEndTurnActionInput;
        _playerControls.Gameplay.ToggleAttackMode.performed -= HandleToggleAttackModeInput;
        _playerControls.Gameplay.Disable();
    }

    private void HandleToggleAttackModeInput(InputAction.CallbackContext context)
    {
        if (_selectedUnit == null || !_selectedUnit.IsAlive || _currentState == InputState.UnitMoving || _currentState == InputState.WaitingForTurn) return; 

        if (_currentState == InputState.UnitActionPhase)
        {
            if (_selectedUnit.CanAffordAction(ATTACK_ACTION_COST))
            {
                _currentState = InputState.SelectingAttackTarget;
                ClearReachableHighlight(true); 
                ShowAttackRange(_selectedUnit); // This is the correct place to call it for state entry
                DebugHelper.Log($"{_selectedUnit.unitName} entered Attack Targeting mode. AP: {_selectedUnit.currentActionPoints}", this);
            }
            else
            {
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford ATTACK (cost: {ATTACK_ACTION_COST}). Has {_selectedUnit.currentActionPoints} AP. Cannot enter attack mode.", this);
            }
        }
        else if (_currentState == InputState.SelectingAttackTarget)
        {
            _currentState = InputState.UnitActionPhase;
            ClearAttackRangeHighlight(true); 
            AttemptToShowMoveRangeForSelectedUnit(); 
            DebugHelper.Log($"{_selectedUnit.unitName} exited Attack Targeting mode, back to Unit Action Phase.", this);
        }
    }

    private void HandleClickInput(InputAction.CallbackContext context)
    {
        if (_selectedUnit == null || !_selectedUnit.IsAlive || _mainCamera == null || _currentState == InputState.UnitMoving || _currentState == InputState.WaitingForTurn) return; 

        Vector2 screenPosition = _playerControls.Gameplay.Point.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
        Plane xyPlane = new Plane(Vector3.forward, Vector3.zero);

        if (!xyPlane.Raycast(ray, out float distance)) { return; }

        Vector3 worldPoint = ray.GetPoint(distance);
        Vector2Int clickedGridPos = GridManager.Instance.WorldToGrid(worldPoint);
        Tile clickedTile = GridManager.Instance.GetTile(clickedGridPos);

        if (clickedTile == null) { DebugHelper.Log("Clicked on an invalid tile area.", this); return; }

        if (_currentState == InputState.UnitActionPhase)
        {
            ProcessClickForMove(clickedTile);
        }
        else if (_currentState == InputState.SelectingAttackTarget)
        {
            ProcessClickForAttack(clickedTile);
        }
    }

    private void ProcessClickForMove(Tile clickedTile)
    {
        if (!_selectedUnit.IsAlive) return; 

        if (clickedTile == _selectedUnit.CurrentTile)
        {
            DebugHelper.Log("Clicked selected unit's tile. No move action taken.", this);
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
                // If the occupant is an enemy, perhaps switch to attack targeting? For now, just block.
                DebugHelper.Log($"Cannot move to occupied tile {clickedTile.gridPosition} (Occupant: {clickedTile.occupyingUnit?.unitName}).", this);
                return;
            }

            List<Tile> path = _pathfinder.FindPath(_selectedUnit.CurrentTile.gridPosition, clickedTile.gridPosition, _selectedUnit);

            if (path != null && path.Count > 0)
            {
                ClearReachableHighlight(false); 
                ShowPathHighlight(path);
                _currentState = InputState.UnitMoving;
                DebugHelper.Log($"Path confirmed to {clickedTile.gridPosition}. Requesting move for {_selectedUnit.unitName}.", this);
                StartCoroutine(MoveUnitAlongPath(_selectedUnit, path));
            }
            else
            {
                DebugHelper.LogWarning($"No path calculated to {clickedTile.gridPosition} for {_selectedUnit.unitName}. It was in reachable, but pathing failed.", this);
                ClearPathHighlight(); 
            }
        }
        else
        {
            DebugHelper.Log($"Clicked tile {clickedTile.gridPosition} is not in {_selectedUnit.unitName}'s reachable move range. No action taken.", this);
        }
    }

    private void ProcessClickForAttack(Tile clickedTile)
    {
        if (!_selectedUnit.IsAlive) return; 

        if (!_selectedUnit.CanAffordAction(ATTACK_ACTION_COST))
        {
            DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford ATTACK (cost: {ATTACK_ACTION_COST}). Has {_selectedUnit.currentActionPoints} AP. Attack blocked.", this);
            _currentState = InputState.UnitActionPhase;
            ClearAttackRangeHighlight(true);
            AttemptToShowMoveRangeForSelectedUnit();
            return;
        }

        if (_highlightedAttackRangeTiles.Contains(clickedTile))
        {
            Unit targetUnit = clickedTile.occupyingUnit;
            if (targetUnit != null && targetUnit != _selectedUnit && targetUnit.IsAlive) 
            {
                DebugHelper.Log($"{_selectedUnit.unitName} (AP: {_selectedUnit.currentActionPoints}) attacks {targetUnit.unitName} on tile {clickedTile.gridPosition}!", this);
                _selectedUnit.SpendActionPoints(ATTACK_ACTION_COST);
                
                targetUnit.TakeDamage(PLACEHOLDER_ATTACK_DAMAGE);

                ClearAttackRangeHighlight(true);
                _currentState = InputState.UnitActionPhase; 
                CheckAndHandleEndOfTurnActions(); 
            }
            else if (targetUnit == _selectedUnit)
            {
                DebugHelper.Log("Cannot target self with a basic attack.", this);
            }
            else if (targetUnit != null && !targetUnit.IsAlive) 
            {
                DebugHelper.Log($"Target {targetUnit.unitName} on tile {clickedTile.gridPosition} is already defeated. Attack cancelled.", this);
            }
            else
            {
                DebugHelper.Log($"No valid (living) target unit on selected attack tile {clickedTile.gridPosition}. Basic attack requires a living unit.", this);
            }
        }
        else 
        {
            DebugHelper.Log($"Clicked tile {clickedTile.gridPosition} is outside current attack range. Cancelling attack selection.", this);
            _currentState = InputState.UnitActionPhase;
            ClearAttackRangeHighlight(true);
            AttemptToShowMoveRangeForSelectedUnit();
        }
    }


    private void HandleWaitActionInput(InputAction.CallbackContext context)
    {
        if ((_currentState == InputState.UnitActionPhase || _currentState == InputState.SelectingAttackTarget) && _selectedUnit != null && _selectedUnit.IsAlive) 
        {
            if (!_selectedUnit.CanAffordAction(WAIT_ACTION_COST))
            {
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford WAIT action (Cost: {WAIT_ACTION_COST}). AP: {_selectedUnit.currentActionPoints}", this);
                return;
            }

            if (_currentState == InputState.SelectingAttackTarget)
            {
                ClearAttackRangeHighlight(true);
                DebugHelper.Log("Wait action selected while targeting attack. Attack targeting cancelled.", this);
            }
             _currentState = InputState.UnitActionPhase; 
            HandleWaitAction();
        }
    }

    private void HandleWaitAction() 
    {
        if (_selectedUnit == null || !_selectedUnit.IsAlive) return; 

        _selectedUnit.SpendActionPoints(WAIT_ACTION_COST);
        DebugHelper.Log($"{_selectedUnit.unitName} performs WAIT action. AP remaining: {_selectedUnit.currentActionPoints}", this);
        ClearReachableHighlight(true);
        ClearAttackRangeHighlight(true);
        ClearPathHighlight();
        TurnManager.Instance.EndUnitTurn(_selectedUnit);
    }

    private void HandleEndTurnActionInput(InputAction.CallbackContext context)
    {
        if ((_currentState == InputState.UnitActionPhase || _currentState == InputState.SelectingAttackTarget) && _selectedUnit != null && _selectedUnit.IsAlive) 
        {
            if (_currentState == InputState.SelectingAttackTarget)
            {
                ClearAttackRangeHighlight(true);
                DebugHelper.Log("End Turn selected while targeting attack. Attack targeting cancelled.", this);
            }
            _currentState = InputState.UnitActionPhase; 
            DebugHelper.Log($"{_selectedUnit.unitName} explicitly ends turn. AP remaining: {_selectedUnit.currentActionPoints}", this);
            ClearReachableHighlight(true);
            ClearAttackRangeHighlight(true);
            ClearPathHighlight();
            TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }
        else
        {
            DebugHelper.Log($"End Turn action ignored. Current State: {_currentState}, SelectedUnit: {(_selectedUnit != null ? _selectedUnit.unitName : "None")}", this);
        }
    }

    private void AttemptToShowMoveRangeForSelectedUnit()
    {
        if (_selectedUnit != null && _selectedUnit.IsAlive && _selectedUnit.CurrentTile != null && _currentState == InputState.UnitActionPhase && _selectedUnit.CanAffordAction(MOVE_ACTION_COST)) 
        {
            int unitMoveRange = _selectedUnit.CalculatedMoveRange;
            ShowReachableRange(_selectedUnit.CurrentTile.gridPosition, unitMoveRange, _selectedUnit);
        } else {
            ClearReachableHighlight(true); 
        }
    }

    private void ShowReachableRange(Vector2Int startPos, int range, Unit requestingUnit)
    {
        ClearReachableHighlight(true);
        if (_pathfinder == null || requestingUnit == null || !requestingUnit.IsAlive || requestingUnit.CurrentTile == null) return; 

        List<Tile> reachable = _pathfinder.GetReachableTiles(requestingUnit.CurrentTile.gridPosition, range, requestingUnit);
        _highlightedReachableTiles.AddRange(reachable);

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

    private void ShowAttackRange(Unit attacker)
    {
        // Clear previous first, to ensure it's always fresh if called multiple times (though we aim not to)
        ClearAttackRangeHighlight(true); 
        
        if (attacker == null || !attacker.IsAlive || attacker.CurrentTile == null || !attacker.CanAffordAction(ATTACK_ACTION_COST)) 
        {
            if (attacker != null && attacker.IsAlive && !attacker.CanAffordAction(ATTACK_ACTION_COST))
                 DebugHelper.LogWarning($"{attacker.unitName} cannot afford attack (AP: {attacker.currentActionPoints}), not showing attack range.", attacker);
            else if (attacker != null && !attacker.IsAlive)
                 DebugHelper.Log($"{attacker.unitName} is not alive, cannot show attack range.", attacker);
            return;
        }

        int attackRange = attacker.CalculatedAttackRange;
        DebugHelper.Log($"PlayerInputHandler: Showing Attack Range for {attacker.unitName}. Range: {attackRange}", this);

        List<Tile> attackableTiles = GridManager.Instance.GetTilesInRange(attacker.CurrentTile.gridPosition, attackRange);
        
        _highlightedAttackRangeTiles.AddRange(attackableTiles);
        foreach (Tile tile in _highlightedAttackRangeTiles)
        {
           if (tile != null && tile != attacker.CurrentTile) 
           {
               tile.SetHighlight(TileHighlightState.AttackRange);
           }
        }
    }

    private void ClearReachableHighlight(bool clearList)
    {
        foreach (Tile tile in _highlightedReachableTiles) {
            if (tile != null && (_selectedUnit == null || tile != _selectedUnit.CurrentTile || !_selectedUnit.IsAlive)) { // Added IsAlive check
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
                bool unitStillSelectedAndAlive = _selectedUnit != null && _selectedUnit.IsAlive;
                bool isNotSelectedUnitTile = !unitStillSelectedAndAlive || tile != _selectedUnit.CurrentTile;

                bool shouldRevertToReachable = _highlightedReachableTiles.Contains(tile) &&
                                               _currentState == InputState.UnitActionPhase && 
                                               isNotSelectedUnitTile;

                if (shouldRevertToReachable)
                {
                    tile.SetHighlight(TileHighlightState.MovementRange);
                }
                else if (isNotSelectedUnitTile) 
                {
                    tile.SetHighlight(TileHighlightState.None);
                }
                // If it IS the selected unit's tile, Update() will handle its highlight.
            }
        }
        _highlightedPathTiles.Clear();
    }

    private void ClearAttackRangeHighlight(bool clearList)
    {
        foreach (Tile tile in _highlightedAttackRangeTiles)
        {
            if (tile != null && (_selectedUnit == null || tile != _selectedUnit.CurrentTile || !_selectedUnit.IsAlive)) { // Added IsAlive check
                tile.SetHighlight(TileHighlightState.None);
            }
        }
        if (clearList) _highlightedAttackRangeTiles.Clear();
    }

    private void ClearSelectionAndHighlights()
    {
        ClearReachableHighlight(true);
        ClearPathHighlight();
        ClearAttackRangeHighlight(true);

        if (_selectedUnit != null && _selectedUnit.IsAlive && _selectedUnit.CurrentTile != null && _selectedUnit.CurrentTile.occupyingUnit == _selectedUnit) { 
            if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _selectedUnit && _selectedUnit.CompareTag("Player")) {
                 // Let Update() handle setting it to SelectedUnit if it's still this unit's active turn.
                 // If not, it should be None.
                 if (_currentState != InputState.UnitActionPhase && _currentState != InputState.SelectingAttackTarget)
                 {
                    _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.None);
                 } else {
                    _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
                 }
            } else {
                _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.None); 
            }
        }
        
        _selectedUnit = null;
        _currentState = InputState.WaitingForTurn;
    }

     private IEnumerator MoveUnitAlongPath(Unit unitToMove, List<Tile> path)
    {
        if (unitToMove == null || !unitToMove.IsAlive || path == null || path.Count == 0) { 
            _currentState = InputState.UnitActionPhase;
            AttemptToShowMoveRangeForSelectedUnit();
            yield break;
        }

        if (unitToMove != TurnManager.Instance.ActiveUnit) {
            DebugHelper.LogError($"MoveUnitAlongPath: {unitToMove.unitName} tried to move, but it's not the active unit's turn ({TurnManager.Instance.ActiveUnit?.unitName})!", this);
            ClearSelectionAndHighlights(); 
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

        if (!unitToMove.IsAlive) 
        {
            DebugHelper.Log($"{unitToMove.unitName} no longer alive after movement finished or was interrupted by death.", this);
            if (_selectedUnit == unitToMove) ClearSelectionAndHighlights();
            yield break;
        }

        DebugHelper.Log($"{unitToMove.unitName} finished movement. AP: {unitToMove.currentActionPoints}/{unitToMove.maxActionPoints}", this);
        ClearPathHighlight();

        if (_selectedUnit != null && _selectedUnit == unitToMove && TurnManager.Instance.ActiveUnit == _selectedUnit && _selectedUnit.IsAlive) 
        {
            _currentState = InputState.UnitActionPhase; 
            CheckAndHandleEndOfTurnActions();
        }
        else 
        {
            if (TurnManager.Instance.ActiveUnit != _selectedUnit || (_selectedUnit != null && !_selectedUnit.IsAlive) ) {
                ClearSelectionAndHighlights(); 
            } else if (_selectedUnit != null) { 
                _currentState = InputState.UnitActionPhase; 
                AttemptToShowMoveRangeForSelectedUnit(); 
            }
        }
    }

    private void CheckAndHandleEndOfTurnActions()
    {
        if (_selectedUnit == null || !_selectedUnit.IsAlive || TurnManager.Instance.ActiveUnit != _selectedUnit) return; 

        bool canMove = _selectedUnit.CanAffordAction(MOVE_ACTION_COST);
        bool canAttack = _selectedUnit.CanAffordAction(ATTACK_ACTION_COST);
        // Wait is implicitly possible if canAttack or canMove (both 1 AP) is true, or if AP == 1.
        // More robustly:
        bool canTakeAnyOneAPAction = _selectedUnit.currentActionPoints >= 1;


        if (canMove) 
        {
            AttemptToShowMoveRangeForSelectedUnit();
            if(_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            DebugHelper.Log($"{_selectedUnit.unitName} can take further actions (e.g., Move). AP: {_selectedUnit.currentActionPoints}", this);
        }
        else if (canAttack) // Cannot move, but can attack
        {
            ClearReachableHighlight(true); 
            if(_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            DebugHelper.Log($"{_selectedUnit.unitName} cannot move, but may be able to Attack. AP: {_selectedUnit.currentActionPoints}", this);
        }
        else if (canTakeAnyOneAPAction) // Cannot move or attack, but can still do *something* (like Wait)
        {
            ClearReachableHighlight(true); 
            ClearAttackRangeHighlight(true); 
            if(_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            DebugHelper.Log($"{_selectedUnit.unitName} has limited AP, can Wait or perform other 1 AP action. AP: {_selectedUnit.currentActionPoints}", this);
        }
        else // No AP left for common 1AP actions
        {
            ClearReachableHighlight(true);
            ClearAttackRangeHighlight(true);
            if(_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
            DebugHelper.Log($"{_selectedUnit.unitName} has no AP for further common actions. Auto-ending turn. AP: {_selectedUnit.currentActionPoints}", this);
            TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }
    }
}