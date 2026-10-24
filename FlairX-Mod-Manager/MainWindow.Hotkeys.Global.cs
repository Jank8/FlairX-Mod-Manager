using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT.Interop;

namespace FlairX_Mod_Manager
{
    public sealed partial class MainWindow
    {

        // Win32 API for checking window focus
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        // Win32 API for sending keyboard input
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        // Get extra info for input simulation (important for some games)
        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        // INPUT structure for SendInput - must match Windows API exactly
        // On 64-bit Windows, INPUT is 40 bytes: 4 (type) + 4 (padding) + 32 (union)
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        // Union must be the size of the largest member (MOUSEINPUT = 32 bytes on x64)
        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const ushort VK_F10 = 0x79;

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
                // Block main window hotkeys when overlay is visible — overlay has exclusive input
                if (_overlayWindow?.IsOverlayVisible == true) return;

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
                    await ExecuteShuffleActiveModsHotkeyInFocus();
                    return;
                }

                // Deactivate all mods hotkey
                if (!string.IsNullOrEmpty(settings.DeactivateAllModsHotkey) && 
                    string.Equals(currentHotkey, settings.DeactivateAllModsHotkey, StringComparison.OrdinalIgnoreCase))
                {
                    e.Handled = true;
                    await ExecuteDeactivateAllModsHotkeyInFocus();
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in global hotkey handler", ex);
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
        public async Task ExecuteShuffleActiveModsHotkey()
        {
            await ExecuteShuffleActiveModsHotkeyInternal();
        }

        // Execute shuffle active mods hotkey action (for in-focus hotkeys)
        public async Task ExecuteShuffleActiveModsHotkeyInFocus()
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

                    // The hotkey did nothing and gave no reason.
                    ShowWarningInfo(SharedUtilities.GetTranslation(
                        SharedUtilities.LoadLanguageDictionary(), "Notify_ModsFolderMissing"));

                    return;
                }
                
                // Get shuffle excluded categories
                var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                var excludedCategories = !string.IsNullOrEmpty(gameTag) 
                    ? SettingsManager.GetShuffleExcludedCategories(gameTag) 
                    : new List<string>();
                
                var activeModsList = new List<string>(); // Use List for "Category|ModName" format
                var selectedMods = new List<string>();
                
                // Step 1: Deactivate ALL mods (except excluded categories) - like Python script
                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = Path.GetFileName(categoryDir);
                    if (string.IsNullOrEmpty(categoryName)) continue;
                    
