using UnityEngine;

public abstract class BaseCard : MonoBehaviour
{
    [Header("Card Data")]
    [SerializeField] protected CardDataSO cardData;

    protected CardVisual cardVisual;
    protected bool isInitialized = false;

    #region Unity Methods
    protected virtual void Awake()
    {
        cardVisual = GetComponent<CardVisual>();

        if (cardVisual == null)
        {
            Debug.LogError($"CardVisual component not found on {gameObject.name}!");
        }
    }
    #endregion

    #region Initialization
    public virtual void Initialize(CardDataSO data)
    {
        cardData = data;

        if (cardData == null)
        {
            Debug.LogError($"Card data is null for {gameObject.name}!");
            return;
        }

        if (cardVisual != null)
        {
            cardVisual.SetCardData(cardData.CardName, cardData.CardDescription, cardData.CardIcon);
        }

        isInitialized = true;
        OnInitialized();
    }

    protected virtual void OnInitialized()
    {
        // Override di child class jika perlu
    }
    #endregion

    #region Card Usage
    public abstract void Use();

    public virtual bool CanUse()
    {
        return isInitialized;
    }

    public virtual void OnPreUse()
    {
        // Override di child class jika perlu
    }

    public virtual void OnPostUse()
    {
        // Override di child class jika perlu
    }
    #endregion

    #region Public Mehtods
    public CardDataSO GetCardData() => cardData;
    public string GetCardName() => cardData != null ? cardData.CardName : "Unknown";
    public string GetCardDescription() => cardData != null ? cardData.CardDescription : "";
    public Sprite GetCardIcon() => cardData != null ? cardData.CardIcon : null;
    public CardVisual GetCardVisual() => cardVisual;
    #endregion
}
