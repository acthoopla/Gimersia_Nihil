using UnityEngine;
using TMPro;
using System.Linq; // <-- PENTING
using System.Collections.Generic; // <-- PENTING

// DIUBAH: Menambahkan tipe PATH secara spesifik
public enum TileType
{
    Normal,
    SnakeStart,
    LadderStart,
    SnakeEnd,
    LadderEnd,
    BlessingCard,
    SnakePathStraight, // <-- BARU
    SnakePathBend1,    // <-- BARU
    SnakePathBend2     // <-- BARU
}

public class Tiles : MonoBehaviour
{
    [Header("Identitas Tile")]
    public int tileID;
    public TileType type = TileType.Normal;

    [Header("Visuals")]
    public TextMeshPro tileNumberText;

    [Header("Logika Ular/Tangga")]
    public Tiles targetTile; // Hanya diisi di 'SnakeStart' atau 'LadderStart'

    // Slot-slot ini akan diisi oleh 'AutoAssignModels'
    [Header("Visual Models (Child Objects)")]
    public GameObject normalModel;
    public GameObject snakeStartModel;
    public GameObject ladderStartModel;

    [Header("Visual Models (Tujuan)")]
    public GameObject snakeEndModel;
    public GameObject ladderEndModel;

    [Header("Visual Models (Jalur Ular)")]
    public GameObject snakePathStraightModel;
    public GameObject snakePathBendModel1;
    public GameObject snakePathBendModel2;

    [Header("Visual Models (Spesial)")]
    public GameObject blessingCardModel;

    // Variabel pelacak untuk OnValidate
    [SerializeField, HideInInspector]
    private Tiles lastKnownTarget;
    [SerializeField, HideInInspector]
    private TileType lastKnownType;
    [SerializeField, HideInInspector]
    private int lastKnownTileID = -1;

    void Start()
    {
        UpdateVisualModel();
        UpdateTileNumber();
        lastKnownTarget = targetTile;
        lastKnownType = type;
        lastKnownTileID = tileID;
    }

    // OnValidate sekarang HANYA menangani update visual & target
    void OnValidate()
    {
        // Auto-assign model JIKA ID BERUBAH
        if (tileID != lastKnownTileID && tileID > 0)
        {
            AutoAssignModels();
            lastKnownTileID = tileID;
        }

        // Auto-set Tipe End-tile
        if (type != lastKnownType || targetTile != lastKnownTarget)
        {
            if (lastKnownTarget != null && lastKnownTarget != targetTile)
            {
                lastKnownTarget.SetType(TileType.Normal, true);
            }
            if (type == TileType.LadderStart && targetTile != null)
            {
                targetTile.SetType(TileType.LadderEnd, true);
            }
            else if (type == TileType.SnakeStart && targetTile != null)
            {
                targetTile.SetType(TileType.SnakeEnd, true);
            }
            if (type == TileType.Normal && lastKnownTarget != null)
            {
                if (lastKnownType == TileType.LadderStart || lastKnownType == TileType.SnakeStart)
                    lastKnownTarget.SetType(TileType.Normal, true);
            }
            lastKnownTarget = targetTile;
            lastKnownType = type;
        }
        if ((type == TileType.LadderEnd || type == TileType.SnakeEnd))
        {
            if (targetTile != null) targetTile = null;
        }

        UpdateVisualModel();
        UpdateTileNumber();
    }

    /// <summary>
    /// Fungsi publik untuk mengubah tipe tile ini (dipanggil oleh tile lain)
    /// </summary>
    public void SetType(TileType newType, bool fromScript = false)
    {
        type = newType;
        UpdateVisualModel();

        // Mencegah 'OnValidate' berjalan berulang-ulang
        if (fromScript)
        {
            lastKnownType = type;
        }
    }

    // --- LOGIKA "AJAIB" ADA DI BAWAH INI ---

