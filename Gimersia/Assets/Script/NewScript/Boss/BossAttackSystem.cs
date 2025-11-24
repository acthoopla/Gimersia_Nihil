using System.Collections;
using UnityEngine;

/// <summary>
/// BossAttackSystem (SRP)
/// - Meng-handle boss -> player damage kalkulasi (berdasarkan row)
/// - Memanggil anim hook (komentar) agar animator/particle dapat di-trigger
/// - Menggunakan CombatSystem untuk benar-benar apply damage
/// - Emit event via EventBus untuk UI/VFX
/// </summary>
public class BossAttackSystem : MonoBehaviour
{
    public static BossAttackSystem Instance { get; private set; }

    [Header("References")]
    public BoardManager boardManager; // harus berisi GetRowForTile(int)
    public CombatSystem combatSystem; // gunakan ApplyDamageToPlayer
    public BossState bossState;

    [Header("Damage per row (index 0 => row1, index 9 => row10)")]
    [Tooltip("Isi 10 nilai damage sesuai GDD (size=10).")]
    public int[] damagePerRow = new int[10] { 1, 2, 2, 3, 4, 5, 6, 8, 9, 10 };

    [Header("Timings (anim waits)")]
    public float waitAfterBossAttackAnim = 0.5f;
    public float waitAfterPlayerHitAnim = 0.2f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Dipanggil saat player mendarat di tile yang menyebabkan boss melakukan serangan pada player.
    /// - player: PlayerState dari pemain yang kena
    /// - landedTileID: tileID tempat player mendarat (dipakai untuk hitung row)
    /// Behavior: menghitung damage sesuai row -> apply multipliers -> panggil anim -> apply damage via CombatSystem
    /// </summary>
    public void HandleBossAttackOnPlayer(PlayerState player, int landedTileID)
    {
        StartCoroutine(BossAttackRoutine(player, landedTileID));
    }

    private IEnumerator BossAttackRoutine(PlayerState player, int landedTileID)
    {
        if (player == null || bossState == null || boardManager == null || combatSystem == null)
        {
            Debug.LogWarning("[BossAttackSystem] Missing references. Abort boss attack.");
            yield break;
        }

        // 1. determine row
        int row = boardManager.GetRowForTile(landedTileID);
        int baseDamage = GetDamageForRow(row);

        // 2. determine multiplier (double edge)
        // NOTE: diagram menunjukkan ada pengecekan double-edge.
        // Kita check bossState.doubleDamageActive (boss buff). Jika mau check player-specific buff, cek player.HasDoubleEdge.
        bool bossDouble = bossState.doubleDamageActive;
        bool playerDoubleEdge = false;
        // jika PlayerState punya flag HasDoubleEdge, coba ambil (refleksi aman)
        try
        {
            var pi = player.GetType().GetProperty("HasDoubleEdge");
            if (pi != null) playerDoubleEdge = (bool)pi.GetValue(player);
        }
        catch { playerDoubleEdge = false; }

        int finalDamage = baseDamage;

        // By design: ONLY one doubling source applies; definisikan prioritas:
        // - jika boss.doubleDamageActive => boss damage x2
        // - else if playerDoubleEdge (mis. pemain 'bersedia' terima lebih damage? This is unusual) => apply as design.
        if (bossDouble) finalDamage *= 2;
        else if (playerDoubleEdge) finalDamage *= 2;

        // 3. ANIMASI (HOOK) -> Boss attack anim
        // COMMENT: panggil animasi boss "Attack" / spawn VFX di sini.
        if (bossState.animator != null)
        {
            // contoh: bossState.animator.SetTrigger("Attack");
            // Turunkan: masukkan nama trigger anim sesuai animatormu
            // bossState.animator.SetTrigger("Attack");
        }
        // Jika kamu ingin spawn efek skill:
        // Spawn VFX: Instantiate(vfxPrefab, bossState.hitPoint.position, Quaternion.identity);

        // tunggu sebentar sampai anim ter-trigger
        yield return new WaitForSeconds(waitAfterBossAttackAnim);

        // 4. ANIMASI PLAYER KENA (HOOK)
        // COMMENT: di sini panggil pemain untuk tampilkan anim "Hurt" atau "Stagger".
        // contoh:
        if (player != null)
        {
            var pawn = TryGetPlayerPawn(player);
            Animator anim = null;
            if (pawn != null)
            {
                var mb = pawn as MonoBehaviour;
                if (mb != null)
                    anim = mb.GetComponent<Animator>();
            }
            if (anim != null) anim.SetTrigger("Hurt");
        }

        yield return new WaitForSeconds(waitAfterPlayerHitAnim);

        // 5. APPLY DAMAGE via CombatSystem -> CombatSystem akan handle immunities/defenseFromCards
        combatSystem.ApplyDamageToPlayer(player, finalDamage, "BossAttack");

        // 6. BROADCAST via EventBus agar UI / VFX / audio dapat merespon
        try { EventBus.DamageTaken(player, finalDamage, "BossAttack"); } catch { /* jika overload berbeda, EventBus fallback akan handle */ }
        try { EventBus.TileLanded(player, boardManager.GetTileByID(landedTileID)); } catch { }

        Debug.Log($"[BossAttackSystem] Boss attacked player {player.name} (row {row}) for {finalDamage} dmg (base {baseDamage}).");
    }

    private int GetDamageForRow(int row)
    {
        if (damagePerRow == null || damagePerRow.Length < 10) return 1;
        int r = Mathf.Clamp(row, 1, damagePerRow.Length);
        return damagePerRow[r - 1];
    }

    // helper: try get player's pawn component which may contain animator
    private PlayerPawn TryGetPlayerPawn(PlayerState player)
    {
        // assume PlayerState is or has reference to PlayerPawn component
        if (player == null) return null;
        try
        {
            var pi = player.GetType().GetProperty("Pawn");
            if (pi != null) return pi.GetValue(player) as PlayerPawn;
        }
        catch { }
        // fallback: try GetComponent if PlayerState is MonoBehaviour
        var comp = player as MonoBehaviour;
        if (comp != null)
        {
            var pp = comp.GetComponent<PlayerPawn>();
            if (pp != null) return pp;
        }
        return null;
    }
}
