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

    // Combat
    Attack,
    AttackCracked,

    Death,

    // Danger 02
    Disarm,
    DisarmCracked,
    Provocation,
    ProvocationCracked,
    Despair,
    DespairCracked,

    // Danger 01
    Damage,
    DamageCracked,

    // Cards
    CardRandom,
    CardMovement,
    CardBuff,

    // Legacy/Fallback 
    BlessingCard,
    MysteryCard,
    Boss,
    Nega,
    SnakePathStraight,
    SnakePathBend1,
    SnakePathBend2
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
    public Transform pathContainer;

    // --- Model References ---
    [Header("Basic Models")]
    public GameObject normalModel;
    public GameObject normalCrackedModel;

    [Header("Movement Models")]
    public GameObject snakeStartModel;
    public GameObject snakeEndModel;
    public GameObject ladderStartModel;
    public GameObject ladderEndModel;

    [Header("Combat Models")]
    public GameObject attackModel;
    public GameObject attackCrackedModel;
    public GameObject deathModel;

    [Header("Danger Models (01 = Damage)")]
    public GameObject danger01Model;
    public GameObject danger01CrackedModel;

    [Header("Danger Models (02 = Disarm/etc)")]
    public GameObject danger02Model;
    public GameObject danger02CrackedModel;

    [Header("Card Models")]
    public GameObject cardBuffModel;
    public GameObject cardMoveModel;
    public GameObject cardRandomModel;

    [Header("Snake Path Models")]
    public GameObject snakePathStraightModel;
    public GameObject snakePathBendModel1;
    public GameObject snakePathBendModel2;

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

    // --- PERBAIKAN LOGIC ONVALIDATE ---
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

        // Cek apakah ada perubahan Tipe atau Target
        if (type != lastKnownType || targetTile != lastKnownTarget)
        {
            // 1. Reset Target Lama (Jika dulu ada target, balikin jadi Normal)
            if (lastKnownTarget != null && lastKnownTarget != targetTile)
            {
                // Cek agar tidak mereset tile yang sebenarnya masih jadi target tile lain (opsional, tapi aman)
                lastKnownTarget.SetType(TileType.Normal, true);
            }

            // 2. Update Target Baru
            if (targetTile != null)
            {
                if (type == TileType.LadderStart)
                {
                    targetTile.SetType(TileType.LadderEnd, true);
                }
                else if (type == TileType.SnakeStart)
                {
                    targetTile.SetType(TileType.SnakeEnd, true);
                }
            }

            // 3. Reset Target Tile sendiri jika kita berubah jadi Normal
            if (type == TileType.Normal && lastKnownTarget != null)
            {
                // Jika tipe kita Normal, kita tidak butuh target.
                // (Opsional: mau kosongkan targetTile atau biarkan reference?)
                // targetTile = null; // Uncomment jika ingin auto-clear
            }

            lastKnownTarget = targetTile;
            lastKnownType = type;
        }

        // Update Visual Diri Sendiri
        gameObject.name = $"Tile_{tileID}_{type}";
        UpdateVisualModel();
        UpdateTileNumber();
        #endregion
    }

    // --- PERBAIKAN SET TYPE AGAR TERSIMPAN DI EDITOR ---
    public void SetType(TileType newType, bool fromScript = false)
    {
        type = newType;
        UpdateVisualModel();

        if (fromScript)
        {
            lastKnownType = type;
        }

        // MAGIC FIX: Beritahu Unity Editor bahwa object ini berubah!
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            // Jika object targetnya adalah prefab instance, ini membantu record perubahannya
            // UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this); 
        }
