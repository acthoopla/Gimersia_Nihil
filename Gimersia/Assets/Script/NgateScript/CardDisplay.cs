using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class CardDisplay : MonoBehaviour, IPointerClickHandler
{
    [Header("Animasi Pilihan")]
    public float selectedScale = 1.3f;
    public float popupYOffset = 150f;

    [Header("UI Referensi (dari Prefab)")]
    public TextMeshProUGUI cardNameText;
    public TextMeshProUGUI cardDescriptionText;
    public Image cardImage;

    // --- Variabel State ---
    public static CardDisplay currentlySelectedCard;
    private CardData cardData;
    private CanvasGroup canvasGroup;

    // --- Variabel untuk Kopi/Duplikat ---
    private bool isPopupCopy = false;
    private CardDisplay originalCard;
    private GameObject popupCopyInstance;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        Debug.Log($"[{name}] Awake. CanvasGroup ditemukan: {canvasGroup != null}");
    }

    public void Setup(CardData data)
    {
        cardData = data;
        // --- LOG DEBUG ---
        if (cardData == null)
        {
            Debug.LogError($"[{name}] Setup dipanggil dengan cardData NULL!");
        }
        else
        {
            Debug.Log($"[{name}] Setup berhasil untuk kartu: {cardData.cardName}");
            if (cardNameText != null) cardNameText.text = cardData.cardName;
            if (cardDescriptionText != null) cardDescriptionText.text = cardData.description;
            if (cardImage != null) cardImage.sprite = cardData.cardImage;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // --- LOGIKA KARTU DUPLIKAT (YANG DI-POPUP) ---
        if (isPopupCopy)
        {
            // --- LOG DEBUG ---
            Debug.Log($"[{name}] (Kopi) Di-klik. Mencoba menggunakan kartu: {(cardData != null ? cardData.cardName : "NULL")}");

            if (MultiplayerManager.Instance == null)
            {
                Debug.LogError("MultiplayerManager.Instance NULL saat Kopi diklik!");
                return;
            }
            if (cardData == null)
            {
                Debug.LogError("cardData NULL saat Kopi diklik!");
                return;
            }
            if (originalCard == null)
            {
                Debug.LogError("originalCard (referensi ke kartu asli) NULL saat Kopi diklik!");
                return;
            }

            if (MultiplayerManager.Instance.IsActionRunning)
            {
                UIManager.Instance.SetActionText("Tidak bisa menggunakan kartu saat aksi berjalan.");
                return;
            }

            // Panggil UseCard dan Deselect
            Debug.Log($"[{name}] (Kopi) Memanggil UseCard...");
            MultiplayerManager.Instance.UseCard(cardData); // <-- Ini baris 62 Anda

            Debug.Log($"[{name}] (Kopi) Memanggil Deselect() pada kartu asli...");
            originalCard.Deselect();
            return;
        }

        // --- LOGIKA KARTU ASLI (YANG DI HAND) ---
        Debug.Log($"[{name}] (Asli) Di-klik.");

        if (cardData == null) { Debug.LogError("cardData NULL saat kartu Asli diklik!"); return; }
        if (UIManager.Instance == null) { Debug.LogError("UIManager.Instance NULL saat kartu Asli diklik!"); return; }
        if (MultiplayerManager.Instance == null) { Debug.LogError("MultiplayerManager.Instance NULL saat kartu Asli diklik!"); return; }

        if (MultiplayerManager.Instance.IsActionRunning) return;

        if (currentlySelectedCard == this)
        {
            Debug.Log($"[{name}] (Asli) Kartu ini sudah terpilih. Memanggil Deselect().");
            Deselect();
        }
        else
        {
            if (currentlySelectedCard != null)
            {
                Debug.Log($"[{name}] (Asli) Kartu lain sedang terpilih. Memanggil Deselect() pada kartu lain dulu.");
                currentlySelectedCard.Deselect();
            }
            Debug.Log($"[{name}] (Asli) Memanggil Select().");
            Select();
        }
    }

    private void Select()
    {
        if (UIManager.Instance.popupContainer == null)
        {
            Debug.LogError("PopupContainer belum di-set di UIManager!");
            return;
        }

        Debug.Log($"[{name}] (Asli) Select(): Menyembunyikan kartu asli.");
        currentlySelectedCard = this;
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Debug.Log($"[{name}] (Asli) Select(): Membuat duplikat...");
        popupCopyInstance = Instantiate(this.gameObject, UIManager.Instance.popupContainer);

        CardDisplay copyScript = popupCopyInstance.GetComponent<CardDisplay>();

        // --- TAMBAHKAN BARIS INI ---
        copyScript.Setup(this.cardData); // <-- Berikan CardData ke duplikatnya
        // -------------------------

        copyScript.isPopupCopy = true;
        copyScript.originalCard = this;

        CanvasGroup copyCanvasGroup = popupCopyInstance.GetComponent<CanvasGroup>();
        copyCanvasGroup.alpha = 1;
        copyCanvasGroup.interactable = true;
        copyCanvasGroup.blocksRaycasts = true;

        RectTransform copyRect = popupCopyInstance.GetComponent<RectTransform>();
        float targetX = Screen.width / 2f;
        float targetY = (Screen.height / 2f) + popupYOffset;
        copyRect.position = new Vector3(targetX, targetY, 0);
        copyRect.localScale = new Vector3(selectedScale, selectedScale, 1f);

        Debug.Log($"[{name}] (Asli) Select(): Duplikat dibuat di {copyRect.position}.");
    }

    public void Deselect()
    {
        if (currentlySelectedCard != this)
        {
            Debug.LogWarning($"[{name}] (Asli) Deselect() dipanggil, tapi kartu ini BUKAN currentlySelectedCard. Mengabaikan.");
            return;
        }

        Debug.Log($"[{name}] (Asli) Deselect(): Menampilkan kartu asli.");
        canvasGroup.alpha = 1;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        if (popupCopyInstance != null)
        {
            Debug.Log($"[{name}] (Asli) Deselect(): Menghancurkan duplikat.");
            Destroy(popupCopyInstance);
        }
        else
        {
            Debug.LogWarning($"[{name}] (Asli) Deselect() dipanggil, tapi popupCopyInstance NULL.");
        }

        currentlySelectedCard = null;
        popupCopyInstance = null;
    }

    void OnDestroy()
    {
        // --- LOG DEBUG ---
        Debug.Log($"[{name}] OnDestroy() dipanggil. Apakah ini kopi? {isPopupCopy}");

        if (isPopupCopy && originalCard != null)
        {
            Debug.Log($"[{name}] (Kopi) Hancur, memanggil Deselect() pada kartu asli untuk bersih-bersih.");
            originalCard.Deselect();
        }
        else if (!isPopupCopy && popupCopyInstance != null)
        {
            Debug.Log($"[{name}] (Asli) Hancur, menghancurkan kopi yang terhubung.");
            Destroy(popupCopyInstance);
        }

        if (currentlySelectedCard == this)
        {
            currentlySelectedCard = null;
        }
    }
}