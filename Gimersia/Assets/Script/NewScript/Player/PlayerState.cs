using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerState: menyimpan state gameplay seorang pemain.
/// - SRP: hanya menyimpan data & helper operation (take damage, add/remove card, flags)
/// - Tidak melakukan movement, UI, atau turn logic.
/// </summary>
[DisallowMultipleComponent]
public class PlayerState : MonoBehaviour
{
    // ----- Konfigurasi dasar -----
    [Header("Core Stats")]
    [Tooltip("Maximum HP player (default 15 sesuai GDD)")]
    public int maxHP = 15;

    [Tooltip("HP saat ini (runtime)")]
    public int currentHP;

    [Header("Card / Hand")]
    [Tooltip("Maksimal kartu di tangan")]
    public int maxHandSize = 6;

    [Tooltip("Kartu yang sedang dipegang pemain")]
    public List<NewCardData> hand = new List<NewCardData>();

    [Header("Temporary Buff / Modifiers")]
    [Tooltip("Flat defense yang berasal dari kartu buff (bersifat additive).")]
    public int defenseFromCards = 0;

    [Tooltip("Modifier untuk roll berikutnya (mis. +2 atau -2 dari efek)")]
    public int nextRollModifier = 0;

    [Header("Flags / Status")]
    [Tooltip("Player kebal (contoh: dari card) untuk beberapa giliran — dipakai oleh systems lain jika perlu")]
    public int immuneToAllNegativeTurns = 0;

    [Tooltip("Player kebal dari snake untuk beberapa penggunaan")]
    public int immuneToSnakeUses = 0;

    [Tooltip("Jika true, player mendapatkan giliran ekstra (set oleh card lalu dikonsumsi oleh TurnManager)")]
    public bool getsExtraTurn = false;

    [Tooltip("Jika true, player akan menggambar 1 kartu pada giliran berikutnya")]
    public bool drawCardNextTurn = false;

    public int TileID = 1;
    public int extraDiceRolls = 0;

    // Event untuk perubahan status (internal convenience)
    public event Action<PlayerState> OnStateChanged;

    // ----- Properti helper -----
    public bool IsDead => currentHP <= 0;
    public bool IsHandFull => hand.Count >= maxHandSize;

    void Awake()
    {
        currentHP = maxHP;
    }

    public void NotifyStateChanged() => OnStateChanged?.Invoke(this);

    /// <summary>
    /// Set current HP (clamped). Memicu event apabila pemain mati.
    /// Gunakan ApplyDamage untuk perhitungan damage (memperhitungkan defenseFromCards).
    /// </summary>
    public void SetHP(int newHP)
    {
        currentHP = Mathf.Clamp(newHP, 0, maxHP);
        OnStateChanged?.Invoke(this);
        if (currentHP <= 0)
        {
            // Publish lewat EventBus supaya sistem lain (TurnManager / UI) tahu
            EventBus.PlayerDied(this);
        }
    }

    /// <summary>
    /// Apply damage ke player. Memperhitungkan defenseFromCards (flat).
    /// Damage minimal 0.
    /// Akan men-trigger EventBus.DamageTaken dan PlayerDied jika HP <= 0.
    /// </summary>
    /// <param name="rawDamage">Damage sebelum pengurangan</param>
    /// <param name="source">opsional, string/enum menjelaskan sumber damage</param>
    public void ApplyDamage(int rawDamage, string source = null)
    {
        int finalDamage = Mathf.Max(0, rawDamage - defenseFromCards);
        if (finalDamage == 0)
        {
            // masih publish event agar UI tahu (mis. "No damage karena defense")
            EventBus.DamageTaken(this, 0);
            return;
        }

        currentHP = Mathf.Max(0, currentHP - finalDamage);

        // Publish event damage taken (semua subscriber tahu)
        EventBus.DamageTaken(this, finalDamage);

        // Informasi internal
        OnStateChanged?.Invoke(this);

        if (currentHP <= 0)
        {
            EventBus.PlayerDied(this);
        }
    }

    /// <summary>
    /// Heal player. Tidak boleh melebihi maxHP.
    /// </summary>
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        OnStateChanged?.Invoke(this);
    }

    /// <summary>
    /// Add defense buff value (misal saat pakai buff card).
    /// Caller bertanggung jawab mengatur durasi / stack logic.
    /// </summary>
    public void AddDefense(int amount)
    {
        defenseFromCards += amount;
        OnStateChanged?.Invoke(this);
    }

    /// <summary>
    /// Remove defense buff value (dipanggil saat buff expired / kartu di-disarm).
    /// Pastikan tidak membuat defense negatif.
    /// </summary>
    public void RemoveDefense(int amount)
    {
        defenseFromCards = Mathf.Max(0, defenseFromCards - amount);
        OnStateChanged?.Invoke(this);
    }

    // -------------------------
    //  Hand / Card helpers
    // -------------------------
    /// <summary>
    /// Coba menambahkan kartu ke hand. Mengembalikan true jika berhasil.
    /// Jika hand penuh, kembalikan false (UI/Caller harus handle Drop/Skip).
    /// </summary>
    public bool TryAddCard(NewCardData card)
    {
        if (card == null) return false;
        if (hand.Count >= maxHandSize) return false;
        hand.Add(card);
        // Notify card draw untuk sistem lain (UI)
        EventBus.CardDrawn(this);
        OnStateChanged?.Invoke(this);
        return true;
    }

    /// <summary>
    /// Force add card (untuk bypass hand limit) — dipakai hanya jika kamu mau auto-drop atau replace.
    /// Caller harus memilih kartu mana yang dibuang.
    /// </summary>
    public void ForceAddCard(NewCardData card)
    {
        if (card == null) return;
        hand.Add(card);
        EventBus.CardDrawn(this);
        OnStateChanged?.Invoke(this);
    }

    /// <summary>
    /// Buang kartu spesifik dari hand (jika ada). Return true jika berhasil.
    /// </summary>
    public bool DiscardCard(NewCardData card)
    {
        bool removed = hand.Remove(card);
        if (removed) OnStateChanged?.Invoke(this);
        return removed;
    }

    /// <summary>
    /// Buang `count` kartu secara acak dari hand. Return list kartu yang dibuang.
    /// Digunakan untuk efek Disarm.
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

        OnStateChanged?.Invoke(this);
        return removed;
    }

    /// <summary>
    /// Clear hand (misal saat reset game).
    /// </summary>
    public void ClearHand()
    {
        hand.Clear();
        OnStateChanged?.Invoke(this);
    }

    // -------------------------
    //  Utility helpers
    // -------------------------
    /// <summary>
    /// Reset stat sementara (dipanggil tiap cycle jika perlu).
    /// Jangan reset maxHP atau permanent state yang seharusnya persist.
    /// </summary>
    public void ResetTemporaryStatus()
    {
        defenseFromCards = 0;
        nextRollModifier = 0;
        immuneToAllNegativeTurns = 0;
        immuneToSnakeUses = 0;
        getsExtraTurn = false;
        drawCardNextTurn = false;
        OnStateChanged?.Invoke(this);
    }

    /// <summary>
    /// Debug helper untuk menampilkan ringkasan state ke console.
    /// </summary>
    public string GetStatusSummary()
    {
        return $"[PlayerState] {gameObject.name} HP:{currentHP}/{maxHP} DEF:{defenseFromCards} Hand:{hand.Count}/{maxHandSize}";
    }
}
