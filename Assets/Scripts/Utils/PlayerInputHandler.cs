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
    public GameObject itemSelectionPanelPrefab;

    [Header("Tooltip Settings")]
    public float unitTooltipDelay = 0.5f;

    private SkillSelectionUI _skillSelectionPanelInstance;
    private UnitInfoPanelUI _unitInfoPanelInstance;
    private ItemSelectionPanelUI _itemSelectionPanelInstance;

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

    private Unit _hoveredUnitOnTileForTooltip = null;
    private Coroutine _unitTooltipCoroutine = null;
    private Tile _lastHoveredTileWithUnit = null;

    private bool _isPointerOverUIThisFrame;

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
        ChangeState(new PIH_WaitingForTurnState());
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            _isPointerOverUIThisFrame = UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }
        else
        {
            _isPointerOverUIThisFrame = false;
        }

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
        HandleTileHoverForTooltip();
    }
    
    private void HandleTileHoverForTooltip()
    {
        if (TooltipUI.Instance == null) return;

        bool actionMenuVisible = (actionMenuUI != null && actionMenuUI.IsVisible());
        bool skillPanelVisible = (_skillSelectionPanelInstance != null && _skillSelectionPanelInstance.IsVisible());
        bool infoPanelVisible = (_unitInfoPanelInstance != null && _unitInfoPanelInstance.IsVisible());
        bool itemPanelVisible = (_itemSelectionPanelInstance != null && _itemSelectionPanelInstance.IsVisible());
        bool majorUIIsOpen = actionMenuVisible || skillPanelVisible || infoPanelVisible || itemPanelVisible;

        if (majorUIIsOpen || _isPointerOverUIThisFrame)
        {
            if (_hoveredUnitOnTileForTooltip != null)
            {
                if (_unitTooltipCoroutine != null) StopCoroutine(_unitTooltipCoroutine);
                _unitTooltipCoroutine = null;
                TooltipUI.Instance.HideTooltip();
                _hoveredUnitOnTileForTooltip = null;
                _lastHoveredTileWithUnit = null;
            }
            return;
        }

        Tile hoveredTile = GetTileUnderMouse(false);
        Unit unitOnHoveredTile = hoveredTile?.occupyingUnit;

        if (unitOnHoveredTile != null)
        {
            if (_hoveredUnitOnTileForTooltip != unitOnHoveredTile || _lastHoveredTileWithUnit != hoveredTile)
            {
                if (_hoveredUnitOnTileForTooltip != null) TooltipUI.Instance.HideTooltip();
                if (_unitTooltipCoroutine != null) StopCoroutine(_unitTooltipCoroutine);
                
                _hoveredUnitOnTileForTooltip = unitOnHoveredTile;
                _lastHoveredTileWithUnit = hoveredTile;
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
                _lastHoveredTileWithUnit = null;
            }
        }
    }

    private IEnumerator ShowUnitTooltipAfterDelay(Unit unit, Vector2 screenPosition)
    {
        yield return new WaitForSeconds(unitTooltipDelay);

        Tile currentTileUnderMouse = GetTileUnderMouse(false);
        bool stillHoveringSameUnitAndTile = (_hoveredUnitOnTileForTooltip == unit && currentTileUnderMouse == _lastHoveredTileWithUnit);
        bool currentPointerOverUI = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        bool conditionsMet = stillHoveringSameUnitAndTile && 
                             TooltipUI.Instance != null && 
                             unit != null && unit.Stats != null && 
                             !currentPointerOverUI; 

        if (conditionsMet)
        {
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
            if (TooltipUI.Instance != null && _hoveredUnitOnTileForTooltip == unit) 
            {
                TooltipUI.Instance.HideTooltip();
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
        
        if (!(_currentStateObject is PIH_UnitActionPhaseState && _itemSelectionPanelInstance != null && _itemSelectionPanelInstance.IsVisible()))
        {
            _itemSelectionPanelInstance?.HidePanel();
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

        if (_unitTooltipCoroutine != null) 
        { 
            StopCoroutine(_unitTooltipCoroutine); 
            _unitTooltipCoroutine = null; 
        }
        if (TooltipUI.Instance != null) TooltipUI.Instance.HideTooltip(); 
        _hoveredUnitOnTileForTooltip = null;
        _lastHoveredTileWithUnit = null;
    }
    
    private Tile GetTileUnderMouse(bool blockIfOverUI = true)
    {
        if (_mainCamera == null || GridManager.Instance == null) return null;
        Vector2 screenPosition = _playerControls.Gameplay.Point.ReadValue<Vector2>();
        
        if (blockIfOverUI && _isPointerOverUIThisFrame)
        {
            if (!(_currentStateObject is PIH_SelectingMoveTargetState || 
                  _currentStateObject is PIH_SelectingAttackTargetState ||
                  _currentStateObject is PIH_SelectingAbilityTargetState))
            {
                return null; 
            }
        }
        else if (!blockIfOverUI && _isPointerOverUIThisFrame) 
        {
            return null;
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

    private void HandleItemPanelClosedByButton()
    {
        _itemSelectionPanelInstance?.HidePanel();
        ChangeState(new PIH_UnitActionPhaseState());
    }
    
    private void OnClickPerformedHandler(InputAction.CallbackContext context) { _currentStateObject?.OnClickInput(context, GetTileUnderMouse(true)); } 
    private void OnToggleAttackModeInputHandler(InputAction.CallbackContext context) { _currentStateObject?.OnToggleAttackModeInput(context); }
    private void OnSelectAbilityInputHandler(InputAction.CallbackContext context) { _currentStateObject?.OnSelectAbilityInput(context); }
    private void OnEndTurnInputHandler(InputAction.CallbackContext context) { _currentStateObject?.OnEndTurnInput(context); }
    
    private void OnToggleActionMenuPerformedHandler(InputAction.CallbackContext context)
    {
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
            _itemSelectionPanelInstance?.HidePanel();
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
        
        if (_itemSelectionPanelInstance != null && _itemSelectionPanelInstance.IsVisible())
        {
            _itemSelectionPanelInstance.HidePanel();
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
        if (_selectedUnit != null && actionMenuUI != null && _mainCamera != null && 
            TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _selectedUnit) 
        {
            _skillSelectionPanelInstance?.HidePanel(); 
            _unitInfoPanelInstance?.HidePanel(); 
            _itemSelectionPanelInstance?.HidePanel();
            
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
        if (_selectedUnit == null) return;
        actionMenuUI?.HideMenu(); 
        _skillSelectionPanelInstance?.HidePanel();
        _unitInfoPanelInstance?.HidePanel(); 
        _itemSelectionPanelInstance?.HidePanel();
        ProcessActionSelection(actionName); 
    }
    
    private void HandleActionMenuSelection(Unit unit, string actionName) 
    {
        if (unit != _selectedUnit || _selectedUnit == null) return; 
        _skillSelectionPanelInstance?.HidePanel(); 
        _unitInfoPanelInstance?.HidePanel(); 
        _itemSelectionPanelInstance?.HidePanel();
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
                ShowSkillSelectionPanel(); 
                break;
            case "Items": 
                ShowItemSelectionPanel(); 
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
        if (_selectedUnit == null || skillSelectionPanelPrefab == null) return;
        if (_skillSelectionPanelInstance == null)
        {
            Canvas mainCanvas = FindFirstObjectByType<Canvas>(); 
            if (mainCanvas == null) { Debug.LogError("PIH: No Canvas found for SkillSelectionPanel!"); return; }
            GameObject panelGO = Instantiate(skillSelectionPanelPrefab, mainCanvas.transform);
            _skillSelectionPanelInstance = panelGO.GetComponent<SkillSelectionUI>();
            if (_skillSelectionPanelInstance == null) { Debug.LogError("PIH: SkillSelectionPanelPrefab missing SkillSelectionUI!", panelGO); Destroy(panelGO); return; }
        }
        Vector2 panelPosition = (Vector2)_mainCamera.WorldToScreenPoint(_selectedUnit.transform.position) + new Vector2(150, 0); 
        _skillSelectionPanelInstance.ShowPanel(_selectedUnit, panelPosition, _selectedUnit.knownAbilities);
        actionMenuUI?.HideMenu(); 
        _unitInfoPanelInstance?.HidePanel(); 
        _itemSelectionPanelInstance?.HidePanel(); 
    }

    private void ShowItemSelectionPanel()
    {
        if (_selectedUnit == null || itemSelectionPanelPrefab == null)
        {
            return;
        }
        if (_itemSelectionPanelInstance == null)
        {
            Canvas mainCanvas = FindFirstObjectByType<Canvas>(); 
            if (mainCanvas == null) { Debug.LogError("PIH: No Canvas found to instantiate ItemSelectionPanel!", this); return; }
            GameObject panelGO = Instantiate(itemSelectionPanelPrefab, mainCanvas.transform);
            _itemSelectionPanelInstance = panelGO.GetComponent<ItemSelectionPanelUI>();
            if (_itemSelectionPanelInstance == null)
            {
                Debug.LogError("PIH: ItemSelectionPanelPrefab is missing the ItemSelectionPanelUI script!", panelGO);
                Destroy(panelGO);
                return;
            }
        }
        _itemSelectionPanelInstance.ShowPanel(_selectedUnit); 
        
        actionMenuUI?.HideMenu();
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
        
        // MODIFIED: Item Consumption Logic
        if (selectedItem.consumeOnUse)
        {
            if (user.inventory != null) // Ensure inventory list exists
            {
                bool removed = user.inventory.Remove(selectedItem); // Remove the first instance of this ItemSO
                if (removed)
                {
                    Debug.Log($"PIH: Consumed and removed '{selectedItem.itemName}' from {user.unitName}'s inventory.", this);
                }
                else
                {
                    // This might happen if the item wasn't actually in the inventory, though it should be if it was displayed.
                    Debug.LogWarning($"PIH: Tried to consume '{selectedItem.itemName}' but it was not found in {user.unitName}'s inventory for removal.", this);
                }
            }
        }
        // End of Item Consumption Logic
        
        _itemSelectionPanelInstance?.HidePanel();
        ChangeState(new PIH_UnitActionPhaseState());
        CheckAndHandleEndOfTurnActionsPIH(); 
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
            bool maintainHighlight = (_currentStateObject is PIH_UnitActionPhaseState || _currentStateObject is PIH_ViewingUnitInfoState) && isSelectedUnitsTurn;

            if (!maintainHighlight && _selectedUnit.Movement.CurrentTile.CurrentHighlightState == TileHighlightState.SelectedUnit)
            {
                 _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.None);
            }
            else if (maintainHighlight && _selectedUnit.Movement.CurrentTile.CurrentHighlightState != TileHighlightState.SelectedUnit)
            {
                _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
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
            _itemSelectionPanelInstance?.HidePanel(); 
            if (_selectedUnit.Movement.CurrentTile != null) _selectedUnit.Movement.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
            TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }
    }
}