using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// MovementSystem (fixed)
/// - Menggerakkan player visual (memanggil MoveToTile/TeleportToTile pada pawn jika tersedia via reflection)
/// - Jika pawn coroutine tersedia, akan StartCoroutine dan yield return (BUT reflection invocation
///   dilakukan outside of try/catch to avoid CS1626).
/// - Fallback internal movement jika tidak ada coroutine di pawn.
/// - Tidak mengasumsikan BoardManager.Instance atau PlayerState.TileID ada; menggunakan reflection helper.
/// </summary>
[DisallowMultipleComponent]
public class MovementSystem : MonoBehaviour
{
    public static MovementSystem Instance { get; private set; }

    [Header("Fallback movement settings")]
    public float stepSpeed = 5f;
    public float stepDelay = 0.08f;
    public float positionTolerance = 0.01f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    #region Public API

    public IEnumerator MovePlayerToTileCoroutine(object playerStateObj, int targetTile)
    {
        if (playerStateObj == null)
        {
            Debug.LogWarning("[MovementSystem] playerStateObj null");
            yield break;
        }

        // Try to obtain BoardManager (via static Instance if exists or FindObjectOfType)
        object boardManager = GetBoardManagerInstance();
        Func<int, Vector3> tilePosProvider = (int id) =>
        {
            Vector3 pos = Vector3.zero;
            try
            {
                // try call GetTilePosition(int)
                if (boardManager != null)
                {
                    MethodInfo m = boardManager.GetType().GetMethod("GetTilePosition", BindingFlags.Public | BindingFlags.Instance);
                    if (m != null)
                    {
                        object ret = m.Invoke(boardManager, new object[] { id });
                        if (ret is Vector3 v) return v;
                    }
                }
            }
            catch { }
            // fallback: try FindObjectOfType<BoardManager> and call its method (if available)
            var bm = FindObjectOfType<BoardManager>();
            if (bm != null) return bm.GetTilePosition(id);
            return pos;
        };

        // Try to find pawn component on same GameObject as playerState — possible names: NewPlayerPawn, PlayerPawn
        Component pawnComp = null;
        try
        {
            // assume playerStateObj is a Component or GameObject or MonoBehaviour; obtain GameObject
            GameObject pg = ExtractGameObject(playerStateObj);
            if (pg != null)
            {
                pawnComp = pg.GetComponent("NewPlayerPawn") as Component;
                if (pawnComp == null) pawnComp = pg.GetComponent(typeof(PlayerPawn)) as Component;
                if (pawnComp == null) pawnComp = pg.GetComponent("PlayerPawn") as Component; // fallback by name
            }
        }
        catch { pawnComp = null; }

        bool moved = false;

        // Attempt to call MoveToTile on pawnComp. Use safe pattern: prepare IEnumerator outside try/catch
        if (pawnComp != null)
        {
            MethodInfo mi = pawnComp.GetType().GetMethod("MoveToTile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            IEnumerator co = null;
            if (mi != null)
            {
                // try various signatures (int, Func<int,Vector3>), (int, Vector3), (int)
                ParameterInfo[] pars = mi.GetParameters();
                try
                {
                    if (pars.Length == 2 && pars[0].ParameterType == typeof(int) && typeof(Delegate).IsAssignableFrom(pars[1].ParameterType))
                    {
                        // We'll create delegate of type matching parameter
                        object delegateParam = CreateTileProviderDelegate(mi, pawnComp, tilePosProvider);
                        co = (IEnumerator)mi.Invoke(pawnComp, new object[] { targetTile, delegateParam });
                    }
                    else if (pars.Length == 2 && pars[0].ParameterType == typeof(int) && pars[1].ParameterType == typeof(Vector3))
                    {
                        Vector3 pos = tilePosProvider(targetTile);
                        co = (IEnumerator)mi.Invoke(pawnComp, new object[] { targetTile, pos });
                    }
                    else if (pars.Length == 1 && pars[0].ParameterType == typeof(int))
                    {
                        co = (IEnumerator)mi.Invoke(pawnComp, new object[] { targetTile });
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MovementSystem] reflection MoveToTile invoke failed: {ex.Message}");
                    co = null;
                }

                if (co != null)
                {
                    // Now yield safely outside try/catch
                    yield return StartCoroutine(co);
                    moved = true;
                }
            }
        }

        // Attempt TeleportToTile similarly (only if not moved)
        if (!moved && pawnComp != null)
        {
            MethodInfo miT = pawnComp.GetType().GetMethod("TeleportToTile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            IEnumerator coT = null;
            if (miT != null)
            {
                ParameterInfo[] parsT = miT.GetParameters();
                try
                {
                    if (parsT.Length == 2 && parsT[0].ParameterType == typeof(int) && typeof(Delegate).IsAssignableFrom(parsT[1].ParameterType))
                    {
                        object delegateParam = CreateTileProviderDelegate(miT, pawnComp, tilePosProvider);
                        coT = (IEnumerator)miT.Invoke(pawnComp, new object[] { targetTile, delegateParam });
                    }
                    else if (parsT.Length == 2 && parsT[0].ParameterType == typeof(int) && parsT[1].ParameterType == typeof(Vector3))
                    {
                        Vector3 pos = tilePosProvider(targetTile);
                        coT = (IEnumerator)miT.Invoke(pawnComp, new object[] { targetTile, pos });
                    }
                    else if (parsT.Length == 1 && parsT[0].ParameterType == typeof(int))
                    {
                        coT = (IEnumerator)miT.Invoke(pawnComp, new object[] { targetTile });
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MovementSystem] reflection TeleportToTile invoke failed: {ex.Message}");
                    coT = null;
                }

                if (coT != null)
                {
                    yield return StartCoroutine(coT);
                    moved = true;
                }
            }
        }

        // Fallback internal movement if nothing moved
        if (!moved)
        {
            // Determine starting tile from playerState
            int startTile = GetPlayerTileID(playerStateObj);
            int bmTotal = GetBoardTotalTiles();

            targetTile = Mathf.Clamp(targetTile, 1, bmTotal);

            if (startTile <= 0)
            {
                // no start; just move directly to final position
                Vector3 finalPos = tilePosProvider(targetTile);
                while (Vector3.Distance(GetPlayerPosition(playerStateObj), finalPos) > positionTolerance)
                {
                    SetPlayerPosition(playerStateObj, Vector3.MoveTowards(GetPlayerPosition(playerStateObj), finalPos, stepSpeed * Time.deltaTime));
                    yield return null;
                }
                SetPlayerTileID(playerStateObj, targetTile);
                yield break;
            }

            if (targetTile == startTile)
            {
                yield break;
            }
            else if (targetTile > startTile)
            {
                for (int i = startTile + 1; i <= targetTile; i++)
                {
                    Vector3 stepPos = tilePosProvider(i);
                    while (Vector3.Distance(GetPlayerPosition(playerStateObj), stepPos) > positionTolerance)
                    {
                        SetPlayerPosition(playerStateObj, Vector3.MoveTowards(GetPlayerPosition(playerStateObj), stepPos, stepSpeed * Time.deltaTime));
                        yield return null;
                    }
                    SetPlayerTileID(playerStateObj, i);
                    yield return new WaitForSeconds(stepDelay);
                }
            }
            else
            {
                for (int i = startTile - 1; i >= targetTile; i--)
                {
                    Vector3 stepPos = tilePosProvider(i);
                    while (Vector3.Distance(GetPlayerPosition(playerStateObj), stepPos) > positionTolerance)
                    {
                        SetPlayerPosition(playerStateObj, Vector3.MoveTowards(GetPlayerPosition(playerStateObj), stepPos, stepSpeed * Time.deltaTime));
                        yield return null;
                    }
                    SetPlayerTileID(playerStateObj, i);
                    yield return new WaitForSeconds(stepDelay);
                }
            }
        }

        // Ensure final tileID
        SetPlayerTileID(playerStateObj, targetTile);

        // Publish movement finished via EventBus (playerStateObj expected to be PlayerState MonoBehaviour or similar)
        // Try to call EventBus.MovementFinished with reflection to avoid compile-time coupling
        try
        {
            // EventBus has method MovementFinished(PlayerState p, int tileID)
            MethodInfo ev = typeof(EventBus).GetMethod("MovementFinished", BindingFlags.Static | BindingFlags.Public);
            if (ev != null)
            {
                ev.Invoke(null, new object[] { playerStateObj, targetTile });
            }
            else
            {
                // fallback: try direct call if types match
                EventBus.MovementFinished((dynamic)playerStateObj, targetTile);
            }
        }
        catch
        {
            // ignore
            try { EventBus.MovementFinished((dynamic)playerStateObj, targetTile); } catch { }
        }
    }

    public void RequestMove(object playerStateObj, int targetTile)
    {
        StartCoroutine(MovePlayerToTileCoroutine(playerStateObj, targetTile));
    }

    #endregion

    #region Helpers (reflection & fallbacks)

    private object GetBoardManagerInstance()
    {
        // try static Instance property
        Type bmType = typeof(BoardManager);
        PropertyInfo pi = bmType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
        if (pi != null)
        {
            try { return pi.GetValue(null); } catch { }
        }

        // fallback to FindObjectOfType
        return FindObjectOfType<BoardManager>();
    }

    private int GetBoardTotalTiles()
    {
        var bm = GetBoardManagerInstance();
        if (bm != null)
        {
            var prop = bm.GetType().GetField("totalTiles", BindingFlags.Public | BindingFlags.Instance)
                       ?? (MemberInfo)bm.GetType().GetProperty("totalTiles", BindingFlags.Public | BindingFlags.Instance);
            try
            {
                FieldInfo fi = bm.GetType().GetField("totalTiles");
                if (fi != null) return (int)fi.GetValue(bm);
                PropertyInfo pi = bm.GetType().GetProperty("totalTiles");
                if (pi != null) return (int)pi.GetValue(bm);
            }
            catch { }
        }
        // default fallback
        return 100;
    }

    private object CreateTileProviderDelegate(MethodInfo targetMethod, Component pawnComp, Func<int, Vector3> provider)
    {
        // create delegate of type matching parameter if possible, otherwise return provider directly
        // This is a best-effort approach. If the target method expects a concrete delegate type, reflection invocation will attempt to accept a compatible delegate.
        return provider;
    }

    private GameObject ExtractGameObject(object obj)
    {
        if (obj == null) return null;
        if (obj is GameObject go) return go;
        if (obj is Component c) return c.gameObject;
        // try to grab 'gameObject' property
        var pi = obj.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
        if (pi != null)
        {
            try { return pi.GetValue(obj) as GameObject; } catch { }
        }
        return null;
    }

    private Vector3 GetPlayerPosition(object playerStateObj)
    {
        var go = ExtractGameObject(playerStateObj);
        if (go != null) return go.transform.position;
        return Vector3.zero;
    }

    private void SetPlayerPosition(object playerStateObj, Vector3 pos)
    {
        var go = ExtractGameObject(playerStateObj);
        if (go != null) go.transform.position = pos;
    }

    private int GetPlayerTileID(object playerStateObj)
    {
        if (playerStateObj == null) return -1;
        Type t = playerStateObj.GetType();

        // try property TileID, currentTileID, tileID
        PropertyInfo pi = t.GetProperty("TileID", BindingFlags.Public | BindingFlags.Instance);
        if (pi != null)
        {
            try { return Convert.ToInt32(pi.GetValue(playerStateObj)); } catch { }
        }
        FieldInfo fi = t.GetField("currentTileID", BindingFlags.Public | BindingFlags.Instance);
        if (fi != null) { try { return Convert.ToInt32(fi.GetValue(playerStateObj)); } catch { } }
        fi = t.GetField("TileID", BindingFlags.Public | BindingFlags.Instance);
        if (fi != null) { try { return Convert.ToInt32(fi.GetValue(playerStateObj)); } catch { } }
        // fallback try method GetTileID()
        MethodInfo mi = t.GetMethod("GetTileID", BindingFlags.Public | BindingFlags.Instance);
        if (mi != null) { try { return Convert.ToInt32(mi.Invoke(playerStateObj, null)); } catch { } }

        return -1;
    }

    private void SetPlayerTileID(object playerStateObj, int id)
    {
        if (playerStateObj == null) return;
        Type t = playerStateObj.GetType();
        PropertyInfo pi = t.GetProperty("TileID", BindingFlags.Public | BindingFlags.Instance);
        if (pi != null)
        {
            try { pi.SetValue(playerStateObj, id); return; } catch { }
        }
        FieldInfo fi = t.GetField("currentTileID", BindingFlags.Public | BindingFlags.Instance);
        if (fi != null) { try { fi.SetValue(playerStateObj, id); return; } catch { } }
        fi = t.GetField("TileID", BindingFlags.Public | BindingFlags.Instance);
        if (fi != null) { try { fi.SetValue(playerStateObj, id); return; } catch { } }
        MethodInfo mi = t.GetMethod("SetTileID", BindingFlags.Public | BindingFlags.Instance);
        if (mi != null) { try { mi.Invoke(playerStateObj, new object[] { id }); return; } catch { } }
        // last fallback: if playerStateObj is Component, try set via PlayerState property names if present
        var comp = playerStateObj as Component;
        if (comp != null)
        {
            // nothing else we can do
        }
    }

    #endregion
}
