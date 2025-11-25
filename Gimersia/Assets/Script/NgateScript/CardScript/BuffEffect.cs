using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BuffType { Immune, Reflect,Heal }

[CreateAssetMenu(menuName = "Card Effects/Buff Effect")]
public class BuffEffect : CardEffect
{
    public BuffType buffType;

    [Tooltip("Jumlah Heal ATAU Jumlah Stack Immune (Berapa kali bisa nahan damage)")]
    public int value = 1;

    public override void ApplyEffect(PlayerState target)
    {
        switch (buffType)
        {
            case BuffType.Immune:
                // Kita ganti logika durasi menjadi "Stack" (Tumpukan)
                // Jadi immune tidak habis waktu, tapi habis kalau terpukul
                target.AddImmunityStack(value);
                Debug.Log($"[Effect] Immune Stack added: {value}. (Lasts until hit)");
                break;

            case BuffType.Reflect:
                target.SetReflect(value);
                Debug.Log("[Effect] Reflect status active (Reflects 5x Damage).");
                break;

            case BuffType.Heal:
                // Logika Heal baru
                target.Heal(value);
                Debug.Log($"[Effect] Healed player by {value}.");
                break;
        }
        target.NotifyStateChanged();
    }
}