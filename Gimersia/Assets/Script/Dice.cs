using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System; // Diperlukan untuk Action

[RequireComponent(typeof(Rigidbody))]
public class Dice : MonoBehaviour
{
    [Header("Referensi Manager")]
    public MultiplayerManager manager;

    [Header("Detektor Wajah")]
    public Transform[] faces;
    public Rigidbody rb;

    [Header("Pengaturan Lempar")]
    public float forceAmount = 10f;
    public float torqueAmount = 10f;
    public float flickSensitivity = 1.5f;

    // --- BARU: Tambahkan variabel ini ---
    [Tooltip("Kecepatan dadu berputar saat di-drag mouse")]
    public float dragSpinSpeed = 200f;
    // ---------------------------------

    // --- Variabel Internal ---
    private Vector3 startPosition;
    private Camera mainCamera;
    private bool isDragging = false;
    private Plane dragPlane;
    private Vector3 dragOffset;
    private List<Vector3> velocityHistory = new List<Vector3>(5);
    private Vector3 lastPosition;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        startPosition = transform.position;
        mainCamera = Camera.main;
        ResetDice();
    }

    public void ResetDice()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = startPosition;
        transform.rotation = Quaternion.identity;
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public IEnumerator WaitForRollToStop(Action<int> callback)
    {
        yield return new WaitForSeconds(0.5f);
        while (rb.velocity.magnitude > 0.05f || rb.angularVelocity.magnitude > 0.05f)
        {
            yield return null;
        }
        yield return new WaitForSeconds(0.2f);
        int result = GetResult();
        callback(result);
    }

    private int GetResult()
    {
        int bestFace = 1;
        float highestY = -Mathf.Infinity;
        for (int i = 0; i < faces.Length; i++)
        {
            if (faces[i].position.y > highestY)
            {
                highestY = faces[i].position.y;
                bestFace = int.Parse(faces[i].name.Split('_')[1]);
            }
        }
        return bestFace;
    }

    // --- Logika Mouse untuk Melempar Dadu ---

    void OnMouseDown()
    {
        if (manager != null && manager.IsActionRunning) return;
        if (rb.IsSleeping() || rb.velocity.magnitude < 0.1f)
        {
            isDragging = true;
            rb.isKinematic = true;
            rb.useGravity = false;
            dragPlane = new Plane(mainCamera.transform.forward, transform.position);
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (dragPlane.Raycast(ray, out float distance))
            {
                dragOffset = transform.position - ray.GetPoint(distance);
            }
            lastPosition = transform.position;
            velocityHistory.Clear();
        }
    }

    void OnMouseDrag()
    {
        if (!isDragging) return;

        // Gerakkan dadu di sepanjang 'bidang'
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (dragPlane.Raycast(ray, out float distance))
        {
            Vector3 newPos = ray.GetPoint(distance) + dragOffset;
            transform.position = newPos;

            // --- BARU: Putar dadu secara manual ---
            // Kita putar di dua sumbu agar terlihat 'mengguling' acak
            transform.Rotate(Vector3.up, dragSpinSpeed * Time.deltaTime, Space.World);
            transform.Rotate(Vector3.right, dragSpinSpeed * 0.8f * Time.deltaTime, Space.Self);
            // --------------------------------------

            // Lacak kecepatan untuk 'flick'
            Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
            lastPosition = transform.position;
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

        // Hidupkan lagi fisika
        rb.isKinematic = false;
        rb.useGravity = true;

        // Hitung rata-rata kecepatan 'flick'
        Vector3 flickVelocity = Vector3.zero;
        if (velocityHistory.Count > 0)
        {
            foreach (var v in velocityHistory)
            {
                flickVelocity += v;
            }
            flickVelocity /= velocityHistory.Count;
        }

        // Terapkan gaya 'flick'
        rb.AddForce(flickVelocity * flickSensitivity, ForceMode.Impulse);

        // Tambahkan putaran acak (ini TIDAK mengambil dari putaran drag)
        rb.AddTorque(
            UnityEngine.Random.Range(-1f, 1f),
            UnityEngine.Random.Range(-1f, 1f),
            UnityEngine.Random.Range(-1f, 1f) * torqueAmount,
            ForceMode.Impulse);

        // Beri tahu manager bahwa dadu TELAH DILEMPAR!
        if (manager != null)
        {
            manager.NotifyDiceThrown();
        }
    }
}