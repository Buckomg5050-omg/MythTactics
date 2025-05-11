using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Text; 

public class UnitInfoPanelUI : MonoBehaviour
{
    [Header("UI References - General")]
    [Tooltip("The main panel GameObject that will be shown/hidden.")]
    public GameObject mainPanel;
    [Tooltip("Button to close the panel.")]
    public Button closeButton;

    [Header("UI References - Unit Info")]
    [Tooltip("Text element to display the unit's name.")]
    public TextMeshProUGUI unitNameText;

    [Header("Primary Attributes Texts")]
    public TextMeshProUGUI coreText;
    public TextMeshProUGUI echoText;
    public TextMeshProUGUI pulseText;
    public TextMeshProUGUI sparkText;
    public TextMeshProUGUI glimmerText;
    public TextMeshProUGUI auraText;

    [Header("Derived Stats Texts")]
    public TextMeshProUGUI vitalityPointsText; 
    public TextMeshProUGUI manaPointsText;     
    public TextMeshProUGUI actionPointsText;   
    public TextMeshProUGUI movementRangeText;

    private Unit _currentUnit;
    private StringBuilder _sb = new StringBuilder(); 

    public static event Action OnInfoPanelClosedByButton;

    void Awake()
    {
        if (mainPanel == null)
        {
            Debug.LogWarning("UnitInfoPanelUI.Awake: MainPanel reference is not set in Inspector. Attempting to use this.gameObject.", this);
            mainPanel = this.gameObject;
        }

        if (mainPanel == null)
        {
            Debug.LogError("UnitInfoPanelUI.Awake: mainPanel is NULL even after attempting to default. The panel cannot be controlled. Disabling script.", this);
            enabled = false;
            return;
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HandleCloseButtonPressed);
        }
    }

    private void HandleCloseButtonPressed()
    {
        OnInfoPanelClosedByButton?.Invoke();
    }

    public void ShowPanel(Unit unitToShowInfoFor)
    {
        if (mainPanel == null) {
            Debug.LogError("UnitInfoPanelUI.ShowPanel: mainPanel is null. Cannot show.", this);
            return;
        }

        _currentUnit = unitToShowInfoFor;
        if (_currentUnit == null)
        {
            Debug.LogError("UnitInfoPanelUI.ShowPanel: unitToShowInfoFor is null. Hiding panel.", this);
            HidePanelInternally();
            return;
        }

        PopulatePanelData();

        if (!mainPanel.activeSelf)
        {
            mainPanel.SetActive(true);
        }
        
        if (!mainPanel.activeSelf) 
        {
            Debug.LogError($"UnitInfoPanelUI: TRIED TO SETACTIVE(TRUE) ON '{mainPanel.name}' BUT IT IS STILL NOT ACTIVE! Check parent GameObjects' active state and other scripts.", this);
        }
    }

    public void HidePanel()
    {
        HidePanelInternally();
    }

    private void HidePanelInternally()
    {
        if (mainPanel == null) return;

        if (mainPanel.activeSelf)
        {
            mainPanel.SetActive(false);
        }
        _currentUnit = null;
    }

    public bool IsVisible()
    {
        if (mainPanel == null) return false;
        return mainPanel.activeSelf;
    }

    private void PopulatePanelData()
    {
        if (_currentUnit == null)
        {
            Debug.LogError("UnitInfoPanelUI.PopulatePanelData: _currentUnit is null.", this);
            return;
        }
        if (_currentUnit.Stats == null)
        {
            Debug.LogError($"UnitInfoPanelUI.PopulatePanelData: _currentUnit '{_currentUnit.unitName}' has no Stats component.", this);
            return;
        }

        // --- Unit Name ---
        if (unitNameText != null)
        {
            unitNameText.text = _currentUnit.unitName;
        }

        // --- Primary Attributes ---
        UnitPrimaryAttributes baseAttributes = _currentUnit.Stats.currentAttributes; 
        UnitPrimaryAttributes effectiveAttributes = _currentUnit.Stats.EffectiveAttributes;

        if (coreText != null) coreText.text = $"Core: {effectiveAttributes.Core} ({baseAttributes.Core})";
        if (echoText != null) echoText.text = $"Echo: {effectiveAttributes.Echo} ({baseAttributes.Echo})";
        if (pulseText != null) pulseText.text = $"Pulse: {effectiveAttributes.Pulse} ({baseAttributes.Pulse})";
        if (sparkText != null) sparkText.text = $"Spark: {effectiveAttributes.Spark} ({baseAttributes.Spark})";
        if (glimmerText != null) glimmerText.text = $"Glimmer: {effectiveAttributes.Glimmer} ({baseAttributes.Glimmer})";
        if (auraText != null) auraText.text = $"Aura: {effectiveAttributes.Aura} ({baseAttributes.Aura})";

        // --- Derived Stats ---
        _sb.Clear();
        if (vitalityPointsText != null)
        {
            _sb.Append("VP: ").Append(_currentUnit.Stats.currentVitalityPoints).Append(" / ").Append(_currentUnit.Stats.MaxVitalityPoints);
            vitalityPointsText.text = _sb.ToString();
            _sb.Clear();
        }

        if (manaPointsText != null)
        {
            _sb.Append("MP: ").Append(_currentUnit.Stats.currentManaPoints).Append(" / ").Append(_currentUnit.Stats.MaxManaPoints);
            manaPointsText.text = _sb.ToString();
            _sb.Clear();
        }
        
        if (actionPointsText != null)
        {
            _sb.Append("AP: ").Append(_currentUnit.CurrentActionPoints).Append(" / ").Append(_currentUnit.MaxActionPoints);
            actionPointsText.text = _sb.ToString();
            _sb.Clear();
        }

        if (movementRangeText != null)
        {
            if (_currentUnit.Movement != null)
            {
                // CORRECTED: Use the existing CalculatedMoveRange property from UnitMovement
                _sb.Append("Move: ").Append(_currentUnit.Movement.CalculatedMoveRange);
                movementRangeText.text = _sb.ToString();
                _sb.Clear();
            }
            else
            {
                movementRangeText.text = "Move: N/A";
            }
        }
    }

    public void RefreshData()
    {
        if (IsVisible() && _currentUnit != null)
        {
            PopulatePanelData();
        }
    }
}