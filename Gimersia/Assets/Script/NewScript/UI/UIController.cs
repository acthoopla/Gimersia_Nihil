using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIController : MonoBehaviour
{
    public static UIController Instance { get; private set; }

    [Header("--- ENEMY STATUS (KIRI) ---")]
    public Slider enemyHpSlider;
    public TextMeshProUGUI enemyHpText;
    public BossState bossState;

    [Header("--- PLAYER STATUS (KANAN) ---")]
    public Slider playerHpSlider;
    public TextMeshProUGUI playerHpText;
    public TextMeshProUGUI diceRollText;

    // UBAHAN 1: Assign ini di Inspector atau biarkan auto-find di Awake
    [Tooltip("Drag PlayerState di sini agar UI langsung tahu siapa player utamanya")]
    public PlayerState playerState;

    [Header("--- CONTROLS ---")]
    public Button goButton;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // UBAHAN 2: Auto-find jika lupa drag di inspector (Safety net)
        if (playerState == null)
        {
            playerState = FindObjectOfType<PlayerState>();
            if (playerState == null) Debug.LogError("UIController: FATAL - PlayerState tidak ditemukan di scene!");
        }
    }

    void Start()
    {
        if (goButton != null)
            goButton.onClick.AddListener(OnGoClicked);

        if (diceRollText != null) diceRollText.text = "-";

        // UBAHAN 3: Inisialisasi tampilan awal langsung
        UpdatePlayerUI(playerState);
    }

    void OnEnable()
    {
        // UBAHAN 4: Subscribe Event Player LANGSUNG, jangan tunggu TurnStarted
        if (playerState != null)
        {
            playerState.OnStateChanged += UpdatePlayerUI;
        }

        var dice = FindObjectOfType<DiceController>();
        if (dice != null) dice.OnDiceResult += HandleDiceResult;

        // Kita tetap dengar TurnStarted hanya untuk reset dadu, bukan untuk cari player
        EventBus.OnTurnStarted += HandleTurnReset;
    }

    void OnDisable()
    {
        // UBAHAN 5: Unsubscribe dengan benar untuk mencegah memory leak
        if (playerState != null)
        {
            playerState.OnStateChanged -= UpdatePlayerUI;
        }

        var dice = FindObjectOfType<DiceController>();
        if (dice != null) dice.OnDiceResult -= HandleDiceResult;

        EventBus.OnTurnStarted -= HandleTurnReset;
    }

    void Update()
    {
        // --- BOSS UPDATE (Tetap pertahankan Polling untuk Boss jika logicnya simple) ---
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

    // UBAHAN 6: Method ini sekarang sesuai signature event Action<PlayerState>
    private void UpdatePlayerUI(PlayerState p)
    {
        if (p == null) return;

        if (playerHpSlider != null)
        {
            playerHpSlider.maxValue = p.maxHP;
            playerHpSlider.value = p.currentHP;
        }

        if (playerHpText != null)
        {
            playerHpText.text = $"{p.currentHP} / {p.maxHP}";
        }
    }

    private void HandleTurnReset(PlayerState p)
    {
        // Hanya reset UI dadu saat ganti giliran
        if (diceRollText != null) diceRollText.text = "-";
    }

    private void HandleDiceResult(int result)
    {
        if (diceRollText != null)
        {
            diceRollText.text = result.ToString();
        }
    }

    public void OnGoClicked()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnGoPressed();
        }
    }
}