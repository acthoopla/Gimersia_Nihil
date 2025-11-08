using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerPawn : MonoBehaviour
{
    [Header("Player Data")]
    public int currentTileID = 1;
    public int playerIndex = 0; // 0-based

    [Header("Visuals")]
    public Renderer bodyRenderer;
    public TextMeshPro labelTMP;
    public Color[] defaultColors; // assign di prefab atau override di inspector

    [Header("Movement")]
    public float stepSpeed = 5f;
    public float stepDelay = 0.08f;

    private Vector3 baseScale;

    void Awake()
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();

        baseScale = transform.localScale;
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
        // Gunakan baseScale agar tidak merusak ukuran asli prefab
        float factor = on ? 1.15f : 1.0f;
        transform.localScale = baseScale * factor;
    }

    // Move per-tile (smooth)
    public IEnumerator MoveToTile(int targetTileID, System.Func<int, Vector3> tilePosProvider)
    {
        int start = currentTileID;
        if (targetTileID == start) yield break;

        if (targetTileID > start)
        {
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
                yield return new WaitForSeconds(stepDelay);
            }
        }
        else
        {
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
                yield return new WaitForSeconds(stepDelay);
            }
        }
    }

    // Teleport (used for ladder/snake)
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
    }
}
