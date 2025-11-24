using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CardHandHolder : MonoBehaviour
{
    [Header("Selection Settings")]
    [SerializeField] private CardSelectionHolder cardSelectionHolder;

    [Header("Hand Settings")]
    [SerializeField] private int maxCardsInHand = 10;

    [Header("Card Layout Settings")]
    [SerializeField] private float cardSpacing = 80f;
    [SerializeField] private float curveHeight = 50f;
    [SerializeField] private float maxRotation = 15f;
    [SerializeField] private float cardMoveSpeed = 10f;

    [Header("Hover Settings")]
    [SerializeField] private float hoverYOffset = 100f;
    [SerializeField] private float hoverScale = 1.2f;

    [Header("Card Scale Settings")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float handScale = 0.9f;

    [Header("UI Settings")]
    [SerializeField] private Transform handHolder;
    [SerializeField] private GameObject cardSlotPrefab;

    private List<CardSlot> cardSlots = new List<CardSlot>();
    private CardVisual hoveredCard;
    private List<BaseCard> cardsInHand = new List<BaseCard>();

    #region Unity Methods
    private void Start()
    {
        CreateCardSlots(maxCardsInHand);
    }

    private void Update()
    {
        UpdateCardPositions();
    }
    #endregion

    #region Initialization
    public void CreateCardSlots(int count)
    {
        ClearExistingSlots();

        for (int i = 0; i < count; i++)
        {
            CreateSlot(i);
        }
    }

    private void ClearExistingSlots()
    {
        foreach (var slot in cardSlots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }
        cardSlots.Clear();
        cardsInHand.Clear();
    }

    private void CreateSlot(int index)
    {
        GameObject slotObj = Instantiate(cardSlotPrefab, handHolder);
        slotObj.name = $"Slot_{index}";

        CardSlot slot = slotObj.GetComponent<CardSlot>();
        if (slot == null)
            slot = slotObj.AddComponent<CardSlot>();

        slot.Initialize(index, this);
        cardSlots.Add(slot);
    }
    #endregion

    #region Card Management
    public bool SpawnCard(CardDataSO cardData, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= cardSlots.Count)
        {
            Debug.LogWarning($"Invalid slot index: {slotIndex}");
            return false;
        }

        CardSlot slot = cardSlots[slotIndex];
        if (slot.HasCard())
        {
            Debug.LogWarning($"Slot {slotIndex} already has a card!");
            return false;
        }

        if (cardData == null || cardData.CardPrefab == null)
        {
            Debug.LogError("CardDataSO or CardPrefab is null!");
            return false;
        }

        GameObject cardObj = Instantiate(cardData.CardPrefab, slot.transform);
        BaseCard cardComponent = cardObj.GetComponent<BaseCard>();
        CardVisual cardVisual = cardObj.GetComponent<CardVisual>();

        if (cardComponent == null || cardVisual == null)
        {
            Debug.LogError("Card prefab must have both Card and CardVisual components!");
            Destroy(cardObj);
            return false;
        }

        cardComponent.Initialize(cardData);
        cardVisual.Initialize(slot);

        slot.SetCard(cardVisual);
        cardsInHand.Add(cardComponent);

        Debug.Log($"Spawned card: {cardData.CardName} in slot {slotIndex}");
        return true;
    }

    public void SpawnCardInFirstEmptySlot(CardDataSO cardData)
    {
        CardSlot emptySlot = GetFirstEmptySlot();
        if (emptySlot != null)
        {
            SpawnCard(cardData, emptySlot.GetSlotIndex());
        }
    }

    public void RemoveCardFromHand(BaseCard cardComponent)
    {
        if (cardsInHand.Contains(cardComponent))
        {
            cardsInHand.Remove(cardComponent);
        }
    }

    public void OnCardClicked(CardVisual card)
    {
        if (cardSelectionHolder == null)
        {
            Debug.LogWarning("Selection holder is not assigned!");
            return;
        }

        if (cardSelectionHolder.IsFull())
        {
            Debug.Log("Selection holder is full!");
            return;
        }

        cardSelectionHolder.AddCard(card);
    }
    #endregion

    #region Slot Management
    public CardSlot GetFirstEmptySlot()
    {
        return cardSlots.FirstOrDefault(slot => !slot.HasCard());
    }

    public CardSlot GetHandSlotByIndex(int index)
    {
        if (index >= 0 && index < cardSlots.Count)
        {
            return cardSlots[index];
        }
        return null;
    }

    public CardSlot GetNearestSlot(Vector3 worldPosition)
    {
        CardSlot nearest = null;
        float minDistance = float.MaxValue;

        foreach (var slot in cardSlots)
        {
            float distance = Vector3.Distance(slot.transform.position, worldPosition);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = slot;
            }
        }

        return nearest;
    }

    public List<CardSlot> GetAllSlots()
    {
        return cardSlots;
    }
    #endregion

    #region Card Positioning
    public void UpdateCardPositions()
    {
        int cardCount = GetCardCountInHand();
        if (cardCount == 0) return;

        List<CardSlot> occupiedSlots = cardSlots.Where(s => s.HasCard()).ToList();

        for (int i = 0; i < occupiedSlots.Count; i++)
        {
            UpdateSingleCardPosition(occupiedSlots[i], i, cardCount);
        }
    }

    private void UpdateSingleCardPosition(CardSlot slot, int index, int totalCards)
    {
        CardVisual card = slot.GetCard();
        if (card == null) return;

        Vector3 targetPos = CalculateCardLocalPosition(index, totalCards);
        float targetRotZ = CalculateCardRotation(index, totalCards);
        float scale = CalculateCardScale(card);

        if (card.IsHovered() && hoveredCard == card)
        {
            targetPos.y += hoverYOffset;
            targetRotZ = 0f;
        }

        card.SetTargetPosition(targetPos);
        card.SetTargetRotation(targetRotZ);
        card.SetTargetScale(scale);
        card.SetBaseSortingOrder(index);
    }

    private Vector3 CalculateCardLocalPosition(int index, int totalCards)
    {
        Vector3 localPos = Vector3.zero;

        if (totalCards > 1)
        {
            float normalizedPosition = (float)index / (totalCards - 1);
            float centeredPosition = normalizedPosition * 2f - 1f;

            localPos.x = centeredPosition * cardSpacing * (totalCards - 1) * 0.5f;
            localPos.y = -Mathf.Abs(centeredPosition) * curveHeight;
        }

        return localPos;
    }

    private float CalculateCardRotation(int index, int totalCards)
    {
        if (totalCards <= 1) return 0f;

        float normalizedPosition = (float)index / (totalCards - 1);
        float centeredPosition = normalizedPosition * 2f - 1f;
        return -centeredPosition * maxRotation;
    }

    private float CalculateCardScale(CardVisual card)
    {
        return (card.IsHovered() && hoveredCard == card) ? hoverScale : handScale;
    }

    public Vector3 CalculateCardFinalWorldPosition(CardSlot targetSlot)
    {
        List<CardSlot> occupiedSlots = cardSlots.Where(s => s.HasCard()).ToList();
        int cardCount = occupiedSlots.Count;

        if (cardCount == 0) return targetSlot.transform.position;

        int targetIndex = occupiedSlots.IndexOf(targetSlot);
        if (targetIndex == -1) return targetSlot.transform.position;

        Vector3 localPos = CalculateCardLocalPosition(targetIndex, cardCount);
        return handHolder.TransformPoint(localPos);
    }
    #endregion

    #region Public Methods
    public void SetHoveredCard(CardVisual card) => hoveredCard = card;
    public bool IsHandFull() => cardSlots.All(slot => slot.HasCard());
    public int GetCardCountInHand() => cardSlots.Count(slot => slot.HasCard());
    public int GetMaxCardsInHand() => maxCardsInHand;
    public float GetCardMoveSpeed() => cardMoveSpeed;

    public List<BaseCard> GetCardsInHand() => new List<BaseCard>(cardsInHand);
    public List<string> GetCardNamesInHand() => cardsInHand.Select(card => card.GetCardName()).ToList();

    public float GetNormalScale() => normalScale;
    public float GetHandScale() => handScale;
    public float GetHoverScale() => hoverScale;
    #endregion
}
