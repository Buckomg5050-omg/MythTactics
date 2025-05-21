// PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using MythTactics.Combat; 
using UnityEngine.EventSystems; 
using System.Collections; 

public class PlayerInputHandler : MonoBehaviour
{
    [Header("UI References")]
    public ActionMenuUI actionMenuUI;
    public GameObject skillSelectionPanelPrefab; 
    public GameObject unitInfoPanelPrefab;
    public GameObject itemSelectionPanelPrefab;

    [Header("Tooltip Settings")]
    public float unitTooltipDelay = 0.5f;

    private SkillSelectionUI _skillSelectionPanelInstance; 
    private UnitInfoPanelUI _unitInfoPanelInstance;
    private ItemSelectionPanelUI _itemSelectionPanelInstance;

    [Header("Action Costs")]
    public static int MoveActionCost = 1;
    public static int WaitActionCost = 0; 
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

    private Unit _hoveredUnitOnTileForTooltip = null;
    private Coroutine _unitTooltipCoroutine = null;
    
    private bool _isPointerOverUIFromUpdate; 
    private bool _actionBarNeedsRefresh = false;

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
        if (skillSelectionPanelPrefab == null) { DebugHelper.LogError("PIH: SkillSelectionPanelPrefab not assigned in Inspector! Skills/Spells will not function.", this); enabled = false; return; }
        if (unitInfoPanelPrefab == null) { DebugHelper.LogWarning("PIH: UnitInfoPanelPrefab not assigned in Inspector. Info panel functionality will be limited.", this); }
        if (itemSelectionPanelPrefab == null) { DebugHelper.LogWarning("PIH: ItemSelectionPanelPrefab not assigned in Inspector. Items functionality will be limited.", this); }


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
        actionMenuUI?.HideActionBar(); 
        ChangeState(new PIH_WaitingForTurnState());
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            _isPointerOverUIFromUpdate = UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            // REMOVED: Per-frame log for IsPointerOverUI during targeting states
            // if (_isPointerOverUIFromUpdate && 
            //     (_currentStateObject is PIH_SelectingMoveTargetState || 
            //      _currentStateObject is PIH_SelectingAttackTargetState || 
            //      _currentStateObject is PIH_SelectingAbilityTargetState) )
            // { // Detailed logging of hit UI objects was here, now only in GetTileUnderMouse for actual clicks }
        }
        else
        {
            _isPointerOverUIFromUpdate = false;
        }

        if (TurnManager.Instance == null) { if (!(_currentStateObject is PIH_WaitingForTurnState)) ChangeState(new PIH_WaitingForTurnState()); return; }
        
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

        if (currentTurnManagerActiveUnit != null && currentTurnManagerActiveUnit.IsAlive && currentTurnManagerActiveUnit.CompareTag("Player") && currentTurnManagerActiveUnit.Movement != null)
        {
            if (_selectedUnit != currentTurnManagerActiveUnit) 
            {
                _selectedUnit = currentTurnManagerActiveUnit;
                ChangeState(new PIH_UnitActionPhaseState()); 
            }
            else if (!(_currentStateObject is PIH_UnitActionPhaseState ||
                       _currentStateObject is PIH_SelectingAttackTargetState ||
                       _currentStateObject is PIH_SelectingMoveTargetState ||
                       _currentStateObject is PIH_UnitMovingState ||
                       _currentStateObject is PIH_SelectingAbilityTargetState ||
                       _currentStateObject is PIH_ViewingUnitInfoState ))
            { 
                ChangeState(new PIH_UnitActionPhaseState()); 
            }

            if (_actionBarNeedsRefresh && _currentStateObject is PIH_UnitActionPhaseState && actionMenuUI != null && _selectedUnit != null)
            {
                if(!actionMenuUI.gameObject.activeSelf) actionMenuUI.gameObject.SetActive(true);
                actionMenuUI.RefreshActionBar(_selectedUnit);
                _actionBarNeedsRefresh = false;
            }
        }
        else 
        { 
            if (_selectedUnit != null || !(_currentStateObject is PIH_WaitingForTurnState)) 
            { 
                _selectedUnit = null; 
                SelectedAbility = null; 
                ChangeState(new PIH_WaitingForTurnState()); 
            }
        }

        _currentStateObject?.UpdateState();
        HandleTileHoverForTooltip();
    }
    
    private IEnumerator ShowUnitTooltipAfterDelay(Unit unit, Vector2 screenPosition)
    {
        yield return new WaitForSeconds(unitTooltipDelay);
        
        bool stillHoveringSameUnit = (_hoveredUnitOnTileForTooltip == unit);
        bool currentPointerOverUI = (EventSystem.current != null) && EventSystem.current.IsPointerOverGameObject();

        bool conditionsMet = stillHoveringSameUnit && TooltipUI.Instance != null && 
                             unit != null && unit.Stats != null && !currentPointerOverUI; 

        if (conditionsMet)
        {
            string tooltipText = $"{unit.unitName}\nVP: {unit.Stats.currentVitalityPoints} / {unit.Stats.MaxVitalityPoints}";
            if (unit.IsAlive) { tooltipText += $"\nAP: {unit.CurrentActionPoints} / {unit.MaxActionPoints}"; } 
            else { tooltipText += "\n(Defeated)"; }
            TooltipUI.Instance.ShowTooltip(tooltipText, screenPosition);
        }
        else
        {
            if (TooltipUI.Instance != null && _hoveredUnitOnTileForTooltip == unit) { TooltipUI.Instance.HideTooltip(); }
        }
        _unitTooltipCoroutine = null;
    }
    
    private void HandleTileHoverForTooltip() 
    { 
        if (TooltipUI.Instance == null) return;

        bool anyMajorPanelOpen = (_skillSelectionPanelInstance != null && _skillSelectionPanelInstance.IsVisible()) ||
                                 (_itemSelectionPanelInstance != null && _itemSelectionPanelInstance.IsVisible()) ||
                                 (_unitInfoPanelInstance != null && _unitInfoPanelInstance.IsVisible()) ||
                                 (actionMenuUI != null && actionMenuUI.IsSubMenuOpen()); 

        if (anyMajorPanelOpen || _isPointerOverUIFromUpdate) 
        {
            if (_hoveredUnitOnTileForTooltip != null) 
            {
                if (_unitTooltipCoroutine != null) StopCoroutine(_unitTooltipCoroutine);
                _unitTooltipCoroutine = null;
                TooltipUI.Instance.HideTooltip();
                _hoveredUnitOnTileForTooltip = null;
            }
            return;
        }
        
        Tile hoveredTileDirectly = GetTileUnderMouse(false, false); 
        Unit unitOnHoveredTile = hoveredTileDirectly?.occupyingUnit;

        if (unitOnHoveredTile != null)
        {
            if (_hoveredUnitOnTileForTooltip != unitOnHoveredTile) 
            {
                if (_hoveredUnitOnTileForTooltip != null) TooltipUI.Instance.HideTooltip(); 
                if (_unitTooltipCoroutine != null) StopCoroutine(_unitTooltipCoroutine);
                
                _hoveredUnitOnTileForTooltip = unitOnHoveredTile;
                if (gameObject.activeInHierarchy) 
                {
                     _unitTooltipCoroutine = StartCoroutine(ShowUnitTooltipAfterDelay(unitOnHoveredTile, Mouse.current.position.ReadValue()));
                }
            }
        }
        else 
        {
            if (_hoveredUnitOnTileForTooltip != null) 
            {
                if (_unitTooltipCoroutine != null) StopCoroutine(_unitTooltipCoroutine);
                _unitTooltipCoroutine = null;
                TooltipUI.Instance.HideTooltip();
                _hoveredUnitOnTileForTooltip = null;
            }
        }
    }

    public void ChangeState(PlayerInputStateBase newState)
    {
        if (_unitTooltipCoroutine != null) { StopCoroutine(_unitTooltipCoroutine); _unitTooltipCoroutine = null; }
        if (TooltipUI.Instance != null) TooltipUI.Instance.HideTooltip();
        _hoveredUnitOnTileForTooltip = null;

        _currentStateObject?.ExitState(); 
        _currentStateObject = newState;
        
        ClearAllHighlights(); 

        if (_currentStateObject is PIH_UnitActionPhaseState && 
            _selectedUnit != null && 
            _selectedUnit.CompareTag("Player") && 
            TurnManager.Instance != null && 
            TurnManager.Instance.ActiveUnit == _selectedUnit)
        {
            if(actionMenuUI != null && !actionMenuUI.gameObject.activeSelf) actionMenuUI.gameObject.SetActive(true);
            _actionBarNeedsRefresh = true; 
        }
        else 
        {
            actionMenuUI?.HideActionBar(); 
            _actionBarNeedsRefresh = false;
        }
        
        if (!(_currentStateObject is PIH_UnitActionPhaseState && _itemSelectionPanelInstance != null && _itemSelectionPanelInstance.IsVisible()))
        {
            _itemSelectionPanelInstance?.HidePanel();
        }

        bool isViewingSkillsOrSpells = (_currentStateObject is PIH_UnitActionPhaseState && 
                                        _skillSelectionPanelInstance != null && 
                                        _skillSelectionPanelInstance.IsVisible());
        bool isSelectingAbilityTarget = (_currentStateObject is PIH_SelectingAbilityTargetState);

        if (!isViewingSkillsOrSpells && !isSelectingAbilityTarget)
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
        ItemSelectionPanelUI.OnItemSelectedForUse += HandleItemSelectedFromPanel;
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
        ItemSelectionPanelUI.OnItemSelectedForUse -= HandleItemSelectedFromPanel;

        if (_playerControls.Gameplay.enabled) _playerControls.Gameplay.Disable();

        if (_unitTooltipCoroutine != null) { StopCoroutine(_unitTooltipCoroutine); _unitTooltipCoroutine = null; }
        if (TooltipUI.Instance != null) TooltipUI.Instance.HideTooltip(); 
        _hoveredUnitOnTileForTooltip = null;
    }
    
    private Tile GetTileUnderMouse(bool blockIfOverUI = true, bool forceUICheckNow = false)
    {
        if (_mainCamera == null || GridManager.Instance == null)
        {
            // Debug.LogWarning("PIH.GetTileUnderMouse: MainCamera or GridManager is null.", this.gameObject); // Can be noisy
            return null;
        }
        Vector2 screenPosition = _playerControls.Gameplay.Point.ReadValue<Vector2>();
        
        bool pointerIsCurrentlyOverUI;
        if (forceUICheckNow)
        {
            pointerIsCurrentlyOverUI = (EventSystem.current != null) && EventSystem.current.IsPointerOverGameObject();
        }
        else
        {
            pointerIsCurrentlyOverUI = _isPointerOverUIFromUpdate; 
        }

        if (blockIfOverUI && pointerIsCurrentlyOverUI)
        {
            // This log is useful for debugging UI blocking issues, but can be noisy during normal play.
            // Keep it commented unless actively debugging this specific problem.
            /*
            if (forceUICheckNow) 
            {
                PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
                eventDataCurrentPosition.position = screenPosition; 
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventDataCurrentPosition, results);
                string hitObjects = results.Count > 0 ? "" : "None (IsPointerOverGameObject was true but RaycastAll returned no results - check Canvas GraphicRaycaster settings OR a non-UI object with a physics raycaster is blocking)";
                foreach(var result in results) { hitObjects += result.gameObject.name + " (layer " + LayerMask.LayerToName(result.gameObject.layer) + "); "; }
                Debug.Log($"PIH.GetTileUnderMouse: Returning NULL because pointer is over UI. IsPointerOverGameObject = {pointerIsCurrentlyOverUI}. Hit UI Objects: [{hitObjects}]", this.gameObject);
            }
            */
            return null; 
        }

        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
        Plane groundPlane = new Plane(Vector3.forward, Vector3.zero); 

        if (!groundPlane.Raycast(ray, out float distance)) 
        {
            // This log can also be noisy if the mouse often leaves the 'hittable' area.
            // if (forceUICheckNow) { // Only log on actual click attempts if raycast fails
            //    Debug.LogWarning($"PIH.GetTileUnderMouse: Ground plane raycast FAILED. Ray origin: {ray.origin}, Ray direction: {ray.direction}. Plane normal: {groundPlane.normal}, Plane point: {groundPlane.distance}", this.gameObject);
            // }
            return null;
        }
        
        Vector3 worldPoint = ray.GetPoint(distance);
        Vector2Int gridPos = GridManager.Instance.WorldToGrid(worldPoint); 
        Tile foundTile = GridManager.Instance.GetTile(gridPos);
        
        return foundTile;
    }

    private void HandleInfoPanelClosedByButton() 
    { 
        _unitInfoPanelInstance?.HidePanel();
        ChangeState(new PIH_UnitActionPhaseState()); 
    }
    
    private void HandleSkillPanelClosedByButton() 
    {
        _skillSelectionPanelInstance?.HidePanel(); 
        ChangeState(new PIH_UnitActionPhaseState()); 
    }
    private void HandleItemPanelClosedByButton() 
    {
        _itemSelectionPanelInstance?.HidePanel();
        ChangeState(new PIH_UnitActionPhaseState()); 
    }
    
    private void OnClickPerformedHandler(InputAction.CallbackContext context) 
    { 
        _currentStateObject?.OnClickInput(context, GetTileUnderMouse(true, true)); 
    } 
    private void OnToggleAttackModeInputHandler(InputAction.CallbackContext context) 
    { 
        // Debug.Log("PlayerInputHandler: OnToggleAttackModeInputHandler (hotkey) triggered", this.gameObject); // Keep if hotkeys are actively used
        _currentStateObject?.OnToggleAttackModeInput(context); 
    }
    private void OnSelectAbilityInputHandler(InputAction.CallbackContext context) 
    { 
        // Debug.Log("PlayerInputHandler: OnSelectAbilityInputHandler (hotkey) triggered", this.gameObject); // Keep if hotkeys are actively used
        _currentStateObject?.OnSelectAbilityInput(context); 
    }
    private void OnEndTurnInputHandler(InputAction.CallbackContext context) 
    { 
        // Debug.Log("PlayerInputHandler: OnEndTurnInputHandler (hotkey for End Turn / Wait) triggered", this.gameObject); // Keep if hotkeys are actively used
        _currentStateObject?.OnEndTurnInput(context); 
    }
    
    private void OnToggleActionMenuPerformedHandler(InputAction.CallbackContext context)
    {
        bool panelWasClosed = false;
        if (_skillSelectionPanelInstance != null && _skillSelectionPanelInstance.IsVisible())
        {
            _skillSelectionPanelInstance.HidePanel();
            panelWasClosed = true;
        }
        else if (_itemSelectionPanelInstance != null && _itemSelectionPanelInstance.IsVisible())
        {
            _itemSelectionPanelInstance.HidePanel();
            panelWasClosed = true;
        }
        else if (_unitInfoPanelInstance != null && _unitInfoPanelInstance.IsVisible())
        {
            _unitInfoPanelInstance.HidePanel();
            panelWasClosed = true;
        }
        
        if (panelWasClosed)
        {
            if (_currentStateObject is PIH_UnitActionPhaseState && _selectedUnit != null)
            {
                 _actionBarNeedsRefresh = true; 
            }
            return; 
        }
        
        if (actionMenuUI != null && actionMenuUI.IsVisible() && actionMenuUI.IsSubMenuOpen())
        {
            actionMenuUI.HideSubMenu(); 
            return;
        }
        // DebugHelper.Log("PIH: ToggleActionMenu input received, no specific sub-panel to close or sub-menu on bar. Action bar visibility managed by turn/state.", this.gameObject);
    }
            
    public void HandleActionFromHotKey(string actionName) 
    {
        // Debug.Log($"PlayerInputHandler: HandleActionFromHotKey for action: {actionName}", this.gameObject); // Keep if useful
        if (_selectedUnit == null) return;
        
        _skillSelectionPanelInstance?.HidePanel();
        _unitInfoPanelInstance?.HidePanel(); 
        _itemSelectionPanelInstance?.HidePanel();
        actionMenuUI?.HideSubMenu(); 

        ProcessActionSelection(actionName); 
    }
    
    private void HandleActionMenuSelection(Unit unit, string actionName) 
    {
        // Debug.Log($"PlayerInputHandler: HandleActionMenuSelection received. Unit: {unit?.unitName ?? "NULL"}, Action: {actionName}", this.gameObject); // Keep this
        if (unit != _selectedUnit || _selectedUnit == null) 
        {
            Debug.LogWarning($"PlayerInputHandler: HandleActionMenuSelection - Mismatched unit or null selected unit. Event unit: {unit?.unitName}, Selected unit: {_selectedUnit?.unitName}. Ignoring.", this.gameObject);
            return; 
        }
        
        if (actionName != ActionMenuUI.SHOW_SPELLS_ACTION_NAME && actionName != "Skills")
        {
            _skillSelectionPanelInstance?.HidePanel();
        }
        if (actionName != "Info")
        {
            _unitInfoPanelInstance?.HidePanel();
        }
        if (actionName != "Items")
        {
            _itemSelectionPanelInstance?.HidePanel();
        }
        
        ProcessActionSelection(actionName);
    }

    private void ProcessActionSelection(string actionName)
    {
        // Debug.Log($"PlayerInputHandler: ProcessActionSelection for action: {actionName}", this.gameObject); // Keep this

        bool actionOpenedPanel = false;
        if (actionName != "Wait" && actionName != "Attack") 
        {
             actionMenuUI?.HideSubMenu(); 
        }

        switch (actionName)
        {
            case "Move":
                if (_selectedUnit.CanAffordAPForAction(MoveActionCost))
                { ChangeState(new PIH_SelectingMoveTargetState()); } 
                else { _actionBarNeedsRefresh = true; } 
                break;
            case ActionMenuUI.BASIC_ATTACK_ACTION_NAME: 
                if (_selectedUnit.CanAffordAPForAction(AttackActionCost)) 
                { ShowAttackRange(_selectedUnit); ChangeState(new PIH_SelectingAttackTargetState()); } 
                else { _actionBarNeedsRefresh = true; }
                break;
            case "Wait": 
                if (_selectedUnit != null && TurnManager.Instance != null)
                {
                    TurnManager.Instance.EndUnitTurn(_selectedUnit);
                }
                break;
            case "Skills":
                ShowSkillSelectionPanel(false); 
                actionOpenedPanel = true;
                break;
            case ActionMenuUI.SHOW_SPELLS_ACTION_NAME: 
                ShowSkillSelectionPanel(true); 
                actionOpenedPanel = true;
                break;
            case "Items": 
                ShowItemSelectionPanel(); 
                actionOpenedPanel = true;
                break;
            case "Info": 
                if (_unitInfoPanelInstance != null) 
                {
                    ChangeState(new PIH_ViewingUnitInfoState()); 
                    actionOpenedPanel = true;
                }
                break;
            case ActionMenuUI.FLEE_ACTION_NAME: 
                DebugHelper.Log($"PIH: Player selected Flee for unit {_selectedUnit.unitName}.", this.gameObject);
                if (_selectedUnit != null && TurnManager.Instance != null)
                {
                    DebugHelper.Log($"PIH: {_selectedUnit.unitName} attempts to flee. Ending combat (Placeholder).", this.gameObject);
                    CombatLogger.LogEvent($"{_selectedUnit.unitName} flees the battle!", Color.magenta, LogMessageType.System);
                    TurnManager.Instance.ForceEndCombat(); 
                }
                break;
            default: 
                _actionBarNeedsRefresh = true; 
                break;
        }

        if (!actionOpenedPanel && (actionName == ActionMenuUI.BASIC_ATTACK_ACTION_NAME || actionName == "Move"))
        {
             _actionBarNeedsRefresh = true;
             CheckAndHandleEndOfTurnActionsPIH();
        } 
        else if (!actionOpenedPanel && actionName != "Wait" && actionName != ActionMenuUI.FLEE_ACTION_NAME)
        {
            _actionBarNeedsRefresh = true;
        }
    }

    private void ShowSkillSelectionPanel(bool forSpells) 
    {
        if (_selectedUnit == null || skillSelectionPanelPrefab == null || _selectedUnit.knownAbilities == null) return;
        
        if (_skillSelectionPanelInstance == null)
        {
            Canvas mainCanvas = FindFirstObjectByType<Canvas>(); 
            if (mainCanvas == null) { Debug.LogError("PIH: No Canvas found for SkillSelectionPanel!"); return; }
            GameObject panelGO = Instantiate(skillSelectionPanelPrefab, mainCanvas.transform);
            _skillSelectionPanelInstance = panelGO.GetComponent<SkillSelectionUI>();
            if (_skillSelectionPanelInstance == null) { Debug.LogError("PIH: SkillSelectionPanelPrefab missing SkillSelectionUI!", panelGO); Destroy(panelGO); return; }
        }

        List<AbilitySO> abilitiesToShow;
        string panelTitle;

        if (forSpells)
        {
            abilitiesToShow = _selectedUnit.knownAbilities.Where(ab => ab != null && ab.abilityType == AbilityType.Spell).ToList();
            panelTitle = "Select Spell";
        }
        else 
        {
            abilitiesToShow = _selectedUnit.knownAbilities.Where(ab => ab != null && (ab.abilityType == AbilityType.Skill || ab.abilityType == AbilityType.BasicAttack)).ToList();
            panelTitle = "Select Skill";
        }
        
        Vector2 panelPosition = (Vector2)_mainCamera.WorldToScreenPoint(_selectedUnit.transform.position) + new Vector2(150, 0); 
        _skillSelectionPanelInstance.ShowPanel(_selectedUnit, panelPosition, abilitiesToShow, panelTitle); 
        
        actionMenuUI?.HideActionBar(); 
        _unitInfoPanelInstance?.HidePanel(); 
        _itemSelectionPanelInstance?.HidePanel(); 
    }

    private void ShowItemSelectionPanel() 
    {
        if (_selectedUnit == null || itemSelectionPanelPrefab == null) return;
        
        if (_itemSelectionPanelInstance == null)
        {
            Canvas mainCanvas = FindFirstObjectByType<Canvas>(); 
            if (mainCanvas == null) { Debug.LogError("PIH: No Canvas found to instantiate ItemSelectionPanel!", this); return; }
            GameObject panelGO = Instantiate(itemSelectionPanelPrefab, mainCanvas.transform);
            _itemSelectionPanelInstance = panelGO.GetComponent<ItemSelectionPanelUI>();
            if (_itemSelectionPanelInstance == null)
            {
                Debug.LogError("PIH: ItemSelectionPanelPrefab is missing the ItemSelectionPanelUI script!", panelGO);
                Destroy(panelGO); return;
            }
        }
        _itemSelectionPanelInstance.ShowPanel(_selectedUnit); 
        
        actionMenuUI?.HideActionBar(); 
        _unitInfoPanelInstance?.HidePanel();
        _skillSelectionPanelInstance?.HidePanel();
    }

    private void HandleSkillSelectedFromPanel(Unit caster, AbilitySO selectedAbility) 
    {
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
    
    private void HandleItemSelectedFromPanel(Unit user, ItemSO selectedItem) 
    {
        if (user != _selectedUnit || selectedItem == null) return;

        if (!user.CanAffordAPForAction(selectedItem.apCostToUse))
        {
            _itemSelectionPanelInstance?.HidePanel(); 
            ChangeState(new PIH_UnitActionPhaseState()); 
            return;
        }

        user.SpendAPForAction(selectedItem.apCostToUse);
        CombatLogger.LogEvent($"{user.unitName} used {selectedItem.itemName}.", Color.yellow, LogMessageType.CombatAction); 

        if (selectedItem.effectToApplyOnUse != null)
        {
            if (EffectSystem.Instance != null)
            {
                EffectSystem.Instance.ApplyEffect(user, selectedItem.effectToApplyOnUse, user);
            }
            else
            {
                Debug.LogError("PIH: EffectSystem.Instance is null! Cannot apply item effect.", this);
            }
        }
        
        if (selectedItem.consumeOnUse)
        {
            if (user.inventory != null) 
            {
                bool removed = user.inventory.Remove(selectedItem); 
                if (removed)
                {
                    Debug.Log($"PIH: Consumed and removed '{selectedItem.itemName}' from {user.unitName}'s inventory.", this);
                }
                else
                {
                    Debug.LogWarning($"PIH: Tried to consume '{selectedItem.itemName}' but it was not found in {user.unitName}'s inventory for removal.", this);
                }
            }
        }
        
        _itemSelectionPanelInstance?.HidePanel();
        _actionBarNeedsRefresh = true; 
        ChangeState(new PIH_UnitActionPhaseState()); 
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
            bool maintainHighlightAsSelected = (_currentStateObject is PIH_UnitActionPhaseState || 
                                                _currentStateObject is PIH_ViewingUnitInfoState) && isSelectedUnitsTurn;
            bool maintainHighlightAsActive = (_currentStateObject is PIH_SelectingMoveTargetState || 
                                             _currentStateObject is PIH_SelectingAttackTargetState ||
                                             _currentStateObject is PIH_SelectingAbilityTargetState) && isSelectedUnitsTurn;

            if (maintainHighlightAsSelected && _selectedUnit.Movement.CurrentTile.CurrentHighlightState != TileHighlightState.SelectedUnit)
            {
                _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }
            else if (maintainHighlightAsActive && _selectedUnit.Movement.CurrentTile.CurrentHighlightState != TileHighlightState.ActiveTurnUnit && _selectedUnit.Movement.CurrentTile.CurrentHighlightState != TileHighlightState.SelectedUnit)
            {
                _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }
        }
    }

    public void ShowReachableRange(Vector2Int startPos, int range, Unit requestingUnit) 
    {
        if (_pathfinder == null || requestingUnit?.IsAlive == false || requestingUnit.Movement?.CurrentTile == null)
        {
            // Debug.LogWarning($"PIH.ShowReachableRange: Aborting. Unit: {requestingUnit?.unitName}", this.gameObject);
            return;
        }
        ClearReachableHighlight(true); 
        
        List<Tile> reachable = _pathfinder.GetReachableTiles(requestingUnit.Movement.CurrentTile.gridPosition, range, requestingUnit);
        // Debug.Log($"PIH.ShowReachableRange: Pathfinder returned {reachable.Count} reachable tiles for {requestingUnit.unitName} from {startPos} with range {range}.", this.gameObject); // Keep this one if needed

        _highlightedReachableTiles.AddRange(reachable);
        // int highlightedCount = 0; 
        foreach (Tile tile in _highlightedReachableTiles) 
        { 
            if (tile != null && tile != requestingUnit.Movement.CurrentTile) 
            {
                tile.SetHighlight(TileHighlightState.MovementRange); 
                // highlightedCount++; 
            }
        }
        // Debug.Log($"PIH.ShowReachableRange: Attempted to highlight {highlightedCount} tiles as MovementRange.", this.gameObject); // Commented out

        if (requestingUnit.Movement.CurrentTile != null) 
        {
            requestingUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
        }
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
    public void ShowAttackRange(Unit attacker) 
    { 
        if (attacker?.IsAlive == false || attacker.Movement?.CurrentTile == null || !attacker.CanAffordAPForAction(AttackActionCost) || GridManager.Instance == null) return;
        ClearAttackRangeHighlight(true); 
        _highlightedAttackRangeTiles.AddRange(GridManager.Instance.GetTilesInRange(attacker.Movement.CurrentTile.gridPosition, attacker.CalculatedAttackRange));
        foreach (Tile tile in _highlightedAttackRangeTiles) { if (tile != null && tile != attacker.Movement.CurrentTile) tile.SetHighlight(TileHighlightState.AttackRange); }
        if (attacker.Movement.CurrentTile != null) attacker.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
    }
    public void ShowAbilityRange(Unit caster, AbilitySO ability) 
    { 
        if (caster?.IsAlive == false || caster.Movement?.CurrentTile == null || ability == null || caster.Combat == null || !caster.Combat.CanAffordAbility(ability) || GridManager.Instance == null) return;
        ClearAbilityRangeHighlight(true); 
        _highlightedAbilityRangeTiles.AddRange(GridManager.Instance.GetTilesInRange(caster.Movement.CurrentTile.gridPosition, ability.range));
        foreach (Tile tile in _highlightedAbilityRangeTiles) { if (tile != null && tile != caster.Movement.CurrentTile) tile.SetHighlight(TileHighlightState.AbilityRange); }
        if (caster.Movement.CurrentTile != null) caster.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
    }

    public void ClearReachableHighlight(bool clearList) { foreach (Tile tile in _highlightedReachableTiles) { if (tile != null && tile.CurrentHighlightState == TileHighlightState.MovementRange ) tile.SetHighlight(TileHighlightState.None); } if (clearList) _highlightedReachableTiles.Clear(); }
    public void ClearPathHighlight() 
    { 
        foreach (Tile tile in _highlightedPathTiles) 
        { 
            if (tile != null && tile.CurrentHighlightState == TileHighlightState.Path) 
            {
                if (_highlightedReachableTiles.Contains(tile) && tile != _selectedUnit?.Movement?.CurrentTile)
                {
                    tile.SetHighlight(TileHighlightState.MovementRange);
                }
                else if (tile == _selectedUnit?.Movement?.CurrentTile && _selectedUnit != null && _selectedUnit.IsAlive)
                {
                    if (_currentStateObject is PIH_SelectingMoveTargetState || _currentStateObject is PIH_UnitActionPhaseState)
                    {
                        tile.SetHighlight(TileHighlightState.SelectedUnit);
                    } else {
                        tile.SetHighlight(TileHighlightState.None);
                    }
                }
                else
                {
                    tile.SetHighlight(TileHighlightState.None);
                }
            }
        } 
        _highlightedPathTiles.Clear(); 
    }
    public void ClearAttackRangeHighlight(bool clearList) { foreach (Tile tile in _highlightedAttackRangeTiles) { if (tile != null && tile.CurrentHighlightState == TileHighlightState.AttackRange) tile.SetHighlight(TileHighlightState.None); } if (clearList) _highlightedAttackRangeTiles.Clear(); }
    public void ClearAbilityRangeHighlight(bool clearList) { foreach (Tile tile in _highlightedAbilityRangeTiles) { if (tile != null && tile.CurrentHighlightState == TileHighlightState.AbilityRange) tile.SetHighlight(TileHighlightState.None); } if (clearList) _highlightedAbilityRangeTiles.Clear(); }

    public void CheckAndHandleEndOfTurnActionsPIH() 
    {
        if (_selectedUnit?.IsAlive == false || _selectedUnit.Movement == null || TurnManager.Instance?.ActiveUnit != _selectedUnit) return;
        if (_selectedUnit.Combat == null) { DebugHelper.LogError($"PIH: SelectedUnit {_selectedUnit.unitName} missing UnitCombat!", _selectedUnit); TurnManager.Instance.EndUnitTurn(_selectedUnit); return; }
        
        bool canTakeAnyOneAPAction = _selectedUnit.CurrentActionPoints >= 1; 
        if (!canTakeAnyOneAPAction) 
        {
            actionMenuUI?.HideActionBar(); 
            _skillSelectionPanelInstance?.HidePanel(); 
            _unitInfoPanelInstance?.HidePanel(); 
            _itemSelectionPanelInstance?.HidePanel(); 
            if (_selectedUnit.Movement.CurrentTile != null) _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
            TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }
    }
}