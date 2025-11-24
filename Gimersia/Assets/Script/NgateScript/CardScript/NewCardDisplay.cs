using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class NewCardDisplay : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    public TextMeshProUGUI cardNameText;
    public TextMeshProUGUI cardDescriptionText;
    public Image cardImage;

    [Header("Settings")]
    public float selectedScale = 1.3f;
    public float popupYOffset = 150f;

    // Data Kartu & Pemiliknya
    private NewCardData cardData;
    private PlayerState owner; // Kita butuh tahu kartu ini punya siapa

    // Logic Variables (Gunakan NewCardDisplay, bukan CardDisplay)
    public static NewCardDisplay currentlySelectedCard;
    private GameObject popupCopyInstance;
    private NewCardDisplay originalCard;
    private bool isPopupCopy = false;
    private CanvasGroup canvasGroup;
    private bool isInSlot = false;

    void Awake() => canvasGroup = GetComponent<CanvasGroup>();

    // Update Setup agar menerima Owner (PlayerState)
    public void Setup(NewCardData data, PlayerState playerOwner, bool isSlotMode)
    {
        cardData = data;
        owner = playerOwner;
        isInSlot = isSlotMode;

        if (cardData != null)
        {
            if (cardNameText) cardNameText.text = cardData.cardName;
            if (cardDescriptionText) cardDescriptionText.text = cardData.description;
            if (cardImage) cardImage.sprite = cardData.cardIcon;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null || cardData == null) return;

        // LOGIKA BARU: SWAP HAND <-> SLOT
        if (isInSlot)
        {
            // Jika di Slot -> Klik -> Balik ke Hand
            owner.ReturnCardToHand(cardData);
        }
        else
        {
            // Jika di Hand -> Klik -> Masuk ke Slot
            owner.SelectCardToSlot(cardData);
        }
    }

    private void UseCardLogic()
    {
        if (cardData != null && owner != null)
        {
            Debug.Log($"Menggunakan kartu: {cardData.cardName}");

            // 1. Jalankan Efek Kartu (Logic SRP)
            cardData.Play(owner);

            // 2. Buang Kartu dari Tangan (Data)
            owner.DiscardCard(cardData);

            // 3. Tutup Popup & Deselect kartu asli
            if (originalCard != null)
            {
                originalCard.Deselect();
            }
        }
        else
        {
            Debug.LogError("Card Data atau Owner null! Pastikan Setup dipanggil dengan benar.");
        }
    }

    private void Select()
    {
        if (UIManager.Instance == null || UIManager.Instance.popupContainer == null)
        {
            Debug.LogWarning("UIManager atau PopupContainer belum diset!");
            return;
        }

        currentlySelectedCard = this;
        canvasGroup.alpha = 0; // Sembunyikan yang asli
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false; // Agar klik tembus ke bawah jika perlu

        // Buat Popup
        popupCopyInstance = Instantiate(this.gameObject, UIManager.Instance.popupContainer);

        // Ambil komponen NewCardDisplay dari hasil copy
        NewCardDisplay copyScript = popupCopyInstance.GetComponent<NewCardDisplay>();

        // Setup copy-an dengan data yang sama
        copyScript.Setup(this.cardData, this.owner, this.isInSlot);
        copyScript.isPopupCopy = true;
        copyScript.originalCard = this;

        // Reset visual popup agar terlihat
        CanvasGroup copyCG = popupCopyInstance.GetComponent<CanvasGroup>();
        copyCG.alpha = 1;
        copyCG.interactable = true;
        copyCG.blocksRaycasts = true;

        // Posisikan di tengah layar
        RectTransform rt = popupCopyInstance.GetComponent<RectTransform>();
        rt.position = new Vector3(Screen.width / 2, Screen.height / 2 + popupYOffset, 0);
        rt.localScale = Vector3.one * selectedScale;
    }

    public void Deselect()
    {
        // Kembalikan visual kartu asli
        canvasGroup.alpha = 1;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        // Hancurkan popup jika ada
        if (popupCopyInstance != null) Destroy(popupCopyInstance);

        currentlySelectedCard = null;
    }

    void OnDestroy()
    {
        if (isPopupCopy && originalCard != null)
            originalCard.Deselect();
        else if (!isPopupCopy && popupCopyInstance != null)
            Destroy(popupCopyInstance);

        if (currentlySelectedCard == this) currentlySelectedCard = null;
    }
}