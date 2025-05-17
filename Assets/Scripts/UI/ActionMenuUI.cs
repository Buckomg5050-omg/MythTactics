// ActionMenuUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System; 
using System.Collections.Generic;

// ... (ActionButtonSetup class, fields, Awake, GetAPCostForAction, UpdateAvailableActions remain the same) ...
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

    private List<GameObject> _activeButtons = new List<GameObject>();
    private Unit _currentUnitInternal; 

    public delegate void ActionSelectedHandler(Unit unit, string actionName);
    public static event ActionSelectedHandler OnActionSelected;

    private List<ActionDefinition> _runtimeAvailableActions = new List<ActionDefinition>();

    public struct ActionDefinition
    {
        public string name;
        public string displayName; 
        public bool canAfford;

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
    }

    private int GetAPCostForAction(string actionName)
    {
        switch (actionName)
        {
            case "Move": return PlayerInputHandler.MoveActionCost;
            case "Attack": return PlayerInputHandler.AttackActionCost;
            case "Skills": return PlayerInputHandler.SkillsActionCost; 
            case "Items": return PlayerInputHandler.ItemsActionCost;   
            case "Info": return PlayerInputHandler.InfoActionCost;     
            case "Wait": return PlayerInputHandler.WaitActionCost;
            default:
                // Debug.LogWarning($"ActionMenuUI: Unknown action name '{actionName}' for AP cost lookup. Assuming 0 AP.");
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

            if (actionSetup.actionName == "Skills")
            {
                bool hasSkills = unit.knownAbilities != null && unit.knownAbilities.Count > 0;
                if (!hasSkills) { continue; } 
            }
            
            bool canAffordAction;
            if (actionSetup.actionName == "Info")
            {
                canAffordAction = true; 
            }
            else
            {
                int apCost = GetAPCostForAction(actionSetup.actionName);
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
        float buttonWidth = prefabRect.sizeDelta.x * prefabRect.transform.localScale.x; // Assuming uniform scale
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
                buttonText.color = currentActionDef.canAfford ? affordableTextColor : unaffordableTextColor;
            }
            Button buttonComponent = buttonInstance.GetComponent<Button>();
            if (buttonComponent != null)
            {
                buttonComponent.interactable = currentActionDef.canAfford;
                string capturedActionName = currentActionDef.name;
                buttonComponent.onClick.AddListener(() => OnActionButtonClicked(capturedActionName));
            }

            // MODIFIED: Set tooltip text for the button
            TooltipTrigger trigger = buttonInstance.GetComponent<TooltipTrigger>();
            if (trigger != null)
            {
                int apCost = GetAPCostForAction(currentActionDef.name);
                string costString = (apCost > 0) ? $"\n(AP Cost: {apCost})" : "\n(Free Action)";
                if (currentActionDef.name == "Info" || currentActionDef.name == "Skills" || currentActionDef.name == "Items") costString = ""; // No AP cost display for menu openers

                trigger.tooltipText = $"{currentActionDef.displayName}{costString}";
            }
            // else 
            // {
            //     Debug.LogWarning($"Button {currentActionDef.name} is missing TooltipTrigger component.", buttonInstance);
            // }


            buttonInstance.name = $"ActionButton_{currentActionDef.name}";
            buttonInstance.SetActive(true);
            _activeButtons.Add(buttonInstance);
        }
    }

    public void HideMenu()
    {
        // ... (HideMenu logic remains the same) ...
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
        // DebugHelper.Log($"ActionMenuUI: Button '{actionName}' clicked for unit '{_currentUnitInternal?.unitName}'. Invoking OnActionSelected.", this.gameObject);
        OnActionSelected?.Invoke(_currentUnitInternal, actionName);
        // HideMenu(); // Hiding is now handled by PlayerInputHandler when state changes or action is processed
    }

    public bool IsVisible()
    {
        return gameObject.activeSelf && _activeButtons.Count > 0;
    }
}