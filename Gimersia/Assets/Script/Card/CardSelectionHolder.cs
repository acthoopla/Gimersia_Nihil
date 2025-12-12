using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

public class CardSelectionHolder : MonoBehaviour
{
    [Header("Selection Settings")]
    [SerializeField] private int maxCards = 5;

    [Header("Visual Settings")]
    [SerializeField] private float selectedScale = 1.2f;

    [Header("Card Layout Settings")]
    [SerializeField] private float cardSpacing = 120f;
    [SerializeField] private float cardMoveSpeed = 10f;

    [Header("Hover Settings")]
    [SerializeField] private float hoverYOffset = 80f;
    [SerializeField] private float hoverScale = 1.2f;

    [Header("UI Settings")]
    [SerializeField] private Transform selectionHolder;
    [SerializeField] private GameObject cardSlotPrefab;
    [SerializeField] private GameObject useButton;
    [SerializeField] private GameObject discardButton;

    [Header("Animation Settings")]
    [SerializeField] private float cardAnimationDelay = 0.15f;

    private List<CardSlot> selectionSlots = new List<CardSlot>();
    private CardVisual hoveredCard;
    private CardHandHolder cardHandHolder;
    private List<BaseCard> cardsInSelection = new List<BaseCard>();
    private PlayerState currentPlayer;

    public bool IsAnimating { get; private set; }

    #region Unity Methods
    private void Start()
    {
        CreateSelectionSlots(maxCards);
        InitializeCardHandHolder();

        if (useButton != null) useButton.GetComponent<Button>().onClick.AddListener(OnUseButtonClicked);
        if (discardButton != null) discardButton.GetComponent<Button>().onClick.AddListener(DiscardSelectedCards);

        HideButtons();
        currentPlayer = FindObjectOfType<PlayerState>();
    }

    private void Update()
    {
        UpdateCardPositions();
    }
    #endregion

    #region Initialization
    private void InitializeCardHandHolder()
    {
        if (cardHandHolder == null) cardHandHolder = FindObjectOfType<CardHandHolder>();
    }

    public void CreateSelectionSlots(int count)
    {
        ClearExistingSlots();
        for (int i = 0; i < count; i++) CreateSlot(i);
    }

    private void ClearExistingSlots()
    {
        foreach (var slot in selectionSlots) if (slot != null) Destroy(slot.gameObject);
        selectionSlots.Clear();
        cardsInSelection.Clear();
    }

    private void CreateSlot(int index)
    {
        GameObject slotObj = Instantiate(cardSlotPrefab, selectionHolder);
        slotObj.name = $"SelectionSlot_{index}";

        CardSlot slot = slotObj.GetComponent<CardSlot>();
        if (slot == null) slot = slotObj.AddComponent<CardSlot>();

        slot.Initialize(index, null);
        selectionSlots.Add(slot);
    }
    #endregion

    #region Card Management (ADD CARD NORMAL)
    public bool AddCard(CardVisual card)
    {
        if (IsFull()) return false;
        CardSlot emptySlot = GetFirstEmptySlot();
        if (emptySlot == null) return false;

        TransferCardToSelection(card, emptySlot);
        return true;
    }

    private void TransferCardToSelection(CardVisual card, CardSlot targetSlot)
    {
        CardSlot originalSlot = card.GetSlot();
        originalSlot?.SetCard(null);

        BaseCard cardComponent = card.GetCardComponent();
        if (cardComponent != null)
        {
            if (cardHandHolder == null) InitializeCardHandHolder();

            cardHandHolder?.RemoveCardFromHand(cardComponent);
            cardsInSelection.Add(cardComponent);

            if (currentPlayer != null)
            {
                currentPlayer.SelectCardToSlot(cardComponent.GetCardData());

                UpdateHighlight();
            }
        }

        card.SetSlot(targetSlot);
        card.SetInSelectionHolder(true);
        targetSlot.SetCard(card);

        Vector3 finalWorldPos = CalculateCardFinalWorldPosition(targetSlot);
        card.StartTransitionTo(targetSlot.transform, finalWorldPos);

        StartCoroutine(WaitForTransitionComplete(card, targetSlot.transform));
        UpdateButtonVisibility();
    }

    public void RemoveCard(CardVisual card)
    {
        if (card == null) return;

        GameCard logicCard = card.GetComponent<GameCard>();
        if (logicCard != null && currentPlayer != null)
        {
            currentPlayer.ReturnCardToHand(logicCard.GetData());

            UpdateHighlight();
        }

        CardSlot currentSlot = card.GetSlot();
        if (currentSlot != null) currentSlot.SetCard(null);

        BaseCard cardComponent = card.GetCardComponent();
        if (cardComponent != null) cardsInSelection.Remove(cardComponent);

        Destroy(card.gameObject);
        UpdateButtonVisibility();
    }

    private IEnumerator WaitForTransitionComplete(CardVisual card, Transform targetParent)
    {
        while (card != null && card.IsTransitioning()) yield return null;
        if (card != null)
        {
            card.CompleteTransitionTo(targetParent);
            UpdateCardPositions();
        }
    }
    #endregion

    #region Use Cards (DOUBLE TRIGGER FIX)
    public void OnUseButtonClicked()
    {
        StartCoroutine(AnimateUseCards(true));
    }

