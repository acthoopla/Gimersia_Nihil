using UnityEngine;
using TMPro; // Tetap pakai TMPro sesuai script lamamu
using System.Collections.Generic;

// 1. ENUM DIPERBARUI (Gabungan visual lama + logika baru)
public enum TileType
{
    Normal,
    SnakeStart,
    LadderStart,
    SnakeEnd,       // Visual only
    LadderEnd,      // Visual only

    // Tipe Baru yang WAJIB ada untuk TileEffectSystem:
    Attack,         // Pemicu Attack Player -> Boss
    Boss,           // Pemicu Attack Boss -> Player
    Nega,           // Tile jahat (Debuff)

    BlessingCard,
    MysteryCard,

    // Visual Jalur Ular
    SnakePathStraight,
    SnakePathBend1,
    SnakePathBend2
}

[DisallowMultipleComponent]
public class Tiles : MonoBehaviour
{
    [Header("Identitas Tile (Logic)")]
    public int tileID;
    public TileType type = TileType.Normal;
    public Tiles targetTile; // Untuk Snake/Ladder destination

    [Header("Visual Anchor (Logic Baru)")]
    [Tooltip("Titik berdiri player. Jika kosong, otomatis pakai tengah tile.")]
    public Transform playerStandPoint;

    [Header("Visual Components (Script Lama)")]
    public TextMeshPro tileNumberText;
    public Transform pathContainer;

    // --- Model References (Script Lama) ---
    [Header("Visual Models")]
    public GameObject normalModel;
    public GameObject snakeStartModel;
    public GameObject ladderStartModel;
    public GameObject snakeEndModel;
    public GameObject ladderEndModel;
    public GameObject blessingCardModel;

    // Tambahan Model untuk Tipe Baru (Opsional, assign di inspector)
    [Header("New Logic Models")]
    public GameObject attackModel;
    public GameObject bossModel;
    public GameObject negaModel;

    [Header("Snake Path Models")]
    public GameObject snakePathStraightModel;
    public GameObject snakePathBendModel1;
    public GameObject snakePathBendModel2;

    // Internal state untuk editor tracking
    [SerializeField, HideInInspector] private Tiles lastKnownTarget;
    [SerializeField, HideInInspector] private TileType lastKnownType;
    [SerializeField, HideInInspector] private int lastKnownTileID = -1;
    private Vector3 originalPosition;

    void Awake()
    {
        originalPosition = transform.position;
    }

    void Start()
    {
        UpdateVisualModel();
        UpdateTileNumber();
        lastKnownTarget = targetTile;
        lastKnownType = type;
        lastKnownTileID = tileID;
    }

    // --- LOGIC BARU: GET POSITION ---
    // Digunakan oleh MovementSystem
    public Vector3 GetPlayerPosition()
    {
        // Prioritaskan playerStandPoint jika ada, jika tidak pakai logic lama (+0.5f Y)
        if (playerStandPoint != null) return playerStandPoint.position;
        return transform.position + Vector3.up * 0.5f;
    }

    // --- FITUR SCRIPT LAMA (Auto Assign & Visuals) ---
    void OnValidate()
    {
        #region Editor Logic
        if (transform.localRotation != Quaternion.identity && pathContainer == null)
            transform.localRotation = Quaternion.identity;

        if (tileID != lastKnownTileID && tileID > 0)
        {
            AutoAssignModels();
            lastKnownTileID = tileID;
        }

        // Logic otomatis update tipe target (Snake/Ladder)
        if (type != lastKnownType || targetTile != lastKnownTarget)
        {
            // Reset target lama jika berubah
            if (lastKnownTarget != null && lastKnownTarget != targetTile)
                lastKnownTarget.SetType(TileType.Normal, true);

            // Set target baru
            if (type == TileType.LadderStart && targetTile != null)
                targetTile.SetType(TileType.LadderEnd, true);
            else if (type == TileType.SnakeStart && targetTile != null)
                targetTile.SetType(TileType.SnakeEnd, true);

            // Jika kembali ke normal
            if (type == TileType.Normal && lastKnownTarget != null)
            {
                if (lastKnownType == TileType.LadderStart || lastKnownType == TileType.SnakeStart)
                    lastKnownTarget.SetType(TileType.Normal, true);
            }

            lastKnownTarget = targetTile;
            lastKnownType = type;
        }

        // Nama object rapi di hierarchy
        gameObject.name = $"Tile_{tileID}_{type}";

        UpdateVisualModel();
        UpdateTileNumber();
        #endregion
    }

    public void SetType(TileType newType, bool fromScript = false)
    {
        type = newType;
        UpdateVisualModel();
        if (fromScript) lastKnownType = type;
    }