    /// <summary>
    /// (Dipanggil oleh Tombol di Inspector) Membuat jalur ular otomatis
    /// </summary>
    public void GenerateSnakePath()
    {
        if (type != TileType.SnakeStart || targetTile == null)
        {
            Debug.LogError("Hanya bisa generate dari tile 'SnakeStart' yang punya 'Target Tile'!");
            return;
        }

        Debug.Log($"--- Mulai Generate Jalur Ular dari {tileID} ke {targetTile.tileID} ---");

        // 1. Buat Peta semua tile
        Dictionary<int, Tiles> map = FindObjectsOfType<Tiles>().ToDictionary(t => t.tileID, t => t);

        // 2. Loop dari tile SEBELUM kepala, sampai tile SETELAH ekor
        for (int i = this.tileID - 1; i > targetTile.tileID; i--)
        {
            if (!map.ContainsKey(i) || !map.ContainsKey(i + 1) || !map.ContainsKey(i - 1))
            {
                Debug.LogWarning($"Melewatkan tile {i}, ID tidak ditemukan di map.");
                continue;
            }

            Tiles tile_curr = map[i];
            Tiles tile_prev = map[i + 1]; // Tile sebelumnya (ID lebih besar)
            Tiles tile_next = map[i - 1]; // Tile berikutnya (ID lebih kecil)

            // 3. Ambil posisi X,Z (mengabaikan Y)
            Vector2 pos_curr = new Vector2(tile_curr.transform.position.x, tile_curr.transform.position.z);
            Vector2 pos_prev = new Vector2(tile_prev.transform.position.x, tile_prev.transform.position.z);
            Vector2 pos_next = new Vector2(tile_next.transform.position.x, tile_next.transform.position.z);

            // 4. Hitung vektor arah
            Vector2 dir_in = (pos_curr - pos_prev).normalized; // Arah dari tile sebelumnya
            Vector2 dir_out = (pos_next - pos_curr).normalized; // Arah ke tile berikutnya

            // 5. Tentukan Lurus atau Belok
            float dot = Vector2.Dot(dir_in, dir_out);
            float angleY = 0;

            if (dot > 0.9f) // Lurus
            {
                tile_curr.SetType(TileType.SnakePathStraight, true);
                // Rotasi = menghadap ke arah keluar (dir_out)
                angleY = Mathf.Atan2(dir_out.x, dir_out.y) * Mathf.Rad2Deg;
                tile_curr.transform.rotation = Quaternion.Euler(0, angleY, 0);
            }
            else // Belok
            {
                // Rotasi = menghadap ke arah masuk (dir_in)
                angleY = Mathf.Atan2(dir_in.x, dir_in.y) * Mathf.Rad2Deg;
                tile_curr.transform.rotation = Quaternion.Euler(0, angleY, 0);

                // Tentukan Belok Kiri atau Kanan
                // Kita gunakan SignedAngle
                float turnAngle = Vector2.SignedAngle(dir_in, dir_out);

                if (turnAngle > 0) // Belok Kanan (Clockwise)
                {
                    tile_curr.SetType(TileType.SnakePathBend2, true); // Asumsi _2 adalah belok kanan
                }
                else // Belok Kiri (Counter-Clockwise)
                {
                    tile_curr.SetType(TileType.SnakePathBend1, true); // Asumsi _1 adalah belok kiri
                }
            }

            Debug.Log($"Tile {i}: Lurus? {dot > 0.9f}. Rotasi: {angleY}. Tipe: {tile_curr.type}");
        }

        Debug.Log("--- Generate Jalur Ular Selesai ---");
    }

    /// <summary>
    /// (Dipanggil oleh Tombol di Inspector) Menghapus jalur ular
    /// </summary>
    public void ClearSnakePath()
    {
        if (type != TileType.SnakeStart || targetTile == null)
        {
            Debug.LogError("Hanya bisa clear dari tile 'SnakeStart' yang punya 'Target Tile'!");
            return;
        }

        Dictionary<int, Tiles> map = FindObjectsOfType<Tiles>().ToDictionary(t => t.tileID, t => t);

        for (int i = this.tileID - 1; i > targetTile.tileID; i--)
        {
            if (map.ContainsKey(i))
            {
                Tiles tile_curr = map[i];
                // Hanya reset jika itu adalah bagian dari jalur
                if (tile_curr.type == TileType.SnakePathStraight ||
                   tile_curr.type == TileType.SnakePathBend1 ||
                   tile_curr.type == TileType.SnakePathBend2)
                {
                    tile_curr.SetType(TileType.Normal, true);
                    tile_curr.transform.rotation = Quaternion.identity;
                }
            }
        }
        Debug.Log($"Jalur ular dari {tileID} ke {targetTile.tileID} dibersihkan.");
    }


