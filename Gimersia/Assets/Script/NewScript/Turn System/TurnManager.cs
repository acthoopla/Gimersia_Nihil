using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public enum TurnState { Idle, StrategyPhase, Executing, Moving, ResolveTile, GameOver }
    public TurnState state = TurnState.Idle;

    [Header("References")]
    public DiceController diceController;
    public DiceInputHandler diceInputHandler;
    public CardSelectionHolder cardSelectionHolder;

    public List<PlayerState> players = new List<PlayerState>();
    private int currentIndex = 0;
    private PlayerState currentPlayer => (players.Count > 0) ? players[currentIndex] : null;

    private int currentDiceRoll = 0;
    private bool awaitingTileResolve = false;

    void Awake() { if (Instance == null) Instance = this; }

    void Start()
    {
        if (!diceController) diceController = FindObjectOfType<DiceController>();
        if (!diceInputHandler) diceInputHandler = FindObjectOfType<DiceInputHandler>();
        if (!cardSelectionHolder) cardSelectionHolder = FindObjectOfType<CardSelectionHolder>();

        if (players.Count == 0) players.AddRange(FindObjectsOfType<PlayerState>());
        if (diceController) diceController.OnDiceResult += HandleDiceResult;

        // [FIX UTAMA] Hanya matikan input jika game BELUM dimulai.
        // Jika NewGameManager sudah start duluan, jangan dimatikan lagi!
        if (state == TurnState.Idle)
        {
            if (UIController.Instance) UIController.Instance.SetGoButtonInteractable(false);
            if (diceInputHandler) diceInputHandler.InputEnabled = false;
        }
    }

    void OnDestroy()
    {
        if (diceController) diceController.OnDiceResult -= HandleDiceResult;
    }

    // ===========================================================================
    // PUBLIC API
    // ===========================================================================

    public void StartGame(List<PlayerState> playerStates, int startIndex = 0)
    {
        players = new List<PlayerState>(playerStates);
        currentIndex = Mathf.Clamp(startIndex, 0, players.Count - 1);

        // Mulai Turn Pertama
        StartTurn();
    }

    public void OnGoPressed()
    {
        if (state != TurnState.StrategyPhase) return;
        StartCoroutine(ExecuteTurnRoutine());
    }

    public void ExecutePendingQueue() => OnGoPressed();

    // ===========================================================================
    // LOGIKA UTAMA
    // ===========================================================================

    private void StartTurn()
    {
        state = TurnState.StrategyPhase;
        PlayerState p = currentPlayer;

        // 1. Reset status DULUAN
        p.ResetTemporaryStatus();

        // 2. BARU panggil event (agar efek tile Provocation/Despair masuk)
        EventBus.TurnStarted(p);

        currentDiceRoll = 0;
        if (diceController) diceController.ResetState();

        // [PENTING] Nyalakan Dadu di sini
        if (diceInputHandler) diceInputHandler.InputEnabled = true;

        if (UIController.Instance)
        {
            UIController.Instance.ShowModifierPanel();
            UIController.Instance.UpdateDiceText(0);
            UIController.Instance.SetGoButtonInteractable(false); // GO Mati sampai dadu dilempar
        }
    }

    private void HandleDiceResult(int result)
    {
        if (state != TurnState.StrategyPhase) return;
        currentDiceRoll = result;

        // [REQ] Matikan dadu setelah dilempar
        if (diceInputHandler) diceInputHandler.InputEnabled = false;

        // Nyalakan Tombol GO
        if (UIController.Instance)
        {
            UIController.Instance.UpdateDiceText(result);
            UIController.Instance.SetGoButtonInteractable(true);
        }

        if (TileHighlighter.Instance != null)
        {
            TileHighlighter.Instance.PreviewDestination(currentPlayer, currentDiceRoll, currentPlayer.CalculatePendingMoveModifier());
        }
    }

    private IEnumerator ExecuteTurnRoutine()
    {
        state = TurnState.Executing;

        // Kunci Input
        if (UIController.Instance) UIController.Instance.SetGoButtonInteractable(false);
        if (diceInputHandler) diceInputHandler.InputEnabled = false;
        if (UIController.Instance) UIController.Instance.HideModifierPanel();
        if (TileHighlighter.Instance != null) TileHighlighter.Instance.HideHighlight();

        // 1. KARTU OTOMATIS (Jika di slot masih ada)
        if (cardSelectionHolder != null && cardSelectionHolder.GetCardCount() > 0)
        {
            Debug.Log("TurnManager: Menggunakan kartu di slot secara otomatis...");
            currentPlayer.ConfirmLoadout();
            // False = Hanya Animasi, Logic dijalankan di loop bawah
            yield return StartCoroutine(cardSelectionHolder.AnimateUseCards(false));
        }

        // 2. JALANKAN KARTU (Dari Pending Queue)
        List<NewCardData> pendingCards = currentPlayer.GetAndClearPending();

        foreach (NewCardData card in pendingCards)
        {
            card.Play(currentPlayer);

            // Cek efek gerakan
            if (currentPlayer.pawn != null && currentPlayer.pawn.IsMoving)
            {
                while (currentPlayer.pawn.IsMoving) yield return null;
                yield return StartCoroutine(ResolveTileEffect(currentPlayer));
                if (state == TurnState.GameOver) yield break;
            }
            yield return new WaitForSeconds(0.2f);
        }

        // 3. JALAN DADU
        int steps = currentDiceRoll + currentPlayer.nextRollModifier;
        currentPlayer.nextRollModifier = 0; // Reset modifier setelah dipakai

        yield return StartCoroutine(MovePawnTo(currentPlayer, steps));

        // 4. SELESAI TURN
        if (state != TurnState.GameOver)
        {
            EventBus.TurnEnded(currentPlayer);
            AdvanceToNextPlayer();
        }
    }

    private void AdvanceToNextPlayer()
    {
        if (players.Count == 0) return;
        currentIndex = (currentIndex + 1) % players.Count;
        StartTurn();
    }

    // ===========================================================================
    // MOVEMENT & TILE LOGIC
    // ===========================================================================

    private IEnumerator MovePawnTo(PlayerState player, int steps)
    {
        state = TurnState.Moving;
        int targetTile = Mathf.Clamp(player.TileID + steps, 1, 100);

        if (MovementSystem.Instance != null)
            yield return StartCoroutine(MovementSystem.Instance.MovePlayerToTileCoroutine(player, targetTile));
        else
            player.TileID = targetTile;

        yield return StartCoroutine(ResolveTileEffect(player));
    }

    private IEnumerator ResolveTileEffect(PlayerState player)
    {
        if (player.TileID == 100)
        {
            if (UIController.Instance) UIController.Instance.ShowGameOver();
            state = TurnState.GameOver;
            yield break;
        }

        var bm = BoardManager.Instance;
        Tiles landedTile = (bm != null) ? bm.GetTileByID(player.TileID) : null;

        EventBus.TileLanded(player, landedTile);

        if (TileEffectSystem.Instance != null)
        {
            state = TurnState.ResolveTile;
            awaitingTileResolve = true;

            float timer = 0;
            while (awaitingTileResolve && timer < 3.0f)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }
    }

    public void NotifyTileResolveComplete(PlayerState player)
    {
        if (player == currentPlayer) awaitingTileResolve = false;
    }

    public int GetCurrentDiceRoll() => currentDiceRoll;
    public PlayerState GetCurrentPlayer() => currentPlayer;
}