using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModDetailUserControl
    {
        
        private async void RestoreDefaultButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                // Block if in edit mode or recording gamepad
                if (_activeHotkeyEditBox != null || _isRecordingGamepad)
                    return;

                if (sender is Border restoreBorder && restoreBorder.Tag is HotkeyButtonData data)
                {
                    try
                    {
                        // Load default hotkey from defaultHotkeys section in mod.json
                        var defaultHotkey = await GetDefaultHotkeyForDescription(data.Description);
                        if (!string.IsNullOrEmpty(defaultHotkey))
                        {
                            var parent = restoreBorder.Parent as StackPanel;
                            if (parent == null) return;

                            var grid = parent.Parent as Grid;
                            if (grid == null) return;

                            // Update the keys panel with default hotkey
                            var keyBackground = (Brush)Application.Current.Resources["HotkeyKeyBackground"];
                            var keysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(defaultHotkey, keyBackground, 64);

                            // Find and replace existing keys panel
                            var existingKeysPanel = grid.Children.OfType<StackPanel>().FirstOrDefault();
                            if (existingKeysPanel != null)
                            {
                                int idx = grid.Children.IndexOf(existingKeysPanel);
                                Grid.SetColumn(keysPanel, 0);
                                grid.Children.RemoveAt(idx);
                                grid.Children.Insert(idx, keysPanel);
                            }

                            // Update data
                            data.KeyCombo = defaultHotkey;

                            // Update row border tag
                            var rowBorder = grid.Parent as Border;
                            if (rowBorder?.Tag is HotkeyRowData rowData)
                            {
                                rowData.Key = defaultHotkey;
                            }

                            // Update all button tags in this row
                            foreach (var btn in parent.Children.OfType<Border>())
                            {
                                if (btn.Tag is HotkeyButtonData btnData)
                                    btnData.KeyCombo = defaultHotkey;
                            }

                            // Save to mod.json
                            await SaveHotkeyChange(data.OriginalIndex, defaultHotkey, data.Description);

                            Logger.LogInfo($"Restored default hotkey: {defaultHotkey} - {data.Description}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to restore default hotkey", ex);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.RestoreDefaultButton_PointerPressed failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.RestoreDefaultButton_PointerPressed failed", ex);
            }
        }

        private async void SaveButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                Logger.LogInfo("SaveButton_PointerPressed called");
                StopGamepadListening();

                if (sender is Border saveBorder && saveBorder.Tag is HotkeyButtonData data)
                {
                    var parent = saveBorder.Parent as StackPanel;
                    if (parent == null) return;

                    var grid = parent.Parent as Grid;
                    if (grid == null) return;

                    // Find edit container (StackPanel with Tag "EditContainer") or TextBox directly
                    TextBox? editBox = null;
                    UIElement? elementToReplace = null;

                    var editContainer = grid.Children.OfType<StackPanel>().FirstOrDefault(sp => sp.Tag as string == "EditContainer");
                    if (editContainer != null)
                    {
                        editBox = editContainer.Children.OfType<TextBox>().FirstOrDefault(t => t.Tag as string == "HotkeyEditBox");
                        elementToReplace = editContainer;
                    }
                    else
                    {
                        editBox = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Tag as string == "HotkeyEditBox");
                        elementToReplace = editBox;
                    }

                    if (editBox == null || elementToReplace == null) return;

                    // Clean up combo text
                    string newKeyCombo = editBox.Text.Trim().ToUpper();
                    var countdownMatch = System.Text.RegularExpressions.Regex.Match(newKeyCombo, @"\s*\(\d+S\)$");
                    if (countdownMatch.Success)
                        newKeyCombo = newKeyCombo.Substring(0, countdownMatch.Index).Trim();
                    if (newKeyCombo.EndsWith("(RELEASED)"))
                        newKeyCombo = newKeyCombo.Replace("(RELEASED)", "").Trim();
                    if (string.IsNullOrEmpty(newKeyCombo)) newKeyCombo = data.KeyCombo;

                    // Update data
                    data.KeyCombo = newKeyCombo;

                    // Replace edit container/TextBox with keys panel
                    var keyBackground = (Brush)Application.Current.Resources["HotkeyKeyBackground"];
                    var keysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(newKeyCombo, keyBackground, 64);

                    int idx = grid.Children.IndexOf(elementToReplace);
                    Grid.SetColumn(keysPanel, 0);
                    grid.Children.RemoveAt(idx);
                    grid.Children.Insert(idx, keysPanel);

                    // Clear countdown reference
                    _countdownTextBlock = null;

                    // Update row border tag
                    var rowBorder = grid.Parent as Border;
                    if (rowBorder?.Tag is HotkeyRowData rowData)
                    {
                        rowData.Key = newKeyCombo;
                    }

                    // Update all button tags
                    foreach (var btn in parent.Children.OfType<Border>())
                    {
                        if (btn.Tag is HotkeyButtonData btnData)
                            btnData.KeyCombo = newKeyCombo;
                    }

                    // Hide save, show edit (buttons: fav[0], edit[1], rec[2], save[3], restore[4]) - rec and restore stay visible
                    saveBorder.Visibility = Visibility.Collapsed;
                    if (parent.Children.Count > 1 && parent.Children[1] is Border editBtn)
                        editBtn.Visibility = Visibility.Visible;

                    // Save to mod.json
                    await SaveHotkeyChange(data.OriginalIndex, newKeyCombo, data.Description);

                    // Reset active edit box to unblock other buttons
                    _activeHotkeyEditBox = null;

                    // Re-enable restore all button
                    RestoreDefaultHotkeysButton.IsEnabled = true;
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.SaveButton_PointerPressed failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.SaveButton_PointerPressed failed", ex);
            }
        }
        
        private async Task SaveHotkeyChange(int index, string newKey, string description)
        {
            if (string.IsNullOrEmpty(_currentModDirectory)) return;
            
            Logger.LogInfo($"SaveHotkeyChange called: index={index}, newKey={newKey}, description={description}, modDir={PathManager.GetRelativePath(_currentModDirectory)}");
            
            try
            {
                var hotkeysJsonPath = Path.Combine(_currentModDirectory, "hotkeys.json");
                
                if (!File.Exists(hotkeysJsonPath))
                {
                    Logger.LogWarning($"Hotkeys file not found: {hotkeysJsonPath}");
                    return;
                }
                
                var json = await Services.FileAccessQueue.ReadAllTextAsync(hotkeysJsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                var defaultHotkeys = new List<Dictionary<string, object>>();
                var hotkeys = new List<Dictionary<string, object>>();
                
                // Preserve default hotkeys with all properties
                if (root.TryGetProperty("defaultHotkeys", out var defaultProp) && defaultProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in defaultProp.EnumerateArray())
                    {
                        var hotkey = new Dictionary<string, object>
                        {
                            ["key"] = h.GetProperty("key").GetString() ?? "",
                            ["description"] = h.GetProperty("description").GetString() ?? ""
                        };
                        
                        // Preserve additional properties if they exist
                        if (h.TryGetProperty("iniFile", out var iniFileProp))
                            hotkey["iniFile"] = iniFileProp.GetString() ?? "";
                        if (h.TryGetProperty("section", out var sectionProp))
                            hotkey["section"] = sectionProp.GetString() ?? "";
                        if (h.TryGetProperty("keyName", out var keyNameProp))
                            hotkey["keyName"] = keyNameProp.GetString() ?? "";
                        if (h.TryGetProperty("originalValue", out var originalValueProp))
                            hotkey["originalValue"] = originalValueProp.GetString() ?? "";
                            
                        defaultHotkeys.Add(hotkey);
                    }
                }
                
                // Update current hotkeys with all properties
                if (root.TryGetProperty("hotkeys", out var hotkeysProp) && hotkeysProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in hotkeysProp.EnumerateArray())
                    {
                        var hotkey = new Dictionary<string, object>
                        {
                            ["key"] = h.GetProperty("key").GetString() ?? "",
                            ["description"] = h.GetProperty("description").GetString() ?? ""
                        };
                        
                        // Preserve additional properties if they exist
                        if (h.TryGetProperty("iniFile", out var iniFileProp))
                            hotkey["iniFile"] = iniFileProp.GetString() ?? "";
                        if (h.TryGetProperty("section", out var sectionProp))
                            hotkey["section"] = sectionProp.GetString() ?? "";
                        if (h.TryGetProperty("keyName", out var keyNameProp))
                            hotkey["keyName"] = keyNameProp.GetString() ?? "";
                        if (h.TryGetProperty("originalValue", out var originalValueProp))
                            hotkey["originalValue"] = originalValueProp.GetString() ?? "";
                            
                        hotkeys.Add(hotkey);
                    }
                }
                
                // Update the specific hotkey
                if (index >= 0 && index < hotkeys.Count)
                {
                    hotkeys[index]["key"] = newKey;
                }
                
                // Save updated hotkeys.json using FileAccessQueue
                await Services.FileAccessQueue.ExecuteAsync(hotkeysJsonPath, async () =>
                {
                    var hotkeysData = new
                    {
                        hotkeys = hotkeys,
                        defaultHotkeys = defaultHotkeys
                    };
                    
                    var newJson = JsonSerializer.Serialize(hotkeysData, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(hotkeysJsonPath, newJson);
                    Logger.LogInfo($"Hotkey updated in hotkeys.json: {newKey} - {description}");
                    Logger.LogInfo($"Updated hotkeys.json content written to: {PathManager.GetRelativePath(hotkeysJsonPath)}");
                });
                
                // Also save the change back to the original INI file
                await HotkeyFinderPage.SaveHotkeyToIniAsync(_currentModDirectory, description, newKey);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save hotkey change to hotkeys.json", ex);
            }
        }


        private async Task LoadHotkeysFromFile()
        {
            try
            {
                ModHotkeysPanel.Children.Clear();
                _originalHotkeyOrder.Clear();
                
                if (string.IsNullOrEmpty(_currentModDirectory))
                {
                    HotkeysSection.Visibility = Visibility.Collapsed;
                    return;
                }
                
                var hotkeysJsonPath = Path.Combine(_currentModDirectory, "hotkeys.json");
                
                if (!File.Exists(hotkeysJsonPath))
                {
                    HotkeysSection.Visibility = Visibility.Collapsed;
                    return;
                }
                
                var json = await Services.FileAccessQueue.ReadAllTextAsync(hotkeysJsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                var hotkeyList = new List<(string key, string desc, int originalIndex, string iniFile)>();
                
                if (root.TryGetProperty("hotkeys", out var hotkeysProp) && hotkeysProp.ValueKind == JsonValueKind.Array)
                {
                    int index = 0;
                    foreach (var hotkey in hotkeysProp.EnumerateArray())
                    {
                        var key = hotkey.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : null;
                        var desc = hotkey.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
                        var iniFile = hotkey.TryGetProperty("iniFile", out var iniProp) ? iniProp.GetString() ?? "" : "";
                        
                        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(desc))
                        {
                            hotkeyList.Add((key, desc, index, iniFile));
                            _originalHotkeyOrder.Add(key);
                        }
                        index++;
                    }
                }
                
                if (hotkeyList.Count > 0)
                {
                    // Sort: favorites first, then by original order
                    hotkeyList = hotkeyList
                        .OrderByDescending(x => _favoriteHotkeys.Contains(x.key))
                        .ThenBy(x => x.originalIndex)
                        .ToList();

                    // Determine sub-mod group from iniFile path (first path segment = sub-mod folder)
                    string GetSubModGroup(string iniFile)
                    {
                        if (string.IsNullOrEmpty(iniFile)) return "";
                        var parts = iniFile.Replace('\\', '/').Split('/');
                        return parts.Length > 1 ? parts[0] : "";
                    }

                    var groups = hotkeyList.GroupBy(x => GetSubModGroup(x.iniFile)).ToList();
                    bool showHeaders = groups.Count > 1 || (groups.Count == 1 && !string.IsNullOrEmpty(groups[0].Key));

                    foreach (var group in groups)
                    {
                        // Add group header if there are multiple sub-mods
                        if (showHeaders && !string.IsNullOrEmpty(group.Key))
                        {
                            var header = new TextBlock
                            {
                                Text = group.Key,
                                FontSize = 13,
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                                Opacity = 0.6,
                                Margin = new Thickness(4, 8, 0, 4)
                            };
                            ModHotkeysPanel.Children.Add(header);
                        }

                        foreach (var (key, desc, origIdx, _) in group)
                        {
                            var hotkeyRow = CreateHotkeyRow(key, desc, origIdx);
                            ModHotkeysPanel.Children.Add(hotkeyRow);
                        }
                    }
                    
                    HotkeysSection.Visibility = Visibility.Visible;
                }
                else
                {
                    HotkeysSection.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load hotkeys from file", ex);
                HotkeysSection.Visibility = Visibility.Collapsed;
            }
        }





        private async Task<string?> GetDefaultHotkeyForDescription(string description)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentModDirectory))
                    return null;
                
                var hotkeysJsonPath = Path.Combine(_currentModDirectory, "hotkeys.json");
                if (!File.Exists(hotkeysJsonPath))
                    return null;
                
                var json = await Services.FileAccessQueue.ReadAllTextAsync(hotkeysJsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("defaultHotkeys", out var defaultHotkeysArray))
                {
                    foreach (var hotkey in defaultHotkeysArray.EnumerateArray())
                    {
                        var hotkeyDesc = hotkey.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
                        var hotkeyKey = hotkey.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : null;
                        
                        if (!string.IsNullOrEmpty(hotkeyDesc) && !string.IsNullOrEmpty(hotkeyKey) && 
                            hotkeyDesc.Equals(description, StringComparison.OrdinalIgnoreCase))
                        {
                            return hotkeyKey;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to get default hotkey for description", ex);
            }
            
            return null;
        }







        private async void RestoreDefaultHotkeysButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Block if any hotkey is being edited
                if (_activeHotkeyEditBox != null || _isRecordingGamepad)
                    return;

                try
                {
                    if (string.IsNullOrEmpty(_currentModDirectory))
                    {
                        Logger.LogWarning("No current mod directory available");
                        return;
                    }

                    var hotkeysJsonPath = Path.Combine(_currentModDirectory, "hotkeys.json");
                    if (!File.Exists(hotkeysJsonPath))
                    {
                        Logger.LogWarning("No hotkeys.json file found");
                        return;
                    }

                    var json = await Services.FileAccessQueue.ReadAllTextAsync(hotkeysJsonPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("defaultHotkeys", out var defaultHotkeysArray))
                    {
                        Logger.LogWarning("No defaultHotkeys section found in hotkeys.json");
                        return;
                    }

                    // Create mapping of description to default key from hotkeys.json
                    var defaultMapping = new Dictionary<string, string>();
                    foreach (var hotkey in defaultHotkeysArray.EnumerateArray())
                    {
                        var desc = hotkey.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
                        var key = hotkey.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : null;

                        if (!string.IsNullOrEmpty(desc) && !string.IsNullOrEmpty(key))
                        {
                            defaultMapping[desc] = key;
                        }
                    }

                    // Update all hotkey rows
                    var keyBackground = (Brush)Application.Current.Resources["HotkeyKeyBackground"];
                    var updatedHotkeys = new List<Dictionary<string, string>>();

                    foreach (var child in ModHotkeysPanel.Children)
                    {
                        if (child is Border rowBorder && rowBorder.Tag is HotkeyRowData rowData)
                        {
                            if (defaultMapping.TryGetValue(rowData.Description, out var defaultKey))
                            {
                                // Find the grid inside the border
                                if (rowBorder.Child is Grid grid)
                                {
                                    // Update keys panel
                                    var existingKeysPanel = grid.Children.OfType<StackPanel>().FirstOrDefault();
                                    if (existingKeysPanel != null)
                                    {
                                        var newKeysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(defaultKey, keyBackground, 64);
                                        int idx = grid.Children.IndexOf(existingKeysPanel);
                                        Grid.SetColumn(newKeysPanel, 0);
                                        grid.Children.RemoveAt(idx);
                                        grid.Children.Insert(idx, newKeysPanel);
                                    }

                                    // Update row data
                                    rowData.Key = defaultKey;

                                    // Update button tags
                                    var buttonsPanel = grid.Children.OfType<StackPanel>().LastOrDefault();
                                    if (buttonsPanel != null)
                                    {
                                        foreach (var btn in buttonsPanel.Children.OfType<Border>())
                                        {
                                            if (btn.Tag is HotkeyButtonData btnData)
                                                btnData.KeyCombo = defaultKey;
                                        }
                                    }
                                }

                                // Add to updated hotkeys list for saving
                                updatedHotkeys.Add(new Dictionary<string, string>
                                {
                                    ["key"] = defaultKey,
                                    ["description"] = rowData.Description
                                });
                            }
                            else
                            {
                                // Keep existing hotkey if no default found
                                updatedHotkeys.Add(new Dictionary<string, string>
                                {
                                    ["key"] = rowData.Key,
                                    ["description"] = rowData.Description
                                });
                            }
                        }
                    }

                    // Save all updated hotkeys to mod.json
                    await SaveAllHotkeys(updatedHotkeys);

                    Logger.LogInfo("Restored all default hotkeys");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to restore all default hotkeys", ex);
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.RestoreDefaultHotkeysButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.RestoreDefaultHotkeysButton_Click failed", ex);
            }
        }

        private async Task SaveAllHotkeys(List<Dictionary<string, string>> hotkeys)
        {
            if (string.IsNullOrEmpty(_currentModDirectory)) return;
            
            try
            {
                var hotkeysJsonPath = Path.Combine(_currentModDirectory, "hotkeys.json");
                if (!File.Exists(hotkeysJsonPath)) return;
                
                await Services.FileAccessQueue.ExecuteAsync(hotkeysJsonPath, async () =>
                {
                    var json = await File.ReadAllTextAsync(hotkeysJsonPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    var defaultHotkeys = new List<Dictionary<string, string>>();
                    
                    // Preserve default hotkeys
                    if (root.TryGetProperty("defaultHotkeys", out var defaultProp) && defaultProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var h in defaultProp.EnumerateArray())
                        {
                            defaultHotkeys.Add(new Dictionary<string, string>
                            {
                                ["key"] = h.GetProperty("key").GetString() ?? "",
                                ["description"] = h.GetProperty("description").GetString() ?? ""
                            });
                        }
                    }
                    
                    var hotkeysData = new
                    {
                        hotkeys = hotkeys,
                        defaultHotkeys = defaultHotkeys
                    };
                    
                    var newJson = JsonSerializer.Serialize(hotkeysData, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(hotkeysJsonPath, newJson);
                    Logger.LogInfo("All hotkeys saved to hotkeys.json");
                });
                
                // Also save all changes back to the original INI files
                foreach (var hotkey in hotkeys)
                {
                    try
                    {
                        await HotkeyFinderPage.SaveHotkeyToIniAsync(_currentModDirectory, hotkey["description"], hotkey["key"]);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to save hotkey to INI: {hotkey["description"]}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save all hotkeys to hotkeys.json", ex);
            }
        }

        private async Task SaveFavoriteHotkeys()
        {
            if (string.IsNullOrEmpty(_modJsonPath) || !File.Exists(_modJsonPath)) return;
            
            try
            {
                await Services.FileAccessQueue.ExecuteAsync(_modJsonPath, async () =>
                {
                    var json = await File.ReadAllTextAsync(_modJsonPath);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var dict = new Dictionary<string, object?>();
                    
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Name == "favoriteHotkeys")
                            continue; // Will be replaced
                        else if (prop.Name == "author" || prop.Name == "url" || prop.Name == "version" || 
                                 prop.Name == "dateChecked" || prop.Name == "dateUpdated")
                            dict[prop.Name] = prop.Value.GetString();
                        else if (prop.Name == "isNSFW" || prop.Name == "statusKeeperSync" || prop.Name == "modBroken")
                            dict[prop.Name] = prop.Value.GetBoolean();
                        else
                            dict[prop.Name] = prop.Value.Deserialize<object>();
                    }
                    
                    // Add favorite hotkeys array
                    dict["favoriteHotkeys"] = _favoriteHotkeys.ToList();
                    
                    var newJson = JsonSerializer.Serialize(dict, IndentedJsonOptions);

                    // Atomic: a truncated mod.json is later "repaired" with defaults by the
                    // startup scanner, which silently drops the mod's metadata.
                    await Services.FileAccessQueue.WriteAllTextUnlockedAsync(_modJsonPath, newJson);
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save favorite hotkeys", ex);
            }
        }
    }
}