    // --- Sisa Script (Tidak berubah dari sebelumnya) ---

    [ContextMenu("Auto-Assign Child Models")]
    void AutoAssignModels()
    {
        if (tileID <= 0) return;
        string theme = (tileID % 2 != 0) ? "Emas" : "Putih";

        normalModel = FindChildModel("Tile_" + theme);
        snakeStartModel = FindChildModel("Tile_Snake");
        ladderStartModel = FindChildModel("Tile_Tangga" + theme);
        snakeEndModel = FindChildModel("Tile_Buntut" + theme);
        ladderEndModel = FindChildModel("Tile_Tangga" + theme);
        snakePathStraightModel = FindChildModel("Tile_JalurBuntutLurus" + theme);
        snakePathBendModel1 = FindChildModel("Tile_JalurBuntutBelok" + theme + "_1");
        snakePathBendModel2 = FindChildModel("Tile_JalurBuntutBelok" + theme + "_2");
        blessingCardModel = FindChildModel("Tile_" + theme + "Coak");

        Transform textChild = transform.Find("Text (TMP)");
        if (textChild != null)
        {
            tileNumberText = textChild.GetComponent<TextMeshPro>();
        }
    }

    GameObject FindChildModel(string childName)
    {
        Transform child = transform.Find(childName);
        return (child != null) ? child.gameObject : null;
    }

    void UpdateTileNumber()
    {
        if (tileNumberText != null)
        {
            tileNumberText.text = tileID.ToString();
        }
    }

    void UpdateVisualModel()
    {
        if (normalModel != null) normalModel.SetActive(false);
        if (snakeStartModel != null) snakeStartModel.SetActive(false);
        if (ladderStartModel != null) ladderStartModel.SetActive(false);
        if (snakeEndModel != null) snakeEndModel.SetActive(false);
        if (ladderEndModel != null) ladderEndModel.SetActive(false);
        if (blessingCardModel != null) blessingCardModel.SetActive(false);
        if (snakePathStraightModel != null) snakePathStraightModel.SetActive(false);
        if (snakePathBendModel1 != null) snakePathBendModel1.SetActive(false);
        if (snakePathBendModel2 != null) snakePathBendModel2.SetActive(false);

        switch (type)
        {
            case TileType.Normal:
                if (normalModel != null) normalModel.SetActive(true);
                break;
            case TileType.SnakeStart:
                if (snakeStartModel != null) snakeStartModel.SetActive(true);
                break;
            case TileType.LadderStart:
                if (ladderStartModel != null) ladderStartModel.SetActive(true);
                break;
            case TileType.SnakeEnd:
                if (snakeEndModel != null) snakeEndModel.SetActive(true);
                break;
            case TileType.LadderEnd:
                if (ladderEndModel != null) ladderEndModel.SetActive(true);
                break;
            case TileType.BlessingCard:
                if (blessingCardModel != null) blessingCardModel.SetActive(true);
                break;
            case TileType.SnakePathStraight:
                if (snakePathStraightModel != null) snakePathStraightModel.SetActive(true);
                break;
            case TileType.SnakePathBend1:
                if (snakePathBendModel1 != null) snakePathBendModel1.SetActive(true);
                break;
            case TileType.SnakePathBend2:
                if (snakePathBendModel2 != null) snakePathBendModel2.SetActive(true);
                break;
        }
    }

    public Vector3 GetPlayerPosition()
    {
        return transform.position + Vector3.up * 0.5f;
    }

    #region Gizmos
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
    #endregion
}