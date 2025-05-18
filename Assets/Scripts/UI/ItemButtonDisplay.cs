// ItemButtonDisplay.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro; // If you use TextMeshPro for the button's text

public class ItemButtonDisplay : MonoBehaviour
{
    [Header("UI References (Assign in Prefab)")]
    public Image itemIconImage;
    public TextMeshProUGUI itemNameText; // Use Text if not using TextMeshPro
    // public TextMeshProUGUI itemQuantityText; // For later when we have item stacks

    private ItemSO _currentItemSO;
    private Unit _currentUser;
    private Button _buttonComponent;
    private TooltipTrigger _tooltipTrigger; // MODIFIED: Added reference

    void Awake()
    {
        _buttonComponent = GetComponent<Button>();
        if (_buttonComponent == null)
        {
            Debug.LogError("ItemButtonDisplay: Button component not found on this GameObject!", this);
        }

        // MODIFIED: Get the TooltipTrigger component
        _tooltipTrigger = GetComponent<TooltipTrigger>();
        if (_tooltipTrigger == null)
        {
            // This is not an error, as tooltips are optional on buttons, but a warning might be useful during development.
            // Debug.LogWarning($"ItemButtonDisplay: TooltipTrigger component not found on button for item {this.gameObject.name}. Item tooltips will not be shown for this button.", this);
        }
    }

    public void Setup(ItemSO itemSO, Unit itemUser, System.Action<ItemSO> onClickCallback)
    {
        _currentItemSO = itemSO;
        _currentUser = itemUser;

        if (_currentItemSO == null)
        {
            // Debug.LogWarning("ItemButtonDisplay.Setup: Received null itemSO.", this); // Already have this log
            if (itemIconImage != null) itemIconImage.enabled = false;
            if (itemNameText != null) itemNameText.text = "N/A";
            if (_buttonComponent != null) _buttonComponent.interactable = false;
            if (_tooltipTrigger != null) _tooltipTrigger.tooltipText = ""; // Clear tooltip for invalid item
            return;
        }

        if (itemIconImage != null)
        {
            if (_currentItemSO.itemIcon != null)
            {
                itemIconImage.sprite = _currentItemSO.itemIcon;
                itemIconImage.enabled = true;
            }
            else
            {
                itemIconImage.enabled = false;
            }
        }

        if (itemNameText != null)
        {
            itemNameText.text = _currentItemSO.itemName;
        }

        // MODIFIED: Set tooltip text
        if (_tooltipTrigger != null)
        {
            // You can customize the format here. Example: Name, AP cost, then description.
            string apCostString = (_currentItemSO.apCostToUse > 0) ? $"(AP: {_currentItemSO.apCostToUse})" : "(Free)";
            _tooltipTrigger.tooltipText = $"<b>{_currentItemSO.itemName}</b> {apCostString}\n<size=90%>{_currentItemSO.description}</size>";
            // Optionally, set the showDelay if you want item tooltips to have a specific delay different from other UI elements
            // _tooltipTrigger.showDelay = 0.75f; // Example: slightly longer delay for item details
        }

        if (_buttonComponent != null)
        {
            _buttonComponent.onClick.RemoveAllListeners();
            _buttonComponent.onClick.AddListener(() => {
                if (onClickCallback != null)
                {
                    onClickCallback(_currentItemSO);
                }
                // else
                // {
                //     Debug.LogWarning($"ItemButtonDisplay: onClickCallback is null for item {_currentItemSO.itemName}", this);
                // }
            });

            bool canAfford = (_currentUser != null) && _currentUser.CanAffordAPForAction(_currentItemSO.apCostToUse);
            _buttonComponent.interactable = canAfford;
        }
    }
}