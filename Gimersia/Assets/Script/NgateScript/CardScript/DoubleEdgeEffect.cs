using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Card Effects/Double Edge Effect")]
public class DoubleEdgeEffect : CardEffect
{
    public override void ApplyEffect(PlayerState target)
    {
        target.HasDoubleEdge = true;
        Debug.Log("[Effect] Double Edge Active: 2x Damage Dealt & Received!");
        target.NotifyStateChanged();
    }
}
