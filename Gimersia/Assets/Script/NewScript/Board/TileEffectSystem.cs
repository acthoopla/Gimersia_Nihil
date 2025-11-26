using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static NewCardSystem;

[DisallowMultipleComponent]
public class TileEffectSystem : MonoBehaviour
{
    public static TileEffectSystem Instance { get; private set; }

    // Mapping damage per baris (Row 1-10)
    private readonly Dictionary<int, int> rowDamage = new Dictionary<int, int>()
    {
        {1, 1}, {2, 2}, {3, 2}, {4, 3}, {5, 4}, {6, 5}, {7, 6}, {8, 8}, {9, 9}, {10, 10}
    };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnEnable() { EventBus.OnTileLanded += HandleTileLanded; }
    void OnDisable() { EventBus.OnTileLanded -= HandleTileLanded; }

    private void HandleTileLanded(PlayerState player, Tiles tile)
    {
        StartCoroutine(ResolveTileRoutine(player, tile));
    }

    private IEnumerator ResolveTileRoutine(PlayerState player, Tiles tile)
    {
        // 1. Validasi Data
        if (player == null || tile == null)
        {
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        yield return new WaitForSeconds(0.15f);
        Debug.Log($"[TileEffect] {player.gameObject.name} landed on Tile {tile.tileID} ({tile.type})");

        // 2. Cek Immunity (Khusus Tile Negatif)
        bool isNegative = IsNegaTile(tile);
        if (isNegative && player.immuneToAllNegativeTurns > 0)
        {
            Debug.Log($"[TileEffect] Player immune to negative tile.");
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        NewTileProperties props = tile.GetComponent<NewTileProperties>();

        // =========================================================
        // LOGIC 1: TANGGA (LADDER) -> TELEPORT -> PILIH HADIAH
        // =========================================================
        if (tile.type == TileType.LadderStart && tile.targetTile != null)
        {
            Debug.Log($"[Ladder] Teleporting to Tile {tile.targetTile.tileID}...");

            // A. Teleport Animation
            if (MovementSystem.Instance != null)
            {
                MovementSystem.Instance.RequestMove(player, tile.targetTile.tileID);
                yield return new WaitForSeconds(1.0f);
                while (player.pawn != null && player.pawn.IsMoving) yield return null;
            }

            // B. Munculkan UI Pilihan (Movement vs Buff)
            if (BlessingUIManager.Instance != null && NewCardManager.Instance != null)
            {
                Debug.Log("[Ladder] Waiting for reward choice (Move/Buff)...");
                bool choiceMade = false;

                BlessingUIManager.Instance.ShowCategoryChoice((selectedCategory) =>
                {
                    NewCardData reward = NewCardManager.Instance.GetRandomCardByCategory(selectedCategory);
                    if (reward != null)
                    {
                        if (player.TryAddCard(reward))
                            Debug.Log($"[Reward] Ladder Reward: {reward.cardName} ({selectedCategory})");
                        else
                            Debug.LogWarning("[Reward] Hand Full!");
                    }
                    choiceMade = true;
                });

                while (!choiceMade) yield return null;
            }
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 2: KARTU INSTAN (Card Tiles)
        // =========================================================

        NewCardData instantCard = null;

        // --- A. Tile Movement (Random Movement Card) ---
        if (tile.type == TileType.CardMovement)
        {
            instantCard = NewCardManager.Instance?.GetRandomCardByCategory(CardCategory.Movement);
        }
        // --- B. Tile Buff (Random Buff Card) ---
        else if (tile.type == TileType.CardBuff)
        {
            instantCard = NewCardManager.Instance?.GetRandomCardByCategory(CardCategory.Buff);
        }
        // --- C. Tile Random (Random Any Card) ---
        else if (tile.type == TileType.CardRandom)
        {
            instantCard = NewCardManager.Instance?.GetRandomCardAny();
        }

        // Jika dapat kartu, berikan ke player
        if (instantCard != null)
        {
            Debug.Log($"[CardTile] Tipe {tile.type} memberikan kartu: {instantCard.cardName}");

            bool success = player.TryAddCard(instantCard);
            if (!success) Debug.LogWarning("[CardTile] Gagal! Hand Player Penuh.");

            // Setelah TryAddCard sukses, Script 'VisualBridge' akan otomatis memunculkan 
            // kartu visual di tangan (placeholder).

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 3: SYSTEM LAIN (Snake, Attack, Nega)
        // =========================================================

        // Snake
        if (tile.type == TileType.SnakeStart && tile.targetTile != null)
        {
            if (player.immuneToSnakeUses > 0)
            {
                player.immuneToSnakeUses--;
            }
            else if (MovementSystem.Instance != null)
            {
                MovementSystem.Instance.RequestMove(player, tile.targetTile.tileID);
                yield break;
            }
        }

        // Attack Tile
        if (IsAttackTile(tile, props))
        {
            int damage = 0;
            if (props != null && props.overrideDamage > 0) damage = props.overrideDamage;
            else
            {
                int row = (BoardManager.Instance != null) ? BoardManager.Instance.GetRow(tile.tileID) : 1;
                if (!rowDamage.TryGetValue(row, out damage)) damage = 1;
            }

            if (CombatSystem.Instance != null) CombatSystem.Instance.ApplyDamageToPlayer(player, damage, "AttackTile");
            else player.ApplyDamage(damage, "AttackTile");

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // Nega Tile
        if (IsNegaTile(tile))
        {
            int primaryDamage = UnityEngine.Random.Range(1, 4);
            player.ApplyDamage(primaryDamage, "NegaTile");

            int choice = UnityEngine.Random.Range(0, 3);
            switch (choice)
            {
                case 0: player.DiscardRandom(2); break;
                case 1: player.nextRollModifier += 2; break;
                case 2: player.nextRollModifier -= 2; break;
            }
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // Boss
        if (IsBossTile(tile, props))
        {
            if (props != null && props.overrideDamage > 0) player.ApplyDamage(props.overrideDamage, "BossTile");
            if (props != null && props.causeShuffleBoard && BoardManager.Instance != null) BoardManager.Instance.ShuffleBoardPositions();

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        TurnManager.Instance?.NotifyTileResolveComplete(player);
    }

    #region Helpers & Detection

    private bool IsAttackTile(Tiles tile, NewTileProperties props)
    {
        if (tile.type == TileType.Attack || tile.type == TileType.AttackCracked) return true;
        if (props != null && props.forceAsAttack) return true;
        return false;
    }

    private bool IsNegaTile(Tiles tile)
    {
        if (tile.type == TileType.Damage || tile.type == TileType.DamageCracked ||
            tile.type == TileType.Disarm || tile.type == TileType.DisarmCracked ||
            tile.type == TileType.Provocation || tile.type == TileType.ProvocationCracked ||
            tile.type == TileType.Despair || tile.type == TileType.DespairCracked) return true;

        NewTileProperties props = tile.GetComponent<NewTileProperties>();
        if (props != null && props.isNegaTile) return true;
        return false;
    }

    private bool IsBossTile(Tiles tile, NewTileProperties props)
    {
        if (tile.type == TileType.Death) return true;
        if (props != null && props.isBossTile) return true;
        if (tile.gameObject.CompareTag("BossTile")) return true;
        return false;
    }
    #endregion
}

// --- SCRIPT TAMBAHAN UNTUK MENGHILANGKAN ERROR CS0246 ---
[DisallowMultipleComponent]
public class NewTileProperties : MonoBehaviour
{
    [Header("Force tile categories")]
    public bool forceAsAttack = false;
    public bool isNegaTile = false;
    public bool isBossTile = false;

    [Header("Optional damage override")]
    public int overrideDamage = 0;

    [Header("Boss special")]
    public bool causeShuffleBoard = false;
    public int advancePlayerBy = 0;
}