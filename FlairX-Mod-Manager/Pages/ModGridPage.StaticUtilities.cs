using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    /// <summary>
    /// ModGridPage partial class - Static utilities and helper methods
    /// NEW SYSTEM: No symlinks, mods stored directly in XXMI/Mods with DISABLED_ prefix
    /// </summary>
    public sealed partial class ModGridPage : Page
    {
        private const string DISABLED_PREFIX = "DISABLED_";

        /// <summary>
        /// Activate mod by removing DISABLED_ prefix
        /// </summary>
        public static bool ActivateModByRename(string modDirectory)
        {
            try
            {
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
                    return false;

                // Find the mod in category structure
                var modPath = FindModFolderPathStatic(modsPath, modDirectory);
                if (string.IsNullOrEmpty(modPath) || !Directory.Exists(modPath))
                    return false;

                var modName = Path.GetFileName(modPath);
                if (!modName.StartsWith(DISABLED_PREFIX))
                    return true; // Already active

                // Remove DISABLED_ prefix
                var parentDir = Path.GetDirectoryName(modPath);
                if (string.IsNullOrEmpty(parentDir))
                    return false;

                var newName = modName.Substring(DISABLED_PREFIX.Length);
                var newPath = Path.Combine(parentDir, newName);

                Directory.Move(modPath, newPath);
                Logger.LogInfo($"Activated mod: {modDirectory}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to activate mod: {modDirectory}", ex);
                return false;
            }
        }

        /// <summary>
        /// Deactivate mod by adding DISABLED_ prefix, with duplicate handling
        /// </summary>
        /// <param name="modDirectory">Original mod directory name</param>
        /// <param name="newModName">Output parameter with the new mod folder name after deactivation</param>
        /// <returns>True if deactivation was successful</returns>
        public static bool DeactivateModByRename(string modDirectory, out string newModName)
        {
            newModName = modDirectory; // Default to original name
            
            try
            {
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
                    return false;

                // Find the mod in category structure
                var modPath = FindModFolderPathStatic(modsPath, modDirectory);
                if (string.IsNullOrEmpty(modPath) || !Directory.Exists(modPath))
                    return false;

                var modName = Path.GetFileName(modPath);
                if (modName.StartsWith(DISABLED_PREFIX))
                {
                    newModName = GetCleanModName(modName); // Return clean name for already disabled mod
                    return true; // Already disabled
                }

                var parentDir = Path.GetDirectoryName(modPath);
                if (string.IsNullOrEmpty(parentDir))
                    return false;

                // Check for duplicates across all categories
                var cleanName = GetCleanModName(modName);
                bool hasDuplicate = CheckForDuplicatesAcrossCategories(modsPath, cleanName);

                string finalName;
                if (hasDuplicate)
                {
                    // Add _duplicate suffix before deactivating
                    finalName = DISABLED_PREFIX + modName + "_duplicate";
                    newModName = modName + "_duplicate"; // Return the new clean name for UI
                    Logger.LogInfo($"Handling duplicate mod during deactivation: {modName} -> {finalName}");
                }
                else
                {
                    // Regular deactivation
                    finalName = DISABLED_PREFIX + modName;
                    newModName = modName; // Keep original name for UI
                }

                var newPath = Path.Combine(parentDir, finalName);

                Directory.Move(modPath, newPath);
                Logger.LogInfo($"Deactivated mod: {modDirectory} -> {finalName}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to deactivate mod: {modDirectory}", ex);
                return false;
            }
        }

        /// <summary>
        /// Deactivate mod by adding DISABLED_ prefix, with duplicate handling (overload for backward compatibility)
        /// </summary>
        public static bool DeactivateModByRename(string modDirectory)
        {
            return DeactivateModByRename(modDirectory, out _);
        }

        /// <summary>
        /// Check if mod is active (doesn't have DISABLED_ prefix)
        /// </summary>
        public static bool IsModActive(string modFolderName)
        {
            return !modFolderName.StartsWith(DISABLED_PREFIX);
        }

        /// <summary>
        /// Find mod folder path in category structure
        /// </summary>
        private static string FindModFolderPathStatic(string modsPath, string modDirectory)
        {
            try
            {
                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    // Check for exact match
                    var exactPath = Path.Combine(categoryDir, modDirectory);
                    if (Directory.Exists(exactPath))
                        return exactPath;
                    
                    // Check for DISABLED_ version
                    var disabledPath = Path.Combine(categoryDir, DISABLED_PREFIX + modDirectory);
                    if (Directory.Exists(disabledPath))
                        return disabledPath;
                    
                    // Check for clean name (without DISABLED_)
                    var cleanName = GetCleanModName(modDirectory);
                    if (cleanName != modDirectory)
                    {
                        var cleanPath = Path.Combine(categoryDir, cleanName);
                        if (Directory.Exists(cleanPath))
                            return cleanPath;
                    }
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Get clean mod name without DISABLED_ prefix
        /// </summary>
        public static string GetCleanModName(string modFolderName)
        {
            if (modFolderName.StartsWith(DISABLED_PREFIX))
                return modFolderName.Substring(DISABLED_PREFIX.Length);
            return modFolderName;
        }

        /// <summary>
        /// Check if there are duplicates of the same mod across categories (both active and inactive versions)
        /// </summary>
        private static bool CheckForDuplicatesAcrossCategories(string modsPath, string cleanModName)
        {
            try
            {
                var foundInstances = new List<(bool isActive, string path)>();

                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;

                    foreach (var modDir in Directory.GetDirectories(categoryDir))
                    {
                        var modFolderName = Path.GetFileName(modDir);
                        var currentCleanName = GetCleanModName(modFolderName);
                        
                        if (string.Equals(currentCleanName, cleanModName, StringComparison.OrdinalIgnoreCase))
                        {
                            bool isActive = !modFolderName.StartsWith(DISABLED_PREFIX);
                            foundInstances.Add((isActive, modDir));
                        }
                    }
                }

                // Check if there are both active and inactive versions
                bool hasActiveVersion = foundInstances.Any(x => x.isActive);
                bool hasInactiveVersion = foundInstances.Any(x => !x.isActive);

                return hasActiveVersion && hasInactiveVersion && foundInstances.Count > 1;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking for duplicates: {cleanModName}", ex);
                return false;
            }
        }

        public static void ApplyPreset(string presetName)
        {
            string gameSpecificPresetsPath = AppConstants.GameConfig.GetPresetsPath(SettingsManager.CurrentSelectedGame);
            string presetPath;
            
            if (string.IsNullOrEmpty(gameSpecificPresetsPath))
            {
                presetPath = Path.Combine(AppContext.BaseDirectory, "Settings", "Presets", presetName + ".json");
            }
            else
            {
                presetPath = Path.Combine(AppContext.BaseDirectory, gameSpecificPresetsPath, presetName + ".json");
            }
            
            if (!File.Exists(presetPath)) return;
            try
            {
                var json = File.ReadAllText(presetPath);
                var preset = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                if (preset != null)
                {
                    // Apply preset by renaming mods
                    foreach (var mod in preset)
                    {
                        if (mod.Value)
                            ActivateModByRename(mod.Key);
                        else
                            DeactivateModByRename(mod.Key);
                    }

                    // Update ActiveMods.json
                    var activeModsPath = PathManager.GetActiveModsPath();
                    var presetJson = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(activeModsPath, presetJson);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to apply preset", ex);
            }
        }

        // Cache management methods
        public static void ClearJsonCache()
        {
            lock (_cacheLock)
            {
                _modJsonCache.Clear();
                _modFileTimestamps.Clear();
                LogToGridLog("CACHE: JSON cache cleared");
            }
        }

        public static void InvalidateModCache(string modDirectory)
        {
            lock (_cacheLock)
            {
                var dirName = Path.GetFileName(modDirectory);
                var cleanName = GetCleanModName(dirName);
                
                if (_modJsonCache.Remove(dirName))
                {
                    _modFileTimestamps.Remove(dirName);
                    LogToGridLog($"CACHE: Invalidated cache for {dirName}");
                }
                if (_modJsonCache.Remove(cleanName))
                {
                    _modFileTimestamps.Remove(cleanName);
                    LogToGridLog($"CACHE: Invalidated cache for {cleanName}");
                }
            }
        }

        private static string CleanPath(string path)
        {
            while (path.Contains("\\\\")) path = path.Replace("\\\\", "\\");
            while (path.Contains("//")) path = path.Replace("//", "/");
            return path;
        }

        public void SaveDefaultPresetAllInactive()
        {
            var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
            if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
                return;

            var allMods = new Dictionary<string, bool>();
            
            foreach (var categoryDir in Directory.GetDirectories(modsPath))
            {
                if (!Directory.Exists(categoryDir)) continue;
                
                foreach (var modDir in Directory.GetDirectories(categoryDir))
                {
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (File.Exists(modJsonPath))
                    {
                        try
                        {
                            var json = File.ReadAllText(modJsonPath);
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            var modCharacter = root.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "other" : "other";
                            if (string.Equals(modCharacter, "other", StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to parse mod.json for preset: {modDir}", ex);
                            continue;
                        }
                        
                        string modName = GetCleanModName(Path.GetFileName(modDir));
                        allMods[modName] = false;
                    }
                }
            }
            
            string gameSpecificPresetsPath = AppConstants.GameConfig.GetPresetsPath(SettingsManager.CurrentSelectedGame);
            string presetPath;
            
            if (string.IsNullOrEmpty(gameSpecificPresetsPath))
            {
                presetPath = Path.Combine(AppContext.BaseDirectory, "Settings", "Presets", "Default Preset.json");
            }
            else
            {
                presetPath = Path.Combine(AppContext.BaseDirectory, gameSpecificPresetsPath, "Default Preset.json");
            }
            
            var presetDir = Path.GetDirectoryName(presetPath) ?? string.Empty;
            Directory.CreateDirectory(presetDir);
            try
            {
                var json = JsonSerializer.Serialize(allMods, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(presetPath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save default preset", ex);
            }
        }

        public void RefreshChangedMods()
        {
            LogToGridLog("INCREMENTAL: Starting incremental mod refresh");
            
            var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
            if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
                return;
            
            var changedMods = new List<string>();
            var newMods = new List<string>();
            var removedMods = new List<string>();
            
            var existingModDirs = new HashSet<string>();
            foreach (var categoryDir in Directory.GetDirectories(modsPath))
            {
                if (!Directory.Exists(categoryDir)) continue;
                
                foreach (var modDir in Directory.GetDirectories(categoryDir))
                {
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (!File.Exists(modJsonPath)) continue;
                    
                    var dirName = GetCleanModName(Path.GetFileName(modDir));
                    existingModDirs.Add(dirName);
                    var lastWriteTime = File.GetLastWriteTime(modJsonPath);
                    
                    lock (_cacheLock)
                    {
                        if (_modFileTimestamps.TryGetValue(dirName, out var cachedTime))
                        {
                            if (lastWriteTime > cachedTime)
                            {
                                changedMods.Add(dirName);
                            }
                        }
                        else
                        {
                            newMods.Add(dirName);
                        }
                    }
                }
            }
            
            lock (_cacheLock)
            {
                removedMods = _modJsonCache.Keys.Where(cached => !existingModDirs.Contains(cached)).ToList();
            }
            
            if (changedMods.Count > 0 || newMods.Count > 0 || removedMods.Count > 0)
            {
                LogToGridLog($"INCREMENTAL: Found {changedMods.Count} changed, {newMods.Count} new, {removedMods.Count} removed mods");
                
                foreach (var removed in removedMods)
                {
                    InvalidateModCache(removed);
                }
                
                foreach (var changed in changedMods)
                {
                    InvalidateModCache(changed);
                }
                 
                if (_currentCategory == null && _allModData.Count > 0)
                {
                    LoadAllMods();
                }
            }
            else
            {
                LogToGridLog("INCREMENTAL: No changes detected");
            }
        }

        public static string GetDisplayPath(string path)
        {
            return CleanPath(path);
        }
    }
}
