using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MultiplayerManager : MonoBehaviour
{
    // (Semua variabel Header-mu tidak berubah)
    #region Variabel Inspector
    [Header("Prefabs & References")]
    public GameObject[] playerPrefabs; 
    public Transform playersParent;

    [Header("UI - Player Count Selection")]
    public GameObject playerSelectPanel;
    public Button btn2Players;
    public Button btn3Players;
    public Button btn4Players;
    public GameObject orderPanel;
    public Button drawOrderButton;
    public TextMeshProUGUI poolText;
    public TextMeshProUGUI orderStatusText;

    [Header("UI - Gameplay")]
    public TextMeshProUGUI infoText;
    public UIManager uiManager;

    [Header("UI - Reverse Choice")]
    public GameObject choicePanel;
    public Button btnMoveSelf;
    public Button btnReverse;
    public Button btnCancelTargetSelect;
    public TextMeshProUGUI choiceInstructionText;

    [Header("Dice Physics")]
    public Dice physicalDice;
    public GameObject diceContainmentWall; 

    [Header("Board Settings")]
    public int totalTilesInBoard = 100;
    
    [Header("Tile Offset")]
    public float tileOffsetBaseRadius = 0.25f;
    public float tileOffsetPerPlayer = 0.18f;
    public float tileOffsetHeightStep = 0.02f;
    #endregion

    // (Semua variabel internal tidak berubah)
    #region Variabel Internal
    private List<Tiles> boardTiles = new List<Tiles>();
    private List<PlayerPawn> players = new List<PlayerPawn>();
    private int selectedPlayerCount = 0;
    private List<int> dicePool = new List<int>();
    private Dictionary<PlayerPawn, int> drawnNumbers = new Dictionary<PlayerPawn, int>();
    private int drawIndex = 0;
    private List<PlayerPawn> turnOrder = new List<PlayerPawn>();
    private int currentTurnIdx = 0;
    private bool isActionRunning = false;
    public bool IsActionRunning => isActionRunning;
    private bool isSpawning = false;
    private int currentCycle = 1; 
    private bool awaitingTargetSelection = false;
    private PlayerPawn selectedTargetForReverse = null;
    private PlayerPawn currentActorForSelection = null;
    private List<PlayerPawn> currentValidTargets = new List<PlayerPawn>();
    private bool isInReverseMode = false;
    #endregion
    
    // (Fungsi Awake() dan Start() tidak berubah)
    #region Unity Callbacks
    void Awake()
    {
        btn2Players.onClick.AddListener(() => OnChoosePlayerCount(2));
        btn3Players.onClick.AddListener(() => OnChoosePlayerCount(3));
        btn4Players.onClick.AddListener(() => OnChoosePlayerCount(4));
        drawOrderButton.onClick.AddListener(OnDrawOrderPressed);
        btnMoveSelf.onClick.AddListener(OnChoice_MoveSelf);
        btnReverse.onClick.AddListener(OnChoice_Reverse);
        if (btnCancelTargetSelect != null) btnCancelTargetSelect.onClick.AddListener(OnChoice_Cancel);
        if (orderPanel != null) orderPanel.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(false);
        if (choiceInstructionText != null) choiceInstructionText.gameObject.SetActive(false);
        if (infoText != null) infoText.text = "Pilih jumlah pemain";
        if (orderStatusText != null) orderStatusText.text = "";
    }

    void Start()
    {
        Tiles[] all = FindObjectsOfType<Tiles>();
        boardTiles = all.OrderBy(t => t.tileID).ToList();
        if (boardTiles.Count == 0)
        {
            Debug.LogError("Tidak menemukan Tiles di scene! Pastikan Tiles terpasang.");
            if (infoText != null) infoText.text = "Error: Board tidak ditemukan";
        }
    }
    #endregion
    
    // (Fungsi Spawn & Order Selection tidak berubah)
    #region Player Spawn & Order Selection
    void OnChoosePlayerCount(int count)
    {
        if (isSpawning) return;
        isSpawning = true;
        if (playerSelectPanel != null) playerSelectPanel.SetActive(false);
        StartCoroutine(ClearAndSpawnRoutine(count));
    }

    void SpawnPlayers(int count)
    {
        players.Clear();
        if (playerPrefabs == null || playerPrefabs.Length < count)
        {
            Debug.LogError($"Error: Prefab pemain tidak cukup! Butuh {count}, tapi hanya ada {playerPrefabs.Length} di Inspector.");
            return;
        }
        for (int i = 0; i < count; i++)
        {
            GameObject prefabToSpawn = playerPrefabs[i];
            GameObject go = Instantiate(prefabToSpawn, playersParent);
            go.name = $"Player_{i + 1}";
            PlayerPawn pp = go.GetComponent<PlayerPawn>();
            if (pp == null) pp = go.AddComponent<PlayerPawn>();
            pp.playerIndex = i;
            pp.currentTileID = 1;
            pp.SetVisualIndex(i);
            pp.SetManager(this);
            Vector3 posWithOffset = GetTilePositionWithOffset(1, pp);
            go.transform.position = posWithOffset;
            players.Add(pp);
        }
        Debug.Log($"SpawnPlayers: spawned {players.Count} players");
    }

    IEnumerator ClearAndSpawnRoutine(int count)
    {
        if (playersParent != null)
        {
            for (int i = playersParent.childCount - 1; i >= 0; i--)
            {
                Transform child = playersParent.GetChild(i);
                Destroy(child.gameObject);
            }
            yield return null;
            while (playersParent.childCount > 0) yield return null;
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

    void StartOrderSelection()
    {
        dicePool = new List<int> { 1, 2, 3, 4, 5, 6 };
        drawnNumbers.Clear();
        drawIndex = 0;
        if (orderPanel != null) orderPanel.SetActive(true);
        UpdatePoolUI();
        UpdateOrderStatusUI();
        if (infoText != null) infoText.text = $"Order Selection: Giliran Player {drawIndex + 1} untuk Draw";
    }

    void UpdatePoolUI()
    {
        if (poolText != null) poolText.text = "Pool: " + string.Join(", ", dicePool);
    }

    void UpdateOrderStatusUI()
    {
        if (orderStatusText == null) return;
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
            Debug.LogError("Pool kosong!");
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
            if (infoText != null) infoText.text = $"Order Selection: Giliran Player {drawIndex + 1} untuk Draw";
        }
        else
        {
            FinalizeTurnOrder();
        }
    }

    void FinalizeTurnOrder()
    {
        turnOrder = drawnNumbers.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
        string orderStr = "Turn Order: " + string.Join(" > ", turnOrder.Select(p => p.name));
        if (infoText != null) infoText.text = orderStr;
        if (orderPanel != null) orderPanel.SetActive(false);
        if (uiManager != null)
        {
            uiManager.SetupPlayerList(turnOrder);
        }
        currentTurnIdx = 0;
        HighlightCurrentPlayer();
    }
    #endregion

    // (Fungsi Gameplay utama di-update di bawah)
    #region Gameplay Loop
    public void NotifyDiceThrown()
    {
        if (isActionRunning) return;
        StartCoroutine(WaitForDiceToSettleAndMove());
    }

    IEnumerator WaitForDiceToSettleAndMove()
    {
        isActionRunning = true;
        PlayerPawn current = turnOrder[currentTurnIdx];
        if (infoText != null) infoText.text = $"{current.name} melempar dadu...";
        int rollResult = 0;
        yield return StartCoroutine(physicalDice.WaitForRollToStop((result) => { rollResult = result; }));
        DisableDiceWall(); 
        yield return StartCoroutine(HandlePlayerRollAndMove(current, rollResult));
    }

    IEnumerator HandlePlayerRollAndMove(PlayerPawn player, int roll)
    {
        if (infoText != null) infoText.text = $"{player.name} roll {roll}";

        List<PlayerPawn> validTargets = GetValidReverseTargets(player);
        bool didReverse = false;

        if (validTargets.Count > 0 && currentCycle > 1)
        {
            // (Logika Reverse tidak berubah)
            #region Reverse Logic
            currentValidTargets = GetValidReverseTargets(player);
            currentActorForSelection = player;
            selectedTargetForReverse = null;
            awaitingTargetSelection = false;
            isInReverseMode = false;
            if (choicePanel != null) choicePanel.SetActive(true);
            foreach (var p in players) p.SetHighlight(currentValidTargets.Contains(p));
            if (btnMoveSelf != null) btnMoveSelf.gameObject.SetActive(true);
            if (btnReverse != null) btnReverse.gameObject.SetActive(true);
            if (btnCancelTargetSelect != null) btnCancelTargetSelect.gameObject.SetActive(false);
            if (choiceInstructionText != null) { choiceInstructionText.gameObject.SetActive(false); choiceInstructionText.text = ""; }
            btnMoveSelf.onClick.RemoveAllListeners();
            btnReverse.onClick.RemoveAllListeners();
            if (btnCancelTargetSelect != null) btnCancelTargetSelect.onClick.RemoveAllListeners();
            bool moveSelfChosenLocal = false;
            void OnBtnMoveSelfLocal() { moveSelfChosenLocal = true; Debug.Log("MoveSelf chosen local"); }
            void OnBtnReverseLocal() { EnterReverseSelectionUI(); awaitingTargetSelection = true; selectedTargetForReverse = null; Debug.Log($"Entered reverse mode for {player.name}. awaitingTargetSelection={awaitingTargetSelection}"); }
            void OnBtnCancelLocal() { awaitingTargetSelection = false; selectedTargetForReverse = null; ExitReverseSelectionUI(); Debug.Log("Reverse selection cancelled (local)"); }
            btnMoveSelf.onClick.AddListener(OnBtnMoveSelfLocal);
            btnReverse.onClick.AddListener(OnBtnReverseLocal);
            if (btnCancelTargetSelect != null) btnCancelTargetSelect.onClick.AddListener(OnBtnCancelLocal);

            while (true)
            {
                if (selectedTargetForReverse != null)
                {
                    PlayerPawn target = selectedTargetForReverse;
                    if (target == null || target.wasReversedThisCycle || target.currentTileID <= 1)
                    {
                        Debug.LogWarning("Target tidak valid saat eksekusi reverse. Fall back ke Move Self.");
                        btnMoveSelf.onClick.RemoveListener(OnBtnMoveSelfLocal);
                        btnReverse.onClick.RemoveListener(OnBtnReverseLocal);
                        if (btnCancelTargetSelect != null) btnCancelTargetSelect.onClick.RemoveListener(OnBtnCancelLocal);
                        CleanupChoiceUI();
                        ExitReverseSelectionUI();
                        break;
                    }
                    int targetStart = target.currentTileID;
                    int targetFinal = Mathf.Max(1, targetStart - roll);
                    yield return StartCoroutine(target.MoveToTile(targetFinal, (int id) => GetTilePositionWithOffset(id, target)));
                    Tiles landed = GetTileByID(target.currentTileID);
                    if (landed != null)
                    {
                        if (landed.type == TileType.LadderStart && landed.targetTile != null)
                        {
                            yield return new WaitForSeconds(0.1f);
                            int tid = landed.targetTile.tileID;
                            yield return StartCoroutine(target.TeleportToTile(tid, (int id) => GetTilePositionWithOffset(id, target)));
                        }
                        else if (landed.type == TileType.SnakeStart && landed.targetTile != null)
                        {
                            yield return new WaitForSeconds(0.1f);
                            int tid = landed.targetTile.tileID;
                            yield return StartCoroutine(target.TeleportToTile(tid, (int id) => GetTilePositionWithOffset(id, target)));
                        }
                    }
                    
                    // --- PERBAIKAN BUG STACKING (1) ---
                    UpdatePawnPositionsOnTile(target.currentTileID); // Update posisi di tile tujuan
                    // ---------------------------------
                    
                    target.wasReversedThisCycle = true;
                    target.ShowReversedBadge(true);
                    if (currentValidTargets != null && currentValidTargets.Contains(target))
                        currentValidTargets.Remove(target);
                    btnMoveSelf.onClick.RemoveListener(OnBtnMoveSelfLocal);
                    btnReverse.onClick.RemoveListener(OnBtnReverseLocal);
                    if (btnCancelTargetSelect != null) btnCancelTargetSelect.onClick.RemoveListener(OnBtnCancelLocal);
                    CleanupChoiceUI();
                    ExitReverseSelectionUI();
                    didReverse = true;
                    break;
                }
                if (moveSelfChosenLocal)
                {
                    btnMoveSelf.onClick.RemoveListener(OnBtnMoveSelfLocal);
                    btnReverse.onClick.RemoveListener(OnBtnReverseLocal);
                    if (btnCancelTargetSelect != null) btnCancelTargetSelect.onClick.RemoveListener(OnBtnCancelLocal);
                    CleanupChoiceUI();
                    ExitReverseSelectionUI();
                    break;
                }
                yield return null;
            }
            #endregion
        }

        if (!didReverse)
        {
            // (Logika Gerak Normal tidak berubah)
            #region Normal Move Logic
            int startTile = player.currentTileID;
            int finalTarget = startTile + roll;

            if (finalTarget == totalTilesInBoard)
            {
                yield return StartCoroutine(player.MoveToTile(finalTarget, (int id) => GetTilePositionWithOffset(id, player)));
                if (infoText != null) infoText.text = $"{player.name} mencapai finish!";
                isActionRunning = false;
                yield break;
            }
            else if (finalTarget > totalTilesInBoard)
            {
                int overshoot = finalTarget - totalTilesInBoard;
                int bounceTarget = totalTilesInBoard - overshoot;
                yield return StartCoroutine(player.MoveToTile(totalTilesInBoard, (int id) => GetTilePositionWithOffset(id, player)));
                yield return new WaitForSeconds(0.2f);
                yield return StartCoroutine(player.MoveToTile(bounceTarget, (int id) => GetTilePositionWithOffset(id, player)));
            }
            else
            {
                yield return StartCoroutine(player.MoveToTile(finalTarget, (int id) => GetTilePositionWithOffset(id, player)));
            }

            Tiles landed2 = GetTileByID(player.currentTileID);
            if (landed2 != null)
            {
                if (landed2.type == TileType.LadderStart && landed2.targetTile != null)
                {
                    if (infoText != null) infoText.text = $"{player.name} Naik tangga!";
                    yield return new WaitForSeconds(0.2f);
                    int targetID = landed2.targetTile.tileID;
                    yield return StartCoroutine(player.TeleportToTile(targetID, (int id) => GetTilePositionWithOffset(id, player)));
                }
                else if (landed2.type == TileType.SnakeStart && landed2.targetTile != null)
                {
                    if (infoText != null) infoText.text = $"{player.name} Turun ular!";
                    yield return new WaitForSeconds(0.2f);
                    int targetID = landed2.targetTile.tileID;
                    yield return StartCoroutine(player.TeleportToTile(targetID, (int id) => GetTilePositionWithOffset(id, player)));
                }
            }
            #endregion
            
            // --- PERBAIKAN BUG STACKING (2) ---
            UpdatePawnPositionsOnTile(player.currentTileID); // Update posisi di tile tujuan
            // ---------------------------------
        }

        currentTurnIdx = (currentTurnIdx + 1) % turnOrder.Count;

        if (currentTurnIdx == 0)
        {
            foreach (var p in players)
            {
                p.wasReversedThisCycle = false;
                p.ShowReversedBadge(false);
            }
            Debug.Log("Cycle selesai: reset wasReversedThisCycle dan badge.");
            currentCycle++; 
            Debug.Log($"Memasuki Cycle baru: {currentCycle}");
        }

        isActionRunning = false;
        HighlightCurrentPlayer();

        yield break;
    }

    void HighlightCurrentPlayer()
    {
        if (physicalDice != null) physicalDice.ResetDice();
        if (turnOrder.Count == 0) return;
        PlayerPawn cur = turnOrder[currentTurnIdx];
        if (infoText != null) infoText.text = $"Giliran: {cur.name}. Ambil & lempar dadunya!";
        for (int i = 0; i < players.Count; i++)
            players[i].SetHighlight(players[i] == cur);
        if (uiManager != null)
        {
            uiManager.UpdateActivePlayer(currentTurnIdx);
        }
    }
    #endregion
    
    // (Fungsi Helper Board & Tembok tidak berubah)
    #region Board, UI, & Dice Wall Helpers
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

    public Vector3 GetTilePositionWithOffset(int tileID, PlayerPawn pawn)
    {
        Vector3 center = GetTilePosition(tileID);
        var onTile = players.Where(p => p.currentTileID == tileID).OrderBy(p => p.playerIndex).ToList();
        
        // --- LOGIKA PENTING UNTUK PREDIKSI POSISI ---
        // Jika pion yang ditanya BELUM ada di tile (dia sedang bergerak ke sana),
        // kita tambahkan dia ke list SEMENTARA untuk menghitung posisi barunya.
        if (!onTile.Contains(pawn))
        {
            var tmp = onTile.ToList();
            tmp.Add(pawn);
            onTile = tmp.OrderBy(p => p.playerIndex).ToList();
        }
        // -------------------------------------------

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
    
    // --- FUNGSI BARU UNTUK FIX STACKING ---
    /// <summary>
    /// Memperbarui (nudge) posisi semua pion di tile tertentu
    /// </summary>
    void UpdatePawnPositionsOnTile(int tileID)
    {
        // Ambil semua pion yang ADA di tile itu SEKARANG
        var pawnsOnTile = players.Where(p => p.currentTileID == tileID).ToList();

        foreach (PlayerPawn pawn in pawnsOnTile)
        {
            // Minta posisi offset yang benar
            Vector3 newPos = GetTilePositionWithOffset(tileID, pawn);
            
            // Suruh pawn pindah ke posisi itu
            pawn.MoveToPosition(newPos);
        }
    }
    // --------------------------------------

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
    
    public void DisableDiceWall()
    {
        if (diceContainmentWall != null)
        {
            diceContainmentWall.SetActive(false);
        }
    }

    public void EnableDiceWall()
    {
        if (diceContainmentWall != null)
        {
            diceContainmentWall.SetActive(true);
        }
    }
    #endregion
    
    // (Fungsi Reverse Helper tidak berubah)
    #region Reverse Helpers
    public List<PlayerPawn> GetValidReverseTargets(PlayerPawn actor)
    {
        return players.Where(p => p != actor && p.currentTileID > 1 && !p.wasReversedThisCycle).ToList();
    }

    public void OnPawnClicked(PlayerPawn clickedPawn)
    {
        if (!isInReverseMode)
        {
            Debug.Log($"Pawn clicked ignored: isInReverseMode={isInReverseMode}");
            return;
        }
        if (currentActorForSelection != null && clickedPawn == currentActorForSelection)
        {
            Debug.Log("Pawn clicked is actor itself - ignored");
            return;
        }
        if (currentValidTargets == null || !currentValidTargets.Contains(clickedPawn))
        {
            Debug.Log($"Pawn clicked not in valid targets: {clickedPawn.name}");
            return;
        }
        selectedTargetForReverse = clickedPawn;
        currentValidTargets.Remove(clickedPawn);
        Debug.Log($"Pawn clicked accepted: {clickedPawn.name} selected as reverse target");
    }

    private void CleanupChoiceUI()
    {
        if (choicePanel != null) choicePanel.SetActive(false);
        foreach (var p in players) p.SetHighlight(false);
        awaitingTargetSelection = false;
        selectedTargetForReverse = null;
        currentActorForSelection = null;
        currentValidTargets = new List<PlayerPawn>();
        btnMoveSelf.onClick.RemoveAllListeners();
        btnReverse.onClick.RemoveAllListeners();
        if (btnCancelTargetSelect != null) btnCancelTargetSelect.onClick.RemoveAllListeners();
        btnMoveSelf.onClick.AddListener(OnChoice_MoveSelf);
        btnReverse.onClick.AddListener(OnChoice_Reverse);
        if (btnCancelTargetSelect != null) btnCancelTargetSelect.onClick.AddListener(OnChoice_Cancel);
        if (choiceInstructionText != null) { choiceInstructionText.gameObject.SetActive(false); choiceInstructionText.text = ""; }
    }

    void EnterReverseSelectionUI()
    {
        isInReverseMode = true;
        if (btnMoveSelf != null) btnMoveSelf.gameObject.SetActive(false);
        if (btnReverse != null) btnReverse.gameObject.SetActive(false);
        if (btnCancelTargetSelect != null) btnCancelTargetSelect.gameObject.SetActive(true);
        if (choiceInstructionText != null)
        {
            choiceInstructionText.gameObject.SetActive(true);
            choiceInstructionText.text = "Choose Enemy To Reverse";
        }
        if (infoText != null) infoText.text = "Pilih lawan untuk dimundurkan (klik pawn) atau Cancel.";
    }

    void ExitReverseSelectionUI()
    {
        isInReverseMode = false;
        if (btnMoveSelf != null) btnMoveSelf.gameObject.SetActive(true);
        if (btnReverse != null) btnReverse.gameObject.SetActive(true);
        if (btnCancelTargetSelect != null) btnCancelTargetSelect.gameObject.SetActive(false);
        if (choiceInstructionText != null)
        {
            choiceInstructionText.gameObject.SetActive(false);
            choiceInstructionText.text = "";
        }
        if (infoText != null && turnOrder.Count > 0)
        {
            PlayerPawn cur = turnOrder[currentTurnIdx];
            infoText.text = $"Giliran: {cur.name}. Ambil & lempar dadunya!";
        }
    }

    void OnChoice_MoveSelf()
    {
        awaitingTargetSelection = false;
        selectedTargetForReverse = null;
    }
    void OnChoice_Reverse()
    {
        isInReverseMode = true;
        awaitingTargetSelection = true;
    }
    void OnChoice_Cancel()
    {
        awaitingTargetSelection = false;
        selectedTargetForReverse = null;
        ExitReverseSelectionUI();
    }
    #endregion
}