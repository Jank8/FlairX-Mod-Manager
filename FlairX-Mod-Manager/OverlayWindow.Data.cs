using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using WinRT;
using WinRT.Interop;

namespace FlairX_Mod_Manager
{
    public sealed partial class OverlayWindow
    {
        public ObservableCollection<OverlayCategoryItem> OverlayCategories { get; } = new();
        public ObservableCollection<OverlayModItem> OverlayMods { get; } = new();
        
        private string? _selectedCategoryPath;
        private int _selectedModIndex = -1;
        private OverlayModItem? _selectedMod;
        
        // Flags to prevent concurrent loading
        private bool _isLoadingMods;
        private bool _isLoadingCategories;
        private bool _isToggling;
        
        // Filter state
        private bool _showActiveOnly;
        
        // Navigation direction tracking for slide animations
        private OverlayModItem? _previousSelectedMod;
        private int _previousSelectedIndex = -1;



        /// <summary>
        /// Load categories and mods
        /// </summary>
        public async void LoadCurrentCategoryMods()
        {
            try
            {
                Logger.LogInfo("LoadCurrentCategoryMods: Starting");
                await LoadCategoriesAsync();
                Logger.LogInfo($"LoadCurrentCategoryMods: Loaded {OverlayCategories.Count} categories");
                
                // Load mods from first category if none selected
                if (_selectedCategoryPath == null && OverlayCategories.Count > 0)
                {
                    Logger.LogInfo("LoadCurrentCategoryMods: Selecting first category");
                    SelectCategory(OverlayCategories[0]);
                }
                else if (_selectedCategoryPath != null)
                {
                    // Find and select the category matching the path
                    var existingCategory = OverlayCategories.FirstOrDefault(c => c.Directory == _selectedCategoryPath);
                    if (existingCategory != null)
                    {
                        Logger.LogInfo($"LoadCurrentCategoryMods: Selecting existing category {existingCategory.Name}");
                        SelectCategory(existingCategory);
                    }
                    else if (OverlayCategories.Count > 0)
                    {
                        Logger.LogInfo("LoadCurrentCategoryMods: Category not found, selecting first");
                        SelectCategory(OverlayCategories[0]);
                    }
                }
                Logger.LogInfo("LoadCurrentCategoryMods: Completed");
            }
            catch (Exception ex)
            {
                Logger.LogError("LoadCurrentCategoryMods: Failed", ex);
            }
        }

        private async System.Threading.Tasks.Task LoadCategoriesAsync()
        {
            // Prevent concurrent loading
            if (_isLoadingCategories) return;
            _isLoadingCategories = true;
            
            try
            {
                // Clear old thumbnails to prevent memory leak
                foreach (var cat in OverlayCategories)
                {
                    cat.Thumbnail = null;
                }
                OverlayCategories.Clear();

                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !System.IO.Directory.Exists(modsPath))
                    return;

                var categories = System.IO.Directory.GetDirectories(modsPath);
                var gameTag = SettingsManager.CurrentSelectedGame;
                var hideEmpty = SettingsManager.Current.HideEmptyCategories;
                var categoryItems = new List<OverlayCategoryItem>();
                
                foreach (var categoryDir in categories)
                {
                    var categoryName = Path.GetFileName(categoryDir);

                    // Skip empty categories if setting enabled
                    if (hideEmpty)
                    {
                        var hasMods = System.IO.Directory.GetDirectories(categoryDir)
                            .Any(d => System.IO.File.Exists(Path.Combine(d, "mod.json")));
                        if (!hasMods) continue;
                    }
                    var isFavorite = !string.IsNullOrEmpty(gameTag) && SettingsManager.IsCategoryFavorite(gameTag, categoryName);
                    
                    var item = new OverlayCategoryItem
                    {
                        Name = categoryName,
                        Directory = categoryDir,
                        IsFavorite = isFavorite,
                        Thumbnail = await LoadCategoryThumbnailAsync(categoryDir),
                        IsSelected = false // Don't set selection here, let SelectCategory handle it
                    };
                    
                    categoryItems.Add(item);
                }
                
                // Sort categories: favorites first, then alphabetically
                var sortedCategories = categoryItems
                    .OrderByDescending(c => !string.IsNullOrEmpty(gameTag) && SettingsManager.IsCategoryFavorite(gameTag, c.Name))
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                
                // Add sorted categories to collection
                foreach (var category in sortedCategories)
                {
                    OverlayCategories.Add(category);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load overlay categories", ex);
            }
            finally
            {
                _isLoadingCategories = false;
            }
        }

