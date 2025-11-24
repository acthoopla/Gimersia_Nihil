using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandUIManager : MonoBehaviour
{
    [Header("References")]
    public PlayerState playerMonitored;
    public Transform handContainer;
    public GameObject cardDisplayPrefab; // Prefab dgn script CardDisplay

    private void Start()
    {
        if (playerMonitored == null)
            playerMonitored = FindObjectOfType<PlayerState>();

        if (playerMonitored != null)
        {
            playerMonitored.OnStateChanged += UpdateHandVisuals;
            UpdateHandVisuals(playerMonitored);
        }
    }

    private void OnDestroy()
    {
        if (playerMonitored != null)
            playerMonitored.OnStateChanged -= UpdateHandVisuals;
    }

    void UpdateHandVisuals(PlayerState player)
    {
        foreach (Transform child in handContainer) Destroy(child.gameObject);

        foreach (NewCardData cardData in player.hand)
        {
            GameObject newCardObj = Instantiate(cardDisplayPrefab, handContainer);

            // Pastikan ambil komponen yang benar (NewCardDisplay)
            NewCardDisplay cardScript = newCardObj.GetComponent<NewCardDisplay>();

            if (cardScript != null)
            {
                // FALSE parameter means "This is Hand Mode"
                cardScript.Setup(cardData, player, false);
            }
        }
    }
}
