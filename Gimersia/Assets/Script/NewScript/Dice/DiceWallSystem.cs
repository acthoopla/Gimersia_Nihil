using UnityEngine;

/// <summary>
/// DiceWallSystem
/// Tugas: Menyalakan tembok penghalang saat dadu dilempar, 
/// dan mematikannya saat dadu berhenti.
/// </summary>
public class DiceWallSystem : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag GameObject Tembok/Barrier di sini")]
    public GameObject wallObject;

    [Tooltip("Drag object yang ada script DiceController")]
    public DiceController diceController;

    void Start()
    {
        // Auto-find jika lupa drag di inspector
        if (diceController == null) diceController = FindObjectOfType<DiceController>();

        if (diceController != null)
        {
            // Subscribe ke event dadu
            diceController.OnDiceThrown += EnableWall;   // Saat dilempar -> Nyala
            diceController.OnDiceResult += DisableWall;  // Saat berhenti (dapat hasil) -> Mati
        }
        else
        {
            Debug.LogWarning("[DiceWallSystem] DiceController tidak ditemukan!");
        }

        // Pastikan kondisi awal mati (supaya player bisa klik dadu tanpa terhalang tembok)
        if (wallObject != null) wallObject.SetActive(false);
    }

    void OnDestroy()
    {
        // Wajib unsubscribe saat object hancur untuk mencegah memory leak
        if (diceController != null)
        {
            diceController.OnDiceThrown -= EnableWall;
            diceController.OnDiceResult -= DisableWall;
        }
    }

    // Dipanggil saat Event OnDiceThrown
    private void EnableWall()
    {
        if (wallObject != null)
        {
            wallObject.SetActive(true);
            // Debug.Log("[DiceWallSystem] Wall ON");
        }
    }

    // Dipanggil saat Event OnDiceResult (butuh parameter int karena signature eventnya Action<int>)
    private void DisableWall(int result)
    {
        if (wallObject != null)
        {
            wallObject.SetActive(false);
            // Debug.Log("[DiceWallSystem] Wall OFF");
        }
    }
}