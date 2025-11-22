using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// CombatSystem (SRP)
/// - Singleton
/// - ApplyDamageToPlayer / Heal / SetHP
/// - Tidak menggunakan 'dynamic' (menggunakan reflection safe)
/// - Memanggil EventBus via reflection-friendly helpers
/// </summary>
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

    /// <summary>
    /// Apply damage to a player (PlayerState). Uses defense-from-cards if present.
    /// </summary>
    public void ApplyDamageToPlayer(PlayerState player, int amount, string source = "")
    {
        if (player == null) return;

        try
        {
            int defense = GetPlayerDefenseFromCards(player);
            int final = Mathf.Max(0, amount - defense);

            int prev = GetPlayerCurrentHP(player);
            int now = Mathf.Max(0, prev - final);
            SetPlayerCurrentHP(player, now);

            if (verboseLog) Debug.Log($"[CombatSystem] {GetPlayerName(player)} took {final} dmg (raw {amount}, def {defense}) from {source}. HP {prev} -> {now}");

            // Event: DamageTaken
            InvokeEventBus_DamageTaken(player, final, source);

            // death?
            if (now <= 0)
            {
                InvokeEventBus_PlayerDied(player);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[CombatSystem] ApplyDamageToPlayer error: " + ex.Message);
        }
    }

    public void HealPlayer(PlayerState player, int amount, string source = "")
    {
        if (player == null) return;
        int prev = GetPlayerCurrentHP(player);
        int max = GetPlayerMaxHP(player);
        int now = prev + amount;
        if (max > 0) now = Mathf.Min(max, now);
        SetPlayerCurrentHP(player, now);
        if (verboseLog) Debug.Log($"[CombatSystem] {GetPlayerName(player)} healed {amount} from {source}. HP {prev} -> {now}");
    }

    public void SetPlayerHP(PlayerState player, int hp)
    {
        if (player == null) return;
        int max = GetPlayerMaxHP(player);
        int val = hp;
        if (max > 0) val = Mathf.Clamp(hp, 0, max);
        SetPlayerCurrentHP(player, val);
    }

    public void ApplyDamageToBoss(BossState boss, int amount, string source = "")
    {
        if (boss == null) return;
        int prev = boss.currentHP;
        int now = Mathf.Max(0, prev - amount);
        boss.currentHP = now;
        if (verboseLog) Debug.Log($"[CombatSystem] Boss took {amount} dmg from {source}. HP {prev} -> {now}");
        // Optional: invoke EventBus for boss damage / death
        if (now <= 0)
        {
            // handle boss death (bisa kasih EventBus.BossDied jika butuh)
        }
    }

    #endregion

    #region Reflection-tolerant helpers (player introspection)

    private string GetPlayerName(PlayerState player)
    {
        try
        {
            return player.gameObject != null ? player.gameObject.name : player.ToString();
        }
        catch { return player.ToString(); }
    }

    private int GetPlayerCurrentHP(PlayerState player)
    {
        if (player == null) return 0;
        Type t = player.GetType();
        object val = TryGetMemberValue(t, player, new string[] { "currentHP", "currentHealth", "hp", "HP", "health" });
        if (val != null) return Convert.ToInt32(val);

        // try method GetCurrentHP()
        MethodInfo mi = t.GetMethod("GetCurrentHP", BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            try { return Convert.ToInt32(mi.Invoke(player, null)); } catch { }
        }
        return 0;
    }

    private int GetPlayerMaxHP(PlayerState player)
    {
        if (player == null) return 0;
        Type t = player.GetType();
        object val = TryGetMemberValue(t, player, new string[] { "maxHP", "maxHealth", "max" });
        if (val != null) return Convert.ToInt32(val);

        var mi = t.GetMethod("GetMaxHP", BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            try { return Convert.ToInt32(mi.Invoke(player, null)); } catch { }
        }
        return 0;
    }

    private void SetPlayerCurrentHP(PlayerState player, int hp)
    {
        if (player == null) return;
        Type t = player.GetType();
        if (!TrySetMemberValue(t, player, new string[] { "currentHP", "currentHealth", "hp", "HP", "health" }, hp))
        {
            // try method SetCurrentHP
            var mi = t.GetMethod("SetCurrentHP", BindingFlags.Public | BindingFlags.Instance);
            if (mi != null)
            {
                try { mi.Invoke(player, new object[] { hp }); }
                catch { }
            }
        }
        // notify if supported
        TryNotifyPlayerStateChanged(player);
    }

    private int GetPlayerDefenseFromCards(PlayerState player)
    {
        if (player == null) return 0;
        Type t = player.GetType();
        object val = TryGetMemberValue(t, player, new string[] { "defenseFromCards", "defense", "cardDefense" });
        if (val != null) return Convert.ToInt32(val);

        var mi = t.GetMethod("GetDefenseFromCards", BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            try { return Convert.ToInt32(mi.Invoke(player, null)); } catch { }
        }
        return 0;
    }

    // generic get/set helpers
    private object TryGetMemberValue(Type t, object obj, string[] names)
    {
        foreach (var n in names)
        {
            var pi = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance);
            if (pi != null)
            {
                try { return pi.GetValue(obj); } catch { }
            }
            var fi = t.GetField(n, BindingFlags.Public | BindingFlags.Instance);
            if (fi != null)
            {
                try { return fi.GetValue(obj); } catch { }
            }
        }
        return null;
    }

    private bool TrySetMemberValue(Type t, object obj, string[] names, object value)
    {
        foreach (var n in names)
        {
            var pi = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance);
            if (pi != null && pi.CanWrite)
            {
                try { pi.SetValue(obj, Convert.ChangeType(value, pi.PropertyType)); return true; } catch { }
            }
            var fi = t.GetField(n, BindingFlags.Public | BindingFlags.Instance);
            if (fi != null)
            {
                try { fi.SetValue(obj, Convert.ChangeType(value, fi.FieldType)); return true; } catch { }
            }
        }
        return false;
    }

    private void TryNotifyPlayerStateChanged(PlayerState player)
    {
        if (player == null) return;
        MethodInfo mi = player.GetType().GetMethod("NotifyStateChanged", BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            try { mi.Invoke(player, null); } catch { }
        }
    }

    #endregion

    #region EventBus invokers (no dynamic)

    private void InvokeEventBus_DamageTaken(PlayerState player, int damage, string source)
    {
        if (player == null) return;

        Type eb = typeof(EventBus);

        // try (PlayerState,int,string)
        MethodInfo m3 = eb.GetMethod("DamageTaken", new Type[] { typeof(PlayerState), typeof(int), typeof(string) });
        if (m3 != null)
        {
            try { m3.Invoke(null, new object[] { player, damage, source }); return; } catch { }
        }

        // try (PlayerState,int)
        MethodInfo m2 = eb.GetMethod("DamageTaken", new Type[] { typeof(PlayerState), typeof(int) });
        if (m2 != null)
        {
            try { m2.Invoke(null, new object[] { player, damage }); return; } catch { }
        }

        // last fallback: try any DamageTaken static method
        try
        {
            MethodInfo any = eb.GetMethod("DamageTaken", BindingFlags.Public | BindingFlags.Static);
            if (any != null)
            {
                var pars = any.GetParameters();
                if (pars.Length == 3) any.Invoke(null, new object[] { player, damage, source });
                else if (pars.Length == 2) any.Invoke(null, new object[] { player, damage });
            }
        }
        catch { }
    }

    private void InvokeEventBus_PlayerDied(PlayerState player)
    {
        if (player == null) return;
        Type eb = typeof(EventBus);
        MethodInfo mi = eb.GetMethod("PlayerDied", new Type[] { typeof(PlayerState) });
        if (mi != null)
        {
            try { mi.Invoke(null, new object[] { player }); return; } catch { }
        }
        // fallback: direct call if matches signature
        try { EventBus.PlayerDied(player); } catch { }
    }

    #endregion
}
