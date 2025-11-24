using UnityEngine;

/// <summary>
/// CardEffectHandler
/// - Modular executor for card effects.
/// - ApplyCardEffect mutates player state or global state (e.g., buff, roll modifier).
/// - Method signature includes `ref int roll` so movement-influencing cards can modify roll value.
/// - This file contains placeholders/examples — sesuaikan efek kartu sesuai NewCardData/effectType.
/// </summary>
public class CardEffectHandler : MonoBehaviour
{
    public static CardEffectHandler Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Apply effect of card to player. May change roll by ref.
    /// Implement real effects here (movement buff, defense, disarm, roll modify, etc).
    /// </summary>
    public void ApplyCardEffect(PlayerState player, NewCardData card, ref int roll)
    {
        if (player == null || card == null) return;

        // Example mapping based on effectType name (adjust to your enum)
        switch (card.effectType)
        {
            case CardEffectType.HermesFavors:
                // Hermes: add to next roll or current roll? example add to roll now
                roll += card.intValue;
                Debug.Log($"[CardEffect] HermesFavors: +{card.intValue} to roll => {roll}");
                break;
            case CardEffectType.IsisProtection:
                // Set immunity on player (if PlayerState exposes property)
                TrySetPlayerIntProperty(player, "immuneToAllNegativeTurns", card.intValue);
                break;
            case CardEffectType.ShieldOfAthena:
                TrySetPlayerIntProperty(player, "immuneToSnakeUses", card.intValue);
                break;
            case CardEffectType.AresProvocation:
                TrySetPlayerBoolProperty(player, "hasAresProvocation", true);
                break;
            // Add movement type cards
            case CardEffectType.RaLight:
                // move player forward immediately by intValue (this might be applied as additional movement)
                // you could modify roll or call MovementSystem to move now
                roll += card.intValue;
                break;
            default:
                Debug.Log($"[CardEffect] Unhandled card effect: {card.effectType}");
                break;
        }

        // HOOK: spawn VFX / play SFX / UI update
    }

    #region Reflection helpers (safe)
    private void TrySetPlayerIntProperty(PlayerState player, string name, int value)
    {
        var t = player.GetType();
        var fi = t.GetField(name);
        if (fi != null && fi.FieldType == typeof(int))
        {
            fi.SetValue(player, value);
            return;
        }
        var pi = t.GetProperty(name);
        if (pi != null && pi.PropertyType == typeof(int) && pi.CanWrite)
        {
            pi.SetValue(player, value);
            return;
        }
    }

    private void TrySetPlayerBoolProperty(PlayerState player, string name, bool value)
    {
        var t = player.GetType();
        var fi = t.GetField(name);
        if (fi != null && fi.FieldType == typeof(bool))
        {
            fi.SetValue(player, value);
            return;
        }
        var pi = t.GetProperty(name);
        if (pi != null && pi.PropertyType == typeof(bool) && pi.CanWrite)
        {
            pi.SetValue(player, value);
            return;
        }
    }
    #endregion
}
