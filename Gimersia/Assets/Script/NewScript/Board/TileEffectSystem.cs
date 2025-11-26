using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static NewCardSystem;

[DisallowMultipleComponent]
public class TileEffectSystem : MonoBehaviour
{
    public static TileEffectSystem Instance { get; private set; }

    [Header("Animation Settings (Ladder)")]
    public GameObject ladderStepPrefab;
    public float ladderDeployHeight = 10f;
    public float ladderDeploySpeed = 15f;
    public float ladderStepDelay = 0.05f;
    public float ladderVerticalOffset = 0.1f;
    public float ladderClimbDuration = 1.5f;

    [Header("Animation Settings (Snake)")]
    public GameObject snakeParticle;
    public float snakeAnimationHeight = -2.0f;
    public float snakeAnimationSpeed = 3.0f;

    [Header("References")]
    public PlayerAnimation playerAnimation;
    public BossAnimation bossAnimation;

    // Mapping damage per baris
    private readonly Dictionary<int, int> rowDamage = new Dictionary<int, int>()
    {
        {1, 1}, {2, 2}, {3, 2}, {4, 3}, {5, 4}, {6, 5}, {7, 6}, {8, 8}, {9, 9}, {10, 10}
    };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnEnable()
    {
        EventBus.OnTileLanded += HandleTileLanded;
        EventBus.OnTurnStarted += HandleTurnStarted;
    }

    void OnDisable()
    {
        EventBus.OnTileLanded -= HandleTileLanded;
        EventBus.OnTurnStarted -= HandleTurnStarted;
    }

    // --- LOGIKA AWAL GILIRAN (Provocation & Despair) ---
    private void HandleTurnStarted(PlayerState player)
    {
        if (BoardManager.Instance == null) return;
        Tiles currentTile = BoardManager.Instance.GetTileByID(player.TileID);

        if (currentTile != null)
        {
            if (currentTile.type == TileType.Provocation)
            {
                Debug.Log($"[TileEffect] Start Bonus! Player di Provocation. Next Roll +2.");
                player.nextRollModifier += 2;
            }
            else if (currentTile.type == TileType.Despair)
            {
                Debug.Log($"[TileEffect] Start Penalty! Player di Despair. Next Roll -2.");
                player.nextRollModifier -= 2;
            }
        }
    }

    private void HandleTileLanded(PlayerState player, Tiles tile)
    {
        StartCoroutine(ResolveTileRoutine(player, tile));
    }

