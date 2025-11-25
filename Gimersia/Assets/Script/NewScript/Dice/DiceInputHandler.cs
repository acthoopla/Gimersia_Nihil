using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(DiceController))]
public class DiceInputHandler : MonoBehaviour
{
    [Header("Throw Settings")]
    public float flickSensitivity = 1.5f; // Dari kode lama
    public float torqueAmount = 10f;      // Dari kode lama
    public float dragSpinSpeed = 200f;    // Dari kode lama

    private DiceController diceController;
    private Camera mainCamera;

    // Logic variable dari kode lama
    private bool isDragging = false;
    private Plane dragPlane;
    private Vector3 dragOffset;
    private Vector3 lastPosition;
    private List<Vector3> velocityHistory = new List<Vector3>(5);

    // Property untuk mematikan input (dikontrol TurnManager)
    public bool InputEnabled { get; set; } = true;

    void Awake()
    {
        diceController = GetComponent<DiceController>();
        mainCamera = Camera.main;
    }

    void OnMouseDown()
    {
        // Cek apakah input boleh atau dadu sedang menggelinding
        if (!InputEnabled || diceController.IsRolling) return;

        isDragging = true;

        // --- LOGIKA PLANE DARI KODE LAMA ---
        // Membuat bidang datar imajiner menghadap kamera di posisi dadu
        dragPlane = new Plane(mainCamera.transform.forward, transform.position);

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (dragPlane.Raycast(ray, out float distance))
        {
            // Hitung offset agar dadu tidak "snap" ke tengah mouse
            dragOffset = transform.position - ray.GetPoint(distance);
        }

        lastPosition = transform.position;
        velocityHistory.Clear();
    }

    void OnMouseDrag()
    {
        if (!isDragging) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (dragPlane.Raycast(ray, out float distance))
        {
            // Pindahkan dadu sesuai posisi mouse di plane
            Vector3 newPos = ray.GetPoint(distance) + dragOffset;
            transform.position = newPos;

            // --- VISUAL SPIN DARI KODE LAMA ---
            transform.Rotate(Vector3.up, dragSpinSpeed * Time.deltaTime, Space.World);
            transform.Rotate(Vector3.right, dragSpinSpeed * 0.8f * Time.deltaTime, Space.Self);

            // Hitung velocity untuk flick
            Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
            lastPosition = transform.position;

            // Simpan history kecepatan (maks 5 frame)
            velocityHistory.Add(velocity);
            if (velocityHistory.Count > 5)
            {
                velocityHistory.RemoveAt(0);
            }
        }
    }

    void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;

        // Hitung rata-rata kecepatan flick (Smoothing)
        Vector3 flickVelocity = Vector3.zero;
        if (velocityHistory.Count > 0)
        {
            foreach (var v in velocityHistory) flickVelocity += v;
            flickVelocity /= velocityHistory.Count;
        }

        // Tambahkan Random Torque (Putaran acak)
        Vector3 randomTorque = new Vector3(
            UnityEngine.Random.Range(-1f, 1f),
            UnityEngine.Random.Range(-1f, 1f),
            UnityEngine.Random.Range(-1f, 1f)
        ) * torqueAmount;

        // Perintah Controller untuk melempar
        // Kita kali flickVelocity dengan sensitivity dari inspector
        diceController.Throw(flickVelocity * flickSensitivity, randomTorque);
    }
}