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
    public GameObject playerWinnerPrefab; // Prefab untuk menampilkan winner
    public Transform winnerListContainer; // Container untuk list winner
    public List<Sprite> crowns; // List sprite mahkota (#1, #2, #3)
    public Button playAgainButton;
    public Button returnToMenuButton_GameOver;

    [Header("UI Utama (Game)")]
    public TextMeshProUGUI infoText;
    public TextMeshProUGUI currentActionText;
    public TextMeshProUGUI cycleText;

    [Header("Game Log")]
    public GameObject gameLogPanel;
    public TextMeshProUGUI gameLogText;
    public ScrollRect logScrollRect;
    public int maxLogLines = 50;

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

    [Header("Tampilan Tangan Pemain")]
    public Transform popupContainer;
    public GameObject cardDisplayPrefab;
    public Transform handContainer;

    private List<GameObject> currentHandObjects = new List<GameObject>();
    private List<GameObject> currentChoiceObjects = new List<GameObject>();
    private List<GameObject> currentWinnerObjects = new List<GameObject>(); // Track winner UI objects
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
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (gameplayPanel != null) gameplayPanel.SetActive(true);
        Time.timeScale = 1f;
        if (cardDetailPanel != null) cardDetailPanel.SetActive(false);
        if (cardChoicePanel != null) cardChoicePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (useCardButton != null) useCardButton.onClick.AddListener(OnUseCardPressed);
        if (confirmChoiceButton != null) confirmChoiceButton.onClick.AddListener(OnConfirmCardChoicePressed);
        if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgainPressed);
        if (returnToMenuButton_GameOver != null) returnToMenuButton_GameOver.onClick.AddListener(OnReturnToMainMenu);

        if (currentActionText != null) currentActionText.text = "";
        if (gameLogText != null) gameLogText.text = "";
    }

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

    private IEnumerator ForceScrollDown()
    {
        if (logScrollRect == null) yield break;
        yield return null;
        logScrollRect.verticalNormalizedPosition = 0f;
    }

    #endregion

    #region Card Logic Functions

    private void OnUseCardPressed()
    {
        if (currentContext == DetailPanelContext.ViewingHand && currentlyShownCard != null)
        {
            MultiplayerManager.Instance.UseCard(currentlyShownCard);
            cardDetailPanel.SetActive(false);
            currentlyShownCard = null;
        }
    }

    public void OnCardChoiceSelected(CardData chosenCard)
    {
        if (MultiplayerManager.Instance == null) return;

        MultiplayerManager.Instance.AddChosenCardToPlayer(chosenCard);

        if (cardChoicePanel != null)
            cardChoicePanel.SetActive(false);

        ClearChoicePrefabs();

        if (handContainer != null)
        {
            handContainer.gameObject.SetActive(true);
        }

        SetActionText($"{MultiplayerManager.Instance.GetCurrentPlayer().name} mengambil kartu {chosenCard.cardName}.");
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
            if (cardInstance == null || cardInstance.cardData == null)
            {
                Debug.LogWarning("DisplayPlayerHand: Melewatkan 1 kartu karena datanya null.");
                continue;
            }

            GameObject newCard = Instantiate(cardDisplayPrefab, handContainer);

            CardDisplay cardDisplayScript = newCard.GetComponent<CardDisplay>();
            if (cardDisplayScript != null)
            {
                cardDisplayScript.Setup(cardInstance.cardData);
            }

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
            //OnCloseDetailPressed();
        }
    }
    #endregion

    #region Player Turn & Game Flow

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
                entryScript.SetActive(false);
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

        // Clear previous winner UI objects
        foreach (GameObject winnerObj in currentWinnerObjects)
        {
            Destroy(winnerObj);
        }
        currentWinnerObjects.Clear();

        if (winnerListContainer == null || playerWinnerPrefab == null)
        {
            Debug.LogWarning("Winner container atau prefab tidak di-set di UIManager!");
            return;
        }

        // Create winner entries
        for (int i = 0; i < winners.Count; i++)
        {
            GameObject winnerEntry = Instantiate(playerWinnerPrefab, winnerListContainer);
            PlayerWinnerDisplay winnerDisplay = winnerEntry.GetComponent<PlayerWinnerDisplay>();

            if (winnerDisplay != null)
            {
                int rank = i + 1;
                Sprite crownSprite = null;

                // Hanya berikan crown untuk rank 1-3
                if (rank <= 3 && crowns != null && crowns.Count >= rank)
                {
                    crownSprite = crowns[rank - 1]; // index 0 untuk rank 1, dst
                }

                winnerDisplay.Setup(winners[i].name, rank, crownSprite);
            }

            currentWinnerObjects.Add(winnerEntry);
        }

        // Tambahkan loser di posisi terakhir dengan rank sesuai jumlah player
        if (loser != null)
        {
            GameObject loserEntry = Instantiate(playerWinnerPrefab, winnerListContainer);
            PlayerWinnerDisplay loserDisplay = loserEntry.GetComponent<PlayerWinnerDisplay>();

            if (loserDisplay != null)
            {
                // Rank loser adalah jumlah total player (winners + loser)
                int loserRank = winners.Count + 1;
                loserDisplay.SetupAsLoser(loser.name, loserRank);
            }

            currentWinnerObjects.Add(loserEntry);
        }
    }

    public void OnPlayAgainPressed()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    #endregion
}
