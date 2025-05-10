// PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MythTactics.Combat;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("References")]
    public GridTester gridTester; // Can be null if not testing
    public const int MoveActionCost = 1; // AP cost
    public const int WaitActionCost = 1; // AP cost
    public const int AttackActionCost = 1; // AP cost
    // public const int AbilityActionCost = 1; // This was likely a general thought, specific ability.apCost is used

    private PlayerInputStateBase _currentStateObject;
    private Unit _selectedUnit = null;
    private PlayerControls _playerControls;
    private Camera _mainCamera;
    private Pathfinder _pathfinder; // From GridManager

    private List<Tile> _highlightedReachableTiles = new List<Tile>();
    private List<Tile> _highlightedPathTiles = new List<Tile>();
    private List<Tile> _highlightedAttackRangeTiles = new List<Tile>();
    private List<Tile> _highlightedAbilityRangeTiles = new List<Tile>();

    public Unit SelectedUnit => _selectedUnit;
    public Pathfinder Pathfinder => _pathfinder;
    public PlayerControls PlayerControls => _playerControls;
    public Camera MainCamera => _mainCamera;
    public IReadOnlyList<Tile> HighlightedReachableTiles => _highlightedReachableTiles.AsReadOnly();
    public IReadOnlyList<Tile> HighlightedAttackRangeTiles => _highlightedAttackRangeTiles.AsReadOnly();
    public IReadOnlyList<Tile> HighlightedAbilityRangeTiles => _highlightedAbilityRangeTiles.AsReadOnly();
    public bool CombatActive => TurnManager.Instance != null && TurnManager.Instance.IsCombatActive;
    public AbilitySO SelectedAbility { get; set; }

    void Awake()
    {
        _mainCamera = Camera.main;
        _playerControls = new PlayerControls();

        if (GridManager.Instance == null) { DebugHelper.LogError("PIH: GridManager.Instance is missing!", this); enabled = false; return; }
        if (TurnManager.Instance == null) { DebugHelper.LogError("PIH: TurnManager.Instance is missing!", this); enabled = false; return; }
        
        _pathfinder = GridManager.Instance.PathfinderInstance;
        if (_pathfinder == null) DebugHelper.LogError("PIH: PathfinderInstance from GridManager is null! Pathfinding will not work.", this);
        
        ChangeState(new PIH_WaitingForTurnState());
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (TurnManager.Instance == null) 
        { 
            if (!(_currentStateObject is PIH_WaitingForTurnState)) ChangeState(new PIH_WaitingForTurnState()); 
            return; 
        }

        if (!CombatActive) 
        { 
            if (!(_currentStateObject is PIH_WaitingForTurnState)) 
            { 
                _selectedUnit = null; 
                ChangeState(new PIH_WaitingForTurnState()); 
            } 
            return; 
        }

        Unit currentTurnManagerActiveUnit = TurnManager.Instance.ActiveUnit;
        if (currentTurnManagerActiveUnit != null && currentTurnManagerActiveUnit.IsAlive && currentTurnManagerActiveUnit.CompareTag("Player"))
        {
            if (_selectedUnit != currentTurnManagerActiveUnit ||
                !(_currentStateObject is PIH_UnitActionPhaseState || 
                  _currentStateObject is PIH_SelectingAttackTargetState || 
                  _currentStateObject is PIH_UnitMovingState || 
                  _currentStateObject is PIH_SelectingAbilityTargetState)) 
            {
                _selectedUnit = currentTurnManagerActiveUnit;
                if (!(_currentStateObject is PIH_UnitMovingState) && 
                    !(_currentStateObject is PIH_SelectingAbilityTargetState) && 
                    !(_currentStateObject is PIH_SelectingAttackTargetState))
                { 
                    ChangeState(new PIH_UnitActionPhaseState()); 
                }
            }
        }
        else // Not player's turn, or player is dead, or no active unit
        {
            if (_selectedUnit != null || !(_currentStateObject is PIH_WaitingForTurnState))
            {
                // Clear selection highlight if it was the player's unit but it's no longer their turn
                if (_selectedUnit?.CurrentTile?.occupyingUnit == _selectedUnit && TurnManager.Instance.ActiveUnit != _selectedUnit) 
                {
                    _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.None);
                }
                _selectedUnit = null; 
                SelectedAbility = null; 
                ChangeState(new PIH_WaitingForTurnState());
            }
        }
        _currentStateObject?.UpdateState(); // Current state handles its own logic
    }

    public void ChangeState(PlayerInputStateBase newState)
    {
        _currentStateObject?.ExitState(); // Call exit on old state
        _currentStateObject = newState;
        _currentStateObject.EnterState(this); // Call enter on new state
        DebugHelper.Log($"PIH changed to state: {newState.GetType().Name}", this);
    }

    private void OnEnable()
    {
        if (_playerControls == null) _playerControls = new PlayerControls();
        _playerControls.Gameplay.Enable();
        _playerControls.Gameplay.Click.performed += OnClickPerformed;
        _playerControls.Gameplay.Wait.performed += OnWaitPerformed;
        _playerControls.Gameplay.EndTurn.performed += OnEndTurnPerformed;
        _playerControls.Gameplay.ToggleAttackMode.performed += OnToggleAttackModePerformed;
        _playerControls.Gameplay.SelectAbility.performed += OnSelectAbilityPerformed; 
    }

    private void OnDisable()
    {
        if (_playerControls == null) return;
        _playerControls.Gameplay.Click.performed -= OnClickPerformed;
        _playerControls.Gameplay.Wait.performed -= OnWaitPerformed;
        _playerControls.Gameplay.EndTurn.performed -= OnEndTurnPerformed;
        _playerControls.Gameplay.ToggleAttackMode.performed -= OnToggleAttackModePerformed;
        _playerControls.Gameplay.SelectAbility.performed -= OnSelectAbilityPerformed; 
        if (_playerControls.Gameplay.enabled) _playerControls.Gameplay.Disable();
    }

    private void OnClickPerformed(InputAction.CallbackContext context)
    {
        if (_mainCamera == null || _currentStateObject == null || GridManager.Instance == null) return;
        Vector2 screenPosition = _playerControls.Gameplay.Point.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
        if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) return; // Assuming grid is on Z=0 plane
        
        Tile clickedTile = GridManager.Instance.GetTile(GridManager.Instance.WorldToGrid(ray.GetPoint(distance)));
        _currentStateObject.OnClickInput(context, clickedTile);
    }

    private void OnToggleAttackModePerformed(InputAction.CallbackContext context) { _currentStateObject?.OnToggleAttackModeInput(context); }
    private void OnSelectAbilityPerformed(InputAction.CallbackContext context) { _currentStateObject?.OnSelectAbilityInput(context); }
    private void OnWaitPerformed(InputAction.CallbackContext context) { _currentStateObject?.OnWaitInput(context); }
    private void OnEndTurnPerformed(InputAction.CallbackContext context) { _currentStateObject?.OnEndTurnInput(context); }

    public void ClearAllHighlights()
    {
        Tile previouslySelectedUnitTile = (_selectedUnit?.IsAlive == true) ? _selectedUnit.CurrentTile : null;

        ClearReachableHighlight(true); 
        ClearPathHighlight(); 
        ClearAttackRangeHighlight(true); 
        ClearAbilityRangeHighlight(true); 
        
        if (_selectedUnit?.IsAlive == true && _selectedUnit.CurrentTile != null)
        {
            if (TurnManager.Instance == null || TurnManager.Instance.ActiveUnit != _selectedUnit)
            {
                // If it's not the active unit's turn, ensure its tile is cleared if it was the one selected
                if (previouslySelectedUnitTile?.occupyingUnit == _selectedUnit) // Ensure it's still this unit's tile
                    previouslySelectedUnitTile.SetHighlight(TileHighlightState.None);
            }
            else // It IS the active unit's turn
            {
                 // Re-highlight active unit's tile as 'SelectedUnit' as it's the baseline for action phase
                 _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }
        }
    }

    public void AttemptToShowMoveRangeForSelectedUnit()
    {
        if (_selectedUnit?.IsAlive == true && _selectedUnit.CurrentTile != null && 
            _selectedUnit.CanAffordAPForAction(MoveActionCost) && // CanAffordAPForAction is still on Unit
            (_currentStateObject is PIH_UnitActionPhaseState)) 
        {
            // CalculatedMoveRange is still on Unit
            ShowReachableRange(_selectedUnit.CurrentTile.gridPosition, _selectedUnit.CalculatedMoveRange, _selectedUnit);
        }
        else 
        {
            ClearReachableHighlight(true);
        }
    }

    public void ShowReachableRange(Vector2Int startPos, int range, Unit requestingUnit)
    {
        ClearReachableHighlight(true);
        if (_pathfinder == null || requestingUnit?.IsAlive == false || requestingUnit.CurrentTile == null) 
        { 
            if (_pathfinder == null) DebugHelper.LogWarning("PIH.ShowReachableRange: Pathfinder is null.", this); 
            return; 
        }
        
        _highlightedReachableTiles.Clear();
        // GetReachableTiles takes Unit for context (e.g. move costs)
        _highlightedReachableTiles.AddRange(_pathfinder.GetReachableTiles(requestingUnit.CurrentTile.gridPosition, range, requestingUnit));
        foreach (Tile tile in _highlightedReachableTiles) 
        {
            if (tile != null && tile != requestingUnit.CurrentTile) 
                tile.SetHighlight(TileHighlightState.MovementRange);
        }
    }

    public void ShowPathHighlight(List<Tile> path)
    {
        ClearPathHighlight();
        if (path == null || path.Count == 0) return;
        _highlightedPathTiles.Clear();
        foreach (Tile tileInPath in path) 
        {
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
        if (attacker?.IsAlive == false || attacker.CurrentTile == null || 
            !attacker.CanAffordAPForAction(AttackActionCost) || // CanAffordAPForAction is still on Unit
            GridManager.Instance == null) 
            return;
        
        _highlightedAttackRangeTiles.Clear();
        // CalculatedAttackRange is still on Unit
        _highlightedAttackRangeTiles.AddRange(GridManager.Instance.GetTilesInRange(attacker.CurrentTile.gridPosition, attacker.CalculatedAttackRange));
        foreach (Tile tile in _highlightedAttackRangeTiles) 
        {
            if (tile != null && tile != attacker.CurrentTile) 
                tile.SetHighlight(TileHighlightState.AttackRange);
        }
    }

    public void ShowAbilityRange(Unit caster, AbilitySO ability)
    {
        ClearAbilityRangeHighlight(true);
        ClearAttackRangeHighlight(true);  
        ClearReachableHighlight(true); 
        
        // MODIFIED: Call CanAffordAbility via caster.Combat
        if (caster?.IsAlive == false || caster.CurrentTile == null || ability == null || 
            caster.Combat == null || !caster.Combat.CanAffordAbility(ability) || 
            GridManager.Instance == null) 
            return;
        
        _highlightedAbilityRangeTiles.Clear();
        _highlightedAbilityRangeTiles.AddRange(GridManager.Instance.GetTilesInRange(caster.CurrentTile.gridPosition, ability.range));
        foreach (Tile tile in _highlightedAbilityRangeTiles) 
        {
            if (tile != null && tile != caster.CurrentTile) 
                tile.SetHighlight(TileHighlightState.AbilityRange); 
        }
        if (caster.CurrentTile != null) caster.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
    }
    
    public void ClearReachableHighlight(bool clearList)
    {
        foreach (Tile tile in _highlightedReachableTiles)
        {
            if (tile != null && tile != _selectedUnit?.CurrentTile)
            {
                if (_highlightedAttackRangeTiles.Contains(tile) && _currentStateObject is PIH_SelectingAttackTargetState) continue;
                if (_highlightedAbilityRangeTiles.Contains(tile) && _currentStateObject is PIH_SelectingAbilityTargetState) continue;
                tile.SetHighlight(TileHighlightState.None);
            }
        }
        if (clearList) _highlightedReachableTiles.Clear();
    }

    public void ClearPathHighlight()
    {
        foreach (Tile tile in _highlightedPathTiles)
        {
            if (tile != null && tile != _selectedUnit?.CurrentTile)
            {
                if (_highlightedReachableTiles.Contains(tile) && _currentStateObject is PIH_UnitActionPhaseState)
                    tile.SetHighlight(TileHighlightState.MovementRange);
                else if (_highlightedAttackRangeTiles.Contains(tile) && _currentStateObject is PIH_SelectingAttackTargetState)
                    tile.SetHighlight(TileHighlightState.AttackRange);
                else if (_highlightedAbilityRangeTiles.Contains(tile) && _currentStateObject is PIH_SelectingAbilityTargetState)
                    tile.SetHighlight(TileHighlightState.AbilityRange);
                else
                    tile.SetHighlight(TileHighlightState.None);
            }
        }
        _highlightedPathTiles.Clear();
    }

    public void ClearAttackRangeHighlight(bool clearList)
    {
        foreach (Tile tile in _highlightedAttackRangeTiles)
        {
            if (tile != null && tile != _selectedUnit?.CurrentTile)
            {
                if (_highlightedAbilityRangeTiles.Contains(tile) && _currentStateObject is PIH_SelectingAbilityTargetState) continue;
                if (_highlightedReachableTiles.Contains(tile) && _currentStateObject is PIH_UnitActionPhaseState)
                    tile.SetHighlight(TileHighlightState.MovementRange);
                else
                    tile.SetHighlight(TileHighlightState.None);
            }
        }
        if (clearList) _highlightedAttackRangeTiles.Clear();
    }

    public void ClearAbilityRangeHighlight(bool clearList)
    {
        foreach (Tile tile in _highlightedAbilityRangeTiles)
        {
             if (tile != null && tile != _selectedUnit?.CurrentTile)
            {
                if (_highlightedAttackRangeTiles.Contains(tile) && _currentStateObject is PIH_SelectingAttackTargetState) continue;
                if (_highlightedReachableTiles.Contains(tile) && _currentStateObject is PIH_UnitActionPhaseState)
                    tile.SetHighlight(TileHighlightState.MovementRange);
                else
                    tile.SetHighlight(TileHighlightState.None);
            }
        }
        if (clearList) _highlightedAbilityRangeTiles.Clear();
    }

    public void CheckAndHandleEndOfTurnActionsPIH()
    {
        if (_selectedUnit?.IsAlive == false || TurnManager.Instance == null || TurnManager.Instance.ActiveUnit != _selectedUnit) return;
        
        // Ensure Combat component exists before trying to use its methods
        if (_selectedUnit.Combat == null)
        {
            DebugHelper.LogError($"PlayerInputHandler: SelectedUnit {_selectedUnit.unitName} is missing UnitCombat component!", _selectedUnit);
            TurnManager.Instance.EndUnitTurn(_selectedUnit); // End turn if critical component is missing
            return;
        }

        bool canMove = _selectedUnit.CanAffordAPForAction(MoveActionCost); 
        bool canAttack = _selectedUnit.CanAffordAPForAction(AttackActionCost); 
        // MODIFIED: Call CanAffordAbility via _selectedUnit.Combat
        bool canUseAnyAbility = _selectedUnit.knownAbilities.Any(ability => _selectedUnit.Combat.CanAffordAbility(ability)); 
        bool canTakeAnyOneAPAction = _selectedUnit.currentActionPoints >= 1; 

        if (canMove || canAttack || canUseAnyAbility)
        {
            if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            if ((_currentStateObject is PIH_UnitActionPhaseState) && canMove) 
            {
                AttemptToShowMoveRangeForSelectedUnit();
            }
        }
        else if (canTakeAnyOneAPAction) 
        {
            ClearReachableHighlight(true); ClearAttackRangeHighlight(true); ClearAbilityRangeHighlight(true);
            if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
        }
        else 
        {
            ClearAllHighlights(); 
            if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }
    }
}