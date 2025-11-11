using UnityEngine;
using TMPro;
using System.Linq;
using System.Collections.Generic;

// Enum (tidak berubah)
public enum TileType
{
    Normal,
    SnakeStart,
    LadderStart,
    SnakeEnd,
    LadderEnd,
    BlessingCard,
    SnakePathStraight,
    SnakePathBend1,
    SnakePathBend2
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

    // DIHAPUS: List Waypoint sudah tidak diperlukan lagi
    // public List<Tiles> snakeWaypoints;

    [Header("Visual Models (Wadah)")]
    [Tooltip("Tarik Child 'PathContainer' ke sini. Ini akan dirotasi.")]
    public Transform pathContainer;

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

    // OnValidate akan menangani SEMUA logika
    void OnValidate()
    {
        if (tileID != lastKnownTileID && tileID > 0)
        {
            AutoAssignModels();
            lastKnownTileID = tileID;
        }

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

        // Selalu panggil fungsi ini di akhir
        UpdateVisualModel();
        UpdateTileNumber();
    }

    public void SetType(TileType newType, bool fromScript = false)
    {
        type = newType;
        UpdateVisualModel();
        if (fromScript)
        {
            lastKnownType = type;
        }
    }

    // DIHAPUS: Fungsi GenerateSnakePath(), ClearSnakePath(), RoundVectorToGrid()

    [ContextMenu("Auto-Assign Child Models")]
    void AutoAssignModels()
    {
        if (tileID <= 0) return;
        string theme = (tileID % 2 != 0) ? "Emas" : "Putih";

        // 1. Temukan Wadah Utama (Container)
        Transform textChild = transform.Find("Text (TMP)");
        if (textChild != null)
        {
            tileNumberText = textChild.GetComponent<TextMeshPro>();
        }

        Transform containerChild = transform.Find("PathContainer");
        if (containerChild != null)
        {
            pathContainer = containerChild;
        }
        else
        {
            Debug.LogError($"AutoAssign GAGAL: Tidak menemukan 'PathContainer' di {gameObject.name}. ", gameObject);
            return;
        }

        // 2. Temukan Model Non-Jalur (Child langsung dari 'transform')
        normalModel = FindChildModel(transform, "Tile_" + theme);
        snakeStartModel = FindChildModel(transform, "Tile_Snake");
        ladderStartModel = FindChildModel(transform, "Tile_Tangga" + theme);
        snakeEndModel = FindChildModel(transform, "Tile_Buntut" + theme);
        ladderEndModel = FindChildModel(transform, "Tile_Tangga" + theme);
        blessingCardModel = FindChildModel(transform, "Tile_" + theme + "Coak");

        // 3. Temukan Model Jalur (Child dari 'pathContainer')
        snakePathStraightModel = FindChildModel(pathContainer, "Tile_JalurBuntutLurus" + theme);
        snakePathBendModel1 = FindChildModel(pathContainer, "Tile_JalurBuntutBelok" + theme + "_1");
        snakePathBendModel2 = FindChildModel(pathContainer, "Tile_JalurBuntutBelok" + theme + "_2");
    }

    GameObject FindChildModel(Transform parentToSearch, string childName)
    {
        if (parentToSearch == null) return null;
        Transform child = parentToSearch.Find(childName);
        return (child != null) ? child.gameObject : null;
    }

    void UpdateTileNumber()
    {
        if (tileNumberText != null)
        {
            tileNumberText.text = tileID.ToString();
        }
    }

    // DIUBAH: Fungsi ini sekarang juga meng-handle rotasi
    void UpdateVisualModel()
    {
        // 1. Matikan SEMUA model
        if (normalModel != null) normalModel.SetActive(false);
        if (snakeStartModel != null) snakeStartModel.SetActive(false);
        if (ladderStartModel != null) ladderStartModel.SetActive(false);
        if (snakeEndModel != null) snakeEndModel.SetActive(false);
        if (ladderEndModel != null) ladderEndModel.SetActive(false);
        if (blessingCardModel != null) blessingCardModel.SetActive(false);
        if (snakePathStraightModel != null) snakePathStraightModel.SetActive(false);
        if (snakePathBendModel1 != null) snakePathBendModel1.SetActive(false);
        if (snakePathBendModel2 != null) snakePathBendModel2.SetActive(false);

        // --- INI LOGIKA ROTASI YANG DIPERBAIKI ---
        if (pathContainer != null)
        {
            // Cek apakah Tipe-nya adalah salah satu dari tipe jalur
            bool isPath = (type == TileType.SnakePathStraight ||
                           type == TileType.SnakePathBend1 ||
                           type == TileType.SnakePathBend2);

            if (isPath)
            {
                // JIKA JALUR:
                // Cek apakah parent-nya punya rotasi (artinya user baru saja memutarnya)
                if (transform.localRotation != Quaternion.identity)
                {
                    Quaternion newRot = transform.localRotation; // 1. Simpan rotasi parent
                    transform.localRotation = Quaternion.identity; // 2. Reset rotasi parent (agar nomor lurus)
                    pathContainer.localRotation = newRot; // 3. Terapkan rotasi ke container
                }
            }
            else
            {
                // JIKA BUKAN JALUR: Reset rotasi container
                pathContainer.localRotation = Quaternion.identity;
            }
        }
        // ----------------------------------------

        // 2. Aktifkan yang benar (logika ini tidak berubah)
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