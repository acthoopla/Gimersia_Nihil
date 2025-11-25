using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public enum TurnState { Idle, Rolling, StrategyPhase, Moving, ResolveTile, EndTurn, GameOver }
    public TurnState state = TurnState.Idle;

    [Header("References")]
    public DiceController diceController;
    public DiceInputHandler diceInputHandler;

    public List<PlayerState> players = new List<PlayerState>();
    private int currentIndex = 0;
    private PlayerState currentPlayer => (players.Count > 0) ? players[currentIndex] : null;

    // Variabel Dadu & Tombol
    private int currentRollValue = 0;
    private bool goButtonPressed = false;

    // Variabel untuk Tile System (biar ga error)
    private bool awaitingTileResolve = false;

    void Awake() { if (Instance == null) Instance = this; else Destroy(gameObject); }

    void Start()
    {
        if (diceController == null) diceController = FindObjectOfType<DiceController>();
        if (diceInputHandler == null) diceInputHandler = FindObjectOfType<DiceInputHandler>();

        if (diceController != null) diceController.OnDiceResult += HandleDiceResult;
    }

    void OnDestroy()
    {
        if (diceController != null) diceController.OnDiceResult -= HandleDiceResult;
    }

    // =======================================================================
    // BAGIAN INI YANG MEMPERBAIKI ERROR (JANGAN DIHAPUS)
    // =======================================================================

    // 1. Fix Error di NewGameManager (Butuh 2 argumen)
    public void StartGame(List<PlayerState> playerStates, int startIndex = 0)
    {
        players = new List<PlayerState>(playerStates);
        currentIndex = Mathf.Clamp(startIndex, 0, players.Count - 1);
        StartCoroutine(RunTurnLoop());
    }

    // 2. Fix Error di TileEffectSystem (Butuh fungsi ini)
    public void NotifyTileResolveComplete(PlayerState player)
    {
        if (state == TurnState.ResolveTile && player == currentPlayer)
        {
            awaitingTileResolve = false;
        }
    }

    // 3. Fix Error di SlotUIManager (Butuh fungsi ini)
    // FUNGSI INI JUGA YANG DIPANGGIL TOMBOL GO
    public void OnExecuteButtonPressed()
    {
        // Hanya respon kalau lagi fase Strategy (setelah dadu, sebelum jalan)
        if (state == TurnState.StrategyPhase)
        {
            Debug.Log(">>> TOMBOL GO DITEKAN! LANJUT JALAN! <<<");
            goButtonPressed = true;
        }
    }

    // Alias untuk UIController kamu (OnGoPressed -> OnExecuteButtonPressed)
    public void OnGoPressed()
    {
        OnExecuteButtonPressed();
    }

    // =======================================================================
    // LOGIKA UTAMA (Dice -> Go -> Move)
    // =======================================================================

    private IEnumerator RunTurnLoop()
    {
        while (true)
        {
            if (players.Count == 0) yield break;

            // --- 1. MULAI GILIRAN ---
            state = TurnState.Rolling;
            goButtonPressed = false;
            currentRollValue = 0;

            // RESET POSISI DADU AGAR KEMBALI KE TENGAH/AWAL
            if (diceController != null)
            {
                diceController.ResetState(); // <--- TAMBAHKAN BARIS INI
            }

            Debug.Log($"Giliran {currentPlayer.name}. Silakan Lempar Dadu.");

            // Nyalakan input dadu
            if (diceInputHandler) diceInputHandler.InputEnabled = true;

            // --- 2. TUNGGU DADU ---
            while (state == TurnState.Rolling) yield return null;

            // --- 3. TUNGGU TOMBOL GO ---
            Debug.Log($"Dadu: {currentRollValue}. Tekan GO di UI untuk jalan.");

            // Game PAUSE disini sampai kamu tekan tombol GO
            while (!goButtonPressed)
            {
                yield return null;
            }

            // --- 4. BERGERAK ---
            yield return StartCoroutine(MoveSequence());

            // --- 5. GANTI PEMAIN ---
            currentIndex = (currentIndex + 1) % players.Count;
            yield return new WaitForSeconds(0.5f);
        }
    }

    // Dipanggil otomatis saat dadu berhenti
    private void HandleDiceResult(int result)
    {
        if (state != TurnState.Rolling) return;

        currentRollValue = result;

        // Matikan input dadu biar ga dilempar lagi
        if (diceInputHandler) diceInputHandler.InputEnabled = false;

        // Pindah ke fase tunggu tombol GO
        state = TurnState.StrategyPhase;
    }

    private IEnumerator MoveSequence()
    {
        state = TurnState.Moving;

        int startTile = currentPlayer.TileID;

        // Hitung target (Dadu + Modifier Kartu kalau ada)
        int moves = currentRollValue + currentPlayer.nextRollModifier;
        currentPlayer.nextRollModifier = 0; // Reset modifier

        int targetTile = Mathf.Max(1, startTile + moves);

        // Cek Batas Board
        int maxTile = (BoardManager.Instance != null) ? BoardManager.Instance.totalTiles : 100;
        if (targetTile > maxTile)
        {
            int overshoot = targetTile - maxTile;
            targetTile = maxTile - overshoot;
        }

        // Panggil Movement System
        if (MovementSystem.Instance != null)
        {
            yield return StartCoroutine(MovementSystem.Instance.MovePlayerToTileCoroutine(currentPlayer, targetTile));
        }
        else
        {
            // Fallback teleport
            currentPlayer.TileID = targetTile;
        }

        // Integrasi Tile Effect System (Wajib ada biar ga error logic, tapi bisa diskip kalau instance null)
        state = TurnState.ResolveTile;
        awaitingTileResolve = true;

        if (TileEffectSystem.Instance != null)
        {
            // Tunggu sampai TileEffectSystem panggil NotifyTileResolveComplete
            // Timeout 2 detik jaga-jaga kalau macet
            float timer = 0;
            while (awaitingTileResolve && timer < 2.0f)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            awaitingTileResolve = false;
        }

        state = TurnState.EndTurn;
    }
}