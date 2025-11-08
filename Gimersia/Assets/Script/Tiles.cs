using UnityEngine;

// Enum ini akan muncul sebagai dropdown di Inspector
public enum TileType
{
    Normal,      // Petak biasa
    SnakeStart,  // Petak tempat MULUT Ular (yang bikin turun)
    LadderStart  // Petak tempat BAWAH Tangga (yang bikin naik)
}

// BARU: Atribut ini memastikan script ini harus punya Renderer (MeshRenderer)
[RequireComponent(typeof(Renderer))]
public class Tiles : MonoBehaviour
{
    [Header("Identitas Tile")]
    [Tooltip("ID unik untuk tile ini, urut dari 1 (start) sampai akhir (misal: 100)")]
    public int tileID;

    [Tooltip("Tipe dari tile ini (Normal, Ular, atau Tangga)")]
    public TileType type = TileType.Normal;

    [Header("Logika Ular/Tangga")]
    [Tooltip("Hanya diisi jika tipe adalah SnakeStart atau LadderStart. " +
             "Ini adalah tile tujuan (bisa ditarik dari Hierarchy).")]
    public Tiles targetTile;

    // BARU: Header dan variabel untuk Material
    [Header("Visual Materials")]
    public Material normalMaterial;  // Material 'Biasa'
    public Material snakeMaterial;   // Material 'B' (Ular)
    public Material ladderMaterial;  // Material 'A' (Tangga)

    // BARU: Referensi ke Renderer
    private Renderer tileRenderer;

    // BARU: Fungsi OnValidate() untuk update otomatis di Editor
    void OnValidate()
    {
        ApplyTileVisuals();
    }

    // BARU: Fungsi Start() untuk memastikan material terpasang saat runtime
    void Start()
    {
        ApplyTileVisuals();
    }

    /// <summary>
    /// BARU: Fungsi terpusat untuk mengganti material
    /// </summary>
    void ApplyTileVisuals()
    {
        // 1. Ambil komponen Renderer jika belum ada
        if (tileRenderer == null)
        {
            tileRenderer = GetComponent<Renderer>();
        }

        // 2. Ganti material berdasarkan 'Type'
        Material materialToApply = null;

        switch (type)
        {
            case TileType.Normal:
                materialToApply = normalMaterial;
                break;
            case TileType.SnakeStart:
                materialToApply = snakeMaterial;
                break;
            case TileType.LadderStart:
                materialToApply = ladderMaterial;
                break;
        }

        // 3. Terapkan materialnya
        // Kita gunakan .sharedMaterial agar aman di editor dan efisien
        if (materialToApply != null)
        {
            tileRenderer.sharedMaterial = materialToApply;
        }
    }


    /// <summary>
    /// Memberikan posisi di mana pemain harus berdiri di atas tile ini.
    /// </summary>
    public Vector3 GetPlayerPosition()
    {
        // Offset 0.5f agar pemain berdiri di atas kubus
        return transform.position + Vector3.up * 0.5f;
    }

    // --- Bantuan Visual di Editor ---
    // (Fungsi OnDrawGizmos dan DrawGizmoArrow tidak berubah)
    void OnDrawGizmos()
    {
        if (targetTile == null) return;

        if (type == TileType.LadderStart)
        {
            Gizmos.color = new Color(0, 1, 0, 0.7f);
            DrawGizmoArrow(transform.position, targetTile.transform.position);
        }
        else if (type == TileType.SnakeStart)
        {
            Gizmos.color = new Color(1, 0, 0, 0.7f);
            DrawGizmoArrow(transform.position, targetTile.transform.position);
        }
    }

    void DrawGizmoArrow(Vector3 start, Vector3 end)
    {
        Gizmos.DrawLine(start, end);
        Vector3 direction = (end - start).normalized;
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 1, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, -1, 0) * Vector3.forward;
        Gizmos.DrawRay(end, right * -0.5f);
        Gizmos.DrawRay(end, left * -0.5f);
    }
}