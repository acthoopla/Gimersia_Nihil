using UnityEngine;

/// <summary>
/// NewCardData: ScriptableObject untuk definisi kartu.
/// NOTE:
/// - Tidak mendefinisikan CardEffectType di sini (menghindari duplikasi).
/// - Category type digunakan dari NewCardSystem.CardCategory (enum didefinisikan di NewCardSystem).
/// </summary>
[CreateAssetMenu(fileName = "NewCardData", menuName = "GDD/Card/NewCardData")]
public class NewCardData : ScriptableObject
{
    public string cardName;
    [TextArea] public string description;

    // CardEffectType klo blum ada tambahin aja
    public CardEffectType effectType;

    // gunakan NewCardSystem.CardCategory (enum berada di NewCardSystem)
    public NewCardSystem.CardCategory category;

    public int intValue; // generic numeric value (damage, move, etc)
    public bool isDefensive = false; // contoh flag
    public Sprite icon;
}
