using UnityEngine;
using TMPro; // Diambil dari kodemu

// DIUBAH: Enum ini adalah gabungan dari kodemu dan kode temanmu
public enum TileType
{
    Normal,      
    SnakeStart,  
    LadderStart, 
    SnakeEnd,    // <-- Dari kodemu
    LadderEnd,   // <-- Dari kodemu
    BlessingCard // <-- BARU: Dari kode temanmu
}

public class Tiles : MonoBehaviour
{
    [Header("Identitas Tile")]
    public int tileID;
    public TileType type = TileType.Normal;

    [Header("Visuals")]
    public TextMeshPro tileNumberText; // <-- Dari kodemu

    [Header("Logika Ular/Tangga")]
    public Tiles targetTile;

    // DIUBAH: Ini adalah sistem visual dari KODEMU
    [Header("Visual Models (Child Objects)")]
    public GameObject normalModel;
    public GameObject snakeStartModel;
    public GameObject ladderStartModel;

    [Header("Visual Models (Tujuan)")]
    public GameObject snakeEndModel;
    public GameObject ladderEndModel;
    
    // BARU: Tambahkan slot model untuk petak kartu
    [Header("Visual Models (Spesial)")] 
    public GameObject blessingCardModel; // <-- BARU

    // Variabel OnValidate (dari kodemu)
    [SerializeField, HideInInspector]
    private Tiles lastKnownTarget;
    [SerializeField, HideInInspector]
    private TileType lastKnownType;


    void Start()
    {
        // Dari kodemu
        UpdateVisualModel();
        UpdateTileNumber();
        lastKnownTarget = targetTile;
        lastKnownType = type;
    }

    // Dari kodemu
    void OnValidate()
    {
        // Logika auto-update target (dari kodemu)
        if (type != lastKnownType || targetTile != lastKnownTarget)
        {
            if (lastKnownTarget != null && lastKnownTarget != targetTile)
            {
                lastKnownTarget.SetType(TileType.Normal);
            }
            if (type == TileType.LadderStart && targetTile != null)
            {
                targetTile.SetType(TileType.LadderEnd);
            }
            else if (type == TileType.SnakeStart && targetTile != null)
            {
                targetTile.SetType(TileType.SnakeEnd);
            }
            if (type == TileType.Normal && targetTile != null)
            {
                 targetTile.SetType(TileType.Normal);
            }
            lastKnownTarget = targetTile;
            lastKnownType = type;
        }
        if ((type == TileType.LadderEnd || type == TileType.SnakeEnd))
        {
            if (targetTile != null) 
            {
                Debug.LogWarning($"Tile 'End' ({name}) tidak seharusnya punya Target. Menghapus target...");
                targetTile = null; 
            }
        }
        
        UpdateVisualModel();
        UpdateTileNumber();
    }

    // Dari kodemu (dengan pembersihan debug)
    void UpdateTileNumber()
    {
        if (tileNumberText != null)
        {
            tileNumberText.text = tileID.ToString();
        }
        // else
        // {
        //     Debug.LogError($"GAGAL! Referensi 'tileNumberText' KOSONG di {gameObject.name}", gameObject);
        // }
    }

    // Dari kodemu
    public void SetType(TileType newType)
    {
        type = newType;
        UpdateVisualModel();
    }


    // DIUBAH: `UpdateVisualModel` dari kodemu, ditambah `BlessingCard`
    void UpdateVisualModel()
    {
        // 1. Matikan SEMUA model
        if (normalModel != null) normalModel.SetActive(false);
        if (snakeStartModel != null) snakeStartModel.SetActive(false);
        if (ladderStartModel != null) ladderStartModel.SetActive(false);
        if (snakeEndModel != null) snakeEndModel.SetActive(false);
        if (ladderEndModel != null) ladderEndModel.SetActive(false);
        if (blessingCardModel != null) blessingCardModel.SetActive(false); // <-- BARU

        // 2. Aktifkan model yang sesuai
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
            case TileType.SnakeEnd:
                if (snakeEndModel != null)
                    snakeEndModel.SetActive(true);
                break;
            case TileType.LadderEnd:
                if (ladderEndModel != null)
                    ladderEndModel.SetActive(true);
                break;
            // BARU: Tambahkan case untuk kartu
            case TileType.BlessingCard: 
                if (blessingCardModel != null)
                    blessingCardModel.SetActive(true);
                break;
        }
    }

    // Dari kodemu
    public Vector3 GetPlayerPosition()
    {
        return transform.position + Vector3.up * 0.5f;
    }

    // Dari kodemu
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