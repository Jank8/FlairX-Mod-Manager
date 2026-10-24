using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Thread-safe image cache manager with size limits to prevent memory leaks
    /// </summary>
    /// <remarks>
    /// Two things were wrong here before: the byte counters were updated inside
    /// <see cref="ConcurrentDictionary{TKey,TValue}.AddOrUpdate(TKey, Func{TKey,TValue}, Func{TKey,TValue,TValue})"/>
    /// factories (which may run more than once under contention, so the total drifted away from
    /// reality and eviction stopped triggering), and eviction called
    /// <c>ClearValue(UriSourceProperty)</c> on bitmaps that tiles were still displaying, which
    /// blanked live tiles.
    /// </remarks>
    public static class ImageCacheManager
    {
        private const long MAX_CACHE_SIZE_MB = 1024; // 1GB cache limit
        private const long MAX_CACHE_SIZE_BYTES = MAX_CACHE_SIZE_MB * 1024 * 1024;
        private const long CLEANUP_THRESHOLD_BYTES = (long)(MAX_CACHE_SIZE_BYTES * 0.9); // Cleanup at 90% capacity

        private static readonly ConcurrentDictionary<string, CacheEntry> _imageCache = new();
        private static readonly ConcurrentDictionary<string, CacheEntry> _ramImageCache = new();
        private static long _currentCacheSizeBytes = 0;
        private static long _currentRamCacheSizeBytes = 0;

        // Only one eviction pass at a time; overlapping passes double-counted removals.
        private static int _cleanupRunning = 0;

        private class CacheEntry
        {
            public BitmapImage Image { get; set; }
            public DateTime LastAccessed { get; set; }
            public long SizeBytes { get; set; }

            public CacheEntry(BitmapImage image, long sizeBytes)
            {
                Image = image;
                LastAccessed = DateTime.Now;
                SizeBytes = sizeBytes;
            }
        }

        private static long EstimateImageSize(BitmapImage image)
        {
            try
            {
                // 4 bytes per pixel (RGBA)
                return (long)image.PixelWidth * (long)image.PixelHeight * 4;
            }
            catch
            {
                return 0;
            }
        }

        #region Public API

        public static BitmapImage? GetCachedImage(string key)
        {
            if (_imageCache.TryGetValue(key, out var entry))
            {
                entry.LastAccessed = DateTime.Now;
                return entry.Image;
            }
            return null;
        }

        public static void CacheImage(string key, BitmapImage image)
        {
            try
            {
                AddOrUpdateEntry(_imageCache, key, image, isRamCache: false);
                TryCleanupIfNeeded(_imageCache, isRamCache: false);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to cache image: {key}", ex);
            }
        }

        public static BitmapImage? GetCachedRamImage(string key)
        {
            if (_ramImageCache.TryGetValue(key, out var entry))
            {
                entry.LastAccessed = DateTime.Now;
                Logger.LogDebug($"RAM Cache HIT for key: {key}");
                return entry.Image;
            }
            Logger.LogDebug($"RAM Cache MISS for key: {key}");
            return null;
        }

        public static void CacheRamImage(string key, BitmapImage image)
        {
            try
            {
                AddOrUpdateEntry(_ramImageCache, key, image, isRamCache: true);
                TryCleanupIfNeeded(_ramImageCache, isRamCache: true);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to cache RAM image: {key}", ex);
            }
        }

        public static void ClearAllCaches()
        {
            try
            {
                int imageCount = _imageCache.Count;
                int ramCount = _ramImageCache.Count;
                long imageMB = _currentCacheSizeBytes / (1024 * 1024);
                long ramMB = _currentRamCacheSizeBytes / (1024 * 1024);

                _imageCache.Clear();
                _ramImageCache.Clear();
                Interlocked.Exchange(ref _currentCacheSizeBytes, 0);
                Interlocked.Exchange(ref _currentRamCacheSizeBytes, 0);

                Logger.LogInfo($"All image caches cleared - Image cache: {imageCount} items ({imageMB}MB), RAM cache: {ramCount} items ({ramMB}MB)");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to clear image caches", ex);
            }
        }

        public static void InvalidateImage(string key)
        {
            try
            {
                if (_imageCache.TryRemove(key, out var imageEntry))
                {
                    Interlocked.Add(ref _currentCacheSizeBytes, -imageEntry.SizeBytes);
                }

                if (_ramImageCache.TryRemove(key, out var ramEntry))
                {
                    Interlocked.Add(ref _currentRamCacheSizeBytes, -ramEntry.SizeBytes);
                }

                Logger.LogDebug($"Invalidated image cache for: {key}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to invalidate image cache for: {key}", ex);
            }
        }

        public static (int ImageCache, int RamCache, long ImageCacheMB, long RamCacheMB) GetCacheSizes()
        {
            return (_imageCache.Count, _ramImageCache.Count,
                    Interlocked.Read(ref _currentCacheSizeBytes) / (1024 * 1024),
                    Interlocked.Read(ref _currentRamCacheSizeBytes) / (1024 * 1024));
        }

        #endregion

        #region Internals

        private static long GetSize(bool isRamCache) =>
            isRamCache
                ? Interlocked.Read(ref _currentRamCacheSizeBytes)
                : Interlocked.Read(ref _currentCacheSizeBytes);

        private static void AdjustSize(bool isRamCache, long delta)
        {
            if (delta == 0) return;

            if (isRamCache)
                Interlocked.Add(ref _currentRamCacheSizeBytes, delta);
            else
                Interlocked.Add(ref _currentCacheSizeBytes, delta);
        }

        /// <summary>
        /// Inserts or replaces an entry and keeps the byte counter exact by applying the delta
        /// only once, after the winning insert/update.
        /// </summary>
        private static void AddOrUpdateEntry(ConcurrentDictionary<string, CacheEntry> cache, string key, BitmapImage image, bool isRamCache)
        {
            var sizeBytes = EstimateImageSize(image);

            while (true)
            {
                if (cache.TryGetValue(key, out var existing))
                {
                    var replacement = new CacheEntry(image, sizeBytes);
                    if (cache.TryUpdate(key, replacement, existing))
                    {
                        AdjustSize(isRamCache, sizeBytes - existing.SizeBytes);
                        return;
                    }
                    continue; // lost a race - retry
                }

                var created = new CacheEntry(image, sizeBytes);
                if (cache.TryAdd(key, created))
                {
                    AdjustSize(isRamCache, sizeBytes);
                    return;
                }
                // another thread added it first - retry as an update
            }
        }

        private static void TryCleanupIfNeeded(ConcurrentDictionary<string, CacheEntry> cache, bool isRamCache)
        {
            if (GetSize(isRamCache) <= CLEANUP_THRESHOLD_BYTES)
                return;

            // Single-flight: concurrent cleanup passes used to remove overlapping entries and
            // subtract their sizes twice.
            if (Interlocked.CompareExchange(ref _cleanupRunning, 1, 0) != 0)
                return;

            try
            {
                CleanupCache(cache, isRamCache);
            }
            finally
            {
                Interlocked.Exchange(ref _cleanupRunning, 0);
            }
        }

        private static void CleanupCache(ConcurrentDictionary<string, CacheEntry> cache, bool isRamCache)
        {
            try
            {
                var currentSize = GetSize(isRamCache);
                Logger.LogInfo($"Starting cache cleanup - Current size: {currentSize / (1024 * 1024)}MB, Items: {cache.Count}");

                // Snapshot ordered by age; entries added while we run keep their own accounting.
                var ordered = cache.OrderBy(kvp => kvp.Value.LastAccessed).ToList();
                long removed = 0;
                int removedCount = 0;

                foreach (var kvp in ordered)
                {
                    if (currentSize - removed <= MAX_CACHE_SIZE_BYTES)
                        break;

                    if (cache.TryRemove(kvp.Key, out var removedEntry))
                    {
                        removed += removedEntry.SizeBytes;
                        removedCount++;

                        // Deliberately not calling ClearValue(UriSourceProperty) here: the bitmap
                        // may still be bound to a visible tile, and clearing it blanked the tile.
                        // Dropping the cache reference is enough for the GC.
                    }
                }

                AdjustSize(isRamCache, -removed);

                Logger.LogInfo($"Cache cleanup completed - Removed {removedCount} images ({removed / (1024 * 1024)}MB), New size: {(currentSize - removed) / (1024 * 1024)}MB");
            }
            catch (Exception ex)
            {
                Logger.LogError("Cache cleanup failed", ex);
            }
        }

        #endregion
    }
}
