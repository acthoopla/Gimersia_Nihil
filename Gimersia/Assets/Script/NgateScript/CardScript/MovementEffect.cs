using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Card Effects/Movement Effect")]
public class MovementEffect : CardEffect
{
    [Tooltip("Positif = Maju, Negatif = Mundur")]
    public int stepAmount;

    public override void ApplyEffect(PlayerState target)
    {
        int currentTile = target.TileID;
        int targetTile = currentTile + stepAmount;

        // Validasi batas minimum tile (misal tile 1)
        if (targetTile < 1) targetTile = 1;
        // Validasi batas max tile bisa diambil dari BoardManager jika ada

        Debug.Log($"[Effect] Move {stepAmount} steps. From {currentTile} to {targetTile}");

        // Update Data
        target.TileID = targetTile;
        target.NotifyStateChanged();

        // Trigger Visual Pawn
        if (target.pawn != null)
        {
            target.pawn.StartCoroutine(target.pawn.MoveToTile(targetTile));
        }
    }
}
