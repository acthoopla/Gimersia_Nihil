using System.Collections.Generic;

[System.Serializable]
public class PlayerData
{
    public string playerName;
    public List<CardData> heldCards; // Ini adalah "penyimpanan" kartu untuk pemain

    public PlayerData(string name)
    {
        playerName = name;
        heldCards = new List<CardData>();
    }
}