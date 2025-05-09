// PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MythTactics.Combat; // NEW: Namespace for states

public class PlayerInputHandler : MonoBehaviour
{
    [Header("References")]
    public GridTester gridTester; 
    
    // Action Costs - made public static for access from states
    public const int MoveActionCost = 1;
    public const int WaitActionCost = 1;
    public const int AttackActionCost = 1;

    // State Machine
    private PlayerInputStateBase _currentStateObject;

    // Unit and Control References
    private Unit _selectedUnit = null;
    private PlayerControls _playerControls;
    private Camera _mainCamera;
    private Pathfinder _pathfinder; 

    // Highlighted Tile Lists (public getters for states)
    private List<Tile> _highlightedReachableTiles = new List<Tile>();
    private List<Tile> _highlightedPathTiles = new List<Tile>();
    private List<Tile> _highlightedAttackRangeTiles = new List<Tile>();

    public Unit SelectedUnit => _selectedUnit;
    public Pathfinder Pathfinder => _pathfinder;
    public PlayerControls PlayerControls => _playerControls; 
    public Camera MainCamera => _mainCamera;
    public IReadOnlyList<Tile> HighlightedReachableTiles => _highlightedReachableTiles.AsReadOnly();
    public IReadOnlyList<Tile> HighlightedAttackRangeTiles => _highlightedAttackRangeTiles.AsReadOnly();
    public bool CombatActive => TurnManager.Instance != null && TurnManager.Instance.IsCombatActive;


    void Awake()
    {
        _mainCamera = Camera.main;
        _playerControls = new PlayerControls();
        
        if (GridManager.Instance == null) { DebugHelper.LogError("PIH: GridManager.Instance is missing!", this); enabled = false; return; }
        if (TurnManager.Instance == null) { DebugHelper.LogError("PIH: TurnManager.Instance is missing!", this); enabled = false; return; }
        
        _pathfinder = new Pathfinder(GridManager.Instance);
        
        ChangeState(new PIH_WaitingForTurnState());
    }

    void Start()
    {
        // Initial unit assignment is now handled by Update checking TurnManager.Instance.ActiveUnit
    }
    
    void Update()
    {
        if (!Application.isPlaying) return;
        if (TurnManager.Instance == null) // Guard against TurnManager being destroyed
        {
            if (!(_currentStateObject is PIH_WaitingForTurnState))
            {
                ChangeState(new PIH_WaitingForTurnState());
            }
            return;
        }

        if (!CombatActive)
        {
            if (!(_currentStateObject is PIH_WaitingForTurnState)) 
            {
                _selectedUnit = null; // Ensure selection is cleared if combat ends
                ChangeState(new PIH_WaitingForTurnState());
            }
            return;
        }

        Unit currentTurnManagerActiveUnit = TurnManager.Instance.ActiveUnit;

        if (currentTurnManagerActiveUnit != null && currentTurnManagerActiveUnit.IsAlive && currentTurnManagerActiveUnit.CompareTag("Player"))
        {
            if (_selectedUnit != currentTurnManagerActiveUnit || !(_currentStateObject is PIH_UnitActionPhaseState || _currentStateObject is PIH_SelectingAttackTargetState) ) 
            {
                // If the active unit changed to this player unit, or if we were in a different state (like WaitingForTurn, UnitMoving)
                // and now this player unit is active again.
                _selectedUnit = currentTurnManagerActiveUnit; 
                if (!(_currentStateObject is PIH_UnitMovingState)) // Don't interrupt moving state by just becoming active again
                {
                    ChangeState(new PIH_UnitActionPhaseState());
                }
            }
        }
        else 
        {
            if (_selectedUnit != null || !(_currentStateObject is PIH_WaitingForTurnState) )
            {
                _selectedUnit = null; 
                ChangeState(new PIH_WaitingForTurnState());
            }
        }
        
        _currentStateObject?.UpdateState();
    }

    public void ChangeState(PlayerInputStateBase newState)
    {
        _currentStateObject?.ExitState();
        _currentStateObject = newState;
        // DebugHelper.Log($"PIH: Changing state to {newState.GetType().Name}", this); // Verbose
        _currentStateObject.EnterState(this);
    }

    private void OnEnable()
    {
        if (_playerControls == null) _playerControls = new PlayerControls();
        _playerControls.Gameplay.Enable();
        _playerControls.Gameplay.Click.performed += OnClickPerformed;
        _playerControls.Gameplay.Wait.performed += OnWaitPerformed;
        _playerControls.Gameplay.EndTurn.performed += OnEndTurnPerformed;
        _playerControls.Gameplay.ToggleAttackMode.performed += OnToggleAttackModePerformed;
    }

