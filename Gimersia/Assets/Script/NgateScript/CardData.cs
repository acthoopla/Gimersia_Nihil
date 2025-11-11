using UnityEngine;
using UnityEngine.UI;

// Enum dari file CardEnums.cs
// public enum CardEffectType { ... }
// public enum CardArchetype { ... }
// public enum CardTargetType { ... }


// Ini memungkinkan Anda membuat "aset" kartu baru dari menu Create di Unity
[CreateAssetMenu(fileName = "New Card", menuName = "Game/Card")]
public class CardData : ScriptableObject
{
    [Header("Info Dasar")]
    public string cardName;
    [TextArea(3, 10)]
    public string description;
    public Sprite cardImage;

    // --- PERUBAHAN DI SINI ---
    [Header("Tag Kosmetik (UI)")]
    [Tooltip("Tipe kartu (Buff, DeBuff, dll.)")]
    public CardArchetype cardArchetype; // <-- MENGGANTIKAN string cardType

    [Tooltip("Target utama kartu (Self, Enemy, dll.)")]
    public CardTargetType cardTargetType; // <-- MENGGANTIKAN string cardTarget
    // -------------------------

    [Header("Effect Logic")]
    [Tooltip("Ini yang menentukan logika kartu")]
    public CardEffectType effectType;

    [Tooltip("Nilai angka untuk efek (misal: 2 cycle, +2 roll, 3 blok)")]
    public int intValue;
}