using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MultiplayerManager : MonoBehaviour
{
    [Header("Prefabs & References")]
    public GameObject playerPrefab;    // prefab pawn harus ada skrip PlayerPawn
    public Transform playersParent;     // parent pawn

    [Header("UI - Player Count Selection")]
    public GameObject playerSelectPanel;
    public Button btn2Players;
    public Button btn3Players;
    public Button btn4Players;
    public GameObject orderPanel;        // panel pas pilih berapa player
    public Button drawOrderButton;         // tombol untuk draw giliran
    public TextMeshProUGUI poolText;        // menampilkan sisa pool
    public TextMeshProUGUI orderStatusText; // menampilkan siapa yg sudah draw & angkanya

    [Header("UI - Gameplay")]
    public TextMeshProUGUI infoText;        // informasi giliran / nilai dice
    public UIManager uiManager; // <-- TAMBAHKAN: Referensi ke UIManager

    [Header("Board Settings")]
    public int totalTilesInBoard = 100;

    [Header("Dice Physics")]
    // Tarik objek Dadu Fisik (yang punya script Dice.cs) ke sini
    public Dice physicalDice;

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

    // Properti agar Dice.cs bisa cek status game
    public bool IsActionRunning => isActionRunning;

    private bool isSpawning = false;

    // radius offset saat ada banyak pawn di satu tile
    public float tileOffsetBaseRadius = 0.25f;
    public float tileOffsetHeightStep = 0.02f;
    public float tileOffsetPerPlayer = 0.18f;

    void Awake()
    {
        // Hook tombol pilihan player
        btn2Players.onClick.AddListener(() => OnChoosePlayerCount(2));
        btn3Players.onClick.AddListener(() => OnChoosePlayerCount(3));
        btn4Players.onClick.AddListener(() => OnChoosePlayerCount(4));

        drawOrderButton.onClick.AddListener(OnDrawOrderPressed);

        // awal UI
        orderPanel.SetActive(false);
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
        if (isSpawning) return;
        isSpawning = true;

        if (playerSelectPanel != null)
            playerSelectPanel.SetActive(false);

        StartCoroutine(ClearAndSpawnRoutine(count));
    }

    void SpawnPlayers(int count)
    {
        players.Clear();
        Vector3 startPos = GetTilePosition(1);
        float offsetRadius = 0.35f;

        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(playerPrefab, playersParent);
            go.name = $"Player{i + 1}";
            PlayerPawn pp = go.GetComponent<PlayerPawn>();
            if (pp == null) pp = go.AddComponent<PlayerPawn>();

            float angle = (360f / count) * i * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * offsetRadius;

            pp.playerIndex = i;
            pp.currentTileID = 1;
            pp.SetVisualIndex(i);
            go.transform.localScale = playerPrefab.transform.localScale;
            Vector3 posWithOffset = GetTilePositionWithOffset(1, pp);
            go.transform.position = posWithOffset;
            players.Add(pp);

            Debug.Log("players.Count = " + players.Count + ", selectedPlayerCount = " + selectedPlayerCount);
        }
    }

    IEnumerator ClearAndSpawnRoutine(int count)
    {
        if (playersParent != null)
        {
            int childCount = playersParent.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                Transform child = playersParent.GetChild(i);
                Destroy(child.gameObject);
            }
            yield return null;
            while (playersParent.childCount > 0)
            {
                yield return null;
            }
        }

        players.Clear();
        drawnNumbers.Clear();
        turnOrder.Clear();
        drawIndex = 0;
        currentTurnIdx = 0;

        selectedPlayerCount = count;
        SpawnPlayers(count);
        StartOrderSelection();
        isSpawning = false;
    }

    // -------------------------
    // Order selection (pool draw)
    // -------------------------
    void StartOrderSelection()
    {
        Debug.Log("players.Count = " + players.Count + ", selectedPlayerCount = " + selectedPlayerCount);
        dicePool = new List<int> { 1, 2, 3, 4, 5, 6 };
        drawnNumbers.Clear();
        drawIndex = 0;

        orderPanel.SetActive(true);
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
        for (int i = 0; i < selectedPlayerCount; i++)
        {
            PlayerPawn p = players.FirstOrDefault(x => x.playerIndex == i);
            if (p != null && drawnNumbers.ContainsKey(p))
                lines.Add($"P{i + 1}: {drawnNumbers[p]}");
            else
                lines.Add($"P{i + 1}: -");
        }
        orderStatusText.text = string.Join("   |   ", lines);
    }

    void OnDrawOrderPressed()
    {
        if (drawIndex >= players.Count) return;
        if (dicePool.Count == 0)
        {
            Debug.LogError("Pool kosong (should not happen for <=4 players)");
            return;
        }

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
            FinalizeTurnOrder();
        }
    }

    void FinalizeTurnOrder()
    {
        // sort players by drawnNumbers DESC
        turnOrder = drawnNumbers.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();

        // display order
        string orderStr = "Turn Order: " + string.Join(" > ", turnOrder.Select(p => p.name));
        infoText.text = orderStr;

        orderPanel.SetActive(false);

        // ---TAMBAHKAN BLOK INI ---
        // Beri tahu UIManager untuk membuat daftar UI pemain
        if (uiManager != null)
        {
            uiManager.SetupPlayerList(turnOrder);
        }
        // -------------------------

        // set current turn to first player
        currentTurnIdx = 0;
        HighlightCurrentPlayer();
    }

    // -------------------------
    // Gameplay: roll & move (ALUR BARU)
    // -------------------------

    /// <summary>
    /// BARU: Fungsi ini dipanggil oleh Dice.cs saat dadu selesai dilempar
    /// </summary>
    public void NotifyDiceThrown()
    {
        if (isActionRunning) return;
        StartCoroutine(WaitForDiceToSettleAndMove());
    }

    /// <summary>
    /// BARU: Menggantikan 'RollDiceSequence' yang lama.
    /// </summary>
    IEnumerator WaitForDiceToSettleAndMove()
    {
        isActionRunning = true; // Kunci game

        PlayerPawn current = turnOrder[currentTurnIdx];
        infoText.text = $"{current.name} melempar dadu...";

        // 1. Tunggu dadu berhenti bergulir
        int rollResult = 0;
        yield return StartCoroutine(physicalDice.WaitForRollToStop((result) =>
        {
            rollResult = result;
        }));

        // 2. Dadu berhenti, panggil HandlePlayerRollAndMove
        yield return StartCoroutine(HandlePlayerRollAndMove(current, rollResult));
    }

    IEnumerator HandlePlayerRollAndMove(PlayerPawn player, int roll)
    {
        infoText.text = $"{player.name} roll {roll}";

        // movement logic (bounce back + ladder/snake)
        int startTile = player.currentTileID;
        int finalTarget = startTile + roll;

        if (finalTarget == totalTilesInBoard)
        {
            // move to finish
            yield return StartCoroutine(player.MoveToTile(finalTarget, (int id) => GetTilePositionWithOffset(id, player)));
            infoText.text = $"{player.name} mencapai finish!";
            isActionRunning = false; // Game berakhir di sini
            yield break;
        }
        else if (finalTarget > totalTilesInBoard)
        {
            // move to 100 then bounce back
            int overshoot = finalTarget - totalTilesInBoard;
            int bounceTarget = totalTilesInBoard - overshoot;

            yield return StartCoroutine(player.MoveToTile(totalTilesInBoard, (int id) => GetTilePositionWithOffset(id, player)));
            yield return new WaitForSeconds(0.2f);
            yield return StartCoroutine(player.MoveToTile(bounceTarget, (int id) => GetTilePositionWithOffset(id, player)));
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

        // --- PERBAIKAN ---
        // isActionRunning = false HARUS di set SEBELUM memanggil HighlightCurrentPlayer.
        // Ini agar OnMouseDown di Dice.cs bisa mendeteksi bahwa game sudah tidak sibuk.
        isActionRunning = false;
        HighlightCurrentPlayer();
        // -----------------

        yield return null;
    }

    void HighlightCurrentPlayer()
    {
        // --- BARU ---
        // Reset dadu ke posisi awal di setiap awal giliran
        if (physicalDice != null)
        {
            physicalDice.ResetDice();
        }
        // ------------

        // UI helper: tunjukkan siapa giliran sekarang
        if (turnOrder.Count == 0) return;
        PlayerPawn cur = turnOrder[currentTurnIdx];

        infoText.text = $"Giliran: {cur.name}. Ambil & lempar dadunya!";

        for (int i = 0; i < players.Count; i++)
        {
            players[i].SetHighlight(players[i] == cur);
        }

        // --- TAMBAHKAN BLOK INI ---
        // Beri tahu UIManager siapa pemain yang aktif
        if (uiManager != null)
        {
            uiManager.UpdateActivePlayer(currentTurnIdx);
        }
        // -------------------------
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

    // (Fungsi helper lain seperti FadeOutPanel, GetTilePositionWithOffset, dll,
    //  tidak saya sertakan di sini karena tidak berubah, tapi pastikan mereka
    //  masih ada di script-mu dari kode sebelumnya)

    // (Pastikan fungsi-fungsi ini masih ada di kodemu)
    public Vector3 GetTilePositionWithOffset(int tileID, PlayerPawn pawn)
    {
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

        float radius = tileOffsetBaseRadius + tileOffsetPerPlayer * (count - 1);
        if (count == 2) radius = Mathf.Max(radius, 0.32f);

        float angle = (360f / count) * slot * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        Vector3 upOffset = Vector3.up * (tileOffsetHeightStep * slot);

        return center + offset + upOffset;
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
}