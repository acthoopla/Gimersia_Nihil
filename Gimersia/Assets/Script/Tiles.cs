using UnityEngine;
using TMPro;
using System.Collections.Generic;

public enum TileType
{
    // Basic
    Normal,
    NormalCracked,

    // Movement
    SnakeStart,
    LadderStart,
    SnakeEnd,
    LadderEnd,

    // Legacy Movement (Opsional, biarkan jika dipakai visual lama)
    SnakePathStraight, SnakePathBend1, SnakePathBend2,

    // Combat
    Attack,
    AttackCracked, // <-- INI YANG HILANG TADI

    Death,

    // Danger 02 (Disarm/Provocation/Despair)
    Disarm,
    DisarmCracked, // <-- INI YANG HILANG
    Provocation,
    ProvocationCracked, // <-- INI YANG HILANG
    Despair,
    DespairCracked, // <-- INI YANG HILANG

    // Danger 01 (Damage)
    Damage,
    DamageCracked, // <-- INI YANG HILANG

    // Cards
    CardRandom,
    CardMovement,
    CardBuff
}

[DisallowMultipleComponent]
public class Tiles : MonoBehaviour
{
    [Header("Identitas Tile")]
    public int tileID;
    public TileType type = TileType.Normal;
    public Tiles targetTile;

    [Header("Visual Anchor")]
    public Transform playerStandPoint;

    [Header("Visual Components")]
    public TextMeshPro tileNumberText;

    // --- Model References (Sesuai Gambar & Tema) ---
    [Header("Basic Models")]
    public GameObject normalModel;          // Tile_Emas / Tile_Putih
    public GameObject normalCrackedModel;   // Tile_EmasCoak / Tile_PutihCoak

    [Header("Movement Models")]
    public GameObject snakeStartModel;      // Tile_Snake
    public GameObject snakeEndModel;        // Tile_BuntutEmas / Tile_BuntutPutih
    public GameObject ladderStartModel;     // Tile_TanggaEmas / Tile_TanggaPutih
    public GameObject ladderEndModel;       // (Visual tangga atas)

    [Header("Combat Models")]
    public GameObject attackModel;          // Tile_Attack
    public GameObject deathModel;           // Tile_Death

    [Header("Danger Models (01 = Damage)")]
    public GameObject danger01Model;        // Tile_Danger_01

    [Header("Danger Models (02 = Disarm/etc)")]
    public GameObject danger02Model;        // Tile_Danger_02

    [Header("Card Models")]
    public GameObject cardBuffModel;        // Tile_Card_Buff_Gold / White
    public GameObject cardMoveModel;        // Tile_Card_Movement_Gold / White
    public GameObject cardRandomModel;      // Tile_Card_Random_Gold / White

    // Internal state
    [SerializeField, HideInInspector] private Tiles lastKnownTarget;
    [SerializeField, HideInInspector] private TileType lastKnownType;
    [SerializeField, HideInInspector] private int lastKnownTileID = -1;
    private Vector3 originalPosition;

    void Awake() { originalPosition = transform.position; }

    void Start()
    {
        UpdateVisualModel();
        UpdateTileNumber();
        lastKnownTarget = targetTile;
        lastKnownType = type;
        lastKnownTileID = tileID;
    }

    public Vector3 GetPlayerPosition()
    {
        if (playerStandPoint != null) return playerStandPoint.position;
        return transform.position + Vector3.up * 0.5f;
    }

