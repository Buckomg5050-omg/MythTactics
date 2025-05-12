// PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using MythTactics.Combat; 

public class PlayerInputHandler : MonoBehaviour
{
    // ... (fields remain the same) ...
    [Header("UI References")]
    public ActionMenuUI actionMenuUI;
    public GameObject skillSelectionPanelPrefab;
    public GameObject unitInfoPanelPrefab; 

    private SkillSelectionUI _skillSelectionPanelInstance; 
    private UnitInfoPanelUI _unitInfoPanelInstance; 

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
    public UnitInfoPanelUI UnitInfoPanelInstance => _unitInfoPanelInstance; 


    // ... (Awake, Update remain the same) ...
    void Awake()
    {
        _mainCamera = Camera.main;
        _playerControls = new PlayerControls();

        if (GridManager.Instance == null) { DebugHelper.LogError("PIH: GridManager.Instance is missing!", this); enabled = false; return; }
        if (TurnManager.Instance == null) { DebugHelper.LogError("PIH: TurnManager.Instance is missing!", this); enabled = false; return; }
        if (actionMenuUI == null) { DebugHelper.LogError("PIH: ActionMenuUI reference is not set in the Inspector!", this); enabled = false; return; }
        if (skillSelectionPanelPrefab == null) { DebugHelper.LogError("PIH: SkillSelectionPanelPrefab not assigned in Inspector!", this); enabled = false; return; }
        if (unitInfoPanelPrefab == null) { DebugHelper.LogWarning("PIH: UnitInfoPanelPrefab not assigned in Inspector. Info panel functionality will be limited.", this); } 

        _pathfinder = GridManager.Instance.PathfinderInstance;
        if (_pathfinder == null) DebugHelper.LogError("PIH: PathfinderInstance from GridManager is null! Pathfinding will not work.", this);

        if (unitInfoPanelPrefab != null) 
        {
            Canvas mainCanvas = FindFirstObjectByType<Canvas>();
            if (mainCanvas != null)
            {
                GameObject panelGO = Instantiate(unitInfoPanelPrefab, mainCanvas.transform);
                _unitInfoPanelInstance = panelGO.GetComponent<UnitInfoPanelUI>();
                if (_unitInfoPanelInstance == null)
                {
                    Debug.LogError("PIH: UnitInfoPanelPrefab is missing the UnitInfoPanelUI script!", panelGO);
                    Destroy(panelGO);
                }
                else
                {
                    _unitInfoPanelInstance.HidePanel(); 
                }
            }
            else
            {
                Debug.LogError("PIH: No Canvas found in scene to instantiate UnitInfoPanel!", this);
            }
        }
        // Ensure SkillSelectionPanel is instantiated (if not already handled by being part of a persistent UI manager)
        // For now, ShowSkillSelectionPanel handles instantiation on demand.
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
                if (!(_currentStateObject is PIH_SelectingMoveTargetState) && 
                    !(_currentStateObject is PIH_UnitMovingState) && 
                    !(_currentStateObject is PIH_SelectingAbilityTargetState) && 
                    !(_currentStateObject is PIH_SelectingAttackTargetState) &&
                    !(_currentStateObject is PIH_ViewingUnitInfoState)) 
                { ChangeState(new PIH_UnitActionPhaseState()); }
            }
            else if (!(_currentStateObject is PIH_UnitActionPhaseState || 
                       _currentStateObject is PIH_SelectingAttackTargetState || 
                       _currentStateObject is PIH_SelectingMoveTargetState || 
                       _currentStateObject is PIH_UnitMovingState || 
                       _currentStateObject is PIH_SelectingAbilityTargetState ||
                       _currentStateObject is PIH_ViewingUnitInfoState )) 
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
            // DebugHelper.Log("PIH ChangeState: Conditions met to show action menu.", this);
            ShowActionMenuForSelectedUnitPublic();
        }
        else 
        {
            actionMenuUI?.HideMenu();
        }
        
        // MODIFIED: Also hide skill selection panel if not in a state that uses it
        // (though specific states like PIH_UnitActionPhaseState might re-show action menu)
        if (!(_currentStateObject is PIH_SelectingAbilityTargetState) && !(_currentStateObject is PIH_UnitActionPhaseState && _skillSelectionPanelInstance != null && _skillSelectionPanelInstance.IsVisible()))
        {
            _skillSelectionPanelInstance?.HidePanel();
        }

        if (!(_currentStateObject is PIH_ViewingUnitInfoState))
        {
            _unitInfoPanelInstance?.HidePanel();
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
        UnitInfoPanelUI.OnInfoPanelClosedByButton += HandleInfoPanelClosedByButton;
        SkillSelectionUI.OnSkillPanelClosedByButton += HandleSkillPanelClosedByButton; // ADDED subscription
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
        UnitInfoPanelUI.OnInfoPanelClosedByButton -= HandleInfoPanelClosedByButton; 
        SkillSelectionUI.OnSkillPanelClosedByButton -= HandleSkillPanelClosedByButton; // ADDED unsubscription

        if (_playerControls.Gameplay.enabled) _playerControls.Gameplay.Disable();
    }

    private void HandleInfoPanelClosedByButton()
    {
        if (_currentStateObject is PIH_ViewingUnitInfoState)
        {
            DebugHelper.Log("PIH: InfoPanelCloseButton event received. Transitioning to UnitActionPhaseState.", this);
            ChangeState(new PIH_UnitActionPhaseState());
        }
    }

    // ADDED: Handler for SkillSelectionUI close button
    private void HandleSkillPanelClosedByButton()
    {
        // If skill panel is closed by its button, hide it and return to action phase
        DebugHelper.Log("PIH: SkillPanelCloseButton event received. Hiding panel and returning to UnitActionPhaseState.", this);
        _skillSelectionPanelInstance?.HidePanel(); // Explicitly hide
        ChangeState(new PIH_UnitActionPhaseState());
    }


    private void OnClickPerformedHandler(InputAction.CallbackContext context) { _currentStateObject?.OnClickInput(context, GetClickedTileFromMouse()); }
    private void OnToggleAttackModeInputHandler(InputAction.CallbackContext context) { _currentStateObject?.OnToggleAttackModeInput(context); }
    private void OnSelectAbilityInputHandler(InputAction.CallbackContext context) { _currentStateObject?.OnSelectAbilityInput(context); }
    private void OnEndTurnInputHandler(InputAction.CallbackContext context) { _currentStateObject?.OnEndTurnInput(context); }
    
    private void OnToggleActionMenuPerformedHandler(InputAction.CallbackContext context)
    {
        if (_currentStateObject != null)
        {
            // Allow the current state to handle this input first
            // For example, PIH_ViewingUnitInfoState uses this to close itself.
            _currentStateObject.OnToggleActionMenuInput(context);
            // If the state changed (e.g., from ViewingInfo to UnitActionPhase), return to avoid double processing.
            if (!(_currentStateObject is PIH_UnitActionPhaseState) && !(_currentStateObject is PIH_ViewingUnitInfoState && actionMenuUI.IsVisible())) // A bit complex, check if state is still one that would do default toggling
            {
                 // If the state was PIH_ViewingUnitInfoState, it would have changed state.
                 // If it changed to PIH_UnitActionPhaseState, the action menu will be shown by ChangeState.
                 // So, if we are no longer in a state that uses the menu, or if the menu is now visible due to state change, just return.
                if (!actionMenuUI.IsVisible() && !(_currentStateObject is PIH_ViewingUnitInfoState)) {
                    // This case is if a state handled it and hid everything, or we are in a non-menu state.
                } else if (actionMenuUI.IsVisible() || _skillSelectionPanelInstance?.IsVisible() == true || _unitInfoPanelInstance?.IsVisible() == true) {
                    // If any menu IS visible (could be after a state change), let the new state logic (or this function's subsequent part) handle it.
                } else {
                     return; // State handled it and likely changed state or hid menus.
                }
            }
        }

        if (_selectedUnit == null || !CombatActive || !_selectedUnit.CompareTag("Player") || TurnManager.Instance.ActiveUnit != _selectedUnit)
        {
            actionMenuUI?.HideMenu(); 
            _skillSelectionPanelInstance?.HidePanel();
            _unitInfoPanelInstance?.HidePanel();
            return;
        }

        // If UnitInfoPanel is visible (and we are not in PIH_ViewingUnitInfoState anymore due to above state handling, this means it's an edge case)
        // This path should ideally not be hit if PIH_ViewingUnitInfoState.OnToggleActionMenuInput works.
        if (_unitInfoPanelInstance != null && _unitInfoPanelInstance.IsVisible())
        {
            _unitInfoPanelInstance.HidePanel();
            // If we were in PIH_ViewingUnitInfoState, the state's own handler should have transitioned.
            // If we are somehow still here, transition to action phase.
            if (!(_currentStateObject is PIH_UnitActionPhaseState)) ChangeState(new PIH_UnitActionPhaseState());
            else ShowActionMenuForSelectedUnitPublic(); // If already in action phase, ensure menu is shown
            return;
        }

        if (_skillSelectionPanelInstance != null && _skillSelectionPanelInstance.IsVisible())
        {
            _skillSelectionPanelInstance.HidePanel(); // Hide it
            // And then transition to UnitActionPhaseState, which will show the ActionMenu
            ChangeState(new PIH_UnitActionPhaseState()); 
            return; 
        }
        
        if (actionMenuUI.IsVisible())
        {
            actionMenuUI.HideMenu();
            if(_currentStateObject is PIH_UnitActionPhaseState && _selectedUnit.Movement.CurrentTile != null)
            { _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); }
        }
        else // If no UI is open, and action menu is hidden, show it
        {
            if (_currentStateObject is PIH_UnitActionPhaseState) { ShowActionMenuForSelectedUnitPublic(); }
            else { ChangeState(new PIH_UnitActionPhaseState()); }
        }
    }
    
    // ... (ShowActionMenuForSelectedUnitPublic, GetClickedTileFromMouse, HandleActionFromHotKey, HandleActionMenuSelection, ProcessActionSelection remain similar) ...
    // ... (Small changes to ensure other panels are hidden when one is shown might be needed in those methods)

    public void ShowActionMenuForSelectedUnitPublic()
    {
        if (_selectedUnit != null && actionMenuUI != null && _mainCamera != null && 
            TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _selectedUnit) 
        {
            _skillSelectionPanelInstance?.HidePanel(); // Ensure other main panels are hidden
            _unitInfoPanelInstance?.HidePanel(); 
            
            Vector2 screenPos = _mainCamera.WorldToScreenPoint(_selectedUnit.transform.position);
            // DebugHelper.Log($"PIH ShowActionMenuForSelectedUnitPublic: Attempting to show menu for '{_selectedUnit.unitName}' via actionMenuUI.ShowMenu(). ScreenPos: {screenPos}", this);
            
            actionMenuUI.ShowMenu(_selectedUnit, screenPos); 
            
            if (_selectedUnit.Movement.CurrentTile != null)
            { 
                _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
            }
        }
        else
        {
            // ... (reason logging)
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
        _unitInfoPanelInstance?.HidePanel(); 
        ProcessActionSelection(actionName); 
    }
    
    private void HandleActionMenuSelection(Unit unit, string actionName) 
    {
        if (unit != _selectedUnit || _selectedUnit == null) { Debug.LogWarning($"PIH: ActionMenuSelection: Action '{actionName}' for unit '{unit?.unitName}', but selected is '{_selectedUnit?.unitName}'. Ignoring.", this); return; }
        Debug.Log($"PIH: ActionMenuSelection: '{actionName}' for unit '{unit.unitName}'.", this);
        
        // ActionMenuUI hides itself when a button is clicked if OnActionSelected is invoked.
        // We still need to ensure other panels are managed.
        _skillSelectionPanelInstance?.HidePanel(); 
        _unitInfoPanelInstance?.HidePanel(); 
        
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
                if (_selectedUnit.CanAffordAPForAction(SkillsActionCost)) { ShowSkillSelectionPanel(); } // Does not change state here directly
                else { Debug.LogWarning($"PIH: {_selectedUnit.unitName} cannot afford 'Skills' menu.", this); ChangeState(new PIH_UnitActionPhaseState()); }
                break;
            case "Info": 
                if (_unitInfoPanelInstance != null)
                {
                    ChangeState(new PIH_ViewingUnitInfoState());
                }
                else 
                { 
                    Debug.LogWarning("PIH: UnitInfoPanelInstance is null. Cannot show info.", this); 
                    ChangeState(new PIH_UnitActionPhaseState());
                }
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
        Vector2 panelPosition = (Vector2)_mainCamera.WorldToScreenPoint(_selectedUnit.transform.position) + new Vector2(150, 0); 
        _skillSelectionPanelInstance.ShowPanel(_selectedUnit, panelPosition, _selectedUnit.knownAbilities);
        actionMenuUI?.HideMenu(); 
        _unitInfoPanelInstance?.HidePanel(); 
        // Note: PIH state remains PIH_UnitActionPhaseState here. Selecting a skill will change it.
        // Closing the skill panel (via its own button) should revert to PIH_UnitActionPhaseState.
    }

    private void HandleSkillSelectedFromPanel(Unit caster, AbilitySO selectedAbility)
    {
        if (caster != _selectedUnit || selectedAbility == null) return;
        if (!_selectedUnit.Combat.CanAffordAbility(selectedAbility, true)) 
        { 
            _skillSelectionPanelInstance?.HidePanel(); // Hide panel if can't afford
            ChangeState(new PIH_UnitActionPhaseState()); 
            return; 
        }
        SelectedAbility = selectedAbility; 
        // SkillSelectionUI itself will call HidePanel when a skill is selected.
        ChangeState(new PIH_SelectingAbilityTargetState()); 
    }
    
    // ... (ClearAllHighlights, ShowReachableRange, etc. remain the same) ...
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
            bool isSpecialPhase = (_currentStateObject is PIH_UnitActionPhaseState || _currentStateObject is PIH_ViewingUnitInfoState) && isSelectedUnitsTurn;


            if (!isSpecialPhase) 
            {
                 if (_selectedUnit.Movement.CurrentTile.CurrentHighlightState == TileHighlightState.SelectedUnit || 
                    _selectedUnit.Movement.CurrentTile.CurrentHighlightState == TileHighlightState.ActiveTurnUnit)
                {
                    if(!isSelectedUnitsTurn || 
                       !(_currentStateObject is PIH_WaitingForTurnState || _currentStateObject is PIH_UnitActionPhaseState || _currentStateObject is PIH_ViewingUnitInfoState))
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
            actionMenuUI?.HideMenu(); 
            _skillSelectionPanelInstance?.HidePanel(); 
            _unitInfoPanelInstance?.HidePanel(); 
            if (_selectedUnit.Movement.CurrentTile != null) _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
            DebugHelper.Log($"PIH: {_selectedUnit.unitName} has no AP left. Ending turn.", _selectedUnit);
            TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }
    }

}