        private async void LoadModsFromCategory(string categoryPath, bool activeOnly = false)
        {
            try
            {
                // Prevent concurrent loading
                if (_isLoadingMods) return;
                _isLoadingMods = true;

                try
                {
                    // Clear old thumbnails to prevent memory leak
                    foreach (var mod in OverlayMods)
                    {
                        mod.Thumbnail = null;
                    }
                    OverlayMods.Clear();

                    if (!System.IO.Directory.Exists(categoryPath))
                        return;

                    var modDirs = System.IO.Directory.GetDirectories(categoryPath);

                    // Check if NSFW filtering is enabled
                    bool hideNSFW = SettingsManager.Current.HideNSFWMods;

                    // Load NSFW list once for fast lookup (if filtering is enabled)
                    HashSet<string> nsfwMods = hideNSFW ? ModListManager.LoadNSFWModsList() : new HashSet<string>();

                    // Build list of mods with their active status and favorite status
                    var modItems = new List<(string dir, string name, bool isActive, bool isFavorite, bool isBroken)>();
                    var gameTag = SettingsManager.CurrentSelectedGame;

                    foreach (var modDir in modDirs)
                    {
                        var modName = Path.GetFileName(modDir);
                        var isActive = !modName.StartsWith("DISABLED", StringComparison.OrdinalIgnoreCase);

                        // Skip inactive mods if filtering
                        if (activeOnly && !isActive)
                            continue;

                        // Get clean name for checks
                        var cleanName = isActive ? modName : modName.Substring(8).TrimStart('_', '-', ' ');

                        // Skip NSFW mods if filtering is enabled (fast HashSet lookup)
                        if (hideNSFW && nsfwMods.Contains(cleanName))
                            continue;

                        // Extract category from path
                        var categoryName = "Other";
                        try
                        {
                            var parent = Directory.GetParent(modDir);
                            if (parent != null) categoryName = parent.Name;
                        }
                        catch { }

                        var isFavorite = !string.IsNullOrEmpty(gameTag) && SettingsManager.IsModFavorite(gameTag, categoryName, cleanName);

                        // Read isBroken from mod.json
                        bool isBroken = false;
                        try
                        {
                            var modJsonPath = Path.Combine(modDir, "mod.json");
                            if (File.Exists(modJsonPath))
                            {
                                var json = Services.FileAccessQueue.ReadAllText(modJsonPath);
                                using var doc = System.Text.Json.JsonDocument.Parse(json);
                                isBroken = doc.RootElement.TryGetProperty("modBroken", out var bp) && bp.ValueKind == System.Text.Json.JsonValueKind.True;
                            }
                        }
                        catch { }

                        modItems.Add((modDir, modName, isActive, isFavorite, isBroken));
                    }

                    // Sort: favorites first, then active mods, then alphabetically by name
                    var sortedMods = modItems
                        .OrderByDescending(m => m.isFavorite)
                        .ThenByDescending(m => m.isActive)
                        .ThenBy(m => m.isActive ? m.name : m.name.Substring(8).TrimStart('_', '-', ' '))
                        .ToList();

                    foreach (var (modDir, modName, isActive, isFavorite, isBroken) in sortedMods)
                    {
                        // Extract category from path
                        var categoryName = "Other";
                        try
                        {
                            var parent = Directory.GetParent(modDir);
                            if (parent != null) categoryName = parent.Name;
                        }
                        catch { }

                        // Clean name for display
                        var displayName = isActive ? modName : modName.Substring(8).TrimStart('_', '-', ' ');

                        var item = new OverlayModItem
                        {
                            Name = displayName,
                            Directory = modDir,
                            Category = categoryName,  // Add category
                            IsActive = isActive,
                            IsFavorite = isFavorite,
                            IsBroken = isBroken,
                            Thumbnail = await LoadThumbnailAsync(modDir),
                            Hotkeys = await LoadModHotkeysAsync(modDir)
                        };

                        OverlayMods.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to load overlay mods", ex);
                }
                finally
                {
                    _isLoadingMods = false;
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("OverlayWindow.LoadModsFromCategory failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("OverlayWindow.LoadModsFromCategory failed", ex);
            }
        }

        private void SelectCategory(OverlayCategoryItem category)
        {
            // Deselect all
            foreach (var cat in OverlayCategories)
                cat.IsSelected = false;
            
            // Select this one
            category.IsSelected = true;
            _selectedCategoryPath = category.Directory;
            
            // Clear selected mod and hotkeys when changing category
            _selectedModIndex = -1;
            _selectedMod = null;
            UpdateHotkeysPanel(null);
            
            // Load mods
            LoadModsFromCategory(category.Directory, _showActiveOnly);
            
            // Save state after category change (unless we're restoring state)
            if (!_isRestoringState)
            {
                SaveOverlayState();
            }
        }
        
        /// <summary>
        /// Toggle filter to show only active mods from all categories
        /// </summary>
        public void ToggleActiveOnlyFilter()
        {
            _showActiveOnly = !_showActiveOnly;
            
            if (_showActiveOnly)
            {
                // Load active mods from ALL categories
                LoadAllActiveMods();
            }
            else
            {
                // Reload current category normally
                if (_selectedCategoryPath != null)
                {
                    LoadModsFromCategory(_selectedCategoryPath, false);
                }
            }
            
            // Reset mod selection
            _selectedModIndex = -1;
            _selectedMod = null;
            UpdateHotkeysPanel(null);
            
            // Save state after filter change
            SaveOverlayState();
            
            Logger.LogInfo($"Overlay filter: show active only = {_showActiveOnly}");
        }
        
        /// <summary>
        /// Load all active mods from all categories
        /// </summary>
        private async void LoadAllActiveMods()
        {
            try
            {
                // Prevent concurrent loading
                if (_isLoadingMods) return;
                _isLoadingMods = true;

                try
                {
                    // Clear old thumbnails to prevent memory leak
                    foreach (var mod in OverlayMods)
                    {
                        mod.Thumbnail = null;
                    }
                    OverlayMods.Clear();

                    var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                    if (string.IsNullOrEmpty(modsPath) || !System.IO.Directory.Exists(modsPath))
                        return;

                    // Collect all active mods from all categories
                    var allActiveMods = new List<(string dir, string name)>();

                    foreach (var categoryDir in System.IO.Directory.GetDirectories(modsPath))
                    {
                        var categoryName = Path.GetFileName(categoryDir);

                        // Get all mods in this category
                        foreach (var modDir in System.IO.Directory.GetDirectories(categoryDir))
                        {
                            var modName = Path.GetFileName(modDir);
                            var isActive = !modName.StartsWith("DISABLED", StringComparison.OrdinalIgnoreCase);

                            // Only show active mods
                            if (!isActive)
                                continue;

                            allActiveMods.Add((modDir, modName));
                        }
                    }

                    // Sort alphabetically by name
                    var sortedMods = allActiveMods.OrderBy(m => m.name).ToList();

                    foreach (var (modDir, modName) in sortedMods)
                    {
                        // Extract category from path
                        var categoryName = "Other";
                        try
                        {
                            var parent = Directory.GetParent(modDir);
                            if (parent != null) categoryName = parent.Name;
                        }
                        catch { }

                        var item = new OverlayModItem
                        {
                            Name = modName,
                            Directory = modDir,
                            Category = categoryName,  // Add category
                            IsActive = true,
                            Thumbnail = await LoadThumbnailAsync(modDir)
                        };

                        OverlayMods.Add(item);
                    }

                    Logger.LogInfo($"Loaded {OverlayMods.Count} active mods from all categories");

                    // If selection went out of range during async load, reset it
                    if (_selectedModIndex >= OverlayMods.Count)
                    {
                        _selectedModIndex = -1;
                        _selectedMod = null;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to load all active mods", ex);
                }
                finally
                {
                    _isLoadingMods = false;
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("OverlayWindow.LoadAllActiveMods failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("OverlayWindow.LoadAllActiveMods failed", ex);
            }
        }

        private void CategoriesRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Element is Button button && args.Index < OverlayCategories.Count)
            {
                button.DataContext = OverlayCategories[args.Index];
            }
        }

        private void ModsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Element is Button button && args.Index < OverlayMods.Count)
            {
                button.DataContext = OverlayMods[args.Index];
            }
        }

        private void CategoryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is OverlayCategoryItem item)
            {
                SelectCategory(item);
            }
        }

