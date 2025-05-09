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
    public GridTester gridTester; // Keep if still used, otherwise can be removed

    public const int MoveActionCost = 1;
    public const int WaitActionCost = 1;
    public const int AttackActionCost = 1;

    private PlayerInputStateBase _currentStateObject;
    private Unit _selectedUnit = null;
    private PlayerControls _playerControls;
    private Camera _mainCamera;
    private Pathfinder _pathfinder; // This will now be assigned from GridManager

    private List<Tile> _highlightedReachableTiles = new List<Tile>();
    private List<Tile> _highlightedPathTiles = new List<Tile>();
    private List<Tile> _highlightedAttackRangeTiles = new List<Tile>();

    public Unit SelectedUnit => _selectedUnit;
    public Pathfinder Pathfinder => _pathfinder; // Property remains the same
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

        // MODIFIED: Get Pathfinder instance from GridManager
        _pathfinder = GridManager.Instance.PathfinderInstance;
        if (_pathfinder == null)
        {
            DebugHelper.LogError("PIH: PathfinderInstance from GridManager is null! Pathfinding will not work.", this);
            // Optionally, disable PIH or parts of its functionality
            // For now, operations requiring pathfinder will likely fail or do nothing.
        }

        ChangeState(new PIH_WaitingForTurnState());
    }

    void Start()
    {
        // Initial unit assignment is handled by Update checking TurnManager.Instance.ActiveUnit
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
                !(_currentStateObject is PIH_UnitActionPhaseState || _currentStateObject is PIH_SelectingAttackTargetState || _currentStateObject is PIH_UnitMovingState))
            {
                _selectedUnit = currentTurnManagerActiveUnit;
                if (!(_currentStateObject is PIH_UnitMovingState))
                {
                    ChangeState(new PIH_UnitActionPhaseState());
                }
            }
        }
        else
        {
            if (_selectedUnit != null || !(_currentStateObject is PIH_WaitingForTurnState))
            {
                if (_selectedUnit != null && _selectedUnit.CurrentTile != null && _selectedUnit.CurrentTile.occupyingUnit == _selectedUnit)
                {
                    if (TurnManager.Instance.ActiveUnit != _selectedUnit)
                        _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.None);
                }
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
        if (_playerControls.Gameplay.enabled) _playerControls.Gameplay.Disable();
    }

    private void OnClickPerformed(InputAction.CallbackContext context)
    {
        if (_mainCamera == null || _currentStateObject == null || GridManager.Instance == null) return;

        Vector2 screenPosition = _playerControls.Gameplay.Point.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
        Plane xyPlane = new Plane(Vector3.forward, Vector3.zero); // Assuming grid is on Z=0 plane

        if (!xyPlane.Raycast(ray, out float distance)) { return; }

        Vector3 worldPoint = ray.GetPoint(distance);
        Vector2Int clickedGridPos = GridManager.Instance.WorldToGrid(worldPoint);
        Tile clickedTile = GridManager.Instance.GetTile(clickedGridPos);

        _currentStateObject.OnClickInput(context, clickedTile);
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

    public void ClearAllHighlights()
    {
        Tile previouslySelectedUnitTile = (_selectedUnit != null && _selectedUnit.IsAlive) ? _selectedUnit.CurrentTile : null;

        ClearReachableHighlight(true);
        ClearPathHighlight();
        ClearAttackRangeHighlight(true);

        if (_selectedUnit != null && _selectedUnit.IsAlive && _selectedUnit.CurrentTile != null)
        {
            if (TurnManager.Instance == null || TurnManager.Instance.ActiveUnit != _selectedUnit)
            {
                if (previouslySelectedUnitTile != null && previouslySelectedUnitTile.occupyingUnit == _selectedUnit)
                {
                    if (TurnManager.Instance == null || TurnManager.Instance.ActiveUnit == null || TurnManager.Instance.ActiveUnit.CurrentTile != previouslySelectedUnitTile)
                    {
                        previouslySelectedUnitTile.SetHighlight(TileHighlightState.None);
                    }
                }
            }
        }
    }

    public void AttemptToShowMoveRangeForSelectedUnit()
    {
        if (_selectedUnit != null && _selectedUnit.IsAlive && _selectedUnit.CurrentTile != null &&
            _selectedUnit.CanAffordAction(MoveActionCost) && (_currentStateObject is PIH_UnitActionPhaseState))
        {
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
        if (_pathfinder == null || requestingUnit == null || !requestingUnit.IsAlive || requestingUnit.CurrentTile == null)
        {
            if (_pathfinder == null) DebugHelper.LogWarning("PIH.ShowReachableRange: Pathfinder is null.", this);
            return;
        }

        List<Tile> reachable = _pathfinder.GetReachableTiles(requestingUnit.CurrentTile.gridPosition, range, requestingUnit);
        _highlightedReachableTiles.Clear();
        _highlightedReachableTiles.AddRange(reachable);

        foreach (Tile tile in _highlightedReachableTiles)
        {
            if (tile != null && tile != requestingUnit.CurrentTile)
            {
                tile.SetHighlight(TileHighlightState.MovementRange);
            }
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

        if (attacker == null || !attacker.IsAlive || attacker.CurrentTile == null || !attacker.CanAffordAction(AttackActionCost) || GridManager.Instance == null)
        {
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
        foreach (Tile tile in _highlightedReachableTiles)
        {
            if (tile != null)
            {
                bool isCurrentActiveUnitTile = TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != null && TurnManager.Instance.ActiveUnit.CurrentTile == tile;
                if (!isCurrentActiveUnitTile)
                {
                    tile.SetHighlight(TileHighlightState.None);
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
                bool isCurrentActiveUnitTile = TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != null && TurnManager.Instance.ActiveUnit.CurrentTile == tile;
                bool isPlayerSelectedUnitTile = _selectedUnit != null && _selectedUnit.CurrentTile == tile;

                if (_highlightedReachableTiles.Contains(tile) && (_currentStateObject is PIH_UnitActionPhaseState) && !isPlayerSelectedUnitTile && !isCurrentActiveUnitTile)
                {
                    tile.SetHighlight(TileHighlightState.MovementRange);
                }
                else if (!isPlayerSelectedUnitTile && !isCurrentActiveUnitTile)
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
            if (tile != null)
            {
                bool isCurrentActiveUnitTile = TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != null && TurnManager.Instance.ActiveUnit.CurrentTile == tile;
                if (!isCurrentActiveUnitTile)
                {
                    tile.SetHighlight(TileHighlightState.None);
                }
            }
        }
        if (clearList) _highlightedAttackRangeTiles.Clear();
    }

    public void CheckAndHandleEndOfTurnActionsPIH()
    {
        if (_selectedUnit == null || !_selectedUnit.IsAlive || TurnManager.Instance == null || TurnManager.Instance.ActiveUnit != _selectedUnit) return;

        bool canMove = _selectedUnit.CanAffordAction(MoveActionCost);
        bool canAttack = _selectedUnit.CanAffordAction(AttackActionCost);
        bool canTakeAnyOneAPAction = _selectedUnit.currentActionPoints >= 1;

        if (canMove || canAttack)
        {
            if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
        }
        else if (canTakeAnyOneAPAction)
        {
            ClearReachableHighlight(true);
            ClearAttackRangeHighlight(true);
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