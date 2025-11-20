using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace FlairX_Mod_Manager.Pages
{
    /// <summary>
    /// ModGridPage partial class - Loading systems (background loading, lazy loading, viewport management)
    /// </summary>
    public sealed partial class ModGridPage : Page
    {
        // Thread-safe Background Loading
        private static volatile bool _isBackgroundLoading = false;
        private static Task? _backgroundLoadTask = null;
        private static readonly object _backgroundLoadLock = new object();

        private static void StartBackgroundLoadingIfNeeded()
        {
            lock (_backgroundLoadLock)
            {
                if (!_isBackgroundLoading && _backgroundLoadTask == null)
                {
                    _isBackgroundLoading = true;
                    _backgroundLoadTask = Task.Run(async () =>
                    {
                        try
                        {
                            await BackgroundLoadModDataAsync();
                        }
                        finally
                        {
                            lock (_backgroundLoadLock)
                            {
                                _isBackgroundLoading = false;
                                _backgroundLoadTask = null;
                            }
                        }
                    });
                }
            }
        }

        private static async Task BackgroundLoadModDataAsync()
        {
            try
            {
                LogToGridLog("BACKGROUND: Starting background mod data loading");
                
                var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                
                if (!Directory.Exists(modLibraryPath)) return;
                
                var categoryDirs = Directory.GetDirectories(modLibraryPath);
                var totalDirs = 0;
                var processed = 0;
                
                // Count total mod directories for progress tracking
                foreach (var categoryDir in categoryDirs)
                {
                    if (Directory.Exists(categoryDir))
                    {
                        totalDirs += Directory.GetDirectories(categoryDir).Length;
                    }
                }
                
                foreach (var categoryDir in categoryDirs)
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = Path.GetFileName(categoryDir);
                    
                    foreach (var modDir in Directory.GetDirectories(categoryDir))
                    {
                        var modJsonPath = Path.Combine(modDir, "mod.json");
                        if (!File.Exists(modJsonPath)) continue;
                        
                        var dirName = Path.GetFileName(modDir);
                    
                    // Check if we need to load/update this mod's data
                    lock (_cacheLock)
                    {
                        var lastWriteTime = File.GetLastWriteTime(modJsonPath);
                        
                        if (_modJsonCache.TryGetValue(dirName, out var cachedData) &&
                            _modFileTimestamps.TryGetValue(dirName, out var cachedTime) &&
                            cachedTime >= lastWriteTime)
                        {
                            // Already cached and up to date
                            continue;
                        }
                        
                        // Load and cache the data
                        try
                        {
                            var json = File.ReadAllText(modJsonPath);
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            var modCharacter = root.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "other" : "other";
                            var modAuthor = root.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? "" : "";
                            var modUrl = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                            var isNSFW = root.TryGetProperty("isNSFW", out var nsfwProp) && nsfwProp.ValueKind == JsonValueKind.True;
                            
                            // Parse dates for sorting
                            var lastChecked = DateTime.MinValue;
                            var lastUpdated = DateTime.MinValue;
                            
                            if (root.TryGetProperty("dateChecked", out var dateCheckedProp) && dateCheckedProp.ValueKind == JsonValueKind.String)
                            {
                                DateTime.TryParse(dateCheckedProp.GetString(), out lastChecked);
                            }
                            
                            if (root.TryGetProperty("dateUpdated", out var dateUpdatedProp) && dateUpdatedProp.ValueKind == JsonValueKind.String)
                            {
                                DateTime.TryParse(dateUpdatedProp.GetString(), out lastUpdated);
                            }
                            
                            // Check for available updates
                            bool hasUpdate = false;
                            if (root.TryGetProperty("gbChangeDate", out var gbChangeProp) && gbChangeProp.ValueKind == JsonValueKind.String &&
                                root.TryGetProperty("dateUpdated", out var dateUpdProp) && dateUpdProp.ValueKind == JsonValueKind.String)
                            {
                                if (DateTime.TryParse(gbChangeProp.GetString(), out var gbDate) && 
                                    DateTime.TryParse(dateUpdProp.GetString(), out var updatedDate))
                                {
                                    hasUpdate = gbDate > updatedDate;
                                }
                            }
                            
                            // If dates are not in JSON, use file system dates as fallback
                            if (lastChecked == DateTime.MinValue)
                            {
                                lastChecked = File.GetLastAccessTime(modDir);
                            }
                            
                            if (lastUpdated == DateTime.MinValue)
                            {
                                lastUpdated = File.GetLastWriteTime(modDir);
                            }
                            
                            var name = Path.GetFileName(modDir);
                            string previewPath = GetOptimalImagePathStatic(modDir);
                            
                            var modData = new ModData
                            { 
                                Name = name, 
                                ImagePath = previewPath, 
                                Directory = dirName, 
                                IsActive = false, // Will be updated when actually used
                                Character = modCharacter,
                                Author = modAuthor,
                                Url = modUrl,
                                Category = categoryName,
                                LastChecked = lastChecked,
                                LastUpdated = lastUpdated,
                                HasUpdate = hasUpdate,
                                IsNSFW = isNSFW
                            };
                            
                            // Cache the data
                            _modJsonCache[dirName] = modData;
                            _modFileTimestamps[dirName] = lastWriteTime;
                        }
                        catch
                        {
                            // Skip problematic files
                        }
                    }
                    
                        processed++;
                        
                        // Small delay to prevent overwhelming the system
                        if (processed % 10 == 0)
                        {
                            await Task.Delay(1);
                        }
                    }
                }
                
                LogToGridLog($"BACKGROUND: Completed background loading - processed {processed}/{totalDirs} directories");
            }
            catch (Exception ex)
            {
                LogToGridLog($"BACKGROUND: Error during background loading: {ex.Message}");
            }
        }

        private static string GetOptimalImagePathStatic(string modDirectory)
        {
            // Static version for background loading
            string webpPath = Path.Combine(modDirectory, "minitile.webp");
            if (File.Exists(webpPath))
            {
                return webpPath;
            }
            
            // Check for JPEG minitile (fallback when WebP encoder not available)
            string minitileJpegPath = Path.Combine(modDirectory, "minitile.jpg");
            if (File.Exists(minitileJpegPath))
            {
                return minitileJpegPath;
            }
            
            string jpegPath = Path.Combine(modDirectory, "preview.jpg");
            return jpegPath;
        }

        private void ModGridPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Monitor scroll changes to trigger lazy loading
            if (ModsScrollViewer != null)
            {
                ModsScrollViewer.ViewChanged += ModsScrollViewer_ViewChanged;
                // Remove ScrollViewer wheel handler - use page level instead
                // Initial load of visible images
                LoadVisibleImages();
            }
            // Monitor window size changes to reload visible images
            this.SizeChanged += ModGridPage_SizeChanged;
            
            // Apply saved zoom level when page loads
            if (Math.Abs(_zoomFactor - 1.0) > 0.001)
            {
                UpdateGridItemSizes();
            }
            
            // Update zoom indicator on startup
            var mainWindow = (Application.Current as App)?.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.UpdateZoomIndicator(_zoomFactor);
            }
            
            // Force focus for WinUI 3 wheel event handling
            if (ModsScrollViewer != null)
            {
                ModsScrollViewer.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                ModsScrollViewer.PointerWheelChanged += ModsScrollViewer_PointerWheelChanged;
            }
            
            // Add global pointer handler to refocus on click
            this.PointerPressed += (s, e) => ModsScrollViewer?.Focus(Microsoft.UI.Xaml.FocusState.Pointer);
        }

        private void ModGridPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // When window is resized, the viewport changes so we need to reload visible images
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to let the layout update
                DispatcherQueue.TryEnqueue(() => 
                {
                    LoadVisibleImages();
                    
                    // Force ScrollViewer reset for WinUI 3 wheel issues
                    if (ModsScrollViewer != null)
                    {
                        ModsScrollViewer.UpdateLayout();
                    }
                });
            });
        }

        // Throttling for scroll events - prevent too many calls
        private DateTime _lastLoadCheck = DateTime.MinValue;
        private const int LOAD_THROTTLE_MS = 200; // Check every 200ms max
        
        // Limit concurrent image loads to prevent overwhelming the system
        private static readonly SemaphoreSlim _imageLoadSemaphore = new SemaphoreSlim(5, 5); // Max 5 concurrent
        private static readonly HashSet<string> _currentlyLoading = new HashSet<string>();
        private static readonly object _loadingLock = new object();
        
        private void ModsScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            // Only load when scroll has stopped (debouncing)
            if (!e.IsIntermediate)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(50); // Small delay for stability
                    
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                    {
                        // Load images and check if need more tiles
                        LoadVisibleImages();
                        LoadMoreModTilesIfNeeded();
                    });
                    
                    // Aggressive disposal after loading
                    await Task.Delay(300);
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => 
                    {
                        PerformAggressiveDisposal();
                    });
                });
            }
        }

        private void LoadMoreModTilesIfNeeded()
        {
            if (ModsScrollViewer == null || _allModData.Count == 0) return;
            if (_allMods.Count >= _allModData.Count) return; // All loaded
            
            // Incremental loading: load when within 3 viewport heights of bottom
            var scrollableHeight = ModsScrollViewer.ScrollableHeight;
            var currentVerticalOffset = ModsScrollViewer.VerticalOffset;
            var viewportHeight = ModsScrollViewer.ViewportHeight;
            
            var loadMoreThreshold = scrollableHeight - (viewportHeight * 3);
            
            if (currentVerticalOffset >= loadMoreThreshold)
            {
                LoadMoreModTilesIncremental();
            }
        }

        private void LoadMoreModTilesIncremental()
        {
            var currentCount = _allMods.Count;
            const int batchSize = 20; // Fixed batch size for incremental loading
            var endIndex = Math.Min(currentCount + batchSize, _allModData.Count);
            
            // Simple loop - no background thread needed
            for (int i = currentCount; i < endIndex; i++)
            {
                var modData = _allModData[i];
                var modTile = new ModTile 
                { 
                    Name = modData.Name, 
                    ImagePath = modData.ImagePath, 
                    Directory = modData.Directory, 
                    IsActive = modData.IsActive,
                    Category = modData.Category,
                    Author = modData.Author,
                    Url = modData.Url,
                    LastChecked = modData.LastChecked,
                    LastUpdated = modData.LastUpdated,
                    HasUpdate = CheckForUpdateLive(modData.Directory),
                    IsVisible = true,
                    ImageSource = null // Lazy load via LoadVisibleImages
                };
                _allMods.Add(modTile);
            }
        }

        // Fixed tile dimensions for index-based visibility
        private const double TILE_HEIGHT = 333.0; // 277 image + 56 caption
        private const double TILE_WIDTH = 277.0;
        private const double TILE_MARGIN = 24.0;
        
        private async void LoadVisibleImages()
        {
            if (ModsGrid?.ItemsSource is not IEnumerable<ModTile> items) return;
            if (ModsScrollViewer == null) return;

            var itemsList = items.ToList();

            // Calculate visible range using index-based approach (100x faster than TransformToVisual)
            var scrollOffset = ModsScrollViewer.VerticalOffset;
            var viewportHeight = ModsScrollViewer.ViewportHeight;
            var viewportWidth = ModsScrollViewer.ActualWidth;
            
            // Calculate items per row based on viewport width
            var effectiveTileWidth = (TILE_WIDTH + TILE_MARGIN) * _zoomFactor;
            var itemsPerRow = Math.Max(1, (int)(viewportWidth / effectiveTileWidth));
            
            // Calculate visible row range with buffer
            var effectiveTileHeight = (TILE_HEIGHT + TILE_MARGIN) * _zoomFactor;
            var firstVisibleRow = Math.Max(0, (int)(scrollOffset / effectiveTileHeight) - 2); // 2 row buffer above
            var lastVisibleRow = (int)((scrollOffset + viewportHeight) / effectiveTileHeight) + 2; // 2 row buffer below
            
            // Calculate visible item indices
            var firstVisibleIndex = firstVisibleRow * itemsPerRow;
            var lastVisibleIndex = Math.Min((lastVisibleRow + 1) * itemsPerRow, itemsList.Count);

            // Collect items to load and dispose
            var itemsToLoad = new List<ModTile>();
            var itemsToDispose = new List<ModTile>();

            for (int i = 0; i < itemsList.Count; i++)
            {
                var mod = itemsList[i];
                bool isVisible = i >= firstVisibleIndex && i < lastVisibleIndex;
                
                if (isVisible && mod.ImageSource == null)
                {
                    itemsToLoad.Add(mod);
                }
                else if (!isVisible && mod.ImageSource != null)
                {
                    // Only dispose if RAM threshold exceeded (Lazy Disposal - #2)
                    var currentMemory = GC.GetTotalMemory(false);
                    if (currentMemory > 500 * 1024 * 1024) // 500MB threshold
                    {
                        itemsToDispose.Add(mod);
                    }
                }
            }

            // Load images asynchronously in background - CRITICAL: don't block UI thread!
            foreach (var mod in itemsToLoad)
            {
                _ = LoadImageAsync(mod);
            }

            // Dispose images if needed
            if (itemsToDispose.Count > 0)
            {
                DisposeDistantImages(itemsToDispose);
            }
        }
        
        private async Task LoadImageAsync(ModTile mod)
        {
            // Skip if already loading this image
            lock (_loadingLock)
            {
                if (_currentlyLoading.Contains(mod.ImagePath))
                    return;
                _currentlyLoading.Add(mod.ImagePath);
            }
            
            try
            {
                // Wait for semaphore slot (max 5 concurrent loads)
                await _imageLoadSemaphore.WaitAsync();
                
                try
                {
                    // Read file on background thread to avoid blocking UI
                    var imageData = await Task.Run(() =>
                    {
                        try
                        {
                            if (File.Exists(mod.ImagePath))
                            {
                                return File.ReadAllBytes(mod.ImagePath);
                            }
                            return null;
                        }
                        catch
                        {
                            return null;
                        }
                    });
                    
                    // Decode on UI thread (required by WinUI 3)
                    if (imageData != null)
                    {
                        var bitmap = new BitmapImage();
                        using (var memStream = new MemoryStream(imageData))
                        {
                            await bitmap.SetSourceAsync(memStream.AsRandomAccessStream());
                        }
                        
                        mod.ImageSource = bitmap;
                        
                        // Apply scaling only if not at 100% zoom
                        if (Math.Abs(ZoomFactor - 1.0) > 0.001)
                        {
                            var container = ModsGrid.ContainerFromItem(mod) as GridViewItem;
                            if (container?.ContentTemplateRoot is FrameworkElement root)
                            {
                                ApplyScalingToContainer(container, root);
                            }
                        }
                    }
                }
                finally
                {
                    _imageLoadSemaphore.Release();
                }
            }
            finally
            {
                lock (_loadingLock)
                {
                    _currentlyLoading.Remove(mod.ImagePath);
                }
            }
        }

        private void DisposeDistantImages(List<ModTile> itemsToDispose)
        {
            if (itemsToDispose.Count == 0) return;

            foreach (var mod in itemsToDispose)
            {
                if (mod.ImageSource != null)
                {
                    try
                    {
                        mod.ImageSource = null;
                    }
                    catch
                    {
                        // Ignore disposal errors
                    }
                }
            }
            
            // Trigger GC only if we disposed a lot of images
            if (itemsToDispose.Count > 20)
            {
                TriggerGarbageCollection();
            }
        }

        private static DateTime _lastGcTime = DateTime.MinValue;
        private static readonly TimeSpan GC_COOLDOWN = TimeSpan.FromSeconds(5);

        private void TriggerGarbageCollection()
        {
            // Only trigger GC if enough time has passed since last GC
            if (DateTime.Now - _lastGcTime < GC_COOLDOWN) return;

            try
            {
                // Force garbage collection (optimized mode)
                GC.Collect(2, GCCollectionMode.Optimized);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Optimized);
                
                _lastGcTime = DateTime.Now;
            }
            catch
            {
                // Ignore GC errors
            }
        }

        private void PerformAggressiveDisposal()
        {
            // Aggressive disposal now uses RAM threshold - only dispose if over limit
            var currentMemory = GC.GetTotalMemory(false);
            if (currentMemory < 500 * 1024 * 1024) return; // Below 500MB threshold, keep images
            
            if (ModsGrid?.ItemsSource is not IEnumerable<ModTile> items) return;

            var itemsToDispose = new List<ModTile>();
            var itemsList = items.ToList();
            
            // Use same index-based calculation as LoadVisibleImages
            if (ModsScrollViewer == null) return;
            
            var scrollOffset = ModsScrollViewer.VerticalOffset;
            var viewportHeight = ModsScrollViewer.ViewportHeight;
            var viewportWidth = ModsScrollViewer.ActualWidth;
            
            var effectiveTileWidth = (TILE_WIDTH + TILE_MARGIN) * _zoomFactor;
            var itemsPerRow = Math.Max(1, (int)(viewportWidth / effectiveTileWidth));
            
            var effectiveTileHeight = (TILE_HEIGHT + TILE_MARGIN) * _zoomFactor;
            var firstVisibleRow = Math.Max(0, (int)(scrollOffset / effectiveTileHeight) - 2);
            var lastVisibleRow = (int)((scrollOffset + viewportHeight) / effectiveTileHeight) + 2;
            
            var firstVisibleIndex = firstVisibleRow * itemsPerRow;
            var lastVisibleIndex = Math.Min((lastVisibleRow + 1) * itemsPerRow, itemsList.Count);

            for (int i = 0; i < itemsList.Count; i++)
            {
                var mod = itemsList[i];
                if (mod.ImageSource != null && (i < firstVisibleIndex || i >= lastVisibleIndex))
                {
                    itemsToDispose.Add(mod);
                }
            }

            if (itemsToDispose.Count > 0)
            {
                DisposeDistantImages(itemsToDispose);
            }
        }
    }
}