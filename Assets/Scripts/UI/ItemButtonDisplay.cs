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

    void Awake()
    {
        _buttonComponent = GetComponent<Button>();
        if (_buttonComponent == null)
        {
            Debug.LogError("ItemButtonDisplay: Button component not found on this GameObject!", this);
        }
    }

    public void Setup(ItemSO itemSO, Unit itemUser, System.Action<ItemSO> onClickCallback)
    {
        _currentItemSO = itemSO;
        _currentUser = itemUser;

        if (_currentItemSO == null)
        {
            Debug.LogWarning("ItemButtonDisplay.Setup: Received null itemSO.", this);
            if (itemIconImage != null) itemIconImage.enabled = false;
            if (itemNameText != null) itemNameText.text = "N/A";
            if (_buttonComponent != null) _buttonComponent.interactable = false;
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
                itemIconImage.enabled = false; // Hide if no icon
            }
        }

        if (itemNameText != null)
        {
            itemNameText.text = _currentItemSO.itemName;
        }

        // Set up button click event
        if (_buttonComponent != null)
        {
            _buttonComponent.onClick.RemoveAllListeners(); // Clear previous listeners
            _buttonComponent.onClick.AddListener(() => {
                if (onClickCallback != null)
                {
                    onClickCallback(_currentItemSO);
                }
                else
                {
                    Debug.LogWarning($"ItemButtonDisplay: onClickCallback is null for item {_currentItemSO.itemName}", this);
                }
            });

            // Check if unit can afford AP cost for the item
            bool canAfford = (_currentUser != null) && _currentUser.CanAffordAPForAction(_currentItemSO.apCostToUse);
            _buttonComponent.interactable = canAfford;
        }
    }
}