using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT.Interop;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// MainWindow partial class - Hotkey handling functionality
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        // Win32 API for checking window focus
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        // Helper method to check if this window is currently in focus
        private bool IsWindowInFocus()
        {
            try
            {
                var currentWindowHandle = WindowNative.GetWindowHandle(this);
                var foregroundWindow = GetForegroundWindow();
                return currentWindowHandle == foregroundWindow;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error checking window focus state", ex);
                // If we can't determine focus state, assume it's in focus to show dialogs
                return true;
            }
        }
        // Global keyboard handler for hotkeys
        private async void MainWindow_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            try
            {
                // Only handle hotkeys if they are enabled and a game is selected
                if (!SettingsManager.Current.HotkeysEnabled || SettingsManager.Current.SelectedGameIndex <= 0)
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

                // Shuffle active mods hotkey
                if (!string.IsNullOrEmpty(settings.ShuffleActiveModsHotkey) && 
                    string.Equals(currentHotkey, settings.ShuffleActiveModsHotkey, StringComparison.OrdinalIgnoreCase))
                {
                    e.Handled = true;
                    ExecuteShuffleActiveModsHotkeyInFocus();
                    return;
                }

                // Deactivate all mods hotkey
                if (!string.IsNullOrEmpty(settings.DeactivateAllModsHotkey) && 
                    string.Equals(currentHotkey, settings.DeactivateAllModsHotkey, StringComparison.OrdinalIgnoreCase))
                {
                    e.Handled = true;
                    ExecuteDeactivateAllModsHotkeyInFocus();
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in global hotkey handler", ex);
            }
        }

        // Execute optimize previews hotkey action
        public async Task ExecuteOptimizePreviewsHotkey()
        {
            try
            {
                Logger.LogInfo("Optimize previews hotkey triggered");
                
                // Check if we're currently on the settings page
                if (contentFrame.Content is FlairX_Mod_Manager.Pages.SettingsUserControl settingsControl)
                {
                    // If we're on settings page, trigger the button click to show progress bar
                    Logger.LogInfo("On settings page - triggering button click with UI");
                    await settingsControl.ExecuteOptimizePreviewsWithUI();
                }
                else
                {
                    // Check if window is in focus to decide whether to show notifications
                    bool isWindowInFocus = IsWindowInFocus();
                    
                    if (isWindowInFocus)
                    {
                        // If we're not on settings page but window is in focus, show progress indication
                        Logger.LogInfo("Not on settings page but window in focus - showing progress indication");
                        
                        // Show info that optimization started
                        var lang = SharedUtilities.LoadLanguageDictionary();
                        ShowSuccessInfo(SharedUtilities.GetTranslation(lang, "OptimizePreviews_Confirm_Title") + " - " + 
                                      SharedUtilities.GetTranslation(lang, "Continue"), 2000);
                        
                        // Run optimize previews directly
                        await FlairX_Mod_Manager.Pages.SettingsUserControl.OptimizePreviewsDirectAsync();
                        
                        // Show completion message
                        ShowSuccessInfo(SharedUtilities.GetTranslation(lang, "OptimizePreviews_Completed"), 3000);
                    }
                    else
                    {
                        // Window not in focus - run silently without notifications
                        Logger.LogInfo("Window not in focus - running optimize previews silently");
                        await FlairX_Mod_Manager.Pages.SettingsUserControl.OptimizePreviewsDirectAsync();
                    }
                }
                
                Logger.LogInfo("Optimize previews hotkey completed");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error executing optimize previews hotkey", ex);
                
                // Only show error notification if window is in focus
                if (IsWindowInFocus())
                {
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    ShowErrorInfo(SharedUtilities.GetTranslation(lang, "Error_Generic"), 3000);
                }
            }
        }

        // Execute reload manager hotkey action
        public async Task ExecuteReloadManagerHotkey()
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
        public async void ExecuteShuffleActiveModsHotkey()
        {
            await ExecuteShuffleActiveModsHotkeyInternal();
        }

        // Execute shuffle active mods hotkey action (for in-focus hotkeys)
        public async void ExecuteShuffleActiveModsHotkeyInFocus()
        {
            Logger.LogInfo("ExecuteShuffleActiveModsHotkeyInFocus called");
            await ExecuteShuffleActiveModsHotkeyInternal();
        }

        // Internal method for shuffle active mods hotkey
        private async Task ExecuteShuffleActiveModsHotkeyInternal()
        {
            try
            {
                Logger.LogInfo("Shuffle active mods hotkey triggered");
                
                // Get XXMI mods path
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrWhiteSpace(modsPath) || !Directory.Exists(modsPath))
                {
                    Logger.LogError("XXMI mods directory does not exist");
                    return;
                }
                
                // Get all categories except "Other"
                var categories = Directory.GetDirectories(modsPath)
                    .Where(categoryDir => !string.Equals(Path.GetFileName(categoryDir), "Other", StringComparison.OrdinalIgnoreCase))
                    .Where(Directory.Exists)
                    .ToList();
                
                if (categories.Count == 0)
                {
                    Logger.LogInfo("No categories found for shuffling");
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
                    
                    // No symlink recreation needed - using DISABLED_ prefix system
                    
                    Logger.LogInfo($"Shuffle completed - activated {selectedMods.Count} random mods");
                    
                    // Reload manager to refresh the view
                    Logger.LogInfo("About to call ReloadModsAsync for shuffle");
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await ReloadModsAsync();
                        Logger.LogInfo("ReloadModsAsync completed for shuffle");
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to save shuffled active mods", ex);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error executing shuffle active mods hotkey", ex);
            }
            
            await Task.CompletedTask;
        }

        // Execute deactivate all mods hotkey action
        public async void ExecuteDeactivateAllModsHotkey()
        {
            await ExecuteDeactivateAllModsHotkeyInternal();
        }

        // Execute deactivate all mods hotkey action (for in-focus hotkeys)
        public async void ExecuteDeactivateAllModsHotkeyInFocus()
        {
            Logger.LogInfo("ExecuteDeactivateAllModsHotkeyInFocus called");
            await ExecuteDeactivateAllModsHotkeyInternal();
        }

        // Internal method for deactivate all mods hotkey
        private async Task ExecuteDeactivateAllModsHotkeyInternal()
        {
            try
            {
                Logger.LogInfo("Deactivate all mods hotkey triggered");
                
                // Get XXMI mods path
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrWhiteSpace(modsPath) || !Directory.Exists(modsPath))
                {
                    Logger.LogError("XXMI mods directory does not exist");
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
                            var otherCategoryPath = Path.Combine(modsPath, "Other");
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
                    
                    // No symlink recreation needed - using DISABLED_ prefix system
                    
                    Logger.LogInfo($"Deactivate all completed - deactivated {deactivatedMods.Count} mods (excluding Other category)");
                    
                    // Reload manager to refresh the view
                    Logger.LogInfo("About to call ReloadModsAsync for deactivate");
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await ReloadModsAsync();
                        Logger.LogInfo("ReloadModsAsync completed for deactivate");
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to save deactivated mods configuration", ex);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error executing deactivate all mods hotkey", ex);
            }
            
            await Task.CompletedTask;
        }
    }
}
