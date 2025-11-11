/*
* File ini berisi semua enum yang berhubungan dengan
* sistem kartu agar mudah diakses dari script lain.
*/

// Berdasarkan "Buff", "DeBuff", "Double Edge"
public enum CardArchetype
{
    Buff,
    Debuff,
    DoubleEdge,
    BuffAndDebuff
}

// Berdasarkan "Self", "Enemy", "Enemies", "Row"
// (Ini mencakup semua tag target di gambarmu)
public enum CardTargetType
{
    Self,
    Enemy,
    Enemies,
    All,
    Row
}

// Enum untuk logika efek (tidak berubah, tapi lebih rapi di sini)
public enum CardEffectType
{
    None,
    AthenaBlessing,
    ShieldOfAthena,
    HermesFavors,
    PoseidonWaves,
    ZeusWrath,
    AresProvocation,
    OdinWisdom,
    ThorHammer,
    LokiTricks,
    RaLight,
    AnubisJudgment,
    IsisProtection,
    AmaterasuRadiance,
    SusanooStorm,
    InariFortune
}