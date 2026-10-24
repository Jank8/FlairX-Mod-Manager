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
    public sealed partial class MainWindow : Window
    {

        // Semaphore to ensure only one ContentDialog is shown at a time
        private static readonly System.Threading.SemaphoreSlim _dialogSemaphore = new(1, 1);

        /// <summary>
        /// Show a ContentDialog safely — waits if another dialog is already open.
        /// </summary>
        public static async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
        {
            await _dialogSemaphore.WaitAsync();
            try
            {
                return await dialog.ShowAsync();
            }
            finally
            {
                _dialogSemaphore.Release();
            }
        }
        private Dictionary<string, string> _lang = new();
        
        // Event for notifying about window size changes
        public static event EventHandler? WindowSizeChanged;
        
        // Current ModGridPage reference for dynamic filtering
        public FlairX_Mod_Manager.Pages.ModGridPage? CurrentModGridPage { get; set; }

        /// <summary>
        /// Public access to the content frame for overlay integration
        /// </summary>
        public Frame ContentFrame => contentFrame;

        /// <summary>
        /// Hidden container for off-screen WebView2 rendering (comments scraping)
        /// </summary>
        public Grid GetHiddenWebViewContainer() => HiddenWebViewContainer;

        /// <summary>
        /// Get the current ModGridPage instance if available
        /// </summary>
        public FlairX_Mod_Manager.Pages.ModGridPage? GetCurrentModGridPage()
        {
            return CurrentModGridPage;
        }

        private void LoadLanguage()
        {
            _lang = SharedUtilities.LoadLanguageDictionary();
        }

        private List<NavigationViewItem> _allMenuItems = new();
        private List<NavigationViewItem> _allFooterItems = new();
        
        // Sliding panels management
        private List<Grid> _openPanelOverlays = new();


        private bool _isInitializationComplete = false;
        
        // Category preview popup timer
        private DispatcherTimer? _categoryPreviewCloseTimer;
        private bool _isPointerOverCategoryIcon = false;

        // Backdrop fields
        WindowsSystemDispatcherQueueHelper? wsdqHelper;
        DesktopAcrylicController? acrylicController;
        MicaController? micaController;
        SystemBackdropConfiguration? configurationSource;

        // Global hotkey manager
        private GlobalHotkeyManager? _globalHotkeyManager;

        // System tray components
        private bool _isClosingToTray = false;



        // Method to refresh global hotkeys when settings change
        public void RefreshGlobalHotkeys()
        {
            try
            {
                Logger.LogInfo("MainWindow.RefreshGlobalHotkeys: Starting");
                _globalHotkeyManager?.RefreshHotkeys();
                Logger.LogInfo("MainWindow.RefreshGlobalHotkeys: Completed - global hotkeys refreshed");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to refresh global hotkeys", ex);
            }
        }

        public MainWindow()
        {
            Logger.LogMethodEntry("MainWindow constructor starting");
            try
            {
                Logger.LogInfo("Starting InitializeComponent");
                InitializeComponent();
                Logger.LogInfo("InitializeComponent completed successfully");
                
                Logger.LogInfo("Loading language dictionary");
                LoadLanguage();
                Logger.LogInfo($"Language loaded - Dictionary contains {_lang.Count} entries");
            }
            catch (Exception ex)
            {
                Logger.LogError("Critical error during basic MainWindow initialization", ex);
                throw;
            }

            try
            {
                Logger.LogInfo("Setting up button tooltips");
                // Set button tooltip translations
                ToolTipService.SetToolTip(ReloadModsButton, SharedUtilities.GetTranslation(_lang, "Reload_Mods_Tooltip"));
                ToolTipService.SetToolTip(BrowseGameBananaButton, SharedUtilities.GetTranslation(_lang, "Browse_GameBanana_Tooltip"));
                ToolTipService.SetToolTip(LauncherFabBorder, SharedUtilities.GetTranslation(_lang, "Launcher_Tooltip"));
                Logger.LogInfo("Button tooltips configured successfully");
                
                Logger.LogInfo("Initializing view mode from settings");
                InitializeViewModeFromSettings();
                Logger.LogInfo($"View mode initialized: {SettingsManager.Current.ViewMode}");

                Logger.LogInfo("Updating game selection ComboBox");
                UpdateGameSelectionComboBoxTexts();
                Logger.LogInfo("Game selection ComboBox updated successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during MainWindow UI setup", ex);
                throw;
            }

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            // Set window icon on taskbar
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);

                    // Also set via WM_SETICON for proper taskbar icon on unpackaged WinUI 3
                    var largeIcons = new IntPtr[1];
                    var smallIcons = new IntPtr[1];
                    ExtractIconEx(iconPath, 0, largeIcons, smallIcons, 1);
                    if (largeIcons[0] != IntPtr.Zero)
                        SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_BIG), largeIcons[0]);
                    if (smallIcons[0] != IntPtr.Zero)
                        SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_SMALL), smallIcons[0]);

                    // Flush Windows icon cache so the correct icon appears on first launch
                    SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to set window icon", ex);
            }
            
            // Disable maximize on double-click title bar
            DisableMaximizeOnDoubleClick(hwnd);
            
            // Force theme on startup according to user settings FIRST
            var theme = FlairX_Mod_Manager.SettingsManager.Current.Theme;
            if (this.Content is FrameworkElement root)
            {
                if (theme == "Light")
                {
                    root.RequestedTheme = ElementTheme.Light;
                    appWindow.TitleBar.ButtonForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonHoverForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonPressedForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(100, 230, 230, 230); // Semi-transparent light gray
                    appWindow.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(150, 210, 210, 210); // Semi-transparent lighter gray
                }
                else if (theme == "Dark")
                {
                    root.RequestedTheme = ElementTheme.Dark;
                    appWindow.TitleBar.ButtonForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 50, 50, 50); // Dark gray
                    appWindow.TitleBar.ButtonPressedForegroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30); // Darker gray
                }
                else
                {
                    root.RequestedTheme = ElementTheme.Default;
                    appWindow.TitleBar.ButtonForegroundColor = null;
                    appWindow.TitleBar.ButtonHoverForegroundColor = null;
                    appWindow.TitleBar.ButtonPressedForegroundColor = null;
                    appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = null;
                    appWindow.TitleBar.ButtonPressedBackgroundColor = null;
                }
            }

            // Initialize backdrop from settings AFTER theme is applied
            string backdropEffect = SettingsManager.Current.BackdropEffect ?? "AcrylicThin";
            
            // Check Windows 10 compatibility and fix incompatible backdrop effects
            backdropEffect = EnsureBackdropCompatibility(backdropEffect);
            
            ApplyBackdropEffect(backdropEffect);
            
            // Ensure settings are loaded before restoring window state
            SettingsManager.Load();
            
            // Restore window state from settings
            RestoreWindowState();
            
            // Subscribe to window events to save state changes
            if (appWindow != null)
            {
                appWindow.Changed += (sender, args) =>
                {
                    if (args.DidSizeChange || args.DidPositionChange || args.DidPresenterChange)
                    {
                        ScheduleWindowStateSave();
                        
                        // Notify about window size change
                        if (args.DidSizeChange)
                        {
                            WindowSizeChanged?.Invoke(this, EventArgs.Empty);
                        }
                        
                        // Handle minimize to tray
                        if (args.DidPresenterChange && appWindow.Presenter is OverlappedPresenter presenter)
                        {
                            if (presenter.State == OverlappedPresenterState.Minimized && 
                                SettingsManager.Current.MinimizeToTrayEnabled)
                            {
                                // Delay minimize to tray to avoid conflicts
                                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                                {
                                    MinimizeToTray();
                                });
                            }
                        }
                    }
                };
            }
            
            // Initialize system tray
            InitializeSystemTray();

            // Save window state when closing
            this.Closed += (sender, args) => 
            {
                Logger.LogInfo("MainWindow.Closed: begin");
                if (!_isClosingToTray)
                {
                    SaveWindowState();
                    _globalHotkeyManager?.Dispose();
                    
                    // Stop global gamepad
                    StopGlobalGamepad();
                    
                    // Close overlay window
                    CloseOverlayWindow();
                    
                    // Clean up tray icon
                    if (_trayIconCreated)
                    {
                        Shell_NotifyIcon(NIM_DELETE, ref _notifyIconData);
                        _trayIconCreated = false;
                    }
                    
                    // Dispose icon
                    _trayIcon?.Dispose();
                    _trayIcon = null;
                    
                    Logger.LogInfo("MainWindow.Closed: synchronous cleanup done, scheduling exit");
                    Logger.Flush();
                    
                    // Force kill process after cleanup to ensure no lingering windows.
                    // Give the log writer and hotkey unregister enough time to finish;
                    // 500 ms was often not enough and looked like a crash in the log.
                    Task.Run(async () =>
                    {
                        await Task.Delay(2000);
                        try
                        {
                            Logger.LogInfo("MainWindow.Closed: force-killing process");
                            Logger.Flush();
                            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                            currentProcess.Kill();
                        }
                        catch
                        {
                            // Process already exited
                        }
                    });
                }
            };

            nvSample.Loaded += NvSample_Loaded;
            
            // Handle pane open/close to show/hide star buttons
            nvSample.PaneClosing += (s, args) =>
            {
                Logger.LogInfo("Pane closing - hiding stars");
                UpdateStarButtonsVisibility();
            };
            
            nvSample.PaneOpening += (s, args) =>
            {
                Logger.LogInfo("Pane opening - showing stars");
                UpdateStarButtonsVisibility();
            };
            
            nvSample.DisplayModeChanged += (s, args) =>
            {
                Logger.LogInfo($"DisplayModeChanged fired - new mode: {nvSample.DisplayMode}");
                UpdateStarButtonsVisibility();
            };
            
            nvSample.Loaded += (s, e) =>
            {
                if (OrangeAnimationProgressBar != null)
                {
                    OrangeAnimationProgressBar.Opacity = FlairX_Mod_Manager.SettingsManager.Current.ShowOrangeAnimation ? 1 : 0;
                }
            };
            
            // Add event to restore view mode button when navigating back to ModGridPage
            contentFrame.Navigated += (s, e) =>
            {
                if (e.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                {
                    // Store reference to current ModGridPage for dynamic filtering
                    CurrentModGridPage = modGridPage;
                    
                    // Restore view mode button from settings when returning to ModGridPage
                    RestoreViewModeButtonFromSettings();
                    
                    // Context menu visibility is now handled automatically by the global refresh system
                }
                else
                {
                    // Clear reference when navigating away
                    CurrentModGridPage = null;
                }
            };
            MainRoot.Loaded += MainRoot_Loaded;
            MainRoot.Loaded += (s, e) =>
            {
                // Ensure settings are loaded before initializing game selection
                SettingsManager.Load();
                
                // Synchronize d3dx.ini with SendF10OnModChange setting on startup
                Pages.GameOverlayPage.EnsureBackgroundKeypressIni(SettingsManager.Current.SendF10OnModChange);
                
                // Restore last position after UI is fully loaded (only if game is selected)
                if (SettingsManager.Current.SelectedGameIndex > 0)
                {
                    RestoreLastPosition();
                }
                
                // Setup crop inspection handler
                SetupCropInspectionHandler();
            };
            SetSearchBoxPlaceholder();
            SetFooterMenuTranslations();
            // Only generate menu if a game is selected
            if (SettingsManager.Current?.SelectedGameIndex > 0)
            {
                _ = GenerateModCharacterMenuAsync();
            }

            // Update All Mods button state based on settings
            UpdateAllModsButtonState();
            
            // Force update button state after UI is fully loaded
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => 
            {
                UpdateAllModsButtonState();
            });

            if (appWindow != null)
            {
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                
                // Set preferred minimum and maximum window sizes
                var presenter = OverlappedPresenter.Create();
                presenter.PreferredMinimumWidth = MIN_WIDTH;
                presenter.PreferredMinimumHeight = MIN_HEIGHT;
                presenter.PreferredMaximumWidth = MAX_WIDTH;
                presenter.PreferredMaximumHeight = MAX_HEIGHT;
                appWindow.SetPresenter(presenter);
            }

            // Set animation based on settings
            if (OrangeAnimationProgressBar != null)
            {
                OrangeAnimationProgressBar.Opacity = FlairX_Mod_Manager.SettingsManager.Current?.ShowOrangeAnimation == true ? 1 : 0;
            }
            
            // Initialize tile format toggle button styles
            UpdateTileFormatButtonStyles();
            
            // Add global keyboard handler for hotkeys - handle at content level
            if (this.Content is FrameworkElement contentElement)
            {
                contentElement.KeyDown += MainWindow_KeyDown;
                
                // Add global keyboard handler for sliding panels
                contentElement.KeyDown += async (s, args) =>
                {
                    try
                    {
                        if (args.Key == Windows.System.VirtualKey.Escape && _openPanelOverlays.Count > 0)
                        {
                            // Check before closing - _openPanelOverlays is cleared inside CloseAllPanelsAsync
                            var modWasInstalled = _openPanelOverlays
                                .SelectMany(o => FindUserControlsInOverlay<Pages.GameBananaBrowserUserControl>(o))
                                .Any(gb => gb._modWasInstalled);

                            await CloseAllPanelsAsync();
                            args.Handled = true;

                            if (modWasInstalled)
                            {
                                await ReloadModsAsync();
                                Logger.LogInfo("Refreshed mod grid after Escape (GameBanana mod was installed)");
                            }
                        }
                    }
                    catch (System.OperationCanceledException)
                    {
                        Logger.LogDebug("MainWindow.handler failed: canceled");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("MainWindow.handler failed", ex);
                    }
                };
            }

            // Initialize global hotkey manager for system-wide hotkeys
            try
            {
                Logger.LogInfo("MainWindow: Starting global hotkey manager initialization");
                _globalHotkeyManager = new GlobalHotkeyManager(this);
                Logger.LogInfo("MainWindow: GlobalHotkeyManager instance created");
                
                // Subclass the window to handle WM_HOTKEY messages
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
                Logger.LogInfo($"MainWindow: Window handle obtained: 0x{windowHandle:X}");
                _wndProcDelegate = WndProc;
                _originalWndProc = SetWindowLongPtr(windowHandle, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
                Logger.LogInfo($"MainWindow: WndProc subclassed, original proc: 0x{_originalWndProc:X}");
                
                _globalHotkeyManager.RegisterAllHotkeys();
                Logger.LogInfo("MainWindow: Global hotkey manager initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize global hotkey manager", ex);
            }
            
            // Initialize global gamepad manager for overlay toggle
            InitializeGlobalGamepad();

            // Pre-initialize overlay window at startup so it has a valid HWND.
            // Show briefly, activate (give it focus), then hide.
            // This registers the overlay as a "known foreground window" with Windows,
            // so subsequent SetForegroundWindow calls work without foreground lock issues.
            if (SettingsManager.Current?.GamepadEnabled == true || SettingsManager.Current?.OverlayHotkeysEnabled == true)
            {
                DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
                {
                    try
                    {
                        if (SettingsManager.Current.SelectedGameIndex > 0)
                        {
                            // Create overlay if it doesn't exist
                            if (_overlayWindow == null)
                            {
                                _overlayWindow = new OverlayWindow(this);
                                _overlayWindow!.ModToggleRequested += OnOverlayModToggleRequested;
                                _overlayWindow!.WindowClosed       += OnOverlayWindowClosed;
                                _overlayWindow!.WindowHidden       += OnOverlayWindowHidden;
                            }

                            // Show briefly, activate, then hide
                            var appWindow = _overlayWindow.GetAppWindow();
                            appWindow?.Show();
                            _overlayWindow.Activate();
                            await Task.Delay(100);
                            appWindow?.Hide();
                            Logger.LogInfo("Overlay pre-initialized with focus at startup");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to pre-initialize overlay", ex);
                    }
                });
            }
        }

        public List<(string Name, string Hotkey)> GetRegisteredHotkeyInfo()
            => _globalHotkeyManager?.GetRegisteredHotkeyInfo() ?? new();

    }

    // Helper class for system dispatcher queue
    class WindowsSystemDispatcherQueueHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            internal int dwSize;
            internal int threadType;
            internal int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

        object? m_dispatcherQueueController = null;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                // one already exists, so we'll just use it.
                return;
            }

            if (m_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTAT_COM_STA

                CreateDispatcherQueueController(options, ref m_dispatcherQueueController!);
            }
        }
    }
}

