using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace FlairX_Mod_Manager
{
    public static class Logger
    {
        private static readonly string LogPath = PathManager.GetSettingsPath("Application.log");
        private static readonly string GridLogPath = PathManager.GetSettingsPath("GridLog.log");
        private static readonly string StatusKeeperLogPath = PathManager.GetSettingsPath("StatusKeeper.log");
        private static readonly string HotkeyFinderLogPath = PathManager.GetSettingsPath("HotkeyFinder.log");
        private static readonly string GameBananaLogPath = PathManager.GetSettingsPath("GameBanana.log");

        private static readonly ConcurrentDictionary<string, LogFileWriter> _writers =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly object StatusKeeperLogLock = new object();
        private static bool _statusKeeperLogInitialized = false;

        // Anonymization setting (default: true for privacy)
        public static bool AnonymizeOutput { get; set; } = true;

        static Logger()
        {
            // Buffered writers flush on a timer; make sure the tail of the log survives shutdown.
            try
            {
                AppDomain.CurrentDomain.ProcessExit += static (_, _) => Flush();
            }
            catch
            {
                // Not fatal - log tail may be lost on exit.
            }
        }

        #region Public API

        public static void LogInfo(string message, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            Log(LogPath, "INFO", message, callerName, callerFile, lineNumber, flushImmediately: false);
        }

        public static void LogPerformance(string operation, TimeSpan duration, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            var message = $"PERFORMANCE: {operation} took {duration.TotalMilliseconds:F2}ms";
            Log(LogPath, "PERF", message, callerName, callerFile, lineNumber, flushImmediately: false);
        }

        public static IDisposable MeasurePerformance(string operation, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            return new PerformanceMeasurement(operation, callerName, callerFile, lineNumber);
        }

        private sealed class PerformanceMeasurement : IDisposable
        {
            private readonly string _operation;
            private readonly string? _callerName;
            private readonly string? _callerFile;
            private readonly int _lineNumber;
            private readonly Stopwatch _stopwatch;

            public PerformanceMeasurement(string operation, string? callerName, string? callerFile, int lineNumber)
            {
                _operation = operation;
                _callerName = callerName;
                _callerFile = callerFile;
                _lineNumber = lineNumber;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                LogPerformance(_operation, _stopwatch.Elapsed, _callerName, _callerFile, _lineNumber);
            }
        }

        public static void LogWarning(string message, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            Log(LogPath, "WARNING", message, callerName, callerFile, lineNumber, flushImmediately: true);
        }

        public static void LogError(string message, Exception? exception = null, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            var fullMessage = exception != null ? $"{message} - Exception: {PathAnonymizer.AnonymizeException(exception)}" : message;
            Log(LogPath, "ERROR", fullMessage, callerName, callerFile, lineNumber, flushImmediately: true);
        }

        /// <summary>
        /// Debug-only trace. Compiled out entirely in Release builds, so no string formatting
        /// and no dictionary lookups happen for these calls in shipped binaries.
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogDebug(string message, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            Debug.WriteLine($"[DEBUG] {FormatMessage(GetMessagePrefix(callerFile, callerName, lineNumber), message)}");
        }

        [Conditional("DEBUG")]
        public static void LogMethodEntry(string? additionalInfo = null, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            LogDebug(string.IsNullOrEmpty(additionalInfo) ? "Method entered" : $"Method entered: {additionalInfo}", callerName, callerFile, lineNumber);
        }

        [Conditional("DEBUG")]
        public static void LogMethodExit(string? additionalInfo = null, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            LogDebug(string.IsNullOrEmpty(additionalInfo) ? "Method exited" : $"Method exited: {additionalInfo}", callerName, callerFile, lineNumber);
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

            var formattedMessage = FormatMessage(GetMessagePrefix(callerFile, callerName, lineNumber), message);

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
            WriteToFile(GridLogPath, "GRID", formattedMessage, flushImmediately: false);
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
                    if (_statusKeeperLogInitialized) return;

                    var logDir = Path.GetDirectoryName(StatusKeeperLogPath);
                    if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }

                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd | HH:mm:ss");
                    File.WriteAllText(StatusKeeperLogPath, $"=== ModStatusKeeper Log Started at {timestamp} ===\n", new UTF8Encoding(false));
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

            var formattedMessage = FormatMessage(GetMessagePrefix(callerFile, callerName, lineNumber), message);

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
            WriteToFile(StatusKeeperLogPath, level, formattedMessage, level == "ERROR" || level == "WARNING");
        }

        /// <summary>
        /// Log StatusKeeper error
        /// </summary>
        public static void LogStatusKeeperError(string message, Exception? exception = null, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            var fullMessage = exception != null ? $"{message} - Exception: {PathAnonymizer.AnonymizeException(exception)}" : message;
            LogStatusKeeper(fullMessage, "ERROR", callerName, callerFile, lineNumber);
        }

        /// <summary>
        /// Log to HotkeyFinder.log file
        /// </summary>
        public static void LogHotkeyFinder(string message, string level = "INFO", [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            var formattedMessage = FormatMessage(GetMessagePrefix(callerFile, callerName, lineNumber), message);

            // Log to debug console
            Debug.WriteLine($"[HotkeyFinder] [{level}] {formattedMessage}");

            // If ErrorOnlyLogging is enabled, only log errors and warnings
            try
            {
                if (SettingsManager.Current?.ErrorOnlyLogging == true && level != "ERROR" && level != "WARNING") return;
            }
            catch
            {
                // SettingsManager not initialized - continue logging
            }

            // Log to HotkeyFinder.log file
            WriteToFile(HotkeyFinderLogPath, level, formattedMessage, level == "ERROR" || level == "WARNING");
        }

        /// <summary>
        /// Log HotkeyFinder error
        /// </summary>
        public static void LogHotkeyFinderError(string message, Exception? exception = null, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            var fullMessage = exception != null ? $"{message} - Exception: {PathAnonymizer.AnonymizeException(exception)}" : message;
            LogHotkeyFinder(fullMessage, "ERROR", callerName, callerFile, lineNumber);
        }

        /// <summary>
        /// Log to GameBanana.log file (browser, comments, API calls)
        /// </summary>
        public static void LogGameBanana(string message, string level = "INFO", [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            var formattedMessage = FormatMessage(GetMessagePrefix(callerFile, callerName, lineNumber), message);
            Debug.WriteLine($"[GameBanana] [{level}] {formattedMessage}");
            WriteToFile(GameBananaLogPath, level, formattedMessage, level == "ERROR" || level == "WARNING");
        }

        /// <summary>
        /// Log GameBanana error
        /// </summary>
        public static void LogGameBananaError(string message, Exception? exception = null, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null, [CallerLineNumber] int lineNumber = 0)
        {
            var fullMessage = exception != null ? $"{message} | Exception: {PathAnonymizer.AnonymizeException(exception)}" : message;
            LogGameBanana(fullMessage, "ERROR", callerName, callerFile, lineNumber);
        }

        /// <summary>
        /// Flushes every buffered log file to disk.
        /// </summary>
        public static void Flush()
        {
            foreach (var writer in _writers.Values)
            {
                writer.Flush();
            }
        }

        public static void ClearLog()
        {
            try
            {
                if (_writers.TryRemove(LogPath, out var writer))
                {
                    writer.Dispose();
                }

                if (File.Exists(LogPath))
                {
                    File.Delete(LogPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to clear log file: {ex.Message}");
            }
        }

        #endregion

        #region Formatting

        // Call sites are compile-time constants, so their formatted prefix is computed once.
        private static readonly ConcurrentDictionary<(string File, string Method, int Line), string> _prefixCache = new();

        private static string GetMessagePrefix(string? callerFile, string? callerName, int lineNumber)
        {
            return _prefixCache.GetOrAdd(
                (callerFile ?? string.Empty, callerName ?? string.Empty, lineNumber),
                static key => BuildMessagePrefix(key.File, key.Method, key.Line));
        }

        private static string BuildMessagePrefix(string callerFile, string callerName, int lineNumber)
        {
            var fileName = !string.IsNullOrEmpty(callerFile) ? Path.GetFileNameWithoutExtension(callerFile) : "Unknown";
            var methodName = !string.IsNullOrEmpty(callerName) ? callerName : "Unknown";
            var lineInfo = lineNumber > 0 ? $":{lineNumber}" : "";
            return $"[{fileName}.{methodName}{lineInfo}] ";
        }

        private static string FormatMessage(string prefix, string message) => prefix + message;

        #endregion

        #region File writing

        private static void Log(string logPath, string level, string message, string? callerName, string? callerFile, int lineNumber, bool flushImmediately)
        {
            var formattedMessage = FormatMessage(GetMessagePrefix(callerFile, callerName, lineNumber), message);

            // Always log to debug console
            Debug.WriteLine($"[{level}] {formattedMessage}");

            // If ErrorOnlyLogging is enabled, only log errors and warnings to file.
            // Use try-catch to handle case when SettingsManager is not yet initialized.
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

            WriteToFile(logPath, level, formattedMessage, flushImmediately);
        }

        private static void WriteToFile(string logPath, string level, string formattedMessage, bool flushImmediately)
        {
            string line;
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var anonymizedMessage = AnonymizeOutput ? PathAnonymizer.Anonymize(formattedMessage) : formattedMessage;
                line = $"[{timestamp}] [{level}] {anonymizedMessage}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to format log entry: {ex.Message}");
                return;
            }

            var writer = _writers.GetOrAdd(logPath, static path => new LogFileWriter(path));
            writer.Write(line, flushImmediately);
        }

        /// <summary>
        /// Buffered append-only writer for a single log file.
        /// Keeping the handle open removes a CreateFile/CloseHandle pair per log line, which used
        /// to run under a process-wide lock and stall every thread that logged.
        /// </summary>
        private sealed class LogFileWriter : IDisposable
        {
            private const int FlushIntervalMs = 1000;
            private const int FileBufferSize = 64 * 1024;
            private const int MaxLinesPerFile = 10_000;
            private const int ArchivedFilesToKeep = 3;

            private readonly string _path;
            private readonly object _gate = new object();
            private StreamWriter? _writer;
            private long _lastFlushTick;
            private long _linesWritten;

            public LogFileWriter(string path)
            {
                _path = path;
            }

            public void Write(string line, bool flushImmediately)
            {
                lock (_gate)
                {
                    try
                    {
                        _writer ??= CreateWriter();
                        _writer.WriteLine(line);
                        _linesWritten++;

                        var now = Environment.TickCount64;
                        if (flushImmediately || now - _lastFlushTick >= FlushIntervalMs)
                        {
                            _writer.Flush();
                            _lastFlushTick = now;
                        }

                        // Keep the active log bounded; without this Application.log grew
                        // for the whole session and became slow to open.
                        if (_linesWritten >= MaxLinesPerFile)
                        {
                            RotateLocked();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to write to log file {_path}: {ex.Message}");
                        DisposeLocked();
                    }
                }
            }

            public void Flush()
            {
                lock (_gate)
                {
                    try
                    {
                        _writer?.Flush();
                        _lastFlushTick = Environment.TickCount64;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to flush log file {_path}: {ex.Message}");
                    }
                }
            }

            private StreamWriter CreateWriter()
            {
                var logDir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                // Shares read access so the user can tail the log while the app is running.
                var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, FileBufferSize);
                var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096) { AutoFlush = false };
                _lastFlushTick = Environment.TickCount64;
                _linesWritten = CountExistingLines();
                return writer;
            }

            private long CountExistingLines()
            {
                try
                {
                    return File.Exists(_path) ? File.ReadLines(_path).LongCount() : 0;
                }
                catch
                {
                    return 0;
                }
            }

            private void RotateLocked()
            {
                try
                {
                    DisposeLocked();

                    // Shift Application.log.2 -> .3, .1 -> .2, active -> .1.
                    for (int i = ArchivedFilesToKeep - 1; i >= 1; i--)
                    {
                        var source = $"{_path}.{i}";
                        var target = $"{_path}.{i + 1}";
                        if (!File.Exists(source))
                        {
                            continue;
                        }

                        if (File.Exists(target))
                        {
                            File.Delete(target);
                        }
                        File.Move(source, target);
                    }

                    if (File.Exists(_path))
                    {
                        var firstArchive = $"{_path}.1";
                        if (File.Exists(firstArchive))
                        {
                            File.Delete(firstArchive);
                        }
                        File.Move(_path, firstArchive);
                    }

                    _writer = CreateWriter();
                    var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    _writer.WriteLine($"[{stamp}] [INFO] Log rotated after {MaxLinesPerFile} lines");
                    _linesWritten = 1;
                    _writer.Flush();
                    _lastFlushTick = Environment.TickCount64;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to rotate log file {_path}: {ex.Message}");
                }
            }

            private void DisposeLocked()
            {
                try
                {
                    _writer?.Dispose();
                }
                catch
                {
                    // Ignore - we are already on the error path.
                }
                finally
                {
                    _writer = null;
                }
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    DisposeLocked();
                }
            }
        }

        #endregion
    }
}
