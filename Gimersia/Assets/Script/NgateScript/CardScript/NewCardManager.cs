using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static NewCardSystem;

public class NewCardManager : MonoBehaviour
{
    [Header("Library Kartu")]
    public List<NewCardData> allAvailableCards; // Masukkan semua ScriptableObject kartu di sini

    [Header("Settings")]
    public int initialDrawCount = 3; // ATURAN: Mulai dengan 3 kartu

    // Singleton
    public static NewCardManager Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Otomatis cari player di scene saat game mulai
        var player = FindObjectOfType<PlayerState>();

        if (player != null)
        {
            StartCoroutine(GiveStartingCards(player));
        }
        else
        {
            Debug.LogWarning("PlayerState tidak ditemukan di scene!");
        }
    }

    // Beri jeda sedikit agar aman
    private IEnumerator GiveStartingCards(PlayerState player)
    {
        yield return new WaitForSeconds(0.1f); // Delay kecil untuk memastikan inisialisasi lain selesai

        Debug.Log("Membagikan kartu awal...");
        for (int i = 0; i < initialDrawCount; i++)
        {
            DrawRandomCard(player);
        }
    }

    public void DrawRandomCard(PlayerState player)
    {
        // Cek Library kosong
        if (allAvailableCards.Count == 0) return;

        // Cek apakah tangan player penuh (SRP: Kita tanya PlayerState, bukan hitung sendiri)
        if (player.IsHandFull)
        {
            Debug.Log("Deck Manager: Tangan player penuh, stop draw.");
            return;
        }

        // Acak Kartu
        NewCardData randomCard = allAvailableCards[Random.Range(0, allAvailableCards.Count)];

        // Coba masukkan ke tangan player
        if (player.TryAddCard(randomCard))
        {
            Debug.Log($"Kartu didapat: {randomCard.cardName}");
        }
    }

    public NewCardData GetRandomCardByCategory(CardCategory category)
    {
        // Filter kartu yang kategorinya cocok
        var filteredCards = allAvailableCards.Where(c => c.category == category).ToList();

        if (filteredCards.Count == 0)
        {
            Debug.LogWarning($"[NewCardManager] Tidak ada kartu dengan kategori {category} di library!");
            return null;
        }

        return filteredCards[Random.Range(0, filteredCards.Count)];
    }

    /// <summary>
    /// Mengambil 1 kartu random dari kategori APAPUN.
    /// </summary>
    public NewCardData GetRandomCardAny()
    {
        if (allAvailableCards.Count == 0) return null;
        return allAvailableCards[Random.Range(0, allAvailableCards.Count)];
    }
}