using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerCardInstance
{
    public CardData cardData;
    public int cycleAcquired;

    public PlayerCardInstance(CardData data, int cycle)
    {
        cardData = data;
        cycleAcquired = cycle;
    }
}
