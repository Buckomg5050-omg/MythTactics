// SkillSelectionUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

// SkillSlotUI class remains the same...
public class SkillSlotUI 
{
    public GameObject slotGameObjectInstance;
    public TextMeshProUGUI skillNameText;
    public TextMeshProUGUI skillCostText;
    public Image skillIconImage; 
    public Button skillButton;
    public AbilitySO boundAbility;

    public SkillSlotUI(GameObject instance, Action<AbilitySO> onSkillSelectedCallback)
    {
        slotGameObjectInstance = instance;
        skillNameText = instance.transform.Find("SkillName_Text")?.GetComponent<TextMeshProUGUI>(); 
        skillCostText = instance.transform.Find("SkillCost_Text")?.GetComponent<TextMeshProUGUI>(); 
        skillIconImage = instance.transform.Find("SkillIcon_Image")?.GetComponent<Image>();     
        skillButton = instance.GetComponent<Button>();

        if (skillNameText == null) Debug.LogError("SkillSlotUI: SkillName_Text not found on instance.", instance);
        if (skillCostText == null) Debug.LogError("SkillSlotUI: SkillCost_Text not found on instance.", instance);
        if (skillIconImage == null && instance.transform.Find("SkillIcon_Image") != null) Debug.LogWarning("SkillSlotUI: SkillIcon_Image GameObject found but Image component is missing.", instance); 
        if (skillButton == null) Debug.LogError("SkillSlotUI: Button component not found on root of instance.", instance);

        skillButton?.onClick.AddListener(() => {
            if (boundAbility != null)
            {
                onSkillSelectedCallback?.Invoke(boundAbility);
            }
        });
    }

    public void Populate(AbilitySO ability)
    {
        boundAbility = ability;
        if (ability == null)
        {
            slotGameObjectInstance.SetActive(false);
            return;
        }
        slotGameObjectInstance.SetActive(true);

        if (skillNameText != null) skillNameText.text = ability.abilityName;
        if (skillCostText != null)
        {
            string costString = $"AP:{ability.apCost}";
            if (ability.mpCost > 0) costString += $" MP:{ability.mpCost}";
            if (ability.spCost > 0) costString += $" SP:{ability.spCost}";
            if (ability.fpCost > 0) costString += $" FP:{ability.fpCost}";
            if (ability.ipCost > 0) costString += $" IP:{ability.ipCost}";
            skillCostText.text = costString;
        }
        
        if (skillIconImage != null)
        {
            if (ability.abilityIcon != null) 
            {
                skillIconImage.sprite = ability.abilityIcon; 
                skillIconImage.enabled = true;
            }
            else
            {
                 skillIconImage.enabled = false; 
            }
        }
    }
}


public class SkillSelectionUI : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject skillSlotPrefab;

    [Header("Panel References")]
    public Transform skillSlotsContainer; 
    public Button closeButton; 

    private List<SkillSlotUI> _currentSkillSlots = new List<SkillSlotUI>();
    private Unit _caster;

    public static event Action<Unit, AbilitySO> OnSkillAbilitySelected;
    public static event Action OnSkillPanelClosedByButton; // ADDED EVENT

    void Awake()
    {
        if (skillSlotPrefab == null) Debug.LogError("SkillSelectionUI: SkillSlot_Prefab not assigned!", this);
        if (skillSlotsContainer == null) 
        {
            Debug.LogWarning("SkillSelectionUI: SkillSlotsContainer not assigned. Attempting to use this.transform.", this);
            skillSlotsContainer = this.transform; 
        }

        if (closeButton != null) 
        {
            // MODIFIED: Listener now calls a method that invokes the event
            closeButton.onClick.AddListener(HandleCloseButtonPressed); 
        }
        else 
        {
            if (transform.Find("CloseButton") != null) 
                 Debug.LogWarning("SkillSelectionUI: CloseButton UI element found, but not assigned in Inspector.", this);
        }

        gameObject.SetActive(false); 
    }

    // ADDED: Method to handle close button press
    private void HandleCloseButtonPressed()
    {
        Debug.Log("SkillSelectionUI: Close button pressed. Invoking OnSkillPanelClosedByButton event.", this);
        OnSkillPanelClosedByButton?.Invoke();
        // HidePanel(); // PIH will now handle hiding the panel when it changes state.
    }

    public void ShowPanel(Unit caster, Vector2 positionToShowAt, List<AbilitySO> abilities)
    {
        _caster = caster;
        if (_caster == null || abilities == null)
        {
            HidePanel(); // Still hide if called with invalid data
            return;
        }

        foreach (SkillSlotUI slot in _currentSkillSlots)
        {
            if (slot != null && slot.slotGameObjectInstance != null) 
                Destroy(slot.slotGameObjectInstance);
        }
        _currentSkillSlots.Clear();

        if (abilities.Count == 0)
        {
            Debug.LogWarning($"{caster.unitName} has no abilities to display in SkillSelectionUI.", caster);
        }

        foreach (AbilitySO ability in abilities)
        {
            if (ability == null) continue;
            if (skillSlotPrefab == null) { Debug.LogError("SkillSelectionUI: skillSlotPrefab is null!", this); break;}
            if (skillSlotsContainer == null) { Debug.LogError("SkillSelectionUI: skillSlotsContainer is null!", this); break;}

            GameObject slotInstance = Instantiate(skillSlotPrefab, skillSlotsContainer);
            SkillSlotUI newSlotUI = new SkillSlotUI(slotInstance, HandleSkillSlotClicked);
            newSlotUI.Populate(ability);
            _currentSkillSlots.Add(newSlotUI);
        }
        
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.position = positionToShowAt; 
        }

        gameObject.SetActive(true);
        Debug.Log($"SkillSelectionUI: Shown for {caster.unitName} with {abilities.Count} skills near {positionToShowAt}.", this);
    }

    private void HandleSkillSlotClicked(AbilitySO selectedAbility)
    {
        if (_caster == null || selectedAbility == null) return;

        Debug.Log($"SkillSelectionUI: Skill '{selectedAbility.abilityName}' selected by {_caster.unitName}.", this);
        OnSkillAbilitySelected?.Invoke(_caster, selectedAbility);
        HidePanel(); // When a skill is selected, we still hide immediately and PIH handles state change.
    }

    public void HidePanel()
    {
        // This method is now primarily called by PlayerInputHandler or when a skill is selected.
        if (gameObject.activeSelf) // Only log if it was actually visible
        {
            gameObject.SetActive(false);
            Debug.Log("SkillSelectionUI: Panel hidden.", this);
        }
    }

    public bool IsVisible()
    {
        return gameObject.activeSelf;
    }
}