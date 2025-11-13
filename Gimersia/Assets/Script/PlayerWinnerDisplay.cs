using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerWinnerDisplay : MonoBehaviour
{
    [Header("UI References")]
    public Image crownIcon;
    public TextMeshProUGUI rankText;
    public TextMeshProUGUI playerNameText;

    public void Setup(string playerName, int rank, Sprite crownSprite)
    {
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        if (rankText != null)
        {
            rankText.text = $"#{rank}";
        }

        if (crownIcon != null)
        {
            if (crownSprite != null)
            {
                crownIcon.sprite = crownSprite;
                crownIcon.gameObject.SetActive(true);
            }
            else
            {
                crownIcon.gameObject.SetActive(false);
            }
        }
    }

    public void SetupAsLoser(string playerName, int rank)
    {
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        if (rankText != null)
        {
            rankText.text = $"#{rank}";
        }

        if (crownIcon != null)
        {
            crownIcon.gameObject.SetActive(false);
        }
    }
}
