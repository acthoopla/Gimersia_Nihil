using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MultiplayerManager : MonoBehaviour
{
    [Header("Prefabs & References")]
    public GameObject playerPrefab;     // prefab pawn harus ada skrip PlayerPawn
    public Transform playersParent;      // parent pawn

    [Header("UI - Player Count Selection")]
    public GameObject playerSelectPanel;
    public Button btn2Players;
    public Button btn3Players;
    public Button btn4Players;
    public GameObject orderPanel;       // panel pas pilih berapa player
    public Button drawOrderButton;         // tombol untuk draw giliran
    public TextMeshProUGUI poolText;       // menampilkan sisa pool
    public TextMeshProUGUI orderStatusText; // menampilkan siapa yg sudah draw & angkanya

    [Header("UI - Gameplay")]
    public Button rollButton;              // tombol roll setelah order selesai
    public TextMeshProUGUI infoText;       // informasi giliran / nilai dice

    [Header("Board Settings")]
    public int totalTilesInBoard = 100;

    // runtime
    private List<Tiles> boardTiles = new List<Tiles>();
    private List<PlayerPawn> players = new List<PlayerPawn>();
    private int selectedPlayerCount = 0;

    // order selection
    private List<int> dicePool = new List<int>(); // pool 1..6
    private Dictionary<PlayerPawn, int> drawnNumbers = new Dictionary<PlayerPawn, int>();
    private int drawIndex = 0; // index pemain yang sedang harus draw (0..n-1)

    // turn order
    private List<PlayerPawn> turnOrder = new List<PlayerPawn>();
    private int currentTurnIdx = 0;
    private bool isActionRunning = false;

    private bool isSpawning = false;

    // radius offset saat ada banyak pawn di satu tile
    public float tileOffsetBaseRadius = 0.25f; // ubah sesuai kebutuhan
    public float tileOffsetHeightStep = 0.02f; // sedikit Y offset agar tidak benar-benar saling menutupi
    public float tileOffsetPerPlayer = 0.18f;

    void Awake()
    {
        // Hook tombol pilihan player
        btn2Players.onClick.AddListener(() => OnChoosePlayerCount(2));
        btn3Players.onClick.AddListener(() => OnChoosePlayerCount(3));
        btn4Players.onClick.AddListener(() => OnChoosePlayerCount(4));

        drawOrderButton.onClick.AddListener(OnDrawOrderPressed);
        rollButton.onClick.AddListener(OnRollPressed);

        // awal UI
        orderPanel.SetActive(false);
        rollButton.gameObject.SetActive(false);
        infoText.text = "Pilih jumlah pemain";
        orderStatusText.text = "";
    }

    void Start()
    {
        // Build board tiles lookup
        Tiles[] all = FindObjectsOfType<Tiles>();
        boardTiles = all.OrderBy(t => t.tileID).ToList();

        if (boardTiles.Count == 0)
        {
            Debug.LogError("Tidak menemukan Tiles di scene! Pastikan Tiles terpasang.");
            infoText.text = "Error: Board tidak ditemukan";
        }
    }

    // -------------------------
    // Player count & spawn
    // -------------------------
    void OnChoosePlayerCount(int count)
    {
        if (isSpawning) return; // mencegah double click
        isSpawning = true;

        // Hide panel segera sehingga pemain tidak bisa klik lagi
        if (playerSelectPanel != null)
            playerSelectPanel.SetActive(false);

        // Start coroutine pembersihan + spawn
        StartCoroutine(ClearAndSpawnRoutine(count));
    }

    void SpawnPlayers(int count)
    {
        players.Clear();
        Vector3 startPos = GetTilePosition(1); // Tile ID 1 dianggap start
        float offsetRadius = 0.35f;

        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(playerPrefab, playersParent);
            go.name = $"Player{i + 1}";
            PlayerPawn pp = go.GetComponent<PlayerPawn>();
            if (pp == null) pp = go.AddComponent<PlayerPawn>(); // fallback

            // position with small offset so pawns don't overlap
            float angle = (360f / count) * i * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * offsetRadius;

            // set index, currentTileID
            pp.playerIndex = i;
            pp.currentTileID = 1;
            pp.SetVisualIndex(i);

            // set scale seperti prefab (safety)
            go.transform.localScale = playerPrefab.transform.localScale;

            // set posisi menggunakan fungsi offset yang sudah ada
            Vector3 posWithOffset = GetTilePositionWithOffset(1, pp);
            go.transform.position = posWithOffset;

            players.Add(pp);

            Debug.Log("players.Count = " + players.Count + ", selectedPlayerCount = " + selectedPlayerCount);
        }
    }

    void ClearExistingPlayers()
    {
        // Hapus semua child di playersParent dengan cara aman
        if (playersParent != null)
        {
            // gunakan while loop berdasarkan childCount untuk menghindari masalah enumerasi
            while (playersParent.childCount > 0)
            {
                Transform child = playersParent.GetChild(0);
                DestroyImmediate(child.gameObject); // di Editor bisa gunakan DestroyImmediate, runtime gunakan Destroy
            }
        }
        players.Clear();
        drawnNumbers.Clear();
        turnOrder.Clear();
        drawIndex = 0;
        currentTurnIdx = 0;
    }

    // -------------------------
    // Order selection (pool draw)
    // -------------------------
    void StartOrderSelection()
    {
        Debug.Log("players.Count = " + players.Count + ", selectedPlayerCount = " + selectedPlayerCount);
        // init pool
        dicePool = new List<int> { 1, 2, 3, 4, 5, 6 };
        drawnNumbers.Clear();
        drawIndex = 0;

        orderPanel.SetActive(true);
        rollButton.gameObject.SetActive(false);
        UpdatePoolUI();
        UpdateOrderStatusUI();

        infoText.text = $"Order Selection: Giliran Player {drawIndex + 1} untuk Draw";
    }

    void UpdatePoolUI()
    {
        poolText.text = "Pool: " + string.Join(", ", dicePool);
    }

    void UpdateOrderStatusUI()
    {
        List<string> lines = new List<string>();

        // Tampilkan sesuai jumlah pemain yang dipilih (selectedPlayerCount)
        for (int i = 0; i < selectedPlayerCount; i++)
        {
            // Temukan player object yang punya playerIndex == i, jika ada
            PlayerPawn p = players.FirstOrDefault(x => x.playerIndex == i);
            if (p != null && drawnNumbers.ContainsKey(p))
                lines.Add($"P{i + 1}: {drawnNumbers[p]}");
            else
                lines.Add($"P{i + 1}: -");
        }

        orderStatusText.text = string.Join("  |  ", lines);
    }

    void OnDrawOrderPressed()
    {
        if (drawIndex >= players.Count) return;
        if (dicePool.Count == 0)
        {
            Debug.LogError("Pool kosong (should not happen for <=4 players)");
            return;
        }

        // ambil random element dari pool dan remove
        int idx = Random.Range(0, dicePool.Count);
        int val = dicePool[idx];
        dicePool.RemoveAt(idx);

        PlayerPawn p = players[drawIndex];
        drawnNumbers[p] = val;

        UpdatePoolUI();
        UpdateOrderStatusUI();

        drawIndex++;
        if (drawIndex < players.Count)
        {
            infoText.text = $"Order Selection: Giliran Player {drawIndex + 1} untuk Draw";
        }
        else
        {
            // selesai draw, finalize turn order
            FinalizeTurnOrder();
        }
    }

    public Vector3 GetTilePositionWithOffset(int tileID, PlayerPawn pawn)
    {
        // ambil center tile
        Vector3 center = GetTilePosition(tileID);

        var onTile = players.Where(p => p.currentTileID == tileID).OrderBy(p => p.playerIndex).ToList();
        if (!onTile.Contains(pawn))
        {
            var tmp = onTile.ToList();
            tmp.Add(pawn);
            onTile = tmp.OrderBy(p => p.playerIndex).ToList();
        }

        int count = Mathf.Max(1, onTile.Count);
        int slot = onTile.FindIndex(p => p == pawn);
        if (slot < 0) slot = 0;

        // adaptif radius: base + perPlayer * (count-1)
        float radius = tileOffsetBaseRadius + tileOffsetPerPlayer * (count - 1);

        // jika hanya 2 pemain, atur radius lebih besar agar tidak berpelukan
        if (count == 2) radius = Mathf.Max(radius, 0.32f);

        float angle = (360f / count) * slot * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        Vector3 upOffset = Vector3.up * (tileOffsetHeightStep * slot);

        return center + offset + upOffset;
    }

    IEnumerator ClearAndSpawnRoutine(int count)
    {
        // 1) Hapus semua child PlayersParent dengan cara aman di runtime
        if (playersParent != null)
        {
            // Hapus semua child secara bertahap
            int childCount = playersParent.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                Transform child = playersParent.GetChild(i);
                Destroy(child.gameObject);
            }

            // Tunggu hingga frame berikutnya agar Unity menyelesaikan Destroy()
            yield return null;
            // Jika masih ada child (jarang), tunggu lagi
            while (playersParent.childCount > 0)
            {
                yield return null;
            }
        }

        // 2) Reset list internal dengan pasti
        players.Clear();
        drawnNumbers.Clear();
        turnOrder.Clear();
        drawIndex = 0;
        currentTurnIdx = 0;

        // 3) Simpan selected count & spawn
        selectedPlayerCount = count;
        SpawnPlayers(count);

        // 4) Start order selection
        StartOrderSelection();

        isSpawning = false;
    }

    IEnumerator FadeOutPanel(CanvasGroup panelGroup, float duration)
    {
        float startAlpha = panelGroup.alpha;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            panelGroup.alpha = Mathf.Lerp(startAlpha, 0, time / duration);
            yield return null;
        }
        panelGroup.alpha = 0;
        panelGroup.gameObject.SetActive(false);
    }

    void FinalizeTurnOrder()
    {
        // sort players by drawnNumbers DESC
        turnOrder = drawnNumbers.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();

        // display order
        string orderStr = "Turn Order: " + string.Join(" > ", turnOrder.Select(p => p.name));
        infoText.text = orderStr;

        // tutup order panel dan aktifkan roll button untuk gameplay
        orderPanel.SetActive(false);
        rollButton.gameObject.SetActive(true);

        // set current turn to first player
        currentTurnIdx = 0;
        HighlightCurrentPlayer();
    }

    // -------------------------
    // Gameplay: roll & move
    // -------------------------
    void OnRollPressed()
    {
        if (isActionRunning) return;
        if (turnOrder == null || turnOrder.Count == 0) return;

        PlayerPawn current = turnOrder[currentTurnIdx];
        StartCoroutine(HandlePlayerRollAndMove(current));
    }

    IEnumerator HandlePlayerRollAndMove(PlayerPawn player)
    {
        isActionRunning = true;

        int roll = Random.Range(1, 7);
        infoText.text = $"{player.name} roll {roll}";

        // movement logic (bounce back + ladder/snake)
        int startTile = player.currentTileID;
        int finalTarget = startTile + roll;

        if (finalTarget == totalTilesInBoard)
        {
            // move to finish
            yield return StartCoroutine(player.MoveToTile(finalTarget, (int id) => GetTilePositionWithOffset(id, player)));
            infoText.text = $"{player.name} mencapai finish!";
            // NOTE: game over handling not implemented here; you can add ranking etc.
            isActionRunning = false;
            yield break;
        }
        else if (finalTarget > totalTilesInBoard)
        {
            // move to 100 then bounce back
            int overshoot = finalTarget - totalTilesInBoard;
            int bounceTarget = totalTilesInBoard - overshoot;

            yield return StartCoroutine(player.MoveToTile(totalTilesInBoard, GetTilePosition));
            yield return new WaitForSeconds(0.2f);
            yield return StartCoroutine(player.MoveToTile(bounceTarget, GetTilePosition));
        }
        else
        {
            // normal move
            yield return StartCoroutine(player.MoveToTile(finalTarget, (int id) => GetTilePositionWithOffset(id, player)));
        }

        // after landing, check tile special (ladder/snake) using Tiles component
        Tiles landed = GetTileByID(player.currentTileID);
        if (landed != null)
        {
            if (landed.type == TileType.LadderStart && landed.targetTile != null)
            {
                infoText.text = $"{player.name} Naik tangga!";
                yield return new WaitForSeconds(0.2f);
                int targetID = landed.targetTile.tileID;
                yield return StartCoroutine(player.TeleportToTile(targetID, (int id) => GetTilePositionWithOffset(id, player)));
            }
            else if (landed.type == TileType.SnakeStart && landed.targetTile != null)
            {
                infoText.text = $"{player.name} Turun ular!";
                yield return new WaitForSeconds(0.2f);
                int targetID = landed.targetTile.tileID;
                yield return StartCoroutine(player.TeleportToTile(targetID, (int id) => GetTilePositionWithOffset(id, player)));
            }
        }

        // selesai gerakan, lanjut ke player selanjutnya
        currentTurnIdx = (currentTurnIdx + 1) % turnOrder.Count;
        HighlightCurrentPlayer();
        isActionRunning = false;
        yield return null;
    }

    void HighlightCurrentPlayer()
    {
        // UI helper: tunjukkan siapa giliran sekarang
        if (turnOrder.Count == 0) return;
        PlayerPawn cur = turnOrder[currentTurnIdx];
        infoText.text = $"Giliran: {cur.name}";
        // bisa tambahkan visual highlight pada pawn melalui PlayerPawn.SetHighlight(bool)
        for (int i = 0; i < players.Count; i++)
        {
            players[i].SetHighlight(players[i] == cur);
        }
    }

    // -------------------------
    // Helpers utk board tiles
    // -------------------------
    Vector3 GetTilePosition(int tileID)
    {
        Tiles t = GetTileByID(tileID);
        if (t != null) return t.GetPlayerPosition();
        Debug.LogWarning($"Tile {tileID} tidak ditemukan, default ke origin");
        return Vector3.zero;
    }

    Tiles GetTileByID(int id)
    {
        return boardTiles.FirstOrDefault(x => x.tileID == id);
    }
}
