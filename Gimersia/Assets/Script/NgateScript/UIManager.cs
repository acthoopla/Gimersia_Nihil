using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic; // Mengambil dari kedua script
using TMPro;
using UnityEngine.SceneManagement; // Diambil dari kodemu

public class UIManager : MonoBehaviour
{
    // --- FITUR SINGLETON DARI TEMANMU ---
    public static UIManager Instance;

    private enum DetailPanelContext { ViewingHand, ConfirmingChoice }
    private DetailPanelContext currentContext;

    // --- FITUR PAUSE/SETTINGS DARI KODEMU ---
    [Header("Pause/Settings Menu")]
    [Tooltip("Panel utama gameplay (berisi daftar pemain, info giliran, tombol settings, dll.)")]
    public GameObject gameplayPanel; // Panel "induk" untuk semua UI game

    [Tooltip("Panel settings yang muncul saat di-pause")]
    public GameObject settingsPanel;

    [Tooltip("Nama scene Main Menu (pastikan ada di Build Settings!)")]
    public string mainMenuSceneName = "MainMenu";
    // ------------------------------------

    // --- FITUR PLAYER TURN UI DARI KODEMU ---
    [Header("Player Turn UI")]
    public GameObject playerUIEntryPrefab;
    public Transform playerUIContainer;
    private List<PlayerUIEntry> uiEntries = new List<PlayerUIEntry>();
    // ------------------------------------

    [Header("Panel Detail Kartu (Pop-up)")]
    public GameObject cardDetailPanel;
    public GameObject cardInfoObject;
    public TextMeshProUGUI cardNameText;
    public TextMeshProUGUI cardDescriptionText;
    public TextMeshProUGUI cardTypeText;
    public TextMeshProUGUI cardTargetText;
    public Image cardImage;
    public Button closeDetailButton;
    public Button useCardButton;
    public Button confirmChoiceButton;

    [Header("Card Choice Panel (Pilih 1 dari 3)")]
    public GameObject cardChoicePanel;
    public GameObject cardChoicePrefab;
    public Transform choiceContainer;

    [Header("UI Utama (Game)")]
    public TextMeshProUGUI cycleText;
    // DIHAPUS: playerHighlightIndicators diganti dengan sistem 'PlayerTurnUI' kodemu
    // public GameObject[] playerHighlightIndicators; 

    [Header("Tampilan Tangan Pemain")]
    public GameObject cardDisplayPrefab;
    public Transform handContainer;

    // Variabel privat
    private List<GameObject> currentHandObjects = new List<GameObject>();
    private List<GameObject> currentChoiceObjects = new List<GameObject>();
    private CardData currentlyShownCard;

    void Awake()
    {
        // Logika Singleton dari temanmu
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // Opsional
        }
        else
        {
            Debug.LogError($"--- ERROR DUPLIKAT UIMANAGER ---");
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // --- LOGIKA START GABUNGAN ---

        // Dari kodemu (mengatur panel settings & gameplay)
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (gameplayPanel != null) gameplayPanel.SetActive(true);
        Time.timeScale = 1f; // Pastikan game tidak ter-pause

        // Dari kode temanmu (mengatur panel kartu)
        if (cardDetailPanel != null) cardDetailPanel.SetActive(false);
        if (cardChoicePanel != null) cardChoicePanel.SetActive(false);

        // Listener tombol kartu (dari temanmu)
        if (closeDetailButton != null) closeDetailButton.onClick.AddListener(OnCloseDetailPressed);
        if (useCardButton != null) useCardButton.onClick.AddListener(OnUseCardPressed);
        if (confirmChoiceButton != null) confirmChoiceButton.onClick.AddListener(OnConfirmCardChoicePressed);

        // (Tombol untuk settings/pause panel akan kamu hubungkan di Inspector)
    }

    // --- LOGIKA TOMBOL PANEL DETAIL (Dari Temanmu) ---
    #region Card Detail Panel
    private void OnCloseDetailPressed()
    {
        cardDetailPanel.SetActive(false);
        currentlyShownCard = null;
    }

    private void OnUseCardPressed()
    {
        if (currentContext == DetailPanelContext.ViewingHand && currentlyShownCard != null)
        {
            // Menggunakan Singleton (Instance) untuk memanggil Manager
            MultiplayerManager.Instance.UseCard(currentlyShownCard);
            cardDetailPanel.SetActive(false);
            currentlyShownCard = null;
        }
    }

    private void OnConfirmCardChoicePressed()
    {
        if (currentContext == DetailPanelContext.ConfirmingChoice && currentlyShownCard != null)
        {
            MultiplayerManager.Instance.AddChosenCardToPlayer(currentlyShownCard);
            cardDetailPanel.SetActive(false);
            cardChoicePanel.SetActive(false);
            ClearChoicePrefabs();
            currentlyShownCard = null;
        }
    }
    #endregion

    // --- FUNGSI TAMPILKAN PANEL DETAIL (Dari Temanmu) ---
    #region Show Card Details
    public void ShowHandCardDetails(CardData card)
    {
        currentContext = DetailPanelContext.ViewingHand;
        currentlyShownCard = card;
        PopulateDetailPanel(card);

        confirmChoiceButton.gameObject.SetActive(false);

        // Logika pengecekan giliran
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
    #endregion

    // --- FUNGSI PANEL PILIHAN (Dari Temanmu) ---
    #region Card Choice Panel
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

        if (handContainer != null)
        {
            handContainer.gameObject.SetActive(false);
        }
    }

