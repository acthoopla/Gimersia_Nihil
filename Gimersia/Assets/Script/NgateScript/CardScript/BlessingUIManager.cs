using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BlessingUIManager : MonoBehaviour
{
    public static BlessingUIManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject blessingPanel; // Panel Hitam/Background UI
    public Transform cardsContainer; // Tempat spawn 3 kartu (Horizontal Layout Group)
    public GameObject cardDisplayPrefab; // Prefab UI Kartu (yang ada script NewCardDisplay)

    [Header("Settings")]
    public float showDelay = 0.5f;

    private Action onChoiceMadeCallback; // Fungsi untuk melanjutkan game setelah milih

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Sembunyikan panel saat awal
        if (blessingPanel) blessingPanel.SetActive(false);
    }

    /// <summary>
    /// Memunculkan UI Blessing dengan 3 pilihan kartu spesifik.
    /// </summary>
    public void ShowBlessingChoice(PlayerState player, NewCardData c1, NewCardData c2, NewCardData c3, Action onComplete)
    {
        if (blessingPanel == null) return;

        onChoiceMadeCallback = onComplete;

        // Bersihkan kartu lama di container
        foreach (Transform child in cardsContainer) Destroy(child.gameObject);

        // List kartu untuk di-loop
        List<NewCardData> choices = new List<NewCardData> { c1, c2, c3 };

        // Spawn 3 Kartu
        foreach (var cardData in choices)
        {
            if (cardData == null) continue;

            GameObject cardObj = Instantiate(cardDisplayPrefab, cardsContainer);

            // Setup Visual menggunakan NewCardDisplay
            NewCardDisplay display = cardObj.GetComponent<NewCardDisplay>();
            if (display != null)
            {
                // Kita gunakan mode Slot (true) atau modif sedikit agar bisa diklik untuk dipilih
                // Di sini saya asumsikan NewCardDisplay kamu punya event click.
                // TAPI, NewCardDisplay kamu saat ini logic kliknya untuk Pindah Slot/Hand.
                // KITA BUTUH MODIFIKASI SEDIKIT DI SINI.

                // Cara Cepat: Tambahkan Button component di runtime atau gunakan EventTrigger
                Button btn = cardObj.GetComponent<Button>();
                if (btn == null) btn = cardObj.AddComponent<Button>();

                // Saat diklik -> Pilih kartu ini
                btn.onClick.AddListener(() => OnCardPicked(player, cardData));

                // Setup visual dasar
                display.Setup(cardData, player, false);
            }
        }

        blessingPanel.SetActive(true);
    }

    void OnCardPicked(PlayerState player, NewCardData pickedCard)
    {
        Debug.Log($"[Blessing] Player memilih: {pickedCard.cardName}");

        // 1. Masukkan kartu ke tangan player
        if (player.TryAddCard(pickedCard))
        {
            // Berhasil
        }
        else
        {
            Debug.LogWarning("Hand penuh! Kartu hangus.");
        }

        // 2. Tutup Panel
        blessingPanel.SetActive(false);

        // 3. Lanjutkan Game (Panggil TileEffectSystem agar TurnManager lanjut)
        onChoiceMadeCallback?.Invoke();
    }
}