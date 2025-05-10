// PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // Ensured this is present
using System.Linq;
using MythTactics.Combat;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("References")]
    public GridTester gridTester; 
    public const int MoveActionCost = 1; 
    public const int WaitActionCost = 1; 
    public const int AttackActionCost = 1; 

    private PlayerInputStateBase _currentStateObject;
    private Unit _selectedUnit = null;
    private PlayerControls _playerControls;
    private Camera _mainCamera;
    private Pathfinder _pathfinder; 

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
        if (currentTurnManagerActiveUnit != null && currentTurnManagerActiveUnit.IsAlive && 
            currentTurnManagerActiveUnit.CompareTag("Player") && currentTurnManagerActiveUnit.Movement != null) // MODIFIED: Add Movement null check
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
        else 
        {
            if (_selectedUnit != null || !(_currentStateObject is PIH_WaitingForTurnState))
            {
                // MODIFIED: Access CurrentTile via _selectedUnit.Movement
                if (_selectedUnit?.Movement?.CurrentTile?.occupyingUnit == _selectedUnit && TurnManager.Instance.ActiveUnit != _selectedUnit) 
                {
                    _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.None);
                }
                _selectedUnit = null; 
                SelectedAbility = null; 
                ChangeState(new PIH_WaitingForTurnState());
            }
        }
        _currentStateObject?.UpdateState();
    }

    public void ChangeState(PlayerInputStateBase newState)
    {
        _currentStateObject?.ExitState(); 
        _currentStateObject = newState;
        _currentStateObject.EnterState(this); 
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
        if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) return;
        
        Tile clickedTile = GridManager.Instance.GetTile(GridManager.Instance.WorldToGrid(ray.GetPoint(distance)));
        _currentStateObject.OnClickInput(context, clickedTile);
    }

    private void OnToggleAttackModePerformed(InputAction.CallbackContext context) { _currentStateObject?.OnToggleAttackModeInput(context); }
    private void OnSelectAbilityPerformed(InputAction.CallbackContext context) { _currentStateObject?.OnSelectAbilityInput(context); }
    private void OnWaitPerformed(InputAction.CallbackContext context) { _currentStateObject?.OnWaitInput(context); }
    private void OnEndTurnPerformed(InputAction.CallbackContext context) { _currentStateObject?.OnEndTurnInput(context); }

    public void ClearAllHighlights()
    {
        // MODIFIED: Access CurrentTile via _selectedUnit.Movement
        Tile previouslySelectedUnitTile = (_selectedUnit?.IsAlive == true && _selectedUnit.Movement != null) ? _selectedUnit.Movement.CurrentTile : null;

        ClearReachableHighlight(true); 
        ClearPathHighlight(); 
        ClearAttackRangeHighlight(true); 
        ClearAbilityRangeHighlight(true); 
        
        if (_selectedUnit?.IsAlive == true && _selectedUnit.Movement != null && _selectedUnit.Movement.CurrentTile != null)
        {
            if (TurnManager.Instance == null || TurnManager.Instance.ActiveUnit != _selectedUnit)
            {
                if (previouslySelectedUnitTile?.occupyingUnit == _selectedUnit) 
                    previouslySelectedUnitTile.SetHighlight(TileHighlightState.None);
            }
            else 
            {
                 _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }
        }
    }

    public void AttemptToShowMoveRangeForSelectedUnit()
    {
        // MODIFIED: Check _selectedUnit.Movement and access CurrentTile & CalculatedMoveRange via it
        if (_selectedUnit?.IsAlive == true && _selectedUnit.Movement != null && _selectedUnit.Movement.CurrentTile != null && 
            _selectedUnit.CanAffordAPForAction(MoveActionCost) && 
            (_currentStateObject is PIH_UnitActionPhaseState)) 
        {
            ShowReachableRange(_selectedUnit.Movement.CurrentTile.gridPosition, _selectedUnit.Movement.CalculatedMoveRange, _selectedUnit);
        }
        else 
        {
            ClearReachableHighlight(true);
        }
    }

    public void ShowReachableRange(Vector2Int startPos, int range, Unit requestingUnit)
    {
        ClearReachableHighlight(true);
        // MODIFIED: Check requestingUnit.Movement and access CurrentTile via it
        if (_pathfinder == null || requestingUnit?.IsAlive == false || requestingUnit.Movement == null || requestingUnit.Movement.CurrentTile == null) 
        { 
            if (_pathfinder == null) DebugHelper.LogWarning("PIH.ShowReachableRange: Pathfinder is null.", this); 
            if (requestingUnit?.Movement == null) DebugHelper.LogWarning($"PIH.ShowReachableRange: {requestingUnit?.unitName} Movement component is null.", this);
            return; 
        }
        
        _highlightedReachableTiles.Clear();
        _highlightedReachableTiles.AddRange(_pathfinder.GetReachableTiles(requestingUnit.Movement.CurrentTile.gridPosition, range, requestingUnit));
        foreach (Tile tile in _highlightedReachableTiles) 
        {
            if (tile != null && tile != requestingUnit.Movement.CurrentTile) 
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
            // MODIFIED: Access CurrentTile via _selectedUnit.Movement
            if (tileInPath != null && (_selectedUnit == null || _selectedUnit.Movement == null || tileInPath != _selectedUnit.Movement.CurrentTile)) 
            { 
                tileInPath.SetHighlight(TileHighlightState.Path); 
                _highlightedPathTiles.Add(tileInPath); 
            }
        }
    }

    public void ShowAttackRange(Unit attacker)
    {
        ClearAttackRangeHighlight(true);
        // MODIFIED: Check attacker.Movement and access CurrentTile via it
        if (attacker?.IsAlive == false || attacker.Movement == null || attacker.Movement.CurrentTile == null || 
            !attacker.CanAffordAPForAction(AttackActionCost) || 
            GridManager.Instance == null) 
            return;
        
        _highlightedAttackRangeTiles.Clear();
        // CalculatedAttackRange is still on Unit
        _highlightedAttackRangeTiles.AddRange(GridManager.Instance.GetTilesInRange(attacker.Movement.CurrentTile.gridPosition, attacker.CalculatedAttackRange));
        foreach (Tile tile in _highlightedAttackRangeTiles) 
        {
            if (tile != null && tile != attacker.Movement.CurrentTile) 
                tile.SetHighlight(TileHighlightState.AttackRange);
        }
    }

    public void ShowAbilityRange(Unit caster, AbilitySO ability)
    {
        ClearAbilityRangeHighlight(true);
        ClearAttackRangeHighlight(true);  
        ClearReachableHighlight(true); 
        
        // MODIFIED: Check caster.Movement and access CurrentTile via it
        if (caster?.IsAlive == false || caster.Movement == null || caster.Movement.CurrentTile == null || ability == null || 
            caster.Combat == null || !caster.Combat.CanAffordAbility(ability) || 
            GridManager.Instance == null) 
            return;
        
        _highlightedAbilityRangeTiles.Clear();
        _highlightedAbilityRangeTiles.AddRange(GridManager.Instance.GetTilesInRange(caster.Movement.CurrentTile.gridPosition, ability.range));
        foreach (Tile tile in _highlightedAbilityRangeTiles) 
        {
            if (tile != null && tile != caster.Movement.CurrentTile) 
                tile.SetHighlight(TileHighlightState.AbilityRange); 
        }
        if (caster.Movement.CurrentTile != null) caster.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
    }
    
    public void ClearReachableHighlight(bool clearList)
    {
        foreach (Tile tile in _highlightedReachableTiles)
        {
            // MODIFIED: Access CurrentTile via _selectedUnit.Movement
            if (tile != null && (_selectedUnit?.Movement == null || tile != _selectedUnit.Movement.CurrentTile))
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
            // MODIFIED: Access CurrentTile via _selectedUnit.Movement
            if (tile != null && (_selectedUnit?.Movement == null || tile != _selectedUnit.Movement.CurrentTile))
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
            // MODIFIED: Access CurrentTile via _selectedUnit.Movement
            if (tile != null && (_selectedUnit?.Movement == null || tile != _selectedUnit.Movement.CurrentTile))
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
            // MODIFIED: Access CurrentTile via _selectedUnit.Movement
            if (tile != null && (_selectedUnit?.Movement == null || tile != _selectedUnit.Movement.CurrentTile))
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
        // MODIFIED: Check _selectedUnit.Movement
        if (_selectedUnit?.IsAlive == false || _selectedUnit.Movement == null || TurnManager.Instance == null || TurnManager.Instance.ActiveUnit != _selectedUnit) return;
        
        if (_selectedUnit.Combat == null)
        {
            DebugHelper.LogError($"PIH: SelectedUnit {_selectedUnit.unitName} missing UnitCombat component!", _selectedUnit);
            TurnManager.Instance.EndUnitTurn(_selectedUnit); 
            return;
        }

        bool canMove = _selectedUnit.CanAffordAPForAction(MoveActionCost); 
        bool canAttack = _selectedUnit.CanAffordAPForAction(AttackActionCost); 
        bool canUseAnyAbility = _selectedUnit.knownAbilities.Any(ability => _selectedUnit.Combat.CanAffordAbility(ability)); 
        bool canTakeAnyOneAPAction = _selectedUnit.currentActionPoints >= 1; 

        if (canMove || canAttack || canUseAnyAbility)
        {
            // MODIFIED: Access CurrentTile via _selectedUnit.Movement
            if (_selectedUnit.Movement.CurrentTile != null) _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            if ((_currentStateObject is PIH_UnitActionPhaseState) && canMove) 
            {
                AttemptToShowMoveRangeForSelectedUnit();
            }
        }
        else if (canTakeAnyOneAPAction) 
        {
            ClearReachableHighlight(true); ClearAttackRangeHighlight(true); ClearAbilityRangeHighlight(true);
            // MODIFIED: Access CurrentTile via _selectedUnit.Movement
            if (_selectedUnit.Movement.CurrentTile != null) _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
        }
        else 
        {
            ClearAllHighlights(); 
            // MODIFIED: Access CurrentTile via _selectedUnit.Movement
            if (_selectedUnit.Movement.CurrentTile != null) _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }
    }
}