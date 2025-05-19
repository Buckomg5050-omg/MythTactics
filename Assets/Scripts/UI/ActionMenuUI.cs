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
    public float menuRadius = 100f;
    public Vector2 menuCenterOffset = new Vector2(0, 50f);

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

    private List<GameObject> _activeButtons = new List<GameObject>();
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


    void Awake()
    {
        if (radialActionButtonPrefab == null)
        {
            Debug.LogError("ActionMenuUI.Awake: RadialActionButton_Prefab not assigned in Inspector! Menu will not function.", this);
        }
        if (radialActionButtonPrefab != null && radialActionButtonPrefab.GetComponent<TooltipTrigger>() == null)
        {
            Debug.LogWarning("ActionMenuUI.Awake: RadialActionButton_Prefab is missing the TooltipTrigger component. Tooltips for action buttons will not work.", this);
        }

        _definedActions = new List<FixedActionConfig>
        {
            new FixedActionConfig(ActionButtonType.Skills, "Skills", "Skills", 135f),
            new FixedActionConfig(ActionButtonType.Items,  "Items",  "Items",   45f),
            new FixedActionConfig(ActionButtonType.Move,   "Move",   "Move",   180f),
            new FixedActionConfig(ActionButtonType.Attack, "Attack", "Attack Options",   0f), 
            new FixedActionConfig(ActionButtonType.Info,   "Info",   "Info",   225f),
            new FixedActionConfig(ActionButtonType.Wait,   "Wait",   "Wait Options",   315f)  
        };
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
                    // MODIFIED: Check for actual skills
                    isActionAvailable = unit.knownAbilities != null && unit.knownAbilities.Any(ab => ab.abilityType == AbilityType.Skill); 
                    canAffordAction = true; // Opening panel is free
                    break;
                case ActionButtonType.Attack: 
                    canAffordAction = true; // Opening sub-menu is free
                    break;
                case ActionButtonType.Items:
                    // Future: Could add a check here if unit has no usable items.
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
    
    public void ShowMenu(Unit unitToShowMenuFor, Vector2 unitScreenPosition)
    {
        HideSubMenu(); 

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        foreach (ActionDefinition ad in _runtimeActiveMainActions) { if (ad.instance != null) Destroy(ad.instance); }
        _activeButtons.Clear(); 
        _runtimeActiveMainActions.Clear();

        this._currentUnitInternal = unitToShowMenuFor;

        if (this._currentUnitInternal == null || radialActionButtonPrefab == null)
        {
            HideMenu();
            return;
        }
        
        UpdateAvailableActions(this._currentUnitInternal); 

        if (_runtimeActiveMainActions.Count == 0)
        {
            HideMenu();
            return;
        }

        RectTransform prefabRect = radialActionButtonPrefab.GetComponent<RectTransform>();
        if (prefabRect == null) { Debug.LogError("ActionMenuUI: radialActionButtonPrefab is missing RectTransform!", radialActionButtonPrefab); HideMenu(); return; }
        float buttonWidth = prefabRect.sizeDelta.x * prefabRect.transform.localScale.x;
        float buttonHeight = prefabRect.sizeDelta.y * prefabRect.transform.localScale.y;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        
        List<Vector2> buttonLocalOffsets = new List<Vector2>();
        foreach(var actionDef in _runtimeActiveMainActions)
        {
            float currentAngleRad = Mathf.Deg2Rad * actionDef.angleDegrees;
            Vector2 localOffset = new Vector2(Mathf.Cos(currentAngleRad), Mathf.Sin(currentAngleRad)) * menuRadius;
            buttonLocalOffsets.Add(localOffset);
            minX = Mathf.Min(minX, localOffset.x - buttonWidth / 2f);
            minY = Mathf.Min(minY, localOffset.y - buttonHeight / 2f);
            maxX = Mathf.Max(maxX, localOffset.x + buttonWidth / 2f);
            maxY = Mathf.Max(maxY, localOffset.y + buttonHeight / 2f);
        }

        Vector2 desiredMenuCenter = unitScreenPosition + menuCenterOffset;
        Vector2 adjustment = Vector2.zero;
        if (desiredMenuCenter.x + minX < 0) adjustment.x = -(desiredMenuCenter.x + minX);
        if (desiredMenuCenter.x + maxX > Screen.width) adjustment.x = Screen.width - (desiredMenuCenter.x + maxX);
        if (desiredMenuCenter.y + minY < 0) adjustment.y = -(desiredMenuCenter.y + minY);
        if (desiredMenuCenter.y + maxY > Screen.height) adjustment.y = Screen.height - (desiredMenuCenter.y + maxY);
        Vector2 finalMenuCenter = desiredMenuCenter + adjustment;

        for (int i = 0; i < _runtimeActiveMainActions.Count; i++)
        {
            ActionDefinition currentActionDefData = _runtimeActiveMainActions[i];
            Vector2 localButtonOffset = buttonLocalOffsets[i];
            GameObject buttonInstance = Instantiate(radialActionButtonPrefab, this.transform);
            
            _runtimeActiveMainActions[i] = new ActionDefinition(
                currentActionDefData.name, currentActionDefData.displayName, 
                currentActionDefData.canAfford, currentActionDefData.angleDegrees, buttonInstance);
            _activeButtons.Add(buttonInstance);

            RectTransform buttonRect = buttonInstance.GetComponent<RectTransform>();
            if (buttonRect != null) { buttonRect.anchorMin = Vector2.zero; buttonRect.anchorMax = Vector2.zero; buttonRect.pivot = new Vector2(0.5f,0.5f); }
            buttonInstance.transform.position = finalMenuCenter + localButtonOffset;
            
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
                buttonComponent.onClick.AddListener(() => OnActionButtonClicked(capturedActionName));
            }

            TooltipTrigger trigger = buttonInstance.GetComponent<TooltipTrigger>();
            if (trigger != null)
            {
                string costString = "";
                string tooltipMainText = currentActionDefData.displayName; 

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
    }

    private void ToggleSubMenu(string mainActionName, GameObject anchorButton, SubMenuType subMenuToOpen)
    {
        if (_currentUnitInternal == null) return; // Added null check for safety

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

                // MODIFIED: Check for actual spells
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
            if (buttonComponent != null) { buttonComponent.interactable = config.interactable; string id = config.internalId; buttonComponent.onClick.AddListener(() => OnSubMenuActionButtonClicked(id)); }
            if (tooltip != null) { tooltip.tooltipText = $"{config.displayName}{costString}"; }

            subButtonInstance.name = $"SubActionButton_{config.displayName.Replace(" ", "")}";
            subButtonInstance.SetActive(true);
            _activeSubMenuButtons.Add(subButtonInstance);
        }
    }

    private void HideSubMenu() 
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

    public void HideMenu() 
    {
        HideSubMenu();
        foreach (GameObject button in _activeButtons) { if (button != null) Destroy(button); }
        _activeButtons.Clear();
        _runtimeActiveMainActions.Clear();
        _currentUnitInternal = null;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void OnActionButtonClicked(string actionName)
    {
        if (_currentUnitInternal == null) return;

        ActionDefinition actionDef = _runtimeActiveMainActions.FirstOrDefault(ad => ad.name == actionName && ad.instance != null);
        if (actionDef.instance == null && (actionName == "Wait" || actionName == "Attack")) {
             Debug.LogError($"ActionMenuUI: Could not find instance for main action button '{actionName}'.", this);
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
        if (_currentUnitInternal == null) return;
        
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
}