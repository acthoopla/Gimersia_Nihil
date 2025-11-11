using UnityEngine;
using UnityEngine.UI;

public enum CardEffectType
{
    None,
    AthenaBlessing,  // Memberikan perlindungan dari dimundurkan 2 cycle
    ShieldOfAthena,  // Imune efek snake 1 kali
    HermesFavors,    // +2 pada nilai roll berikutnya
    PoseidonWaves,   // Mendorong seluruh bidak di row
    ZeusWrath,       // Pilih 1 pemain, mundurkan 3 blok
    AresProvocation, // -1 dadu sendiri, +2 dadu memundurkan lawan
    OdinWisdom,      // +1 dadu untuk roll berikutnya
    ThorHammer,      // Memundurkan pemain didepanmu 2 tile
    LokiTricks,      // Menukar posisi mu dengan pemain target
    RaLight,         // Maju 2 blok ke depan
    AnubisJudgment           // Memilih 1 pemain untuk di
}

// Ini memungkinkan Anda membuat "aset" kartu baru dari menu Create di Unity
[CreateAssetMenu(fileName = "New Card", menuName = "Game/Card")]
public class CardData : ScriptableObject
{
    public string cardName;
    [TextArea(3, 10)]
    public string description;
    public Sprite cardImage;

    [Tooltip("Hanya untuk kosmetik/UI")]
    public string cardType; // "Buff", "DeBuff", "Double Edge"
    [Tooltip("Hanya untuk kosmetik/UI")]
    public string cardTarget; // "Self", "Enemy", "Row"

    [Header("Effect Logic")]
    [Tooltip("Ini yang menentukan logika kartu")]
    public CardEffectType effectType;

    [Tooltip("Nilai angka untuk efek (misal: 2 cycle, +2 roll, 3 blok)")]
    public int intValue;
}
