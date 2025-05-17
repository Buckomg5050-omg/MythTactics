using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections; 

public class CombatLogUI : MonoBehaviour
{
    [Header("UI References")]
    public Button toggleButton;
    public GameObject logDisplayPanel;
    public ScrollRect logScrollRect;
    public RectTransform logContentContainer;
    public GameObject logMessagePrefab;

    [Header("Filter UI")] 
    public GameObject filterTogglesPanel; 
    public Toggle filterSystemToggle;
    public Toggle filterTurnFlowToggle;
    public Toggle filterCombatActionToggle;
    public Toggle filterStatusChangeToggle;
    public Toggle filterMovementToggle;

    [Header("Settings")]
    public int maxMessages = 100;

    private List<LogMessageEntry> _logMessageEntries = new List<LogMessageEntry>();
    private bool _isLogVisible = false; 
    private Coroutine _scrollCoroutine = null; 

    private struct LogMessageEntry
    {
        public GameObject gameObject;
        public LogMessageType type;
        public TextMeshProUGUI textComponent; 

        public LogMessageEntry(GameObject go, LogMessageType messageType, TextMeshProUGUI tmp)
        {
            gameObject = go;
            type = messageType;
            textComponent = tmp;
        }
    }

    void Awake()
    {
        if (toggleButton == null) Debug.LogError("CombatLogUI: ToggleButton not assigned!", this);
        if (logDisplayPanel == null) Debug.LogError("CombatLogUI: LogDisplayPanel not assigned!", this);
        if (logScrollRect == null) Debug.LogError("CombatLogUI: LogScrollRect not assigned!", this);
        if (logContentContainer == null) Debug.LogError("CombatLogUI: LogContentContainer not assigned!", this);
        if (logMessagePrefab == null) Debug.LogError("CombatLogUI: LogMessagePrefab not assigned!", this);
        
        if (filterTogglesPanel == null) Debug.LogWarning("CombatLogUI: FilterTogglesPanel not assigned.", this);

        if (filterSystemToggle == null) Debug.LogWarning("CombatLogUI: FilterSystemToggle not assigned.", this);
        else filterSystemToggle.onValueChanged.AddListener(delegate { OnFilterChanged(); });

        if (filterTurnFlowToggle == null) Debug.LogWarning("CombatLogUI: FilterTurnFlowToggle not assigned.", this);
        else filterTurnFlowToggle.onValueChanged.AddListener(delegate { OnFilterChanged(); });
        
        if (filterCombatActionToggle == null) Debug.LogWarning("CombatLogUI: FilterCombatActionToggle not assigned.", this);
        else filterCombatActionToggle.onValueChanged.AddListener(delegate { OnFilterChanged(); });

        if (filterStatusChangeToggle == null) Debug.LogWarning("CombatLogUI: FilterStatusChangeToggle not assigned.", this);
        else filterStatusChangeToggle.onValueChanged.AddListener(delegate { OnFilterChanged(); });

        if (filterMovementToggle == null) Debug.LogWarning("CombatLogUI: FilterMovementToggle not assigned.", this);
        else filterMovementToggle.onValueChanged.AddListener(delegate { OnFilterChanged(); });

        if (toggleButton != null)
        {
            // MODIFIED: Added direct log on click
            toggleButton.onClick.AddListener(() => {
                Debug.Log("<<<<< CombatLogUI ToggleButton CLICKED >>>>>", this); 
                ToggleLogVisibility();
            });
        }

        _isLogVisible = false; 
        if (logDisplayPanel != null) logDisplayPanel.SetActive(false);
        if (filterTogglesPanel != null) filterTogglesPanel.SetActive(false);
        Debug.Log($"CombatLogUI.Awake: Initial _isLogVisible: {_isLogVisible}, logDisplayPanel.activeSelf: {logDisplayPanel?.activeSelf}, filterTogglesPanel.activeSelf: {filterTogglesPanel?.activeSelf}", this);
    }

    void OnEnable()
    {
        CombatLogger.OnLogMessage += HandleNewLogMessage; 
    }

    void OnDisable()
    {
        CombatLogger.OnLogMessage -= HandleNewLogMessage; 
    }

