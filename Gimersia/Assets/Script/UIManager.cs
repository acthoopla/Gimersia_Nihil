using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("Referensi UI")]
    public GameObject playerUIEntryPrefab; // Prefab [Player 1] yang kita buat
    public Transform playerUIContainer;    // Panel "PlayerTurnPanel" (Vertical Layout Group)

    // Daftar untuk menyimpan referensi ke semua script UI yang di-spawn
    private List<PlayerUIEntry> uiEntries = new List<PlayerUIEntry>();

    /// <summary>
    /// Dipanggil oleh MultiplayerManager *SATU KALI* saat giliran ditentukan.
    /// </summary>
    public void SetupPlayerList(List<PlayerPawn> playerTurnOrder)
    {
        // Bersihkan daftar lama jika ada
        foreach (Transform child in playerUIContainer)
        {
            Destroy(child.gameObject);
        }
        uiEntries.Clear();

        // Buat (spawn) UI entry baru untuk setiap pemain
        foreach (PlayerPawn player in playerTurnOrder)
        {
            // Instantiate prefab-nya dan pasang di container
            GameObject entryGO = Instantiate(playerUIEntryPrefab, playerUIContainer);

            // Ambil script-nya
            PlayerUIEntry entryScript = entryGO.GetComponent<PlayerUIEntry>();

            if (entryScript != null)
            {
                // Set nama UI-nya (misal: "Player 1")
                entryScript.Setup(player.name);

                // Simpan script ini ke daftar kita
                uiEntries.Add(entryScript);
            }
        }
    }

    /// <summary>
    /// Dipanggil oleh MultiplayerManager *SETIAP AWAL GILIRAN*.
    /// </summary>
    public void UpdateActivePlayer(int activeIndex)
    {
        // Loop semua UI entry yang sudah kita simpan
        for (int i = 0; i < uiEntries.Count; i++)
        {
            // Jika index-nya cocok dengan giliran sekarang
            if (i == activeIndex)
            {
                // Suruh dia "maju"
                uiEntries[i].SetActive(true);
            }
            else
            {
                // Suruh dia "mundur" ke posisi normal
                uiEntries[i].SetActive(false);
            }
        }
    }
}
