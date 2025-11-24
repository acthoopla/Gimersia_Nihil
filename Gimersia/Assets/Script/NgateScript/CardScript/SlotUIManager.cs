using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlotUIManager : MonoBehaviour
{
    [Header("References")]
    public PlayerState playerMonitored;
    public Transform slotContainer; // Panel Horizontal untuk 3 slot
    public GameObject cardDisplayPrefab;

    private void Start()
    {
        if (playerMonitored == null) playerMonitored = FindObjectOfType<PlayerState>();

        if (playerMonitored != null)
        {
            playerMonitored.OnStateChanged += UpdateSlotVisuals;
            UpdateSlotVisuals(playerMonitored);
        }
    }

    private void OnDestroy()
    {
        if (playerMonitored != null) playerMonitored.OnStateChanged -= UpdateSlotVisuals;
    }

    void UpdateSlotVisuals(PlayerState player)
    {
        // Bersihkan Slot Lama
        foreach (Transform child in slotContainer) Destroy(child.gameObject);

        // Render Kartu di Slot (List selectedCards)
        foreach (NewCardData cardData in player.selectedCards)
        {
            GameObject newCardObj = Instantiate(cardDisplayPrefab, slotContainer);

            NewCardDisplay cardScript = newCardObj.GetComponent<NewCardDisplay>();
            if (cardScript != null)
            {
                // TRUE parameter means "This is in Slot Mode"
                cardScript.Setup(cardData, player, true);
            }
        }
    }

    // Hubungkan fungsi ini ke Tombol "EXECUTE" di UI Canvas
    public void OnExecuteClick()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnExecuteButtonPressed();
        }
    }
}