    public void ToggleLogVisibility()
    {
        Debug.Log($"CombatLogUI.ToggleLogVisibility: Called. Current _isLogVisible BEFORE toggle: {_isLogVisible}", this);
        _isLogVisible = !_isLogVisible; 
        Debug.Log($"CombatLogUI.ToggleLogVisibility: _isLogVisible AFTER toggle: {_isLogVisible}", this);

        if (logDisplayPanel != null)
        {
            Debug.Log($"CombatLogUI.ToggleLogVisibility: Setting logDisplayPanel active state to: {_isLogVisible}. Current activeSelf: {logDisplayPanel.activeSelf}", this);
            logDisplayPanel.SetActive(_isLogVisible);
            Debug.Log($"CombatLogUI.ToggleLogVisibility: logDisplayPanel.activeSelf AFTER SetActive: {logDisplayPanel.activeSelf}", this);
        }
        if (filterTogglesPanel != null) 
        {
            Debug.Log($"CombatLogUI.ToggleLogVisibility: Setting filterTogglesPanel active state to: {_isLogVisible}. Current activeSelf: {filterTogglesPanel.activeSelf}", this);
            filterTogglesPanel.SetActive(_isLogVisible);
            Debug.Log($"CombatLogUI.ToggleLogVisibility: filterTogglesPanel.activeSelf AFTER SetActive: {filterTogglesPanel.activeSelf}", this);
        }

        if (_isLogVisible) 
        {
            Debug.Log("CombatLogUI.ToggleLogVisibility: Log is now visible. Applying filters and scrolling.", this);
            ApplyFiltersToExistingMessages(); 
            ScrollToBottom(); 
        }
        else 
        {
            Debug.Log("CombatLogUI.ToggleLogVisibility: Log is now hidden. Stopping scroll coroutine if active.", this);
            if (_scrollCoroutine != null) 
            {
                StopCoroutine(_scrollCoroutine);
                _scrollCoroutine = null;
                Debug.Log("CombatLogUI.ToggleLogVisibility: Scroll coroutine stopped.", this);
            }
        }
    }

    private void HandleNewLogMessage(string message, Color color, LogMessageType type) 
    {
        if (logMessagePrefab == null || logContentContainer == null) return;

        if (_logMessageEntries.Count >= maxMessages)
        {
            LogMessageEntry oldestEntry = _logMessageEntries[0];
            _logMessageEntries.RemoveAt(0);
            Destroy(oldestEntry.gameObject);
        }

        GameObject newMessageGO = Instantiate(logMessagePrefab, logContentContainer);
        TextMeshProUGUI messageText = newMessageGO.GetComponent<TextMeshProUGUI>();
        
        if (messageText != null)
        {
            messageText.text = message;
            messageText.color = color;
        }
        else
        {
            Debug.LogError("CombatLogUI: LogMessagePrefab is missing TextMeshProUGUI component!", newMessageGO);
        }
        
        LogMessageEntry newEntry = new LogMessageEntry(newMessageGO, type, messageText);
        _logMessageEntries.Add(newEntry);

        bool shouldBeVisibleBasedOnFilters = IsMessageVisible(type);
        newMessageGO.SetActive(shouldBeVisibleBasedOnFilters);

        if (_isLogVisible && shouldBeVisibleBasedOnFilters) 
        {
            ScrollToBottom();
        }
    }
    
    private void OnFilterChanged()
    {
        if (_isLogVisible) 
        {
            ApplyFiltersToExistingMessages();
            ScrollToBottom(); 
        }
    }

    private void ApplyFiltersToExistingMessages()
    {
        if (logContentContainer == null) return;

        bool changedVisibility = false;
        foreach (LogMessageEntry entry in _logMessageEntries)
        {
            if (entry.gameObject != null)
            {
                bool currentActiveState = entry.gameObject.activeSelf;
                bool newActiveState = IsMessageVisible(entry.type);
                if (currentActiveState != newActiveState)
                {
                    entry.gameObject.SetActive(newActiveState);
                    changedVisibility = true;
                }
            }
        }
        
        if (changedVisibility && logContentContainer.gameObject.activeInHierarchy) 
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(logContentContainer);
        }
    }

    private bool IsMessageVisible(LogMessageType type)
    {
        switch (type)
        {
            case LogMessageType.System:         
                return filterSystemToggle != null ? filterSystemToggle.isOn : true; 
            case LogMessageType.TurnFlow:       
                return filterTurnFlowToggle != null ? filterTurnFlowToggle.isOn : true;
            case LogMessageType.CombatAction:   
                return filterCombatActionToggle != null ? filterCombatActionToggle.isOn : true;
            case LogMessageType.StatusChange:   
                return filterStatusChangeToggle != null ? filterStatusChangeToggle.isOn : true;
            case LogMessageType.Movement:       
                return filterMovementToggle != null ? filterMovementToggle.isOn : true;
            default: 
                return true; 
        }
    }

    private void ScrollToBottom()
    {
        if (logScrollRect == null || !logScrollRect.gameObject.activeInHierarchy) return;
        
        if (_scrollCoroutine != null) 
        {
            StopCoroutine(_scrollCoroutine);
        }
        _scrollCoroutine = StartCoroutine(ScrollToBottomCoroutine());
    }

    private IEnumerator ScrollToBottomCoroutine() 
    {
        yield return new WaitForEndOfFrame(); 
        
        if (logScrollRect != null && logScrollRect.gameObject.activeInHierarchy)
        {
            logScrollRect.verticalNormalizedPosition = 0f;
        }
        _scrollCoroutine = null; 
    }
}