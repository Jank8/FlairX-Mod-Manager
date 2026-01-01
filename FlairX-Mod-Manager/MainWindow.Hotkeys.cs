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
        // Overlay window instance
        private OverlayWindow? _overlayWindow;
        
        // Global gamepad manager for overlay toggle
        private GamepadManager? _globalGamepadManager;

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
                            var cleanName = FlairX_Mod_Manager.Pages.ModGridPage.GetCleanModName(modFolderName);
                            newActiveMods[cleanName] = false;
                            deactivatedCount++;
                            Logger.LogInfo($"Deactivated: {modFolderName}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to deactivate {modFolderName}", ex);
                            var cleanName = FlairX_Mod_Manager.Pages.ModGridPage.GetCleanModName(modFolderName);
                            newActiveMods[cleanName] = true; // Keep as active if rename failed
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

        #region Global Gamepad Support

        /// <summary>
        /// Initialize global gamepad manager for overlay toggle
        /// </summary>
        public void InitializeGlobalGamepad()
        {
            if (!SettingsManager.Current.GamepadEnabled)
            {
                Logger.LogInfo("Global gamepad support disabled in settings");
                return;
            }

            try
            {
                _globalGamepadManager = new GamepadManager();
                _globalGamepadManager.ButtonPressed += OnGlobalGamepadButtonPressed;
                _globalGamepadManager.ButtonReleased += OnGlobalGamepadButtonReleased;
                _globalGamepadManager.ControllerConnected += (s, e) =>
                {
                    Logger.LogInfo("Global gamepad connected");
                };
                _globalGamepadManager.ControllerDisconnected += (s, e) =>
                {
                    Logger.LogInfo("Global gamepad disconnected");
                    _heldButtons.Clear();
                };
                _globalGamepadManager.StartPolling();
                Logger.LogInfo("Global gamepad manager initialized");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize global gamepad", ex);
            }
        }

        /// <summary>
        /// Stop global gamepad manager
        /// </summary>
        public void StopGlobalGamepad()
        {
            if (_globalGamepadManager != null)
            {
                _globalGamepadManager.ButtonPressed -= OnGlobalGamepadButtonPressed;
                _globalGamepadManager.ButtonReleased -= OnGlobalGamepadButtonReleased;
                _globalGamepadManager.Dispose();
                _globalGamepadManager = null;
                Logger.LogInfo("Global gamepad manager stopped");
            }
        }

        /// <summary>
        /// Refresh global gamepad (call after settings change)
        /// </summary>
        public void RefreshGlobalGamepad()
        {
            StopGlobalGamepad();
            InitializeGlobalGamepad();
        }

        private HashSet<string> _heldButtons = new();

        private void OnGlobalGamepadButtonPressed(object? sender, GamepadButtonEventArgs e)
        {
            var buttonName = e.GetButtonDisplayName();
            _heldButtons.Add(buttonName);

            // Check if current held buttons match the configured combo
            var configuredCombo = SettingsManager.Current.GamepadToggleOverlayCombo ?? "Back+Start";
            var comboButtons = new HashSet<string>(configuredCombo.Split('+'));
            
            if (_heldButtons.SetEquals(comboButtons))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    Logger.LogInfo($"Gamepad combo {configuredCombo} detected - toggling overlay");
                    ToggleOverlayWindow(vibrate: true);
                });
                
                // Reset to prevent repeated triggers
                _heldButtons.Clear();
            }
        }

        private void OnGlobalGamepadButtonReleased(object? sender, GamepadButtonEventArgs e)
        {
            var buttonName = e.GetButtonDisplayName();
            _heldButtons.Remove(buttonName);
        }

        #endregion

        /// <summary>
        /// Toggle the overlay window visibility
        /// </summary>
        /// <param name="vibrate">Whether to vibrate gamepad on show (default false for keyboard)</param>
        public void ToggleOverlayWindow(bool vibrate = false)
        {
            try
            {
                Logger.LogInfo("ToggleOverlayWindow: Starting");
                
                // Check if game is selected
                if (SettingsManager.Current.SelectedGameIndex <= 0)
                {
                    Logger.LogInfo("Cannot show overlay - no game selected");
                    return;
                }
                Logger.LogInfo($"ToggleOverlayWindow: Game selected index = {SettingsManager.Current.SelectedGameIndex}");

                // Create overlay window if it doesn't exist
                if (_overlayWindow == null)
                {
                    Logger.LogInfo("ToggleOverlayWindow: Creating new OverlayWindow");
                    _overlayWindow = new OverlayWindow(this);
                    Logger.LogInfo("ToggleOverlayWindow: OverlayWindow created, subscribing events");
                    _overlayWindow.ModToggleRequested += OnOverlayModToggleRequested;
                    _overlayWindow.WindowClosed += OnOverlayWindowClosed;
                    _overlayWindow.WindowHidden += OnOverlayWindowHidden;
                    Logger.LogInfo("Overlay window created and events subscribed");
                }

                // Toggle visibility
                Logger.LogInfo("ToggleOverlayWindow: Calling Toggle()");
                _overlayWindow.Toggle(vibrate);
                Logger.LogInfo($"Overlay window toggled");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error toggling overlay window", ex);
            }
        }

        /// <summary>
        /// Toggle active-only filter in overlay (only if overlay is visible)
        /// </summary>
        public void ToggleOverlayActiveFilter()
        {
            try
            {
                if (_overlayWindow != null && _overlayWindow.IsOverlayVisible)
                {
                    _overlayWindow.ToggleActiveOnlyFilter();
                    Logger.LogInfo("Overlay active filter toggled");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error toggling overlay active filter", ex);
            }
        }

        /// <summary>
        /// Handle mod toggle request from overlay
        /// </summary>
        private void OnOverlayModToggleRequested(string modPath)
        {
            try
            {
                Logger.LogInfo($"Overlay requested mod toggle: {modPath}");
                
                // Use lightweight refresh instead of full reload
                RefreshModTileState(modPath);
                
                // Send F10 to reload mods in game if enabled
                if (SettingsManager.Current.SendF10OnOverlayClose)
                {
                    SendF10KeyPress();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling overlay mod toggle", ex);
            }
        }

        /// <summary>
        /// Lightweight refresh of a single mod tile state (no loading window)
        /// </summary>
        private void RefreshModTileState(string modPath)
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (CurrentModGridPage != null)
                    {
                        CurrentModGridPage.RefreshSingleModState(modPath);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error refreshing mod tile state: {modPath}", ex);
            }
        }

        /// <summary>
        /// Handle overlay window closed event
        /// </summary>
        private void OnOverlayWindowClosed(object? sender, EventArgs e)
        {
            try
            {
                Logger.LogInfo("Overlay window was closed by user");
                
                // Clean up reference so a new window can be created
                if (_overlayWindow != null)
                {
                    _overlayWindow.ModToggleRequested -= OnOverlayModToggleRequested;
                    _overlayWindow.WindowClosed -= OnOverlayWindowClosed;
                    _overlayWindow.WindowHidden -= OnOverlayWindowHidden;
                    _overlayWindow = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling overlay window closed", ex);
                _overlayWindow = null;
            }
        }

        /// <summary>
        /// Handle overlay window hidden event (when toggled off)
        /// </summary>
        private void OnOverlayWindowHidden(object? sender, EventArgs e)
        {
            try
            {
                Logger.LogInfo("Overlay window was hidden");
                // F10 is now sent on each mod toggle, not on overlay close
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling overlay window hidden", ex);
            }
        }

        /// <summary>
        /// Send F10 key press to reload mods in game (using SendInput like No-Reload-Mod-Manager)
        /// </summary>
        private async void SendF10KeyPress()
        {
            try
            {
                Logger.LogInfo("SendF10KeyPress: Starting...");
                
                const ushort SCAN_F10 = 0x44; // Scan code for F10
                
                // Send F10 key down
                var inputDown = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = VK_F10,
                            wScan = SCAN_F10,
                            dwFlags = 0,
                            time = 0,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                };
                
                var sizeOfInput = Marshal.SizeOf<INPUT>();
                Logger.LogInfo($"SendF10KeyPress: INPUT struct size = {sizeOfInput}, VK=0x{VK_F10:X2}, Scan=0x{SCAN_F10:X2}");
                
                var resultDown = SendInput(1, new[] { inputDown }, sizeOfInput);
                Logger.LogInfo($"SendF10KeyPress: SendInput key down result: {resultDown}");
                
                await Task.Delay(50);
                
                // Send F10 key up
                var inputUp = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = VK_F10,
                            wScan = SCAN_F10,
                            dwFlags = KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                };
                
                var resultUp = SendInput(1, new[] { inputUp }, sizeOfInput);
                Logger.LogInfo($"SendF10KeyPress: SendInput key up result: {resultUp}");
                
                Logger.LogInfo("F10 key press sent via SendInput");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to send F10 key press", ex);
            }
        }

        /// <summary>
        /// Close overlay window when main window closes
        /// </summary>
        public void CloseOverlayWindow()
        {
            try
            {
                if (_overlayWindow != null)
                {
                    // Unsubscribe from events first
                    _overlayWindow.ModToggleRequested -= OnOverlayModToggleRequested;
                    _overlayWindow.WindowClosed -= OnOverlayWindowClosed;
                    _overlayWindow.WindowHidden -= OnOverlayWindowHidden;
                    
                    // Try to close the window safely
                    try
                    {
                        _overlayWindow.Close();
                    }
                    catch
                    {
                        // Window may already be closed or in invalid state
                    }
                    
                    _overlayWindow = null;
                    Logger.LogInfo("Overlay window closed");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error closing overlay window", ex);
                _overlayWindow = null;
            }
        }

        /// <summary>
        /// Update overlay window theme
        /// </summary>
        public void UpdateOverlayTheme()
        {
            try
            {
                if (_overlayWindow != null)
                {
                    // Re-apply backdrop to update theme
                    var backdrop = SettingsManager.Current.OverlayBackdrop ?? "AcrylicThin";
                    _overlayWindow.ApplyBackdrop(backdrop);
                    Logger.LogInfo("Overlay theme updated");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error updating overlay theme", ex);
            }
        }

        /// <summary>
        /// Update overlay window backdrop
        /// </summary>
        public void UpdateOverlayBackdrop(string backdrop)
        {
            try
            {
                if (_overlayWindow != null)
                {
                    _overlayWindow.ApplyBackdrop(backdrop);
                    Logger.LogInfo($"Overlay backdrop updated to: {backdrop}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error updating overlay backdrop", ex);
            }
        }

    }
}
