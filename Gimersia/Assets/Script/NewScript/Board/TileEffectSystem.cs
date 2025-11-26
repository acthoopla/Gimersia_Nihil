using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static NewCardSystem;

[DisallowMultipleComponent]
public class TileEffectSystem : MonoBehaviour
{
    public static TileEffectSystem Instance { get; private set; }

    [Header("Animation Settings")]
    public GameObject ladderStepPrefab;
    public float ladderDeployHeight = 10f;
    public float ladderDeploySpeed = 15f;
    public float ladderStepDelay = 0.05f;
    public float ladderVerticalOffset = 0.1f;
    public float ladderClimbDuration = 1.5f;

    public GameObject snakeParticle;
    public float snakeAnimationHeight = -2.0f;
    public float snakeAnimationSpeed = 3.0f;

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
    // Ini memodifikasi dadu SEBELUM dikocok
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
        // LOGIC 1: TANGGA
        // =========================================================
        if (tile.type == TileType.LadderStart && tile.targetTile != null)
        {
            yield return StartCoroutine(AnimateLadderSequence(player, tile, tile.targetTile));
            player.TileID = tile.targetTile.tileID;

            // Reward Card Choice
            if (BlessingUIManager.Instance != null && NewCardManager.Instance != null)
            {
                bool choiceMade = false;
                BlessingUIManager.Instance.ShowCategoryChoice((selectedCategory) =>
                {
                    NewCardData reward = NewCardManager.Instance.GetRandomCardByCategory(selectedCategory);
                    if (reward != null) player.TryAddCard(reward);
                    choiceMade = true;
                });
                while (!choiceMade) yield return null;
            }

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 2: ULAR
        // =========================================================
        if (tile.type == TileType.SnakeStart && tile.targetTile != null)
        {
            if (player.immuneToSnakeUses > 0)
            {
                player.immuneToSnakeUses--;
            }
            else
            {
                yield return StartCoroutine(AnimateSnakeSequence(player, tile, tile.targetTile));
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
        // LOGIC 4: ATTACK TILE (Ke Boss)
        // =========================================================
        if (tile.type == TileType.Attack)
        {
            int row = GetRow(tile.tileID);
            int damage = 0;
            if (!rowDamage.TryGetValue(row, out damage)) damage = Mathf.Max(2, row);

            if (CombatSystem.Instance != null && FindObjectOfType<BossState>() != null)
                CombatSystem.Instance.ApplyDamageToBoss(FindObjectOfType<BossState>(), damage, "Tile Attack");

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 5: SPECIAL NEGA TILES (PRIORITAS TINGGI)
        // [FIX] Cek Disarm DISINI sebelum cek Damage biasa
        // =========================================================

        if (tile.type == TileType.Disarm)
        {
            Debug.Log("[TileEffect] DISARM! Membuang 2 kartu acak.");
            player.DiscardRandom(2);

            // Jika mau Disarm juga mengurangi HP, hapus 'yield break' di bawah ini
            // dan biarkan code lanjut ke blok Damage.
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        if (tile.type == TileType.Provocation || tile.type == TileType.Despair)
        {
            // Efek status sudah ditangani di HandleTurnStarted (awal giliran depan).
            // Di sini cuma log visual saja.
            Debug.Log($"[TileEffect] Status {tile.type} aktif untuk giliran depan.");
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        if (tile.type == TileType.Death)
        {
            // Logika kematian sebenarnya sudah di TurnManager (Tile 100), 
            // tapi kalau ada tile Death di tengah jalan:
            player.ApplyDamage(999, "Death Tile");
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 6: GENERIC DAMAGE TILE (Sisa NegaTile)
        // =========================================================
        if (tile.type == TileType.Damage || IsNegaTile(tile))
        {
            int row = GetRow(tile.tileID);
            int damage = (row >= 8) ? 3 : (row >= 4 ? 2 : 1);

            if (CombatSystem.Instance != null) CombatSystem.Instance.ApplyDamageToPlayer(player, damage, "Damage Tile");
            else player.ApplyDamage(damage);

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        TurnManager.Instance?.NotifyTileResolveComplete(player);
    }

    // --- ANIMATIONS & HELPERS (Tetap Sama) ---

    private bool IsNegaTile(Tiles tile)
    {
        return tile.type == TileType.Damage ||
               tile.type == TileType.Disarm ||
               tile.type == TileType.Provocation ||
               tile.type == TileType.Despair;
    }

    private int GetRow(int tileID) => tileID <= 0 ? 1 : ((tileID - 1) / 10) + 1;

    private IEnumerator AnimateLadderSequence(PlayerState player, Tiles startTile, Tiles endTile)
    {
        if (ladderStepPrefab == null) { player.transform.position = endTile.GetPlayerPosition(); yield break; }
        // ... (Kode animasi tangga sama persis seperti sebelumnya) ...
        // Agar hemat tempat saya persingkat, tapi pastikan blok animasi ini ada
        // Kalau hilang, copy dari jawaban sebelumnya.

        List<GameObject> deployedSteps = new List<GameObject>();
        Vector3 startPos = startTile.GetPlayerPosition() + Vector3.up * ladderVerticalOffset;
        Vector3 endPos = endTile.GetPlayerPosition() + Vector3.up * ladderVerticalOffset;
        float dist = Vector3.Distance(startPos, endPos);
        Quaternion rot = Quaternion.LookRotation((endPos - startPos).normalized);
        int steps = Mathf.Max(1, Mathf.RoundToInt(dist));

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

        Vector3 startClimb = player.transform.position; Vector3 endClimb = endTile.GetPlayerPosition();
        float climbTimer = 0f;
        player.transform.LookAt(new Vector3(endClimb.x, player.transform.position.y, endClimb.z));

        while (climbTimer < 1f)
        {
            climbTimer += Time.deltaTime / ladderClimbDuration;
            player.transform.position = Vector3.Lerp(startClimb, endClimb, climbTimer);
            if (player.pawn != null) player.pawn.transform.position = player.transform.position;
            yield return null;
        }
        player.transform.position = endClimb;
        if (player.pawn != null) player.pawn.transform.position = endClimb;
        foreach (var s in deployedSteps) Destroy(s);
    }

    private IEnumerator AnimateSnakeSequence(PlayerState player, Tiles startTile, Tiles endTile)
    {
        // ... (Kode animasi ular sama persis seperti sebelumnya) ...
        Vector3 sPos = startTile.transform.position, ePos = endTile.transform.position, pPos = player.transform.position;
        Vector3 off = new Vector3(0, snakeAnimationHeight, 0);
        if (snakeParticle) Destroy(Instantiate(snakeParticle, pPos, Quaternion.identity), 2f);

        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * snakeAnimationSpeed;
            startTile.transform.position = Vector3.Lerp(sPos, sPos + off, t);
            endTile.transform.position = Vector3.Lerp(ePos, ePos + off, t);
            player.transform.position = Vector3.Lerp(pPos, pPos + off, t);
            yield return null;
        }
        player.transform.position = endTile.GetPlayerPosition() + off;
        yield return new WaitForSeconds(0.5f);
        t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * snakeAnimationSpeed;
            startTile.transform.position = Vector3.Lerp(sPos + off, sPos, t);
            endTile.transform.position = Vector3.Lerp(ePos + off, ePos, t);
            player.transform.position = Vector3.Lerp(player.transform.position, endTile.GetPlayerPosition(), t);
            yield return null;
        }
        startTile.transform.position = sPos; endTile.transform.position = ePos;
        player.transform.position = endTile.GetPlayerPosition();
    }
}