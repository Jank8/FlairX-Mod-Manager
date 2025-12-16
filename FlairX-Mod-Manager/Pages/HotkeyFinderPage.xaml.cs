using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class HotkeyFinderPage : Page
    {
        public HotkeyFinderPage()
        {
            this.InitializeComponent();
        }

        // All UI removed - hotkeys are now detected automatically on startup and after mod reload
        // Static methods below are used by the automatic detection system
        // Shared method for detecting and updating hotkeys for mods
        private static async Task DetectAndUpdateHotkeysAsync(
            IEnumerable<string> modDirs,
            bool recursive,
            CancellationToken token = default)
        {
            foreach (var modDir in modDirs)
            {
                if (token.IsCancellationRequested)
                    break;

                // Collect INI files
                var iniFiles = new List<string>();
                if (recursive)
                {
                    FindIniFilesStaticRecursive(modDir, iniFiles);
                }
                else
                {
                    try
                    {
                        var files = Directory.GetFiles(modDir, "*.ini");
                        foreach (var file in files)
                        {
                            var fileName = Path.GetFileName(file);
                            if (!string.Equals(fileName, "desktop.ini", StringComparison.OrdinalIgnoreCase) &&
                                !fileName.ToLowerInvariant().StartsWith("disabled"))
                            {
                                iniFiles.Add(file);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to find INI files in {modDir}", ex);
                    }
                }

                // Parse INI files and collect hotkeys
                var hotkeys = new List<HotkeyInfo>();
                foreach (var iniFile in iniFiles)
                {
                    if (token.IsCancellationRequested)
                        break;
                    try
                    {
                        var fileHotkeys = await ParseIniFileCommonAsync(iniFile, token);
                        hotkeys.AddRange(fileHotkeys);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to parse INI file {iniFile}", ex);
                    }
                }

                // Remove duplicates
                hotkeys = hotkeys.GroupBy(h => h.Key + "|" + h.Description)
                                 .Select(g => g.First())
                                 .ToList();

                if (hotkeys.Count > 0)
                {
                    Logger.LogInfo($"Found {hotkeys.Count} hotkeys in mod: {Path.GetFileName(modDir)}");
                    await UpdateModJsonWithHotkeysCommonAsync(modDir, hotkeys, token);
                }
                else
                {
                    Logger.LogInfo($"No hotkeys found in mod: {Path.GetFileName(modDir)}");
                }
            }
        }

        // Static method to refresh all mods hotkeys (for use after reload)
        public static async Task RefreshAllModsHotkeysStaticAsync()
        {
            try
            {
                var xxmiModsPath = SharedUtilities.GetSafeXXMIModsPath();
                if (string.IsNullOrEmpty(xxmiModsPath) || !Directory.Exists(xxmiModsPath))
                {
                    // Normal when no game is selected yet
                    Logger.LogInfo("XXMI Mods path not configured - skipping hotkey detection");
                    return;
                }
                
                // Get all mod directories from all categories
                var modDirectories = new List<string>();
                foreach (var categoryDir in Directory.GetDirectories(xxmiModsPath))
                {
                    if (Directory.Exists(categoryDir))
                    {
                        modDirectories.AddRange(Directory.GetDirectories(categoryDir));
                    }
                }
                
                // Detect hotkeys from .ini files and create hotkeys.json
                await DetectAndUpdateHotkeysAsync(modDirectories.ToArray(), true);
                
                // Clean up old hotkeys sections from mod.json files
                await CleanupOldHotkeysFromModJsonAsync(modDirectories);
                Logger.LogInfo($"Refreshed hotkeys for {modDirectories.Count} mods");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to refresh all mods hotkeys", ex);
            }
        }
        
        // Static method for single mod hotkey detection (kept for compatibility)
        public static async Task AutoDetectHotkeysForModStaticAsync(string modPath)
        {
            try
            {
                await DetectAndUpdateHotkeysAsync(new[] { modPath }, false);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error auto-detecting hotkeys for {modPath}", ex);
            }
        }

        // Key mapping
        private static readonly Dictionary<string, string> KeyMapping = new()
        {
            // Arrow keys
            ["VK_UP"] = "↑", ["VK_DOWN"] = "↓", ["VK_LEFT"] = "←", ["VK_RIGHT"] = "→",
            ["UP"] = "↑", ["DOWN"] = "↓", ["LEFT"] = "←", ["RIGHT"] = "→",
            
            // Numeric keys
            ["VK_NUMPAD0"] = "NUM 0", ["VK_NUMPAD1"] = "NUM 1", ["VK_NUMPAD2"] = "NUM 2",
            ["VK_NUMPAD3"] = "NUM 3", ["VK_NUMPAD4"] = "NUM 4", ["VK_NUMPAD5"] = "NUM 5",
            ["VK_NUMPAD6"] = "NUM 6", ["VK_NUMPAD7"] = "NUM 7", ["VK_NUMPAD8"] = "NUM 8",
            ["VK_NUMPAD9"] = "NUM 9", ["NUMPAD0"] = "NUM 0", ["NUMPAD1"] = "NUM 1",
            ["NUMPAD2"] = "NUM 2", ["NUMPAD3"] = "NUM 3", ["NUMPAD4"] = "NUM 4",
            ["NUMPAD5"] = "NUM 5", ["NUMPAD6"] = "NUM 6", ["NUMPAD7"] = "NUM 7",
            ["NUMPAD8"] = "NUM 8", ["NUMPAD9"] = "NUM 9",
            ["VK_MULTIPLY"] = "NUM *", ["VK_ADD"] = "NUM +", ["VK_SUBTRACT"] = "NUM -",
            ["VK_DECIMAL"] = "NUM .", ["VK_DIVIDE"] = "NUM /",
            
            // Mouse buttons
            ["VK_LBUTTON"] = "LMB", ["VK_RBUTTON"] = "RMB", ["VK_MBUTTON"] = "MMB",
            ["VK_XBUTTON1"] = "X1", ["VK_XBUTTON2"] = "X2",
            
            // Modifier keys
            ["VK_ALT"] = "ALT", ["VK_CTRL"] = "CTRL", ["CONTROL"] = "CTRL", ["VK_CONTROL"] = "CTRL",
            ["VK_LCONTROL"] = "L-CTRL", ["VK_RCONTROL"] = "R-CTRL", ["LCTRL"] = "L-CTRL", ["RCTRL"] = "R-CTRL",
            ["VK_SHIFT"] = "SHIFT", ["VK_LSHIFT"] = "L-SHIFT", ["VK_RSHIFT"] = "R-SHIFT",
            ["LSHIFT"] = "L-SHIFT", ["RSHIFT"] = "R-SHIFT",
            ["VK_MENU"] = "ALT", ["VK_LMENU"] = "L-ALT", ["VK_RMENU"] = "R-ALT",
            ["LALT"] = "L-ALT", ["RALT"] = "R-ALT",
            
            // Special keys
            ["VK_OEM_MINUS"] = "-", ["VK_OEM_PLUS"] = "+", ["VK_BACKSPACE"] = "BACKSPACE",
            ["DELETE"] = "DEL", ["VK_DELETE"] = "DEL", ["VK_ESCAPE"] = "ESC",
            ["VK_RETURN"] = "ENTER", ["VK_TAB"] = "TAB", ["VK_SPACE"] = "SPACE",
            
            // Special characters
            ["VK_OEM_1"] = ";", ["VK_OEM_2"] = "/", ["VK_OEM_3"] = "`",
            ["VK_OEM_4"] = "[", ["VK_OEM_5"] = "\\", ["VK_OEM_6"] = "]",
            ["VK_OEM_7"] = "'", ["VK_OEM_8"] = "§", ["VK_OEM_COMMA"] = ",",
            ["VK_OEM_PERIOD"] = ".",
            
            // Xbox controller buttons
            ["XB_A"] = "XB A", ["XB_B"] = "XB B", ["XB_X"] = "XB X", ["XB_Y"] = "XB Y",
            ["XB_LEFT_SHOULDER"] = "XB LB", ["XB_RIGHT_SHOULDER"] = "XB RB",
            ["XB_LEFT_TRIGGER"] = "XB LT", ["XB_RIGHT_TRIGGER"] = "XB RT",
            ["XB_LEFT_THUMB"] = "XB LS", ["XB_RIGHT_THUMB"] = "XB RS",
            ["XB_START"] = "XB Start", ["XB_BACK"] = "XB Back",
            ["XB_DPAD_UP"] = "XB ↑", ["XB_DPAD_DOWN"] = "XB ↓",
            ["XB_DPAD_LEFT"] = "XB ←", ["XB_DPAD_RIGHT"] = "XB →",
            
            // PlayStation controller buttons
            ["PS_SQUARE"] = "□", ["PS_CROSS"] = "×", ["PS_CIRCLE"] = "○", ["PS_TRIANGLE"] = "△",
            ["PS_L1"] = "L1", ["PS_R1"] = "R1", ["PS_L2"] = "L2", ["PS_R2"] = "R2",
            ["PS_L3"] = "L3", ["PS_R3"] = "R3", ["PS_SHARE"] = "Share",
            ["PS_OPTIONS"] = "Options", ["PS_TOUCHPAD"] = "Touchpad",
            ["PS_DPAD_UP"] = "PS ↑", ["PS_DPAD_DOWN"] = "PS ↓",
            ["PS_DPAD_LEFT"] = "PS ←", ["PS_DPAD_RIGHT"] = "PS →",
            
            // Function keys
            ["VK_F1"] = "F1", ["VK_F2"] = "F2", ["VK_F3"] = "F3", ["VK_F4"] = "F4",
            ["VK_F5"] = "F5", ["VK_F6"] = "F6", ["VK_F7"] = "F7", ["VK_F8"] = "F8",
            ["VK_F9"] = "F9", ["VK_F10"] = "F10", ["VK_F11"] = "F11", ["VK_F12"] = "F12",
            
            // Navigation keys
            ["VK_HOME"] = "HOME", ["VK_END"] = "END", ["VK_PRIOR"] = "PAGE UP",
            ["VK_NEXT"] = "PAGE DOWN", ["VK_INSERT"] = "INS",
            
            // Lock keys
            ["VK_CAPITAL"] = "CAPS", ["VK_NUMLOCK"] = "NUM LOCK", ["VK_SCROLL"] = "SCROLL LOCK",
            
            // Windows keys
            ["VK_LWIN"] = "WIN", ["VK_RWIN"] = "WIN", ["VK_APPS"] = "MENU"
        };

        // Helper classes
        public class HotkeyInfo
        {
            public string Key { get; set; } = "";
            public string Description { get; set; } = "";
        }

        // Helper class for recursively finding INI files
        private static void FindIniFilesStaticRecursive(string folderPath, List<string> iniFiles)
        {
            try
            {
                // First, find INI files in the current directory
                var files = Directory.GetFiles(folderPath, "*.ini");
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    if (!string.Equals(fileName, "desktop.ini", StringComparison.OrdinalIgnoreCase) &&
                        !fileName.ToLowerInvariant().StartsWith("disabled"))
                    {
                        iniFiles.Add(file);
                    }
                }
                // Then, search subdirectories
                var directories = Directory.GetDirectories(folderPath);
                foreach (var dir in directories)
                {
                    FindIniFilesStaticRecursive(dir, iniFiles);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Directory access failed for {folderPath}", ex);
            }
        }
        
        // Static version of ParseIniFileAsync method
        private static async Task<List<HotkeyInfo>> ParseIniFileStaticAsync(string iniFilePath)
        {
            return await ParseIniFileCommonAsync(iniFilePath);
        }
        
        // Static version of ParseKeyValue method
        private static HotkeyInfo? ParseKeyValueStatic(string keyValue, string keyType, bool isBack = false)
        {
            return ParseKeyValueCommon(keyValue, keyType, isBack);
        }
        
        // Static version of UpdateModJsonWithHotkeysAsync method
        private static async Task UpdateModJsonWithHotkeysStaticAsync(string modPath, List<HotkeyInfo> hotkeys)
        {
            await UpdateModJsonWithHotkeysCommonAsync(modPath, hotkeys);
        }
        
        // Common method for parsing INI files
        private static async Task<List<HotkeyInfo>> ParseIniFileCommonAsync(string iniFilePath, CancellationToken token = default)
        {
            var hotkeys = new List<HotkeyInfo>();
            
            try
            {
                var lines = await File.ReadAllLinesAsync(iniFilePath, token);
                var currentSection = "";
                var isKeySection = false;
                var keyType = "";

                var sectionPattern = new Regex(@"\[(.*?)\]");
                var keyPattern = new Regex(@"^key\s*=\s*(.+)", RegexOptions.IgnoreCase);
                var backPattern = new Regex(@"^back\s*=\s*(.+)", RegexOptions.IgnoreCase);

                foreach (var line in lines)
                {
                    if (token.IsCancellationRequested)
                        break;

                    var trimmedLine = line.Trim();
                    
                    if (trimmedLine.StartsWith(";") || string.IsNullOrEmpty(trimmedLine))
                        continue;

                    if (trimmedLine.StartsWith("["))
                    {
                        var sectionMatch = sectionPattern.Match(trimmedLine);
                        if (sectionMatch.Success)
                        {
                            currentSection = sectionMatch.Groups[1].Value;
                            if (currentSection.ToLowerInvariant().StartsWith("key"))
                            {
                                isKeySection = true;
                                keyType = currentSection.Substring(3).Trim();
                                
                                // Filter key description similar to the original JS plugin
                                // Recursively remove prefixes
                                bool changed;
                                do {
                                    changed = false;
                                    string keyTypeLower = keyType.ToLowerInvariant();
                                    
                                    // Check and remove prefixes in order from longest to shortest
                                    if (keyTypeLower.StartsWith("swapswapvar")) {
                                        keyType = keyType.Substring(11).Trim();
                                        changed = true;
                                    }
                                    else if (keyTypeLower.StartsWith("keyswapvar")) {
                                        keyType = keyType.Substring(10).Trim();
                                        changed = true;
                                    }
                                    else if (keyTypeLower.StartsWith("keyswap") || keyTypeLower.StartsWith("swapvar")) {
                                        keyType = keyType.Substring(7).Trim();
                                        changed = true;
                                    }
                                    else if (keyTypeLower.StartsWith("key")) {
                                        keyType = keyType.Substring(3).Trim();
                                        changed = true;
                                    }
                                    else if (keyTypeLower.StartsWith("swap")) {
                                        keyType = keyType.Substring(4).Trim();
                                        changed = true;
                                    }
                                } while (changed && !string.IsNullOrWhiteSpace(keyType));
                                
                                // If description is empty after removing prefixes, set it to "Toggle"
                                if (string.IsNullOrWhiteSpace(keyType))
                                    keyType = "Toggle";
                            }
                            else
                            {
                                isKeySection = false;
                            }
                        }
                        continue;
                    }

                    if (!isKeySection)
                        continue;

                    var keyMatch = keyPattern.Match(trimmedLine);
                    var backMatch = backPattern.Match(trimmedLine);
                    var isBack = false;
                    
                    if (backMatch.Success)
                    {
                        keyMatch = backMatch;
                        isBack = true;
                    }
                    
                    if (keyMatch.Success)
                    {
                        var keyValue = keyMatch.Groups[1].Value.Trim();
                        var hotkeyInfo = ParseKeyValueCommon(keyValue, keyType, isBack);
                        if (hotkeyInfo != null)
                        {
                            hotkeys.Add(hotkeyInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"File parsing failed for {iniFilePath}", ex);
            }

            return hotkeys;
        }

        // Common method for processing key values
        private static HotkeyInfo? ParseKeyValueCommon(string keyValue, string keyType, bool isBack = false)
        {
            var parts = keyValue.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            // Find the main key (last element that does not start with "no_")
            var mainKey = "";
            var mainKeyIndex = -1;
            
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (!parts[i].ToLowerInvariant().StartsWith("no_"))
                {
                    mainKey = parts[i].ToUpperInvariant();
                    mainKeyIndex = i;
                    break;
                }
            }
            
            // If main key not found, use last element
            if (string.IsNullOrEmpty(mainKey))
            {
                mainKey = parts[parts.Length - 1].ToUpperInvariant();
                mainKeyIndex = parts.Length - 1;
            }
            
            // Check if key is in mapping
            if (!KeyMapping.TryGetValue(mainKey, out var mappedMainKey))
                mappedMainKey = mainKey;

            var modifiers = new List<string>();
            
            // Set of excluded modifiers (for NO_XXX)
            var excludedModifiers = new HashSet<string>();
            
            // First, identify excluded modifiers
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i].ToLowerInvariant();
                if (part.StartsWith("no_"))
                {
                    // Extract modifier name without 'no_' prefix
                    var excludedMod = part.Substring(3).ToUpperInvariant();
                    excludedModifiers.Add(excludedMod);
                    // Also add mapped version if it exists
                    if (KeyMapping.TryGetValue(excludedMod, out var mappedExcluded))
                    {
                        excludedModifiers.Add(mappedExcluded);
                    }
                }
            }
            
            // Then add non-excluded modifiers
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i].ToUpperInvariant();
                if (!part.StartsWith("NO_"))
                {
                    var mappedModifier = KeyMapping.TryGetValue(part, out var mappedMod) ? mappedMod : part;
                    if (!modifiers.Contains(mappedModifier) && !excludedModifiers.Contains(mappedModifier))
                    {
                        modifiers.Add(mappedModifier);
                    }
                }
            }

            var keyDisplay = modifiers.Count > 0 
                ? $"{string.Join("+", modifiers)}+{mappedMainKey}"
                : mappedMainKey;

            // Add "(Back)" suffix if it's a back-type key
            var backSuffix = isBack ? " (Back)" : "";
            
            return new HotkeyInfo
            {
                Key = keyDisplay,
                Description = keyType + backSuffix
            };
        }

        // Common method for updating mod.json file
        private static async Task UpdateModJsonWithHotkeysCommonAsync(string modPath, List<HotkeyInfo> hotkeys, CancellationToken token = default)
        {
            var modJsonPath = Path.Combine(modPath, "mod.json");
            
            try
            {
                // Use FileAccessQueue to prevent race conditions with other parts of the app
                await Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                {
                    Dictionary<string, object> modData;
                    var existingFavorites = new HashSet<string>();
                    
                    if (File.Exists(modJsonPath))
                    {
                        var existingJson = await File.ReadAllTextAsync(modJsonPath, token);
                        modData = JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson) ?? new();
                        
                        // Load existing favorite hotkeys
                        if (modData.TryGetValue("favoriteHotkeys", out var favObj) && favObj is JsonElement favElement)
                        {
                            if (favElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var fav in favElement.EnumerateArray())
                                {
                                    var favKey = fav.GetString();
                                    if (!string.IsNullOrEmpty(favKey))
                                    {
                                        existingFavorites.Add(favKey);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Fallback: create minimal structure if mod.json doesn't exist
                        modData = new Dictionary<string, object>
                        {
                            ["author"] = "unknown",
                            ["url"] = "https://",
                            ["version"] = "",
                            ["dateChecked"] = "0000-00-00",
                            ["dateUpdated"] = "0000-00-00"
                        };
                    }

                    var hotkeyArray = hotkeys.Select(h => new Dictionary<string, string>
                    {
                        ["key"] = h.Key,
                        ["description"] = h.Description
                    }).ToArray();

                    // Only create/update hotkeys.json file - don't modify mod.json
                    await CreateOrUpdateHotkeysJsonAsync(modPath, hotkeyArray, token);
                }, token);
            }
            catch (OperationCanceledException)
            {
                // Cancelled - don't log as error
            }
            catch (Exception ex)
            {
                Logger.LogError($"File update failed for {modJsonPath}", ex);
            }
        }

        // Method to clean up old hotkeys sections from mod.json files
        private static async Task CleanupOldHotkeysFromModJsonAsync(List<string> modDirectories)
        {
            try
            {
                Logger.LogInfo("Cleaning up old hotkeys sections from mod.json files");
                int cleanedCount = 0;
                
                foreach (var modDir in modDirectories)
                {
                    try
                    {
                        var modJsonPath = Path.Combine(modDir, "mod.json");
                        
                        if (!File.Exists(modJsonPath))
                            continue;
                        
                        var modJson = await File.ReadAllTextAsync(modJsonPath);
                        using var doc = JsonDocument.Parse(modJson);
                        var root = doc.RootElement;
                        
                        // Check if mod.json has hotkeys section
                        if (root.TryGetProperty("hotkeys", out _))
                        {
                            // Remove hotkeys section from mod.json
                            await RemoveHotkeysFromModJsonAsync(modJsonPath);
                            cleanedCount++;
                            Logger.LogInfo($"Removed old hotkeys section from: {Path.GetFileName(modDir)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to clean hotkeys from mod: {Path.GetFileName(modDir)}", ex);
                    }
                }
                
                if (cleanedCount > 0)
                {
                    Logger.LogInfo($"Cleanup completed: removed hotkeys sections from {cleanedCount} mod.json files");
                }
                else
                {
                    Logger.LogInfo("No mod.json files had hotkeys sections to remove");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to cleanup old hotkeys sections", ex);
            }
        }

        // Method to remove hotkeys section from mod.json
        private static async Task RemoveHotkeysFromModJsonAsync(string modJsonPath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(modJsonPath);
                var modData = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                
                // Remove hotkeys section
                modData.Remove("hotkeys");
                
                var newJson = JsonSerializer.Serialize(modData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(modJsonPath, newJson);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to remove hotkeys from {modJsonPath}", ex);
            }
        }

        // Method to create or update hotkeys.json file
        private static async Task CreateOrUpdateHotkeysJsonAsync(string modPath, Dictionary<string, string>[] hotkeyArray, CancellationToken token = default)
        {
            try
            {
                Logger.LogInfo($"CreateOrUpdateHotkeysJsonAsync called for mod: {Path.GetFileName(modPath)} with {hotkeyArray.Length} hotkeys");
                var hotkeysJsonPath = Path.Combine(modPath, "hotkeys.json");
                
                var existingDefaultHotkeys = new List<Dictionary<string, string>>();
                var existingHotkeys = new List<Dictionary<string, string>>();
                
                // If hotkeys.json already exists, preserve defaultHotkeys and load existing hotkeys
                if (File.Exists(hotkeysJsonPath))
                {
                    var existingJson = await File.ReadAllTextAsync(hotkeysJsonPath, token);
                    using var doc = JsonDocument.Parse(existingJson);
                    var root = doc.RootElement;
                    
                    // Preserve existing defaultHotkeys
                    if (root.TryGetProperty("defaultHotkeys", out var defaultProp) && defaultProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var h in defaultProp.EnumerateArray())
                        {
                            existingDefaultHotkeys.Add(new Dictionary<string, string>
                            {
                                ["key"] = h.GetProperty("key").GetString() ?? "",
                                ["description"] = h.GetProperty("description").GetString() ?? ""
                            });
                        }
                    }
                    
                    // Load existing hotkeys (user modifications)
                    if (root.TryGetProperty("hotkeys", out var hotkeysProp) && hotkeysProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var h in hotkeysProp.EnumerateArray())
                        {
                            existingHotkeys.Add(new Dictionary<string, string>
                            {
                                ["key"] = h.GetProperty("key").GetString() ?? "",
                                ["description"] = h.GetProperty("description").GetString() ?? ""
                            });
                        }
                    }
                }
                
                // Convert new hotkeys from finder
                var newHotkeys = hotkeyArray.ToList();
                
                // If no existing hotkeys.json, use new hotkeys as both current and default
                if (!File.Exists(hotkeysJsonPath))
                {
                    existingDefaultHotkeys = new List<Dictionary<string, string>>(newHotkeys);
                    existingHotkeys = new List<Dictionary<string, string>>(newHotkeys);
                }
                else
                {
                    // Update defaultHotkeys with any new keys found by hotkey finder
                    var existingDefaultKeys = new HashSet<string>(existingDefaultHotkeys.Select(h => h["description"]));
                    
                    foreach (var newHotkey in newHotkeys)
                    {
                        if (!existingDefaultKeys.Contains(newHotkey["description"]))
                        {
                            // New hotkey found - add to defaults
                            existingDefaultHotkeys.Add(new Dictionary<string, string>(newHotkey));
                            
                            // Also add to current hotkeys if not already there
                            var existingCurrentKeys = new HashSet<string>(existingHotkeys.Select(h => h["description"]));
                            if (!existingCurrentKeys.Contains(newHotkey["description"]))
                            {
                                existingHotkeys.Add(new Dictionary<string, string>(newHotkey));
                            }
                        }
                    }
                }
                
                // Create hotkeys.json structure
                var hotkeysData = new
                {
                    hotkeys = existingHotkeys,
                    defaultHotkeys = existingDefaultHotkeys
                };
                
                var json = JsonSerializer.Serialize(hotkeysData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(hotkeysJsonPath, json, token);
                
                Logger.LogInfo($"Created/updated hotkeys.json for mod: {Path.GetFileName(modPath)}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create/update hotkeys.json for {modPath}", ex);
            }
        }


    }
}