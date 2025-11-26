using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    // --- TAMBAHKAN INI ---
    [Tooltip("Waktu (detik) yang dibutuhkan player untuk manjat/slide ke atas")]
    public float ladderClimbDuration = 1.5f;

    [Header("Animation Settings (Snake)")]
    public GameObject snakeParticle;
    public float snakeAnimationHeight = -2.0f;
    public float snakeAnimationSpeed = 3.0f;

    void Awake() { if (Instance == null) Instance = this; }
    void OnEnable() 
    { 
        EventBus.OnTileLanded += HandleTileLanded;
        EventBus.OnTurnStarted += HandleTurnStarted;
    }
    void OnDisable() 
    { 
        EventBus.OnTileLanded -= HandleTileLanded;
        EventBus.OnTurnStarted -= HandleTurnStarted; // <--- TAMBAH INI (Biar gak memori leak)
    }

    // --- LOGIKA BARU: DITAMBAHKAN MANUAL ---
    private void HandleTurnStarted(PlayerState player)
    {
        // Pastikan BoardManager ada
        if (BoardManager.Instance == null) return;

        // 1. Ambil Data Tile tempat player berdiri SAAT INI
        Tiles currentTile = BoardManager.Instance.GetTileByID(player.TileID);

        if (currentTile != null)
        {
            // 2. Cek Provocation (Hanya tipe biasa)
            if (currentTile.type == TileType.Provocation)
            {
                Debug.Log($"[TileEffect] Start Bonus! Player berdiri di Provocation. Next Roll +2.");
                player.nextRollModifier += 2;
            }
            // 3. Cek Despair (Hanya tipe biasa)
            else if (currentTile.type == TileType.Despair)
            {
                Debug.Log($"[TileEffect] Start Penalty! Player berdiri di Despair. Next Roll -2.");
                player.nextRollModifier -= 2;
            }
        }
    } 
    // ---------------------------------------

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
        Debug.Log($"[TileEffect] Player di Tile {tile.tileID} ({tile.type})");

        // --- 1. TANGGA ---
        if (tile.type == TileType.LadderStart)
        {
            if (tile.targetTile != null)
            {
                yield return StartCoroutine(AnimateLadderSequence(player, tile, tile.targetTile));
                player.TileID = tile.targetTile.tileID;
                // Logic hadiah tangga dimatikan sementara
            }
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // --- 2. ULAR ---
        if (tile.type == TileType.SnakeStart)
        {
            if (tile.targetTile != null)
            {
                if (player.immuneToSnakeUses > 0)
                {
                    player.immuneToSnakeUses--;
                    Debug.Log("Player kebal ular!");
                }
                else
                {
                    yield return StartCoroutine(AnimateSnakeSequence(player, tile, tile.targetTile));
                    player.TileID = tile.targetTile.tileID;
                }
            }
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // --- 3. ATTACK ---
        if (tile.type == TileType.Attack)
        {
            int row = GetRow(tile.tileID);
            int damage = Mathf.Max(2, row);
            Debug.Log($"[Attack] Row {row} -> {damage} Damage to Boss");

            if (CombatSystem.Instance != null && FindObjectOfType<BossState>() != null)
                CombatSystem.Instance.ApplyDamageToBoss(FindObjectOfType<BossState>(), damage, "Tile Attack");

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // --- 4. DAMAGE (Danger 01) & NEGA ---
        if (tile.type == TileType.Damage)
        {
            int row = GetRow(tile.tileID);
            int damage = (row >= 8) ? 3 : (row >= 4 ? 2 : 1);
            Debug.Log($"[Damage/Nega] Row {row} -> {damage} Damage to Player");

            if (CombatSystem.Instance != null)
                CombatSystem.Instance.ApplyDamageToPlayer(player, damage, "Damage Tile");
            else
                player.ApplyDamage(damage);

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // --- 5. DISARM (Danger 02) ---
        // --- 5. SPECIAL DANGERS ---
        if (tile.type == TileType.Disarm || tile.type == TileType.Provocation || tile.type == TileType.Despair)
        {
            // Hanya Log saja, tidak ada aksi fisik saat mendarat
            Debug.Log($"[Special Danger] Mendarat di {tile.type}. Efek aktif di awal giliran depan!");

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // --- 6. NEW CARD TILES (Fixed Error) ---
        if (tile.type == TileType.CardRandom || tile.type == TileType.CardMovement || tile.type == TileType.CardBuff)
        {
            // Logic dimatikan dulu biar gak error CS1061 di NewCardManager
            Debug.Log($"[Card Tile] Mendarat di {tile.type}. (Logic Kartu Disabled)");

            /* // Nanti jika NewCardManager sudah siap, uncomment ini:
            if (NewCardManager.Instance != null) {
                if (tile.type == TileType.CardRandom) NewCardManager.Instance.GetRandomCardAny();
                // dst...
            }
            */

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // --- 7. DEATH & LAINNYA ---
        if (tile.type == TileType.Death || tile.type == TileType.Despair || tile.type == TileType.Provocation)
        {
            Debug.Log($"[Special Tile] {tile.type} triggered (No Logic yet).");
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // NORMAL / FALLBACK
        TurnManager.Instance?.NotifyTileResolveComplete(player);
    }

    // --- ANIMATIONS ---
    private IEnumerator AnimateLadderSequence(PlayerState player, Tiles startTile, Tiles endTile)
    {
        if (ladderStepPrefab == null)
        {
            player.transform.position = endTile.GetPlayerPosition();
            yield break;
        }

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
        yield return new WaitForSeconds(0.2f);

        // --- MULAI LOGIKA SLIDE ---
        Vector3 startClimb = player.transform.position;
        Vector3 endClimb = endTile.GetPlayerPosition();
        float climbTimer = 0f;

        // Opsional: Hadapkan player ke arah tangga
        player.transform.LookAt(new Vector3(endClimb.x, player.transform.position.y, endClimb.z));

        // Loop gerak halus (Lerp)
        while (climbTimer < 1f)
        {
            climbTimer += Time.deltaTime / ladderClimbDuration;

            Vector3 nextPos = Vector3.Lerp(startClimb, endClimb, climbTimer);

            // Update posisi
            player.transform.position = nextPos;
            if (player.pawn != null) player.pawn.transform.position = nextPos;

            yield return null;
        }

        // Pastikan posisi akhir pas
        player.transform.position = endClimb;
        if (player.pawn != null) player.pawn.transform.position = endClimb;
        // --- SELESAI LOGIKA SLIDE ---

        yield return new WaitForSeconds(0.5f);
        foreach (var s in deployedSteps) Destroy(s);
    }

    private IEnumerator AnimateSnakeSequence(PlayerState player, Tiles startTile, Tiles endTile)
    {
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

    private int GetRow(int tileID) => tileID <= 0 ? 1 : ((tileID - 1) / 10) + 1;
}