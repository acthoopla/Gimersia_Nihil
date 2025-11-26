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

        if (targetTile < 1) targetTile = 1;

        if (BoardManager.Instance != null && targetTile > BoardManager.Instance.totalTilesInBoard)
            targetTile = BoardManager.Instance.totalTilesInBoard;

        Debug.Log($"[Effect] Move {stepAmount}. From {currentTile} -> {targetTile}");
        if (target.pawn != null)
        {
            target.pawn.StartCoroutine(target.pawn.MoveToTile(targetTile));
        }
        else
        {
            target.TileID = targetTile;
            target.NotifyStateChanged();
        }
    }
}
