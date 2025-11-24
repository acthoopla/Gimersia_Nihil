using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TileEffectSystem (SRP)
/// - Resolve efek tile saat player mendarat.
/// - Subscribe ke EventBus.OnTileLanded (dipanggil oleh TurnManager setelah Movement finished).
/// - Mendukung: Snake (teleport down), Ladder (teleport up + card reward), BlessingCard (card tile),
///   Attack tile (damage berdasarkan row atau override via NewTileProperties), Nega tile (damage 1-3 + child effect),
///   Boss tile (basic hooks).
/// - Jika tile meresultkan teleport, sistem akan memanggil MovementSystem.RequestMove(...) dan TIDAK memanggil
///   TurnManager.NotifyTileResolveComplete sampai teleport chain benar-benar selesai (yaitu landing pada tile tanpa teleport).
/// 
/// - Ketergantungan (di-check runtime): BoardManager, MovementSystem, CombatSystem, CardSystem, TurnManager
/// </summary>
[DisallowMultipleComponent]
public class TileEffectSystem : MonoBehaviour
{
    // Row damage mapping sesuai GDD (index 1..10 used)
    private readonly Dictionary<int, int> rowDamage = new Dictionary<int, int>()
    {
        {1, 1}, {2, 2}, {3, 2}, {4, 3}, {5, 4}, {6, 5}, {7, 6}, {8, 8}, {9, 9}, {10, 10}
    };

    void OnEnable()
    {
        EventBus.OnTileLanded += HandleTileLanded;
    }

    void OnDisable()
    {
        EventBus.OnTileLanded -= HandleTileLanded;
    }

    private void HandleTileLanded(PlayerState player, Tiles tile)
    {
        // Start coroutine to resolve tile effects
        StartCoroutine(ResolveTileRoutine(player, tile));
    }

