// TurnOrderUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections; // Required for Coroutines
using System.Collections.Generic;

public class UnitTurnSlotUI
{
    public GameObject slotGameObject;
    public Image unitSpriteImage;
    public Image slotBackgroundImage;
    private LayoutElement _layoutElement;

    // CanvasGroups for fading
    private CanvasGroup _unitSpriteCanvasGroup;
    private CanvasGroup _slotBackgroundCanvasGroup;

    // Sprites, scales, and dimensions
    private Sprite _activeDiamondSprite;
    private Sprite _upcomingDiamondSprite;
    private Vector2 _activeSpriteScale;
    private Vector2 _normalSpriteScale;
    private Vector2 _activeSlotDimensions;
    private Vector2 _upcomingSlotDimensions;
    private Vector2 _activeSlotBackgroundImageActualSize;
    private Vector2 _upcomingSlotBackgroundImageActualSize;

    // Coroutine management
    private MonoBehaviour _coroutineRunner; // To run coroutines from this non-MB class
    private Coroutine _transitionCoroutine;
    private Coroutine _backgroundFadeCoroutine;
    private Coroutine _spriteScaleCoroutine;
    private Coroutine _slotSizeCoroutine;


    public UnitTurnSlotUI(GameObject slotInstance, MonoBehaviour coroutineRunner,
                          Sprite activeDiamond, Sprite upcomingDiamond,
                          Vector2 activeScale, Vector2 normalScale,
                          Vector2 activeDims, Vector2 upcomingDims,
                          Vector2 activeBgImgSize, Vector2 upcomingBgImgSize)
    {
        slotGameObject = slotInstance;
        _coroutineRunner = coroutineRunner; // Store the MonoBehaviour instance

        _activeDiamondSprite = activeDiamond;
        _upcomingDiamondSprite = upcomingDiamond;
        _activeSpriteScale = activeScale;
        _normalSpriteScale = normalScale;
        _activeSlotDimensions = activeDims;
        _upcomingSlotDimensions = upcomingDims;
        _activeSlotBackgroundImageActualSize = activeBgImgSize;
        _upcomingSlotBackgroundImageActualSize = upcomingBgImgSize;

        _layoutElement = slotInstance.GetComponent<LayoutElement>();
        unitSpriteImage = slotInstance.transform.Find("Unit_Sprite_Image")?.GetComponent<Image>();
        _unitSpriteCanvasGroup = unitSpriteImage?.GetComponent<CanvasGroup>();
        slotBackgroundImage = slotInstance.transform.Find("Slot_Background_Image")?.GetComponent<Image>();
        _slotBackgroundCanvasGroup = slotBackgroundImage?.GetComponent<CanvasGroup>();

        if (_layoutElement == null) Debug.LogError("UnitTurnSlotUI: Slot instance is missing LayoutElement.", slotInstance);
        if (unitSpriteImage == null) Debug.LogError("UnitTurnSlotUI: Could not find Unit_Sprite_Image.", slotInstance);
        if (_unitSpriteCanvasGroup == null) Debug.LogError("UnitTurnSlotUI: Unit_Sprite_Image is missing CanvasGroup.", slotInstance);
        if (slotBackgroundImage == null) Debug.LogError("UnitTurnSlotUI: Could not find Slot_Background_Image.", slotInstance);
        if (_slotBackgroundCanvasGroup == null) Debug.LogError("UnitTurnSlotUI: Slot_Background_Image is missing CanvasGroup.", slotInstance);
    }

    public void TransitionToState(Unit unit, bool isActiveUnit, float duration)
    {
        if (_transitionCoroutine != null) _coroutineRunner.StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = _coroutineRunner.StartCoroutine(PerformTransition(unit, isActiveUnit, duration));
    }

