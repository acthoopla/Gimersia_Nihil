using UnityEngine;

public abstract class BaseCard : MonoBehaviour
{
    [Header("Card Data")]
    // UBAH: Gunakan NewCardData (Logic + Visual)
    [SerializeField] protected NewCardData cardData;

    protected CardVisual cardVisual;
    protected bool isInitialized = false;

    // Tambahkan referensi Owner (Siapa yang mainin kartu?)
    protected PlayerState owner;

    #region Unity Methods
    protected virtual void Awake()
    {
        cardVisual = GetComponent<CardVisual>();
        if (cardVisual == null) Debug.LogError($"CardVisual component not found on {gameObject.name}!");
    }
    #endregion

    #region Initialization
    // UBAH: Parameter jadi NewCardData
    public virtual void Initialize(NewCardData data, PlayerState playerOwner)
    {
        cardData = data;
        owner = playerOwner; // Simpan owner

        if (cardData == null)
        {
            Debug.LogError($"Card data is null for {gameObject.name}!");
            return;
        }

        if (cardVisual != null)
        {
            // Set tampilan visual menggunakan data dari NewCardData
            cardVisual.SetCardData(cardData.CardName, cardData.CardDescription, cardData.CardIcon);
        }

        isInitialized = true;
        OnInitialized();
    }

    protected virtual void OnInitialized() { }
    #endregion

    #region Card Usage
    public abstract void Use(); // Implementasi ada di GameCard.cs

    public virtual bool CanUse()
    {
        return isInitialized && owner != null;
    }

    public virtual void OnPreUse() { }
    public virtual void OnPostUse()
    {
        // Opsional: Hancurkan gameobject setelah animasi selesai
        Destroy(gameObject);
    }
    #endregion

    #region Public Methods
    public NewCardData GetCardData() => cardData;
    public string GetCardName() => cardData != null ? cardData.CardName : "Unknown";

    public NewCardData GetData()
    {
        return cardData;
    }

    // Helper untuk integrasi logika klik
    public PlayerState GetOwner() => owner;
    #endregion
}
