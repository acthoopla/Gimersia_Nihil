using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// BoardManager (SRP)
/// - Memuat Tiles dari scene
/// - Menyediakan lookup Tile by ID
/// - Menghitung row (1..10)
/// - Menyediakan posisi tile (GetTilePosition)
/// - Utility: shuffle board positions (boss effect) & restore
///
/// Catatan:
/// - Tiles adalah component kelas Tiles (tidak menggunakan ScriptableObject)
/// - BoardManager tidak menjalankan efek tile; itu tugas TileEffectSystem
/// - BoardManager hanya bertanggung jawab atas data & posisi papan
/// </summary>
[DisallowMultipleComponent]
public class BoardManager : MonoBehaviour
{
    [Header("Board Settings")]
    [Tooltip("Total tile pada board (default 100 untuk 10x10)")]
    public int totalTiles = 100;

    [Tooltip("Auto find Tiles on Start (jika false, isi tiles manual di inspector)")]
    public bool autoFindTiles = true;

    [Tooltip("List tile (index tidak harus berurutan). Tiles.tileID harus unik.")]
    public List<Tiles> tiles = new List<Tiles>();

    // Internal caches
    private Dictionary<int, Tiles> tileLookup = new Dictionary<int, Tiles>();
    private Vector3[] originalPositions;
    private Quaternion[] originalRotations;
    private bool positionsCached = false;

    public static BoardManager Instance { get; private set; }

