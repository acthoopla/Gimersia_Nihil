using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIController : MonoBehaviour
{
    // 1. Tambahkan Singleton agar bisa dipanggil dari CardSelectionHolder
    public static UIController Instance { get; private set; }

    [Header("References")]
    public Button goButton;
    public TextMeshProUGUI diceRollText;

    [Header("Modifier UI (New)")]
    public GameObject modifierPanel;       // Panel kotak list kartu
    public TextMeshProUGUI modifierLogText; // Teks untuk "-1 Move", "Heal", dll

    void Awake()
    {
        // Setup Singleton
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Sembunyikan panel modifier saat game mulai
        if (modifierPanel) modifierPanel.SetActive(false);
    }

    void Start()
    {
        if (goButton != null)
            goButton.onClick.AddListener(OnGoClicked);
    }

    void OnEnable()
    {
        // Logika Event Dadu milikmu (Tetap dipertahankan)
        var dice = FindObjectOfType<DiceController>();
        if (dice != null) dice.OnDiceResult += UpdateDiceText;
    }

    void OnDisable()
    {
        var dice = FindObjectOfType<DiceController>();
        if (dice != null) dice.OnDiceResult -= UpdateDiceText;
    }

    // 2. Ubah menjadi PUBLIC agar TurnManager juga bisa akses jika perlu
    public void UpdateDiceText(int result)
    {
        if (diceRollText != null) diceRollText.text = "Roll: " + result;
    }

    // --- TAMBAHAN: LOGIK MODIFIER UI (Dipanggil CardSelectionHolder) ---
    public void ShowModifierPanel()
    {
        if (modifierPanel)
        {
            modifierPanel.SetActive(true);
            if (modifierLogText) modifierLogText.text = "Modifier used:\n"; // Reset Header
        }

        if (goButton) goButton.interactable = true;
    }

    public void HideModifierPanel()
    {
        if (modifierPanel) modifierPanel.SetActive(false);
    }

    public void AddModifierLog(string text)
    {
        if (modifierLogText)
        {
            modifierLogText.text += $"- {text}\n";
        }
    }
    // ------------------------------------------------------------------

    public void OnGoClicked()
    {
        if (goButton) goButton.interactable = false; // Cegah spam klik

        // Panggil TurnManager
        if (TurnManager.Instance != null)
        {
            // Kita panggil fungsi ExecutePendingQueue untuk menjalankan kartu yang antre
            // (Pastikan TurnManager punya fungsi ini atau aliasnya)
            TurnManager.Instance.ExecutePendingQueue();

            // Atau jika kamu pakai alias OnGoPressed di TurnManager, bisa pakai ini:
            // TurnManager.Instance.OnGoPressed();
        }
    }
}