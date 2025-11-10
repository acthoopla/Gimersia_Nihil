using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// PlayerPawn.cs (Versi Final Gabungan)
/// Menggunakan 'SetManager' (lebih efisien)
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
    public Renderer bodyRenderer;          // mesh renderer utama
    public TextMeshPro labelTMP;           // optional: label 3D (P1, P2, ...)
    public Color[] defaultColors;          // warna per index
    public GameObject reversedBadge;       // optional: objek badge (TextMeshPro 3D atau sprite) yang muncul saat reversed

    [Header("Movement")]
    public float stepSpeed = 5f;           // kecepatan movement
    public float stepDelay = 0.08f;        // jeda antar step per tile

    // --- PERUBAHAN DI SINI ---
    // internal
    private Vector3 baseScale;
    private MultiplayerManager manager; // Referensi yang di-cache (lebih efisien)
    // -------------------------

    void Awake()
    {
        baseScale = transform.localScale;
        if (reversedBadge != null) reversedBadge.SetActive(false);
        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();
    }

    // --- TAMBAHKAN FUNGSI INI ---
    /// <summary>
    /// Dipanggil oleh MultiplayerManager saat spawn untuk menyimpan referensi.
    /// </summary>
    public void SetManager(MultiplayerManager mgr)
    {
        manager = mgr;
    }
    // ----------------------------

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
        if (reversedBadge != null) reversedBadge.SetActive(wasReversedThisCycle);
    }

    public void SetHighlight(bool on)
    {
        float factor = on ? 1.10f : 1.0f;
        transform.localScale = baseScale * factor;
    }

    public void ShowReversedBadge(bool show)
    {
        wasReversedThisCycle = show;
        if (reversedBadge != null) reversedBadge.SetActive(show);
    }

    // -----------------------------------
    // Input: OnMouseDown -> kirim ke Manager
    // -----------------------------------

    // --- MODIFIKASI FUNGSI INI ---
    void OnMouseDown()
    {
        Debug.Log($"PlayerPawn clicked: {name}");

        // Gunakan referensi manager yang sudah di-cache (jauh lebih cepat)
        if (manager != null)
        {
            manager.OnPawnClicked(this);
        }
        else
        {
            Debug.LogError("Manager reference is missing on " + name + "! Make sure SpawnPlayers calls SetManager().");
        }
    }
    // ---------------------------

    // -----------------------------------
    // Movement coroutines: (TIDAK BERUBAH)
    // -----------------------------------
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