    void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);

        if (autoFindTiles)
        {
            LoadTilesFromScene();
        }
        else
        {
            BuildLookupFromList();
        }
    }

    #region Loading / Lookup
    /// <summary>
    /// Temukan semua Tiles di scene dan susun berdasarkan tileID.
    /// </summary>
    public void LoadTilesFromScene()
    {
        Tiles[] all = FindObjectsOfType<Tiles>();
        tiles = all.OrderBy(t => t.tileID).ToList();
        BuildLookupFromList();
        CacheOriginalTransforms();
    }

    /// <summary>
    /// Build dictionary lookup dari list tiles (harus dipanggil setelah tiles di-populate)
    /// </summary>
    private void BuildLookupFromList()
    {
        tileLookup.Clear();
        foreach (var t in tiles)
        {
            if (t == null) continue;
            if (!tileLookup.ContainsKey(t.tileID))
                tileLookup.Add(t.tileID, t);
            else
                Debug.LogWarning($"[BoardManager] Duplicate tileID detected: {t.tileID} on object {t.gameObject.name}");
        }
    }

    /// <summary>
    /// Ambil Tiles object berdasarkan tileID. Mengembalikan null jika tidak ditemukan.
    /// </summary>
    public Tiles GetTileByID(int tileID)
    {
        if (tileLookup.TryGetValue(tileID, out Tiles t)) return t;
        return null;
    }

    /// <summary>
    /// Ambil posisi "player anchor" dari tile (menggunakan Tiles.GetPlayerPosition()).
    /// BoardManager tidak menambahkan offset per-player; itu dilakukan oleh MovementSystem / MultiplayerManager.
    /// </summary>
    public Vector3 GetTilePosition(int tileID)
    {
        Tiles t = GetTileByID(tileID);
        if (t == null) return Vector3.zero;
        return t.GetPlayerPosition();
    }

    /// <summary>
    /// Mengembalikan semua tiles terurut by tileID (ascending).
    /// </summary>
    public List<Tiles> GetAllTilesOrdered()
    {
        return tiles.OrderBy(t => t.tileID).ToList();
    }

    /// <summary>
    /// Hitung row (1..boardHeight) berdasarkan tileID.
    /// Asumsi board width = 10 untuk GDD saat ini; rumus umum:
    /// row = floor((tileID - 1) / rowWidth) + 1
    /// </summary>
    public int GetRow(int tileID, int rowWidth = 10)
    {
        if (tileID <= 0) return 0;
        return Mathf.FloorToInt((tileID - 1) / (float)rowWidth) + 1;
    }

    /// <summary>
    /// Ambil semua tiles pada row tertentu (1-based).
    /// </summary>
    public List<Tiles> GetTilesInRow(int row, int rowWidth = 10)
    {
        if (row <= 0) return new List<Tiles>();
        int start = (row - 1) * rowWidth + 1;
        int end = Mathf.Min(totalTiles, row * rowWidth);
        List<Tiles> result = new List<Tiles>();
        for (int i = start; i <= end; i++)
        {
            var t = GetTileByID(i);
            if (t != null) result.Add(t);
        }
        return result;
    }
    #endregion

    #region Shuffle / Restore (Boss effect)
    /// <summary>
    /// Cache original transforms (pos & rot) for restore later.
    /// Dipanggil otomatis saat LoadTilesFromScene.
    /// </summary>
    private void CacheOriginalTransforms()
    {
        if (tiles == null || tiles.Count == 0) return;
        originalPositions = new Vector3[tiles.Count];
        originalRotations = new Quaternion[tiles.Count];
        for (int i = 0; i < tiles.Count; i++)
        {
            originalPositions[i] = tiles[i].transform.position;
            originalRotations[i] = tiles[i].transform.rotation;
        }
        positionsCached = true;
    }

    /// <summary>
    /// Shuffle board positions: menukar transform.position/rotation antar tile secara acak.
    /// Efek: secara visual susunan papan berubah sementara data tile (tile.tileID, tile.type, target) tetap melekat ke object tile.
    /// Gunakan ini bila boss effect ingin *mengacak papan secara visual*.
    /// </summary>
    public void ShuffleBoardPositions(int? seed = null)
    {
        if (tiles == null || tiles.Count == 0) return;
        System.Random rng = (seed.HasValue) ? new System.Random(seed.Value) : new System.Random();
        int n = tiles.Count;
        Vector3[] positions = tiles.Select(t => t.transform.position).ToArray();
        Quaternion[] rotations = tiles.Select(t => t.transform.rotation).ToArray();

        // Fisher-Yates shuffle of indices
        int[] indices = Enumerable.Range(0, n).ToArray();
        for (int i = n - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            int tmp = indices[i];
            indices[i] = indices[j];
            indices[j] = tmp;
        }

        // Assign shuffled transforms
        for (int i = 0; i < n; i++)
        {
            tiles[i].transform.position = positions[indices[i]];
            tiles[i].transform.rotation = rotations[indices[i]];
        }
    }

    /// <summary>
    /// Kembalikan posisi papan ke posisi semula (sebelum shuffle).
    /// </summary>
    public void RestoreBoardPositions()
    {
        if (!positionsCached || tiles == null || tiles.Count == 0) return;
        for (int i = 0; i < tiles.Count; i++)
        {
            tiles[i].transform.position = originalPositions[i];
            tiles[i].transform.rotation = originalRotations[i];
        }
    }

    /// <summary>
    /// Mengacak tileID pada objek tiles (ganti tile.tileID antar object).
    /// PERINGATAN: ini mengubah tileID sehingga semua referensi ID harus disesuaikan.
    /// Gunakan hanya jika ingin benar-benar menukar indeks tile (lebih 'keras' daripada shuffle positions).
    /// </summary>
    public void ShuffleTileIDs(int? seed = null)
    {
        if (tiles == null || tiles.Count == 0) return;
        System.Random rng = (seed.HasValue) ? new System.Random(seed.Value) : new System.Random();
        // create shuffled list of ids
        var ids = tiles.Select(t => t.tileID).ToList();
        int n = ids.Count;
        for (int i = n - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            int tmp = ids[i];
            ids[i] = ids[j];
            ids[j] = tmp;
        }

        // Assign shuffled ids (and update visuals)
        for (int i = 0; i < tiles.Count; i++)
        {
            tiles[i].tileID = ids[i];
            // Ensure tile updates visuals/validation
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(tiles[i]);
#endif
            // Update display (if Tiles has method to refresh visuals)
            tiles[i].UpdateTileNumber();
            tiles[i].UpdateVisualModel();
        }

        // Rebuild lookup
        BuildLookupFromList();
    }
    #endregion

    #region Debug / Editor Helpers
    [ContextMenu("Log Board Summary")]
    public void LogBoardSummary()
    {
        Debug.Log($"[BoardManager] Total tiles: {tiles.Count}");
        foreach (var t in tiles.OrderBy(x => x.tileID))
        {
            Debug.Log($"Tile {t.tileID} -> {t.gameObject.name} (type: {t.type})");
        }
    }

    [ContextMenu("Validate Tile IDs")]
    public void ValidateTileIDs()
    {
        var dup = tiles.GroupBy(x => x.tileID).Where(g => g.Count() > 1).ToList();
        if (dup.Count > 0)
        {
            Debug.LogWarning("[BoardManager] Duplicate tileIDs found!");
            foreach (var g in dup)
            {
                Debug.LogWarning($"tileID {g.Key} has {g.Count()} objects.");
            }
        }
        else
        {
            Debug.Log("[BoardManager] TileID validation OK.");
        }
    }
    #endregion
}
