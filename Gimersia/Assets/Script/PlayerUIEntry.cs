using System.Collections;
using System.Collections.Generic;
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
        // Ambil komponen RectTransform
        rectTransform = GetComponent<RectTransform>();

        // Simpan posisi X normalnya
        // (Vertical Layout Group akan mengatur Y, kita hanya peduli X)
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

        Vector2 currentPos = rectTransform.anchoredPosition;

        if (isActive)
        {
            // Geser ke KANAN (maju)
            rectTransform.anchoredPosition = new Vector2(originalX + activeXOffset, currentPos.y);
            playerNameText.fontStyle = FontStyles.Bold; // Bonus: bikin tebal
        }
        else
        {
            // Kembalikan ke posisi NORMAL
            rectTransform.anchoredPosition = new Vector2(originalX, currentPos.y);
            playerNameText.fontStyle = FontStyles.Normal; // Bonus: bikin normal
        }
    }
}
