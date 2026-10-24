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
        // Debounce timer for F10 sending - prevents spam when user changes multiple mods quickly
        private static System.Threading.Timer? _f10DebounceTimer;
        private static readonly object _f10DebounceLock = new object();
        private const int F10_DEBOUNCE_DELAY_MS = 500; // Wait 500ms after last mod change before sending F10

        /// <summary>
        /// Send F10 key press to notify XXMI of mod state changes (optimized for background use).
        /// With check_foreground_window = 0 in d3dx.ini, this works without admin privileges.
        /// Sends 3 times with short intervals to ensure XXMI catches it under load.
        /// Automatically ensures d3dx.ini has check_foreground_window = 0 if enabled.
        /// </summary>
        public async void SendF10KeyPress()
        {
            try
            {
                Logger.LogInfo("SendF10KeyPress: Starting...");

                // Note: d3dx.ini is ensured on startup and when settings change
                // No need to check here on every F10 press to avoid UI freeze

                const ushort SCAN_F10 = 0x44;
                var sizeOfInput = Marshal.SizeOf<INPUT>();

                for (int i = 0; i < 3; i++)
                {
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

                    var resultDown = SendInput(1, new[] { inputDown }, sizeOfInput);
                    Logger.LogInfo($"SendInput F10 key down [{i}] result: {resultDown}");

                    await Task.Delay(50);

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
                    Logger.LogInfo($"SendInput F10 key up [{i}] result: {resultUp}");

                    if (i < 2)
                        await Task.Delay(150);
                }

                Logger.LogInfo("F10 key press sent via SendInput (3x)");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to send F10 key press", ex);
            }
        }

        /// <summary>
        /// Send F10 with debouncing - resets timer on each call, sends only after delay with no new calls.
        /// This prevents F10 spam when user changes multiple mods quickly in succession.
        /// </summary>
        public void SendF10KeyPressDebounced()
        {
            if (!SettingsManager.Current.SendF10OnModChange)
                return;

            lock (_f10DebounceLock)
            {
                // Cancel previous timer if exists
                _f10DebounceTimer?.Dispose();

                // Create new timer that fires once after delay
                _f10DebounceTimer = new System.Threading.Timer(_ =>
                {
                    Logger.LogInfo($"F10 debounce timer fired - sending F10 after {F10_DEBOUNCE_DELAY_MS}ms of inactivity");
                    DispatcherQueue.TryEnqueue(() => SendF10KeyPress());
                }, null, F10_DEBOUNCE_DELAY_MS, Timeout.Infinite);

                Logger.LogInfo($"F10 debounce timer reset - will send in {F10_DEBOUNCE_DELAY_MS}ms if no more changes");
            }
        }
    }
}
