using UnityEngine;
using TMPro;

public class PlayerUIEntry : MonoBehaviour
{
    // Referensi ke teks UI-nya
    public TextMeshProUGUI playerNameText;

    // Referensi ke RectTransform-nya sendiri
    private RectTransform rectTransform;

    // Variabel untuk menyimpan posisi X
    private float originalX;

    [Tooltip("Seberapa jauh UI 'maju' saat giliran aktif")]
    public float activeXOffset = 50f; // 50 pixel ke kanan

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalX = rectTransform.anchoredPosition.x;
    }

    /// <summary>
    /// Mengatur teks nama di UI
    /// </summary>
    public void Setup(string name)
    {
        if (playerNameText != null)
        {
            playerNameText.text = name;
        }
    }

    /// <summary>
    /// Mengatur apakah UI ini 'maju' (aktif) atau 'mundur' (normal)
    /// </summary>
    public void SetActive(bool isActive)
    {
        if (rectTransform == null) return;
        if (playerNameText.fontStyle == FontStyles.Strikethrough) return; // Jangan aktifkan jika sudah menang

        Vector2 currentPos = rectTransform.anchoredPosition;

        if (isActive)
        {
            // Geser ke KANAN (maju)
            rectTransform.anchoredPosition = new Vector2(originalX + activeXOffset, currentPos.y);
            playerNameText.fontStyle = FontStyles.Bold;
        }
        else
        {
            // Kembalikan ke posisi NORMAL
            rectTransform.anchoredPosition = new Vector2(originalX, currentPos.y);
            playerNameText.fontStyle = FontStyles.Normal;
        }
    }

    // --- FUNGSI BARU ---
    /// <summary>
    /// Dipanggil oleh UIManager saat pemain ini menang
    /// </summary>
    public void SetAsWinner()
    {
        if (playerNameText != null)
        {
            // 1. Set teks jadi abu-abu dan coret
            playerNameText.color = Color.gray;
            playerNameText.fontStyle = FontStyles.Strikethrough;
        }

        // 2. Pastikan posisinya kembali normal (tidak menjorok)
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(originalX, rectTransform.anchoredPosition.y);
        }
    }
    // -------------------
}