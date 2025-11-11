using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;
using System.Text; // Diambil dari kodemu

public class UIManager : MonoBehaviour
{
    public static UIManager Instance; // Diambil dari temanmu

    private enum DetailPanelContext { ViewingHand, ConfirmingChoice }
    private DetailPanelContext currentContext;

    // Dari kodemu
    [Header("Pause/Settings Menu")]
    public GameObject gameplayPanel;
    public GameObject settingsPanel;
    public string mainMenuSceneName = "MainMenu";

    // Dari kodemu
    [Header("Player Turn UI")]
    public GameObject playerUIEntryPrefab;
    public Transform playerUIContainer;
    private List<PlayerUIEntry> uiEntries = new List<PlayerUIEntry>();

    // Dari kodemu
    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI winnerListText;
    public Button playAgainButton;
    public Button returnToMenuButton_GameOver;

    // Dari temanmu
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

    // Dari temanmu
    [Header("Card Choice Panel (Pilih 1 dari 3)")]
    public GameObject cardChoicePanel;
    public GameObject cardChoicePrefab;
    public Transform choiceContainer;

    [Header("UI Utama (Game)")]
    public TextMeshProUGUI cycleText; // Dari temanmu

    // Dari temanmu
    [Header("Tampilan Tangan Pemain")]
    public GameObject cardDisplayPrefab;
    public Transform handContainer;

    // Variabel privat gabungan
    private List<GameObject> currentHandObjects = new List<GameObject>();
    private List<GameObject> currentChoiceObjects = new List<GameObject>();
    private CardData currentlyShownCard;

    void Awake()
    {
        // Logika Singleton (dari temanmu)
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        // --- LOGIKA START GABUNGAN ---
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (gameplayPanel != null) gameplayPanel.SetActive(true);
        Time.timeScale = 1f;
        if (cardDetailPanel != null) cardDetailPanel.SetActive(false);
        if (cardChoicePanel != null) cardChoicePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        
        // Listener tombol kartu
        if (closeDetailButton != null) closeDetailButton.onClick.AddListener(OnCloseDetailPressed);
        if (useCardButton != null) useCardButton.onClick.AddListener(OnUseCardPressed);
        if (confirmChoiceButton != null) confirmChoiceButton.onClick.AddListener(OnConfirmCardChoicePressed);

        // Listener tombol Game Over
        if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgainPressed);
        if (returnToMenuButton_GameOver != null) returnToMenuButton_GameOver.onClick.AddListener(OnReturnToMainMenu);
    }

    // --- SEMUA FUNGSI KARTU DARI TEMANMU DI AMBIL ---
    #region Card Logic Functions
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
        if (currentContext == DetailPanelContext.ConfirmingChoice && currentlyShownCard != null)
        {
            MultiplayerManager.Instance.AddChosenCardToPlayer(currentlyShownCard);
            cardDetailPanel.SetActive(false);
            cardChoicePanel.SetActive(false);
            ClearChoicePrefabs();
            currentlyShownCard = null;
            
            // Perbaikan dari temanmu: Tampilkan lagi 'hand'
            if (handContainer != null)
            {
                handContainer.gameObject.SetActive(true);
            }
        }
    }

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

        // Perbaikan dari temanmu: Sembunyikan 'hand'
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

    public void UpdateCycle(int count)
    {
        if (cycleText != null)
            cycleText.text = "Cycle : " + count;
    }

    public void DisplayPlayerHand(PlayerPawn player)
    {
        // Perbaikan dari temanmu: Tampilkan 'hand'
        if (handContainer != null)
        {
            handContainer.gameObject.SetActive(true);
        }
        
        foreach (GameObject cardObj in currentHandObjects)
        {
            Destroy(cardObj);
        }
        currentHandObjects.Clear();
        if (player == null) return;
        if (cardDisplayPrefab == null) return;
        if (handContainer == null) return;
        foreach (PlayerCardInstance cardInstance in player.heldCards)
        {
            GameObject newCard = Instantiate(cardDisplayPrefab, handContainer);
            newCard.GetComponent<CardDisplay>().Setup(cardInstance.cardData);
            currentHandObjects.Add(newCard);
        }
    }
    
    // BARU: Fungsi dari temanmu
    public void HidePlayerHand()
    {
        foreach (GameObject cardObj in currentHandObjects)
        {
            Destroy(cardObj);
        }
        currentHandObjects.Clear();
        if (currentContext == DetailPanelContext.ViewingHand)
        {
            OnCloseDetailPressed();
        }
    }
    #endregion

    // --- SEMUA FUNGSI KODEMU (Turn List, Settings, Game Over) DIAMBIL ---
    #region Player Turn & Game Flow (From Your Code)
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

    public void UpdateActivePlayer(int activeIndex)
    {
        for (int i = 0; i < uiEntries.Count; i++)
        {
            if (i == activeIndex)
            {
                uiEntries[i].SetActive(true);
            }
            else
            {
                uiEntries[i].SetActive(false);
            }
        }
    }

    public void OnOpenSettings()
    {
        if (settingsPanel == null || gameplayPanel == null) return;
        Time.timeScale = 0f;
        settingsPanel.SetActive(true);
        gameplayPanel.SetActive(false);
    }

    public void OnResumeGame()
    {
        if (settingsPanel == null || gameplayPanel == null) return;
        Time.timeScale = 1f;
        settingsPanel.SetActive(false);
        gameplayPanel.SetActive(true);
    }

    public void OnReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void SetPlayerAsWinner(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < uiEntries.Count)
        {
            uiEntries[playerIndex].SetAsWinner();
        }
    }
    
    public void ShowGameOver(List<PlayerPawn> winners, PlayerPawn loser)
    {
        if (gameplayPanel != null) gameplayPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        if (winnerListText != null)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b><size=120%>PERINGKAT</size></b>");
            sb.AppendLine(); 
            for (int i = 0; i < winners.Count; i++)
            {
                sb.AppendLine($"<color=green>#{i + 1}: {winners[i].name}</color>");
            }
            if (loser != null)
            {
                sb.AppendLine($"<color=red>Kalah: {loser.name}</color>");
            }
            winnerListText.text = sb.ToString();
        }
    }

    public void OnPlayAgainPressed()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    #endregion
}