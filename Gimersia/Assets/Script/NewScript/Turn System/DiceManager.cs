using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// DiceManager (fixed)
/// - Does not reference undefined PhysicalDice type; instead uses GameObject prefab and reflection to find a dice component if present.
/// - WaitForRollToStop no longer uses yield inside try/catch patterns incorrectly.
/// - Calls TurnManager.Instance.OnDiceResult when done.
/// </summary>
[DisallowMultipleComponent]
public class DiceManager : MonoBehaviour
{
    public static DiceManager Instance { get; private set; }

    [Header("Dice Prefabs (optional)")]
    [Tooltip("If you have a physical dice prefab, assign its GameObject here. Otherwise simulated rolls will be used.")]
    public GameObject physicalDicePrefab;
    public GameObject followerDicePrefab;

    [Header("Simulation")]
    public bool allowSimulatedRoll = true;
    public int rngSeed = 0;

    private System.Random rng;

    // runtime dice objects if instantiated
    private GameObject activeDiceObj;
    private GameObject activeFollowerObj;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        rng = (rngSeed != 0) ? new System.Random(rngSeed) : null;
    }

    #region Public API

    public void RequestRollForPlayer(object playerStateObj)
    {
        if (playerStateObj == null)
        {
            Debug.LogWarning("[DiceManager] RequestRollForPlayer null");
            return;
        }
        StartCoroutine(RollRoutine(playerStateObj));
    }

    /// <summary>
    /// Waits for a single die result. If a physical dice object with a compatible component exists, it will attempt to use it via reflection.
    /// Otherwise returns simulated result.
    /// </summary>
    public IEnumerator WaitForRollToStop(Action<int> callback)
    {
        // If an active dice object exists and has a method WaitForRollToStop(Action<int>), we'll try to call it.
        if (activeDiceObj != null)
        {
            Component diceComp = activeDiceObj.GetComponent<MonoBehaviour>();
            if (diceComp != null)
            {
                MethodInfoWaitForRoll(diceComp, callback);
                // the method call above will start its own coroutine via reflection if possible.
                // If not invoked, we fallback below.
                yield break;
            }
        }

        // fallback: simulated
        int val = SimulateSingleDieRoll();
        callback?.Invoke(val);
        yield break;
    }

    #endregion

    #region Internal

    private IEnumerator RollRoutine(object playerStateObj)
    {
        if (TurnManager.Instance == null)
        {
            Debug.LogError("[DiceManager] TurnManager not found.");
            yield break;
        }

        // Determine if dual-dice requested by reading playerState.extraDiceRolls with reflection
        int extraDice = GetPlayerExtraDiceRolls(playerStateObj);
        bool useDual = extraDice > 0;

        // If a physical dice prefab is assigned, try to instantiate / use it
        if (physicalDicePrefab != null)
        {
            if (activeDiceObj == null)
            {
                activeDiceObj = Instantiate(physicalDicePrefab);
            }
            if (useDual && followerDicePrefab != null && activeFollowerObj == null)
            {
                activeFollowerObj = Instantiate(followerDicePrefab);
            }

            // try to run WaitForRollToStop on dice component(s) via reflection
            int roll1 = 0, roll2 = 0;
            bool succeeded1 = false, succeeded2 = false;

            Component diceComp = activeDiceObj.GetComponent<MonoBehaviour>();
            if (diceComp != null)
            {
                // Try to call StartRoll if present (non-blocking)
                TryCallMethodIfExists(diceComp, "StartRoll");

                // Now attempt to obtain result via WaitForRollToStop that accepts Action<int>
                IEnumerator waitCo = BuildWaitCoroutineForDice(diceComp, (r) => { roll1 = r; succeeded1 = true; });
                if (waitCo != null) yield return StartCoroutine(waitCo);
                else
                {
                    roll1 = SimulateSingleDieRoll();
                    succeeded1 = true;
                }
            }
            else
            {
                roll1 = SimulateSingleDieRoll();
                succeeded1 = true;
            }

            if (useDual)
            {
                if (activeFollowerObj != null)
                {
                    Component followerComp = activeFollowerObj.GetComponent<MonoBehaviour>();
                    TryCallMethodIfExists(followerComp, "StartRoll");
                    IEnumerator waitCo2 = BuildWaitCoroutineForDice(followerComp, (r) => { roll2 = r; succeeded2 = true; });
                    if (waitCo2 != null) yield return StartCoroutine(waitCo2);
                    else { roll2 = SimulateSingleDieRoll(); succeeded2 = true; }
                }
                else
                {
                    roll2 = SimulateSingleDieRoll();
                    succeeded2 = true;
                }
            }

            int total = roll1 + roll2;
            // deliver result
            TurnManager.Instance.OnDiceResult((dynamic)playerStateObj, total);
            yield break;
        }

        // No physical dice prefab -> simulated
        if (allowSimulatedRoll)
        {
            int r1 = SimulateSingleDieRoll();
            int r2 = useDual ? SimulateSingleDieRoll() : 0;
            int total = r1 + r2;
            yield return new WaitForSeconds(0.15f);
            TurnManager.Instance.OnDiceResult((dynamic)playerStateObj, total);
            yield break;
        }

        Debug.LogError("[DiceManager] No dice available and simulation disabled.");
        yield break;
    }

    #endregion

    #region Reflection helpers

    private void MethodInfoWaitForRoll(Component diceComp, Action<int> callback)
    {
        if (diceComp == null) return;
        var mi = diceComp.GetType().GetMethod("WaitForRollToStop", BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            // If method returns IEnumerator and takes Action<int>, we can StartCoroutine on it
            try
            {
                var ret = mi.Invoke(diceComp, new object[] { callback });
                if (ret is IEnumerator co)
                {
                    StartCoroutine(co);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DiceManager] reflection WaitForRollToStop failed: " + ex.Message);
            }
        }

        // fallback: simulate and invoke callback
        callback?.Invoke(SimulateSingleDieRoll());
    }

    private IEnumerator BuildWaitCoroutineForDice(Component diceComp, Action<int> callback)
    {
        if (diceComp == null)
        {
            callback?.Invoke(SimulateSingleDieRoll());
            yield break;
        }

        var mi = diceComp.GetType().GetMethod("WaitForRollToStop", BindingFlags.Public | BindingFlags.Instance);
        if (mi == null)
        {
            // no such method: simulate
            callback?.Invoke(SimulateSingleDieRoll());
            yield break;
        }

        object ret = null;
        Exception invokeException = null;

        // Invoke inside try/catch but DO NOT yield inside the try/catch
        try
        {
            ret = mi.Invoke(diceComp, new object[] { callback });
        }
        catch (Exception ex)
        {
            invokeException = ex;
        }

        if (invokeException != null)
        {
            Debug.LogWarning("[DiceManager] BuildWaitCoroutineForDice invoke failed: " + invokeException.Message);
            callback?.Invoke(SimulateSingleDieRoll());
            yield break;
        }

        // Now handle return value OUTSIDE of try/catch so yields are allowed
        if (ret is IEnumerator co)
        {
            yield return StartCoroutine(co);
            yield break;
        }
        else
        {
            // if it returned non-coroutine, fallback to simulate and invoke callback
            callback?.Invoke(SimulateSingleDieRoll());
            yield break;
        }
    }

    private void TryCallMethodIfExists(Component comp, string methodName)
    {
        if (comp == null) return;
        var mi = comp.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            try { mi.Invoke(comp, null); }
            catch { }
        }
    }

    #endregion

    #region Utilities

    private int SimulateSingleDieRoll()
    {
        if (rng != null) return rng.Next(1, 7);
        return UnityEngine.Random.Range(1, 7);
    }

    private int GetPlayerExtraDiceRolls(object playerStateObj)
    {
        if (playerStateObj == null) return 0;
        Type t = playerStateObj.GetType();
        var fi = t.GetField("extraDiceRolls", BindingFlags.Public | BindingFlags.Instance);
        if (fi != null) { try { return Convert.ToInt32(fi.GetValue(playerStateObj)); } catch { } }
        var pi = t.GetProperty("extraDiceRolls", BindingFlags.Public | BindingFlags.Instance);
        if (pi != null) { try { return Convert.ToInt32(pi.GetValue(playerStateObj)); } catch { } }
        // try other common names
        fi = t.GetField("extraDice", BindingFlags.Public | BindingFlags.Instance);
        if (fi != null) { try { return Convert.ToInt32(fi.GetValue(playerStateObj)); } catch { } }
        pi = t.GetProperty("extraDice", BindingFlags.Public | BindingFlags.Instance);
        if (pi != null) { try { return Convert.ToInt32(pi.GetValue(playerStateObj)); } catch { } }
        return 0;
    }

    #endregion
}
