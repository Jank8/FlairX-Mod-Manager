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

        private static void SaveActiveModsList(List<string> modNames)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetActiveModsFilename(gameTag);
            SaveModList(filename, modNames);
        }

        private static HashSet<string> LoadActiveModsList()
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetActiveModsFilename(gameTag);
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
        public static void AddToNSFWList(string categoryName, string modName)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetNSFWModsFilename(gameTag);
            AddToList(filename, $"{categoryName}|{modName}");
        }

        /// <summary>
        /// Remove a single mod from NSFW list (incremental update)
        /// </summary>
        public static void RemoveFromNSFWList(string categoryName, string modName)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetNSFWModsFilename(gameTag);
            RemoveFromList(filename, $"{categoryName}|{modName}");
        }

        /// <summary>
        /// Add a single mod to Broken list (incremental update)
        /// </summary>
        public static void AddToBrokenList(string categoryName, string modName)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetBrokenModsFilename(gameTag);
            AddToList(filename, $"{categoryName}|{modName}");
        }

        /// <summary>
        /// Remove a single mod from Broken list (incremental update)
        /// </summary>
        public static void RemoveFromBrokenList(string categoryName, string modName)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetBrokenModsFilename(gameTag);
            RemoveFromList(filename, $"{categoryName}|{modName}");
        }

        /// <summary>
        /// Add a single mod to Outdated list (incremental update)
        /// </summary>
        public static void AddToOutdatedList(string categoryName, string modName)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetOutdatedModsFilename(gameTag);
            AddToList(filename, $"{categoryName}|{modName}");
        }

        /// <summary>
        /// Remove a single mod from Outdated list (incremental update)
        /// </summary>
        public static void RemoveFromOutdatedList(string categoryName, string modName)
        {
            var gameTag = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var filename = AppConstants.GameConfig.GetOutdatedModsFilename(gameTag);
            RemoveFromList(filename, $"{categoryName}|{modName}");
        }

        /// <summary>
        /// Remove a mod from all lists (when mod is deleted)
        /// </summary>
        public static void RemoveFromAllLists(string categoryName, string modName)
        {
            RemoveFromNSFWList(categoryName, modName);
            RemoveFromBrokenList(categoryName, modName);
            RemoveFromOutdatedList(categoryName, modName);
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

                    var json = JsonSerializer.Serialize(data, IndentedWriteOptions);
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

                        var newJson = JsonSerializer.Serialize(data, IndentedWriteOptions);
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

                        var newJson = JsonSerializer.Serialize(data, IndentedWriteOptions);
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
