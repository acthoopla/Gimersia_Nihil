using System.Collections; // <-- Tambahkan ini
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // <-- Tambahkan ini

public class UIManager : MonoBehaviour
{
    [Header("Player Turn UI")]
    public GameObject playerUIEntryPrefab; // Prefab [Player 1] yang kita buat
    public Transform playerUIContainer;    // Panel "PlayerTurnPanel" (Vertical Layout Group)

    // --- BAGIAN BARU: PAUSE & SETTINGS ---
    [Header("Pause/Settings Menu")]
    [Tooltip("Panel utama gameplay (berisi daftar pemain, info giliran, tombol settings, dll.)")]
    public GameObject gameplayPanel; // Panel "induk" untuk semua UI game

    [Tooltip("Panel settings yang muncul saat di-pause")]
    public GameObject settingsPanel;

    [Tooltip("Nama scene Main Menu (pastikan ada di Build Settings!)")]
    public string mainMenuSceneName = "MainMenu"; // <-- Isi di Inspector
    // ------------------------------------

    private List<PlayerUIEntry> uiEntries = new List<PlayerUIEntry>();

    // --- FUNGSI BARU ---
    void Start()
    {
        // Pastikan settings panel mati saat game dimulai
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        // Pastikan game UI nyala
        if (gameplayPanel != null)
        {
            gameplayPanel.SetActive(true);
        }

        // Pastikan game tidak ter-pause (jika kita kembali dari scene lain)
        Time.timeScale = 1f;
    }
    // -------------------

    /// <summary>
    /// Dipanggil oleh MultiplayerManager *SATU KALI* saat giliran ditentukan.
    /// </summary>
    public void SetupPlayerList(List<PlayerPawn> playerTurnOrder)
    {
        // (Fungsi ini tidak berubah)
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
    /// </summary>
    public void UpdateActivePlayer(int activeIndex)
    {
        // (Fungsi ini tidak berubah)
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

    // --- FUNGSI-FUNGSI BARU UNTUK SETTINGS ---

    /// <summary>
    /// Dipanggil oleh tombol Settings di UI Gameplay.
    /// </summary>
    public void OnOpenSettings()
    {
        if (settingsPanel == null || gameplayPanel == null) return;

        // Jeda permainan
        Time.timeScale = 0f;

        // Tukar panel
        settingsPanel.SetActive(true);
        gameplayPanel.SetActive(false);
    }

    /// <summary>
    /// Dipanggil oleh tombol 'Resume' di dalam Settings Panel.
    /// </summary>
    public void OnResumeGame()
    {
        if (settingsPanel == null || gameplayPanel == null) return;

        // Lanjutkan permainan
        Time.timeScale = 1f;

        // Tukar panel kembali
        settingsPanel.SetActive(false);
        gameplayPanel.SetActive(true);
    }

    /// <summary>
    /// Dipanggil oleh tombol 'Return to Main Menu' di Settings Panel.
    /// </summary>
    public void OnReturnToMainMenu()
    {
        // PENTING: Selalu set timeScale kembali ke 1
        // sebelum meninggalkan scene agar game tidak 'freeze'.
        Time.timeScale = 1f;

        SceneManager.LoadScene(mainMenuSceneName);
    }
}