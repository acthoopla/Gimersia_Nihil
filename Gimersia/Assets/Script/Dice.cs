using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

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

    [Tooltip("Kecepatan dadu berputar saat di-drag mouse")]
    public float dragSpinSpeed = 200f;

    // --- FITUR DUAL DICE ---
    [Header("Dual Dice System")]
    [Tooltip("Dadu kedua yang akan ikut terlempar (jika aktif)")]
    public Dice followerDice;

    [Tooltip("Jarak dadu kedua saat di-drag")]
    public Vector3 followerOffset = new Vector3(1.2f, 0, 0);
    // -----------------------

    // Variabel Internal
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

        // Pastikan fisika mati saat reset (mode 'siap diambil')
        rb.isKinematic = true;
        rb.useGravity = false;

        if (manager != null)
        {
            manager.DisableDiceWall();
        }
    }

    // --- FUNGSI BARU: Untuk dilempar oleh script (bukan mouse) ---
    public void RollFromScript(Vector3 force, Vector3 torque)
    {
        // Aktifkan fisika
        rb.isKinematic = false;
        rb.useGravity = true;

        // Terapkan gaya
        rb.AddForce(force, ForceMode.Impulse);
        rb.AddTorque(torque, ForceMode.Impulse);
    }
    // -----------------------------------------------------------

    public IEnumerator WaitForRollToStop(Action<int> callback)
    {
        yield return new WaitForSeconds(0.5f);
        // Tunggu sampai diam
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

    // --- Logika Mouse ---

    void OnMouseDown()
    {
        if (manager != null && manager.IsActionRunning) return;

        // Hanya bisa diambil jika diam
        if (rb.IsSleeping() || rb.velocity.magnitude < 0.1f)
        {
            isDragging = true;
            rb.isKinematic = true;
            rb.useGravity = false;

            // Setup plane untuk drag
            dragPlane = new Plane(mainCamera.transform.forward, transform.position);
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (dragPlane.Raycast(ray, out float distance))
            {
                dragOffset = transform.position - ray.GetPoint(distance);
            }

            lastPosition = transform.position;
            velocityHistory.Clear();

            // --- DUAL DICE: Siapkan follower ---
            if (followerDice != null && followerDice.gameObject.activeSelf)
            {
                // Matikan fisika follower agar bisa kita gerakkan manual
                followerDice.rb.isKinematic = true;
                followerDice.rb.useGravity = false;
            }
        }
    }

    void OnMouseDrag()
    {
        if (!isDragging) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (dragPlane.Raycast(ray, out float distance))
        {
            Vector3 newPos = ray.GetPoint(distance) + dragOffset;
            transform.position = newPos;

            // Efek putar saat drag
            float spinStep = dragSpinSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, spinStep, Space.World);
            transform.Rotate(Vector3.right, spinStep * 0.8f, Space.Self);

            // Hitung velocity untuk lemparan
            Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
            lastPosition = transform.position;
            velocityHistory.Add(velocity);
            if (velocityHistory.Count > 5) velocityHistory.RemoveAt(0);

            // --- DUAL DICE: Gerakkan follower ---
            if (followerDice != null && followerDice.gameObject.activeSelf)
            {
                // Follower menempel di samping (offset)
                followerDice.transform.position = transform.position + followerOffset;

                // Ikut berputar juga biar keren
                followerDice.transform.Rotate(Vector3.up, -spinStep, Space.World); // Putar arah lawan
                followerDice.transform.Rotate(Vector3.right, spinStep, Space.Self);
            }
        }
    }

    void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;

        if (manager != null) manager.EnableDiceWall();

        // 1. Hidupkan fisika Dadu Utama
        rb.isKinematic = false;
        rb.useGravity = true;

        // 2. Hitung Gaya Lempar (Flick)
        Vector3 flickVelocity = Vector3.zero;
        if (velocityHistory.Count > 0)
        {
            foreach (var v in velocityHistory) flickVelocity += v;
            flickVelocity /= velocityHistory.Count;
        }
        Vector3 finalForce = flickVelocity * flickSensitivity;

        // Gaya putar acak
        Vector3 randomTorque = new Vector3(
            UnityEngine.Random.Range(-1f, 1f),
            UnityEngine.Random.Range(-1f, 1f),
            UnityEngine.Random.Range(-1f, 1f)) * torqueAmount;

        // 3. Terapkan gaya ke Dadu Utama
        rb.AddForce(finalForce, ForceMode.Impulse);
        rb.AddTorque(randomTorque, ForceMode.Impulse);

        // --- DUAL DICE: Lempar Follower ---
        if (followerDice != null && followerDice.gameObject.activeSelf)
        {
            // Beri sedikit variasi gaya agar tidak jatuhnya kembar persis
            Vector3 followerForce = finalForce + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f));
            Vector3 followerTorque = randomTorque * -1f; // Putar balik

            followerDice.RollFromScript(followerForce, followerTorque);
        }

        // 4. Lapor ke Manager
        if (manager != null)
        {
            manager.NotifyDiceThrown();
        }
    }
}