    private IEnumerator ResolveTileRoutine(PlayerState player, Tiles tile)
    {
        if (player == null || tile == null)
        {
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        yield return new WaitForSeconds(0.2f);
        Debug.Log($"[TileEffect] Landed on {tile.tileID} ({tile.type})");

        // 1. Cek Immunity (Semua Tile Jahat)
        if (IsNegaTile(tile) && player.immuneToAllNegativeTurns > 0)
        {
            Debug.Log($"[TileEffect] Player immune.");
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 1: TANGGA (ANIMASI + PILIH HADIAH)
        // =========================================================
        if (tile.type == TileType.LadderStart && tile.targetTile != null)
        {
            // A. Jalankan Animasi Tangga (Original)
            yield return StartCoroutine(AnimateLadderSequence(player, tile, tile.targetTile));

            // B. Update Data Posisi setelah animasi selesai
            player.TileID = tile.targetTile.tileID;

            // C. Reward Card Choice (Fitur Baru)
            if (BlessingUIManager.Instance != null && NewCardManager.Instance != null)
            {
                bool choiceMade = false;
                BlessingUIManager.Instance.ShowCategoryChoice((selectedCategory) =>
                {
                    NewCardData reward = NewCardManager.Instance.GetRandomCardByCategory(selectedCategory);
                    if (reward != null)
                    {
                        if (player.TryAddCard(reward)) Debug.Log($"[Ladder] Got {reward.cardName}");
                        else Debug.LogWarning("[Ladder] Hand Full!");
                    }
                    choiceMade = true;
                });
                while (!choiceMade) yield return null;
            }

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 2: ULAR (ANIMASI ORIGINAL)
        // =========================================================
        if (tile.type == TileType.SnakeStart && tile.targetTile != null)
        {
            if (player.immuneToSnakeUses > 0)
            {
                player.immuneToSnakeUses--;
                Debug.Log("[Snake] Immune Used!");
            }
            else
            {
                // A. Jalankan Animasi Ular (Original)
                yield return StartCoroutine(AnimateSnakeSequence(player, tile, tile.targetTile));

                // B. Update Data Posisi
                player.TileID = tile.targetTile.tileID;
            }
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 3: KARTU INSTAN
        // =========================================================
        NewCardData instantCard = null;
        if (tile.type == TileType.CardMovement) instantCard = NewCardManager.Instance?.GetRandomCardByCategory(CardCategory.Movement);
        else if (tile.type == TileType.CardBuff) instantCard = NewCardManager.Instance?.GetRandomCardByCategory(CardCategory.Buff);
        else if (tile.type == TileType.CardRandom) instantCard = NewCardManager.Instance?.GetRandomCardAny();

        if (instantCard != null)
        {
            player.TryAddCard(instantCard);
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 4: ATTACK TILE
        // =========================================================
        if (tile.type == TileType.Attack)
        {
            int row = GetRow(tile.tileID);
            int damage = 0;

            playerAnimation.PlayAttack();

            // 1. Hitung Damage Dasar
            if (!rowDamage.TryGetValue(row, out damage)) damage = Mathf.Max(2, row);

            // 2. [FIX] CEK DOUBLE EDGE (Kali 2 jika aktif)
            if (player.HasDoubleEdge)
            {
                damage *= 2;
                Debug.Log("[Attack] Double Edge Aktif! Damage ke Boss dikali 2.");
            }

            // 3. Kirim Damage ke Boss
            if (CombatSystem.Instance != null && FindObjectOfType<BossState>() != null)
                CombatSystem.Instance.ApplyDamageToBoss(FindObjectOfType<BossState>(), damage, "Tile Attack");

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 5: DISARM
        // =========================================================
        if (tile.type == TileType.Disarm)
        {
            Debug.Log("[TileEffect] DISARM! Membuang 2 kartu acak.");
            player.DiscardRandom(2);
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 6: PROVOCATION / DESPAIR (Log Visual Only)
        // =========================================================
        if (tile.type == TileType.Provocation || tile.type == TileType.Despair)
        {
            Debug.Log($"[TileEffect] Status {tile.type} aktif untuk giliran depan.");
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 7: GENERIC DAMAGE TILE
        // =========================================================
        if (tile.type == TileType.Damage || IsNegaTile(tile))
        {
            int row = GetRow(tile.tileID);
            int damage = (row >= 8) ? 3 : (row >= 4 ? 2 : 1);

            if (CombatSystem.Instance != null) CombatSystem.Instance.ApplyDamageToPlayer(player, damage, "Damage Tile");
            else player.ApplyDamage(damage);

            if (BossAttackSystem.Instance?.GetDamageForRow(1) >= BossAttackSystem.Instance?.GetDamageForRow(3))
            {
                int random = UnityEngine.Random.Range(0, 2);
                if (random == 0)
                {
                    bossAnimation.PlayAttackOne();
                }
                else if (random == 1)
                {
                    bossAnimation.PlayAttackTwo();
                }
            }
            else if (BossAttackSystem.Instance?.GetDamageForRow(4) >= BossAttackSystem.Instance?.GetDamageForRow(7))
            {
                bossAnimation.PlayAttackThree();
            }
            else if (BossAttackSystem.Instance?.GetDamageForRow(8) >= BossAttackSystem.Instance?.GetDamageForRow(10))
            {
                bossAnimation.PlayAttackFour();
            }

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // Boss & Death
        if (tile.type == TileType.Death)
        {
            player.ApplyDamage(999, "Death Tile");
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        TurnManager.Instance?.NotifyTileResolveComplete(player);
    }

    // --- ANIMATIONS & HELPERS ---

    private bool IsNegaTile(Tiles tile)
    {
        return tile.type == TileType.Damage ||
               tile.type == TileType.Disarm ||
               tile.type == TileType.Provocation ||
               tile.type == TileType.Despair;
    }

    private int GetRow(int tileID) => tileID <= 0 ? 1 : ((tileID - 1) / 10) + 1;

    // --- ANIMASI TANGGA (Original Logic) ---
    private IEnumerator AnimateLadderSequence(PlayerState player, Tiles startTile, Tiles endTile)
    {
        if (ladderStepPrefab == null) { player.transform.position = endTile.GetPlayerPosition(); yield break; }

        List<GameObject> deployedSteps = new List<GameObject>();
        Vector3 startPos = startTile.GetPlayerPosition() + Vector3.up * ladderVerticalOffset;
        Vector3 endPos = endTile.GetPlayerPosition() + Vector3.up * ladderVerticalOffset;
        float dist = Vector3.Distance(startPos, endPos);
        Quaternion rot = Quaternion.LookRotation((endPos - startPos).normalized);
        int steps = Mathf.Max(1, Mathf.RoundToInt(dist));

        // 1. Spawn Anak Tangga
        for (int i = 0; i <= steps; i++)
        {
            Vector3 finalPos = Vector3.Lerp(startPos, endPos, (float)i / steps);
            Vector3 spawnPos = finalPos + Vector3.up * ladderDeployHeight;
            GameObject step = Instantiate(ladderStepPrefab, spawnPos, rot);
            deployedSteps.Add(step);
            float t = 0;
            while (t < 1f) { t += Time.deltaTime * ladderDeploySpeed; step.transform.position = Vector3.Lerp(spawnPos, finalPos, t); yield return null; }
            step.transform.position = finalPos;
            yield return new WaitForSeconds(ladderStepDelay);
        }

        // 2. Player Memanjat (Lerp Position + LookAt)
        Vector3 startClimb = player.transform.position;
        Vector3 endClimb = endTile.GetPlayerPosition();
        float climbTimer = 0f;

        player.transform.LookAt(new Vector3(endClimb.x, player.transform.position.y, endClimb.z));

        while (climbTimer < 1f)
        {
            climbTimer += Time.deltaTime / ladderClimbDuration;
            player.transform.position = Vector3.Lerp(startClimb, endClimb, climbTimer);
            if (player.pawn != null) player.pawn.transform.position = player.transform.position; // Sinkronkan Pawn Visual
            yield return null;
        }

        player.transform.position = endClimb;
        if (player.pawn != null) player.pawn.transform.position = endClimb;

        // 3. Hapus Tangga
        foreach (var s in deployedSteps) Destroy(s);
    }

    // --- ANIMASI ULAR (Original Logic) ---
    private IEnumerator AnimateSnakeSequence(PlayerState player, Tiles startTile, Tiles endTile)
    {
        Vector3 sPos = startTile.transform.position, ePos = endTile.transform.position, pPos = player.transform.position;
        Vector3 off = new Vector3(0, snakeAnimationHeight, 0); // Turun ke bawah tanah
        if (snakeParticle) Destroy(Instantiate(snakeParticle, pPos, Quaternion.identity), 2f);

        // Turun
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * snakeAnimationSpeed;
            startTile.transform.position = Vector3.Lerp(sPos, sPos + off, t); // Optional: Animasi tile turun
            player.transform.position = Vector3.Lerp(pPos, pPos + off, t);
            yield return null;
        }

        // Pindah Posisi (Di bawah tanah)
        player.transform.position = endTile.GetPlayerPosition() + off;
        yield return new WaitForSeconds(0.5f);

        // Naik
        t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * snakeAnimationSpeed;
            endTile.transform.position = Vector3.Lerp(ePos + off, ePos, t); // Optional
            player.transform.position = Vector3.Lerp(player.transform.position, endTile.GetPlayerPosition(), t);
            yield return null;
        }

        // Reset Posisi Tile (Jaga-jaga)
        startTile.transform.position = sPos;
        endTile.transform.position = ePos;
        player.transform.position = endTile.GetPlayerPosition();
    }
}