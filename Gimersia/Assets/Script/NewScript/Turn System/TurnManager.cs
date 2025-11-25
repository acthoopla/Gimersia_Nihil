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
        StrategyPhase,  // FASE GABUNGAN (Bebas Lempar Dadu & Pilih Kartu)
        Executing,      // Saat GO ditekan (Sedang jalan)
        EndTurn,
        GameOver
    }

    [Header("Turn Settings")]
    public float delayBetweenTurns = 0.5f;

    public List<PlayerState> players = new List<PlayerState>();
    private int currentIndex = 0;
    public TurnState state = TurnState.Idle;

    private PlayerState currentPlayer => (players.Count > 0) ? players[currentIndex] : null;

    // Flags & Data
    private int currentDiceRoll = 0; // Menyimpan hasil dadu
    private bool hasRolledDice = false; // Cek apakah sudah lempar dadu?
    private bool awaitingMovement = false;
    private bool awaitingTileResolve = false;

    [Header("References")]
    public DiceController diceController;
    public DiceInputHandler diceInputHandler;

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
        if (playerStates == null || playerStates.Count == 0) return;
        players = new List<PlayerState>(playerStates);
        currentIndex = Mathf.Clamp(startIndex, 0, players.Count - 1);
        StartCoroutine(RunTurnLoop());
    }

    // Dipanggil otomatis saat dadu berhenti
    private void HandleDiceResult(int result)
    {
        // Hanya terima hasil dadu saat fase Strategi
        if (state != TurnState.StrategyPhase) return;

        currentDiceRoll = result;
        hasRolledDice = true;

        // Matikan input dadu agar tidak lempar 2x (Opsional)
        if (diceInputHandler) diceInputHandler.InputEnabled = false;

        // Update Teks UI
        if (UIController.Instance != null) UIController.Instance.UpdateDiceText(result);

        Debug.Log($"[TurnManager] Dadu Disimpan: {currentDiceRoll}");
    }

    public void NotifyTileResolveComplete(PlayerState player)
    {
        if (player == currentPlayer) awaitingTileResolve = false;
    }

    // DIPANGGIL TOMBOL GO
    public void OnExecuteButtonPressed()
    {
        if (state != TurnState.StrategyPhase) return;

        // Validasi: Harus lempar dadu dulu sebelum jalan!
        if (!hasRolledDice)
        {
            Debug.LogWarning("Harap lempar dadu terlebih dahulu sebelum GO!");
            return; // Jangan jalan kalau belum ada dadu
        }

        // Mulai Eksekusi
        StartCoroutine(ExecuteStrategySequence());
    }

    public void ExecutePendingQueue() => OnExecuteButtonPressed();
    public void OnGoPressed() => OnExecuteButtonPressed();


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

            Debug.Log($"=== Giliran {p.name} ===");
            EventBus.TurnStarted(p);
            p.ResetTemporaryStatus();
            yield return null;

            // 2. STRATEGY PHASE (BEBAS: Lempar Dadu / Pilih Kartu)
            state = TurnState.StrategyPhase;

            // Reset Variables
            currentDiceRoll = 0;
            hasRolledDice = false;

            // Reset Dadu Visual
            if (diceController != null) diceController.ResetState();

            // Aktifkan Input Dadu
            if (diceInputHandler) diceInputHandler.InputEnabled = true;

            // Tampilkan Panel Modifier (Kosong Awalnya)
            if (UIController.Instance != null)
            {
                UIController.Instance.ShowModifierPanel();
                UIController.Instance.UpdateDiceText(0); // Reset teks dadu
            }

            Debug.Log("Fase Strategi: Silakan Lempar Dadu & Pilih Kartu, lalu tekan GO.");

            // GAME PAUSE DISINI: Menunggu fungsi OnExecuteButtonPressed() dipanggil
            while (state == TurnState.StrategyPhase)
            {
                yield return null;
            }

            // ... Script lanjut ke ExecuteStrategySequence (yang mengubah state ke Executing) ...

            // Tunggu sampai semua animasi & logic selesai (EndTurn)
            while (state != TurnState.EndTurn && state != TurnState.GameOver)
            {
                yield return null;
            }

            // 3. END TURN
            EventBus.TurnEnded(p);
            AdvanceToNextActivePlayer();
            yield return new WaitForSeconds(delayBetweenTurns);
        }
    }

    // =======================================================================
    // URUTAN EKSEKUSI (DADU -> KARTU)
    // =======================================================================

    private IEnumerator ExecuteStrategySequence()
    {
        state = TurnState.Executing; // Kunci state agar tidak bisa input lagi

        if (UIController.Instance != null) UIController.Instance.HideModifierPanel();

        Debug.Log(">>> MEMULAI EKSEKUSI URUTAN <<<");

        // -------------------------------------------------
        // LANGKAH 1: JALANKAN DADU DULUAN (Sesuai Request)
        // -------------------------------------------------
        Debug.Log($"[Sequence] 1. Menjalankan Dadu: {currentDiceRoll} langkah.");

        // Hitung target dadu
        int startTile = currentPlayer.TileID;
        int targetWithDice = startTile + currentDiceRoll;

        // Jalankan Pawn (Dadu)
        yield return StartCoroutine(MovePawnTo(currentPlayer, targetWithDice));

        yield return new WaitForSeconds(0.5f); // Jeda nafas

        // -------------------------------------------------
        // LANGKAH 2: JALANKAN KARTU (MODIFIER) SATU PER SATU
        // -------------------------------------------------
        List<NewCardData> cards = currentPlayer.GetAndClearPending();

        if (cards.Count > 0)
        {
            Debug.Log($"[Sequence] 2. Menjalankan {cards.Count} Kartu Modifier.");

            foreach (NewCardData card in cards)
            {
                Debug.Log($"[Logic Card] Mengaktifkan: {card.cardName}");

                // A. Jalankan Logic Effect
                card.Play(currentPlayer);
                currentPlayer.ConsumeSelectedCard(card);

                // B. Cek apakah Pawn Perlu Bergerak (Jika kartu movement)
                // Kita cek flag IsMoving dari Pawn. Tapi karena Play() di atas mungkin
                // memicu coroutine MoveToTile di Pawn, kita tunggu.

                yield return null; // Tunggu 1 frame

                if (currentPlayer.pawn != null && currentPlayer.pawn.IsMoving)
                {
                    // Tunggu Pawn Sampai
                    float timeOut = 3f;
                    while (currentPlayer.pawn.IsMoving && timeOut > 0)
                    {
                        timeOut -= Time.deltaTime;
                        yield return null;
                    }
                }

                yield return new WaitForSeconds(0.5f); // Jeda antar kartu
            }
        }

        // -------------------------------------------------
        // LANGKAH 3: SELESAI
        // -------------------------------------------------
        Debug.Log("[Sequence] Semua Selesai. Giliran Berakhir.");
        state = TurnState.EndTurn;
    }

    // Helper untuk menggerakkan pawn (Logic Validasi Papan)
    private IEnumerator MovePawnTo(PlayerState player, int targetTile)
    {
        // Validasi Max Board
        int maxTile = (BoardManager.Instance != null) ? BoardManager.Instance.totalTiles : 100;

        // Validasi Min Board
        if (targetTile < 1) targetTile = 1;

        // Logic Bounce (Memantul jika lebih)
        if (targetTile > maxTile)
        {
            int overshoot = targetTile - maxTile;
            targetTile = maxTile - overshoot;
        }

        // Panggil Sistem Gerak
        awaitingMovement = true;
        if (MovementSystem.Instance != null)
        {
            yield return StartCoroutine(MovementSystem.Instance.MovePlayerToTileCoroutine(player, targetTile));
        }
        else
        {
            // Fallback Teleport
            player.TileID = targetTile;
        }
        awaitingMovement = false;

        // Tile Resolve (Ular/Tangga/Efek Tile)
        // Dijalankan setiap kali pawn mendarat (baik karena dadu atau kartu)
        yield return StartCoroutine(ResolveTileEffect(player));
    }

    private IEnumerator ResolveTileEffect(PlayerState player)
    {
        awaitingTileResolve = true;
        if (TileEffectSystem.Instance != null)
        {
            // Tunggu TileEffectSystem bilang "NotifyTileResolveComplete"
            // (Logic pemanggilan event ada di MovementSystem/EventBus)
            float timer = 0;
            while (awaitingTileResolve && timer < 2.0f) // Timeout 2 detik
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