    private IEnumerator PerformTransition(Unit unit, bool isActiveUnit, float duration)
    {
        if (unit != null && slotGameObject != null)
        {
            slotGameObject.SetActive(true);

            // --- Determine target values ---
            Sprite targetDiamondSprite = isActiveUnit ? _activeDiamondSprite : _upcomingDiamondSprite;
            Vector2 targetSpriteScale = isActiveUnit ? _activeSpriteScale : _normalSpriteScale;
            Vector2 targetSlotDimensions = isActiveUnit ? _activeSlotDimensions : _upcomingSlotDimensions;
            Vector2 targetBgImgActualSize = isActiveUnit ? _activeSlotBackgroundImageActualSize : _upcomingSlotBackgroundImageActualSize;
            Sprite unitDisplaySprite = unit.GetComponentInChildren<SpriteRenderer>()?.sprite;

            // --- Start individual animations ---
            // Background Sprite and Size (Fade out old, swap, fade in new)
            if (slotBackgroundImage.sprite != targetDiamondSprite || slotBackgroundImage.rectTransform.sizeDelta != targetBgImgActualSize)
            {
                if (_backgroundFadeCoroutine != null) _coroutineRunner.StopCoroutine(_backgroundFadeCoroutine);
                _backgroundFadeCoroutine = _coroutineRunner.StartCoroutine(
                    AnimateImageChange(_slotBackgroundCanvasGroup, slotBackgroundImage, targetDiamondSprite, targetBgImgActualSize, duration)
                );
            } else { // Ensure it's visible if no sprite change but was hidden
                 if (_slotBackgroundCanvasGroup != null) _slotBackgroundCanvasGroup.alpha = 1f;
                 if (slotBackgroundImage != null) slotBackgroundImage.enabled = true;
            }


            // Unit Sprite (if different or visibility changes)
            if (unitSpriteImage.sprite != unitDisplaySprite || !unitSpriteImage.enabled) {
                 if (unitDisplaySprite != null) {
                    // Simple fade for sprite change for now, could be more complex (fade out, change, fade in)
                    if (_unitSpriteCanvasGroup != null) _unitSpriteCanvasGroup.alpha = 0f; // Instant hide before pop
                    unitSpriteImage.sprite = unitDisplaySprite;
                    unitSpriteImage.enabled = true;
                    if (_unitSpriteCanvasGroup != null) _coroutineRunner.StartCoroutine(FadeCanvasGroup(_unitSpriteCanvasGroup, 1f, duration / 2f)); // Fade in
                 } else {
                    if (_unitSpriteCanvasGroup != null) _coroutineRunner.StartCoroutine(FadeCanvasGroup(_unitSpriteCanvasGroup, 0f, duration / 2f, () => unitSpriteImage.enabled = false ));
                 }
            } else {
                 if (_unitSpriteCanvasGroup != null) _unitSpriteCanvasGroup.alpha = 1f; // Ensure visible
            }


            // Unit Sprite Scale
            if (unitSpriteImage.rectTransform.localScale != (Vector3)targetSpriteScale)
            {
                if (_spriteScaleCoroutine != null) _coroutineRunner.StopCoroutine(_spriteScaleCoroutine);
                _spriteScaleCoroutine = _coroutineRunner.StartCoroutine(AnimateScale(unitSpriteImage.rectTransform, targetSpriteScale, duration));
            }

            // Overall Slot Dimensions
            if (_layoutElement.preferredWidth != targetSlotDimensions.x || _layoutElement.preferredHeight != targetSlotDimensions.y)
            {
                if (_slotSizeCoroutine != null) _coroutineRunner.StopCoroutine(_slotSizeCoroutine);
                _slotSizeCoroutine = _coroutineRunner.StartCoroutine(AnimateLayoutElementSize(_layoutElement, targetSlotDimensions, duration));
            }

            // Wait for the longest relevant part of the transition if needed, or let them run concurrently.
            // For now, we let them run concurrently.
            yield return null; // Or yield return new WaitForSeconds(duration); if you want to block this main coroutine
        }
        else // No unit for this slot
        {
            // Fade out the whole slot if it's becoming inactive
            if (slotGameObject.activeSelf) {
                // This needs a CanvasGroup on the slotGameObject's root itself to fade everything.
                // For now, direct hide. To improve: add CanvasGroup to prefab root, fade that.
                if (_unitSpriteCanvasGroup != null) _unitSpriteCanvasGroup.alpha = 0;
                if (_slotBackgroundCanvasGroup != null) _slotBackgroundCanvasGroup.alpha = 0;
                slotGameObject.SetActive(false);
            }
        }
    }
    
    private IEnumerator AnimateImageChange(CanvasGroup cg, Image image, Sprite newSprite, Vector2 newSize, float duration)
    {
        if (cg == null || image == null) yield break;

        float halfDuration = duration * 0.5f; // Fade out, then fade in

        // Fade out
        yield return _coroutineRunner.StartCoroutine(FadeCanvasGroup(cg, 0f, halfDuration));

        // Change content
        image.sprite = newSprite;
        image.rectTransform.sizeDelta = newSize;

        // Fade in
        yield return _coroutineRunner.StartCoroutine(FadeCanvasGroup(cg, 1f, halfDuration));
        _backgroundFadeCoroutine = null;
    }

