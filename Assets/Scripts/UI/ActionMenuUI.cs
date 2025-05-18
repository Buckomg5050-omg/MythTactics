// ActionMenuUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

[System.Serializable]
public class ActionButtonSetup
{
    public string actionName;
}

public class ActionMenuUI : MonoBehaviour
{
    [Header("Prefab & Settings")]
    public GameObject radialActionButtonPrefab;
    public float menuRadius = 100f;
    public Vector2 menuCenterOffset = new Vector2(0, 50f);

    [Header("Configurable Actions")]
    public List<ActionButtonSetup> configurableActions = new List<ActionButtonSetup>();

    [Header("Button Text Colors")]
    public Color affordableTextColor = Color.black;
    public Color unaffordableTextColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    // REMOVED: No direct reference to ItemSelectionPanelUI here
    // public ItemSelectionPanelUI itemSelectionPanelUI;

    private List<GameObject> _activeButtons = new List<GameObject>();
    private Unit _currentUnitInternal;

    public delegate void ActionSelectedHandler(Unit unit, string actionName);
    public static event ActionSelectedHandler OnActionSelected;

    private List<ActionDefinition> _runtimeAvailableActions = new List<ActionDefinition>();

    public struct ActionDefinition
    {
        public string name;
        public string displayName;
        public bool canAfford; // For actions like Move/Attack, this is AP. For Skills/Items, this is effectively "can open panel".

        public ActionDefinition(string name, string displayName, bool canAfford)
        {
            this.name = name;
            this.displayName = displayName;
            this.canAfford = canAfford;
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
        // REMOVED: Null check for itemSelectionPanelUI
    }

    private int GetAPCostForAction(string actionName)
    {
        // This returns AP cost for actions that are directly executed.
        // Opening sub-menus like "Items" or "Skills" via OnActionSelected is considered free at this stage.
        // The AP cost for using an item or skill will be handled by the respective systems.
        switch (actionName)
        {
            case "Move": return PlayerInputHandler.MoveActionCost;
            case "Attack": return PlayerInputHandler.AttackActionCost;
            case "Skills": return 0; // Cost to OPEN panel is 0. Actual skill use has cost.
            case "Items": return 0;  // Cost to OPEN panel is 0. Actual item use has cost.
            case "Info": return PlayerInputHandler.InfoActionCost; // Typically 0
            case "Wait": return PlayerInputHandler.WaitActionCost;
            default:
                return 0;
        }
    }

    private void UpdateAvailableActions(Unit unit)
    {
        _runtimeAvailableActions.Clear();
        if (unit == null || configurableActions == null) return;

        foreach (ActionButtonSetup actionSetup in configurableActions)
        {
            if (string.IsNullOrEmpty(actionSetup.actionName)) continue;

            bool canAffordAction;

            if (actionSetup.actionName == "Skills")
            {
                bool hasSkills = unit.knownAbilities != null && unit.knownAbilities.Count > 0;
                if (!hasSkills) { continue; } // Don't add "Skills" button if no skills
                canAffordAction = true; // Opening skill menu is always "affordable"
            }
            else if (actionSetup.actionName == "Items")
            {
                // Future: Could add a check here if unit has no usable items, then 'continue;'
                // bool hasItems = unit.HasUsableItems(); // Requires inventory system
                // if (!hasItems) { continue; }
                canAffordAction = true; // Opening item menu is always "affordable"
            }
            else if (actionSetup.actionName == "Info")
            {
                canAffordAction = true;
            }
            else
            {
                int apCost = GetAPCostForAction(actionSetup.actionName); // This will be 0 for Items/Skills now
                canAffordAction = unit.CanAffordAPForAction(apCost);
            }
            _runtimeAvailableActions.Add(new ActionDefinition(actionSetup.actionName, actionSetup.actionName, canAffordAction));
        }
    }


    public void ShowMenu(Unit unitToShowMenuFor, Vector2 unitScreenPosition)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        foreach (GameObject button in _activeButtons) { if (button != null) Destroy(button); }
        _activeButtons.Clear();

        this._currentUnitInternal = unitToShowMenuFor;

        if (this._currentUnitInternal == null || radialActionButtonPrefab == null)
        {
            HideMenu();
            return;
        }

        UpdateAvailableActions(this._currentUnitInternal);

        int numActions = _runtimeAvailableActions.Count;
        if (numActions == 0)
        {
            HideMenu();
            return;
        }

        List<Vector2> buttonLocalPositions = new List<Vector2>();
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        RectTransform prefabRect = radialActionButtonPrefab.GetComponent<RectTransform>();
        if (prefabRect == null) {
            Debug.LogError("ActionMenuUI: radialActionButtonPrefab is missing RectTransform!", radialActionButtonPrefab);
            HideMenu();
            return;
        }
        float buttonWidth = prefabRect.sizeDelta.x * prefabRect.transform.localScale.x;
        float buttonHeight = prefabRect.sizeDelta.y * prefabRect.transform.localScale.y;
        float angleStep = 360f / numActions;
        float startAngleOffset = 90f;
        if (numActions == 1) startAngleOffset = 0;
        else if (numActions > 4) startAngleOffset = 90f - (angleStep / 2f);

