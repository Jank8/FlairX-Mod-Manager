using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Thread-safe file access queue that serializes read/write operations per file path.
    /// Prevents race conditions when multiple parts of the app access the same file.
    /// </summary>
    /// <remarks>
    /// This type sits on the hottest path of the application (a mod rebuild performs thousands
    /// of calls), so the fast path allocates nothing: the path is normalized once, uncontended
    /// acquisitions never start a timer, and nothing is logged unless a caller actually blocks.
    /// </remarks>
    public static class FileAccessQueue
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Contention slow enough to be worth a log line.</summary>
        private const double ContentionLogThresholdMs = 50;

        private static SemaphoreSlim GetLock(string normalizedPath) =>
            _fileLocks.GetOrAdd(normalizedPath, static _ => new SemaphoreSlim(1, 1));

        private static string Normalize(string filePath) => Path.GetFullPath(filePath);

        private static void LogContention(string filePath, string operation, long startTimestamp)
        {
            if (startTimestamp == 0)
                return;

            var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            if (elapsedMs < ContentionLogThresholdMs)
                return;

            Logger.LogInfo($"[FileQueue] Waited {elapsedMs:F0}ms for {Path.GetFileName(filePath)} ({operation})");
        }

        /// <summary>
        /// Read file content with queued access.
        /// </summary>
        public static async Task<string> ReadAllTextAsync(string filePath, CancellationToken token = default)
        {
            var path = Normalize(filePath);
            var semaphore = GetLock(path);

            long start = 0;
            if (!semaphore.Wait(0))
            {
                start = Stopwatch.GetTimestamp();
                await semaphore.WaitAsync(token).ConfigureAwait(false);
            }

            try
            {
                return await File.ReadAllTextAsync(filePath, token).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
                LogContention(path, "read", start);
            }
        }

        /// <summary>
        /// Read file content synchronously with queued access.
        /// </summary>
        public static string ReadAllText(string filePath)
        {
            var path = Normalize(filePath);
            var semaphore = GetLock(path);

            long start = 0;
            if (!semaphore.Wait(0))
            {
                start = Stopwatch.GetTimestamp();
                semaphore.Wait();
            }

            try
            {
                return File.ReadAllText(filePath);
            }
            finally
            {
                semaphore.Release();
                LogContention(path, "read", start);
            }
        }

        /// <summary>
        /// Write file content with queued access.
        /// </summary>
        public static async Task WriteAllTextAsync(string filePath, string content, CancellationToken token = default)
        {
            var path = Normalize(filePath);
            var semaphore = GetLock(path);

            long start = 0;
            if (!semaphore.Wait(0))
            {
                start = Stopwatch.GetTimestamp();
                await semaphore.WaitAsync(token).ConfigureAwait(false);
            }

            try
            {
                await WriteAtomicallyAsync(path, content, token).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
                LogContention(path, "write", start);
            }
        }

        /// <summary>
        /// Write file content synchronously with queued access.
        /// </summary>
        public static void WriteAllText(string filePath, string content)
        {
            var path = Normalize(filePath);
            var semaphore = GetLock(path);

            long start = 0;
            if (!semaphore.Wait(0))
            {
                start = Stopwatch.GetTimestamp();
                semaphore.Wait();
            }

            try
            {
                WriteAtomically(path, content);
            }
            finally
            {
                semaphore.Release();
                LogContention(path, "write", start);
            }
        }

        /// <summary>
        /// Execute a custom file operation with queued access.
        /// </summary>
        public static async Task<T> ExecuteAsync<T>(string filePath, Func<Task<T>> operation, CancellationToken token = default)
        {
            var path = Normalize(filePath);
            var semaphore = GetLock(path);

            long start = 0;
            if (!semaphore.Wait(0))
            {
                start = Stopwatch.GetTimestamp();
                await semaphore.WaitAsync(token).ConfigureAwait(false);
            }

            try
            {
                return await operation().ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
                LogContention(path, "execute", start);
            }
        }

        /// <summary>
        /// Execute a custom file operation with queued access (no return value).
        /// </summary>
        public static async Task ExecuteAsync(string filePath, Func<Task> operation, CancellationToken token = default)
        {
            var path = Normalize(filePath);
            var semaphore = GetLock(path);

            long start = 0;
            if (!semaphore.Wait(0))
            {
                start = Stopwatch.GetTimestamp();
                await semaphore.WaitAsync(token).ConfigureAwait(false);
            }

            try
            {
                await operation().ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
                LogContention(path, "execute", start);
            }
        }

        /// <summary>
        /// Atomic write for callers that already hold the path lock - i.e. inside an
        /// <see cref="ExecuteAsync(string, Func{Task}, CancellationToken)"/> block, where calling
        /// <see cref="WriteAllTextAsync"/> would deadlock on the same non-reentrant semaphore.
        /// </summary>
        public static Task WriteAllTextUnlockedAsync(string filePath, string content, CancellationToken token = default)
            => WriteAtomicallyAsync(Path.GetFullPath(filePath), content, token);

        #region Atomic writes

        /// <summary>
        /// Writes through a temporary file in the same directory and swaps it in.
        /// </summary>
        /// <remarks>
        /// <c>File.WriteAllText</c> truncates the target in place: a crash, power loss or failed
        /// write part-way leaves a truncated JSON file. For mod.json that was actively
        /// destructive, because the startup repair path in <c>App.EnsureModJsonInXXMIMods</c>
        /// replaces an unparsable file with defaults - silently discarding the mod's metadata.
        /// </remarks>
        private static void WriteAtomically(string filePath, string content)
        {
            var tempPath = PrepareTempPath(filePath);
            try
            {
                File.WriteAllText(tempPath, content);
                CommitTempFile(tempPath, filePath);
            }
            finally
            {
                TryDeleteTempFile(tempPath);
            }
        }

        private static async Task WriteAtomicallyAsync(string filePath, string content, CancellationToken token)
        {
            var tempPath = PrepareTempPath(filePath);
            try
            {
                await File.WriteAllTextAsync(tempPath, content, token).ConfigureAwait(false);
                CommitTempFile(tempPath, filePath);
            }
            finally
            {
                TryDeleteTempFile(tempPath);
            }
        }

        private static string PrepareTempPath(string filePath)
        {
            var fullPath = Path.GetFullPath(filePath);

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Same directory, so the swap stays on one volume.
            return fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        }

        private static void CommitTempFile(string tempPath, string filePath)
        {
            if (File.Exists(filePath))
            {
                // Atomic on NTFS and keeps the original file's ACLs.
                File.Replace(tempPath, filePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, filePath);
            }
        }

        private static void TryDeleteTempFile(string tempPath)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best effort - a leftover temp file is harmless.
            }
        }

        #endregion

        /// <summary>
        /// Clean up unused locks (call periodically if needed).
        /// </summary>
        public static void Cleanup()
        {
            var removed = 0;
            foreach (var kvp in _fileLocks)
            {
                if (kvp.Value.CurrentCount == 1) // Not in use
                {
                    if (_fileLocks.TryRemove(kvp.Key, out _))
                    {
                        removed++;
                    }
                }
            }

            if (removed > 0)
            {
                Logger.LogInfo($"[FileQueue] Cleanup: removed {removed} unused locks");
            }
        }

        /// <summary>
        /// Move directory with queued access (for both source and destination).
        /// </summary>
        public static void MoveDirectory(string sourcePath, string destPath)
        {
            var normalizedSource = Normalize(sourcePath);
            var normalizedDest = Normalize(destPath);

            var sourceLock = GetLock(normalizedSource);
            var destLock = GetLock(normalizedDest);

            // Acquire locks in a consistent order to prevent deadlocks. The order is decided on
            // the normalized keys, otherwise two threads could disagree about which is "first".
            var (first, firstPath, second, _) =
                string.Compare(normalizedSource, normalizedDest, StringComparison.OrdinalIgnoreCase) < 0
                    ? (sourceLock, normalizedSource, destLock, normalizedDest)
                    : (destLock, normalizedDest, sourceLock, normalizedSource);

            long start = 0;
            if (!first.Wait(0))
            {
                start = Stopwatch.GetTimestamp();
                first.Wait();
            }

            try
            {
                if (!second.Wait(0))
                {
                    if (start == 0)
                        start = Stopwatch.GetTimestamp();
                    second.Wait();
                }

                try
                {
                    Directory.Move(sourcePath, destPath);
                }
                finally
                {
                    second.Release();
                }
            }
            finally
            {
                first.Release();
                LogContention(firstPath, "move", start);
            }
        }
    }
}
