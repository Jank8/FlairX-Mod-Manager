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
    public sealed partial class HotkeyFinderPage
    {


        
        // Method to save hotkey changes back to INI files
        public static async Task SaveHotkeyToIniAsync(string modPath, string description, string newKey, bool isRetry = false)
        {
            try
            {
                Logger.LogHotkeyFinder($"SaveHotkeyToIniAsync called: modPath={PathManager.GetRelativePath(modPath)}, description={description}, newKey={newKey}");
                var hotkeysJsonPath = Path.Combine(modPath, "hotkeys.json");
                
                if (!File.Exists(hotkeysJsonPath))
                {
                    Logger.LogHotkeyFinder($"Hotkeys file not found: {hotkeysJsonPath}", "WARNING");
                    return;
                }
                
                var json = await Services.FileAccessQueue.ReadAllTextAsync(hotkeysJsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                // Find the hotkey in defaultHotkeys to get INI file information
                string iniFile = "";
                string section = "";
                string keyName = "";
                string originalValue = "";
                
                if (root.TryGetProperty("defaultHotkeys", out var defaultProp) && defaultProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in defaultProp.EnumerateArray())
                    {
                        if (h.GetProperty("description").GetString() == description)
                        {
                            if (h.TryGetProperty("iniFile", out var iniFileProp))
                                iniFile = iniFileProp.GetString() ?? "";
                            if (h.TryGetProperty("section", out var sectionProp))
                                section = sectionProp.GetString() ?? "";
                            if (h.TryGetProperty("keyName", out var keyNameProp))
                                keyName = keyNameProp.GetString() ?? "";
                            if (h.TryGetProperty("originalValue", out var originalValueProp))
                                originalValue = originalValueProp.GetString() ?? "";
                            
                            Logger.LogHotkeyFinder($"Found hotkey info: iniFile={iniFile}, section={section}, keyName={keyName}");
                            break;
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(iniFile) || string.IsNullOrEmpty(section) || string.IsNullOrEmpty(keyName))
                {
                    Logger.LogHotkeyFinder($"Missing INI file information for hotkey: {description}", "WARNING");
                    return;
                }
                
                // Convert relative path back to absolute path
                var absoluteIniPath = Path.IsPathRooted(iniFile) ? iniFile : Path.Combine(modPath, iniFile);
                
                // Normalize the path to handle any Unicode or path separator issues
                absoluteIniPath = Path.GetFullPath(absoluteIniPath);
                
                Logger.LogHotkeyFinder($"Converting path: iniFile='{iniFile}' -> absoluteIniPath='{absoluteIniPath}'");
                
                // Convert display key back to INI format
                var iniKeyValue = ConvertDisplayKeyToIniFormat(newKey);
                
                // Check if INI file exists
                if (!File.Exists(absoluteIniPath))
                {
                    Logger.LogHotkeyFinder($"INI file not found: {PathManager.GetRelativePath(absoluteIniPath)}", "WARNING");
                    
                    // Try to find the INI file with the same name in the mod directory
                    var iniFileName = Path.GetFileName(iniFile);
                    var foundIniFiles = Directory.GetFiles(modPath, iniFileName, SearchOption.AllDirectories);
                    
                    if (foundIniFiles.Length > 0)
                    {
                        var newIniPath = foundIniFiles[0];
                        var newRelativeIniPath = Path.GetRelativePath(modPath, newIniPath);
                        
                        Logger.LogHotkeyFinder($"Found INI file at new location: {PathManager.GetRelativePath(newIniPath)}");
                        Logger.LogHotkeyFinder($"Updating hotkeys.json with new path: {newRelativeIniPath}");
                        
                        // Update the hotkeys.json with the new path
                        await UpdateIniPathInHotkeysJsonAsync(modPath, description, newRelativeIniPath);
                        
                        // Use the new path for saving
                        absoluteIniPath = newIniPath;
                    }
                    else
                    {
                        Logger.LogHotkeyFinder($"INI file '{iniFileName}' not found anywhere in mod directory: {PathManager.GetRelativePath(modPath)}", "ERROR");
                        return;
                    }
                }
                
                // Update the INI file
                await UpdateIniFileAsync(absoluteIniPath, section, keyName, iniKeyValue);
                
                Logger.LogHotkeyFinder($"Updated INI file {PathManager.GetRelativePath(absoluteIniPath)}: [{section}] {keyName} = {iniKeyValue}");
            }
            catch (Exception ex)
            {
                Logger.LogHotkeyFinderError($"Failed to save hotkey to INI file: {description}", ex);
            }
        }
        
        // Method to convert display key format back to INI format
        private static string ConvertDisplayKeyToIniFormat(string displayKey)
        {
            // Reverse the key mapping process - handle duplicate values by taking the first key
            var reverseMapping = KeyMapping
                .GroupBy(kvp => kvp.Value)
                .ToDictionary(g => g.Key, g => g.First().Key);
            
            // Split by + to get modifiers and main key
            var parts = displayKey.Split('+');
            var iniParts = new List<string>();
            
            foreach (var part in parts)
            {
                var trimmedPart = part.Trim();
                if (reverseMapping.TryGetValue(trimmedPart, out var iniKey))
                {
                    iniParts.Add(iniKey);
                }
                else
                {
                    // If not in reverse mapping, use as-is but convert to uppercase
                    iniParts.Add(trimmedPart.ToUpperInvariant());
                }
            }
            
            return string.Join(" ", iniParts);
        }
        
        // Method to update a specific key in an INI file
        private static async Task UpdateIniFileAsync(string iniFilePath, string section, string keyName, string newValue)
        {
            try
            {
                await Services.FileAccessQueue.ExecuteAsync(iniFilePath, async () =>
                {
                    var raw = await File.ReadAllTextAsync(iniFilePath);
                    var newline = raw.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

                    // Split on '\n' only. Splitting on '\r' and '\n' at the same time produced
                    // an extra empty entry for every CRLF, so the first save doubled the file.
                    var normalized = raw.Replace("\r\n", "\n").Replace('\r', '\n');
                    var lines = normalized.Split('\n').ToList();
                    var updatedLines = new List<string>();
                    bool inTargetSection = false;
                    bool keyUpdated = false;

                    var sectionPattern = new Regex(@"^\s*\[(.*?)\]");
                    var keyPattern = new Regex(@"^(\s*)" + Regex.Escape(keyName) + @"\s*=\s*(.*)$", RegexOptions.IgnoreCase);

                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();

                        // Check if we're entering a new section
                        if (trimmedLine.StartsWith("["))
                        {
                            var sectionMatch = sectionPattern.Match(line);
                            if (sectionMatch.Success)
                            {
                                inTargetSection = sectionMatch.Groups[1].Value.Equals(section, StringComparison.OrdinalIgnoreCase);
                            }
                            updatedLines.Add(line);
                            continue;
                        }

                        // If we're in the target section and this line contains our key
                        if (inTargetSection && !trimmedLine.StartsWith(";") && !string.IsNullOrEmpty(trimmedLine))
                        {
                            var match = keyPattern.Match(line);
                            if (match.Success)
                            {
                                // Keep the original indentation and any trailing ';' comment.
                                var indentation = match.Groups[1].Value;
                                var rest = match.Groups[2].Value;
                                var commentIndex = rest.IndexOf(';');
                                var comment = commentIndex >= 0
                                    ? "  " + rest.Substring(commentIndex).TrimStart()
                                    : string.Empty;

                                updatedLines.Add($"{indentation}{keyName} = {newValue}{comment}");
                                keyUpdated = true;
                                continue;
                            }
                        }

                        updatedLines.Add(line);
                    }

                    if (!keyUpdated)
                    {
                        Logger.LogHotkeyFinder($"Key '{keyName}' not found in section '[{section}]' of file {PathManager.GetRelativePath(iniFilePath)}", "WARNING");
                        return;
                    }

                    var output = string.Join(newline, updatedLines);
                    if (raw.EndsWith("\n", StringComparison.Ordinal) && !output.EndsWith(newline, StringComparison.Ordinal))
                        output += newline;

                    // Already inside the queue for this path - use the unlocked atomic writer.
                    await Services.FileAccessQueue.WriteAllTextUnlockedAsync(iniFilePath, output);

                    Logger.LogHotkeyFinder($"Successfully updated {PathManager.GetRelativePath(iniFilePath)}: [{section}] {keyName} = {newValue}");
                });
            }
            catch (Exception ex)
            {
                Logger.LogHotkeyFinderError($"Failed to update INI file {iniFilePath}", ex);
            }
        }
        // Method to update INI file path in hotkeys.json
        private static async Task UpdateIniPathInHotkeysJsonAsync(string modPath, string description, string newIniPath)
        {
            try
            {
                var hotkeysJsonPath = Path.Combine(modPath, "hotkeys.json");
                
                if (!File.Exists(hotkeysJsonPath))
                    return;
                
                var json = await Services.FileAccessQueue.ReadAllTextAsync(hotkeysJsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                var defaultHotkeys = new List<Dictionary<string, object>>();
                var hotkeys = new List<Dictionary<string, object>>();
                
                // Update defaultHotkeys
                if (root.TryGetProperty("defaultHotkeys", out var defaultProp) && defaultProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in defaultProp.EnumerateArray())
                    {
                        var hotkey = new Dictionary<string, object>
                        {
                            ["key"] = h.GetProperty("key").GetString() ?? "",
                            ["description"] = h.GetProperty("description").GetString() ?? ""
                        };
                        
                        // Update iniFile path if this is the hotkey we're looking for
                        if (h.GetProperty("description").GetString() == description)
                        {
                            hotkey["iniFile"] = newIniPath;
                        }
                        else if (h.TryGetProperty("iniFile", out var iniFileProp))
                        {
                            hotkey["iniFile"] = iniFileProp.GetString() ?? "";
                        }
                        
                        // Preserve other properties
                        if (h.TryGetProperty("section", out var sectionProp))
                            hotkey["section"] = sectionProp.GetString() ?? "";
                        if (h.TryGetProperty("keyName", out var keyNameProp))
                            hotkey["keyName"] = keyNameProp.GetString() ?? "";
                        if (h.TryGetProperty("originalValue", out var originalValueProp))
                            hotkey["originalValue"] = originalValueProp.GetString() ?? "";
                            
                        defaultHotkeys.Add(hotkey);
                    }
                }
                
                // Update current hotkeys
                if (root.TryGetProperty("hotkeys", out var hotkeysProp) && hotkeysProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in hotkeysProp.EnumerateArray())
                    {
                        var hotkey = new Dictionary<string, object>
                        {
                            ["key"] = h.GetProperty("key").GetString() ?? "",
                            ["description"] = h.GetProperty("description").GetString() ?? ""
                        };
                        
                        // Update iniFile path if this is the hotkey we're looking for
                        if (h.GetProperty("description").GetString() == description)
                        {
                            hotkey["iniFile"] = newIniPath;
                        }
                        else if (h.TryGetProperty("iniFile", out var iniFileProp))
                        {
                            hotkey["iniFile"] = iniFileProp.GetString() ?? "";
                        }
                        
                        // Preserve other properties
                        if (h.TryGetProperty("section", out var sectionProp))
                            hotkey["section"] = sectionProp.GetString() ?? "";
                        if (h.TryGetProperty("keyName", out var keyNameProp))
                            hotkey["keyName"] = keyNameProp.GetString() ?? "";
                        if (h.TryGetProperty("originalValue", out var originalValueProp))
                            hotkey["originalValue"] = originalValueProp.GetString() ?? "";
                            
                        hotkeys.Add(hotkey);
                    }
                }
                
                // Save updated hotkeys.json
                var hotkeysData = new
                {
                    hotkeys = hotkeys,
                    defaultHotkeys = defaultHotkeys
                };
                
                var newJson = JsonSerializer.Serialize(hotkeysData, new JsonSerializerOptions { WriteIndented = true });
                await Services.FileAccessQueue.WriteAllTextAsync(hotkeysJsonPath, newJson);
                
                Logger.LogHotkeyFinder($"Updated INI path in hotkeys.json for: {description}");
            }
            catch (Exception ex)
            {
                Logger.LogHotkeyFinderError($"Failed to update INI path in hotkeys.json", ex);
            }
        }
    }
}