    private void ClearChoicePrefabs()
    {
        foreach (GameObject choice in currentChoiceObjects)
        {
            Destroy(choice);
        }
        currentChoiceObjects.Clear();
    }
    #endregion

    // --- FUNGSI UI UTAMA & TANGAN (Gabungan) ---
    #region Main UI & Player Hand
    public void UpdateCycle(int count)
    {
        if (cycleText != null)
            cycleText.text = "Cycle : " + count;
    }

    // DIHAPUS: UpdatePlayerTurnHighlight (dari temanmu)
    // Kita akan menggunakan 'UpdateActivePlayer' dari kodemu karena lebih canggih

    public void DisplayPlayerHand(PlayerPawn player)
    {
        if (handContainer != null)
        {
            handContainer.gameObject.SetActive(true);
        }

        // (Fungsi ini diambil dari temanmu, tidak berubah)
        foreach (GameObject cardObj in currentHandObjects)
        {
            Destroy(cardObj);
        }
        currentHandObjects.Clear();

        if (player == null) return;
        if (cardDisplayPrefab == null)
        {
            Debug.LogError($"--- ERROR UIMANAGER --- Slot 'Card Display Prefab' KOSONG.");
            return;
        }
        if (handContainer == null)
        {
            Debug.LogError($"--- ERROR UIMANAGER --- Slot 'Hand Container' KOSONG.");
            return;
        }

        foreach (PlayerCardInstance cardInstance in player.heldCards)
        {
            GameObject newCard = Instantiate(cardDisplayPrefab, handContainer);
            newCard.GetComponent<CardDisplay>().Setup(cardInstance.cardData);
            currentHandObjects.Add(newCard);
        }
    }

    public void HidePlayerHand()
    {
        // 1. Bersihkan semua prefab kartu yang sedang tampil
        foreach (GameObject cardObj in currentHandObjects)
        {
            Destroy(cardObj);
        }
        currentHandObjects.Clear();

        // 2. (Opsional) Tutup panel detail jika sedang terbuka
        if (currentContext == DetailPanelContext.ViewingHand)
        {
            OnCloseDetailPressed(); // Panggil fungsi "close" yang sudah ada
        }
    }
    #endregion

    // --- FUNGSI-FUNGSI DARI KODEMU (Player Turn List & Settings) ---
    #region Player Turn & Settings (From Your Code)

    /// <summary>
    /// Dipanggil oleh MultiplayerManager *SATU KALI* saat giliran ditentukan.
    /// (Ini dari kodemu)
    /// </summary>
    public void SetupPlayerList(List<PlayerPawn> playerTurnOrder)
    {
        if (playerUIContainer == null || playerUIEntryPrefab == null)
        {
            Debug.LogWarning("Referensi Player Turn UI (Container/Prefab) kosong di UIManager.");
            return;
        }

        foreach (Transform child in playerUIContainer)
        {
            Destroy(child.gameObject);
        }
        uiEntries.Clear();

        foreach (PlayerPawn player in playerTurnOrder)
        {
            GameObject entryGO = Instantiate(playerUIEntryPrefab, playerUIContainer);
            PlayerUIEntry entryScript = entryGO.GetComponent<PlayerUIEntry>();
            if (entryScript != null)
            {
                entryScript.Setup(player.name);
                uiEntries.Add(entryScript);
            }
        }
    }

    /// <summary>
    /// Dipanggil oleh MultiplayerManager *SETIAP AWAL GILIRAN*.
    /// (Ini dari kodemu, menggantikan 'UpdatePlayerTurnHighlight' temanmu)
    /// </summary>
    public void UpdateActivePlayer(int activeIndex)
    {
        for (int i = 0; i < uiEntries.Count; i++)
        {
            if (i == activeIndex)
            {
                uiEntries[i].SetActive(true); // Maju
            }
            else
            {
                uiEntries[i].SetActive(false); // Mundur
            }
        }
    }

    /// <summary>
    /// Dipanggil oleh tombol Settings di UI Gameplay. (Dari kodemu)
    /// </summary>
    public void OnOpenSettings()
    {
        if (settingsPanel == null || gameplayPanel == null) return;
        Time.timeScale = 0f; // Jeda
        settingsPanel.SetActive(true);
        gameplayPanel.SetActive(false);
    }

    /// <summary>
    /// Dipanggil oleh tombol 'Resume' di dalam Settings Panel. (Dari kodemu)
    /// </summary>
    public void OnResumeGame()
    {
        if (settingsPanel == null || gameplayPanel == null) return;
        Time.timeScale = 1f; // Lanjut
        settingsPanel.SetActive(false);
        gameplayPanel.SetActive(true);
    }

    /// <summary>
    /// Dipanggil oleh tombol 'Return to Main Menu' di Settings Panel. (Dari kodemu)
    /// </summary>
    public void OnReturnToMainMenu()
    {
        Time.timeScale = 1f; // Selalu reset TimeScale!
        SceneManager.LoadScene(mainMenuSceneName);
    }
    #endregion
}