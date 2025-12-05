using System;
using System.Collections.Concurrent;
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

        private static SemaphoreSlim GetLock(string filePath)
        {
            var normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();
            return _fileLocks.GetOrAdd(normalizedPath, _ => new SemaphoreSlim(1, 1));
        }

        /// <summary>
        /// Read file content with queued access
        /// </summary>
        public static async Task<string> ReadAllTextAsync(string filePath, CancellationToken token = default)
        {
            var semaphore = GetLock(filePath);
            await semaphore.WaitAsync(token);
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
            semaphore.Wait();
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
            await semaphore.WaitAsync(token);
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
            semaphore.Wait();
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
            await semaphore.WaitAsync(token);
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
            await semaphore.WaitAsync(token);
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
            foreach (var kvp in _fileLocks)
            {
                if (kvp.Value.CurrentCount == 1) // Not in use
                {
                    _fileLocks.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}
