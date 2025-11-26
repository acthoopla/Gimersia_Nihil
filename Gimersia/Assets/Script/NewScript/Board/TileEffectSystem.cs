using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// Pastikan namespace NewCardSystem ada atau sesuaikan
using static NewCardSystem; 

[DisallowMultipleComponent]
public class TileEffectSystem : MonoBehaviour
{
    public static TileEffectSystem Instance { get; private set; }

    [Header("Animation Settings (User Logic)")]
    public GameObject ladderStepPrefab;
    public float ladderDeployHeight = 10f;
    public float ladderDeploySpeed = 15f;
    public float ladderStepDelay = 0.05f;
    public float ladderVerticalOffset = 0.1f;
    [Tooltip("Waktu (detik) yang dibutuhkan player untuk manjat/slide ke atas")]
    public float ladderClimbDuration = 1.5f;

    [Header("Animation Settings (Snake)")]
    public GameObject snakeParticle;
    public float snakeAnimationHeight = -2.0f;
    public float snakeAnimationSpeed = 3.0f;

    [Header("Data Settings (Friend Logic)")]
    // Mapping damage per baris (Row 1-10) - Optimasi Dictionary
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
        EventBus.OnTurnStarted += HandleTurnStarted; // [MERGE] Penting untuk Provocation/Despair
    }

    void OnDisable() 
    { 
        EventBus.OnTileLanded -= HandleTileLanded;
        EventBus.OnTurnStarted -= HandleTurnStarted;
    }

    // --- [MERGE] LOGIKA AWAL GILIRAN (PUNYAMU) ---
    private void HandleTurnStarted(PlayerState player)
    {
        if (BoardManager.Instance == null) return;
        Tiles currentTile = BoardManager.Instance.GetTileByID(player.TileID);

        if (currentTile != null)
        {
            if (currentTile.type == TileType.Provocation)
            {
                Debug.Log($"[TileEffect] Start Bonus! Player berdiri di Provocation. Next Roll +2.");
                player.nextRollModifier += 2;
            }
            else if (currentTile.type == TileType.Despair)
            {
                Debug.Log($"[TileEffect] Start Penalty! Player berdiri di Despair. Next Roll -2.");
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
        Debug.Log($"[TileEffect] {player.gameObject.name} landed on Tile {tile.tileID} ({tile.type})");

        // [MERGE] Ambil Properties dari Teman
        NewTileProperties props = tile.GetComponent<NewTileProperties>();

        // 1. Cek Immunity (Negative Tiles)
        if (IsNegaTile(tile, props) && player.immuneToAllNegativeTurns > 0)
        {
            Debug.Log($"[TileEffect] Player immune to negative tile.");
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 1: TANGGA (ANIMASI USER -> REWARD TEMAN)
        // =========================================================
        if (tile.type == TileType.LadderStart && tile.targetTile != null)
        {
            // A. Jalankan Animasi (Punyamu)
            yield return StartCoroutine(AnimateLadderSequence(player, tile, tile.targetTile));
            
            // Update posisi logic setelah animasi selesai
            player.TileID = tile.targetTile.tileID;

            // B. Logic Hadiah (Punya Teman) - Card/Buff Choice
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
        // LOGIC 2: ULAR (ANIMASI USER -> LOGIC TEMAN)
        // =========================================================
        if (tile.type == TileType.SnakeStart && tile.targetTile != null)
        {
            if (player.immuneToSnakeUses > 0)
            {
                player.immuneToSnakeUses--;
                Debug.Log("Player kebal ular!");
            }
            else
            {
                // A. Jalankan Animasi Turun (Punyamu)
                yield return StartCoroutine(AnimateSnakeSequence(player, tile, tile.targetTile));
                player.TileID = tile.targetTile.tileID;
            }
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 3: KARTU INSTAN (LOGIC TEMAN)
        // =========================================================
        NewCardData instantCard = null;
        if (tile.type == TileType.CardMovement) instantCard = NewCardManager.Instance?.GetRandomCardByCategory(CardCategory.Movement);
        else if (tile.type == TileType.CardBuff) instantCard = NewCardManager.Instance?.GetRandomCardByCategory(CardCategory.Buff);
        else if (tile.type == TileType.CardRandom) instantCard = NewCardManager.Instance?.GetRandomCardAny();

        if (instantCard != null)
        {
            Debug.Log($"[CardTile] Dapat kartu: {instantCard.cardName}");
            player.TryAddCard(instantCard);
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // =========================================================
        // LOGIC 4: ATTACK & DAMAGE (GABUNGAN)
        // =========================================================
        
        // Attack Tile
        if (IsAttackTile(tile, props))
        {
            int damage = 0;
            // Prioritas: Override Props > Rumus Row Dictionary > Rumus Row Manual
            if (props != null && props.overrideDamage > 0) damage = props.overrideDamage;
            else
            {
                int row = GetRow(tile.tileID);
                if (!rowDamage.TryGetValue(row, out damage)) damage = Mathf.Max(2, row); // Fallback ke logika lama
            }

            if (CombatSystem.Instance != null && FindObjectOfType<BossState>() != null)
                 CombatSystem.Instance.ApplyDamageToBoss(FindObjectOfType<BossState>(), damage, "Tile Attack");

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // Nega/Damage Tile
        if (IsNegaTile(tile, props))
        {
            int damage = 0;
            if (props != null && props.overrideDamage > 0) damage = props.overrideDamage;
            else
            {
                int row = GetRow(tile.tileID);
                damage = (row >= 8) ? 3 : (row >= 4 ? 2 : 1); // Logika Punyamu
            }

            // Random Effect (Punya Teman) jika Nega Tile khusus
            if (props != null && props.isNegaTile)
            {
                 int choice = UnityEngine.Random.Range(0, 3);
                 if(choice == 0) player.DiscardRandom(2);
                 else if(choice == 1) player.nextRollModifier += 2;
                 else if(choice == 2) player.nextRollModifier -= 2;
            }

            if (CombatSystem.Instance != null) CombatSystem.Instance.ApplyDamageToPlayer(player, damage, "Damage Tile");
            else player.ApplyDamage(damage);

            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // Special Dangers (Disarm/Provocation/Despair) - Log Only
        if (tile.type == TileType.Disarm || tile.type == TileType.Provocation || tile.type == TileType.Despair)
        {
            Debug.Log($"[Special Danger] Efek {tile.type} aktif di awal giliran depan!");
            TurnManager.Instance?.NotifyTileResolveComplete(player);
            yield break;
        }

        // Boss Logic (Friend)
        if (IsBossTile(tile, props))
        {
             if (props != null && props.overrideDamage > 0) player.ApplyDamage(props.overrideDamage, "BossTile");
             if (props != null && props.causeShuffleBoard && BoardManager.Instance != null) BoardManager.Instance.ShuffleBoardPositions();
        }

        TurnManager.Instance?.NotifyTileResolveComplete(player);
    }

    // --- ANIMATIONS (PUNYAMU: DIPERTAHANKAN) ---
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

        // Animasi Anak Tangga Muncul
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

        // Animasi Player Naik
        Vector3 startClimb = player.transform.position;
        Vector3 endClimb = endTile.GetPlayerPosition();
        float climbTimer = 0f;
        player.transform.LookAt(new Vector3(endClimb.x, player.transform.position.y, endClimb.z));

        while (climbTimer < 1f)
        {
            climbTimer += Time.deltaTime / ladderClimbDuration;
            Vector3 nextPos = Vector3.Lerp(startClimb, endClimb, climbTimer);
            player.transform.position = nextPos;
            if (player.pawn != null) player.pawn.transform.position = nextPos;
            yield return null;
        }

        player.transform.position = endClimb;
        if (player.pawn != null) player.pawn.transform.position = endClimb;

        yield return new WaitForSeconds(0.5f);
        foreach (var s in deployedSteps) Destroy(s);
    }

    private IEnumerator AnimateSnakeSequence(PlayerState player, Tiles startTile, Tiles endTile)
    {
        Vector3 sPos = startTile.transform.position, ePos = endTile.transform.position, pPos = player.transform.position;
        Vector3 off = new Vector3(0, snakeAnimationHeight, 0);
        if (snakeParticle) Destroy(Instantiate(snakeParticle, pPos, Quaternion.identity), 2f);

        float t = 0;
        // Turun
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
        
        // Naik Balik (Tile saja)
        t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * snakeAnimationSpeed;
            startTile.transform.position = Vector3.Lerp(sPos + off, sPos, t);
            endTile.transform.position = Vector3.Lerp(ePos + off, ePos, t);
            // Player ikut naik ke surface tile tujuan
            player.transform.position = Vector3.Lerp(player.transform.position, endTile.GetPlayerPosition(), t);
            yield return null;
        }
        startTile.transform.position = sPos; endTile.transform.position = ePos;
        player.transform.position = endTile.GetPlayerPosition();
    }

    // --- HELPERS (MERGED) ---
    private int GetRow(int tileID) => tileID <= 0 ? 1 : ((tileID - 1) / 10) + 1;

    private bool IsAttackTile(Tiles tile, NewTileProperties props)
    {
        // Cek Enum Native
        if (tile.type == TileType.Attack) return true;
        // Cek Override dari Teman
        if (props != null && props.forceAsAttack) return true;

        return false;
    }

    private bool IsNegaTile(Tiles tile, NewTileProperties props)
    {
        // 1. Cek Enum (Hardcoded List)
        // Saran: Jika nanti nambah tipe negatif baru, masukkan ke sini
        if (tile.type == TileType.Damage ||
            tile.type == TileType.Disarm ||
            tile.type == TileType.Provocation ||
            tile.type == TileType.Despair)
        {
            return true;
        }

        // 2. Cek Override dari Teman
        if (props != null && props.isNegaTile) return true;

        return false;
    }

    private bool IsBossTile(Tiles tile, NewTileProperties props)
    {
        // 1. Cek Enum
        if (tile.type == TileType.Death) return true;

        // 2. Cek Override dari Teman
        if (props != null && props.isBossTile) return true;

        // [DIHAPUS] if (tile.gameObject.CompareTag("BossTile")) return true; 
        // Alasannya: Menyebabkan crash jika Tag tidak terdaftar di Project Settings.

        return false;
    }
}

// [MERGE] Script Tambahan dari Temanmu (biar tidak error CS0246)
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