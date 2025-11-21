using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// NewPlayerPawn (SRP)
/// - Visual-only component untuk pawn pemain.
/// - Menyediakan coroutine MoveToTile / TeleportToTile dengan beberapa signature.
/// - Mengupdate PlayerState.TileID saat berpindah.
/// - Tidak melakukan logika game (damage, card, turn) — hanya animasi/visual.
/// 
/// Pastikan GameObject ini juga memiliki komponen PlayerState (atau NewPlayerState).
/// MovementSystem akan memanggil coroutine ini (via reflection atau langsung).
/// </summary>
[DisallowMultipleComponent]
public class NewPlayerPawn : MonoBehaviour
{
    [Header("References")]
    public PlayerState playerState; // referensi; jika null, coba GetComponent di Awake
    public GameObject reversedBadge;
    public Renderer bodyRenderer;

    [Header("Movement tuning")]
    public float stepSpeed = 5f;        // units per second (move)
    public float stepDelay = 0.08f;     // pause after stepping each tile
    public float rotationSpeed = 720f;  // degrees per second for smooth rotation
    public float teleportSpeed = 12f;   // speed when teleporting
    public float positionTolerance = 0.01f;

    [Header("Visual FX")]
    public GameObject ladderParticle;

    private Vector3 baseScale;

    void Awake()
    {
        if (playerState == null)
            playerState = GetComponent<PlayerState>();

        baseScale = transform.localScale;

        if (reversedBadge != null)
            reversedBadge.SetActive(false);

        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();
    }

    #region Public Visual helpers
    public void SetVisualIndex(int idx)
    {
        // optional colorization based on index if material array provided, keep simple
        transform.localScale = baseScale;
    }

    public void SetHighlight(bool on)
    {
        float factor = on ? 1.15f : 1.0f;
        transform.localScale = baseScale * factor;
    }

    public void ShowReversedBadge(bool show)
    {
        if (reversedBadge != null) reversedBadge.SetActive(show);
    }

    public void PlayLadderParticle()
    {
        if (ladderParticle != null) ladderParticle.SetActive(true);
    }

    public void StopLadderParticle()
    {
        if (ladderParticle != null) ladderParticle.SetActive(false);
    }
    #endregion

    #region Move Coroutines (signatures supporting MovementSystem reflection)

    // Signature A: (int targetTileID, Func<int, Vector3> tilePosProvider)
    public IEnumerator MoveToTile(int targetTileID, Func<int, Vector3> tilePosProvider)
    {
        if (tilePosProvider == null)
        {
            Debug.LogWarning("[NewPlayerPawn] MoveToTile called with null tilePosProvider. Using BoardManager fallback.");
            var bm = BoardManager.Instance != null ? BoardManager.Instance : FindObjectOfType<BoardManager>();
            if (bm != null) tilePosProvider = (id) => bm.GetTilePosition(id);
            else yield break;
        }

        Vector3 targetPos = tilePosProvider(targetTileID);
        yield return StartCoroutine(MoveToTile_Internal(targetTileID, targetPos, false));
    }

    // Signature B: (int targetTileID, Vector3 targetPos)
    public IEnumerator MoveToTile(int targetTileID, Vector3 targetPos)
    {
        yield return StartCoroutine(MoveToTile_Internal(targetTileID, targetPos, false));
    }

    // Signature C: (int targetTileID)
    public IEnumerator MoveToTile(int targetTileID)
    {
        var bm = BoardManager.Instance != null ? BoardManager.Instance : FindObjectOfType<BoardManager>();
        if (bm == null)
        {
            Debug.LogWarning("[NewPlayerPawn] MoveToTile(int) called but BoardManager not found.");
            yield break;
        }
        Vector3 pos = bm.GetTilePosition(targetTileID);
        yield return StartCoroutine(MoveToTile_Internal(targetTileID, pos, false));
    }

    // Teleport signatures mirror MoveToTile but treat motion as teleport (faster, no stepDelay)
    public IEnumerator TeleportToTile(int targetTileID, Func<int, Vector3> tilePosProvider)
    {
        if (tilePosProvider == null)
        {
            var bm = BoardManager.Instance != null ? BoardManager.Instance : FindObjectOfType<BoardManager>();
            if (bm != null) tilePosProvider = (id) => bm.GetTilePosition(id);
            else yield break;
        }
        Vector3 targetPos = tilePosProvider(targetTileID);
        yield return StartCoroutine(MoveToTile_Internal(targetTileID, targetPos, true));
    }

    public IEnumerator TeleportToTile(int targetTileID, Vector3 targetPos)
    {
        yield return StartCoroutine(MoveToTile_Internal(targetTileID, targetPos, true));
    }

