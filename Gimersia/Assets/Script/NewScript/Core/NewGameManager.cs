using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// NewGameManager
/// - Composition root untuk inisialisasi sistem (SRP: hanya tangani startup & wiring)
/// - Mencari & register subsystems (BoardManager, TurnManager, MovementSystem, TileEffectSystem, CombatSystem, NewCardSystem)
/// - Collect PlayerState instances (dari scene atau prefab container) dan start TurnManager
/// - Menyediakan API runtime untuk restart game / debug helpers
/// 
/// Catatan:
/// - NewGameManager tidak menjalankan flow/turns sendiri.
/// - Untuk referensi versi lama (sebelum refactor), lihat file arsip:
///   /mnt/data/multiplayermanager_original.cs
/// </summary>
[DisallowMultipleComponent]
public class NewGameManager : MonoBehaviour
{
    public static NewGameManager Instance { get; private set; }

    [Header("References (optional, will auto-find if null)")]
    public BoardManager boardManager;
    public TurnManager turnManager;
    public MovementSystem movementSystem;
    public TileEffectSystem tileEffectSystem;
    public CombatSystem combatSystem;
    public NewCardSystem cardSystem;

    [Header("Player discovery")]
    [Tooltip("Jika diberi container, NewGameManager akan mencari PlayerState di dalam container itu. Jika null, akan mencari di seluruh scene.")]
    public Transform playersContainer;

    [Tooltip("Jika true, NewGameManager akan auto-collect PlayerState components saat Start()")]
    public bool autoCollectPlayers = true;

    [Header("Startup settings")]
    [Tooltip("Index pemain yang mulai (default 0)")]
    public int startPlayerIndex = 0;

    [Tooltip("Jika true, TurnManager.StartGame dipanggil otomatis di Awake/Start")]
    public bool autoStartGame = true;

