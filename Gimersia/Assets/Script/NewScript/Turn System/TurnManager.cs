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
        Idle,
        StartTurn,
        StrategyPhase,  // FASE UTAMA: Bebas Lempar Dadu & Pilih Kartu
        Executing,      // Sedang menjalankan Dadu & Kartu (Pawn Jalan)
        Moving,         // Sedang animasi jalan (Sub-state)
        ResolveTile,    // Sedang efek tile (Sub-state)
        EndTurn,
        GameOver
    }

    public TurnState state = TurnState.Idle;

    [Header("References")]
    public DiceController diceController;
    public DiceInputHandler diceInputHandler;

    public List<PlayerState> players = new List<PlayerState>();
    private int currentIndex = 0;
    private PlayerState currentPlayer => (players.Count > 0 && currentIndex >= 0 && currentIndex < players.Count) ? players[currentIndex] : null;

    // Variabel Dadu & Tombol
    private int currentDiceRoll = 0;
    private bool hasRolledDice = false;
    private bool goButtonPressed = false;

    // Variabel Tile
    private bool awaitingTileResolve = false;
    private bool awaitingMovement = false;

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
    // PUBLIC API
    // =======================================================================

    public void StartGame(List<PlayerState> playerStates, int startIndex = 0)
    {
        players = new List<PlayerState>(playerStates);
        currentIndex = Mathf.Clamp(startIndex, 0, players.Count - 1);
        StartCoroutine(RunTurnLoop());
    }

    public void NotifyTileResolveComplete(PlayerState player)
    {
        if (player == currentPlayer) awaitingTileResolve = false;
    }

    // --- FIX ERROR: Tambahkan Fungsi Ini ---
    public void ExecutePendingQueue() => OnExecuteButtonPressed();
    public void OnGoPressed() => OnExecuteButtonPressed();
    // ---------------------------------------

    public void OnExecuteButtonPressed()
    {
        if (state == TurnState.StrategyPhase)
        {
            // Validasi: Harus lempar dadu dulu!
            if (!hasRolledDice)
            {
                Debug.LogWarning("Harap lempar dadu terlebih dahulu sebelum GO!");
                return;
            }

            Debug.Log(">>> TOMBOL GO DITEKAN! MULAI EKSEKUSI! <<<");
            goButtonPressed = true;
        }
    }

    // =======================================================================
    // LOGIKA UTAMA
    // =======================================================================

    private IEnumerator RunTurnLoop()
    {
        while (true)
        {
            if (players.Count == 0) yield break;

            // 1. START TURN
            state = TurnState.StartTurn;
            PlayerState p = currentPlayer;

            EventBus.TurnStarted(p);
            p.ResetTemporaryStatus();
            yield return null;

            // 2. STRATEGY PHASE (Gabungan Dadu & Kartu)
            state = TurnState.StrategyPhase;

            // Reset
            currentDiceRoll = 0;
            hasRolledDice = false;
            goButtonPressed = false;

            // Aktifkan Dadu & UI Modifier
            if (diceController != null) diceController.ResetState();
            if (diceInputHandler) diceInputHandler.InputEnabled = true;

            if (UIController.Instance != null)
            {
                UIController.Instance.ShowModifierPanel();
                UIController.Instance.UpdateDiceText(0);
            }

            Debug.Log($"[Turn] Giliran {p.name}. Silakan Lempar Dadu & Pilih Kartu.");

            // TUNGGU TOMBOL GO
            while (!goButtonPressed)
            {
                yield return null;
            }

            // 3. EKSEKUSI BERURUTAN
            state = TurnState.Executing;

            if (diceInputHandler) diceInputHandler.InputEnabled = false;
            if (UIController.Instance != null) UIController.Instance.HideModifierPanel();

            yield return StartCoroutine(ExecuteTurnSequence());

            // 4. SELESAI
            state = TurnState.EndTurn;
            EventBus.TurnEnded(p);

            AdvanceToNextActivePlayer();
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void HandleDiceResult(int result)
    {
        if (state != TurnState.StrategyPhase) return;

        currentDiceRoll = result;
        hasRolledDice = true;

        if (UIController.Instance != null) UIController.Instance.UpdateDiceText(result);
    }

    // =======================================================================
    // SEQUENCE (Jalan Dadu -> Jalan Kartu)
    // =======================================================================

    private IEnumerator ExecuteTurnSequence()
    {
        // A. Jalan Dadu
        Debug.Log($"[Sequence] 1. Jalan Dadu: {currentDiceRoll}");

        int startTile = currentPlayer.TileID;
        int targetWithDice = startTile + currentDiceRoll;

        yield return StartCoroutine(MovePawnTo(currentPlayer, targetWithDice));
        yield return new WaitForSeconds(0.5f);

        // B. Jalan Kartu Modifier
        List<NewCardData> pendingCards = currentPlayer.GetAndClearPending();

        if (pendingCards.Count > 0)
        {
            Debug.Log($"[Sequence] 2. Menjalankan {pendingCards.Count} Kartu.");

            foreach (NewCardData card in pendingCards)
            {
                // 1. Logic
                card.Play(currentPlayer);
                currentPlayer.ConsumeSelectedCard(card);

                // 2. Tunggu Pawn jika gerak
                yield return null;
                if (currentPlayer.pawn != null && currentPlayer.pawn.IsMoving)
                {
                    float timeOut = 3f;
                    while (currentPlayer.pawn.IsMoving && timeOut > 0)
                    {
                        timeOut -= Time.deltaTime;
                        yield return null;
                    }
                    // Cek efek tile setelah kartu jalan
                    yield return StartCoroutine(ResolveTileEffect(currentPlayer));
                }

                yield return new WaitForSeconds(0.5f);
            }
        }

        Debug.Log("[Sequence] Selesai.");
    }

    private IEnumerator MovePawnTo(PlayerState player, int targetTile)
    {
        // 1. Validasi Board
        int maxTile = (BoardManager.Instance != null) ? BoardManager.Instance.totalTiles : 100;
        if (targetTile < 1) targetTile = 1;
        if (targetTile > maxTile)
        {
            int overshoot = targetTile - maxTile;
            targetTile = maxTile - overshoot;
        }

        // 2. Gerak Pawn
        state = TurnState.Moving;

        if (MovementSystem.Instance != null)
        {
            // Tunggu animasi jalan selesai
            yield return StartCoroutine(MovementSystem.Instance.MovePlayerToTileCoroutine(player, targetTile));
        }
        else
        {
            player.TileID = targetTile;
        }

        // --- PERBAIKAN DISINI: PAKSA TRIGGER EVENT TILE ---
        // Kita tidak menunggu Event dari script lain. Kita panggil manual di sini.

        var bm = BoardManager.Instance;
        Tiles landedTile = (bm != null) ? bm.GetTileByID(player.TileID) : null;

        Debug.Log($"[TurnManager] Pawn Stop di Tile {player.TileID}. Memanggil Efek Tile...");

        // Panggil Event secara manual
        EventBus.TileLanded(player, landedTile);
        // --------------------------------------------------

        // 3. Tunggu Efek Selesai
        yield return StartCoroutine(ResolveTileEffect(player));
    }
    private IEnumerator ResolveTileEffect(PlayerState player)
    {
        state = TurnState.ResolveTile;
        awaitingTileResolve = true;

        if (TileEffectSystem.Instance != null)
        {
            float timer = 0;
            while (awaitingTileResolve && timer < 3.0f)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            awaitingTileResolve = false;
        }
    }

    private void HandleMovementFinished(PlayerState player, int tileID)
    {
        if (player == currentPlayer)
        {
            var bm = BoardManager.Instance;
            EventBus.TileLanded(player, bm != null ? bm.GetTileByID(tileID) : null);
        }
    }
    private void HandlePlayerDied(PlayerState player) { if (player == currentPlayer) state = TurnState.EndTurn; }
    private void AdvanceToNextActivePlayer()
    {
        if (players.Count == 0) return;
        currentIndex = (currentIndex + 1) % players.Count;
    }
}