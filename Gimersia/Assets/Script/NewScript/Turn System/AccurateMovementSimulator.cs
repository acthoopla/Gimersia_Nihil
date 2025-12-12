using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simulator gerakan yang akurat sesuai dengan logika game sebenarnya
/// - Support gerakan maju dan mundur
/// - Snake/ladder sebagai teleport instant
/// </summary>
public static class AccurateMovementSimulator
{
    /// <summary>
    /// Hitung tile akhir setelah bergerak sejumlah steps (bisa positif/maju atau negatif/mundur)
    /// </summary>
    public static (int finalTile, bool isSnakeLadder, int jumpSourceTile) CalculateFinalTile(
        int startTile,
        int steps,
        BoardManager boardManager)
    {
        if (boardManager == null)
        {
            // Jika steps negatif (mundur), clamp ke minimum 1
            int final = startTile + steps;
            if (final < 1) final = 1;
            if (final > 100) final = 100;
            return (final, false, 0);
        }

        int maxTile = boardManager.totalTiles;

        // Hitung tile pendaratan (support mundur)
        int landingTile = startTile + steps;

        // Clamp ke range 1-100
        if (landingTile < 1) landingTile = 1;
        if (landingTile > maxTile) landingTile = maxTile;

        // Cek apakah tile pendaratan adalah snake/ladder
        Tiles landingTileObj = boardManager.GetTileByID(landingTile);

        if (landingTileObj != null && landingTileObj.targetTile != null)
        {
            // Hanya tile dengan target (snake/ladder) yang mempengaruhi
            // Catatan: Dalam game sebenarnya, mundur ke snake/ladder masih berlaku
            bool isSpecialTile = landingTileObj.type == TileType.LadderStart ||
                                 landingTileObj.type == TileType.SnakeStart;

            if (isSpecialTile)
            {
                return (landingTileObj.targetTile.tileID, true, landingTile);
            }
        }

        return (landingTile, false, 0);
    }

    /// <summary>
    /// Hitung path yang akan dilalui (support mundur)
    /// </summary>
    public static List<int> CalculatePath(int startTile, int steps, BoardManager boardManager)
    {
        List<int> path = new List<int>();
        int current = startTile;
        int maxTile = boardManager?.totalTiles ?? 100;

        path.Add(current);

        if (steps > 0) // Maju
        {
            for (int i = 0; i < steps; i++)
            {
                current++;
                if (current > maxTile)
                {
                    current = maxTile;
                    break;
                }

                path.Add(current);
            }
        }
        else if (steps < 0) // Mundur
        {
            for (int i = 0; i < Mathf.Abs(steps); i++)
            {
                current--;
                if (current < 1)
                {
                    current = 1;
                    break;
                }

                path.Add(current);
            }
        }

        return path;
    }

    /// <summary>
    /// Cek apakah tile adalah danger tile
    /// </summary>
    public static bool IsDangerTile(TileType type)
    {
        return type == TileType.Damage ||
               type == TileType.Attack ||
               type == TileType.SnakeStart ||
               type == TileType.Disarm ||
               type == TileType.Despair ||
               type == TileType.Provocation ||
               type == TileType.Death;
    }

    /// <summary>
    /// Cek apakah tile adalah snake/ladder tile
    /// </summary>
    public static bool IsSnakeLadderTile(TileType type)
    {
        return type == TileType.LadderStart ||
               type == TileType.SnakeStart;
    }

    /// <summary>
    /// Cek apakah tile adalah card tile
    /// </summary>
    public static bool IsCardTile(TileType type)
    {
        return type == TileType.CardRandom ||
               type == TileType.CardMovement ||
               type == TileType.CardBuff;
    }
}