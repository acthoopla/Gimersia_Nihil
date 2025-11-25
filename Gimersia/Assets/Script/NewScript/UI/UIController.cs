using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIController : MonoBehaviour
{
    [Header("References")]
    public Button goButton;
    public TextMeshProUGUI diceRollText;

    void Start()
    {
        if (goButton != null)
            goButton.onClick.AddListener(OnGoClicked);
    }

    void OnEnable()
    {
        var dice = FindObjectOfType<DiceController>();
        if (dice != null) dice.OnDiceResult += UpdateDiceText;
    }

    void OnDisable()
    {
        var dice = FindObjectOfType<DiceController>();
        if (dice != null) dice.OnDiceResult -= UpdateDiceText;
    }

    void UpdateDiceText(int result)
    {
        if (diceRollText != null) diceRollText.text = "Roll: " + result;
    }

    public void OnGoClicked()
    {
        // Panggil TurnManager untuk lanjut jalan
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnGoPressed();
        }
    }
}