                    // Skip excluded categories
                    if (excludedCategories.Contains(categoryName, StringComparer.OrdinalIgnoreCase))
                    {
                        // Keep excluded category mods as they are
                        foreach (var modDir in Directory.GetDirectories(categoryDir))
                        {
                            var modFolderName = Path.GetFileName(modDir);
                            if (string.IsNullOrEmpty(modFolderName)) continue;
                            var cleanName = GetCleanModName(modFolderName);
                            bool isActive = !modFolderName.StartsWith("DISABLED_");
                            if (isActive)
                            {
                                activeModsList.Add($"{categoryName}|{cleanName}"); // Use "Category|ModName" format
                            }
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
                            continue;
                        }
                        
                        // Add DISABLED_ prefix
                        var newName = "DISABLED_" + modFolderName;
                        var newPath = Path.Combine(categoryDir, newName);
                        
                        try
                        {
                            Services.FileAccessQueue.MoveDirectory(modDir, newPath);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to deactivate {modFolderName}", ex);
                        }
                    }
                }
                
                // Step 2: Activate 1 random mod from each category (except excluded)
                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = Path.GetFileName(categoryDir);
                    if (string.IsNullOrEmpty(categoryName)) continue;
                    
                    // Skip excluded categories
                    if (excludedCategories.Contains(categoryName, StringComparer.OrdinalIgnoreCase))
                        continue;
                    
                    // Get all disabled mods in this category
                    var disabledMods = Directory.GetDirectories(categoryDir)
                        .Where(modDir => Path.GetFileName(modDir).StartsWith("DISABLED_"))
                        .Where(modDir => File.Exists(Path.Combine(modDir, "mod.json")))
                        .ToList();
                    
                    if (disabledMods.Count > 0)
                    {
                        // Select random mod
                        var randomModDir = disabledMods[Random.Shared.Next(disabledMods.Count)];
                        var modFolderName = Path.GetFileName(randomModDir);
                        var cleanName = modFolderName.Substring("DISABLED_".Length);
                        
                        // Remove DISABLED_ prefix
                        var newPath = Path.Combine(categoryDir, cleanName);
                        
                        try
                        {
                            Services.FileAccessQueue.MoveDirectory(randomModDir, newPath);
                            activeModsList.Add($"{categoryName}|{cleanName}"); // Use "Category|ModName" format
                            selectedMods.Add($"{categoryName}: {cleanName}");
                            Logger.LogInfo($"Activated random mod: {cleanName}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to activate {cleanName}", ex);
                        }
                    }
                }
                
                // Save new active mods configuration with ModListData format
                try
                {
                    var activeModsPath = PathManager.GetActiveModsPath();
                    var data = new ModListManager.ModListData
                    {
                        LastUpdated = DateTime.UtcNow,
                        Mods = activeModsList
                    };
                    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                    Services.FileAccessQueue.WriteAllText(activeModsPath, json);
                    
                    Logger.LogInfo($"Shuffle completed - activated {selectedMods.Count} random mods");
                    
                    // Reload manager to refresh the view
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        try
                        {
                            await ReloadModsAsync();
                            if (SettingsManager.Current.SendF10OnModChange)
                            {
                                // Wait for LoadingWindow to close and game to regain focus
                                await Task.Delay(500);
                                SendF10KeyPress();
                            }
                        }
                        catch (System.OperationCanceledException)
                        {
                            Logger.LogDebug("MainWindow.ExecuteShuffleActiveModsHotkeyInternal failed: canceled");
                        }
                        catch (System.Exception ex)
                        {
                            Logger.LogError("MainWindow.ExecuteShuffleActiveModsHotkeyInternal failed", ex);
                        }
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
        public async Task ExecuteDeactivateAllModsHotkey()
        {
            await ExecuteDeactivateAllModsHotkeyInternal();
        }

        // Execute deactivate all mods hotkey action (for in-focus hotkeys)
        public async Task ExecuteDeactivateAllModsHotkeyInFocus()
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

                    // The hotkey did nothing and gave no reason.
                    ShowWarningInfo(SharedUtilities.GetTranslation(
                        SharedUtilities.LoadLanguageDictionary(), "Notify_ModsFolderMissing"));

                    return;
                }
                
                // Get shuffle excluded categories (these will be excluded from deactivation too)
                var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                var excludedCategories = !string.IsNullOrEmpty(gameTag) 
                    ? SettingsManager.GetShuffleExcludedCategories(gameTag) 
                    : new List<string>();
                
                int deactivatedCount = 0;
                int duplicatesHandled = 0;
                var activeModsList = new List<string>(); // Use List for "Category|ModName" format
                
                // First pass: collect all mod names to detect duplicates
                var allModNames = new Dictionary<string, List<(string categoryPath, string categoryName, string folderName, bool isActive)>>();
                
                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = Path.GetFileName(categoryDir);
                    if (string.IsNullOrEmpty(categoryName)) continue;
                    
                    foreach (var modDir in Directory.GetDirectories(categoryDir))
                    {
                        var modFolderName = Path.GetFileName(modDir);
                        if (string.IsNullOrEmpty(modFolderName)) continue;
                        var cleanName = GetCleanModName(modFolderName);
                        bool isActive = !modFolderName.StartsWith("DISABLED_");
                        
                        if (!allModNames.ContainsKey(cleanName))
                        {
                            allModNames[cleanName] = new List<(string, string, string, bool)>();
                        }
                        allModNames[cleanName].Add((categoryDir, categoryName, modFolderName, isActive));
                    }
                }
                
                // Iterate through all category directories
                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = Path.GetFileName(categoryDir);
                    if (string.IsNullOrEmpty(categoryName)) continue;
                    
                    // Skip excluded categories (same as shuffle exclusion)
                    if (excludedCategories.Contains(categoryName, StringComparer.OrdinalIgnoreCase))
                    {
                        // Keep excluded category mods as they are - record active ones
                        foreach (var modDir in Directory.GetDirectories(categoryDir))
                        {
                            var modFolderName = Path.GetFileName(modDir);
                            if (string.IsNullOrEmpty(modFolderName)) continue;
                            var cleanName = GetCleanModName(modFolderName);
                            bool isActive = !modFolderName.StartsWith("DISABLED_");
                            if (isActive)
                            {
                                activeModsList.Add($"{categoryName}|{cleanName}"); // Use "Category|ModName" format
                            }
                        }
                        continue;
                    }
                    
                    // Deactivate all mods in other categories
                    foreach (var modDir in Directory.GetDirectories(categoryDir))
                    {
                        var modFolderName = Path.GetFileName(modDir);
                        var cleanName = GetCleanModName(modFolderName);
                        
                        // Skip if already disabled
                        if (modFolderName.StartsWith("DISABLED_"))
                        {
                            continue;
                        }
                        
                        // Check for duplicates (same clean name exists in multiple states)
                        bool hasDuplicate = allModNames.ContainsKey(cleanName) && allModNames[cleanName].Count > 1;
                        
                        string newName;
                        if (hasDuplicate)
                        {
                            // Check if there's both active and inactive version of the same mod
                            var modInstances = allModNames[cleanName];
                            bool hasActiveVersion = modInstances.Any(m => m.isActive);
                            bool hasInactiveVersion = modInstances.Any(m => !m.isActive);
                            
                            if (hasActiveVersion && hasInactiveVersion)
                            {
                                // Add _duplicate suffix before deactivating
                                newName = "DISABLED_" + modFolderName + "_duplicate";
                                duplicatesHandled++;
                                Logger.LogInfo($"Handling duplicate mod: {modFolderName} -> {newName}");
                            }
                            else
                            {
                                // Regular deactivation
                                newName = "DISABLED_" + modFolderName;
                            }
                        }
                        else
                        {
                            // Regular deactivation
                            newName = "DISABLED_" + modFolderName;
                        }
                        
                        var newPath = Path.Combine(categoryDir, newName);
                        
                        try
                        {
                            Services.FileAccessQueue.MoveDirectory(modDir, newPath);
                            deactivatedCount++;
                            Logger.LogInfo($"Deactivated: {modFolderName} -> {newName}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to deactivate {modFolderName}", ex);
                            // If rename failed, keep as active
                            activeModsList.Add($"{categoryName}|{cleanName}");
                        }
                    }
                }
                
                // Save new active mods configuration with ModListData format
                try
                {
                    var activeModsPath = PathManager.GetActiveModsPath();
                    var data = new ModListManager.ModListData
                    {
                        LastUpdated = DateTime.UtcNow,
                        Mods = activeModsList
                    };
                    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                    Services.FileAccessQueue.WriteAllText(activeModsPath, json);
                    
                    Logger.LogInfo($"Deactivate all completed - deactivated {deactivatedCount} mods (excluding {excludedCategories.Count} categories), handled {duplicatesHandled} duplicates");
                    
                    // Reload manager to refresh the view
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        try
                        {
                            await ReloadModsAsync();
                            if (SettingsManager.Current.SendF10OnModChange)
                            {
                                // Wait for LoadingWindow to close and game to regain focus
                                await Task.Delay(500);
                                SendF10KeyPress();
                            }
                        }
                        catch (System.OperationCanceledException)
                        {
                            Logger.LogDebug("MainWindow.ExecuteDeactivateAllModsHotkeyInternal failed: canceled");
                        }
                        catch (System.Exception ex)
                        {
                            Logger.LogError("MainWindow.ExecuteDeactivateAllModsHotkeyInternal failed", ex);
                        }
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


