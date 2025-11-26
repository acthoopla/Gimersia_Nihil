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
    public BossState bossState; // [MERGE] Referensi punyamu

    [Header("--- PLAYER STATUS (KANAN) ---")]
    public Slider playerHpSlider;
    public TextMeshProUGUI playerHpText;
    public TextMeshProUGUI diceRollText;
    
    [Tooltip("Drag PlayerState di sini agar UI langsung tahu siapa player utamanya")]
    public PlayerState playerState;

    [Header("--- CARD MODIFIER UI (TEMAN) ---")]
    public GameObject modifierPanel;        // Panel kotak list kartu
    public TextMeshProUGUI modifierLogText; // Teks untuk "-1 Move", "Heal", dll

    [Header("--- CONTROLS ---")]
    public Button goButton;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // [MERGE] Auto-find PlayerState (Punyamu)
        if (playerState == null)
        {
            playerState = FindObjectOfType<PlayerState>();
            // Warning dihapus agar tidak spam error kalau belum ada player spawn
        }

        // [MERGE] Hide panel modifier (Punya Teman)
        if (modifierPanel) modifierPanel.SetActive(false);
    }

    void Start()
    {
        if (goButton != null)
            goButton.onClick.AddListener(OnGoClicked);

        if (diceRollText != null) diceRollText.text = "Roll: -";

        // [MERGE] Update UI Awal (Punyamu)
        UpdatePlayerUI(playerState);
    }

    void OnEnable()
    {
        // [MERGE] Subscribe Player Logic (Punyamu)
        if (playerState != null) playerState.OnStateChanged += UpdatePlayerUI;

        // [MERGE] Subscribe Dice Logic (Gabungan)
        var dice = FindObjectOfType<DiceController>();
        if (dice != null) dice.OnDiceResult += UpdateDiceText;
        
        EventBus.OnTurnStarted += HandleTurnReset;
    }

    void OnDisable()
    {
        if (playerState != null) playerState.OnStateChanged -= UpdatePlayerUI;
        
        var dice = FindObjectOfType<DiceController>();
        if (dice != null) dice.OnDiceResult -= UpdateDiceText;

        EventBus.OnTurnStarted -= HandleTurnReset;
    }

    void Update()
    {
        // [MERGE] Boss Status Update (Punyamu)
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

    // --- EVENT HANDLERS ---

    // [MERGE] Logic Player HP (Punyamu)
    private void UpdatePlayerUI(PlayerState p)
    {
        if (p == null) return;
        if (playerHpSlider != null)
        {
            playerHpSlider.maxValue = p.maxHP;
            playerHpSlider.value = p.currentHP;
        }
        if (playerHpText != null) playerHpText.text = $"{p.currentHP} / {p.maxHP}";
    }

    // [MERGE] Logic Text Dadu (Gabungan)
    public void UpdateDiceText(int result)
    {
        if (diceRollText != null) diceRollText.text = "Roll: " + result;
    }

    private void HandleTurnReset(PlayerState p)
    {
        if (diceRollText != null) diceRollText.text = "Roll: -";
    }

    // --- MODIFIER UI (LOGIC TEMAN) ---
    public void ShowModifierPanel()
    {
        if (modifierPanel)
        {
            modifierPanel.SetActive(true);
            if (modifierLogText) modifierLogText.text = "Modifier used:\n";
        }
        if (goButton) goButton.interactable = true;
    }

    public void HideModifierPanel()
    {
        if (modifierPanel) modifierPanel.SetActive(false);
    }

    public void AddModifierLog(string text)
    {
        if (modifierLogText) modifierLogText.text += $"- {text}\n";
    }

    // --- ACTION ---
    public void OnGoClicked()
    {
        if (goButton) goButton.interactable = false;

        // [MERGE] Panggil TurnManager dengan method yang paling lengkap (Punya Teman)
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.ExecutePendingQueue();
        }
    }
}