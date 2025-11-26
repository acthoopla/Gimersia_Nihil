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
        // Pastikan HP penuh saat mulai
        currentHP = maxHP;
    }

    // --- [FIX] WIN CONDITION LOGIC ---

    public void TakeDamage(int damage)
    {
        // Jangan dipukul kalau sudah mati (biar UI Win gak ke-trigger 2x)
        if (currentHP <= 0) return;

        currentHP -= damage;
        Debug.Log($"Boss terkena {damage} damage! Sisa HP: {currentHP}");

        if (currentHP <= 0)
        {
            currentHP = 0;
            Die();
        }
    }

    private void Die()
    {
        Debug.Log(">>> VICTORY: Boss Defeated! <<<");

        if (animator != null) animator.SetTrigger("Die");

        // Panggil UI Victory
        if (UIController.Instance != null)
        {
            UIController.Instance.ShowVictory();
        }

        // Hentikan permainan di TurnManager
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.state = TurnManager.TurnState.GameOver;
        }
    }

    public void ResetHP()
    {
        currentHP = maxHP;
    }

    public bool IsDead => currentHP <= 0;
}