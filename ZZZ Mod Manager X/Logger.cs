using System;
using System.Diagnostics;
using System.IO;

namespace ZZZ_Mod_Manager_X
{
    public static class Logger
    {
        private static readonly string LogPath = PathManager.GetSettingsPath("Application.log");
        private static readonly object LogLock = new object();

        public static void LogInfo(string message)
        {
            Log("INFO", message);
        }

        public static void LogWarning(string message)
        {
            Log("WARNING", message);
        }

        public static void LogError(string message, Exception? exception = null)
        {
            var fullMessage = exception != null ? $"{message} - Exception: {exception}" : message;
            Log("ERROR", fullMessage);
        }

        public static void LogDebug(string message)
        {
            Debug.WriteLine($"[DEBUG] {message}");
            // Only log debug messages to console, not to file
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