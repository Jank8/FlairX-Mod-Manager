using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Manager for global hotkeys that work system-wide, even when the application is not in focus
    /// </summary>
    public class GlobalHotkeyManager : IDisposable
    {
        // Win32 API imports
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int nExitCode);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // Structures
        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        // Virtual key codes for common keys
        private enum VirtualKeys : uint
        {
            VK_A = 0x41, VK_B = 0x42, VK_C = 0x43, VK_D = 0x44, VK_E = 0x45, VK_F = 0x46, VK_G = 0x47,
            VK_H = 0x48, VK_I = 0x49, VK_J = 0x4A, VK_K = 0x4B, VK_L = 0x4C, VK_M = 0x4D, VK_N = 0x4E,
            VK_O = 0x4F, VK_P = 0x50, VK_Q = 0x51, VK_R = 0x52, VK_S = 0x53, VK_T = 0x54, VK_U = 0x55,
            VK_V = 0x56, VK_W = 0x57, VK_X = 0x58, VK_Y = 0x59, VK_Z = 0x5A,
            VK_0 = 0x30, VK_1 = 0x31, VK_2 = 0x32, VK_3 = 0x33, VK_4 = 0x34, VK_5 = 0x35,
            VK_6 = 0x36, VK_7 = 0x37, VK_8 = 0x38, VK_9 = 0x39,
            VK_F1 = 0x70, VK_F2 = 0x71, VK_F3 = 0x72, VK_F4 = 0x73, VK_F5 = 0x74, VK_F6 = 0x75,
            VK_F7 = 0x76, VK_F8 = 0x77, VK_F9 = 0x78, VK_F10 = 0x79, VK_F11 = 0x7A, VK_F12 = 0x7B
        }

        // Modifier key constants
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        // Windows message constants
        private const int WM_HOTKEY = 0x0312;

        // Hotkey IDs
        private const int HOTKEY_RELOAD_MANAGER = 2;
        private const int HOTKEY_SHUFFLE_ACTIVE_MODS = 3;
        private const int HOTKEY_DEACTIVATE_ALL_MODS = 4;
        private const int HOTKEY_TOGGLE_OVERLAY = 5;
        private const int HOTKEY_FILTER_ACTIVE = 6;

        private readonly IntPtr _windowHandle;
        private readonly MainWindow _mainWindow;
        private readonly Dictionary<int, Func<Task>> _hotkeyActions;
        private readonly HashSet<int> _registeredHotkeys;
        private bool _disposed = false;

        public GlobalHotkeyManager(MainWindow mainWindow)
        {
            Logger.LogInfo("GlobalHotkeyManager: Constructor starting");
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _windowHandle = WindowNative.GetWindowHandle(mainWindow);
            Logger.LogInfo($"GlobalHotkeyManager: Window handle obtained: 0x{_windowHandle:X}");
            _hotkeyActions = new Dictionary<int, Func<Task>>();
            _registeredHotkeys = new HashSet<int>();

            // Setup hotkey actions
            SetupHotkeyActions();
            Logger.LogInfo("GlobalHotkeyManager: Constructor completed");
        }

        private void SetupHotkeyActions()
        {
            _hotkeyActions[HOTKEY_RELOAD_MANAGER] = async () =>
            {
                Logger.LogInfo("Global hotkey: Reload manager triggered");
                await _mainWindow.ExecuteReloadManagerHotkey();
            };

            _hotkeyActions[HOTKEY_SHUFFLE_ACTIVE_MODS] = async () =>
            {
                Logger.LogInfo("Global hotkey: Shuffle active mods triggered");
                await Task.Run(() => _mainWindow.ExecuteShuffleActiveModsHotkey());
            };

            _hotkeyActions[HOTKEY_DEACTIVATE_ALL_MODS] = async () =>
            {
                Logger.LogInfo("Global hotkey: Deactivate all mods triggered");
                await Task.Run(() => _mainWindow.ExecuteDeactivateAllModsHotkey());
            };

            _hotkeyActions[HOTKEY_TOGGLE_OVERLAY] = async () =>
            {
                Logger.LogInfo("Global hotkey: Toggle overlay triggered");
                await Task.CompletedTask;
                _mainWindow.DispatcherQueue.TryEnqueue(() => _mainWindow.ToggleOverlayWindow());
            };

            _hotkeyActions[HOTKEY_FILTER_ACTIVE] = async () =>
            {
                Logger.LogInfo("Global hotkey: Filter active mods triggered");
                await Task.CompletedTask;
                _mainWindow.DispatcherQueue.TryEnqueue(() => _mainWindow.ToggleOverlayActiveFilter());
            };
        }

        public void RegisterAllHotkeys()
        {
            try
            {
                Logger.LogInfo("RegisterAllHotkeys: Starting hotkey registration");
                var settings = SettingsManager.Current;

                // Check if hotkeys are enabled
                Logger.LogInfo($"RegisterAllHotkeys: HotkeysEnabled = {settings.HotkeysEnabled}");
                if (!settings.HotkeysEnabled)
                {
                    Logger.LogInfo("Hotkeys are disabled - skipping registration");
                    return;
                }

                // Register reload manager hotkey
                Logger.LogInfo($"RegisterAllHotkeys: ReloadManagerHotkey = '{settings.ReloadManagerHotkey}'");
                if (!string.IsNullOrEmpty(settings.ReloadManagerHotkey))
                {
                    RegisterHotkey(HOTKEY_RELOAD_MANAGER, settings.ReloadManagerHotkey);
                }

                // Register shuffle active mods hotkey
                Logger.LogInfo($"RegisterAllHotkeys: ShuffleActiveModsHotkey = '{settings.ShuffleActiveModsHotkey}'");
                if (!string.IsNullOrEmpty(settings.ShuffleActiveModsHotkey))
                {
                    RegisterHotkey(HOTKEY_SHUFFLE_ACTIVE_MODS, settings.ShuffleActiveModsHotkey);
                }

                // Register deactivate all mods hotkey
                Logger.LogInfo($"RegisterAllHotkeys: DeactivateAllModsHotkey = '{settings.DeactivateAllModsHotkey}'");
                if (!string.IsNullOrEmpty(settings.DeactivateAllModsHotkey))
                {
                    RegisterHotkey(HOTKEY_DEACTIVATE_ALL_MODS, settings.DeactivateAllModsHotkey);
                }

                // Register toggle overlay hotkey
                Logger.LogInfo($"RegisterAllHotkeys: ToggleOverlayHotkey = '{settings.ToggleOverlayHotkey}'");
                if (!string.IsNullOrEmpty(settings.ToggleOverlayHotkey))
                {
                    RegisterHotkey(HOTKEY_TOGGLE_OVERLAY, settings.ToggleOverlayHotkey);
                }

                // Register filter active mods hotkey
                Logger.LogInfo($"RegisterAllHotkeys: FilterActiveHotkey = '{settings.FilterActiveHotkey}'");
                if (!string.IsNullOrEmpty(settings.FilterActiveHotkey))
                {
                    RegisterHotkey(HOTKEY_FILTER_ACTIVE, settings.FilterActiveHotkey);
                }

                Logger.LogInfo($"RegisterAllHotkeys: Completed - registered {_registeredHotkeys.Count} global hotkeys");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to register global hotkeys", ex);
            }
        }

        private void RegisterHotkey(int id, string hotkeyString)
        {
            try
            {
                Logger.LogInfo($"RegisterHotkey: Attempting to register '{hotkeyString}' with ID {id}");
                if (ParseHotkeyString(hotkeyString, out uint modifiers, out uint virtualKey))
                {
                    Logger.LogInfo($"RegisterHotkey: Parsed '{hotkeyString}' - modifiers=0x{modifiers:X}, virtualKey=0x{virtualKey:X}");
                    if (RegisterHotKey(_windowHandle, id, modifiers, virtualKey))
                    {
                        _registeredHotkeys.Add(id);
                        Logger.LogInfo($"RegisterHotkey: SUCCESS - Registered global hotkey: {hotkeyString} (ID: {id})");
                    }
                    else
                    {
                        var lastError = Marshal.GetLastWin32Error();
                        Logger.LogWarning($"RegisterHotkey: FAILED - Could not register global hotkey: {hotkeyString} (ID: {id}), Win32 Error: {lastError}");
                    }
                }
                else
                {
                    Logger.LogWarning($"RegisterHotkey: FAILED - Could not parse hotkey string: {hotkeyString}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error registering hotkey {hotkeyString}", ex);
            }
        }

        private bool ParseHotkeyString(string hotkeyString, out uint modifiers, out uint virtualKey)
        {
            modifiers = 0;
            virtualKey = 0;

            Logger.LogInfo($"ParseHotkeyString: Parsing '{hotkeyString}'");

            if (string.IsNullOrEmpty(hotkeyString))
            {
                Logger.LogWarning("ParseHotkeyString: Hotkey string is null or empty");
                return false;
            }

            var parts = hotkeyString.Split('+');
            if (parts.Length == 0)
            {
                Logger.LogWarning("ParseHotkeyString: No parts found after split");
                return false;
            }

            Logger.LogInfo($"ParseHotkeyString: Split into {parts.Length} parts: [{string.Join(", ", parts)}]");

            // Parse modifiers
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var modifier = parts[i].Trim().ToLower();
                switch (modifier)
                {
                    case "ctrl":
                        modifiers |= MOD_CONTROL;
                        Logger.LogInfo($"ParseHotkeyString: Added CTRL modifier (0x{MOD_CONTROL:X})");
                        break;
                    case "alt":
                        modifiers |= MOD_ALT;
                        Logger.LogInfo($"ParseHotkeyString: Added ALT modifier (0x{MOD_ALT:X})");
                        break;
                    case "shift":
                        modifiers |= MOD_SHIFT;
                        Logger.LogInfo($"ParseHotkeyString: Added SHIFT modifier (0x{MOD_SHIFT:X})");
                        break;
                    case "win":
                        modifiers |= MOD_WIN;
                        Logger.LogInfo($"ParseHotkeyString: Added WIN modifier (0x{MOD_WIN:X})");
                        break;
                    default:
                        Logger.LogWarning($"ParseHotkeyString: Unknown modifier '{modifier}'");
                        break;
                }
            }

            // Parse main key
            var keyString = parts[parts.Length - 1].Trim().ToUpper();
            Logger.LogInfo($"ParseHotkeyString: Main key string: '{keyString}'");
            
            // Handle letters A-Z
            if (keyString.Length == 1 && keyString[0] >= 'A' && keyString[0] <= 'Z')
            {
                virtualKey = (uint)keyString[0];
                Logger.LogInfo($"ParseHotkeyString: Parsed as letter key, VK=0x{virtualKey:X} ('{keyString}')");
                return true;
            }
            
            // Handle numbers 0-9
            if (keyString.Length == 1 && keyString[0] >= '0' && keyString[0] <= '9')
            {
                virtualKey = (uint)keyString[0];
                Logger.LogInfo($"ParseHotkeyString: Parsed as number key, VK=0x{virtualKey:X} ('{keyString}')");
                return true;
            }
            
            // Handle function keys
            if (keyString.StartsWith("F") && keyString.Length > 1)
            {
                if (int.TryParse(keyString.Substring(1), out int fKeyNum) && fKeyNum >= 1 && fKeyNum <= 12)
                {
                    virtualKey = (uint)VirtualKeys.VK_F1 + (uint)(fKeyNum - 1);
                    Logger.LogInfo($"ParseHotkeyString: Parsed as function key F{fKeyNum}, VK=0x{virtualKey:X}");
                    return true;
                }
            }

            Logger.LogWarning($"ParseHotkeyString: Could not parse key '{keyString}'");
            return false;
        }

        public void UnregisterAllHotkeys()
        {
            try
            {
                Logger.LogInfo($"UnregisterAllHotkeys: Unregistering {_registeredHotkeys.Count} hotkeys");
                foreach (var id in _registeredHotkeys)
                {
                    UnregisterHotKey(_windowHandle, id);
                    Logger.LogInfo($"UnregisterAllHotkeys: Unregistered hotkey ID {id}");
                }
                _registeredHotkeys.Clear();
                Logger.LogInfo("UnregisterAllHotkeys: Completed - all global hotkeys unregistered");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error unregistering global hotkeys", ex);
            }
        }

        public void RefreshHotkeys()
        {
            Logger.LogInfo("RefreshHotkeys: Starting hotkey refresh");
            UnregisterAllHotkeys();
            RegisterAllHotkeys();
            Logger.LogInfo("RefreshHotkeys: Completed");
        }

        public async void OnHotkeyPressed(int id)
        {
            try
            {
                Logger.LogInfo($"OnHotkeyPressed: Hotkey ID {id} pressed");
                
                // Check if hotkeys are enabled before executing
                if (!SettingsManager.Current.HotkeysEnabled)
                {
                    Logger.LogInfo($"OnHotkeyPressed: Hotkey {id} pressed but hotkeys are disabled - ignoring");
                    return;
                }

                if (_hotkeyActions.TryGetValue(id, out var action))
                {
                    Logger.LogInfo($"OnHotkeyPressed: Executing action for hotkey ID {id}");
                    await action();
                    Logger.LogInfo($"OnHotkeyPressed: Action completed for hotkey ID {id}");
                }
                else
                {
                    Logger.LogWarning($"OnHotkeyPressed: No action found for hotkey ID {id}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error executing hotkey action for ID {id}", ex);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Logger.LogInfo("GlobalHotkeyManager.Dispose: Starting cleanup");
                UnregisterAllHotkeys();
                _disposed = true;
                Logger.LogInfo("GlobalHotkeyManager.Dispose: Completed");
            }
        }
    }
}