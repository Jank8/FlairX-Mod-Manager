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
        /// <summary>
        /// Get filtered mod data based on current settings (hide broken, hide NSFW)
        /// Uses cached persistent lists for fast filtering
        /// </summary>
        private IEnumerable<ModData> GetFilteredModData()
        {
            bool hideBroken = SettingsManager.Current.HideBrokenMods;
            bool hideNSFW = SettingsManager.Current.HideNSFWMods;
            
            // Use cached lists if available
            // If cache is null, don't filter (lists haven't been built yet)
            HashSet<string>? nsfwMods = hideNSFW ? _cachedNSFWMods : null;
            HashSet<string>? brokenMods = hideBroken ? _cachedBrokenMods : null;
            
            return _allModData.Where(modData => 
            {
                // Filter broken mods if setting is enabled AND cache exists
                if (hideBroken && brokenMods != null && brokenMods.Contains(modData.Name))
                    return false;
                
                // Filter NSFW mods if setting is enabled AND cache exists
                if (hideNSFW && nsfwMods != null && nsfwMods.Contains(modData.Name))
                    return false;
                
                return true;
            });
        }
        
        public void LoadCategories()
        {
            LogToGridLog($"LoadCategories() called - CurrentViewMode: {CurrentViewMode}");
            Logger.LogDebug($"LoadCategories() called - CurrentViewMode: {CurrentViewMode}");
            
            // Clear current category when loading categories view
            _currentCategory = null;
            
            // --- FIX: Always clear mod data to avoid leaking mod tiles into category view ---
            _allModData.Clear();
            _lastLoadedModDataIndex = 0;

            var gameTag = SettingsManager.CurrentSelectedGame;
            if (string.IsNullOrEmpty(gameTag)) 
            {
                LogToGridLog("LoadCategories: No game selected, returning");
                return;
            }
            
            var gameModsPath = AppConstants.GameConfig.GetModsPath(gameTag);
            string modsPath = PathManager.GetAbsolutePath(gameModsPath);
            
            if (!Directory.Exists(modsPath)) return;
            
            var categories = new List<ModTile>();
            
            foreach (var categoryDir in Directory.GetDirectories(modsPath))
            {
                if (!Directory.Exists(categoryDir)) continue;
                
                var categoryName = Path.GetFileName(categoryDir);
                if (string.IsNullOrEmpty(categoryName)) continue;
                
                // Add all categories, even empty ones
                var categoryTile = new ModTile
                {
                    Name = categoryName,
                    Directory = categoryName,
                    IsCategory = true,
                    ImagePath = GetCategoryMiniTilePath(categoryName),
                    IsFavorite = SettingsManager.IsCategoryFavorite(gameTag, categoryName) // Load favorite status
                };
                
                // Load mini tile image
                LoadCategoryMiniTile(categoryTile);
                categories.Add(categoryTile);
            }
            
            // Sort categories: favorites first, then alphabetically
            var sortedCategories = categories.OrderByDescending(c => c.IsFavorite)
                                            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                                            .ToList();
            
            // Update UI on main thread
            DispatcherQueue.TryEnqueue(() =>
            {
                _allMods.Clear();
                foreach (var category in sortedCategories)
                {
                    _allMods.Add(category);
                }
                
                // Set the ItemsSource to display categories
                ModsGrid.ItemsSource = _allMods;
                UpdateEmptyState();
                
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
            
            var gameModsPath = AppConstants.GameConfig.GetModsPath(gameTag);
            string categoryPath = PathManager.GetAbsolutePath(Path.Combine(gameModsPath, categoryName));
            
            // Look for category mini tile image (check both formats)
            string categoryMiniWebp = Path.Combine(categoryPath, "catmini.webp");
            string categoryMiniJpg = Path.Combine(categoryPath, "catmini.jpg");
            
            if (File.Exists(categoryMiniWebp))
            {
                LogToGridLog($"Found category mini tile: {categoryMiniWebp}");
                return categoryMiniWebp;
            }
            else if (File.Exists(categoryMiniJpg))
            {
                LogToGridLog($"Found category mini tile: {categoryMiniJpg}");
                return categoryMiniJpg;
            }
            
            LogToGridLog($"No category mini tile found at: {categoryPath}");
            return categoryMiniJpg; // Return default path for consistency
        }

        private void LoadCategoryMiniTile(ModTile categoryTile)
        {
            var miniTilePath = categoryTile.ImagePath;
            
            LogToGridLog($"LoadCategoryMiniTile: Checking {miniTilePath} for category {categoryTile.Name}");
            
            // Load minitile.jpg if it exists (same as mods)
            if (File.Exists(miniTilePath))
            {
                LogToGridLog($"LoadCategoryMiniTile: Found minitile for {categoryTile.Name}");
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        // Convert to absolute path for UriSource
                        var absolutePath = Path.GetFullPath(miniTilePath);
                        // Use UriSource for WebP support via Windows codecs
                        bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
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
            var modsPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
            
            if (!Directory.Exists(modsPath)) return;
            
            _allModData.Clear();
            _lastLoadedModDataIndex = 0;
            var categoryPath = Path.Combine(modsPath, category);
            
            if (Directory.Exists(categoryPath))
            {
                foreach (var modDir in Directory.GetDirectories(categoryPath))
                {
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (!File.Exists(modJsonPath)) continue;
                    
                    try
                    {
                        var dirName = Path.GetFileName(modDir);
                        var name = GetCleanModName(dirName);
                        string previewPath = GetOptimalImagePath(modDir);
                        var isActive = IsModActive(dirName);
                        
                        // Parse JSON for additional data
                        var modCharacter = "other";
                        var modAuthor = "";
                        var modUrl = "";
                        var lastChecked = DateTime.MinValue;
                        var lastUpdated = DateTime.MinValue;
                        var isNSFW = false;
                        var isBroken = false;
                        
                        bool hasUpdate = false;
                        
                        try
                        {
                            var json = Services.FileAccessQueue.ReadAllText(modJsonPath);
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            
                            modCharacter = root.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "other" : "other";
                            modAuthor = root.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? "" : "";
                            modUrl = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                            isNSFW = root.TryGetProperty("isNSFW", out var nsfwProp) && nsfwProp.ValueKind == JsonValueKind.True;
                            isBroken = root.TryGetProperty("modBroken", out var brokenProp) && brokenProp.ValueKind == JsonValueKind.True;
                            if (root.TryGetProperty("dateChecked", out var dateCheckedProp) && dateCheckedProp.ValueKind == JsonValueKind.String)
                            {
                                DateTime.TryParse(dateCheckedProp.GetString(), out lastChecked);
                            }
                            
                            if (root.TryGetProperty("dateUpdated", out var dateUpdatedProp) && dateUpdatedProp.ValueKind == JsonValueKind.String)
                            {
                                DateTime.TryParse(dateUpdatedProp.GetString(), out lastUpdated);
                            }
                            
                            // Check for available updates
                            if (root.TryGetProperty("gbChangeDate", out var gbChangeProp) && gbChangeProp.ValueKind == JsonValueKind.String &&
                                root.TryGetProperty("dateUpdated", out var dateUpdProp) && dateUpdProp.ValueKind == JsonValueKind.String)
                            {
                                if (DateTime.TryParse(gbChangeProp.GetString(), out var gbDate) && 
                                    DateTime.TryParse(dateUpdProp.GetString(), out var updatedDate))
                                {
                                    hasUpdate = gbDate > updatedDate;
                                }
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
                            Name = name,  // Already cleaned by GetCleanModName above
                            ImagePath = previewPath,
                            Directory = dirName,  // Use actual folder name for file operations
                            IsActive = isActive,
                            Character = modCharacter,
                            Author = modAuthor,
                            Url = modUrl,
                            Category = category,
                            LastChecked = lastChecked,
                            LastUpdated = lastUpdated,
                            HasUpdate = hasUpdate,
                            IsNSFW = isNSFW,
                            IsBroken = isBroken
                        };
                        
                        _allModData.Add(modData);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to process mod in {modDir}", ex);
                    }
                }
            }
            
            // Sort the lightweight data: favorites first, then active first (if enabled), then alphabetically
            if (SettingsManager.Current.ActiveModsToTopEnabled)
            {
                var gameTag = SettingsManager.CurrentSelectedGame ?? "";
                _allModData = _allModData
                    .OrderByDescending(m => SettingsManager.IsModFavorite(gameTag, m.Name))
                    .ThenByDescending(m => m.IsActive)
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                var gameTag = SettingsManager.CurrentSelectedGame ?? "";
                _allModData = _allModData
                    .OrderByDescending(m => SettingsManager.IsModFavorite(gameTag, m.Name))
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
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
            // GetFilteredModData() already applies broken and NSFW filters
            var mods = new List<ModTile>();
            
            foreach (var modData in GetFilteredModData())
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
                    HasUpdate = CheckForUpdateLive(modData.Directory), // Live check without cache
                    IsVisible = true,
                    IsBroken = modData.IsBroken,
                    IsNSFW = modData.IsNSFW,
                    IsFavorite = SettingsManager.IsModFavorite(SettingsManager.CurrentSelectedGame ?? "", modData.Name), // Load favorite status
                    ImageSource = null // Start with no image - lazy load when visible
                };
                
                mods.Add(modTile);
            }
                
            LogToGridLog($"Loaded {mods.Count} mods for category: {category}");
            ModsGrid.ItemsSource = mods;
            UpdateEmptyState();
            
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
            Logger.LogDebug($"LoadAllMods() called - CurrentViewMode: {CurrentViewMode}");
            
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
            var modsPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
            
            if (!Directory.Exists(modsPath)) return;
            
            _allModData.Clear();
            _lastLoadedModDataIndex = 0;
            
            // Lists for rebuilding persistent JSON files
            var nsfwMods = new List<string>();
            var brokenMods = new List<string>();
            var outdatedMods = new List<string>();
            
            // Process category directories (1st level) and mod directories (2nd level)
            foreach (var categoryDir in Directory.GetDirectories(modsPath))
            {
                if (!Directory.Exists(categoryDir)) continue;
                
                var categoryName = Path.GetFileName(categoryDir);
                
                foreach (var modDir in Directory.GetDirectories(categoryDir))
                {
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (!File.Exists(modJsonPath)) continue;
                    
                    var dirName = Path.GetFileName(modDir);
                    
                    // Read mod data directly from file (no cache)
                    try
                    {
                        var json = Services.FileAccessQueue.ReadAllText(modJsonPath);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        
                        var modCharacter = root.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "other" : "other";
                        var modAuthor = root.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? "" : "";
                        var modUrl = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                        var isNSFW = root.TryGetProperty("isNSFW", out var nsfwProp) && nsfwProp.ValueKind == JsonValueKind.True;
                        var isBroken = root.TryGetProperty("modBroken", out var brokenProp) && brokenProp.ValueKind == JsonValueKind.True;
                        
                        // Parse dates
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
                        
                        // Calculate HasUpdate here (we already have the data from mod.json)
                        bool hasUpdate = false;
                        if (root.TryGetProperty("gbChangeDate", out var gbChangeProp) && gbChangeProp.ValueKind == JsonValueKind.String)
                        {
                            var gbChangeDateStr = gbChangeProp.GetString();
                            if (!string.IsNullOrEmpty(gbChangeDateStr) && lastUpdated != DateTime.MinValue)
                            {
                                if (DateTime.TryParse(gbChangeDateStr, out var gbDate))
                                {
                                    hasUpdate = gbDate > lastUpdated;
                                }
                            }
                        }
                        
                        var cleanName = GetCleanModName(dirName);
                        var isActive = IsModActive(dirName);
                        
                        // Build persistent lists
                        if (isNSFW) nsfwMods.Add(cleanName);
                        if (isBroken) brokenMods.Add(cleanName);
                        if (hasUpdate) outdatedMods.Add(cleanName);
                        
                        var modData = new ModData
                        {
                            Name = cleanName,
                            ImagePath = GetOptimalImagePath(modDir),
                            Directory = dirName,
                            IsActive = isActive,
                            Character = modCharacter,
                            Author = modAuthor,
                            Url = modUrl,
                            Category = categoryName,
                            LastChecked = lastChecked,
                            LastUpdated = lastUpdated,
                            IsNSFW = isNSFW,
                            IsBroken = isBroken,
                            HasUpdate = hasUpdate
                        };
                        
                        // Add mod to all mods data (Other category is already filtered out above)
                        _allModData.Add(modData);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error loading mod data from {modJsonPath}: {ex.Message}");
                    }
                }
            }
            
            // Save persistent lists to JSON files
            ModListManager.SaveNSFWModsList(nsfwMods);
            ModListManager.SaveBrokenModsList(brokenMods);
            ModListManager.SaveOutdatedModsList(outdatedMods);
            
            // Cache the lists in memory for fast access
            _cachedNSFWMods = new HashSet<string>(nsfwMods);
            _cachedBrokenMods = new HashSet<string>(brokenMods);
            _cachedOutdatedMods = new HashSet<string>(outdatedMods);
            
            // Cache favorites list once before sorting to avoid repeated calls
            var gameTag = SettingsManager.CurrentSelectedGame ?? "";
            var favoritesList = new HashSet<string>();
            foreach (var mod in _allModData)
            {
                if (SettingsManager.IsModFavorite(gameTag, mod.Name))
                {
                    favoritesList.Add(mod.Name);
                }
            }
            
            // Sort the lightweight data: favorites first, then active first (if enabled), then alphabetically
            if (SettingsManager.Current.ActiveModsToTopEnabled)
            {
                _allModData = _allModData
                    .OrderByDescending(m => favoritesList.Contains(m.Name))
                    .ThenByDescending(m => m.IsActive)
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                _allModData = _allModData
                    .OrderByDescending(m => favoritesList.Contains(m.Name))
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
                
            LogToGridLog($"Loaded {_allModData.Count} mod data entries (NSFW: {nsfwMods.Count}, Broken: {brokenMods.Count}, Outdated: {outdatedMods.Count})");
        }


        private void LoadVirtualizedModTiles()
        {
            // Calculate how many items we need to show initially
            var initialLoadCount = CalculateInitialLoadCount();
            
            var initialMods = new List<ModTile>();
            int loaded = 0;
            int lastProcessedIndex = 0;
            
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
            
            for (int i = 0; i < _allModData.Count && loaded < initialLoadCount; i++)
            {
                lastProcessedIndex = i + 1; // Track actual index in _allModData
                var modData = _allModData[i];
                
                // Filter broken mods if setting is enabled (fast HashSet lookup)
                if (hideBroken && brokenMods.Contains(modData.Name))
                {
                    continue;
                }
                
                // Filter NSFW mods if setting is enabled (fast HashSet lookup)
                if (hideNSFW && nsfwMods.Contains(modData.Name))
                {
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
                    ImageSource = null // Start with no image - lazy load when visible
                };
                initialMods.Add(modTile);
                loaded++;
            }
            
            _allMods = new ObservableCollection<ModTile>(initialMods);
            ModsGrid.ItemsSource = _allMods;
            UpdateEmptyState();
            
            // Track how many items from _allModData we've processed (actual index, not count of loaded items)
            _lastLoadedModDataIndex = lastProcessedIndex;
            
            LogToGridLog($"Created {initialMods.Count} initial ModTiles out of {_allModData.Count} total (Broken filter: {hideBroken}, Last index: {_lastLoadedModDataIndex})");
            
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
            // Check files in order of preference - optimized to minimize File.Exists calls
            var webpPath = Path.Combine(modDirectory, "minitile.webp");
            var jpegPath = Path.Combine(modDirectory, "minitile.jpg");
            
            // Check webp first, if exists return immediately
            if (File.Exists(webpPath))
                return webpPath;
            
            // Return jpeg path (may or may not exist, but we return it as default)
            return jpegPath;
        }

        private void LoadActiveModsOnly()
        {
            LogToGridLog("LoadActiveModsOnly() called");
            
            // Load all mod data first
            LoadAllModData();
            
            // Filter to show only active mods from _allModData
            var activeModTiles = new List<ModTile>();
            
            // Load persistent lists once for fast filtering
            bool hideBroken = SettingsManager.Current.HideBrokenMods;
            bool hideNSFW = SettingsManager.Current.HideNSFWMods;
            HashSet<string> nsfwMods = hideNSFW ? ModListManager.LoadNSFWModsList() : new HashSet<string>();
            HashSet<string> brokenMods = hideBroken ? ModListManager.LoadBrokenModsList() : new HashSet<string>();
            
            foreach (var modData in _allModData)
            {
                // Update active state first
                modData.IsActive = _activeMods.TryGetValue(GetCleanModName(modData.Directory), out var active) && active;
                
                if (modData.IsActive)
                {
                    // Filter broken mods if setting is enabled (fast HashSet lookup)
                    if (hideBroken && brokenMods.Contains(modData.Name))
                    {
                        continue;
                    }
                    
                    // Filter NSFW mods if setting is enabled (fast HashSet lookup)
                    if (hideNSFW && nsfwMods.Contains(modData.Name))
                    {
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
                        HasUpdate = CheckForUpdateLive(modData.Directory), // Live check without cache
                        IsVisible = true,
                        IsBroken = modData.IsBroken,
                        IsNSFW = modData.IsNSFW,
                        ImageSource = null // Start with no image - lazy load when visible
                    };
                    activeModTiles.Add(modTile);
                }
            }
            
            // Sort active mods alphabetically
            var sortedActiveMods = activeModTiles
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            
            LogToGridLog($"Found {sortedActiveMods.Count} active mods (Broken filter: {hideBroken})");
            ModsGrid.ItemsSource = sortedActiveMods;
            UpdateEmptyState();
            
            // Load visible images after setting new data source
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to let the grid update
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
        }

        private void LoadBrokenModsOnly()
        {
            LogToGridLog("LoadBrokenModsOnly() called");
            
            // Load all mod data first
            LoadAllModData();
            
            // Load broken mods list for fast filtering
            var brokenModsList = ModListManager.LoadBrokenModsList();
            
            // Filter to show only broken mods using the persistent list
            var brokenModTiles = new List<ModTile>();
            foreach (var modData in _allModData)
            {
                // Fast HashSet lookup instead of checking IsBroken flag
                if (brokenModsList.Contains(modData.Name))
                {
                    var modTile = new ModTile 
                    { 
                        Name = modData.Name, 
                        ImagePath = modData.ImagePath, 
                        Directory = modData.Directory, 
                        IsActive = _activeMods.TryGetValue(GetCleanModName(modData.Directory), out var active) && active,
                        Category = modData.Category,
                        Author = modData.Author,
                        Url = modData.Url,
                        LastChecked = modData.LastChecked,
                        LastUpdated = modData.LastUpdated,
                        HasUpdate = CheckForUpdateLive(modData.Directory), // Live check without cache
                        IsVisible = true,
                        IsBroken = true, // We know it's broken from the list
                        IsNSFW = modData.IsNSFW,
                        ImageSource = null // Start with no image - lazy load when visible
                    };
                    brokenModTiles.Add(modTile);
                }
            }
            
            // Sort broken mods alphabetically
            var sortedBrokenMods = brokenModTiles
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            
            LogToGridLog($"Found {sortedBrokenMods.Count} broken mods");
            ModsGrid.ItemsSource = sortedBrokenMods;
            UpdateEmptyState();
            
            // Load visible images after setting new data source
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to let the grid update
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
        }

        private void LoadOutdatedModsOnly()
        {
            LogToGridLog("LoadOutdatedModsOnly() called");
            
            // Load all mod data first
            LoadAllModData();
            
            // Filter to show only outdated mods from _allModData
            var outdatedModTiles = new List<ModTile>();
            
            // Load persistent lists once for fast filtering
            bool hideBroken = SettingsManager.Current.HideBrokenMods;
            bool hideNSFW = SettingsManager.Current.HideNSFWMods;
            HashSet<string> nsfwMods = hideNSFW ? ModListManager.LoadNSFWModsList() : new HashSet<string>();
            HashSet<string> brokenMods = hideBroken ? ModListManager.LoadBrokenModsList() : new HashSet<string>();
            
            foreach (var modData in _allModData)
            {
                // Update active state
                modData.IsActive = _activeMods.TryGetValue(GetCleanModName(modData.Directory), out var active) && active;
                
                // Check if mod has update (live check)
                bool hasUpdate = CheckForUpdateLive(modData.Directory);
                
                if (hasUpdate)
                {
                    // Filter broken mods if setting is enabled (fast HashSet lookup)
                    if (hideBroken && brokenMods.Contains(modData.Name))
                    {
                        continue;
                    }
                    
                    // Filter NSFW mods if setting is enabled (fast HashSet lookup)
                    if (hideNSFW && nsfwMods.Contains(modData.Name))
                    {
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
                        HasUpdate = hasUpdate,
                        IsVisible = true,
                        IsBroken = modData.IsBroken,
                        IsNSFW = modData.IsNSFW,
                        ImageSource = null // Start with no image - lazy load when visible
                    };
                    outdatedModTiles.Add(modTile);
                }
            }
            
            // Sort outdated mods alphabetically
            var sortedOutdatedMods = outdatedModTiles
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            
            LogToGridLog($"Found {sortedOutdatedMods.Count} outdated mods (Broken filter: {hideBroken})");
            ModsGrid.ItemsSource = sortedOutdatedMods;
            UpdateEmptyState();
            
            // Load visible images after setting new data source
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to let the grid update
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
        }

        // No cache system - direct file reading only
    }
}