    private IEnumerator AnimateScale(RectTransform targetTransform, Vector2 targetScale, float duration)
    {
        if (targetTransform == null) yield break;
        Vector3 startScale = targetTransform.localScale;
        Vector3 endScale = new Vector3(targetScale.x, targetScale.y, startScale.z); // Preserve Z
        float timer = 0f;

        while (timer < duration)
        {
            targetTransform.localScale = Vector3.Lerp(startScale, endScale, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
        targetTransform.localScale = endScale;
        _spriteScaleCoroutine = null;
    }

    private IEnumerator AnimateLayoutElementSize(LayoutElement le, Vector2 targetSize, float duration)
    {
        if (le == null) yield break;
        Vector2 startSize = new Vector2(le.preferredWidth, le.preferredHeight);
        float timer = 0f;

        while (timer < duration)
        {
            le.preferredWidth = Mathf.Lerp(startSize.x, targetSize.x, timer / duration);
            le.preferredHeight = Mathf.Lerp(startSize.y, targetSize.y, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
        le.preferredWidth = targetSize.x;
        le.preferredHeight = targetSize.y;
        _slotSizeCoroutine = null;
    }

    public static IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration, System.Action onComplete = null)
    {
        if (cg == null) { onComplete?.Invoke(); yield break; }
        float startAlpha = cg.alpha;
        float timer = 0f;

        if (duration <= 0) // Instant if duration is zero or negative
        {
            cg.alpha = targetAlpha;
            onComplete?.Invoke();
            yield break;
        }
        
        while (timer < duration)
        {
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
        cg.alpha = targetAlpha;
        onComplete?.Invoke();
    }
}


public class TurnOrderUI : MonoBehaviour // This IS a MonoBehaviour
{
    [Header("UI Prefabs & References")]
    public GameObject unitTurnSlotPrefab;
    public TextMeshProUGUI activeUnitNameTextDisplay;
    private CanvasGroup _activeUnitNameCanvasGroup; // For fading the name

    [Header("Transition Settings")]
    [Tooltip("Duration for UI transitions (fades, scales) in seconds.")]
    public float transitionDuration = 0.3f;

    // ... (other existing fields like diamond sprites, scales, dimensions) ...
    [Header("Slot Visuals - Sprites & Scale")]
    public Sprite activeUnitDiamondSprite;
    public Sprite upcomingUnitDiamondSprite;
    public Vector2 activeUnitSpriteScale = new Vector2(1.5f, 1.5f);
    public Vector2 normalUnitSpriteScale = Vector2.one;

    [Header("Slot Visuals - Overall Slot Dimensions")]
    public Vector2 activeSlotDimensions = new Vector2(100f, 80f); 
    public Vector2 upcomingSlotDimensions = new Vector2(60f, 70f); 

    [Header("Slot Visuals - Background Image Actual Size")]
    public Vector2 activeSlotBackgroundImageSize = new Vector2(90f, 90f); 
    public Vector2 upcomingSlotBackgroundImageSize = new Vector2(55f, 55f); 
    
    private Transform _slotsContainer; 
    [Header("Display Settings")]
    public int numberOfSlotsToDisplay = 5;
    private List<UnitTurnSlotUI> _uiSlots = new List<UnitTurnSlotUI>();
    private Coroutine _activeNameFadeCoroutine;


    void Awake()
    {
        _slotsContainer = this.transform;
        if (unitTurnSlotPrefab == null) { Debug.LogError("TurnOrderUI: UnitTurnSlot Prefab not assigned!", this); this.enabled = false; return; }
        if (activeUnitNameTextDisplay == null) { Debug.LogError("TurnOrderUI: Active Unit Name Text Display not assigned!", this); this.enabled = false; return; }
        
        _activeUnitNameCanvasGroup = activeUnitNameTextDisplay.GetComponent<CanvasGroup>();
        if (_activeUnitNameCanvasGroup == null) { Debug.LogError("TurnOrderUI: ActiveUnitName_Text_Display is MISSING a CanvasGroup component. Please add one.", this); this.enabled = false; return; }

        // ... (other null checks from before) ...
        LayoutElement prefabRootLayoutElement = unitTurnSlotPrefab.GetComponent<LayoutElement>();
        if (prefabRootLayoutElement == null) { Debug.LogError("TurnOrderUI: UnitTurnSlot_Prefab root is MISSING LayoutElement. Disabling.", this); this.enabled = false; return; }
        
        Image bgImageInPrefab = unitTurnSlotPrefab.transform.Find("Slot_Background_Image")?.GetComponent<Image>();
        if (bgImageInPrefab == null || bgImageInPrefab.GetComponent<CanvasGroup>() == null) { Debug.LogError("TurnOrderUI: Prefab's Slot_Background_Image or its CanvasGroup not found/set up.", this); this.enabled = false; return; }
        Image unitSpriteInPrefab = unitTurnSlotPrefab.transform.Find("Unit_Sprite_Image")?.GetComponent<Image>();
        if (unitSpriteInPrefab == null || unitSpriteInPrefab.GetComponent<CanvasGroup>() == null) { Debug.LogError("TurnOrderUI: Prefab's Unit_Sprite_Image or its CanvasGroup not found/set up.", this); this.enabled = false; return; }


        if (_activeUnitNameCanvasGroup != null) _activeUnitNameCanvasGroup.alpha = 0f; // Start hidden
        activeUnitNameTextDisplay.gameObject.SetActive(true); // Keep GO active, control via alpha

        InitializeDisplay();
    }

    void InitializeDisplay()
    {
        foreach (Transform child in _slotsContainer) { Destroy(child.gameObject); }
        _uiSlots.Clear();

        for (int i = 0; i < numberOfSlotsToDisplay; i++)
        {
            GameObject slotInstance = Instantiate(unitTurnSlotPrefab, _slotsContainer);
            slotInstance.name = $"UnitTurnSlot_{i}";
            // Pass 'this' (TurnOrderUI instance) as the coroutine runner
            UnitTurnSlotUI slotUI = new UnitTurnSlotUI(slotInstance, this,
                                                       activeUnitDiamondSprite, upcomingUnitDiamondSprite,
                                                       activeUnitSpriteScale, normalUnitSpriteScale,
                                                       activeSlotDimensions, upcomingSlotDimensions,
                                                       activeSlotBackgroundImageSize, upcomingSlotBackgroundImageSize);
            _uiSlots.Add(slotUI);
            // Set initial state without animation, or animate in if desired
             CanvasGroup slotRootCG = slotInstance.GetComponent<CanvasGroup>(); // If you add one to root for full slot fade
             if(slotRootCG != null) slotRootCG.alpha = 0; // Start slots invisible
             else slotInstance.SetActive(false); // Fallback
        }
    }

    public void UpdateTurnOrderDisplay(List<Unit> upcomingUnits, Unit activeUnit)
    {
        if (upcomingUnits == null)
        {
            for (int i = 0; i < _uiSlots.Count; i++) { _uiSlots[i].TransitionToState(null, false, transitionDuration); } // Transition to "empty" state
            if (_activeUnitNameCanvasGroup != null)
            {
                if (_activeNameFadeCoroutine != null) StopCoroutine(_activeNameFadeCoroutine);
                _activeNameFadeCoroutine = StartCoroutine(UnitTurnSlotUI.FadeCanvasGroup(_activeUnitNameCanvasGroup, 0f, transitionDuration));
            }
            return;
        }

        bool activeUnitFoundInList = false;
        for (int i = 0; i < _uiSlots.Count; i++)
        {
            if (i < upcomingUnits.Count)
            {
                Unit unitToShow = upcomingUnits[i];
                bool isThisUnitActive = (unitToShow == activeUnit);
                _uiSlots[i].TransitionToState(unitToShow, isThisUnitActive, transitionDuration); // Call new transition method

                if (isThisUnitActive && unitToShow != null) activeUnitFoundInList = true;
            }
            else
            {
                _uiSlots[i].TransitionToState(null, false, transitionDuration); // Transition to "empty" state
            }
        }

        if (_activeUnitNameCanvasGroup != null)
        {
            if (_activeNameFadeCoroutine != null) StopCoroutine(_activeNameFadeCoroutine);
            if (activeUnit != null && activeUnitFoundInList)
            {
                activeUnitNameTextDisplay.text = activeUnit.unitName;
                _activeNameFadeCoroutine = StartCoroutine(UnitTurnSlotUI.FadeCanvasGroup(_activeUnitNameCanvasGroup, 1f, transitionDuration));
            }
            else
            {
                _activeNameFadeCoroutine = StartCoroutine(UnitTurnSlotUI.FadeCanvasGroup(_activeUnitNameCanvasGroup, 0f, transitionDuration));
            }
        }
    }
}