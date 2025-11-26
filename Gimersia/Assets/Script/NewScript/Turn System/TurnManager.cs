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
        Executing,      // Sedang menjalankan Queue (Dadu -> Kartu 1 -> Kartu 2)
        Moving,         // Sub-state: Animasi Jalan
        ResolveTile,    // Sub-state: Efek Tile
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

    // [MERGE] Alias agar tombol UI lama tetap jalan
    public void OnGoPressed() => ExecutePendingQueue();

    public void ExecutePendingQueue()
    {
        if (state == TurnState.StrategyPhase)
        {
            if (!hasRolledDice)
            {
                Debug.LogWarning("Harap lempar dadu terlebih dahulu sebelum GO!");
                return;
            }
            Debug.Log(">>> EXECUTE QUEUE! <<<");
            goButtonPressed = true;
        }
    }

    // =======================================================================
    // LOGIKA LOOP (MENGGUNAKAN STRUKTUR TEMAN AGAR KARTU JALAN)
    // =======================================================================

    private IEnumerator RunTurnLoop()
    {
        while (true)
        {
            if (players.Count == 0) yield break;

            // 1. START TURN
            state = TurnState.StartTurn;
            PlayerState p = currentPlayer;

            EventBus.TurnStarted(p); // [MERGE] Trigger punyamu
            p.ResetTemporaryStatus();
            yield return null;

            // 2. STRATEGY PHASE (Dadu + Kartu)
            state = TurnState.StrategyPhase;
            currentDiceRoll = 0;
            hasRolledDice = false;
            goButtonPressed = false;

            if (diceController != null) diceController.ResetState();
            if (diceInputHandler) diceInputHandler.InputEnabled = true;

            // Tampilkan UI Card Modifier (Punya Teman)
            if (UIController.Instance != null)
            {
                UIController.Instance.ShowModifierPanel();
                UIController.Instance.UpdateDiceText(0);
            }

            Debug.Log($"[Turn] Giliran {p.name}. Strategy Phase.");

            // TUNGGU TOMBOL GO
            while (!goButtonPressed) yield return null;

            // 3. EKSEKUSI
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
        // A. JALAN DADU
        Debug.Log($"[Sequence] 1. Jalan Dadu: {currentDiceRoll}");
        int startTile = currentPlayer.TileID;
        int targetWithDice = startTile + currentDiceRoll + currentPlayer.nextRollModifier;
        currentPlayer.nextRollModifier = 0; // Reset modifier

        yield return StartCoroutine(MovePawnTo(currentPlayer, targetWithDice));
        yield return new WaitForSeconds(0.5f);

        // B. JALAN KARTU (Pending List)
        List<NewCardData> pendingCards = currentPlayer.GetAndClearPending(); // Pastikan PlayerState punya method ini

        if (pendingCards.Count > 0)
        {
            Debug.Log($"[Sequence] 2. Menjalankan {pendingCards.Count} Kartu.");
            foreach (NewCardData card in pendingCards)
            {
                // Logic Kartu (Punya Teman)
                card.Play(currentPlayer);
                currentPlayer.ConsumeSelectedCard(card);

                yield return null;
                // Jika kartu menyebabkan gerakan, tunggu sampai selesai
                if (currentPlayer.pawn != null && currentPlayer.pawn.IsMoving)
                {
                    // Tunggu movement system selesai (Punya Teman logika wait-nya, tapi pakai MovementSystem punyamu)
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
    }

    // [MERGE] Core Movement Logic
    private IEnumerator MovePawnTo(PlayerState player, int targetTile)
    {
        // 1. Validasi Batas Board
        int maxTile = (BoardManager.Instance != null) ? BoardManager.Instance.totalTiles : 100;
        if (targetTile < 1) targetTile = 1;
        if (targetTile > maxTile)
        {
            int overshoot = targetTile - maxTile;
            targetTile = maxTile - overshoot;
        }

        // 2. Gerak Pawn (MENGGUNAKAN MOVEMENT SYSTEM PUNYAMU)
        state = TurnState.Moving;

        if (MovementSystem.Instance != null)
        {
            // [MERGE] Gunakan Coroutine Punyamu untuk animasi langkah
            yield return StartCoroutine(MovementSystem.Instance.MovePlayerToTileCoroutine(player, targetTile));
        }
        else
        {
            // Fallback teleport
            player.TileID = targetTile;
        }

        // 3. Trigger Tile Effect Manual (Penting agar tidak miss event)
        var bm = BoardManager.Instance;
        Tiles landedTile = (bm != null) ? bm.GetTileByID(player.TileID) : null;

        Debug.Log($"[TurnManager] Landed on {player.TileID}. Triggering Effects...");
        EventBus.TileLanded(player, landedTile);

        // 4. Tunggu Efek Selesai (Snake, Ladder, dll)
        yield return StartCoroutine(ResolveTileEffect(player));
    }

    private IEnumerator ResolveTileEffect(PlayerState player)
    {
        state = TurnState.ResolveTile;
        awaitingTileResolve = true;

        if (TileEffectSystem.Instance != null)
        {
            float timer = 0;
            // Tunggu flag 'awaitingTileResolve' dimatikan oleh TileEffectSystem
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

    private void AdvanceToNextActivePlayer()
    {
        if (players.Count == 0) return;
        currentIndex = (currentIndex + 1) % players.Count;
    }
}