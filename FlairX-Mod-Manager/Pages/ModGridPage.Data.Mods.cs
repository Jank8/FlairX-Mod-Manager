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
    public sealed partial class ModGridPage
    {

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
                    Category = modData.Category ?? "Other", // Prevent null warning
                    Author = modData.Author,
                    Url = modData.Url,
                    LastChecked = modData.LastChecked,
                    LastUpdated = modData.LastUpdated,
                    HasUpdate = CheckForUpdateLive(modData), // Live check without cache
                    IsVisible = true,
                    IsBroken = modData.IsBroken,
                    IsNSFW = modData.IsNSFW,
                    IsFavorite = SettingsManager.IsModFavorite(SettingsManager.CurrentSelectedGame ?? "", modData.Category ?? "Other", modData.Name), // Load favorite status
                    ImageSource = null // Start with no image - lazy load when visible
                };
                
                mods.Add(modTile);
            }
                
            LogToGridLog($"Loaded {mods.Count} mods for category: {category}");
            
            // Update _allMods collection instead of creating new list
            DispatcherQueue.TryEnqueue(() =>
            {
                _allMods.Clear();
                foreach (var mod in mods)
                {
                    _allMods.Add(mod);
                }
                
                // Ensure ItemsSource is set
                if (ModsGrid.ItemsSource != _allMods)
                {
                    ModsGrid.ItemsSource = _allMods;
                }
                
                UpdateEmptyState();
                
                // Load visible images after setting new data source
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // Small delay to let the grid update
                    DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
                });
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
            
            // Check filters and use appropriate list from ModListManager
            bool hideBroken = SettingsManager.Current.HideBrokenMods;
            bool hideNSFW = SettingsManager.Current.HideNSFWMods;
            
            Logger.LogInfo($"LoadAllMods: Filters - HideBroken: {hideBroken}, HideNSFW: {hideNSFW}");
            
            List<ModListManager.ModInfo> filteredMods;
            
            // Use pre-filtered lists when possible for better performance
            if (hideBroken && hideNSFW)
            {
                // Need to filter both - get all and filter manually
                var allMods = ModListManager.GetAllMods();
                filteredMods = allMods.Where(m => !m.IsBroken && !m.IsNSFW).ToList();
            }
            else if (hideBroken)
            {
                // Filter only broken - get all and filter
                var allMods = ModListManager.GetAllMods();
                filteredMods = allMods.Where(m => !m.IsBroken).ToList();
            }
            else if (hideNSFW)
            {
                // Filter only NSFW - get all and filter
                var allMods = ModListManager.GetAllMods();
                filteredMods = allMods.Where(m => !m.IsNSFW).ToList();
            }
            else
            {
                // No filters - use all mods
                filteredMods = ModListManager.GetAllMods();
            }
            
            Logger.LogInfo($"LoadAllMods: Got {filteredMods.Count} mods after filtering");
            
            // Sort: favorites first, then active first (if enabled), then alphabetically
            if (SettingsManager.Current.ActiveModsToTopEnabled)
            {
                filteredMods = filteredMods
                    .OrderByDescending(m => m.IsFavorite)
                    .ThenByDescending(m => m.IsActive)
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                filteredMods = filteredMods
                    .OrderByDescending(m => m.IsFavorite)
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            
            // Convert to lightweight ModData for lazy loading
            _allModData.Clear();
            _lastLoadedModDataIndex = 0;
            
            foreach (var modInfo in filteredMods)
            {
                _allModData.Add(new ModData
                {
                    Name = modInfo.Name,
                    Directory = modInfo.Directory,
                    ImagePath = modInfo.ImagePath,
                    IsActive = modInfo.IsActive,
                    Category = modInfo.Category,
                    Author = modInfo.Author,
                    Url = modInfo.Url,
                    LastChecked = modInfo.LastChecked,
                    LastUpdated = modInfo.LastUpdated,
                    HasUpdate = modInfo.HasUpdate,
                    IsNSFW = modInfo.IsNSFW,
                    IsBroken = modInfo.IsBroken,
                    IsFavorite = modInfo.IsFavorite,
                    Character = modInfo.Character
                });
            }
            
            // Load only first batch of tiles (lazy loading)
            const int initialLoadCount = 30;
            var initialTiles = new List<ModTile>();
            
            for (int i = 0; i < Math.Min(initialLoadCount, _allModData.Count); i++)
            {
                var modData = _allModData[i];
                initialTiles.Add(new ModTile
                {
                    Name = modData.Name,
                    Directory = modData.Directory,
                    ImagePath = modData.ImagePath,
                    IsActive = modData.IsActive,
                    Category = modData.Category,
                    Author = modData.Author,
                    Url = modData.Url,
                    LastChecked = modData.LastChecked,
                    LastUpdated = modData.LastUpdated,
                    HasUpdate = modData.HasUpdate,
                    IsVisible = true,
                    IsBroken = modData.IsBroken,
                    IsNSFW = modData.IsNSFW,
                    IsFavorite = modData.IsFavorite,
                    ImageSource = null // Lazy load when visible
                });
                _lastLoadedModDataIndex = i + 1;
            }
            
            // Update UI
            DispatcherQueue.TryEnqueue(() =>
            {
                _allMods.Clear();
                foreach (var mod in initialTiles)
                {
                    _allMods.Add(mod);
                }
                
                UpdateEmptyState();
            });
            
            LogToGridLog($"Loaded {initialTiles.Count} initial tiles from {_allModData.Count} total mods");
            
            // Load visible images
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
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
                        if (isNSFW) nsfwMods.Add($"{categoryName}|{cleanName}"); // Include category
                        if (isBroken) brokenMods.Add($"{categoryName}|{cleanName}"); // Include category
                        if (hasUpdate) outdatedMods.Add($"{categoryName}|{cleanName}"); // Include category
                        
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
                if (SettingsManager.IsModFavorite(gameTag, mod.Category ?? "Other", mod.Name))
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
                if (SettingsManager.IsModFavorite(gameTag, mod.Category ?? "Other", mod.Name))
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
            
            // Clear and repopulate existing collection instead of creating new instance
            DispatcherQueue.TryEnqueue(() =>
            {
                _allMods.Clear();
                foreach (var mod in initialMods)
                {
                    _allMods.Add(mod);
                }
                
                UpdateEmptyState();
            });
            
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
            var webpPath = Path.Combine(modDirectory, "minitile.webp");
            var jpegPath = Path.Combine(modDirectory, "minitile.jpg");
            
            if (File.Exists(webpPath))
                return webpPath;
            
            if (File.Exists(jpegPath))
                return jpegPath;
            
            return string.Empty;
        }
    }
}