    void OnValidate()
    {
        #region Editor Logic
        if (tileID != lastKnownTileID && tileID > 0)
        {
            AutoAssignModels();
            lastKnownTileID = tileID;
        }

        if (type != lastKnownType || targetTile != lastKnownTarget)
        {
            if (lastKnownTarget != null && lastKnownTarget != targetTile)
                lastKnownTarget.SetType(TileType.Normal, true);

            if (type == TileType.LadderStart && targetTile != null)
                targetTile.SetType(TileType.LadderEnd, true);
            else if (type == TileType.SnakeStart && targetTile != null)
                targetTile.SetType(TileType.SnakeEnd, true);

            lastKnownTarget = targetTile;
            lastKnownType = type;
        }

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

    // --- LOGIC AUTO ASSIGN (Sesuai Request Ganjil/Genap) ---
    [ContextMenu("Auto-Assign Child Models")]
    void AutoAssignModels()
    {
        if (tileID <= 0) return;

        // ATURAN: Ganjil = Emas, Genap = Putih
        string theme = (tileID % 2 != 0) ? "Emas" : "Putih";

        // Untuk Kartu, nama filenya pakai bahasa Inggris (Gold/White)
        string cardTheme = (tileID % 2 != 0) ? "Gold" : "White";

        Transform textChild = transform.Find("Text (TMP)");
        if (textChild != null) tileNumberText = textChild.GetComponent<TextMeshPro>();

        // --- Cari Model Berdasarkan Tema ---

        // Basic
        normalModel = FindChildModel(transform, "Tile_" + theme);
        normalCrackedModel = FindChildModel(transform, "Tile_" + theme + "Coak");

        // Movement
        snakeStartModel = FindChildModel(transform, "Tile_Snake"); // Biasanya Snake cuma 1 warna/umum
        snakeEndModel = FindChildModel(transform, "Tile_Buntut" + theme);
        ladderStartModel = FindChildModel(transform, "Tile_Tangga" + theme);
        ladderEndModel = FindChildModel(transform, "Tile_Tangga" + theme);

        // Combat
        attackModel = FindChildModel(transform, "Tile_Attack_" + cardTheme);

        deathModel = FindChildModel(transform, "Tile_Death");

        // Dangers
        danger01Model = FindChildModel(transform, "Tile_Danger_" + cardTheme + "_01");
        danger02Model = FindChildModel(transform, "Tile_Danger_" + cardTheme + "_02");

        // Cards
        cardRandomModel = FindChildModel(transform, "Tile_Card_Random_" + cardTheme);
        cardBuffModel = FindChildModel(transform, "Tile_Card_Buff_" + cardTheme);
        cardMoveModel = FindChildModel(transform, "Tile_Card_Movement_" + cardTheme);
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
        // 1. Matikan SEMUA
        if (normalModel) normalModel.SetActive(false);
        if (normalCrackedModel) normalCrackedModel.SetActive(false);

        if (snakeStartModel) snakeStartModel.SetActive(false);
        if (snakeEndModel) snakeEndModel.SetActive(false);
        if (ladderStartModel) ladderStartModel.SetActive(false);
        if (ladderEndModel) ladderEndModel.SetActive(false);

        if (attackModel) attackModel.SetActive(false);
        if (deathModel) deathModel.SetActive(false);

        if (danger01Model) danger01Model.SetActive(false);
        if (danger02Model) danger02Model.SetActive(false);

        if (cardRandomModel) cardRandomModel.SetActive(false);
        if (cardBuffModel) cardBuffModel.SetActive(false);
        if (cardMoveModel) cardMoveModel.SetActive(false);

        // 2. Nyalakan Sesuai Tipe
        switch (type)
        {
            // Basic
            case TileType.Normal: if (normalModel) normalModel.SetActive(true); break;
            case TileType.NormalCracked: if (normalCrackedModel) normalCrackedModel.SetActive(true); else if (normalModel) normalModel.SetActive(true); break;

            // Movement
            case TileType.SnakeStart: if (snakeStartModel) snakeStartModel.SetActive(true); break;
            case TileType.SnakeEnd: if (snakeEndModel) snakeEndModel.SetActive(true); break;
            case TileType.LadderStart: if (ladderStartModel) ladderStartModel.SetActive(true); break;
            case TileType.LadderEnd: if (ladderEndModel) ladderEndModel.SetActive(true); break;

            // Attack
            case TileType.Attack: if (attackModel) attackModel.SetActive(true); break;

            // Death
            case TileType.Death: if (deathModel) deathModel.SetActive(true); break;

            // Danger 01 (Damage) -> Map ke Model Danger 01
            case TileType.Damage:
                if (danger01Model) danger01Model.SetActive(true); break;

            // Danger 02 (Disarm / Provoke / Despair) -> Map ke Model Danger 02
            case TileType.Disarm:
            case TileType.Provocation:
            case TileType.Despair:
                if (danger02Model) danger02Model.SetActive(true); break;
            
            // Cards
            case TileType.CardRandom:
                if (cardRandomModel) cardRandomModel.SetActive(true); break;

            case TileType.CardMovement: if (cardMoveModel) cardMoveModel.SetActive(true); break;
            case TileType.CardBuff: if (cardBuffModel) cardBuffModel.SetActive(true); break;

            default: if (normalModel) normalModel.SetActive(true); break;
        }
    }

    public void SetCracked()
    {
        switch (type)
        {
            case TileType.Normal:
                SetType(TileType.NormalCracked, true);
                break;
        }
    }

    // GIZMOS (Tetap ada untuk debugging)
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