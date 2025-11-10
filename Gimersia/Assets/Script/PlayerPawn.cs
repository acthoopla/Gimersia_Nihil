using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// PlayerPawn.cs (versi final, kompatibel dengan MultiplayerManager.cs)
/// - Menyediakan data pemain: currentTileID, playerIndex
/// - Flag wasReversedThisCycle untuk imun sementara
/// - Fungsi ShowReversedBadge untuk menyalakan/mematikan badge visual
/// - OnMouseDown memanggil MultiplayerManager.OnPawnClicked(this)
/// - MoveToTile & TeleportToTile coroutine untuk animasi gerak
/// </summary>
[RequireComponent(typeof(Collider))] // memastikan ada collider agar OnMouseDown dipanggil
public class PlayerPawn : MonoBehaviour
{
    [Header("Data Pemain")]
    public int currentTileID = 1;
    public int playerIndex = 0; // 0-based index
    [Tooltip("True jika pawn ini sudah dimundurkan di cycle saat ini")]
    public bool wasReversedThisCycle = false;

    [Header("Visuals")]
    public Renderer bodyRenderer;         // mesh renderer utama
    public TextMeshPro labelTMP;          // optional: label 3D (P1, P2, ...)
    public Color[] defaultColors;         // warna per index
    public GameObject reversedBadge;      // optional: objek badge (TextMeshPro 3D atau sprite) yang muncul saat reversed

    [Header("Movement")]
    public float stepSpeed = 5f;          // kecepatan movement
    public float stepDelay = 0.08f;       // jeda antar step per tile

    // internal
    private Vector3 baseScale;

    void Awake()
    {
        // simpan skala awal agar highlight tidak merusak skala dasar
        baseScale = transform.localScale;

        // safety: sembunyikan badge saat start
        if (reversedBadge != null) reversedBadge.SetActive(false);

        // jika tidak diassign, coba ambil renderer otomatis
        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();
    }

    /// <summary>
    /// Mengatur tampilan pawn berdasarkan index (warna, label, skala).
    /// Dipanggil saat Spawn atau saat ingin reset visual.
    /// </summary>
    public void SetVisualIndex(int idx)
    {
        playerIndex = idx;

        if (labelTMP != null) labelTMP.text = $"P{idx + 1}";

        if (bodyRenderer != null && defaultColors != null && defaultColors.Length > 0)
        {
            Color c = defaultColors[idx % defaultColors.Length];
            // Jangan mengganti sharedMaterial di runtime jika tidak perlu; gunakan material instance jika ingin unik
            bodyRenderer.material.color = c;
        }

        // pastikan skala dikembalikan ke baseScale
        transform.localScale = baseScale;

        // update badge sesuai flag
        if (reversedBadge != null) reversedBadge.SetActive(wasReversedThisCycle);
    }

    /// <summary>
    /// Highlight visual kecil untuk menandai giliran atau target.
    /// Menggunakan baseScale agar highlight reversible.
    /// </summary>
    public void SetHighlight(bool on)
    {
        float factor = on ? 1.10f : 1.0f;
        transform.localScale = baseScale * factor;
    }

    /// <summary>
    /// Tampilkan atau sembunyikan badge reversed.
    /// Panggil ini dari Manager saat flag di-set/reset.
    /// </summary>
    public void ShowReversedBadge(bool show)
    {
        wasReversedThisCycle = show;
        if (reversedBadge != null) reversedBadge.SetActive(show);
    }

    // -----------------------------------
    // Input: OnMouseDown -> kirim ke Manager
    // -----------------------------------
    void OnMouseDown()
    {
        // Debug line kecil agar mudah trace di Console
        Debug.Log($"PlayerPawn clicked: {name}");

        // Cari Manager (fast path: cache jika perlu). Manager akan memutuskan apakah klik relevan.
        MultiplayerManager mgr = FindObjectOfType<MultiplayerManager>();
        if (mgr != null)
        {
            mgr.OnPawnClicked(this);
        }
    }

    // -----------------------------------
    // Movement coroutines: maju / mundur per tile
    // -----------------------------------
    /// <summary>
    /// Gerak smooth per tile dari currentTileID -> targetTileID (bisa maju atau mundur).
    /// tilePosProvider: fungsi yang memberikan posisi world untuk tiap tile ID.
    /// </summary>
    public IEnumerator MoveToTile(int targetTileID, System.Func<int, Vector3> tilePosProvider)
    {
        int start = currentTileID;
        if (targetTileID == start) yield break;

        // Jika maju
        if (targetTileID > start)
        {
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
                yield return new WaitForSeconds(stepDelay);
            }
        }
        else
        {
            // Jika mundur
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
                yield return new WaitForSeconds(stepDelay);
            }
        }
    }

    /// <summary>
    /// Teleportation (mis. naik tangga / turun ular) — geraknya lebih cepat.
    /// </summary>
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
    }
}
