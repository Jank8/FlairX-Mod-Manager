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
                var cacheKey = $"{modData.Category ?? "Other"}|{modData.Name}";
                
                // Filter broken mods if setting is enabled AND cache exists
                if (hideBroken && brokenMods != null && brokenMods.Contains(cacheKey))
                    return false;
                
                // Filter NSFW mods if setting is enabled AND cache exists
                if (hideNSFW && nsfwMods != null && nsfwMods.Contains(cacheKey))
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
                var modCount = Directory.GetDirectories(categoryDir).Length;
                
                // Skip empty categories if setting is enabled
                if (SettingsManager.Current.HideEmptyCategories && modCount == 0)
                    continue;
                
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
                
                // ItemsSource is already set in constructor, no need to set again
                UpdateEmptyState();
                
                var langDict = SharedUtilities.LoadLanguageDictionary();
                CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "All_Categories");
                
                // Hide back button and folder button in categories view
                CategoryBackButton.Visibility = Visibility.Collapsed;
                CategoryOpenFolderButton.Visibility = Visibility.Collapsed;
                
                // Update context flyout to disable context menu for category tiles
                UpdateContextFlyout();
                
                // Update grid item sizes to reflect current tile format
                UpdateGridItemSizes();
                
                LogToGridLog($"Loaded {categories.Count} categories");
            });
        }

        private string GetCategoryMiniTilePath(string categoryName)
        {
            var gameTag = SettingsManager.CurrentSelectedGame;
            if (string.IsNullOrEmpty(gameTag)) return "";
            
            var gameModsPath = AppConstants.GameConfig.GetModsPath(gameTag);
            string categoryPath = PathManager.GetAbsolutePath(Path.Combine(gameModsPath, categoryName));
            
            // Determine which format to use based on user setting
            bool useWide = SettingsManager.Current.UseWideTileFormat;
            
            // Try wide format first if enabled
            if (useWide)
            {
                string categoryMiniWideWebp = Path.Combine(categoryPath, "catmini-wide.webp");
                string categoryMiniWideJpg = Path.Combine(categoryPath, "catmini-wide.jpg");
                
                if (File.Exists(categoryMiniWideWebp))
                {
                    LogToGridLog($"Found category mini tile (wide): {categoryMiniWideWebp}");
                    return categoryMiniWideWebp;
                }
                else if (File.Exists(categoryMiniWideJpg))
                {
                    LogToGridLog($"Found category mini tile (wide): {categoryMiniWideJpg}");
                    return categoryMiniWideJpg;
                }
            }
            
            // Fall back to classic format
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
            
            // Get mods for this category from ModListManager (fast - no file I/O)
            var categoryMods = ModListManager.GetModsByCategory(category);
            
            Logger.LogInfo($"LoadModsByCategory: Got {categoryMods.Count} mods for category '{category}'");
            
            // Apply filters based on settings
            bool hideBroken = SettingsManager.Current.HideBrokenMods;
            bool hideNSFW = SettingsManager.Current.HideNSFWMods;
            
            Logger.LogInfo($"LoadModsByCategory: Filters - HideBroken: {hideBroken}, HideNSFW: {hideNSFW}");
            
            var filteredMods = categoryMods.Where(mod =>
            {
                if (hideBroken && mod.IsBroken) return false;
                if (hideNSFW && mod.IsNSFW) return false;
                return true;
            }).ToList();
            
            Logger.LogInfo($"LoadModsByCategory: After filtering: {filteredMods.Count} mods");
            
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
            
            // Convert to ModTiles
            var modTiles = filteredMods.Select(modInfo => new ModTile
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
                IsVisible = true,
                IsBroken = modInfo.IsBroken,
                IsNSFW = modInfo.IsNSFW,
                IsFavorite = modInfo.IsFavorite,
                ImageSource = null // Lazy load when visible
            }).ToList();
            
            // Update UI
            DispatcherQueue.TryEnqueue(() =>
            {
                _allMods.Clear();
                foreach (var mod in modTiles)
                {
                    _allMods.Add(mod);
                }
                
                UpdateEmptyState();
                
                // Load visible images after UI is updated
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
                });
            });
            
            LogToGridLog($"Loaded {modTiles.Count} mods for category '{category}' from ModListManager");
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
                    .OrderByDescending(m => SettingsManager.IsModFavorite(gameTag, m.Category ?? "Other", m.Name))
                    .ThenByDescending(m => m.IsActive)
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                var gameTag = SettingsManager.CurrentSelectedGame ?? "";
                _allModData = _allModData
                    .OrderByDescending(m => SettingsManager.IsModFavorite(gameTag, m.Category ?? "Other", m.Name))
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            
            LogToGridLog($"Loaded {_allModData.Count} mod data entries for category: {category}");
        }
    }
}
