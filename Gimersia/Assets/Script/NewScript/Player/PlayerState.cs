using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerState: menyimpan state gameplay seorang pemain.
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

    // Alias untuk backward compatibility
    public List<NewCardData> heldCards { get => hand; set => hand = value ?? new List<NewCardData>(); }

    void Awake() { currentHP = maxHP; }
    public void NotifyStateChanged() => OnStateChanged?.Invoke(this);

    // --- [PERBAIKAN ERROR] LOGIC PENDING QUEUE ---

    /// <summary>
    /// Dipanggil oleh TurnManager saat tombol GO ditekan.
    /// Memindahkan semua kartu dari Slot (Visual) ke Pending Queue (Logic).
    /// </summary>
    public void ConfirmLoadout()
    {
        if (selectedCards.Count > 0)
        {
            // Pindahkan isi selectedCards ke pendingQueue
            pendingQueue.AddRange(selectedCards);

            // Kosongkan slot visual karena kartu sudah dianggap "terpakai" secara logika
            selectedCards.Clear();

            NotifyStateChanged(); // Update UI agar slot terlihat kosong saat animasi jalan
        }
    }

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

    // --- LOGIC HAND (ADD / REMOVE) ---

    public bool TryAddCard(NewCardData card)
    {
        if (card == null || IsHandFull) return false;
        hand.Add(card);
        NotifyStateChanged();
        return true;
    }

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

    // --- LOGIC SLOT ---

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

    // --- CORE GAMEPLAY ---

    public void ApplyDamage(int rawDamage, string source = null)
    {
        if (currentHP <= 0) return; // Sudah mati

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
            if (UIController.Instance.bossState != null)
            {
                UIController.Instance.bossState.TakeDamage(rawDamage * reflectMultiplier);
            }
            reflectMultiplier = 0;
            NotifyStateChanged();
            return;
        }

        int dmg = Mathf.Max(0, rawDamage - defenseFromCards);
        if (HasDoubleEdge) dmg *= 2;

        currentHP = Mathf.Max(0, currentHP - dmg);
        NotifyStateChanged();

        // Cek Kematian Player
        if (currentHP <= 0)
        {
            Debug.Log("Player Mati (HP Habis)!");
            if (UIController.Instance != null)
            {
                UIController.Instance.ShowGameOver();
            }
        }
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