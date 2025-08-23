using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
                
                var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
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
                                LastUpdated = lastUpdated
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

        private void ModsScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            var now = DateTime.Now;
            
            // Throttle during rapid scrolling - only process every 100ms
            if ((now - _lastScrollTime).TotalMilliseconds < 100)
            {
                return; // Skip this scroll event
            }
            
            _lastScrollTime = now;
            
            // Use low priority dispatcher to prevent blocking mouse wheel
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                // Load images when user scrolls
                LoadVisibleImages();
                
                // Load more ModTiles if user is scrolling near the end
                LoadMoreModTilesIfNeeded();
            });
            
            // If scrolling has stopped, trigger more aggressive disposal
            if (!e.IsIntermediate)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500); // Wait 500ms after scroll stops
                    DispatcherQueue.TryEnqueue(() => PerformAggressiveDisposal());
                });
            }
        }

        private void LoadMoreModTilesIfNeeded()
        {
            if (ModsScrollViewer == null || _allModData.Count == 0) return;
            
            // Check if we're near the bottom and need to load more items
            var scrollableHeight = ModsScrollViewer.ScrollableHeight;
            var currentVerticalOffset = ModsScrollViewer.VerticalOffset;
            var viewportHeight = ModsScrollViewer.ViewportHeight;
            
            // Load more when we're within 2 viewport heights of the bottom
            var loadMoreThreshold = scrollableHeight - (viewportHeight * 2);
            
            if (currentVerticalOffset >= loadMoreThreshold && _allMods.Count < _allModData.Count)
            {
                LoadMoreModTiles();
            }
        }

        private void LoadMoreModTiles()
        {
            var currentCount = _allMods.Count;
            var batchSize = CalculateInitialLoadCount(); // Load same batch size as initial
            var endIndex = Math.Min(currentCount + batchSize, _allModData.Count);
            
            LogToGridLog($"Loading more ModTiles: {currentCount} to {endIndex} out of {_allModData.Count}");
            
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
                    IsVisible = true,
                    ImageSource = null // Start with no image - lazy load when visible
                };
                _allMods.Add(modTile);
            }
            
            LogToGridLog($"Added {endIndex - currentCount} more ModTiles, total now: {_allMods.Count}");
        }

        private void LoadVisibleImages()
        {
            if (ModsGrid?.ItemsSource is not IEnumerable<ModTile> items) return;

            var visibleItems = new HashSet<ModTile>();
            var itemsToLoad = new List<ModTile>();
            var itemsToDispose = new List<ModTile>();

            foreach (var mod in items)
            {
                // Get the container for this item
                var container = ModsGrid.ContainerFromItem(mod) as GridViewItem;
                bool isVisible = container != null && IsItemVisible(container);
                
                if (isVisible)
                {
                    visibleItems.Add(mod);
                    
                    // Only load if image is not already loaded
                    if (mod.ImageSource == null)
                    {
                        itemsToLoad.Add(mod);
                    }
                }
                else if (mod.ImageSource != null && !IsItemInPreloadBuffer(container))
                {
                    // Item is not visible and not in preload buffer - candidate for disposal
                    itemsToDispose.Add(mod);
                }
            }

            // Load new images and apply scaling with error handling
            foreach (var mod in itemsToLoad)
            {
                try
                {
                    LogToGridLog($"LAZY LOAD: Loading image for {mod.Directory}");
                    mod.ImageSource = CreateBitmapImage(mod.ImagePath);
                    
                    // Apply scaling only if not at 100% zoom to reduce work
                    if (Math.Abs(ZoomFactor - 1.0) > 0.001)
                    {
                        var container = ModsGrid.ContainerFromItem(mod) as GridViewItem;
                        if (container?.ContentTemplateRoot is FrameworkElement root)
                        {
                            ApplyScalingToContainer(container, root);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogToGridLog($"ERROR: Failed to load image for {mod.Directory}: {ex.Message}");
                    // Skip this problematic mod and continue
                }
            }

            // Dispose images that are far from viewport (memory management)
            DisposeDistantImages(itemsToDispose);
            
            // Trigger garbage collection if we disposed many images
            if (itemsToDispose.Count > 20)
            {
                TriggerGarbageCollection();
            }
        }

        private bool IsItemInPreloadBuffer(GridViewItem? container)
        {
            if (ModsScrollViewer == null || container == null) return false;

            try
            {
                var transform = container.TransformToVisual(ModsScrollViewer);
                var containerBounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));
                var scrollViewerBounds = new Windows.Foundation.Rect(0, 0, ModsScrollViewer.ActualWidth, ModsScrollViewer.ActualHeight);

                // Reduced buffer - keep images loaded within 2 rows of viewport for better memory management
                var extendedBuffer = (container.ActualHeight + 24) * 2;
                var extendedTop = scrollViewerBounds.Top - extendedBuffer;
                var extendedBottom = scrollViewerBounds.Bottom + extendedBuffer;

                return containerBounds.Top < extendedBottom && containerBounds.Bottom > extendedTop;
            }
            catch
            {
                return false;
            }
        }

        private void DisposeDistantImages(List<ModTile> itemsToDispose)
        {
            if (itemsToDispose.Count == 0) return;

            var disposedCount = 0;
            foreach (var mod in itemsToDispose)
            {
                if (mod.ImageSource != null)
                {
                    try
                    {
                        // Clear the BitmapImage reference to free memory
                        mod.ImageSource = null;
                        disposedCount++;
                    }
                    catch (Exception ex)
                    {
                        LogToGridLog($"DISPOSAL: Error disposing image for {mod.Directory}: {ex.Message}");
                    }
                }
            }

            if (disposedCount > 0)
            {
                LogToGridLog($"DISPOSAL: Disposed {disposedCount} images to free memory");
                
                // Force immediate garbage collection after disposing many images
                if (disposedCount > 10)
                {
                    TriggerGarbageCollection();
                }
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
                var memoryBefore = GC.GetTotalMemory(false) / 1024 / 1024;
                
                // Force garbage collection
                GC.Collect(2, GCCollectionMode.Optimized);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Optimized);
                
                var memoryAfter = GC.GetTotalMemory(true) / 1024 / 1024;
                var memoryFreed = memoryBefore - memoryAfter;
                
                _lastGcTime = DateTime.Now;
                LogToGridLog($"GC: Freed {memoryFreed}MB (Before: {memoryBefore}MB, After: {memoryAfter}MB)");
            }
            catch (Exception ex)
            {
                LogToGridLog($"GC: Error during garbage collection: {ex.Message}");
            }
        }

        private void PerformAggressiveDisposal()
        {
            if (ModsGrid?.ItemsSource is not IEnumerable<ModTile> items) return;

            var itemsToDispose = new List<ModTile>();
            var totalLoaded = 0;

            foreach (var mod in items)
            {
                if (mod.ImageSource != null)
                {
                    totalLoaded++;
                    
                    // Get the container for this item
                    var container = ModsGrid.ContainerFromItem(mod) as GridViewItem;
                    
                    // Dispose if not in the 2-row buffer
                    if (!IsItemInPreloadBuffer(container))
                    {
                        itemsToDispose.Add(mod);
                    }
                }
            }

            if (itemsToDispose.Count > 0)
            {
                LogToGridLog($"AGGRESSIVE: Disposing {itemsToDispose.Count} images out of {totalLoaded} loaded");
                DisposeDistantImages(itemsToDispose);
            }
        }

        private bool IsItemVisible(GridViewItem container)
        {
            if (ModsScrollViewer == null || container == null) return false;

            try
            {
                var transform = container.TransformToVisual(ModsScrollViewer);
                var containerBounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));
                var scrollViewerBounds = new Windows.Foundation.Rect(0, 0, ModsScrollViewer.ActualWidth, ModsScrollViewer.ActualHeight);

                // Extend both top and bottom boundaries by 2 row heights for smooth scrolling in both directions
                var preloadBuffer = (container.ActualHeight + 24) * 2; // 2 rows with typical margins
                var extendedTop = scrollViewerBounds.Top - preloadBuffer;
                var extendedBottom = scrollViewerBounds.Bottom + preloadBuffer;

                // Check if container intersects with extended viewport (includes 2 rows above and below)
                return containerBounds.Left < scrollViewerBounds.Right &&
                       containerBounds.Right > scrollViewerBounds.Left &&
                       containerBounds.Top < extendedBottom &&
                       containerBounds.Bottom > extendedTop;
            }
            catch
            {
                return false;
            }
        }
    }
}