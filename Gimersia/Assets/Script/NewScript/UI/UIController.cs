using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIController : MonoBehaviour
{
    public static UIController Instance { get; private set; }

    [Header("--- ENEMY STATUS (KIRI) ---")]
    [Tooltip("Slider HP Boss")]
    public Slider enemyHpSlider;
    [Tooltip("Text HP Boss (Contoh: 100/100)")]
    public TextMeshProUGUI enemyHpText;
    [Tooltip("Drag Boss (Object yang ada script BossState)")]
    public BossState bossState;

    [Header("--- PLAYER STATUS (KANAN) ---")]
    [Tooltip("Slider HP Player")]
    public Slider playerHpSlider;
    [Tooltip("Text HP Player")]
    public TextMeshProUGUI playerHpText;
    [Tooltip("Text Angka Dadu")]
    public TextMeshProUGUI diceRollText;

    [Header("--- CONTROLS ---")]
    [Tooltip("Tombol GO untuk mulai jalan")]
    public Button goButton;

    // Cache player aktif agar hemat performa
    private PlayerState activePlayer;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Sambungkan tombol GO
        if (goButton != null)
            goButton.onClick.AddListener(OnGoClicked);

        // Reset Teks Dadu di awal
        if (diceRollText != null) diceRollText.text = "-";
    }

    void OnEnable()
    {
        // 1. Dengar saat giliran baru mulai (Update Player HP)
        EventBus.OnTurnStarted += HandleTurnStarted;

        // 2. Dengar saat dadu selesai dilempar (Update Angka Dadu)
        var dice = FindObjectOfType<DiceController>();
        if (dice != null) dice.OnDiceResult += HandleDiceResult;
    }

    void OnDisable()
    {
        EventBus.OnTurnStarted -= HandleTurnStarted;
        var dice = FindObjectOfType<DiceController>();
        if (dice != null) dice.OnDiceResult -= HandleDiceResult;
    }

    void Update()
    {
        // --- UPDATE REALTIME HP BOSS ---
        // Kita update tiap frame karena BossState sangat sederhana (Data Only)
        if (bossState != null)
        {
            if (enemyHpSlider != null)
            {
                enemyHpSlider.maxValue = bossState.maxHP;
                enemyHpSlider.value = bossState.currentHP;
            }
            if (enemyHpText != null)
            {
                enemyHpText.text = $"{bossState.currentHP} / {bossState.maxHP}";
            }
        }
    }

    // --- EVENT HANDLERS ---

    private void HandleTurnStarted(PlayerState player)
    {
        activePlayer = player;

        // Update Tampilan Player saat giliran mulai
        UpdatePlayerUI();

        // Subscribe: Jika HP player berubah di tengah jalan (kena damage), UI ikut berubah
        player.OnStateChanged += (p) => UpdatePlayerUI();

        // Reset Dadu jadi kosong atau "-"
        if (diceRollText != null) diceRollText.text = "-";
    }

    private void HandleDiceResult(int result)
    {
        if (diceRollText != null)
        {
            diceRollText.text = result.ToString();
        }
    }

    // Helper Update Player
    private void UpdatePlayerUI()
    {
        if (activePlayer == null) return;

        if (playerHpSlider != null)
        {
            playerHpSlider.maxValue = activePlayer.maxHP;
            playerHpSlider.value = activePlayer.currentHP;
        }

        if (playerHpText != null)
        {
            playerHpText.text = $"{activePlayer.currentHP} / {activePlayer.maxHP}";
        }
    }

    // --- FUNGSI TOMBOL GO ---
    public void OnGoClicked()
    {
        // Langsung suruh TurnManager jalan
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnGoPressed();
        }
    }
}