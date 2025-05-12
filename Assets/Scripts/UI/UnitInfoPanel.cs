using UnityEngine;
using UnityEngine.UI; 
using TMPro;
using System;
using System.Text;
using System.Collections.Generic; 
using System.Collections; 

public class UnitInfoPanelUI : MonoBehaviour
{
    [Header("UI References - General")]
    public GameObject mainPanel;
    public Button closeButton;

    [Header("UI References - Unit Info")]
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

    [Header("Status Effects UI")]
    public GameObject statusEffectsListPanel; 
    public GameObject statusEffectEntryPrefab;

    private Unit _currentUnit;
    private StringBuilder _sb = new StringBuilder();
    private List<GameObject> _activeStatusEffectEntries = new List<GameObject>(); 
    private Coroutine _activateLayoutCoroutine = null; 

    public static event Action OnInfoPanelClosedByButton;

    void Awake()
    {
        if (mainPanel == null)
        {
            // Debug.LogWarning("UnitInfoPanelUI.Awake: MainPanel reference is not set in Inspector. Attempting to use this.gameObject.", this);
            mainPanel = this.gameObject;
        }

        if (mainPanel == null)
        {
            Debug.LogError("UnitInfoPanelUI.Awake: mainPanel is NULL. Disabling script.", this);
            enabled = false;
            return;
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HandleCloseButtonPressed);
        }

        if (statusEffectsListPanel == null)
        {
            Debug.LogWarning("UnitInfoPanelUI.Awake: StatusEffectsListPanel not assigned. Status effects will not be displayed.", this);
        }
        if (statusEffectEntryPrefab == null)
        {
            Debug.LogWarning("UnitInfoPanelUI.Awake: StatusEffectEntryPrefab not assigned. Status effects will not be displayed.", this);
        }

        if (statusEffectsListPanel != null && statusEffectsListPanel.activeSelf)
        {
            statusEffectsListPanel.SetActive(false);
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
            // Debug.LogError("UnitInfoPanelUI.ShowPanel: unitToShowInfoFor is null. Hiding panel.", this);
            HidePanelInternally(); 
            return;
        }

        if (!mainPanel.activeSelf)
        {
            mainPanel.SetActive(true);
        }

