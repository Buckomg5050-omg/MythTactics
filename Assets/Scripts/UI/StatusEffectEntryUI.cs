using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class StatusEffectEntryUI : MonoBehaviour
{
    [Header("UI References")]
    public Image effectIconImage;
    public TextMeshProUGUI effectNameText;
    public TextMeshProUGUI effectDetailsText; // For duration/stacks

    private StringBuilder _sb = new StringBuilder();

    public void Populate(ActiveStatusEffect activeEffect)
    {
        if (activeEffect == null || activeEffect.BaseEffect == null)
        {
            // Clear or hide this entry
            if (effectIconImage != null) effectIconImage.enabled = false;
            if (effectNameText != null) effectNameText.text = "";
            if (effectDetailsText != null) effectDetailsText.text = "";
            gameObject.SetActive(false); // Hide the entry if effect is null
            return;
        }

        gameObject.SetActive(true); // Ensure it's active if populated

        EffectSO baseEffect = activeEffect.BaseEffect;

        if (effectIconImage != null)
        {
            if (baseEffect.icon != null)
            {
                effectIconImage.sprite = baseEffect.icon;
                effectIconImage.enabled = true;
            }
            else
            {
                effectIconImage.enabled = false; // No icon to show
            }
        }

        if (effectNameText != null)
        {
            effectNameText.text = baseEffect.effectName;
        }

        if (effectDetailsText != null)
        {
            _sb.Clear();
            if (baseEffect.durationType == EffectDurationType.Permanent)
            {
                _sb.Append("Permanent");
            }
            else
            {
                _sb.Append("Turns: ").Append(activeEffect.RemainingDuration);
            }

            if (baseEffect.maxStacks > 1)
            {
                _sb.Append(" (S: ").Append(activeEffect.CurrentStacks).Append("/").Append(baseEffect.maxStacks).Append(")");
            }
            effectDetailsText.text = _sb.ToString();
        }
    }
}