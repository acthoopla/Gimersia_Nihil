using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardHeal : BaseCard
{
    [Header("Heal Settings")]
    [SerializeField] private int healAmount = 20;

    #region Card Usage
    public override void Use()
    {
        Debug.Log($"[{GetCardName()}] Healing player for {healAmount} HP!");

        // TODO: Implementasi healing logic
        // Contoh: PlayerHealth.Instance.Heal(healAmount);

        SpawnHealEffect();
    }

    public override bool CanUse()
    {
        if (!base.CanUse())
            return false;

        // TODO: Cek apakah player butuh healing
        // Contoh: return PlayerHealth.Instance.CurrentHealth < PlayerHealth.Instance.MaxHealth;

        return true;
    }
    #endregion

    #region Lifecycle Callbacks
    protected override void OnInitialized()
    {
        base.OnInitialized();
        Debug.Log($"Heal Card initialized with heal amount: {healAmount}");
    }

    public override void OnPreUse()
    {
        base.OnPreUse();
        Debug.Log($"[{GetCardName()}] Preparing to heal...");
    }

    public override void OnPostUse()
    {
        base.OnPostUse();
        Debug.Log($"[{GetCardName()}] Healing complete!");
    }
    #endregion

    #region Visual Effects
    private void SpawnHealEffect()
    {
        Debug.Log("Heal effect spawned!");
    }
    #endregion
}
