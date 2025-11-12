using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;
using System.Text;
using System.Linq;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    private enum DetailPanelContext { ViewingHand, ConfirmingChoice }
    private DetailPanelContext currentContext;

    [Header("Pause/Settings Menu")]
    public GameObject gameplayPanel;
    public GameObject settingsPanel;
    public string mainMenuSceneName = "MainMenu";

    [Header("Player Turn UI")]
    public GameObject playerUIEntryPrefab;
    public Transform playerUIContainer;
    private List<PlayerUIEntry> uiEntries = new List<PlayerUIEntry>();

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI winnerListText;
    public Button playAgainButton;
    public Button returnToMenuButton_GameOver;

    [Header("UI Utama (Game)")]
    public TextMeshProUGUI infoText;
    public TextMeshProUGUI currentActionText;
    public TextMeshProUGUI cycleText;

    [Header("Game Log")]
    public GameObject gameLogPanel; // <-- Panelnya masih ada
    // DIHAPUS: public Button toggleLogButton; 
    public TextMeshProUGUI gameLogText;
    public ScrollRect logScrollRect;
    public int maxLogLines = 50;

    [Header("Panel Detail Kartu (Pop-up)")]
    // ... (sisa variabel card panel tidak berubah)
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
    // ... (tidak berubah)
    public GameObject cardChoicePanel;
    public GameObject cardChoicePrefab;
    public Transform choiceContainer;

    [Header("Tampilan Tangan Pemain")]
    // ... (tidak berubah)
    public GameObject cardDisplayPrefab;
    public Transform handContainer;

    private List<GameObject> currentHandObjects = new List<GameObject>();
    private List<GameObject> currentChoiceObjects = new List<GameObject>();
    private CardData currentlyShownCard;
    private List<string> logMessages = new List<string>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        // Panel
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (gameplayPanel != null) gameplayPanel.SetActive(true);
        Time.timeScale = 1f;
        if (cardDetailPanel != null) cardDetailPanel.SetActive(false);
        if (cardChoicePanel != null) cardChoicePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // --- PERUBAHAN DI SINI ---
        // if (gameLogPanel != null) gameLogPanel.SetActive(true); // Langsung nyala
        // -------------------------

        // Tombol Kartu
        if (closeDetailButton != null) closeDetailButton.onClick.AddListener(OnCloseDetailPressed);
        if (useCardButton != null) useCardButton.onClick.AddListener(OnUseCardPressed);
        if (confirmChoiceButton != null) confirmChoiceButton.onClick.AddListener(OnConfirmCardChoicePressed);

        // Tombol Game Over
        if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgainPressed);
        if (returnToMenuButton_GameOver != null) returnToMenuButton_GameOver.onClick.AddListener(OnReturnToMainMenu);

        // DIHAPUS: Listener untuk toggleLogButton

        // Teks
        if (currentActionText != null) currentActionText.text = "";
        if (gameLogText != null) gameLogText.text = "";
    }

    // --- FUNGSI LOGIKA BARU ---
    #region Game Log & Status

    public void Log(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        logMessages.Add(message);
        if (logMessages.Count > maxLogLines)
        {
            logMessages.RemoveAt(0);
        }
        if (gameLogText != null)
        {
            gameLogText.text = string.Join("\n", logMessages);
        }
        StartCoroutine(ForceScrollDown());
    }

    public void SetTurnText(string message)
    {
        if (infoText != null)
        {
            infoText.text = message;
        }
        Log(message);
    }

    public void SetActionText(string message)
    {
        if (currentActionText != null)
        {
            currentActionText.text = message;
        }
        Log(message);
    }

    public void ClearActionText()
    {
        if (currentActionText != null)
        {
            currentActionText.text = "";
        }
    }

    // DIHAPUS: Fungsi OnToggleLogPressed()

    private IEnumerator ForceScrollDown()
    {
        if (logScrollRect == null) yield break;
        yield return null;
        logScrollRect.verticalNormalizedPosition = 0f;
    }

    #endregion

    // (Sisa script tidak berubah)
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
        cardTypeText.text = card.cardArchetype.ToString();
        cardTargetText.text = card.cardTargetType.ToString();
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
        {
            string cycle = "";
            cycle = count > 1 ? "Cycles" : "Cycle";
            cycleText.text = $"{cycle} {count}";
        }
    }

    public void DisplayPlayerHand(PlayerPawn player)
    {
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