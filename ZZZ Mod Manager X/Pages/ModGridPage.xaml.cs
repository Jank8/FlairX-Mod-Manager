using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using System.IO.Compression;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace ZZZ_Mod_Manager_X.Pages
{
    public sealed partial class ModGridPage : Page
    {
        public class ModTile : INotifyPropertyChanged
        {
            public string Name { get; set; } = "";
            public string ImagePath { get; set; } = "";
            public string Directory { get; set; } = ""; // Store only the directory name
            private BitmapImage? _imageSource;
            public BitmapImage? ImageSource
            {
                get => _imageSource;
                set { if (_imageSource != value) { _imageSource = value; OnPropertyChanged(nameof(ImageSource)); } }
            }
            private bool _isActive;
            public bool IsActive
            {
                get => _isActive;
                set { if (_isActive != value) { _isActive = value; OnPropertyChanged(nameof(IsActive)); } }
            }
            private bool _isHovered;
            public bool IsHovered
            {
                get => _isHovered;
                set { if (_isHovered != value) { _isHovered = value; OnPropertyChanged(nameof(IsHovered)); } }
            }
            private bool _isFolderHovered;
            public bool IsFolderHovered
            {
                get => _isFolderHovered;
                set { if (_isFolderHovered != value) { _isFolderHovered = value; OnPropertyChanged(nameof(IsFolderHovered)); } }
            }
            private bool _isDeleteHovered;
            public bool IsDeleteHovered
            {
                get => _isDeleteHovered;
                set { if (_isDeleteHovered != value) { _isDeleteHovered = value; OnPropertyChanged(nameof(IsDeleteHovered)); } }
            }
            private bool _isVisible = true;
            public bool IsVisible
            {
                get => _isVisible;
                set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); } }
            }
            // Removed IsInViewport - using new scroll-based lazy loading instead
            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private static string ActiveModsStatePath 
        {
            get
            {
                return PathManager.GetActiveModsPath();
            }
        }
        private static string SymlinkStatePath => Path.Combine(AppContext.BaseDirectory, "Settings", "SymlinkState.json");
        private Dictionary<string, bool> _activeMods = new();
        private string? _lastSymlinkTarget;
        private ObservableCollection<ModTile> _allMods = new();
        private string? _currentCategory; // Track current category for back navigation
        private string? _previousCategory; // Track category before search for restoration
        private bool _isSearchActive = false; // Track if we're currently in search mode
        
        // Virtualized loading - store all mod data but only create visible ModTiles
        private List<ModData> _allModData = new();
        
        // Thread-safe JSON Caching System
        private static readonly Dictionary<string, ModData> _modJsonCache = new();
        private static readonly Dictionary<string, DateTime> _modFileTimestamps = new();
        private static readonly object _cacheLock = new object();
        
        // Thread-safe Background Loading
        private static volatile bool _isBackgroundLoading = false;
        private static Task? _backgroundLoadTask = null;
        private static readonly object _backgroundLoadLock = new object();

        private double _zoomFactor = 1.0;
        private DateTime _lastScrollTime = DateTime.MinValue;
        public double ZoomFactor
        {
            get => _zoomFactor;
            set
            {
                // Only allow enlarging, minimum is 1.0 (100%)
                double clamped = Math.Max(1.0, Math.Min(2.5, value));
                if (_zoomFactor != clamped)
                {
                    _zoomFactor = clamped;
                    
                    // Update grid sizes asynchronously to avoid blocking mouse wheel
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                    {
                        UpdateGridItemSizes();
                    });
                    
                    // Save zoom level to settings
                    ZZZ_Mod_Manager_X.SettingsManager.Current.ZoomLevel = clamped;
                    ZZZ_Mod_Manager_X.SettingsManager.Save();
                    
                    // Update zoom indicator in main window
                    var mainWindow = (Application.Current as App)?.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.UpdateZoomIndicator(clamped);
                    }
                }
            }
        }

        public void ResetZoom()
        {
            ZoomFactor = 1.0;
        }

        private void ApplyScalingToContainer(GridViewItem container, FrameworkElement root)
        {
            if (Math.Abs(ZoomFactor - 1.0) < 0.001) // At 100% zoom
            {
                // Remove transform completely at 100% to match original state
                root.RenderTransform = null;
                
                // Clear container size to let it auto-size naturally
                container.ClearValue(FrameworkElement.WidthProperty);
                container.ClearValue(FrameworkElement.HeightProperty);
            }
            else
            {
                // Apply ScaleTransform for other zoom levels
                var scaleTransform = new ScaleTransform
                {
                    ScaleX = ZoomFactor,
                    ScaleY = ZoomFactor,
                    CenterX = _baseTileSize / 2,
                    CenterY = (_baseTileSize + _baseDescHeight) / 2
                };
                
                root.RenderTransform = scaleTransform;
                container.Width = _baseTileSize * ZoomFactor + (24 * ZoomFactor);
                container.Height = (_baseTileSize + _baseDescHeight) * ZoomFactor + (24 * ZoomFactor);
            }
        }

        private double _baseTileSize = 277;
        private double _baseDescHeight = 56;

        private void UpdateGridItemSizes()
        {
            // Use ScaleTransform approach instead of manual resizing
            if (ModsGrid != null)
            {
                // Update WrapGrid ItemWidth/ItemHeight for proportional layout
                if (ModsGrid.ItemsPanelRoot is WrapGrid wrapGrid)
                {
                    if (Math.Abs(ZoomFactor - 1.0) < 0.001) // At 100% zoom
                    {
                        // Reset to original auto-sizing at 100%
                        wrapGrid.ClearValue(WrapGrid.ItemWidthProperty);
                        wrapGrid.ClearValue(WrapGrid.ItemHeightProperty);
                    }
                    else
                    {
                        var scaledMargin = 24 * ZoomFactor;
                        wrapGrid.ItemWidth = _baseTileSize * ZoomFactor + scaledMargin;
                        wrapGrid.ItemHeight = (_baseTileSize + _baseDescHeight) * ZoomFactor + scaledMargin;
                    }
                }

                foreach (var item in ModsGrid.Items)
                {
                    var container = ModsGrid.ContainerFromItem(item) as GridViewItem;
                    if (container?.ContentTemplateRoot is FrameworkElement root)
                    {
                        ApplyScalingToContainer(container, root);
                    }
                }

                ModsGrid.InvalidateArrange();
                ModsGrid.UpdateLayout();
                
                // Force extents recalculation for zoom - fixes wheel event routing
                ModsGrid.InvalidateMeasure();
                if (ModsScrollViewer != null)
                {
                    ModsScrollViewer.InvalidateScrollInfo();
                    ModsScrollViewer.UpdateLayout();
                }
            }
        }

        // No longer needed - using ScaleTransform approach

        // No longer needed - using ScaleTransform approach

        // No longer needed - using ScaleTransform approach

        public ModGridPage()
        {
            this.InitializeComponent();
            LoadActiveMods();
            LoadSymlinkState();
            (App.Current as ZZZ_Mod_Manager_X.App)?.EnsureModJsonInModLibrary();
            this.Loaded += ModGridPage_Loaded;
            
            // Load saved zoom level from settings
            _zoomFactor = ZZZ_Mod_Manager_X.SettingsManager.Current.ZoomLevel;
            
            // Handle container generation to apply scaling to new items
            ModsGrid.ContainerContentChanging += ModsGrid_ContainerContentChanging;
            
            StartBackgroundLoadingIfNeeded();
        }

        private void ModsGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue) return;
            
            // Apply scaling to ALL newly generated containers, not just when zoom != 1.0
            if (args.ItemContainer is GridViewItem container)
            {
                container.Loaded += (s, e) => 
                {
                    if (container.ContentTemplateRoot is FrameworkElement root)
                    {
                        ApplyScalingToContainer(container, root);
                    }
                };
            }
        }

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
                
                var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                if (!Directory.Exists(modLibraryPath)) return;
                
                var directories = Directory.GetDirectories(modLibraryPath);
                var totalDirs = directories.Length;
                var processed = 0;
                
                foreach (var dir in directories)
                {
                    var modJsonPath = Path.Combine(dir, "mod.json");
                    if (!File.Exists(modJsonPath)) continue;
                    
                    var dirName = Path.GetFileName(dir);
                    
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
                            
                            var name = Path.GetFileName(dir);
                            string previewPath = GetOptimalImagePathStatic(dir);
                            
                            var modData = new ModData
                            { 
                                Name = name, 
                                ImagePath = previewPath, 
                                Directory = dirName, 
                                IsActive = false, // Will be updated when actually used
                                Character = modCharacter,
                                Author = modAuthor,
                                Url = modUrl
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

        // Removed - using page-level wheel handler instead

        private void ModsScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            LogToGridLog($"Wheel event reached ScrollViewer at zoom {_zoomFactor}");
            
            // Only handle zoom if enabled in settings
            if ((e.KeyModifiers & Windows.System.VirtualKeyModifiers.Control) == Windows.System.VirtualKeyModifiers.Control &&
                ZZZ_Mod_Manager_X.SettingsManager.Current.ModGridZoomEnabled)
            {
                var properties = e.GetCurrentPoint(ModsScrollViewer).Properties;
                var delta = properties.MouseWheelDelta;
                
                var oldZoom = _zoomFactor;
                if (delta > 0)
                {
                    ZoomFactor += 0.05; // 5% step
                }
                else if (delta < 0)
                {
                    ZoomFactor -= 0.05; // 5% step
                }
                
                if (oldZoom != _zoomFactor)
                {
                    e.Handled = true;
                    LogToGridLog("Zoom wheel event handled");
                }
            }
            else
            {
                LogToGridLog("Normal scroll wheel event - letting ScrollViewer handle");
            }
        }

        // Removed - didn't fix the scroll issue

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            // Handle Ctrl+0 for zoom reset if zoom is enabled
            if (e.Key == Windows.System.VirtualKey.Number0 && 
                (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down &&
                ZZZ_Mod_Manager_X.SettingsManager.Current.ModGridZoomEnabled)
            {
                ResetZoom();
                e.Handled = true;
                return;
            }
            
            base.OnKeyDown(e);
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

        private static void LogToGridLog(string message)
        {
            // Only log if grid logging is enabled in settings
            if (!SettingsManager.Current.GridLoggingEnabled) return;
            
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logPath = Path.Combine(AppContext.BaseDirectory, "Settings", "GridLog.log");
                var settingsDir = Path.GetDirectoryName(logPath);
                
                if (!string.IsNullOrEmpty(settingsDir) && !Directory.Exists(settingsDir))
                {
                    Directory.CreateDirectory(settingsDir);
                }
                
                var logEntry = $"[{timestamp}] {message}\n";
                File.AppendAllText(logPath, logEntry, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write to GridLog: {ex.Message}");
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string modName && !string.IsNullOrEmpty(modName))
            {
                // Open mod details for given name
                var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                var modDir = Path.Combine(modLibraryPath, modName);
                var modJsonPath = Path.Combine(modDir, "mod.json");
                if (File.Exists(modJsonPath))
                {
                    var json = File.ReadAllText(modJsonPath);
                    CategoryTitle.Text = $"Mod details: {modName}";
                    // You can add mod details display in grid here
                    // Example: display JSON in TextBlock
                    ModsGrid.ItemsSource = new[] { json };
                    return;
                }
            }
            if (e.Parameter is string character && !string.IsNullOrEmpty(character))
            {
                _currentCategory = character; // Store current category
                if (string.Equals(character, "other", StringComparison.OrdinalIgnoreCase))
                {
                    CategoryTitle.Text = LanguageManager.Instance.T("Category_Other_Mods");
                    LoadMods(character);
                }
                else if (string.Equals(character, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    CategoryTitle.Text = LanguageManager.Instance.T("Category_Active_Mods");
                    LoadActiveModsOnly();
                }
                else
                {
                    CategoryTitle.Text = character;
                    LoadMods(character);
                }
            }
            else
            {
                _currentCategory = null; // All mods view
                CategoryTitle.Text = LanguageManager.Instance.T("Category_All_Mods");
                LoadAllMods();
            }
            
            // Notify MainWindow to update heart button after category title is set
            NotifyMainWindowToUpdateHeartButton();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            
            // KEEP EVERYTHING IN MEMORY - don't clear grid or collections
            // This prevents memory spikes when navigating back to the page
            // The cached collection and images stay loaded for instant access
            LogToGridLog("NAVIGATION: Keeping ModGridPage data in memory for fast return");
        }

        private void LoadActiveMods()
        {
            if (File.Exists(ActiveModsStatePath))
            {
                try
                {
                    var json = File.ReadAllText(ActiveModsStatePath);
                    _activeMods = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to load active mods", ex);
                    _activeMods = new();
                }
            }
        }

        private void SaveActiveMods()
        {
            try
            {
                var json = JsonSerializer.Serialize(_activeMods, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ActiveModsStatePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save active mods", ex);
            }
        }

        private void LoadSymlinkState()
        {
            if (File.Exists(SymlinkStatePath))
            {
                try
                {
                    var json = File.ReadAllText(SymlinkStatePath);
                    var state = JsonSerializer.Deserialize<SymlinkState>(json);
                    _lastSymlinkTarget = state?.TargetPath ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to load symlink state", ex);
                    _lastSymlinkTarget = null;
                }
            }
        }

        private void SaveSymlinkState(string targetPath)
        {
            try
            {
                var state = new SymlinkState { TargetPath = targetPath };
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SymlinkStatePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save symlink state", ex);
            }
        }

        private class SymlinkState
        {
            public string? TargetPath { get; set; }
        }

        // Lightweight mod data for virtualized loading
        private class ModData
        {
            public string Name { get; set; } = "";
            public string ImagePath { get; set; } = "";
            public string Directory { get; set; } = "";
            public bool IsActive { get; set; }
            public string Character { get; set; } = "";
            public string Author { get; set; } = "";
            public string Url { get; set; } = "";
        }



        private void LoadMods(string character)
        {
            LogToGridLog($"LoadMods() called for character: {character}");
            
            var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(modLibraryPath)) return;
            
            var mods = new List<ModTile>();
            foreach (var dir in Directory.GetDirectories(modLibraryPath))
            {
                var modJsonPath = Path.Combine(dir, "mod.json");
                if (!File.Exists(modJsonPath)) continue;
                try
                {
                    var json = File.ReadAllText(modJsonPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var modCharacter = root.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "other" : "other";
                    if (!string.Equals(modCharacter, character, StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    var name = Path.GetFileName(dir);
                    string previewPath = GetOptimalImagePath(dir);
                    var dirName = Path.GetFileName(dir);
                    var isActive = _activeMods.TryGetValue(dirName, out var active) && active;
                    
                    var modTile = new ModTile 
                    { 
                        Name = name, 
                        ImagePath = previewPath, 
                        Directory = dirName, 
                        IsActive = isActive, 
                        IsVisible = true,
                        ImageSource = null // Start with no image - lazy load when visible
                    };
                    
                    mods.Add(modTile);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to parse mod.json for {dir}", ex);
                }
            }
            
            var sorted = mods
                .OrderByDescending(m => m.IsActive)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
                
            LogToGridLog($"Loaded {sorted.Count} mods for character: {character}");
            ModsGrid.ItemsSource = sorted;
            
            // Load visible images after setting new data source
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to let the grid update
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
        }

        private void LoadAllMods()
        {
            LogToGridLog("LoadAllMods() called - using virtualized loading");
            
            // First, load all mod data (lightweight)
            LoadAllModData();
            
            // Then create only the initial visible ModTiles
            LoadVirtualizedModTiles();
        }

        private void LoadAllModData()
        {
            var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(modLibraryPath)) return;
            
            _allModData.Clear();
            var cacheHits = 0;
            var cacheMisses = 0;
            
            foreach (var dir in Directory.GetDirectories(modLibraryPath))
            {
                var modJsonPath = Path.Combine(dir, "mod.json");
                if (!File.Exists(modJsonPath)) continue;
                
                var dirName = Path.GetFileName(dir);
                var modData = GetCachedModData(dir, modJsonPath);
                
                if (modData != null)
                {
                    // Update active state (this can change without file modification)
                    modData.IsActive = _activeMods.TryGetValue(dirName, out var active) && active;
                    
                    // Skip "other" mods in All Mods view - they have their own category
                    if (!string.Equals(modData.Character, "other", StringComparison.OrdinalIgnoreCase))
                    {
                        _allModData.Add(modData);
                    }
                    cacheHits++;
                }
                else
                {
                    cacheMisses++;
                }
            }
            
            // Sort the lightweight data
            _allModData = _allModData
                .OrderByDescending(m => m.IsActive)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
                
            LogToGridLog($"Loaded {_allModData.Count} mod data entries (Cache hits: {cacheHits}, Cache misses: {cacheMisses})");
        }

        private ModData? GetCachedModData(string dir, string modJsonPath)
        {
            var dirName = Path.GetFileName(dir);
            
            lock (_cacheLock)
            {
                // Check if file has been modified since last cache
                var lastWriteTime = File.GetLastWriteTime(modJsonPath);
                
                if (_modJsonCache.TryGetValue(dirName, out var cachedData) &&
                    _modFileTimestamps.TryGetValue(dirName, out var cachedTime) &&
                    cachedTime >= lastWriteTime)
                {
                    // Cache hit - return cached data
                    return cachedData;
                }
                
                // Cache miss - load and cache the data
                try
                {
                    var json = File.ReadAllText(modJsonPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var modCharacter = root.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "other" : "other";
                    var modAuthor = root.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? "" : "";
                    var modUrl = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                    
                    var name = Path.GetFileName(dir);
                    string previewPath = GetOptimalImagePath(dir);
                    var isActive = _activeMods.TryGetValue(dirName, out var active) && active;
                    
                    var modData = new ModData
                    { 
                        Name = name, 
                        ImagePath = previewPath, 
                        Directory = dirName, 
                        IsActive = isActive,
                        Character = modCharacter,
                        Author = modAuthor,
                        Url = modUrl
                    };
                    
                    // Cache the data
                    _modJsonCache[dirName] = modData;
                    _modFileTimestamps[dirName] = lastWriteTime;
                    
                    return modData;
                }
                catch
                {
                    return null;
                }
            }
        }

        private void LoadVirtualizedModTiles()
        {
            // Calculate how many items we need to show initially
            var initialLoadCount = CalculateInitialLoadCount();
            
            var initialMods = new List<ModTile>();
            for (int i = 0; i < Math.Min(initialLoadCount, _allModData.Count); i++)
            {
                var modData = _allModData[i];
                var modTile = new ModTile 
                { 
                    Name = modData.Name, 
                    ImagePath = modData.ImagePath, 
                    Directory = modData.Directory, 
                    IsActive = modData.IsActive, 
                    IsVisible = true,
                    ImageSource = null // Start with no image - lazy load when visible
                };
                initialMods.Add(modTile);
            }
            
            _allMods = new ObservableCollection<ModTile>(initialMods);
            ModsGrid.ItemsSource = _allMods;
            LogToGridLog($"Created {initialMods.Count} initial ModTiles out of {_allModData.Count} total");
            
            // Load visible images after setting new data source
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to let the grid update
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
        }

        private int CalculateInitialLoadCount()
        {
            // Estimate based on typical grid layout
            // Assume ~4-6 items per row, and load 3-4 rows initially
            var estimatedItemsPerRow = 5;
            var initialRows = 4;
            var bufferRows = 2; // Extra buffer for smooth scrolling
            
            return estimatedItemsPerRow * (initialRows + bufferRows);
        }
        
        private string GetOptimalImagePath(string modDirectory)
        {
            // Prefer WebP minitile for grid display (much smaller file size)
            string webpPath = Path.Combine(modDirectory, "minitile.webp");
            if (File.Exists(webpPath))
            {
                LogToGridLog($"Using WebP minitile for {Path.GetFileName(modDirectory)}");
                return webpPath;
            }
            
            // Check for JPEG minitile (fallback when WebP encoder not available)
            string minitileJpegPath = Path.Combine(modDirectory, "minitile.jpg");
            if (File.Exists(minitileJpegPath))
            {
                LogToGridLog($"Using JPEG minitile for {Path.GetFileName(modDirectory)}");
                return minitileJpegPath;
            }
            
            // Fallback to original JPEG
            string jpegPath = Path.Combine(modDirectory, "preview.jpg");
            if (File.Exists(jpegPath))
            {
                LogToGridLog($"Using original JPEG for {Path.GetFileName(modDirectory)}");
                return jpegPath;
            }
            
            // No image found
            return jpegPath; // Return path anyway for consistency
        }

        private BitmapImage CreateBitmapImage(string imagePath)
        {
            var bitmap = new BitmapImage();
            try
            {
                if (File.Exists(imagePath))
                {
                    // Read file into memory steam to avoid file locking issues
                    byte[] imageData = File.ReadAllBytes(imagePath);
                    using (var memStream = new MemoryStream(imageData))
                    {
                        bitmap.SetSource(memStream.AsRandomAccessStream());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load image {imagePath}: {ex.Message}");
            }
            return bitmap;
        }
        


        public void FilterMods(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                // Clear search - return to appropriate view based on previous state
                _isSearchActive = false;
                
                if (_previousCategory != null)
                {
                    // Return to previous category if it exists
                    _currentCategory = _previousCategory;
                    if (string.Equals(_previousCategory, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        CategoryTitle.Text = LanguageManager.Instance.T("Category_Active_Mods");
                        LoadActiveModsOnly();
                    }
                    else if (string.Equals(_previousCategory, "other", StringComparison.OrdinalIgnoreCase))
                    {
                        CategoryTitle.Text = LanguageManager.Instance.T("Category_Other_Mods");
                        LoadMods(_previousCategory);
                    }
                    else
                    {
                        CategoryTitle.Text = _previousCategory;
                        LoadMods(_previousCategory);
                    }
                }
                else
                {
                    // Default to All Mods if available
                    _currentCategory = null;
                    CategoryTitle.Text = LanguageManager.Instance.T("Category_All_Mods");
                    LoadAllMods();
                }
                _previousCategory = null; // Clear previous category after restoration
                // Scroll view to top after clearing search
                ModsScrollViewer?.ChangeView(0, 0, 1);
            }
            else
            {
                // Start search - save current category if not already searching
                if (!_isSearchActive)
                {
                    _previousCategory = _currentCategory;
                    _isSearchActive = true;
                }
                
                // Set search title
                CategoryTitle.Text = LanguageManager.Instance.T("Search_Results");
                
                // Load all mod data for searching if not already loaded
                if (_allModData.Count == 0)
                {
                    LoadAllModData();
                }
                
                // Search through the lightweight ModData and create ModTiles for matches
                var filteredData = _allModData.Where(modData => 
                    modData.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    modData.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    modData.Url.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
                var filteredMods = new List<ModTile>();
                
                foreach (var modData in filteredData)
                {
                    var modTile = new ModTile 
                    { 
                        Name = modData.Name, 
                        ImagePath = modData.ImagePath, 
                        Directory = modData.Directory, 
                        IsActive = modData.IsActive, 
                        IsVisible = true,
                        ImageSource = null // Start with no image - lazy load when visible
                    };
                    filteredMods.Add(modTile);
                }
                
                var filtered = new ObservableCollection<ModTile>(filteredMods);
                ModsGrid.ItemsSource = filtered;

                // Load visible images after filtering
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // Small delay to let the grid update
                    DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
                });

                // Scroll horizontally to first visible mod (with animation)
                if (filtered.Count > 0 && ModsGrid.ContainerFromIndex(0) is GridViewItem firstItem)
                {
                    firstItem.UpdateLayout();
                    var transform = firstItem.TransformToVisual(ModsScrollViewer);
                    var point = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                    // Scroll so that the first item is visible at the left
                    ModsScrollViewer.ChangeView(point.X, 0, 1, false);
                }
                else
                {
                    // Fallback: scroll to start
                    ModsScrollViewer?.ChangeView(0, 0, 1, false);
                }
            }
        }

        private void ModActiveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                // Validate mod directory name for security
                if (!SecurityValidator.IsValidModDirectoryName(mod.Directory))
                    return;

                // Always use current path from settings
                var modsDir = ZZZ_Mod_Manager_X.SettingsManager.Current.XXMIModsDirectory;
                if (string.IsNullOrWhiteSpace(modsDir))
                    modsDir = Path.Combine(AppContext.BaseDirectory, "XXMI", "ZZMI", "Mods");
                var modsDirFull = Path.GetFullPath(modsDir);
                if (_lastSymlinkTarget != null && !_lastSymlinkTarget.Equals(modsDirFull, StringComparison.OrdinalIgnoreCase))
                {
                    RemoveAllSymlinks(_lastSymlinkTarget);
                }
                var linkPath = Path.Combine(modsDirFull, mod.Directory);
                var absModDir = Path.Combine(ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary"), mod.Directory);
                // Remove double slashes in paths
                linkPath = CleanPath(linkPath);
                absModDir = CleanPath(absModDir);
                if (!_activeMods.TryGetValue(mod.Directory, out var isActive) || !isActive)
                {
                    if (!Directory.Exists(modsDirFull))
                        Directory.CreateDirectory(modsDirFull);
                    if (!Directory.Exists(linkPath) && !File.Exists(linkPath))
                    {
                        CreateSymlink(linkPath, absModDir);
                    }
                    _activeMods[mod.Directory] = true;
                    mod.IsActive = true;
                }
                else
                {
                    if ((Directory.Exists(linkPath) || File.Exists(linkPath)) && IsSymlink(linkPath))
                        Directory.Delete(linkPath, true);
                    _activeMods[mod.Directory] = false;
                    mod.IsActive = false;
                }
                SaveActiveMods();
                SaveSymlinkState(modsDirFull);
                // Reset hover only on clicked tile
                mod.IsHovered = false;
            }
        }

        private void ModActiveButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                mod.IsHovered = true;
            }
        }

        private void ModActiveButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                mod.IsHovered = false;
            }
        }

        private void OpenModFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                // Validate mod directory name for security
                if (!SecurityValidator.IsValidModDirectoryName(mod.Directory))
                    return;

                // Always use the current ModLibraryDirectory setting
                var modLibraryDir = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory;
                if (string.IsNullOrWhiteSpace(modLibraryDir))
                    modLibraryDir = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                var folder = Path.GetFullPath(Path.Combine(modLibraryDir, mod.Directory));
                if (Directory.Exists(folder))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{folder}\"",
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
            }
        }

        private void OpenModFolderButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                mod.IsFolderHovered = true;
            }
        }

        private void OpenModFolderButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                mod.IsFolderHovered = false;
            }
        }

        private void DeleteModButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                _ = DeleteModWithConfirmation(mod);
            }
        }

        private void DeleteModButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                mod.IsDeleteHovered = true;
            }
        }

        private void DeleteModButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                mod.IsDeleteHovered = false;
            }
        }

        private async Task DeleteModWithConfirmation(ModTile mod)
        {
            try
            {
                // Show confirmation dialog
                var dialog = new ContentDialog
                {
                    Title = LanguageManager.Instance.T("Delete_Mod_Confirm_Title"),
                    Content = string.Format(LanguageManager.Instance.T("Delete_Mod_Confirm_Message"), mod.Name),
                    PrimaryButtonText = LanguageManager.Instance.T("Delete"),
                    CloseButtonText = LanguageManager.Instance.T("Cancel"),
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                    return; // User cancelled

                // Save current scroll position
                var currentScrollPosition = ModsScrollViewer?.VerticalOffset ?? 0;

                // Validate mod directory name for security
                if (!SecurityValidator.IsValidModDirectoryName(mod.Directory))
                    return;

                // Get mod folder path
                var modLibraryDir = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory;
                if (string.IsNullOrWhiteSpace(modLibraryDir))
                    modLibraryDir = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                
                var modFolderPath = Path.Combine(modLibraryDir, mod.Directory);
                
                if (!Directory.Exists(modFolderPath))
                    return; // Folder doesn't exist

                // Move folder to recycle bin using Windows Shell API
                MoveToRecycleBin(modFolderPath);

                // Remove from active mods if it was active
                if (mod.IsActive && _activeMods.ContainsKey(mod.Directory))
                {
                    _activeMods.Remove(mod.Directory);
                    SaveActiveMods();
                }

                // Remove from cache
                lock (_cacheLock)
                {
                    _modJsonCache.Remove(mod.Directory);
                    _modFileTimestamps.Remove(mod.Directory);
                }

                // Refresh the grid while preserving scroll position
                await RefreshGridWithScrollPosition(currentScrollPosition);

                LogToGridLog($"DELETED: Mod '{mod.Name}' moved to recycle bin");
            }
            catch (Exception ex)
            {
                LogToGridLog($"DELETE ERROR: Failed to delete mod '{mod.Name}': {ex.Message}");
                
                // Show error dialog
                var errorDialog = new ContentDialog
                {
                    Title = LanguageManager.Instance.T("Error_Title"),
                    Content = ex.Message,
                    CloseButtonText = LanguageManager.Instance.T("OK"),
                    XamlRoot = this.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        private async Task RefreshGridWithScrollPosition(double scrollPosition)
        {
            // Reload the current view
            if (_currentCategory == null)
            {
                LoadAllMods();
            }
            else if (string.Equals(_currentCategory, "Active", StringComparison.OrdinalIgnoreCase))
            {
                LoadActiveModsOnly();
            }
            else
            {
                LoadMods(_currentCategory);
            }

            // Wait for UI to update, then restore scroll position
            await Task.Delay(100);
            if (ModsScrollViewer != null)
            {
                ModsScrollViewer.ScrollToVerticalOffset(scrollPosition);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        private const uint FO_DELETE = 0x0003;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOCONFIRMATION = 0x0010;

        private void MoveToRecycleBin(string path)
        {
            var shf = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = path + '\0', // Must be null-terminated
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION
            };
            
            int result = SHFileOperation(ref shf);
            if (result != 0)
            {
                throw new Exception($"Failed to move folder to recycle bin. Error code: {result}");
            }
        }
        private void CreateSymlink(string linkPath, string targetPath)
        {
            try
            {
                // Normalize paths to handle spaces and special characters properly
                linkPath = Path.GetFullPath(linkPath);
                targetPath = Path.GetFullPath(targetPath);
                
                // Ensure target directory exists
                if (!Directory.Exists(targetPath))
                {
                    Logger.LogError($"Target directory does not exist: {targetPath}");
                    return;
                }

                // Ensure parent directory of link exists
                var linkParent = Path.GetDirectoryName(linkPath);
                if (!string.IsNullOrEmpty(linkParent) && !Directory.Exists(linkParent))
                {
                    Directory.CreateDirectory(linkParent);
                }

                // Create the symbolic link
                bool success = CreateSymbolicLink(linkPath, targetPath, 1); // 1 = directory
                if (!success)
                {
                    var error = Marshal.GetLastWin32Error();
                    Logger.LogError($"Failed to create symlink from {linkPath} to {targetPath}. Win32 Error: {error}");
                }
                else
                {
                    Logger.LogInfo($"Created symlink: {linkPath} -> {targetPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception creating symlink from {linkPath} to {targetPath}", ex);
            }
        }
        private bool IsSymlink(string path)
        {
            var dirInfo = new DirectoryInfo(path);
            return dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        private void RemoveAllSymlinks(string modsDir)
        {
            if (!Directory.Exists(modsDir)) return;
            foreach (var dir in Directory.GetDirectories(modsDir))
            {
                if (IsSymlink(dir))
                    Directory.Delete(dir);
            }
        }

        private void ModName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                // Validate mod directory name for security
                if (!SecurityValidator.IsValidModDirectoryName(mod.Directory))
                    return;

                // Navigate to mod details page, pass both directory and current category
                var frame = this.Frame;
                var navParam = new ZZZ_Mod_Manager_X.Pages.ModDetailPage.ModDetailNav
                {
                    ModDirectory = mod.Directory ?? string.Empty,
                    Category = _currentCategory ?? string.Empty
                };
                frame?.Navigate(typeof(ZZZ_Mod_Manager_X.Pages.ModDetailPage), navParam);
            }
        }

        public static void RecreateSymlinksFromActiveMods()
        {
            var modsDir = ZZZ_Mod_Manager_X.SettingsManager.Current.XXMIModsDirectory;
            var defaultModsDir = Path.Combine(AppContext.BaseDirectory, "XXMI", "ZZMI", "Mods");
            if (string.IsNullOrWhiteSpace(modsDir))
                modsDir = defaultModsDir;
            var modsDirFull = Path.GetFullPath(modsDir);
            var defaultModsDirFull = Path.GetFullPath(defaultModsDir);
            var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");

            // Remove symlinks from old location (SymlinkState)
            var symlinkStatePath = Path.Combine(AppContext.BaseDirectory, "Settings", "SymlinkState.json");
            string? lastSymlinkTarget = null;
            if (File.Exists(symlinkStatePath))
            {
                try
                {
                    var json = File.ReadAllText(symlinkStatePath);
                    var state = JsonSerializer.Deserialize<SymlinkState>(json);
                    lastSymlinkTarget = state?.TargetPath;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to read symlink state during recreation", ex);
                }
            }
            if (!string.IsNullOrWhiteSpace(lastSymlinkTarget) && !lastSymlinkTarget.Equals(modsDirFull, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(lastSymlinkTarget))
                {
                    foreach (var dir in Directory.GetDirectories(lastSymlinkTarget))
                    {
                        if ((Directory.Exists(dir) || File.Exists(dir)) && IsSymlinkStatic(dir))
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                }
            }
            // Remove symlinks from default location if NOT currently selected
            if (!defaultModsDirFull.Equals(modsDirFull, StringComparison.OrdinalIgnoreCase) && Directory.Exists(defaultModsDirFull))
            {
                foreach (var dir in Directory.GetDirectories(defaultModsDirFull))
                {
                    if ((Directory.Exists(dir) || File.Exists(dir)) && IsSymlinkStatic(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
            }
            // Remove symlinks from new location
            if (Directory.Exists(modsDirFull))
            {
                foreach (var dir in Directory.GetDirectories(modsDirFull))
                {
                    if ((Directory.Exists(dir) || File.Exists(dir)) && IsSymlinkStatic(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
            }

            // Use game-specific ActiveMods path
            var activeModsPath = PathManager.GetActiveModsPath();
            if (!File.Exists(activeModsPath)) return;
            try
            {
                var json = File.ReadAllText(activeModsPath);
                var relMods = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
                foreach (var kv in relMods)
                {
                    if (kv.Value)
                    {
                        var absModDir = Path.Combine(modLibraryPath, kv.Key);
                        var linkPath = Path.Combine(modsDirFull, kv.Key);
                        if (!Directory.Exists(linkPath) && !File.Exists(linkPath))
                        {
                            CreateSymlinkStatic(linkPath, absModDir);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to recreate symlinks from active mods", ex);
            }
        }

        public static void ApplyPreset(string presetName)
        {
            // Use game-specific presets path
            string gameSpecificPresetsPath = AppConstants.GameConfig.GetPresetsPath(SettingsManager.CurrentSelectedGame);
            string presetPath;
            
            if (string.IsNullOrEmpty(gameSpecificPresetsPath))
            {
                // Fallback to root presets directory when no game selected
                presetPath = Path.Combine(AppContext.BaseDirectory, "Settings", "Presets", presetName + ".json");
            }
            else
            {
                presetPath = Path.Combine(AppContext.BaseDirectory, gameSpecificPresetsPath, presetName + ".json");
            }
            
            if (!File.Exists(presetPath)) return;
            try
            {
                RecreateSymlinksFromActiveMods();
                var json = File.ReadAllText(presetPath);
                var preset = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                if (preset != null)
                {
                    // Use game-specific ActiveMods path
                    var activeModsPath = PathManager.GetActiveModsPath();
                    var presetJson = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(activeModsPath, presetJson);
                    RecreateSymlinksFromActiveMods();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to apply preset", ex);
            }
        }

        public void SaveDefaultPresetAllInactive()
        {
            var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            var allMods = new Dictionary<string, bool>();
            if (Directory.Exists(modLibraryPath))
            {
                var dirs = Directory.GetDirectories(modLibraryPath);
                foreach (var dir in dirs)
                {
                    var modJsonPath = Path.Combine(dir, "mod.json");
                    if (File.Exists(modJsonPath))
                    {
                        try
                        {
                            var json = File.ReadAllText(modJsonPath);
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            var modCharacter = root.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "other" : "other";
                            if (string.Equals(modCharacter, "other", StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to parse mod.json for preset: {dir}", ex);
                            continue;
                        }
                        string modName = Path.GetFileName(dir);
                        allMods[modName] = false;
                    }
                }
            }
            // Use game-specific preset directory
            string gameSpecificPresetsPath = AppConstants.GameConfig.GetPresetsPath(ZZZ_Mod_Manager_X.SettingsManager.CurrentSelectedGame);
            string presetPath;
            
            if (string.IsNullOrEmpty(gameSpecificPresetsPath))
            {
                // Fallback to root presets directory when no game selected
                presetPath = Path.Combine(AppContext.BaseDirectory, "Settings", "Presets", "Default Preset.json");
            }
            else
            {
                presetPath = Path.Combine(AppContext.BaseDirectory, gameSpecificPresetsPath, "Default Preset.json");
            }
            
            var presetDir = Path.GetDirectoryName(presetPath) ?? string.Empty;
            Directory.CreateDirectory(presetDir);
            try
            {
                var json = JsonSerializer.Serialize(allMods, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(presetPath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save default preset", ex);
            }
        }

        // Cache management methods
        public static void ClearJsonCache()
        {
            lock (_cacheLock)
            {
                _modJsonCache.Clear();
                _modFileTimestamps.Clear();
                LogToGridLog("CACHE: JSON cache cleared");
            }
        }

        public static void InvalidateModCache(string modDirectory)
        {
            lock (_cacheLock)
            {
                var dirName = Path.GetFileName(modDirectory);
                if (_modJsonCache.Remove(dirName))
                {
                    _modFileTimestamps.Remove(dirName);
                    LogToGridLog($"CACHE: Invalidated cache for {dirName}");
                }
            }
        }

        // Incremental update - only reload specific mods that have changed
        public void RefreshChangedMods()
        {
            LogToGridLog("INCREMENTAL: Starting incremental mod refresh");
            
            var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(modLibraryPath)) return;
            
            var changedMods = new List<string>();
            var newMods = new List<string>();
            var removedMods = new List<string>();
            
            // Check for changed and new mods
            foreach (var dir in Directory.GetDirectories(modLibraryPath))
            {
                var modJsonPath = Path.Combine(dir, "mod.json");
                if (!File.Exists(modJsonPath)) continue;
                
                var dirName = Path.GetFileName(dir);
                var lastWriteTime = File.GetLastWriteTime(modJsonPath);
                
                lock (_cacheLock)
                {
                    if (_modFileTimestamps.TryGetValue(dirName, out var cachedTime))
                    {
                        if (lastWriteTime > cachedTime)
                        {
                            changedMods.Add(dirName);
                        }
                    }
                    else
                    {
                        newMods.Add(dirName);
                    }
                }
            }
            
            // Check for removed mods
            lock (_cacheLock)
            {
                var existingDirs = Directory.GetDirectories(modLibraryPath).Select(Path.GetFileName).ToHashSet();
                removedMods = _modJsonCache.Keys.Where(cached => !existingDirs.Contains(cached)).ToList();
            }
            
            // Process changes
            if (changedMods.Count > 0 || newMods.Count > 0 || removedMods.Count > 0)
            {
                LogToGridLog($"INCREMENTAL: Found {changedMods.Count} changed, {newMods.Count} new, {removedMods.Count} removed mods");
                
                // Remove deleted mods from cache
                foreach (var removed in removedMods)
                {
                    InvalidateModCache(removed);
                }
                
                // Invalidate changed mods (they'll be reloaded on next access)
                foreach (var changed in changedMods)
                {
                    InvalidateModCache(changed);
                }
                
                // New mods will be loaded automatically when accessed
                 
                // Refresh the current view if we're showing all mods
                if (_currentCategory == null && _allModData.Count > 0)
                {
                    LoadAllMods();
                }
            }
            else
            {
                LogToGridLog("INCREMENTAL: No changes detected");
            }
        }

        private void LoadActiveModsOnly()
        {
            LogToGridLog("LoadActiveModsOnly() called");
            
            // Load all mods first if not already loaded
            if (_allMods.Count == 0)
            {
                LoadAllMods();
            }
            
            // Filter to show only active mods
            var activeMods = _allMods.Where(mod => 
            {
                // Update active state first
                mod.IsActive = _activeMods.TryGetValue(mod.Directory, out var active) && active;
                return mod.IsActive;
            }).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
            
            LogToGridLog($"Found {activeMods.Count} active mods");
            ModsGrid.ItemsSource = activeMods;
            
            // Load visible images after setting new data source
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to let the grid update
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
        }

        public string GetCategoryTitleText()
        {
            return CategoryTitle?.Text ?? string.Empty;
        }

        // Add function to clean double slashes
        private static string CleanPath(string path)
        {
            while (path.Contains("\\\\")) path = path.Replace("\\\\", "\\");
            while (path.Contains("//")) path = path.Replace("//", "/");
            return path;
        }



        // Static helper for symlink creation
        private static void CreateSymlinkStatic(string linkPath, string targetPath)
        {
            // targetPath powinien by� zawsze pe�n� �cie�k� do katalogu moda w bibliotece mod�w
            // Je�li targetPath jest nazw� katalogu, zbuduj pe�n� �cie�k� 
            var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(targetPath))
            {
                targetPath = Path.Combine(modLibraryPath, Path.GetFileName(targetPath));
            }
            targetPath = Path.GetFullPath(targetPath);
            CreateSymbolicLink(linkPath, targetPath, 1); // 1 = directory
        }

        // Static helper for symlink check
        public static bool IsSymlinkStatic(string path)
        {
            try
            {
                if (!Directory.Exists(path) && !File.Exists(path))
                    return false;
                    
                var dirInfo = new DirectoryInfo(path);
                return dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to check if path is symlink: {path}", ex);
                return false;
            }
        }



        /// <summary>
        /// Validates and ensures symlinks are properly synchronized with active mods
        /// </summary>
        public static void ValidateAndFixSymlinks()
        {
            try
            {
                Logger.LogInfo("Starting symlink validation and repair");
                
                var modsDir = SettingsManager.Current.XXMIModsDirectory;
                if (string.IsNullOrWhiteSpace(modsDir))
                    modsDir = Path.Combine(AppContext.BaseDirectory, "XXMI", "ZZMI", "Mods");
                
                var modsDirFull = Path.GetFullPath(modsDir);
                var modLibraryPath = SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                
                // Load active mods using game-specific path
                var activeModsPath = PathManager.GetActiveModsPath();
                var activeMods = new Dictionary<string, bool>();
                
                if (File.Exists(activeModsPath))
                {
                    try
                    {
                        var json = File.ReadAllText(activeModsPath);
                        activeMods = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to load active mods for validation", ex);
                    }
                }
                
                // Check for orphaned symlinks (symlinks that shouldn't exist)
                if (Directory.Exists(modsDirFull))
                {
                    var existingDirs = Directory.GetDirectories(modsDirFull);
                    foreach (var dir in existingDirs)
                    {
                        if (IsSymlinkStatic(dir))
                        {
                            var dirName = Path.GetFileName(dir);
                            if (!activeMods.ContainsKey(dirName) || !activeMods[dirName])
                            {
                                // This symlink shouldn't exist - remove it
                                try
                                {
                                    Directory.Delete(dir, true);
                                    Logger.LogInfo($"Removed orphaned symlink: {dir}");
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError($"Failed to remove orphaned symlink: {dir}", ex);
                                }
                            }
                        }
                    }
                }
                
                // Check for missing symlinks (active mods without symlinks)
                foreach (var mod in activeMods.Where(m => m.Value))
                {
                    var linkPath = Path.Combine(modsDirFull, mod.Key);
                    var sourcePath = Path.Combine(modLibraryPath, mod.Key);
                    
                    if (!Directory.Exists(linkPath) && Directory.Exists(sourcePath))
                    {
                        // Missing symlink for active mod - create it
                        try
                        {
                            CreateSymlinkStatic(linkPath, sourcePath);
                            Logger.LogInfo($"Created missing symlink: {linkPath} -> {sourcePath}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to create missing symlink: {linkPath}", ex);
                        }
                    }
                    else if (Directory.Exists(linkPath) && !IsSymlinkStatic(linkPath))
                    {
                        // Directory exists but is not a symlink - this is problematic
                        Logger.LogWarning($"Directory exists but is not a symlink: {linkPath}");
                    }
                }
                
                Logger.LogInfo("Symlink validation and repair completed");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to validate and fix symlinks", ex);
            }
        }

        // Instance method for UI refresh (already present, but ensure public)
        public void RefreshUIAfterLanguageChange()
        {
            // Od�wie�enie listy kategorii mod�w w menu nawigacji
            var mainWindow = ((App)Application.Current).MainWindow as ZZZ_Mod_Manager_X.MainWindow;
            if (mainWindow != null)
            {
                _ = mainWindow.GenerateModCharacterMenuAsync();
            }
            // Check mod directories and create mod.json in level 1 directories
            (App.Current as ZZZ_Mod_Manager_X.App)?.EnsureModJsonInModLibrary();
            LoadAllMods();
        }

        // Add function to display path with single slashes
        public static string GetDisplayPath(string path)
        {
            return CleanPath(path);
        }

        // Notify MainWindow to update heart button
        private void NotifyMainWindowToUpdateHeartButton()
        {
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                // Use dispatcher to ensure UI update happens after page is fully loaded
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => 
                {
                    mainWindow.UpdateShowActiveModsButtonIcon();
                });
            }
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool>? _canExecute;
        public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T)parameter!) ?? true;
        public void Execute(object? parameter) => _execute((T)parameter!);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}