using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    private enum DetailPanelContext { ViewingHand, ConfirmingChoice }
    private DetailPanelContext currentContext;

    [Header("Panel Detail Kartu (Pop-up)")]
    public GameObject cardDetailPanel;
    public GameObject cardInfoObject;
    public TextMeshProUGUI cardNameText;
    public TextMeshProUGUI cardDescriptionText;
    public TextMeshProUGUI cardTypeText;
    public TextMeshProUGUI cardTargetText;
    public Image cardImage;
    public Button closeDetailButton; // Tombol 'X'
    [Tooltip("Tombol 'Gunakan Kartu' (saat melihat dari tangan)")]
    public Button useCardButton;
    [Tooltip("Tombol 'Pilih Kartu Ini' (saat memilih dari 3 pilihan)")]
    public Button confirmChoiceButton;

    [Header("Card Choice Panel (Pilih 1 dari 3)")]
    public GameObject cardChoicePanel;
    public GameObject cardChoicePrefab;
    public Transform choiceContainer;

    [Header("UI Utama (Game)")]
    public TextMeshProUGUI cycleText;
    public GameObject[] playerHighlightIndicators;

    [Header("Tampilan Tangan Pemain")]
    public GameObject cardDisplayPrefab;
    public Transform handContainer;

    // Variabel privat
    private List<GameObject> currentHandObjects = new List<GameObject>();
    private List<GameObject> currentChoiceObjects = new List<GameObject>();
    private CardData currentlyShownCard;

    void Awake()
    {
        if (Instance == null)
        {
            // Jika belum ada instance, jadikan ini instance utama
            Instance = this;

            // Opsional: Jika UIManager Anda ada di root,
            // tambahkan ini agar tidak hancur saat pindah scene
            // DontDestroyOnLoad(gameObject); 
        }
        else
        {
            // --- INI PERLINDUNGAN DUPLIKAT ---
            // Jika Instance SUDAH ADA, berarti ini duplikat.
            Debug.LogError($"--- ERROR DUPLIKAT UIMANAGER ---");
            Debug.LogError($"Sebuah UIManager sudah ada di GameObject '{Instance.gameObject.name}'.");
            Debug.LogError($"UIManager duplikat di '{this.gameObject.name}' akan dihancurkan.");
            Destroy(gameObject); // Hancurkan duplikat ini
        }
    }

    void Start()
    {
        cardDetailPanel.SetActive(false);
        cardChoicePanel.SetActive(false);

        // --- PERBAIKAN BUG DI SINI ---
        // 1. Menggunakan fungsi 'OnCloseDetailPressed' yang benar
        closeDetailButton.onClick.AddListener(OnCloseDetailPressed);

        if (useCardButton != null)
        {
            useCardButton.onClick.AddListener(OnUseCardPressed);
        }

        // 2. Menambahkan listener yang hilang untuk tombol 'Confirm'
        if (confirmChoiceButton != null)
        {
            confirmChoiceButton.onClick.AddListener(OnConfirmCardChoicePressed);
        }
        // -------------------------
    }

    // --- LOGIKA TOMBOL PANEL DETAIL ---

    private void OnCloseDetailPressed()
    {
        cardDetailPanel.SetActive(false);
        currentlyShownCard = null;
    }

    private void OnUseCardPressed()
    {
        if (currentContext == DetailPanelContext.ViewingHand && currentlyShownCard != null)
        {
            MultiplayerManager.Instance.UseCard(currentlyShownCard);
            cardDetailPanel.SetActive(false);
            currentlyShownCard = null;
        }
    }

    private void OnConfirmCardChoicePressed()
    {
        // Debug.Log("Confirm Ditekan!"); // (Untuk debug)
        if (currentContext == DetailPanelContext.ConfirmingChoice && currentlyShownCard != null)
        {
            MultiplayerManager.Instance.AddChosenCardToPlayer(currentlyShownCard);
            cardDetailPanel.SetActive(false);
            cardChoicePanel.SetActive(false);
            ClearChoicePrefabs();
            currentlyShownCard = null;
        }
    }

    // --- FUNGSI TAMPILKAN PANEL DETAIL ---

    public void ShowHandCardDetails(CardData card)
    {
        currentContext = DetailPanelContext.ViewingHand;
        currentlyShownCard = card;
        PopulateDetailPanel(card);

        confirmChoiceButton.gameObject.SetActive(false);
        bool isMyTurn = (MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsPlayerTurn(MultiplayerManager.Instance.GetCurrentPlayer()));
        useCardButton.gameObject.SetActive(isMyTurn && !MultiplayerManager.Instance.IsActionRunning);

        if (cardDetailPanel != null) cardDetailPanel.SetActive(true);
        if (cardInfoObject != null) cardInfoObject.SetActive(true);
    }

    public void ShowChoiceCardDetails(CardData card)
    {
        currentContext = DetailPanelContext.ConfirmingChoice;
        currentlyShownCard = card;
        PopulateDetailPanel(card);

        useCardButton.gameObject.SetActive(false);
        confirmChoiceButton.gameObject.SetActive(true);

        if (cardDetailPanel != null) cardDetailPanel.SetActive(true);
        if (cardInfoObject != null) cardInfoObject.SetActive(true);
    }

    private void PopulateDetailPanel(CardData card)
    {
        cardNameText.text = card.cardName;
        cardDescriptionText.text = card.description;
        cardTypeText.text = card.cardType;
        cardTargetText.text = card.cardTarget;
        cardImage.sprite = card.cardImage;
    }


    // --- FUNGSI PANEL PILIHAN (Pilih 1 dari 3) ---

    public void StartCardSelection(List<CardData> cardsToShow)
    {
        ClearChoicePrefabs();
        foreach (CardData card in cardsToShow)
        {
            GameObject choiceGO = Instantiate(cardChoicePrefab, choiceContainer);
            choiceGO.GetComponent<CardChoiceDisplay>().Setup(card, this);
            currentChoiceObjects.Add(choiceGO);
        }
        cardChoicePanel.SetActive(true);
    }

    private void ClearChoicePrefabs()
    {
        foreach (GameObject choice in currentChoiceObjects)
        {
            Destroy(choice);
        }
        currentChoiceObjects.Clear();
    }


    // --- FUNGSI UI UTAMA & TANGAN ---

    public void UpdateCycle(int count)
    {
        // Pastikan tidak null
        if (cycleText != null)
            cycleText.text = "Cycle : " + count;
    }

    public void UpdatePlayerTurnHighlight(int playerIndex)
    {
        for (int i = 0; i < playerHighlightIndicators.Length; i++)
        {
            if (playerHighlightIndicators[i] != null)
            {
                playerHighlightIndicators[i].SetActive(i == playerIndex);
            }
        }
    }

    // --- UPDATE LOGIKA TAS DI SINI ---
    public void DisplayPlayerHand(PlayerPawn player)
    {
        // 1. Bersihkan kartu lama
        foreach (GameObject cardObj in currentHandObjects)
        {
            Destroy(cardObj);
        }
        currentHandObjects.Clear();

        if (player == null) return;

        // --- INI PERLINDUNGAN BARU ---
        // Cek apakah variabel Inspector-nya KOSONG
        if (cardDisplayPrefab == null)
        {
            Debug.LogError($"--- ERROR UIMANAGER ---");
            Debug.LogError($"Slot 'Card Display Prefab' (prefab 'CardMockup_Prefab') KOSONG.");
            Debug.LogError($"Tolong isi di Inspector GameObject '{this.gameObject.name}'.");
            return; // Hentikan fungsi di sini agar tidak crash
        }
        if (handContainer == null)
        {
            Debug.LogError($"--- ERROR UIMANAGER ---");
            Debug.LogError($"Slot 'Hand Container' (tempat 'tas' kartu) KOSONG.");
            Debug.LogError($"Tolong isi di Inspector GameObject '{this.gameObject.name}'.");
            return; // Hentikan fungsi di sini agar tidak crash
        }
        // -----------------------------

        // 2. Tampilkan kartu baru (Tas pemain saat ini)
        // Kode ini hanya akan berjalan jika kedua variabel di atas TERISI
        foreach (PlayerCardInstance cardInstance in player.heldCards)
        {
            GameObject newCard = Instantiate(cardDisplayPrefab, handContainer);
            newCard.GetComponent<CardDisplay>().Setup(cardInstance.cardData);
            currentHandObjects.Add(newCard);
        }
    }
}