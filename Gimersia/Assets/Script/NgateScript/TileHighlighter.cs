using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TileHighlighter v2.0 - dengan Akurasi dan Object Pooling
/// - Preview akurat sesuai logika game (snake/ladder instant teleport)
/// - Object pooling untuk performance
/// - Visual berbeda berdasarkan tipe tile
/// </summary>
[DisallowMultipleComponent]
public class TileHighlighter : MonoBehaviour
{
    public static TileHighlighter Instance { get; private set; }

    [Header("Marker Prefab")]
    [Tooltip("Prefab untuk marker highlight. Harus memiliki Renderer component.")]
    public GameObject highlightPrefab;

    [Header("Position Settings")]
    public float yOffset = 1.0f;

    [Header("Visual Settings")]
    [Tooltip("Material untuk tile normal (default: semi-transparent green)")]
    public Material normalMaterial;

    [Tooltip("Material untuk ladder tile (default: semi-transparent blue)")]
    public Material ladderMaterial;

    [Tooltip("Material untuk snake tile (default: semi-transparent red)")]
    public Material snakeMaterial;

    [Tooltip("Material untuk danger tile (damage, attack, etc) (default: semi-transparent orange)")]
    public Material dangerMaterial;

    [Tooltip("Material untuk card tile (default: semi-transparent purple)")]
    public Material cardMaterial;

    [Header("Object Pooling")]
    [Tooltip("Aktifkan object pooling untuk performance")]
    public bool usePooling = true;

    [Tooltip("Jumlah awal marker di pool")]
    [Range(1, 20)]
    public int initialPoolSize = 5;

    [Tooltip("Maksimal marker yang bisa dipool (untuk mencegah memory leak)")]
    [Range(5, 50)]
    public int maxPoolSize = 15;

    // Object pooling system
    private Queue<GameObject> markerPool = new Queue<GameObject>();
    private List<GameObject> activeMarkers = new List<GameObject>();

