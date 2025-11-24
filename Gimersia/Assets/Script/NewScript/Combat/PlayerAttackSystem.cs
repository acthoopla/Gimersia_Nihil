using UnityEngine;
using System.Collections;

public class PlayerAttackSystem : MonoBehaviour
{
    public static PlayerAttackSystem Instance { get; private set; }

    [Header("References")]
    public BoardManager board;
    public CombatSystem combat;
    public BossState boss;

    [Header("Damage Curve Per Row (size = 10)")]
    public int[] rowDamage = new int[10];

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Entry point ketika player mendarat di Attack Tile.
    /// </summary>
    public void StartPlayerAttackSequence(PlayerState player, Tiles tile)
    {
        StartCoroutine(PlayerAttackRoutine(player, tile));
    }

    private IEnumerator PlayerAttackRoutine(PlayerState player, Tiles tile)
    {
        // 1. TUNGGU ANIMASI LANDING DARI PLAYER SELESAI
        //-----------------------------------------------------
        // PLACEHOLDER ANIM HOOK:
        // panggil animasi landing + tunggu sampai selesai (opsional)
        if (player.pawn != null)
        {
            // COMMENT: Letakkan animasi landing di sini
            // player.pawn.AnimPlay("Landing");
        }
        yield return new WaitForSeconds(0.2f);


        // 2. HITUNG ROW TILE
        int row = board.GetRowForTile(tile.tileID);
        int baseDamage = GetBaseDamageForRow(row);


        // 3. CEK BUFF DOUBLE EDGE
        bool hasDouble = player.HasDoubleEdge;
        int finalDamage = hasDouble ? baseDamage * 2 : baseDamage;


        // 4. ANIMASI PLAYER ATTACK
        //-----------------------------------------------------
        // COMMENT: Disinilah kamu memanggil animasi serangan player
        // player.pawn.AnimPlay("Attack");
        //-----------------------------------------------------
        yield return new WaitForSeconds(0.5f); // tunggu anim selesai


        // 5. ANIMASI BOSS KENA DAMAGE
        //-----------------------------------------------------
        // COMMENT: animasi kena pukul/ shake camera / hit effect
        // boss.Animator.Play("Hit");
        //-----------------------------------------------------
        yield return new WaitForSeconds(0.3f);


        // 6. APPLY DAMAGE KE BOSS (CombatSystem)
        combat.ApplyDamageToBoss(boss, finalDamage, "AttackTile");


        // 7. BROADCAST EVENT (UI / FX)
        EventBus.PlayerAttackBossEvent(player, finalDamage, tile);


        Debug.Log($"[PlayerAttackSystem] {player.name} menyerang Boss untuk {finalDamage} damage (row {row}).");
    }


    private int GetBaseDamageForRow(int row)
    {
        if (row < 1 || row > 10)
            return 1;
        return rowDamage[row - 1];
    }
}
