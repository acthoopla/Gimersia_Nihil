using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileHighlighter : MonoBehaviour
{
    public static TileHighlighter Instance { get; private set; }

    [Header("Settings")]
    public GameObject highlightPrefab;
    public float yOffset = 1.0f;

    private GameObject currentMarker;

    void Awake() { if (Instance == null) Instance = this; else Destroy(gameObject); }

    public void PreviewDestination(PlayerState player, int diceValue, int cardModifier)
    {
        if (player == null || BoardManager.Instance == null) return;

        int totalMoves = diceValue + cardModifier;
        if (totalMoves == 0) { HideHighlight(); return; }

        int startTile = player.TileID;
        int maxTile = BoardManager.Instance.totalTiles;

        int finalTarget = startTile + totalMoves;

        if (finalTarget > maxTile)
        {
            finalTarget = maxTile;
        }

        if (finalTarget < 1) finalTarget = 1;

        Tiles targetTileObj = BoardManager.Instance.GetTileByID(finalTarget);
        if (targetTileObj != null)
        {
            ShowMarkerAt(targetTileObj.GetPlayerPosition());
        }
        else
        {
            HideHighlight();
        }
    }

    public void HideHighlight() { if (currentMarker != null) currentMarker.SetActive(false); }

    private void ShowMarkerAt(Vector3 position)
    {
        if (currentMarker == null && highlightPrefab != null) currentMarker = Instantiate(highlightPrefab);
        if (currentMarker != null)
        {
            currentMarker.SetActive(true);
            currentMarker.transform.position = position + Vector3.up * yOffset;
        }
    }
}