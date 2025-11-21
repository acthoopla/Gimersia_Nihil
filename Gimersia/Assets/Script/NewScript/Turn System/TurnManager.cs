using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurnManager (SRP)
/// - Mengelola state machine giliran (turn) pemain
/// - Berinteraksi via EventBus dan public API (tidak menggerakkan pawn langsung)
/// - Menjaga batas penggunaan kartu per turn
/// - Menunggu movement animation selesai (EventBus.MovementFinished)
/// - Menunggu tile effect selesai (TileEffectSystem harus memanggil NotifyTileResolveComplete)
/// 
/// Integrasi:
/// - DiceManager memanggil TurnManager.OnDiceResult(playerState, roll)
/// - MovementSystem harus listen ke TurnManager.MoveRequest atau kamu bisa memanggil MovementSystem.MovePlayer(...)
/// - TileEffectSystem harus listen ke EventBus.OnMovementFinished dan setelah selesai memanggil
///   TurnManager.Instance.NotifyTileResolveComplete(playerState);
/// </summary>
[DisallowMultipleComponent]
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public enum TurnState
    {
        Idle,
        StartTurn,
        PreRoll,
        WaitingForRoll, // dice physics or dice manager
        PreMovePlay,    // optional: play movement cards
        CalculatingMove,
        Moving,         // waiting movement animation complete (EventBus)
        ResolveTile,    // waiting tile effect resolution (TileEffectSystem calls back)
        PostMovePlay,
        EndTurn,
        GameOver
    }

    [Header("Turn Settings")]
    public int maxCardsPerTurn = 3;

    // runtime
    public List<PlayerState> players = new List<PlayerState>();
    private int currentIndex = 0;
    private TurnState state = TurnState.Idle;
    private int cardsPlayedThisTurn = 0;
    private PlayerState currentPlayer => (players.Count > 0 && currentIndex >= 0 && currentIndex < players.Count) ? players[currentIndex] : null;

    // movement bookkeeping
    private bool awaitingTileResolve = false;
    private bool awaitingMovementFinishForPlayer = false;
    private int lastRequestedTargetTile = -1;

    // callbacks & timeouts
    public float preMoveTimeout = 10f;   // optional: auto-skip pre-move if player idle
    public float postMoveTimeout = 20f;  // optional: auto-end if player idle

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnEnable()
    {
        EventBus.OnMovementFinished += HandleMovementFinished;
        EventBus.OnPlayerDied += HandlePlayerDied;
    }

    void OnDisable()
    {
        EventBus.OnMovementFinished -= HandleMovementFinished;
        EventBus.OnPlayerDied -= HandlePlayerDied;
    }

    #region Public API
    /// <summary>
    /// Initialize players list and start the first turn.
    /// PlayerState objects should already exist on player prefabs.
    /// </summary>
    public void StartGame(List<PlayerState> playerStates, int startIndex = 0)
    {
        if (playerStates == null || playerStates.Count == 0)
        {
            Debug.LogError("[TurnManager] StartGame requires at least 1 player.");
            return;
        }

        players = new List<PlayerState>(playerStates);
        currentIndex = Mathf.Clamp(startIndex, 0, players.Count - 1);
        cardsPlayedThisTurn = 0;
        state = TurnState.StartTurn;
        StartCoroutine(RunTurnLoop());
    }

    /// <summary>
    /// DiceManager harus memanggil ini ketika hasil roll sudah tersedia untuk current player.
    /// </summary>
    public void OnDiceResult(PlayerState player, int totalRoll)
    {
        if (state != TurnState.WaitingForRoll)
        {
            Debug.LogWarning($"[TurnManager] OnDiceResult called in state {state}. Ignoring.");
            return;
        }
        if (player != currentPlayer)
        {
            Debug.LogWarning("[TurnManager] OnDiceResult called for non-current player. Ignoring.");
            return;
        }

        StartCoroutine(HandleRollAndMove(player, totalRoll));
    }

    /// <summary>
    /// Dipanggil oleh CardSystem / UI saat player memainkan kartu.
    /// TurnManager hanya track jumlah kartu yang dimainkan agar tidak melebihi maxCardsPerTurn.
    /// CardSystem tetap eksekusi efek kartu sendiri.
    /// </summary>
    public bool TryConsumeCardPlaySlot(PlayerState player)
    {
        if (player != currentPlayer) return false;
        if (cardsPlayedThisTurn >= maxCardsPerTurn) return false;
        cardsPlayedThisTurn++;
        return true;
    }

    /// <summary>
    /// Dipanggil TileEffectSystem ketika semua efek di tile telah selesai dieksekusi.
    /// Ini memberitahu TurnManager untuk lanjut ke PostMovePlay.
    /// </summary>
    public void NotifyTileResolveComplete(PlayerState player)
    {
        if (player != currentPlayer)
        {
            Debug.LogWarning("[TurnManager] NotifyTileResolveComplete for non-current player ignored.");
            return;
        }

        if (state == TurnState.ResolveTile && awaitingTileResolve)
        {
            awaitingTileResolve = false;
            // allow state machine coroutine to continue
        }
        else
        {
            // mungkin tile tidak punya efek, atau sudah ditangani
            awaitingTileResolve = false;
        }
    }

    #endregion

    #region Turn Loop
    private IEnumerator RunTurnLoop()
    {
        while (true)
        {
            if (players == null || players.Count == 0)
            {
                state = TurnState.Idle;
                yield break;
            }

            if (IsGameOver())
            {
                state = TurnState.GameOver;
                Debug.Log("[TurnManager] Game over!");
                yield break;
            }

            // START TURN
            state = TurnState.StartTurn;
            cardsPlayedThisTurn = 0;
            PlayerState p = currentPlayer;
            EventBus.TurnStarted(p);
            // give a frame so subscribers can react (UI)
            yield return null;

            // PRE-ROLL
            state = TurnState.PreRoll;
            // notify UI to enable roll button via EventBus or direct UI binding
            // we wait here until OnDiceResult is called by DiceManager / UI
            state = TurnState.WaitingForRoll;
            // Wait until OnDiceResult triggers coroutine; we just wait until that finishes
            // We'll block here by yielding until the roll coroutine completes (handled in HandleRollAndMove)
            while (state == TurnState.WaitingForRoll)
            {
                yield return null;
            }

            // After roll+move+resolve+postmove, we will be in EndTurn state; proceed to next player
            // Wait until EndTurn is reached
            while (state != TurnState.EndTurn && state != TurnState.GameOver)
            {
                yield return null;
            }

            // EndTurn cleanup and advance turn index
            EventBus.TurnEnded(currentPlayer);

            if (state == TurnState.GameOver) yield break;

            AdvanceToNextActivePlayer();

            // small delay between turns for polish
            yield return new WaitForSeconds(0.2f);
        }
    }

    private IEnumerator HandleRollAndMove(PlayerState player, int roll)
    {
        // Ensure we are in the right state
        if (state != TurnState.WaitingForRoll)
        {
            yield break;
        }

        // Apply any nextRollModifier that player may have (set by card)
        int effectiveRoll = roll + player.nextRollModifier;
        player.nextRollModifier = 0;

        // clamp minimum 1
        effectiveRoll = Mathf.Max(1, effectiveRoll);

        // PRE-MOVE play: allow player to play movement-type cards BEFORE moving
        state = TurnState.PreMovePlay;
        // In basic mode we allow immediate skip; in extended mode UI may let player play movement cards
        float startPre = Time.time;
        bool preMoveDone = false;
        // We'll give UI a chance to play PreMove cards; external UI should call TryConsumeCardPlaySlot when playing
        while (!preMoveDone && Time.time - startPre < preMoveTimeout)
        {
            // If external system signals pre-move done (via UI button) it should call ContinueAfterPreMove()
            // For now we'll proceed immediately after small delay to avoid blocking
            preMoveDone = true;
            yield return null;
        }

        // CALCULATE MOVEMENT
        state = TurnState.CalculatingMove;
        int startTile = player.TileID;
        int targetTile = startTile + effectiveRoll;

        // handle overshoot: bounce back rule
        if (targetTile == BoardDefaultTotal()) // reaching exact finish -> lose (per GDD)
        {
            // Move to finish tile - MovementSystem should animate
            state = TurnState.Moving;
            lastRequestedTargetTile = targetTile;
            awaitingMovementFinishForPlayer = true;
            // Notify or call MovementSystem to move player (MovementSystem should call EventBus.MovementFinished when done)
            // MovementSystem may be accessed via instance or via EventBus; here we just publish a request event via EventBus if you choose
            // Example: MovementSystem.Instance.RequestMove(player, targetTile);
            // For decoupling, the project can implement a MovementRequest event; for now, try calling MovementSystem directly if available.
            if (MovementSystem.Instance != null)
                StartCoroutine(MovementSystem.Instance.MovePlayerToTileCoroutine(player, targetTile));
            else
                Debug.LogWarning("[TurnManager] MovementSystem.Instance not found; make sure MovementSystem exists and subscribes to move requests.");

            // wait for movement finish (EventBus.OnMovementFinished will set awaitingMovementFinishForPlayer to false)
            while (awaitingMovementFinishForPlayer)
                yield return null;

            // After movement, tile resolve will be triggered by TileEffectSystem (subscribed to EventBus.OnMovementFinished)
            // Now wait for tile resolve to complete (TileEffectSystem must call NotifyTileResolveComplete)
            state = TurnState.ResolveTile;
            awaitingTileResolve = true;
            // Wait until TileEffectSystem calls NotifyTileResolveComplete
            while (awaitingTileResolve)
                yield return null;

            // Post-move
            state = TurnState.PostMovePlay;
            // Allow player to play up to remaining card slots for this turn
            float startPost = Time.time;
            bool postDone = false;
            while (!postDone && Time.time - startPost < postMoveTimeout)
            {
                // For now we proceed immediately; UI should provide an "End Turn" button which triggers ContinueAfterPostMove()
                postDone = true;
                yield return null;
            }

            // End turn
            state = TurnState.EndTurn;
            yield break;
        }
        else if (targetTile > BoardDefaultTotal())
        {
            // bounce logic
            int overshoot = targetTile - BoardDefaultTotal();
            int bounceTarget = BoardDefaultTotal() - overshoot;

            // Move to finish tile then bounce back
            state = TurnState.Moving;
            awaitingMovementFinishForPlayer = true;
            if (MovementSystem.Instance != null)
                StartCoroutine(MovementSystem.Instance.MovePlayerToTileCoroutine(player, BoardDefaultTotal()));
            else
                Debug.LogWarning("[TurnManager] MovementSystem.Instance not found (bounce step).");
            while (awaitingMovementFinishForPlayer) yield return null;

            yield return new WaitForSeconds(0.2f);

            awaitingMovementFinishForPlayer = true;
            if (MovementSystem.Instance != null)
                StartCoroutine(MovementSystem.Instance.MovePlayerToTileCoroutine(player, bounceTarget));
            while (awaitingMovementFinishForPlayer) yield return null;

            // Wait for tile resolve and continue (same as above)
            state = TurnState.ResolveTile;
            awaitingTileResolve = true;
            while (awaitingTileResolve) yield return null;

            state = TurnState.PostMovePlay;
            yield return null;
            state = TurnState.EndTurn;
            yield break;
        }
        else
        {
            // Normal move
            state = TurnState.Moving;
            awaitingMovementFinishForPlayer = true;
            lastRequestedTargetTile = targetTile;
            if (MovementSystem.Instance != null)
                StartCoroutine(MovementSystem.Instance.MovePlayerToTileCoroutine(player, targetTile));
            else
                Debug.LogWarning("[TurnManager] MovementSystem.Instance not found; cannot move player.");

            while (awaitingMovementFinishForPlayer)
                yield return null;

            // Wait tile resolve
            state = TurnState.ResolveTile;
            awaitingTileResolve = true;
            while (awaitingTileResolve)
                yield return null;

            // Post-move
            state = TurnState.PostMovePlay;
            // for now auto-proceed
            yield return null;
            state = TurnState.EndTurn;
            yield break;
        }
    }

    #endregion

    #region Movement / Tile callbacks
    // MovementSystem or EventBus should call EventBus.MovementFinished(player, tileID)
    private void HandleMovementFinished(PlayerState player, int tileID)
    {
        // Only respond if this is current player
        if (player != currentPlayer)
        {
            Debug.Log("[TurnManager] MovementFinished for non-current player ignored.");
            return;
        }

        // clear waiting flag
        awaitingMovementFinishForPlayer = false;

        // Let other systems know player landed (TileEffectSystem should be subscribed to this EventBus event)
        EventBus.TileLanded(player, BoardManagerInstance()?.GetTileByID(tileID));
    }

    private void HandlePlayerDied(PlayerState player)
    {
        if (player == currentPlayer)
        {
            // When current player died mid-turn, end the turn immediately
            Debug.Log($"[TurnManager] Current player {player.gameObject.name} died. Ending their turn.");
            // set state so the RunTurnLoop moves on
            state = TurnState.EndTurn;
        }
        else
        {
            // If some other player died, remove from players list gracefully (optional)
            // We won't remove here to avoid modifying collection while enumerating; leave game flow to cleanup elsewhere
            Debug.Log($"[TurnManager] Player died: {player.gameObject.name}");
        }
    }
    #endregion

    #region Helpers
    private int BoardDefaultTotal()
    {
        // default 100, but if you want dynamic read from BoardManager
        var bm = BoardManagerInstance();
        return (bm != null) ? bm.totalTiles : 100;
    }

    private BoardManager BoardManagerInstance()
    {
        return (BoardManager.Instance != null) ? BoardManager.Instance : FindObjectOfType<BoardManager>();
    }

    private MovementSystem MovementSystemInstance()
    {
        return (MovementSystem.Instance != null) ? MovementSystem.Instance : FindObjectOfType<MovementSystem>();
    }

    private bool IsGameOver()
    {
        if (players == null || players.Count == 0) return true;
        int active = 0;
        foreach (var p in players) if (!p.IsDead) active++;
        return (active <= 1);
    }

    private void AdvanceToNextActivePlayer()
    {
        if (players == null || players.Count == 0) return;

        // advance index circularly; skip dead/winner players if TurnManager doesn't want them
        int attempts = 0;
        do
        {
            currentIndex = (currentIndex + 1) % players.Count;
            attempts++;
            if (attempts > players.Count + 5) break;
        }
        while (players[currentIndex].IsDead); // skip dead players

        // Reset per-turn flags if cycle restarted externally (TurnManager will call ResetTemporaryStatus via PlayerState if needed)
    }
    #endregion

    #region Debug / Editor Helpers
    [ContextMenu("PrintTurnStatus")]
    void PrintTurnStatus()
    {
        Debug.Log($"[TurnManager] State: {state} Current: {(currentPlayer != null ? currentPlayer.gameObject.name : "-")} Index: {currentIndex} CardsUsed: {cardsPlayedThisTurn}");
    }
    #endregion
}
