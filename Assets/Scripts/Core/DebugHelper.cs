// DebugHelper.cs
using UnityEngine;

/// <summary>
/// Provides static helper methods for logging custom debug messages.
/// This helps in filtering and managing game-specific logs.
/// This class is static and should NOT be attached to a GameObject.
/// </summary>
public static class DebugHelper
{
    private static bool _enableLogs = true; // Master switch for our custom logs

    /// <summary>
    /// Logs a standard message to the Unity Console, prefixed for easy identification.
    /// </summary>
    public static void Log(object message, Object context = null)
    {
        if (!_enableLogs) return;
        Debug.Log($"[MythTactics] {message}", context);
    }

    /// <summary>
    /// Logs a warning message to the Unity Console, prefixed for easy identification.
    /// </summary>
    public static void LogWarning(object message, Object context = null)
    {
        if (!_enableLogs) return;
        Debug.LogWarning($"[MythTactics] {message}", context);
    }

    /// <summary>
    /// Logs an error message to the Unity Console, prefixed for easy identification.
    /// </summary>
    public static void LogError(object message, Object context = null)
    {
        Debug.LogError($"[MythTactics] ERROR: {message}", context);
    }

    /// <summary>
    /// Globally enables or disables custom logging through DebugHelper.
    /// </summary>
    public static void SetLoggingEnabled(bool isEnabled)
    {
        _enableLogs = isEnabled;
        Debug.Log($"[MythTactics] DebugHelper logging has been {(isEnabled ? "ENABLED" : "DISABLED")}.");
    }
}