    private void OnDisable()
    {
        if (_playerControls == null) return;
        _playerControls.Gameplay.Click.performed -= OnClickPerformed;
        _playerControls.Gameplay.Wait.performed -= OnWaitPerformed;
        _playerControls.Gameplay.EndTurn.performed -= OnEndTurnPerformed;
        _playerControls.Gameplay.ToggleAttackMode.performed -= OnToggleAttackModePerformed;
        _playerControls.Gameplay.Disable();
    }

    private void OnClickPerformed(InputAction.CallbackContext context)
    {
        if (_mainCamera == null || _currentStateObject == null) return; // Basic guards
        
        Vector2 screenPosition = _playerControls.Gameplay.Point.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
        Plane xyPlane = new Plane(Vector3.forward, Vector3.zero);

        if (!xyPlane.Raycast(ray, out float distance)) { return; }

        Vector3 worldPoint = ray.GetPoint(distance);
        Vector2Int clickedGridPos = GridManager.Instance.WorldToGrid(worldPoint);
        Tile clickedTile = GridManager.Instance.GetTile(clickedGridPos);
        
        _currentStateObject.OnClickInput(context, clickedTile); // Delegate to current state
    }

    private void OnToggleAttackModePerformed(InputAction.CallbackContext context)
    {
        _currentStateObject?.OnToggleAttackModeInput(context);
    }

    private void OnWaitPerformed(InputAction.CallbackContext context)
    {
        _currentStateObject?.OnWaitInput(context);
    }

    private void OnEndTurnPerformed(InputAction.CallbackContext context)
    {
        _currentStateObject?.OnEndTurnInput(context);
    }

