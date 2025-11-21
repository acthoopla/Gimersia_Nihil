using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// CombatSystem (SRP)
/// - Menangani penerapan damage/heal pada pemain (PlayerState).
/// - Mem-publish event lewat EventBus jika tersedia (DamageTaken, PlayerDied).
/// - Berusaha kompatibel dengan PlayerState yang berbeda nama field/property:
///   (currentHP, currentHealth, hp, maxHP, maxHealth)
/// - Singleton (Instance)
/// </summary>
[DisallowMultipleComponent]
public class CombatSystem : MonoBehaviour
{
    public static CombatSystem Instance { get; private set; }

    [Header("Combat Settings")]
    public bool useConsoleLog = true;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    #region Public API

    /// <summary>
    /// Terapkan damage ke player. Menghormati potensi defense dari kartu (defenseFromCards property bila ada).
    /// source: string singkat menjelaskan asal damage (tile, boss, card, dll).
    /// </summary>
    public void ApplyDamageToPlayer(object playerObj, int amount, string source = "")
    {
        if (playerObj == null) return;
        try
        {
            int defense = GetPlayerDefenseFromCards(playerObj);
            int final = Mathf.Max(0, amount - defense);

            // kurangi HP
            int prev = GetPlayerCurrentHP(playerObj);
            int now = Mathf.Max(0, prev - final);
            SetPlayerCurrentHP(playerObj, now);

            if (useConsoleLog) Debug.Log($"[CombatSystem] {GetPlayerName(playerObj)} took {final} dmg (raw {amount}, def {defense}) from {source}. HP {prev} -> {now}");

            // EventBus.DamageTaken(player, final, source) jika tersedia
            TryInvokeEventBusDamageTaken(playerObj, final, source);

            // Check death
            if (now <= 0)
            {
                TryInvokeEventBusPlayerDied(playerObj);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[CombatSystem] ApplyDamageToPlayer failed: " + ex.Message);
        }
    }

    /// <summary>
    /// Heal player by amount (clamped by maxHP if available)
    /// </summary>
    public void HealPlayer(object playerObj, int amount, string source = "")
    {
        if (playerObj == null) return;
        int prev = GetPlayerCurrentHP(playerObj);
        int max = GetPlayerMaxHP(playerObj);
        int now = prev + amount;
        if (max > 0) now = Mathf.Min(max, now);
        SetPlayerCurrentHP(playerObj, now);
        if (useConsoleLog) Debug.Log($"[CombatSystem] {GetPlayerName(playerObj)} healed {amount} from {source}. HP {prev} -> {now}");
    }

    /// <summary>
    /// Set current HP directly (clamped)
    /// </summary>
    public void SetPlayerHP(object playerObj, int hp)
    {
        if (playerObj == null) return;
        int max = GetPlayerMaxHP(playerObj);
        int val = hp;
        if (max > 0) val = Mathf.Clamp(hp, 0, max);
        SetPlayerCurrentHP(playerObj, val);
    }

    #endregion

    #region Helpers - Player introspection (reflection tolerant)

    private string GetPlayerName(object playerObj)
    {
        try
        {
            // if Component or GameObject, get name
            if (playerObj is Component c) return c.gameObject.name;
            var pi = playerObj.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
            if (pi != null)
            {
                var go = pi.GetValue(playerObj) as GameObject;
                if (go != null) return go.name;
            }
            return playerObj.ToString();
        }
        catch { return playerObj.ToString(); }
    }

    private int GetPlayerCurrentHP(object playerObj)
    {
        Type t = playerObj.GetType();
        // Try common property/field names
        object val = TryGetMemberValue(t, playerObj, new string[] { "currentHP", "currentHealth", "hp", "HP", "health" });
        if (val != null) return Convert.ToInt32(val);

        // Maybe PlayerState has GetCurrentHP()
        var mi = t.GetMethod("GetCurrentHP", BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            try { return Convert.ToInt32(mi.Invoke(playerObj, null)); } catch { }
        }

        return 0;
    }

    private int GetPlayerMaxHP(object playerObj)
    {
        Type t = playerObj.GetType();
        object val = TryGetMemberValue(t, playerObj, new string[] { "maxHP", "maxHealth", "max" });
        if (val != null) return Convert.ToInt32(val);
        var mi = t.GetMethod("GetMaxHP", BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            try { return Convert.ToInt32(mi.Invoke(playerObj, null)); } catch { }
        }
        return 0;
    }

    private void SetPlayerCurrentHP(object playerObj, int hp)
    {
        Type t = playerObj.GetType();
        if (!TrySetMemberValue(t, playerObj, new string[] { "currentHP", "currentHealth", "hp", "HP", "health" }, hp))
        {
            // try setter method
            var mi = t.GetMethod("SetCurrentHP", BindingFlags.Public | BindingFlags.Instance);
            if (mi != null)
            {
                try { mi.Invoke(playerObj, new object[] { hp }); return; } catch { }
            }
            // if can't set, attempt to set field "currentTileID" accidental? skip
        }
        // notify state changed if PlayerState supports it
        TryNotifyPlayerStateChanged(playerObj);
    }

    private int GetPlayerDefenseFromCards(object playerObj)
    {
        // Try to read a field/property like defenseFromCards
        Type t = playerObj.GetType();
        object val = TryGetMemberValue(t, playerObj, new string[] { "defenseFromCards", "defense", "cardDefense" });
        if (val != null) return Convert.ToInt32(val);

        // maybe player has method GetDefenseFromCards()
        var mi = t.GetMethod("GetDefenseFromCards", BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            try { return Convert.ToInt32(mi.Invoke(playerObj, null)); } catch { }
        }
        return 0;
    }

    private object TryGetMemberValue(Type t, object obj, string[] names)
    {
        foreach (var n in names)
        {
            var pi = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance);
            if (pi != null) { try { return pi.GetValue(obj); } catch { } }
            var fi = t.GetField(n, BindingFlags.Public | BindingFlags.Instance);
            if (fi != null) { try { return fi.GetValue(obj); } catch { } }
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

    private void TryNotifyPlayerStateChanged(object playerObj)
    {
        Type t = playerObj.GetType();
        var mi = t.GetMethod("NotifyStateChanged", BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            try { mi.Invoke(playerObj, null); return; } catch { }
        }
        // fallback: if it exposes event OnStateChanged, cannot invoke from outside; skip
    }

    #endregion

    #region EventBus helpers

    private void TryInvokeEventBusDamageTaken(object playerObj, int damage, string source)
    {
        // If there's static EventBus.DamageTaken(PlayerState, int, string) -> call it
        Type eb = typeof(EventBus); // if EventBus missing, compile will fail; assume EventBus class exists
        try
        {
            var mi = eb.GetMethod("DamageTaken", BindingFlags.Public | BindingFlags.Static);
            if (mi != null)
            {
                mi.Invoke(null, new object[] { playerObj, damage, source });
                return;
            }
        }
        catch { /* ignore */ }

        // else try dynamic invocation (if signature matches)
        try { EventBus.DamageTaken((dynamic)playerObj, damage, source); } catch { }
    }

    private void TryInvokeEventBusPlayerDied(object playerObj)
    {
        try
        {
            var mi = typeof(EventBus).GetMethod("PlayerDied", BindingFlags.Public | BindingFlags.Static);
            if (mi != null)
            {
                mi.Invoke(null, new object[] { playerObj });
                return;
            }
        }
        catch { }
        try { EventBus.PlayerDied((dynamic)playerObj); } catch { }
    }

    #endregion
}
