using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// UnitTurnSlotUI class definition
public class UnitTurnSlotUI
{
    public GameObject slotGameObject;
    public Image unitSpriteImage;
    public Image slotBackgroundImage;
    private LayoutElement _layoutElement;

    private CanvasGroup _unitSpriteCanvasGroup;
    private CanvasGroup _slotBackgroundCanvasGroup;

    private Sprite _activeDiamondSprite;
    private Sprite _upcomingDiamondSprite;
    private Vector2 _activeSpriteScale;
    private Vector2 _normalSpriteScale;
    private Vector2 _activeSlotDimensions;
    private Vector2 _upcomingSlotDimensions;
    private Vector2 _activeSlotBackgroundImageActualSize;
    private Vector2 _upcomingSlotBackgroundImageActualSize;

    private MonoBehaviour _coroutineRunner;
    private Coroutine _transitionCoroutine;
    private Coroutine _backgroundFadeCoroutine;
    private Coroutine _spriteScaleCoroutine;
    private Coroutine _slotSizeCoroutine;
    private Coroutine _unitSpriteFadeCoroutine;

    public UnitTurnSlotUI(GameObject slotInstance, MonoBehaviour coroutineRunner,
                          Sprite activeDiamond, Sprite upcomingDiamond,
                          Vector2 activeScale, Vector2 normalScale,
                          Vector2 activeDims, Vector2 upcomingDims,
                          Vector2 activeBgImgSize, Vector2 upcomingBgImgSize)
    {
        slotGameObject = slotInstance;
        _coroutineRunner = coroutineRunner;

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
        if (_unitSpriteCanvasGroup == null && unitSpriteImage != null) Debug.LogError("UnitTurnSlotUI: Unit_Sprite_Image is missing CanvasGroup.", slotInstance);
        if (slotBackgroundImage == null) Debug.LogError("UnitTurnSlotUI: Could not find Slot_Background_Image.", slotInstance);
        if (_slotBackgroundCanvasGroup == null && slotBackgroundImage != null) Debug.LogError("UnitTurnSlotUI: Slot_Background_Image is missing CanvasGroup.", slotInstance);
    }

    public void TransitionToState(Unit unit, bool isActiveUnit, float duration)
    {
        if (_transitionCoroutine != null) _coroutineRunner.StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = _coroutineRunner.StartCoroutine(PerformTransition(unit, isActiveUnit, duration));
    }