    // --- Highlight Management Methods ---
    public void ClearAllHighlights()
    {
        ClearReachableHighlight(true);
        ClearPathHighlight();
        ClearAttackRangeHighlight(true);
        
        // If a unit was selected, but its tile is highlighted as ActiveTurnUnit, don't change it to None.
        // Let TurnManager handle ActiveTurnUnit highlight. PIH focuses on selection/action highlights.
        if (_selectedUnit != null && _selectedUnit.IsAlive && _selectedUnit.CurrentTile != null)
        {
            Tile unitTile = _selectedUnit.CurrentTile;
            if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _selectedUnit)
            {
                 // If it's the active unit's tile, PIH_UnitActionPhaseState.EnterState will set it to SelectedUnit.
                 // If we are transitioning to WaitingForTurn, this selected unit will be nulled out anyway.
            }
            else if (unitTile.occupyingUnit == _selectedUnit) // Ensure it's still occupying the tile
            {
                 // If it's not the active unit's turn, clear the selected highlight.
                 unitTile.SetHighlight(TileHighlightState.None);
            }
        }
    }
    
    public void AttemptToShowMoveRangeForSelectedUnit()
    {
        if (_selectedUnit != null && _selectedUnit.IsAlive && _selectedUnit.CurrentTile != null && 
            _selectedUnit.CanAffordAction(MoveActionCost) && (_currentStateObject is PIH_UnitActionPhaseState) ) 
        {
            ShowReachableRange(_selectedUnit.CurrentTile.gridPosition, _selectedUnit.CalculatedMoveRange, _selectedUnit);
        } else {
            ClearReachableHighlight(true); 
        }
    }

    public void ShowReachableRange(Vector2Int startPos, int range, Unit requestingUnit)
    {
        ClearReachableHighlight(true); // Always clear previous before showing new
        if (_pathfinder == null || requestingUnit == null || !requestingUnit.IsAlive || requestingUnit.CurrentTile == null) return;

        List<Tile> reachable = _pathfinder.GetReachableTiles(requestingUnit.CurrentTile.gridPosition, range, requestingUnit);
        _highlightedReachableTiles.Clear(); 
        _highlightedReachableTiles.AddRange(reachable);

        foreach (Tile tile in _highlightedReachableTiles) {
             if (tile != null && tile != requestingUnit.CurrentTile) { // Don't highlight unit's own tile as movement target
                tile.SetHighlight(TileHighlightState.MovementRange);
             }
        }
    }

    public void ShowPathHighlight(List<Tile> path) 
    {
        ClearPathHighlight(); 
        if (path == null || path.Count == 0) return;
        _highlightedPathTiles.Clear();

        foreach (Tile tileInPath in path) {
            if (tileInPath != null && (_selectedUnit == null || tileInPath != _selectedUnit.CurrentTile)) 
            {
                tileInPath.SetHighlight(TileHighlightState.Path); 
                _highlightedPathTiles.Add(tileInPath); 
            }
        }
    }
    
    public void ShowAttackRange(Unit attacker)
    {
        ClearAttackRangeHighlight(true); 
        
        if (attacker == null || !attacker.IsAlive || attacker.CurrentTile == null || !attacker.CanAffordAction(AttackActionCost))
        {
            // Specific warnings/logs are better handled in the state that calls this, if needed.
            return;
        }

        int attackRangeVal = attacker.CalculatedAttackRange;
        List<Tile> attackableTilesResult = GridManager.Instance.GetTilesInRange(attacker.CurrentTile.gridPosition, attackRangeVal);
        _highlightedAttackRangeTiles.Clear(); 
        _highlightedAttackRangeTiles.AddRange(attackableTilesResult);

        foreach (Tile tile in _highlightedAttackRangeTiles)
        {
           if (tile != null && tile != attacker.CurrentTile) 
           {
               tile.SetHighlight(TileHighlightState.AttackRange);
           }
        }
    }

    public void ClearReachableHighlight(bool clearList)
    {
        foreach (Tile tile in _highlightedReachableTiles) {
            if (tile != null) {
                // Only clear if it's not the currently selected unit's tile being managed by PIH_UnitActionPhaseState
                // or the globally active unit's tile managed by TurnManager
                bool isSelectedUnitTile = (_selectedUnit != null && _selectedUnit.CurrentTile == tile);
                bool isActiveTurnUnitTile = (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != null && TurnManager.Instance.ActiveUnit.CurrentTile == tile);

                if (!isSelectedUnitTile && !isActiveTurnUnitTile)
                {
                    tile.SetHighlight(TileHighlightState.None); 
                }
                else if (isSelectedUnitTile && !(_currentStateObject is PIH_UnitActionPhaseState) && !(_currentStateObject is PIH_SelectingAttackTargetState))
                {
                    // If it's selected unit tile but we are not in a phase that actively manages it,
                    // it might revert to None if not also ActiveTurnUnit tile
                    if(!isActiveTurnUnitTile) tile.SetHighlight(TileHighlightState.None);
                }
            }
        }
        if (clearList) _highlightedReachableTiles.Clear();
    }

    public void ClearPathHighlight()
    {
        foreach (Tile tile in _highlightedPathTiles)
        {
            if (tile != null)
            {
                bool isSelectedUnitTile = (_selectedUnit != null && _selectedUnit.CurrentTile == tile);
                bool isActiveTurnUnitTile = (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != null && TurnManager.Instance.ActiveUnit.CurrentTile == tile);

                if (_highlightedReachableTiles.Contains(tile) && (_currentStateObject is PIH_UnitActionPhaseState) && !isSelectedUnitTile)
                {
                    tile.SetHighlight(TileHighlightState.MovementRange);
                }
                else if (!isSelectedUnitTile && !isActiveTurnUnitTile)
                {
                    tile.SetHighlight(TileHighlightState.None);
                }
            }
        }
        _highlightedPathTiles.Clear();
    }

    public void ClearAttackRangeHighlight(bool clearList)
    {
        foreach (Tile tile in _highlightedAttackRangeTiles)
        {
             if (tile != null) {
                bool isSelectedUnitTile = (_selectedUnit != null && _selectedUnit.CurrentTile == tile);
                bool isActiveTurnUnitTile = (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != null && TurnManager.Instance.ActiveUnit.CurrentTile == tile);
                if (!isSelectedUnitTile && !isActiveTurnUnitTile)
                {
                    tile.SetHighlight(TileHighlightState.None); 
                }
             }
        }
        if (clearList) _highlightedAttackRangeTiles.Clear();
    }

    public void CheckAndHandleEndOfTurnActionsPIH()
    {
        if (_selectedUnit == null || !_selectedUnit.IsAlive || TurnManager.Instance.ActiveUnit != _selectedUnit) return; 

        bool canMove = _selectedUnit.CanAffordAction(MoveActionCost);
        bool canAttack = _selectedUnit.CanAffordAction(AttackActionCost);
        bool canTakeAnyOneAPAction = _selectedUnit.currentActionPoints >= 1;

        if (canMove || canAttack) // If primary actions are possible, just ensure UI is up-to-date
        {
            // AttemptToShowMoveRangeForSelectedUnit(); // Called by PIH_UnitActionPhaseState.UpdateState
            if(_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            // DebugHelper.Log($"{_selectedUnit.unitName} can take further actions. AP: {_selectedUnit.currentActionPoints}", this);
        }
        else if (canTakeAnyOneAPAction) 
        {
            ClearReachableHighlight(true); 
            ClearAttackRangeHighlight(true); 
            if(_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            DebugHelper.Log($"{_selectedUnit.unitName} has limited AP, can Wait or perform other 1 AP action. AP: {_selectedUnit.currentActionPoints}", this);
        }
        else 
        {
            ClearAllHighlights(); // Clear all action-related highlights
            if(_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); // Keep unit selected visually for a moment
            DebugHelper.Log($"{_selectedUnit.unitName} has no AP for further common actions. Auto-ending turn. AP: {_selectedUnit.currentActionPoints}", this);
            if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
            // Update loop will detect ActiveUnit change and switch PIH to WaitingForTurnState
        }
    }
}