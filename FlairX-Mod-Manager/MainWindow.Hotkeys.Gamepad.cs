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
        
        // Global gamepad manager for overlay toggle
        private GamepadManager? _globalGamepadManager;

        // Public accessor so OverlayWindow can share the same XInput instance
        public GamepadManager? GlobalGamepadManager => _globalGamepadManager;


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
            // Re-attach shared instance to overlay if it exists
            _overlayWindow?.RefreshGamepad(_globalGamepadManager);
        }

        private HashSet<string> _heldButtons = new();

        private void OnGlobalGamepadButtonPressed(object? sender, GamepadButtonEventArgs e)
        {
            var buttonName = e.GetButtonDisplayName();
            _heldButtons.Add(buttonName);

            // When overlay is visible it owns all gamepad input — don't process main window combos
            if (_overlayWindow?.IsOverlayVisible == true) return;

            // Check if current held buttons match the configured combo
            var configuredCombo = SettingsManager.Current?.GamepadToggleOverlayCombo ?? "Back+Start";
            var comboButtons = new HashSet<string>(configuredCombo.Split('+'));
            
            if (_heldButtons.SetEquals(comboButtons))
            {
                Logger.LogInfo($"Gamepad combo matched: {configuredCombo} - opening overlay");
                SimulateOverlayHotkey();
                _heldButtons.Clear();
            }
        }

        private void OnGlobalGamepadButtonReleased(object? sender, GamepadButtonEventArgs e)
        {
            var buttonName = e.GetButtonDisplayName();
            _heldButtons.Remove(buttonName);
        }
    }
}
