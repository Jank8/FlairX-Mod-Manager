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
        // Thread-safe Background Loading (without cache)
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
                LogToGridLog("BACKGROUND: Starting background mod data pre-loading");
                
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
                
                // Pre-load mod.json files in background to warm up file system cache
                foreach (var categoryDir in categoryDirs)
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    foreach (var modDir in Directory.GetDirectories(categoryDir))
                    {
                        var modJsonPath = Path.Combine(modDir, "mod.json");
                        if (!File.Exists(modJsonPath)) continue;
                        
                        try
                        {
                            // Just read the file to warm up OS file cache
                            _ = Services.FileAccessQueue.ReadAllText(modJsonPath);
                        }
                        catch
                        {
                            // Skip problematic files
                        }
                        
                        processed++;
                        
                        // Small delay to prevent overwhelming the system
                        if (processed % 20 == 0)
                        {
                            await Task.Delay(1);
                        }
                    }
                }
                
                LogToGridLog($"BACKGROUND: Completed pre-loading - processed {processed}/{totalDirs} mod.json files");
            }
            catch (Exception ex)
            {
                LogToGridLog($"BACKGROUND: Error during pre-loading: {ex.Message}");
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
            
            // No fallback to preview.jpg - return empty if no minitile exists
            return string.Empty;
        }

        private void ModGridPage_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo("ModGridPage_Loaded: Starting");
            
            // Rebuild mod lists on first load and then load mods into grid
            Task.Run(async () =>
            {
                try
                {
                    ModListManager.RebuildAllLists();
                    
                    // Sync _activeMods with actual folder state after rebuild
                    await DispatcherQueue.EnqueueAsync(() =>
                    {
                        SyncActiveModsWithFolders();
                    });
                    
                    // After lists are built, load mods into grid on UI thread
                    await DispatcherQueue.EnqueueAsync(() =>
                    {
                        try
                        {
                            Logger.LogInfo($"ModGridPage_Loaded: CurrentViewMode = {CurrentViewMode}, _currentCategory = {_currentCategory}");
                            
                            // Don't load anything if OnNavigatedTo already loaded a specific category
                            if (!string.IsNullOrEmpty(_currentCategory))
                            {
                                Logger.LogInfo($"ModGridPage_Loaded: Skipping load because category '{_currentCategory}' is already loaded");
                                return;
                            }
                            
                            // Check current view mode and load appropriate view
                            if (CurrentViewMode == ViewMode.Categories)
                            {
                                LoadCategories();
                            }
                            else
                            {
                                LoadAllMods();
                            }
                            // LoadVisibleImages() is called by LoadAllMods/LoadCategories
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Failed to load mods after rebuild", ex);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to rebuild mod lists", ex);
                }
            });
            
            // Monitor scroll changes to trigger lazy loading
            if (ModsScrollViewer != null)
            {
                ModsScrollViewer.ViewChanged += ModsScrollViewer_ViewChanged;

                // After first layout pass, reload visible images with correct viewport dimensions
                // This catches the case where LoadVisibleImages was called with viewport=0 at startup
                ModsScrollViewer.LayoutUpdated += OnScrollViewerFirstLayout;
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

        private System.Threading.CancellationTokenSource? _resizeDebounceToken;

        private bool _firstLayoutDone = false;
        private void OnScrollViewerFirstLayout(object? sender, object e)
        {
            if (_firstLayoutDone) return;
            if (ModsScrollViewer == null) return;
            if (ModsScrollViewer.ViewportHeight <= 0 || ModsScrollViewer.ActualWidth <= 0) return;

            // Viewport is now known - reload images that may have been skipped or failed
            _firstLayoutDone = true;
            ModsScrollViewer.LayoutUpdated -= OnScrollViewerFirstLayout;
            LoadVisibleImages();
            Logger.LogInfo("OnScrollViewerFirstLayout: Triggered LoadVisibleImages after first layout pass");
        }
        
        private void ModGridPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Debounce resize events - cancel previous pending reload
            _resizeDebounceToken?.Cancel();
            _resizeDebounceToken = new System.Threading.CancellationTokenSource();
            var token = _resizeDebounceToken.Token;
            
            // When window is resized, the viewport changes so we need to reload visible images
            DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await Task.Delay(100, token); // Small delay to let the layout update
                    if (token.IsCancellationRequested) return;
                    
                    LoadVisibleImages();
                    ModsScrollViewer?.UpdateLayout();
                }
                catch (TaskCanceledException)
                {
                    // Debounced - ignore
                }
            });
        }

        // Throttling for scroll events - prevent too many calls
        private DateTime _lastLoadCheck = DateTime.MinValue;
        private const int LOAD_THROTTLE_MS = 200; // Check every 200ms max
        
        // Limit concurrent image loads to prevent overwhelming the system
        private readonly SemaphoreSlim _imageLoadSemaphore = new SemaphoreSlim(5, 5); // Max 5 concurrent
        private readonly HashSet<string> _currentlyLoading = new HashSet<string>();
        private readonly object _loadingLock = new object();
        
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
            if (_lastLoadedModDataIndex >= _allModData.Count) return; // All data processed
            
            // Don't use incremental loading if we have active filter/search/category
            // In those cases, all filtered mods are already loaded
            if (!string.IsNullOrEmpty(_currentCategory) || _isSearchActive)
                return;
            
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
            const int batchSize = 20; // Fixed batch size for incremental loading
            
            // Load persistent lists once for fast filtering
            bool hideBroken = SettingsManager.Current.HideBrokenMods;
            bool hideNSFW = SettingsManager.Current.HideNSFWMods;
            HashSet<string> nsfwMods = hideNSFW ? ModListManager.LoadNSFWModsList() : new HashSet<string>();
            HashSet<string> brokenMods = hideBroken ? ModListManager.LoadBrokenModsList() : new HashSet<string>();
            
            // Cache favorites list once to avoid repeated calls
            var gameTag = SettingsManager.CurrentSelectedGame ?? "";
            var favoritesList = new HashSet<string>();
            foreach (var mod in _allModData)
            {
                if (SettingsManager.IsModFavorite(gameTag, mod.Name))
                {
                    favoritesList.Add(mod.Name);
                }
            }
            
            int added = 0;
            int startIndex = _lastLoadedModDataIndex;
            
            // Process items from _allModData starting from last processed index
            for (int i = startIndex; i < _allModData.Count && added < batchSize; i++)
            {
                var modData = _allModData[i];
                
                // Filter broken mods if setting is enabled (fast HashSet lookup)
                if (hideBroken && brokenMods.Contains(modData.Name))
                {
                    _lastLoadedModDataIndex = i + 1;
                    continue;
                }
                
                // Filter NSFW mods if setting is enabled (fast HashSet lookup)
                if (hideNSFW && nsfwMods.Contains(modData.Name))
                {
                    _lastLoadedModDataIndex = i + 1;
                    continue;
                }
                
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
                    HasUpdate = modData.HasUpdate, // Use cached value from ModData
                    IsVisible = true,
                    IsBroken = modData.IsBroken,
                    IsNSFW = modData.IsNSFW,
                    IsFavorite = favoritesList.Contains(modData.Name), // Use cached favorites list
                    ImageSource = null // Lazy load via LoadVisibleImages
                };
                _allMods.Add(modTile);
                added++;
                _lastLoadedModDataIndex = i + 1;
            }
            
            // Apply zoom scaling to newly added containers after they're realized
            if (added > 0 && Math.Abs(_zoomFactor - 1.0) > 0.001)
            {
                var currentCount = _allMods.Count - added;
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    ApplyZoomToNewContainers(currentCount, _allMods.Count);
                });
            }
        }
        
        private void ApplyZoomToNewContainers(int startIndex, int endIndex)
        {
            if (ModsGrid == null) return;
            
            for (int i = startIndex; i < endIndex; i++)
            {
                var container = ModsGrid.ContainerFromIndex(i) as GridViewItem;
                if (container?.ContentTemplateRoot is FrameworkElement root)
                {
                    ApplyScalingToContainer(container, root);
                }
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

            int firstVisibleIndex;
            int lastVisibleIndex;

            // If layout hasn't measured yet (viewport is 0), load all items.
            // This happens on startup before the first layout pass completes and would
            // otherwise cause the active mod thumbnail to be skipped.
            if (viewportWidth <= 0 || viewportHeight <= 0)
            {
                firstVisibleIndex = 0;
                lastVisibleIndex = itemsList.Count;
            }
            else
            {
                // Calculate items per row based on viewport width
                var effectiveTileWidth = (TILE_WIDTH + TILE_MARGIN) * _zoomFactor;
                var itemsPerRow = Math.Max(1, (int)(viewportWidth / effectiveTileWidth));

                // Calculate visible row range with buffer
                var effectiveTileHeight = (TILE_HEIGHT + TILE_MARGIN) * _zoomFactor;
                var firstVisibleRow = Math.Max(0, (int)(scrollOffset / effectiveTileHeight) - 2); // 2 row buffer above
                var lastVisibleRow = (int)((scrollOffset + viewportHeight) / effectiveTileHeight) + 2; // 2 row buffer below

                firstVisibleIndex = firstVisibleRow * itemsPerRow;
                lastVisibleIndex = Math.Min((lastVisibleRow + 1) * itemsPerRow, itemsList.Count);
            }

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
            var imagePath = mod.ImagePath;
            
            // Skip if ImagePath is empty or null
            if (string.IsNullOrEmpty(imagePath))
                return;
            
            // Skip if already loading this image (thread-safe check and add)
            lock (_loadingLock)
            {
                if (_currentlyLoading.Contains(imagePath))
                    return;
                _currentlyLoading.Add(imagePath);
            }
            
            try
            {
                // Check cache first
                var cachedImage = ImageCacheManager.GetCachedImage(imagePath);
                if (cachedImage != null)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        mod.ImageSource = cachedImage;
                        Logger.LogDebug($"LoadImageAsync: Loaded from cache {imagePath}");
                    });
                    return;
                }
                
                // Check if file exists on background thread
                var fileExists = await Task.Run(() => File.Exists(imagePath));
                
                // Load on UI thread (required by WinUI 3)
                if (fileExists)
                {
                    // Ensure we're on UI thread for BitmapImage creation
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            Logger.LogDebug($"LoadImageAsync: Loading {imagePath}");
                            var bitmap = new BitmapImage();

                            // Track load failure so we can retry
                            bitmap.ImageFailed += (s, e) =>
                            {
                                Logger.LogWarning($"LoadImageAsync: ImageFailed for {imagePath}, clearing ImageSource for retry");
                                mod.ImageSource = null;
                                lock (_loadingLock)
                                {
                                    _currentlyLoading.Remove(imagePath);
                                }
                            };

                            bitmap.ImageOpened += (s, e) =>
                            {
                                // Cache only after successful load
                                ImageCacheManager.CacheImage(imagePath, bitmap);
                                Logger.LogDebug($"LoadImageAsync: ImageOpened for {imagePath}");
                            };

                            // Use UriSource with absolute path for WebP support via Windows codecs
                            var absolutePath = Path.GetFullPath(imagePath);
                            bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);

                            // Assign immediately - WinUI will decode async, ImageOpened fires when done
                            mod.ImageSource = bitmap;
                            Logger.LogDebug($"LoadImageAsync: ImageSource assigned for {imagePath}");

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
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to load image {imagePath}", ex);
                        }
                    });
                }
            }
            finally
            {
                lock (_loadingLock)
                {
                    _currentlyLoading.Remove(imagePath);
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

        /// <summary>
        /// Refresh a single mod tile's image after preview download/optimization
        /// </summary>
        public void RefreshModTileImage(string modPath)
        {
            try
            {
                var modDirName = Path.GetFileName(modPath);
                
                DispatcherQueue.TryEnqueue(() =>
                {
                    // Find the tile in grid view
                    if (ModsGrid?.ItemsSource is System.Collections.ObjectModel.ObservableCollection<ModTile> gridCollection)
                    {
                        var tile = gridCollection.FirstOrDefault(t => 
                            t.Directory.Equals(modDirName, StringComparison.OrdinalIgnoreCase) ||
                            t.Directory.EndsWith("\\" + modDirName, StringComparison.OrdinalIgnoreCase) ||
                            t.Directory.EndsWith("/" + modDirName, StringComparison.OrdinalIgnoreCase));
                        
                        if (tile != null)
                        {
                            // Clear image to force reload
                            tile.ImageSource = null;
                            
                            // Update image path to new minitile if exists (check both formats)
                            var minitileWebpPath = Path.Combine(modPath, "minitile.webp");
                            var minitileJpgPath = Path.Combine(modPath, "minitile.jpg");
                            if (File.Exists(minitileWebpPath))
                            {
                                tile.ImagePath = minitileWebpPath;
                            }
                            else if (File.Exists(minitileJpgPath))
                            {
                                tile.ImagePath = minitileJpgPath;
                            }
                            
                            Logger.LogInfo($"Refreshing tile image for: {modDirName}");
                        }
                    }
                    
                    // Find the tile in table view
                    if (ModsTableList?.ItemsSource is System.Collections.ObjectModel.ObservableCollection<ModTile> tableCollection)
                    {
                        var tile = tableCollection.FirstOrDefault(t => 
                            t.Directory.Equals(modDirName, StringComparison.OrdinalIgnoreCase) ||
                            t.Directory.EndsWith("\\" + modDirName, StringComparison.OrdinalIgnoreCase) ||
                            t.Directory.EndsWith("/" + modDirName, StringComparison.OrdinalIgnoreCase));
                        
                        if (tile != null)
                        {
                            tile.ImageSource = null;
                            
                            var minitileWebpPath = Path.Combine(modPath, "minitile.webp");
                            var minitileJpgPath = Path.Combine(modPath, "minitile.jpg");
                            if (File.Exists(minitileWebpPath))
                            {
                                tile.ImagePath = minitileWebpPath;
                            }
                            else if (File.Exists(minitileJpgPath))
                            {
                                tile.ImagePath = minitileJpgPath;
                            }
                        }
                    }
                    
                    // Reload visible images
                    LoadVisibleImages();
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to refresh mod tile image for: {modPath}", ex);
            }
        }

        /// <summary>
        /// Lightweight refresh of a single mod's active state (for overlay toggle)
        /// </summary>
        public void RefreshSingleModState(string modPath)
        {
            try
            {
                var modDirName = Path.GetFileName(modPath);
                var cleanName = GetCleanModName(modDirName);
                var isActive = !modDirName.StartsWith("DISABLED", StringComparison.OrdinalIgnoreCase);
                
                DispatcherQueue.TryEnqueue(() =>
                {
                    // Update in grid view
                    if (ModsGrid?.ItemsSource is System.Collections.ObjectModel.ObservableCollection<ModTile> gridCollection)
                    {
                        var tile = gridCollection.FirstOrDefault(t => 
                            GetCleanModName(t.Directory).Equals(cleanName, StringComparison.OrdinalIgnoreCase));
                        
                        if (tile != null)
                        {
                            tile.IsActive = isActive;
                            tile.Directory = modDirName; // Update directory name (with or without DISABLED_)
                            Logger.LogInfo($"Updated grid tile state: {cleanName} -> IsActive={isActive}");
                        }
                    }
                    
                    // Update in table view
                    if (ModsTableList?.ItemsSource is System.Collections.ObjectModel.ObservableCollection<ModTile> tableCollection)
                    {
                        var tile = tableCollection.FirstOrDefault(t => 
                            GetCleanModName(t.Directory).Equals(cleanName, StringComparison.OrdinalIgnoreCase));
                        
                        if (tile != null)
                        {
                            tile.IsActive = isActive;
                            tile.Directory = modDirName;
                        }
                    }
                    
                    // Update in _allMods
                    var allModsTile = _allMods?.FirstOrDefault(t => 
                        GetCleanModName(t.Directory).Equals(cleanName, StringComparison.OrdinalIgnoreCase));
                    if (allModsTile != null)
                    {
                        allModsTile.IsActive = isActive;
                        allModsTile.Directory = modDirName;
                    }
                    
                    // Update in _allModData cache
                    var modData = _allModData?.FirstOrDefault(m => 
                        GetCleanModName(m.Directory).Equals(cleanName, StringComparison.OrdinalIgnoreCase));
                    if (modData != null)
                    {
                        modData.IsActive = isActive;
                        modData.Directory = cleanName; // Keep clean name in data
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to refresh single mod state: {modPath}", ex);
            }
        }
    }
}