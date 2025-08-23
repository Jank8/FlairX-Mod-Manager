using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// MainWindow partial class - Hotkey handling functionality
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        // Global keyboard handler for hotkeys
        private async void MainWindow_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            try
            {
                // Only handle hotkeys if a game is selected
                if (SettingsManager.Current.SelectedGameIndex <= 0)
                    return;

                // Get current modifier keys
                var modifiers = new List<string>();
                if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    modifiers.Add("Ctrl");
                if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    modifiers.Add("Shift");
                if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    modifiers.Add("Alt");

                // Build current hotkey string
                var currentHotkey = string.Join("+", modifiers.Concat(new[] { e.Key.ToString() }));

                // Check if current hotkey matches any configured hotkey
                var settings = SettingsManager.Current;

                // Optimize previews hotkey
                if (!string.IsNullOrEmpty(settings.OptimizePreviewsHotkey) && 
                    string.Equals(currentHotkey, settings.OptimizePreviewsHotkey, StringComparison.OrdinalIgnoreCase))
                {
                    e.Handled = true;
                    await ExecuteOptimizePreviewsHotkey();
                    return;
                }

                // Reload manager hotkey
                if (!string.IsNullOrEmpty(settings.ReloadManagerHotkey) && 
                    string.Equals(currentHotkey, settings.ReloadManagerHotkey, StringComparison.OrdinalIgnoreCase))
                {
                    e.Handled = true;
                    await ExecuteReloadManagerHotkey();
                    return;
                }

                // Shuffle active mods hotkey (placeholder for future implementation)
                if (!string.IsNullOrEmpty(settings.ShuffleActiveModsHotkey) && 
                    string.Equals(currentHotkey, settings.ShuffleActiveModsHotkey, StringComparison.OrdinalIgnoreCase))
                {
                    e.Handled = true;
                    ExecuteShuffleActiveModsHotkey();
                    return;
                }

                // Deactivate all mods hotkey
                if (!string.IsNullOrEmpty(settings.DeactivateAllModsHotkey) && 
                    string.Equals(currentHotkey, settings.DeactivateAllModsHotkey, StringComparison.OrdinalIgnoreCase))
                {
                    e.Handled = true;
                    ExecuteDeactivateAllModsHotkey();
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in global hotkey handler", ex);
            }
        }

        // Execute optimize previews hotkey action
        private async Task ExecuteOptimizePreviewsHotkey()
        {
            try
            {
                Logger.LogInfo("Optimize previews hotkey triggered");
                
                // Navigate to settings page if not already there
                if (!(contentFrame.Content is FlairX_Mod_Manager.Pages.SettingsPage))
                {
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.SettingsPage), null, new DrillInNavigationTransitionInfo());
                    // Wait a moment for navigation to complete
                    await Task.Delay(100);
                }

                // Get the settings page and trigger optimize previews
                if (contentFrame.Content is FlairX_Mod_Manager.Pages.SettingsPage settingsPage)
                {
                    // Use reflection to call the private OptimizePreviewsButton_Click method
                    var optimizeMethod = settingsPage.GetType().GetMethod("OptimizePreviewsButton_Click", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (optimizeMethod != null)
                    {
                        optimizeMethod.Invoke(settingsPage, new object[] { settingsPage, new RoutedEventArgs() });
                        Logger.LogInfo("Optimize previews started via hotkey");
                    }
                    else
                    {
                        Logger.LogError("Could not find OptimizePreviewsButton_Click method");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error executing optimize previews hotkey", ex);
            }
        }

        // Execute reload manager hotkey action
        private async Task ExecuteReloadManagerHotkey()
        {
            try
            {
                Logger.LogInfo("Reload manager hotkey triggered");
                await ReloadModsAsync();
                Logger.LogInfo("Reload manager completed via hotkey");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error executing reload manager hotkey", ex);
            }
        }

        // Execute shuffle active mods hotkey action
        private void ExecuteShuffleActiveModsHotkey()
        {
            try
            {
                Logger.LogInfo("Shuffle active mods hotkey triggered");
                
                // Get mod library path
                var modLibraryPath = SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrWhiteSpace(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                
                if (!Directory.Exists(modLibraryPath))
                {
                    Logger.LogError("Mod library directory does not exist");
                    return;
                }
                
                // Get all categories except "Other"
                var categories = Directory.GetDirectories(modLibraryPath)
                    .Where(categoryDir => !string.Equals(Path.GetFileName(categoryDir), "Other", StringComparison.OrdinalIgnoreCase))
                    .Where(Directory.Exists)
                    .ToList();
                
                if (categories.Count == 0)
                {
                    Logger.LogInfo("No categories found for shuffling");
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    var dialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "Information"),
                        Content = SharedUtilities.GetTranslation(lang, "ShuffleActiveMods_NoCategories"),
                        CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                        XamlRoot = this.Content.XamlRoot
                    };
                    _ = dialog.ShowAsync();
                    return;
                }
                
                var random = new Random();
                var newActiveMods = new Dictionary<string, bool>();
                var selectedMods = new List<string>();
                
                // Load current active mods to deactivate them
                var activeModsPath = PathManager.GetActiveModsPath();
                if (File.Exists(activeModsPath))
                {
                    try
                    {
                        var json = File.ReadAllText(activeModsPath);
                        var currentMods = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
                        // Set all current mods to inactive
                        foreach (var mod in currentMods.Keys)
                        {
                            newActiveMods[mod] = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to load current active mods for shuffling", ex);
                    }
                }
                
                // Select 1 random mod from each category (excluding "Other")
                foreach (var categoryDir in categories)
                {
                    var modDirs = Directory.GetDirectories(categoryDir)
                        .Where(modDir => File.Exists(Path.Combine(modDir, "mod.json")))
                        .ToList();
                    
                    if (modDirs.Count > 0)
                    {
                        // Select random mod from this category
                        var randomModDir = modDirs[random.Next(modDirs.Count)];
                        var modName = Path.GetFileName(randomModDir);
                        
                        newActiveMods[modName] = true;
                        selectedMods.Add($"{Path.GetFileName(categoryDir)}: {modName}");
                    }
                }
                
                // Save new active mods configuration
                try
                {
                    var json = JsonSerializer.Serialize(newActiveMods, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(activeModsPath, json);
                    
                    // Recreate symlinks
                    FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
                    
                    Logger.LogInfo($"Shuffle completed - activated {selectedMods.Count} random mods");
                    
                    // Show success dialog with selected mods
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    var selectedModsText = string.Join("\n", selectedMods);
                    var dialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "ShuffleActiveMods_Title"),
                        Content = string.Format(SharedUtilities.GetTranslation(lang, "ShuffleActiveMods_Message"), selectedMods.Count) + "\n\n" + selectedModsText,
                        CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                        XamlRoot = this.Content.XamlRoot
                    };
                    _ = dialog.ShowAsync();
                    
                    // Refresh the current view if we're on ModGridPage
                    if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                    {
                        // Force refresh to show new active states
                        modGridPage.RefreshUIAfterLanguageChange();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to save shuffled active mods", ex);
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    var errorDialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "Error_Title"),
                        Content = SharedUtilities.GetTranslation(lang, "Error_Generic"),
                        CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                        XamlRoot = this.Content.XamlRoot
                    };
                    _ = errorDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error executing shuffle active mods hotkey", ex);
            }
        }

        // Execute deactivate all mods hotkey action
        private void ExecuteDeactivateAllModsHotkey()
        {
            try
            {
                Logger.LogInfo("Deactivate all mods hotkey triggered");
                
                // Get mod library path
                var modLibraryPath = SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrWhiteSpace(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                
                if (!Directory.Exists(modLibraryPath))
                {
                    Logger.LogError("Mod library directory does not exist");
                    return;
                }
                
                // Load current active mods
                var activeModsPath = PathManager.GetActiveModsPath();
                var newActiveMods = new Dictionary<string, bool>();
                var deactivatedMods = new List<string>();
                
                if (File.Exists(activeModsPath))
                {
                    try
                    {
                        var json = File.ReadAllText(activeModsPath);
                        var currentMods = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
                        
                        // Deactivate all mods except those in "Other" category
                        foreach (var mod in currentMods.Keys)
                        {
                            // Check if mod is in "Other" category
                            bool isInOtherCategory = false;
                            var otherCategoryPath = Path.Combine(modLibraryPath, "Other");
                            if (Directory.Exists(otherCategoryPath))
                            {
                                var modPath = Path.Combine(otherCategoryPath, mod);
                                if (Directory.Exists(modPath))
                                {
                                    isInOtherCategory = true;
                                }
                            }
                            
                            if (isInOtherCategory)
                            {
                                // Keep "Other" category mods active if they were active
                                newActiveMods[mod] = currentMods.TryGetValue(mod, out var isActive) && isActive;
                            }
                            else
                            {
                                // Deactivate all other mods
                                if (currentMods.TryGetValue(mod, out var wasActive) && wasActive)
                                {
                                    deactivatedMods.Add(mod);
                                }
                                newActiveMods[mod] = false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to load current active mods for deactivation", ex);
                        return;
                    }
                }
                
                // Save new active mods configuration
                try
                {
                    var json = JsonSerializer.Serialize(newActiveMods, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(activeModsPath, json);
                    
                    // Recreate symlinks
                    FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
                    
                    Logger.LogInfo($"Deactivate all completed - deactivated {deactivatedMods.Count} mods (excluding Other category)");
                    
                    // Show success dialog
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    var dialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "DeactivateAllMods_Title"),
                        Content = string.Format(SharedUtilities.GetTranslation(lang, "DeactivateAllMods_Message"), deactivatedMods.Count),
                        CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                        XamlRoot = this.Content.XamlRoot
                    };
                    _ = dialog.ShowAsync();
                    
                    // Refresh the current view if we're on ModGridPage
                    if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                    {
                        // Force refresh to show new active states
                        modGridPage.RefreshUIAfterLanguageChange();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to save deactivated mods configuration", ex);
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    var errorDialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "Error_Title"),
                        Content = SharedUtilities.GetTranslation(lang, "Error_Generic"),
                        CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                        XamlRoot = this.Content.XamlRoot
                    };
                    _ = errorDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error executing deactivate all mods hotkey", ex);
            }
        }
    }
}