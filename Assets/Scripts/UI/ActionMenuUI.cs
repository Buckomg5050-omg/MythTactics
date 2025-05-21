// ActionMenuUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;

public class ActionMenuUI : MonoBehaviour
{
    [Header("Prefab & Settings")]
    public GameObject radialActionButtonPrefab; 

    [Header("Action Bar Layout Settings")]
    public float buttonSpacing = 10f; 

    [Header("Button Text Colors")]
    public Color affordableTextColor = Color.black;
    public Color unaffordableTextColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Header("Wait Sub-Menu Settings")] 
    public float waitSubMenuRadius = 70f;
    public float waitSubMenuItemScale = 0.65f;
    public float waitEndTurnButtonAngle = 180f;
    public float waitFleeButtonAngle = 90f;
    public float waitBackButtonAngle = 0f;

    [Header("Attack Sub-Menu Settings")] 
    public float attackSubMenuRadius = 70f;
    public float attackSubMenuItemScale = 0.65f;
    public float basicAttackButtonAngle = 180f; 
    public float spellsButtonAngle = 0f;      
    public float attackBackButtonAngle = 90f;  

    private List<GameObject> _activeMainButtonInstances = new List<GameObject>(); 
    private Unit _currentUnitInternal;

    public delegate void ActionSelectedHandler(Unit unit, string actionName);
    public static event ActionSelectedHandler OnActionSelected;

    private enum ActionButtonType { Skills, Items, Move, Attack, Info, Wait }
    private enum SubMenuType { None, Wait, Attack } 

    private struct FixedActionConfig
    {
        public ActionButtonType type;
        public string actionName; 
        public string displayName; 
        public float angleDegrees; 

        public FixedActionConfig(ActionButtonType type, string actionName, string displayName, float angleDegrees)
        {
            this.type = type;
            this.actionName = actionName;
            this.displayName = displayName;
            this.angleDegrees = angleDegrees;
        }
    }
    private List<FixedActionConfig> _definedActions;
    private List<ActionDefinition> _runtimeActiveMainActions = new List<ActionDefinition>();

    private List<GameObject> _activeSubMenuButtons = new List<GameObject>();
    private SubMenuType _currentOpenSubMenu = SubMenuType.None;
    private GameObject _currentSubMenuAnchor = null; 

    private const string SUB_ACTION_END_TURN = "ExecuteEndTurn";
    private const string SUB_ACTION_FLEE = "ExecuteFlee";
    private const string SUB_ACTION_BACK = "ExecuteBack"; 
    private const string SUB_ACTION_BASIC_ATTACK = "ExecuteBasicAttack";
    private const string SUB_ACTION_SHOW_SPELLS = "ShowSpellSelection";

    public const string FLEE_ACTION_NAME = "Flee";
    public const string BASIC_ATTACK_ACTION_NAME = "BasicAttack"; 
    public const string SHOW_SPELLS_ACTION_NAME = "ShowSpells"; 

    public struct ActionDefinition 
    {
        public string name;
        public string displayName;
        public bool canAfford;
        public float angleDegrees; 
        public GameObject instance; 

        public ActionDefinition(string name, string displayName, bool canAfford, float angleDegrees, GameObject instance = null)
        {
            this.name = name;
            this.displayName = displayName;
            this.canAfford = canAfford;
            this.angleDegrees = angleDegrees; 
            this.instance = instance;
        }
    }
    
    private RectTransform _rectTransform; 

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform == null)
        {
            Debug.LogError("ActionMenuUI: RectTransform component not found on this GameObject!", this);
        }

        // DEBUG: Check GraphicRaycaster
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            GraphicRaycaster gr = parentCanvas.GetComponent<GraphicRaycaster>();
            if (gr == null)
            {
                Debug.LogError("ActionMenuUI: Parent Canvas is MISSING GraphicRaycaster component! UI clicks will not work.", parentCanvas.gameObject);
            }
            else if (!gr.enabled)
            {
                Debug.LogWarning("ActionMenuUI: Parent Canvas has GraphicRaycaster, but it is DISABLED! UI clicks will not work.", parentCanvas.gameObject);
            }
            else
            {
                Debug.Log("ActionMenuUI: Parent Canvas has an ENABLED GraphicRaycaster.", parentCanvas.gameObject);
            }
        }
        else
        {
            Debug.LogError("ActionMenuUI: NO PARENT CANVAS FOUND! UI clicks will not work.", this.gameObject);
        }


        if (radialActionButtonPrefab == null) { Debug.LogError("ActionMenuUI: RadialActionButtonPrefab is null", this); }
        if (radialActionButtonPrefab != null && radialActionButtonPrefab.GetComponent<TooltipTrigger>() == null) { Debug.LogWarning("ActionMenuUI: RadialActionButtonPrefab missing TooltipTrigger.", this); }

        _definedActions = new List<FixedActionConfig>
        {
            new FixedActionConfig(ActionButtonType.Move,   "Move",   "Move",   0f), 
            new FixedActionConfig(ActionButtonType.Attack, "Attack", "Attack",   0f),
            new FixedActionConfig(ActionButtonType.Skills, "Skills", "Skills", 0f),
            new FixedActionConfig(ActionButtonType.Items,  "Items",  "Items",   0f),
            new FixedActionConfig(ActionButtonType.Info,   "Info",   "Info",   0f),
            new FixedActionConfig(ActionButtonType.Wait,   "Wait",   "Wait",   0f)  
        };
    }

    // Test method for direct listener assignment
    public void TestMoveButtonClicked()
    {
        Debug.Log("ActionMenuUI: TestMoveButtonClicked() CALLED DIRECTLY!", this.gameObject);
        OnActionButtonClicked("Move"); 
    }

    private int GetAPCostForAction(string actionName) 
    {
        switch (actionName)
        {
            case "Move": return PlayerInputHandler.MoveActionCost;
            case "Attack": return 0; 
            case SUB_ACTION_BASIC_ATTACK: return PlayerInputHandler.AttackActionCost; 
            case "Skills": return 0;
            case SUB_ACTION_SHOW_SPELLS: return 0; 
            case "Items": return 0;
            case "Info": return PlayerInputHandler.InfoActionCost;
            case "Wait": return 0; 
            case SUB_ACTION_END_TURN: return 0;
            case SUB_ACTION_FLEE: return 0;
            case SUB_ACTION_BACK: return 0;
            default: return 0;
        }
    }

    private void UpdateAvailableActions(Unit unit) 
    {
        _runtimeActiveMainActions.Clear(); 
        if (unit == null || _definedActions == null) return;

        foreach (FixedActionConfig fixedAction in _definedActions)
        {
            bool isActionAvailable = true;
            bool canAffordAction;

            switch (fixedAction.type)
            {
                case ActionButtonType.Skills:
                    isActionAvailable = unit.knownAbilities != null && unit.knownAbilities.Any(ab => ab.abilityType == AbilityType.Skill); 
                    canAffordAction = true; 
                    break;
                case ActionButtonType.Attack: 
                    canAffordAction = true; 
                    break;
                case ActionButtonType.Items:
                    canAffordAction = true; 
                    break;
                case ActionButtonType.Info:
                case ActionButtonType.Wait:
                    canAffordAction = true;
                    break;
                case ActionButtonType.Move:
                    int apCost = GetAPCostForAction(fixedAction.actionName); 
                    canAffordAction = unit.CanAffordAPForAction(apCost);
                    break;
                default:
                    isActionAvailable = false;
                    canAffordAction = false;
                    break;
            }

            if (isActionAvailable)
            {
                _runtimeActiveMainActions.Add(new ActionDefinition(fixedAction.actionName, fixedAction.displayName, canAffordAction, fixedAction.angleDegrees, null));
            }
        }
    }
    
    public void RefreshActionBar(Unit unitToShowMenuFor) 
    {
        HideSubMenu(); 

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        foreach (GameObject btnGO in _activeMainButtonInstances) { if (btnGO != null) Destroy(btnGO); }
        _activeMainButtonInstances.Clear(); 
        _runtimeActiveMainActions.Clear();

        this._currentUnitInternal = unitToShowMenuFor;

        if (this._currentUnitInternal == null || radialActionButtonPrefab == null)
        {
            return;
        }
        
        UpdateAvailableActions(this._currentUnitInternal); 

        if (_runtimeActiveMainActions.Count == 0)
        {
            return;
        }

        RectTransform buttonPrefabRect = radialActionButtonPrefab.GetComponent<RectTransform>();
        if (buttonPrefabRect == null) { Debug.LogError("ActionMenuUI: radialActionButtonPrefab is missing RectTransform!", radialActionButtonPrefab); return; }
        
        float buttonWidth = buttonPrefabRect.sizeDelta.x * buttonPrefabRect.transform.localScale.x;
        
        float totalWidthOfButtons = (_runtimeActiveMainActions.Count * buttonWidth) + (Mathf.Max(0, _runtimeActiveMainActions.Count - 1) * buttonSpacing);
        float currentX = -(totalWidthOfButtons / 2f) + (buttonWidth / 2f); 

        List<ActionDefinition> newRuntimeActions = new List<ActionDefinition>();

        for (int i = 0; i < _runtimeActiveMainActions.Count; i++)
        {
            ActionDefinition currentActionDefData = _runtimeActiveMainActions[i];
            
            GameObject buttonInstance = Instantiate(radialActionButtonPrefab, this.transform); 
            
            newRuntimeActions.Add(new ActionDefinition(
                currentActionDefData.name, 
                currentActionDefData.displayName, 
                currentActionDefData.canAfford, 
                currentActionDefData.angleDegrees, 
                buttonInstance));
            _activeMainButtonInstances.Add(buttonInstance);

            RectTransform buttonRect = buttonInstance.GetComponent<RectTransform>();
            if (buttonRect != null) 
            {
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f); 
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.anchoredPosition = new Vector2(currentX, 0); 
            }
            
            currentX += buttonWidth + buttonSpacing;

            TextMeshProUGUI buttonText = buttonInstance.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = currentActionDefData.displayName; 
                buttonText.color = currentActionDefData.canAfford ? affordableTextColor : unaffordableTextColor;
            }

            Button buttonComponent = buttonInstance.GetComponent<Button>();
            if (buttonComponent != null)
            {
                buttonComponent.interactable = currentActionDefData.canAfford;
                string capturedActionName = currentActionDefData.name;
                
                buttonComponent.onClick.RemoveAllListeners(); 

                if (currentActionDefData.name == "Move") // Specific test for "Move"
                {
                    buttonComponent.onClick.AddListener(TestMoveButtonClicked);
                    Debug.Log($"ActionMenuUI: Added DIRECT listener (TestMoveButtonClicked) for '{currentActionDefData.name}' button.", this.gameObject);
                }
                else // For all other buttons, use the lambda
                {
                    buttonComponent.onClick.AddListener(() => {
                        Debug.Log($"ActionMenuUI: LAMBDA for button '{capturedActionName}' TRIGGERED. Calling OnActionButtonClicked.", this.gameObject);
                        OnActionButtonClicked(capturedActionName);
                    });
                }
            }
            else
            {
                Debug.LogError($"ActionMenuUI: Button component MISSING on instantiated button for '{currentActionDefData.name}'!", buttonInstance);
            }

            TooltipTrigger trigger = buttonInstance.GetComponent<TooltipTrigger>();
            if (trigger != null)
            {
                string costString = "";
                string tooltipMainText = currentActionDefData.displayName; 
                if (currentActionDefData.name == "Attack") tooltipMainText = "Attack Options"; 
                else if (currentActionDefData.name == "Wait") tooltipMainText = "Wait Options"; 

                if (currentActionDefData.name == "Wait" || currentActionDefData.name == "Attack") 
                { 
                    costString = "\n(Free Action)"; 
                }
                else if (currentActionDefData.name != "Info" && currentActionDefData.name != "Skills" && currentActionDefData.name != "Items")
                {
                    int apCostForDirectAction = GetAPCostForAction(currentActionDefData.name);
                    costString = (apCostForDirectAction > 0) ? $"\n(AP Cost: {apCostForDirectAction})" : "\n(Free Action)";
                }
                else { costString = "\n(Free Action)"; }
                trigger.tooltipText = $"{tooltipMainText}{costString}";
            }
            buttonInstance.name = $"ActionButton_{currentActionDefData.name}";
            buttonInstance.SetActive(true);
        }
        _runtimeActiveMainActions = newRuntimeActions; 
    }
    
    private void ToggleSubMenu(string mainActionName, GameObject anchorButton, SubMenuType subMenuToOpen) 
    {
        if (_currentUnitInternal == null || anchorButton == null) return; 

        if (_currentOpenSubMenu == subMenuToOpen && _currentSubMenuAnchor == anchorButton)
        {
            HideSubMenu();
        }
        else
        {
            HideSubMenu(); 
            _currentOpenSubMenu = subMenuToOpen;
            _currentSubMenuAnchor = anchorButton;

            foreach (ActionDefinition ad in _runtimeActiveMainActions)
            {
                if (ad.instance != null && ad.instance != _currentSubMenuAnchor)
                {
                    ad.instance.SetActive(false);
                }
            }
            if (_currentSubMenuAnchor != null) _currentSubMenuAnchor.SetActive(true);

            List<(string actionName, string displayName, float angle, string internalId, float itemScale, float radius, bool interactable)> subActionsConfig =
                new List<(string, string, float, string, float, float, bool)>();

            if (subMenuToOpen == SubMenuType.Wait)
            {
                subActionsConfig.Add(( "End Turn", "End Turn", waitEndTurnButtonAngle, SUB_ACTION_END_TURN, waitSubMenuItemScale, waitSubMenuRadius, true));
                subActionsConfig.Add(( FLEE_ACTION_NAME, FLEE_ACTION_NAME, waitFleeButtonAngle, SUB_ACTION_FLEE, waitSubMenuItemScale, waitSubMenuRadius, true));
                subActionsConfig.Add(( "Back", "Back", waitBackButtonAngle, SUB_ACTION_BACK, waitSubMenuItemScale, waitSubMenuRadius, true));
            }
            else if (subMenuToOpen == SubMenuType.Attack)
            {
                bool canAffordBasicAttack = _currentUnitInternal.CanAffordAPForAction(GetAPCostForAction(SUB_ACTION_BASIC_ATTACK));
                subActionsConfig.Add(( BASIC_ATTACK_ACTION_NAME, "Basic Attack", basicAttackButtonAngle, SUB_ACTION_BASIC_ATTACK, attackSubMenuItemScale, attackSubMenuRadius, canAffordBasicAttack ));

                bool hasSpells = _currentUnitInternal.knownAbilities != null && _currentUnitInternal.knownAbilities.Any(ab => ab.abilityType == AbilityType.Spell);
                if (hasSpells)
                {
                     subActionsConfig.Add(( SHOW_SPELLS_ACTION_NAME, "Spells", spellsButtonAngle, SUB_ACTION_SHOW_SPELLS, attackSubMenuItemScale, attackSubMenuRadius, true )); 
                }
                subActionsConfig.Add(( "Back", "Back", attackBackButtonAngle, SUB_ACTION_BACK, attackSubMenuItemScale, attackSubMenuRadius, true ));
            }
            InstantiateSubMenuButtons(anchorButton.transform.position, subActionsConfig);
        }
    }
        
    private void InstantiateSubMenuButtons(Vector2 anchorPos, List<(string actionName, string displayName, float angle, string internalId, float itemScale, float radius, bool interactable)> configs) 
    {
        foreach (var config in configs)
        {
            float angleRad = Mathf.Deg2Rad * config.angle;
            Vector2 offset = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * config.radius; 
            Vector2 buttonPos = anchorPos + offset;

            GameObject subButtonInstance = Instantiate(radialActionButtonPrefab, this.transform);
            subButtonInstance.transform.localScale = Vector3.one * config.itemScale;

            RectTransform subButtonRect = subButtonInstance.GetComponent<RectTransform>();
            if (subButtonRect != null) { subButtonRect.anchorMin = Vector2.zero; subButtonRect.anchorMax = Vector2.zero; subButtonRect.pivot = new Vector2(0.5f,0.5f); }
            subButtonInstance.transform.position = buttonPos;

            TextMeshProUGUI buttonText = subButtonInstance.GetComponentInChildren<TextMeshProUGUI>();
            Button buttonComponent = subButtonInstance.GetComponent<Button>();
            TooltipTrigger tooltip = subButtonInstance.GetComponent<TooltipTrigger>();

            string costString = "";
            int apCostForTooltip = (config.internalId == SUB_ACTION_BASIC_ATTACK) ? GetAPCostForAction(SUB_ACTION_BASIC_ATTACK) : 0;

            if(config.internalId == SUB_ACTION_BASIC_ATTACK) costString = (apCostForTooltip > 0) ? $"\n(AP Cost: {apCostForTooltip})" : "\n(Free Action)";
            else if (config.internalId == SUB_ACTION_SHOW_SPELLS) costString = "\n(Select Spell)";
            else if (config.internalId == SUB_ACTION_FLEE) costString = "\n(Attempt Flee)";
            else if (config.internalId == SUB_ACTION_BACK) costString = "\n(Return)";
            else costString = "\n(Free Action)";

            if (buttonText != null) { buttonText.text = config.displayName; buttonText.color = config.interactable ? affordableTextColor : unaffordableTextColor; }
            if (buttonComponent != null) 
            { 
                buttonComponent.interactable = config.interactable; 
                string id = config.internalId; 
                buttonComponent.onClick.RemoveAllListeners();
                buttonComponent.onClick.AddListener(() => {
                    Debug.Log($"ActionMenuUI: LAMBDA for SUB-button '{id}' TRIGGERED. Calling OnSubMenuActionButtonClicked.", this.gameObject);
                    OnSubMenuActionButtonClicked(id);
                });
            }
            else
            {
                 Debug.LogError($"ActionMenuUI: Button component MISSING on instantiated SUB-button for '{config.displayName}'!", subButtonInstance);
            }
            if (tooltip != null) { tooltip.tooltipText = $"{config.displayName}{costString}"; }

            subButtonInstance.name = $"SubActionButton_{config.displayName.Replace(" ", "")}";
            subButtonInstance.SetActive(true);
            _activeSubMenuButtons.Add(subButtonInstance);
        }
    }

    public void HideSubMenu() 
    {
        foreach (GameObject button in _activeSubMenuButtons)
        {
            if (button != null) Destroy(button);
        }
        _activeSubMenuButtons.Clear();

        if (_currentOpenSubMenu != SubMenuType.None) 
        {
            foreach (ActionDefinition ad in _runtimeActiveMainActions)
            {
                if (ad.instance != null) ad.instance.SetActive(true); 
            }
        }
        _currentOpenSubMenu = SubMenuType.None;
        _currentSubMenuAnchor = null;
    }

    public void HideActionBar() 
    {
        HideSubMenu(); 
        foreach (GameObject buttonGO in _activeMainButtonInstances) 
        {
            if (buttonGO != null) Destroy(buttonGO);
        }
        _activeMainButtonInstances.Clear();
        _runtimeActiveMainActions.Clear(); 
        if (gameObject.activeSelf) gameObject.SetActive(false);
        _currentUnitInternal = null; 
    }
    
    private void OnActionButtonClicked(string actionName) 
    {
        Debug.Log($"ActionMenuUI: OnActionButtonClicked ENTERED with actionName: '{actionName}', _currentUnitInternal: {_currentUnitInternal?.unitName ?? "NULL"}", this.gameObject); 
        if (_currentUnitInternal == null) 
        {
            Debug.LogWarning("ActionMenuUI: OnActionButtonClicked - _currentUnitInternal is NULL. Aborting.", this.gameObject);
            return;
        }

        ActionDefinition actionDef = _runtimeActiveMainActions.FirstOrDefault(ad => ad.name == actionName && ad.instance != null);
        
        if (actionDef.instance == null && (actionName == "Wait" || actionName == "Attack")) {
             Debug.LogError($"ActionMenuUI: Could not find instance for main action button '{actionName}' to anchor sub-menu.", this);
             return;
        }

        if (actionName == "Wait")
        {
            ToggleSubMenu(actionName, actionDef.instance, SubMenuType.Wait);
        }
        else if (actionName == "Attack")
        {
            ToggleSubMenu(actionName, actionDef.instance, SubMenuType.Attack);
        }
        else
        {
            HideSubMenu(); 
            OnActionSelected?.Invoke(_currentUnitInternal, actionName);
        }
    }

    private void OnSubMenuActionButtonClicked(string subActionInternalId) 
    {
        Debug.Log($"ActionMenuUI: OnSubMenuActionButtonClicked ENTERED with subActionInternalId: '{subActionInternalId}', _currentUnitInternal: {_currentUnitInternal?.unitName ?? "NULL"}", this.gameObject);
        if (_currentUnitInternal == null) 
        {
            Debug.LogWarning("ActionMenuUI: OnSubMenuActionButtonClicked - _currentUnitInternal is NULL. Aborting.", this.gameObject);
            return;
        }
        
        HideSubMenu(); 

        if (subActionInternalId == SUB_ACTION_BACK)
        {
            return; 
        }
        
        string actionToForward = "";

        switch (subActionInternalId)
        {
            case SUB_ACTION_END_TURN:       actionToForward = "Wait"; break;
            case SUB_ACTION_FLEE:           actionToForward = FLEE_ACTION_NAME; break;
            case SUB_ACTION_BASIC_ATTACK:   actionToForward = BASIC_ATTACK_ACTION_NAME; break;
            case SUB_ACTION_SHOW_SPELLS:    actionToForward = SHOW_SPELLS_ACTION_NAME; break;
        }

        if (!string.IsNullOrEmpty(actionToForward))
        {
            OnActionSelected?.Invoke(_currentUnitInternal, actionToForward);
        }
    }

    public bool IsVisible() 
    { 
        return gameObject.activeSelf && (_runtimeActiveMainActions.Any(ad => ad.instance != null && ad.instance.activeSelf) || _activeSubMenuButtons.Count > 0);
    }

    public bool IsSubMenuOpen()
    {
        return _currentOpenSubMenu != SubMenuType.None && _activeSubMenuButtons.Count > 0;
    }

    public void SimulateMainActionClick(string actionName)
    {
        Debug.Log($"ActionMenuUI: SimulateMainActionClick for '{actionName}' called.", this.gameObject); 
        if (gameObject.activeSelf && _currentUnitInternal != null)
        {
            ActionDefinition actionDef = _runtimeActiveMainActions.FirstOrDefault(ad => ad.name == actionName);
            if (actionDef.name == actionName) 
            {
                OnActionButtonClicked(actionName);
            }
            else
            {
                Debug.LogWarning($"ActionMenuUI.SimulateMainActionClick: Action '{actionName}' not found or not currently available.", this);
            }
        }
    }

    public void SimulateSubMenuActionClick(string subActionInternalIdToSimulate)
    {
        Debug.Log($"ActionMenuUI: SimulateSubMenuActionClick for '{subActionInternalIdToSimulate}' called.", this.gameObject); 
        if (gameObject.activeSelf && _currentUnitInternal != null)
        {
            OnSubMenuActionButtonClicked(subActionInternalIdToSimulate);
        }
    }
}