        private async System.Threading.Tasks.Task<BitmapImage?> LoadCategoryThumbnailAsync(string categoryDir)
        {
            try
            {
                // Check format based on user settings
                var imageFormat = SettingsManager.Current.ImageFormat;
                var preferWebP = imageFormat.Equals("WebP", StringComparison.OrdinalIgnoreCase);
                
                Logger.LogInfo($"LoadCategoryThumbnailAsync: categoryDir={categoryDir}, preferWebP={preferWebP}");
                
                // Try catprev (webp/jpg) first, then catmini (webp/jpg), then gbicon (webp/png only - needs alpha)
                var catprevWebp = Path.Combine(categoryDir, "catprev.webp");
                var catprevJpg = Path.Combine(categoryDir, "catprev.jpg");
                var catminiWebp = Path.Combine(categoryDir, "catmini.webp");
                var catminiJpg = Path.Combine(categoryDir, "catmini.jpg");
                var gbiconWebp = Path.Combine(categoryDir, "gbicon.webp");
                var gbiconPng = Path.Combine(categoryDir, "gbicon.png");
                
                string? thumbPath = null;
                
                // Check in priority order
                if (preferWebP)
                {
                    if (File.Exists(catprevWebp))
                        thumbPath = catprevWebp;
                    else if (File.Exists(catprevJpg))
                        thumbPath = catprevJpg;
                    else if (File.Exists(catminiWebp))
                        thumbPath = catminiWebp;
                    else if (File.Exists(catminiJpg))
                        thumbPath = catminiJpg;
                    else if (File.Exists(gbiconWebp))
                        thumbPath = gbiconWebp;
                    else if (File.Exists(gbiconPng))
                        thumbPath = gbiconPng;
                }
                else
                {
                    if (File.Exists(catprevJpg))
                        thumbPath = catprevJpg;
                    else if (File.Exists(catprevWebp))
                        thumbPath = catprevWebp;
                    else if (File.Exists(catminiJpg))
                        thumbPath = catminiJpg;
                    else if (File.Exists(catminiWebp))
                        thumbPath = catminiWebp;
                    else if (File.Exists(gbiconPng))
                        thumbPath = gbiconPng;
                    else if (File.Exists(gbiconWebp))
                        thumbPath = gbiconWebp;
                }
                
                Logger.LogInfo($"LoadCategoryThumbnailAsync: Selected path={thumbPath}");
                
                if (thumbPath != null)
                {
                    var tcs = new System.Threading.Tasks.TaskCompletionSource<BitmapImage?>();
                    
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            Logger.LogInfo($"LoadCategoryThumbnailAsync: Creating BitmapImage for {thumbPath}");
                            var bitmap = new BitmapImage();
                            bitmap.DecodePixelWidth = 64;
                            bitmap.DecodePixelHeight = 64;
                            
                            var absolutePath = Path.GetFullPath(thumbPath);
                            Logger.LogInfo($"LoadCategoryThumbnailAsync: Absolute path={absolutePath}");
                            bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
                            
                            Logger.LogInfo($"LoadCategoryThumbnailAsync: BitmapImage created successfully");
                            tcs.SetResult(bitmap);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"LoadCategoryThumbnailAsync: Failed to create BitmapImage for {thumbPath}", ex);
                            tcs.SetResult(null);
                        }
                    });
                    
