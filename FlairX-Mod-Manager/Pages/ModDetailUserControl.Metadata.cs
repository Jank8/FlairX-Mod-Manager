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

        private string? _modJsonPath;

        // Serializer options are immutable after first use; allocating them per save showed up
        // as repeated work on the mod-detail hot path.
        private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
        
        private void ModUrlTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Clear placeholder text styling when focused
            if (string.IsNullOrWhiteSpace(ModUrlTextBox.Text))
            {
                ModUrlTextBox.ClearValue(TextBox.ForegroundProperty); // Use default theme color
            }
        }
        
        private void ModUrlTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Restore placeholder styling if empty
            if (string.IsNullOrWhiteSpace(ModUrlTextBox.Text))
            {
                ModUrlTextBox.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray);
            }
        }

        
        private async void ModVersionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                await UpdateModJsonField("version", ModVersionTextBox.Text);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.ModVersionTextBox_TextChanged failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.ModVersionTextBox_TextChanged failed", ex);
            }
        }

        private async void ModAuthorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                await UpdateModJsonField("author", ModAuthorTextBox.Text);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.ModAuthorTextBox_TextChanged failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.ModAuthorTextBox_TextChanged failed", ex);
            }
        }

        private async void ModUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                // Don't save empty text or placeholder
                var urlValue = string.IsNullOrWhiteSpace(ModUrlTextBox.Text) ? "" : ModUrlTextBox.Text;
                await UpdateModJsonField("url", urlValue);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.ModUrlTextBox_TextChanged failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.ModUrlTextBox_TextChanged failed", ex);
            }
        }

        private async void ModDateCheckedPicker_DateChanged(object sender, Microsoft.UI.Xaml.Controls.CalendarDatePickerDateChangedEventArgs e)
        {
            try
            {
                var dateValue = e.NewDate?.ToString("yyyy-MM-dd") ?? "";
                await UpdateModJsonField("dateChecked", dateValue);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.ModDateCheckedPicker_DateChanged failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.ModDateCheckedPicker_DateChanged failed", ex);
            }
        }

        private async void ModDateUpdatedPicker_DateChanged(object sender, Microsoft.UI.Xaml.Controls.CalendarDatePickerDateChangedEventArgs e)
        {
            try
            {
                var dateValue = e.NewDate?.ToString("yyyy-MM-dd") ?? "";
                await UpdateModJsonField("dateUpdated", dateValue);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.ModDateUpdatedPicker_DateChanged failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.ModDateUpdatedPicker_DateChanged failed", ex);
            }
        }

        private async void ModNSFWCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                await UpdateModJsonFieldBool("isNSFW", true);
                NSFWBadge.Visibility = Visibility.Visible;
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.ModNSFWCheckBox_Checked failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.ModNSFWCheckBox_Checked failed", ex);
            }
        }

        private async void ModNSFWCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                await UpdateModJsonFieldBool("isNSFW", false);
                NSFWBadge.Visibility = Visibility.Collapsed;
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.ModNSFWCheckBox_Unchecked failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.ModNSFWCheckBox_Unchecked failed", ex);
            }
        }

        private async void ModStatusKeeperSyncCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                await UpdateModJsonFieldBool("statusKeeperSync", true);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.ModStatusKeeperSyncCheckBox_Checked failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.ModStatusKeeperSyncCheckBox_Checked failed", ex);
            }
        }

        private async void ModStatusKeeperSyncCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                await UpdateModJsonFieldBool("statusKeeperSync", false);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.ModStatusKeeperSyncCheckBox_Unchecked failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.ModStatusKeeperSyncCheckBox_Unchecked failed", ex);
            }
        }

        private async void ModBrokenCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.LogInfo("ModBrokenCheckBox_Checked called");
                await UpdateModJsonFieldBool("modBroken", true);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.ModBrokenCheckBox_Checked failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.ModBrokenCheckBox_Checked failed", ex);
            }
        }

        private async void ModBrokenCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.LogInfo("ModBrokenCheckBox_Unchecked called");
                await UpdateModJsonFieldBool("modBroken", false);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.ModBrokenCheckBox_Unchecked failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.ModBrokenCheckBox_Unchecked failed", ex);
            }
        }

        private void OpenUrlButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = ModUrlTextBox.Text;
                if (!string.IsNullOrEmpty(url))
                {
                    // Check if it's a GameBanana URL
                    if (url.Contains("gamebanana.com", StringComparison.OrdinalIgnoreCase))
                    {
                        // Get game tag from current game
                        var gameTag = SettingsManager.CurrentSelectedGame;
                        if (!string.IsNullOrEmpty(gameTag))
                        {
                            // Open GameBanana browser with mod URL and current mod path
                            var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                            mainWindow?.ShowGameBananaBrowserPanel(gameTag, url, _currentModDirectory);
                        }
                        else
                        {
                            Logger.LogWarning("No game tag found, cannot open GameBanana browser");
                        }
                    }
                    else
                    {
                        // Open in external browser for non-GameBanana URLs
                        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                            url = "https://" + url;
                        
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error opening URL", ex);
            }
        }

        private void TodayDateCheckedButton_Click(object sender, RoutedEventArgs e)
        {
            ModDateCheckedPicker.Date = DateTimeOffset.Now;
        }

        private void TodayDateUpdatedButton_Click(object sender, RoutedEventArgs e)
        {
            ModDateUpdatedPicker.Date = DateTimeOffset.Now;
        }

        private void CheckForUpdates(JsonElement root)
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                
                // Get gbChangeDate and dateUpdated from mod.json
                string? gbChangeDate = root.TryGetProperty("gbChangeDate", out var gbChangeProp) ? gbChangeProp.GetString() : null;
                string? dateUpdated = root.TryGetProperty("dateUpdated", out var dateUpdatedProp) ? dateUpdatedProp.GetString() : null;

                // Check if both dates exist and gbChangeDate is newer than dateUpdated
                if (!string.IsNullOrWhiteSpace(gbChangeDate) && !string.IsNullOrWhiteSpace(dateUpdated))
                {
                    if (DateTime.TryParse(gbChangeDate, out var gbDate) && DateTime.TryParse(dateUpdated, out var updatedDate))
                    {
                        if (gbDate > updatedDate)
                        {
                            // Show update available only if URL is valid
                            UpdateAvailableNotification.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_UpdateAvailable");
                            UpdateAvailableNotification.Visibility = Visibility.Visible;
                            return;
                        }
                    }
                }

                // Hide notification if no update available and URL is valid
                UpdateAvailableNotification.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error checking for updates", ex);
                UpdateAvailableNotification.Visibility = Visibility.Collapsed;
            }
        }

        private async Task UpdateModJsonField(string field, string value)
        {
            if (string.IsNullOrEmpty(_modJsonPath)) return;
            if (!File.Exists(_modJsonPath))
            {
                Logger.LogError($"mod.json does not exist at: {_modJsonPath}");
                return;
            }
            try
            {
                // Capture the path once: if the user opens another mod while the write is
                // queued, reading the field again after an await could write to that mod.
                var modJsonPath = _modJsonPath;
                string? newJson = null;
                await Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                {
                    var json = await File.ReadAllTextAsync(modJsonPath);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var dict = new Dictionary<string, object?>();
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Name == field)
                            dict[field] = value;
                        else if (prop.Name == "author" || prop.Name == "url" || prop.Name == "version" || prop.Name == "dateChecked" || prop.Name == "dateUpdated")
                            dict[prop.Name] = prop.Value.GetString();
                        else
                            dict[prop.Name] = prop.Value.Deserialize<object>();
                    }
                    if (!dict.ContainsKey(field))
                        dict[field] = value;
                    newJson = JsonSerializer.Serialize(dict, IndentedJsonOptions);

                    // Atomic: a truncated mod.json is later "repaired" with defaults by the
                    // startup scanner, which silently drops the mod's metadata.
                    await Services.FileAccessQueue.WriteAllTextUnlockedAsync(modJsonPath, newJson);
                });
                
                // Re-check for updates after saving dateUpdated
                if (field == "dateUpdated" && newJson != null)
                {
                    var updatedDoc = JsonDocument.Parse(newJson);
                    CheckForUpdates(updatedDoc.RootElement);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save mod details", ex);

                // Silent before: the field on screen showed the new value, but nothing was saved.
                if (App.Current is App saveApp && saveApp.MainWindow is MainWindow saveWindow)
                {
                    var saveLang = SharedUtilities.LoadLanguageDictionary();
                    saveWindow.ShowErrorInfo(SharedUtilities.GetTranslation(saveLang, "Notify_ModSaveFailed"));
                }
            }
        }

        private async Task UpdateModJsonFieldBool(string field, bool value)
        {
            if (string.IsNullOrEmpty(_modJsonPath)) return;
            if (!File.Exists(_modJsonPath))
            {
                Logger.LogError($"mod.json does not exist at: {_modJsonPath}");
                return;
            }
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
                        if (prop.Name == field)
                            dict[field] = value;
                        else if (prop.Name == "author" || prop.Name == "url" || prop.Name == "version" || prop.Name == "dateChecked" || prop.Name == "dateUpdated")
                            dict[prop.Name] = prop.Value.GetString();
                        else if (prop.Name == "isNSFW" || prop.Name == "statusKeeperSync" || prop.Name == "modBroken")
                            dict[prop.Name] = prop.Value.GetBoolean();
                        else
                            dict[prop.Name] = prop.Value.Deserialize<object>();
                    }
                    if (!dict.ContainsKey(field))
                        dict[field] = value;
                    var newJson = JsonSerializer.Serialize(dict, IndentedJsonOptions);

                    // Atomic: a truncated mod.json is later "repaired" with defaults by the
                    // startup scanner, which silently drops the mod's metadata.
                    await Services.FileAccessQueue.WriteAllTextUnlockedAsync(_modJsonPath, newJson);
                });
                
                // Update persistent lists when NSFW or Broken status changes
                if (field == "isNSFW" || field == "modBroken")
                {
                    try
                    {
                        var modDirName = Path.GetFileName(Path.GetDirectoryName(_modJsonPath));
                        if (!string.IsNullOrEmpty(modDirName))
                        {
                            var cleanName = modDirName.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase) 
                                ? modDirName.Substring(9) 
                                : modDirName;
                            
                            // Extract category from path (parent folder of mod)
                            var categoryName = "Other";
                            try
                            {
                                if (!string.IsNullOrEmpty(_currentModDirectory))
                                {
                                    var modParent = Directory.GetParent(_currentModDirectory);
                                    if (modParent != null)
                                    {
                                        categoryName = modParent.Name;
                                    }
                                }
                            }
                            catch { }
                            
                            if (field == "isNSFW")
                            {
                                if (value)
                                    ModListManager.AddToNSFWList(categoryName, cleanName);
                                else
                                    ModListManager.RemoveFromNSFWList(categoryName, cleanName);
                                    
                                Logger.LogInfo($"Updated NSFW list for mod: {categoryName}|{cleanName} (isNSFW: {value})");
                            }
                            else if (field == "modBroken")
                            {
                                if (value)
                                    ModListManager.AddToBrokenList(categoryName, cleanName);
                                else
                                    ModListManager.RemoveFromBrokenList(categoryName, cleanName);
                                    
                                Logger.LogInfo($"Updated Broken list for mod: {categoryName}|{cleanName} (modBroken: {value})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to update persistent lists for field: {field}", ex);
                    }
                }
                
                // Trigger NSFW filtering if enabled
                if (field == "isNSFW")
                {
                    var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                    if (mainWindow?.CurrentModGridPage != null)
                    {
                        mainWindow.CurrentModGridPage.FilterNSFWMods(SettingsManager.Current.HideNSFWMods);
                    }
                }
                
                // Trigger tile refresh for modBroken field
                if (field == "modBroken")
                {
                    Logger.LogInfo($"Triggering tile refresh for modBroken: {_currentModDirectory} -> {value}");
                    var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                    if (mainWindow?.CurrentModGridPage != null && !string.IsNullOrEmpty(_currentModDirectory))
                    {
                        // Extract just the folder name from full path
                        var modDirectoryName = Path.GetFileName(_currentModDirectory);
                        Logger.LogInfo($"Extracted folder name: '{modDirectoryName}' from full path: '{_currentModDirectory}'");
                        Logger.LogInfo($"Calling RefreshModTile for: {modDirectoryName} (from full path: {_currentModDirectory})");
                        mainWindow.CurrentModGridPage.RefreshModTile(modDirectoryName, value);
                    }
                    else
                    {
                        Logger.LogWarning($"Cannot refresh tile - mainWindow: {mainWindow != null}, CurrentModGridPage: {mainWindow?.CurrentModGridPage != null}, _currentModDirectory: '{_currentModDirectory}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save mod details", ex);

                // Silent before: the field on screen showed the new value, but nothing was saved.
                if (App.Current is App saveApp && saveApp.MainWindow is MainWindow saveWindow)
                {
                    var saveLang = SharedUtilities.LoadLanguageDictionary();
                    saveWindow.ShowErrorInfo(SharedUtilities.GetTranslation(saveLang, "Notify_ModSaveFailed"));
                }
            }
        }
    }
}
