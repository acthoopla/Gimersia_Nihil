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

    [Header("References")]
    public BoardManager boardManager;
    public TurnManager turnManager;
    public MovementSystem movementSystem;
    public TileEffectSystem tileEffectSystem;
    public CombatSystem combatSystem;

    // PERBAIKAN: Ganti NewCardSystem menjadi NewCardManager
    public NewCardManager cardManager;

    [Header("Player discovery")]
    public Transform playersContainer;
    public bool autoCollectPlayers = true;

    [Header("Startup settings")]
    public int startPlayerIndex = 0;
    public bool autoStartGame = true;

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

        if (boardManager == null) boardManager = FindObjectOfType<BoardManager>();
        if (turnManager == null) turnManager = FindObjectOfType<TurnManager>();
        if (movementSystem == null) movementSystem = FindObjectOfType<MovementSystem>();
        if (tileEffectSystem == null) tileEffectSystem = FindObjectOfType<TileEffectSystem>();
        if (combatSystem == null) combatSystem = FindObjectOfType<CombatSystem>();

        // PERBAIKAN: Cari NewCardManager
        if (cardManager == null) cardManager = FindObjectOfType<NewCardManager>();

        if (boardManager == null) Debug.LogError("[NewGameManager] BoardManager not found!");
        if (turnManager == null) Debug.LogError("[NewGameManager] TurnManager not found!");
    }

    void Start()
    {
        if (autoCollectPlayers) CollectPlayersFromScene();
        if (autoStartGame) StartGame();
    }

    public void CollectPlayersFromScene()
    {
        players.Clear();
        if (playersContainer != null)
        {
            var states = playersContainer.GetComponentsInChildren<PlayerState>(true);
            players = states.OrderBy(s => s.transform.GetSiblingIndex()).ToList();
        }
        else
        {
            var all = FindObjectsOfType<PlayerState>();
            players = all.OrderBy(p => p.gameObject.name).ToList();
        }

        // Cek pawn component
        foreach (var p in players)
        {
            if (p != null)
            {
                if (p.GetComponent<NewPlayerPawn>() == null && p.GetComponent<PlayerPawn>() == null)
                {
                    Debug.LogWarning($"[NewGameManager] PlayerState {p.name} tidak memiliki script Pawn!");
                }
            }
        }

        Debug.Log($"[NewGameManager] Collected {players.Count} PlayerState(s).");
    }

    public void StartGame()
    {
        if (turnManager == null)
        {
            Debug.LogError("[NewGameManager] Cannot start: TurnManager missing.");
            return;
        }

        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("[NewGameManager] No players found. Collecting...");
            CollectPlayersFromScene();
            if (players.Count == 0) return;
        }

        boardManager?.LoadTilesFromScene();

        foreach (var p in players)
        {
            ResetPlayerForNewGame(p);
        }

        turnManager.StartGame(players, Mathf.Clamp(startPlayerIndex, 0, players.Count - 1));
        Debug.Log("[NewGameManager] Game started.");
    }

    public void RestartGame()
    {
        Debug.Log("[NewGameManager] Restarting game...");
        foreach (var p in players)
        {
            ResetPlayerForNewGame(p);

            // Pindahkan visual ke Tile 1
            if (MovementSystem.Instance != null)
            {
                MovementSystem.Instance.RequestMove(p, 1);
            }
            else
            {
                var bm = BoardManager.Instance != null ? BoardManager.Instance : FindObjectOfType<BoardManager>();
                if (bm != null)
                {
                    p.transform.position = bm.GetTilePosition(1);
                    p.TileID = 1;
                }
            }
        }

        boardManager?.RestoreBoardPositions();
        StartGame();
    }

    private void ResetPlayerForNewGame(PlayerState p)
    {
        if (p == null) return;

        // Reset Stats
        p.maxHP = 15;
        p.currentHP = p.maxHP;

        // Panggil fungsi reset status yang ada di PlayerState
        p.ResetTemporaryStatus();

        // Reset Hand
        p.ClearHand();

        // PERBAIKAN: Hapus baris p.getsExtraTurn dan p.drawCardNextTurn karena variabel itu sudah tidak ada
        // p.getsExtraTurn = false;  <-- HAPUS
        // p.drawCardNextTurn = false; <-- HAPUS

        // Reset Posisi Data
        p.TileID = 1;

        // Reset Posisi Visual
        if (boardManager != null)
        {
            Vector3 startPos = boardManager.GetTilePosition(1);
            // Coba pakai MovementSystem dulu biar mulus, kalau ga ada baru teleport kasar
            if (MovementSystem.Instance != null)
            {
                // RequestMove biasanya async, tapi untuk init gapapa
                // Atau bisa set transform langsung:
                p.transform.position = startPos;
            }
            else
            {
                p.transform.position = startPos;
            }
        }

        p.NotifyStateChanged();
    }

    #region Editor Helpers
#if UNITY_EDITOR
    [ContextMenu("Collect Players")]
    public void EditorCollectPlayers() => CollectPlayersFromScene();

    [ContextMenu("Start Game")]
    public void EditorStartGame() => StartGame();
#endif
    #endregion
}