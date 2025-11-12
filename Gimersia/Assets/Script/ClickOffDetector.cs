using UnityEngine.EventSystems;
using UnityEngine;

public class ClickOffDetector : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        // Kode ini sudah sempurna, akan memanggil Deselect()
        if (CardDisplay.currentlySelectedCard != null)
        {
            CardDisplay.currentlySelectedCard.Deselect();
        }
    }
}