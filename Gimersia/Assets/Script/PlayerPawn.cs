using System.Collections;
using System.Collections.Generic; // <-- BARU: Diambil dari temanmu
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Collider))] // <-- BARU: Diambil dari temanmu
public class PlayerPawn : MonoBehaviour
{
    [Header("Player Data")]
    public int currentTileID = 1;
    public int playerIndex = 0; 
    private MultiplayerManager manager; 

    [Header("Visuals")]
    public Renderer bodyRenderer;
    public TextMeshPro labelTMP;
    public Color[] defaultColors; 
    public GameObject reversedBadge; 

    [Header("Movement")]
    public float stepSpeed = 5f;
    public float stepDelay = 0.08f;
    public float rotationSpeed = 360f; // <-- Dari KODEMU

    // --- VARIABEL BARU DARI KODE TEMANMU ---
    [Header("Card System Data")]
    public List<PlayerCardInstance> heldCards = new List<PlayerCardInstance>();

    [Header("Card Status Effects")]
    public int immuneToReverseCycles = 0;
    public int immuneToSnakeUses = 0;
    public int nextRollModifier = 0;
    public int extraDiceRolls = 0;
    public bool hasAresProvocation = false;
    public int skipTurns = 0;
    // -------------------------------------

    [HideInInspector]
    public bool wasReversedThisCycle = false; 

    private Vector3 baseScale;

    void Awake()
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();
        baseScale = transform.localScale;
        if (reversedBadge != null) reversedBadge.SetActive(false);
    }
    
    // Fungsi 'SetManager' dari KODEMU
    public void SetManager(MultiplayerManager m)
    {
        manager = m;
    }

    // Fungsi 'ShowReversedBadge' dari KODEMU
    public void ShowReversedBadge(bool show)
    {
        wasReversedThisCycle = show; // <-- Logika temanmu digabung di sini
        if (reversedBadge != null)
        {
            reversedBadge.SetActive(show);
        }
    }

    // Fungsi 'OnMouseDown' dari KODEMU
    void OnMouseDown()
    {
        if (manager != null)
        {
            manager.OnPawnClicked(this);
        }
    }

    public void SetVisualIndex(int idx)
    {
        playerIndex = idx;
        if (labelTMP != null) labelTMP.text = $"P{idx + 1}";
        if (bodyRenderer != null && defaultColors != null && defaultColors.Length > 0)
        {
            Color c = defaultColors[idx % defaultColors.Length];
            bodyRenderer.material.color = c;
        }
        transform.localScale = baseScale;
    }

    public void SetHighlight(bool on)
    {
        float factor = on ? 1.15f : 1.0f; // Menggunakan 1.15f dari kodemu
        transform.localScale = baseScale * factor;
    }

    // DIUBAH: 'MoveToTile' adalah dari KODEMU (dengan logika rotasi)
    public IEnumerator MoveToTile(int targetTileID, System.Func<int, Vector3> tilePosProvider)
    {
        int start = currentTileID;
        if (targetTileID == start) yield break;

        if (targetTileID > start)
        {
            // Gerak Maju
            for (int i = start + 1; i <= targetTileID; i++)
            {
                Vector3 targetPos = tilePosProvider(i);
                while (Vector3.Distance(transform.position, targetPos) > 0.01f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, stepSpeed * Time.deltaTime);
                    yield return null;
                }
                transform.position = targetPos;
                currentTileID = i;

                // Logika Rotasi dari KODEMU
                int row = (i - 1) / 10;
                float targetYAngle = (row % 2 == 0) ? 0f : 180f;
                yield return StartCoroutine(SmoothRotate(targetYAngle));
                // --------------------------

                yield return new WaitForSeconds(stepDelay);
            }
        }
        else
        {
            // Gerak Mundur
            for (int i = start - 1; i >= targetTileID; i--)
            {
                Vector3 targetPos = tilePosProvider(i);
                while (Vector3.Distance(transform.position, targetPos) > 0.01f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, stepSpeed * Time.deltaTime);
                    yield return null;
                }
                transform.position = targetPos;
                currentTileID = i;

                // Logika Rotasi dari KODEMU
                int row = (i - 1) / 10;
                float targetYAngle = (row % 2 == 0) ? 0f : 180f;
                yield return StartCoroutine(SmoothRotate(targetYAngle));
                // --------------------------

                yield return new WaitForSeconds(stepDelay);
            }
        }
    }

    // DIUBAH: 'TeleportToTile' adalah dari KODEMU (dengan logika rotasi)
    public IEnumerator TeleportToTile(int targetTileID, System.Func<int, Vector3> tilePosProvider)
    {
        Vector3 targetPos = tilePosProvider(targetTileID);
        float speed = stepSpeed * 2.0f;
        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPos;
        currentTileID = targetTileID;

        // Logika Rotasi (Instan) dari KODEMU
        int row = (currentTileID - 1) / 10;
        float targetYAngle = (row % 2 == 0) ? 0f : 180f;
        transform.rotation = Quaternion.Euler(0, targetYAngle, 0);
    }

    // --- SEMUA FUNGSI HELPER BARU DARI KODEMU DISIMPAN ---

    IEnumerator SmoothRotate(float targetYAngle)
    {
        Quaternion targetRotation = Quaternion.Euler(0, targetYAngle, 0);
        if (Quaternion.Angle(transform.rotation, targetRotation) < 1f)
        {
            transform.rotation = targetRotation;
            yield break;
        }
        Quaternion startRotation = transform.rotation;
        float duration = Quaternion.Angle(startRotation, targetRotation) / rotationSpeed;
        float t = 0f;
        while (t < 1.0f)
        {
            t += Time.deltaTime / duration;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }
        transform.rotation = targetRotation;
    }

    public void MoveToPosition(Vector3 targetPos)
    {
        if (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            StartCoroutine(NudgeToPosition(targetPos));
        }
    }

    IEnumerator NudgeToPosition(Vector3 targetPos)
    {
        float nudgeSpeed = stepSpeed * 1.5f;
        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, nudgeSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPos;
    }
}