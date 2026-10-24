using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace FlairX_Mod_Manager
{
    public sealed partial class MainWindow
    {
        private const int WM_TRAYICON = 0x8000;
        private const int NIF_MESSAGE = 0x01;
        private const int NIF_ICON = 0x02;
        private const int NIF_TIP = 0x04;
        private const int NIM_ADD = 0x00;
        private const int NIM_DELETE = 0x02;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;
        
        [StructLayout(LayoutKind.Sequential)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }
        
        [DllImport("shell32.dll")]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA pnid);
        
        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const uint WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        
        private NOTIFYICONDATA _notifyIconData;
        private bool _trayIconCreated = false;
        private System.Drawing.Icon? _trayIcon; // Keep reference to prevent GC

        // Win32 API for subclassing window
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        
        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll")]
        private static extern bool TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);
        
        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();
        
        [DllImport("user32.dll")]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);
        
        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);
        
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        // Focus stealing APIs — used in WndProc while we have "foreground love" from WM_HOTKEY
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
        
        private const int GWL_WNDPROC = -4;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint MF_STRING = 0x0000;
        private const uint WM_COMMAND = 0x0111;
        private const uint WM_HOTKEY = 0x0312;
        private const uint TRAY_SHOW_ID = 1001;
        private const uint TRAY_EXIT_ID = 1002;
        
        private IntPtr _originalWndProc = IntPtr.Zero;
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProcDelegate? _wndProcDelegate;

        // Window procedure for handling hotkey messages and tray icon
        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                Logger.LogInfo($"WndProc: WM_HOTKEY message received, ID: {id}");

                // For overlay toggle: steal focus HERE while we have "foreground love" from WM_HOTKEY
                // This must happen BEFORE TryEnqueue — the foreground token is only valid synchronously
                const int HOTKEY_TOGGLE_OVERLAY = 5;
                if (id == HOTKEY_TOGGLE_OVERLAY && _overlayWindow != null && !_overlayWindow.IsOverlayVisible)
                {
                    try
                    {
                        var overlayHwnd = WinRT.Interop.WindowNative.GetWindowHandle(_overlayWindow);
                        IntPtr fgHwnd  = GetForegroundWindow();
                        uint   fgThread = GetWindowThreadProcessId(fgHwnd, out _);
                        uint   myThread = GetCurrentThreadId();

                        bool attached = fgThread != 0 && fgThread != myThread &&
                                        AttachThreadInput(myThread, fgThread, true);
                        try
                        {
                            BringWindowToTop(overlayHwnd);
                            SwitchToThisWindow(overlayHwnd, true);
                            SetForegroundWindow(overlayHwnd);
                        }
                        finally
                        {
                            if (attached) AttachThreadInput(myThread, fgThread, false);
                        }
                        Logger.LogInfo("WndProc: SetForegroundWindow on overlay called with foreground love");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("WndProc: focus steal failed", ex);
                    }
                }

                _globalHotkeyManager?.OnHotkeyPressed(id);
                return IntPtr.Zero;
            }
            else if (msg == WM_TRAYICON)
            {
                int lParam32 = lParam.ToInt32();
                if (lParam32 == WM_LBUTTONDBLCLK)
                {
                    ShowFromTray();
                }
                else if (lParam32 == WM_RBUTTONUP)
                {
                    ShowTrayContextMenu();
                }
                return IntPtr.Zero;
            }
            else if (msg == WM_COMMAND)
            {
                uint commandId = (uint)(wParam.ToInt32() & 0xFFFF);
                if (commandId == TRAY_SHOW_ID)
                {
                    ShowFromTray();
                    return IntPtr.Zero;
                }
                else if (commandId == TRAY_EXIT_ID)
                {
                    ExitApplication();
                    return IntPtr.Zero;
                }
            }
            
            return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
        }



        // GenerateModCharacterMenuAsync method moved to MainWindow.UIManagement.cs

        // EnsurePresetsMenuItemExists method moved to MainWindow.UIManagement.cs

        // Menu generation and UI refresh methods moved to MainWindow.UIManagement.cs

        // Launcher FAB and UI events moved to MainWindow.EventHandlers.cs
        // Game selection methods moved to MainWindow.UIManagement.cs

        // Window management and backdrop methods moved to MainWindow.WindowManagement.cs

        // System Tray functionality
        private void InitializeSystemTray()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                
                _notifyIconData = new NOTIFYICONDATA();
                _notifyIconData.cbSize = Marshal.SizeOf(_notifyIconData);
                _notifyIconData.hWnd = hwnd;
                _notifyIconData.uID = 1;
                _notifyIconData.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
                _notifyIconData.uCallbackMessage = WM_TRAYICON;
                var lang = SharedUtilities.LoadLanguageDictionary();
                _notifyIconData.szTip = SharedUtilities.GetTranslation(lang, "App_Name");
                
                // Load icon - keep reference to prevent GC
                try
                {
                    // Try multiple paths to find the icon
                    string[] iconPaths = {
                        "Assets/app.ico",
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico"),
                        Path.Combine(Environment.CurrentDirectory, "Assets", "app.ico")
                    };
                    
                    bool iconLoaded = false;
                    foreach (var iconPath in iconPaths)
                    {
                        if (File.Exists(iconPath))
                        {
                            _trayIcon = new System.Drawing.Icon(iconPath);
                            _notifyIconData.hIcon = _trayIcon.Handle;
                            Logger.LogInfo($"Loaded tray icon from: {iconPath}");
                            iconLoaded = true;
                            break;
                        }
                        else
                        {
                            Logger.LogDebug($"Icon not found at: {iconPath}");
                        }
                    }
                    
                    if (!iconLoaded)
                    {
                        // Use default system icon
                        _notifyIconData.hIcon = LoadIcon(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION
                        Logger.LogWarning("Could not find app.ico, using default system icon for tray");
                    }
                }
                catch (Exception iconEx)
                {
                    Logger.LogError("Failed to load tray icon, using default", iconEx);
                    _notifyIconData.hIcon = LoadIcon(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION
                }
                
                Logger.LogInfo("System tray initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize system tray", ex);
            }
        }

        private void MinimizeToTray()
        {
            if (SettingsManager.Current.MinimizeToTrayEnabled)
            {
                try
                {
                    _isClosingToTray = true;
                    
                    // Add tray icon
                    if (!_trayIconCreated)
                    {
                        bool success = Shell_NotifyIcon(NIM_ADD, ref _notifyIconData);
                        _trayIconCreated = success;
                        Logger.LogInfo($"Tray icon creation: {(success ? "SUCCESS" : "FAILED")}");
                        
                        if (!success)
                        {
                            Logger.LogError("Failed to create tray icon - Shell_NotifyIcon returned false");
                        }
                    }
                    
                    // Hide window from taskbar using Win32 API
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    ShowWindow(hwnd, SW_HIDE);
                    Logger.LogInfo("Application minimized to system tray");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to minimize to tray", ex);
                    _isClosingToTray = false;
                }
            }
        }

        private void ShowFromTray()
        {
            try
            {
                _isClosingToTray = false;
                
                // Remove tray icon
                if (_trayIconCreated)
                {
                    Shell_NotifyIcon(NIM_DELETE, ref _notifyIconData);
                    _trayIconCreated = false;
                }
                
                // Show window using Win32 API
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                ShowWindow(hwnd, SW_SHOW);
                this.Activate();
                
                // Restore window if minimized
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                
                if (appWindow?.Presenter is OverlappedPresenter presenter)
                {
                    if (presenter.State == OverlappedPresenterState.Minimized)
                    {
                        presenter.Restore();
                    }
                }
                
                Logger.LogInfo("Application restored from system tray");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to show from tray", ex);
            }
        }

        private void ExitApplication()
        {
            try
            {
                _isClosingToTray = false;
                
                // Remove tray icon
                if (_trayIconCreated)
                {
                    Shell_NotifyIcon(NIM_DELETE, ref _notifyIconData);
                    _trayIconCreated = false;
                }
                
                // Dispose icon
                _trayIcon?.Dispose();
                _trayIcon = null;
                
                SaveWindowState();
                _globalHotkeyManager?.Dispose();
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to exit application", ex);
            }
        }

        private void ShowTrayContextMenu()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                
                // Create popup menu
                IntPtr hMenu = CreatePopupMenu();
                if (hMenu == IntPtr.Zero)
                {
                    Logger.LogError("Failed to create popup menu");
                    return;
                }
                
                // Load language dictionary for menu items
                var lang = SharedUtilities.LoadLanguageDictionary();
                string showText = SharedUtilities.GetTranslation(lang, "SystemTray_Show") ?? "Show";
                string exitText = SharedUtilities.GetTranslation(lang, "SystemTray_Exit") ?? "Exit";
                
                // Add menu items
                AppendMenu(hMenu, MF_STRING, TRAY_SHOW_ID, showText);
                AppendMenu(hMenu, MF_STRING, TRAY_EXIT_ID, exitText);
                
                // Get cursor position
                GetCursorPos(out POINT cursorPos);
                
                // Show context menu
                TrackPopupMenuEx(hMenu, TPM_RIGHTBUTTON, cursorPos.X, cursorPos.Y, hwnd, IntPtr.Zero);
                
                // Clean up
                DestroyMenu(hMenu);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to show tray context menu", ex);
            }
        }
    }
}