        for (int i = 0; i < numActions; i++)
        {
            float angle = i * angleStep;
            float currentAngleRad = Mathf.Deg2Rad * (angle + startAngleOffset);
            Vector2 localPos = new Vector2(Mathf.Cos(currentAngleRad), Mathf.Sin(currentAngleRad)) * menuRadius;
            buttonLocalPositions.Add(localPos);
            minX = Mathf.Min(minX, localPos.x - buttonWidth / 2f);
            minY = Mathf.Min(minY, localPos.y - buttonHeight / 2f);
            maxX = Mathf.Max(maxX, localPos.x + buttonWidth / 2f);
            maxY = Mathf.Max(maxY, localPos.y + buttonHeight / 2f);
        }

        Vector2 desiredMenuCenter = unitScreenPosition + menuCenterOffset;
        Vector2 adjustment = Vector2.zero;
        if (desiredMenuCenter.x + minX < 0) adjustment.x = -(desiredMenuCenter.x + minX);
        if (desiredMenuCenter.x + maxX > Screen.width) adjustment.x = Screen.width - (desiredMenuCenter.x + maxX);
        if (desiredMenuCenter.y + minY < 0) adjustment.y = -(desiredMenuCenter.y + minY);
        if (desiredMenuCenter.y + maxY > Screen.height) adjustment.y = Screen.height - (desiredMenuCenter.y + maxY);
        Vector2 finalMenuCenter = desiredMenuCenter + adjustment;

        for (int i = 0; i < numActions; i++)
        {
            GameObject buttonInstance = Instantiate(radialActionButtonPrefab, this.transform);
            RectTransform buttonRect = buttonInstance.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                buttonRect.anchorMin = Vector2.zero;
                buttonRect.anchorMax = Vector2.zero;
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
            }
            buttonInstance.transform.position = finalMenuCenter + buttonLocalPositions[i];
            ActionDefinition currentActionDef = _runtimeAvailableActions[i];
            TextMeshProUGUI buttonText = buttonInstance.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = currentActionDef.displayName;
                // For items/skills/info, color is always affordable because opening panel is free.
                // For other actions, it depends on AP.
                buttonText.color = currentActionDef.canAfford ? affordableTextColor : unaffordableTextColor;
            }
            Button buttonComponent = buttonInstance.GetComponent<Button>();
            if (buttonComponent != null)
            {
                // Interactability for items/skills/info is always true (opening panel is free)
                // For other actions, it depends on AP.
                buttonComponent.interactable = currentActionDef.canAfford;

                string capturedActionName = currentActionDef.name;
                buttonComponent.onClick.AddListener(() => OnActionButtonClicked(capturedActionName));
            }

            TooltipTrigger trigger = buttonInstance.GetComponent<TooltipTrigger>();
            if (trigger != null)
            {
                string costString = "";
                // Tooltip for "Items" and "Skills" should not show AP cost, as opening is free.
                // The cost is for *using* an item/skill from the subsequent panel.
                if (currentActionDef.name != "Info" && currentActionDef.name != "Skills" && currentActionDef.name != "Items")
                {
                    // For other actions, get their direct AP cost for the tooltip
                    int apCostForDirectAction = 0; // Default to 0
                     switch (currentActionDef.name) // Re-fetch specific costs for tooltip clarity
                    {
                        case "Move": apCostForDirectAction = PlayerInputHandler.MoveActionCost; break;
                        case "Attack": apCostForDirectAction = PlayerInputHandler.AttackActionCost; break;
                        case "Wait": apCostForDirectAction = PlayerInputHandler.WaitActionCost; break;
                        // Info, Skills, Items are handled by the outer if
                    }
                    costString = (apCostForDirectAction > 0) ? $"\n(AP Cost: {apCostForDirectAction})" : "\n(Free Action)";
                }
                trigger.tooltipText = $"{currentActionDef.displayName}{costString}";
            }

            buttonInstance.name = $"ActionButton_{currentActionDef.name}";
            buttonInstance.SetActive(true);
            _activeButtons.Add(buttonInstance);
        }
    }

    public void HideMenu()
    {
        foreach (GameObject button in _activeButtons)
        {
            if (button != null) Destroy(button);
        }
        _activeButtons.Clear();
        _currentUnitInternal = null;
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnActionButtonClicked(string actionName)
    {
        if (_currentUnitInternal == null) return;

        // ALL actions now simply invoke OnActionSelected.
        // The PlayerInputHandler (or other listeners) will decide how to react,
        // e.g., open the ItemSelectionPanelUI or SkillSelectionPanelUI.
        // DebugHelper.Log($"ActionMenuUI: Button '{actionName}' clicked for unit '{_currentUnitInternal?.unitName}'. Invoking OnActionSelected.", this.gameObject);
        OnActionSelected?.Invoke(_currentUnitInternal, actionName);

        // The ActionMenuUI usually hides after an action is selected and PlayerInputHandler takes over.
        // If PlayerInputHandler doesn't hide it, it might need to be hidden explicitly
        // when sub-panels like ItemSelectionPanelUI are shown.
        // For now, we assume PlayerInputHandler will manage hiding this menu when appropriate.
    }

    public bool IsVisible()
    {
        return gameObject.activeSelf && _activeButtons.Count > 0;
    }
}