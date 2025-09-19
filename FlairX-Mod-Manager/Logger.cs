using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace FlairX_Mod_Manager
{
    public static class Logger
    {
        private static readonly string LogPath = PathManager.GetSettingsPath("Application.log");
        private static readonly object LogLock = new object();

        public static void LogInfo(string message, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null)
        {
            Log("INFO", FormatMessage(message, callerName, callerFile));
        }

        public static void LogWarning(string message, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null)
        {
            Log("WARNING", FormatMessage(message, callerName, callerFile));
        }

        public static void LogError(string message, Exception? exception = null, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null)
        {
            var fullMessage = exception != null ? $"{message} - Exception: {exception}" : message;
            Log("ERROR", FormatMessage(fullMessage, callerName, callerFile));
        }

        public static void LogDebug(string message, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null)
        {
            var formattedMessage = FormatMessage(message, callerName, callerFile);
            Debug.WriteLine($"[DEBUG] {formattedMessage}");
            // Only log debug messages to console, not to file
        }

        public static void LogMethodEntry(string? additionalInfo = null, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null)
        {
            var message = string.IsNullOrEmpty(additionalInfo) ? "Method entered" : $"Method entered: {additionalInfo}";
            LogDebug(message, callerName, callerFile);
        }

        public static void LogMethodExit(string? additionalInfo = null, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null)
        {
            var message = string.IsNullOrEmpty(additionalInfo) ? "Method exited" : $"Method exited: {additionalInfo}";
            LogDebug(message, callerName, callerFile);
        }

        public static void LogGrid(string message, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null)
        {
            // Grid-specific logging that respects the GridLoggingEnabled setting
            if (!SettingsManager.Current.GridLoggingEnabled) return;
            
            var formattedMessage = $"GRID: {message}";
            LogDebug(formattedMessage, callerName, callerFile);
        }

        private static string FormatMessage(string message, string? callerName, string? callerFile)
        {
            var fileName = !string.IsNullOrEmpty(callerFile) ? Path.GetFileNameWithoutExtension(callerFile) : "Unknown";
            var methodName = !string.IsNullOrEmpty(callerName) ? callerName : "Unknown";
            return $"[{fileName}.{methodName}] {message}";
        }

        private static void Log(string level, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logMessage = $"[{timestamp}] [{level}] {message}";
            
            // Always log to debug console
            Debug.WriteLine(logMessage);

            // Log to file with thread safety
            try
            {
                lock (LogLock)
                {
                    var logDir = Path.GetDirectoryName(LogPath);
                    if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }
                    
                    File.AppendAllText(LogPath, logMessage + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        public static void ClearLog()
        {
            try
            {
                lock (LogLock)
                {
                    if (File.Exists(LogPath))
                    {
                        File.Delete(LogPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to clear log file: {ex.Message}");
            }
        }
    }
}