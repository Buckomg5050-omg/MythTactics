// ActionMenuUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System; 
using System.Collections.Generic; // For List

// Keep this outside the ActionMenuUI class if you want to use it elsewhere,
// or inside if it's only for ActionMenuUI's Inspector.
[System.Serializable] // Make it show up in the Inspector
public class ActionButtonSetup
{
    public string actionName; // e.g., "Move", "Attack", "Skills", "Items", "Info", "Wait"
    // public Sprite icon; // Future: for an icon on the button
    // public string displayNameOverride; // Future: if "Move (1AP)" needs to be different from actionName "Move"
}

public class ActionMenuUI : MonoBehaviour
{
    [Header("Prefab & Settings")]
    [Tooltip("The prefab for a single action button in the radial menu.")]
    public GameObject radialActionButtonPrefab; 
    [Tooltip("The radius of the circle on which buttons will be placed.")]
    public float menuRadius = 100f; 
    [Tooltip("Offset from the unit's position to the center of the radial menu.")]
    public Vector2 menuCenterOffset = new Vector2(0, 50f); 

    [Header("Configurable Actions")]
    [Tooltip("Define the actions that can appear on the menu. Order matters for display.")]
    public List<ActionButtonSetup> configurableActions = new List<ActionButtonSetup>(); // This will be set in Inspector

    private List<GameObject> _activeButtons = new List<GameObject>();
    private Unit _currentUnitInternal; 

    public delegate void ActionSelectedHandler(Unit unit, string actionName);
    public static event ActionSelectedHandler OnActionSelected;

    // This list is now built dynamically from configurableActions
    private List<ActionDefinition> _runtimeAvailableActions = new List<ActionDefinition>();

    // ActionDefinition struct remains the same
    public struct ActionDefinition
    {
        public string name;
        public string displayName; // We'll use 'name' for display for now
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
        // Ensure it starts hidden, PlayerInputHandler will call ShowMenu
        gameObject.SetActive(false); 
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
                Debug.LogWarning($"ActionMenuUI: Unknown action name '{actionName}' for AP cost lookup. Assuming 0 AP.");
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

            // Specific condition for "Skills" button
            if (actionSetup.actionName == "Skills")
            {
                bool hasSkills = unit.knownAbilities != null && unit.knownAbilities.Count > 0;
                if (!hasSkills)
                {
                    continue; // Skip adding "Skills" button if unit has no skills
                }
            }
            // TODO: Add similar specific conditions for "Items" if needed (e.g., if unit has no items)

            int apCost = GetAPCostForAction(actionSetup.actionName);
            bool canAfford = unit.CanAffordAPForAction(apCost);
            
            // For now, displayName is same as name. Can be enhanced later.
            _runtimeAvailableActions.Add(new ActionDefinition(actionSetup.actionName, actionSetup.actionName, canAfford));
        }
    }

    public void ShowMenu(Unit unitToShowMenuFor, Vector2 unitScreenPosition) 
    {
        DebugHelper.Log($"ActionMenuUI.ShowMenu START: Called for unit '{unitToShowMenuFor?.unitName}'. Prefab assigned: {(radialActionButtonPrefab != null)}. Current visibility: {IsVisible()}", this.gameObject);

        // 1. Clear previous state and activate this GameObject
        foreach (GameObject button in _activeButtons) { if (button != null) Destroy(button); }
        _activeButtons.Clear();
        this._currentUnitInternal = null; 
        
        if (!gameObject.activeSelf) { gameObject.SetActive(true); }

        // 2. Assign the new unit
        this._currentUnitInternal = unitToShowMenuFor;
        DebugHelper.Log($"ActionMenuUI.ShowMenu MID: _currentUnitInternal is '{this._currentUnitInternal?.unitName}'. Passed unit was '{unitToShowMenuFor?.unitName}'. Prefab null? {(radialActionButtonPrefab == null)}", this.gameObject);

        if (this._currentUnitInternal == null || radialActionButtonPrefab == null) 
        {
            DebugHelper.LogWarning("ActionMenuUI.ShowMenu CRITICAL FAIL: _currentUnitInternal or radialActionButtonPrefab is null.", this.gameObject);
            if (this._currentUnitInternal == null) DebugHelper.LogWarning("    REASON: _currentUnitInternal IS NULL.", this.gameObject);
            if (radialActionButtonPrefab == null) DebugHelper.LogWarning("    REASON: radialActionButtonPrefab IS NULL. Check Inspector!", this.gameObject);
            HideMenu(); 
            return;
        }

        UpdateAvailableActions(this._currentUnitInternal); // Use the Inspector-configured actions

        int numActions = _runtimeAvailableActions.Count; // Use the count of dynamically built actions
        if (numActions == 0) 
        {
            DebugHelper.LogWarning("ActionMenuUI.ShowMenu: No available actions for unit after filtering. Hiding menu.", this.gameObject);
            HideMenu();
            return;
        }

        // ... (rest of the button instantiation and placement logic - unchanged) ...
        List<Vector2> buttonLocalPositions = new List<Vector2>();
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        RectTransform prefabRect = radialActionButtonPrefab.GetComponent<RectTransform>();
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

        DebugHelper.Log($"ActionMenuUI.ShowMenu: Instantiating {numActions} buttons around final center {finalMenuCenter}", this.gameObject);

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

            // Use _runtimeAvailableActions which was built from configurableActions
            ActionDefinition currentActionDef = _runtimeAvailableActions[i]; 
            TextMeshProUGUI buttonText = buttonInstance.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = currentActionDef.displayName; // Use displayName (which is same as name for now)
                buttonText.color = currentActionDef.canAfford ? Color.white : Color.gray;
            }

            Button buttonComponent = buttonInstance.GetComponent<Button>();
            if (buttonComponent != null)
            {
                buttonComponent.interactable = currentActionDef.canAfford;
                string capturedActionName = currentActionDef.name;
                buttonComponent.onClick.AddListener(() => OnActionButtonClicked(capturedActionName));
            }
            
            buttonInstance.name = $"ActionButton_{currentActionDef.name}";
            buttonInstance.SetActive(true);
            _activeButtons.Add(buttonInstance);
        }
        DebugHelper.Log($"ActionMenuUI.ShowMenu END: Successfully processed for unit '{this._currentUnitInternal?.unitName}'. Menu should be visible with {_activeButtons.Count} buttons.", this.gameObject);
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
        DebugHelper.Log($"ActionMenuUI: Button '{actionName}' clicked for unit '{_currentUnitInternal?.unitName}'. Invoking OnActionSelected.", this.gameObject);
        OnActionSelected?.Invoke(_currentUnitInternal, actionName);
    }

    public bool IsVisible()
    {
        return gameObject.activeSelf && _activeButtons.Count > 0;
    }
}