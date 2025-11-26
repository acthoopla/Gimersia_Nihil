using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Card Effects/Movement Effect")]
public class MovementEffect : CardEffect
{
    [Tooltip("Positif = Tambah langkah, Negatif = Kurangi langkah")]
    public int stepAmount;

    public override void ApplyEffect(PlayerState target)
    {
        // LOGIKA LAMA (SALAH):
        // target.pawn.MoveToTile(...) <-- Ini yang bikin jalan 2x

        // LOGIKA BARU (BENAR):
        // Cukup tambahkan angka ke modifier. 
        // Nanti TurnManager yang akan menjumlahkan (Dadu + Modifier) saat fase jalan.
        target.nextRollModifier += stepAmount;

        Debug.Log($"[Effect] Movement Modifier Active: {stepAmount}. Total Modifier: {target.nextRollModifier}");

        // Opsional: Beri tahu UI kalau modifier nambah (biar player sadar efeknya masuk)
        if (UIController.Instance != null)
        {
            string tanda = stepAmount > 0 ? "+" : "";
            UIController.Instance.AddModifierLog($"Dice {tanda}{stepAmount}");
        }
    }
}