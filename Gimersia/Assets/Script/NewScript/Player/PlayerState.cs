using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerState : MonoBehaviour
{
    [Header("Core Stats")]
    public int maxHP = 15;
    public int currentHP;

    [Header("Hand & Slots")]
    public int maxHandSize = 6;
    public List<NewCardData> hand = new List<NewCardData>();

    public int maxSlots = 3;
    public List<NewCardData> selectedCards = new List<NewCardData>();

    public List<NewCardData> pendingQueue = new List<NewCardData>();

    [Header("Status")]
    public int TileID = 1;
    public int immuneToAllNegativeTurns = 0;
    public int immuneStacks = 0;
    public int immuneToSnakeUses = 0;
    public int reflectMultiplier = 0;
    public bool hasDoubleEdge = false;
    public int defenseFromCards = 0;
    public int nextRollModifier = 0;

    [Header("References")]
    public NewPlayerPawn pawn;
    public PlayerAnimation playerAnimation;
    public event Action<PlayerState> OnStateChanged;

    public bool IsHandFull => hand.Count >= maxHandSize;
    public bool HasDoubleEdge { get => hasDoubleEdge; set => hasDoubleEdge = value; }

    // Alias backward compatibility
    public List<NewCardData> heldCards { get => hand; set => hand = value ?? new List<NewCardData>(); }

    void Awake()
    {
        currentHP = maxHP;
    }

    public void NotifyStateChanged() => OnStateChanged?.Invoke(this);

    // --- DAMAGE & LOSE CONDITION ---

    public void ApplyDamage(int rawDamage, string source = null)
    {
        if (currentHP <= 0) return;

        // 1. Cek Immune Global
        if (immuneToAllNegativeTurns > 0) return;

        // 2. Cek Immune Stacks
        if (immuneStacks > 0)
        {
            immuneStacks--;
            Debug.Log($"[Player] Damage {rawDamage} ditahan oleh Immune Stack!");

            playerAnimation.PlayDefend();

            NotifyStateChanged();
            return;
        }

        // 3. Cek Reflect
        if (reflectMultiplier > 0)
        {
            Debug.Log($"[Player] Reflect {reflectMultiplier}x active!");
            if (UIController.Instance != null && UIController.Instance.bossState != null)
            {
                UIController.Instance.bossState.TakeDamage(rawDamage * reflectMultiplier);
            }
            reflectMultiplier = 0;
            NotifyStateChanged();
            return;
        }

        // 4. Kalkulasi Damage
        int dmg = Mathf.Max(0, rawDamage - defenseFromCards);
        if (HasDoubleEdge) dmg *= 2;

        currentHP = Mathf.Max(0, currentHP - dmg);
        NotifyStateChanged();

        // 5. CEK KEMATIAN (LOSE CONDITION)
        if (currentHP <= 0)
        {
            Debug.Log(">>> GAME OVER: Player HP Habis! <<<");

            playerAnimation.PlayDeath();

            if (UIController.Instance != null)
                UIController.Instance.ShowGameOver();

            if (TurnManager.Instance != null)
                TurnManager.Instance.state = TurnManager.TurnState.GameOver;
        }
    }

    public void Heal(int amount)
    {
        if (currentHP <= 0) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        NotifyStateChanged();
    }

    // --- HAND MANAGEMENT (YANG HILANG TADI) ---

    // [FIX ERROR] Method ini saya kembalikan
    public bool DiscardCard(NewCardData card)
    {
        bool removed = hand.Remove(card);
        if (removed) NotifyStateChanged();
        return removed;
    }

    public bool TryAddCard(NewCardData card)
    {
        if (card == null || IsHandFull) return false;
        hand.Add(card);
        NotifyStateChanged();
        return true;
    }

    public void ClearHand()
    {
        hand.Clear();
        NotifyStateChanged();
    }

    public List<NewCardData> DiscardRandom(int count)
    {
        List<NewCardData> removed = new List<NewCardData>();
        if (hand.Count == 0) return removed;
        count = Mathf.Min(count, hand.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = UnityEngine.Random.Range(0, hand.Count);
            removed.Add(hand[idx]); hand.RemoveAt(idx);
        }
        NotifyStateChanged();
        return removed;
    }

    // --- SLOT & PENDING LOGIC ---

    public void ConfirmLoadout()
    {
        if (selectedCards.Count > 0)
        {
            pendingQueue.AddRange(selectedCards);
            selectedCards.Clear();
            NotifyStateChanged();
        }
    }

    public void AddToPending(NewCardData card) => pendingQueue.Add(card);

    public List<NewCardData> GetAndClearPending()
    {
        var list = new List<NewCardData>(pendingQueue);
        pendingQueue.Clear();
        return list;
    }

    public bool SelectCardToSlot(NewCardData card)
    {
        if (!hand.Contains(card) || selectedCards.Count >= maxSlots) return false;
        hand.Remove(card);
        selectedCards.Add(card);
        NotifyStateChanged();
        return true;
    }

    public bool ReturnCardToHand(NewCardData card)
    {
        if (!selectedCards.Contains(card) || IsHandFull) return false;
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

    // --- STATUS HELPERS ---

    public void ResetTemporaryStatus()
    {
        defenseFromCards = 0;
        immuneToAllNegativeTurns = 0;
        immuneStacks = 0;
        reflectMultiplier = 0;
        // nextRollModifier TIDAK direset disini agar efek tile persist
        // nextRollModifier = 0;  <-- Jangan di-uncomment
        hasDoubleEdge = false;
        NotifyStateChanged();
    }

    public void SetHP(int hp) { currentHP = hp; NotifyStateChanged(); }
    public void AddImmunityStack(int amt) { immuneStacks += amt; NotifyStateChanged(); }
    public void SetReflect(int mult) { reflectMultiplier = mult; NotifyStateChanged(); }
}