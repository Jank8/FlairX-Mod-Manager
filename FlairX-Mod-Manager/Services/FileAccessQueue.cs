using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Thread-safe file access queue that serializes read/write operations per file path.
    /// Prevents race conditions when multiple parts of the app access the same file.
    /// </summary>
    public static class FileAccessQueue
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();
        private static readonly ConcurrentDictionary<string, int> _waitingCount = new();

        private static SemaphoreSlim GetLock(string filePath)
        {
            var normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();
            return _fileLocks.GetOrAdd(normalizedPath, _ => new SemaphoreSlim(1, 1));
        }

        private static string GetFileName(string filePath) => Path.GetFileName(filePath);

        private static void LogWaitStart(string filePath, string operation)
        {
            var fileName = GetFileName(filePath);
            var normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();
            var waiting = _waitingCount.AddOrUpdate(normalizedPath, 1, (_, count) => count + 1);
            
            if (waiting > 1)
            {
                Logger.LogInfo($"[FileQueue] Waiting for {fileName} ({operation}) - {waiting} in queue");
            }
        }

        private static void LogWaitEnd(string filePath, string operation, long elapsedMs)
        {
            var fileName = GetFileName(filePath);
            var normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();
            _waitingCount.AddOrUpdate(normalizedPath, 0, (_, count) => Math.Max(0, count - 1));
            
            if (elapsedMs > 100) // Only log if waited more than 100ms
            {
                Logger.LogInfo($"[FileQueue] Acquired {fileName} ({operation}) after {elapsedMs}ms");
            }
        }

        /// <summary>
        /// Read file content with queued access
        /// </summary>
        public static async Task<string> ReadAllTextAsync(string filePath, CancellationToken token = default)
        {
            var semaphore = GetLock(filePath);
            LogWaitStart(filePath, "read");
            var sw = Stopwatch.StartNew();
            await semaphore.WaitAsync(token);
            LogWaitEnd(filePath, "read", sw.ElapsedMilliseconds);
            try
            {
                return await File.ReadAllTextAsync(filePath, token);
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Read file content synchronously with queued access
        /// </summary>
        public static string ReadAllText(string filePath)
        {
            var semaphore = GetLock(filePath);
            LogWaitStart(filePath, "read");
            var sw = Stopwatch.StartNew();
            semaphore.Wait();
            LogWaitEnd(filePath, "read", sw.ElapsedMilliseconds);
            try
            {
                return File.ReadAllText(filePath);
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Write file content with queued access
        /// </summary>
        public static async Task WriteAllTextAsync(string filePath, string content, CancellationToken token = default)
        {
            var semaphore = GetLock(filePath);
            LogWaitStart(filePath, "write");
            var sw = Stopwatch.StartNew();
            await semaphore.WaitAsync(token);
            LogWaitEnd(filePath, "write", sw.ElapsedMilliseconds);
            try
            {
                await File.WriteAllTextAsync(filePath, content, token);
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Write file content synchronously with queued access
        /// </summary>
        public static void WriteAllText(string filePath, string content)
        {
            var semaphore = GetLock(filePath);
            LogWaitStart(filePath, "write");
            var sw = Stopwatch.StartNew();
            semaphore.Wait();
            LogWaitEnd(filePath, "write", sw.ElapsedMilliseconds);
            try
            {
                File.WriteAllText(filePath, content);
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Execute custom file operation with queued access
        /// </summary>
        public static async Task<T> ExecuteAsync<T>(string filePath, Func<Task<T>> operation, CancellationToken token = default)
        {
            var semaphore = GetLock(filePath);
            LogWaitStart(filePath, "execute");
            var sw = Stopwatch.StartNew();
            await semaphore.WaitAsync(token);
            LogWaitEnd(filePath, "execute", sw.ElapsedMilliseconds);
            try
            {
                return await operation();
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Execute custom file operation with queued access (no return value)
        /// </summary>
        public static async Task ExecuteAsync(string filePath, Func<Task> operation, CancellationToken token = default)
        {
            var semaphore = GetLock(filePath);
            LogWaitStart(filePath, "execute");
            var sw = Stopwatch.StartNew();
            await semaphore.WaitAsync(token);
            LogWaitEnd(filePath, "execute", sw.ElapsedMilliseconds);
            try
            {
                await operation();
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Clean up unused locks (call periodically if needed)
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
                        _waitingCount.TryRemove(kvp.Key, out _);
                        removed++;
                    }
                }
            }
            if (removed > 0)
            {
                Logger.LogInfo($"[FileQueue] Cleanup: removed {removed} unused locks");
            }
        }
    }
}
