using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerState: menyimpan state gameplay seorang pemain.
/// - SRP: hanya menyimpan data & helper operation (take damage, add/remove card, flags)
/// - Tidak melakukan movement, UI, atau turn logic.
/// 
/// Tambahan: menyediakan backward-compatible aliases:
/// - pawn (public field)
/// - heldCards (property yang membungkus hand)
/// - HasDoubleEdge (property untuk efek double edge)
/// 
/// </summary>
[DisallowMultipleComponent]
public class PlayerState : MonoBehaviour
{
    [Header("Core Stats")]
    public int maxHP = 15;
    public int currentHP;

    [Header("Hand & Slots")]
    public int maxHandSize = 6;
    public List<NewCardData> hand = new List<NewCardData>();

    // Slot untuk visual selection (Max 3)
    public int maxSlots = 3;
    public List<NewCardData> selectedCards = new List<NewCardData>();

    [Header("Execution Queue")]
    // Antrean kartu yang menunggu tombol GO
    public List<NewCardData> pendingQueue = new List<NewCardData>();

    [Header("Status")]
    public int TileID = 1;
    public int immuneToAllNegativeTurns = 0;
    public int immuneToSnakeUses = 0;
    public int immuneStacks = 0;
    public int reflectMultiplier = 0;
    public bool hasDoubleEdge = false;
    public int defenseFromCards = 0;
    public int nextRollModifier = 0;

    [Header("References")]
    public NewPlayerPawn pawn;
    public event Action<PlayerState> OnStateChanged;

    public bool IsHandFull => hand.Count >= maxHandSize;
    public bool HasDoubleEdge { get => hasDoubleEdge; set => hasDoubleEdge = value; }

    void Awake() { currentHP = maxHP; }
    public void NotifyStateChanged() => OnStateChanged?.Invoke(this);

    // --- 1. LOGIC HAND (ADD / REMOVE) ---

    public bool TryAddCard(NewCardData card)
    {
        if (card == null || IsHandFull) return false;
        hand.Add(card);
        NotifyStateChanged();
        return true;
    }

    // FUNGSI INI YANG HILANG SEBELUMNYA (FIX ERROR)
    public bool DiscardCard(NewCardData card)
    {
        bool removed = hand.Remove(card);
        if (removed) NotifyStateChanged();
        return removed;
    }

    public List<NewCardData> DiscardRandom(int count)
    {
        List<NewCardData> removed = new List<NewCardData>();
        if (count <= 0 || hand.Count == 0) return removed;

        System.Random rng = new System.Random();
        count = Mathf.Min(count, hand.Count);

        for (int i = 0; i < count; i++)
        {
            int idx = rng.Next(0, hand.Count);
            NewCardData c = hand[idx];
            hand.RemoveAt(idx);
            removed.Add(c);
        }
        NotifyStateChanged();
        return removed;
    }

    public void ClearHand()
    {
        hand.Clear();
        NotifyStateChanged();
    }

    // --- 2. LOGIC PENDING QUEUE ---

    public void AddToPending(NewCardData card)
    {
        pendingQueue.Add(card);
    }

    public List<NewCardData> GetAndClearPending()
    {
        List<NewCardData> toExecute = new List<NewCardData>(pendingQueue);
        pendingQueue.Clear();
        return toExecute;
    }

    // --- 3. LOGIC SLOT ---

    public bool SelectCardToSlot(NewCardData card)
    {
        if (card == null || selectedCards.Count >= maxSlots || !hand.Contains(card)) return false;
        hand.Remove(card);
        selectedCards.Add(card);
        NotifyStateChanged();
        return true;
    }

    public bool ReturnCardToHand(NewCardData card)
    {
        if (card == null || !selectedCards.Contains(card) || IsHandFull) return false;
        selectedCards.Remove(card);
        hand.Add(card);
        NotifyStateChanged();
        return true;
    }

    public void ConsumeSelectedCard(NewCardData card)
    {
        if (selectedCards.Contains(card))
        {
            selectedCards.Remove(card);
            NotifyStateChanged();
        }
    }

    // --- 4. CORE GAMEPLAY ---

    public void ApplyDamage(int rawDamage, string source = null)
    {
        if (immuneToAllNegativeTurns > 0) { return; }

        if (immuneStacks > 0)
        {
            immuneStacks--;
            NotifyStateChanged();
            return;
        }

        if (reflectMultiplier > 0)
        {
            Debug.Log($"Reflect {reflectMultiplier}x activated!");
            return;
        }

        int dmg = Mathf.Max(0, rawDamage - defenseFromCards);
        if (HasDoubleEdge) dmg *= 2;

        currentHP = Mathf.Max(0, currentHP - dmg);
        NotifyStateChanged();
    }

    public void Heal(int amount)
    {
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        NotifyStateChanged();
    }

    public void ResetTemporaryStatus()
    {
        defenseFromCards = 0;
        immuneToAllNegativeTurns = 0;
        immuneStacks = 0;
        reflectMultiplier = 0;
        nextRollModifier = 0;
        hasDoubleEdge = false;
        NotifyStateChanged();
    }

    public void SetHP(int hp)
    {
        currentHP = hp;
        NotifyStateChanged();
    }

    public void AddImmunityStack(int amt)
    {
        immuneStacks += amt;
        NotifyStateChanged();
    }

    public void SetReflect(int mult)
    {
        reflectMultiplier = mult;
        NotifyStateChanged();
    }
}