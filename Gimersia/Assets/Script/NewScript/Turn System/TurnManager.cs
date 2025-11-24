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
        PreMovePlay,    // Memainkan kartu sebelum roll/move
        PreRoll,
        WaitingForRoll,
        CalculatingMove,
        Moving,         // Menunggu animasi jalan selesai
        ResolveTile,    // Menunggu efek tile selesai
        EndTurn,
        GameOver
    }

    [Header("Turn Settings")]
    public float delayBetweenTurns = 0.5f;

    // Runtime
    public List<PlayerState> players = new List<PlayerState>();
    private int currentIndex = 0;
    private TurnState state = TurnState.Idle;

    // Helper property untuk mengambil player yang sedang jalan
    private PlayerState currentPlayer => (players.Count > 0 && currentIndex >= 0 && currentIndex < players.Count) ? players[currentIndex] : null;

    // Bookkeeping flags
    private bool awaitingTileResolve = false;
    private bool awaitingMovementFinishForPlayer = false;
    private int lastRollValue = 0;
    private bool hasExecutedCards = false;

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
    public void StartGame(List<PlayerState> playerStates, int startIndex = 0)
    {
        if (playerStates == null || playerStates.Count == 0)
        {
            Debug.LogError("[TurnManager] StartGame butuh minimal 1 player.");
            return;
        }

        players = new List<PlayerState>(playerStates);
        currentIndex = Mathf.Clamp(startIndex, 0, players.Count - 1);

        StartCoroutine(RunTurnLoop());
    }

    // Dipanggil oleh DiceManager saat dadu selesai dilempar
    public void OnDiceResult(PlayerState player, int totalRoll)
    {
        if (state != TurnState.WaitingForRoll || player != currentPlayer) return;

        lastRollValue = Mathf.Max(1, totalRoll);
        StartCoroutine(HandleRollAndMove(player, lastRollValue));
    }

    // Dipanggil oleh TileEffectSystem saat efek tile selesai
    public void NotifyTileResolveComplete(PlayerState player)
    {
        if (player == currentPlayer)
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
            if (players.Count == 0 || IsGameOver()) { state = TurnState.GameOver; yield break; }

            // 1. START TURN
            state = TurnState.StartTurn;
            PlayerState p = currentPlayer;
            EventBus.TurnStarted(p);
            p.ResetTemporaryStatus();
            yield return null;

            // 2. FASE SUSUN KARTU (PreMovePlay)
            state = TurnState.PreMovePlay;
            hasExecutedCards = false;

            Debug.Log($"[Turn] Giliran {p.name}. Silakan susun kartu & tekan EXECUTE.");

            // TUNGGU SAMPAI PLAYER TEKAN TOMBOL EXECUTE
            // UI Button harus memanggil: TurnManager.Instance.OnExecuteButtonPressed()
            while (!hasExecutedCards)
            {
                yield return null;
            }

            // 3. ROLL DADU (Otomatis setelah kartu selesai)
            state = TurnState.WaitingForRoll;

            if (DiceManager.Instance != null)
            {
                DiceManager.Instance.RequestRollForPlayer(p);
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
                OnDiceResult(p, UnityEngine.Random.Range(1, 7));
            }

            // Tunggu move selesai
            while (state != TurnState.EndTurn && state != TurnState.GameOver) yield return null;

            // 4. END TURN
            EventBus.TurnEnded(p);
            AdvanceToNextActivePlayer();
            yield return new WaitForSeconds(delayBetweenTurns);
        }
    }
    public void OnExecuteButtonPressed()
    {
        if (state == TurnState.PreMovePlay && !hasExecutedCards)
        {
            StartCoroutine(ExecuteCardSequence(currentPlayer));
        }
        else
        {
            Debug.LogWarning("Belum giliran main kartu / Sudah dieksekusi!");
        }
    }

    private IEnumerator ExecuteCardSequence(PlayerState player)
    {
        // Jika ada kartu di slot, jalankan satu per satu
        if (player.selectedCards.Count > 0)
        {
            Debug.Log($"[TurnManager] Mengeksekusi {player.selectedCards.Count} kartu...");

            // Copy list agar aman
            List<NewCardData> sequence = new List<NewCardData>(player.selectedCards);

            foreach (NewCardData card in sequence)
            {
                Debug.Log($"[Sequence] Menggunakan: {card.cardName}");

                // 1. Play Effect
                card.Play(player);

                // 2. Hapus dari Slot
                player.ConsumeSelectedCard(card);

                // 3. Jeda Animasi (Biar kelihatan urutannya)
                yield return new WaitForSeconds(0.8f);
            }
        }

        Debug.Log("[TurnManager] Fase Kartu Selesai. Lanjut Roll Dadu.");
        hasExecutedCards = true; // Ini akan memutus loop 'while' di RunTurnLoop
    }
    private IEnumerator HandleRollAndMove(PlayerState player, int roll)
    {
        // Hitung modifier (misal dari Tile Nega sebelumnya)
        int effectiveRoll = roll + player.nextRollModifier;
        player.nextRollModifier = 0; // Reset modifier setelah dipakai
        effectiveRoll = Mathf.Max(1, effectiveRoll); // Minimal jalan 1 langkah

        // (Logic PreMove Card DIHAPUS sesuai request)

        // --- MOVE LOGIC ---
        state = TurnState.CalculatingMove;
        int startTile = player.TileID;
        int targetTile = startTile + effectiveRoll;

        // Cek Max Board (Bounce Logic / Stop at Max)
        int maxTile = (BoardManager.Instance != null) ? BoardManager.Instance.totalTiles : 100;

        // Logika Bounce (Memantul jika lebih)
        if (targetTile > maxTile)
        {
            int overshoot = targetTile - maxTile;
            targetTile = maxTile - overshoot;
        }

        state = TurnState.Moving;
        awaitingMovementFinishForPlayer = true;

        if (MovementSystem.Instance != null)
        {
            StartCoroutine(MovementSystem.Instance.MovePlayerToTileCoroutine(player, targetTile));
        }
        else
        {
            // Fallback teleport
            player.TileID = targetTile;
            awaitingMovementFinishForPlayer = false;
        }

        // Tunggu animasi jalan selesai
        while (awaitingMovementFinishForPlayer) yield return null;

        // --- TILE RESOLVE ---
        state = TurnState.ResolveTile;
        awaitingTileResolve = true;

        // Jika tidak ada TileEffectSystem, langsung skip
        if (TileEffectSystem.Instance == null)
        {
            awaitingTileResolve = false;
        }

        // Tunggu TileEffectSystem memanggil NotifyTileResolveComplete
        while (awaitingTileResolve) yield return null;

        // (Logic PostMove Card DIHAPUS sesuai request)

        state = TurnState.EndTurn;
    }
    #endregion

    #region Handlers & Helpers
    private void HandleMovementFinished(PlayerState player, int tileID)
    {
        if (player == currentPlayer)
        {
            awaitingMovementFinishForPlayer = false;

            // Trigger Tile Landed event
            var bm = (BoardManager.Instance != null) ? BoardManager.Instance : FindObjectOfType<BoardManager>();
            Tiles tile = (bm != null) ? bm.GetTileByID(tileID) : null;

            EventBus.TileLanded(player, tile);
        }
    }

    private void HandlePlayerDied(PlayerState player)
    {
        if (player == currentPlayer)
        {
            state = TurnState.EndTurn;
        }
    }

    private bool IsGameOver()
    {
        if (players == null || players.Count == 0) return true;
        int activeCount = 0;
        foreach (var p in players)
        {
            if (!p.IsDead) activeCount++;
        }
        // Game Over jika sisa 1 pemain atau kurang (sesuaikan rule kamu)
        return activeCount <= 1 && players.Count > 1;
    }

    private void AdvanceToNextActivePlayer()
    {
        if (players == null || players.Count == 0) return;
        int attempts = 0;
        do
        {
            currentIndex = (currentIndex + 1) % players.Count;
            attempts++;
        }
        while (players[currentIndex].IsDead && attempts < players.Count);
    }
    #endregion
}