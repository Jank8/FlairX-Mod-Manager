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
                Logger.LogDebug($"Caching image: {key} ({sizeBytes / 1024}KB)");
                
                _imageCache.AddOrUpdate(key,
                    k => {
                        System.Threading.Interlocked.Add(ref _currentCacheSizeBytes, sizeBytes);
                        Logger.LogDebug($"Added new image to cache: {key}");
                        return new CacheEntry(image, sizeBytes);
                    },
                    (k, existing) =>
                    {
                        System.Threading.Interlocked.Add(ref _currentCacheSizeBytes, sizeBytes - existing.SizeBytes);
                        existing.Image = image;
                        existing.LastAccessed = DateTime.Now;
                        existing.SizeBytes = sizeBytes;
                        Logger.LogDebug($"Updated existing image in cache: {key}");
                        return existing;
                    });

                // Cleanup if cache is getting too large
                if (_currentCacheSizeBytes > CLEANUP_THRESHOLD_BYTES)
                {
                    Logger.LogInfo($"Image cache size exceeded threshold ({_currentCacheSizeBytes / (1024 * 1024)}MB), starting cleanup");
                    CleanupCache(_imageCache, ref _currentCacheSizeBytes);
                }
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
                Logger.LogDebug($"Caching RAM image: {key} ({sizeBytes / 1024}KB)");
                
                _ramImageCache.AddOrUpdate(key,
                    k => {
                        System.Threading.Interlocked.Add(ref _currentRamCacheSizeBytes, sizeBytes);
                        Logger.LogDebug($"Added new RAM image to cache: {key}");
                        return new CacheEntry(image, sizeBytes);
                    },
                    (k, existing) =>
                    {
                        System.Threading.Interlocked.Add(ref _currentRamCacheSizeBytes, sizeBytes - existing.SizeBytes);
                        existing.Image = image;
                        existing.LastAccessed = DateTime.Now;
                        existing.SizeBytes = sizeBytes;
                        Logger.LogDebug($"Updated existing RAM image in cache: {key}");
                        return existing;
                    });

                // Cleanup if cache is getting too large
                if (_currentRamCacheSizeBytes > CLEANUP_THRESHOLD_BYTES)
                {
                    Logger.LogInfo($"RAM cache size exceeded threshold ({_currentRamCacheSizeBytes / (1024 * 1024)}MB), starting cleanup");
                    CleanupCache(_ramImageCache, ref _currentRamCacheSizeBytes);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to cache RAM image: {key}", ex);
            }
        }

        private static void CleanupCache(ConcurrentDictionary<string, CacheEntry> cache, ref long currentCacheSizeBytes)
        {
            try
            {
                Logger.LogInfo($"Starting cache cleanup - Current size: {currentCacheSizeBytes / (1024 * 1024)}MB, Items: {cache.Count}");
                
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
                            Logger.LogError($"Failed to clear image value for {kvp.Key}", ex);
                        }
                    }
                }
                
                System.Threading.Interlocked.Add(ref currentCacheSizeBytes, -removed);
                Logger.LogInfo($"Cache cleanup completed - Removed {removedCount} images ({removed / (1024 * 1024)}MB), New size: {currentCacheSizeBytes / (1024 * 1024)}MB");
            }
            catch (Exception ex)
            {
                Logger.LogError("Cache cleanup failed", ex);
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
                _currentCacheSizeBytes = 0;
                _currentRamCacheSizeBytes = 0;
                
                Logger.LogInfo($"All image caches cleared - Image cache: {imageCount} items ({imageMB}MB), RAM cache: {ramCount} items ({ramMB}MB)");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to clear image caches", ex);
            }
        }

        public static (int ImageCache, int RamCache, long ImageCacheMB, long RamCacheMB) GetCacheSizes()
        {
            return (_imageCache.Count, _ramImageCache.Count, _currentCacheSizeBytes / (1024 * 1024), _currentRamCacheSizeBytes / (1024 * 1024));
        }
    }
}