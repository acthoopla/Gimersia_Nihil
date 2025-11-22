using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurnManager (merged)
/// - Mengelola state machine giliran (turn) pemain (full featured)
/// - Menambahkan card-draw (3 pilihan) pada StartTurn dan fase penggunaan kartu (pre/post)
/// - Mengintegrasikan dengan DiceManager, MovementSystem, NewCardSystem, CardEffectHandler, CombatSystem
/// - Menunggu movement animation selesai (EventBus.OnMovementFinished)
/// - Menunggu tile effect selesai (TileEffectSystem harus memanggil NotifyTileResolveComplete)
/// 
/// HOOKs (ganti dengan UIManager / animator / subsystems milikmu):
/// - Card selection UI: ShowCardSelection / WaitForCardChoiceCoroutine
/// - Card usage UI: ShowHandAndPickCards / WaitForCardUsageCoroutine
/// - DiceManager.Instance.RequestRollForPlayer(player) harus memanggil OnDiceResult(player, total)
/// - MovementSystem.Instance.MovePlayerToTileCoroutine(player, targetTile) harus memanggil EventBus.MovementFinished when done
/// - TileEffectSystem harus memanggil TurnManager.Instance.NotifyTileResolveComplete(player) when finished
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

    [Header("Card Settings")]
    [Tooltip("Jika true, saat StartTurn pemain diberi opsi mengambil 3 kartu random (pilihan 1 dari 3).")]
    public bool enableStartTurnCardChoice = true;

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

    // dice/roll
    private int lastRollValue = 0;

    // callbacks & timeouts
    public float preMoveTimeout = 10f;   // optional: auto-skip pre-move if player idle
    public float postMoveTimeout = 20f;  // optional: auto-end if player idle

    // Small timings
    public float smallDelay = 0.12f;

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
    /// Initialize players list and start the turn loop.
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
    /// Pastikan DiceManager memanggil TurnManager.Instance.OnDiceResult(player, total)
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

        // store last roll value and allow state machine coroutine to proceed
        lastRollValue = Mathf.Max(1, totalRoll);
        // Move the state forward by changing it from WaitingForRoll; the RunTurnLoop/HandleRollAndMove expects that
        // We'll call the handler coroutine right away.
        StartCoroutine(HandleRollAndMove(player, lastRollValue));
    }

    /// <summary>
    /// Dipanggil oleh CardSystem / UI saat player memainkan kartu.
    /// TurnManager hanya track jumlah kartu yang dimainkan agar tidak melebihi maxCardsPerTurn.
    /// CardSystem tetap eksekusi efek kartu sendiri (kebanyakan via CardEffectHandler).
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
            // state machine coroutine will continue
        }
        else
        {
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

            // Event: Turn started
            EventBus.TurnStarted(currentPlayer);

            // HOOK: UI show turn start
            // UIManager?.SetTurnText($"{p.name} giliran");

            // small frame to let UI update
            yield return null;

            // Optionally give player 3-card choice at start of turn
            if (enableStartTurnCardChoice)
            {
                yield return StartCoroutine(HandleStartTurnCardChoice(p));
            }

            // PRE-ROLL
            state = TurnState.PreRoll;
            // HOOK: enable roll button (UI) or auto roll via DiceManager
            // If using physical dice, DiceManager will enable physical dice, otherwise you can call RequestRollForPlayer

            // REQUEST ROLL
            state = TurnState.WaitingForRoll;
            lastRollValue = 0;

            if (DiceManager.Instance != null)
            {
                // HOOK: show dice UI / animate dice ready
                DiceManager.Instance.RequestRollForPlayer(p);
            }
            else
            {
                // fallback: simulate immediate roll
                yield return new WaitForSeconds(0.15f);
                OnDiceResult(p, UnityEngine.Random.Range(1, 7));
            }

            // Wait — the OnDiceResult will start HandleRollAndMove coroutine and change flow.
            // We just wait here until state reaches EndTurn (HandleRollAndMove will set it)
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

        // HOOK: show UI for pre-move card usage (player can play movement/buff cards)
        // We'll wait for a coroutine that lets UI pick cards and apply them.
        yield return StartCoroutine(HandlePreMoveCardUsage(player, effectiveRoll));

        // CALCULATE MOVEMENT
        state = TurnState.CalculatingMove;
        int startTile = player.TileID;
        int targetTile = startTile + effectiveRoll;

        // handle overshoot: bounce back rule or finish handling
        int boardTotal = BoardDefaultTotal();
        if (targetTile == boardTotal)
        {
            // Move to finish tile - MovementSystem should animate; then player loses (finish)
            state = TurnState.Moving;
            lastRequestedTargetTile = targetTile;
            awaitingMovementFinishForPlayer = true;

            if (MovementSystem.Instance != null)
                StartCoroutine(MovementSystem.Instance.MovePlayerToTileCoroutine(player, targetTile));
            else
                Debug.LogWarning("[TurnManager] MovementSystem.Instance not found; make sure MovementSystem exists and subscribes to move requests.");

            // wait for movement finish (EventBus.OnMovementFinished)
            while (awaitingMovementFinishForPlayer)
                yield return null;

            // After movement, tile resolve will be triggered by TileEffectSystem (subscribed to EventBus.OnMovementFinished)
            // Now wait for tile resolve to complete (TileEffectSystem must call NotifyTileResolveComplete)
            state = TurnState.ResolveTile;
            awaitingTileResolve = true;
            while (awaitingTileResolve)
                yield return null;

            // Post-move
            state = TurnState.PostMovePlay;
            yield return StartCoroutine(HandlePostMoveCardUsage(player));
            state = TurnState.EndTurn;
            yield break;
        }
        else if (targetTile > boardTotal)
        {
            // bounce logic
            int overshoot = targetTile - boardTotal;
            int bounceTarget = boardTotal - overshoot;

            // Move to finish tile then bounce back
            state = TurnState.Moving;
            awaitingMovementFinishForPlayer = true;
            if (MovementSystem.Instance != null)
                StartCoroutine(MovementSystem.Instance.MovePlayerToTileCoroutine(player, boardTotal));
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
            yield return StartCoroutine(HandlePostMoveCardUsage(player));
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
            yield return StartCoroutine(HandlePostMoveCardUsage(player));
            state = TurnState.EndTurn;
            yield break;
        }
    }
    #endregion

    #region Card-related Coroutines (hooks for UI)
    /// <summary>
    /// Start-turn card choice: show 3 random cards and let player pick 1 to add to hand.
    /// Uses NewCardSystem.Instance.GetRandomCardSelection(3) and NewCardSystem.Instance.TryAddCardToPlayer(...)
    /// Replace WaitForCardChoiceSimulation with real UI coroutine.
    /// </summary>
    private IEnumerator HandleStartTurnCardChoice(PlayerState player)
    {
        if (player == null) yield break;
        if (NewCardSystem.Instance == null)
        {
            yield break;
        }

        List<NewCardData> choices = NewCardSystem.Instance.GetRandomCardSelection(3);
        if (choices == null || choices.Count == 0) yield break;

        // HOOK: Show card choice UI and wait for player selection.
        // Replace the simulation below with UI coroutine that yields until user picks.
        NewCardData chosen = null;
        yield return StartCoroutine(WaitForCardChoiceSimulation(choices, (c) => chosen = c));

        if (chosen != null)
        {
            // Give chosen card to player (uses cycle = current turn cycle; using 1 for example)
            NewCardSystem.Instance.TryAddCardToPlayer(player, chosen, /*cycle*/ 1);
            // HOOK: UIManager.DisplayPlayerHand(player)
        }
        yield return null;
    }

    /// <summary>
    /// Pre-move card usage: allow player to play movement-type cards that modify roll or movement.
    /// This coroutine will call CardEffectHandler.ApplyCardEffect for each chosen card in order left->right.
    /// It expects UI to call TurnManager.TryConsumeCardPlaySlot when a card is played (server-side check).
    /// </summary>
    private IEnumerator HandlePreMoveCardUsage(PlayerState player, int effectiveRoll)
    {
        // If player has no cards or Card system missing, early exit
        if (player == null || NewCardSystem.Instance == null || CardEffectHandler.Instance == null)
            yield break;

        // HOOK: Show player's hand UI and allow selecting up to remaining slots
        List<NewCardSystem.PlayerCardInstance> chosen = null;
        yield return StartCoroutine(WaitForCardUsageSimulation(player, maxCardsPerTurn - cardsPlayedThisTurn, (list) => chosen = list));

        if (chosen != null && chosen.Count > 0)
        {
            foreach (var pci in chosen)
            {
                // check slot consumption
                if (!TryConsumeCardPlaySlot(player))
                {
                    Debug.Log("[TurnManager] Card play slot exceeded; skipping additional cards.");
                    break;
                }

                // Execute card effect which may modify effectiveRoll (ref)
                CardEffectHandler.Instance.ApplyCardEffect(player, pci.cardData, ref effectiveRoll);

                // Remove card from player's hand if necessary via NewCardSystem
                NewCardSystem.Instance.TryRemoveCardFromPlayer(player, pci);

                // small buffer for visuals
                yield return new WaitForSeconds(smallDelay);
            }

            // HOOK: UIManager.DisplayPlayerHand(player);
        }
    }

    /// <summary>
    /// Post-move card usage: similar to pre-move, allow usage of remaining cards (post-move)
    /// </summary>
    private IEnumerator HandlePostMoveCardUsage(PlayerState player)
    {
        if (player == null || NewCardSystem.Instance == null || CardEffectHandler.Instance == null)
            yield break;

        List<NewCardSystem.PlayerCardInstance> chosen = null;
        yield return StartCoroutine(WaitForCardUsageSimulation(player, maxCardsPerTurn - cardsPlayedThisTurn, (list) => chosen = list));

        if (chosen != null && chosen.Count > 0)
        {
            foreach (var pci in chosen)
            {
                if (!TryConsumeCardPlaySlot(player))
                {
                    Debug.Log("[TurnManager] Card play slot exceeded; skipping additional cards.");
                    break;
                }
                int dummyRoll = 0;
                CardEffectHandler.Instance.ApplyCardEffect(player, pci.cardData, ref dummyRoll);
                NewCardSystem.Instance.TryRemoveCardFromPlayer(player, pci);
                yield return new WaitForSeconds(smallDelay);
            }
            // HOOK: UIManager.DisplayPlayerHand(player);
        }
    }

    #region Simulated UI coroutines (replace with actual UI)
    // These simulate selection/dismissal so the flow runs without a UI. Replace them with real UI waiting coroutines.

    private IEnumerator WaitForCardChoiceSimulation(List<NewCardData> choices, Action<NewCardData> callback)
    {
        // simulation: auto pick first after delay
        yield return new WaitForSeconds(0.35f);
        NewCardData pick = (choices != null && choices.Count > 0) ? choices[0] : null;
        callback?.Invoke(pick);
        yield break;
    }

    private IEnumerator WaitForCardUsageSimulation(PlayerState player, int maxPick, Action<List<NewCardSystem.PlayerCardInstance>> callback)
    {
        // simulation: by default pick nothing and continue. Replace with actual UI selection.
        yield return new WaitForSeconds(0.15f);
        callback?.Invoke(new List<NewCardSystem.PlayerCardInstance>());
        yield break;
    }

    #endregion
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

        // Let other systems know player landed (TileEffectSystem should be subscribed to EventBus.OnMovementFinished)
        try
        {
            var bm = BoardManagerInstance();
            if (bm != null)
                EventBus.TileLanded(player, bm.GetTileByID(tileID));
            else
                EventBus.TileLanded(player, null);
        }
        catch { }
    }

    private void HandlePlayerDied(PlayerState player)
    {
        if (player == currentPlayer)
        {
            // When current player died mid-turn, end the turn immediately
            Debug.Log($"[TurnManager] Current player {player.gameObject.name} died. Ending their turn.");
            state = TurnState.EndTurn;
        }
        else
        {
            // If some other player died, remove from players list gracefully (optional)
            Debug.Log($"[TurnManager] Player died: {player.gameObject.name}");
        }
    }
    #endregion

    #region Helpers
    private int BoardDefaultTotal()
    {
        var bm = BoardManagerInstance();
        return (bm != null) ? bm.totalTilesInBoard : 100;
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

        int attempts = 0;
        do
        {
            currentIndex = (currentIndex + 1) % players.Count;
            attempts++;
            if (attempts > players.Count + 5) break;
        }
        while (players[currentIndex].IsDead); // skip dead players
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
