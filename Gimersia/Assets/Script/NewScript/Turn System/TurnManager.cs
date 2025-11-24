using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public enum TurnState
    {
        Idle, StartTurn, PreMovePlay, WaitingForRoll,
        CalculatingMove, Moving, ResolveTile, EndTurn, GameOver
    }

    [Header("References")]
    // KITA BUTUH INI KARENA DICEMANAGER SUDAH DIHAPUS
    public DiceController diceController;
    public DiceInputHandler diceInputHandler;

    [Header("Turn Settings")]
    public float delayBetweenTurns = 0.5f;

    public List<PlayerState> players = new List<PlayerState>();
    private int currentIndex = 0;
    public TurnState state = TurnState.Idle; // Public biar bisa dipantau di Inspector

    private PlayerState currentPlayer => (players.Count > 0) ? players[currentIndex] : null;

    // Flags
    private bool awaitingTileResolve = false;
    private bool awaitingMovementFinishForPlayer = false;
    private bool hasFinishedCardPhase = false; // Renamed for clarity

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Cari referensi dadu otomatis jika belum diassign
        if (diceController == null) diceController = FindObjectOfType<DiceController>();
        if (diceInputHandler == null) diceInputHandler = FindObjectOfType<DiceInputHandler>();

        // Subscribe event Dadu
        if (diceController != null)
        {
            diceController.OnDiceResult += HandleDiceResult;
        }
    }

    void OnDestroy()
    {
        if (diceController != null) diceController.OnDiceResult -= HandleDiceResult;
        EventBus.OnMovementFinished -= HandleMovementFinished;
        EventBus.OnPlayerDied -= HandlePlayerDied;
    }

    void OnEnable()
    {
        EventBus.OnMovementFinished += HandleMovementFinished;
        EventBus.OnPlayerDied += HandlePlayerDied;
    }

    #region Public API
    public void StartGame(List<PlayerState> playerStates, int startIndex = 0)
    {
        if (playerStates == null || playerStates.Count == 0) return;
        players = new List<PlayerState>(playerStates);
        currentIndex = Mathf.Clamp(startIndex, 0, players.Count - 1);
        StartCoroutine(RunTurnLoop());
    }

    // Dipanggil oleh TileEffectSystem
    public void NotifyTileResolveComplete(PlayerState player)
    {
        if (player == currentPlayer) awaitingTileResolve = false;
    }

    // BUTTON EVENT: Player menekan tombol "Execute Cards"
    public void OnExecuteButtonPressed()
    {
        if (state == TurnState.PreMovePlay && !hasFinishedCardPhase)
        {
            StartCoroutine(ExecuteCardSequence(currentPlayer));
        }
    }

    // BUTTON EVENT: Player menekan tombol "Skip / Roll Dice" (PENTING!)
    public void OnSkipCardPhasePressed()
    {
        if (state == TurnState.PreMovePlay && !hasFinishedCardPhase)
        {
            Debug.Log("[TurnManager] Player memilih skip fase kartu.");
            hasFinishedCardPhase = true; // Langsung lanjut
        }
    }
    #endregion

    #region Turn Loop
    private IEnumerator RunTurnLoop()
    {
        while (true)
        {
            if (players.Count == 0 || IsGameOver()) { state = TurnState.GameOver; yield break; }

            // 1. START TURN
            state = TurnState.StartTurn;
            PlayerState p = currentPlayer;
            EventBus.TurnStarted(p);
            p.ResetTemporaryStatus();
            yield return null;

            // 2. FASE KARTU
            state = TurnState.PreMovePlay;
            hasFinishedCardPhase = false;

            Debug.Log($"[Turn] Giliran {p.name}. Susun kartu lalu Execute, atau tekan Skip.");

            // TUNGGU SAMPAI: Player Execute Kartu ATAU Player tekan Skip
            while (!hasFinishedCardPhase)
            {
                yield return null;
            }

            // 3. FASE DADU
            state = TurnState.WaitingForRoll;

            // Aktifkan Input Dadu
            if (diceInputHandler != null)
            {
                diceInputHandler.InputEnabled = true;
                // Opsional: Tampilkan UI "Silakan Lempar Dadu"
            }
            else
            {
                // Fallback jika tidak ada sistem dadu (Debug)
                yield return new WaitForSeconds(1f);
                HandleDiceResult(UnityEngine.Random.Range(1, 7));
            }

            // Tunggu sampai state berubah (diubah oleh HandleRollAndMove)
            while (state == TurnState.WaitingForRoll) yield return null;

            // Tunggu animasi jalan & tile resolve selesai
            while (state != TurnState.EndTurn && state != TurnState.GameOver) yield return null;

            // 4. END TURN
            if (diceInputHandler != null) diceInputHandler.InputEnabled = false; // Matikan input dadu
            EventBus.TurnEnded(p);
            AdvanceToNextActivePlayer();
            yield return new WaitForSeconds(delayBetweenTurns);
        }
    }

    private IEnumerator ExecuteCardSequence(PlayerState player)
    {
        if (player.selectedCards.Count > 0)
        {
            List<NewCardData> sequence = new List<NewCardData>(player.selectedCards);
            foreach (NewCardData card in sequence)
            {
                // Disini butuh NewCardData.cs yang valid
                if (card != null) card.Play(player);
                player.ConsumeSelectedCard(card);
                yield return new WaitForSeconds(0.8f);
            }
        }
        hasFinishedCardPhase = true; // Selesai eksekusi, lanjut loop
    }

    // Callback dari Event DiceController.OnDiceResult
    private void HandleDiceResult(int totalRoll)
    {
        // Pastikan hanya merespon jika sedang menunggu roll & giliran player ini
        if (state != TurnState.WaitingForRoll) return;

        Debug.Log($"[TurnManager] Dadu Angka: {totalRoll}");

        // Matikan input dadu agar tidak bisa dilempar lagi saat jalan
        if (diceInputHandler != null) diceInputHandler.InputEnabled = false;

        StartCoroutine(HandleRollAndMove(currentPlayer, totalRoll));
    }

    private IEnumerator HandleRollAndMove(PlayerState player, int roll)
    {
        // Kalkulasi Modifier
        int effectiveRoll = Mathf.Max(1, roll + player.nextRollModifier);
        player.nextRollModifier = 0;

        // --- MOVE ---
        state = TurnState.CalculatingMove;
        int startTile = player.TileID;
        int targetTile = startTile + effectiveRoll;
        int maxTile = (BoardManager.Instance != null) ? BoardManager.Instance.totalTiles : 100;

        // Bounce Logic
        if (targetTile > maxTile)
        {
            int overshoot = targetTile - maxTile;
            targetTile = maxTile - overshoot;
        }

        state = TurnState.Moving;
        awaitingMovementFinishForPlayer = true;

        if (MovementSystem.Instance != null)
            StartCoroutine(MovementSystem.Instance.MovePlayerToTileCoroutine(player, targetTile));
        else
        {
            player.TileID = targetTile;
            awaitingMovementFinishForPlayer = false;
        }

        while (awaitingMovementFinishForPlayer) yield return null;

        // --- TILE RESOLVE ---
        state = TurnState.ResolveTile;
        awaitingTileResolve = true;

        if (TileEffectSystem.Instance == null) awaitingTileResolve = false;

        while (awaitingTileResolve) yield return null;

        state = TurnState.EndTurn;
    }
    #endregion

    #region Helpers
    private void HandleMovementFinished(PlayerState player, int tileID)
    {
        if (player == currentPlayer)
        {
            awaitingMovementFinishForPlayer = false;
            var bm = BoardManager.Instance;
            Tiles tile = (bm != null) ? bm.GetTileByID(tileID) : null;
            EventBus.TileLanded(player, tile);
        }
    }

    private void HandlePlayerDied(PlayerState player)
    {
        if (player == currentPlayer) state = TurnState.EndTurn;
    }

    private bool IsGameOver()
    {
        if (players == null || players.Count < 2) return false; // Single player debugging
        int active = 0;
        foreach (var p in players) if (!p.IsDead) active++;
        return active <= 1; // Last man standing
    }

    private void AdvanceToNextActivePlayer()
    {
        if (players.Count == 0) return;
        int attempts = 0;
        do
        {
            currentIndex = (currentIndex + 1) % players.Count;
            attempts++;
        } while (players[currentIndex].IsDead && attempts < players.Count);
    }
    #endregion
}