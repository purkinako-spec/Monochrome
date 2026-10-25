using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AiderErrorLogger
{
    private static readonly string logDir = "AiderLogs";
    private static readonly string logPath = "AiderLogs/unity_errors.log";

    static AiderErrorLogger()
    {
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        Application.logMessageReceived -= HandleLog;
        Application.logMessageReceived += HandleLog;
    }

    private static void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            try
            {
                string logMessage = $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{type}] {logString}\n{stackTrace}\n\n";
                File.AppendAllText(logPath, logMessage);
            }
            catch
            {
            }
        }
    }

    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        if (!EditorUtility.scriptCompilationFailed)
        {
            try
            {
                if (File.Exists(logPath))
                {
                    File.WriteAllText(logPath, string.Empty);
                }
            }
            catch
            {
            }
        }
    }
}
