using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// NewPlayerPawn (SRP)
/// - Visual-only component untuk pawn pemain.
/// - Menyediakan coroutine MoveToTile / TeleportToTile dengan beberapa signature.
/// - Mengupdate PlayerState.TileID saat berpindah.
/// - Tidak melakukan logika game (damage, card, turn) — hanya animasi/visual.
/// 
/// Pastikan GameObject ini juga memiliki komponen PlayerState (atau NewPlayerState).
/// MovementSystem akan memanggil coroutine ini (via reflection atau langsung).
/// </summary>
[DisallowMultipleComponent]
public class NewPlayerPawn : MonoBehaviour
{
    [Header("References")]
    public PlayerState playerState;
    public GameObject reversedBadge;
    public Renderer bodyRenderer;
    public Animator animator;

    [Header("Movement tuning")]
    public float stepSpeed = 5f;
    public float stepDelay = 0.08f;
    public float rotationSpeed = 720f;
    public float teleportSpeed = 12f;
    public float positionTolerance = 0.01f;

    [Header("Visual FX")]
    public GameObject ladderParticle;

    private Vector3 baseScale;

    // Property status gerak (Penting untuk TurnManager)
    public bool IsMoving { get; private set; } = false;

    void Awake()
    {
        if (playerState == null) playerState = GetComponent<PlayerState>();
        baseScale = transform.localScale;
        if (reversedBadge != null) reversedBadge.SetActive(false);
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
    }

    // --- API MOVETO (Dipanggil Card/Manager) ---
    public IEnumerator MoveToTile(int targetTileID)
    {
        var bm = BoardManager.Instance != null ? BoardManager.Instance : FindObjectOfType<BoardManager>();
        if (bm == null) yield break;
        Vector3 pos = bm.GetTilePosition(targetTileID);
        yield return StartCoroutine(MoveToTile_Internal(targetTileID, pos, false));
    }

    public IEnumerator TeleportToTile(int targetTileID)
    {
        var bm = BoardManager.Instance != null ? BoardManager.Instance : FindObjectOfType<BoardManager>();
        if (bm == null) yield break;
        Vector3 pos = bm.GetTilePosition(targetTileID);
        yield return StartCoroutine(MoveToTile_Internal(targetTileID, pos, true));
    }

    // --- LOGIKA UTAMA ---
    private IEnumerator MoveToTile_Internal(int targetTileID, Vector3 targetPos, bool isTeleport)
    {
        IsMoving = true; // Mulai

        if (playerState == null) playerState = GetComponent<PlayerState>();

        // Rotasi awal menghadap target global
        Vector3 startPos = transform.position;
        Vector3 globalDir = (targetPos - startPos);
        if (globalDir.sqrMagnitude > 0.001f)
        {
            float targetYAngle = Quaternion.LookRotation(globalDir).eulerAngles.y;
            yield return StartCoroutine(SmoothRotateToAngle(targetYAngle));
        }

        // 1. TELEPORT
        if (isTeleport)
        {
            while (Vector3.Distance(transform.position, targetPos) > positionTolerance)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPos, teleportSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = targetPos;
            if (playerState != null) playerState.TileID = targetTileID;
            SnapRotationByRow(targetTileID);
            IsMoving = false;
            yield break;
        }

        // 2. STEP BY STEP
        int startTile = (playerState != null) ? playerState.TileID : -1;

        // Jika data tile awal tidak valid, jalan langsung (fallback)
        if (startTile <= 0 || startTile == targetTileID)
        {
            // Langsung ke target jika diam/error
            transform.position = targetPos;
            if (playerState != null) playerState.TileID = targetTileID;
            SnapRotationByRow(targetTileID);
            IsMoving = false;
            yield break;
        }

        if (targetTileID > startTile)
        {
            // --- MAJU (Forward) ---
            for (int t = startTile + 1; t <= targetTileID; t++)
            {
                Vector3 nextPos = GetPos(t);

                // Animasi Jalan
                while (Vector3.Distance(transform.position, nextPos) > positionTolerance)
                {
                    transform.position = Vector3.MoveTowards(transform.position, nextPos, stepSpeed * Time.deltaTime);
                    yield return null;
                }
                transform.position = nextPos;

                // Update ID per langkah
                if (playerState != null) playerState.TileID = t;
                yield return new WaitForSeconds(stepDelay);
            }
        }
        else
        {
            // --- MUNDUR (Backward) ---
            // Loop dari (Start-1) menurun ke Target
            for (int t = startTile - 1; t >= targetTileID; t--)
            {
                Vector3 nextPos = GetPos(t);

                // Rotasi khusus mundur (gunakan stepDir)
                Vector3 stepDir = nextPos - transform.position;
                if (stepDir.sqrMagnitude > 0.001f)
                {
                    // Menghadap ke arah langkah (walaupun mundur, badan berputar)
                    Quaternion targetRot = Quaternion.LookRotation(stepDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 0.5f);
                }

                // Animasi Jalan
                while (Vector3.Distance(transform.position, nextPos) > positionTolerance)
                {
                    transform.position = Vector3.MoveTowards(transform.position, nextPos, stepSpeed * Time.deltaTime);
                    yield return null;
                }
                transform.position = nextPos;

                // Update ID per langkah
                if (playerState != null) playerState.TileID = t;
                yield return new WaitForSeconds(stepDelay);
            }
        }

        // Final Snap
        transform.position = targetPos;
        if (playerState != null) playerState.TileID = targetTileID;
        SnapRotationByRow(targetTileID);

        IsMoving = false; // Selesai
    }

    // Helper: Ambil posisi tile dengan aman
    private Vector3 GetPos(int tileID)
    {
        if (BoardManager.Instance != null) return BoardManager.Instance.GetTilePosition(tileID);
        return transform.position;
    }

    private IEnumerator SmoothRotateToAngle(float targetYAngle)
    {
        Quaternion targetRot = Quaternion.Euler(0, targetYAngle, 0);
        if (Quaternion.Angle(transform.rotation, targetRot) < 1f) { transform.rotation = targetRot; yield break; }

        float t = 0f;
        Quaternion start = transform.rotation;
        while (t < 1f)
        {
            t += Time.deltaTime * (rotationSpeed / 180f);
            transform.rotation = Quaternion.Slerp(start, targetRot, t);
            yield return null;
        }
        transform.rotation = targetRot;
    }

    private void SnapRotationByRow(int tileID)
    {
        if (tileID <= 0) return;
        int row = BoardManager.Instance != null ? BoardManager.Instance.GetRow(tileID) : ((tileID - 1) / 10) + 1;
        float targetYAngle = (row % 2 != 0) ? 0f : 180f; // Ganjil Kanan, Genap Kiri
        transform.rotation = Quaternion.Euler(0, targetYAngle, 0);
    }
}
