using System.Collections;
using System.Collections.Generic; // <-- Pastikan ini ada
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Collider))]
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
    public float rotationSpeed = 360f;

    [Header("Card System Data")]
    public List<PlayerCardInstance> heldCards = new List<PlayerCardInstance>();

    [Header("Card Status Effects")]
    public int immuneToReverseCycles = 0;
    public int immuneToSnakeUses = 0;
    public int nextRollModifier = 0;
    public int extraDiceRolls = 0;
    public bool hasAresProvocation = false;
    public int skipTurns = 0;

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

    public void SetManager(MultiplayerManager m)
    {
        manager = m;
    }

    public void ShowReversedBadge(bool show)
    {
        wasReversedThisCycle = show;
        if (reversedBadge != null)
        {
            reversedBadge.SetActive(show);
        }
    }

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
        float factor = on ? 1.15f : 1.0f;
        transform.localScale = baseScale * factor;
    }

    // --- FUNGSI INI DIUBAH SECARA SIGNIFIKAN ---
    public IEnumerator MoveToTile(int targetTileID, System.Func<int, Vector3> tilePosProvider)
    {
        int start = currentTileID;
        if (targetTileID == start) yield break;

        if (targetTileID > start)
        {
            // Gerak Maju
            for (int i = start + 1; i <= targetTileID; i++)
            {
                // --- PERBAIKAN ROTASI (PINDAH KE ATAS) ---
                int currentRow = (currentTileID - 1) / 10; // Baris kita sekarang (misal: 0)
                int nextRow = (i - 1) / 10;            // Baris tile tujuan (misal: 1)

                if (nextRow != currentRow) // Cek jika kita AKAN pindah baris
                {
                    // Kita ada di 'currentTileID' (misal 10), mau pindah ke 'i' (misal 11)
                    float targetYAngle = (nextRow % 2 == 0) ? 0f : 180f; // Tentukan rotasi baris baru
                    yield return StartCoroutine(SmoothRotate(targetYAngle)); // Putar badan dulu
                }
                // ------------------------------------------

                Vector3 targetPos = tilePosProvider(i);
                while (Vector3.Distance(transform.position, targetPos) > 0.01f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, stepSpeed * Time.deltaTime);
                    yield return null;
                }
                transform.position = targetPos;
                currentTileID = i; // Update ID SETELAH sampai

                // (Logika rotasi yang lama di sini DIHAPUS)

                yield return new WaitForSeconds(stepDelay);
            }
        }
        else
        {
            // Gerak Mundur (Bounce Back)
            for (int i = start - 1; i >= targetTileID; i--)
            {
                // --- PERBAIKAN ROTASI (PINDAH KE ATAS) ---
                int currentRow = (currentTileID - 1) / 10;
                int nextRow = (i - 1) / 10;

                if (nextRow != currentRow) // Cek jika pindah baris
                {
                    float targetYAngle = (nextRow % 2 == 0) ? 0f : 180f;
                    yield return StartCoroutine(SmoothRotate(targetYAngle)); // Putar badan dulu
                }
                // ------------------------------------------

                Vector3 targetPos = tilePosProvider(i);
                while (Vector3.Distance(transform.position, targetPos) > 0.01f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, stepSpeed * Time.deltaTime);
                    yield return null;
                }
                transform.position = targetPos;
                currentTileID = i;

                yield return new WaitForSeconds(stepDelay);
            }
        }
    }

    // Teleport (dipakai Ular & Kartu)
    public IEnumerator TeleportToTile(int targetTileID, System.Func<int, Vector3> tilePosProvider)
    {
        Vector3 targetPos = tilePosProvider(targetTileID);
        yield return StartCoroutine(TeleportToPosition(targetTileID, targetPos));
    }

    // --- FUNGSI BARU UNTUK TANGGA ---
    /// <summary>
    /// Teleportasi yang menerima Vector3 (dipakai Tangga)
    /// </summary>
    public IEnumerator TeleportToTile(int targetTileID, Vector3 targetPos)
    {
        yield return StartCoroutine(TeleportToPosition(targetTileID, targetPos));
    }
    // --------------------------------

    /// <summary>
    /// Logika inti teleportasi
    /// </summary>
    private IEnumerator TeleportToPosition(int targetTileID, Vector3 targetPos)
    {
        float speed = stepSpeed * 2.0f; // Gerak cepat
        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPos;
        currentTileID = targetTileID;

        // Snap rotasi instan setelah teleport
        int row = (currentTileID - 1) / 10;
        float targetYAngle = (row % 2 == 0) ? 0f : 180f;
        transform.rotation = Quaternion.Euler(0, targetYAngle, 0);
    }
    // --------------------------------


    // (Fungsi SmoothRotate, MoveToPosition, NudgeToPosition tidak berubah)
    #region Helper Coroutines
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
        if (duration <= 0) duration = 0.1f; // Pengaman
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
    #endregion
}