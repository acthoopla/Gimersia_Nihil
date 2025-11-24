using UnityEngine;

[CreateAssetMenu(fileName = "New Card Data", menuName = "Card System/Card Data")]
public class CardDataSO : ScriptableObject
{
    [Header("Card Information")]
    [SerializeField] private string cardName;
    [SerializeField, TextArea(3, 6)] private string cardDescription;
    [SerializeField] private Sprite cardIcon;

    [Header("Card Prefab")]
    [SerializeField] private GameObject cardPrefab;

    public string CardName => cardName;
    public string CardDescription => cardDescription;
    public Sprite CardIcon => cardIcon;
    public GameObject CardPrefab => cardPrefab;
}
