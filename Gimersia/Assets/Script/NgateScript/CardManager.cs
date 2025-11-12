using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    [Tooltip("Tarik SEMUA aset CardData Anda ke list ini")]
    public List<CardData> allBlessingCards;

    // Fungsi ini dipanggil oleh MultiplayerManager
    public void GiveRandomCardToPlayer(PlayerPawn player, int currentCycle)
    {
        if (allBlessingCards.Count == 0)
        {
            Debug.LogError("Deck kosong! Tidak bisa mengambil kartu.");
            return;
        }

        int randomIndex = Random.Range(0, allBlessingCards.Count);
        CardData cardToGive = allBlessingCards[randomIndex];

        PlayerCardInstance newCardInstance = new PlayerCardInstance(cardToGive, currentCycle);
        player.heldCards.Add(newCardInstance);

        Debug.Log(player.name + " mendapatkan kartu: " + cardToGive.cardName);
    }

    // Mengambil beberapa kartu acak unik dari deck
    public List<CardData> GetRandomCardSelection(int count)
    {
        List<CardData> selection = new List<CardData>();

        // Buat salinan sementara dari deck agar kita bisa "mengambil" tanpa merusak aslinya
        List<CardData> tempDeck = allBlessingCards.ToList();

        for (int i = 0; i < count; i++)
        {
            // Jika deck habis, berhenti
            if (tempDeck.Count == 0)
                break;

            int randomIndex = Random.Range(0, tempDeck.Count);
            CardData chosenCard = tempDeck[randomIndex];

            selection.Add(chosenCard);
            tempDeck.RemoveAt(randomIndex); // Hapus dari deck sementara agar tidak terambil lagi
        }

        return selection;
    }
}