// PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using MythTactics.Combat; 

public class PlayerInputHandler : MonoBehaviour
{
    [Header("UI References")]
    public ActionMenuUI actionMenuUI;
    public GameObject skillSelectionPanelPrefab;

    private SkillSelectionUI _skillSelectionPanelInstance; 

    [Header("Action Costs")] 
    public static int MoveActionCost = 1;
    public static int WaitActionCost = 1; 
    public static int AttackActionCost = 1;
    public static int SkillsActionCost = 0; 
    public static int ItemsActionCost = 0;  
    public static int InfoActionCost = 0;   

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
        if (actionMenuUI == null) { DebugHelper.LogError("PIH: ActionMenuUI reference is not set in the Inspector!", this); enabled = false; return; }
        if (skillSelectionPanelPrefab == null) { DebugHelper.LogError("PIH: SkillSelectionPanelPrefab not assigned in Inspector!", this); enabled = false; return; }

        _pathfinder = GridManager.Instance.PathfinderInstance;
        if (_pathfinder == null) DebugHelper.LogError("PIH: PathfinderInstance from GridManager is null! Pathfinding will not work.", this);

        // Ensure ActionMenuUI starts hidden and its internal state is clean
        if(actionMenuUI != null) actionMenuUI.HideMenu();


        ChangeState(new PIH_WaitingForTurnState());
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (TurnManager.Instance == null) { if (!(_currentStateObject is PIH_WaitingForTurnState)) ChangeState(new PIH_WaitingForTurnState()); return; }
        if (!CombatActive) { if (!(_currentStateObject is PIH_WaitingForTurnState)) { _selectedUnit = null; ChangeState(new PIH_WaitingForTurnState()); } return; }

        Unit currentTurnManagerActiveUnit = TurnManager.Instance.ActiveUnit;
        
