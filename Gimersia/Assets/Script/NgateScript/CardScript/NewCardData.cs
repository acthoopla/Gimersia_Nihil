using System.Collections.Generic;
using UnityEngine;
using static NewCardSystem;

[CreateAssetMenu(fileName = "NewCardData", menuName = "CardData")]
public class NewCardData : ScriptableObject
{
    [Header("Visual Info (UI)")]
    public string cardName;
    [TextArea] public string description;
    public Sprite cardIcon; 
    public GameObject cardPrefab;

    [Header("Properties")]
    public CardCategory category;

    [Header("Logic Effects (SRP)")]
    public List<CardEffect> effects;

    // --- Helper Properties (Agar cocok dengan BaseCard) ---
    public string CardName => cardName;
    public string CardDescription => description;
    public Sprite CardIcon => cardIcon;
    public GameObject CardPrefab => cardPrefab;

    // Fungsi Logika Utama
    public void Play(PlayerState target)
    {
        Debug.Log($"[Logic] Executing: {cardName}");
        if (target == null || effects == null) return;
        foreach (var effect in effects)
        {
            if (effect != null) effect.ApplyEffect(target);
        }
    }
}