    private IEnumerator ResolveTileRoutine(PlayerState player, Tiles tile)
    {
        if (player == null)
        {
            Debug.LogWarning("[TileEffectSystem] ResolveTile: player null");
            yield break;
        }

        // If tile is null (shouldn't normally happen), notify immediate complete.
        if (tile == null)
        {
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // Small UI-friendly delay so movement settle visuals complete first
        yield return new WaitForSeconds(0.15f);

        // Debug
        Debug.Log($"[TileEffectSystem] {player.gameObject.name} landed on Tile {tile.tileID} ({tile.type})");

        // Check for immunity to all negative turns first (some tiles are negative)
        bool isNegativeTile = IsNegaTile(tile);
        if (isNegativeTile && player.immuneToAllNegativeTurns > 0)
        {
            // If immune, skip negative tile effects (but still consider damage tile? GDD says immunity prevents negative)
            Debug.Log($"[TileEffectSystem] {player.gameObject.name} is immune to negative tiles.");
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // Resolve by tile type / properties
        // Priority:
        //  - SnakeStart / LadderStart (teleport chain handled by MovementSystem + subsequent TileLanded events)
        //  - BlessingCard (Card tile)
        //  - AttackTile (detected via NewTileProperties or tag)
        //  - NegaTile (Damage 1-3 + child effect)
        //  - Boss tile (basic hooks)
        //  - Default: no effect

        // Try get NewTileProperties if present for explicit data
        NewTileProperties props = tile.GetComponent<NewTileProperties>();

        // 1) Ladder
        if (tile.type == TileType.LadderStart && tile.targetTile != null)
        {
            // Ladder behavior:
            // - Play ladder anim via MovementSystem (we just request move to target)
            // - After landing on target, TileLanded will be called again by TurnManager/MovementSystem chain
            // - Ladder special: give player choice of card category (movement or buff) then grant 1 random from that category
            Debug.Log($"[TileEffectSystem] Ladder triggered: teleport {tile.tileID} -> {tile.targetTile.tileID}");

            // Optionally give choice + card via CardSystem if exists
            // For now: call CardSystem to give 1 random from chosen category after teleport landing (CardSystem should implement choice UI)
            // We'll assume CardSystem exposes GiveLadderReward(player, allowChoiceCategories)
            if (MovementSystem.Instance != null)
            {
                MovementSystem.Instance.RequestMove(player, tile.targetTile.tileID);
                // Do NOT call NotifyTileResolveComplete here — wait for new TileLanded when teleport finishes
                yield break;
            }
            else
            {
                Debug.LogWarning("[TileEffectSystem] MovementSystem missing; cannot teleport for ladder. Resolving as no-op.");
                TurnManager.Instance?.NotifyTileResolveComplete(player);
                yield break;
            }
        }

        // 2) Snake
        if (tile.type == TileType.SnakeStart && tile.targetTile != null)
        {
            // If player has snake immunity uses, consume
            if (player.immuneToSnakeUses > 0)
            {
                player.immuneToSnakeUses--;
                Debug.Log($"[TileEffectSystem] {player.gameObject.name} immune to snake (remaining uses {player.immuneToSnakeUses})");
                TurnManager.Instance?.NotifyTileResolveComplete(player);
                yield break;
            }

            Debug.Log($"[TileEffectSystem] Snake triggered: {tile.tileID} -> {tile.targetTile.tileID}");
            if (MovementSystem.Instance != null)
            {
                MovementSystem.Instance.RequestMove(player, tile.targetTile.tileID);
                // Do not NotifyTileResolveComplete now; wait for chain
                yield break;
            }
            else
            {
                Debug.LogWarning("[TileEffectSystem] MovementSystem missing; cannot teleport for snake. Resolving as no-op.");
                TurnManager.Instance?.NotifyTileResolveComplete(player);
                yield break;
            }
        }

        // 3) BlessingCard (Card Tile)
        if (tile.type == TileType.BlessingCard)
        {
            Debug.Log($"[TileEffectSystem] Card tile: give 1 random card (movement or buff) to {player.gameObject.name}");
            // Use CardSystem if available
            if (NewCardSystem.Instance != null)
            {
                // Draw one random from categories Movement + Buff
                NewCardSystem.Instance.GiveRandomCardFromCategories(player, new NewCardSystem.CardCategory[] {
                    NewCardSystem.CardCategory.Movement,
                    NewCardSystem.CardCategory.Buff
                });
            }
            else
            {
                Debug.LogWarning("[TileEffectSystem] CardSystem not found; skipping card grant.");
            }

            // Completed
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // 4) Attack tile: check explicit props or fallback to rowDamage
        if (IsAttackTile(tile, props))
        {
            int damage = 0;
            if (props != null && props.overrideDamage > 0)
            {
                damage = props.overrideDamage;
            }
            else
            {
                // use row-based damage via BoardManager
                BoardManager bm = BoardManager.Instance != null ? BoardManager.Instance : FindObjectOfType<BoardManager>();
                int row = (bm != null) ? bm.GetRow(tile.tileID) : ((tile.tileID - 1) / 10) + 1;
                if (!rowDamage.TryGetValue(row, out damage))
                    damage = 1;
            }

            Debug.Log($"[TileEffectSystem] Attack tile: applying {damage} damage to {player.gameObject.name}");
            if (CombatSystem.Instance != null)
                CombatSystem.Instance.ApplyDamageToPlayer(player, damage, $"AttackTile (row {(BoardManager.Instance != null ? BoardManager.Instance.GetRow(tile.tileID) : ((tile.tileID - 1) / 10 + 1))})");
            else
                player.ApplyDamage(damage, "AttackTile");

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // 5) Nega/Debuff Tile
        if (IsNegaTile(tile))
        {
            // Primary effect: Damage to player (1..3)
            int primaryDamage = UnityEngine.Random.Range(1, 4); // 1..3 inclusive
            Debug.Log($"[TileEffectSystem] Nega tile: primary damage {primaryDamage} to {player.gameObject.name}");
            if (CombatSystem.Instance != null)
                CombatSystem.Instance.ApplyDamageToPlayer(player, primaryDamage, "NegaTile");
            else
                player.ApplyDamage(primaryDamage, "NegaTile");

            // Then pick ONE child effect (disarm / +2 dice / -2 dice)
            int choice = UnityEngine.Random.Range(0, 3);
            switch (choice)
            {
                case 0: // Disarm: discard 2 random cards
                    var removed = player.DiscardRandom(2);
                    Debug.Log($"[TileEffectSystem] Nega->Disarm removed {removed.Count} cards from {player.gameObject.name}");
                    break;
                case 1: // +2 final dice (applies to next roll)
                    player.nextRollModifier += 2;
                    Debug.Log($"[TileEffectSystem] Nega->Dice+2 applied to {player.gameObject.name}");
                    break;
                case 2: // -2 final dice
                    player.nextRollModifier -= 2;
                    Debug.Log($"[TileEffectSystem] Nega->Dice-2 applied to {player.gameObject.name}");
                    break;
            }

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // 6) Boss tile detection (via props or tag)
        if (IsBossTile(tile, props))
        {
            Debug.Log($"[TileEffectSystem] Boss tile landed by {player.gameObject.name}. (Hook for boss actions)");
            // Basic handling: if props define bossDamage apply it
            if (props != null && props.overrideDamage > 0)
            {
                if (CombatSystem.Instance != null)
                    CombatSystem.Instance.ApplyDamageToPlayer(player, props.overrideDamage, "BossTile");
                else
                    player.ApplyDamage(props.overrideDamage, "BossTile");
            }

            // If props indicate shuffle or advance, call BoardManager / MovementSystem accordingly
            if (props != null && props.causeShuffleBoard)
            {
                BoardManager bm = BoardManager.Instance != null ? BoardManager.Instance : FindObjectOfType<BoardManager>();
                if (bm != null) bm.ShuffleBoardPositions();
            }
            if (props != null && props.advancePlayerBy > 0)
            {
                int target = Mathf.Min((BoardManager.Instance != null ? BoardManager.Instance.totalTiles : 100), player.TileID + props.advancePlayerBy);
                if (MovementSystem.Instance != null)
                {
                    MovementSystem.Instance.RequestMove(player, target);
                    yield break; // Wait for teleport/movement to finish (turn manager will handle chain)
                }
            }

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // Default: nothing special
        TurnManager.Instance?.NotifyTileResolveComplete(player);
        yield break;
    }

    #region Helpers & detection
    private bool IsAttackTile(Tiles tile, NewTileProperties props)
    {
        if (props != null && props.forceAsAttack) return true;
        // fallback: check tag or name
        if (tile.gameObject.CompareTag("AttackTile")) return true;
        if (tile.gameObject.name.ToLower().Contains("attack")) return true;
        // If tile type is Normal but has metadata, use that
        return false;
    }

    private bool IsNegaTile(Tiles tile)
    {
        // We define NegaTile via tag or name, or via NewTileProperties
        NewTileProperties props = tile.GetComponent<NewTileProperties>();
        if (props != null && props.isNegaTile) return true;
        if (tile.gameObject.CompareTag("NegaTile")) return true;
        if (tile.gameObject.name.ToLower().Contains("nega") || tile.gameObject.name.ToLower().Contains("debuff")) return true;
        return false;
    }

    private bool IsBossTile(Tiles tile, NewTileProperties props)
    {
        if (props != null && props.isBossTile) return true;
        if (tile.gameObject.CompareTag("BossTile")) return true;
        if (tile.gameObject.name.ToLower().Contains("boss")) return true;
        return false;
    }
    #endregion
}

/// <summary>
/// Optional helper component you can attach to Tile GameObjects to specify metadata without changing Tiles.cs.
/// Put this on any Tile to override behaviors (attack damage, boss flags, nega flags, etc).
/// This is optional — TileEffectSystem will fallback to tag / name / Tiles.type.
/// </summary>
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
