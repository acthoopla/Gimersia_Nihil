using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MultiplayerManager : MonoBehaviour
{
    // Bagian 'Instance' (Singleton) ini diambil dari kode temanmu.
    public static MultiplayerManager Instance; 

    #region Variabel Inspector
    [Header("Prefabs & References")]
    // DIUBAH: Menggunakan array prefab dari KODEMU
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
    // DIUBAH: Menggunakan referensi UIManager dari KODEMU.
    // Script UIManager ini sekarang harus menghandle Turn List dan UI Kartu
    public UIManager uiManager; 

    [Header("UI - Reverse Choice")]
    public GameObject choicePanel;
    public Button btnMoveSelf;
    public Button btnReverse;
    public Button btnCancelTargetSelect;
    public TextMeshProUGUI choiceInstructionText;
    
    // BARU: Diambil dari kode temanmu
    [Header("Card System References")]
    public CardManager cardManager;

    [Header("Dice Physics")]
    public Dice physicalDice;
    // BARU: Diambil dari KODEMU
    public GameObject diceContainmentWall; 

    [Header("Board Settings")]
    public int totalTilesInBoard = 100;

    [Header("Tile Offset")]
    public float tileOffsetBaseRadius = 0.25f;
    public float tileOffsetPerPlayer = 0.18f;
    public float tileOffsetHeightStep = 0.02f;
    #endregion

    #region Variabel Internal
    // Variabel internal ini adalah gabungan dari kodemu dan kode temanmu
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
    
    // BARU: Diambil dari kode temanmu
    private PlayerPawn playerWaitingForCard;
    #endregion

    #region Unity Callbacks
    void Awake()
    {
        // Menggunakan Singleton dari kode temanmu
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        // Menggunakan listener dari kodemu (sudah lengkap)
        btn2Players.onClick.AddListener(() => OnChoosePlayerCount(2));
        btn3Players.onClick.AddListener(() => OnChoosePlayerCount(3));
        btn4Players.onClick.AddListener(() => OnChoosePlayerCount(4));
        drawOrderButton.onClick.AddListener(OnDrawOrderPressed);
        btnMoveSelf.onClick.AddListener(OnChoice_MoveSelf);
        btnReverse.onClick.AddListener(OnChoice_Reverse);
        if (btnCancelTargetSelect != null) btnCancelTargetSelect.onClick.AddListener(OnChoice_Cancel);

        // UI init (gabungan)
        if (orderPanel != null) orderPanel.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(false);
        if (choiceInstructionText != null) choiceInstructionText.gameObject.SetActive(false);
        // Tombol Roll dihapus (sesuai kodemu)
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

        // BARU: Diambil dari kode temanmu (memakai var `uiManager` kodemu)
        if (uiManager != null)
            uiManager.UpdateCycle(currentCycle);
    }
    #endregion

    #region Player Spawn & Order Selection
    void OnChoosePlayerCount(int count)
    {
        if (isSpawning) return;
        isSpawning = true;
        if (playerSelectPanel != null) playerSelectPanel.SetActive(false);
        StartCoroutine(ClearAndSpawnRoutine(count));
    }

    // DIUBAH: Menggunakan `SpawnPlayers` dari KODEMU (array prefabs)
    // dan ditambahkan logika kartu dari temanmu
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
            // Logika dari KODEMU
            GameObject prefabToSpawn = playerPrefabs[i]; 
            GameObject go = Instantiate(prefabToSpawn, playersParent);
            go.name = $"Player_{i + 1}";
            PlayerPawn pp = go.GetComponent<PlayerPawn>();
            if (pp == null) pp = go.AddComponent<PlayerPawn>();
            
            // Data dari kodemu
            pp.playerIndex = i;
            pp.currentTileID = 1;
            pp.SetVisualIndex(i);
            pp.SetManager(this); // <-- PENTING! Dari kodemu

            // BARU: Data dari kode temanmu
            pp.heldCards = new List<PlayerCardInstance>(); 
            
            // Logika dari kodemu
            Vector3 posWithOffset = GetTilePositionWithOffset(1, pp);
            go.transform.position = posWithOffset;
            players.Add(pp);
        }
        Debug.Log($"SpawnPlayers: spawned {players.Count} players");
    }

    // DIUBAH: Menggunakan `ClearAndSpawnRoutine` gabungan
    IEnumerator ClearAndSpawnRoutine(int count)
    {
        if (playersParent != null)
        {
            for (int i = playersParent.childCount - 1; i >= 0; i--)
            {
                Destroy(playersParent.GetChild(i).gameObject);
            }
            yield return null;
        }
        players.Clear(); drawnNumbers.Clear(); turnOrder.Clear();
        drawIndex = 0; currentTurnIdx = 0; currentCycle = 1; // Reset cycle dari temanmu
        selectedPlayerCount = count;
        SpawnPlayers(count);
        StartOrderSelection();
        isSpawning = false;
    }

    void StartOrderSelection()
    {
        dicePool = new List<int> { 1, 2, 3, 4, 5, 6 };
        drawnNumbers.Clear(); drawIndex = 0;
        if (orderPanel != null) orderPanel.SetActive(true);
        UpdatePoolUI(); UpdateOrderStatusUI();
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
        if (drawIndex >= players.Count || dicePool.Count == 0) return;
        int idx = Random.Range(0, dicePool.Count);
        int val = dicePool[idx];
        dicePool.RemoveAt(idx);
        PlayerPawn p = players[drawIndex];
        drawnNumbers[p] = val;
        UpdatePoolUI(); UpdateOrderStatusUI();
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

    // DIUBAH: `FinalizeTurnOrder` digabung
    void FinalizeTurnOrder()
    {
        turnOrder = drawnNumbers.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
        string orderStr = "Turn Order: " + string.Join(" > ", turnOrder.Select(p => p.name));
        if (infoText != null) infoText.text = orderStr;
        if (orderPanel != null) orderPanel.SetActive(false);
        
        if (uiManager != null)
        {
            // Panggilan dari KODEMU
            uiManager.SetupPlayerList(turnOrder);
            // Panggilan dari kode temanmu
            uiManager.UpdateCycle(currentCycle);
        }

        currentTurnIdx = 0;
        HighlightCurrentPlayer();
    }
    #endregion

    #region Gameplay Loop
    // DIUBAH: `WaitForDiceToSettleAndMove` adalah basis dari temanmu
    // Ditambah `DisableDiceWall` dari kodemu
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
        
        // Panggil fungsi `DisableDiceWall` dari KODEMU di sini
        // Kita panggil di sini agar tembok mati SEBELUM dadu dilempar
        // (Perbaikan dari logika sebelumnya, ini lebih baik)
        // physicalDice.ResetDice() akan memanggil ini
        
        int rollResult = 0;
        yield return StartCoroutine(physicalDice.WaitForRollToStop((result) => { rollResult = result; }));

        // Panggil `DisableDiceWall` dari KODEMU
        DisableDiceWall(); // Matikan tembok setelah dadu berhenti

        // --- LOGIKA KARTU TEMANMU (Start) ---
        if (current.nextRollModifier != 0) // Hermes Favors
        {
            if (infoText != null) infoText.text = $"Roll {rollResult} + Buff Hermes {current.nextRollModifier}!";
            rollResult += current.nextRollModifier;
            current.nextRollModifier = 0; 
            yield return new WaitForSeconds(1f);
        }

        if (current.hasAresProvocation) // Ares Provocation
        {
            rollResult -= 1; // -1 untuk diri sendiri
            if (infoText != null) infoText.text = $"Roll {rollResult + 1} - Debuff Ares 1 = {rollResult}!";
            yield return new WaitForSeconds(1f);
        }
        rollResult = Mathf.Max(1, rollResult);

        int totalRolls = 1 + current.extraDiceRolls;
        current.extraDiceRolls = 0; 
        // --- LOGIKA KARTU TEMANMU (End) ---

        for (int i = 0; i < totalRolls; i++)
        {
            if (i > 0)
            {
                if (infoText != null) infoText.text = $"{current.name} melempar Dadu Ekstra (Odin)!";
                yield return new WaitForSeconds(1f);
                yield return StartCoroutine(physicalDice.WaitForRollToStop((result) => { rollResult = result; }));
                DisableDiceWall(); // Matikan tembok lagi jika dadu dilempar ulang
            }
            isActionRunning = true; // Set lagi karena HandlePlayerRollAndMove akan set ke false
            yield return StartCoroutine(HandlePlayerRollAndMove(current, rollResult));
            
            if (current.currentTileID == totalTilesInBoard) // Jika menang, hentikan multi-roll
                break;
        }
        
        // Panggil `AdvanceTurn` (logika baru dari temanmu)
        AdvanceTurn();
    }

    // DIUBAH: `HandlePlayerRollAndMove` adalah basis temanmu
    // Ditambah aturan `currentCycle` dan fix tumpuk `UpdatePawnPositionsOnTile` dari KODEMU
    IEnumerator HandlePlayerRollAndMove(PlayerPawn player, int roll)
    {
        if (infoText != null) infoText.text = $"{player.name} roll {roll}";
        List<PlayerPawn> validTargets = GetValidReverseTargets(player);
        bool didReverse = false;

        // DIUBAH: Menambahkan aturan Cycle 1 dari KODEMU
        if (validTargets.Count > 0 && currentCycle > 1)
        {
            #region Reverse Logic (Gabungan)
            currentValidTargets = validTargets;
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
            void OnBtnMoveSelfLocal() { moveSelfChosenLocal = true; }
            void OnBtnReverseLocal() { EnterReverseSelectionUI(); awaitingTargetSelection = true; selectedTargetForReverse = null; }
            void OnBtnCancelLocal() { awaitingTargetSelection = false; selectedTargetForReverse = null; ExitReverseSelectionUI(); }
            
            btnMoveSelf.onClick.AddListener(OnBtnMoveSelfLocal);
            btnReverse.onClick.AddListener(OnBtnReverseLocal);
            if (btnCancelTargetSelect != null) btnCancelTargetSelect.onClick.AddListener(OnBtnCancelLocal);

            while (true)
            {
                if (selectedTargetForReverse != null)
                {
                    PlayerPawn target = selectedTargetForReverse;
                    btnMoveSelf.onClick.RemoveListener(OnBtnMoveSelfLocal);
                    btnReverse.onClick.RemoveListener(OnBtnReverseLocal);
                    if (btnCancelTargetSelect != null) btnCancelTargetSelect.onClick.RemoveListener(OnBtnCancelLocal);
                    CleanupChoiceUI();
                    ExitReverseSelectionUI();

                    // Logika Ares Provocation dari temanmu
                    int finalRoll = roll;
                    if (player.hasAresProvocation)
                    {
                        finalRoll += 2; 
                        if (infoText != null) infoText.text = $"Ares Provocation! Mundur {finalRoll}!";
                        yield return new WaitForSeconds(1f);
                    }
                    
                    int targetStart = target.currentTileID;
                    int targetFinal = Mathf.Max(1, targetStart - finalRoll);
                    yield return StartCoroutine(target.MoveToTile(targetFinal, (int id) => GetTilePositionWithOffset(id, target)));
                    
                    // Panggil helper `CheckLandingTile` dari temanmu
                    yield return StartCoroutine(CheckLandingTile(target));
                    
                    // Panggil fix tumpuk dari KODEMU
                    UpdatePawnPositionsOnTile(target.currentTileID);
                    
                    target.wasReversedThisCycle = true;
                    target.ShowReversedBadge(true);
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
            #region Normal Move Logic
            int startTile = player.currentTileID;
            int finalTarget = startTile + roll;
            if (finalTarget == totalTilesInBoard)
            {
                yield return StartCoroutine(player.MoveToTile(finalTarget, (int id) => GetTilePositionWithOffset(id, player)));
                if (infoText != null) infoText.text = $"{player.name} mencapai finish!";
                // Jangan panggil AdvanceTurn di sini
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
            
            // Panggil helper `CheckLandingTile` dari temanmu
            yield return StartCoroutine(CheckLandingTile(player));
            
            // Panggil fix tumpuk dari KODEMU
            UpdatePawnPositionsOnTile(player.currentTileID);
            #endregion
        }
        
        isActionRunning = false; // Selesai
        yield break; // Kembalikan kontrol ke WaitForDiceToSettleAndMove
    }

    // BARU: Fungsi `CheckLandingTile` dari kode temanmu
    IEnumerator CheckLandingTile(PlayerPawn player)
    {
        Tiles landed = GetTileByID(player.currentTileID);
        if (landed == null) yield break;

        // Cek Ular (dengan logika Shield of Athena)
        if (landed.type == TileType.SnakeStart && landed.targetTile != null)
        {
            if (player.immuneToSnakeUses > 0)
            {
                player.immuneToSnakeUses--; 
                if (infoText != null) infoText.text = $"{player.name} kebal dari ular!";
                yield return new WaitForSeconds(1f);
            }
            else
            {
                if (infoText != null) infoText.text = $"{player.name} Turun ular!";
                yield return new WaitForSeconds(0.2f);
                int targetID = landed.targetTile.tileID;
                yield return StartCoroutine(player.TeleportToTile(targetID, (int id) => GetTilePositionWithOffset(id, player)));
                
                // Panggil fix tumpuk dari KODEMU setelah teleport
                UpdatePawnPositionsOnTile(player.currentTileID);
            }
        }
        // Cek Tangga
        else if (landed.type == TileType.LadderStart && landed.targetTile != null)
        {
            if (infoText != null) infoText.text = $"{player.name} Naik tangga!";
            yield return new WaitForSeconds(0.2f);
            int targetID = landed.targetTile.tileID;
            yield return StartCoroutine(player.TeleportToTile(targetID, (int id) => GetTilePositionWithOffset(id, player)));
            
            // Panggil fix tumpuk dari KODEMU setelah teleport
            UpdatePawnPositionsOnTile(player.currentTileID);
        }
        // Cek Kartu Blessing
        else if (landed.type == TileType.BlessingCard)
        {
            if (infoText != null) infoText.text = $"{player.name} mendarat di petak Blessing!";
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(ShowCardChoiceRoutine(player));
        }
    }

    // BARU: Fungsi `AdvanceTurn` dari kode temanmu
    // DIUBAH: Ditambah logika 'currentCycle' dari kodemu
    void AdvanceTurn()
    {
        currentTurnIdx = (currentTurnIdx + 1) % turnOrder.Count;

        if (currentTurnIdx == 0) // Cycle baru dimulai
        {
            currentCycle++; // <-- Logika Cycle dari KODEMU
            Debug.Log($"--- CYCLE BARU DIMULAI: {currentCycle} ---");
            if (uiManager != null) uiManager.UpdateCycle(currentCycle);

            foreach (var p in players)
            {
                p.wasReversedThisCycle = false;
                p.ShowReversedBadge(false);
                p.hasAresProvocation = false;
                if (p.immuneToReverseCycles > 0)
                    p.immuneToReverseCycles--;
                CheckForExpiredCards(p);
            }
        }

        PlayerPawn nextPlayer = turnOrder[currentTurnIdx];
        if (nextPlayer.skipTurns > 0)
        {
            if (infoText != null) infoText.text = $"{nextPlayer.name} skip giliran karena efek Anubis!";
            nextPlayer.skipTurns--;
            AdvanceTurn(); // Langsung ganti giliran lagi
            return;
        }

        isActionRunning = false;
        HighlightCurrentPlayer();
    }

    // BARU: Fungsi `CheckForExpiredCards` dari kode temanmu
    void CheckForExpiredCards(PlayerPawn player)
    {
        List<PlayerCardInstance> expiredCards = player.heldCards
            .Where(card => (currentCycle - card.cycleAcquired) >= 3)
            .ToList();

        if (expiredCards.Count > 0)
        {
            Debug.Log($"{player.name} kehilangan {expiredCards.Count} kartu kadaluarsa.");
            if (infoText != null) infoText.text = $"{player.name} kehilangan {expiredCards.Count} kartu!";
            foreach (var expired in expiredCards)
            {
                player.heldCards.Remove(expired);
            }
            if (player == GetCurrentPlayer() && uiManager != null)
            {
                uiManager.DisplayPlayerHand(player);
            }
        }
    }

    // DIUBAH: `HighlightCurrentPlayer` gabungan
    void HighlightCurrentPlayer()
    {
        // Panggilan ResetDice dari KODEMU
        if (physicalDice != null) physicalDice.ResetDice();
        
        if (turnOrder.Count == 0) return;
        PlayerPawn cur = turnOrder[currentTurnIdx];
        if (infoText != null) infoText.text = $"Giliran: {cur.name}. Ambil & lempar dadunya!";

        for (int i = 0; i < players.Count; i++)
            players[i].SetHighlight(players[i] == cur);

        if (uiManager != null)
        {
            // Panggilan dari KODEMU
            uiManager.UpdateActivePlayer(currentTurnIdx);
            // Panggilan dari kode temanmu
            uiManager.DisplayPlayerHand(cur);
        }
    }
    #endregion

    #region Board, UI, & Dice Wall Helpers
    Vector3 GetTilePosition(int tileID) 
    { 
        Tiles t = GetTileByID(tileID);
        if (t != null) return t.GetPlayerPosition();
        return Vector3.zero;
    }

    Tiles GetTileByID(int id) 
    { 
        return boardTiles.FirstOrDefault(x => x.tileID == id); 
    }

    // DIUBAH: Menggunakan `GetTilePositionWithOffset` dari KODEMU
    // (Karena sudah berisi logika prediksi yang lebih baik)
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
    
    // BARU: Fungsi `UpdatePawnPositionsOnTile` dari KODEMU
    void UpdatePawnPositionsOnTile(int tileID)
    {
        var pawnsOnTile = players.Where(p => p.currentTileID == tileID).ToList();
        foreach (PlayerPawn pawn in pawnsOnTile)
        {
            Vector3 newPos = GetTilePositionWithOffset(tileID, pawn);
            pawn.MoveToPosition(newPos);
        }
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
    
    // BARU: Fungsi Tembok Dadu dari KODEMU
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

    #region Reverse Helpers (Gabungan)
    // DIUBAH: Menggunakan `GetValidReverseTargets` dari kode temanmu
    // (Karena sudah ada cek imunitas)
    public List<PlayerPawn> GetValidReverseTargets(PlayerPawn actor)
    {
        return players.Where(p =>
            p != actor &&
            p.currentTileID > 1 &&
            !p.wasReversedThisCycle &&
            p.immuneToReverseCycles <= 0 // Cek kebal reverse
        ).ToList();
    }

    // DIUBAH: Menggunakan `OnPawnClicked` dari kode temanmu
    // (Lebih aman, mengecek `awaitingTargetSelection`)
    public void OnPawnClicked(PlayerPawn clickedPawn)
    {
        // (Logika ini juga dipakai untuk kartu)
        if (!isInReverseMode || !awaitingTargetSelection)
        {
            Debug.Log($"Pawn clicked ignored: isInReverseMode={isInReverseMode}, awaiting={awaitingTargetSelection}");
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
        selectedTargetForReverse = clickedPawn; // Kunci targetnya!
        awaitingTargetSelection = false; // Berhenti menunggu
        Debug.Log($"Pawn clicked accepted: {clickedPawn.name}");
    }

    // (Semua fungsi UI Reverse (Cleanup, Enter, Exit, OnChoice) diambil dari temanmu
    // karena kodemu identik/lebih simpel)
    private void CleanupChoiceUI()
    {
        if (choicePanel != null) choicePanel.SetActive(false);
        foreach (var p in players) p.SetHighlight(false);
        awaitingTargetSelection = false;
        selectedTargetForReverse = null;
        currentActorForSelection = null;
        currentValidTargets.Clear();
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
            choiceInstructionText.text = "Pilih Target"; // Teks temanmu
        }
        if (infoText != null) infoText.text = "Pilih target (klik pawn) atau Cancel."; // Teks temanmu
    }

    void ExitReverseSelectionUI()
    {
        isInReverseMode = false;
        if (btnMoveSelf != null) btnMoveSelf.gameObject.SetActive(true);
        if (btnReverse != null) btnReverse.gameObject.SetActive(true);
        if (btnCancelTargetSelect != null) btnCancelTargetSelect.gameObject.SetActive(false);
        if (choiceInstructionText != null) { choiceInstructionText.gameObject.SetActive(false); choiceInstructionText.text = ""; }
        if (turnOrder.Count > 0 && currentTurnIdx < turnOrder.Count)
            HighlightCurrentPlayer();
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
    
    #region Card System Functions
    // --- SEMUA FUNGSI SISTEM KARTU DARI TEMANMU DIAMBIL ---
    
    public bool IsPlayerTurn(PlayerPawn player) { return (turnOrder.Count > 0 && turnOrder[currentTurnIdx] == player); }
    public PlayerPawn GetCurrentPlayer() { return (turnOrder.Count > 0) ? turnOrder[currentTurnIdx] : null; }

    private IEnumerator ShowCardChoiceRoutine(PlayerPawn player)
    {
        isActionRunning = true;
        playerWaitingForCard = player;
        if (infoText != null) infoText.text = $"{player.name} sedang memilih Blessing...";

        List<CardData> cardSelection = cardManager.GetRandomCardSelection(3);
        // Memakai `uiManager` gabungan
        uiManager.StartCardSelection(cardSelection); 

        while (uiManager.cardChoicePanel.activeSelf)
        {
            yield return null;
        }

        if (infoText != null) infoText.text = $"Giliran {player.name} berlanjut.";
        playerWaitingForCard = null;
        isActionRunning = false;
    }

    public void AddChosenCardToPlayer(CardData chosenCard)
    {
        if (playerWaitingForCard != null)
        {
            playerWaitingForCard.heldCards.Add(new PlayerCardInstance(chosenCard, currentCycle));
            if (uiManager != null)
            {
                uiManager.DisplayPlayerHand(playerWaitingForCard);
            }
        }
    }

    public void UseCard(CardData card)
    {
        if (isActionRunning)
        {
            infoText.text = "Tidak bisa menggunakan kartu saat aksi berjalan.";
            return;
        }

        PlayerPawn user = turnOrder[currentTurnIdx];
        PlayerCardInstance cardInstance = user.heldCards.FirstOrDefault(c => c.cardData == card);
        if (cardInstance != null)
        {
            user.heldCards.Remove(cardInstance);
        }
        else
        {
            Debug.LogWarning($"Pemain {user.name} mencoba menggunakan {card.name} tapi tidak ditemukan!");
            return;
        }

        if (uiManager != null)
        {
            uiManager.DisplayPlayerHand(user);
        }

        Debug.Log(user.name + " menggunakan kartu: " + card.cardName);

        switch (card.effectType)
        {
            case CardEffectType.AthenaBlessing:
                user.immuneToReverseCycles = card.intValue;
                infoText.text = $"{user.name} kebal 'reverse' selama {card.intValue} cycle!";
                break;
            case CardEffectType.ShieldOfAthena:
                user.immuneToSnakeUses = card.intValue;
                infoText.text = $"{user.name} kebal 'snake' untuk {card.intValue} kali!";
                break;
            case CardEffectType.HermesFavors:
                user.nextRollModifier += card.intValue;
                infoText.text = $"{user.name} mendapat +{card.intValue} di roll berikutnya!";
                break;
            case CardEffectType.PoseidonWaves:
                StartCoroutine(Effect_PoseidonWavesRoutine(user, card.intValue));
                break;
            case CardEffectType.ZeusWrath:
                StartCoroutine(Effect_TargetedMoveRoutine(user, card.intValue, "ZeusWrath"));
                break;
            case CardEffectType.AresProvocation:
                user.hasAresProvocation = true;
                infoText.text = $"{user.name} kini memiliki Ares Provocation!";
                break;
            case CardEffectType.OdinWisdom:
                user.extraDiceRolls += card.intValue;
                infoText.text = $"{user.name} mendapat {card.intValue} lempar dadu tambahan!";
                break;
            case CardEffectType.ThorHammer:
                StartCoroutine(Effect_ThorHammerRoutine(user, card.intValue));
                break;
            case CardEffectType.LokiTricks:
                StartCoroutine(Effect_TargetedMoveRoutine(user, 0, "LokiTricks"));
                break;
            case CardEffectType.RaLight:
                StartCoroutine(Effect_MovePlayer(user, card.intValue, "RaLight"));
                break;
            case CardEffectType.AnubisJudgment:
                StartCoroutine(Effect_TargetedStatusRoutine(user, card.intValue, "AnubisJudgment"));
                break;
        }
    }

    // --- Coroutine Helper untuk Efek Kartu ---
    private IEnumerator Effect_MovePlayer(PlayerPawn player, int moveAmount, string effectName)
    {
        isActionRunning = true;
        int targetTile = Mathf.Min(totalTilesInBoard, Mathf.Max(1, player.currentTileID + moveAmount));
        infoText.text = $"{player.name} menggunakan {effectName}, maju ke petak {targetTile}";
        yield return StartCoroutine(player.MoveToTile(targetTile, (int id) => GetTilePositionWithOffset(id, player)));
        yield return StartCoroutine(CheckLandingTile(player));
        
        // Panggil fix tumpuk dari KODEMU
        UpdatePawnPositionsOnTile(player.currentTileID);
        
        isActionRunning = false;
    }

    private IEnumerator Effect_TargetedMoveRoutine(PlayerPawn user, int moveAmount, string effectName)
    {
        isActionRunning = true;
        infoText.text = $"{user.name} menggunakan {effectName}! Pilih target.";
        List<PlayerPawn> validTargets = players.Where(p => p != user).ToList();
        if (effectName == "ZeusWrath")
        {
            validTargets = players.Where(p => p != user && p.immuneToReverseCycles <= 0).ToList();
        }
        if (validTargets.Count == 0) { infoText.text = "Tidak ada target!"; isActionRunning = false; yield break; }
        currentValidTargets = validTargets; currentActorForSelection = user; selectedTargetForReverse = null;
        awaitingTargetSelection = true; isInReverseMode = true; EnterReverseSelectionUI();
        while (selectedTargetForReverse == null && isInReverseMode) { yield return null; }
        if (!isInReverseMode || selectedTargetForReverse == null)
        {
            infoText.text = "Penggunaan kartu dibatalkan."; CleanupChoiceUI(); isActionRunning = false; yield break;
        }
        PlayerPawn target = selectedTargetForReverse; CleanupChoiceUI(); ExitReverseSelectionUI();

        if (effectName == "ZeusWrath")
        {
            infoText.text = $"{target.name} terkena Zeus Wrath! Mundur {moveAmount} petak!";
            int targetTile = Mathf.Max(1, target.currentTileID - moveAmount);
            yield return StartCoroutine(target.MoveToTile(targetTile, (int id) => GetTilePositionWithOffset(id, target)));
            yield return StartCoroutine(CheckLandingTile(target));
            
            // Panggil fix tumpuk dari KODEMU
            UpdatePawnPositionsOnTile(target.currentTileID);
        }
        else if (effectName == "LokiTricks")
        {
            infoText.text = $"{user.name} dan {target.name} bertukar posisi!";
            int userTile = user.currentTileID; int targetTile = target.currentTileID;
            yield return StartCoroutine(user.TeleportToTile(targetTile, (int id) => GetTilePositionWithOffset(id, user)));
            yield return StartCoroutine(target.TeleportToTile(userTile, (int id) => GetTilePositionWithOffset(id, target)));
            
            // Panggil fix tumpuk dari KODEMU (untuk kedua pemain)
            UpdatePawnPositionsOnTile(user.currentTileID);
            UpdatePawnPositionsOnTile(target.currentTileID);
        }
        isActionRunning = false;
    }

    private IEnumerator Effect_TargetedStatusRoutine(PlayerPawn user, int statusValue, string effectName)
    {
        isActionRunning = true;
        infoText.text = $"{user.name} menggunakan {effectName}! Pilih target.";
        List<PlayerPawn> validTargets = players.Where(p => p != user).ToList();
        if (validTargets.Count == 0) { infoText.text = "Tidak ada target!"; isActionRunning = false; yield break; }
        currentValidTargets = validTargets; currentActorForSelection = user; selectedTargetForReverse = null;
        awaitingTargetSelection = true; isInReverseMode = true; EnterReverseSelectionUI();
        while (selectedTargetForReverse == null && isInReverseMode) { yield return null; }
        if (!isInReverseMode || selectedTargetForReverse == null)
        {
            infoText.text = "Penggunaan kartu dibatalkan."; CleanupChoiceUI(); isActionRunning = false; yield break;
        }
        PlayerPawn target = selectedTargetForReverse; CleanupChoiceUI(); ExitReverseSelectionUI();
        if (effectName == "AnubisJudgment")
        {
            target.skipTurns += statusValue;
            infoText.text = $"{target.name} akan skip {statusValue} giliran!";
        }
        isActionRunning = false;
    }
    
    private IEnumerator Effect_ThorHammerRoutine(PlayerPawn user, int moveAmount)
    {
        isActionRunning = true;
        infoText.text = "Mencari target Thor Hammer...";
        yield return new WaitForSeconds(0.5f);
        PlayerPawn target = null;
        int minDistance = int.MaxValue;
        foreach (PlayerPawn p in players)
        {
            if (p == user) continue;
            int distance = p.currentTileID - user.currentTileID;
            if (distance > 0 && distance < minDistance)
            {
                minDistance = distance;
                target = p;
            }
        }
        if (target != null && target.immuneToReverseCycles <= 0)
        {
            infoText.text = $"{target.name} terkena Thor Hammer! Mundur {moveAmount} petak!";
            int targetTile = Mathf.Max(1, target.currentTileID - moveAmount);
            yield return StartCoroutine(target.MoveToTile(targetTile, (int id) => GetTilePositionWithOffset(id, target)));
            yield return StartCoroutine(CheckLandingTile(target));
            
            // Panggil fix tumpuk dari KODEMU
            UpdatePawnPositionsOnTile(target.currentTileID);
        }
        else if (target != null && target.immuneToReverseCycles > 0)
        {
            infoText.text = $"{target.name} kebal dari Thor Hammer!";
        }
        else
        {
            infoText.text = "Tidak ada pemain di depanmu!";
        }
        isActionRunning = false;
    }
    
    private IEnumerator Effect_PoseidonWavesRoutine(PlayerPawn user, int moveAmount)
    {
        isActionRunning = true;
        infoText.text = "Poseidon Waves belum diimplementasi.";
        // ... (Logika placeholder temanmu)
        yield return new WaitForSeconds(1f);
        isActionRunning = false;
    }
    #endregion
}