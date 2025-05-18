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
    private TooltipTrigger _tooltipTrigger;

    void Awake()
    {
        _buttonComponent = GetComponent<Button>();
        if (_buttonComponent == null)
        {
            Debug.LogError("ItemButtonDisplay: Button component not found on this GameObject!", this);
        }

        _tooltipTrigger = GetComponent<TooltipTrigger>();
    }

    public void Setup(ItemSO itemSO, Unit itemUser, System.Action<ItemSO> onClickCallback)
    {
        _currentItemSO = itemSO;
        _currentUser = itemUser;

        if (_currentItemSO == null)
        {
            if (itemIconImage != null) itemIconImage.enabled = false;
            if (itemNameText != null) itemNameText.text = "N/A";
            if (_buttonComponent != null) _buttonComponent.interactable = false;
            if (_tooltipTrigger != null) _tooltipTrigger.tooltipText = "";
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

        if (_tooltipTrigger != null)
        {
            string apCostString = (_currentItemSO.apCostToUse > 0) ? $"(AP: {_currentItemSO.apCostToUse})" : "(Free)";
            // MODIFIED: Changed _currentItemSO.description to _currentItemSO.shortDescription
            _tooltipTrigger.tooltipText = $"<b>{_currentItemSO.itemName}</b> {apCostString}\n<size=90%>{_currentItemSO.shortDescription}</size>";
            // You could also add _currentItemSO.loreText here if you want, perhaps on a new line or with a condition.
            // For example:
            // if (!string.IsNullOrEmpty(_currentItemSO.loreText))
            // {
            // _tooltipTrigger.tooltipText += $"\n<i><size=80%>{_currentItemSO.loreText}</size></i>";
            // }
        }

        if (_buttonComponent != null)
        {
            _buttonComponent.onClick.RemoveAllListeners();
            _buttonComponent.onClick.AddListener(() => {
                if (onClickCallback != null)
                {
                    onClickCallback(_currentItemSO);
                }
            });

            bool canAfford = (_currentUser != null) && _currentUser.CanAffordAPForAction(_currentItemSO.apCostToUse);
            _buttonComponent.interactable = canAfford;
        }
    }
}