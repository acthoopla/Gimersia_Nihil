using UnityEngine;
using TMPro;

// DIUBAH: Tambahkan tipe 'End'
public enum TileType
{
    Normal,      // Petak biasa
    SnakeStart,  // Petak tempat MULUT Ular (yang bikin turun)
    LadderStart, // Petak tempat BAWAH Tangga (yang bikin naik)
    SnakeEnd,    // Petak tempat EKOR Ular (tujuan)
    LadderEnd    // Petak tempat ATAS Tangga (tujuan)
}

// DIHAPUS: Parent tidak lagi perlu Renderer
// [RequireComponent(typeof(Renderer))] 
public class Tiles : MonoBehaviour
{
    [Header("Identitas Tile")]
    [Tooltip("ID unik untuk tile ini, urut dari 1 (start) sampai akhir (misal: 100)")]
    public int tileID;

    [Tooltip("Tipe dari tile ini (Normal, Ular, atau Tangga)")]
    public TileType type = TileType.Normal;

    [Header("Visuals")]
    [Tooltip("Tarik komponen TextMeshPro child ke sini untuk menampilkan nomor ID")]
    public TextMeshPro tileNumberText;

    [Header("Logika Ular/Tangga")]
    [Tooltip("Hanya diisi jika tipe adalah SnakeStart atau LadderStart. " +
             "Ini adalah tile tujuan (bisa ditarik dari Hierarchy).")]
    public Tiles targetTile;

    // --- BAGIAN INI DIUBAH ---
    [Header("Visual Models (Child Objects)")]
    [Tooltip("Tarik child object untuk visual 'Normal' ke sini")]
    public GameObject normalModel;

    [Tooltip("Tarik child object untuk visual 'Mulai Ular' ke sini")]
    public GameObject snakeStartModel; // Diganti nama dari 'snakeModel'

    [Tooltip("Tarik child object untuk visual 'Mulai Tangga' ke sini")]
    public GameObject ladderStartModel; // Diganti nama dari 'ladderModel'

    [Header("Visual Models (Tujuan)")] // <-- BARU
    [Tooltip("Tarik child object untuk visual 'Tujuan Ular' ke sini")]
    public GameObject snakeEndModel;

    [Tooltip("Tarik child object untuk visual 'Tujuan Tangga' ke sini")]
    public GameObject ladderEndModel;
    // ---------------------------------

    // Variabel ini untuk melacak perubahan di OnValidate
    [SerializeField, HideInInspector]
    private Tiles lastKnownTarget;
    [SerializeField, HideInInspector]
    private TileType lastKnownType;


    void Start()
    {
        // Panggil UpdateVisualModel saat game start
        UpdateVisualModel();
        UpdateTileNumber();

        // Inisialisasi pelacak state
        lastKnownTarget = targetTile;
        lastKnownType = type;
    }

    // Fungsi OnValidate() akan otomatis update di Editor
    void OnValidate()
    {
        // --- LOGIKA BARU UNTUK AUTO-UPDATE TARGET ---

        // 1. Cek apakah ada perubahan yang relevan
        if (type != lastKnownType || targetTile != lastKnownTarget)
        {
            // 2. BERSIHKAN TARGET LAMA
            // Jika target lama ada DAN target lama itu BUKAN target baru
            if (lastKnownTarget != null && lastKnownTarget != targetTile)
            {
                // Suruh target lama kembali jadi 'Normal'
                lastKnownTarget.SetType(TileType.Normal);
            }

            // 3. ATUR TARGET BARU
            if (type == TileType.LadderStart && targetTile != null)
            {
                targetTile.SetType(TileType.LadderEnd);
            }
            else if (type == TileType.SnakeStart && targetTile != null)
            {
                targetTile.SetType(TileType.SnakeEnd);
            }

            // 4. Jika tile ini di-set jadi Normal, bersihkan targetnya
            if (type == TileType.Normal && targetTile != null)
            {
                targetTile.SetType(TileType.Normal);
            }

            // 5. Simpan state saat ini untuk perbandingan berikutnya
            lastKnownTarget = targetTile;
            lastKnownType = type;
        }

        // --- Logika Pengaman ---
        // Jika user set 'End' secara manual, jangan biarkan
        if ((type == TileType.LadderEnd || type == TileType.SnakeEnd))
        {
            // Cek apakah ada 'Start' tile yang menunjuk ke kita
            // Ini agak rumit tanpa referensi balik, jadi kita lakukan
            // cara sederhana: jika user set manual, reset ke Normal
            // (Kita asumsikan 'targetTile' hanya di-set di 'Start' tile)
            if (targetTile != null)
            {
                Debug.LogWarning($"Tile 'End' ({name}) tidak seharusnya punya Target. Menghapus target...");
                targetTile = null; // Tile 'End' tidak bisa punya target
            }
        }

        // Selalu update visual diri sendiri
        UpdateVisualModel();
        UpdateTileNumber();
    }

    void UpdateTileNumber()
    {
        if (tileNumberText != null)
        {
            // Set teks-nya sesuai tileID
            tileNumberText.text = tileID.ToString();

            // --- TAMBAHKAN INI ---
            Debug.Log($"Berhasil set TILE {tileID} ke teks: {tileNumberText.text}", gameObject);
            // ---------------------
        }
        else
        {
            // --- TAMBAHKAN INI ---
            Debug.LogError($"GAGAL! Referensi 'tileNumberText' KOSONG di {gameObject.name}", gameObject);
            // ---------------------
        }
    }

    /// <summary>
    /// Fungsi publik untuk mengubah tipe tile ini (dipanggil oleh tile lain)
    /// </summary>
    public void SetType(TileType newType)
    {
        type = newType;
        UpdateVisualModel();
    }


    /// <summary>
    /// Mengaktifkan/Menonaktifkan child GameObject berdasarkan 'Type'.
    /// </summary>
    void UpdateVisualModel()
    {
        // 1. Matikan SEMUA model terlebih dahulu
        if (normalModel != null) normalModel.SetActive(false);
        if (snakeStartModel != null) snakeStartModel.SetActive(false);
        if (ladderStartModel != null) ladderStartModel.SetActive(false);
        if (snakeEndModel != null) snakeEndModel.SetActive(false);     // <-- BARU
        if (ladderEndModel != null) ladderEndModel.SetActive(false);   // <-- BARU

        // 2. Aktifkan HANYA model yang sesuai dengan 'Type'
        switch (type)
        {
            case TileType.Normal:
                if (normalModel != null)
                    normalModel.SetActive(true);
                break;
            case TileType.SnakeStart:
                if (snakeStartModel != null)
                    snakeStartModel.SetActive(true);
                break;
            case TileType.LadderStart:
                if (ladderStartModel != null)
                    ladderStartModel.SetActive(true);
                break;
            case TileType.SnakeEnd: // <-- BARU
                if (snakeEndModel != null)
                    snakeEndModel.SetActive(true);
                break;
            case TileType.LadderEnd: // <-- BARU
                if (ladderEndModel != null)
                    ladderEndModel.SetActive(true);
                break;
        }
    }


    /// <summary>
    /// Memberikan posisi di mana pemain harus berdiri di atas tile ini.
    /// (Tidak berubah)
    /// </summary>
    public Vector3 GetPlayerPosition()
    {
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