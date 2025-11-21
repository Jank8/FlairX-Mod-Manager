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
                
                var random = new Random();
                var newActiveMods = new Dictionary<string, bool>();
                var selectedMods = new List<string>();
                
                // Step 1: Deactivate ALL mods (except Other category) - like Python script
                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = Path.GetFileName(categoryDir);
                    
                    // Skip "Other" category
                    if (string.Equals(categoryName, "Other", StringComparison.OrdinalIgnoreCase))
                    {
                        // Keep Other category mods as they are
                        foreach (var modDir in Directory.GetDirectories(categoryDir))
                        {
                            var modFolderName = Path.GetFileName(modDir);
                            var cleanName = FlairX_Mod_Manager.Pages.ModGridPage.GetCleanModName(modFolderName);
                            bool isActive = !modFolderName.StartsWith("DISABLED_");
                            newActiveMods[cleanName] = isActive;
                        }
                        continue;
                    }
                    
                    // Deactivate all mods in this category
                    foreach (var modDir in Directory.GetDirectories(categoryDir))
                    {
                        var modFolderName = Path.GetFileName(modDir);
                        
                        // Skip if already disabled
                        if (modFolderName.StartsWith("DISABLED_"))
                        {
                            var cleanName = FlairX_Mod_Manager.Pages.ModGridPage.GetCleanModName(modFolderName);
                            newActiveMods[cleanName] = false;
                            continue;
                        }
                        
                        // Add DISABLED_ prefix
                        var newName = "DISABLED_" + modFolderName;
                        var newPath = Path.Combine(categoryDir, newName);
                        
                        try
                        {
                            Directory.Move(modDir, newPath);
                            newActiveMods[modFolderName] = false;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to deactivate {modFolderName}", ex);
                        }
                    }
                }
                
                // Step 2: Activate 1 random mod from each category (except Other)
                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = Path.GetFileName(categoryDir);
                    
                    // Skip "Other" category
                    if (string.Equals(categoryName, "Other", StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    // Get all disabled mods in this category
                    var disabledMods = Directory.GetDirectories(categoryDir)
                        .Where(modDir => Path.GetFileName(modDir).StartsWith("DISABLED_"))
                        .Where(modDir => File.Exists(Path.Combine(modDir, "mod.json")))
                        .ToList();
                    
                    if (disabledMods.Count > 0)
                    {
                        // Select random mod
                        var randomModDir = disabledMods[random.Next(disabledMods.Count)];
                        var modFolderName = Path.GetFileName(randomModDir);
                        var cleanName = modFolderName.Substring("DISABLED_".Length);
                        
                        // Remove DISABLED_ prefix
                        var newPath = Path.Combine(categoryDir, cleanName);
                        
                        try
                        {
                            Directory.Move(randomModDir, newPath);
                            newActiveMods[cleanName] = true;
                            selectedMods.Add($"{categoryName}: {cleanName}");
                            Logger.LogInfo($"Activated random mod: {cleanName}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to activate {cleanName}", ex);
                        }
                    }
                }
                
                // Save new active mods configuration
                try
                {
                    var activeModsPath = PathManager.GetActiveModsPath();
                    var json = JsonSerializer.Serialize(newActiveMods, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(activeModsPath, json);
                    
                    Logger.LogInfo($"Shuffle completed - activated {selectedMods.Count} random mods");
                    
                    // Reload manager to refresh the view
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await ReloadModsAsync();
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
                
                int deactivatedCount = 0;
                var newActiveMods = new Dictionary<string, bool>();
                
                // Iterate through all category directories (like Python script)
                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = Path.GetFileName(categoryDir);
                    
                    // Skip "Other" category
                    if (string.Equals(categoryName, "Other", StringComparison.OrdinalIgnoreCase))
                    {
                        // Keep Other category mods as they are - just record their current state
                        foreach (var modDir in Directory.GetDirectories(categoryDir))
                        {
                            var modFolderName = Path.GetFileName(modDir);
                            var cleanName = FlairX_Mod_Manager.Pages.ModGridPage.GetCleanModName(modFolderName);
                            bool isActive = !modFolderName.StartsWith("DISABLED_");
                            newActiveMods[cleanName] = isActive;
                        }
                        continue;
                    }
                    
                    // Deactivate all mods in other categories
                    foreach (var modDir in Directory.GetDirectories(categoryDir))
                    {
                        var modFolderName = Path.GetFileName(modDir);
                        
                        // Skip if already disabled
                        if (modFolderName.StartsWith("DISABLED_"))
                        {
                            var cleanName = FlairX_Mod_Manager.Pages.ModGridPage.GetCleanModName(modFolderName);
                            newActiveMods[cleanName] = false;
                            continue;
                        }
                        
                        // Add DISABLED_ prefix
                        var newName = "DISABLED_" + modFolderName;
                        var newPath = Path.Combine(categoryDir, newName);
                        
                        try
                        {
                            Directory.Move(modDir, newPath);
                            newActiveMods[modFolderName] = false;
                            deactivatedCount++;
                            Logger.LogInfo($"Deactivated: {modFolderName}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to deactivate {modFolderName}", ex);
                            newActiveMods[modFolderName] = true; // Keep as active if rename failed
                        }
                    }
                }
                
                // Save new active mods configuration
                try
                {
                    var activeModsPath = PathManager.GetActiveModsPath();
                    var json = JsonSerializer.Serialize(newActiveMods, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(activeModsPath, json);
                    
                    Logger.LogInfo($"Deactivate all completed - deactivated {deactivatedCount} mods (excluding Other category)");
                    
                    // Reload manager to refresh the view
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await ReloadModsAsync();
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
