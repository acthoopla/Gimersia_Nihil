using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(DicePhysics))]
[RequireComponent(typeof(DiceFaceDetector))]
[RequireComponent(typeof(AudioSource))]
public class DiceController : MonoBehaviour
{
    // Komponen SRP Pecahan
    private DicePhysics dicePhysics;
    private DiceFaceDetector diceCalculator;
    private AudioSource audioSource;

    private Vector3 startPos;
    private Quaternion startRot;

    // Events
    public event Action OnDiceThrown; // Untuk Manager (nyalain tembok)
    public event Action<int> OnDiceResult; // Untuk TurnManager

    public bool IsRolling { get; private set; } = false;

    void Awake()
    {
        dicePhysics = GetComponent<DicePhysics>();
        diceCalculator = GetComponent<DiceFaceDetector>();
        audioSource = GetComponent<AudioSource>();

        startPos = transform.position;
        startRot = transform.rotation;

        // Pastikan posisi awal reset
        ResetState();
    }

    // Dipanggil InputHandler
    public void Throw(Vector3 force, Vector3 torque)
    {
        if (IsRolling) return;
        IsRolling = true;

        // 1. Perintah Physics: Lempar!
        dicePhysics.Launch(force, torque);

        // 2. Mainkan Audio
        if (audioSource) audioSource.Play();

        // 3. Lapor Event
        OnDiceThrown?.Invoke();

        // 4. Mulai pantau kapan berhenti
        StartCoroutine(WaitForStopRoutine());
    }

    public void ResetState()
    {
        IsRolling = false;
        dicePhysics.StopAndReset(startPos, startRot);
    }

    private IEnumerator WaitForStopRoutine()
    {
        // Jeda safety awal
        yield return new WaitForSeconds(0.5f);

        // Tunggu sampai Physics bilang "sudah diam"
        while (dicePhysics.IsMoving())
        {
            yield return null;
        }

        // Jeda stabilisasi
        yield return new WaitForSeconds(0.2f);

        IsRolling = false;

        // 5. Perintah Calculator: Hitung sekarang!
        int result = diceCalculator.CalculateResult();

        Debug.Log($"[DiceController] Angka Keluar: {result}");
        OnDiceResult?.Invoke(result);
    }
}