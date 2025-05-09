// TurnOrderUI.cs
using UnityEngine;
using UnityEngine.UI; // Required for Image
using TMPro;          // Required for TextMeshProUGUI
using System.Collections.Generic;

// Helper class to hold references for each UI slot's components
public class UnitTurnSlotUI
{
    public GameObject slotGameObject; // The root GameObject of the slot instance
    public Image unitSpriteImage;
    public TextMeshProUGUI unitNameText;
    public Image highlightBorder; // Optional: For highlighting the active unit's slot

    public UnitTurnSlotUI(GameObject slotInstance)
    {
        slotGameObject = slotInstance;
        // Find components within the instantiated slot prefab
        // Adjust these paths if your prefab structure is different
        unitSpriteImage = slotInstance.transform.Find("Unit_Sprite_Image")?.GetComponent<Image>();
        unitNameText = slotInstance.transform.Find("Unit_Name_Text")?.GetComponent<TextMeshProUGUI>();
        
        // Example for an optional highlight border image within the slot prefab
        // highlightBorder = slotInstance.transform.Find("Highlight_Border_Image")?.GetComponent<Image>(); 
        // if (highlightBorder != null) highlightBorder.enabled = false; // Start disabled

        if (unitSpriteImage == null) Debug.LogError("UnitTurnSlotUI: Could not find Unit_Sprite_Image component in prefab instance.", slotInstance);
        if (unitNameText == null) Debug.LogError("UnitTurnSlotUI: Could not find Unit_Name_Text component in prefab instance.", slotInstance);
    }

    public void SetData(Unit unit, bool isActiveUnit)
    {
        if (unit != null)
        {
            slotGameObject.SetActive(true);
            unitNameText.text = unit.unitName;

            // Get the SpriteRenderer from the Unit's prefab or instance
            SpriteRenderer unitPrefabSpriteRenderer = unit.GetComponentInChildren<SpriteRenderer>(); // Assumes Unit has a SpriteRenderer as a child or on itself
            if (unitPrefabSpriteRenderer != null && unitPrefabSpriteRenderer.sprite != null)
            {
                unitSpriteImage.sprite = unitPrefabSpriteRenderer.sprite;
                unitSpriteImage.enabled = true;
            }
            else
            {
                unitSpriteImage.enabled = false; // Hide image if no sprite
                Debug.LogWarning($"TurnOrderUI: Unit '{unit.unitName}' has no SpriteRenderer or sprite to display.", unit);
            }

            // Handle active unit highlight (example)
            // if (highlightBorder != null) highlightBorder.enabled = isActiveUnit;
            // Or change background color, scale, etc.
            if (isActiveUnit)
            {
                // Example: tint the slot or name text
                unitNameText.color = Color.yellow; // Simple highlight for active unit
            }
            else
            {
                unitNameText.color = Color.white;
            }
        }
        else // No unit for this slot
        {
            slotGameObject.SetActive(false); // Hide the slot
        }
    }
}

public class TurnOrderUI : MonoBehaviour
{
    [Header("UI Prefabs & References")]
    [Tooltip("Assign your UnitTurnSlot prefab here.")]
    public GameObject unitTurnSlotPrefab;
    
    // The panel this script is attached to, used as parent for slots
    private Transform _slotsContainer; 

    [Header("Display Settings")]
    [Tooltip("Number of unit turns to display in the forecast (including active unit).")]
    public int numberOfSlotsToDisplay = 5;

    private List<UnitTurnSlotUI> _uiSlots = new List<UnitTurnSlotUI>();

    void Awake()
    {
        _slotsContainer = this.transform; // Assuming this script is on the TurnOrderDisplay_Panel
        if (unitTurnSlotPrefab == null)
        {
            Debug.LogError("TurnOrderUI: UnitTurnSlot Prefab not assigned!", this);
            this.enabled = false;
            return;
        }
        InitializeDisplay();
    }

    void InitializeDisplay()
    {
        // Clear any existing slots if re-initializing (e.g., in editor)
        foreach (Transform child in _slotsContainer)
        {
            Destroy(child.gameObject);
        }
        _uiSlots.Clear();

        // Instantiate the required number of UI slots
        for (int i = 0; i < numberOfSlotsToDisplay; i++)
        {
            GameObject slotInstance = Instantiate(unitTurnSlotPrefab, _slotsContainer);
            slotInstance.name = $"UnitTurnSlot_{i}";
            UnitTurnSlotUI slotUI = new UnitTurnSlotUI(slotInstance);
            _uiSlots.Add(slotUI);
            slotInstance.SetActive(false); // Start with slots hidden until populated
        }
        DebugHelper.Log($"TurnOrderUI: Initialized {numberOfSlotsToDisplay} UI slots.", this);
    }

    // This method will be called by TurnManager or another system to update the display
    public void UpdateTurnOrderDisplay(List<Unit> upcomingUnits, Unit activeUnit)
    {
        if (upcomingUnits == null)
        {
            // DebugHelper.LogWarning("TurnOrderUI: UpdateTurnOrderDisplay called with null upcomingUnits list.", this);
            // Hide all slots if no data
            for (int i = 0; i < _uiSlots.Count; i++)
            {
                _uiSlots[i].SetData(null, false);
            }
            return;
        }

        // DebugHelper.Log($"TurnOrderUI: Updating display for {upcomingUnits.Count} upcoming units. Active: {activeUnit?.unitName}", this);

        for (int i = 0; i < _uiSlots.Count; i++)
        {
            if (i < upcomingUnits.Count)
            {
                Unit unitToShow = upcomingUnits[i];
                bool isThisUnitActive = (unitToShow == activeUnit);
                _uiSlots[i].SetData(unitToShow, isThisUnitActive);
            }
            else
            {
                _uiSlots[i].SetData(null, false); // Hide unused slots
            }
        }
    }
    
    // Example of how TurnManager might provide data (this method won't be in TurnOrderUI, but TurnManager)
    // public List<Unit> GetTurnOrderForecast(int count)
    // {
    //     // In TurnManager:
    //     // 1. Take _combatUnits
    //     // 2. Sort them by actionCounter (descending for >1000, then ascending for <1000, or just by predicted next turn)
    //     //    This forecast logic can be complex. A simpler way:
    //     //    - Current ActiveUnit is first.
    //     //    - Then simulate N ticks of AC advancement for all other units and see who comes up next.
    //     //    - Or, for a very basic forecast: sort all non-active units by (ActionCounterThreshold - unit.actionCounter) / unit.EffectiveSpeed (estimated ticks to turn).
    //     // Return 'count' units.
    // }
}