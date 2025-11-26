using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static NewCardSystem;

public class BlessingUIManager : MonoBehaviour
{
    public static BlessingUIManager Instance { get; private set; }

    [Header("UI References (3 Card Choice)")]
    public GameObject blessingPanel;
    public Transform cardsContainer;
    public GameObject cardDisplayPrefab;

    // --- REFERENSI UI KATEGORI (Untuk Tangga) ---
    [Header("UI References (Category Choice)")]
    public GameObject categoryPanel; // Panel baru untuk milih tipe
    public Button movementBtn;
    public Button buffBtn;
    // -------------------------------------------

    private Action onChoiceMadeCallback;
    private Action<CardCategory> onCategorySelectedCallback;

    void Awake()
    {
        // Singleton Setup yang Benar
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (blessingPanel) blessingPanel.SetActive(false);
        if (categoryPanel) categoryPanel.SetActive(false);

        // Setup Button Listeners untuk Kategori
        if (movementBtn) movementBtn.onClick.AddListener(() => OnCategoryClicked(CardCategory.Movement));
        if (buffBtn) buffBtn.onClick.AddListener(() => OnCategoryClicked(CardCategory.Buff));
    }

    // --- FUNGSI 1: LOGIC PILIH 3 KARTU (Blessing Card) ---
    public void ShowBlessingChoice(PlayerState player, NewCardData c1, NewCardData c2, NewCardData c3, Action onComplete)
    {
        if (blessingPanel == null) return;
        onChoiceMadeCallback = onComplete;

        foreach (Transform child in cardsContainer) Destroy(child.gameObject);

        List<NewCardData> choices = new List<NewCardData> { c1, c2, c3 };
        foreach (var cardData in choices)
        {
            if (cardData == null) continue;
            GameObject cardObj = Instantiate(cardDisplayPrefab, cardsContainer);

            // Kita pakai NewCardDisplay untuk visual
            NewCardDisplay display = cardObj.GetComponent<NewCardDisplay>(); // Pastikan script ini ada di prefab

            if (display != null)
            {
                // Tambahkan tombol klik manual jika NewCardDisplay tidak handle klik pemilihan
                Button btn = cardObj.GetComponent<Button>();
                if (btn == null) btn = cardObj.AddComponent<Button>();

                btn.onClick.AddListener(() => OnCardPicked(player, cardData));

                // Setup tampilan (false = mode hand biasa, tidak di slot)
                display.Setup(cardData, player, false);
            }
        }
        blessingPanel.SetActive(true);
    }

    void OnCardPicked(PlayerState player, NewCardData pickedCard)
    {
        if (player.TryAddCard(pickedCard))
        {
            Debug.Log($"[Blessing] Player mengambil: {pickedCard.cardName}");
        }
        else
        {
            Debug.LogWarning("Hand penuh!");
        }
        blessingPanel.SetActive(false);
        onChoiceMadeCallback?.Invoke();
    }

    // --- FUNGSI 2: LOGIC PILIH KATEGORI (Untuk Tangga / Ladder) ---
    // INI FUNGSI YANG SEBELUMNYA HILANG (FIX CS1061)
    public void ShowCategoryChoice(Action<CardCategory> callback)
    {
        if (categoryPanel == null)
        {
            Debug.LogWarning("Category Panel belum di-assign di Inspector BlessingUIManager!");
            // Jika lupa assign, panggil callback dengan default biar game ga macet
            callback?.Invoke(CardCategory.Movement);
            return;
        }

        onCategorySelectedCallback = callback;
        categoryPanel.SetActive(true);
    }

    private void OnCategoryClicked(CardCategory category)
    {
        Debug.Log($"[UI] Player memilih kategori: {category}");
        categoryPanel.SetActive(false);
        onCategorySelectedCallback?.Invoke(category);
    }
}