                    // Bounded wait: if the UI queue is shutting down (or TryEnqueue failed) the
                    // callback never runs. An unbounded await here left the caller - and its
                    // _isLoadingMods guard - stuck permanently, so the overlay stopped loading mods.
                    var completed = await System.Threading.Tasks.Task.WhenAny(
                        tcs.Task,
                        System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(5)));

                    return completed == tcs.Task ? await tcs.Task : null;
                }
                else
                {
                    Logger.LogWarning($"LoadCategoryThumbnailAsync: No thumbnail found for {categoryDir}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load category thumbnail for {categoryDir}", ex);
            }
            
            return null;
        }

        private async System.Threading.Tasks.Task<BitmapImage?> LoadThumbnailAsync(string modDir)
        {
            try
            {
                // Check format based on user settings
                var imageFormat = SettingsManager.Current.ImageFormat;
                var preferWebP = imageFormat.Equals("WebP", StringComparison.OrdinalIgnoreCase);
                
                var thumbPathWebp = Path.Combine(modDir, "minitile.webp");
                var thumbPathJpg = Path.Combine(modDir, "minitile.jpg");
                
                string? thumbPath = null;
                
                // Check in order based on user preference
                if (preferWebP)
                {
                    if (File.Exists(thumbPathWebp))
                        thumbPath = thumbPathWebp;
                    else if (File.Exists(thumbPathJpg))
                        thumbPath = thumbPathJpg;
                }
                else
                {
                    if (File.Exists(thumbPathJpg))
                        thumbPath = thumbPathJpg;
                    else if (File.Exists(thumbPathWebp))
                        thumbPath = thumbPathWebp;
                }
                
                if (thumbPath != null)
                {
                    var tcs = new System.Threading.Tasks.TaskCompletionSource<BitmapImage?>();
                    
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.DecodePixelWidth = 208;
                            bitmap.DecodePixelHeight = 250;
                            
                            // Use UriSource for WebP support via Windows codecs
                            // IMPORTANT: Must use absolute path for WebP to work
                            var absolutePath = Path.GetFullPath(thumbPath);
                            bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
                            
                            tcs.SetResult(bitmap);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to create BitmapImage for {thumbPath}", ex);
                            tcs.SetResult(null);
                        }
                    });
                    
                    // Bounded wait: if the UI queue is shutting down (or TryEnqueue failed) the
                    // callback never runs. An unbounded await here left the caller - and its
                    // _isLoadingMods guard - stuck permanently, so the overlay stopped loading mods.
                    var completed = await System.Threading.Tasks.Task.WhenAny(
                        tcs.Task,
                        System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(5)));

                    return completed == tcs.Task ? await tcs.Task : null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load mod thumbnail for {modDir}", ex);
            }
            
            return null;
        }

        private void ModItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is OverlayModItem item)
            {
                // Update selected mod and hotkeys panel
                UpdateSelectedMod(item);
                
                // Left click toggles the mod
                ToggleMod(item);
            }
        }

        private void ModItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is OverlayModItem item)
            {
                // Right click only selects the mod (shows hotkeys) without toggling
                UpdateSelectedMod(item);
                
                // Mark event as handled to prevent context menu
                e.Handled = true;
                
                Logger.LogInfo($"Right-clicked mod: {item.Name} - selected without toggling");
            }
        }

        /// <summary>
        /// Update selected mod and refresh hotkeys panel
        /// </summary>
        private void UpdateSelectedMod(OverlayModItem item)
        {
            try
            {
                // Deselect all mods
                foreach (var mod in OverlayMods)
                {
                    mod.IsSelected = false;
                }
                
                // Select clicked mod
                item.IsSelected = true;
                _selectedMod = item;
                
                // Update hotkeys panel
                UpdateHotkeysPanel(item);
                
                // Save state after mod selection (unless we're restoring state)
                if (!_isRestoringState)
                {
                    SaveOverlayState();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to update selected mod", ex);
            }
        }

        private void ToggleMod(OverlayModItem item, bool vibrate = false)
        {
            if (_isToggling)
            {
                Logger.LogInfo("ToggleMod: skipped — already toggling");
                return;
            }
            Logger.LogInfo($"ToggleMod: starting for {item.Name}, IsActive={item.IsActive}, vibrate={vibrate}");
            _ = ToggleModAsync(item, vibrate);
        }

        private async Task ToggleModAsync(OverlayModItem item, bool vibrate = false)
        {
            _isToggling = true;
            try
            {
                var dir = item.Directory;
                var parentDir = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parentDir)) return;
                
                var currentName = Path.GetFileName(dir);
                
                string newName;
                bool newState;
                
                if (item.IsActive)
                {
                    // Disable: add DISABLED prefix
                    newName = "DISABLED_" + currentName;
                    newState = false;
                }
                else
                {
                    // Enable: remove DISABLED prefix
                    newName = currentName.StartsWith("DISABLED_") 
                        ? currentName.Substring(9) 
                        : currentName.TrimStart('D', 'I', 'S', 'A', 'B', 'L', 'E', '_');
                    newState = true;

                    // Auto-deactivate conflicting mods in the same category (if setting enabled)
                    if (SettingsManager.Current.AutoDeactivateConflictingMods)
                    {
                        var categoryName = Path.GetFileName(parentDir);
                        var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                        var conflictExcluded = SettingsManager.GetConflictExcludedCategories(gameTag);

                        if (!conflictExcluded.Contains(categoryName, StringComparer.OrdinalIgnoreCase))
                        {
                            await Pages.ModGridPage.DeactivateModsInCategory(categoryName, newName);

                            // Update IsActive state for other items in the same category
                            foreach (var other in OverlayMods)
                            {
                                if (other != item && other.IsActive &&
                                    string.Equals(Path.GetFileName(Path.GetDirectoryName(other.Directory)), categoryName, StringComparison.OrdinalIgnoreCase))
                                {
                                    other.IsActive = false;
                                    var otherName = Path.GetFileName(other.Directory);
                                    other.Directory = Path.Combine(parentDir, "DISABLED_" + otherName);
                                }
                            }
                        }
                    }
                }
                
                var newPath = Path.Combine(parentDir!, newName);
                
                bool toggled = false;
                if (dir != newPath)
                {
                    if (System.IO.Directory.Exists(newPath))
                    {
                        Logger.LogInfo($"Overlay toggle: target path already exists, updating state only: {newName}");
                        toggled = true;
                    }
                    else
                    {
                        Services.FileAccessQueue.MoveDirectory(dir, newPath);
                        toggled = true;
                    }
                }

                if (toggled)
                {
                    // Send F10 with debouncing to prevent spam when toggling multiple mods quickly
                    if (SettingsManager.Current.SendF10OnModChange)
                    {
                        Logger.LogInfo($"ToggleModAsync: triggering debounced F10 send");
                        _mainWindow?.SendF10KeyPressDebounced();
                    }

                    // UI updates on UI thread
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        item.Directory = newPath;
                        item.IsActive = newState;
                        
                        Logger.LogInfo($"Overlay toggled mod: {currentName} -> {newName}");
                        
                        if (vibrate)
                            _gamepadManager?.Vibrate(30000, 30000, 300);
                        
                        ModToggleRequested?.Invoke(newPath);
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to toggle mod from overlay: {item.Name}", ex);
            }
            finally
            {
                _isToggling = false;
            }
        }

        /// <summary>
        /// Check if overlay window is currently visible
        /// </summary>
        public bool IsOverlayVisible => _appWindow?.IsVisible == true;

        /// <summary>
        /// Refresh overlay data when favorites change
        /// </summary>
        public void RefreshOverlayData()
        {
            try
            {
                Logger.LogInfo("RefreshOverlayData: Starting overlay refresh");
                
                // Refresh categories and mods to update favorite status and sorting
                DispatcherQueue.TryEnqueue(() =>
                {
                    LoadCurrentCategoryMods();
                });
                
                Logger.LogInfo("RefreshOverlayData: Overlay refresh completed");
            }
            catch (Exception ex)
            {
                Logger.LogError("RefreshOverlayData: Failed to refresh overlay", ex);
            }
        }
    }
}