    public IEnumerator AnimateUseCards(bool executeLogic)
    {
        IsAnimating = true;
        List<CardVisual> visualsToUse = CollectAllCardVisuals();

        if (UIController.Instance != null) UIController.Instance.ShowModifierPanel();

        HideButtons();

        for (int i = 0; i < visualsToUse.Count; i++)
        {
            CardVisual visual = visualsToUse[i];
            if (visual != null)
            {
                if (executeLogic)
                {
                    var logicComp = visual.GetComponent<GameCard>();
                    if (logicComp != null)
                    {
                        NewCardData data = logicComp.GetCardData();
                        if (currentPlayer != null)
                        {
                            data.Play(currentPlayer);
                            currentPlayer.ConsumeSelectedCard(data);
                        }
                        if (UIController.Instance != null) UIController.Instance.AddModifierLog(data.cardName);
                    }
                }
                visual.AnimateUseCard();
            }
            yield return new WaitForSeconds(0.3f);
        }

        yield return new WaitForSeconds(0.5f);
        DestroyCards(visualsToUse);
        ClearSelectionList();

        UpdateHighlight();

        IsAnimating = false;
        UpdateButtonVisibility();
    }
    #endregion

    #region Discard Cards
    public void DiscardSelectedCards()
    {
        StartCoroutine(AnimateDiscardCards());
    }

    private IEnumerator AnimateDiscardCards()
    {
        IsAnimating = true;
        List<CardVisual> cardsToDiscard = CollectAllCardVisuals();
        HideButtons();

        if (currentPlayer != null)
        {
            foreach (var cardVisual in cardsToDiscard)
            {
                var comp = cardVisual.GetCardComponent();
                if (comp != null) currentPlayer.ConsumeSelectedCard(comp.GetCardData());
            }
            UpdateHighlight();
        }

        foreach (var visual in cardsToDiscard)
        {
            visual.AnimateDiscardCard();
            yield return new WaitForSeconds(0.1f);
        }

        yield return new WaitForSeconds(0.6f);
        DestroyCards(cardsToDiscard);
        ClearSelectionList();
        IsAnimating = false;
        UpdateButtonVisibility();
    }
    #endregion

    #region Helpers & Positioning (UNCHANGED)
    public float GetSelectedScale() => selectedScale;
    public void SetHoveredCard(CardVisual card) => hoveredCard = card;
    public float GetCardMoveSpeed() => cardMoveSpeed;

    private List<CardVisual> CollectAllCardVisuals()
    {
        List<CardVisual> cards = new List<CardVisual>();
        foreach (var slot in selectionSlots)
        {
            if (slot.HasCard())
            {
                cards.Add(slot.GetCard());
                slot.SetCard(null);
            }
        }
        return cards;
    }

    private void DestroyCards(List<CardVisual> cards)
    {
        foreach (var c in cards) if (c != null) Destroy(c.gameObject);
    }
    private void ClearSelectionList() => cardsInSelection.Clear();
    private CardSlot GetFirstEmptySlot() => selectionSlots.FirstOrDefault(s => !s.HasCard());
    public bool IsFull() => selectionSlots.All(s => s.HasCard());
    public int GetCardCount() => selectionSlots.Count(s => s.HasCard());

    private void UpdateButtonVisibility()
    {
        bool hasCards = selectionSlots.Any(s => s.HasCard());
        if (useButton) useButton.SetActive(hasCards);
        if (discardButton) discardButton.SetActive(hasCards);
    }
    private void HideButtons()
    {
        if (useButton) useButton.SetActive(false);
        if (discardButton) discardButton.SetActive(false);
    }

    private void UpdateCardPositions()
    {
        List<CardSlot> occupiedSlots = selectionSlots.Where(s => s.HasCard()).ToList();
        for (int i = 0; i < occupiedSlots.Count; i++) UpdateSingleCardPosition(occupiedSlots[i], i, occupiedSlots.Count);
    }

    private void UpdateSingleCardPosition(CardSlot slot, int index, int totalCards)
    {
        CardVisual card = slot.GetCard();
        if (card == null || card.IsTransitioning()) return;
        Vector3 localPos = CalculateCardLocalPosition(index, totalCards);
        float scale = (card.IsHovered() && hoveredCard == card) ? hoverScale : selectedScale;
        if (card.IsHovered() && hoveredCard == card) localPos.y += hoverYOffset;
        card.SetTargetPosition(localPos);
        card.SetTargetRotation(0f);
        card.SetTargetScale(scale);
    }

    private Vector3 CalculateCardLocalPosition(int index, int totalCards)
    {
        if (totalCards <= 1) return Vector3.zero;
        float totalWidth = (totalCards - 1) * cardSpacing;
        float startX = -totalWidth / 2f;
        return new Vector3(startX + (index * cardSpacing), 0, 0);
    }

    private Vector3 CalculateCardFinalWorldPosition(CardSlot targetSlot) => targetSlot.transform.position;

    private void UpdateHighlight()
    {
        if (TileHighlighter.Instance == null || TurnManager.Instance == null) return;

        int dice = TurnManager.Instance.GetCurrentDiceRoll();

        int mod = 0;
        if (currentPlayer != null)
        {
            mod = currentPlayer.CalculatePendingMoveModifier();
        }

        TileHighlighter.Instance.PreviewDestination(currentPlayer, dice, mod);
    }
    #endregion
}