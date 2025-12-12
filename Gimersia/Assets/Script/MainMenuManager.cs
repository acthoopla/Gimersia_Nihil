using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene to Load")]
    public string gameSceneName = "GameScene"; // <-- Ganti "GameScene" di Inspector

    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject creditPanel;
    public GameObject tutorialPanel;

    [Header("References")]
    public SceneLoader sceneLoader;

    // --- Panggil saat game baru dimulai ---
    void Start()
    {
        // Pastikan hanya main menu yang aktif saat awal
        // dan yang lain tersembunyi.
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
        creditPanel.SetActive(false);
        tutorialPanel.SetActive(false);
    }

    // --- Fungsi Tombol Main Menu ---

    public void OnPlayPressed()
    {
        Debug.Log($"Memuat scene: {gameSceneName}");
        sceneLoader.LoadNextLevel(gameSceneName);
        // SceneManager.LoadScene(gameSceneName);
    }

    public void OnSettingsPressed()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void OnExitPressed()
    {
        Debug.Log("Keluar dari game...");

        // Ini hanya berfungsi di build (EXE, APK), tidak di Editor
        Application.Quit();

        // Kode ini untuk mematikan Play Mode di Editor Unity
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void OnCreditPressed()
    {
        mainMenuPanel.SetActive(false);
        creditPanel.SetActive(true);
    }

    // --- Fungsi Tombol 'Close' ---

    public void OnCloseCreditPressed()
    {
        creditPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    // (Opsional) Kamu bisa buat fungsi ini untuk tombol close di Settings
    public void OnCloseSettingsPressed()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    public void OnTutorialPressed()
    {
        mainMenuPanel.SetActive(false);
        tutorialPanel.SetActive(true);
    }

    public void OnCloseTutorialPressed()
    {
        tutorialPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }
}
