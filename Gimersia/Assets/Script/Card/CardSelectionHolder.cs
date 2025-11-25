using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class CardSelectionHolder : MonoBehaviour
{
    [Header("Selection Settings")]
    [SerializeField] private int maxCards = 5;

    [Header("Card Layout Settings")]
    [SerializeField] private float cardSpacing = 120f;
    [SerializeField] private float cardMoveSpeed = 10f;

    [Header("Hover Settings")]
    [SerializeField] private float hoverYOffset = 80f;
    [SerializeField] private float hoverScale = 1.2f;

    [Header("Card Scale Settings")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float selectedScale = 1f;

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

    #region Unity Methods
    private void Start()
    {
        CreateSelectionSlots(maxCards);
        InitializeCardHandHolder();
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
        if (cardHandHolder == null)
        {
            cardHandHolder = FindObjectOfType<CardHandHolder>();
            if (cardHandHolder == null)
            {
                Debug.LogError("CardHandHolder not found in scene!");
            }
        }
    }

    public void CreateSelectionSlots(int count)
    {
        ClearExistingSlots();

        for (int i = 0; i < count; i++)
        {
            CreateSlot(i);
        }
    }

    private void ClearExistingSlots()
    {
        foreach (var slot in selectionSlots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }
        selectionSlots.Clear();
        cardsInSelection.Clear();
    }

    private void CreateSlot(int index)
    {
        GameObject slotObj = Instantiate(cardSlotPrefab, selectionHolder);
        slotObj.name = $"SelectionSlot_{index}";

        CardSlot slot = slotObj.GetComponent<CardSlot>();
        if (slot == null)
            slot = slotObj.AddComponent<CardSlot>();

        slot.Initialize(index, null);
        selectionSlots.Add(slot);
    }
    #endregion

    #region Card Management
    public bool AddCard(CardVisual card)
    {
        if (IsFull())
        {
            Debug.Log("Selection holder is full!");
            return false;
        }

        CardSlot emptySlot = GetFirstEmptySlot();
        if (emptySlot == null)
        {
            Debug.Log("Selection holder is full!");
            return false;
        }

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
            cardHandHolder?.RemoveCardFromHand(cardComponent);
            cardsInSelection.Add(cardComponent);

            if (currentPlayer != null)
            {
                currentPlayer.SelectCardToSlot(cardComponent.GetCardData());
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

        // 1. Update LOGIC (Kembalikan data ke PlayerState)
        GameCard logicCard = card.GetComponent<GameCard>();
        if (logicCard != null && currentPlayer != null)
        {
            // Ini akan mentrigger Event OnStateChanged di PlayerState
            // VisualBridge akan menangkap event ini dan men-spawn kartu di tangan secara otomatis
            currentPlayer.ReturnCardToHand(logicCard.GetData());
        }

        // 2. Update VISUAL SLOT (Hapus dari slot saat ini)
        CardSlot currentSlot = card.GetSlot();
        if (currentSlot != null)
        {
            currentSlot.SetCard(null);
        }

        BaseCard cardComponent = card.GetCardComponent();
        if (cardComponent != null)
        {
            cardsInSelection.Remove(cardComponent);
        }

        // 3. DESTROY VISUAL (JANGAN ANIMASI BALIK)
        // Kita hancurkan visual ini karena VisualBridge akan membuatkan yang baru di tangan
        Destroy(card.gameObject);

        // 4. Update Button Visibility
        UpdateButtonVisibility();
    }
    private void TransferCardToHand(CardVisual card, CardSlot targetSlot)
    {
        CardSlot currentSlot = card.GetSlot();
        currentSlot?.SetCard(null);

        BaseCard cardComponent = card.GetCardComponent();
        if (cardComponent != null)
        {
            cardsInSelection.Remove(cardComponent);
        }

        card.SetSlot(targetSlot);
        card.SetInSelectionHolder(false);
        targetSlot.SetCard(card);

        Vector3 finalWorldPos = CalculateCardFinalWorldPositionInHand(targetSlot);
        card.StartTransitionTo(targetSlot.transform, finalWorldPos);

        StartCoroutine(WaitForTransitionComplete(card, targetSlot.transform));
        UpdateButtonVisibility();
    }

    private IEnumerator WaitForTransitionComplete(CardVisual card, Transform targetParent)
    {
        while (card != null && card.IsTransitioning())
        {
            yield return null;
        }

        if (card != null)
        {
            card.CompleteTransitionTo(targetParent);
            yield return null;
            UpdateCardPositions();
        }
    }
    #endregion

    #region Use Cards
    public void UseSelectedCards()
    {
        StartCoroutine(AnimateUseCards());
    }

    private IEnumerator AnimateUseCards()
    {
        List<CardVisual> visualsToUse = CollectAllCardVisuals();

        if (UIController.Instance != null)
        {
            UIController.Instance.ShowModifierPanel();
        }

        Debug.Log("Fase Visual: Animasi & Masuk Antrean...");
        HideButtons();

        for (int i = 0; i < visualsToUse.Count; i++)
        {
            CardVisual visual = visualsToUse[i];

            if (visual != null)
            {
                var logicComp = visual.GetComponent<GameCard>();
                if (logicComp != null)
                {
                    NewCardData data = logicComp.GetCardData();

                    if (currentPlayer != null)
                        currentPlayer.AddToPending(data);

                    if (UIController.Instance != null)
                    {
                        UIController.Instance.AddModifierLog(data.cardName);
                    }
                }

                visual.AnimateUseCard();
            }

            yield return new WaitForSeconds(0.3f);
        }

        yield return new WaitForSeconds(0.5f);

        DestroyCards(visualsToUse);
        ClearSelectionList();
    }

    private void ExecuteCardEffects(List<BaseCard> cards)
    {
        if (currentPlayer == null) currentPlayer = FindObjectOfType<PlayerState>();

        foreach (var cardComponent in cards)
        {
            if (cardComponent != null && cardComponent.CanUse())
            {
                // A. Jalankan Logika Kartu (Play Effect)
                cardComponent.Use();

                // B. PENTING: Hapus Data dari PlayerState agar tidak dianggap masih ada
                if (currentPlayer != null)
                {
                    currentPlayer.ConsumeSelectedCard(cardComponent.GetCardData());
                }
            }
        }
    }
    #endregion

    #region Discard Cards
    public void DiscardSelectedCards()
    {
        StartCoroutine(AnimateDiscardCards());
    }

    private IEnumerator AnimateDiscardCards()
    {
        List<CardVisual> cardsToDiscard = CollectAllCardVisuals();

        // FIX ERROR CS0103: Definisikan cardComponents di sini
        List<BaseCard> cardComponents = GetCardsInSelection();

        Debug.Log($"Discarding {cardsToDiscard.Count} cards");
        HideButtons();

        if (currentPlayer != null)
        {
            foreach (var card in cardComponents)
            {
                // Gunakan variable cardComponents yang sudah didefinisikan di atas
                currentPlayer.ConsumeSelectedCard(card.GetCardData());
            }
        }

        yield return StartCoroutine(AnimateCardsSequentially(cardsToDiscard, false));

        DestroyCards(cardsToDiscard);
        ClearSelectionList();
        UpdateButtonVisibility();
    }
    #endregion

    #region Animation Helpers
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

    private IEnumerator AnimateCardsSequentially(List<CardVisual> cards, bool isUse)
    {
        List<CardVisual> animatedCards = new List<CardVisual>();

        if (isUse)
        {
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                AnimateCardWithIndex(cards[i], cards.Count - 1 - i, isUse, animatedCards);
            }
        }
        else
        {
            for (int i = 0; i < cards.Count; i++)
            {
                AnimateCardWithIndex(cards[i], i, isUse, animatedCards);
            }
        }

        // 0.6f value must same as exitAnimationDuration in CardVisual.cs
        float maxAnimationTime = 0.6f + (cards.Count * cardAnimationDelay);
        yield return new WaitForSeconds(maxAnimationTime);
    }

    private void AnimateCardWithIndex(CardVisual card, int index, bool isUse, List<CardVisual> animatedCards)
    {
        if (card != null)
        {
            animatedCards.Add(card);
            float delay = index * cardAnimationDelay;
            StartCoroutine(AnimateCardWithDelay(card, isUse, delay));
        }
    }

    private IEnumerator AnimateCardWithDelay(CardVisual card, bool isUse, float delay)
    {
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }

        if (card != null)
        {
            if (isUse)
            {
                card.AnimateUseCard();
            }
            else
            {
                card.AnimateDiscardCard();
            }
        }
    }

    private void DestroyCards(List<CardVisual> cards)
    {
        foreach (var card in cards)
        {
            if (card != null)
            {
                Destroy(card.gameObject);
            }
        }
    }

    private void ClearSelectionList()
    {
        cardsInSelection.Clear();
    }
    #endregion

    #region Card Positioning
    private void UpdateCardPositions()
    {
        List<CardSlot> occupiedSlots = GetOccupiedSlots();
        int cardCount = occupiedSlots.Count;

        if (cardCount == 0) return;

        for (int i = 0; i < occupiedSlots.Count; i++)
        {
            UpdateSingleCardPosition(occupiedSlots[i], i, cardCount);
        }
    }

    private void UpdateSingleCardPosition(CardSlot slot, int index, int totalCards)
    {
        CardVisual card = slot.GetCard();
        if (card == null || card.IsTransitioning()) return;

        Vector3 targetPos = CalculateCardLocalPosition(index, totalCards);
        float scale = CalculateCardScale(card);

        if (card.IsHovered() && hoveredCard == card)
        {
            targetPos.y += hoverYOffset;
        }

        card.SetTargetPosition(targetPos);
        card.SetTargetRotation(0f);
        card.SetTargetScale(scale);
        card.SetBaseSortingOrder(index);
    }

    private Vector3 CalculateCardLocalPosition(int index, int totalCards)
    {
        Vector3 localPos = Vector3.zero;

        if (totalCards > 1)
        {
            float totalWidth = (totalCards - 1) * cardSpacing;
            float startX = -totalWidth / 2f;
            localPos.x = startX + (index * cardSpacing);
        }

        return localPos;
    }

    private float CalculateCardScale(CardVisual card)
    {
        return (card.IsHovered() && hoveredCard == card) ? hoverScale : selectedScale;
    }

    private Vector3 CalculateCardFinalWorldPosition(CardSlot targetSlot)
    {
        List<CardSlot> occupiedSlots = GetOccupiedSlots();
        int cardCount = occupiedSlots.Count;

        if (cardCount == 0) return targetSlot.transform.position;

        int targetIndex = occupiedSlots.IndexOf(targetSlot);
        if (targetIndex == -1) return targetSlot.transform.position;

        Vector3 localPos = CalculateCardLocalPosition(targetIndex, cardCount);
        return selectionHolder.TransformPoint(localPos);
    }

    private Vector3 CalculateCardFinalWorldPositionInHand(CardSlot targetSlot)
    {
        if (cardHandHolder == null)
        {
            InitializeCardHandHolder();
        }

        if (cardHandHolder == null)
        {
            return targetSlot.transform.position;
        }

        return cardHandHolder.CalculateCardFinalWorldPosition(targetSlot);
    }
    #endregion

    #region Button Management
    private void HideButtons()
    {
        useButton?.SetActive(false);
        discardButton?.SetActive(false);
    }

    private void UpdateButtonVisibility()
    {
        bool hasCards = selectionSlots.Any(s => s.HasCard());

        useButton?.SetActive(hasCards);
        discardButton?.SetActive(hasCards);
    }
    #endregion

    #region Helper Methods
    private CardSlot GetFirstEmptySlot()
    {
        return selectionSlots.FirstOrDefault(s => !s.HasCard());
    }

    private CardSlot FindFirstEmptySlotInHand()
    {
        if (cardHandHolder == null)
        {
            InitializeCardHandHolder();
        }

        return cardHandHolder?.GetFirstEmptySlot();
    }

    private List<CardSlot> GetOccupiedSlots()
    {
        return selectionSlots.Where(s => s.HasCard()).ToList();
    }
    #endregion

    #region Public Methods
    public void SetHoveredCard(CardVisual card) => hoveredCard = card;
    public int GetCardCount() => selectionSlots.Count(s => s.HasCard());
    public bool IsFull() => selectionSlots.All(s => s.HasCard());
    public float GetCardMoveSpeed() => cardMoveSpeed;

    public List<BaseCard> GetCardsInSelection() => new List<BaseCard>(cardsInSelection);
    public List<string> GetCardNamesInSelection() => cardsInSelection.Select(card => card.GetCardName()).ToList();

    public float GetNormalScale() => normalScale;
    public float GetSelectedScale() => selectedScale;
    public float GetHoverScale() => hoverScale;
    #endregion
}
