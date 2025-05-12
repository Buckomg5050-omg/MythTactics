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

    // MODIFIED: Defined new colors for better contrast
    [Header("Button Text Colors")]
    [Tooltip("Color for text on affordable/interactable action buttons.")]
    public Color affordableTextColor = Color.black; 
    [Tooltip("Color for text on unaffordable/non-interactable action buttons.")]
    public Color unaffordableTextColor = new Color(0.4f, 0.4f, 0.4f, 1f); // Darker grey (R:102, G:102, B:102)

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
        // ... (existing ShowMenu setup logic) ...
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        
        foreach (GameObject button in _activeButtons) { if (button != null) Destroy(button); }
        _activeButtons.Clear();
        
        this._currentUnitInternal = unitToShowMenuFor;

        if (this._currentUnitInternal == null || radialActionButtonPrefab == null) 
        {
            DebugHelper.LogWarning("ActionMenuUI.ShowMenu CRITICAL FAIL: _currentUnitInternal or radialActionButtonPrefab is null.", this.gameObject);
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
        float buttonWidth = prefabRect.sizeDelta.x * prefabRect.transform.localScale.x;
        float buttonHeight = prefabRect.sizeDelta.y * prefabRect.transform.localScale.y;
        float angleStep = 360f / numActions;
        float startAngleOffset = 90f; 
        if (numActions == 1) startAngleOffset = 0; 
        else if (numActions > 4) startAngleOffset = 90f - (angleStep / 2f); 
        for (int i = 0; i < numActions; i++) { float angle = i * angleStep; float currentAngleRad = Mathf.Deg2Rad * (angle + startAngleOffset); Vector2 localPos = new Vector2(Mathf.Cos(currentAngleRad), Mathf.Sin(currentAngleRad)) * menuRadius; buttonLocalPositions.Add(localPos); minX = Mathf.Min(minX, localPos.x - buttonWidth / 2f); minY = Mathf.Min(minY, localPos.y - buttonHeight / 2f); maxX = Mathf.Max(maxX, localPos.x + buttonWidth / 2f); maxY = Mathf.Max(maxY, localPos.y + buttonHeight / 2f); }
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
                // MODIFIED: Use Inspector-configurable colors
                buttonText.color = currentActionDef.canAfford ? affordableTextColor : unaffordableTextColor; 
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