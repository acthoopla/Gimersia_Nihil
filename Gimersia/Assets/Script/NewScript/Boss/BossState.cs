using UnityEngine;

public class BossState : MonoBehaviour
{
    [Header("Boss Stats")]
    public int maxHP = 100;
    public int currentHP = 100;

    [Header("Boss Flags")]
    public bool doubleDamageActive = false;

    [Header("References")]
    public Animator animator;
    public Transform hitPoint;

    void Awake()
    {
        currentHP = maxHP;
    }

    // --- LOGIC TERIMA DAMAGE (WIN CONDITION) ---
    public void TakeDamage(int damage)
    {
        if (currentHP <= 0) return; // Sudah mati jangan dipukul lagi

        currentHP -= damage;
        if (currentHP < 0) currentHP = 0;

        Debug.Log($"Boss kena {damage} damage. Sisa HP: {currentHP}");

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Boss Mati!");
        if (animator != null) animator.SetTrigger("Die"); // Jika ada animasi

        // Panggil UI Victory
        if (UIController.Instance != null)
        {
            UIController.Instance.ShowVictory();
        }
    }

    public void ResetHP() { currentHP = maxHP; }
    public bool IsDead => currentHP <= 0;
}