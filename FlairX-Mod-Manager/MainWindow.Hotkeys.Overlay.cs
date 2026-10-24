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
        
        // Overlay window instance
        private OverlayWindow? _overlayWindow;
        
        // Public property to access overlay window
        public OverlayWindow? OverlayWindow => _overlayWindow;


        /// <summary>
        /// Toggle the overlay window visibility
        /// </summary>
        /// <param name="vibrate">Whether to vibrate gamepad on show (default false for keyboard)</param>
        public void ToggleOverlayWindow(bool vibrate = false)
        {
            try
            {
                Logger.LogInfo($"ToggleOverlayWindow: Starting (vibrate={vibrate})");
                
                // Check if game is selected
                if (SettingsManager.Current.SelectedGameIndex <= 0)
                {
                    Logger.LogInfo("ToggleOverlayWindow: Cannot show overlay - no game selected");
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
                bool wasVisible = _overlayWindow.IsOverlayVisible;
                _overlayWindow.Toggle(vibrate);

                // If we just showed the overlay, call Activate() so WinUI3 finalizes focus
                // (SetForegroundWindow was already called from WndProc for keyboard hotkeys)
                if (!wasVisible)
                {
                    _overlayWindow.Activate();
                }
                
                // Hotkeys stay registered while the overlay is visible: the toggle shortcut
                // must still be able to close it, and GlobalHotkeyManager.OnHotkeyPressed
                // already blocks every non-overlay action in that state.
                Logger.LogInfo("Overlay window toggled (hotkeys remain registered)");
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
        /// Toggle developer tools section in settings (Ctrl+Alt+Shift+H)
        /// </summary>
        public void ToggleDevToolsSection()
        {
            try
            {
                // Find open settings panel
                var settingsPanel = _openPanelOverlays
                    .SelectMany(o => FindUserControlsInOverlay<Pages.SettingsUserControl>(o))
                    .FirstOrDefault();

                if (settingsPanel != null)
                {
                    var wasVisible = settingsPanel.IsDevToolsVisible;
                    settingsPanel.ToggleDevTools();

                    if (!wasVisible)
                        ShowSuccessInfo("Developer tools unlocked");

                    Logger.LogInfo($"Dev tools section toggled: {!wasVisible}");
                }
                else
                {
                    Logger.LogInfo("ToggleDevToolsSection: Settings panel not open");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error toggling dev tools section", ex);
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
                // F10 is sent directly from ToggleModAsync
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
        /// Handle overlay window closed event - window handles this internally now
        /// </summary>
        private void OnOverlayWindowClosed(object? sender, EventArgs e)
        {
            try
            {
                Logger.LogInfo("Overlay window close event received (window hides itself)");
                // Window now handles close by hiding itself in OverlayWindow_Closed
                // No action needed here
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling overlay window closed", ex);
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
                // Hotkeys are registered for the whole session. Refreshing here only
                // unregistered and re-registered them, which could lose a combination that
                // another application grabbed in the meantime.
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling overlay window hidden", ex);
            }
        }

        /// <summary>
        /// <summary>
        /// Opens overlay when triggered by gamepad combo.
        /// User will need to use keyboard hotkey for focus (gamepad just opens the window).
        /// </summary>
        private void SimulateOverlayHotkey()
        {
            try
            {
                Logger.LogInfo("SimulateOverlayHotkey: gamepad combo detected, opening overlay");

                DispatcherQueue?.TryEnqueue(() =>
                {
                    ToggleOverlayWindow(vibrate: true); // vibrate to indicate gamepad action
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("SimulateOverlayHotkey failed", ex);
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
                    
                    // Force close the window (not just hide)
                    try
                    {
                        _overlayWindow.ForceClose();
                    }
                    catch
                    {
                        // Window may already be closed or in invalid state
                    }
                    
                    _overlayWindow = null;
                    Logger.LogInfo("Overlay window force closed");
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
