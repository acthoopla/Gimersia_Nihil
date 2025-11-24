using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameCard : BaseCard
{
    // Fungsi ini dipanggil saat kartu 'dieksekusi' (misal oleh TurnManager)
    public override void Use()
    {
        if (CanUse())
        {
            // PANGGIL LOGIC SRP DISINI
            cardData.Play(owner);
        }
    }

    // Dipanggil oleh CardVisual saat animasi 'Use' dimulai
    public override void OnPreUse()
    {
        // Bisa tambah SFX disini
        Debug.Log($"Visual: Kartu {GetCardName()} terbang!");
    }
}