    // Cache
    private PlayerState currentPlayer;
    private int currentPreviewTile = -1;
    private bool isInitialized = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Optional: jika ingin persist antar scene
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializePool();
    }

    void Start()
    {
        // Auto-setup materials jika belum di-set
        SetupDefaultMaterials();
    }

    /// <summary>
    /// Inisialisasi object pool
    /// </summary>
    private void InitializePool()
    {
        if (!usePooling || highlightPrefab == null)
        {
            Debug.LogWarning("[TileHighlighter] Pooling disabled or prefab missing");
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewPooledMarker();
        }

        isInitialized = true;
        Debug.Log($"[TileHighlighter] Pool initialized with {initialPoolSize} markers");
    }

    /// <summary>
    /// Buat marker baru untuk pool
    /// </summary>
    private void CreateNewPooledMarker()
    {
        if (markerPool.Count >= maxPoolSize)
        {
            Debug.LogWarning("[TileHighlighter] Pool size limit reached");
            return;
        }

        GameObject marker = Instantiate(highlightPrefab);
        marker.SetActive(false);
        marker.transform.SetParent(transform);
        marker.name = "HighlightMarker_Pooled";
        markerPool.Enqueue(marker);
    }

    /// <summary>
    /// Ambil marker dari pool (atau buat baru jika pool kosong)
    /// </summary>
    private GameObject GetMarkerFromPool()
    {
        if (!usePooling || !isInitialized)
        {
            // Fallback: instantiate langsung
            GameObject marker = Instantiate(highlightPrefab);
            marker.name = "HighlightMarker_Dynamic";
            return marker;
        }

        // Jika pool kosong, buat marker baru (dengan batas maxPoolSize)
        if (markerPool.Count == 0)
        {
            if (markerPool.Count + activeMarkers.Count < maxPoolSize)
            {
                CreateNewPooledMarker();
            }
            else
            {
                Debug.LogWarning("[TileHighlighter] Pool exhausted, creating dynamic marker");
                GameObject marker = Instantiate(highlightPrefab);
                marker.name = "HighlightMarker_Dynamic";
                return marker;
            }
        }

        // Ambil dari pool
        GameObject pooledMarker = markerPool.Dequeue();
        pooledMarker.SetActive(true);
        activeMarkers.Add(pooledMarker);
        return pooledMarker;
    }

    /// <summary>
    /// Kembalikan marker ke pool
    /// </summary>
    private void ReturnMarkerToPool(GameObject marker)
    {
        if (marker == null) return;

        if (usePooling && isInitialized && markerPool.Count < maxPoolSize)
        {
            marker.SetActive(false);
            markerPool.Enqueue(marker);
            activeMarkers.Remove(marker);
        }
        else
        {
            // Destroy jika pooling tidak aktif atau pool penuh
            Destroy(marker);
            activeMarkers.Remove(marker);
        }
    }

    /// <summary>
    /// Bersihkan semua marker aktif
    /// </summary>
    public void ClearAllMarkers()
    {
        // Copy list untuk menghindari modifikasi selama iterasi
        List<GameObject> markersToClear = new List<GameObject>(activeMarkers);

        foreach (var marker in markersToClear)
        {
            ReturnMarkerToPool(marker);
        }

        activeMarkers.Clear();
        currentPreviewTile = -1;
    }

    /// <summary>
    /// Setup default materials jika belum di-set di inspector
    /// </summary>
    private void SetupDefaultMaterials()
    {
        // Coba buat default materials jika null
        if (normalMaterial == null)
        {
            normalMaterial = CreateDefaultMaterial("TileHighlighter_Normal", new Color(0, 1, 0, 0.3f));
        }

        if (ladderMaterial == null)
        {
            ladderMaterial = CreateDefaultMaterial("TileHighlighter_Ladder", new Color(0, 0.5f, 1, 0.4f));
        }

        if (snakeMaterial == null)
        {
            snakeMaterial = CreateDefaultMaterial("TileHighlighter_Snake", new Color(1, 0, 0, 0.4f));
        }

        if (dangerMaterial == null)
        {
            dangerMaterial = CreateDefaultMaterial("TileHighlighter_Danger", new Color(1, 0.5f, 0, 0.4f));
        }

        if (cardMaterial == null)
        {
            cardMaterial = CreateDefaultMaterial("TileHighlighter_Card", new Color(0.8f, 0, 1, 0.4f));
        }
    }

    /// <summary>
    /// Buat material default dengan warna tertentu
    /// </summary>
    private Material CreateDefaultMaterial(string name, Color color)
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.name = name;
        mat.color = color;
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000; // Transparent queue

        return mat;
    }

    /// <summary>
    /// Preview tujuan gerakan pemain
    /// </summary>
    public void PreviewDestination(PlayerState player, int diceValue, int cardModifier)
    {
        ClearAllMarkers();

        if (player == null)
        {
            Debug.LogWarning("[TileHighlighter] Player is null");
            return;
        }

        if (BoardManager.Instance == null)
        {
            Debug.LogWarning("[TileHighlighter] BoardManager.Instance is null");
            return;
        }

        int totalMoves = diceValue + cardModifier;

        // PERUBAHAN: Tidak return jika totalMoves == 0, kita masih mau show preview
        // bahkan untuk 0 moves (mungkin untuk debug)
        if (totalMoves == 0)
        {
            Debug.Log("[TileHighlighter] No movement (total moves = 0)");
            // Tapi kita tetap clear marker dan tidak menampilkan apa-apa
            return;
        }

        currentPlayer = player;

        // Hitung tile akhir menggunakan simulator akurat
        var result = AccurateMovementSimulator.CalculateFinalTile(
            player.TileID,
            totalMoves,
            BoardManager.Instance
        );

        int finalTile = result.finalTile;
        bool isSnakeLadder = result.isSnakeLadder;
        int jumpSourceTile = result.jumpSourceTile;

        currentPreviewTile = finalTile;

        // Tampilkan marker di tile akhir
        Tiles targetTileObj = BoardManager.Instance.GetTileByID(finalTile);
        if (targetTileObj != null)
        {
            ShowMarkerAt(targetTileObj, finalTile);

            // Jika ada snake/ladder, tampilkan juga marker di asal snake/ladder
            if (isSnakeLadder && jumpSourceTile > 0 && jumpSourceTile != finalTile)
            {
                Tiles jumpSourceObj = BoardManager.Instance.GetTileByID(jumpSourceTile);
                if (jumpSourceObj != null)
                {
                    ShowMarkerAt(jumpSourceObj, jumpSourceTile);
                }
            }

            // DEBUG: Log arah gerakan
            string direction = totalMoves > 0 ? "MAJU" : "MUNDUR";
            Debug.Log($"[TileHighlighter] {direction}: {player.TileID} + {totalMoves} = {finalTile}");
        }
        else
        {
            Debug.LogWarning($"[TileHighlighter] Tile {finalTile} not found in board");
        }
    }

    /// <summary>
    /// Tampilkan marker di tile tertentu
    /// </summary>
    private void ShowMarkerAt(Tiles tile, int tileID)
    {
        if (tile == null) return;

        GameObject marker = GetMarkerFromPool();
        if (marker == null)
        {
            Debug.LogError("[TileHighlighter] Failed to get marker from pool");
            return;
        }

        // Set position
        Vector3 tilePosition = tile.GetPlayerPosition();
        marker.transform.position = tilePosition + Vector3.up * yOffset;

        // Set visual berdasarkan tipe tile
        SetupMarkerVisual(marker, tile.type, tileID);
    }

    /// <summary>
    /// Setup visual marker (material, scale, dll)
    /// </summary>
    private void SetupMarkerVisual(GameObject marker, TileType tileType, int tileID)
    {
        if (marker == null) return;

        // CARI RENDERER - coba berbagai jenis renderer
        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer == null)
        {
            // Coba cari di children
            renderer = marker.GetComponentInChildren<Renderer>();
        }

        if (renderer != null)
        {
            // Dapatkan material yang sesuai
            Material material = GetMaterialForTileType(tileType);
            if (material != null)
            {
                // FORCE: Buat instance material baru agar tidak shared
                Material materialInstance = new Material(material);
                materialInstance.name = material.name + "_Instance";

                // Assign ke renderer
                renderer.material = materialInstance;

                // Debug log
                Debug.Log($"[TileHighlighter] Set material {material.name} on tile {tileID} ({tileType})");
            }
            else
            {
                Debug.LogWarning($"[TileHighlighter] No material found for tile type: {tileType}");
            }
        }
        else
        {
            Debug.LogWarning($"[TileHighlighter] No Renderer found on marker prefab: {marker.name}");

        }

        // Set scale berdasarkan tipe tile
        float scale = GetScaleForTileType(tileType);
        marker.transform.localScale = Vector3.one * scale;

        // Rotasi (optional)
        marker.transform.rotation = Quaternion.identity;

        // Beri nama untuk debugging
        marker.name = $"HighlightMarker_Tile{tileID}_{tileType}";
    }
    /// <summary>
    /// Dapatkan material yang sesuai untuk tipe tile
    /// </summary>
    private Material GetMaterialForTileType(TileType type)
    {
        switch (type)
        {
            case TileType.LadderStart:
            case TileType.LadderEnd:
                return ladderMaterial ?? normalMaterial;

            case TileType.SnakeStart:
            case TileType.SnakeEnd:
                return snakeMaterial ?? normalMaterial;

            case TileType.Damage:
            case TileType.Disarm:
            case TileType.Despair:
            case TileType.Provocation:
            case TileType.Death:
                return dangerMaterial ?? normalMaterial;

            case TileType.CardRandom:
            case TileType.CardMovement:
            case TileType.CardBuff:
            case TileType.Attack:
                return cardMaterial ?? normalMaterial;

            default:
                return normalMaterial;
        }
    }

    /// <summary>
    /// Dapatkan scale yang sesuai untuk tipe tile
    /// </summary>
    private float GetScaleForTileType(TileType type)
    {
        // Buat snake/ladder lebih besar agar terlihat jelas
        if (type == TileType.LadderStart || type == TileType.SnakeStart)
            return 1.5f;

        if (AccurateMovementSimulator.IsDangerTile(type))
            return 1.2f;

        if (AccurateMovementSimulator.IsCardTile(type))
            return 1.3f;

        return 1.0f;
    }

    /// <summary>
    /// Sembunyikan semua highlight
    /// </summary>
    public void HideHighlight()
    {
        ClearAllMarkers();
        currentPreviewTile = -1;
    }

    /// <summary>
    /// Cek apakah sedang menampilkan preview
    /// </summary>
    public bool IsShowingPreview()
    {
        return activeMarkers.Count > 0;
    }

    /// <summary>
    /// Dapatkan tile yang sedang di-preview
    /// </summary>
    public int GetCurrentPreviewTile()
    {
        return currentPreviewTile;
    }

    /// <summary>
    /// Debug method untuk testing
    /// </summary>
    [ContextMenu("Test Preview (5 moves)")]
    public void TestPreview()
    {
        if (currentPlayer == null)
        {
            currentPlayer = FindObjectOfType<PlayerState>();
        }

        if (currentPlayer != null && BoardManager.Instance != null)
        {
            PreviewDestination(currentPlayer, 5, 0);
        }
        else
        {
            Debug.LogWarning("[TileHighlighter] TestPreview: PlayerState or BoardManager not found");
        }
    }

    [ContextMenu("Clear Preview")]
    public void ClearPreview()
    {
        ClearAllMarkers();
    }

    [ContextMenu("Print Pool Status")]
    public void PrintPoolStatus()
    {
        Debug.Log($"[TileHighlighter] Pool Status: {markerPool.Count} in pool, {activeMarkers.Count} active, {markerPool.Count + activeMarkers.Count} total");
    }

    /// <summary>
    /// Cleanup saat di-destroy
    /// </summary>
    void OnDestroy()
    {
        ClearAllMarkers();

        // Destroy semua marker di pool
        while (markerPool.Count > 0)
        {
            GameObject marker = markerPool.Dequeue();
            if (marker != null)
                Destroy(marker);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}