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
            Logger.LogDebug($"LoadCategories() called - CurrentViewMode: {CurrentViewMode}");
            
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
            
            var gameModsPath = AppConstants.GameConfig.GetModsPath(gameTag);
            string modsPath = PathManager.GetAbsolutePath(gameModsPath);
            
            if (!Directory.Exists(modsPath)) return;
            
            var categories = new List<ModTile>();
            
            foreach (var categoryDir in Directory.GetDirectories(modsPath))
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
            
            // Look for category mini tile image
            string categoryMini = Path.Combine(categoryPath, "catmini.jpg");
            LogToGridLog($"Looking for category mini tile: {categoryMini}");
            return categoryMini;
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
            var modsPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
            
            if (!Directory.Exists(modsPath)) return;
            
            _allModData.Clear();
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
                            
                            // Debug logging for broken mods
                            if (isBroken)
                            {
                                Logger.LogInfo($"LoadCategoryModData: FOUND BROKEN MOD in JSON: {name} (Directory: {dirName}) - Category: {category}");
                            }
                            
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
            var mods = new List<ModTile>();
            foreach (var modData in _allModData)
            {
                // Filter NSFW mods if setting is enabled
                if (modData.IsNSFW && SettingsManager.Current.BlurNSFWThumbnails)
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
                    IsFavorite = SettingsManager.IsModFavorite(SettingsManager.CurrentSelectedGame ?? "", modData.Name), // Load favorite status
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
            Logger.LogDebug($"LoadAllMods() called - CurrentViewMode: {CurrentViewMode}");
            Logger.LogDebug($"LoadAllMods() stack trace: {Environment.StackTrace}");
            
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
            
            // Process category directories (1st level) and mod directories (2nd level)
            foreach (var categoryDir in Directory.GetDirectories(modsPath))
            {
                if (!Directory.Exists(categoryDir)) continue;
                
                var categoryName = Path.GetFileName(categoryDir);
                
                // Skip "Other" category in All Mods view - it has its own menu item
                if (categoryName.Equals("Other", StringComparison.OrdinalIgnoreCase))
                    continue;
                
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
                        
                        var cleanName = GetCleanModName(dirName);
                        var isActive = IsModActive(dirName);
                        // categoryName is already declared in outer scope
                        
                        // Debug logging for broken mods
                        if (isBroken)
                        {
                            Logger.LogInfo($"LoadAllModData: FOUND BROKEN MOD in JSON: {cleanName} (Directory: {dirName}) - Category: {categoryName}");
                        }
                        
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
                            IsBroken = isBroken
                        };
                        
                        Logger.LogInfo($"Loaded mod: {cleanName} - IsBroken: {isBroken}, Category: {categoryName}");
                        
                        // Add mod to all mods data (Other category is already filtered out above)
                        _allModData.Add(modData);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error loading mod data from {modJsonPath}: {ex.Message}");
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
                
            LogToGridLog($"Loaded {_allModData.Count} mod data entries");
        }

        private ModData? GetCachedModData(string dir, string modJsonPath)
        {
            var dirName = Path.GetFileName(dir);
            var cleanName = GetCleanModName(dirName);
            
            Logger.LogInfo($"GetCachedModData called for: {cleanName} (Directory: {dirName})");
            
            // Always read from file - no cache for modBroken
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
                
                // Debug logging for broken mods
                if (isBroken)
                {
                    Logger.LogInfo($"GetCachedModData: Found BROKEN mod in JSON: {cleanName} (Directory: {dirName}) - IsBroken will be set to TRUE");
                }
                else
                {
                    Logger.LogInfo($"GetCachedModData: Mod {cleanName} (Directory: {dirName}) - IsBroken will be set to FALSE");
                }
                
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
                
                string previewPath = GetOptimalImagePath(dir);
                var isActive = IsModActive(dirName);
                
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
                    Name = cleanName,  // Display name without DISABLED_ prefix
                    ImagePath = previewPath, 
                    Directory = dirName,  // Use actual folder name for file operations
                    IsActive = isActive,
                    Character = modCharacter,
                    Author = modAuthor,
                    Url = modUrl,
                    Category = categoryName,
                    LastChecked = lastChecked,
                    LastUpdated = lastUpdated,
                    IsNSFW = isNSFW,
                    IsBroken = isBroken
                };
                
                return modData;
            }
            catch
            {
                return null;
            }
        }

        private void LoadVirtualizedModTiles()
        {
            // Calculate how many items we need to show initially
            var initialLoadCount = CalculateInitialLoadCount();
            
            var initialMods = new List<ModTile>();
            int loaded = 0;
            bool hideNSFW = SettingsManager.Current.BlurNSFWThumbnails;
            
            for (int i = 0; i < _allModData.Count && loaded < initialLoadCount; i++)
            {
                var modData = _allModData[i];
                
                // Filter NSFW mods if setting is enabled
                if (modData.IsNSFW && hideNSFW)
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
                    IsFavorite = SettingsManager.IsModFavorite(SettingsManager.CurrentSelectedGame ?? "", modData.Name), // Load favorite status
                    ImageSource = null // Start with no image - lazy load when visible
                };
                
                // Debug logging for broken mods
                if (modData.IsBroken)
                {
                    Logger.LogInfo($"LoadVirtualizedModTiles: Creating ModTile for BROKEN mod: {modData.Name} (Directory: {modData.Directory}) - IsBroken: {modTile.IsBroken}");
                }
                
                initialMods.Add(modTile);
                loaded++;
            }
            
            _allMods = new ObservableCollection<ModTile>(initialMods);
            ModsGrid.ItemsSource = _allMods;
            LogToGridLog($"Created {initialMods.Count} initial ModTiles out of {_allModData.Count} total (NSFW filter: {hideNSFW})");
            
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
            // Check files in order of preference - cache path strings to avoid repeated string operations
            var webpPath = Path.Combine(modDirectory, "minitile.webp");
            if (File.Exists(webpPath))
                return webpPath;
            
            var jpegPath = Path.Combine(modDirectory, "minitile.jpg");
            if (File.Exists(jpegPath))
                return jpegPath;
            
            // Return jpeg path as default (may not exist)
            return jpegPath;
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
                modData.IsActive = _activeMods.TryGetValue(GetCleanModName(modData.Directory), out var active) && active;
                
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
                        HasUpdate = CheckForUpdateLive(modData.Directory), // Live check without cache
                        IsVisible = true,
                        IsBroken = modData.IsBroken,
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

        private void LoadBrokenModsOnly()
        {
            LogToGridLog("LoadBrokenModsOnly() called");
            
            // For broken mods, we need to load from ALL categories including "Other"
            LoadActiveModData(); // This now loads all categories including "Other"
            
            // Filter to show only broken mods from _allModData
            var brokenModTiles = new List<ModTile>();
            foreach (var modData in _allModData)
            {
                if (modData.IsBroken)
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
                        IsBroken = modData.IsBroken,
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
            
            // Load from ALL categories
            LoadActiveModData();
            
            // Filter to show only outdated mods from _allModData
            var outdatedModTiles = new List<ModTile>();
            foreach (var modData in _allModData)
            {
                // Update active state
                modData.IsActive = _activeMods.TryGetValue(GetCleanModName(modData.Directory), out var active) && active;
                
                // Check if mod has update (live check)
                bool hasUpdate = CheckForUpdateLive(modData.Directory);
                
                if (hasUpdate)
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
                        HasUpdate = hasUpdate,
                        IsVisible = true,
                        IsBroken = modData.IsBroken,
                        ImageSource = null // Start with no image - lazy load when visible
                    };
                    outdatedModTiles.Add(modTile);
                }
            }
            
            // Sort outdated mods alphabetically
            var sortedOutdatedMods = outdatedModTiles
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            
            LogToGridLog($"Found {sortedOutdatedMods.Count} outdated mods");
            ModsGrid.ItemsSource = sortedOutdatedMods;
            
            // Load visible images after setting new data source
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to let the grid update
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
        }

        private void LoadActiveModData()
        {
            var modsPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
            
            if (!Directory.Exists(modsPath)) return;
            
            _allModData.Clear();
            var cacheHits = 0;
            var cacheMisses = 0;
            
            // Process category directories excluding "Other" for active mods view (same as All Mods)
            foreach (var categoryDir in Directory.GetDirectories(modsPath))
            {
                if (!Directory.Exists(categoryDir)) continue;
                
                var categoryName = Path.GetFileName(categoryDir);
                
                // Skip "Other" category in Active Mods view - it has its own menu item
                if (categoryName.Equals("Other", StringComparison.OrdinalIgnoreCase))
                    continue;
                
                foreach (var modDir in Directory.GetDirectories(categoryDir))
                {
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (!File.Exists(modJsonPath)) continue;
                    
                    var dirName = Path.GetFileName(modDir);
                    var modData = GetCachedModData(modDir, modJsonPath);
                    
                    if (modData != null)
                    {
                        // Update active state (this can change without file modification)
                        modData.IsActive = IsModActive(dirName);
                        
                        // Add category information from folder structure
                        modData.Category = Path.GetFileName(categoryDir);
                        
                        // Add mod to active mods data (Other category is already filtered out above)
                        _allModData.Add(modData);
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