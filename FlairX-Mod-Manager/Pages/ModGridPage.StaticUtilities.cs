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
                
                // Update ModListManager with new directory name
                var cleanName = GetCleanModName(newName);
                ModListManager.UpdateModActivation(cleanName, newName, true);
                
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
                    newModName = finalName; // Return the actual new folder name
                    Logger.LogInfo($"Handling duplicate mod during deactivation: {modName} -> {finalName}");
                }
                else
                {
                    // Regular deactivation
                    finalName = DISABLED_PREFIX + modName;
                    newModName = finalName; // Return the actual new folder name
                }

                var newPath = Path.Combine(parentDir, finalName);

                Directory.Move(modPath, newPath);
                
                // Update ModListManager with new directory name
                ModListManager.UpdateModActivation(cleanName, finalName, false);
                
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
        /// Get list of active mods in the same category as the specified mod
        /// </summary>
        /// <param name="modDirectory">The mod directory name to check category for</param>
        /// <returns>List of active mod names in the same category, or empty list if none found</returns>
        public static List<string> GetActiveModsInCategory(string modDirectory)
        {
            var activeModsInCategory = new List<string>();
            
            try
            {
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
                    return activeModsInCategory;

                // Find the category of the specified mod
                string? targetCategory = null;
                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = Path.GetFileName(categoryDir);
                    if (string.IsNullOrEmpty(categoryName)) continue;
                    
                    // Check if the mod exists in this category (active or disabled)
                    var exactPath = Path.Combine(categoryDir, modDirectory);
                    var disabledPath = Path.Combine(categoryDir, DISABLED_PREFIX + modDirectory);
                    var cleanName = GetCleanModName(modDirectory);
                    var cleanPath = Path.Combine(categoryDir, cleanName);
                    
                    if (Directory.Exists(exactPath) || Directory.Exists(disabledPath) || 
                        (cleanName != modDirectory && Directory.Exists(cleanPath)))
                    {
                        targetCategory = categoryName;
                        break;
                    }
                }
                
                if (string.IsNullOrEmpty(targetCategory))
                    return activeModsInCategory;
                
                // Skip conflict check for "Other" category - multiple mods allowed
                if (string.Equals(targetCategory, "Other", StringComparison.OrdinalIgnoreCase))
                    return activeModsInCategory;
                
                // Get all active mods in the found category
                var categoryPath = Path.Combine(modsPath, targetCategory);
                if (!Directory.Exists(categoryPath))
                    return activeModsInCategory;
                
                foreach (var modDir in Directory.GetDirectories(categoryPath))
                {
                    var modFolderName = Path.GetFileName(modDir);
                    
                    // Skip if this is a disabled mod (has DISABLED_ prefix)
                    if (modFolderName.StartsWith(DISABLED_PREFIX))
                        continue;
                    
                    // Skip if this is the same mod we're trying to activate
                    var cleanModName = GetCleanModName(modFolderName);
                    var targetCleanName = GetCleanModName(modDirectory);
                    if (string.Equals(cleanModName, targetCleanName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    // Check if mod.json exists to confirm it's a valid mod
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (File.Exists(modJsonPath))
                    {
                        activeModsInCategory.Add(cleanModName);
                    }
                }
                
                Logger.LogInfo($"Found {activeModsInCategory.Count} active mods in category '{targetCategory}' for mod '{modDirectory}'");
                return activeModsInCategory;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to get active mods in category for: {modDirectory}", ex);
                return activeModsInCategory;
            }
        }

        /// <summary>
        /// Get the category name for a specific mod
        /// </summary>
        /// <param name="modDirectory">The mod directory name</param>
        /// <returns>Category name or empty string if not found</returns>
        public static string GetModCategory(string modDirectory)
        {
            try
            {
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
                    return string.Empty;

                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = Path.GetFileName(categoryDir);
                    
                    // Check if the mod exists in this category (active or disabled)
                    var exactPath = Path.Combine(categoryDir, modDirectory);
                    var disabledPath = Path.Combine(categoryDir, DISABLED_PREFIX + modDirectory);
                    var cleanName = GetCleanModName(modDirectory);
                    var cleanPath = Path.Combine(categoryDir, cleanName);
                    
                    if (Directory.Exists(exactPath) || Directory.Exists(disabledPath) || 
                        (cleanName != modDirectory && Directory.Exists(cleanPath)))
                    {
                        return categoryName;
                    }
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to get category for mod: {modDirectory}", ex);
                return string.Empty;
            }
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
                var json = Services.FileAccessQueue.ReadAllText(presetPath);
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
                    Services.FileAccessQueue.WriteAllText(activeModsPath, presetJson);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to apply preset", ex);
            }
        }
        // No cache system - direct file reading for reliability

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
                            var json = Services.FileAccessQueue.ReadAllText(modJsonPath);
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
                Services.FileAccessQueue.WriteAllText(presetPath, json);
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
            
            // Count current mods on disk
            var currentModCount = 0;
            foreach (var categoryDir in Directory.GetDirectories(modsPath))
            {
                if (!Directory.Exists(categoryDir)) continue;
                
                foreach (var modDir in Directory.GetDirectories(categoryDir))
                {
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (File.Exists(modJsonPath))
                    {
                        currentModCount++;
                    }
                }
            }
            
            // If mod count changed, reload and rebuild persistent lists
            if (currentModCount != _allModData.Count)
            {
                LogToGridLog($"INCREMENTAL: Mod count changed ({_allModData.Count} -> {currentModCount}), reloading and rebuilding lists");
                
                // Clear cached lists so they get rebuilt
                _cachedNSFWMods = null;
                _cachedBrokenMods = null;
                _cachedOutdatedMods = null;
                
                if (_currentCategory == null && _allModData.Count > 0)
                {
                    LoadAllMods(); // This will call LoadAllModData which rebuilds the lists
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

        /// <summary>
        /// Deactivate all active mods in a specific category except the specified mod
        /// </summary>
        /// <param name="categoryName">Name of the category</param>
        /// <param name="excludeModDirectory">Mod directory to exclude from deactivation</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task DeactivateModsInCategory(string categoryName, string excludeModDirectory)
        {
            try
            {
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
                    return;

                var categoryPath = Path.Combine(modsPath, categoryName);
                if (!Directory.Exists(categoryPath))
                    return;

                var deactivatedCount = 0;
                var excludeCleanName = GetCleanModName(excludeModDirectory);

                foreach (var modDir in Directory.GetDirectories(categoryPath))
                {
                    var modFolderName = Path.GetFileName(modDir);
                    
                    // Skip if already disabled
                    if (modFolderName.StartsWith(DISABLED_PREFIX))
                        continue;
                    
                    // Skip the mod we're about to activate
                    var cleanModName = GetCleanModName(modFolderName);
                    if (string.Equals(cleanModName, excludeCleanName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    // Check if it's a valid mod
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (!File.Exists(modJsonPath))
                        continue;
                    
                    // Deactivate the mod
                    if (DeactivateModByRename(modFolderName))
                    {
                        deactivatedCount++;
                        Logger.LogInfo($"Auto-deactivated conflicting mod: {modFolderName}");
                    }
                }

                Logger.LogInfo($"Auto-deactivated {deactivatedCount} conflicting mods in category '{categoryName}'");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to deactivate mods in category '{categoryName}'", ex);
            }
        }
    }
}
