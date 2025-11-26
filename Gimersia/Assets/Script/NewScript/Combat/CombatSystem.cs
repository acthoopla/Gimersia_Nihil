using System;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class CombatSystem : MonoBehaviour
{
    public static CombatSystem Instance { get; private set; }

    [Header("Settings")]
    public bool verboseLog = true;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    #region Public API

    public void ApplyDamageToPlayer(PlayerState player, int amount, string source = "")
    {
        if (player == null) return;

        // [FIX] Panggil method ApplyDamage di PlayerState langsung
        // Agar logika Reflect, Immune, dan Lose Condition di sana jalan semua.
        player.ApplyDamage(amount, source);
    }

    public void HealPlayer(PlayerState player, int amount, string source = "")
    {
        if (player == null) return;
        player.Heal(amount);
    }

    public void SetPlayerHP(PlayerState player, int hp)
    {
        if (player == null) return;
        player.SetHP(hp);
    }

    public void ApplyDamageToBoss(BossState boss, int amount, string source = "")
    {
        if (boss == null) return;

        // [FIX] Panggil TakeDamage di BossState agar Win Condition jalan
        boss.TakeDamage(amount);

        if (verboseLog) Debug.Log($"[CombatSystem] Boss took {amount} dmg from {source}.");
    }

    #endregion
}