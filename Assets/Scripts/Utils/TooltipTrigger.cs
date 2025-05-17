using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections; // Required for Coroutines

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea(3, 10)]
    [Tooltip("The text to display in the tooltip when this element is hovered over.")]
    public string tooltipText = "Default Tooltip Text";

    [Tooltip("Delay in seconds before the tooltip appears on hover.")]
    public float showDelay = 1.0f; // Default to half a second

    private Coroutine _showTooltipCoroutine;
    private bool _isMouseOver = false; // To track if mouse is still over when delay finishes

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isMouseOver = true;
        // UnityEngine.Debug.Log($"TooltipTrigger on {gameObject.name}: OnPointerEnter. Text: '{tooltipText}'", this);

        // Stop any previous hide/show coroutines for this trigger
        if (_showTooltipCoroutine != null)
        {
            StopCoroutine(_showTooltipCoroutine);
            _showTooltipCoroutine = null;
        }

        // Start a new coroutine to show the tooltip after a delay
        if (TooltipUI.Instance != null && !string.IsNullOrEmpty(tooltipText) && gameObject.activeInHierarchy)
        {
            _showTooltipCoroutine = StartCoroutine(ShowTooltipAfterDelay(eventData.position));
        }
        else if (TooltipUI.Instance == null)
        {
            UnityEngine.Debug.LogWarning("TooltipTrigger: TooltipUI.Instance is null. Cannot show tooltip.", this);
        }
    }

    private IEnumerator ShowTooltipAfterDelay(Vector2 screenPosition)
    {
        // UnityEngine.Debug.Log($"TooltipTrigger on {gameObject.name}: Starting delay coroutine.", this);
        yield return new WaitForSeconds(showDelay);

        if (_isMouseOver && TooltipUI.Instance != null) // Check if mouse is still over this trigger
        {
            // UnityEngine.Debug.Log($"TooltipTrigger on {gameObject.name}: Delay complete, showing tooltip.", this);
            TooltipUI.Instance.ShowTooltip(tooltipText, screenPosition);
        }
        // else
        // {
            // UnityEngine.Debug.Log($"TooltipTrigger on {gameObject.name}: Delay complete, but mouse no longer over or TooltipUI is null.", this);
        // }
        _showTooltipCoroutine = null; // Coroutine finished
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isMouseOver = false;
        // UnityEngine.Debug.Log($"TooltipTrigger on {gameObject.name}: OnPointerExit.", this);

        // Stop the show coroutine if mouse exits before delay is complete
        if (_showTooltipCoroutine != null)
        {
            StopCoroutine(_showTooltipCoroutine);
            _showTooltipCoroutine = null;
            // UnityEngine.Debug.Log($"TooltipTrigger on {gameObject.name}: Show delay coroutine cancelled by OnPointerExit.", this);
        }

        // Hide the tooltip if it was already visible
        if (TooltipUI.Instance != null)
        {
            TooltipUI.Instance.HideTooltip();
        }
    }

    void OnDisable()
    {
        // Ensure coroutine is stopped and tooltip hidden if this object gets disabled
        if (_showTooltipCoroutine != null)
        {
            StopCoroutine(_showTooltipCoroutine);
            _showTooltipCoroutine = null;
        }
        if (_isMouseOver && TooltipUI.Instance != null)
        {
            TooltipUI.Instance.HideTooltip();
        }
        _isMouseOver = false;
    }
}