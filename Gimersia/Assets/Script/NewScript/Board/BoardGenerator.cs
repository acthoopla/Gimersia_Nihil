using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// BoardGenerator
/// - Generate tile types per-row (10 tiles per row) berdasarkan aturan user:
///   * per-row distribution (contoh: 2 blessing, 3 attack, 3 damage, 2 normal) — configurable per row
///   * snake/ladder placement configurable (default 3 each)
///   * spacing rule: antara dua snake-head (atau dua ladder-foot) minimal 3 row
///   * tile #1 always Normal; tile #total always InstantDeath (TileType specific)
/// - Overwrites Tiles.type dan Tiles.targetTile (untuk snake/ladder).
/// - Setelah generate, memanggil BoardManager.BuildLookupFromList() dan mereset visual Tiles.
/// </summary>
[DisallowMultipleComponent]
public class BoardGenerator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("BoardManager instance yang ada di scene")]
    public BoardManager boardManager; // akan dipakai untuk mengambil tiles list & lookup

    [Header("Board layout")]
    public int tilesPerRow = 10; // fixed 10 sesuai request
    [Tooltip("Jumlah ladder yang akan dibuat (default 3)")]
    public int ladderCount = 3;
    [Tooltip("Jumlah snake yang akan dibuat (default 3)")]
    public int snakeCount = 3;

    [Header("Per-row distribution (jumlah per row, total harus = tilesPerRow jika tidak ada snake/ladder di row)")]
    [Tooltip("Defaults: 2 blessing, 3 attack, 3 damage, remaining normal")]
    public int blessingPerRow = 2;
    public int attackPerRow = 3;
    public int damagePerRow = 3;
    // Normal computed as tilesPerRow - (others + reservedSpecialSlots)

    [Header("Generation options")]
    public int randomSeed = 0;
    public bool useSeed = false;
    [Tooltip("Auto generate di Start (false jika mau generate manual via Context Menu)")]
    public bool autoGenerateOnStart = false;

    private System.Random rng;

    void Start()
    {
        if (useSeed) rng = new System.Random(randomSeed);
        else rng = new System.Random();

        if (autoGenerateOnStart) GenerateBoard();
    }

    [ContextMenu("GenerateBoard")]
    public void GenerateBoard()
    {
        if (boardManager == null)
        {
            boardManager = FindObjectOfType<BoardManager>();
            if (boardManager == null)
            {
                Debug.LogError("[BoardGenerator] BoardManager tidak ditemukan di scene.");
                return;
            }
        }

        // safety checks
        int totalTiles = Mathf.Max(1, boardManager.totalTilesInBoard);
        if (totalTiles % tilesPerRow != 0)
        {
            Debug.LogWarning("[BoardGenerator] totalTilesInBoard tidak kelipatan tilesPerRow. Adjusting rows by floor.");
        }
        int rows = Mathf.Max(1, totalTiles / tilesPerRow);

        // Grab ordered tiles (by tileID) from BoardManager
        List<Tiles> allTiles = boardManager.GetAllTilesOrdered();
        if (allTiles == null || allTiles.Count == 0)
        {
            Debug.LogError("[BoardGenerator] BoardManager tiles list kosong. Pastikan tiles sudah ada di scene dan BoardManager.LoadTilesFromScene() dipanggil.");
            return;
        }

        // ensure we have exactly totalTiles elements ordered by tileID 1..totalTiles
        // We'll operate only on tileIDs 1..min(totalTiles, allTiles.Count)
        int usableTotal = Math.Min(totalTiles, allTiles.Count);

        // Reset all tiles to Normal first (except we will mark tile100 later)
        for (int i = 1; i <= usableTotal; i++)
        {
            Tiles t = boardManager.GetTileByID(i);
            if (t == null) continue;
            t.targetTile = null;
            t.SetType(TileType.Normal, fromScript: true);
        }

        // Enforce tile #1 normal
        var tile1 = boardManager.GetTileByID(1);
        if (tile1 != null) tile1.SetType(TileType.Normal, true);

        // Enforce tile #total => instant death (use Nega or special tag)
        var tileLast = boardManager.GetTileByID(usableTotal);
        if (tileLast != null)
        {
            // Use Nega as instant-death semantic or you can set custom type if you added it
            tileLast.SetType(TileType.Nega, true);
            // optionally attach NewTileProperties with overrideDamage large / instant death logic
        }

        // Keep track of reserved positions (tileIDs) for snake heads/tails/ladder feet/ends
        HashSet<int> reserved = new HashSet<int>();

        // 1) Place ladders
        List<int> ladderFootRows = new List<int>(); // to enforce spacing rule between ladder feet (by row)
        int placedLadders = 0;
        int ladderAttempts = 0;
        while (placedLadders < ladderCount && ladderAttempts < 1000)
        {
            ladderAttempts++;
            // choose foot row (1..rows-1) ; foot cannot be last row
            int footRow = rng.Next(1, rows); // row index 1-based
            // spacing: must be at least 3 rows away from existing ladderFootRows
            if (ladderFootRows.Any(r => Math.Abs(r - footRow) < 3)) continue;

            // choose topRow > footRow
            int topRow = rng.Next(footRow + 1, rows + 1);

            // choose column positions (1..tilesPerRow)
            int footCol = rng.Next(1, tilesPerRow + 1);
            int topCol = rng.Next(1, tilesPerRow + 1);

            int footID = ((footRow - 1) * tilesPerRow) + footCol;
            int topID = ((topRow - 1) * tilesPerRow) + topCol;

            // invalid IDs or reserved or special tiles (#1/#total)
            if (footID <= 1 || topID >= usableTotal) continue;
            if (reserved.Contains(footID) || reserved.Contains(topID)) continue;

            var footTile = boardManager.GetTileByID(footID);
            var topTile = boardManager.GetTileByID(topID);
            if (footTile == null || topTile == null) continue;

            // avoid placing ladder onto a tile that's already a snake head/foot etc
            if (footTile.type != TileType.Normal || topTile.type != TileType.Normal) continue;

            // commit
            footTile.targetTile = topTile;
            footTile.SetType(TileType.LadderStart, fromScript: true);
            topTile.SetType(TileType.LadderEnd, fromScript: true);

            reserved.Add(footID);
            reserved.Add(topID);
            ladderFootRows.Add(footRow);
            placedLadders++;
        }
        if (placedLadders < ladderCount)
            Debug.LogWarning($"[BoardGenerator] Hanya berhasil menempatkan {placedLadders}/{ladderCount} ladder(s).");

        // 2) Place snakes
        List<int> snakeHeadRows = new List<int>();
        int placedSnakes = 0;
        int snakeAttempts = 0;
        while (placedSnakes < snakeCount && snakeAttempts < 1000)
        {
            snakeAttempts++;
            // choose head row (2..rows) because head must be above tail
            int headRow = rng.Next(2, rows + 1);
            // enforce spacing: at least 3 rows away from previous heads
            if (snakeHeadRows.Any(r => Math.Abs(r - headRow) < 3)) continue;

            // choose tail row < headRow
            int tailRow = rng.Next(1, headRow); // 1..headRow-1

            // choose columns
            int headCol = rng.Next(1, tilesPerRow + 1);
            int tailCol = rng.Next(1, tilesPerRow + 1);

            int headID = ((headRow - 1) * tilesPerRow) + headCol;
            int tailID = ((tailRow - 1) * tilesPerRow) + tailCol;

            // invalid
            if (headID <= 1 || tailID <= 1 || headID >= usableTotal || tailID >= usableTotal) continue;
            if (reserved.Contains(headID) || reserved.Contains(tailID)) continue;

            var headTile = boardManager.GetTileByID(headID);
            var tailTile = boardManager.GetTileByID(tailID);
            if (headTile == null || tailTile == null) continue;
            if (headTile.type != TileType.Normal || tailTile.type != TileType.Normal) continue;

            // commit
            headTile.targetTile = tailTile;
            headTile.SetType(TileType.SnakeStart, fromScript: true);
            tailTile.SetType(TileType.SnakeEnd, fromScript: true);

            reserved.Add(headID);
            reserved.Add(tailID);
            snakeHeadRows.Add(headRow);
            placedSnakes++;
        }
        if (placedSnakes < snakeCount)
            Debug.LogWarning($"[BoardGenerator] Hanya berhasil menempatkan {placedSnakes}/{snakeCount} snake(s).");

        // 3) For each row, fill remaining (non-reserved) tiles with distribution:
        for (int r = 1; r <= rows; r++)
        {
            // collect tileIDs in row
            List<int> rowTileIDs = new List<int>();
            for (int c = 1; c <= tilesPerRow; c++)
            {
                int id = ((r - 1) * tilesPerRow) + c;
                if (id > usableTotal) continue;
                rowTileIDs.Add(id);
            }

            // skip if empty row
            if (rowTileIDs.Count == 0) continue;

            // build candidate slots (exclude reserved tiles like snake/ladder parts and #1 and #total)
            List<int> freeSlots = rowTileIDs.Where(id =>
            {
                if (reserved.Contains(id)) return false;
                if (id == 1) return false;
                if (id == usableTotal) return false;
                var t = boardManager.GetTileByID(id);
                if (t == null) return false;
                return t.type == TileType.Normal; // only fill normal tiles
            }).ToList();

            // compute how many special tiles to place this row
            int wantBless = blessingPerRow;
            int wantAttack = attackPerRow;
            int wantDamage = damagePerRow;

            // clamp totals if not enough free slots
            int totalWant = wantBless + wantAttack + wantDamage;
            if (totalWant > freeSlots.Count)
            {
                // scale down proportionally (simple greedy reduce)
                int overflow = totalWant - freeSlots.Count;
                // reduce attack then damage then blessing
                while (overflow > 0)
                {
                    if (wantAttack > 0) { wantAttack--; overflow--; continue; }
                    if (wantDamage > 0) { wantDamage--; overflow--; continue; }
                    if (wantBless > 0) { wantBless--; overflow--; continue; }
                    break;
                }
            }

            // shuffle freeSlots indices
            var shuffled = freeSlots.OrderBy(x => rng.Next()).ToList();

            int idx = 0;
            // place blessing
            for (int i = 0; i < wantBless && idx < shuffled.Count; i++, idx++)
            {
                var id = shuffled[idx];
                var tile = boardManager.GetTileByID(id);
                if (tile != null)
                {
                    tile.SetType(TileType.BlessingCard, fromScript: true);
                    reserved.Add(id);
                }
            }
            // place attack
            for (int i = 0; i < wantAttack && idx < shuffled.Count; i++, idx++)
            {
                var id = shuffled[idx];
                var tile = boardManager.GetTileByID(id);
                if (tile != null)
                {
                    tile.SetType(TileType.Attack, fromScript: true);
                    reserved.Add(id);
                }
            }
            // place damage (nega)
            for (int i = 0; i < wantDamage && idx < shuffled.Count; i++, idx++)
            {
                var id = shuffled[idx];
                var tile = boardManager.GetTileByID(id);
                if (tile != null)
                {
                    tile.SetType(TileType.Nega, fromScript: true);
                    reserved.Add(id);
                }
            }

            // remaining free slots remain Normal (already set)
        }

        // Finally: update visuals and rebuild lookup
        // BoardManager.BuildLookupFromList() will use tile.tileID uniqueness; don't change IDs here.
        boardManager.BuildLookupFromList();

        // Update visuals for each tile (Tiles.OnValidate/Start updates visuals but call explicit to be safe)
        foreach (var tile in boardManager.GetAllTilesOrdered())
        {
            if (tile == null) continue;
            tile.UpdateTileNumber();
            tile.UpdateVisualModel();
        }

        Debug.Log("[BoardGenerator] Generation complete.");
    }
}
