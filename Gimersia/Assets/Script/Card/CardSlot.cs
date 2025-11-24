using UnityEngine;

public class CardSlot : MonoBehaviour
{
    private int slotIndex;
    private CardHandHolder handHolder;
    private CardVisual currentCard;

    public void Initialize(int index, CardHandHolder holder)
    {
        slotIndex = index;
        handHolder = holder;
    }

    public bool HasCard()
    {
        return currentCard != null;
    }

    public CardVisual GetCard()
    {
        return currentCard;
    }

    public void SetCard(CardVisual card)
    {
        currentCard = card;
    }

    public int GetSlotIndex()
    {
        return slotIndex;
    }

    public CardHandHolder GetHandHolder()
    {
        return handHolder;
    }
}
