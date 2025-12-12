using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class UIController : MonoBehaviour
{
    public static UIController Instance { get; private set; }

    [Header("References")]
    public SceneLoader sceneLoader;

    [Header("--- PANELS ---")]
    public GameObject gameOverLosePanel;
    public GameObject gameOverWinPanel;

    [Header("--- SETTINGS UI ---")]
    [Tooltip("Drag GameObject 'Settings' (Parent Panel)")]
    public GameObject settingsPanel;

    [Tooltip("Drag Child: ExitButton")]
    public Button exitButton;

    [Tooltip("Drag Child: BackGameButton")]
    public Button resumeButton;

    [Tooltip("Tombol Gear/Pause di layar utama")]
    public Button openSettingsButton;

    [Tooltip("Nama Scene Main Menu (Case Sensitive!)")]
    public string mainMenuSceneName = "MainMenu";
    public string gameplaySceneName = "Gameplay";

    [Header("--- TILE INFO UI ---")]
    public GameObject tileInfoPanel;
    public TextMeshProUGUI tileInfoText;

    [Header("--- ENEMY STATUS (KIRI) ---")]
    public Slider enemyHpSlider;
    public TextMeshProUGUI enemyHpText;
    public BossState bossState;

    [Header("--- PLAYER STATUS (KANAN) ---")]
    public Slider playerHpSlider;
    public TextMeshProUGUI playerHpText;
    public TextMeshProUGUI diceRollText;

    [Tooltip("Drag PlayerState di sini")]
    public PlayerState playerState;

    [Header("--- CARD MODIFIER UI ---")]
    public GameObject modifierPanel;
    public TextMeshProUGUI modifierLogText;

    [Header("--- CONTROLS ---")]
    public Button goButton;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (playerState == null) playerState = FindObjectOfType<PlayerState>();

        // Reset kondisi panel
        if (gameOverLosePanel) gameOverLosePanel.SetActive(false);
        if (gameOverWinPanel) gameOverWinPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);

        if (tileInfoPanel) tileInfoPanel.SetActive(true);
        if (tileInfoText) tileInfoText.text = "Start Game";
    }

    void Start()
    {
        // 1. Setup Tombol Gameplay
        if (goButton != null) goButton.onClick.AddListener(OnGoClicked);

        // 2. Setup Tombol Settings
        if (openSettingsButton != null) openSettingsButton.onClick.AddListener(OnOpenSettingsClicked);

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(OnExitGameClicked);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(OnResumeGameClicked);
        }

        UpdatePlayerUI(playerState);

        // Nonaktifkan tombol Go di awal permainan
        SetGoButtonInteractable(false);
    }

    void OnEnable()
    {
        if (playerState != null) playerState.OnStateChanged += UpdatePlayerUI;
        var dice = FindObjectOfType<DiceController>();
        if (dice != null) dice.OnDiceResult += UpdateDiceText;

        EventBus.OnTurnStarted += HandleTurnReset;
        EventBus.OnTileLanded += HandleTileLanded;
    }

    void OnDisable()
    {
        if (playerState != null) playerState.OnStateChanged -= UpdatePlayerUI;
        var dice = FindObjectOfType<DiceController>();
        if (dice != null) dice.OnDiceResult -= UpdateDiceText;

        EventBus.OnTurnStarted -= HandleTurnReset;
        EventBus.OnTileLanded -= HandleTileLanded;
    }

    void Update()
    {
        if (bossState != null)
        {
            if (enemyHpSlider != null)
            {
                enemyHpSlider.maxValue = bossState.maxHP;
                enemyHpSlider.value = bossState.currentHP;
            }
            if (enemyHpText != null)
                enemyHpText.text = $"{bossState.currentHP} / {bossState.maxHP}";
        }
    }

    // ============================================================
    // LOGIC SETTINGS (TANPA FREEZE TIME)
    // ============================================================

    public void OnOpenSettingsClicked()
    {
        if (settingsPanel != null)
        {
            // Cukup nyalakan panel saja. Game tetap jalan di background.
            // Pastikan Panel Settings punya komponen Image (Background) 
            // dengan Alpha > 0 dan 'Raycast Target' ON agar player tidak bisa klik board.
            settingsPanel.SetActive(true);
        }
    }

    public void OnResumeGameClicked()
    {
        Debug.Log("Resume Game Clicked");
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    public void OnRestartGameClicked()
    {
        Debug.Log("Exit Game Clicked");
        sceneLoader.LoadNextLevel(gameplaySceneName);
    }

    public void OnExitGameClicked()
    {
        Debug.Log("Exit Game Clicked");
        sceneLoader.LoadNextLevel(mainMenuSceneName);
    }

    // ============================================================
    // LOGIC TILE INFO
    // ============================================================

    private void HandleTileLanded(PlayerState player, Tiles tile)
    {
        if (tileInfoText == null || tile == null) return;
        string message = GetTileDescription(tile);
        tileInfoText.text = message;
    }

    private string GetTileDescription(Tiles tile)
    {
        switch (tile.type)
        {
            case TileType.Normal: return "Safe Zone\nTake a break.";
            case TileType.SnakeStart: return "SNAKE!\nDown you go.";
            case TileType.LadderStart: return "LADDER!\nAscend & Choose a blessing";
            case TileType.Damage: return "TRAP!\nAttacked.";
            case TileType.Attack: return "ATTACK!\nDirect attack on the enemy.";
            case TileType.CardMovement:
            case TileType.CardBuff:
            case TileType.CardRandom: return "BLESSED\nGet a Card(s).";
            case TileType.Disarm: return "DISARM!\nDiscard 2 cards from your hand.";
            case TileType.Provocation: return "PROVOKED!\nAdd + 2 move on your next turn.";
            case TileType.Despair: return "DESPAIR!\nReduce - 2 move on your next turn.";
            case TileType.Death: return "DEATH TILE\nGame Over.";
            default: return $"Tile {tile.tileID}\nNo effect.";
        }
    }

    // --- FUNGSI UTAMA KONTROL TOMBOL GO ---
    public void SetGoButtonInteractable(bool state)
    {
        if (goButton != null)
            goButton.interactable = state;
    }

    public void OnGoClicked()
    {
        SetGoButtonInteractable(false);
        if (TurnManager.Instance != null) TurnManager.Instance.OnGoPressed();
    }

    // --- HELPERS & EVENT HANDLERS ---

    private void UpdatePlayerUI(PlayerState p)
    {
        if (p == null) return;
        if (playerHpSlider != null) { playerHpSlider.maxValue = p.maxHP; playerHpSlider.value = p.currentHP; }
        if (playerHpText != null) playerHpText.text = $"{p.currentHP} / {p.maxHP}";
    }

    public void UpdateDiceText(int result)
    {
        if (diceRollText != null) diceRollText.text = "Roll: " + result;

        // Tambahkan kode untuk mengaktifkan tombol Go
        SetGoButtonInteractable(true);
    }

    private void HandleTurnReset(PlayerState p)
    {
        if (diceRollText != null) diceRollText.text = "Roll: -";
        if (modifierLogText != null) modifierLogText.text = "";

        // Nonaktifkan tombol Go sampai dadu dilempar
        SetGoButtonInteractable(false);
    }

    public void ShowModifierPanel() { if (modifierPanel) modifierPanel.SetActive(true); }
    public void HideModifierPanel() { if (modifierPanel) modifierPanel.SetActive(false); }
    public void AddModifierLog(string text) { if (modifierLogText) modifierLogText.text += $"- {text}\n"; }

    public void ShowGameOver() { if (gameOverLosePanel) gameOverLosePanel.SetActive(true); }
    public void ShowVictory() { if (gameOverWinPanel) gameOverWinPanel.SetActive(true); }
}