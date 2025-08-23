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
    /// </summary>
    public sealed partial class ModGridPage : Page
    {
        public static void RecreateSymlinksFromActiveMods()
        {
            var modsDir = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
            var defaultModsDir = Path.Combine(AppContext.BaseDirectory, "XXMI", "ZZMI", "Mods");
            if (string.IsNullOrWhiteSpace(modsDir))
                modsDir = defaultModsDir;
            var modsDirFull = Path.GetFullPath(modsDir);
            var defaultModsDirFull = Path.GetFullPath(defaultModsDir);
            var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
            if (string.IsNullOrEmpty(modLibraryPath))
                modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");

            // Remove symlinks from old location (SymlinkState)
            var symlinkStatePath = Path.Combine(AppContext.BaseDirectory, "Settings", "SymlinkState.json");
            string? lastSymlinkTarget = null;
            if (File.Exists(symlinkStatePath))
            {
                try
                {
                    var json = File.ReadAllText(symlinkStatePath);
                    var state = JsonSerializer.Deserialize<SymlinkState>(json);
                    lastSymlinkTarget = state?.TargetPath;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to read symlink state during recreation", ex);
                }
            }
            if (!string.IsNullOrWhiteSpace(lastSymlinkTarget) && !lastSymlinkTarget.Equals(modsDirFull, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(lastSymlinkTarget))
                {
                    foreach (var dir in Directory.GetDirectories(lastSymlinkTarget))
                    {
                        if ((Directory.Exists(dir) || File.Exists(dir)) && IsSymlinkStatic(dir))
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                }
            }
            // Remove symlinks from default location if NOT currently selected
            if (!defaultModsDirFull.Equals(modsDirFull, StringComparison.OrdinalIgnoreCase) && Directory.Exists(defaultModsDirFull))
            {
                foreach (var dir in Directory.GetDirectories(defaultModsDirFull))
                {
                    if ((Directory.Exists(dir) || File.Exists(dir)) && IsSymlinkStatic(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
            }
            // Remove symlinks from new location
            if (Directory.Exists(modsDirFull))
            {
                foreach (var dir in Directory.GetDirectories(modsDirFull))
                {
                    if ((Directory.Exists(dir) || File.Exists(dir)) && IsSymlinkStatic(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
            }

            // Use game-specific ActiveMods path
            var activeModsPath = PathManager.GetActiveModsPath();
            if (!File.Exists(activeModsPath)) return;
            try
            {
                var json = File.ReadAllText(activeModsPath);
                var relMods = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
                foreach (var kv in relMods)
                {
                    if (kv.Value)
                    {
                        // Find the mod folder in the new category-based structure
                        var absModDir = FindModFolderPathStatic(modLibraryPath, kv.Key);
                        if (!string.IsNullOrEmpty(absModDir))
                        {
                            var linkPath = Path.Combine(modsDirFull, kv.Key);
                            if (!Directory.Exists(linkPath) && !File.Exists(linkPath))
                            {
                                CreateSymlinkStatic(linkPath, absModDir);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to recreate symlinks from active mods", ex);
            }
        }

        public static void ApplyPreset(string presetName)
        {
            // Use game-specific presets path
            string gameSpecificPresetsPath = AppConstants.GameConfig.GetPresetsPath(SettingsManager.CurrentSelectedGame);
            string presetPath;
            
            if (string.IsNullOrEmpty(gameSpecificPresetsPath))
            {
                // Fallback to root presets directory when no game selected
                presetPath = Path.Combine(AppContext.BaseDirectory, "Settings", "Presets", presetName + ".json");
            }
            else
            {
                presetPath = Path.Combine(AppContext.BaseDirectory, gameSpecificPresetsPath, presetName + ".json");
            }
            
            if (!File.Exists(presetPath)) return;
            try
            {
                RecreateSymlinksFromActiveMods();
                var json = File.ReadAllText(presetPath);
                var preset = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                if (preset != null)
                {
                    // Use game-specific ActiveMods path
                    var activeModsPath = PathManager.GetActiveModsPath();
                    var presetJson = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(activeModsPath, presetJson);
                    RecreateSymlinksFromActiveMods();
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
                if (_modJsonCache.Remove(dirName))
                {
                    _modFileTimestamps.Remove(dirName);
                    LogToGridLog($"CACHE: Invalidated cache for {dirName}");
                }
            }
        }

        // Add function to clean double slashes
        private static string CleanPath(string path)
        {
            while (path.Contains("\\\\")) path = path.Replace("\\\\", "\\");
            while (path.Contains("//")) path = path.Replace("//", "/");
            return path;
        }

        // Static helper for symlink creation
        private static void CreateSymlinkStatic(string linkPath, string targetPath)
        {
            // targetPath powinien być zawsze pełną ścieżką do katalogu moda w bibliotece modów
            // Jeśli targetPath jest nazwą katalogu, zbuduj pełną ścieżkę 
            var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
            if (string.IsNullOrEmpty(modLibraryPath))
                modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(targetPath))
            {
                targetPath = Path.Combine(modLibraryPath, Path.GetFileName(targetPath));
            }
            
            // Check if target directory exists before creating symlink
            if (!Directory.Exists(targetPath))
            {
                System.Diagnostics.Debug.WriteLine($"Target directory does not exist: {targetPath}");
                System.Diagnostics.Debug.WriteLine("Triggering manager reload due to missing mod directory...");
                
                // Trigger automatic reload
                TriggerManagerReloadStatic();
                return; // Don't create symlink for non-existent directory
            }
            
            targetPath = Path.GetFullPath(targetPath);
            CreateSymbolicLink(linkPath, targetPath, 1); // 1 = directory
        }

        // Static helper for symlink check
        public static bool IsSymlinkStatic(string path)
        {
            try
            {
                if (!Directory.Exists(path) && !File.Exists(path))
                    return false;
                    
                var dirInfo = new DirectoryInfo(path);
                return dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to check if path is symlink: {path}", ex);
                return false;
            }
        }

        private static async void TriggerManagerReloadStatic()
        {
            try
            {
                // Get the main window to trigger reload
                var mainWindow = (App.Current as App)?.MainWindow as FlairX_Mod_Manager.MainWindow;
                if (mainWindow != null)
                {
                    System.Diagnostics.Debug.WriteLine("Triggering manager reload due to missing mod directory for symlink creation...");
                    
                    // Small delay to let current operations complete
                    await Task.Delay(100);
                    
                    // Trigger reload using the same method as the reload button
                    var reloadMethod = mainWindow.GetType().GetMethod("ReloadModsAsync", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (reloadMethod != null)
                    {
                        var task = reloadMethod.Invoke(mainWindow, null) as Task;
                        if (task != null)
                        {
                            await task;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error triggering manager reload: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates and ensures symlinks are properly synchronized with active mods
        /// </summary>
        public static void ValidateAndFixSymlinks()
        {
            try
            {
                Logger.LogInfo("Starting symlink validation and repair");
                
                var modsDir = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrWhiteSpace(modsDir))
                    modsDir = Path.Combine(AppContext.BaseDirectory, "XXMI", "ZZMI", "Mods");
                
                var modsDirFull = Path.GetFullPath(modsDir);
                var modLibraryPath = SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                
                // Load active mods using game-specific path
                var activeModsPath = PathManager.GetActiveModsPath();
                var activeMods = new Dictionary<string, bool>();
                
                if (File.Exists(activeModsPath))
                {
                    try
                    {
                        var json = File.ReadAllText(activeModsPath);
                        activeMods = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to load active mods for validation", ex);
                    }
                }
                
                // Check for orphaned symlinks (symlinks that shouldn't exist)
                if (Directory.Exists(modsDirFull))
                {
                    var existingDirs = Directory.GetDirectories(modsDirFull);
                    foreach (var dir in existingDirs)
                    {
                        if (IsSymlinkStatic(dir))
                        {
                            var dirName = Path.GetFileName(dir);
                            if (!activeMods.ContainsKey(dirName) || !activeMods[dirName])
                            {
                                // This symlink shouldn't exist - remove it
                                try
                                {
                                    Directory.Delete(dir, true);
                                    Logger.LogInfo($"Removed orphaned symlink: {dir}");
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError($"Failed to remove orphaned symlink: {dir}", ex);
                                }
                            }
                        }
                    }
                }
                
                // Check for missing symlinks (active mods without symlinks)
                foreach (var mod in activeMods.Where(m => m.Value))
                {
                    var linkPath = Path.Combine(modsDirFull, mod.Key);
                    var sourcePath = FindModFolderPathStatic(modLibraryPath, mod.Key);
                    
                    if (!Directory.Exists(linkPath) && !string.IsNullOrEmpty(sourcePath) && Directory.Exists(sourcePath))
                    {
                        // Missing symlink for active mod - create it
                        try
                        {
                            CreateSymlinkStatic(linkPath, sourcePath);
                            Logger.LogInfo($"Created missing symlink: {linkPath} -> {sourcePath}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to create missing symlink: {linkPath}", ex);
                        }
                    }
                    else if (Directory.Exists(linkPath) && !IsSymlinkStatic(linkPath))
                    {
                        // Directory exists but is not a symlink - this is problematic
                        Logger.LogWarning($"Directory exists but is not a symlink: {linkPath}");
                    }
                }
                
                Logger.LogInfo("Symlink validation and repair completed");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to validate and fix symlinks", ex);
            }
        }

        public void SaveDefaultPresetAllInactive()
        {
            var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
            if (string.IsNullOrEmpty(modLibraryPath))
                modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            var allMods = new Dictionary<string, bool>();
            if (Directory.Exists(modLibraryPath))
            {
                foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
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
                            string modName = Path.GetFileName(modDir);
                            allMods[modName] = false;
                        }
                    }
                }
            }
            // Use game-specific preset directory
            string gameSpecificPresetsPath = AppConstants.GameConfig.GetPresetsPath(FlairX_Mod_Manager.SettingsManager.CurrentSelectedGame);
            string presetPath;
            
            if (string.IsNullOrEmpty(gameSpecificPresetsPath))
            {
                // Fallback to root presets directory when no game selected
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

        // Incremental update - only reload specific mods that have changed
        public void RefreshChangedMods()
        {
            LogToGridLog("INCREMENTAL: Starting incremental mod refresh");
            
            var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
            if (string.IsNullOrEmpty(modLibraryPath))
                modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(modLibraryPath)) return;
            
            var changedMods = new List<string>();
            var newMods = new List<string>();
            var removedMods = new List<string>();
            
            // Check for changed and new mods
            var existingModDirs = new HashSet<string>();
            foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
            {
                if (!Directory.Exists(categoryDir)) continue;
                
                foreach (var modDir in Directory.GetDirectories(categoryDir))
                {
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (!File.Exists(modJsonPath)) continue;
                    
                    var dirName = Path.GetFileName(modDir);
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
            
            // Check for removed mods
            lock (_cacheLock)
            {
                removedMods = _modJsonCache.Keys.Where(cached => !existingModDirs.Contains(cached)).ToList();
            }
            
            // Process changes
            if (changedMods.Count > 0 || newMods.Count > 0 || removedMods.Count > 0)
            {
                LogToGridLog($"INCREMENTAL: Found {changedMods.Count} changed, {newMods.Count} new, {removedMods.Count} removed mods");
                
                // Remove deleted mods from cache
                foreach (var removed in removedMods)
                {
                    InvalidateModCache(removed);
                }
                
                // Invalidate changed mods (they'll be reloaded on next access)
                foreach (var changed in changedMods)
                {
                    InvalidateModCache(changed);
                }
                
                // New mods will be loaded automatically when accessed
                 
                // Refresh the current view if we're showing all mods
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

        // Add function to display path with single slashes
        public static string GetDisplayPath(string path)
        {
            return CleanPath(path);
        }
    }
}