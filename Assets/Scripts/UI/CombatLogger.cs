using UnityEngine;
using System;
// Assuming LogMessageType is accessible here, e.g., defined above or in an imported namespace
// If not, and you put it in a separate file/namespace, add the 'using' directive.
// For this example, I'll assume it's defined just above this class or in the same file.

/*
public enum LogMessageType
{
    System, TurnFlow, CombatAction, StatusChange, Movement, DebugInfo
}
*/
public enum LogMessageType
{
    System,         // General system messages, combat start/end, round changes
    TurnFlow,       // Unit turn start/end
    CombatAction,   // Attacks, ability damage/effects, healing
    StatusChange,   // Effects applied, removed, ticked (DoT/HoT results)
    Movement,
    Debug           // For messages you might only want during development
}
public static class CombatLogger
{
    // MODIFIED: Event now includes LogMessageType
    public static event Action<string, Color, LogMessageType> OnLogMessage;

    private static Color _defaultColor = Color.white;
    private static LogMessageType _defaultType = LogMessageType.System;

    // Main logging method
    public static void LogEvent(string message, Color color, LogMessageType type)
    {
        Debug.Log($"CombatLog [{type}]: {message}");
        OnLogMessage?.Invoke(message, color, type);
    }

    // Overload for default color and type
    public static void LogEvent(string message)
    {
        LogEvent(message, _defaultColor, _defaultType);
    }

    // Overload for specified color, default type
    public static void LogEvent(string message, Color color)
    {
        LogEvent(message, color, _defaultType);
    }


    // --- Helper Methods updated to use specific LogMessageTypes ---

    public static void LogDamage(Unit attacker, Unit target, int damage, MythTactics.Combat.DamageType damageType, bool isCritical)
    {
        string critString = isCritical ? " (CRITICAL!)" : "";
        Color messageColor = Color.yellow;
        if (target.CompareTag("Player")) messageColor = new Color(1f, 0.5f, 0.5f);
        else if (attacker.CompareTag("Player")) messageColor = new Color(0.5f, 1f, 0.5f);

        LogEvent($"{attacker.unitName} deals {damage} {damageType}{critString} damage to {target.unitName}.", messageColor, LogMessageType.CombatAction);
    }

    public static void LogHeal(Unit caster, Unit target, int amount)
    {
        LogEvent($"{caster.unitName} heals {target.unitName} for {amount} VP.", Color.green, LogMessageType.CombatAction);
    }

    public static void LogStatusApplied(Unit target, string effectName, Unit caster)
    {
        string casterName = caster != null ? caster.unitName : "System"; // Changed "Unknown Source" to "System"
        LogEvent($"{target.unitName} receives {effectName} from {casterName}.", Color.cyan, LogMessageType.StatusChange);
    }

    public static void LogStatusRefreshed(Unit target, string effectName, Unit caster)
    {
        string casterName = caster != null ? caster.unitName : "System";
        LogEvent($"{effectName} on {target.unitName} (from {casterName}) duration refreshed.", Color.cyan, LogMessageType.StatusChange);
    }

    public static void LogStatusStacked(Unit target, string effectName, int newStackCount, Unit caster)
    {
        string casterName = caster != null ? caster.unitName : "System";
        LogEvent($"{effectName} on {target.unitName} (from {casterName}) stacked to x{newStackCount}.", Color.cyan, LogMessageType.StatusChange);
    }

    public static void LogStatusExpired(Unit target, string effectName)
    {
        LogEvent($"{effectName} on {target.unitName} expired.", Color.gray, LogMessageType.StatusChange);
    }

    public static void LogStatusTickDamage(Unit target, string effectName, int damage, MythTactics.Combat.DamageType damageType, Unit caster)
    {
        string casterName = caster != null ? caster.unitName : "System";
        LogEvent($"{target.unitName} takes {damage} {damageType} damage from '{effectName}' tick (from {casterName}).", Color.magenta, LogMessageType.StatusChange);
    }

    public static void LogStatusTickHeal(Unit target, string effectName, int amount, Unit caster)
    {
        string casterName = caster != null ? caster.unitName : "System";
        LogEvent($"{target.unitName} heals {amount} VP from '{effectName}' tick (from {casterName}).", Color.green, LogMessageType.StatusChange);
    }

    public static void LogMovement(Unit unit, int tilesMoved)
    {
        if (tilesMoved > 0) // Only log if actual movement occurred
        {
            LogEvent($"{unit.unitName} moved {tilesMoved} tiles.", new Color(0.7f, 0.7f, 1f), LogMessageType.Movement);
        }
    }

    public static void LogTurnEvent(string message) // Typically System or TurnFlow
    {
        // Let's make turn start/end more specific
        if (message.ToLower().Contains("turn start"))
        {
            LogEvent($"--- {message} ---", Color.white, LogMessageType.TurnFlow);
        }
        else if (message.ToLower().Contains("turn end"))
        {
            LogEvent($"--- {message} ---", new Color(0.8f,0.8f,0.8f), LogMessageType.TurnFlow);
        }
        else // General system messages like Combat Start/End, Round Begin
        {
            LogEvent($"--- {message} ---", Color.gray, LogMessageType.System);
        }
    }

    public static void LogAbilityUsed(Unit caster, string abilityName, Unit target = null)
    {
        string targetString = target != null ? $" targeting {target.unitName}" : (abilityName.ToLower().Contains("self") ? " on SELF" : "");
        LogEvent($"{caster.unitName} uses {abilityName}{targetString}.", Color.white, LogMessageType.CombatAction);
    }

    public static void LogMiss(Unit attacker, string actionName, Unit target)
    {
        LogEvent($"{attacker.unitName}'s {actionName} MISSES {target.unitName}.", Color.yellow, LogMessageType.CombatAction);
    }

    public static void LogAPSpent(Unit unit, int amountSpent, int currentAP, int maxAP)
    {
        LogEvent($"{unit.unitName} spent {amountSpent} AP (Now: {currentAP}/{maxAP}).", new Color(0.8f, 0.8f, 0.3f), LogMessageType.System); // Or CombatAction if preferred
    }

    public static void LogResourceRegen(Unit unit, string message) // message already formatted with details
    {
        LogEvent(message, Color.blue, LogMessageType.System);
    }

     public static void LogDefeated(Unit defeatedUnit, Unit defeater)
    {
        string defeaterName = defeater != null ? defeater.unitName : "effects";
        LogEvent($"{defeatedUnit.unitName} has been defeated by {defeaterName}!", Color.magenta, LogMessageType.System);
    }

    // MODIFIED: Added LogLevelUp method
    public static void LogLevelUp(Unit unit, int newLevel)
    {
        if (unit == null) return;
        LogEvent($"{unit.unitName} has reached Level {newLevel}!", new Color(1f, 0.84f, 0f), LogMessageType.System); // Gold color for level up
    }
    // End of MODIFICATION
}