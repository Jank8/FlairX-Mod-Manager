using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Central manager for all mod lists - creates, updates, and provides fast access to mod data
    /// Grid reads from these lists instead of parsing hundreds of mod.json files
    /// </summary>
    public static class ModListManager
    {
        /// <summary>
        /// Complete mod information stored in master list
        /// </summary>
        public class ModInfo
        {
            public string Name { get; set; } = "";
            public string Directory { get; set; } = "";
            public string Category { get; set; } = "";
            public string Character { get; set; } = "";
            public string Author { get; set; } = "";
            public string Url { get; set; } = "";
            public string ImagePath { get; set; } = "";
            public DateTime LastChecked { get; set; } = DateTime.MinValue;
            public DateTime LastUpdated { get; set; } = DateTime.MinValue;
            public bool IsActive { get; set; }
            public bool IsNSFW { get; set; }
            public bool IsBroken { get; set; }
            public bool HasUpdate { get; set; }
            public bool IsFavorite { get; set; }
            public bool StatusKeeperSync { get; set; }
            public string SyncMethod { get; set; } = "";
            public List<NamespaceInfo> Namespaces { get; set; } = new List<NamespaceInfo>();
        }

        /// <summary>
        /// Namespace information for StatusKeeper sync
        /// </summary>
        public class NamespaceInfo
        {
            public string Namespace { get; set; } = "";
            public List<string> IniFiles { get; set; } = new List<string>();
        }

        private class ModListData
        {
            public DateTime LastUpdated { get; set; }
            public List<string> Mods { get; set; } = new List<string>();
        }

        private class MasterModListData
        {
            public DateTime LastUpdated { get; set; }
            public List<ModInfo> Mods { get; set; } = new List<ModInfo>();
        }

        private static readonly object _listLock = new object();
        
        // In-memory cache for fast access
        private static List<ModInfo>? _cachedMasterList = null;
        private static HashSet<string>? _cachedActiveList = null;
        private static HashSet<string>? _cachedNSFWList = null;
        private static HashSet<string>? _cachedBrokenList = null;
        private static HashSet<string>? _cachedOutdatedList = null;
        private static HashSet<string>? _cachedStatusKeeperSyncList = null;
        private static DateTime _lastCacheUpdate = DateTime.MinValue;

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
            var statusKeeperSyncList = new List<string>();
            
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
                                statusKeeperSync = false,
                                dateChecked = DateTime.UtcNow.ToString("o"),
                                dateUpdated = DateTime.UtcNow.ToString("o")
                            };
                            
                            var json = JsonSerializer.Serialize(defaultModJson, new JsonSerializerOptions { WriteIndented = true });
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

                        // Debug logging for specific mod
                        if (cleanName.Contains("Bottom Heavy", StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.LogInfo($"ModListManager: Processing 'Bottom Heavy' - DirName: {dirName}, CleanName: {cleanName}, IsActive: {isActive}");
                        }

                        // Parse mod.json
                        var json = Services.FileAccessQueue.ReadAllText(modJsonPath);
                        
                        // Debug logging for specific mod
                        if (cleanName.Contains("Bottom Heavy", StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.LogInfo($"ModListManager: 'Bottom Heavy' mod.json read successfully, length: {json.Length}");
                        }
                        
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

                        var isFavorite = SettingsManager.IsModFavorite(gameTag, cleanName);
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
                        
                        // Debug logging for specific mod
                        if (cleanName.Contains("Bottom Heavy", StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.LogInfo($"ModListManager: Found 'Bottom Heavy' - Name: {cleanName}, Category: {categoryName}, IsNSFW: {isNSFW}, IsActive: {isActive}");
                        }

                        // Build filter lists
                        if (isActive) activeList.Add(cleanName);
                        if (isNSFW) nsfwList.Add(cleanName);
                        if (isBroken) brokenList.Add(cleanName);
                        if (hasUpdate) outdatedList.Add(cleanName);
                        if (statusKeeperSync) statusKeeperSyncList.Add(cleanName);
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
            SaveStatusKeeperSyncList(statusKeeperSyncList);

            // Update cache
            lock (_listLock)
            {
                _cachedMasterList = masterList;
                _cachedActiveList = new HashSet<string>(activeList);
                _cachedNSFWList = new HashSet<string>(nsfwList);
                _cachedBrokenList = new HashSet<string>(brokenList);
                _cachedOutdatedList = new HashSet<string>(outdatedList);
                _cachedStatusKeeperSyncList = new HashSet<string>(statusKeeperSyncList);
                _lastCacheUpdate = DateTime.UtcNow;
            }

            Logger.LogInfo($"ModListManager: Rebuilt lists - Total dirs: {totalModDirs}, Processed: {processedMods}, Skipped: {skippedMods}");
            Logger.LogInfo($"ModListManager: Lists - Master: {masterList.Count}, Active: {activeList.Count}, NSFW: {nsfwList.Count}, Broken: {brokenList.Count}, Outdated: {outdatedList.Count}, StatusKeeperSync: {statusKeeperSyncList.Count}");
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
                return new List<ModInfo>(_cachedMasterList);
            }
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
        public static bool IsModNSFW(string modName)
        {
            lock (_listLock)
            {
                if (_cachedNSFWList == null)
                {
                    _cachedNSFWList = LoadNSFWModsList();
                }
                return _cachedNSFWList.Contains(modName);
            }
        }

        /// <summary>
        /// Check if mod is in Broken list (fast HashSet lookup)
        /// </summary>
        public static bool IsModBroken(string modName)
        {
            lock (_listLock)
            {
                if (_cachedBrokenList == null)
                {
                    _cachedBrokenList = LoadBrokenModsList();
                }
                return _cachedBrokenList.Contains(modName);
            }
        }

        /// <summary>
        /// Check if mod is in Outdated list (fast HashSet lookup)
        /// </summary>
        public static bool IsModOutdated(string modName)
        {
            lock (_listLock)
            {
                if (_cachedOutdatedList == null)
                {
                    _cachedOutdatedList = LoadOutdatedModsList();
                }
                return _cachedOutdatedList.Contains(modName);
            }
        }

        /// <summary>
        /// Check if mod is active (fast HashSet lookup)
        /// </summary>
        public static bool IsModActive(string modName)
        {
            lock (_listLock)
            {
                if (_cachedActiveList == null)
                {
                    _cachedActiveList = LoadActiveModsList();
                }
                return _cachedActiveList.Contains(modName);
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
        /// Check if mod has StatusKeeper sync enabled (fast HashSet lookup)
        /// </summary>
        public static bool IsModStatusKeeperSync(string modName)
        {
            lock (_listLock)
            {
                if (_cachedStatusKeeperSyncList == null)
                {
                    _cachedStatusKeeperSyncList = LoadStatusKeeperSyncList();
                }
                return _cachedStatusKeeperSyncList.Contains(modName);
            }
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
                _cachedStatusKeeperSyncList = null;
                _lastCacheUpdate = DateTime.MinValue;
            }
            Logger.LogDebug("ModListManager: Cache invalidated");
        }

        private static string GetCleanModName(string dirName)
        {
            if (dirName.StartsWith("DISABLED", StringComparison.OrdinalIgnoreCase))
            {
                return dirName.Substring(8).TrimStart('_', '-', ' ');
            }
            return dirName;
        }

        private static string GetOptimalImagePath(string modDirectory)
        {
            var webpPath = Path.Combine(modDirectory, "minitile.webp");
            var jpegPath = Path.Combine(modDirectory, "minitile.jpg");
            
            if (File.Exists(webpPath))
                return webpPath;
            
            return jpegPath;
        }

        private static void SaveMasterList(List<ModInfo> mods)
        {
            lock (_listLock)
            {
                try
                {
                    var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
                    var filename = $"mods_master_{gameTag}.json";
                    var settingsPath = PathManager.GetSettingsPath(filename);
                    
                    var data = new MasterModListData
                    {
                        LastUpdated = DateTime.UtcNow,
                        Mods = mods
                    };

                    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
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
                    var filename = $"mods_master_{gameTag}.json";
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

        private static void SaveActiveModsList(List<string> modNames)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = $"mods_active_{gameTag}.json";
            SaveModList(filename, modNames);
        }

        private static HashSet<string> LoadActiveModsList()
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = $"mods_active_{gameTag}.json";
            return LoadModList(filename);
        }

        /// <summary>
        /// Save StatusKeeper sync mods list for current game
        /// </summary>
        public static void SaveStatusKeeperSyncList(List<string> modNames)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = $"mods_statuskeeper_{gameTag}.json";
            SaveModList(filename, modNames);
        }

        /// <summary>
        /// Load StatusKeeper sync mods list for current game
        /// </summary>
        public static HashSet<string> LoadStatusKeeperSyncList()
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = $"mods_statuskeeper_{gameTag}.json";
            return LoadModList(filename);
        }

        /// <summary>
        /// Save NSFW mods list for current game
        /// </summary>
        public static void SaveNSFWModsList(List<string> modNames)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetNSFWModsFilename(gameTag);
            SaveModList(filename, modNames);
        }

        /// <summary>
        /// Save broken mods list for current game
        /// </summary>
        public static void SaveBrokenModsList(List<string> modNames)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetBrokenModsFilename(gameTag);
            SaveModList(filename, modNames);
        }

        /// <summary>
        /// Save outdated mods list for current game
        /// </summary>
        public static void SaveOutdatedModsList(List<string> modNames)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetOutdatedModsFilename(gameTag);
            SaveModList(filename, modNames);
        }

        /// <summary>
        /// Load NSFW mods list for current game
        /// </summary>
        public static HashSet<string> LoadNSFWModsList()
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetNSFWModsFilename(gameTag);
            return LoadModList(filename);
        }

        /// <summary>
        /// Load broken mods list for current game
        /// </summary>
        public static HashSet<string> LoadBrokenModsList()
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetBrokenModsFilename(gameTag);
            return LoadModList(filename);
        }

        /// <summary>
        /// Load outdated mods list for current game
        /// </summary>
        public static HashSet<string> LoadOutdatedModsList()
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetOutdatedModsFilename(gameTag);
            return LoadModList(filename);
        }

        /// <summary>
        /// Add a single mod to NSFW list (incremental update)
        /// </summary>
        public static void AddToNSFWList(string modName)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetNSFWModsFilename(gameTag);
            AddToList(filename, modName);
        }

        /// <summary>
        /// Remove a single mod from NSFW list (incremental update)
        /// </summary>
        public static void RemoveFromNSFWList(string modName)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetNSFWModsFilename(gameTag);
            RemoveFromList(filename, modName);
        }

        /// <summary>
        /// Add a single mod to Broken list (incremental update)
        /// </summary>
        public static void AddToBrokenList(string modName)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetBrokenModsFilename(gameTag);
            AddToList(filename, modName);
        }

        /// <summary>
        /// Remove a single mod from Broken list (incremental update)
        /// </summary>
        public static void RemoveFromBrokenList(string modName)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetBrokenModsFilename(gameTag);
            RemoveFromList(filename, modName);
        }

        /// <summary>
        /// Add a single mod to Outdated list (incremental update)
        /// </summary>
        public static void AddToOutdatedList(string modName)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetOutdatedModsFilename(gameTag);
            AddToList(filename, modName);
        }

        /// <summary>
        /// Remove a single mod from Outdated list (incremental update)
        /// </summary>
        public static void RemoveFromOutdatedList(string modName)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetOutdatedModsFilename(gameTag);
            RemoveFromList(filename, modName);
        }

        /// <summary>
        /// Remove a mod from all lists (when mod is deleted)
        /// </summary>
        public static void RemoveFromAllLists(string modName)
        {
            RemoveFromNSFWList(modName);
            RemoveFromBrokenList(modName);
            RemoveFromOutdatedList(modName);
        }

        /// <summary>
        /// Add newly installed mod to lists (incremental update)
        /// </summary>
        public static void AddInstalledMod(string modName, string directory, string category, string author, string url, string imagePath, bool isActive, bool isNSFW, bool isBroken, bool hasUpdate)
        {
            lock (_listLock)
            {
                // Load master list if not cached
                if (_cachedMasterList == null)
                {
                    _cachedMasterList = LoadMasterList();
                }

                // Check if mod already exists
                var existingMod = _cachedMasterList.FirstOrDefault(m => m.Name == modName);
                if (existingMod != null)
                {
                    // Update existing mod
                    existingMod.Directory = directory;
                    existingMod.Category = category;
                    existingMod.Author = author;
                    existingMod.Url = url;
                    existingMod.ImagePath = imagePath;
                    existingMod.IsActive = isActive;
                    existingMod.IsNSFW = isNSFW;
                    existingMod.IsBroken = isBroken;
                    existingMod.HasUpdate = hasUpdate;
                    existingMod.LastUpdated = DateTime.UtcNow;
                }
                else
                {
                    // Add new mod
                    var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
                    var isFavorite = SettingsManager.IsModFavorite(gameTag, modName);
                    
                    var modInfo = new ModInfo
                    {
                        Name = modName,
                        Directory = directory,
                        Category = category,
                        Author = author,
                        Url = url,
                        ImagePath = imagePath,
                        LastChecked = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow,
                        IsActive = isActive,
                        IsNSFW = isNSFW,
                        IsBroken = isBroken,
                        HasUpdate = hasUpdate,
                        IsFavorite = isFavorite
                    };

                    _cachedMasterList.Add(modInfo);
                }

                // Save updated master list
                SaveMasterList(_cachedMasterList);

                // Update filter lists
                UpdateFilterLists(modName, isActive, isNSFW, isBroken, hasUpdate);
            }

            Logger.LogInfo($"ModListManager: Added/updated mod '{modName}' in lists");
        }

        /// <summary>
        /// Update mod activation status (when mod is activated/deactivated)
        /// <summary>
        /// Update mod activation status (when mod is activated/deactivated)
        /// Updates IsActive flag and Directory name in master list
        /// </summary>
        public static void UpdateModActivation(string modName, string newDirectory, bool isActive)
        {
            lock (_listLock)
            {
                // Update master list
                if (_cachedMasterList == null)
                {
                    _cachedMasterList = LoadMasterList();
                }

                var mod = _cachedMasterList.FirstOrDefault(m => m.Name == modName);
                if (mod != null)
                {
                    mod.IsActive = isActive;
                    mod.Directory = newDirectory;
                    SaveMasterList(_cachedMasterList);
                }

                // Update active list
                if (_cachedActiveList == null)
                {
                    _cachedActiveList = LoadActiveModsList();
                }

                if (isActive)
                {
                    _cachedActiveList.Add(modName);
                }
                else
                {
                    _cachedActiveList.Remove(modName);
                }

                SaveActiveModsList(_cachedActiveList.ToList());
            }

            Logger.LogDebug($"ModListManager: Updated activation for '{modName}' to {isActive}, directory: {newDirectory}");
        }

        /// <summary>
        /// Remove mod from all lists (when mod is deleted)
        /// </summary>
        public static void RemoveMod(string modName)
        {
            lock (_listLock)
            {
                // Remove from master list
                if (_cachedMasterList == null)
                {
                    _cachedMasterList = LoadMasterList();
                }

                _cachedMasterList.RemoveAll(m => m.Name == modName);
                SaveMasterList(_cachedMasterList);

                // Remove from all filter lists
                RemoveFromAllLists(modName);
            }

            Logger.LogDebug($"ModListManager: Removed mod '{modName}' from all lists");
        }

        private static void UpdateFilterLists(string modName, bool isActive, bool isNSFW, bool isBroken, bool hasUpdate)
        {
            // Load all filter lists if not cached
            if (_cachedActiveList == null) _cachedActiveList = LoadActiveModsList();
            if (_cachedNSFWList == null) _cachedNSFWList = LoadNSFWModsList();
            if (_cachedBrokenList == null) _cachedBrokenList = LoadBrokenModsList();
            if (_cachedOutdatedList == null) _cachedOutdatedList = LoadOutdatedModsList();

            // Update active list
            if (isActive)
                _cachedActiveList.Add(modName);
            else
                _cachedActiveList.Remove(modName);

            // Update NSFW list
            if (isNSFW)
                _cachedNSFWList.Add(modName);
            else
                _cachedNSFWList.Remove(modName);

            // Update broken list
            if (isBroken)
                _cachedBrokenList.Add(modName);
            else
                _cachedBrokenList.Remove(modName);

            // Update outdated list
            if (hasUpdate)
                _cachedOutdatedList.Add(modName);
            else
                _cachedOutdatedList.Remove(modName);

            // Save all lists
            SaveActiveModsList(_cachedActiveList.ToList());
            SaveNSFWModsList(_cachedNSFWList.ToList());
            SaveBrokenModsList(_cachedBrokenList.ToList());
            SaveOutdatedModsList(_cachedOutdatedList.ToList());
        }

        /// <summary>
        /// Update mod in lists based on mod.json properties (when mod is installed/updated)
        /// </summary>
        public static void UpdateModInLists(string modName, bool isNSFW, bool isBroken, bool hasUpdate)
        {
            lock (_listLock)
            {
                UpdateFilterLists(modName, IsModActive(modName), isNSFW, isBroken, hasUpdate);
            }
        }

        private static void SaveModList(string filename, List<string> modNames)
        {
            lock (_listLock)
            {
                try
                {
                    var settingsPath = PathManager.GetSettingsPath(filename);
                    var data = new ModListData
                    {
                        LastUpdated = DateTime.UtcNow,
                        Mods = modNames
                    };

                    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                    Services.FileAccessQueue.WriteAllText(settingsPath, json);
                    
                    Logger.LogDebug($"Saved {modNames.Count} mods to {filename}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to save mod list {filename}", ex);
                }
            }
        }

        private static HashSet<string> LoadModList(string filename)
        {
            lock (_listLock)
            {
                try
                {
                    var settingsPath = PathManager.GetSettingsPath(filename);
                    
                    if (!File.Exists(settingsPath))
                    {
                        Logger.LogDebug($"Mod list file not found: {filename}");
                        return new HashSet<string>();
                    }

                    var json = Services.FileAccessQueue.ReadAllText(settingsPath);
                    var data = JsonSerializer.Deserialize<ModListData>(json);
                    
                    if (data?.Mods != null)
                    {
                        Logger.LogDebug($"Loaded {data.Mods.Count} mods from {filename} (updated: {data.LastUpdated})");
                        return new HashSet<string>(data.Mods);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to load mod list {filename}", ex);
                }

                return new HashSet<string>();
            }
        }

        private static void AddToList(string filename, string modName)
        {
            lock (_listLock)
            {
                try
                {
                    var settingsPath = PathManager.GetSettingsPath(filename);
                    var data = new ModListData();

                    // Load existing data if file exists
                    if (File.Exists(settingsPath))
                    {
                        var json = Services.FileAccessQueue.ReadAllText(settingsPath);
                        data = JsonSerializer.Deserialize<ModListData>(json) ?? new ModListData();
                    }

                    // Add mod if not already in list
                    if (!data.Mods.Contains(modName))
                    {
                        data.Mods.Add(modName);
                        data.LastUpdated = DateTime.UtcNow;

                        var newJson = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                        Services.FileAccessQueue.WriteAllText(settingsPath, newJson);
                        
                        Logger.LogDebug($"Added '{modName}' to {filename}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to add mod to list {filename}", ex);
                }
            }
        }

        private static void RemoveFromList(string filename, string modName)
        {
            lock (_listLock)
            {
                try
                {
                    var settingsPath = PathManager.GetSettingsPath(filename);
                    
                    if (!File.Exists(settingsPath))
                        return;

                    var json = Services.FileAccessQueue.ReadAllText(settingsPath);
                    var data = JsonSerializer.Deserialize<ModListData>(json);
                    
                    if (data?.Mods != null && data.Mods.Remove(modName))
                    {
                        data.LastUpdated = DateTime.UtcNow;

                        var newJson = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                        Services.FileAccessQueue.WriteAllText(settingsPath, newJson);
                        
                        Logger.LogDebug($"Removed '{modName}' from {filename}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to remove mod from list {filename}", ex);
                }
            }
        }
    }
}
