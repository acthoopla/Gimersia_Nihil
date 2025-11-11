using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text; // <-- PENTING: Tambahkan ini untuk StringBuilder

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance;

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
    public UIManager uiManager;

    [Header("UI - Reverse Choice")]
    public GameObject choicePanel;
    public Button btnMoveSelf;
    public Button btnReverse;
    public Button btnCancelTargetSelect;
    public TextMeshProUGUI choiceInstructionText;

    [Header("Card System References")]
    public CardManager cardManager;

    [Header("Dice Physics")]
    public Dice physicalDice;
    public GameObject diceContainmentWall;

    [Header("Game Animations")]
    public float snakeAnimationHeight = -2.0f;
    public float snakeAnimationSpeed = 3.0f;
    [Space(10)]
    public GameObject ladderStepPrefab;
    public float ladderDeployHeight = 10f;
    public float ladderDeploySpeed = 15f;
    public float ladderStepDelay = 0.05f;
    public float ladderVerticalOffset = 0.1f;

    [Header("Board Settings")]
    public int totalTilesInBoard = 100;

    [Header("Tile Offset")]
    public float tileOffsetBaseRadius = 0.25f;
    public float tileOffsetPerPlayer = 0.18f;
    public float tileOffsetHeightStep = 0.02f;
    #endregion

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
    private PlayerPawn playerWaitingForCard;
    private List<PlayerPawn> winners = new List<PlayerPawn>();
    #endregion

    #region Unity Callbacks & Setup
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

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
        if (uiManager != null) uiManager.SetTurnText("Pilih jumlah pemain");
        if (orderStatusText != null) orderStatusText.text = "";
    }

    void Start()
    {
        Tiles[] all = FindObjectsOfType<Tiles>();
        boardTiles = all.OrderBy(t => t.tileID).ToList();

        if (boardTiles.Count == 0)
        {
            Debug.LogError("Tidak menemukan Tiles di scene! Pastikan Tiles terpasang.");
            if (uiManager != null) uiManager.SetTurnText("Error: Board tidak ditemukan");
        }

        if (uiManager != null)
            uiManager.UpdateCycle(currentCycle);
    }

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
            go.name = $"Player {i + 1}";

            PlayerPawn pp = go.GetComponent<PlayerPawn>();
            if (pp == null) pp = go.AddComponent<PlayerPawn>();

            pp.playerIndex = i;
            pp.currentTileID = 1;
            pp.SetVisualIndex(i);
            pp.SetManager(this);
            pp.heldCards = new List<PlayerCardInstance>();

            Vector3 posWithOffset = GetTilePositionWithOffset(1, pp);
            go.transform.position = posWithOffset;
            players.Add(pp);
        }
    }

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
        players.Clear();
        drawnNumbers.Clear();
        turnOrder.Clear();
        winners.Clear();
        drawIndex = 0;
        currentTurnIdx = 0;
        currentCycle = 1;
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
        if (drawOrderButton != null) drawOrderButton.interactable = true; // Pastikan tombol aktif
        UpdatePoolUI(); UpdateOrderStatusUI();
        if (uiManager != null) uiManager.SetTurnText($"Order Selection: Giliran Player {drawIndex + 1} untuk Draw");
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

    // --- FUNGSI INI DIUBAH ---
    void OnDrawOrderPressed()
    {
        if (isSpawning) return; // Mencegah klik saat coroutine berjalan
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
            // Jika masih ada pemain, update teks
            if (uiManager != null) uiManager.SetTurnText($"Order Selection: Giliran Player {drawIndex + 1} untuk Draw");
        }
        else
        {
            // Jika pemain terakhir, panggil COROUTINE
            isSpawning = true; // Gunakan flag ini untuk mencegah klik ganda
            StartCoroutine(FinalizeTurnOrderSequence()); // <-- PANGGIL COROUTINE BARU
        }
    }

    // DIHAPUS: Fungsi 'FinalizeTurnOrder()' yang lama dihapus

    // --- FUNGSI BARU (Menggantikan FinalizeTurnOrder) ---
    IEnumerator FinalizeTurnOrderSequence()
    {
        // 1. Matikan tombol & ambil komponen Teks-nya
        TextMeshProUGUI buttonText = null;
        if (drawOrderButton != null)
        {
            drawOrderButton.interactable = false;
            buttonText = drawOrderButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        // 2. Hitung urutan
        turnOrder = drawnNumbers.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();

        // 3. Buat string log (PERMINTAAN BARU)
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Urutan Giliran:</b>"); // Judul lebih singkat
        for (int i = 0; i < turnOrder.Count; i++)
        {
            PlayerPawn player = turnOrder[i];
            sb.AppendLine($"{player.name} (Jalan ke-{i + 1})");
        }

        // 4. Tampilkan di Tombol
        if (buttonText != null)
        {
            // (Kamu mungkin perlu menyesuaikan Font Size agar muat)
            // buttonText.fontSize = 18; 
            buttonText.text = sb.ToString(); // <-- Tampilkan di tombol
        }

        // 5. Tampilkan di UI & Log
        if (uiManager != null)
        {
            uiManager.SetTurnText("Undian Selesai!"); // Teks atas
            uiManager.Log(sb.ToString()); // Tetap kirim ke log
            // HAPUS: uiManager.SetActionText(sb.ToString()); 
        }

        // 6. TUNGGU 3 DETIK (sesuai permintaan)
        yield return new WaitForSeconds(3f);

        // 7. Sembunyikan Panel Order
        if (orderPanel != null) orderPanel.SetActive(false);

        // 8. (Opsional) Reset teks tombol
        if (buttonText != null)
        {
            buttonText.text = "Draw";
            // kembalikan font size jika diubah
        }

        // 9. Siapkan UI Gameplay (Turn list & Kartu)
        if (uiManager != null)
        {
            uiManager.SetupPlayerList(turnOrder);
            uiManager.UpdateCycle(currentCycle);
        }

        // 10. Mulai game
        currentTurnIdx = 0;
        HighlightCurrentPlayer();

        // 11. Selesai
        isSpawning = false;
    }
    // ------------------------------------------
    #endregion

    // (Sisa script dari sini ke bawah tidak ada perubahan)
    // ...
    // (Salin-tempel semua sisa fungsi Anda yang ada di:
    //  - #region Gameplay Loop
    //  - #region Board, UI, & Dice Wall Helpers
    //  - #region Reverse Helpers
    //  - #region Card System Functions
    //  - #region Animations
    //  ...tepat di sini)
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

        if (current.drawCardNextTurn)
        {
            current.drawCardNextTurn = false;
            if (uiManager != null) uiManager.SetActionText("Inari Fortune! Dapat 1 kartu baru!");
            yield return new WaitForSeconds(1f);
            if (cardManager != null)
                cardManager.GiveRandomCardToPlayer(current, currentCycle);
            if (uiManager != null)
                uiManager.DisplayPlayerHand(current);
        }

        if (uiManager != null) uiManager.SetActionText($"{current.name} melempar dadu...");

        int rollResult = 0;
        yield return StartCoroutine(physicalDice.WaitForRollToStop((result) => { rollResult = result; }));

        DisableDiceWall();

        if (current.nextRollModifier != 0)
        {
            if (uiManager != null) uiManager.SetActionText($"Roll {rollResult} + Buff Hermes {current.nextRollModifier}!");
            rollResult += current.nextRollModifier;
            current.nextRollModifier = 0;
            yield return new WaitForSeconds(1f);
        }
        if (current.hasAresProvocation)
        {
            rollResult -= 1;
            if (uiManager != null) uiManager.SetActionText($"Roll {rollResult + 1} - Debuff Ares 1 = {rollResult}!");
            yield return new WaitForSeconds(1f);
        }
        rollResult = Mathf.Max(1, rollResult);

        int totalRolls = 1 + current.extraDiceRolls;
        current.extraDiceRolls = 0;

        for (int i = 0; i < totalRolls; i++)
        {
            if (i > 0)
            {
                if (uiManager != null) uiManager.SetActionText($"{current.name} melempar Dadu Ekstra (Odin)!");
                yield return new WaitForSeconds(1f);
                yield return StartCoroutine(physicalDice.WaitForRollToStop((result) => { rollResult = result; }));
                DisableDiceWall();
            }
            isActionRunning = true;

            bool isFirstRoll = (i == 0);
            yield return StartCoroutine(HandlePlayerRollAndMove(current, rollResult, isFirstRoll));

            if (winners.Contains(current))
                break;
        }

        AdvanceTurn();
    }

    IEnumerator HandlePlayerRollAndMove(PlayerPawn player, int roll, bool isFirstRoll)
    {
        if (uiManager != null) uiManager.SetActionText($"{player.name} roll {roll}");

        List<PlayerPawn> validTargets = GetValidReverseTargets(player);
        bool didReverse = false;

        if (isFirstRoll && validTargets.Count > 0 && currentCycle > 1)
        {
            #region Reverse Logic
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
                    int finalRoll = roll;
                    if (player.hasAresProvocation)
                    {
                        finalRoll += 2;
                        if (uiManager != null) uiManager.SetActionText($"Ares Provocation! Mundur {finalRoll}!");
                        yield return new WaitForSeconds(1f);
                    }
                    int targetStart = target.currentTileID;
                    int targetFinal = Mathf.Max(1, targetStart - finalRoll);
                    yield return StartCoroutine(target.MoveToTile(targetFinal, (int id) => GetTilePositionWithOffset(id, target)));
                    yield return StartCoroutine(CheckLandingTile(target));
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

            if (finalTarget > totalTilesInBoard)
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

            yield return StartCoroutine(CheckLandingTile(player));
            UpdatePawnPositionsOnTile(player.currentTileID);
            #endregion
        }

        isActionRunning = false;
        yield break;
    }

    IEnumerator CheckLandingTile(PlayerPawn player)
    {
        if (player.currentTileID == totalTilesInBoard)
        {
            if (uiManager != null) uiManager.SetActionText($"{player.name} mencapai finish!");

            if (!winners.Contains(player))
            {
                winners.Add(player);
                player.SetHighlight(false);
                if (uiManager != null)
                {
                    int winnerIndex = turnOrder.IndexOf(player);
                    uiManager.SetPlayerAsWinner(winnerIndex);
                }
            }
            yield break;
        }

        Tiles landed = GetTileByID(player.currentTileID);
        if (landed == null) yield break;

        if (landed.type == TileType.SnakeStart && landed.targetTile != null)
        {
            if (player.immuneToAllNegativeTurns > 0)
            {
                if (uiManager != null) uiManager.SetActionText($"{player.name} kebal dari ular (Isis Protection)!");
                yield return new WaitForSeconds(1f);
            }
            else if (player.immuneToSnakeUses > 0)
            {
                player.immuneToSnakeUses--;
                if (uiManager != null) uiManager.SetActionText($"{player.name} kebal dari ular (Shield of Athena)!");
                yield return new WaitForSeconds(1f);
            }
            else
            {
                if (uiManager != null) uiManager.SetActionText($"{player.name} Turun ular!");
                yield return new WaitForSeconds(0.2f);
                Tiles startTile = landed;
                Tiles endTile = landed.targetTile;
                yield return StartCoroutine(AnimateSnakeSequence(player, startTile, endTile));
                UpdatePawnPositionsOnTile(player.currentTileID);
            }
        }
        else if (landed.type == TileType.LadderStart && landed.targetTile != null)
        {
            if (uiManager != null) uiManager.SetActionText($"{player.name} Naik tangga!");
            yield return new WaitForSeconds(0.2f);

            Tiles startTile = landed;
            Tiles endTile = landed.targetTile;
            yield return StartCoroutine(AnimateLadderSequence(player, startTile, endTile));

            if (player.hasAmaterasuRadiance)
            {
                player.hasAmaterasuRadiance = false;
                player.getsExtraTurn = true;
                if (uiManager != null) uiManager.SetActionText($"Amaterasu Radiance! {player.name} mendapat giliran ekstra!");
                yield return new WaitForSeconds(1.5f);
            }

            UpdatePawnPositionsOnTile(player.currentTileID);
        }
        else if (landed.type == TileType.BlessingCard)
        {
            if (uiManager != null) uiManager.SetActionText($"{player.name} mendarat di petak Blessing!");
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(ShowCardChoiceRoutine(player));
        }
    }

    void AdvanceTurn()
    {
        PlayerPawn currentPlayer = turnOrder[currentTurnIdx];

        if (currentPlayer.getsExtraTurn)
        {
            currentPlayer.getsExtraTurn = false;
            if (uiManager != null) uiManager.SetActionText($"{currentPlayer.name} mengambil giliran ekstra!");
            isActionRunning = false;
            HighlightCurrentPlayer();
            return;
        }

        int activePlayerCount = turnOrder.Count - winners.Count;
        if (activePlayerCount <= 1)
        {
            isActionRunning = true;
            if (physicalDice != null) physicalDice.gameObject.SetActive(false);
            PlayerPawn loser = turnOrder.FirstOrDefault(p => !winners.Contains(p));

            string gameOverMsg = "Game Selesai!";
            if (loser != null)
                gameOverMsg = $"Game Selesai! {loser.name} adalah yang terakhir!";
            else
                gameOverMsg = "Game Selesai! Seri!";

            if (uiManager != null)
            {
                uiManager.SetActionText(gameOverMsg);
                uiManager.SetTurnText("GAME OVER");
                uiManager.ShowGameOver(winners, loser);
            }
            return;
        }

        PlayerPawn nextPlayer;
        do
        {
            currentTurnIdx = (currentTurnIdx + 1) % turnOrder.Count;
            nextPlayer = turnOrder[currentTurnIdx];
        }
        while (winners.Contains(nextPlayer));

        if (currentTurnIdx == 0)
        {
            currentCycle++;
            if (uiManager != null) uiManager.Log($"--- CYCLE BARU DIMULAI: {currentCycle} ---");
            if (uiManager != null) uiManager.UpdateCycle(currentCycle);
            foreach (var p in players)
            {
                if (winners.Contains(p)) continue;
                p.wasReversedThisCycle = false;
                p.ShowReversedBadge(false);
                p.hasAmaterasuRadiance = false;
                p.hasAresProvocation = false;
                if (p.immuneToReverseCycles > 0)
                    p.immuneToReverseCycles--;
                if (p.immuneToAllNegativeTurns > 0)
                    p.immuneToAllNegativeTurns--;
                CheckForExpiredCards(p);
            }
        }

        if (nextPlayer.skipTurns > 0)
        {
            if (nextPlayer.immuneToAllNegativeTurns > 0)
            {
                if (uiManager != null) uiManager.SetActionText($"{nextPlayer.name} kebal dari skip turn (Isis Protection)!");
                nextPlayer.skipTurns = 0;
            }
            else
            {
                if (uiManager != null) uiManager.SetActionText($"{nextPlayer.name} skip giliran karena efek Anubis!");
                nextPlayer.skipTurns--;
                AdvanceTurn();
                return;
            }
        }

        isActionRunning = false;
        HighlightCurrentPlayer();
    }

    void CheckForExpiredCards(PlayerPawn player)
    {
        List<PlayerCardInstance> expiredCards = player.heldCards
            .Where(card => (currentCycle - card.cycleAcquired) >= 3)
            .ToList();
        if (expiredCards.Count > 0)
        {
            string msg = $"{player.name} kehilangan {expiredCards.Count} kartu kadaluarsa.";
            Debug.Log(msg);
            if (uiManager != null) uiManager.SetActionText(msg);
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

    void HighlightCurrentPlayer()
    {
        if (physicalDice != null) physicalDice.ResetDice();
        if (turnOrder.Count == 0) return;

        PlayerPawn cur = turnOrder[currentTurnIdx];

        if (winners.Contains(cur))
        {
            AdvanceTurn();
            return;
        }

        if (uiManager != null)
        {
            uiManager.SetTurnText($"Giliran: {cur.name}");
            uiManager.ClearActionText();
        }

        for (int i = 0; i < players.Count; i++)
            players[i].SetHighlight(players[i] == cur);

        if (uiManager != null)
        {
            uiManager.UpdateActivePlayer(currentTurnIdx);
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

    #region Reverse Helpers
    public List<PlayerPawn> GetValidReverseTargets(PlayerPawn actor)
    {
        return players.Where(p =>
            p != actor &&
            p.currentTileID > 1 &&
            !p.wasReversedThisCycle &&
            p.immuneToReverseCycles <= 0 &&
            p.immuneToAllNegativeTurns <= 0
        ).ToList();
    }
    public void OnPawnClicked(PlayerPawn clickedPawn)
    {
        if (!isInReverseMode || !awaitingTargetSelection)
        {
            return;
        }
        if (currentActorForSelection != null && clickedPawn == currentActorForSelection)
        {
            return;
        }
        if (currentValidTargets == null || !currentValidTargets.Contains(clickedPawn))
        {
            return;
        }
        selectedTargetForReverse = clickedPawn;
        awaitingTargetSelection = false;
    }
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
            choiceInstructionText.text = "Pilih Target";
        }
        if (uiManager != null) uiManager.SetTurnText("Pilih target (klik pawn) atau Cancel.");
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
    public bool IsPlayerTurn(PlayerPawn player) { return (turnOrder.Count > 0 && turnOrder[currentTurnIdx] == player); }
    public PlayerPawn GetCurrentPlayer() { return (turnOrder.Count > 0) ? turnOrder[currentTurnIdx] : null; }

    private IEnumerator ShowCardChoiceRoutine(PlayerPawn player)
    {
        isActionRunning = true;
        playerWaitingForCard = player;
        if (uiManager != null) uiManager.SetActionText($"{player.name} sedang memilih Blessing...");
        List<CardData> cardSelection = cardManager.GetRandomCardSelection(3);
        uiManager.StartCardSelection(cardSelection);
        while (uiManager.cardChoicePanel.activeSelf)
        {
            yield return null;
        }
        if (uiManager != null) uiManager.SetActionText($"Giliran {player.name} berlanjut.");
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
            if (uiManager != null) uiManager.SetActionText("Tidak bisa menggunakan kartu saat aksi berjalan.");
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
                if (uiManager != null) uiManager.SetActionText($"{user.name} kebal 'reverse' selama {card.intValue} cycle!");
                break;
            case CardEffectType.ShieldOfAthena:
                user.immuneToSnakeUses = card.intValue;
                if (uiManager != null) uiManager.SetActionText($"{user.name} kebal 'snake' untuk {card.intValue} kali!");
                break;
            case CardEffectType.HermesFavors:
                user.nextRollModifier += card.intValue;
                if (uiManager != null) uiManager.SetActionText($"{user.name} mendapat +{card.intValue} di roll berikutnya!");
                break;
            case CardEffectType.PoseidonWaves:
                StartCoroutine(Effect_PoseidonWavesRoutine(user, card.intValue));
                break;
            case CardEffectType.ZeusWrath:
                StartCoroutine(Effect_TargetedMoveRoutine(user, card.intValue, "ZeusWrath"));
                break;
            case CardEffectType.AresProvocation:
                user.hasAresProvocation = true;
                if (uiManager != null) uiManager.SetActionText($"{user.name} kini memiliki Ares Provocation!");
                break;
            case CardEffectType.OdinWisdom:
                user.extraDiceRolls += card.intValue;
                if (uiManager != null) uiManager.SetActionText($"{user.name} mendapat {card.intValue} lempar dadu tambahan!");
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
            case CardEffectType.IsisProtection:
                user.immuneToAllNegativeTurns = card.intValue;
                if (uiManager != null) uiManager.SetActionText($"{user.name} kebal dari efek negatif selama {card.intValue} giliran!");
                break;
            case CardEffectType.AmaterasuRadiance:
                user.hasAmaterasuRadiance = true;
                if (uiManager != null) uiManager.SetActionText($"{user.name} akan mendapat giliran ekstra jika naik tangga!");
                break;
            case CardEffectType.SusanooStorm:
                StartCoroutine(Effect_SusanooStormRoutine(user, card.intValue));
                break;
            case CardEffectType.InariFortune:
                user.nextRollModifier += card.intValue;
                user.drawCardNextTurn = true;
                if (uiManager != null) uiManager.SetActionText($"{user.name} akan mendapat +{card.intValue} roll & 1 kartu di giliran berikutnya!");
                break;
        }
    }
    public void HidePlayerCardHand()
    {
        if (uiManager != null)
        {
            uiManager.HidePlayerHand();
        }
    }
    private IEnumerator Effect_SusanooStormRoutine(PlayerPawn user, int moveAmount)
    {
        isActionRunning = true;
        try
        {
            if (uiManager != null) uiManager.SetActionText($"{user.name} menggunakan Susanoo Storm!");
            yield return new WaitForSeconds(0.5f);
            List<PlayerPawn> targets = players.Where(p => p != user && p.currentTileID > user.currentTileID).ToList();
            if (targets.Count == 0)
            {
                if (uiManager != null) uiManager.SetActionText("Tidak ada pemain di depanmu!");
                yield return new WaitForSeconds(1f);
            }
            else
            {
                foreach (PlayerPawn target in targets)
                {
                    if (target.immuneToAllNegativeTurns > 0 || target.immuneToReverseCycles > 0)
                    {
                        if (uiManager != null) uiManager.SetActionText($"{target.name} kebal dari efek ini!");
                    }
                    else
                    {
                        if (uiManager != null) uiManager.SetActionText($"{target.name} terkena badai! Mundur {moveAmount} petak!");
                        int targetTile = Mathf.Max(1, target.currentTileID - moveAmount);
                        yield return StartCoroutine(target.MoveToTile(targetTile, (int id) => GetTilePositionWithOffset(id, target)));
                        yield return StartCoroutine(CheckLandingTile(target));
                        UpdatePawnPositionsOnTile(target.currentTileID);
                    }
                    yield return new WaitForSeconds(0.3f);
                }
            }
        }
        finally { isActionRunning = false; }
    }
    private IEnumerator Effect_MovePlayer(PlayerPawn player, int moveAmount, string effectName)
    {
        isActionRunning = true;
        try
        {
            int targetTile = Mathf.Min(totalTilesInBoard, Mathf.Max(1, player.currentTileID + moveAmount));
            if (uiManager != null) uiManager.SetActionText($"{player.name} menggunakan {effectName}, maju ke petak {targetTile}");
            yield return StartCoroutine(player.MoveToTile(targetTile, (int id) => GetTilePositionWithOffset(id, player)));
            yield return StartCoroutine(CheckLandingTile(player));
            UpdatePawnPositionsOnTile(player.currentTileID);
        }
        finally
        {
            isActionRunning = false;
        }
    }

    private IEnumerator SelectTargetRoutine(PlayerPawn user, List<PlayerPawn> validTargets, System.Action<PlayerPawn> callback)
    {
        if (validTargets.Count == 0)
        {
            if (uiManager != null) uiManager.SetActionText("Tidak ada target yang valid!");
            yield return new WaitForSeconds(1f);
            callback(null);
            yield break;
        }
        currentValidTargets = validTargets;
        currentActorForSelection = user;
        selectedTargetForReverse = null;
        awaitingTargetSelection = true;
        isInReverseMode = true;
        EnterReverseSelectionUI();
        while (selectedTargetForReverse == null && isInReverseMode)
        {
            yield return null;
        }
        PlayerPawn chosenTarget = null;
        if (!isInReverseMode || selectedTargetForReverse == null)
        {
            if (uiManager != null) uiManager.SetActionText("Penggunaan kartu dibatalkan.");
            CleanupChoiceUI();
            chosenTarget = null;
        }
        else
        {
            chosenTarget = selectedTargetForReverse;
            CleanupChoiceUI();
            ExitReverseSelectionUI();
        }
        callback(chosenTarget);
    }

    private IEnumerator Effect_TargetedMoveRoutine(PlayerPawn user, int moveAmount, string effectName)
    {
        isActionRunning = true;
        try
        {
            if (uiManager != null) uiManager.SetActionText($"{user.name} menggunakan {effectName}! Pilih target.");
            List<PlayerPawn> validTargets = players.Where(p => p != user && p.immuneToAllNegativeTurns <= 0).ToList();
            if (effectName == "ZeusWrath")
            {
                validTargets = validTargets.Where(p => p.immuneToReverseCycles <= 0).ToList();
            }

            PlayerPawn target = null;
            yield return StartCoroutine(SelectTargetRoutine(user, validTargets, (chosenPawn) => {
                target = chosenPawn;
            }));

            if (target == null)
            {
                yield break;
            }

            if (effectName == "ZeusWrath")
            {
                if (uiManager != null) uiManager.SetActionText($"{target.name} terkena Zeus Wrath! Mundur {moveAmount} petak!");
                int targetTile = Mathf.Max(1, target.currentTileID - moveAmount);
                yield return StartCoroutine(target.MoveToTile(targetTile, (int id) => GetTilePositionWithOffset(id, target)));
                yield return StartCoroutine(CheckLandingTile(target));
                UpdatePawnPositionsOnTile(target.currentTileID);
            }
            else if (effectName == "LokiTricks")
            {
                if (uiManager != null) uiManager.SetActionText($"{user.name} dan {target.name} bertukar posisi!");
                int userTile = user.currentTileID; int targetTile = target.currentTileID;
                yield return StartCoroutine(user.TeleportToTile(targetTile, (int id) => GetTilePositionWithOffset(id, user)));
                yield return StartCoroutine(target.TeleportToTile(userTile, (int id) => GetTilePositionWithOffset(id, target)));
                UpdatePawnPositionsOnTile(user.currentTileID);
                UpdatePawnPositionsOnTile(target.currentTileID);
            }
        }
        finally
        {
            isActionRunning = false;
        }
    }

    private IEnumerator Effect_TargetedStatusRoutine(PlayerPawn user, int statusValue, string effectName)
    {
        isActionRunning = true;
        try
        {
            if (uiManager != null) uiManager.SetActionText($"{user.name} menggunakan {effectName}! Pilih target.");

            List<PlayerPawn> validTargets = players.Where(p => p != user && p.immuneToAllNegativeTurns <= 0).ToList();

            PlayerPawn target = null;
            yield return StartCoroutine(SelectTargetRoutine(user, validTargets, (chosenPawn) => {
                target = chosenPawn;
            }));

            if (target == null)
            {
                yield break;
            }

            if (effectName == "AnubisJudgment")
            {
                target.skipTurns += statusValue;
                if (uiManager != null) uiManager.SetActionText($"{target.name} akan skip {statusValue} giliran!");
            }
        }
        finally
        {
            isActionRunning = false;
        }
    }

    private IEnumerator Effect_ThorHammerRoutine(PlayerPawn user, int moveAmount)
    {
        isActionRunning = true;
        try
        {
            if (uiManager != null) uiManager.SetActionText("Mencari target Thor Hammer...");
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

            if (target != null && target.immuneToAllNegativeTurns <= 0 && target.immuneToReverseCycles <= 0)
            {
                if (uiManager != null) uiManager.SetActionText($"{target.name} terkena Thor Hammer! Mundur {moveAmount} petak!");
                int targetTile = Mathf.Max(1, target.currentTileID - moveAmount);
                yield return StartCoroutine(target.MoveToTile(targetTile, (int id) => GetTilePositionWithOffset(id, target)));
                yield return StartCoroutine(CheckLandingTile(target));
                UpdatePawnPositionsOnTile(target.currentTileID);
            }
            else if (target != null)
            {
                if (uiManager != null) uiManager.SetActionText($"{target.name} kebal dari Thor Hammer!");
            }
            else
            {
                if (uiManager != null) uiManager.SetActionText("Tidak ada pemain di depanmu!");
            }
        }
        finally
        {
            isActionRunning = false;
        }
    }

    private IEnumerator Effect_PoseidonWavesRoutine(PlayerPawn user, int moveAmount)
    {
        isActionRunning = true;
        try
        {
            if (uiManager != null) uiManager.SetActionText($"{user.name} menggunakan Poseidon Waves!");
            yield return new WaitForSeconds(0.5f);

            int userRow = (user.currentTileID - 1) / 10;
            List<PlayerPawn> targets = players.Where(p =>
                p != user &&
                ((p.currentTileID - 1) / 10) == userRow
            ).ToList();

            if (targets.Count == 0)
            {
                if (uiManager != null) uiManager.SetActionText("Tidak ada pemain lain di barismu!");
                yield return new WaitForSeconds(1f);
            }
            else
            {
                foreach (PlayerPawn target in targets)
                {
                    if (target.immuneToAllNegativeTurns > 0 || target.immuneToReverseCycles > 0)
                    {
                        if (uiManager != null) uiManager.SetActionText($"{target.name} kebal dari efek ini!");
                    }
                    else
                    {
                        if (uiManager != null) uiManager.SetActionText($"{target.name} terdorong ombak! Mundur {moveAmount} petak!");
                        int targetTile = Mathf.Max(1, target.currentTileID - moveAmount);
                        yield return StartCoroutine(target.MoveToTile(targetTile, (int id) => GetTilePositionWithOffset(id, target)));
                        yield return StartCoroutine(CheckLandingTile(target));
                        UpdatePawnPositionsOnTile(target.currentTileID);
                    }
                    yield return new WaitForSeconds(0.3f);
                }
            }
        }
        finally { isActionRunning = false; }
    }
    #endregion

    #region Animations
    // (Fungsi AnimateSnakeSequence dan AnimateLadderSequence tidak berubah)
    private IEnumerator AnimateSnakeSequence(PlayerPawn player, Tiles startTile, Tiles endTile)
    {
        Vector3 verticalOffset = new Vector3(0, snakeAnimationHeight, 0);
        Vector3 startTilePos_Original = startTile.transform.position;
        Vector3 endTilePos_Original = endTile.transform.position;
        Vector3 playerPos_Start_Original = player.transform.position;

        Vector3 startTilePos_Down = startTilePos_Original + verticalOffset;
        Vector3 endTilePos_Down = endTilePos_Original + verticalOffset;
        Vector3 playerPos_Start_Down = playerPos_Start_Original + verticalOffset;

        float t = 0;
        while (t < 1.0f)
        {
            t += Time.deltaTime * snakeAnimationSpeed;
            startTile.transform.position = Vector3.Lerp(startTilePos_Original, startTilePos_Down, t);
            endTile.transform.position = Vector3.Lerp(endTilePos_Original, endTilePos_Down, t);
            player.transform.position = Vector3.Lerp(playerPos_Start_Original, playerPos_Start_Down, t);
            yield return null;
        }
        startTile.transform.position = startTilePos_Down;
        endTile.transform.position = endTilePos_Down;
        player.transform.position = playerPos_Start_Down;

        Vector3 playerPos_End_Down = endTile.GetPlayerPosition() + verticalOffset;
        player.transform.position = playerPos_End_Down;
        player.currentTileID = endTile.tileID;

        int row = (player.currentTileID - 1) / 10;
        float targetYAngle = (row % 2 == 0) ? 0f : 180f;
        player.transform.rotation = Quaternion.Euler(0, targetYAngle, 0);

        yield return new WaitForSeconds(0.25f);

        Vector3 playerPos_End_Original = endTile.GetPlayerPosition();
        t = 0;
        while (t < 1.0f)
        {
            t += Time.deltaTime * snakeAnimationSpeed;
            startTile.transform.position = Vector3.Lerp(startTilePos_Down, startTilePos_Original, t);
            endTile.transform.position = Vector3.Lerp(endTilePos_Down, endTilePos_Original, t);
            player.transform.position = Vector3.Lerp(playerPos_End_Down, playerPos_End_Original, t);
            yield return null;
        }
        startTile.transform.position = startTilePos_Original;
        endTile.transform.position = endTilePos_Original;
        player.transform.position = playerPos_End_Original;
    }

    private IEnumerator AnimateLadderSequence(PlayerPawn player, Tiles startTile, Tiles endTile)
    {
        if (ladderStepPrefab == null)
        {
            Debug.LogError("Prefab Tangga (ladderStepPrefab) belum di-set di MultiplayerManager!");
            yield return StartCoroutine(player.TeleportToTile(endTile.tileID, (int id) => GetTilePositionWithOffset(id, player)));
            yield break;
        }

        List<GameObject> deployedLadderSteps = new List<GameObject>();

        Vector3 verticalOffset = new Vector3(0, ladderVerticalOffset, 0);
        Vector3 startPos = startTile.GetPlayerPosition() + verticalOffset;
        Vector3 endPos = endTile.GetPlayerPosition() + verticalOffset;

        Vector3 direction = (endPos - startPos).normalized;
        float distance = Vector3.Distance(startPos, endPos);
        Quaternion rotation = Quaternion.LookRotation(direction);
        int stepCount = Mathf.Max(1, Mathf.RoundToInt(distance));

        for (int i = 0; i <= stepCount; i++)
        {
            float t_lerp = (float)i / stepCount;
            Vector3 stepFinalPos = Vector3.Lerp(startPos, endPos, t_lerp);
            Vector3 stepSpawnPos = stepFinalPos + new Vector3(0, ladderDeployHeight, 0);

            GameObject step = Instantiate(ladderStepPrefab, stepSpawnPos, rotation);
            deployedLadderSteps.Add(step);

            float fallTime = 0;
            float fallDuration = Vector3.Distance(stepSpawnPos, stepFinalPos) / ladderDeploySpeed;
            if (fallDuration <= 0) fallDuration = 0.1f;

            while (fallTime < 1.0f)
            {
                fallTime += Time.deltaTime / fallDuration;
                step.transform.position = Vector3.Lerp(stepSpawnPos, stepFinalPos, fallTime);
                yield return null;
            }
            step.transform.position = stepFinalPos;

            yield return new WaitForSeconds(ladderStepDelay);
        }

        yield return StartCoroutine(player.TeleportToTile(endTile.tileID, endPos));

        yield return new WaitForSeconds(0.5f);

        foreach (GameObject step in deployedLadderSteps)
        {
            Destroy(step);
            yield return new WaitForSeconds(ladderStepDelay / 2);
        }
    }
    #endregion
}