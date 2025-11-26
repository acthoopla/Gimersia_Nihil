using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GodHand : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public GameObject hand;
    public Transform fingerTip;      // untuk klik biasa
    public Transform palmReference;  // untuk hold dice
    public Animator anim;

    [Header("Settings")]
    public float depth = 5f;
    public float hideDelay = 0.5f;

    [Header("Rotation Settings")]
    public float maxTiltAngle = 30f;
    public float rotationSmoothSpeed = 8f;

    [Header("Dice Visual Only")]
    public string diceTag = "Dice";

    private float timer;
    private Vector3 fingerTipOffset;  // offset dari hand pivot ke fingerTip
    private Vector3 palmOffset;       // offset dari hand pivot ke palmReference
    private Quaternion targetRotation;
    private bool isHoldingDice = false;

    void Start()
    {
        hand.SetActive(false);

        // Calculate offsets di Start
        fingerTipOffset = hand.transform.position - fingerTip.position;
        palmOffset = hand.transform.position - palmReference.position;

        targetRotation = hand.transform.rotation;

        Debug.Log($"FingerTip Offset: {fingerTipOffset}");
        Debug.Log($"Palm Offset: {palmOffset}");
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryShowHand();
        }

        if (Input.GetMouseButtonUp(0) && isHoldingDice)
        {
            ReleaseDiceVisual();
        }

        if (hand.activeInHierarchy)
        {
            UpdateHandPositionAndRotation();
        }

        HandleAutoHide();
    }

    void TryShowHand()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit) && hit.collider.CompareTag(diceTag))
        {
            StartDiceHoldVisual(hit.point);
        }
        //else
        //{
        //    ShowHandNormal();
        //}
    }

    void StartDiceHoldVisual(Vector3 hitPoint)
    {
        hand.SetActive(true);
        isHoldingDice = true;
        anim.SetTrigger("Hold");

        // GUNAKAN PALM REFERENCE untuk hold dice
        // Posisikan sehingga palmReference tepat di hit point
        hand.transform.position = hitPoint + palmOffset;

        // Set rotation
        Vector3 mousePos = Input.mousePosition;
        UpdateTargetRotation(mousePos.y);
        hand.transform.rotation = targetRotation;

        timer = hideDelay;

        Debug.Log($"Dice Hold - Hit Point: {hitPoint}, Hand Position: {hand.transform.position}, Palm Position: {palmReference.position}");
    }

    void ReleaseDiceVisual()
    {
        if (isHoldingDice)
        {
            anim.SetTrigger("Throw");
            isHoldingDice = false;
            timer = hideDelay + 0.5f; //dikasih tambahan delay biar animasi throw kelihatan
        }
    }

    void ShowHandNormal()
    {
        hand.SetActive(true);
        anim.SetTrigger("Tap");

        // GUNAKAN FINGER TIP untuk klik biasa
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = depth;
        Vector3 targetWorldPos = cam.ScreenToWorldPoint(mousePos);

        hand.transform.position = targetWorldPos + fingerTipOffset;
        UpdateTargetRotation(mousePos.y);
        hand.transform.rotation = targetRotation;

        timer = hideDelay;

        Debug.Log($"Normal Tap - World Pos: {targetWorldPos}, Hand Position: {hand.transform.position}, FingerTip Position: {fingerTip.position}");
    }

    void UpdateHandPositionAndRotation()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = depth;
        Vector3 targetWorldPos = cam.ScreenToWorldPoint(mousePos);

        UpdateTargetRotation(mousePos.y);

        // Smooth rotation
        hand.transform.rotation = Quaternion.Lerp(
            hand.transform.rotation,
            targetRotation,
            rotationSmoothSpeed * Time.deltaTime
        );

        // TENTUKAN REFERENCE POINT BERDASARKAN MODE
        if (isHoldingDice)
        {
            // MODE HOLD DICE: Pastikan palmReference tepat di target position
            Vector3 currentPalmPos = palmReference.position;
            Vector3 positionError = targetWorldPos - currentPalmPos;
            hand.transform.position += positionError;

            Debug.Log($"Hold Update - Target: {targetWorldPos}, Palm: {currentPalmPos}, Error: {positionError}");
        }
        else
        {
            // MODE NORMAL: Pastikan fingerTip tepat di target position  
            Vector3 currentFingerTipPos = fingerTip.position;
            Vector3 positionError = targetWorldPos - currentFingerTipPos;
            hand.transform.position += positionError;
        }
    }

    void UpdateTargetRotation(float mouseY)
    {
        float normalizedY = mouseY / Screen.height;
        float tiltFactor = (normalizedY - 0.5f) * 2f;
        float tiltAngle = tiltFactor * maxTiltAngle;

        Quaternion baseRotation = Quaternion.LookRotation(cam.transform.forward);
        targetRotation = baseRotation * Quaternion.Euler(tiltAngle, 0f, 0f);
    }

    void HandleAutoHide()
    {
        if (timer > 0 && !isHoldingDice)
        {
            timer -= Time.deltaTime;
            if (timer <= 0)
                hand.SetActive(false);
        }
        else if (isHoldingDice)
        {
            timer = hideDelay;
        }
    }

    void OnDrawGizmos()
    {
        if (hand != null)
        {
            // FingerTip - MERAH
            if (fingerTip != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(fingerTip.position, 0.1f);
                Gizmos.DrawLine(hand.transform.position, fingerTip.position);
            }

            // PalmReference - HIJAU (kuning saat hold)
            if (palmReference != null)
            {
                Gizmos.color = isHoldingDice ? Color.yellow : Color.green;
                Gizmos.DrawWireSphere(palmReference.position, 0.15f);
                Gizmos.DrawLine(hand.transform.position, palmReference.position);

                // Text label
#if UNITY_EDITOR
                UnityEditor.Handles.Label(palmReference.position, isHoldingDice ? "Palm (Active)" : "Palm");
#endif
            }

            // Hand pivot - BIRU
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(hand.transform.position, 0.08f);
        }
    }
}