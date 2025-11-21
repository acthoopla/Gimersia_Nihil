using System;
using UnityEngine;

/// <summary>
/// Global EventBus — simple static invoker untuk subsystems.
/// Memastikan ada overload / event untuk berbagai pola pemanggilan:
/// - DamageTaken(player, int)
/// - DamageTaken(player, int, string)
/// 
/// Jaga agar semua subsystem dapat subscribe tanpa coupling kuat.
/// </summary>
public static class EventBus
{
    // Player Movement
    public static event Action<PlayerState, int> OnMovementFinished;      // (player, tileID)

    // Tile Effects
    public static event Action<PlayerState, Tiles> OnTileLanded;          // (player, tileObject)

    // Damage/Combat
    public static event Action<PlayerState, int> OnDamageTaken;                 // (player, amount)
    public static event Action<PlayerState, int, string> OnDamageTakenDetailed; // (player, amount, source)
    public static event Action<PlayerState> OnPlayerDied;

    // Turn System
    public static event Action<PlayerState> OnTurnStarted;
    public static event Action<PlayerState> OnTurnEnded;

    // Card System
    public static event Action<PlayerState> OnCardDrawn;

    // === INVOKE / SHIM (overload-friendly) ===

    // Movement
    public static void MovementFinished(PlayerState p, int tileID)
        => OnMovementFinished?.Invoke(p, tileID);

    // Tile landed
    public static void TileLanded(PlayerState p, Tiles t)
        => OnTileLanded?.Invoke(p, t);

    // Damage: 2-arg version
    public static void DamageTaken(PlayerState p, int amount)
    {
        OnDamageTaken?.Invoke(p, amount);
        // Also forward to detailed event with "unknown" source so listeners of detailed always get notified
        OnDamageTakenDetailed?.Invoke(p, amount, "unknown");
    }

    // Damage: 3-arg version (caller can supply a source string like "AttackTile", "Boss", "Card", etc.)
    public static void DamageTaken(PlayerState p, int amount, string source)
    {
        OnDamageTaken?.Invoke(p, amount);
        OnDamageTakenDetailed?.Invoke(p, amount, source);
    }

    // Player died
    public static void PlayerDied(PlayerState p)
        => OnPlayerDied?.Invoke(p);

    // Turn events
    public static void TurnStarted(PlayerState p)
        => OnTurnStarted?.Invoke(p);

    public static void TurnEnded(PlayerState p)
        => OnTurnEnded?.Invoke(p);

    // Card drawn
    public static void CardDrawn(PlayerState p)
        => OnCardDrawn?.Invoke(p);
}
