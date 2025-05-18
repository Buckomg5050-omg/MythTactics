// ItemSelectionPanelUI.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
// using TMPro; // If your item button prefab uses TextMeshPro

public class ItemSelectionPanelUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelRoot;
    public GameObject itemButtonPrefab; // This prefab should have ItemButtonDisplay.cs
    public Transform itemButtonContainer;

    private Unit _currentUnit;
    private List<GameObject> _activeItemButtons = new List<GameObject>();

    public delegate void ItemSelectedForUseHandler(Unit user, ItemSO selectedItem);
    public static event ItemSelectedForUseHandler OnItemSelectedForUse;

    // Optional: Event for a dedicated close button on this panel
    // public static event System.Action OnItemPanelClosedByButton;

    void Awake()
    {
        Debug.Log($"ItemSelectionPanelUI.Awake: Called for {this.gameObject.name}. Panel Root assigned: {panelRoot != null}", this);
        if (panelRoot == null)
        {
            Debug.LogError("ItemSelectionPanelUI: Panel Root not assigned in Inspector!", this);
            return;
        }
        if (itemButtonPrefab == null)
        {
            Debug.LogWarning("ItemSelectionPanelUI: Item Button Prefab not assigned. Panel will not show items.", this);
        }
        else if (itemButtonPrefab.GetComponent<ItemButtonDisplay>() == null) // Check for ItemButtonDisplay script
        {
            Debug.LogError("ItemSelectionPanelUI: Item Button Prefab is missing the ItemButtonDisplay script!", itemButtonPrefab);
        }
        if (itemButtonContainer == null)
        {
            Debug.LogWarning("ItemSelectionPanelUI: Item Button Container not assigned. Panel will not show items.", this);
        }

        panelRoot.SetActive(false);
        Debug.Log($"ItemSelectionPanelUI.Awake: panelRoot '{panelRoot.name}' set to inactive.", this);
    }

    public void ShowPanel(Unit selectedUnit)
    {
        Debug.Log($"ItemSelectionPanelUI.ShowPanel: Method called for unit '{selectedUnit?.unitName}'. Panel Root: '{panelRoot?.name}', Is panelRoot null? {panelRoot == null}", this);
        if (panelRoot == null)
        {
            Debug.LogError("ItemSelectionPanelUI.ShowPanel: Panel Root is NULL. Cannot show panel.", this);
            return;
        }
        if (selectedUnit == null)
        {
            Debug.LogWarning("ItemSelectionPanelUI.ShowPanel: selectedUnit is NULL. Cannot show panel.", this);
            HidePanel();
            return;
        }

        _currentUnit = selectedUnit;
        panelRoot.SetActive(true);
        Debug.Log($"ItemSelectionPanelUI.ShowPanel: panelRoot '{panelRoot.name}' SetActive(true) CALLED. Is panelRoot activeInHierarchy now? {panelRoot.activeInHierarchy}", this);
        
        PopulatePanel(_currentUnit); // MODIFIED: Call updated PopulatePanel
    }

    public void HidePanel()
    {
        Debug.Log($"ItemSelectionPanelUI.HidePanel: Method called. Panel Root: '{panelRoot?.name}'", this);
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
            Debug.Log($"ItemSelectionPanelUI.HidePanel: panelRoot '{panelRoot.name}' SetActive(false) CALLED. Is panelRoot activeInHierarchy now? {panelRoot.activeInHierarchy}", this);
        }
        ClearItemButtons();
        _currentUnit = null;
        // OnItemPanelClosedByButton?.Invoke();
    }

    private void PopulatePanel(Unit unit)
    {
        ClearItemButtons();
        Debug.Log($"ItemSelectionPanelUI.PopulatePanel: Populating for unit {unit?.unitName}. Item Button Prefab null? {itemButtonPrefab == null}. Container null? {itemButtonContainer == null}", this);


        if (itemButtonPrefab == null || itemButtonContainer == null || unit == null || unit.inventory == null)
        {
            Debug.LogWarning("ItemSelectionPanelUI.PopulatePanel: Cannot populate - missing references or unit inventory.", this);
            // Optionally, display a "No Items" message in the UI
            return;
        }

        if (unit.inventory.Count == 0)
        {
            Debug.Log($"ItemSelectionPanelUI.PopulatePanel: Unit {unit.unitName} has no items in inventory.", this);
            // Optionally, display a "No Items" message in the UI
            return;
        }

        Debug.Log($"ItemSelectionPanelUI.PopulatePanel: Unit {unit.unitName} has {unit.inventory.Count} items. Iterating...", this);
        foreach (ItemSO item in unit.inventory)
        {
            if (item == null)
            {
                Debug.LogWarning("ItemSelectionPanelUI.PopulatePanel: Encountered a null item in unit's inventory.", this);
                continue;
            }

            // Only display items usable in combat (and consumables for now)
            if (!item.isUsableInCombat || item.itemType != ItemType.Consumable)
            {
                Debug.Log($"ItemSelectionPanelUI.PopulatePanel: Skipping item '{item.itemName}' (Not usable in combat or not consumable).", this);
                continue;
            }

            GameObject itemButtonGO = Instantiate(itemButtonPrefab, itemButtonContainer);
            itemButtonGO.name = $"ItemButton_{item.itemName}";

            ItemButtonDisplay itemButtonDisplay = itemButtonGO.GetComponent<ItemButtonDisplay>();
            if (itemButtonDisplay != null)
            {
                // Pass the OnItemButtonClicked method as the callback
                itemButtonDisplay.Setup(item, unit, OnItemButtonClicked);
                Debug.Log($"ItemSelectionPanelUI.PopulatePanel: Created button for item: {item.itemName}", this);
            }
            else
            {
                Debug.LogError($"ItemSelectionPanelUI.PopulatePanel: Instantiated itemButtonPrefab is missing ItemButtonDisplay script for item: {item.itemName}", itemButtonGO);
            }
            _activeItemButtons.Add(itemButtonGO);
        }
        if (_activeItemButtons.Count == 0)
        {
             Debug.Log($"ItemSelectionPanelUI.PopulatePanel: Unit {unit.unitName} has no *usable* items to display.", this);
            // Optionally, display a "No Usable Items" message in the UI
        }
    }

    private void ClearItemButtons()
    {
        foreach (GameObject buttonGO in _activeItemButtons)
        {
            if (buttonGO != null) Destroy(buttonGO);
        }
        _activeItemButtons.Clear();
    }

    // This method is now a callback passed to ItemButtonDisplay
    private void OnItemButtonClicked(ItemSO selectedItem)
    {
        if (_currentUnit == null || selectedItem == null)
        {
            Debug.LogError("ItemSelectionPanelUI.OnItemButtonClicked: Current unit or selected item is null.", this);
            return;
        }

        // AP check is now also done on the button itself, but double-check here before invoking event
        if (!_currentUnit.CanAffordAPForAction(selectedItem.apCostToUse))
        {
            Debug.LogWarning($"ItemSelectionPanelUI: {_currentUnit.unitName} cannot afford {selectedItem.itemName} (Cost: {selectedItem.apCostToUse} AP). This should ideally be caught by button interactability.", this);
            // Potentially play a "can't use" sound or UI feedback
            return;
        }

        Debug.Log($"ItemSelectionPanelUI: {_currentUnit.unitName} selected item {selectedItem.itemName} via button. Invoking OnItemSelectedForUse.", this);
        OnItemSelectedForUse?.Invoke(_currentUnit, selectedItem);

        // Hiding the panel is now handled by PlayerInputHandler after the OnItemSelectedForUse event is processed.
        // HidePanel();
    }

    public bool IsVisible()
    {
        return panelRoot != null && panelRoot.activeSelf;
    }
}