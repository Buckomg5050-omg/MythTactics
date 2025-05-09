// DebugHelper.cs
using UnityEngine;

public static class DebugHelper
{
    private static bool _enableLogs = true;

    public static void Log(object message, Object context = null)
    {
        if (!_enableLogs) return;
        Debug.Log($"[MythTactics] {message}", context);
    }

    public static void LogWarning(object message, Object context = null)
    {
        if (!_enableLogs) return;
        Debug.LogWarning($"[MythTactics] {message}", context);
    }

    public static void LogError(object message, Object context = null)
    {
        Debug.LogError($"[MythTactics] ERROR: {message}", context);
    }

    public static void SetLoggingEnabled(bool isEnabled)
    {
        _enableLogs = isEnabled;
        Debug.Log($"[MythTactics] DebugHelper logging has been {(isEnabled ? "ENABLED" : "DISABLED")}.");
    }
}