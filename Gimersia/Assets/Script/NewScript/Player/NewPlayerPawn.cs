using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class NewPlayerPawn : MonoBehaviour
{
    [Header("References")]
    public PlayerState playerState;
    public GameObject reversedBadge;
    public Renderer bodyRenderer;
    public Animator animator;

    [Header("Movement Tuning")]
    public float stepSpeed = 5f;
    public float stepDelay = 0.05f;
    public float turnDuration = 0.4f;   // Durasi putar balik
    public float turnWait = 0.1f;       // Jeda setelah putar sebelum mulai jalan
    public float teleportSpeed = 15f;
    public float positionTolerance = 0.05f;

    [Header("Visual FX")]
    public GameObject ladderParticle;

    private Vector3 baseScale;
    public bool IsMoving { get; private set; }

    void Awake()
    {
        if (playerState == null) playerState = GetComponent<PlayerState>();
        baseScale = transform.localScale;
        if (reversedBadge != null) reversedBadge.SetActive(false);
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
    }

    #region Public Visual helpers
    public void SetVisualIndex(int idx) { transform.localScale = baseScale; }
    public void SetHighlight(bool on) { transform.localScale = baseScale * (on ? 1.15f : 1.0f); }
    public void ShowReversedBadge(bool show) { if (reversedBadge) reversedBadge.SetActive(show); }
    public void PlayLadderParticle() { if (ladderParticle) ladderParticle.SetActive(true); }
    public void StopLadderParticle() { if (ladderParticle) ladderParticle.SetActive(false); }
    #endregion

    #region Move Coroutines

    public IEnumerator MoveToTile(int targetTileID, Func<int, Vector3> tilePosProvider)
    {
        if (tilePosProvider == null)
        {
            var bm = BoardManager.Instance != null ? BoardManager.Instance : FindObjectOfType<BoardManager>();
            if (bm != null) tilePosProvider = (id) => bm.GetTilePosition(id);
            else yield break;
        }
        yield return StartCoroutine(MoveToTile_Internal(targetTileID, tilePosProvider(targetTileID), false));
    }

    public IEnumerator MoveToTile(int targetTileID, Vector3 targetPos)
    {
        yield return StartCoroutine(MoveToTile_Internal(targetTileID, targetPos, false));
    }

    public IEnumerator MoveToTile(int targetTileID)
    {
        var bm = BoardManager.Instance != null ? BoardManager.Instance : FindObjectOfType<BoardManager>();
        if (bm != null) yield return StartCoroutine(MoveToTile_Internal(targetTileID, bm.GetTilePosition(targetTileID), false));
    }

    public IEnumerator TeleportToTile(int targetTileID)
    {
        var bm = BoardManager.Instance != null ? BoardManager.Instance : FindObjectOfType<BoardManager>();
        if (bm != null) yield return StartCoroutine(MoveToTile_Internal(targetTileID, bm.GetTilePosition(targetTileID), true));
    }

    #endregion

    #region Internal Movement Logic

    private IEnumerator MoveToTile_Internal(int targetTileID, Vector3 targetPos, bool isTeleport)
    {
        IsMoving = true;
        if (playerState == null) playerState = GetComponent<PlayerState>();

        // === TELEPORT ===
        if (isTeleport)
        {
            while (Vector3.Distance(transform.position, targetPos) > positionTolerance)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPos, teleportSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = targetPos;
            if (playerState) playerState.TileID = targetTileID;
            ForceRotationToRow(targetTileID);
            IsMoving = false;
            yield break;
        }

        // === STEP-BY-STEP ===
        int startTile = (playerState != null) ? playerState.TileID : 1;

        // Hadap arah awal
        ForceRotationToRow(startTile);

        if (startTile > 0 && targetTileID != startTile)
        {
            // MAJU
            if (targetTileID > startTile)
            {
                for (int t = startTile + 1; t <= targetTileID; t++)
                {
                    Vector3 nextPos = (BoardManager.Instance != null) ? BoardManager.Instance.GetTilePosition(t) : targetPos;

                    // --- DETEKSI BELOKAN (GANTI BARIS) ---
                    // Cek: Apakah tile tujuan (t) ada di baris yang berbeda dari tile sebelumnya (t-1)?
                    // Cara paling aman: Cek Row index
                    int prevRow = GetRowIndex(t - 1);
                    int currentRow = GetRowIndex(t);
                    bool isRowChange = (currentRow != prevRow);

                    if (isRowChange)
                    {
                        // 1. BERHENTI & PUTAR BALIK DULU (Menghadap Jalur Baru)
                        yield return StartCoroutine(RotateToRow(t));
                        yield return new WaitForSeconds(turnWait);

                        // 2. BARU JALAN NAIK KE TILE TUJUAN
                        yield return StartCoroutine(SlideToPosition(nextPos));
                    }
                    else
                    {
                        // Jalan Lurus Biasa (Sliding)
                        yield return StartCoroutine(SlideToPosition(nextPos));
                    }

                    // Update Data
                    if (playerState != null) playerState.TileID = t;

                    // Jeda langkah
                    if (!isRowChange) yield return new WaitForSeconds(stepDelay);
                }
            }
            // MUNDUR
            else
            {
                for (int t = startTile - 1; t >= targetTileID; t--)
                {
                    Vector3 nextPos = (BoardManager.Instance != null) ? BoardManager.Instance.GetTilePosition(t) : targetPos;
                    yield return StartCoroutine(SlideToPosition(nextPos));
                    if (playerState != null) playerState.TileID = t;
                    yield return new WaitForSeconds(stepDelay);
                }
                yield return StartCoroutine(RotateToRow(targetTileID));
            }
        }
        else
        {
            yield return StartCoroutine(SlideToPosition(targetPos));
        }

        transform.position = targetPos;
        if (playerState) playerState.TileID = targetTileID;
        ForceRotationToRow(targetTileID);
        IsMoving = false;
    }

    // --- HELPER ACTIONS ---

    private IEnumerator SlideToPosition(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > positionTolerance)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, stepSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
    }

    // Rotasi Halus menghadap Arah Baris (Ganjil/Genap)
    private IEnumerator RotateToRow(int tileID)
    {
        float targetY = GetRowRotationY(tileID);
        Quaternion targetRot = Quaternion.Euler(0, targetY, 0);
        Quaternion startRot = transform.rotation;

        // Jika sudutnya sama, tidak perlu putar (hemat waktu)
        if (Quaternion.Angle(startRot, targetRot) < 1f) yield break;

        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / turnDuration;
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }
        transform.rotation = targetRot;
    }

    private void ForceRotationToRow(int tileID)
    {
        float targetY = GetRowRotationY(tileID);
        transform.rotation = Quaternion.Euler(0, targetY, 0);
    }

    private float GetRowRotationY(int tileID)
    {
        int row = GetRowIndex(tileID);
        // Row Ganjil (1,3,5) -> 0 derajat (Kanan)
        // Row Genap (2,4,6) -> 180 derajat (Kiri)
        return (row % 2 != 0) ? 0f : 180f;
    }

    // Helper untuk ambil nomor baris
    private int GetRowIndex(int tileID)
    {
        if (tileID <= 0) return 1;
        // Prioritas: Tanya BoardManager (karena dia punya logika grid yang akurat)
        if (BoardManager.Instance != null) return BoardManager.Instance.GetRow(tileID);
        // Fallback matematika sederhana (1-10 = Row 1, 11-20 = Row 2)
        return ((tileID - 1) / 10) + 1;
    }

    #endregion
}