using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("Game Elements")]
    public Transform playerPiece;      // Kapsul (Player)

    [Header("Board Settings")]
    [Tooltip("Jumlah total tile di papanmu. Penting untuk validasi & menang.")]
    public int totalTilesInBoard = 100;

    [Header("UI")]
    public Button rollButton;
    public TextMeshProUGUI diceText;

    // --- Private Variables ---
    // DIUBAH: Kita ganti array dengan Dictionary.
    // Key = tileID (1-100), Value = Komponen Tile
    private Dictionary<int, Tiles> boardMap = new Dictionary<int, Tiles>();

    // DIUBAH: Kita gunakan tileID (1-based)
    private int currentPlayerTileID = 1;
    private bool isMoving = false;

    // DIHAPUS: Variabel prefab, boardSize, dll. sudah tidak perlu.

    void Start()
    {
        // BARU: Panggil fungsi untuk memetakan papan
        BuildBoardMap();

        // DIUBAH: Mulai dari tile ID 1
        PlacePlayerAt(1);

        rollButton.onClick.AddListener(OnRollDicePressed);
    }

    /// <summary>
    /// BARU: Fungsi ini mencari semua Tile di scene dan memvalidasinya.
    /// </summary>
    void BuildBoardMap()
    {
        Debug.Log("Mulai memetakan papan...");
        Tiles[] allTiles = FindObjectsOfType<Tiles>(); // Temukan semua script Tile

        bool errorFound = false;
        foreach (Tiles tile in allTiles)
        {
            if (boardMap.ContainsKey(tile.tileID))
            {
                Debug.LogError($"ERROR: Tile ID duplikat! ID {tile.tileID} ada di {tile.gameObject.name} dan {boardMap[tile.tileID].gameObject.name}", tile.gameObject);
                errorFound = true;
            }
            else
            {
                boardMap.Add(tile.tileID, tile);
            }
        }

        if (boardMap.Count != totalTilesInBoard)
        {
            Debug.LogError($"ERROR: Jumlah tile tidak cocok! Diharapkan {totalTilesInBoard}, tapi hanya ditemukan {boardMap.Count} tile unik.");
            errorFound = true;
        }

        if (!boardMap.ContainsKey(1))
        {
            Debug.LogError($"ERROR: Tile Start (ID 1) tidak ditemukan!");
            errorFound = true;
        }

        if (errorFound)
        {
            Debug.LogError("Papan GAGAL dimuat karena ada error. Mohon cek pesan error di atas.");
            rollButton.interactable = false; // Matikan game
            diceText.text = "Error Papan!";
        }
        else
        {
            Debug.Log($"Sukses! Papan {boardMap.Count} tile berhasil dimuat.");
        }
    }


    /// <summary>
    /// DIUBAH: Sekarang menggunakan tileID dan mengambil posisi dari Tile.
    /// </summary>
    void PlacePlayerAt(int tileID)
    {
        if (!boardMap.ContainsKey(tileID))
        {
            Debug.LogError($"Tidak bisa menemukan tile dengan ID {tileID}!");
            return;
        }

        currentPlayerTileID = tileID;
        playerPiece.position = boardMap[tileID].GetPlayerPosition();
    }

    /// <summary>
    /// Fungsi ini tidak banyak berubah.
    /// </summary>
    void OnRollDicePressed()
    {
        if (isMoving) return;

        int roll = Random.Range(1, 7); // Hasil 1-6
        diceText.text = $"Kamu dapat angka {roll}!";

        StartCoroutine(MovePlayerRoutine(roll));
    }

    /// <summary>
    /// DIUBAH: Logika gerakan disesuaikan untuk tileID dan boardMap.
    /// </summary>
    /// <summary>
    /// DIUBAH: Logika gerakan disesuaikan untuk aturan "Bounce Back".
    /// </summary>
    IEnumerator MovePlayerRoutine(int steps)
    {
        isMoving = true;
        rollButton.interactable = false;

        int finalTargetTileID = currentPlayerTileID + steps;
        bool needsToCheckSpecialTiles = true;

        // --- Cek Kemenangan & Bounce Back ---
        if (finalTargetTileID == totalTilesInBoard) // TEPAT MENANG
        {
            // Gerak maju seperti biasa
            yield return StartCoroutine(AnimateMove(finalTargetTileID, false));

            diceText.text = "KAMU MENANG!";
            Debug.Log("Game Over - Player Wins!");

            needsToCheckSpecialTiles = false; // Game berakhir, tidak perlu cek ular/tangga
            yield break; // Hentikan coroutine
        }
        else if (finalTargetTileID > totalTilesInBoard) // MELEBIHI (Bounce Back)
        {
            diceText.text = "Terlalu jauh! Mundur...";

            int overshoot = finalTargetTileID - totalTilesInBoard;
            finalTargetTileID = totalTilesInBoard - overshoot; // Ini target akhir setelah mundur

            // 1. Animasikan gerakan MAJU sampai ke petak 100
            yield return StartCoroutine(AnimateMove(totalTilesInBoard, false));

            yield return new WaitForSeconds(0.3f); // Jeda sebentar di petak 100

            // 2. Animasikan gerakan MUNDUR ke target akhir
            yield return StartCoroutine(AnimateMoveBackward(finalTargetTileID));

            // Tetap perlu cek ular/tangga di petak 97 (misalnya)
            needsToCheckSpecialTiles = true;
        }
        else // Gerakan Normal (belum menang)
        {
            // Gerak maju seperti biasa
            yield return StartCoroutine(AnimateMove(finalTargetTileID, false));
            needsToCheckSpecialTiles = true;
        }


        // --- Cek Ular atau Tangga (HANYA jika game belum berakhir) ---
        if (needsToCheckSpecialTiles)
        {
            // 'currentPlayerTileID' sudah di-update oleh AnimateMove atau AnimateMoveBackward
            Tiles landedTile = boardMap[currentPlayerTileID];

            if (landedTile.type == TileType.LadderStart)
            {
                diceText.text = "Naik Tangga!";
                yield return new WaitForSeconds(0.3f);
                int specialTargetID = landedTile.targetTile.tileID;
                yield return StartCoroutine(AnimateMove(specialTargetID, true)); // 'true' untuk teleport
            }
            else if (landedTile.type == TileType.SnakeStart)
            {
                diceText.text = "Turun Ular!";
                yield return new WaitForSeconds(0.3f);
                int specialTargetID = landedTile.targetTile.tileID;
                yield return StartCoroutine(AnimateMove(specialTargetID, true)); // 'true' untuk teleport
            }
        }

        // Hanya aktifkan tombol jika game belum dimenangkan
        if (currentPlayerTileID != totalTilesInBoard)
        {
            isMoving = false;
            rollButton.interactable = true;
        }
    }

    /// <summary>
    /// DIUBAH: Logika animasi disesuaikan untuk boardMap.
    /// </summary>
    IEnumerator AnimateMove(int targetTileID, bool isTeleport)
    {
        float moveSpeed = isTeleport ? 15f : 5f; // Gerakan ular/tangga lebih cepat
        int startTileID = currentPlayerTileID;

        // Jika gerakan normal, bergerak petak per petak
        if (!isTeleport)
        {
            for (int i = startTileID + 1; i <= targetTileID; i++)
            {
                Vector3 targetPos = boardMap[i].GetPlayerPosition();
                while (Vector3.Distance(playerPiece.position, targetPos) > 0.01f)
                {
                    playerPiece.position = Vector3.MoveTowards(playerPiece.position, targetPos, moveSpeed * Time.deltaTime);
                    yield return null;
                }
                playerPiece.position = targetPos; // Pastikan presisi
                currentPlayerTileID = i; // Update posisi saat ini
                yield return new WaitForSeconds(0.1f); // Jeda antar tile
            }
        }
        // Jika gerakan spesial (ular/tangga), langsung meluncur
        else
        {
            Vector3 targetPos = boardMap[targetTileID].GetPlayerPosition();
            while (Vector3.Distance(playerPiece.position, targetPos) > 0.01f)
            {
                playerPiece.position = Vector3.MoveTowards(playerPiece.position, targetPos, moveSpeed * Time.deltaTime);
                yield return null;
            }
            playerPiece.position = targetPos;
            currentPlayerTileID = targetTileID; // Langsung update posisi
        }
    }

    /// <summary>
    /// BARU: Coroutine untuk menganimasikan gerakan mundur (saat bounce back).
    /// </summary>
    IEnumerator AnimateMoveBackward(int targetTileID)
    {
        float moveSpeed = 5f; // Kecepatan gerak mundur
        int startTileID = currentPlayerTileID; // (Posisi saat ini, harusnya 100)

        // Loop mundur dari petak saat ini ke petak tujuan
        for (int i = startTileID - 1; i >= targetTileID; i--)
        {
            Vector3 targetPos = boardMap[i].GetPlayerPosition();
            while (Vector3.Distance(playerPiece.position, targetPos) > 0.01f)
            {
                playerPiece.position = Vector3.MoveTowards(playerPiece.position, targetPos, moveSpeed * Time.deltaTime);
                yield return null;
            }
            playerPiece.position = targetPos; // Pastikan presisi
            currentPlayerTileID = i; // Update posisi saat ini
            yield return new WaitForSeconds(0.1f); // Jeda antar tile
        }
    }
}