#endif
    }

    [ContextMenu("Auto-Assign Child Models")]
    void AutoAssignModels()
    {
        if (tileID <= 0) return;
        string theme = (tileID % 2 != 0) ? "Emas" : "Putih";
        string cardTheme = (tileID % 2 != 0) ? "Gold" : "White";

        Transform textChild = transform.Find("Text (TMP)");
        if (textChild != null) tileNumberText = textChild.GetComponent<TextMeshPro>();

        Transform containerChild = transform.Find("PathContainer");
        if (containerChild != null) pathContainer = containerChild;

        normalModel = FindChildModel(transform, "Tile_" + theme);
        normalCrackedModel = FindChildModel(transform, "Tile_" + theme + "Coak");

        snakeStartModel = FindChildModel(transform, "Tile_Snake");
        snakeEndModel = FindChildModel(transform, "Tile_Buntut" + theme);
        ladderStartModel = FindChildModel(transform, "Tile_Tangga" + theme);
        ladderEndModel = FindChildModel(transform, "Tile_Tangga" + theme);

        attackModel = FindChildModel(transform, "Tile_Attack");
        attackCrackedModel = FindChildModel(transform, "Tile_Attack_Cracked");
        deathModel = FindChildModel(transform, "Tile_Death");

        danger01Model = FindChildModel(transform, "Tile_Danger_01");
        danger01CrackedModel = FindChildModel(transform, "Tile_Danger_Cracked_01");
        danger02Model = FindChildModel(transform, "Tile_Danger_02");
        danger02CrackedModel = FindChildModel(transform, "Tile_Danger_Cracked_02");

        cardRandomModel = FindChildModel(transform, "Tile_Card_Random_" + cardTheme);
        cardBuffModel = FindChildModel(transform, "Tile_Card_Buff_" + cardTheme);
        cardMoveModel = FindChildModel(transform, "Tile_Card_Movement_" + cardTheme);

        if (pathContainer != null)
        {
            snakePathStraightModel = FindChildModel(pathContainer, "Tile_JalurBuntutLurus" + theme);
            snakePathBendModel1 = FindChildModel(pathContainer, "Tile_JalurBuntutBelok" + theme + "_1");
            snakePathBendModel2 = FindChildModel(pathContainer, "Tile_JalurBuntutBelok" + theme + "_2");
        }
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
        if (attackCrackedModel) attackCrackedModel.SetActive(false);
        if (deathModel) deathModel.SetActive(false);

        if (danger01Model) danger01Model.SetActive(false);
        if (danger01CrackedModel) danger01CrackedModel.SetActive(false);
        if (danger02Model) danger02Model.SetActive(false);
        if (danger02CrackedModel) danger02CrackedModel.SetActive(false);

        if (cardRandomModel) cardRandomModel.SetActive(false);
        if (cardBuffModel) cardBuffModel.SetActive(false);
        if (cardMoveModel) cardMoveModel.SetActive(false);

        if (snakePathStraightModel) snakePathStraightModel.SetActive(false);
        if (snakePathBendModel1) snakePathBendModel1.SetActive(false);
        if (snakePathBendModel2) snakePathBendModel2.SetActive(false);

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
            case TileType.AttackCracked: if (attackCrackedModel) attackCrackedModel.SetActive(true); else if (attackModel) attackModel.SetActive(true); break;

            // Death
            case TileType.Death: if (deathModel) deathModel.SetActive(true); break;

            // Danger 01
            case TileType.Damage:
            case TileType.Nega:
                if (danger01Model) danger01Model.SetActive(true); break;
            case TileType.DamageCracked:
                if (danger01CrackedModel) danger01CrackedModel.SetActive(true); else if (danger01Model) danger01Model.SetActive(true); break;

            // Danger 02
            case TileType.Disarm:
            case TileType.Provocation:
            case TileType.Despair:
                if (danger02Model) danger02Model.SetActive(true); break;

            case TileType.DisarmCracked:
            case TileType.ProvocationCracked:
            case TileType.DespairCracked:
                if (danger02CrackedModel) danger02CrackedModel.SetActive(true); else if (danger02Model) danger02Model.SetActive(true); break;

            // Cards
            case TileType.CardRandom:
            case TileType.BlessingCard:
            case TileType.MysteryCard:
                if (cardRandomModel) cardRandomModel.SetActive(true); break;

            case TileType.CardMovement: if (cardMoveModel) cardMoveModel.SetActive(true); break;
            case TileType.CardBuff: if (cardBuffModel) cardBuffModel.SetActive(true); break;

            // Paths
            case TileType.SnakePathStraight: if (snakePathStraightModel) snakePathStraightModel.SetActive(true); break;
            case TileType.SnakePathBend1: if (snakePathBendModel1) snakePathBendModel1.SetActive(true); break;
            case TileType.SnakePathBend2: if (snakePathBendModel2) snakePathBendModel2.SetActive(true); break;

            default: if (normalModel) normalModel.SetActive(true); break;
        }
    }

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