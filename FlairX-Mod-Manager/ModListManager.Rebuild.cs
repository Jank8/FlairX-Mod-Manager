using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager
{
    public static partial class ModListManager
    {

        private static readonly object _listLock = new object();
        
        // In-memory cache for fast access
        private static List<ModInfo>? _cachedMasterList = null;
        private static HashSet<string>? _cachedActiveList = null;
        private static HashSet<string>? _cachedNSFWList = null;
        private static HashSet<string>? _cachedBrokenList = null;
        private static HashSet<string>? _cachedOutdatedList = null;
        private static DateTime _lastCacheUpdate = DateTime.MinValue;

        // Serializer options are immutable after first use; allocating them per call showed up
        // on the rebuild path, which writes one mod.json per mod.
        private static readonly JsonSerializerOptions IndentedWriteOptions = new() { WriteIndented = true };

        // Image paths are absolute and depend on the mods root and the tile format. They are
        // recomputed only when one of those changes, not on every GetAllMods() call.
        private static string _imagePathBaseDirectory = string.Empty;
        private static bool _imagePathUseWideFormat;


        /// <summary>
        /// Rebuild all lists by scanning mod directories and mod.json files
        /// Call this on startup or when mods are added/removed
        /// </summary>
        public static void RebuildAllLists()
        {
            Logger.LogInfo("ModListManager: Rebuilding all mod lists...");
            
            var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
            if (!Directory.Exists(modsPath))
            {
                Logger.LogWarning($"ModListManager: Mods directory not found: {modsPath}");
                return;
            }

            var masterList = new List<ModInfo>();
            var activeList = new List<string>();
            var nsfwList = new List<string>();
            var brokenList = new List<string>();
            var outdatedList = new List<string>();
            
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            
            int totalModDirs = 0;
            int processedMods = 0;
            int skippedMods = 0;

            // Scan all categories and mods
            foreach (var categoryDir in Directory.GetDirectories(modsPath))
            {
                var categoryName = Path.GetFileName(categoryDir);
                if (string.IsNullOrEmpty(categoryName))
                {
                    Logger.LogWarning($"ModListManager: Skipping category with empty name: {categoryDir}");
                    continue;
                }
                
                Logger.LogDebug($"ModListManager: Scanning category: {categoryName}");

                foreach (var modDir in Directory.GetDirectories(categoryDir))
                {
                    totalModDirs++;
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    
                    // Create mod.json if it doesn't exist
                    if (!File.Exists(modJsonPath))
                    {
                        try
                        {
                            var defaultModJson = new
                            {
                                author = "",
                                url = "",
                                character = "other",
                                isNSFW = false,
                                modBroken = false,
                                statusKeeperSync = true,
                                dateChecked = DateTime.UtcNow.ToString("o"),
                                dateUpdated = DateTime.UtcNow.ToString("o")
                            };
                            
                            var json = JsonSerializer.Serialize(defaultModJson, IndentedWriteOptions);
                            Services.FileAccessQueue.WriteAllText(modJsonPath, json);
                            Logger.LogInfo($"ModListManager: Created missing mod.json for {modDir}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"ModListManager: Failed to create mod.json for {modDir}", ex);
                            skippedMods++;
                            continue; // Skip this mod if we can't create mod.json
                        }
                    }

                    try
                    {
                        var dirName = Path.GetFileName(modDir);
                        var cleanName = GetCleanModName(dirName);
                        var isActive = !dirName.StartsWith("DISABLED", StringComparison.OrdinalIgnoreCase);

                        // Parse mod.json
                        var json = Services.FileAccessQueue.ReadAllText(modJsonPath);
                        
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        var author = root.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? "" : "";
                        var url = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                        var character = root.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "other" : "other";
                        var isNSFW = root.TryGetProperty("isNSFW", out var nsfwProp) && nsfwProp.ValueKind == JsonValueKind.True;
                        var isBroken = root.TryGetProperty("modBroken", out var brokenProp) && brokenProp.ValueKind == JsonValueKind.True;
                        var statusKeeperSync = root.TryGetProperty("statusKeeperSync", out var syncProp) && syncProp.ValueKind != JsonValueKind.False;
                        
                        // Parse StatusKeeper sync data
                        var syncMethod = "";
                        var namespaces = new List<NamespaceInfo>();
                        
                        if (statusKeeperSync && root.TryGetProperty("syncMethod", out var syncMethodProp))
                        {
                            syncMethod = syncMethodProp.GetString() ?? "";
                            
                            // Parse namespaces if syncMethod is "namespace"
                            if (syncMethod == "namespace" && root.TryGetProperty("namespaces", out var namespacesProp) && 
                                namespacesProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var namespaceItem in namespacesProp.EnumerateArray())
                                {
                                    if (namespaceItem.TryGetProperty("namespace", out var namespaceProp) &&
                                        namespaceItem.TryGetProperty("iniFiles", out var iniFilesProp) &&
                                        iniFilesProp.ValueKind == JsonValueKind.Array)
                                    {
                                        var namespaceInfo = new NamespaceInfo
                                        {
                                            Namespace = namespaceProp.GetString() ?? "",
                                            IniFiles = new List<string>()
                                        };
                                        
                                        foreach (var iniFile in iniFilesProp.EnumerateArray())
                                        {
                                            var iniFileName = iniFile.GetString();
                                            if (!string.IsNullOrEmpty(iniFileName))
                                            {
                                                namespaceInfo.IniFiles.Add(iniFileName);
                                            }
                                        }
                                        
                                        namespaces.Add(namespaceInfo);
                                    }
                                }
                            }
                        }

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

                        // Check for updates
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

                        var isFavorite = SettingsManager.IsModFavorite(gameTag, categoryName, cleanName);
                        var imagePath = GetOptimalImagePath(modDir);

                        var modInfo = new ModInfo
                        {
                            Name = cleanName,
                            Directory = dirName,
                            Category = categoryName,
                            Character = character,
                            Author = author,
                            Url = url,
                            ImagePath = imagePath,
                            LastChecked = lastChecked,
                            LastUpdated = lastUpdated,
                            IsActive = isActive,
                            IsNSFW = isNSFW,
                            IsBroken = isBroken,
                            HasUpdate = hasUpdate,
                            IsFavorite = isFavorite,
                            StatusKeeperSync = statusKeeperSync,
                            SyncMethod = syncMethod,
                            Namespaces = namespaces
                        };

                        masterList.Add(modInfo);
                        processedMods++;

                        // Build filter lists
                        if (isActive) activeList.Add($"{categoryName}|{cleanName}"); // Include category
                        if (isNSFW) nsfwList.Add($"{categoryName}|{cleanName}"); // Include category
                        if (isBroken) brokenList.Add($"{categoryName}|{cleanName}"); // Include category
                        if (hasUpdate) outdatedList.Add($"{categoryName}|{cleanName}"); // Include category
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"ModListManager: Failed to process mod in {modDir}", ex);
                        
                        // Extra logging for specific mod
                        var dirName = Path.GetFileName(modDir);
                        if (dirName != null && dirName.Contains("Bottom Heavy", StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.LogError($"ModListManager: CRITICAL - Failed to process 'Bottom Heavy': {ex.Message}");
                        }
                        
                        skippedMods++;
                    }
                }
            }

            // Save all lists
            SaveMasterList(masterList);
            SaveActiveModsList(activeList);
            SaveNSFWModsList(nsfwList);
            SaveBrokenModsList(brokenList);
            SaveOutdatedModsList(outdatedList);

            // Update cache
            lock (_listLock)
            {
                _cachedMasterList = masterList;
                _cachedActiveList = new HashSet<string>(activeList);
                _cachedNSFWList = new HashSet<string>(nsfwList);
                _cachedBrokenList = new HashSet<string>(brokenList);
                _cachedOutdatedList = new HashSet<string>(outdatedList);
                _lastCacheUpdate = DateTime.UtcNow;
                _imagePathBaseDirectory = SettingsManager.GetCurrentXXMIModsDirectory();
                _imagePathUseWideFormat = SettingsManager.Current.UseWideTileFormat;
            }

            Logger.LogInfo($"ModListManager: Rebuilt lists - Total dirs: {totalModDirs}, Processed: {processedMods}, Skipped: {skippedMods}");
            Logger.LogInfo($"ModListManager: Lists - Master: {masterList.Count}, Active: {activeList.Count}, NSFW: {nsfwList.Count}, Broken: {brokenList.Count}, Outdated: {outdatedList.Count}");
        }

        /// <summary>
        /// Get all mods from master list (cached)
        /// </summary>
        public static List<ModInfo> GetAllMods()
        {
            lock (_listLock)
            {
                if (_cachedMasterList == null)
                {
                    _cachedMasterList = LoadMasterList();
                }

                EnsureImagePathsCurrent();
                return new List<ModInfo>(_cachedMasterList);
            }
        }

        /// <summary>
        /// Recomputes absolute image paths only when the mods root or the tile format changed.
        /// This used to run up to four File.Exists checks per mod on every GetAllMods() call,
        /// and GetAllMods() backs every category/filter query in the app.
        /// </summary>
        private static void EnsureImagePathsCurrent()
        {
            if (_cachedMasterList == null)
                return;

            var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
            var useWide = SettingsManager.Current.UseWideTileFormat;

            if (string.Equals(_imagePathBaseDirectory, modsPath, StringComparison.OrdinalIgnoreCase) &&
                _imagePathUseWideFormat == useWide)
            {
                return;
            }

            foreach (var mod in _cachedMasterList)
            {
                if (string.IsNullOrEmpty(mod.Directory) || string.IsNullOrEmpty(mod.Category))
                    continue;

                mod.ImagePath = GetOptimalImagePath(Path.Combine(modsPath, mod.Category, mod.Directory));
            }

            _imagePathBaseDirectory = modsPath;
            _imagePathUseWideFormat = useWide;
        }

        /// <summary>
        /// Get mods filtered by category
        /// </summary>
        public static List<ModInfo> GetModsByCategory(string category)
        {
            var allMods = GetAllMods();
            return allMods.Where(m => string.Equals(m.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// Get active mods only
        /// </summary>
        public static List<ModInfo> GetActiveMods()
        {
            var allMods = GetAllMods();
            return allMods.Where(m => m.IsActive).ToList();
        }

        /// <summary>
        /// Get broken mods only
        /// </summary>
        public static List<ModInfo> GetBrokenMods()
        {
            var allMods = GetAllMods();
            return allMods.Where(m => m.IsBroken).ToList();
        }

        /// <summary>
        /// Get outdated mods only
        /// </summary>
        public static List<ModInfo> GetOutdatedMods()
        {
            var allMods = GetAllMods();
            return allMods.Where(m => m.HasUpdate).ToList();
        }

        /// <summary>
        /// Get NSFW mods only
        /// </summary>
        public static List<ModInfo> GetNSFWMods()
        {
            var allMods = GetAllMods();
            return allMods.Where(m => m.IsNSFW).ToList();
        }

        /// <summary>
        /// Check if mod is in NSFW list (fast HashSet lookup)
        /// </summary>
        public static bool IsModNSFW(string categoryName, string modName)
        {
            lock (_listLock)
            {
                if (_cachedNSFWList == null)
                {
                    _cachedNSFWList = LoadNSFWModsList();
                }
                return _cachedNSFWList.Contains($"{categoryName}|{modName}");
            }
        }

        /// <summary>
        /// Check if mod is in Broken list (fast HashSet lookup)
        /// </summary>
        public static bool IsModBroken(string categoryName, string modName)
        {
            lock (_listLock)
            {
                if (_cachedBrokenList == null)
                {
                    _cachedBrokenList = LoadBrokenModsList();
                }
                return _cachedBrokenList.Contains($"{categoryName}|{modName}");
            }
        }

        /// <summary>
        /// Check if mod is in Outdated list (fast HashSet lookup)
        /// </summary>
        public static bool IsModOutdated(string categoryName, string modName)
        {
            lock (_listLock)
            {
                if (_cachedOutdatedList == null)
                {
                    _cachedOutdatedList = LoadOutdatedModsList();
                }
                return _cachedOutdatedList.Contains($"{categoryName}|{modName}");
            }
        }

        /// <summary>
        /// Check if mod is active (fast HashSet lookup)
        /// </summary>
        public static bool IsModActive(string categoryName, string modName)
        {
            lock (_listLock)
            {
                if (_cachedActiveList == null)
                {
                    _cachedActiveList = LoadActiveModsList();
                }
                return _cachedActiveList.Contains($"{categoryName}|{modName}");
            }
        }

        /// <summary>
        /// Get mods with StatusKeeper sync enabled
        /// </summary>
        public static List<ModInfo> GetStatusKeeperSyncMods()
        {
            var allMods = GetAllMods();
            return allMods.Where(m => m.StatusKeeperSync).ToList();
        }

        /// <summary>
        /// Invalidate cache - forces reload on next access
        /// </summary>
        public static void InvalidateCache()
        {
            lock (_listLock)
            {
                _cachedMasterList = null;
                _cachedActiveList = null;
                _cachedNSFWList = null;
                _cachedBrokenList = null;
                _cachedOutdatedList = null;
                _lastCacheUpdate = DateTime.MinValue;
            }
            Logger.LogInfo("ModListManager: Cache invalidated");
        }

        private static string GetOptimalImagePath(string modDirectory)
        {
            // Determine which format to use based on user setting
            bool useWide = SettingsManager.Current.UseWideTileFormat;
            
            // Try wide format first if enabled
            if (useWide)
            {
                var wideWebpPath = Path.Combine(modDirectory, "minitile-wide.webp");
                var wideJpegPath = Path.Combine(modDirectory, "minitile-wide.jpg");
                
                if (File.Exists(wideWebpPath))
                    return wideWebpPath;
                
                if (File.Exists(wideJpegPath))
                    return wideJpegPath;
            }
            
            // Fall back to classic format
            var webpPath = Path.Combine(modDirectory, "minitile.webp");
            var jpegPath = Path.Combine(modDirectory, "minitile.jpg");
            
            if (File.Exists(webpPath))
                return webpPath;
            
            if (File.Exists(jpegPath))
                return jpegPath;
            
            // Return empty string if no minitile exists
            return string.Empty;
        }

        private static void SaveMasterList(List<ModInfo> mods)
        {
            lock (_listLock)
            {
                try
                {
                    var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
                    var filename = AppConstants.GameConfig.GetMasterModsFilename(gameTag);
                    var settingsPath = PathManager.GetSettingsPath(filename);
                    
                    // Convert ImagePath to relative filename before saving
                    var modsToSave = mods.Select(m => new ModInfo
                    {
                        Name = m.Name,
                        Directory = m.Directory,
                        Category = m.Category,
                        Character = m.Character,
                        Author = m.Author,
                        Url = m.Url,
                        ImagePath = string.IsNullOrEmpty(m.ImagePath) ? string.Empty : Path.GetFileName(m.ImagePath),
                        LastChecked = m.LastChecked,
                        LastUpdated = m.LastUpdated,
                        IsActive = m.IsActive,
                        IsNSFW = m.IsNSFW,
                        IsBroken = m.IsBroken,
                        HasUpdate = m.HasUpdate,
                        IsFavorite = m.IsFavorite,
                        StatusKeeperSync = m.StatusKeeperSync,
                        SyncMethod = m.SyncMethod,
                        Namespaces = m.Namespaces
                    }).ToList();
                    
                    var data = new MasterModListData
                    {
                        LastUpdated = DateTime.UtcNow,
                        Mods = modsToSave
                    };

                    var json = JsonSerializer.Serialize(data, IndentedWriteOptions);
                    Services.FileAccessQueue.WriteAllText(settingsPath, json);
                    
                    Logger.LogDebug($"Saved {mods.Count} mods to master list");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to save master mod list", ex);
                }
            }
        }

        private static List<ModInfo> LoadMasterList()
        {
            lock (_listLock)
            {
                try
                {
                    var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
                    var filename = AppConstants.GameConfig.GetMasterModsFilename(gameTag);
                    var settingsPath = PathManager.GetSettingsPath(filename);
                    
                    if (!File.Exists(settingsPath))
                    {
                        Logger.LogDebug("Master mod list not found, returning empty list");
                        return new List<ModInfo>();
                    }

                    var json = Services.FileAccessQueue.ReadAllText(settingsPath);
                    var data = JsonSerializer.Deserialize<MasterModListData>(json);
                    
                    if (data?.Mods != null)
                    {
                        // Recalculate ImagePath from disk - stored paths may be stale
                        // (e.g. mod was activated/deactivated, changing directory name)
                        var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                        foreach (var mod in data.Mods)
                        {
                            if (string.IsNullOrEmpty(mod.Directory) || string.IsNullOrEmpty(mod.Category))
                                continue;
                            var modDir = Path.Combine(modsPath, mod.Category, mod.Directory);
                            mod.ImagePath = GetOptimalImagePath(modDir);
                        }

                        _imagePathBaseDirectory = modsPath;
                        _imagePathUseWideFormat = SettingsManager.Current.UseWideTileFormat;

                        Logger.LogDebug($"Loaded {data.Mods.Count} mods from master list (updated: {data.LastUpdated})");
                        return data.Mods;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to load master mod list", ex);
                }

                return new List<ModInfo>();
            }
        }
    }
}
