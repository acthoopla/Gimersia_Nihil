using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// BoardGenerator v1.1 - Dengan Batasan Tangga & Ular
/// - Generate board tile types per row (10 tile/row)
/// - Pools per row:
///     * CardPool: 2 slots (random among CardRandom/CardMovement/CardBuff)
///     * Negatile: 2 damage + 1 debuff (disarm/provocation/despair random)
///     * Attack: 3 slots
/// - Snake & Ladder placement dengan batasan tinggi/panjang
/// - Tile #1 forced Normal; last tile forced Damage/Instant-death (TileType.Damage used as instant damage)
/// - Requires BoardManager with GetTileByID, GetAllTilesOrdered, BuildLookupFromList
/// - Requires Tiles.SetType(TileType, bool) and Tiles.SetCracked() + Tiles.UpdateVisualModel()
/// </summary>
[DisallowMultipleComponent]
public class BoardGenerator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag BoardManager instance di scene")]
    public BoardManager boardManager;

    [Header("Board layout")]
    [Tooltip("Tiles per row (fixed 10)")]
    public int tilesPerRow = 10;

    [Tooltip("Total tiles in board (biasanya 100)")]
    public int totalTilesInBoard = 100;

    [Header("Pool per row (defaults sesuai spec)")]
    [Tooltip("Card pool slots per row (CardRandom/CardMovement/CardBuff)")]
    public int cardPoolPerRow = 2;

    [Tooltip("Negatile damage slots per row (damage to player)")]
    public int negatileDamagePerRow = 2;

    [Tooltip("Negatile debuff slots per row (disarm/provocation/despair random)")]
    public int negatileDebuffPerRow = 1;

    [Tooltip("Attack slots per row")]
    public int attackPerRow = 3;

    [Header("Snakes & Ladders - Count")]
    [Tooltip("Number of ladders")]
    public int ladderCount = 3;

    [Tooltip("Number of snakes")]
    public int snakeCount = 3;

    [Header("Ladder Constraints")]
    [Tooltip("Maximum rows a ladder can climb (default 4)")]
    [Range(1, 10)]
    public int maxLadderClimbRows = 4;

    [Tooltip("Minimum rows a ladder must climb (default 2)")]
    [Range(1, 10)]
    public int minLadderClimbRows = 2;

    [Tooltip("Minimum spacing between ladder foot rows (default 3)")]
    [Range(1, 10)]
    public int ladderRowSpacing = 3;

    [Header("Snake Constraints")]
    [Tooltip("Maximum rows a snake can descend (default 4)")]
    [Range(1, 10)]
    public int maxSnakeLengthRows = 4;

    [Tooltip("Minimum rows a snake must descend (default 2)")]
    [Range(1, 10)]
    public int minSnakeLengthRows = 2;

    [Tooltip("Minimum spacing between snake head rows (default 3)")]
    [Range(1, 10)]
    public int snakeRowSpacing = 3;

    [Header("Generation options")]
    public bool useSeed = false;
    public int randomSeed = 12345;
    public bool autoGenerateOnStart = false;

    [Range(0f, 1f)]
    public float crackedChance = 0.05f;

    [Tooltip("Maximum attempts to place each ladder/snake before giving up")]
    public int maxPlacementAttempts = 2000;

    private System.Random rng;

    private void Start()
    {
        if (boardManager == null)
            boardManager = FindObjectOfType<BoardManager>();

        rng = useSeed ? new System.Random(randomSeed) : new System.Random();

        if (autoGenerateOnStart)
            GenerateBoard();
    }

    [ContextMenu("GenerateBoard")]
    public void GenerateBoard()
    {
        if (boardManager == null)
        {
            Debug.LogError("[BoardGenerator] BoardManager not assigned/found.");
            return;
        }

        // Ambil totalTilesInBoard dari BoardManager jika ada
        try
        {
            if (boardManager.totalTilesInBoard > 0)
                totalTilesInBoard = boardManager.totalTilesInBoard;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BoardGenerator] Error reading boardManager.totalTilesInBoard: {e.Message}");
        }

        int rows = Math.Max(1, totalTilesInBoard / tilesPerRow);
        if (rows * tilesPerRow != totalTilesInBoard)
            Debug.LogWarning($"[BoardGenerator] totalTilesInBoard ({totalTilesInBoard}) not divisible by tilesPerRow ({tilesPerRow}); using {rows} rows.");

        var allTiles = boardManager.GetAllTilesOrdered();
        if (allTiles == null || allTiles.Count == 0)
        {
            Debug.LogError("[BoardGenerator] BoardManager has no tiles. Ensure tiles are created & loaded.");
            return;
        }

        int usableTotal = Math.Min(totalTilesInBoard, allTiles.Count);

        // Validasi constraints
        if (minLadderClimbRows > maxLadderClimbRows)
        {
            Debug.LogWarning("[BoardGenerator] minLadderClimbRows > maxLadderClimbRows, swapping values.");
            int temp = minLadderClimbRows;
            minLadderClimbRows = maxLadderClimbRows;
            maxLadderClimbRows = temp;
        }

        if (minSnakeLengthRows > maxSnakeLengthRows)
        {
            Debug.LogWarning("[BoardGenerator] minSnakeLengthRows > maxSnakeLengthRows, swapping values.");
            int temp = minSnakeLengthRows;
            minSnakeLengthRows = maxSnakeLengthRows;
            maxSnakeLengthRows = temp;
        }

        // Reset all tiles to Normal
        for (int id = 1; id <= usableTotal; id++)
        {
            Tiles t = boardManager.GetTileByID(id);
            if (t == null) continue;
            t.targetTile = null;
            t.SetType(TileType.Normal, true);
        }

        // enforce tile 1 normal
        var tile1 = boardManager.GetTileByID(1);
        if (tile1 != null) tile1.SetType(TileType.Normal, true);

        // enforce last tile = instant death
        var tileLast = boardManager.GetTileByID(usableTotal);
        if (tileLast != null) tileLast.SetType(TileType.Death, true);

        // reserved: set of tileIDs already used by snake/ladder or pool placement to avoid overwriting
        HashSet<int> reserved = new HashSet<int>();

        // --- Place ladders dengan batasan tinggi ---
        List<int> ladderFootRows = new List<int>();
        int placedLadders = 0;
        int attempts = 0;

        Debug.Log($"[BoardGenerator] Placing ladders with constraints: Min={minLadderClimbRows}, Max={maxLadderClimbRows}, Spacing={ladderRowSpacing}");

        while (placedLadders < ladderCount && attempts < maxPlacementAttempts)
        {
            attempts++;

            // Foot row harus memiliki ruang untuk naik minimal minLadderClimbRows
            // Foot row maksimal adalah rows - minLadderClimbRows (karena harus bisa naik minimal minLadderClimbRows)
            int maxFootRow = rows - minLadderClimbRows;
            if (maxFootRow < 1)
            {
                Debug.LogWarning("[BoardGenerator] Board too small for ladder constraints. Skipping ladder placement.");
                break;
            }

            int footRow = rng.Next(1, maxFootRow + 1); // foot tidak bisa di last row

            // Spacing rule: jarak minimal antar ladder foot
            if (ladderFootRows.Any(r => Math.Abs(r - footRow) < ladderRowSpacing)) continue;

            // Tentukan range untuk top row berdasarkan batasan
            int minTopRow = footRow + minLadderClimbRows;
            int maxTopRow = Math.Min(rows, footRow + maxLadderClimbRows);

            // Jika tidak ada ruang yang valid, skip
            if (minTopRow > maxTopRow) continue;

            int topRow = rng.Next(minTopRow, maxTopRow + 1);
            int footCol = rng.Next(1, tilesPerRow + 1);
            int topCol = rng.Next(1, tilesPerRow + 1);

            int footID = (footRow - 1) * tilesPerRow + footCol;
            int topID = (topRow - 1) * tilesPerRow + topCol;

            if (!IsValidNormalSlot(footID, usableTotal, reserved)) continue;
            if (!IsValidNormalSlot(topID, usableTotal, reserved)) continue;

            Tiles footTile = boardManager.GetTileByID(footID);
            Tiles topTile = boardManager.GetTileByID(topID);
            if (footTile == null || topTile == null) continue;
            if (footTile.type != TileType.Normal || topTile.type != TileType.Normal) continue;

            footTile.targetTile = topTile;
            footTile.SetType(TileType.LadderStart, true);
            topTile.SetType(TileType.LadderEnd, true);

            reserved.Add(footID);
            reserved.Add(topID);
            ladderFootRows.Add(footRow);
            placedLadders++;

            Debug.Log($"Placed ladder: {footID}(row {footRow}) -> {topID}(row {topRow}), height={topRow - footRow} rows");
        }

        if (placedLadders < ladderCount)
            Debug.LogWarning($"[BoardGenerator] hanya menempatkan {placedLadders}/{ladderCount} ladder(s) setelah {attempts} attempts.");
        else
            Debug.Log($"[BoardGenerator] Berhasil menempatkan semua {ladderCount} ladder(s)");

        // --- Place snakes dengan batasan panjang ---
        List<int> snakeHeadRows = new List<int>();
        int placedSnakes = 0;
        attempts = 0;

        Debug.Log($"[BoardGenerator] Placing snakes with constraints: Min={minSnakeLengthRows}, Max={maxSnakeLengthRows}, Spacing={snakeRowSpacing}");

        while (placedSnakes < snakeCount && attempts < maxPlacementAttempts)
        {
            attempts++;

            // Head row harus memiliki ruang untuk turun minimal minSnakeLengthRows
            // Head row minimal adalah 1 + minSnakeLengthRows (karena harus bisa turun minimal minSnakeLengthRows)
            int minHeadRow = 1 + minSnakeLengthRows;
            if (minHeadRow > rows)
            {
                Debug.LogWarning("[BoardGenerator] Board too small for snake constraints. Skipping snake placement.");
                break;
            }

            int headRow = rng.Next(minHeadRow, rows + 1); // head minimal di row 2 (karena minSnakeLengthRows >= 2)

            // Spacing rule: jarak minimal antar snake head
            if (snakeHeadRows.Any(r => Math.Abs(r - headRow) < snakeRowSpacing)) continue;

            // Tentukan range untuk tail row berdasarkan batasan
            int maxTailRow = headRow - minSnakeLengthRows;
            int minTailRow = Math.Max(1, headRow - maxSnakeLengthRows);

            // Jika tidak ada ruang yang valid, skip
            if (minTailRow > maxTailRow) continue;

            int tailRow = rng.Next(minTailRow, maxTailRow + 1);
            int headCol = rng.Next(1, tilesPerRow + 1);
            int tailCol = rng.Next(1, tilesPerRow + 1);

            int headID = (headRow - 1) * tilesPerRow + headCol;
            int tailID = (tailRow - 1) * tilesPerRow + tailCol;

            if (!IsValidNormalSlot(headID, usableTotal, reserved)) continue;
            if (!IsValidNormalSlot(tailID, usableTotal, reserved)) continue;

            Tiles headTile = boardManager.GetTileByID(headID);
            Tiles tailTile = boardManager.GetTileByID(tailID);
            if (headTile == null || tailTile == null) continue;
            if (headTile.type != TileType.Normal || tailTile.type != TileType.Normal) continue;

            headTile.targetTile = tailTile;
            headTile.SetType(TileType.SnakeStart, true);
            tailTile.SetType(TileType.SnakeEnd, true);

            reserved.Add(headID);
            reserved.Add(tailID);
            snakeHeadRows.Add(headRow);
            placedSnakes++;

            Debug.Log($"Placed snake: {headID}(row {headRow}) -> {tailID}(row {tailRow}), length={headRow - tailRow} rows");
        }

        if (placedSnakes < snakeCount)
            Debug.LogWarning($"[BoardGenerator] hanya menempatkan {placedSnakes}/{snakeCount} snake(s) setelah {attempts} attempts.");
        else
            Debug.Log($"[BoardGenerator] Berhasil menempatkan semua {snakeCount} snake(s)");

        // --- Fill each row with pools (sisa kode tidak berubah) ---
        for (int r = 1; r <= rows; r++)
        {
            List<int> rowIDs = new List<int>();
            for (int c = 1; c <= tilesPerRow; c++)
            {
                int id = (r - 1) * tilesPerRow + c;
                if (id > usableTotal) continue;
                rowIDs.Add(id);
            }

            // free slots = normal & not reserved, excluding tile1 and last tile
            List<int> freeSlots = rowIDs.Where(id =>
            {
                if (id == 1) return false;
                if (id == usableTotal) return false;
                if (reserved.Contains(id)) return false;
                var t = boardManager.GetTileByID(id);
                if (t == null) return false;
                return t.type == TileType.Normal;
            }).ToList();

            int wantCard = cardPoolPerRow;
            int wantDamage = negatileDamagePerRow;
            int wantDebuff = negatileDebuffPerRow;
            int wantAttack = attackPerRow;

            int totalWant = wantCard + wantDamage + wantDebuff + wantAttack;
            if (totalWant > freeSlots.Count)
            {
                int overflow = totalWant - freeSlots.Count;
                // reduce attack first, then damage, then card, then debuff
                while (overflow > 0)
                {
                    if (wantAttack > 0) { wantAttack--; overflow--; continue; }
                    if (wantDamage > 0) { wantDamage--; overflow--; continue; }
                    if (wantCard > 0) { wantCard--; overflow--; continue; }
                    if (wantDebuff > 0) { wantDebuff--; overflow--; continue; }
                    break;
                }
            }

            var shuffled = freeSlots.OrderBy(x => rng.Next()).ToList();
            int idx = 0;

            // Card pool slots (each pick random among CardRandom/CardMovement/CardBuff)
            for (int i = 0; i < wantCard && idx < shuffled.Count; i++, idx++)
            {
                int id = shuffled[idx];
                Tiles tile = boardManager.GetTileByID(id);
                if (tile == null) continue;
                int pick = rng.Next(0, 3);
                if (pick == 0) tile.SetType(TileType.CardRandom, true);
                else if (pick == 1) tile.SetType(TileType.CardMovement, true);
                else tile.SetType(TileType.CardBuff, true);

                if (rng.NextDouble() < crackedChance) tile.SetCracked();
                reserved.Add(id);
            }

            // Damage negatile slots
            for (int i = 0; i < wantDamage && idx < shuffled.Count; i++, idx++)
            {
                int id = shuffled[idx];
                Tiles tile = boardManager.GetTileByID(id);
                if (tile == null) continue;
                tile.SetType(TileType.Damage, true);
                if (rng.NextDouble() < crackedChance) tile.SetCracked();
                reserved.Add(id);
            }

            // Debuff negatile slots (random among Disarm/Provocation/Despair)
            for (int i = 0; i < wantDebuff && idx < shuffled.Count; i++, idx++)
            {
                int id = shuffled[idx];
                Tiles tile = boardManager.GetTileByID(id);
                if (tile == null) continue;
                int pick = rng.Next(0, 3);
                if (pick == 0) tile.SetType(TileType.Disarm, true);
                else if (pick == 1) tile.SetType(TileType.Provocation, true);
                else tile.SetType(TileType.Despair, true);

                if (rng.NextDouble() < crackedChance) tile.SetCracked();
                reserved.Add(id);
            }

            // Attack slots
            for (int i = 0; i < wantAttack && idx < shuffled.Count; i++, idx++)
            {
                int id = shuffled[idx];
                Tiles tile = boardManager.GetTileByID(id);
                if (tile == null) continue;
                tile.SetType(TileType.Attack, true);
                if (rng.NextDouble() < crackedChance) tile.SetCracked();
                reserved.Add(id);
            }

            // leftover remain Normal
        }

        // Rebuild lookup & update visuals
        boardManager.BuildLookupFromList();
        foreach (Tiles t in boardManager.GetAllTilesOrdered())
        {
            if (t == null) continue;
            t.UpdateTileNumber();
            t.UpdateVisualModel();
        }

        // Log statistik akhir
        int totalSpecialTiles = reserved.Count;
        int totalNormalTiles = usableTotal - totalSpecialTiles - 2; // -2 untuk tile 1 dan terakhir
        Debug.Log($"[BoardGenerator] GenerateBoard complete. " +
                  $"Tiles: {usableTotal} total, {totalSpecialTiles} special, {totalNormalTiles} normal, " +
                  $"{placedLadders} ladders, {placedSnakes} snakes");
    }

    // helper: checks if id is a valid normal slot (not tile1, not last, not reserved, and currently Normal)
    private bool IsValidNormalSlot(int id, int usableTotal, HashSet<int> reserved)
    {
        if (id <= 1) return false;
        if (id >= usableTotal) return false;
        if (reserved.Contains(id)) return false;
        Tiles t = boardManager.GetTileByID(id);
        if (t == null) return false;
        return t.type == TileType.Normal;
    }

    [ContextMenu("Validate Constraints")]
    public void ValidateConstraints()
    {
        if (minLadderClimbRows > maxLadderClimbRows)
            Debug.LogError($"Ladder: min ({minLadderClimbRows}) > max ({maxLadderClimbRows})");

        if (minSnakeLengthRows > maxSnakeLengthRows)
            Debug.LogError($"Snake: min ({minSnakeLengthRows}) > max ({maxSnakeLengthRows})");

        if (maxLadderClimbRows > 10)
            Debug.LogWarning($"maxLadderClimbRows ({maxLadderClimbRows}) is quite large for 10-row board");

        if (maxSnakeLengthRows > 10)
            Debug.LogWarning($"maxSnakeLengthRows ({maxSnakeLengthRows}) is quite large for 10-row board");

        Debug.Log("[BoardGenerator] Constraints validation complete.");
    }
}