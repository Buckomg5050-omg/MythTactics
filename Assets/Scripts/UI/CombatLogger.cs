using UnityEngine;
using System;
using MythTactics.Combat;

public static class CombatLogger
{
    // Event to send a formatted message and its color to the UI
    public static event Action<string, Color> OnLogMessage;

    // Default color
    private static Color _defaultColor = Color.white;

    // Method for game systems to call
    public static void LogEvent(string message)
    {
        LogEvent(message, _defaultColor);
    }

    public static void LogEvent(string message, Color color)
    {
        Debug.Log($"CombatLog: {message}"); // Also log to Unity console for debugging
        OnLogMessage?.Invoke(message, color);
    }

    // Example helper for damage
    public static void LogDamage(Unit attacker, Unit target, int damage, DamageType damageType, bool isCritical)
    {
        string critString = isCritical ? " (CRITICAL!)" : "";
        // Color coding can be done here or passed as parameter
        Color messageColor = Color.yellow; // Example color for damage
        if (target.CompareTag("Player")) messageColor = new Color(1f, 0.5f, 0.5f); // Reddish for player taking damage
        else if (attacker.CompareTag("Player")) messageColor = new Color(0.5f, 1f, 0.5f); // Greenish for player dealing damage

        LogEvent($"{attacker.unitName} deals {damage} {damageType}{critString} damage to {target.unitName}.", messageColor);
    }

    // Example helper for healing
    public static void LogHeal(Unit caster, Unit target, int amount)
    {
        LogEvent($"{caster.unitName} heals {target.unitName} for {amount} VP.", Color.green);
    }

    // Example helper for status effect application
    public static void LogStatusApplied(Unit target, string effectName, Unit caster)
    {
        string casterName = caster != null ? caster.unitName : "Unknown Source";
        LogEvent($"{target.unitName} receives {effectName} from {casterName}.", Color.cyan);
    }

    // Example helper for movement
    public static void LogMovement(Unit unit, int tilesMoved)
    {
        LogEvent($"{unit.unitName} moved {tilesMoved} tiles.", new Color(0.7f, 0.7f, 1f)); // Light blue
    }

    // Example helper for turn events
    public static void LogTurnEvent(string message)
    {
        LogEvent($"--- {message} ---", Color.grey); // Divider-like
    }
}