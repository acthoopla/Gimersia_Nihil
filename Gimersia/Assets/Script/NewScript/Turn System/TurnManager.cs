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

    private int currentRollValue = 0;
    private bool goButtonPressed = false;
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

    // --- PUBLIC API ---
    public void StartGame(List<PlayerState> playerStates, int startIndex = 0)
    {
        players = new List<PlayerState>(playerStates);
        currentIndex = Mathf.Clamp(startIndex, 0, players.Count - 1);
        StartCoroutine(RunTurnLoop());
    }

    public void NotifyTileResolveComplete(PlayerState player)
    {
        if (state == TurnState.ResolveTile && player == currentPlayer)
            awaitingTileResolve = false;
    }

    public void OnExecuteButtonPressed() => OnGoPressed();

    public void OnGoPressed()
    {
        if (state == TurnState.StrategyPhase) goButtonPressed = true;
    }

    // --- LOOP ---
    private IEnumerator RunTurnLoop()
    {
        while (true)
        {
            if (players.Count == 0) yield break;

            // 1. Start Turn
            state = TurnState.Rolling;
            goButtonPressed = false;
            currentRollValue = 0;
            if (diceController != null) diceController.ResetState();

            // ==========================================
            // TAMBAHKAN KODE INI (JANGAN LEWATKAN)
            // ==========================================
            Debug.Log($"[TurnManager] Triggering TurnStarted untuk {currentPlayer.name}");

            // Panggil lewat EventBus helper yang sudah kamu buat
            EventBus.TurnStarted(currentPlayer);
            // ==========================================

            Debug.Log($"Giliran {currentPlayer.name}. Silakan Lempar Dadu.");
            if (diceInputHandler) diceInputHandler.InputEnabled = true;

            // 2. Tunggu Dadu
            while (state == TurnState.Rolling) yield return null;

            // 3. Tunggu Go
            Debug.Log($"Dadu: {currentRollValue}. Tekan GO untuk jalan.");
            while (!goButtonPressed) yield return null;

            // 4. Jalan & Efek
            yield return StartCoroutine(MoveSequence());

            // 5. Next Player
            currentIndex = (currentIndex + 1) % players.Count;
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void HandleDiceResult(int result)
    {
        if (state != TurnState.Rolling) return;
        currentRollValue = result;
        if (diceInputHandler) diceInputHandler.InputEnabled = false;
        state = TurnState.StrategyPhase;
    }

    private IEnumerator MoveSequence()
    {
        state = TurnState.Moving;

        int startTile = currentPlayer.TileID;
        int moves = currentRollValue + currentPlayer.nextRollModifier;
        currentPlayer.nextRollModifier = 0;

        int targetTile = Mathf.Max(1, startTile + moves);
        int maxTile = (BoardManager.Instance != null) ? BoardManager.Instance.totalTiles : 100;

        if (targetTile > maxTile) targetTile = maxTile - (targetTile - maxTile);
        if (targetTile < 1) targetTile = 1;

        // --- PERGERAKAN ---
        if (MovementSystem.Instance != null)
        {
            // Tunggu animasi jalan selesai
            yield return StartCoroutine(MovementSystem.Instance.MovePlayerToTileCoroutine(currentPlayer, targetTile));
        }
        else
        {
            currentPlayer.TileID = targetTile; // Teleport instant jika gak ada movement system
        }

        // --- PAKSA TRIGGER TILE EFFECT DISINI (FIX ERROR LOG KOSONG) ---
        // Kita tidak lagi menunggu EventBus dari MovementSystem yang sering macet.
        // Kita panggil langsung karena kita tahu pergerakan sudah selesai di baris atas.

        state = TurnState.ResolveTile;

        var bm = BoardManager.Instance;
        // Ambil tile data dari posisi terakhir player
        Tiles landedTile = (bm != null) ? bm.GetTileByID(currentPlayer.TileID) : null;

        Debug.Log($"[TurnManager] Force Trigger Tile Landed pada ID: {currentPlayer.TileID}");
        EventBus.TileLanded(currentPlayer, landedTile);

        // --- TUNGGU EFEK SELESAI ---
        awaitingTileResolve = true;
        if (TileEffectSystem.Instance != null)
        {
            float timer = 0;
            // Tunggu sampai NotifyTileResolveComplete dipanggil atau timeout 3 detik
            while (awaitingTileResolve && timer < 3.0f)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }

        state = TurnState.EndTurn;
    }
}