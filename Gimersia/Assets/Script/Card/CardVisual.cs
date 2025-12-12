using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class CardVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Visual References")]
    [SerializeField] private Image cardIconImage;
    [SerializeField] private TextMeshProUGUI cardNameText;

    [Header("Card Data")]
    [SerializeField] private string cardName = "Card";
    [SerializeField] private string cardDescription = "Description";
    [SerializeField] private Sprite cardIcon;

    [Header("Animation Settings")]
    [SerializeField] private float transitionDuration = 0.5f;
    [SerializeField] private float transitionArcHeight = 100f;
    [SerializeField] private float transitionScaleFactor = 0.3f;
    [SerializeField] private Ease transitionEase = Ease.OutCubic;

    [Header("Use/Discard Animation Settings")]
    [SerializeField] private float exitAnimationDuration = 1.0f;
    [SerializeField] private float exitDistance = 1200f;
    [SerializeField] private float exitRotation = 30f;
    [SerializeField] private Ease exitEase = Ease.OutCubic;

    [Header("Animation Variations")]
    [SerializeField] private float yOffsetVariation = 80f;
    [SerializeField] private float slightDurationVariation = 0.1f;

    [Header("Scale Settings")]
    [SerializeField] private float useStartScale = 1.2f;
    [SerializeField] private float useEndScale = 0.5f;
    [SerializeField] private float discardEndScale = 0.5f;

    [Header("Tooltip Settings")]
    [SerializeField] private bool disableTooltipDuringTransition = true;

    private CardSlot parentSlot;
    private CardHandHolder handHolder;
    private CardSelectionHolder selectionHolder;
    private BaseCard cardComponent;
    private CardAudio cardAudio;

    private Vector3 targetPosition;
    private float targetRotationZ;
    private float targetScale = 1f;

    private bool isHovered = false;
    private bool isInSelectionHolder = false;
    private bool isTransitioning = false;
    private bool isTooltipForcedHidden = false;

    private Canvas cardCanvas;
    private int baseSortingOrder = 0;
    private const int HOVER_SORT_BOOST = 1000;

    private Tween currentTransitionTween;
    private RectTransform rectTransform;

    #region Unity Methods
    private void Awake()
    {
        InitializeComponents();
        SetupCanvas();
    }

    private void Update()
    {
        if (!isTransitioning)
        {
            UpdateNormalMovement();
        }

        if (isHovered && !isTooltipForcedHidden && CardTooltip.Instance != null && CardTooltip.Instance.IsShowing())
        {
            UpdateTooltipPosition();
        }
    }

    private void OnDestroy()
    {
        CleanupTweens();

        if (isHovered)
        {
            HideTooltip();
        }
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        rectTransform = GetComponent<RectTransform>();
        cardComponent = GetComponent<BaseCard>();
        cardAudio = GetComponent<CardAudio>();
    }

    private void SetupCanvas()
    {
        cardCanvas = gameObject.GetComponent<Canvas>();
        if (cardCanvas == null)
        {
            cardCanvas = gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();
        }
        cardCanvas.overrideSorting = true;
        cardCanvas.sortingOrder = 0;
    }

    public void Initialize(CardSlot slot)
    {
        parentSlot = slot;
        handHolder = slot.GetHandHolder();

        if (selectionHolder == null)
        {
            selectionHolder = FindObjectOfType<CardSelectionHolder>();
        }

        ResetTransform();
    }

    private void ResetTransform()
    {
        rectTransform.localPosition = Vector3.zero;
        rectTransform.localRotation = Quaternion.identity;

        if (handHolder != null)
        {
            targetScale = handHolder.GetHandScale();
        }
        rectTransform.localScale = Vector3.one * targetScale;
    }
    #endregion

    #region Card Data Management
    public void SetCardData(string name, string description, Sprite icon)
    {
        cardName = name;
        cardDescription = description;
        cardIcon = icon;

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (cardNameText != null)
        {
            cardNameText.text = cardName;
        }

        if (cardIconImage != null && cardIcon != null)
        {
            cardIconImage.sprite = cardIcon;
        }
    }
    #endregion

    #region Movement & Positioning
    private void UpdateNormalMovement()
    {
        float moveSpeed = GetCurrentMoveSpeed();
        float snapThreshold = 0.01f;

        float posDistance = Vector3.Distance(rectTransform.localPosition, targetPosition);
        if (posDistance > snapThreshold)
        {
            rectTransform.localPosition = Vector3.Lerp(
                rectTransform.localPosition,
                targetPosition,
                Time.deltaTime * moveSpeed
            );
        }
        else
        {
            rectTransform.localPosition = targetPosition;
        }

        Quaternion targetQuat = Quaternion.Euler(0, 0, targetRotationZ);
        float rotDistance = Quaternion.Angle(rectTransform.localRotation, targetQuat);
        if (rotDistance > snapThreshold)
        {
            rectTransform.localRotation = Quaternion.Lerp(
                rectTransform.localRotation,
                targetQuat,
                Time.deltaTime * moveSpeed
            );
        }
        else
        {
            rectTransform.localRotation = targetQuat;
        }

        float scaleDistance = Mathf.Abs(rectTransform.localScale.x - targetScale);
        if (scaleDistance > snapThreshold * 0.1f)
        {
            rectTransform.localScale = Vector3.Lerp(
                rectTransform.localScale,
                Vector3.one * targetScale,
                Time.deltaTime * moveSpeed
            );
        }
        else
        {
            rectTransform.localScale = Vector3.one * targetScale;
        }
    }

    private float GetCurrentMoveSpeed()
    {
        if (isInSelectionHolder && selectionHolder != null)
        {
            return selectionHolder.GetCardMoveSpeed();
        }

        if (handHolder != null)
        {
            return handHolder.GetCardMoveSpeed();
        }

        return 10f;
    }

    public void SetTargetPosition(Vector3 pos) => targetPosition = pos;
    public void SetTargetRotation(float rotZ) => targetRotationZ = rotZ;
    public void SetTargetScale(float scale) => targetScale = scale;
    #endregion

    #region Transition Animation
    public void StartTransitionTo(Transform targetParent, Vector3 worldTargetPos)
    {
        CleanupTweens();

        Vector3 worldStartPos = rectTransform.position;
        Quaternion targetRotation = Quaternion.identity;

        MoveToCanvasRoot();
        isTransitioning = true;

        if (disableTooltipDuringTransition && isHovered)
        {
            ForceHideTooltip();
        }

        float destinationScale = GetDestinationScale(targetParent);

        currentTransitionTween = DOTween.Sequence()
            .Append(rectTransform.DOMove(worldTargetPos, transitionDuration))
            .Join(rectTransform.DORotateQuaternion(targetRotation, transitionDuration))
            .Join(rectTransform.DOScale(destinationScale, transitionDuration))
            .SetEase(transitionEase)
            .OnUpdate(() => UpdateTransitionArc(worldStartPos, worldTargetPos))
            .OnComplete(() => CompleteTransitionTo(targetParent));
    }

    private float GetDestinationScale(Transform targetParent)
    {
        CardSelectionHolder selectionDest = targetParent?.GetComponentInParent<CardSelectionHolder>();
        if (selectionDest != null)
        {
            return selectionDest.GetSelectedScale();
        }

        CardHandHolder handDest = targetParent?.GetComponentInParent<CardHandHolder>();
        if (handDest != null)
        {
            return handDest.GetHandScale();
        }

        return targetScale;
    }

    private void UpdateTransitionArc(Vector3 startPos, Vector3 targetPos)
    {
        if (currentTransitionTween == null) return;

        float progress = currentTransitionTween.ElapsedPercentage();

        float arcHeight = Mathf.Sin(progress * Mathf.PI) * transitionArcHeight;
        Vector3 basePos = Vector3.Lerp(startPos, targetPos, progress);
        basePos.y += arcHeight;
        rectTransform.position = basePos;

        float currentScale = rectTransform.localScale.x;
        float scaleVariation = Mathf.Sin(progress * Mathf.PI) * transitionScaleFactor;
        rectTransform.localScale = Vector3.one * (currentScale + scaleVariation);
    }

    public void CompleteTransitionTo(Transform targetParent)
    {
        CleanupTweens();

        Vector3 worldPos = rectTransform.position;

        transform.SetParent(targetParent, false);

        Vector3 targetLocalPos = targetParent.InverseTransformPoint(worldPos);
        rectTransform.localPosition = targetLocalPos;
        rectTransform.localRotation = Quaternion.identity;

        float finalScale = GetDestinationScale(targetParent);
        rectTransform.localScale = Vector3.one * finalScale;
        targetScale = finalScale;

        targetPosition = rectTransform.localPosition;
        targetRotationZ = 0f;

        isTransitioning = false;

        if (isTooltipForcedHidden && isHovered)
        {
            RestoreTooltip();
        }
    }
    #endregion

    #region Use/Discard Animation
    public void AnimateUseCard()
    {
        CleanupTweens();
        CallCardPreUse();
        PrepareExitAnimation();
        PlayExitAnimation(exitDistance, -exitRotation, useStartScale, useEndScale, true);
        cardAudio.PlayUseAudio();
    }

    public void AnimateDiscardCard()
    {
        CleanupTweens();
        PrepareExitAnimation();
        PlayExitAnimation(-exitDistance, exitRotation, targetScale, discardEndScale, false);
        cardAudio.PlayUseAudio();
    }

    private void CallCardPreUse()
    {
        if (cardComponent != null)
        {
            cardComponent.OnPreUse();
        }
    }

    private void PrepareExitAnimation()
    {
        isTransitioning = true;
        MoveToCanvasRoot();
        BringToFront();

        if (isHovered)
        {
            ForceHideTooltip();
        }

        if (currentTransitionTween == null)
        {
            rectTransform.localScale = Vector3.one * useStartScale;
        }
    }

    private void PlayExitAnimation(float xDistance, float rotationAmount, float startScale, float endScale, bool isUse)
    {
        Vector3 currentPos = rectTransform.position;
        Vector3 currentRotation = rectTransform.eulerAngles;

        float yOffset = Random.Range(-yOffsetVariation, yOffsetVariation);
        float variedDuration = exitAnimationDuration * Random.Range(1f - slightDurationVariation, 1f + slightDurationVariation);
        Vector3 targetPos = currentPos + new Vector3(xDistance, yOffset, 0);

        float startRotationZ = currentRotation.z;
        float targetRotationZ = startRotationZ + rotationAmount;

        currentTransitionTween = DOTween.Sequence()
            .Append(rectTransform.DOMove(targetPos, variedDuration).SetEase(exitEase))
            .Join(DOVirtual.Float(startRotationZ, targetRotationZ, variedDuration, value =>
            {
                rectTransform.rotation = Quaternion.Euler(0, 0, value);
            }).SetEase(Ease.OutCubic))
            .Join(rectTransform.DOScale(endScale, variedDuration).SetEase(Ease.InBack))
            .OnUpdate(() => UpdateExitArc())
            .OnComplete(() =>
            {
                if (isUse && cardComponent != null)
                {
                    cardComponent.OnPostUse();
                }
            });
    }

    private void UpdateExitArc()
    {
        if (currentTransitionTween == null) return;

        float progress = currentTransitionTween.ElapsedPercentage();
        float arcHeight = Mathf.Sin(progress * Mathf.PI) * 60f;
        Vector3 posWithArc = rectTransform.position;
        posWithArc.y += arcHeight;
        rectTransform.position = posWithArc;
    }
    #endregion

    #region Helper Methods
    private void MoveToCanvasRoot()
    {
        Canvas rootCanvas = GetRootCanvas();
        if (rootCanvas != null)
        {
            transform.SetParent(rootCanvas.transform, true);
            transform.SetAsLastSibling();
        }
    }

    private Canvas GetRootCanvas()
    {
        Canvas[] canvases = GetComponentsInParent<Canvas>();
        return canvases.Length > 0 ? canvases[canvases.Length - 1] : null;
    }

    private void BringToFront()
    {
        if (cardCanvas != null)
        {
            cardCanvas.sortingOrder = 9999;
        }
    }

    private void CleanupTweens()
    {
        if (currentTransitionTween != null && currentTransitionTween.IsActive())
        {
            currentTransitionTween.Kill();
            currentTransitionTween = null;
        }
    }
    #endregion

    #region Pointer Events
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isTransitioning) return;

        isHovered = true;
        cardCanvas.sortingOrder = baseSortingOrder + HOVER_SORT_BOOST;

        NotifyHolderOfHover(true);
        ShowTooltip();

        cardAudio.PlayHoverAudio();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        cardCanvas.sortingOrder = baseSortingOrder;

        NotifyHolderOfHover(false);
        HideTooltip();

        isTooltipForcedHidden = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isTransitioning) return;

        if (isHovered)
        {
            ForceHideTooltip();
        }

        if (isInSelectionHolder)
        {
            selectionHolder?.RemoveCard(this);
        }
        else
        {
            handHolder?.OnCardClicked(this);
        }
    }

    private void NotifyHolderOfHover(bool isHovering)
    {
        if (isInSelectionHolder)
        {
            selectionHolder?.SetHoveredCard(isHovering ? this : null);
        }
        else
        {
            handHolder?.SetHoveredCard(isHovering ? this : null);
        }
    }
    #endregion

    #region Tooltip Management
    private void ShowTooltip()
    {
        if (isTooltipForcedHidden) return;

        CardTooltip.Instance?.ShowTooltip(cardName, cardDescription, cardIcon, rectTransform.position, isInSelectionHolder);
    }

    private void HideTooltip()
    {
        CardTooltip.Instance?.HideTooltip();
        isTooltipForcedHidden = false;
    }

    private void ForceHideTooltip()
    {
        CardTooltip.Instance?.HideTooltip();
        isTooltipForcedHidden = true;
    }

    private void RestoreTooltip()
    {
        if (isHovered && !isTransitioning)
        {
            isTooltipForcedHidden = false;
            ShowTooltip();
        }
    }

    private void UpdateTooltipPosition()
    {
        if (CardTooltip.Instance != null && CardTooltip.Instance.IsShowing())
        {
            CardTooltip.Instance.ShowTooltip(cardName, cardDescription, cardIcon, rectTransform.position, isInSelectionHolder);
        }
    }
    #endregion

    #region Sorting Order
    public void SetBaseSortingOrder(int order)
    {
        baseSortingOrder = order;
        if (!isHovered)
        {
            cardCanvas.sortingOrder = baseSortingOrder;
        }
    }
    #endregion

    #region Public Methods
    public void SetInSelectionHolder(bool inHolder)
    {
        isInSelectionHolder = inHolder;

        if (isHovered && CardTooltip.Instance != null && CardTooltip.Instance.IsShowing())
        {
            UpdateTooltipPosition();
        }
    }

    public void SetSlot(CardSlot slot) => parentSlot = slot;

    public CardSlot GetSlot() => parentSlot;
    public bool IsHovered() => isHovered;
    public bool IsTransitioning() => isTransitioning;
    public string GetCardName() => cardName;
    public string GetCardDescription() => cardDescription;
    public Sprite GetCardIcon() => cardIcon;
    public BaseCard GetCardComponent() => cardComponent;

    public float GetUseStartScale() => useStartScale;
    public float GetUseEndScale() => useEndScale;
    public float GetDiscardEndScale() => discardEndScale;
    #endregion
}
