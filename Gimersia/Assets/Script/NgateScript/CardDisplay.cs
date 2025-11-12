using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class CardDisplay : MonoBehaviour
{
    private CardData cardData;
    private Button button;

    // (Opsional) Tampilkan gambar kartu jika Anda mau
    // public Image cardArtImage; 

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnCardClicked);
    }

    // Mengisi data ke kartu ini
    public void Setup(CardData data)
    {
        cardData = data;

        // (Opsional) Update tampilan mockup
        // if(cardArtImage != null)
        //    cardArtImage.sprite = cardData.cardImage;
    }

    // Saat kartu ini di-klik
    private void OnCardClicked()
    {
        // Tampilkan detailnya menggunakan UIManager
        if (cardData != null && UIManager.Instance != null)
        {
            UIManager.Instance.ShowHandCardDetails(cardData);
        }
    }
}