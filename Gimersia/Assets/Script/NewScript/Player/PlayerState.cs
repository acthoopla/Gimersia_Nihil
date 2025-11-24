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

    [Header("Card / Hand")]
    public int maxHandSize = 6;
    public List<NewCardData> hand = new List<NewCardData>();

    [Header("Action Slots")]
    public int maxSlots = 3; // Batas 3 kartu
    public List<NewCardData> selectedCards = new List<NewCardData>(); // Antrean eksekusi

    [Header("Status & Flags")]
    public int TileID = 1;
    public int immuneStacks = 0; // Jumlah stack immune (bisa nahan berapa kali)
    public int immuneToSnakeUses = 0;
    public int reflectMultiplier = 0;
    [SerializeField] private bool hasDoubleEdge = false;
    public int defenseFromCards = 0;

    [Tooltip("Modifikasi roll dadu berikutnya (bisa + atau -). Reset setelah roll.")]
    public int nextRollModifier = 0;

    [Header("References")]
    public NewPlayerPawn pawn; // Referensi ke script visual pawn

    // Events: Memberitahu UI (HandUIManager) jika ada perubahan
    public event Action<PlayerState> OnStateChanged;

    // Properties
    public bool IsHandFull => hand.Count >= maxHandSize;
    public bool IsDead => currentHP <= 0;

    public bool HasDoubleEdge
    {
        get => hasDoubleEdge;
        set => hasDoubleEdge = value;
    }

    // Backward Compatibility Property (Agar script lama tetap jalan)
    public List<NewCardData> heldCards
    {
        get => hand;
        set => hand = value ?? new List<NewCardData>();
    }

    void Awake()
    {
        currentHP = maxHP;
    }

    public void NotifyStateChanged() => OnStateChanged?.Invoke(this);

    // --- LOGIC HEALTH & DAMAGE ---
    public void ApplyDamage(int rawDamage, string source = null)
    {
        // 1. CEK REFLECT (Sekarang pakai multiplier)
        if (reflectMultiplier > 0)
        {
            int reflectDmg = rawDamage * reflectMultiplier;
            Debug.Log($"[Reflect] Player memantulkan {reflectDmg} damage ({reflectMultiplier}x dari {rawDamage})!");

            // TODO: Panggil CombatSystem untuk deal damage ke musuh
            // CombatSystem.Instance.DealDamageToBoss(reflectDmg);

            reflectMultiplier = 0;
            NotifyStateChanged();

            return;
        }

        // 2. CEK IMMUNE
        if (immuneStacks > 0)
        {
            immuneStacks--;
            Debug.Log($"[Immune] Damage {rawDamage} ditahan! Sisa Immune Stack: {immuneStacks}");
            NotifyStateChanged();
            return;
        }

        // 3. LOGIKA DAMAGE NORMAL
        int damageAfterDefense = Mathf.Max(0, rawDamage - defenseFromCards);
        float multiplier = HasDoubleEdge ? 2f : 1f;
        int finalDamage = Mathf.RoundToInt(damageAfterDefense * multiplier);

        currentHP = Mathf.Max(0, currentHP - finalDamage);
        NotifyStateChanged();
    }

    public void Heal(int amount)
    {
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        NotifyStateChanged();
    }

    public void AddImmunityStack(int amount)
    {
        immuneStacks += amount;
        NotifyStateChanged();
    }

    public void SetReflect(int multiplier)
    {
        reflectMultiplier = multiplier;
        NotifyStateChanged();
    }

    // --- LOGIC HAND (MANDIRI) ---
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

    /// <summary>
    /// Buang `count` kartu secara acak (misal kena Nega Tile Disarm).
    /// </summary>
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

    // -------------------------
    //  Utility helpers
    // -------------------------
    public void ResetTemporaryStatus()
    {
        defenseFromCards = 0;
        immuneStacks = 0;
        // nextRollModifier TIDAK direset di sini, karena dipakai saat roll dadu
        reflectMultiplier = 0;
        hasDoubleEdge = false; // Reset status double edge setiap ganti giliran (opsional)
        NotifyStateChanged();
    }

    // Method SetHP yang dipanggil NewGameManager saat restart
    public void SetHP(int hp)
    {
        currentHP = hp;
        NotifyStateChanged();
    }

    public bool SelectCardToSlot(NewCardData card)
    {
        if (card == null) return false;
        if (selectedCards.Count >= maxSlots) return false; // Slot penuh
        if (!hand.Contains(card)) return false;

        hand.Remove(card);
        selectedCards.Add(card); // Masuk antrean
        NotifyStateChanged();
        return true;
    }

    public bool ReturnCardToHand(NewCardData card)
    {
        if (card == null) return false;
        if (!selectedCards.Contains(card)) return false;
        if (IsHandFull) return false; // Hand penuh

        selectedCards.Remove(card);
        hand.Add(card); // Balik ke hand
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

    public string GetStatusSummary()
    {
        return $"[PlayerState] {gameObject.name} HP:{currentHP}/{maxHP} DEF:{defenseFromCards} Hand:{hand.Count}/{maxHandSize}";
    }
}