    private IEnumerator PerformTransition(Unit unit, bool isActiveUnit, float duration)
    {
        // Debug.Log($"SLOT {slotGameObject.name}: PerformTransition START for Unit: {unit?.unitName}, IsActive: {isActiveUnit}, CurrentBG: {slotBackgroundImage?.sprite?.name}", _coroutineRunner);

        if (unit != null && slotGameObject != null)
        {
            if (!slotGameObject.activeSelf) slotGameObject.SetActive(true);

            Sprite targetDiamondSprite = isActiveUnit ? _activeDiamondSprite : _upcomingDiamondSprite;
            Vector2 targetSpriteScale = isActiveUnit ? _activeSpriteScale : _normalSpriteScale;
            Vector2 targetSlotDimensions = isActiveUnit ? _activeSlotDimensions : _upcomingSlotDimensions;
            Vector2 targetBgImgActualSize = isActiveUnit ? _activeSlotBackgroundImageActualSize : _upcomingSlotBackgroundImageActualSize;
            Sprite unitDisplaySprite = null;
            if (unit.TryGetComponent<SpriteRenderer>(out var sr))
            {
                unitDisplaySprite = sr.sprite;
            }
            else if (unit.transform.childCount > 0 && unit.transform.GetChild(0).TryGetComponent<SpriteRenderer>(out var childSr))
            {
                unitDisplaySprite = childSr.sprite;
            }

            if (slotBackgroundImage != null && _slotBackgroundCanvasGroup != null)
            {
                if (slotBackgroundImage.sprite != targetDiamondSprite || slotBackgroundImage.rectTransform.sizeDelta != targetBgImgActualSize)
                {
                    if (_backgroundFadeCoroutine != null) _coroutineRunner.StopCoroutine(_backgroundFadeCoroutine);
                    _backgroundFadeCoroutine = _coroutineRunner.StartCoroutine(
                        AnimateImageChange(_slotBackgroundCanvasGroup, slotBackgroundImage, targetDiamondSprite, targetBgImgActualSize, duration, "BG_ImageChange")
                    );
                }
                else
                {
                    if (targetDiamondSprite != null && _slotBackgroundCanvasGroup.alpha < 1f) {
                         if (_backgroundFadeCoroutine != null) _coroutineRunner.StopCoroutine(_backgroundFadeCoroutine);
                        _backgroundFadeCoroutine = _coroutineRunner.StartCoroutine(FadeCanvasGroup(_slotBackgroundCanvasGroup, 1f, duration, "BG_EnsureVisible"));
                    } else if (targetDiamondSprite == null && _slotBackgroundCanvasGroup.alpha > 0f) {
                         if (_backgroundFadeCoroutine != null) _coroutineRunner.StopCoroutine(_backgroundFadeCoroutine);
                        _backgroundFadeCoroutine = _coroutineRunner.StartCoroutine(FadeCanvasGroup(_slotBackgroundCanvasGroup, 0f, duration, "BG_EnsureHidden"));
                    }
                    if (slotBackgroundImage != null) slotBackgroundImage.enabled = (targetDiamondSprite != null);
                }
            }

            if (unitSpriteImage != null && _unitSpriteCanvasGroup != null)
            {
                if (_unitSpriteFadeCoroutine != null) _coroutineRunner.StopCoroutine(_unitSpriteFadeCoroutine);

                if (unitDisplaySprite != null) {
                    bool needsNewSprite = unitSpriteImage.sprite != unitDisplaySprite;
                    bool needsFadeIn = !_unitSpriteCanvasGroup.gameObject.activeSelf || _unitSpriteCanvasGroup.alpha < 0.99f;

                    if (needsNewSprite) {
                        if (unitSpriteImage.sprite != null && _unitSpriteCanvasGroup.alpha > 0.01f) {
                             _unitSpriteFadeCoroutine = _coroutineRunner.StartCoroutine(FadeCanvasGroup(_unitSpriteCanvasGroup, 0f, duration / 2f, () => {
                                unitSpriteImage.sprite = unitDisplaySprite;
                                unitSpriteImage.enabled = true;
                                _unitSpriteFadeCoroutine = _coroutineRunner.StartCoroutine(FadeCanvasGroup(_unitSpriteCanvasGroup, 1f, duration / 2f, "UnitSprite_FadeInAfterSwap"));
                            }, "UnitSprite_FadeOutOld"));
                        } else { 
                            unitSpriteImage.sprite = unitDisplaySprite;
                            unitSpriteImage.enabled = true;
                            _unitSpriteFadeCoroutine = _coroutineRunner.StartCoroutine(FadeCanvasGroup(_unitSpriteCanvasGroup, 1f, duration, "UnitSprite_DirectFadeIn"));
                        }
                    } else if (needsFadeIn) { 
                        unitSpriteImage.enabled = true; 
                         _unitSpriteFadeCoroutine = _coroutineRunner.StartCoroutine(FadeCanvasGroup(_unitSpriteCanvasGroup, 1f, duration, "UnitSprite_FadeInSameSprite"));
                    }
                } else { 
                    _unitSpriteFadeCoroutine = _coroutineRunner.StartCoroutine(FadeCanvasGroup(_unitSpriteCanvasGroup, 0f, duration, () => { if(unitSpriteImage != null) unitSpriteImage.enabled = false; }, "UnitSprite_FadeOutEmpty"));
                }
            }

            if (unitSpriteImage != null && unitSpriteImage.rectTransform.localScale != (Vector3)targetSpriteScale)
            {
                if (_spriteScaleCoroutine != null) _coroutineRunner.StopCoroutine(_spriteScaleCoroutine);
                _spriteScaleCoroutine = _coroutineRunner.StartCoroutine(AnimateScale(unitSpriteImage.rectTransform, targetSpriteScale, duration));
            }

            if (_layoutElement != null && (_layoutElement.preferredWidth != targetSlotDimensions.x || _layoutElement.preferredHeight != targetSlotDimensions.y))
            {
                if (_slotSizeCoroutine != null) _coroutineRunner.StopCoroutine(_slotSizeCoroutine);
                _slotSizeCoroutine = _coroutineRunner.StartCoroutine(AnimateLayoutElementSize(_layoutElement, targetSlotDimensions, duration));
            }
        }
        else
        {
            if (slotGameObject.activeSelf) {
                CanvasGroup rootCG = slotGameObject.GetComponent<CanvasGroup>();
                if (rootCG != null)
                {
                    if (_backgroundFadeCoroutine != null) _coroutineRunner.StopCoroutine(_backgroundFadeCoroutine);
                    if (_unitSpriteFadeCoroutine != null) _coroutineRunner.StopCoroutine(_unitSpriteFadeCoroutine);
                    _coroutineRunner.StartCoroutine(FadeCanvasGroup(rootCG, 0f, duration, () => slotGameObject.SetActive(false), "Slot_RootFadeOut"));
                }
                else
                {
                    if (_unitSpriteCanvasGroup != null) _unitSpriteCanvasGroup.alpha = 0;
                    if (unitSpriteImage != null) unitSpriteImage.enabled = false;
                    if (_slotBackgroundCanvasGroup != null) _slotBackgroundCanvasGroup.alpha = 0;
                    if (slotBackgroundImage != null) slotBackgroundImage.enabled = false;
                    slotGameObject.SetActive(false);
                }
            }
        }
        // Debug.Log($"SLOT {slotGameObject.name}: PerformTransition has LAUNCHED SUB-TASKS for Unit: {unit?.unitName}", _coroutineRunner);
        _transitionCoroutine = null;
        yield return null;
    }