    [ContextMenu("Auto-Assign Child Models")]
    void AutoAssignModels()
    {
        if (tileID <= 0) return;
        string theme = (tileID % 2 != 0) ? "Emas" : "Putih";

        Transform textChild = transform.Find("Text (TMP)");
        if (textChild != null) tileNumberText = textChild.GetComponent<TextMeshPro>();

        Transform containerChild = transform.Find("PathContainer");
        if (containerChild != null) pathContainer = containerChild;

        // Cari model berdasarkan nama (sesuai aset kamu)
        normalModel = FindChildModel(transform, "Tile_" + theme);
        snakeStartModel = FindChildModel(transform, "Tile_Snake");
        ladderStartModel = FindChildModel(transform, "Tile_Tangga" + theme);
        snakeEndModel = FindChildModel(transform, "Tile_Buntut" + theme);
        ladderEndModel = FindChildModel(transform, "Tile_Tangga" + theme);
        blessingCardModel = FindChildModel(transform, "Tile_" + theme + "Plate");

        // Cari model path
        if (pathContainer != null)
        {
            snakePathStraightModel = FindChildModel(pathContainer, "Tile_JalurBuntutLurus" + theme);
            snakePathBendModel1 = FindChildModel(pathContainer, "Tile_JalurBuntutBelok" + theme + "_1");
            snakePathBendModel2 = FindChildModel(pathContainer, "Tile_JalurBuntutBelok" + theme + "_2");
        }

        // Note: Model Attack/Boss/Nega mungkin belum ada di aset lamamu,
        // jadi assign manual di inspector jika sudah ada prefabnya.
    }

    GameObject FindChildModel(Transform parent, string name)
    {
        if (parent == null) return null;
        Transform child = parent.Find(name);
        return (child != null) ? child.gameObject : null;
    }

    public void UpdateTileNumber()
    {
        if (tileNumberText != null) tileNumberText.text = tileID.ToString();
    }

    public void UpdateVisualModel()
    {
        // Matikan semua dulu
        if (normalModel) normalModel.SetActive(false);
        if (snakeStartModel) snakeStartModel.SetActive(false);
        if (ladderStartModel) ladderStartModel.SetActive(false);
        if (snakeEndModel) snakeEndModel.SetActive(false);
        if (ladderEndModel) ladderEndModel.SetActive(false);
        if (blessingCardModel) blessingCardModel.SetActive(false);
        if (attackModel) attackModel.SetActive(false);
        if (bossModel) bossModel.SetActive(false);
        if (negaModel) negaModel.SetActive(false);

        // Path models
        if (snakePathStraightModel) snakePathStraightModel.SetActive(false);
        if (snakePathBendModel1) snakePathBendModel1.SetActive(false);
        if (snakePathBendModel2) snakePathBendModel2.SetActive(false);

        // Aktifkan sesuai tipe
        switch (type)
        {
            case TileType.Normal: if (normalModel) normalModel.SetActive(true); break;
            case TileType.SnakeStart: if (snakeStartModel) snakeStartModel.SetActive(true); break;
            case TileType.LadderStart: if (ladderStartModel) ladderStartModel.SetActive(true); break;
            case TileType.SnakeEnd: if (snakeEndModel) snakeEndModel.SetActive(true); break;
            case TileType.LadderEnd: if (ladderEndModel) ladderEndModel.SetActive(true); break;
            case TileType.BlessingCard: if (blessingCardModel) blessingCardModel.SetActive(true); break;

            // Tipe Baru
            case TileType.Attack: if (attackModel) attackModel.SetActive(true); else if (normalModel) normalModel.SetActive(true); break;
            case TileType.Boss: if (bossModel) bossModel.SetActive(true); else if (normalModel) normalModel.SetActive(true); break;
            case TileType.Nega: if (negaModel) negaModel.SetActive(true); else if (normalModel) normalModel.SetActive(true); break;

            // Paths
            case TileType.SnakePathStraight: if (snakePathStraightModel) snakePathStraightModel.SetActive(true); break;
            case TileType.SnakePathBend1: if (snakePathBendModel1) snakePathBendModel1.SetActive(true); break;
            case TileType.SnakePathBend2: if (snakePathBendModel2) snakePathBendModel2.SetActive(true); break;
        }
    }

    // --- GIZMOS (Script Lama) ---
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
        Vector3 dir = (end - start).normalized;
        Vector3 right = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 1, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(dir) * Quaternion.Euler(0, -1, 0) * Vector3.forward;
        Gizmos.DrawRay(end, right * -0.5f);
        Gizmos.DrawRay(end, left * -0.5f);
    }
}