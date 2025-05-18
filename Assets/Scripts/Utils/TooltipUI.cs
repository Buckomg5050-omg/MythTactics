// TooltipUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance { get; private set; }

    [Header("UI References")]
    public RectTransform tooltipPanel; 
    public TextMeshProUGUI tooltipText;
    public Image tooltipBackground; 

    [Header("Settings")]
    public Vector2 offset = new Vector2(15f, -15f);
    public Vector2 textPadding = new Vector2(10f, 10f); 

    private Canvas _parentCanvas;
    private RectTransform _canvasRectTransform;
    private Camera _uiCamera; 
    private string _currentTooltipText = ""; 

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Optional: If you want this to persist across scenes and only have one.
            // DontDestroyOnLoad(gameObject); 
        }
        else if (Instance != this)
        {
            UnityEngine.Debug.LogWarning("Duplicate TooltipUI instance found. Destroying this one.", this);
            Destroy(gameObject);
            return;
        }

        if (tooltipPanel == null) UnityEngine.Debug.LogError("TooltipUI: TooltipPanel not assigned!", this);
        if (tooltipText == null) UnityEngine.Debug.LogError("TooltipUI: TooltipText not assigned!", this);
        
        _parentCanvas = GetComponentInParent<Canvas>();
        if (_parentCanvas == null)
        {
            UnityEngine.Debug.LogError("TooltipUI: Must be a child of a Canvas for proper operation!", this);
        }
        else
        {
            _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
            _uiCamera = _parentCanvas.worldCamera; 
        }

        if (tooltipPanel != null) 
        {
            tooltipPanel.gameObject.SetActive(false); 
        }
    }

    public void ShowTooltip(string text, Vector2 screenPosition)
    {
        if (tooltipPanel == null || tooltipText == null) return;

        if (string.IsNullOrEmpty(text))
        {
            HideTooltip(); 
            return;
        }

        bool wasAlreadyVisible = tooltipPanel.gameObject.activeSelf;
        bool textChanged = (_currentTooltipText != text);
        _currentTooltipText = text; 

        tooltipText.text = text;
        
        if (tooltipBackground != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipText.rectTransform);
            Vector2 textSize = new Vector2(tooltipText.preferredWidth, tooltipText.preferredHeight);
            tooltipBackground.rectTransform.sizeDelta = textSize + textPadding * 2; 
            tooltipPanel.sizeDelta = tooltipBackground.rectTransform.sizeDelta; 
        }
        else 
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipText.rectTransform);
            tooltipPanel.sizeDelta = new Vector2(tooltipText.preferredWidth, tooltipText.preferredHeight) + textPadding * 2;
        }
        
        tooltipPanel.gameObject.SetActive(true); 

        // MODIFIED: Ensure the *TooltipUI's root GameObject* is rendered on top within its parent canvas's children
        if (this.transform.parent != null) // Check if the TooltipUI GameObject itself has a parent
        {
            this.transform.SetAsLastSibling(); // This moves the entire TooltipUI object to the end of its parent's child list
        }
        // End of Modification

        InternalUpdatePosition(screenPosition); 
    }

    // ... HideTooltip, InternalUpdatePosition, ClampToScreen, LateUpdate remain the same ...
    public void HideTooltip()
    {
        if (tooltipPanel != null && tooltipPanel.gameObject.activeSelf)
        {
            tooltipPanel.gameObject.SetActive(false);
            _currentTooltipText = ""; 
        }
    }

    private void InternalUpdatePosition(Vector2 screenPosition)
    {
        if (tooltipPanel == null) return;
        if (_parentCanvas == null || _canvasRectTransform == null)
        {
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null)
            {
                _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
                _uiCamera = _parentCanvas.worldCamera;
            }
            else
            {
                return; 
            }
        }

        Vector2 anchoredPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRectTransform, 
            screenPosition,       
            _uiCamera,            
            out anchoredPosition);

        tooltipPanel.anchoredPosition = anchoredPosition + offset;
        ClampToScreen();
    }

    private void ClampToScreen()
    {
        if (tooltipPanel == null || _parentCanvas == null || _canvasRectTransform == null) return;

        Vector3[] panelCorners = new Vector3[4];
        tooltipPanel.GetWorldCorners(panelCorners); 

        float currentCanvasScale = _parentCanvas.scaleFactor; 
        if (currentCanvasScale <= 0) currentCanvasScale = 1f; 

        float minScreenX = 0;
        float maxScreenX = Screen.width;
        float minScreenY = 0;
        float maxScreenY = Screen.height;

        Vector2 screenSpaceOffset = Vector2.zero;

        Vector2 bottomLeftScreen = RectTransformUtility.WorldToScreenPoint(_uiCamera, panelCorners[0]);
        Vector2 topRightScreen = RectTransformUtility.WorldToScreenPoint(_uiCamera, panelCorners[2]);
        Vector2 topLeftScreen = RectTransformUtility.WorldToScreenPoint(_uiCamera, panelCorners[1]);

        if (bottomLeftScreen.x < minScreenX) { screenSpaceOffset.x = (minScreenX - bottomLeftScreen.x); }
        else if (topRightScreen.x > maxScreenX) { screenSpaceOffset.x = (maxScreenX - topRightScreen.x); }

        if (bottomLeftScreen.y < minScreenY) { screenSpaceOffset.y = (minScreenY - bottomLeftScreen.y); }
        else if (topLeftScreen.y > maxScreenY) { screenSpaceOffset.y = (maxScreenY - topLeftScreen.y); } 
        
        tooltipPanel.anchoredPosition += screenSpaceOffset / currentCanvasScale;
    }

    void LateUpdate()
    {
        if (tooltipPanel != null && tooltipPanel.gameObject.activeSelf)
        {
            InternalUpdatePosition(Input.mousePosition);
        }
    }
}