    private IEnumerator AnimateImageChange(CanvasGroup cg, Image image, Sprite newSprite, Vector2 newSize, float duration, string debugID = "")
    {
        if (cg == null || image == null) { /* Debug.LogWarning($"SLOT {debugID} AnimateImageChange ABORTED: cg or image is null for slot {slotGameObject.name}", _coroutineRunner); */ yield break; }

        bool spriteIsChanging = (image.sprite != newSprite);
        bool sizeIsChanging = (image.rectTransform.sizeDelta != newSize);
        
        // Debug.Log($"SLOT {image.transform.parent.name}: AnimateImageChange START ({debugID}) for {image.gameObject.name}. TargetSprite: {newSprite?.name}, CurrentSprite: {image.sprite?.name}, SizeChanging: {sizeIsChanging}", _coroutineRunner);

        if (spriteIsChanging) {
            image.sprite = newSprite; 
            // Debug.Log($"SLOT {image.transform.parent.name}: AnimateImageChange - INSTANT SWAP sprite for {image.gameObject.name} to {newSprite?.name} ({debugID})", _coroutineRunner);
        }
        
        image.enabled = (newSprite != null);
        image.rectTransform.sizeDelta = newSize;

        float targetAlpha = (newSprite != null) ? 1f : 0f;

        if (duration <= 0 || (Mathf.Approximately(cg.alpha, targetAlpha) && !spriteIsChanging && image.enabled == (newSprite != null) )) {
            cg.alpha = targetAlpha;
            // Debug.Log($"SLOT {image.transform.parent.name}: AnimateImageChange INSTANT/NO-OP ({debugID}) for {image.gameObject.name}. TargetAlpha: {targetAlpha}", _coroutineRunner);
            if (debugID == "BG_ImageChange") _backgroundFadeCoroutine = null;
            yield break;
        }
        
        // Debug.Log($"SLOT {image.transform.parent.name}: AnimateImageChange - Fading alpha for {image.gameObject.name} from {cg.alpha} to {targetAlpha} ({debugID})", _coroutineRunner);
        yield return _coroutineRunner.StartCoroutine(FadeCanvasGroup(cg, targetAlpha, duration, "AnimateImageChange_FadeToTarget"));
        
        if (debugID == "BG_ImageChange") _backgroundFadeCoroutine = null; 
        // Debug.Log($"SLOT {image.transform.parent.name}: AnimateImageChange END ({debugID}) for {image.gameObject.name}", _coroutineRunner);
    }