    public IEnumerator TeleportToTile(int targetTileID)
    {
        var bm = BoardManager.Instance != null ? BoardManager.Instance : FindObjectOfType<BoardManager>();
        if (bm == null)
        {
            Debug.LogWarning("[NewPlayerPawn] TeleportToTile(int) called but BoardManager not found.");
            yield break;
        }
        Vector3 pos = bm.GetTilePosition(targetTileID);
        yield return StartCoroutine(MoveToTile_Internal(targetTileID, pos, true));
    }

    #endregion

    #region Internal movement implementation
    private IEnumerator MoveToTile_Internal(int targetTileID, Vector3 targetPos, bool isTeleport)
    {
        // If no PlayerState available, set manual tracking of a "fake" tileID by trying to find component
        if (playerState == null)
            playerState = GetComponent<PlayerState>();

        // Rotate to face direction if needed (compute direction)
        Vector3 startPos = transform.position;
        Vector3 dir = (targetPos - startPos);
        if (dir.sqrMagnitude > 0.001f)
        {
            float targetYAngle = Quaternion.LookRotation(dir).eulerAngles.y;
            yield return StartCoroutine(SmoothRotateToAngle(targetYAngle));
        }

        if (isTeleport)
        {
            // Teleport style: fast MoveTowards without step delays
            float dist = Vector3.Distance(transform.position, targetPos);
            while (Vector3.Distance(transform.position, targetPos) > positionTolerance)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPos, teleportSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = targetPos;
            // update tile id
            if (playerState != null) playerState.TileID = targetTileID;

            // Snap rotation based on row parity (same as original behavior)
            SnapRotationByRow(playerState?.TileID ?? targetTileID);
            yield break;
        }

        // Step-by-step movement per tile: compute start/end tile index if playerState available
        int startTile = (playerState != null) ? playerState.TileID : -1;
        if (startTile <= 0)
        {
            // If no start info, simply move directly
            while (Vector3.Distance(transform.position, targetPos) > positionTolerance)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPos, stepSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = targetPos;
            if (playerState != null) playerState.TileID = targetTileID;
            SnapRotationByRow(playerState?.TileID ?? targetTileID);
            yield break;
        }

        // Determine direction of movement in tile steps
        if (targetTileID == startTile)
        {
            // no move
            yield break;
        }
        else if (targetTileID > startTile)
        {
            for (int t = startTile + 1; t <= targetTileID; t++)
            {
                Vector3 nextPos = (BoardManager.Instance != null) ? BoardManager.Instance.GetTilePosition(t) : targetPos;
                // Smooth move
                while (Vector3.Distance(transform.position, nextPos) > positionTolerance)
                {
                    transform.position = Vector3.MoveTowards(transform.position, nextPos, stepSpeed * Time.deltaTime);
                    yield return null;
                }
                transform.position = nextPos;
                // Update tile id AFTER we reach step
                if (playerState != null) playerState.TileID = t;
                yield return new WaitForSeconds(stepDelay);
            }
        }
        else // moving backward
        {
            for (int t = startTile - 1; t >= targetTileID; t--)
            {
                Vector3 nextPos = (BoardManager.Instance != null) ? BoardManager.Instance.GetTilePosition(t) : targetPos;
                while (Vector3.Distance(transform.position, nextPos) > positionTolerance)
                {
                    transform.position = Vector3.MoveTowards(transform.position, nextPos, stepSpeed * Time.deltaTime);
                    yield return null;
                }
                transform.position = nextPos;
                if (playerState != null) playerState.TileID = t;
                yield return new WaitForSeconds(stepDelay);
            }
        }

        // After finishing steps, ensure final snap position
        transform.position = targetPos;
        if (playerState != null) playerState.TileID = targetTileID;

        // Snap rotation by row parity (same rule as original)
        SnapRotationByRow(playerState.TileID);
    }

    private IEnumerator SmoothRotateToAngle(float targetYAngle)
    {
        Quaternion targetRot = Quaternion.Euler(0, targetYAngle, 0);
        if (Quaternion.Angle(transform.rotation, targetRot) < 1f)
        {
            transform.rotation = targetRot;
            yield break;
        }

        Quaternion start = transform.rotation;
        float angleDiff = Quaternion.Angle(start, targetRot);
        float duration = Mathf.Max(0.05f, angleDiff / rotationSpeed);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.rotation = Quaternion.Slerp(start, targetRot, t);
            yield return null;
        }
        transform.rotation = targetRot;
    }

    private void SnapRotationByRow(int tileID)
    {
        if (tileID <= 0) return;
        int row = BoardManager.Instance != null ? BoardManager.Instance.GetRow(tileID) : ((tileID - 1) / 10) + 1;
        float targetYAngle = (row % 2 == 0) ? 0f : 180f;
        transform.rotation = Quaternion.Euler(0, targetYAngle, 0);
    }
    #endregion
}
