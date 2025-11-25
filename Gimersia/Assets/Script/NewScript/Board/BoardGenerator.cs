using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class BoardGenerator : MonoBehaviour
{
    [Header("References")]
    public BoardManager boardManager;

    [Header("Board Layout")]
    public int tilesPerRow = 10;
    public int ladderCount = 3;
    public int snakeCount = 3;

    [Header("Card Distribution (Per Row)")]
    public int cardRandomPerRow = 1;
    public int cardMovementPerRow = 0;
    public int cardBuffPerRow = 0;

    [Header("Danger Distribution (Per Row)")]
    public int attackPerRow = 2;
    public int damagePerRow = 2;
    public int disarmPerRow = 1;
    public int provocationPerRow = 0;
    public int despairPerRow = 0;

    [Header("Generation Options")]
    public int randomSeed = 0;
    public bool useSeed = false;
    public bool autoGenerateOnStart = false;

    [Header("Special Rules")]
    [Tooltip("Peluang tile Danger menjadi versi Cracked (0.0 - 1.0). Default 0.5 (50%)")]
    public float crackedChance = 0.5f;

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
        // 1. Setup BoardManager
        if (boardManager == null)
        {
            boardManager = FindObjectOfType<BoardManager>();
            if (boardManager == null)
            {
                Debug.LogError("[BoardGenerator] BoardManager tidak ditemukan.");
                return;
            }
        }

        int totalTiles = Mathf.Max(1, boardManager.totalTilesInBoard);
        int rows = Mathf.Max(1, totalTiles / tilesPerRow);
        List<Tiles> allTiles = boardManager.GetAllTilesOrdered();
        int usableTotal = Math.Min(totalTiles, allTiles.Count);

        // 2. Reset All to Normal (Default Base)
        // Loop sampai usableTotal - 1 dulu agar tile terakhir tidak tersentuh normal
        for (int i = 1; i < usableTotal; i++)
        {
            Tiles t = boardManager.GetTileByID(i);
            if (t == null) continue;
            t.targetTile = null;

            bool isCracked = rng.NextDouble() < crackedChance;
            t.SetType(isCracked ? TileType.NormalCracked : TileType.Normal, true);
        }

        // 3. Set Special Fixed Tiles (Start & End)

        // Tile 1: Always Normal
        var tile1 = boardManager.GetTileByID(1);
        if (tile1 != null)
        {
            tile1.targetTile = null;
            tile1.SetType(TileType.Normal, true);
        }

        // Tile 100 (Last): ALWAYS DEATH
        var tileLast = boardManager.GetTileByID(usableTotal);
        if (tileLast != null)
        {
            tileLast.targetTile = null;
            tileLast.SetType(TileType.Death, true);
        }

        // Reserve Start & End so they won't be overwritten
        HashSet<int> reserved = new HashSet<int>();
        reserved.Add(1);
        reserved.Add(usableTotal);

        // 4. Place Ladders
        PlaceLadders(rows, usableTotal, reserved);

        // 5. Place Snakes
        PlaceSnakes(rows, usableTotal, reserved);

        // 6. Fill Rows with Random Content
        for (int r = 1; r <= rows; r++)
        {
            FillRowWithContent(r, usableTotal, reserved);
        }

        // 7. Finalize
        boardManager.BuildLookupFromList();
        foreach (var tile in boardManager.GetAllTilesOrdered())
        {
            if (tile == null) continue;
            tile.UpdateTileNumber();
            tile.UpdateVisualModel();
        }

        Debug.Log($"[BoardGenerator] Generated. Tile {usableTotal} is DEATH.");
    }

    private void PlaceLadders(int rows, int usableTotal, HashSet<int> reserved)
    {
        List<int> ladderFootRows = new List<int>();
        int placed = 0, attempts = 0;
        while (placed < ladderCount && attempts < 1000)
        {
            attempts++;
            int footRow = rng.Next(1, rows);
            if (ladderFootRows.Any(r => Math.Abs(r - footRow) < 3)) continue;

            int topRow = rng.Next(footRow + 1, rows + 1);
            int footID = ((footRow - 1) * tilesPerRow) + rng.Next(1, tilesPerRow + 1);
            int topID = ((topRow - 1) * tilesPerRow) + rng.Next(1, tilesPerRow + 1);

            if (footID <= 1 || topID >= usableTotal) continue; // Pastikan tidak kena tile 100
            if (reserved.Contains(footID) || reserved.Contains(topID)) continue;

            var footT = boardManager.GetTileByID(footID);
            var topT = boardManager.GetTileByID(topID);
            if (footT == null || topT == null) continue;

            footT.targetTile = topT;
            footT.SetType(TileType.LadderStart, true);
            topT.SetType(TileType.LadderEnd, true);

            reserved.Add(footID); reserved.Add(topID);
            ladderFootRows.Add(footRow);
            placed++;
        }
    }

    private void PlaceSnakes(int rows, int usableTotal, HashSet<int> reserved)
    {
        List<int> snakeHeadRows = new List<int>();
        int placed = 0, attempts = 0;
        while (placed < snakeCount && attempts < 1000)
        {
            attempts++;
            int headRow = rng.Next(2, rows + 1);
            if (snakeHeadRows.Any(r => Math.Abs(r - headRow) < 3)) continue;

            int tailRow = rng.Next(1, headRow);
            int headID = ((headRow - 1) * tilesPerRow) + rng.Next(1, tilesPerRow + 1);
            int tailID = ((tailRow - 1) * tilesPerRow) + rng.Next(1, tilesPerRow + 1);

            if (headID >= usableTotal || tailID <= 1) continue; // Pastikan tidak kena tile 100
            if (reserved.Contains(headID) || reserved.Contains(tailID)) continue;

            var headT = boardManager.GetTileByID(headID);
            var tailT = boardManager.GetTileByID(tailID);
            if (headT == null || tailT == null) continue;

            headT.targetTile = tailT;
            headT.SetType(TileType.SnakeStart, true);
            tailT.SetType(TileType.SnakeEnd, true);

            reserved.Add(headID); reserved.Add(tailID);
            snakeHeadRows.Add(headRow);
            placed++;
        }
    }

    private void FillRowWithContent(int row, int usableTotal, HashSet<int> reserved)
    {
        List<int> freeSlots = new List<int>();
        for (int c = 1; c <= tilesPerRow; c++)
        {
            int id = ((row - 1) * tilesPerRow) + c;
            if (id > usableTotal) continue;
            if (!reserved.Contains(id)) freeSlots.Add(id);
        }

        if (freeSlots.Count == 0) return;

        freeSlots = freeSlots.OrderBy(x => rng.Next()).ToList();
        int slotIdx = 0;

        void Place(int count, TileType normalType, TileType crackedType)
        {
            for (int i = 0; i < count && slotIdx < freeSlots.Count; i++)
            {
                int id = freeSlots[slotIdx++];
                var t = boardManager.GetTileByID(id);
                if (t != null)
                {
                    bool isCracked = (crackedType != normalType) && (rng.NextDouble() < crackedChance);
                    t.SetType(isCracked ? crackedType : normalType, true);
                    reserved.Add(id);
                }
            }
        }

        // --- CARDS ---
        Place(cardRandomPerRow, TileType.CardRandom, TileType.CardRandom);
        Place(cardMovementPerRow, TileType.CardMovement, TileType.CardMovement);
        Place(cardBuffPerRow, TileType.CardBuff, TileType.CardBuff);

        // --- DANGERS (Normal / Cracked) ---
        Place(attackPerRow, TileType.Attack, TileType.AttackCracked);
        Place(damagePerRow, TileType.Damage, TileType.DamageCracked);
        Place(disarmPerRow, TileType.Disarm, TileType.DisarmCracked);
        Place(provocationPerRow, TileType.Provocation, TileType.ProvocationCracked);
        Place(despairPerRow, TileType.Despair, TileType.DespairCracked);

        // (Tidak ada pemanggilan Place(Death) disini, jadi Death aman hanya di 100)
    }
}