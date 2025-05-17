// PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using MythTactics.Combat; 
using System.Collections; 

public class PlayerInputHandler : MonoBehaviour
{
    [Header("UI References")]
    public ActionMenuUI actionMenuUI;
    public GameObject skillSelectionPanelPrefab;
    public GameObject unitInfoPanelPrefab; 

    [Header("Tooltip Settings")] 
    // REMOVED: public LayerMask unitLayerMask; 
    [Tooltip("Delay in seconds before a unit tooltip appears when hovering its tile.")]
    public float unitTooltipDelay = 0.5f; // This delay still applies

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
    
    // Fields for unit tooltip handling (tile-based)
    private Unit _hoveredUnitOnTileForTooltip = null; // RENAMED for clarity
    private Coroutine _unitTooltipCoroutine = null; 
    private Tile _lastHoveredTileWithUnit = null; // To track tile changes

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


    void Awake()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null) Debug.LogError("PIH: Main Camera not found!", this);

        _playerControls = new PlayerControls();

        if (GridManager.Instance == null) { DebugHelper.LogError("PIH: GridManager.Instance is missing!", this); enabled = false; return; }
        if (TurnManager.Instance == null) { DebugHelper.LogError("PIH: TurnManager.Instance is missing!", this); enabled = false; return; }
        if (actionMenuUI == null) { DebugHelper.LogError("PIH: ActionMenuUI reference is not set in the Inspector!", this); enabled = false; return; }
        if (skillSelectionPanelPrefab == null) { DebugHelper.LogError("PIH: SkillSelectionPanelPrefab not assigned in Inspector!", this); enabled = false; return; }
        if (unitInfoPanelPrefab == null) { DebugHelper.LogWarning("PIH: UnitInfoPanelPrefab not assigned in Inspector. Info panel functionality will be limited.", this); } 
        // REMOVED: LayerMask warning as it's no longer used for this
        // if (unitLayerMask.value == 0) { DebugHelper.LogWarning("PIH: Unit Layer Mask is not set (value is 0) in Inspector. Unit tooltips via raycast will likely not work. Please assign the 'Units' layer.", this); }


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
        HandleTileHoverForTooltip(); // MODIFIED: Call new method
    }
    
    // NEW METHOD: Handles tooltips based on tile hover
    private void HandleTileHoverForTooltip()
    {
        if (TooltipUI.Instance == null) return;

        // Suppress unit tooltips if major UI panels are open
        if ((actionMenuUI != null && actionMenuUI.IsVisible()) ||
            (_skillSelectionPanelInstance != null && _skillSelectionPanelInstance.IsVisible()) ||
            (_unitInfoPanelInstance != null && _unitInfoPanelInstance.IsVisible()))
        {
            if (_hoveredUnitOnTileForTooltip != null) 
            {
                // Debug.Log($"PIH.HandleTileHoverForTooltip: Major UI is up, hiding tooltip for {_hoveredUnitOnTileForTooltip.unitName}", this);
                if (_unitTooltipCoroutine != null)
                {
                    StopCoroutine(_unitTooltipCoroutine);
                    _unitTooltipCoroutine = null;
                }
                TooltipUI.Instance.HideTooltip();
                _hoveredUnitOnTileForTooltip = null;
                _lastHoveredTileWithUnit = null;
            }
            return; 
        }

        Tile hoveredTile = GetTileUnderMouse(false); // Get tile without UI check override for hover
        Unit unitOnHoveredTile = hoveredTile?.occupyingUnit;

        if (unitOnHoveredTile != null) // Mouse is over a tile with a unit
        {
            if (_hoveredUnitOnTileForTooltip != unitOnHoveredTile || _lastHoveredTileWithUnit != hoveredTile)
            {
                // Debug.Log($"PIH.HandleTileHoverForTooltip: New unit/tile hovered: {unitOnHoveredTile.unitName} on tile {hoveredTile.gridPosition}. Previous: {_hoveredUnitOnTileForTooltip?.unitName}", this);
                if (_hoveredUnitOnTileForTooltip != null) {
                     TooltipUI.Instance.HideTooltip(); 
                }
                if (_unitTooltipCoroutine != null)
                {
                    StopCoroutine(_unitTooltipCoroutine);
                }
                _hoveredUnitOnTileForTooltip = unitOnHoveredTile;
                _lastHoveredTileWithUnit = hoveredTile;
                if(gameObject.activeInHierarchy) 
                {
                    _unitTooltipCoroutine = StartCoroutine(ShowUnitTooltipAfterDelay(unitOnHoveredTile, Mouse.current.position.ReadValue()));
                }
            }
        }
        else // Mouse is over an empty tile or not over the grid
        {
            if (_hoveredUnitOnTileForTooltip != null) 
            {
                // Debug.Log($"PIH.HandleTileHoverForTooltip: Mouse no longer over a unit's tile. Hiding tooltip for {_hoveredUnitOnTileForTooltip.unitName}", this);
                if (_unitTooltipCoroutine != null)
                {
                    StopCoroutine(_unitTooltipCoroutine);
                    _unitTooltipCoroutine = null;
                }
                TooltipUI.Instance.HideTooltip();
                _hoveredUnitOnTileForTooltip = null;
                _lastHoveredTileWithUnit = null;
            }
        }
    }

    // GetUnitUnderMouse REMOVED as it's replaced by tile-based logic

    private IEnumerator ShowUnitTooltipAfterDelay(Unit unit, Vector2 screenPosition)
    {
        // Debug.Log($"PIH.ShowUnitTooltipAfterDelay: Coroutine started for {unit?.unitName}", this);
        yield return new WaitForSeconds(unitTooltipDelay);

        // Check if we are still hovering over the same unit and TooltipUI is available
        // Also check if the unit is still on that tile (in case it moved during the delay)
        Tile currentTileUnderMouse = GetTileUnderMouse(false);
        if (_hoveredUnitOnTileForTooltip == unit && 
            currentTileUnderMouse == _lastHoveredTileWithUnit && // Ensure still on same tile
            TooltipUI.Instance != null && unit != null && unit.Stats != null)
        {
            // Debug.Log($"PIH.ShowUnitTooltipAfterDelay: Delay complete, showing tooltip for {unit.unitName}", this);
            string tooltipText = $"{unit.unitName}\nVP: {unit.Stats.currentVitalityPoints} / {unit.Stats.MaxVitalityPoints}";
            if (unit.IsAlive)
            {
                 tooltipText += $"\nAP: {unit.CurrentActionPoints} / {unit.MaxActionPoints}";
            } else {
                 tooltipText += "\n(Defeated)";
            }
            TooltipUI.Instance.ShowTooltip(tooltipText, screenPosition);
        }
        else
        {
            // Debug.Log($"PIH.ShowUnitTooltipAfterDelay: Conditions not met (unit changed or mouse moved off tile) for {unit?.unitName}. Hovered: {_hoveredUnitOnTileForTooltip?.unitName}", this);
            if (TooltipUI.Instance != null && _hoveredUnitOnTileForTooltip == unit) // If this was the unit we started the coroutine for but conditions failed
            {
                TooltipUI.Instance.HideTooltip(); // Ensure it's hidden
            }
        }
        _unitTooltipCoroutine = null;
    }

    public void ChangeState(PlayerInputStateBase newState)
    {
        if (_unitTooltipCoroutine != null) { StopCoroutine(_unitTooltipCoroutine); _unitTooltipCoroutine = null; }
        if (TooltipUI.Instance != null) TooltipUI.Instance.HideTooltip();
        _hoveredUnitOnTileForTooltip = null;
        _lastHoveredTileWithUnit = null;

        _currentStateObject?.ExitState(); 
        _currentStateObject = newState;
        
        ClearAllHighlights(); 

        if (_currentStateObject is PIH_UnitActionPhaseState && 
            _selectedUnit != null && 
            _selectedUnit.CompareTag("Player") && 
            TurnManager.Instance != null && 
            TurnManager.Instance.ActiveUnit == _selectedUnit)
        {
            ShowActionMenuForSelectedUnitPublic();
        }
        else 
        {
            actionMenuUI?.HideMenu();
        }
        
        if (!(_currentStateObject is PIH_SelectingAbilityTargetState) && !(_currentStateObject is PIH_UnitActionPhaseState && _skillSelectionPanelInstance != null && _skillSelectionPanelInstance.IsVisible()))
        {
            _skillSelectionPanelInstance?.HidePanel();
        }

        if (!(_currentStateObject is PIH_ViewingUnitInfoState))
        {
            _unitInfoPanelInstance?.HidePanel();
        }
        
        _currentStateObject.EnterState(this); 
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
        SkillSelectionUI.OnSkillPanelClosedByButton += HandleSkillPanelClosedByButton; 
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
        SkillSelectionUI.OnSkillPanelClosedByButton -= HandleSkillPanelClosedByButton; 

        if (_playerControls.Gameplay.enabled) _playerControls.Gameplay.Disable();

        if (_unitTooltipCoroutine != null) 
        { 
            StopCoroutine(_unitTooltipCoroutine); 
            _unitTooltipCoroutine = null; 
        }
        if (TooltipUI.Instance != null) TooltipUI.Instance.HideTooltip(); 
        _hoveredUnitOnTileForTooltip = null;
        _lastHoveredTileWithUnit = null;
    }
    
    // RENAMED and MODIFIED to optionally bypass UI check for hover
    private Tile GetTileUnderMouse(bool blockIfOverUI = true)
    {
        if (_mainCamera == null || GridManager.Instance == null) return null;
        Vector2 screenPosition = _playerControls.Gameplay.Point.ReadValue<Vector2>();
        
        if (blockIfOverUI && UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            // Standard UI check for clicks, allow certain states to click through
            if (!(_currentStateObject is PIH_SelectingMoveTargetState || 
                  _currentStateObject is PIH_SelectingAttackTargetState ||
                  _currentStateObject is PIH_SelectingAbilityTargetState))
            {
                return null; 
            }
        }
        // For pure hover detection (blockIfOverUI = false), we still want to check IsPointerOverGameObject
        // to prevent world tooltips if *any* UI is under the mouse.
        else if (!blockIfOverUI && UnityEngine.EventSystems.EventSystem.current != null &&
                 UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return null; // Don't show world tooltip if mouse is over any UI
        }


        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
        Plane groundPlane = new Plane(Vector3.forward, Vector3.zero); 

        if (!groundPlane.Raycast(ray, out float distance)) 
        {
            return null;
        }
        
        Vector3 worldPoint = ray.GetPoint(distance);
        Vector2Int gridPos = GridManager.Instance.WorldToGrid(worldPoint);
        return GridManager.Instance.GetTile(gridPos);
    }

    private void HandleInfoPanelClosedByButton()
    {
        if (_currentStateObject is PIH_ViewingUnitInfoState)
        {
            ChangeState(new PIH_UnitActionPhaseState());
        }
    }
    
    private void HandleSkillPanelClosedByButton()
    {
        _skillSelectionPanelInstance?.HidePanel(); 
        ChangeState(new PIH_UnitActionPhaseState());
    }

    // MODIFIED: Calls GetTileUnderMouse(true) for click context
    private void OnClickPerformedHandler(InputAction.CallbackContext context) { _currentStateObject?.OnClickInput(context, GetTileUnderMouse(true)); } 
    private void OnToggleAttackModeInputHandler(InputAction.CallbackContext context) { _currentStateObject?.OnToggleAttackModeInput(context); }
    private void OnSelectAbilityInputHandler(InputAction.CallbackContext context) { _currentStateObject?.OnSelectAbilityInput(context); }
    private void OnEndTurnInputHandler(InputAction.CallbackContext context) { _currentStateObject?.OnEndTurnInput(context); }
    
    private void OnToggleActionMenuPerformedHandler(InputAction.CallbackContext context)
    {
        // ... (This method remains largely the same as your last working version) ...
        bool stateHandledToggle = false;
        if (_currentStateObject != null)
        {
            var stateBeforeToggle = _currentStateObject.GetType();
            _currentStateObject.OnToggleActionMenuInput(context);
            if (_currentStateObject.GetType() != stateBeforeToggle) 
            {
                stateHandledToggle = true;
            }
        }

        if (stateHandledToggle) return; 

        if (_selectedUnit == null || !CombatActive || !_selectedUnit.CompareTag("Player") || TurnManager.Instance.ActiveUnit != _selectedUnit)
        {
            actionMenuUI?.HideMenu(); 
            _skillSelectionPanelInstance?.HidePanel();
            _unitInfoPanelInstance?.HidePanel();
            return;
        }

        if (_unitInfoPanelInstance != null && _unitInfoPanelInstance.IsVisible())
        {
            _unitInfoPanelInstance.HidePanel();
            if (!(_currentStateObject is PIH_UnitActionPhaseState)) ChangeState(new PIH_UnitActionPhaseState());
            else ShowActionMenuForSelectedUnitPublic(); 
            return;
        }

        if (_skillSelectionPanelInstance != null && _skillSelectionPanelInstance.IsVisible())
        {
            _skillSelectionPanelInstance.HidePanel(); 
            ChangeState(new PIH_UnitActionPhaseState()); 
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
        // ... (remains the same) ...
        if (_selectedUnit != null && actionMenuUI != null && _mainCamera != null && 
            TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _selectedUnit) 
        {
            _skillSelectionPanelInstance?.HidePanel(); 
            _unitInfoPanelInstance?.HidePanel(); 
            
            Vector2 screenPos = _mainCamera.WorldToScreenPoint(_selectedUnit.transform.position);
            actionMenuUI.ShowMenu(_selectedUnit, screenPos); 
            
            if (_selectedUnit.Movement.CurrentTile != null)
            { 
                _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
            }
        }
    }
    
    public void HandleActionFromHotKey(string actionName)
    {
        // ... (remains the same) ...
        if (_selectedUnit == null) return;
        actionMenuUI?.HideMenu(); 
        _skillSelectionPanelInstance?.HidePanel();
        _unitInfoPanelInstance?.HidePanel(); 
        ProcessActionSelection(actionName); 
    }
    
    private void HandleActionMenuSelection(Unit unit, string actionName) 
    {
        // ... (remains the same) ...
        if (unit != _selectedUnit || _selectedUnit == null) return; 
        _skillSelectionPanelInstance?.HidePanel(); 
        _unitInfoPanelInstance?.HidePanel(); 
        ProcessActionSelection(actionName);
    }

    private void ProcessActionSelection(string actionName)
    {
        // ... (remains the same) ...
        bool actionPotentiallyEndsTurnOrRequiresApCheck = false;
        switch (actionName)
        {
            case "Move":
                if (_selectedUnit.CanAffordAPForAction(MoveActionCost))
                { ChangeState(new PIH_SelectingMoveTargetState()); } 
                else { ChangeState(new PIH_UnitActionPhaseState()); }
                break;
            case "Attack":
                if (_selectedUnit.CanAffordAPForAction(AttackActionCost)) 
                { ShowAttackRange(_selectedUnit); ChangeState(new PIH_SelectingAttackTargetState()); } 
                else { ChangeState(new PIH_UnitActionPhaseState()); }
                break;
            case "Wait":
                if (_selectedUnit.CanAffordAPForAction(WaitActionCost)) { _selectedUnit.SpendAPForAction(WaitActionCost); actionPotentiallyEndsTurnOrRequiresApCheck = true; ChangeState(new PIH_UnitActionPhaseState()); }
                else { ChangeState(new PIH_UnitActionPhaseState()); }
                break;
            case "Skills":
                if (_selectedUnit.CanAffordAPForAction(SkillsActionCost)) { ShowSkillSelectionPanel(); } 
                else { ChangeState(new PIH_UnitActionPhaseState()); }
                break;
            case "Info": 
                if (_unitInfoPanelInstance != null) { ChangeState(new PIH_ViewingUnitInfoState()); }
                else { ChangeState(new PIH_UnitActionPhaseState());}
                break;
            default: 
                ChangeState(new PIH_UnitActionPhaseState());
                break;
        }
        if (actionPotentiallyEndsTurnOrRequiresApCheck) { CheckAndHandleEndOfTurnActionsPIH(); }
    }

    private void ShowSkillSelectionPanel()
    {
        // ... (remains the same) ...
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
    }

    private void HandleSkillSelectedFromPanel(Unit caster, AbilitySO selectedAbility)
    {
        // ... (remains the same) ...
        if (caster != _selectedUnit || selectedAbility == null) return;
        if (!_selectedUnit.Combat.CanAffordAbility(selectedAbility, true)) 
        { 
            _skillSelectionPanelInstance?.HidePanel(); 
            ChangeState(new PIH_UnitActionPhaseState()); 
            return; 
        }
        SelectedAbility = selectedAbility; 
        ChangeState(new PIH_SelectingAbilityTargetState()); 
    }
    
    public void ClearAllHighlights()
    {
        // ... (remains the same) ...
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
        // ... (remains the same) ...
        if (_pathfinder == null || requestingUnit?.IsAlive == false || requestingUnit.Movement?.CurrentTile == null) return;
        ClearReachableHighlight(true); 
        _highlightedReachableTiles.AddRange(_pathfinder.GetReachableTiles(requestingUnit.Movement.CurrentTile.gridPosition, range, requestingUnit));
        foreach (Tile tile in _highlightedReachableTiles) { if (tile != null && tile != requestingUnit.Movement.CurrentTile) tile.SetHighlight(TileHighlightState.MovementRange); }
        if (requestingUnit.Movement.CurrentTile != null) requestingUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
    }

    public void ShowPathHighlight(List<Tile> path)
    {
        // ... (remains the same) ...
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
        // ... (remains the same) ...
        if (attacker?.IsAlive == false || attacker.Movement?.CurrentTile == null || !attacker.CanAffordAPForAction(AttackActionCost) || GridManager.Instance == null) return;
        ClearAttackRangeHighlight(true); 
        _highlightedAttackRangeTiles.AddRange(GridManager.Instance.GetTilesInRange(attacker.Movement.CurrentTile.gridPosition, attacker.CalculatedAttackRange));
        foreach (Tile tile in _highlightedAttackRangeTiles) { if (tile != null && tile != attacker.Movement.CurrentTile) tile.SetHighlight(TileHighlightState.AttackRange); }
        if (attacker.Movement.CurrentTile != null) attacker.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
    }
    public void ShowAbilityRange(Unit caster, AbilitySO ability) { 
        // ... (remains the same) ...
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
        // ... (remains the same) ...
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