    private IEnumerator AnimateScale(RectTransform targetTransform, Vector2 targetScale, float duration)
    {
        if (targetTransform == null) yield break;
        // Debug.Log($"SLOT {targetTransform.parent.name}: AnimateScale START for {targetTransform.gameObject.name}", _coroutineRunner);
        Vector3 startScale = targetTransform.localScale;
        Vector3 endScale = new Vector3(targetScale.x, targetScale.y, startScale.z); 
        float timer = 0f;

        if (duration <= 0) { targetTransform.localScale = endScale; _spriteScaleCoroutine = null; /* Debug.Log($"SLOT {targetTransform.parent.name}: AnimateScale INSTANT for {targetTransform.gameObject.name}", _coroutineRunner); */ yield break; }

        while (timer < duration)
        {
            targetTransform.localScale = Vector3.Lerp(startScale, endScale, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
        targetTransform.localScale = endScale;
        _spriteScaleCoroutine = null;
        // Debug.Log($"SLOT {targetTransform.parent.name}: AnimateScale END for {targetTransform.gameObject.name}", _coroutineRunner);
    }

    private IEnumerator AnimateLayoutElementSize(LayoutElement le, Vector2 targetSize, float duration)
    {
        if (le == null) yield break;
        // Debug.Log($"SLOT {le.gameObject.name}: AnimateLayoutElementSize START", _coroutineRunner);
        Vector2 startSize = new Vector2(le.preferredWidth, le.preferredHeight);
        float timer = 0f;

        if (duration <= 0) { le.preferredWidth = targetSize.x; le.preferredHeight = targetSize.y; _slotSizeCoroutine = null; /* Debug.Log($"SLOT {le.gameObject.name}: AnimateLayoutElementSize INSTANT", _coroutineRunner); */ yield break; }

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
        // Debug.Log($"SLOT {le.gameObject.name}: AnimateLayoutElementSize END", _coroutineRunner);
    }

    public static IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration, string debugID = "") 
    {
        if (cg == null) { /* Debug.LogWarning($"FadeCanvasGroup ABORTED ({debugID}): cg is null"); */ yield break; }
        float startAlpha = cg.alpha;
        float timer = 0f;

        if (duration <= 0) 
        {
            cg.alpha = targetAlpha;
            yield break;
        }
        
        while (timer < duration)
        {
            if (cg == null) yield break; 
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
        if (cg != null) cg.alpha = targetAlpha;
    }
     
    public static IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration, System.Action onComplete, string debugID = "")
    {
        if (cg == null) { /* Debug.LogWarning($"FadeCanvasGroup (with onComplete) ABORTED ({debugID}): cg is null"); */ onComplete?.Invoke(); yield break; }
        float startAlpha = cg.alpha;
        float timer = 0f;

        if (duration <= 0) 
        {
            cg.alpha = targetAlpha;
            onComplete?.Invoke();
            yield break;
        }
        
        while (timer < duration)
        {
            if (cg == null) { onComplete?.Invoke(); yield break; } 
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
        if (cg != null) cg.alpha = targetAlpha;
        onComplete?.Invoke();
    }
}


public class TurnOrderUI : MonoBehaviour 
{
    [Header("UI Prefabs & References")]
    public GameObject unitTurnSlotPrefab;
    public TextMeshProUGUI activeUnitNameTextDisplay;
    private CanvasGroup _activeUnitNameCanvasGroup; 

    [Header("Transition Settings")]
    public float transitionDuration = 0.3f;

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

        LayoutElement prefabRootLayoutElement = unitTurnSlotPrefab.GetComponent<LayoutElement>();
        if (prefabRootLayoutElement == null) { Debug.LogError("TurnOrderUI: UnitTurnSlot_Prefab root is MISSING LayoutElement. Disabling.", this); this.enabled = false; return; }
        
        Image bgImageInPrefab = unitTurnSlotPrefab.transform.Find("Slot_Background_Image")?.GetComponent<Image>();
        if (bgImageInPrefab == null || bgImageInPrefab.GetComponent<CanvasGroup>() == null) { Debug.LogError("TurnOrderUI: Prefab's Slot_Background_Image or its CanvasGroup not found/set up.", this); this.enabled = false; return; }
        Image unitSpriteInPrefab = unitTurnSlotPrefab.transform.Find("Unit_Sprite_Image")?.GetComponent<Image>();
        if (unitSpriteInPrefab == null || unitSpriteInPrefab.GetComponent<CanvasGroup>() == null) { Debug.LogError("TurnOrderUI: Prefab's Unit_Sprite_Image or its CanvasGroup not found/set up.", this); this.enabled = false; return; }


        if (_activeUnitNameCanvasGroup != null) _activeUnitNameCanvasGroup.alpha = 0f; 
        activeUnitNameTextDisplay.gameObject.SetActive(true); 

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
            UnitTurnSlotUI slotUI = new UnitTurnSlotUI(slotInstance, this,
                                                       activeUnitDiamondSprite, upcomingUnitDiamondSprite,
                                                       activeUnitSpriteScale, normalUnitSpriteScale,
                                                       activeSlotDimensions, upcomingSlotDimensions,
                                                       activeSlotBackgroundImageSize, upcomingSlotBackgroundImageSize);
            _uiSlots.Add(slotUI);
            slotInstance.SetActive(false);
        }
    }

