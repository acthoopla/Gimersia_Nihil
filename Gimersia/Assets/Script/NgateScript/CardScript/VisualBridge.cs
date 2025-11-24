using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisualBridge : MonoBehaviour
{
    [Header("Links")]
    public PlayerState monitoredPlayer;
    public CardHandHolder handHolder;

    void Start()
    {
        if (monitoredPlayer == null)
            monitoredPlayer = FindObjectOfType<PlayerState>();

        if (monitoredPlayer != null && handHolder != null)
        {
            // 1. Kenalkan Owner ke Visual
            handHolder.SetOwner(monitoredPlayer);

            // 2. Buat Slot Kosong DI AWAL saja (jangan di-loop update)
            handHolder.CreateCardSlots(monitoredPlayer.maxHandSize);

            // 3. Subscribe Event
            monitoredPlayer.OnStateChanged += SyncHandVisuals;

            // 4. Sync Awal
            SyncHandVisuals(monitoredPlayer);
        }
    }

    void OnDestroy()
    {
        if (monitoredPlayer != null) monitoredPlayer.OnStateChanged -= SyncHandVisuals;
    }

    // Fungsi Smart Sync (Hanya update perbedaan)
    void SyncHandVisuals(PlayerState player)
    {
        if (handHolder == null) return;

        List<NewCardData> logicHand = player.hand;
        int maxSlots = player.maxHandSize;

        for (int i = 0; i < maxSlots; i++)
        {
            // Ambil referensi Slot visual dan Data logic di index ini
            CardSlot visualSlot = handHolder.GetHandSlotByIndex(i);
            NewCardData logicData = (i < logicHand.Count) ? logicHand[i] : null;

            // --- CEK APAKAH VISUAL & LOGIC SINKRON? ---

            // Cek data visual saat ini (jika ada)
            NewCardData currentVisualData = null;
            if (visualSlot.HasCard())
            {
                // Ambil data dari kartu yang tertempel
                var cardVis = visualSlot.GetCard();
                if (cardVis != null)
                {
                    var cardComp = cardVis.GetComponent<GameCard>();
                    if (cardComp != null) currentVisualData = cardComp.GetCardData();
                }
            }

            // KONDISI 1: Logic ada kartu, tapi Visual kosong/beda -> SPAWN
            if (logicData != null)
            {
                if (currentVisualData != logicData)
                {
                    // Kalau slot ada isinya tapi salah data, hapus dulu
                    if (visualSlot.HasCard())
                    {
                        Destroy(visualSlot.GetCard().gameObject);
                        visualSlot.SetCard(null);
                    }

                    // Spawn kartu yang benar
                    handHolder.SpawnCard(logicData, i);
                }
                // Jika sama, biarkan saja (jangan destroy/spawn ulang!)
            }
            // KONDISI 2: Logic kosong (sudah dipakai), tapi Visual masih ada -> HAPUS
            else
            {
                if (visualSlot.HasCard())
                {
                    // Hapus visualnya
                    Destroy(visualSlot.GetCard().gameObject);
                    visualSlot.SetCard(null);
                }
            }
        }
    }
}