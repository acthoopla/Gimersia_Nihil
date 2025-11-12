using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class CardChoiceDisplay : MonoBehaviour
{
    // --- Referensi UI di dalam Prefab ---
    public TextMeshProUGUI cardNameText; // <-- DIUBAH
    public TextMeshProUGUI cardDescriptionText; // <-- DIUBAH
    public Image cardImage;

    private CardData myCard;
    private UIManager uiManager;

    // Fungsi ini dipanggil oleh UIManager
    public void Setup(CardData card, UIManager manager)
    {
        myCard = card;
        uiManager = manager;

        cardNameText.text = card.cardName;
        cardDescriptionText.text = card.description;
        cardImage.sprite = card.cardImage;
    }

    // Hubungkan ini ke OnClick() Button di Inspector
    public void OnChoiceClicked()
    {
        if (myCard != null && uiManager != null)
        {
            // DIUBAH: Langsung panggil fungsi baru di UIManager,
            // bukan lagi ShowChoiceCardDetails
            uiManager.OnCardChoiceSelected(myCard);
        }
    }
}