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
    public static partial class ModListManager
    {

        /// <summary>
        /// <summary>
        /// Update IsFavorite on a cached mod without rebuilding the entire list.
        /// Call this after SettingsManager.ToggleModFavorite.
        /// </summary>
        public static void UpdateModFavorite(string modName, bool isFavorite)
        {
            lock (_listLock)
            {
                if (_cachedMasterList == null) return;
                var mod = _cachedMasterList.FirstOrDefault(m => m.Name == modName);
                if (mod != null)
                    mod.IsFavorite = isFavorite;
            }
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

                // Convert imagePath to relative filename if it's absolute
                var relativeImagePath = string.IsNullOrEmpty(imagePath) ? string.Empty : Path.GetFileName(imagePath);

                // Check if mod already exists
                var existingMod = _cachedMasterList.FirstOrDefault(m => m.Name == modName);
                if (existingMod != null)
                {
                    // Update existing mod
                    existingMod.Directory = directory;
                    existingMod.Category = category;
                    existingMod.Author = author;
                    existingMod.Url = url;
                    existingMod.ImagePath = relativeImagePath;
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
                    var isFavorite = SettingsManager.IsModFavorite(gameTag, category, modName);
                    
                    var modInfo = new ModInfo
                    {
                        Name = modName,
                        Directory = directory,
                        Category = category,
                        Author = author,
                        Url = url,
                        ImagePath = relativeImagePath,
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
                UpdateFilterLists(category, modName, isActive, isNSFW, isBroken, hasUpdate);
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

                // ActiveMods.json uses "Category|Name" keys everywhere else. This method used
                // to append a bare name, which then never matched the list-based lookups.
                var activeKey = mod != null && !string.IsNullOrEmpty(mod.Category)
                    ? $"{mod.Category}|{modName}"
                    : modName;

                _cachedActiveList.Remove(modName); // drop the legacy bare key if present
                if (isActive)
                {
                    _cachedActiveList.Add(activeKey);
                }
                else
                {
                    _cachedActiveList.Remove(activeKey);
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

                // Remove from all filter lists (category unknown at deletion, will be cleaned up by next rebuild)
                // Note: Can't remove without category, will be cleaned up by next RebuildAllLists
            }

            Logger.LogDebug($"ModListManager: Removed mod '{modName}' from all lists");
        }

        private static void UpdateFilterLists(string category, string modName, bool isActive, bool isNSFW, bool isBroken, bool hasUpdate)
        {
            // Load all filter lists if not cached
            if (_cachedActiveList == null) _cachedActiveList = LoadActiveModsList();
            if (_cachedNSFWList == null) _cachedNSFWList = LoadNSFWModsList();
            if (_cachedBrokenList == null) _cachedBrokenList = LoadBrokenModsList();
            if (_cachedOutdatedList == null) _cachedOutdatedList = LoadOutdatedModsList();

            var cacheKey = $"{category}|{modName}";
            
            // Update active list
            if (isActive)
                _cachedActiveList.Add(cacheKey);
            else
                _cachedActiveList.Remove(cacheKey);

            // Update NSFW list
            if (isNSFW)
                _cachedNSFWList.Add(cacheKey);
            else
                _cachedNSFWList.Remove(cacheKey);

            // Update broken list
            if (isBroken)
                _cachedBrokenList.Add(cacheKey);
            else
                _cachedBrokenList.Remove(cacheKey);

            // Update outdated list
            if (hasUpdate)
                _cachedOutdatedList.Add(cacheKey);
            else
                _cachedOutdatedList.Remove(cacheKey);

            // Save all lists
            SaveActiveModsList(_cachedActiveList.ToList());
            SaveNSFWModsList(_cachedNSFWList.ToList());
            SaveBrokenModsList(_cachedBrokenList.ToList());
            SaveOutdatedModsList(_cachedOutdatedList.ToList());
        }

        /// <summary>
        /// Update mod in lists based on mod.json properties (when mod is installed/updated)
        /// </summary>
        public static void UpdateModInLists(string categoryName, string modName, bool isActive, bool isNSFW, bool isBroken, bool hasUpdate)
        {
            lock (_listLock)
            {
                UpdateFilterLists(categoryName, modName, isActive, isNSFW, isBroken, hasUpdate);
            }
        }
    }
}

