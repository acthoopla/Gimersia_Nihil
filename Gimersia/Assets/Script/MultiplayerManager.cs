using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance; // Singleton

    [Header("Prefabs & References")]
    public GameObject playerPrefab;
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
    public Button rollButton;
    public TextMeshProUGUI infoText;

    [Header("UI - Reverse Choice")]
    public GameObject choicePanel;
    public Button btnMoveSelf;
    public Button btnReverse;
    public Button btnCancelTargetSelect;
    public TextMeshProUGUI choiceInstructionText;

    [Header("Card System References")]
    public UIManager cardUIManager;
    public CardManager cardManager;

    [Header("Dice Physics")]
    public Dice physicalDice;

    [Header("Board Settings")]
    public int totalTilesInBoard = 100;

    [Header("Tile Offset")]
    public float tileOffsetBaseRadius = 0.25f;
    public float tileOffsetPerPlayer = 0.18f;
    public float tileOffsetHeightStep = 0.02f;

    // --- Runtime ---
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

    // --- Runtime (Targeting) ---
    private bool awaitingTargetSelection = false;
    private PlayerPawn selectedTargetForReverse = null;
    private PlayerPawn currentActorForSelection = null;
    private List<PlayerPawn> currentValidTargets = new List<PlayerPawn>();
    private bool isInReverseMode = false;
    private PlayerPawn playerWaitingForCard;

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
        if (rollButton != null) rollButton.gameObject.SetActive(false);
        if (infoText != null) infoText.text = "Pilih jumlah pemain";
        if (orderStatusText != null) orderStatusText.text = "";
    }

    void Start()
    {
        Tiles[] all = FindObjectsOfType<Tiles>();
        boardTiles = all.OrderBy(t => t.tileID).ToList();

        if (boardTiles.Count == 0)
        {
            Debug.LogError("Tidak menemukan Tiles di scene!");
            if (infoText != null) infoText.text = "Error: Board tidak ditemukan";
        }

        if (cardUIManager != null)
            cardUIManager.UpdateCycle(currentCycle);
    }

    // -------------------------
    // Player count & spawn
    // -------------------------
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
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(playerPrefab, playersParent);
            go.name = $"Player_{i + 1}";
            PlayerPawn pp = go.GetComponent<PlayerPawn>();
            if (pp == null) pp = go.AddComponent<PlayerPawn>();

            pp.playerIndex = i;
            pp.currentTileID = 1;
            pp.SetVisualIndex(i);
            pp.heldCards = new List<PlayerCardInstance>();

            go.transform.localScale = playerPrefab.transform.localScale;
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
        players.Clear(); drawnNumbers.Clear(); turnOrder.Clear();
        drawIndex = 0; currentTurnIdx = 0; currentCycle = 1;
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
        dicePool = new List<int> { 1, 2, 3, 4, 5, 6 };
        drawnNumbers.Clear(); drawIndex = 0;
        if (orderPanel != null) orderPanel.SetActive(true);
        if (rollButton != null) rollButton.gameObject.SetActive(false);
        UpdatePoolUI(); UpdateOrderStatusUI();
        if (infoText != null) infoText.text = $"Order Selection: Giliran Player {drawIndex + 1} untuk Draw";
    }

    void UpdatePoolUI() { /* ... */ }
    void UpdateOrderStatusUI() { /* ... */ }
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

    void FinalizeTurnOrder()
    {
        turnOrder = drawnNumbers.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
        string orderStr = "Turn Order: " + string.Join(" > ", turnOrder.Select(p => p.name));
        if (infoText != null) infoText.text = orderStr;
        if (orderPanel != null) orderPanel.SetActive(false);
        currentTurnIdx = 0;
        if (cardUIManager != null) cardUIManager.UpdateCycle(currentCycle);
        HighlightCurrentPlayer();
    }

    // -------------------------
    // Gameplay: dice + move
    // -------------------------
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

        // --- KONSUMSI EFEK STATUS PADA ROLL ---
        if (current.nextRollModifier != 0) // Hermes Favors
        {
            if (infoText != null) infoText.text = $"Roll {rollResult} + Buff Hermes {current.nextRollModifier}!";
            rollResult += current.nextRollModifier;
            current.nextRollModifier = 0; // Efek terpakai
            yield return new WaitForSeconds(1f);
        }

        if (current.hasAresProvocation) // Ares Provocation
        {
            rollResult -= 1; // -1 untuk diri sendiri
            if (infoText != null) infoText.text = $"Roll {rollResult + 1} - Debuff Ares 1 = {rollResult}!";
            yield return new WaitForSeconds(1f);
        }
        rollResult = Mathf.Max(1, rollResult);

        // Cek Odin Wisdom
        int totalRolls = 1 + current.extraDiceRolls;
        current.extraDiceRolls = 0; // Efek terpakai

        for (int i = 0; i < totalRolls; i++)
        {
            if (i > 0)
            {
                if (infoText != null) infoText.text = $"{current.name} melempar Dadu Ekstra (Odin)!";
                yield return new WaitForSeconds(1f);
                yield return StartCoroutine(physicalDice.WaitForRollToStop((result) => { rollResult = result; }));
            }
            isActionRunning = true;
            yield return StartCoroutine(HandlePlayerRollAndMove(current, rollResult));
            if (current.currentTileID == totalTilesInBoard)
                break;
        }
        AdvanceTurn();
    }

    IEnumerator HandlePlayerRollAndMove(PlayerPawn player, int roll)
    {
        if (infoText != null) infoText.text = $"{player.name} roll {roll}";
        List<PlayerPawn> validTargets = GetValidReverseTargets(player);
        bool didReverse = false;

        if (validTargets.Count > 0)
        {
            // ... (Logika UI Pilihan Reverse / Move Self Anda)
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
            void OnBtnReverseLocal()
            {
                EnterReverseSelectionUI();
                awaitingTargetSelection = true;
                selectedTargetForReverse = null;
            }
            void OnBtnCancelLocal()
            {
                awaitingTargetSelection = false;
                selectedTargetForReverse = null;
                ExitReverseSelectionUI();
            }
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

                    // --- KONSUMSI EFEK ARES PROVOCATION ---
                    int finalRoll = roll;
                    if (player.hasAresProvocation)
                    {
                        finalRoll += 2; // +2 untuk memundurkan lawan
                        if (infoText != null) infoText.text = $"Ares Provocation! Mundur {finalRoll}!";
                        yield return new WaitForSeconds(1f);
                    }
                    // ------------------------------------

                    int targetStart = target.currentTileID;
                    int targetFinal = Mathf.Max(1, targetStart - finalRoll);
                    yield return StartCoroutine(target.MoveToTile(targetFinal, (int id) => GetTilePositionWithOffset(id, target)));
                    yield return StartCoroutine(CheckLandingTile(target));
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
        }

        if (!didReverse)
        {
            // ... (Logika Move Self Anda)
            int startTile = player.currentTileID;
            int finalTarget = startTile + roll;
            if (finalTarget == totalTilesInBoard)
            {
                yield return StartCoroutine(player.MoveToTile(finalTarget, (int id) => GetTilePositionWithOffset(id, player)));
                if (infoText != null) infoText.text = $"{player.name} mencapai finish!";
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
            yield return StartCoroutine(CheckLandingTile(player));
        }
        isActionRunning = false;
        yield break;
    }

    // Fungsi helper untuk cek tile setelah mendarat
    IEnumerator CheckLandingTile(PlayerPawn player)
    {
        Tiles landed = GetTileByID(player.currentTileID);
        if (landed == null) yield break;

        // Cek Ular
        if (landed.type == TileType.SnakeStart && landed.targetTile != null)
        {
            // --- KONSUMSI EFEK SHIELD OF ATHENA ---
            if (player.immuneToSnakeUses > 0)
            {
                player.immuneToSnakeUses--; // Efek terpakai
                if (infoText != null) infoText.text = $"{player.name} kebal dari ular!";
                yield return new WaitForSeconds(1f);
            }
            else
            {
                if (infoText != null) infoText.text = $"{player.name} Turun ular!";
                yield return new WaitForSeconds(0.2f);
                int targetID = landed.targetTile.tileID;
                yield return StartCoroutine(player.TeleportToTile(targetID, (int id) => GetTilePositionWithOffset(id, player)));
            }
        }
        // Cek Tangga
        else if (landed.type == TileType.LadderStart && landed.targetTile != null)
        {
            if (infoText != null) infoText.text = $"{player.name} Naik tangga!";
            yield return new WaitForSeconds(0.2f);
            int targetID = landed.targetTile.tileID;
            yield return StartCoroutine(player.TeleportToTile(targetID, (int id) => GetTilePositionWithOffset(id, player)));
        }
        // Cek Kartu Blessing
        else if (landed.type == TileType.BlessingCard)
        {
            if (infoText != null) infoText.text = $"{player.name} mendarat di petak Blessing!";
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(ShowCardChoiceRoutine(player));
        }
    }

    // Fungsi untuk ganti giliran & logika cycle
    void AdvanceTurn()
    {
        currentTurnIdx = (currentTurnIdx + 1) % turnOrder.Count;

        // --- KONSUMSI EFEK CYCLE ---
        if (currentTurnIdx == 0) // Cycle baru dimulai
        {
            currentCycle++;
            Debug.Log($"--- CYCLE BARU DIMULAI: {currentCycle} ---");
            if (cardUIManager != null) cardUIManager.UpdateCycle(currentCycle);

            foreach (var p in players)
            {
                p.wasReversedThisCycle = false;
                p.ShowReversedBadge(false);
                p.hasAresProvocation = false;

                // Kurangi durasi buff Athena Blessing
                if (p.immuneToReverseCycles > 0)
                    p.immuneToReverseCycles--;

                // Cek kartu kadaluarsa
                CheckForExpiredCards(p);
            }
        }

        // --- KONSUMSI EFEK ANUBIS JUDGMENT ---
        PlayerPawn nextPlayer = turnOrder[currentTurnIdx];
        if (nextPlayer.skipTurns > 0)
        {
            if (infoText != null) infoText.text = $"{nextPlayer.name} skip giliran karena efek Anubis!";
            nextPlayer.skipTurns--;
            AdvanceTurn(); // Langsung ganti giliran lagi
            return;
        }
        // ------------------------

        isActionRunning = false;
        HighlightCurrentPlayer();
    }

    // Fungsi untuk cek kartu kadaluarsa
    void CheckForExpiredCards(PlayerPawn player)
    {
        // Dapat di C1. C2: 2-1=1. C3: 3-1=2. C4: 4-1=3 (Kadaluarsa)
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
            // Langsung update 'tas' jika pemain saat ini adalah yang kehilangan
            if (player == GetCurrentPlayer())
            {
                cardUIManager.DisplayPlayerHand(player);
            }
        }
    }

    // Fungsi untuk update 'tas' pemain
    void HighlightCurrentPlayer()
    {
        if (physicalDice != null) physicalDice.ResetDice();
        if (turnOrder.Count == 0) return;

        PlayerPawn cur = turnOrder[currentTurnIdx];
        if (infoText != null) infoText.text = $"Giliran: {cur.name}. Ambil & lempar dadunya!";

        for (int i = 0; i < players.Count; i++)
            players[i].SetHighlight(players[i] == cur);

        if (cardUIManager != null)
        {
            cardUIManager.DisplayPlayerHand(cur);
            cardUIManager.UpdatePlayerTurnHighlight(currentTurnIdx);
        }
    }

    // -------------------------
    // Board helpers
    // -------------------------
    Vector3 GetTilePosition(int tileID) { /* ... */ return boardTiles.FirstOrDefault(x => x.tileID == tileID)?.GetPlayerPosition() ?? Vector3.zero; }
    Tiles GetTileByID(int id) { /* ... */ return boardTiles.FirstOrDefault(x => x.tileID == id); }
    public Vector3 GetTilePositionWithOffset(int tileID, PlayerPawn pawn)
    { /* ... (Logika offset Anda) ... */
        Vector3 center = GetTilePosition(tileID);
        var onTile = players.Where(p => p.currentTileID == tileID).OrderBy(p => p.playerIndex).ToList();
        if (!onTile.Contains(pawn)) { var tmp = onTile.ToList(); tmp.Add(pawn); onTile = tmp.OrderBy(p => p.playerIndex).ToList(); }
        int count = Mathf.Max(1, onTile.Count); int slot = onTile.FindIndex(p => p == pawn); if (slot < 0) slot = 0;
        float radius = tileOffsetBaseRadius + tileOffsetPerPlayer * (count - 1); if (count == 2) radius = Mathf.Max(radius, 0.32f);
        float angle = (360f / count) * slot * Mathf.Deg2Rad; Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        Vector3 upOffset = Vector3.up * (tileOffsetHeightStep * slot); return center + offset + upOffset;
    }

    // -------------------------
    // Reverse helpers
    // -------------------------
    public List<PlayerPawn> GetValidReverseTargets(PlayerPawn actor)
    {
        // --- KONSUMSI EFEK ATHENA BLESSING ---
        return players.Where(p =>
            p != actor &&
            p.currentTileID > 1 &&
            !p.wasReversedThisCycle &&
            p.immuneToReverseCycles <= 0 // Cek kebal reverse
        ).ToList();
    }

    // Dipanggil oleh skrip PlayerPawn saat pion di-klik
    public void OnPawnClicked(PlayerPawn clickedPawn)
    {
        if (!isInReverseMode || !awaitingTargetSelection) return;
        if (currentActorForSelection != null && clickedPawn == currentActorForSelection) return; // Tidak bisa target diri sendiri
        if (currentValidTargets == null || !currentValidTargets.Contains(clickedPawn)) return; // Target tidak valid

        selectedTargetForReverse = clickedPawn; // Kunci targetnya!
        awaitingTargetSelection = false; // Berhenti menunggu
        Debug.Log($"Pawn clicked accepted: {clickedPawn.name}");
    }

    // Membersihkan UI pilihan setelah selesai
    private void CleanupChoiceUI()
    {
        if (choicePanel != null) choicePanel.SetActive(false);
        foreach (var p in players) p.SetHighlight(false); // Matikan semua highlight

        awaitingTargetSelection = false;
        selectedTargetForReverse = null;
        currentActorForSelection = null;
        currentValidTargets.Clear();

        // Hubungkan kembali listener default ke tombol
        btnMoveSelf.onClick.RemoveAllListeners();
        btnReverse.onClick.RemoveAllListeners();
        if (btnCancelTargetSelect != null) btnCancelTargetSelect.onClick.RemoveAllListeners();

        btnMoveSelf.onClick.AddListener(OnChoice_MoveSelf);
        btnReverse.onClick.AddListener(OnChoice_Reverse);
        if (btnCancelTargetSelect != null) btnCancelTargetSelect.onClick.AddListener(OnChoice_Cancel);

        if (choiceInstructionText != null) { choiceInstructionText.gameObject.SetActive(false); choiceInstructionText.text = ""; }
    }

    // Mengubah UI ke mode "Pilih Target"
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
        if (infoText != null) infoText.text = "Pilih target (klik pawn) atau Cancel.";
    }

    // Mengembalikan UI ke mode "Pilih Aksi"
    void ExitReverseSelectionUI()
    {
        isInReverseMode = false;
        if (btnMoveSelf != null) btnMoveSelf.gameObject.SetActive(true);
        if (btnReverse != null) btnReverse.gameObject.SetActive(true);
        if (btnCancelTargetSelect != null) btnCancelTargetSelect.gameObject.SetActive(false);
        if (choiceInstructionText != null) { choiceInstructionText.gameObject.SetActive(false); choiceInstructionText.text = ""; }

        // Kembalikan highlight ke pemain saat ini
        if (turnOrder.Count > 0 && currentTurnIdx < turnOrder.Count)
            HighlightCurrentPlayer();
    }

    // Fungsi-fungsi yang terhubung ke tombol
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


    // ------------------------------------
    // --- FUNGSI BARU: SISTEM KARTU EFEK ---
    // ------------------------------------

    public bool IsPlayerTurn(PlayerPawn player) { /* ... */ return (turnOrder.Count > 0 && turnOrder[currentTurnIdx] == player); }
    public PlayerPawn GetCurrentPlayer() { /* ... */ return (turnOrder.Count > 0) ? turnOrder[currentTurnIdx] : null; }

    // Coroutine untuk alur "Pilih 1 dari 3"
    private IEnumerator ShowCardChoiceRoutine(PlayerPawn player)
    {
        isActionRunning = true;
        playerWaitingForCard = player;
        if (infoText != null) infoText.text = $"{player.name} sedang memilih Blessing...";

        List<CardData> cardSelection = cardManager.GetRandomCardSelection(3);
        cardUIManager.StartCardSelection(cardSelection);

        while (cardUIManager.cardChoicePanel.activeSelf)
        {
            yield return null;
        }

        if (infoText != null) infoText.text = $"Giliran {player.name} berlanjut.";
        playerWaitingForCard = null;
        isActionRunning = false;
    }

    // Dipanggil oleh UIManager.OnConfirmCardChoicePressed()
    public void AddChosenCardToPlayer(CardData chosenCard)
    {
        if (playerWaitingForCard != null)
        {
            playerWaitingForCard.heldCards.Add(new PlayerCardInstance(chosenCard, currentCycle));

            if (cardUIManager != null)
            {
                cardUIManager.DisplayPlayerHand(playerWaitingForCard);
            }
        }
    }

    // =============================================================
    // --- FUNGSI UTAMA: LOGIKA SEMUA KARTU ANDA ADA DI SINI ---
    // =============================================================
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

        if (cardUIManager != null)
        {
            cardUIManager.DisplayPlayerHand(user);
        }

        Debug.Log(user.name + " menggunakan kartu: " + card.cardName);

        // --- INI ADALAH LOGIKA YANG ANDA MINTA ---
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
        isActionRunning = false;
    }

    private IEnumerator Effect_TargetedMoveRoutine(PlayerPawn user, int moveAmount, string effectName)
    {
        isActionRunning = true;
        infoText.text = $"{user.name} menggunakan {effectName}! Pilih target.";
        List<PlayerPawn> validTargets = players.Where(p => p != user).ToList();

        if (effectName == "ZeusWrath") // ZeusWrath hanya bisa target pemain yang tidak kebal
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
        }
        else if (effectName == "LokiTricks")
        {
            infoText.text = $"{user.name} dan {target.name} bertukar posisi!";
            int userTile = user.currentTileID; int targetTile = target.currentTileID;
            yield return StartCoroutine(user.TeleportToTile(targetTile, (int id) => GetTilePositionWithOffset(id, user)));
            yield return StartCoroutine(target.TeleportToTile(userTile, (int id) => GetTilePositionWithOffset(id, target)));
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

    // --- FUNGSI LOGIKA BARU UNTUK THOR HAMMER ---
    private IEnumerator Effect_ThorHammerRoutine(PlayerPawn user, int moveAmount)
    {
        isActionRunning = true;
        infoText.text = "Mencari target Thor Hammer...";
        yield return new WaitForSeconds(0.5f);

        PlayerPawn target = null;
        int minDistance = int.MaxValue;

        // Cari pemain terdekat DI DEPAN
        foreach (PlayerPawn p in players)
        {
            if (p == user) continue;
            int distance = p.currentTileID - user.currentTileID;
            if (distance > 0 && distance < minDistance) // 'distance > 0' berarti "di depan"
            {
                minDistance = distance;
                target = p;
            }
        }

        if (target != null && target.immuneToReverseCycles <= 0) // Cek kebal
        {
            infoText.text = $"{target.name} terkena Thor Hammer! Mundur {moveAmount} petak!";
            int targetTile = Mathf.Max(1, target.currentTileID - moveAmount);
            yield return StartCoroutine(target.MoveToTile(targetTile, (int id) => GetTilePositionWithOffset(id, target)));
            yield return StartCoroutine(CheckLandingTile(target));
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

    // --- FUNGSI LOGIKA BARU UNTUK POSEIDON (PLACEHOLDER) ---
    private IEnumerator Effect_PoseidonWavesRoutine(PlayerPawn user, int moveAmount)
    {
        isActionRunning = true;
        infoText.text = "Poseidon Waves belum diimplementasi.";
        Debug.LogWarning("Logika Poseidon Waves sangat kompleks. Anda perlu:");
        Debug.LogWarning("1. Cara menentukan 'row' dari 'tileID'.");
        Debug.LogWarning("2. UI baru untuk memilih 'maju' atau 'mundur'.");
        yield return new WaitForSeconds(1f);
        isActionRunning = false;
    }
}