    // runtime players list
    private List<PlayerState> players = new List<PlayerState>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Debug.LogWarning("[NewGameManager] Duplicate NewGameManager detected. Destroying new instance.");
            Destroy(gameObject);
            return;
        }

        // auto find essential subsystems if null
        if (boardManager == null) boardManager = FindObjectOfType<BoardManager>();
        if (turnManager == null) turnManager = FindObjectOfType<TurnManager>();
        if (movementSystem == null) movementSystem = FindObjectOfType<MovementSystem>();
        if (tileEffectSystem == null) tileEffectSystem = FindObjectOfType<TileEffectSystem>();
        if (combatSystem == null) combatSystem = FindObjectOfType<CombatSystem>();
        if (cardSystem == null) cardSystem = FindObjectOfType<NewCardSystem>();

        // sanity warnings
        if (boardManager == null) Debug.LogError("[NewGameManager] BoardManager not found in scene!");
        if (turnManager == null) Debug.LogError("[NewGameManager] TurnManager not found in scene!");
        if (movementSystem == null) Debug.LogError("[NewGameManager] MovementSystem not found in scene!");
    }

    void Start()
    {
        // optionally auto collect players (PlayerState components)
        if (autoCollectPlayers)
        {
            CollectPlayersFromScene();
        }

        // optionally start game immediately
        if (autoStartGame)
        {
            StartGame();
        }
    }

    /// <summary>
    /// Kumpulkan PlayerState dari 'playersContainer' bila diisi, atau dari seluruh scene jika container null.
    /// Player order akan diambil sesuai child order jika container diberikan; otherwise diambil berdasarkan transform name order.
    /// </summary>
    public void CollectPlayersFromScene()
    {
        players.Clear();
        if (playersContainer != null)
        {
            var states = playersContainer.GetComponentsInChildren<PlayerState>(true);
            // keep the order by transform sibling index
            players = states.OrderBy(s => s.transform.GetSiblingIndex()).ToList();
        }
        else
        {
            // global find (non-deterministic order). We try to sort by name to make it stable.
            var all = FindObjectsOfType<PlayerState>();
            players = all.OrderBy(p => p.gameObject.name).ToList();
        }

        // Ensure all have default init values
        foreach (var p in players)
        {
            if (p != null)
            {
                // Attach default components if not present (defensive)
                if (p.GetComponent<NewPlayerPawn>() == null && p.GetComponent<PlayerPawn>() == null)
                {
                    Debug.LogWarning($"[NewGameManager] PlayerState {p.gameObject.name} has no pawn component (NewPlayerPawn or PlayerPawn). Movement may fallback.");
                }
            }
        }

        Debug.Log($"[NewGameManager] Collected {players.Count} PlayerState(s).");
    }

    /// <summary>
    /// Start the game by feeding players list into TurnManager.
    /// </summary>
    public void StartGame()
    {
        if (turnManager == null)
        {
            Debug.LogError("[NewGameManager] Cannot start game: TurnManager missing.");
            return;
        }

        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("[NewGameManager] No players found. Collecting players automatically...");
            CollectPlayersFromScene();
            if (players.Count == 0)
            {
                Debug.LogError("[NewGameManager] No players available to start the game.");
                return;
            }
        }

        // Reset board if necessary
        boardManager?.LoadTilesFromScene();

        // Reset player states to default before start
        foreach (var p in players)
        {
            ResetPlayerForNewGame(p);
        }

        // Register singleton dependencies if desired (some modules already handle their own singleton)
        // Start TurnManager with PlayerState list
        turnManager.StartGame(players, Mathf.Clamp(startPlayerIndex, 0, players.Count - 1));
        Debug.Log("[NewGameManager] Game started.");
    }

    /// <summary>
    /// Hard restart game: reset board, reset players, and re-run StartGame.
    /// </summary>
    public void RestartGame()
    {
        Debug.Log("[NewGameManager] Restarting game...");
        // optionally clear winners etc (TurnManager may track)
        // Reset player objects and their visuals
        foreach (var p in players)
        {
            ResetPlayerForNewGame(p);
            // Move player visuals to starting tile (1) if MovementSystem available
            if (MovementSystem.Instance != null)
            {
                MovementSystem.Instance.RequestMove(p, 1);
            }
            else
            {
                // fallback: set transform pos to board start
                var bm = BoardManager.Instance != null ? BoardManager.Instance : FindObjectOfType<BoardManager>();
                if (bm != null)
                {
                    p.transform.position = bm.GetTilePosition(1);
                    p.TileID = 1;
                }
            }
        }

        // restore board positions if shuffle used
        boardManager?.RestoreBoardPositions();

        // restart turn manager
        StartGame();
    }

    /// <summary>
    /// Reset per-player data to the default starting state.
    /// </summary>
    private void ResetPlayerForNewGame(PlayerState p)
    {
        if (p == null) return;
        p.maxHP = 15;
        p.currentHP = p.maxHP;
        p.defenseFromCards = 0;
        p.nextRollModifier = 0;
        p.immuneToAllNegativeTurns = 0;
        p.immuneToSnakeUses = 0;
        p.getsExtraTurn = false;
        p.drawCardNextTurn = false;
        p.ClearHand();

        // reset tile to 1
        p.TileID = 1;

        // Visuals: move to starting tile if board manager available
        if (boardManager != null)
        {
            Vector3 startPos = boardManager.GetTilePosition(1);
            // If NewPlayerPawn exists it may provide a teleport method; attempt safe wrapper
            var pawnComp = p.GetComponent<NewPlayerPawn>() as Component ?? p.GetComponent<PlayerPawn>() as Component;
            if (pawnComp != null)
            {
                // Use MovementSystem to move pawn to start to avoid bypassing animations
                if (MovementSystem.Instance != null)
                {
                    MovementSystem.Instance.RequestMove(p, 1);
                }
                else
                {
                    p.transform.position = startPos;
                }
            }
            else
            {
                p.transform.position = startPos;
            }
        }

        // Notify subscribers (UI) via PlayerState.OnStateChanged (internal) or EventBus if needed
        p.NotifyStateChanged();
    }

    #region Editor / Debug helpers
#if UNITY_EDITOR
    [ContextMenu("Collect Players (Editor)")]
    public void EditorCollectPlayers() => CollectPlayersFromScene();

    [ContextMenu("Start Game (Editor)")]
    public void EditorStartGame() => StartGame();
#endif
    #endregion

    #region Utility / Accessors
    public List<PlayerState> GetPlayers() => players;
    public PlayerState GetPlayerByIndex(int idx) => (players != null && idx >= 0 && idx < players.Count) ? players[idx] : null;
    #endregion
}
