using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.Text;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class StatusKeeperSyncPage
    {



        // ==================== D3DX USER INI PATH MANAGEMENT ====================
        
        private static string? GetD3dxUserPathStatic()
        {
            var defaultPath = SettingsManager.Current.StatusKeeperD3dxUserIniPath;
            if (string.IsNullOrEmpty(defaultPath))
            {
                LogStatic("d3dx_user.ini path is not set, attempting to find it automatically...", "WARNING");
            }
            
            if (!string.IsNullOrEmpty(defaultPath) && File.Exists(defaultPath))
            {
                return defaultPath;
            }

            // Try to find d3dx_user.ini automatically using current game settings
            var currentGame = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
            
            var xxmiModsPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
            

            var tryPaths = new string[]
            {
                Path.Combine(Path.GetDirectoryName(xxmiModsPath) ?? string.Empty, "d3dx_user.ini"),
                Path.Combine(".", "XXMI", currentGame, "d3dx_user.ini"),
            };

            foreach (var p in tryPaths)
            {
                LogStatic($"Checking for d3dx_user.ini at: {p}");
                if (File.Exists(p))
                {
                    LogStatic($"Found d3dx_user.ini at: {p}");
                    SettingsManager.Current.StatusKeeperD3dxUserIniPath = p;
                    SettingsManager.Save();
                    // Note: No SetBreadcrumbBar here since static and no UI instance
                    return p;
                }
            }

            LogStatic($"d3dx_user.ini not found at any expected location: {string.Join(", ", tryPaths)}", "ERROR");
            return null;
        }

        // ==================== PERSISTENT VARIABLES PARSING ====================
        
        private static Dictionary<string, Dictionary<string, string>> ParsePersistentVariables()
        {
            var d3dxUserPath = GetD3dxUserPathStatic();
            
            LogStatic($"Looking for d3dx_user.ini at: {d3dxUserPath}");

            if (string.IsNullOrEmpty(d3dxUserPath) || !File.Exists(d3dxUserPath))
            {
                LogStatic($"d3dx_user.ini not found at: {d3dxUserPath}", "ERROR");
                throw new Exception("d3dx_user.ini not found");
            }

            var content = Services.FileAccessQueue.ReadAllText(d3dxUserPath);
            LogStatic($"Successfully read d3dx_user.ini ({content.Length} characters)");

            var allEntries = new Dictionary<string, Dictionary<string, string>>();
            var lines = content.Split('\n');
            LogStatic($"Parsing {lines.Length} lines from d3dx_user.ini");

            // Build namespace to files mapping for new format
            var namespaceToFiles = BuildNamespaceMapping();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";")) continue;

                // OLD FORMAT: $\mods\<path>\<file>.ini\<variable> = <value>
                var oldMatch = Regex.Match(trimmedLine, @"^\$\\mods\\(.+\.ini)\\([^=]+?)\s*=\s*(.+)$");
                if (oldMatch.Success)
                {
                    var fullIniPath = oldMatch.Groups[1].Value;
                    var varName = oldMatch.Groups[2].Value.Trim();
                    var value = oldMatch.Groups[3].Value.Trim();

                    // Extract mod folder name from path
                    // Path format: category\modname\file.ini
                    // We need the modname (second part)
                    var pathParts = fullIniPath.Split('\\', '/');
                    string modFolderName;
                    
                    if (pathParts.Length >= 2)
                    {
                        // Get the mod folder name (second part of path)
                        modFolderName = pathParts[1];
                    }
                    else
                    {
                        // Fallback to first part if path is malformed
                        modFolderName = pathParts[0];
                    }
                    
                    // Check if this mod has StatusKeeper sync enabled
                    if (!IsModSyncEnabled(modFolderName))
                    {
                        LogStatic($"⏭️ OLD FORMAT: Skipping {fullIniPath} -> {varName} (mod sync disabled)");
                        continue;
                    }

                    LogStatic($"✅ OLD FORMAT: {fullIniPath} -> {varName} = {value}");

                    if (!allEntries.ContainsKey(fullIniPath))
                    {
                        allEntries[fullIniPath] = new Dictionary<string, string>();
                    }
                    allEntries[fullIniPath][varName] = value;
                    continue;
                }

                // NEW FORMAT: $\<namespace>\<variable> = <value>
                var newMatch = Regex.Match(trimmedLine, @"^\$\\([^\\]+(?:\\[^\\]+)*)\\([^=]+?)\s*=\s*(.+)$");
                if (newMatch.Success)
                {
                    var namespacePath = newMatch.Groups[1].Value;
                    var varName = newMatch.Groups[2].Value.Trim();
                    var value = newMatch.Groups[3].Value.Trim();

                    // Find the .ini files that have this namespace
                    if (namespaceToFiles.TryGetValue(namespacePath, out var iniFilePaths))
                    {
                        LogStatic($"✅ NEW FORMAT: namespace={namespacePath} -> {varName} = {value}");
                        LogStatic($"  Mapped to files: {string.Join(", ", iniFilePaths)}");
                        
                        // Add variable to ALL files with this namespace
                        foreach (var iniFilePath in iniFilePaths)
                        {
                            if (!allEntries.ContainsKey(iniFilePath))
                            {
                                allEntries[iniFilePath] = new Dictionary<string, string>();
                            }
                            allEntries[iniFilePath][varName] = value;
                        }
                    }
                    else
                    {
                        // Try case-insensitive search
                        var foundKey = namespaceToFiles.Keys.FirstOrDefault(k => 
                            string.Equals(k, namespacePath, StringComparison.OrdinalIgnoreCase));
                        
                        if (foundKey != null)
                        {
                            LogStatic($"✅ NEW FORMAT: namespace={namespacePath} -> {varName} = {value}");
                            LogStatic($"  ✅ Found case-insensitive match: '{foundKey}' -> {string.Join(", ", namespaceToFiles[foundKey])}");
                            
                            // Add variable to ALL files with this namespace
                            foreach (var iniFilePath in namespaceToFiles[foundKey])
                            {
                                if (!allEntries.ContainsKey(iniFilePath))
                                {
                                    allEntries[iniFilePath] = new Dictionary<string, string>();
                                }
                                allEntries[iniFilePath][varName] = value;
                            }
                        }
                        else
                        {
                            // Namespace not found - could be disabled mod, missing mod, or outdated entry
                            // Only log at DEBUG level to avoid spam
                            LogStatic($"⏭️ Skipped namespace '{namespacePath}' (mod not found or sync disabled)", "DEBUG");
                        }
                    }
                    continue;
                }

                // Log unmatched lines that look like they should be variables
                if (trimmedLine.StartsWith("$\\") && trimmedLine.Contains("="))
                {
                    LogStatic($"⚠️ Line didn't match any pattern: {trimmedLine}", "WARN");
                }
            }

            LogStatic($"Parsing complete: Found {allEntries.Count} files with persistent variables");
            return allEntries;
        }

        private static bool IsModSyncEnabled(string modFolderName)
        {
            try
            {
                // Read directly from mod.json (StatusKeeper only processes active mods from d3dx_user.ini)
                var modsPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
                {
                    return true; // Default to enabled if path not found
                }

                // Search for mod in all categories
                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    var modPath = Path.Combine(categoryDir, modFolderName);
                    var modJsonPath = Path.Combine(modPath, "mod.json");
                    
                    if (File.Exists(modJsonPath))
                    {
                        var json = Services.FileAccessQueue.ReadAllText(modJsonPath);
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        
                        // statusKeeperSync defaults to true if not present or not explicitly false
                        var statusKeeperSync = root.TryGetProperty("statusKeeperSync", out var syncProp) && syncProp.ValueKind != System.Text.Json.JsonValueKind.False;
                        return statusKeeperSync;
                    }
                }
                
                // Mod not found, default to enabled
                return true;
            }
            catch (Exception ex)
            {
                LogStatic($"Error checking mod sync status for {modFolderName}: {ex.Message}", "WARN");
                return true; // Default to enabled on error
            }
        }

        private static Dictionary<string, List<string>> BuildNamespaceMapping()
        {
            var namespaceToFiles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            
            // Separate log for debugging mod scanning
            var scanLogPath = PathManager.GetSettingsPath("ModScanDebug.log");
            var scanLog = new StringBuilder();
            scanLog.AppendLine($"=== Mod Scan Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            
            try
            {
                var modLibraryPath = SharedUtilities.GetSafeXXMIModsPath();
                
                if (!Directory.Exists(modLibraryPath))
                {
                    LogStatic($"ModLibrary directory not found: {modLibraryPath}", "WARN");
                    scanLog.AppendLine($"ERROR: ModLibrary not found: {modLibraryPath}");
                    File.WriteAllText(scanLogPath, scanLog.ToString());
                    return namespaceToFiles;
                }

                scanLog.AppendLine($"ModLibrary: {modLibraryPath}");
                LogStatic($"Building namespace mapping (active mods only) from: {modLibraryPath}");
                int activeModCount = 0;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Get mods with StatusKeeper sync enabled from ModListManager (fast - no file I/O)
                var syncMods = ModListManager.GetStatusKeeperSyncMods();
                
                // Group by category for organized logging
                var modsByCategory = syncMods.GroupBy(m => m.Category).OrderBy(g => g.Key);

                foreach (var categoryGroup in modsByCategory)
                {
                    var categoryName = categoryGroup.Key;
                    scanLog.AppendLine($"[{categoryName}]");
                    
                    foreach (var modInfo in categoryGroup.OrderBy(m => m.Name))
                    {
                        // Skip inactive mods
                        if (!modInfo.IsActive)
                        {
                            continue;
                        }
                        
                        try
                        {
                            var modStopwatch = System.Diagnostics.Stopwatch.StartNew();
                            activeModCount++;
                            
                            string syncMethod = string.IsNullOrEmpty(modInfo.SyncMethod) ? "classic" : modInfo.SyncMethod;
                        
                            // Process namespace sync method
                            if (syncMethod == "namespace" && modInfo.Namespaces != null && modInfo.Namespaces.Count > 0)
                            {
                                foreach (var namespaceInfo in modInfo.Namespaces)
                                {
                                    var namespacePath = namespaceInfo.Namespace;
                                    if (string.IsNullOrEmpty(namespacePath)) continue;

                                    var iniFiles = new List<string>();
                                    foreach (var iniFile in namespaceInfo.IniFiles)
                                    {
                                        if (!string.IsNullOrEmpty(iniFile))
                                        {
                                            // Include category in path: Category/ModName/file.ini
                                            var fullIniPath = Path.Combine(categoryName, modInfo.Directory, iniFile).Replace('\\', '/');
                                            iniFiles.Add(fullIniPath);
                                        }
                                    }

                                    if (iniFiles.Count > 0)
                                    {
                                        namespaceToFiles[namespacePath] = iniFiles;
                                    }
                                }
                            }
                            
                            modStopwatch.Stop();
                            scanLog.AppendLine($"  OK [{modStopwatch.ElapsedMilliseconds}ms] ({syncMethod}): {modInfo.Directory}");
                        }
                        catch (Exception ex)
                        {
                            scanLog.AppendLine($"  ERROR: {modInfo.Directory} - {ex.Message}");
                            LogStatic($"Error processing mod {modInfo.Name}: {ex.Message}", "WARN");
                        }
                    }
                }

                stopwatch.Stop();
                scanLog.AppendLine($"=== Scan Complete: {namespaceToFiles.Count} namespaces from {activeModCount} active mods in {stopwatch.ElapsedMilliseconds}ms ===");
                File.WriteAllText(scanLogPath, scanLog.ToString());
                
                LogStatic($"Namespace mapping complete: {namespaceToFiles.Count} namespaces from {activeModCount} active mods in {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                LogStatic($"Error building namespace mapping: {ex.Message}", "ERROR");
            }

            return namespaceToFiles;
        }

        // ==================== FILE SYNC OPERATIONS ====================
        
        private static string? ResolvePath(string d3dxPath)
        {
            try
            {
                // Use XXMI Mods directory
                var xxmiModsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                
                
                var currentGame = SettingsManager.CurrentSelectedGame;
                LogStatic($"DEBUG: Current game: '{currentGame}', XXMI Mods: '{xxmiModsPath}'");
                LogStatic($"DEBUG: Resolving path '{d3dxPath}' from XXMI Mods: '{xxmiModsPath}'");
                
                // Try direct path first
                var directPath = Path.Combine(xxmiModsPath, d3dxPath.Replace('/', '\\'));
                LogStatic($"DEBUG: Trying direct path: '{directPath}'");
                if (File.Exists(directPath))
                {
                    LogStatic($"DEBUG: Found direct path: '{directPath}'");
                    return directPath;
                }
                
                // Fall back to case-insensitive resolution
                var currentPath = Path.GetFullPath(xxmiModsPath);
                var pathParts = d3dxPath.Split('\\', '/').Where(part => !string.IsNullOrEmpty(part)).ToArray();
                LogStatic($"DEBUG: Fallback search, path parts: [{string.Join(", ", pathParts)}]");

                foreach (var part in pathParts)
                {
                    if (Directory.Exists(currentPath))
                    {
                        var items = Directory.GetFileSystemEntries(currentPath);
                        var found = items.FirstOrDefault(item => 
                            Path.GetFileName(item).Equals(part, StringComparison.OrdinalIgnoreCase));
                        
                        if (found != null)
                        {
                            currentPath = found;
                            LogStatic($"DEBUG: Found part '{part}' -> '{currentPath}'");
                        }
                        else
                        {
                            LogStatic($"DEBUG: Part '{part}' not found in '{currentPath}'");
                            LogStatic($"DEBUG: Available items in '{currentPath}': [{string.Join(", ", Directory.GetFileSystemEntries(currentPath).Select(Path.GetFileName))}]");
                            return null;
                        }
                    }
                    else
                    {
                        LogStatic($"DEBUG: Directory does not exist: '{currentPath}'");
                        return null;
                    }
                }

                LogStatic($"DEBUG: Final resolved path: '{currentPath}'");
                return currentPath;
            }
            catch (Exception error)
            {
                LogStatic($"Error resolving path {d3dxPath}: {error.Message}", "ERROR");
                return null;
            }
        }

        private static (bool updated, int updateCount) UpdateVariablesInFile(string filePath, Dictionary<string, string> variables)
        {
            LogStatic($"DEBUG: UpdateVariablesInFile called for: '{filePath}'");
            LogStatic($"DEBUG: Variables to update: [{string.Join(", ", variables.Select(kv => $"{kv.Key}={kv.Value}"))}]");
            
            if (!File.Exists(filePath))
            {
                LogStatic($"DEBUG: File does not exist: '{filePath}'");
                return (false, 0);
            }

            var content = Services.FileAccessQueue.ReadAllText(filePath);
            // Split ONLY on '\n', do not split on '\r'
            var lines = content.Split('\n');
            var newLines = new List<string>(lines.Length);
            int updateCount = 0;
            bool modified = false;
            bool inConstantsSection = false;

            foreach (var line in lines)
            {
                // Remove trailing '\r' if present
                var cleanLine = line.EndsWith("\r") ? line.Substring(0, line.Length - 1) : line;
                var trimmedLine = cleanLine.Trim();

                // Check for section headers
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2).ToLower();
                    inConstantsSection = sectionName == "constants";
                    newLines.Add(cleanLine);
                    continue;
                }

                // Only update variables if we're in the [Constants] section
                if (inConstantsSection && !string.IsNullOrEmpty(trimmedLine) &&
                    !trimmedLine.StartsWith(";") && trimmedLine.Contains("="))
                {
                    // Match variable assignment (no indentation, no adding new lines)
                    var match = Regex.Match(cleanLine, @"^(.*?\$([^=\s]+))\s*=\s*(.*)$");
                    if (match.Success)
                    {
                        var fullVarDeclaration = match.Groups[1].Value;
                        var varName = match.Groups[2].Value;
                        var currentValue = match.Groups[3].Value.Trim();

                        var matchingVarKey = variables.Keys.FirstOrDefault(key =>
                            key.Equals(varName, StringComparison.OrdinalIgnoreCase));

                        if (matchingVarKey != null)
                        {
                            var newValue = variables[matchingVarKey];
                            if (currentValue != newValue)
                            {
                                newLines.Add($"{fullVarDeclaration} = {newValue}");
                                updateCount++;
                                modified = true;
                                LogStatic($"✅ Updated {varName}: {currentValue} → {newValue} in {Path.GetFileName(filePath)} [Constants]");
                            }
                            else
                            {
                                newLines.Add(cleanLine);
                            }
                            continue;
                        }
                    }
                }

                newLines.Add(cleanLine);
            }

            if (modified)
            {
                // Join using '\n' only (no extra lines)
                var newContent = string.Join("\n", newLines);
                Services.FileAccessQueue.WriteAllText(filePath, newContent);
            }

            return (modified, updateCount);
        }

        private static bool UpdateConstantsSection(string iniPath, Dictionary<string, string> constants)
        {
            if (!File.Exists(iniPath))
            {
                LogStatic($"File not found: {iniPath}", "WARN");
                return false;
            }

            bool result = false;
            Services.FileAccessQueue.ExecuteAsync(iniPath, async () =>
            {
                var content = await File.ReadAllTextAsync(iniPath, System.Text.Encoding.UTF8);
                var newline = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

                // Split on '\n' only. Splitting on '\r' and '\n' added an empty entry for every
                // CRLF, which doubled the file's line count on the first synchronisation.
                var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
                var lines = normalized.Split('\n');
                var newLines = new List<string>();
                bool inConstantsSection = false;
                bool constantsSectionFound = false;
                bool constantsWereUpdated = false;
                var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        var sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();

                        if (inConstantsSection)
                        {
                            // Add any remaining constants that were not present in the file
                            foreach (var kvp in constants)
                            {
                                if (!usedKeys.Contains(kvp.Key))
                                {
                                    newLines.Add($"    {kvp.Key} = {kvp.Value}");
                                    constantsWereUpdated = true;
                                }
                            }
                            inConstantsSection = false;
                        }

                        if (sectionName.Equals("constants", StringComparison.OrdinalIgnoreCase))
                        {
                            inConstantsSection = true;
                            constantsSectionFound = true;
                            newLines.Add(line);
                            continue;
                        }
                    }

                    if (inConstantsSection && !string.IsNullOrEmpty(trimmedLine) &&
                        !trimmedLine.StartsWith(";") && trimmedLine.Contains("="))
                    {
                        // Capture indentation
                        var match = Regex.Match(line, @"^(\s*)(.+?)\s*=\s*(.*)$");
                        if (match.Success)
                        {
                            var indentation = match.Groups[1].Value;
                            var key = match.Groups[2].Value.Trim();
                            var value = match.Groups[3].Value.Trim();
                            usedKeys.Add(key);
                            if (constants.TryGetValue(key, out var newValue))
                            {
                                if (value != newValue)
                                {
                                    newLines.Add($"{indentation}{key} = {newValue}");
                                    constantsWereUpdated = true;
                                }
                                else
                                {
                                    newLines.Add(line);
                                }
                            }
                            else
                            {
                                newLines.Add(line);
                            }
                            continue;
                        }
                    }

                    newLines.Add(line);
                }

                if (inConstantsSection)
                {
                    foreach (var kvp in constants)
                    {
                        if (!usedKeys.Contains(kvp.Key))
                        {
                            newLines.Add($"    {kvp.Key} = {kvp.Value}");
                            constantsWereUpdated = true;
                        }
                    }
                }

                if (!constantsSectionFound && constants.Count > 0)
                {
                    newLines.Add("");
                    newLines.Add("[Constants]");
                    foreach (var kvp in constants)
                    {
                        newLines.Add($"    {kvp.Key} = {kvp.Value}");
                        constantsWereUpdated = true;
                    }
                }

                if (constantsWereUpdated)
                {
                    var newContent = string.Join(newline, newLines);
                    await Services.FileAccessQueue.WriteAllTextUnlockedAsync(iniPath, newContent);
                    LogStatic($"✅ Updated [Constants] section in {Path.GetFileName(iniPath)}");
                    result = true;
                }
            }).GetAwaiter().GetResult();

            return result;
        }

        /// <summary>
        /// Runs one synchronisation pass.
        /// </summary>
        /// <remarks>
        /// The body is entirely blocking file I/O, and callers used to invoke it straight from
        /// OnNavigatedTo and from a Toggled handler, so opening the tab ran a full INI sync on the
        /// UI thread. Passes are also serialised: the file watcher, the periodic timer and a
        /// manual sync could previously read-modify-write the same .ini files simultaneously and
        /// lose updates.
        /// </remarks>
        private static async Task<(int updateCount, int fileCount, int lodSyncCount)> SyncPersistentVariables()
        {
            if (!_syncLock.Wait(0))
            {
                LogStatic("Sync already in progress - skipping this pass", "WARN");
                return (0, 0, 0);
            }

            try
            {
                return await Task.Run(SyncPersistentVariablesCore).ConfigureAwait(false);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private static (int updateCount, int fileCount, int lodSyncCount) SyncPersistentVariablesCore()
        {
            var allEntries = ParsePersistentVariables();

            if (allEntries.Count == 0)
            {
                return (0, 0, 0);
            }

            int updateCount = 0;
            int fileCount = 0;
            var updatedMainFiles = new List<string>();

            foreach (var entry in allEntries)
            {
                var d3dxPath = entry.Key;
                var variables = entry.Value;

                var actualPath = ResolvePath(d3dxPath);
                if (string.IsNullOrEmpty(actualPath) || !File.Exists(actualPath))
                {
                    continue;
                }

                var fileName = Path.GetFileName(actualPath).ToLower();
                var isLodFile = fileName.Contains("_lod") || fileName.Contains("_lod1.ini") || fileName.Contains("_lod2.ini");
                if (isLodFile)
                {
                    LogStatic($"Skipping LOD file {Path.GetFileName(actualPath)} - will be updated via [Constants] sync from main file");
                    continue;
                }

                var result = UpdateVariablesInFile(actualPath, variables);
                if (result.updated)
                {
                    fileCount++;
                    updateCount += result.updateCount;
                    updatedMainFiles.Add(actualPath);
                }
            }

            // Sync [Constants] sections from updated main files to their LOD files
            int lodSyncCount = 0;
            if (updatedMainFiles.Count > 0)
            {
                LogStatic($"Starting LOD sync for {updatedMainFiles.Count} updated main files...");
            }

            foreach (var mainFilePath in updatedMainFiles)
            {
                var constants = ExtractConstantsSection(mainFilePath);
                if (constants == null || constants.Count == 0)
                {
                    LogStatic($"No [Constants] section found in {Path.GetFileName(mainFilePath)}, skipping LOD sync");
                    continue;
                }

                LogStatic($"Found {constants.Count} constants in {Path.GetFileName(mainFilePath)}");

                var mainDir = Path.GetDirectoryName(mainFilePath);
                if (string.IsNullOrEmpty(mainDir)) continue;

                try
                {
                    var dirItems = Directory.GetFiles(mainDir, "*.ini");
                    var lodFiles = dirItems.Where(item =>
                    {
                        var fileName = Path.GetFileName(item).ToLower();
                        return fileName.EndsWith(".ini") && 
                              (fileName.Contains("_lod") || fileName.Contains("_lod1.ini") || fileName.Contains("_lod2.ini"));
                    }).ToArray();

                    if (lodFiles.Length == 0)
                    {
                        LogStatic($"No LOD files found in directory: {Path.GetFileName(mainDir)}");
                    }
                    else
                    {
                        LogStatic($"Found {lodFiles.Length} LOD files in directory: {string.Join(", ", lodFiles.Select(Path.GetFileName))}");
                    }

                    foreach (var lodFile in lodFiles)
                    {
                        if (UpdateConstantsSection(lodFile, constants))
                        {
                            lodSyncCount++;
                            LogStatic($"🔄 Synced [Constants] from {Path.GetFileName(mainFilePath)} to {Path.GetFileName(lodFile)}");
                        }
                        else
                        {
                            LogStatic($"Failed to sync [Constants] to {Path.GetFileName(lodFile)}", "WARN");
                        }
                    }
                }
                catch (Exception error)
                {
                    LogStatic($"Failed to sync LOD files for {Path.GetFileName(mainFilePath)}: {error.Message}", "WARN");
                }
            }

            if (updateCount > 0 || lodSyncCount > 0)
            {
                LogStatic($"🎯 Sync complete: {updateCount} variables updated in {fileCount}/{allEntries.Count} main files");
                if (lodSyncCount > 0)
                {
                    LogStatic($"🔄 LOD sync complete: {lodSyncCount} LOD files updated with [Constants] sections");
                }
            }

            return (updateCount, fileCount, lodSyncCount);
        }

        private static Dictionary<string, string>? ExtractConstantsSection(string iniPath)
        {
            if (!File.Exists(iniPath))
            {
                LogStatic($"File not found: {iniPath}", "WARN");
                return null;
            }

            var content = Services.FileAccessQueue.ReadAllText(iniPath);
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var constants = new Dictionary<string, string>();
            bool inConstantsSection = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                    inConstantsSection = sectionName.Equals("constants", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (inConstantsSection && !string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith(";"))
                {
                    var match = Regex.Match(trimmedLine, @"^(.+?)\s*=\s*(.*)$");
                    if (match.Success)
                    {
                        var key = match.Groups[1].Value.Trim();
                        var value = match.Groups[2].Value.Trim();
                        constants[key] = value;
                    }
                }
            }

            return constants.Count > 0 ? constants : null;
        }
    }
}
