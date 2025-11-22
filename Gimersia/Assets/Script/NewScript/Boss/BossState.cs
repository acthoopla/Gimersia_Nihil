using UnityEngine;

/// <summary>
/// BossState: menyimpan data boss (HP, apakah boss punya buff double damage, dll)
/// SRP: hanya data/state, tidak melakukan kalkulasi damage.
/// </summary>
public class BossState : MonoBehaviour
{
    [Header("Boss Stats")]
    public int maxHP = 100;
    public int currentHP = 100;

    [Header("Boss Flags")]
    [Tooltip("Jika true, boss damage akan dikalikan 2 saat menyerang.")]
    public bool doubleDamageActive = false;

    [Header("References (opsional)")]
    public Animator animator; // kalau pakai Mecanim
    public Transform hitPoint; // posisi spawn VFX

    public void ResetHP() { currentHP = maxHP; }

    public bool IsDead => currentHP <= 0;
}