        if (currentTurnManagerActiveUnit != null && currentTurnManagerActiveUnit.IsAlive && currentTurnManagerActiveUnit.CompareTag("Player") && currentTurnManagerActiveUnit.Movement != null)      
        {
            if (_selectedUnit != currentTurnManagerActiveUnit) 
            {
                _selectedUnit = currentTurnManagerActiveUnit;
                if (_selectedUnit.Stats == null) { DebugHelper.LogWarning($"PIH Update: Player unit {_selectedUnit.unitName} selected, but Stats component is null.", this); }
                if (!(_currentStateObject is PIH_SelectingMoveTargetState) && !(_currentStateObject is PIH_UnitMovingState) && !(_currentStateObject is PIH_SelectingAbilityTargetState) && !(_currentStateObject is PIH_SelectingAttackTargetState) ) 
                { ChangeState(new PIH_UnitActionPhaseState()); }
            }
            else if (!(_currentStateObject is PIH_UnitActionPhaseState || _currentStateObject is PIH_SelectingAttackTargetState || _currentStateObject is PIH_SelectingMoveTargetState || _currentStateObject is PIH_UnitMovingState || _currentStateObject is PIH_SelectingAbilityTargetState )) 
            { ChangeState(new PIH_UnitActionPhaseState()); }
        }
        else { if (_selectedUnit != null || !(_currentStateObject is PIH_WaitingForTurnState)) { _selectedUnit = null; SelectedAbility = null; ChangeState(new PIH_WaitingForTurnState()); } }
        _currentStateObject?.UpdateState();
    }

    public void ChangeState(PlayerInputStateBase newState)
    {
        _currentStateObject?.ExitState(); 
        _currentStateObject = newState;
        
        string pihSelectedUnitName = _selectedUnit != null ? _selectedUnit.unitName : "null";
        string tmActiveUnitName = TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != null ? TurnManager.Instance.ActiveUnit.unitName : "null";
        bool isPlayerSelected = _selectedUnit != null && _selectedUnit.CompareTag("Player");
        bool isTurnManagerActiveSelected = TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _selectedUnit;

        DebugHelper.Log($"PIH ChangeState Check: NewState={newState.GetType().Name}, SelectedUnit={pihSelectedUnitName}, IsPlayerTag={isPlayerSelected}, TM.ActiveUnit={tmActiveUnitName}, IsTMActiveUnitAlsoSelectedUnit={isTurnManagerActiveSelected}", this);

        ClearAllHighlights(); 

        if (_currentStateObject is PIH_UnitActionPhaseState && 
            _selectedUnit != null && 
            _selectedUnit.CompareTag("Player") && 
            TurnManager.Instance != null && 
            TurnManager.Instance.ActiveUnit == _selectedUnit)
        {
            DebugHelper.Log("PIH ChangeState: Conditions met to show action menu.", this);
            ShowActionMenuForSelectedUnitPublic();
        }
        else 
        {
            actionMenuUI?.HideMenu();
        }
        
        if (!(_currentStateObject is PIH_SelectingAbilityTargetState )) 
        {
            _skillSelectionPanelInstance?.HidePanel();
        }
        
        _currentStateObject.EnterState(this); 
        DebugHelper.Log($"PIH changed to state (finalized): {newState.GetType().Name}", this);
    }

    private void OnEnable()
    {
        if (_playerControls == null) _playerControls = new PlayerControls();
        _playerControls.Gameplay.Enable();
        _playerControls.Gameplay.Click.performed += OnClickPerformedHandler; 
        _playerControls.Gameplay.EndTurn.performed += OnEndTurnInputHandler; 
        _playerControls.Gameplay.ToggleAttackMode.performed += OnToggleAttackModeInputHandler; 
        _playerControls.Gameplay.SelectAbility.performed += OnSelectAbilityInputHandler; 
        _playerControls.Gameplay.ToggleActionMenu.performed += OnToggleActionMenuPerformedHandler;
        
        ActionMenuUI.OnActionSelected += HandleActionMenuSelection; 
        SkillSelectionUI.OnSkillAbilitySelected += HandleSkillSelectedFromPanel; 
    }

    private void OnDisable()
    {
        if (_playerControls == null) return;
        _playerControls.Gameplay.Click.performed -= OnClickPerformedHandler; 
        _playerControls.Gameplay.EndTurn.performed -= OnEndTurnInputHandler;
        _playerControls.Gameplay.ToggleAttackMode.performed -= OnToggleAttackModeInputHandler;
        _playerControls.Gameplay.SelectAbility.performed -= OnSelectAbilityInputHandler;
        _playerControls.Gameplay.ToggleActionMenu.performed -= OnToggleActionMenuPerformedHandler;

        ActionMenuUI.OnActionSelected -= HandleActionMenuSelection; 
        SkillSelectionUI.OnSkillAbilitySelected -= HandleSkillSelectedFromPanel; 

        if (_playerControls.Gameplay.enabled) _playerControls.Gameplay.Disable();
    }

    private void OnClickPerformedHandler(InputAction.CallbackContext context) { _currentStateObject?.OnClickInput(context, GetClickedTileFromMouse()); }
    private void OnToggleAttackModeInputHandler(InputAction.CallbackContext context) { _currentStateObject?.OnToggleAttackModeInput(context); }
    private void OnSelectAbilityInputHandler(InputAction.CallbackContext context) { _currentStateObject?.OnSelectAbilityInput(context); }
    private void OnEndTurnInputHandler(InputAction.CallbackContext context) { _currentStateObject?.OnEndTurnInput(context); }

    private void OnToggleActionMenuPerformedHandler(InputAction.CallbackContext context)
    {
        if (_selectedUnit == null || !CombatActive || !_selectedUnit.CompareTag("Player") || TurnManager.Instance.ActiveUnit != _selectedUnit)
        {
            actionMenuUI?.HideMenu(); _skillSelectionPanelInstance?.HidePanel(); return;
        }
        if (_skillSelectionPanelInstance != null && _skillSelectionPanelInstance.IsVisible())
        {
            _skillSelectionPanelInstance.HidePanel();
            if (_currentStateObject is PIH_UnitActionPhaseState) { ShowActionMenuForSelectedUnitPublic(); }
            return; 
        }
        if (actionMenuUI.IsVisible())
        {
            actionMenuUI.HideMenu();
            if(_currentStateObject is PIH_UnitActionPhaseState && _selectedUnit.Movement.CurrentTile != null)
            { _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); }
        }
        else
        {
            if (_currentStateObject is PIH_UnitActionPhaseState) { ShowActionMenuForSelectedUnitPublic(); }
            else { ChangeState(new PIH_UnitActionPhaseState()); }
        }
    }
    
    public void ShowActionMenuForSelectedUnitPublic()
    {
        if (_selectedUnit != null && actionMenuUI != null && _mainCamera != null && 
            TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _selectedUnit) 
        {
            _skillSelectionPanelInstance?.HidePanel();
            
            Vector2 screenPos = _mainCamera.WorldToScreenPoint(_selectedUnit.transform.position);
            DebugHelper.Log($"PIH ShowActionMenuForSelectedUnitPublic: Attempting to show menu for '{_selectedUnit.unitName}' via actionMenuUI.ShowMenu(). ScreenPos: {screenPos}", this);
            
            actionMenuUI.ShowMenu(_selectedUnit, screenPos); 
            
            if (_selectedUnit.Movement.CurrentTile != null)
            { 
                _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
            }
        }
        else
        {
            string reason = _selectedUnit == null ? "_selectedUnit is null" : 
                            (actionMenuUI == null ? "actionMenuUI is null" : 
                            (_mainCamera == null ? "_mainCamera is null" : 
                            (TurnManager.Instance == null ? "TurnManager.Instance is null" :
                            (TurnManager.Instance.ActiveUnit != _selectedUnit ? $"TM ActiveUnit ('{TurnManager.Instance.ActiveUnit?.name}') != SelectedUnit ('{_selectedUnit?.name}')" 
                            : "Unknown reason for not showing menu"))));
            DebugHelper.LogWarning($"PIH ShowActionMenuForSelectedUnitPublic: Not shown. Reason: {reason}", this);
        }
    }

    private Tile GetClickedTileFromMouse()
    {
        if (_mainCamera == null || GridManager.Instance == null) return null;
        Vector2 screenPosition = _playerControls.Gameplay.Point.ReadValue<Vector2>();
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return null; 
        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
        if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) return null;
        return GridManager.Instance.GetTile(GridManager.Instance.WorldToGrid(ray.GetPoint(distance)));
    }
    
    public void HandleActionFromHotKey(string actionName)
    {
        if (_selectedUnit == null) return;
        Debug.Log($"PIH: Hotkey Action: '{actionName}' for unit '{_selectedUnit.unitName}'.", this);
        actionMenuUI?.HideMenu(); 
        _skillSelectionPanelInstance?.HidePanel();
        ProcessActionSelection(actionName); 
    }
    
    private void HandleActionMenuSelection(Unit unit, string actionName) 
    {
        if (unit != _selectedUnit || _selectedUnit == null) { Debug.LogWarning($"PIH: ActionMenuSelection: Action '{actionName}' for unit '{unit?.unitName}', but selected is '{_selectedUnit?.unitName}'. Ignoring.", this); return; }
        Debug.Log($"PIH: ActionMenuSelection: '{actionName}' for unit '{unit.unitName}'.", this);
        _skillSelectionPanelInstance?.HidePanel(); 
        ProcessActionSelection(actionName);
    }

    private void ProcessActionSelection(string actionName)
    {
        bool actionPotentiallyEndsTurnOrRequiresApCheck = false;
        switch (actionName)
        {
            case "Move":
                if (_selectedUnit.CanAffordAPForAction(MoveActionCost))
                { ChangeState(new PIH_SelectingMoveTargetState()); } 
                else { Debug.LogWarning($"PIH: {_selectedUnit.unitName} cannot afford 'Move'.", this); ChangeState(new PIH_UnitActionPhaseState()); }
                break;
            case "Attack":
                if (_selectedUnit.CanAffordAPForAction(AttackActionCost)) 
                { ShowAttackRange(_selectedUnit); ChangeState(new PIH_SelectingAttackTargetState()); } 
                else { Debug.LogWarning($"PIH: {_selectedUnit.unitName} cannot afford 'Attack'.", this); ChangeState(new PIH_UnitActionPhaseState()); }
                break;
            case "Wait":
                if (_selectedUnit.CanAffordAPForAction(WaitActionCost)) { _selectedUnit.SpendAPForAction(WaitActionCost); actionPotentiallyEndsTurnOrRequiresApCheck = true; ChangeState(new PIH_UnitActionPhaseState()); }
                else { Debug.LogWarning($"PIH: {_selectedUnit.unitName} cannot afford 'Wait'.", this); ChangeState(new PIH_UnitActionPhaseState()); }
                break;
            case "Skills":
                if (_selectedUnit.CanAffordAPForAction(SkillsActionCost)) { ShowSkillSelectionPanel(); } 
                else { Debug.LogWarning($"PIH: {_selectedUnit.unitName} cannot afford 'Skills' menu.", this); ChangeState(new PIH_UnitActionPhaseState()); }
                break;
            default: 
                Debug.Log($"PIH: Action '{actionName}' for {_selectedUnit.unitName} - default processing (revert to action phase).", this);
                ChangeState(new PIH_UnitActionPhaseState());
                break;
        }
        if (actionPotentiallyEndsTurnOrRequiresApCheck) { CheckAndHandleEndOfTurnActionsPIH(); }
    }
    
    private void ShowSkillSelectionPanel()
    {
        if (_selectedUnit == null || skillSelectionPanelPrefab == null) return;
        if (_skillSelectionPanelInstance == null)
        {
            Canvas mainCanvas = FindFirstObjectByType<Canvas>(); 
            if (mainCanvas == null) { Debug.LogError("PIH: No Canvas found!"); return; }
            GameObject panelGO = Instantiate(skillSelectionPanelPrefab, mainCanvas.transform);
            _skillSelectionPanelInstance = panelGO.GetComponent<SkillSelectionUI>();
            if (_skillSelectionPanelInstance == null) { Debug.LogError("PIH: SkillSelectionPanelPrefab missing SkillSelectionUI!", panelGO); Destroy(panelGO); return; }
        }
        Vector2 panelPosition = (Vector2)_mainCamera.WorldToScreenPoint(_selectedUnit.transform.position) + new Vector2(0, 250); 
        _skillSelectionPanelInstance.ShowPanel(_selectedUnit, panelPosition, _selectedUnit.knownAbilities);
        actionMenuUI?.HideMenu(); 
    }

    private void HandleSkillSelectedFromPanel(Unit caster, AbilitySO selectedAbility)
    {
        if (caster != _selectedUnit || selectedAbility == null) return;
        if (!_selectedUnit.Combat.CanAffordAbility(selectedAbility, true)) { ChangeState(new PIH_UnitActionPhaseState()); return; }
        SelectedAbility = selectedAbility; 
        ChangeState(new PIH_SelectingAbilityTargetState()); 
    }

    public void ClearAllHighlights()
    {
        List<Tile> tilesToClearReach = new List<Tile>(_highlightedReachableTiles);
        List<Tile> tilesToClearPath = new List<Tile>(_highlightedPathTiles);
        List<Tile> tilesToClearAttack = new List<Tile>(_highlightedAttackRangeTiles);
        List<Tile> tilesToClearAbility = new List<Tile>(_highlightedAbilityRangeTiles);

        foreach (Tile tile in tilesToClearReach) { if (tile != null) tile.SetHighlight(TileHighlightState.None); }
        foreach (Tile tile in tilesToClearPath) { if (tile != null) tile.SetHighlight(TileHighlightState.None); }
        foreach (Tile tile in tilesToClearAttack) { if (tile != null) tile.SetHighlight(TileHighlightState.None); }
        foreach (Tile tile in tilesToClearAbility) { if (tile != null) tile.SetHighlight(TileHighlightState.None); }

        _highlightedReachableTiles.Clear(); 
        _highlightedPathTiles.Clear(); 
        _highlightedAttackRangeTiles.Clear(); 
        _highlightedAbilityRangeTiles.Clear(); 
        
        if (_selectedUnit?.Movement?.CurrentTile != null)
        {
            bool isSelectedUnitsTurn = TurnManager.Instance?.ActiveUnit == _selectedUnit;
            bool isActionPhaseAndMenuShouldBeComing = _currentStateObject is PIH_UnitActionPhaseState && isSelectedUnitsTurn;

            if (!isActionPhaseAndMenuShouldBeComing) 
            {
                 if (_selectedUnit.Movement.CurrentTile.CurrentHighlightState == TileHighlightState.SelectedUnit || 
                    _selectedUnit.Movement.CurrentTile.CurrentHighlightState == TileHighlightState.ActiveTurnUnit)
                {
                    if(!isSelectedUnitsTurn || 
                       !(_currentStateObject is PIH_WaitingForTurnState || _currentStateObject is PIH_UnitActionPhaseState)) // Keep highlight if in action phase or waiting for turn (where unit might still be "focused")
                         _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.None);
                }
            }
        }
    }
    
    public void ShowReachableRange(Vector2Int startPos, int range, Unit requestingUnit)
    {
        if (_pathfinder == null || requestingUnit?.IsAlive == false || requestingUnit.Movement?.CurrentTile == null) return;
        ClearReachableHighlight(true); 
        _highlightedReachableTiles.AddRange(_pathfinder.GetReachableTiles(requestingUnit.Movement.CurrentTile.gridPosition, range, requestingUnit));
        foreach (Tile tile in _highlightedReachableTiles) { if (tile != null && tile != requestingUnit.Movement.CurrentTile) tile.SetHighlight(TileHighlightState.MovementRange); }
        if (requestingUnit.Movement.CurrentTile != null) requestingUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
    }

    public void ShowPathHighlight(List<Tile> path)
    {
        ClearPathHighlight(); 
        if (path == null || path.Count == 0) return;
        _highlightedPathTiles.AddRange(path); 
        foreach (Tile tileInPath in _highlightedPathTiles)
        {
            if (tileInPath != null && (_selectedUnit?.Movement?.CurrentTile == null || tileInPath != _selectedUnit.Movement.CurrentTile))
            {
                if(tileInPath.CurrentHighlightState != TileHighlightState.MovementRange && tileInPath.CurrentHighlightState != TileHighlightState.SelectedUnit)
                    tileInPath.SetHighlight(TileHighlightState.Path);
            }
        }
    }

    public void ShowAttackRange(Unit attacker) { 
        if (attacker?.IsAlive == false || attacker.Movement?.CurrentTile == null || !attacker.CanAffordAPForAction(AttackActionCost) || GridManager.Instance == null) return;
        ClearAttackRangeHighlight(true); 
        _highlightedAttackRangeTiles.AddRange(GridManager.Instance.GetTilesInRange(attacker.Movement.CurrentTile.gridPosition, attacker.CalculatedAttackRange));
        foreach (Tile tile in _highlightedAttackRangeTiles) { if (tile != null && tile != attacker.Movement.CurrentTile) tile.SetHighlight(TileHighlightState.AttackRange); }
        if (attacker.Movement.CurrentTile != null) attacker.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
    }
    public void ShowAbilityRange(Unit caster, AbilitySO ability) { 
        if (caster?.IsAlive == false || caster.Movement?.CurrentTile == null || ability == null || caster.Combat == null || !caster.Combat.CanAffordAbility(ability) || GridManager.Instance == null) return;
        ClearAbilityRangeHighlight(true); 
        _highlightedAbilityRangeTiles.AddRange(GridManager.Instance.GetTilesInRange(caster.Movement.CurrentTile.gridPosition, ability.range));
        foreach (Tile tile in _highlightedAbilityRangeTiles) { if (tile != null && tile != caster.Movement.CurrentTile) tile.SetHighlight(TileHighlightState.AbilityRange); }
        if (caster.Movement.CurrentTile != null) caster.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
    }

    public void ClearReachableHighlight(bool clearList) { foreach (Tile tile in _highlightedReachableTiles) { if (tile != null && tile.CurrentHighlightState == TileHighlightState.MovementRange ) tile.SetHighlight(TileHighlightState.None); } if (clearList) _highlightedReachableTiles.Clear(); }
    public void ClearPathHighlight() { foreach (Tile tile in _highlightedPathTiles) { if (tile != null && tile.CurrentHighlightState == TileHighlightState.Path) tile.SetHighlight(TileHighlightState.None); } _highlightedPathTiles.Clear(); }
    public void ClearAttackRangeHighlight(bool clearList) { foreach (Tile tile in _highlightedAttackRangeTiles) { if (tile != null && tile.CurrentHighlightState == TileHighlightState.AttackRange) tile.SetHighlight(TileHighlightState.None); } if (clearList) _highlightedAttackRangeTiles.Clear(); }
    public void ClearAbilityRangeHighlight(bool clearList) { foreach (Tile tile in _highlightedAbilityRangeTiles) { if (tile != null && tile.CurrentHighlightState == TileHighlightState.AbilityRange) tile.SetHighlight(TileHighlightState.None); } if (clearList) _highlightedAbilityRangeTiles.Clear(); }

    public void CheckAndHandleEndOfTurnActionsPIH()
    {
        if (_selectedUnit?.IsAlive == false || _selectedUnit.Movement == null || TurnManager.Instance?.ActiveUnit != _selectedUnit) return;
        if (_selectedUnit.Combat == null) { DebugHelper.LogError($"PIH: SelectedUnit {_selectedUnit.unitName} missing UnitCombat!", _selectedUnit); TurnManager.Instance.EndUnitTurn(_selectedUnit); return; }
        bool canTakeAnyOneAPAction = _selectedUnit.CurrentActionPoints >= 1; 
        if (!canTakeAnyOneAPAction) 
        {
            actionMenuUI?.HideMenu(); _skillSelectionPanelInstance?.HidePanel(); 
            if (_selectedUnit.Movement.CurrentTile != null) _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
            DebugHelper.Log($"PIH: {_selectedUnit.unitName} has no AP left. Ending turn.", _selectedUnit);
            TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }
    }
}