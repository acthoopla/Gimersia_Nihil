using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// DiceManager
/// - Singleton
/// - Mendukung physical dice prefab (opsional) atau simulated roll
/// - Tidak menggunakan 'dynamic'
/// - Mengirim hasil ke TurnManager via reflection-safe helper
/// </summary>
[DisallowMultipleComponent]
public class DiceManager : MonoBehaviour
{
    public static DiceManager Instance { get; private set; }

    [Header("Dice Prefabs (optional)")]
    public GameObject physicalDicePrefab;
    public GameObject followerDicePrefab;

    [Header("Simulation")]
    public bool allowSimulatedRoll = true;
    public int rngSeed = 0;

    private System.Random rng;

    // instantiated dice objects (optional)
    private GameObject activeDiceObj;
    private GameObject activeFollowerObj;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        rng = (rngSeed != 0) ? new System.Random(rngSeed) : null;
    }

    /// <summary>
    /// Public API — request roll for a player (PlayerState)
    /// </summary>
    public void RequestRollForPlayer(PlayerState player)
    {
        if (player == null)
        {
            Debug.LogWarning("[DiceManager] RequestRollForPlayer: player null");
            return;
        }
        StartCoroutine(RollRoutine(player));
    }

    private IEnumerator RollRoutine(PlayerState player)
    {
        if (player == null)
        {
            yield break;
        }

        // Check whether player has extra dice (reflection tolerant)
        int extraDice = GetPlayerExtraDiceRolls(player);
        bool useDual = extraDice > 0;

        // If physical dice prefab assigned, try to use it
        if (physicalDicePrefab != null)
        {
            if (activeDiceObj == null)
            {
                activeDiceObj = Instantiate(physicalDicePrefab);
                activeDiceObj.SetActive(false);
            }
            if (useDual && followerDicePrefab != null && activeFollowerObj == null)
            {
                activeFollowerObj = Instantiate(followerDicePrefab);
                activeFollowerObj.SetActive(false);
            }

            activeDiceObj.SetActive(true);
            if (activeFollowerObj != null) activeFollowerObj.SetActive(useDual);

            int roll1 = 0, roll2 = 0;

            // StartRoll if available
            TryCallMethodIfExistsOnComponent(activeDiceObj, "StartRoll");
            if (activeFollowerObj != null) TryCallMethodIfExistsOnComponent(activeFollowerObj, "StartRoll");

            // Wait for dice results via BuildWaitCoroutineForDice (safe)
            yield return StartCoroutine(BuildWaitCoroutineForDice(activeDiceObj, (r) => roll1 = r));
            if (useDual)
            {
                if (activeFollowerObj != null)
                    yield return StartCoroutine(BuildWaitCoroutineForDice(activeFollowerObj, (r) => roll2 = r));
                else
                    roll2 = SimulateSingleDieRoll();
            }

            int total = roll1 + roll2;
            // deliver
            InvokeTurnManagerOnDiceResult(player, total);
            yield break;
        }

        // No physical dice: simulated
        if (allowSimulatedRoll)
        {
            int r1 = SimulateSingleDieRoll();
            int r2 = useDual ? SimulateSingleDieRoll() : 0;
            int total = r1 + r2;
            yield return new WaitForSeconds(0.15f);
            InvokeTurnManagerOnDiceResult(player, total);
            yield break;
        }

        Debug.LogError("[DiceManager] No dice available and simulation disabled.");
        yield break;
    }

    /// <summary>
    /// Coroutine helper that will call the dice component's WaitForRollToStop(Action<int>) if available.
    /// Uses reflection safely (invocation done inside try/catch but yields outside).
    /// </summary>
    private IEnumerator BuildWaitCoroutineForDice(GameObject diceObj, Action<int> callback)
    {
        if (diceObj == null)
        {
            callback?.Invoke(SimulateSingleDieRoll());
            yield break;
        }

        Component diceComp = diceObj.GetComponent<MonoBehaviour>();
        if (diceComp == null)
        {
            callback?.Invoke(SimulateSingleDieRoll());
            yield break;
        }

        MethodInfo mi = diceComp.GetType().GetMethod("WaitForRollToStop", BindingFlags.Public | BindingFlags.Instance);
        if (mi == null)
        {
            // no such method
            callback?.Invoke(SimulateSingleDieRoll());
            yield break;
        }

        object ret = null;
        Exception invokeEx = null;

        // Invoke but DO NOT yield inside try/catch
        try
        {
            ret = mi.Invoke(diceComp, new object[] { callback });
        }
        catch (Exception ex)
        {
            invokeEx = ex;
        }

        if (invokeEx != null)
        {
            Debug.LogWarning("[DiceManager] WaitForRollToStop invoke failed: " + invokeEx.Message);
            callback?.Invoke(SimulateSingleDieRoll());
            yield break;
        }

        if (ret is IEnumerator co)
        {
            yield return StartCoroutine(co);
            yield break;
        }
        else
        {
            // the invoked method didn't return IEnumerator — fallback simulate
            callback?.Invoke(SimulateSingleDieRoll());
            yield break;
        }
    }

    private int SimulateSingleDieRoll()
    {
        if (rng != null) return rng.Next(1, 7);
        return UnityEngine.Random.Range(1, 7);
    }

    private void TryCallMethodIfExistsOnComponent(GameObject go, string methodName)
    {
        if (go == null || string.IsNullOrEmpty(methodName)) return;
        Component comp = go.GetComponent<MonoBehaviour>();
        if (comp == null) return;
        MethodInfo mi = comp.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        if (mi != null)
        {
            try { mi.Invoke(comp, null); }
            catch { }
        }
    }

    private int GetPlayerExtraDiceRolls(PlayerState player)
    {
        // prefer property/field if present
        try
        {
            Type t = player.GetType();
            var fi = t.GetField("extraDiceRolls", BindingFlags.Public | BindingFlags.Instance);
            if (fi != null) return Convert.ToInt32(fi.GetValue(player));
            var pi = t.GetProperty("extraDiceRolls", BindingFlags.Public | BindingFlags.Instance);
            if (pi != null) return Convert.ToInt32(pi.GetValue(player));
            // fallback other common names
            fi = t.GetField("extraDice", BindingFlags.Public | BindingFlags.Instance);
            if (fi != null) return Convert.ToInt32(fi.GetValue(player));
            pi = t.GetProperty("extraDice", BindingFlags.Public | BindingFlags.Instance);
            if (pi != null) return Convert.ToInt32(pi.GetValue(player));
        }
        catch { }
        return 0;
    }

    /// <summary>
    /// Invoke TurnManager.OnDiceResult in a reflection-safe way (no dynamic).
    /// </summary>
    private void InvokeTurnManagerOnDiceResult(PlayerState player, int total)
    {
        if (TurnManager.Instance == null)
        {
            Debug.LogWarning("[DiceManager] TurnManager.Instance is null");
            return;
        }

        Type tmType = TurnManager.Instance.GetType();
        // try strongly-typed method first: OnDiceResult(PlayerState, int)
        MethodInfo miStrong = tmType.GetMethod("OnDiceResult", new Type[] { typeof(PlayerState), typeof(int) });
        if (miStrong != null)
        {
            try { miStrong.Invoke(TurnManager.Instance, new object[] { player, total }); return; }
            catch (Exception ex) { Debug.LogWarning("[DiceManager] invoke OnDiceResult failed: " + ex.Message); }
        }

        // fallback: find method named OnDiceResult with 2 params
        try
        {
            MethodInfo mi = tmType.GetMethod("OnDiceResult", BindingFlags.Public | BindingFlags.Instance);
            if (mi != null)
            {
                var pars = mi.GetParameters();
                if (pars.Length == 2)
                {
                    mi.Invoke(TurnManager.Instance, new object[] { player, total });
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DiceManager] fallback invoke OnDiceResult failed: " + ex.Message);
        }

        Debug.LogWarning("[DiceManager] Could not deliver dice result to TurnManager.");
    }
}