        PopulatePanelData(); 
    }

    public void HidePanel()
    {
        HidePanelInternally();
    }

    private void HidePanelInternally()
    {
        if (_activateLayoutCoroutine != null) 
        {
            StopCoroutine(_activateLayoutCoroutine);
            _activateLayoutCoroutine = null;
        }

        if (mainPanel == null) return;

        if (mainPanel.activeSelf)
        {
            mainPanel.SetActive(false);
        }
        if (statusEffectsListPanel != null && statusEffectsListPanel.activeSelf)
        {
            statusEffectsListPanel.SetActive(false);
        }
        ClearStatusEffectEntries(); 
        _currentUnit = null;
    }

    public bool IsVisible()
    {
        if (mainPanel == null) return false;
        return mainPanel.activeSelf;
    }

    private void PopulatePanelData()
    {
        if (!mainPanel.activeInHierarchy && gameObject.activeInHierarchy) // Check if this script's GO is active
        {
            // Debug.LogWarning("UnitInfoPanelUI.PopulatePanelData: Called while mainPanel is not active in hierarchy but this GO is. Some UI updates (like coroutines for layout) might not initiate if mainPanel is this.gameObject and it was just activated.", this);
        }
        
        if (_currentUnit == null || _currentUnit.Stats == null)
        {
            if (unitNameText != null) unitNameText.text = "N/A";
            if (coreText != null) coreText.text = "Core: N/A";
            if (echoText != null) echoText.text = "Echo: N/A";
            if (pulseText != null) pulseText.text = "Pulse: N/A";
            if (sparkText != null) sparkText.text = "Spark: N/A";
            if (glimmerText != null) glimmerText.text = "Glimmer: N/A";
            if (auraText != null) auraText.text = "Aura: N/A";
            if (vitalityPointsText != null) vitalityPointsText.text = "VP: N/A";
            if (manaPointsText != null) manaPointsText.text = "MP: N/A";
            if (actionPointsText != null) actionPointsText.text = "AP: N/A";
            if (movementRangeText != null) movementRangeText.text = "Move: N/A";
            
            if (statusEffectsListPanel != null) statusEffectsListPanel.SetActive(false);
            ClearStatusEffectEntries();
            return;
        }

        if (unitNameText != null) unitNameText.text = _currentUnit.unitName;

        UnitPrimaryAttributes baseAttributes = _currentUnit.Stats.currentAttributes;
        UnitPrimaryAttributes effectiveAttributes = _currentUnit.Stats.EffectiveAttributes;

        if (coreText != null) coreText.text = $"Core: {effectiveAttributes.Core} ({baseAttributes.Core})";
        if (echoText != null) echoText.text = $"Echo: {effectiveAttributes.Echo} ({baseAttributes.Echo})";
        if (pulseText != null) pulseText.text = $"Pulse: {effectiveAttributes.Pulse} ({baseAttributes.Pulse})";
        if (sparkText != null) sparkText.text = $"Spark: {effectiveAttributes.Spark} ({baseAttributes.Spark})";
        if (glimmerText != null) glimmerText.text = $"Glimmer: {effectiveAttributes.Glimmer} ({baseAttributes.Glimmer})";
        if (auraText != null) auraText.text = $"Aura: {effectiveAttributes.Aura} ({baseAttributes.Aura})";

        _sb.Clear();
        if (vitalityPointsText != null)
        {
            _sb.Append("VP: ").Append(_currentUnit.Stats.currentVitalityPoints).Append(" / ").Append(_currentUnit.Stats.MaxVitalityPoints);
            vitalityPointsText.text = _sb.ToString(); _sb.Clear();
        }
        if (manaPointsText != null)
        {
            _sb.Append("MP: ").Append(_currentUnit.Stats.currentManaPoints).Append(" / ").Append(_currentUnit.Stats.MaxManaPoints);
            manaPointsText.text = _sb.ToString(); _sb.Clear();
        }
        if (actionPointsText != null)
        {
            _sb.Append("AP: ").Append(_currentUnit.CurrentActionPoints).Append(" / ").Append(_currentUnit.MaxActionPoints);
            actionPointsText.text = _sb.ToString(); _sb.Clear();
        }
        if (movementRangeText != null)
        {
            if (_currentUnit.Movement != null)
            {
                _sb.Append("Move: ").Append(_currentUnit.Movement.CalculatedMoveRange);
                movementRangeText.text = _sb.ToString(); _sb.Clear();
            }
            else { movementRangeText.text = "Move: N/A"; }
        }

        PopulateStatusEffects();
    }

    private void ClearStatusEffectEntries()
    {
        foreach (GameObject entryGO in _activeStatusEffectEntries)
        {
            if (entryGO != null) Destroy(entryGO);
        }
        _activeStatusEffectEntries.Clear();
    }

    private void PopulateStatusEffects()
    {
        ClearStatusEffectEntries(); 

        if (statusEffectsListPanel == null || statusEffectEntryPrefab == null || _currentUnit == null || _currentUnit.Stats == null)
        {
            if (statusEffectsListPanel != null) statusEffectsListPanel.SetActive(false);
            return;
        }

        IReadOnlyList<ActiveStatusEffect> effects = _currentUnit.Stats.ActiveEffects;

        statusEffectsListPanel.SetActive(false); 

        if (effects.Count > 0)
        {
            foreach (ActiveStatusEffect effect in effects)
            {
                if (effect == null || effect.BaseEffect == null) continue;

                GameObject entryInstance = Instantiate(statusEffectEntryPrefab);
                if (statusEffectsListPanel.transform != null)
                {
                    entryInstance.transform.SetParent(statusEffectsListPanel.transform, false); 
                }
                else
                {
                     Debug.LogError("PopulateStatusEffects: statusEffectsListPanel.transform is null! Cannot parent status effect entry.", statusEffectsListPanel);
                     Destroy(entryInstance); 
                     continue;
                }
                entryInstance.transform.localScale = Vector3.one;

                StatusEffectEntryUI entryUI = entryInstance.GetComponent<StatusEffectEntryUI>();
                if (entryUI != null)
                {
                    entryUI.Populate(effect);
                }
                else
                {
                    Debug.LogError("PopulateStatusEffects: StatusEffectEntryPrefab is missing StatusEffectEntryUI script.", statusEffectEntryPrefab);
                    Destroy(entryInstance); 
                }
                _activeStatusEffectEntries.Add(entryInstance);
            }

            if (_activateLayoutCoroutine != null)
            {
                StopCoroutine(_activateLayoutCoroutine);
            }
            if(gameObject.activeInHierarchy) 
            {
                _activateLayoutCoroutine = StartCoroutine(ActivateAndRebuildLayout(statusEffectsListPanel));
            }
            else 
            {
                // Debug.LogWarning("UnitInfoPanelUI.PopulateStatusEffects: Main panel (this.gameObject) is inactive. Activating status panel directly without coroutine delay.", this);
                statusEffectsListPanel.SetActive(true);
                RectTransform panelRectTransform = statusEffectsListPanel.GetComponent<RectTransform>();
                if (panelRectTransform != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(panelRectTransform);
                }
            }
        }
    }

    private IEnumerator ActivateAndRebuildLayout(GameObject panelToActivate)
    {
        yield return null; 

        if (panelToActivate != null && _currentUnit != null && _currentUnit.Stats != null && _currentUnit.Stats.ActiveEffects.Count > 0)
        {
            panelToActivate.SetActive(true);
            RectTransform panelRectTransform = panelToActivate.GetComponent<RectTransform>();
            if (panelRectTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRectTransform);
            }
            else
            {
                Debug.LogError("ActivateAndRebuildLayout: panelToActivate does not have a RectTransform.", panelToActivate);
            }
        }
        else if (panelToActivate != null) 
        {
            panelToActivate.SetActive(false); 
        }
        _activateLayoutCoroutine = null; 
    }

    public void RefreshData()
    {
        if (IsVisible() && _currentUnit != null)
        {
            PopulatePanelData(); 
        }
    }
}