using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Thread-safe image cache manager with size limits to prevent memory leaks
    /// </summary>
    public static class ImageCacheManager
    {
        private const long MAX_CACHE_SIZE_MB = -1; // Unlimited cache size
        private const long MAX_CACHE_SIZE_BYTES = long.MaxValue; // No limit
        private const long CLEANUP_THRESHOLD_BYTES = long.MaxValue; // Never cleanup

        private static readonly ConcurrentDictionary<string, CacheEntry> _imageCache = new();
        private static readonly ConcurrentDictionary<string, CacheEntry> _ramImageCache = new();
        private static long _currentCacheSizeBytes = 0;
        private static long _currentRamCacheSizeBytes = 0;

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
                long sizeBytes = EstimateImageSize(image);
                _imageCache.AddOrUpdate(key,
                    k => {
                        System.Threading.Interlocked.Add(ref _currentCacheSizeBytes, sizeBytes);
                        return new CacheEntry(image, sizeBytes);
                    },
                    (k, existing) =>
                    {
                        System.Threading.Interlocked.Add(ref _currentCacheSizeBytes, sizeBytes - existing.SizeBytes);
                        existing.Image = image;
                        existing.LastAccessed = DateTime.Now;
                        existing.SizeBytes = sizeBytes;
                        return existing;
                    });

                // Cleanup if cache is getting too large
                if (_currentCacheSizeBytes > CLEANUP_THRESHOLD_BYTES)
                {
                    CleanupCache(_imageCache, ref _currentCacheSizeBytes);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to cache image {key}: {ex.Message}");
            }
        }

        public static BitmapImage? GetCachedRamImage(string key)
        {
            if (_ramImageCache.TryGetValue(key, out var entry))
            {
                entry.LastAccessed = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"🎯 RAM Cache HIT for key: {key}");
                return entry.Image;
            }
            System.Diagnostics.Debug.WriteLine($"❌ RAM Cache MISS for key: {key}");
            return null;
        }

        public static void CacheRamImage(string key, BitmapImage image)
        {
            try
            {
                long sizeBytes = EstimateImageSize(image);
                _ramImageCache.AddOrUpdate(key,
                    k => {
                        System.Threading.Interlocked.Add(ref _currentRamCacheSizeBytes, sizeBytes);
                        return new CacheEntry(image, sizeBytes);
                    },
                    (k, existing) =>
                    {
                        System.Threading.Interlocked.Add(ref _currentRamCacheSizeBytes, sizeBytes - existing.SizeBytes);
                        existing.Image = image;
                        existing.LastAccessed = DateTime.Now;
                        existing.SizeBytes = sizeBytes;
                        return existing;
                    });

                // Cleanup if cache is getting too large
                if (_currentRamCacheSizeBytes > CLEANUP_THRESHOLD_BYTES)
                {
                    CleanupCache(_ramImageCache, ref _currentRamCacheSizeBytes);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to cache RAM image {key}: {ex.Message}");
            }
        }

        private static void CleanupCache(ConcurrentDictionary<string, CacheEntry> cache, ref long currentCacheSizeBytes)
        {
            try
            {
                var ordered = cache.OrderBy(kvp => kvp.Value.LastAccessed).ToList();
                long sizeToRemove = currentCacheSizeBytes - MAX_CACHE_SIZE_BYTES;
                long removed = 0;
                int removedCount = 0;
                foreach (var kvp in ordered)
                {
                    if (currentCacheSizeBytes - removed <= MAX_CACHE_SIZE_BYTES)
                        break;
                    if (cache.TryRemove(kvp.Key, out var removedEntry))
                    {
                        removed += removedEntry.SizeBytes;
                        removedCount++;
                        try
                        {
                            removedEntry.Image?.ClearValue(BitmapImage.UriSourceProperty);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to clear image value: {ex.Message}");
                        }
                    }
                }
                System.Threading.Interlocked.Add(ref currentCacheSizeBytes, -removed);
                Debug.WriteLine($"Cleaned up {removedCount} cached images. Cache size: {currentCacheSizeBytes / (1024 * 1024)} MB");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cache cleanup failed: {ex.Message}");
            }
        }

        public static void ClearAllCaches()
        {
            try
            {
                _imageCache.Clear();
                _ramImageCache.Clear();
                _currentCacheSizeBytes = 0;
                _currentRamCacheSizeBytes = 0;
                Debug.WriteLine("All image caches cleared");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to clear caches: {ex.Message}");
            }
        }

        public static (int ImageCache, int RamCache, long ImageCacheMB, long RamCacheMB) GetCacheSizes()
        {
            return (_imageCache.Count, _ramImageCache.Count, _currentCacheSizeBytes / (1024 * 1024), _currentRamCacheSizeBytes / (1024 * 1024));
        }
    }
}