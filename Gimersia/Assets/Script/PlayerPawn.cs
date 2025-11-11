using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerPawn : MonoBehaviour
{
    [Header("Player Data")]
    public int currentTileID = 1;
    public int playerIndex = 0; // 0-based
    private MultiplayerManager manager; // <-- TAMBAHKAN INI

    [Header("Visuals")]
    public Renderer bodyRenderer;
    public TextMeshPro labelTMP;
    public Color[] defaultColors; // assign di prefab atau override di inspector
    public GameObject reversedBadge; // <-- TAMBAHKAN INI

    [Header("Movement")]
    public float stepSpeed = 5f;
    public float stepDelay = 0.08f;
    public float rotationSpeed = 360f; // <-- TAMBAHKAN INI (Kecepatan putar)

    // Status
    [HideInInspector]
    public bool wasReversedThisCycle = false; // <-- TAMBAHKAN INI

    private Vector3 baseScale;

    void Awake()
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();

        baseScale = transform.localScale;
        if (reversedBadge != null) reversedBadge.SetActive(false); // Pastikan mati di awal
    }

    // --- TAMBAHKAN 3 FUNGSI BARU DI BAWAH INI ---

    /// <summary>
    /// Memberi tahu pawn siapa managernya
    /// </summary>
    public void SetManager(MultiplayerManager m)
    {
        manager = m;
    }

    /// <summary>
    /// Menampilkan/menyembunyikan tanda 'kena reverse'
    /// </summary>
    public void ShowReversedBadge(bool show)
    {
        if (reversedBadge != null)
        {
            reversedBadge.SetActive(show);
        }
    }

    /// <summary>
    /// Dipanggil oleh Manager saat pawn ini di-klik
    /// </summary>
    void OnMouseDown()
    {
        if (manager != null)
        {
            manager.OnPawnClicked(this);
        }
    }

    // ------------------------------------------

    public void SetVisualIndex(int idx)
    {
        playerIndex = idx;
        if (labelTMP != null) labelTMP.text = $"P{idx + 1}";
        if (bodyRenderer != null && defaultColors != null && defaultColors.Length > 0)
        {
            Color c = defaultColors[idx % defaultColors.Length];
            bodyRenderer.material.color = c;
        }

        transform.localScale = baseScale;
    }

    public void SetHighlight(bool on)
    {
        float factor = on ? 1.15f : 1.0f;
        transform.localScale = baseScale * factor;
    }

    // Move per-tile (smooth)
    public IEnumerator MoveToTile(int targetTileID, System.Func<int, Vector3> tilePosProvider)
    {
        int start = currentTileID;
        if (targetTileID == start) yield break;

        if (targetTileID > start)
        {
            // Gerak Maju
            for (int i = start + 1; i <= targetTileID; i++)
            {
                Vector3 targetPos = tilePosProvider(i);
                while (Vector3.Distance(transform.position, targetPos) > 0.01f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, stepSpeed * Time.deltaTime);
                    yield return null;
                }
                transform.position = targetPos;
                currentTileID = i;

                // --- LOGIKA ROTASI BARU ---
                int row = (i - 1) / 10; // (Tile 1-10=row 0), (Tile 11-20=row 1)
                float targetYAngle = (row % 2 == 0) ? 0f : 180f; // Row ganjil 180, row genap 0
                yield return StartCoroutine(SmoothRotate(targetYAngle)); // Tunggu rotasi selesai
                // --------------------------

                yield return new WaitForSeconds(stepDelay);
            }
        }
        else
        {
            // Gerak Mundur (Bounce Back)
            for (int i = start - 1; i >= targetTileID; i--)
            {
                Vector3 targetPos = tilePosProvider(i);
                while (Vector3.Distance(transform.position, targetPos) > 0.01f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, stepSpeed * Time.deltaTime);
                    yield return null;
                }
                transform.position = targetPos;
                currentTileID = i;

                // --- LOGIKA ROTASI BARU ---
                int row = (i - 1) / 10;
                float targetYAngle = (row % 2 == 0) ? 0f : 180f;
                yield return StartCoroutine(SmoothRotate(targetYAngle));
                // --------------------------

                yield return new WaitForSeconds(stepDelay);
            }
        }
    }

    // Teleport (used for ladder/snake)
    public IEnumerator TeleportToTile(int targetTileID, System.Func<int, Vector3> tilePosProvider)
    {
        Vector3 targetPos = tilePosProvider(targetTileID);
        float speed = stepSpeed * 2.0f;
        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPos;
        currentTileID = targetTileID;

        // --- LOGIKA ROTASI BARU (INSTAN) ---
        // Saat teleport, rotasi langsung di-snap
        int row = (currentTileID - 1) / 10;
        float targetYAngle = (row % 2 == 0) ? 0f : 180f;
        transform.rotation = Quaternion.Euler(0, targetYAngle, 0);
        // ---------------------------------
    }

    // --- TAMBAHKAN 2 FUNGSI BARU DI BAWAH INI ---

    /// <summary>
    /// Coroutine untuk berputar halus ke sudut Y tertentu
    /// </summary>
    IEnumerator SmoothRotate(float targetYAngle)
    {
        Quaternion targetRotation = Quaternion.Euler(0, targetYAngle, 0);

        // Jika sudah dekat, snap saja
        if (Quaternion.Angle(transform.rotation, targetRotation) < 1f)
        {
            transform.rotation = targetRotation;
            yield break;
        }

        Quaternion startRotation = transform.rotation;
        float duration = Quaternion.Angle(startRotation, targetRotation) / rotationSpeed;
        float t = 0f;

        while (t < 1.0f)
        {
            t += Time.deltaTime / duration;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        transform.rotation = targetRotation; // Snap ke rotasi final
    }

    /// <summary>
    /// Menggerakkan pawn ke posisi offset baru (untuk fix tumpuk)
    /// </summary>
    public void MoveToPosition(Vector3 targetPos)
    {
        // Hanya gerak jika posisinya memang beda
        if (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            StartCoroutine(NudgeToPosition(targetPos));
        }
    }

    IEnumerator NudgeToPosition(Vector3 targetPos)
    {
        float nudgeSpeed = stepSpeed * 1.5f; // Sedikit lebih cepat
        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, nudgeSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPos;
    }
}