using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIController : MonoBehaviour
{
    public static UIController Instance { get; private set; }

    [Header("--- PANELS ---")]
    public GameObject gameOverLosePanel;
    public GameObject gameOverWinPanel;

    [Header("--- ENEMY STATUS (KIRI) ---")]
    public Slider enemyHpSlider;
    public TextMeshProUGUI enemyHpText;
    // [FIX ERROR] Variabel ini dikembalikan karena PlayerState membutuhkannya
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

        // Matikan panel di awal
        if (gameOverLosePanel) gameOverLosePanel.SetActive(false);
        if (gameOverWinPanel) gameOverWinPanel.SetActive(false);
    }

    void Start()
    {
        if (goButton != null) goButton.onClick.AddListener(OnGoClicked);
        UpdatePlayerUI(playerState);
    }

    void OnEnable()
    {
        if (playerState != null) playerState.OnStateChanged += UpdatePlayerUI;
        var dice = FindObjectOfType<DiceController>();
        if (dice != null) dice.OnDiceResult += UpdateDiceText;
    }

    void OnDisable()
    {
        if (playerState != null) playerState.OnStateChanged -= UpdatePlayerUI;
        var dice = FindObjectOfType<DiceController>();
        if (dice != null) dice.OnDiceResult -= UpdateDiceText;
    }

    void Update()
    {
        // Update Visual Boss HP
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

    // --- FUNGSI UTAMA KONTROL TOMBOL GO ---
    public void SetGoButtonInteractable(bool state)
    {
        if (goButton != null)
            goButton.interactable = state;
    }

    public void OnGoClicked()
    {
        // Matikan tombol segera agar tidak bisa diklik 2x
        SetGoButtonInteractable(false);

        if (TurnManager.Instance != null)
        {
            // Bisa panggil OnGoPressed atau ExecutePendingQueue (sama saja sekarang)
            TurnManager.Instance.OnGoPressed();
        }
    }

    // --- HELPERS LAINNYA ---
    private void UpdatePlayerUI(PlayerState p)
    {
        if (p == null) return;
        if (playerHpSlider != null) { playerHpSlider.maxValue = p.maxHP; playerHpSlider.value = p.currentHP; }
        if (playerHpText != null) playerHpText.text = $"{p.currentHP} / {p.maxHP}";
    }

    public void UpdateDiceText(int result)
    {
        if (diceRollText != null) diceRollText.text = "Roll: " + result;
    }

    public void ShowModifierPanel() { if (modifierPanel) modifierPanel.SetActive(true); }
    public void HideModifierPanel() { if (modifierPanel) modifierPanel.SetActive(false); }
    public void AddModifierLog(string text) { if (modifierLogText) modifierLogText.text += $"- {text}\n"; }

    public void ShowGameOver() { if (gameOverLosePanel) gameOverLosePanel.SetActive(true); }
    public void ShowVictory() { if (gameOverWinPanel) gameOverWinPanel.SetActive(true); }
}