    public void UpdateTurnOrderDisplay(List<Unit> upcomingUnits, Unit activeUnit)
    {
        if (_uiSlots.Count > 0 && _uiSlots[0] != null && _uiSlots[0].slotBackgroundImage != null)
        {
            // This log can be very spammy, consider removing or reducing its frequency if everything works
            // Debug.Log($"<<<<< TurnOrderUI.UpdateTurnOrderDisplay ENTRY: Slot 0 Current BG: {_uiSlots[0].slotBackgroundImage.sprite?.name}, ActiveUnit Param: {activeUnit?.unitName} >>>>>", this);
        }

        if (upcomingUnits == null)
        {
            Debug.LogWarning("TurnOrderUI.UpdateTurnOrderDisplay: upcomingUnits list is null. Clearing display.", this);
            for (int i = 0; i < _uiSlots.Count; i++) 
            {
                if (_uiSlots[i] != null) _uiSlots[i].TransitionToState(null, false, transitionDuration); 
            }
            if (_activeUnitNameCanvasGroup != null)
            {
                if (_activeNameFadeCoroutine != null) StopCoroutine(_activeNameFadeCoroutine);
                _activeNameFadeCoroutine = StartCoroutine(UnitTurnSlotUI.FadeCanvasGroup(_activeUnitNameCanvasGroup, 0f, transitionDuration, "ActiveName_Clear"));
            }
            return;
        }

        bool activeUnitFoundInList = false;
        for (int i = 0; i < _uiSlots.Count; i++)
        {
            if (_uiSlots[i] == null) continue; 

            if (i < upcomingUnits.Count)
            {
                Unit unitToShow = upcomingUnits[i];
                bool isThisUnitActive = (unitToShow == activeUnit);
                
                // This log can also be spammy.
                // Debug.Log($"TurnOrderUI.Update: Slot {i} ({_uiSlots[i].slotGameObject.name}), Unit: {unitToShow?.unitName}, IsActiveTarget: {isThisUnitActive}, ActiveUnitParam: {activeUnit?.unitName}, TargetSprite: {(isThisUnitActive ? activeUnitDiamondSprite?.name : upcomingUnitDiamondSprite?.name)}", this);

                _uiSlots[i].TransitionToState(unitToShow, isThisUnitActive, transitionDuration); 

                if (isThisUnitActive && unitToShow != null) activeUnitFoundInList = true;
            }
            else
            {
                _uiSlots[i].TransitionToState(null, false, transitionDuration); 
            }
        }

        if (_activeUnitNameCanvasGroup != null && activeUnitNameTextDisplay != null)
        {
            if (_activeNameFadeCoroutine != null) StopCoroutine(_activeNameFadeCoroutine);
            if (activeUnit != null && activeUnitFoundInList) 
            {
                activeUnitNameTextDisplay.text = activeUnit.unitName;
                _activeNameFadeCoroutine = StartCoroutine(UnitTurnSlotUI.FadeCanvasGroup(_activeUnitNameCanvasGroup, 1f, transitionDuration, "ActiveName_Show"));
            }
            else
            {
                _activeNameFadeCoroutine = StartCoroutine(UnitTurnSlotUI.FadeCanvasGroup(_activeUnitNameCanvasGroup, 0f, transitionDuration, "ActiveName_Hide"));
            }
        }
    }
}