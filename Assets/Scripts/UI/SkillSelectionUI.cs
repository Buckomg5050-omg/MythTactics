// SkillSelectionUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class SkillSlotUI // Helper class for managing individual skill slot instances
{
    public GameObject slotGameObjectInstance;
    public TextMeshProUGUI skillNameText;
    public TextMeshProUGUI skillCostText;
    public Image skillIconImage; // Optional
    public Button skillButton;
    public AbilitySO boundAbility;

    public SkillSlotUI(GameObject instance, Action<AbilitySO> onSkillSelectedCallback)
    {
        slotGameObjectInstance = instance;
        // Adjust these names if your SkillSlot_Prefab child elements are named differently
        skillNameText = instance.transform.Find("SkillName_Text")?.GetComponent<TextMeshProUGUI>(); 
        skillCostText = instance.transform.Find("SkillCost_Text")?.GetComponent<TextMeshProUGUI>(); 
        skillIconImage = instance.transform.Find("SkillIcon_Image")?.GetComponent<Image>();     
        skillButton = instance.GetComponent<Button>();

        if (skillNameText == null) Debug.LogError("SkillSlotUI: SkillName_Text not found on instance. Check prefab child naming.", instance);
        if (skillCostText == null) Debug.LogError("SkillSlotUI: SkillCost_Text not found on instance. Check prefab child naming.", instance);
        // Icon is optional
        if (skillIconImage == null && instance.transform.Find("SkillIcon_Image") != null) Debug.LogWarning("SkillSlotUI: SkillIcon_Image GameObject found but Image component is missing or name is incorrect.", instance); 
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
            if (ability.abilityIcon != null) // MODIFIED: Was ability.icon
            {
                skillIconImage.sprite = ability.abilityIcon; // MODIFIED: Was ability.icon
                skillIconImage.enabled = true;
            }
            else
            {
                 skillIconImage.enabled = false; // Hide if no icon provided
            }
        }
    }
}

public class SkillSelectionUI : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("The prefab for a single skill slot UI element.")]
    public GameObject skillSlotPrefab;

    [Header("Panel References")]
    [Tooltip("The parent transform where skill slots will be instantiated (likely the panel itself or a child Content object).")]
    public Transform skillSlotsContainer; 
    [Tooltip("Optional: Assign the 'Close' or 'Back' button for this panel.")] // Description added
    public Button closeButton; // UNCOMMENTED

    private List<SkillSlotUI> _currentSkillSlots = new List<SkillSlotUI>();
    private Unit _caster;

    public static event Action<Unit, AbilitySO> OnSkillAbilitySelected;

    void Awake()
    {
        if (skillSlotPrefab == null) Debug.LogError("SkillSelectionUI: SkillSlot_Prefab not assigned!", this);
        if (skillSlotsContainer == null) 
        {
            Debug.LogWarning("SkillSelectionUI: SkillSlotsContainer not assigned. Attempting to use this.transform (panel root).", this);
            skillSlotsContainer = this.transform; 
        }

        if (closeButton != null) // UNCOMMENTED
        {
            closeButton.onClick.AddListener(HidePanel); // UNCOMMENTED
        }
        else 
        {
            // Only log warning if a button named "CloseButton" exists but wasn't assigned,
            // otherwise it's truly optional.
            if (transform.Find("CloseButton") != null) // Example common name for a close button
                 Debug.LogWarning("SkillSelectionUI: CloseButton UI element found in prefab, but not assigned in the Inspector.", this);
        }

        gameObject.SetActive(false); 
    }

    public void ShowPanel(Unit caster, Vector2 positionToShowAt, List<AbilitySO> abilities)
    {
        _caster = caster;
        if (_caster == null || abilities == null)
        {
            HidePanel();
            return;
        }

        foreach (SkillSlotUI slot in _currentSkillSlots)
        {
            if (slot != null && slot.slotGameObjectInstance != null) // Add null check for robustness
                Destroy(slot.slotGameObjectInstance);
        }
        _currentSkillSlots.Clear();

        if (abilities.Count == 0)
        {
            Debug.LogWarning($"{caster.unitName} has no abilities to display in SkillSelectionUI.", caster);
            // TODO: Optionally show a "No Skills Available" message within the panel
            // For now, just make sure it shows if it's supposed to be empty but visible.
            // HidePanel(); // Decided by caller whether an empty panel is an error or valid state
        }

        foreach (AbilitySO ability in abilities)
        {
            if (ability == null) continue;
            if (skillSlotPrefab == null) { Debug.LogError("SkillSelectionUI: skillSlotPrefab is null in ShowPanel!", this); break;}
            if (skillSlotsContainer == null) { Debug.LogError("SkillSelectionUI: skillSlotsContainer is null in ShowPanel!", this); break;}


            GameObject slotInstance = Instantiate(skillSlotPrefab, skillSlotsContainer);
            SkillSlotUI newSlotUI = new SkillSlotUI(slotInstance, HandleSkillSlotClicked);
            newSlotUI.Populate(ability);
            _currentSkillSlots.Add(newSlotUI);
        }
        
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.position = positionToShowAt; 
            // TODO: Implement screen clamping for this panel.
            // Consider pivot point of the panel when positioning. If pivot is center, positionToShowAt is its center.
            // If pivot is top-left, positionToShowAt becomes its top-left corner.
            // For "near the button" positioning, you might want to offset based on button size and panel size/pivot.
        }

        gameObject.SetActive(true);
        Debug.Log($"SkillSelectionUI: Shown for {caster.unitName} with {abilities.Count} skills near {positionToShowAt}.", this);
    }

    private void HandleSkillSlotClicked(AbilitySO selectedAbility)
    {
        if (_caster == null || selectedAbility == null) return;

        Debug.Log($"SkillSelectionUI: Skill '{selectedAbility.abilityName}' selected by {_caster.unitName}.", this);
        OnSkillAbilitySelected?.Invoke(_caster, selectedAbility);
        HidePanel();
    }

    public void HidePanel()
    {
        gameObject.SetActive(false);
        // _caster = null; // Keep caster info until explicitly cleared or new one is set by ShowPanel
        // Don't clear _currentSkillSlots here, ShowPanel does it to prevent issues if HidePanel is called mid-population or multiple times
        Debug.Log("SkillSelectionUI: Panel hidden.", this);
    }

    public bool IsVisible()
    {
        return gameObject.activeSelf;
    }
}