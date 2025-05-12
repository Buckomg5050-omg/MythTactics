using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;

public class CombatLogUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Button to toggle the visibility of the log panel.")]
    public Button toggleButton;
    [Tooltip("The main panel that contains the log content. This will be shown/hidden.")]
    public GameObject logDisplayPanel;
    [Tooltip("The ScrollRect containing the log messages.")]
    public ScrollRect logScrollRect;
    [Tooltip("The Transform (Content object) inside the ScrollRect where log message Text objects will be parented.")]
    public RectTransform logContentContainer;
    [Tooltip("Prefab for a single log message TextMeshProUGUI element.")]
    public GameObject logMessagePrefab; // This should be a prefab with just a TextMeshProUGUI component

    [Header("Settings")]
    [Tooltip("Maximum number of messages to keep in the log.")]
    public int maxMessages = 100;

    private List<GameObject> _logMessageObjects = new List<GameObject>();
    private bool _isLogVisible = false;
    private StringBuilder _sb = new StringBuilder(); // For formatting messages

    void Awake()
    {
        if (toggleButton == null) Debug.LogError("CombatLogUI: ToggleButton not assigned!", this);
        if (logDisplayPanel == null) Debug.LogError("CombatLogUI: LogDisplayPanel not assigned!", this);
        if (logScrollRect == null) Debug.LogError("CombatLogUI: LogScrollRect not assigned!", this);
        if (logContentContainer == null) Debug.LogError("CombatLogUI: LogContentContainer not assigned!", this);
        if (logMessagePrefab == null) Debug.LogError("CombatLogUI: LogMessagePrefab not assigned!", this);

        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleLogVisibility);
        }

        // Start with the log panel hidden
        if (logDisplayPanel != null)
        {
            logDisplayPanel.SetActive(_isLogVisible);
        }
    }

    void OnEnable()
    {
        // Subscribe to an event from a CombatLogger system
        CombatLogger.OnLogMessage += AddMessage;
    }

    void OnDisable()
    {
        // Unsubscribe
        CombatLogger.OnLogMessage -= AddMessage;
    }

    public void ToggleLogVisibility()
    {
        _isLogVisible = !_isLogVisible;
        if (logDisplayPanel != null)
        {
            logDisplayPanel.SetActive(_isLogVisible);
            if (_isLogVisible)
            {
                ScrollToBottom();
            }
        }
        // Optionally change toggle button text/sprite (e.g., "Show Log" / "Hide Log")
    }

    public void AddMessage(string message, Color color) // Modified to accept color
    {
        if (logMessagePrefab == null || logContentContainer == null) return;

        if (_logMessageObjects.Count >= maxMessages)
        {
            GameObject oldestMessage = _logMessageObjects[0];
            _logMessageObjects.RemoveAt(0);
            Destroy(oldestMessage);
        }

        GameObject newMessageGO = Instantiate(logMessagePrefab, logContentContainer);
        TextMeshProUGUI messageText = newMessageGO.GetComponent<TextMeshProUGUI>();
        if (messageText != null)
        {
            messageText.text = message;
            messageText.color = color; // Apply color
        }
        else
        {
            Debug.LogError("CombatLogUI: LogMessagePrefab is missing TextMeshProUGUI component!", newMessageGO);
        }
        
        _logMessageObjects.Add(newMessageGO);

        if (_isLogVisible) // Only scroll if visible
        {
            ScrollToBottom();
        }
    }

    private void ScrollToBottom()
    {
        if (logScrollRect == null) return;
        // Canvas.ForceUpdateCanvases(); // May not be needed with coroutine
        // logScrollRect.verticalNormalizedPosition = 0f;
        StartCoroutine(ScrollToBottomNextFrame());
    }

    private IEnumerator<object> ScrollToBottomNextFrame()
    {
        yield return null; // Wait for one frame for layout to update
        if (logScrollRect != null)
        {
            logScrollRect.verticalNormalizedPosition = 0f;
        }
    }
}