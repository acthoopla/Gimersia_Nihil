using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// NewCardSystem
/// - Singleton Instance
/// - Menyediakan CardCategory enum (so other scripts can reference NewCardSystem.CardCategory)
/// - API publik: GiveRandomCardToPlayer, GiveRandomCardFromCategories, GetRandomCardSelection, DiscardRandom, TryAddCardToPlayer
/// - Menyimpan internal hand jika PlayerState tidak expose heldCards
/// </summary>
[DisallowMultipleComponent]
public class NewCardSystem : MonoBehaviour
{
    public static NewCardSystem Instance { get; private set; }

    [Header("Card Pool")]
    public NewCardData[] cardPool;

    [Header("Hand rules")]
    public int maxHandSize = 6;
    public int defaultCycleAcquired = 1;

    // Internal registry for players that don't expose a compatible heldCards
    private Dictionary<object, List<PlayerCardInstance>> internalHands = new Dictionary<object, List<PlayerCardInstance>>();

    System.Random rng = new System.Random();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (cardPool == null) cardPool = new NewCardData[0];
    }

    // Expose CardCategory so other scripts can reference NewCardSystem.CardCategory
    public enum CardCategory
    {
        Movement,
        Buff,
        Attack,
        Defense,
        Utility
    }

    [Serializable]
    public class PlayerCardInstance
    {
        public NewCardData cardData;
        public int cycleAcquired;

        public PlayerCardInstance(NewCardData d, int cycle)
        {
            cardData = d;
            cycleAcquired = cycle;
        }

        public override string ToString()
        {
            return $"{cardData?.cardName ?? "(none)"} (cycle {cycleAcquired})";
        }
    }

    #region Public API

    public void GiveRandomCardToPlayer(object playerObj, int currentCycle)
    {
        if (cardPool == null || cardPool.Length == 0)
        {
            Debug.LogWarning("[NewCardSystem] Card pool empty.");
            return;
        }
        var card = cardPool[rng.Next(cardPool.Length)];
        AddCardToPlayerInternal(playerObj, new PlayerCardInstance(card, currentCycle));
    }

    public void GiveRandomCardFromCategories(object playerObj, CardCategory[] categories)
    {
        if (cardPool == null || cardPool.Length == 0) return;
        var matched = cardPool.Where(c => categories == null || categories.Length == 0 || categories.Contains(c.category)).ToArray();
        if (matched.Length == 0) matched = cardPool;
        var card = matched[rng.Next(matched.Length)];
        AddCardToPlayerInternal(playerObj, new PlayerCardInstance(card, defaultCycleAcquired));
    }

    public List<NewCardData> GetRandomCardSelection(int count)
    {
        List<NewCardData> list = new List<NewCardData>();
        if (cardPool == null || cardPool.Length == 0) return list;
        var indices = Enumerable.Range(0, cardPool.Length).OrderBy(x => rng.Next()).Take(count);
        foreach (var i in indices) list.Add(cardPool[i]);
        return list;
    }

    public List<PlayerCardInstance> DiscardRandom(object playerObj, int count)
    {
        var hand = GetHandReference(playerObj, createIfMissing: false);
        var removed = new List<PlayerCardInstance>();
        if (hand == null || hand.Count == 0) return removed;

        for (int i = 0; i < count && hand.Count > 0; i++)
        {
            int idx = rng.Next(hand.Count);
            removed.Add(hand[idx]);
            hand.RemoveAt(idx);
        }

        TryNotifyHandChanged(playerObj);
        return removed;
    }

    public bool TryAddCardToPlayer(object playerObj, NewCardData cardData, int cycle)
    {
        if (cardData == null) return false;
        var pi = new PlayerCardInstance(cardData, cycle);
        return AddCardToPlayerInternal(playerObj, pi);
    }

    #endregion

    #region Internal helpers

    private bool AddCardToPlayerInternal(object playerObj, PlayerCardInstance pi)
    {
        if (playerObj == null) return false;

        var hand = GetHandReference(playerObj, createIfMissing: true);
        if (hand == null)
        {
            Debug.LogWarning("[NewCardSystem] Could not get or create hand for player.");
            return false;
        }

        // enforce max hand size
        if (hand.Count >= maxHandSize)
        {
            // Default policy: drop oldest (index 0). Better: present UI choice.
            hand.RemoveAt(0);
            Debug.Log("[NewCardSystem] Hand full: dropped oldest card to make space.");
        }

        hand.Add(pi);
        TryNotifyHandChanged(playerObj);
        if (playerObj is UnityEngine.Component comp)
        {
            Debug.Log($"[NewCardSystem] Gave card {pi.cardData.cardName} to {comp.gameObject.name}");
        }
        return true;
    }

    private List<PlayerCardInstance> GetHandReference(object playerObj, bool createIfMissing)
    {
        if (playerObj == null) return null;
        Type t = playerObj.GetType();

        // first try exact matching field/property type List<PlayerCardInstance>
        var pf = t.GetField("heldCards", BindingFlags.Public | BindingFlags.Instance);
        if (pf != null && pf.FieldType.IsGenericType && pf.FieldType.GetGenericArguments()[0] == typeof(PlayerCardInstance))
        {
            var val = pf.GetValue(playerObj) as List<PlayerCardInstance>;
            if (val == null && createIfMissing)
            {
                var newList = new List<PlayerCardInstance>();
                pf.SetValue(playerObj, newList);
                return newList;
            }
            return val;
        }

        var pp = t.GetProperty("heldCards", BindingFlags.Public | BindingFlags.Instance);
        if (pp != null && pp.PropertyType.IsGenericType && pp.PropertyType.GetGenericArguments()[0] == typeof(PlayerCardInstance))
        {
            var val = pp.GetValue(playerObj) as List<PlayerCardInstance>;
            if (val == null && createIfMissing)
            {
                var newList = new List<PlayerCardInstance>();
                pp.SetValue(playerObj, newList);
                return newList;
            }
            return val;
        }

        // fallback: internal registry
        if (!internalHands.TryGetValue(playerObj, out List<PlayerCardInstance> list))
        {
            if (!createIfMissing) return null;
            list = new List<PlayerCardInstance>();
            internalHands[playerObj] = list;
        }
        return list;
    }

    private void TryNotifyHandChanged(object playerObj)
    {
        /*if (playerObj == null) return;
        Type t = playerObj.GetType();
        var mi = t.GetMethod("OnHandChanged", BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            try { mi.Invoke(playerObj, null); return; } catch { }
        }*/
        var t = playerObj.GetType();
        var mi = t.GetMethod("OnHandChanged", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        mi?.Invoke(playerObj, null);
        var notify = t.GetMethod("NotifyStateChanged", BindingFlags.Public | BindingFlags.Instance);
        if (notify != null)
        {
            try { notify.Invoke(playerObj, null); } catch { }
        }
    }

    // *** Perbaikan di sini: TryRemoveCardFromPlayer sekarang mendukung PlayerState yang menyimpan NewCardData (List<NewCardData>)
    // serta kasus List<PlayerCardInstance>.
    public bool TryRemoveCardFromPlayer(PlayerState player, PlayerCardInstance pci)
    {
        if (player == null || pci == null) return false;

        // 1) coba akses heldCards sebagai List<PlayerCardInstance> via reflection (field or property)
        Type t = player.GetType();
        var pf = t.GetField("heldCards", BindingFlags.Public | BindingFlags.Instance);
        if (pf != null && pf.FieldType.IsGenericType)
        {
            var arg = pf.FieldType.GetGenericArguments()[0];
            if (arg == typeof(PlayerCardInstance))
            {
                var list = pf.GetValue(player) as List<PlayerCardInstance>;
                if (list == null) return false;
                bool removed = list.Remove(pci);
                if (removed) TryNotifyHandChanged(player);
                return removed;
            }
            else if (arg == typeof(NewCardData))
            {
                // Player stores raw NewCardData — remove the underlying cardData
                var list = pf.GetValue(player) as List<NewCardData>;
                if (list == null) return false;
                bool removed = list.Remove(pci.cardData);
                if (removed) TryNotifyHandChanged(player);
                return removed;
            }
        }

        // 2) try property 'heldCards' as well
        var pp = t.GetProperty("heldCards", BindingFlags.Public | BindingFlags.Instance);
        if (pp != null && pp.PropertyType.IsGenericType)
        {
            var arg = pp.PropertyType.GetGenericArguments()[0];
            if (arg == typeof(PlayerCardInstance))
            {
                var list = pp.GetValue(player) as List<PlayerCardInstance>;
                if (list == null) return false;
                bool removed = list.Remove(pci);
                if (removed) TryNotifyHandChanged(player);
                return removed;
            }
            else if (arg == typeof(NewCardData))
            {
                var list = pp.GetValue(player) as List<NewCardData>;
                if (list == null) return false;
                bool removed = list.Remove(pci.cardData);
                if (removed) TryNotifyHandChanged(player);
                return removed;
            }
        }

        // 3) fallback: try internal registry mapping object->List<PlayerCardInstance>
        if (internalHands.TryGetValue(player, out List<PlayerCardInstance> internalList))
        {
            bool removed2 = internalList.Remove(pci);
            if (removed2) TryNotifyHandChanged(player);
            return removed2;
        }

        // 4) as last resort, try to search player's public 'hand' or 'hand' list of NewCardData
        var pfHand = t.GetField("hand", BindingFlags.Public | BindingFlags.Instance);
        if (pfHand != null && pfHand.FieldType.IsGenericType && pfHand.FieldType.GetGenericArguments()[0] == typeof(NewCardData))
        {
            var list = pfHand.GetValue(player) as List<NewCardData>;
            if (list != null)
            {
                bool removed = list.Remove(pci.cardData);
                if (removed) TryNotifyHandChanged(player);
                return removed;
            }
        }

        var ppHand = t.GetProperty("hand", BindingFlags.Public | BindingFlags.Instance);
        if (ppHand != null && ppHand.PropertyType.IsGenericType && ppHand.PropertyType.GetGenericArguments()[0] == typeof(NewCardData))
        {
            var list = ppHand.GetValue(player) as List<NewCardData>;
            if (list != null)
            {
                bool removed = list.Remove(pci.cardData);
                if (removed) TryNotifyHandChanged(player);
                return removed;
            }
        }

        return false;
    }

    #endregion

    #region Utilities

    public string DumpHand(object playerObj)
    {
        var hand = GetHandReference(playerObj, createIfMissing: false);
        if (hand == null) return "(no hand)";
        return string.Join(", ", hand.Select(h => h.cardData ? h.cardData.cardName : "(null)"));
    }

    #endregion
}
