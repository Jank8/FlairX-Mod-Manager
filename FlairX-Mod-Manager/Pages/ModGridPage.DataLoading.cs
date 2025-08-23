using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    /// <summary>
    /// ModGridPage partial class - Data loading and mod data management
    /// </summary>
    public sealed partial class ModGridPage : Page
    {
        private void LoadCategories()
        {
            LogToGridLog($"LoadCategories() called - CurrentViewMode: {CurrentViewMode}");
            System.Diagnostics.Debug.WriteLine($"LoadCategories() called - CurrentViewMode: {CurrentViewMode}");
            
            // Clear current category when loading categories view
            _currentCategory = null;
            
            // --- FIX: Always clear mod data to avoid leaking mod tiles into category view ---
            _allModData.Clear();

            var gameTag = SettingsManager.CurrentSelectedGame;
            if (string.IsNullOrEmpty(gameTag)) 
            {
                LogToGridLog("LoadCategories: No game selected, returning");
                return;
            }
            
            var gameModLibraryPath = AppConstants.GameConfig.GetModLibraryPath(gameTag);
            string modLibraryPath = PathManager.GetAbsolutePath(gameModLibraryPath);
            
            if (!Directory.Exists(modLibraryPath)) return;
            
            var categories = new List<ModTile>();
            
            foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
            {
                if (!Directory.Exists(categoryDir)) continue;
                
                var categoryName = Path.GetFileName(categoryDir);
                if (string.IsNullOrEmpty(categoryName)) continue;
                
                // Skip "Other" category in category view - it has its own menu item
                if (categoryName.Equals("Other", StringComparison.OrdinalIgnoreCase))
                    continue;
                
                // Add all categories, even empty ones
                var categoryTile = new ModTile
                {
                    Name = categoryName,
                    Directory = categoryName,
                    IsCategory = true,
                    ImagePath = GetCategoryMiniTilePath(categoryName)
                };
                
                // Load mini tile image
                LoadCategoryMiniTile(categoryTile);
                categories.Add(categoryTile);
            }
            
            // Update UI on main thread
            DispatcherQueue.TryEnqueue(() =>
            {
                _allMods.Clear();
                foreach (var category in categories.OrderBy(c => c.Name))
                {
                    _allMods.Add(category);
                }
                
                // Set the ItemsSource to display categories
                ModsGrid.ItemsSource = _allMods;
                
                var langDict = SharedUtilities.LoadLanguageDictionary();
                CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "All_Categories");
                
                // Hide back button and folder button in categories view
                CategoryBackButton.Visibility = Visibility.Collapsed;
                CategoryOpenFolderButton.Visibility = Visibility.Collapsed;
                
                // Update context flyout to disable context menu for category tiles
                UpdateContextFlyout();
                
                LogToGridLog($"Loaded {categories.Count} categories");
            });
        }

        private string GetCategoryMiniTilePath(string categoryName)
        {
            var gameTag = SettingsManager.CurrentSelectedGame;
            if (string.IsNullOrEmpty(gameTag)) return "";
            
            var gameModLibraryPath = AppConstants.GameConfig.GetModLibraryPath(gameTag);
            string categoryPath = PathManager.GetAbsolutePath(Path.Combine(gameModLibraryPath, categoryName));
            
            // Look for category-specific preview image only
            string categoryPreview = Path.Combine(categoryPath, "catprev.jpg");
            LogToGridLog($"Looking for category preview: {categoryPreview}");
            return categoryPreview;
        }

        private void LoadCategoryMiniTile(ModTile categoryTile)
        {
            var miniTilePath = categoryTile.ImagePath;
            
            LogToGridLog($"LoadCategoryMiniTile: Checking {miniTilePath} for category {categoryTile.Name}");
            
            // Load minitile.jpg if it exists (same as mods)
            if (File.Exists(miniTilePath))
            {
                LogToGridLog($"LoadCategoryMiniTile: Found minitile for {categoryTile.Name}");
                DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        using var stream = File.OpenRead(miniTilePath);
                        await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
                        categoryTile.ImageSource = bitmap;
                    }
                    catch (Exception ex)
                    {
                        LogToGridLog($"Failed to load category minitile for {categoryTile.Name}: {ex.Message}");
                    }
                });
            }
            else
            {
                LogToGridLog($"LoadCategoryMiniTile: No minitile found for {categoryTile.Name} at {miniTilePath}");
            }
            // If minitile.jpg doesn't exist, categoryTile.ImageSource remains null (no image)
        }

        private void LoadModsByCategory(string category)
        {
            LogToGridLog($"LoadModsByCategory() called for category: {category}");
            
            // Exit table view if active and clear sorting when navigating to category
            if (CurrentViewMode == ViewMode.Table)
            {
                _currentSortMode = SortMode.None;
                CurrentViewMode = ViewMode.Mods;
            }
            
            // First, load all mod data for this category (lightweight)
            LoadCategoryModData(category);
            
            // Then create only the initial visible ModTiles (same as LoadAllMods)
            LoadVirtualizedModTiles();
        }

        private void LoadCategoryModData(string category)
        {
            var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
            if (string.IsNullOrEmpty(modLibraryPath))
                modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(modLibraryPath)) return;
            
            _allModData.Clear();
            var categoryPath = Path.Combine(modLibraryPath, category);
            
            if (Directory.Exists(categoryPath))
            {
                foreach (var modDir in Directory.GetDirectories(categoryPath))
                {
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (!File.Exists(modJsonPath)) continue;
                    
                    try
                    {
                        var name = Path.GetFileName(modDir);
                        string previewPath = GetOptimalImagePath(modDir);
                        var dirName = Path.GetFileName(modDir);
                        var isActive = _activeMods.TryGetValue(dirName, out var active) && active;
                        
                        // Parse JSON for additional data
                        var modCharacter = "other";
                        var modAuthor = "";
                        var modUrl = "";
                        var lastChecked = DateTime.MinValue;
                        var lastUpdated = DateTime.MinValue;
                        
                        try
                        {
                            var json = File.ReadAllText(modJsonPath);
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            
                            modCharacter = root.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "other" : "other";
                            modAuthor = root.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? "" : "";
                            modUrl = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                            
                            if (root.TryGetProperty("dateChecked", out var dateCheckedProp) && dateCheckedProp.ValueKind == JsonValueKind.String)
                            {
                                DateTime.TryParse(dateCheckedProp.GetString(), out lastChecked);
                            }
                            
                            if (root.TryGetProperty("dateUpdated", out var dateUpdatedProp) && dateUpdatedProp.ValueKind == JsonValueKind.String)
                            {
                                DateTime.TryParse(dateUpdatedProp.GetString(), out lastUpdated);
                            }
                        }
                        catch
                        {
                            // Use defaults if JSON parsing fails
                        }
                        
                        // Use file system dates as fallback
                        if (lastChecked == DateTime.MinValue)
                        {
                            lastChecked = File.GetLastAccessTime(modDir);
                        }
                        
                        if (lastUpdated == DateTime.MinValue)
                        {
                            lastUpdated = File.GetLastWriteTime(modDir);
                        }
                        
                        var modData = new ModData
                        {
                            Name = name,
                            ImagePath = previewPath,
                            Directory = dirName,
                            IsActive = isActive,
                            Character = modCharacter,
                            Author = modAuthor,
                            Url = modUrl,
                            Category = category,
                            LastChecked = lastChecked,
                            LastUpdated = lastUpdated
                        };
                        
                        _allModData.Add(modData);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to process mod in {modDir}", ex);
                    }
                }
            }
            
            // Sort the lightweight data: active first (if enabled), then alphabetically
            if (SettingsManager.Current.ActiveModsToTopEnabled)
            {
                _allModData = _allModData
                    .OrderByDescending(m => m.IsActive)
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                _allModData = _allModData
                    .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            
            LogToGridLog($"Loaded {_allModData.Count} mod data entries for category: {category}");
        }

        private void LoadMods(string category)
        {
            LogToGridLog($"LoadMods() called for category: {category}");
            
            // Use the same logic as LoadModsByCategory but with direct ModTile creation
            // First, load all mod data for this category (lightweight)
            LoadCategoryModData(category);
            
            // Then create ModTiles directly (not virtualized like LoadModsByCategory)
            var mods = new List<ModTile>();
            foreach (var modData in _allModData)
            {
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
                
                mods.Add(modTile);
            }
                
            LogToGridLog($"Loaded {mods.Count} mods for category: {category}");
            ModsGrid.ItemsSource = mods;
            
            // Load visible images after setting new data source
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to let the grid update
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
        }

        public void LoadAllMods()
        {
            LogToGridLog($"LoadAllMods() called - CurrentViewMode: {CurrentViewMode}");
            System.Diagnostics.Debug.WriteLine($"LoadAllMods() called - CurrentViewMode: {CurrentViewMode}");
            System.Diagnostics.Debug.WriteLine($"LoadAllMods() stack trace: {Environment.StackTrace}");
            
            // Exit table view if active and clear sorting when loading all mods
            if (CurrentViewMode == ViewMode.Table)
            {
                _currentSortMode = SortMode.None;
                CurrentViewMode = ViewMode.Mods;
            }
            
            // First, load all mod data (lightweight)
            LoadAllModData();
            
            // Then create only the initial visible ModTiles
            LoadVirtualizedModTiles();
        }

        private void LoadAllModData()
        {
            var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
            if (string.IsNullOrEmpty(modLibraryPath))
                modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(modLibraryPath)) return;
            
            _allModData.Clear();
            var cacheHits = 0;
            var cacheMisses = 0;
            
            // Process category directories (1st level) and mod directories (2nd level)
            foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
            {
                if (!Directory.Exists(categoryDir)) continue;
                
                foreach (var modDir in Directory.GetDirectories(categoryDir))
                {
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (!File.Exists(modJsonPath)) continue;
                    
                    var dirName = Path.GetFileName(modDir);
                    var modData = GetCachedModData(modDir, modJsonPath);
                    
                    if (modData != null)
                    {
                        // Update active state (this can change without file modification)
                        modData.IsActive = _activeMods.TryGetValue(dirName, out var active) && active;
                        
                        // Add category information from folder structure
                        modData.Category = Path.GetFileName(categoryDir);
                        
                        // Skip "Other" category mods in All Mods view - they have their own category
                        if (!string.Equals(modData.Category, "Other", StringComparison.OrdinalIgnoreCase))
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
            }
            
            // Sort the lightweight data: active first (if enabled), then alphabetically
            if (SettingsManager.Current.ActiveModsToTopEnabled)
            {
                _allModData = _allModData
                    .OrderByDescending(m => m.IsActive)
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                _allModData = _allModData
                    .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
                
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
                    
                    // Use file system dates as fallback
                    if (lastChecked == DateTime.MinValue)
                    {
                        lastChecked = File.GetLastAccessTime(dir);
                    }
                    
                    if (lastUpdated == DateTime.MinValue)
                    {
                        lastUpdated = File.GetLastWriteTime(dir);
                    }
                    
                    var name = Path.GetFileName(dir);
                    string previewPath = GetOptimalImagePath(dir);
                    var isActive = _activeMods.TryGetValue(dirName, out var active) && active;
                    
                    // Determine category from directory structure
                    var categoryName = "Unknown";
                    try
                    {
                        var parentDir = Directory.GetParent(dir);
                        if (parentDir != null)
                        {
                            categoryName = parentDir.Name;
                        }
                    }
                    catch
                    {
                        // Use default if unable to determine category
                    }
                    
                    var modData = new ModData
                    { 
                        Name = name, 
                        ImagePath = previewPath, 
                        Directory = dirName, 
                        IsActive = isActive,
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
                    Category = modData.Category,
                    Author = modData.Author,
                    Url = modData.Url,
                    LastChecked = modData.LastChecked,
                    LastUpdated = modData.LastUpdated,
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

        private void LoadActiveModsOnly()
        {
            LogToGridLog("LoadActiveModsOnly() called");
            
            // For active mods, we need to load from ALL categories including "Other"
            LoadActiveModData();
            
            // Filter to show only active mods from _allModData
            var activeModTiles = new List<ModTile>();
            foreach (var modData in _allModData)
            {
                // Update active state first
                modData.IsActive = _activeMods.TryGetValue(modData.Directory, out var active) && active;
                
                if (modData.IsActive)
                {
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
                    activeModTiles.Add(modTile);
                }
            }
            
            // Sort active mods alphabetically
            var sortedActiveMods = activeModTiles
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            
            LogToGridLog($"Found {sortedActiveMods.Count} active mods");
            ModsGrid.ItemsSource = sortedActiveMods;
            
            // Load visible images after setting new data source
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to let the grid update
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
        }

        private void LoadActiveModData()
        {
            var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
            if (string.IsNullOrEmpty(modLibraryPath))
                modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(modLibraryPath)) return;
            
            _allModData.Clear();
            var cacheHits = 0;
            var cacheMisses = 0;
            
            // Process category directories excluding "Other" for active mods view (same as All Mods)
            foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
            {
                if (!Directory.Exists(categoryDir)) continue;
                
                foreach (var modDir in Directory.GetDirectories(categoryDir))
                {
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (!File.Exists(modJsonPath)) continue;
                    
                    var dirName = Path.GetFileName(modDir);
                    var modData = GetCachedModData(modDir, modJsonPath);
                    
                    if (modData != null)
                    {
                        // Update active state (this can change without file modification)
                        modData.IsActive = _activeMods.TryGetValue(dirName, out var active) && active;
                        
                        // Add category information from folder structure
                        modData.Category = Path.GetFileName(categoryDir);
                        
                        // Skip "Other" category mods in Active Mods view - same as All Mods
                        if (!string.Equals(modData.Category, "Other", StringComparison.OrdinalIgnoreCase))
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
            }
            
            // Sort the lightweight data: active first (if enabled), then alphabetically
            if (SettingsManager.Current.ActiveModsToTopEnabled)
            {
                _allModData = _allModData
                    .OrderByDescending(m => m.IsActive)
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                _allModData = _allModData
                    .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
                
            LogToGridLog($"Loaded {_allModData.Count} mod data entries for active mods view (Cache hits: {cacheHits}, Cache misses: {cacheMisses})");
        }
    }
}