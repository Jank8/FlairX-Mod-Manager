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
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
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
        private const int HOTKEY_OPTIMIZE_PREVIEWS = 1;
        private const int HOTKEY_RELOAD_MANAGER = 2;
        private const int HOTKEY_SHUFFLE_ACTIVE_MODS = 3;
        private const int HOTKEY_DEACTIVATE_ALL_MODS = 4;

        private readonly IntPtr _windowHandle;
        private readonly MainWindow _mainWindow;
        private readonly Dictionary<int, Func<Task>> _hotkeyActions;
        private readonly HashSet<int> _registeredHotkeys;
        private bool _disposed = false;

        public GlobalHotkeyManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _windowHandle = WindowNative.GetWindowHandle(mainWindow);
            _hotkeyActions = new Dictionary<int, Func<Task>>();
            _registeredHotkeys = new HashSet<int>();

            // Setup hotkey actions
            SetupHotkeyActions();
        }

        private void SetupHotkeyActions()
        {
            _hotkeyActions[HOTKEY_OPTIMIZE_PREVIEWS] = async () =>
            {
                Logger.LogInfo("Global hotkey: Optimize previews triggered");
                await _mainWindow.ExecuteOptimizePreviewsHotkey();
            };

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
        }

        public void RegisterAllHotkeys()
        {
            try
            {
                var settings = SettingsManager.Current;

                // Check if hotkeys are enabled
                if (!settings.HotkeysEnabled)
                {
                    Logger.LogInfo("Hotkeys are disabled - skipping registration");
                    return;
                }

                // Register optimize previews hotkey
                if (!string.IsNullOrEmpty(settings.OptimizePreviewsHotkey))
                {
                    RegisterHotkey(HOTKEY_OPTIMIZE_PREVIEWS, settings.OptimizePreviewsHotkey);
                }

                // Register reload manager hotkey
                if (!string.IsNullOrEmpty(settings.ReloadManagerHotkey))
                {
                    RegisterHotkey(HOTKEY_RELOAD_MANAGER, settings.ReloadManagerHotkey);
                }

                // Register shuffle active mods hotkey
                if (!string.IsNullOrEmpty(settings.ShuffleActiveModsHotkey))
                {
                    RegisterHotkey(HOTKEY_SHUFFLE_ACTIVE_MODS, settings.ShuffleActiveModsHotkey);
                }

                // Register deactivate all mods hotkey
                if (!string.IsNullOrEmpty(settings.DeactivateAllModsHotkey))
                {
                    RegisterHotkey(HOTKEY_DEACTIVATE_ALL_MODS, settings.DeactivateAllModsHotkey);
                }

                Logger.LogInfo($"Registered {_registeredHotkeys.Count} global hotkeys");
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
                if (ParseHotkeyString(hotkeyString, out uint modifiers, out uint virtualKey))
                {
                    if (RegisterHotKey(_windowHandle, id, modifiers, virtualKey))
                    {
                        _registeredHotkeys.Add(id);
                        Logger.LogInfo($"Registered global hotkey: {hotkeyString} (ID: {id})");
                    }
                    else
                    {
                        Logger.LogWarning($"Failed to register global hotkey: {hotkeyString} (ID: {id})");
                    }
                }
                else
                {
                    Logger.LogWarning($"Failed to parse hotkey string: {hotkeyString}");
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

            if (string.IsNullOrEmpty(hotkeyString))
                return false;

            var parts = hotkeyString.Split('+');
            if (parts.Length == 0)
                return false;

            // Parse modifiers
            for (int i = 0; i < parts.Length - 1; i++)
            {
                switch (parts[i].Trim().ToLower())
                {
                    case "ctrl":
                        modifiers |= MOD_CONTROL;
                        break;
                    case "alt":
                        modifiers |= MOD_ALT;
                        break;
                    case "shift":
                        modifiers |= MOD_SHIFT;
                        break;
                    case "win":
                        modifiers |= MOD_WIN;
                        break;
                }
            }

            // Parse main key
            var keyString = parts[parts.Length - 1].Trim().ToUpper();
            
            // Handle letters A-Z
            if (keyString.Length == 1 && keyString[0] >= 'A' && keyString[0] <= 'Z')
            {
                virtualKey = (uint)keyString[0];
                return true;
            }
            
            // Handle numbers 0-9
            if (keyString.Length == 1 && keyString[0] >= '0' && keyString[0] <= '9')
            {
                virtualKey = (uint)keyString[0];
                return true;
            }
            
            // Handle function keys
            if (keyString.StartsWith("F") && keyString.Length > 1)
            {
                if (int.TryParse(keyString.Substring(1), out int fKeyNum) && fKeyNum >= 1 && fKeyNum <= 12)
                {
                    virtualKey = (uint)VirtualKeys.VK_F1 + (uint)(fKeyNum - 1);
                    return true;
                }
            }

            return false;
        }

        public void UnregisterAllHotkeys()
        {
            try
            {
                foreach (var id in _registeredHotkeys)
                {
                    UnregisterHotKey(_windowHandle, id);
                }
                _registeredHotkeys.Clear();
                Logger.LogInfo("Unregistered all global hotkeys");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error unregistering global hotkeys", ex);
            }
        }

        public void RefreshHotkeys()
        {
            UnregisterAllHotkeys();
            RegisterAllHotkeys();
        }

        public async void OnHotkeyPressed(int id)
        {
            try
            {
                // Check if hotkeys are enabled before executing
                if (!SettingsManager.Current.HotkeysEnabled)
                {
                    Logger.LogInfo($"Hotkey {id} pressed but hotkeys are disabled - ignoring");
                    return;
                }

                if (_hotkeyActions.TryGetValue(id, out var action))
                {
                    await action();
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
                UnregisterAllHotkeys();
                _disposed = true;
            }
        }
    }
}