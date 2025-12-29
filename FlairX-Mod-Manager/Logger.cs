using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace FlairX_Mod_Manager
{
    public static class Logger
    {
        private static readonly string LogPath = PathManager.GetSettingsPath("Application.log");
        private static readonly string GridLogPath = PathManager.GetSettingsPath("GridLog.log");
        private static readonly string StatusKeeperLogPath = PathManager.GetSettingsPath("StatusKeeper.log");
        private static readonly object LogLock = new object();
        private static readonly object GridLogLock = new object();
        private static readonly object StatusKeeperLogLock = new object();
        private static bool _statusKeeperLogInitialized = false;

        public static void LogInfo(string message, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            Log("INFO", FormatMessage(message, callerName, callerFile, lineNumber));
        }

        public static void LogPerformance(string operation, TimeSpan duration, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            var message = $"PERFORMANCE: {operation} took {duration.TotalMilliseconds:F2}ms";
            Log("PERF", FormatMessage(message, callerName, callerFile, lineNumber));
        }

        public static IDisposable MeasurePerformance(string operation, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            return new PerformanceMeasurement(operation, callerName, callerFile, lineNumber);
        }

        private class PerformanceMeasurement : IDisposable
        {
            private readonly string _operation;
            private readonly string? _callerName;
            private readonly string? _callerFile;
            private readonly int _lineNumber;
            private readonly System.Diagnostics.Stopwatch _stopwatch;

            public PerformanceMeasurement(string operation, string? callerName, string? callerFile, int lineNumber)
            {
                _operation = operation;
                _callerName = callerName;
                _callerFile = callerFile;
                _lineNumber = lineNumber;
                _stopwatch = System.Diagnostics.Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                LogPerformance(_operation, _stopwatch.Elapsed, _callerName, _callerFile, _lineNumber);
            }
        }

        public static void LogWarning(string message, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            Log("WARNING", FormatMessage(message, callerName, callerFile, lineNumber));
        }

        public static void LogError(string message, Exception? exception = null, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            var fullMessage = exception != null ? $"{message} - Exception: {exception}" : message;
            Log("ERROR", FormatMessage(fullMessage, callerName, callerFile, lineNumber));
        }

        public static void LogDebug(string message, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            var formattedMessage = FormatMessage(message, callerName, callerFile, lineNumber);
            Debug.WriteLine($"[DEBUG] {formattedMessage}");
            // Only log debug messages to console, not to file
        }

        public static void LogMethodEntry(string? additionalInfo = null, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            var message = string.IsNullOrEmpty(additionalInfo) ? "Method entered" : $"Method entered: {additionalInfo}";
            LogDebug(message, callerName, callerFile, lineNumber);
        }

        public static void LogMethodExit(string? additionalInfo = null, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            var message = string.IsNullOrEmpty(additionalInfo) ? "Method exited" : $"Method exited: {additionalInfo}";
            LogDebug(message, callerName, callerFile, lineNumber);
        }

        public static void LogGrid(string message, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            // Grid-specific logging that respects the GridLoggingEnabled setting
            try
            {
                if (SettingsManager.Current?.GridLoggingEnabled != true) return;
            }
            catch
            {
                return; // SettingsManager not initialized - skip grid logging
            }
            
            var formattedMessage = FormatMessage(message, callerName, callerFile, lineNumber);
            
            // Log to debug console
            Debug.WriteLine($"[GRID] {formattedMessage}");
            
            // If ErrorOnlyLogging is enabled, skip file logging for non-errors/warnings
            try
            {
                if (SettingsManager.Current?.ErrorOnlyLogging == true) return;
            }
            catch
            {
                // SettingsManager not initialized - continue logging
            }
            
            // Log to GridLog.log file
            LogToFile(GridLogPath, GridLogLock, "GRID", formattedMessage);
        }

        /// <summary>
        /// Initialize StatusKeeper log file with header
        /// </summary>
        public static void InitStatusKeeperLog()
        {
            if (_statusKeeperLogInitialized) return;
            
            try
            {
                lock (StatusKeeperLogLock)
                {
                    var logDir = Path.GetDirectoryName(StatusKeeperLogPath);
                    if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }
                    
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd | HH:mm:ss");
                    File.WriteAllText(StatusKeeperLogPath, $"=== ModStatusKeeper Log Started at {timestamp} ===\n", System.Text.Encoding.UTF8);
                    _statusKeeperLogInitialized = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize StatusKeeper log: {ex.Message}");
            }
        }

        /// <summary>
        /// Log to StatusKeeper.log file
        /// </summary>
        public static void LogStatusKeeper(string message, string level = "INFO", [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            // Check if StatusKeeper logging is enabled
            try
            {
                if (SettingsManager.Current?.StatusKeeperLoggingEnabled != true) return;
            }
            catch
            {
                return; // SettingsManager not initialized - skip StatusKeeper logging
            }
            
            var formattedMessage = FormatMessage(message, callerName, callerFile, lineNumber);
            
            // Log to debug console
            Debug.WriteLine($"[StatusKeeper] [{level}] {formattedMessage}");
            
            // If ErrorOnlyLogging is enabled, only log errors and warnings
            try
            {
                if (SettingsManager.Current?.ErrorOnlyLogging == true && level != "ERROR" && level != "WARNING") return;
            }
            catch
            {
                // SettingsManager not initialized - continue logging
            }
            
            // Ensure log is initialized
            if (!_statusKeeperLogInitialized)
            {
                InitStatusKeeperLog();
            }
            
            // Log to StatusKeeper.log file
            LogToFile(StatusKeeperLogPath, StatusKeeperLogLock, level, formattedMessage);
        }

        /// <summary>
        /// Log StatusKeeper error
        /// </summary>
        public static void LogStatusKeeperError(string message, Exception? exception = null, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            var fullMessage = exception != null ? $"{message} - Exception: {exception}" : message;
            LogStatusKeeper(fullMessage, "ERROR", callerName, callerFile, lineNumber);
        }

        private static string FormatMessage(string message, string? callerName, string? callerFile, int lineNumber = 0)
        {
            var fileName = !string.IsNullOrEmpty(callerFile) ? Path.GetFileNameWithoutExtension(callerFile) : "Unknown";
            var methodName = !string.IsNullOrEmpty(callerName) ? callerName : "Unknown";
            var lineInfo = lineNumber > 0 ? $":{lineNumber}" : "";
            return $"[{fileName}.{methodName}{lineInfo}] {message}";
        }

        private static void LogToFile(string logPath, object lockObj, string level, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logMessage = $"[{timestamp}] [{level}] {message}";
            
            try
            {
                lock (lockObj)
                {
                    var logDir = Path.GetDirectoryName(logPath);
                    if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }
                    
                    File.AppendAllText(logPath, logMessage + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write to log file {logPath}: {ex.Message}");
            }
        }

        private static void Log(string level, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logMessage = $"[{timestamp}] [{level}] {message}";
            
            // Always log to debug console
            Debug.WriteLine(logMessage);

            // If ErrorOnlyLogging is enabled, only log errors and warnings to file
            // Use try-catch to handle case when SettingsManager is not yet initialized
            try
            {
                if (SettingsManager.Current?.ErrorOnlyLogging == true && level != "ERROR" && level != "WARNING")
                {
                    return;
                }
            }
            catch
            {
                // SettingsManager not initialized yet - log everything during startup
            }

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