using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisualBridge : MonoBehaviour
{
    [Header("Links")]
    [Tooltip("Player yang akan dipantau (Drag Player GameObject ke sini)")]
    public PlayerState monitoredPlayer;

    [Tooltip("Script Hand Holder yang mengatur posisi visual kartu")]
    public CardHandHolder handHolder;

    void Start()
    {
        // 1. Cari Player Otomatis jika belum di-assign di Inspector
        if (monitoredPlayer == null)
            monitoredPlayer = FindObjectOfType<PlayerState>();

        if (monitoredPlayer != null && handHolder != null)
        {
            // 2. Kenalkan Owner ke Visual (Agar kartu tahu siapa pemiliknya)
            handHolder.SetOwner(monitoredPlayer);

            // 3. Buat Slot Kosong DI AWAL saja (Sekali seumur hidup)
            // Kita tidak me-reset slot di Update agar animasi mulus
            handHolder.CreateCardSlots(monitoredPlayer.maxHandSize);

            // 4. Subscribe ke Event Perubahan Data
            monitoredPlayer.OnStateChanged += SyncHandVisuals;

            // 5. Sinkronisasi Pertama Kali (Untuk memunculkan kartu awal)
            SyncHandVisuals(monitoredPlayer);
        }
        else
        {
            Debug.LogError("[VisualBridge] Error: PlayerState atau CardHandHolder belum di-assign!");
        }
    }

    void OnDestroy()
    {
        // Jangan lupa Unsubscribe saat objek hancur untuk mencegah memory leak
        if (monitoredPlayer != null)
            monitoredPlayer.OnStateChanged -= SyncHandVisuals;
    }

    // --- FUNGSI UTAMA: SMART SYNC ---
    // Fungsi ini dipanggil otomatis setiap kali ada perubahan di PlayerState (Add/Remove card)
    void SyncHandVisuals(PlayerState player)
    {
        if (handHolder == null) return;

        List<NewCardData> logicHand = player.hand;
        int maxSlots = player.maxHandSize;

        // Loop semua slot yang ada
        for (int i = 0; i < maxSlots; i++)
        {
            // Ambil referensi Slot visual di index ini
            CardSlot visualSlot = handHolder.GetHandSlotByIndex(i);

            // Ambil data Logic di index ini (jika ada)
            NewCardData logicData = (i < logicHand.Count) ? logicHand[i] : null;

            // --- CEK APAKAH VISUAL & LOGIC SINKRON? ---

            // Cek data kartu visual yang sedang tertempel di slot ini (jika ada)
            NewCardData currentVisualData = null;
            if (visualSlot.HasCard())
            {
                var cardVis = visualSlot.GetCard();
                if (cardVis != null)
                {
                    var cardComp = cardVis.GetComponent<GameCard>();
                    if (cardComp != null) currentVisualData = cardComp.GetCardData();
                }
            }

            // KASUS 1: Di Logic ADA kartu
            if (logicData != null)
            {
                // Tapi Visualnya SALAH atau KOSONG -> Kita perbaiki (Spawn Baru)
                if (currentVisualData != logicData)
                {
                    // Kalau slot ada isinya tapi salah data (misal ketimpa), hapus dulu
                    if (visualSlot.HasCard())
                    {
                        Destroy(visualSlot.GetCard().gameObject);
                        visualSlot.SetCard(null);
                    }

                    // Spawn kartu yang benar dari Template
                    handHolder.SpawnCard(logicData, i);
                }
                // Jika (currentVisualData == logicData), kita DIAMKAN SAJA.
                // Jangan di-destroy/spawn ulang agar tidak kedip/dobel.
            }

            // KASUS 2: Di Logic KOSONG (misal kartu sudah dipakai)
            else
            {
                // Tapi Visual MASIH ADA -> Kita Hapus (Garbage Collection)
                if (visualSlot.HasCard())
                {
                    Destroy(visualSlot.GetCard().gameObject);
                    visualSlot.SetCard(null);
                }
            }
        }
    }
}