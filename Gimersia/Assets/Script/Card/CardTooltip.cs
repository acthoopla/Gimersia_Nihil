using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardTooltip : MonoBehaviour
{
    public static CardTooltip Instance { get; private set; }

    [Header("Tooltip Settings")]
    [SerializeField] private float fadeSpeed = 10f;

    [Header("UI Settings")]
    [SerializeField] private GameObject tooltipObject;
    [SerializeField] private TextMeshProUGUI cardNameText;
    [SerializeField] private TextMeshProUGUI cardDescriptionText;
    [SerializeField] private Image cardIconImage;

    [Header("Layout")]
    [SerializeField] private LayoutElement layoutElement;
    [SerializeField] private float maxWidth = 400f;

    [Header("Offset Settings")]
    [SerializeField] private Vector2 handOffset = new Vector2(0, 150f);
    [SerializeField] private Vector2 selectionOffset = new Vector2(0, -150f);

    [Header("Sorting Settings")]
    [SerializeField] private int tooltipSortingOrder = 10000;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas tooltipCanvas;
    private bool isShowing = false;

    #region Unity Methods
    private void Awake()
    {
        InitializeSingleton();
        InitializeComponents();
        HideTooltipImmediate();
    }

    private void Update()
    {
        UpdateFade();
    }
    #endregion

    #region Initialization
    private void InitializeSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeComponents()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        tooltipCanvas = GetComponent<Canvas>();
        if (tooltipCanvas == null)
        {
            tooltipCanvas = gameObject.AddComponent<Canvas>();
        }

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        tooltipCanvas.overrideSorting = true;
        tooltipCanvas.sortingOrder = tooltipSortingOrder;

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (layoutElement == null)
        {
            layoutElement = GetComponent<LayoutElement>();
        }

        if (layoutElement != null)
        {
            layoutElement.preferredWidth = maxWidth;
        }
    }

    private void HideTooltipImmediate()
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }

        canvasGroup.alpha = 0f;
    }
    #endregion

    #region Fade Animation
    private void UpdateFade()
    {
        if (isShowing)
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1f, Time.deltaTime * fadeSpeed);
        }
        else
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 0f, Time.deltaTime * fadeSpeed);

            if (canvasGroup.alpha < 0.01f && tooltipObject != null)
            {
                tooltipObject.SetActive(false);
            }
        }
    }
    #endregion

    #region Private Methods
    private void ActivateTooltip()
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(true);
        }

        if (tooltipCanvas != null)
        {
            tooltipCanvas.sortingOrder = tooltipSortingOrder;
        }

        isShowing = true;
    }

    private void SetTooltipContent(string cardName, string cardDescription, Sprite cardIcon)
    {
        if (cardNameText != null)
        {
            cardNameText.text = cardName;
        }

        if (cardDescriptionText != null)
        {
            cardDescriptionText.text = cardDescription;
        }

        if (cardIconImage != null && cardIcon != null)
        {
            cardIconImage.sprite = cardIcon;
            cardIconImage.gameObject.SetActive(true);
        }
        else if (cardIconImage != null)
        {
            cardIconImage.gameObject.SetActive(false);
        }
    }

    private void UpdatePosition(Vector3 cardPosition, bool isInSelectionHolder = false)
    {
        if (rectTransform == null) return;

        Vector2 currentOffset = isInSelectionHolder ? selectionOffset : handOffset;
        Vector3 targetPosition = cardPosition + (Vector3)currentOffset;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            targetPosition = ClampToScreenBounds(targetPosition);
        }

        rectTransform.position = targetPosition;
    }

    private Vector3 ClampToScreenBounds(Vector3 targetPosition)
    {
        Vector2 tooltipSize = rectTransform.sizeDelta;

        float minX = tooltipSize.x / 2;
        float maxX = Screen.width - tooltipSize.x / 2;
        float minY = tooltipSize.y / 2;
        float maxY = Screen.height - tooltipSize.y / 2;

        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);

        return targetPosition;
    }

    private void ForceLayoutRebuild()
    {
        if (layoutElement != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }
    #endregion

    #region Public Methods
    public void ShowTooltip(CardDataSO cardData, Vector3 cardPosition, bool isInSelectionHolder = false)
    {
        if (cardData == null) return;

        ShowTooltip(cardData.CardName, cardData.CardDescription, cardData.CardIcon, cardPosition, isInSelectionHolder);
    }

    public void ShowTooltip(string cardName, string cardDescription, Sprite cardIcon, Vector3 cardPosition, bool isInSelectionHolder = false)
    {
        ActivateTooltip();
        SetTooltipContent(cardName, cardDescription, cardIcon);
        UpdatePosition(cardPosition, isInSelectionHolder);
        ForceLayoutRebuild();
    }

    public void HideTooltip()
    {
        isShowing = false;
    }

    public bool IsShowing()
    {
        return isShowing;
    }
    #endregion
}
