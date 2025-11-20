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

namespace FlairX_Mod_Manager
{
    public sealed partial class MainWindow : Window
    {
        private const int MIN_WIDTH = 1300;
        private const int MIN_HEIGHT = 720;
        private Dictionary<string, string> _lang = new();
        
        // Event for notifying about window size changes
        public static event EventHandler? WindowSizeChanged;
        
        // Current ModGridPage reference for dynamic filtering
        public FlairX_Mod_Manager.Pages.ModGridPage? CurrentModGridPage { get; set; }

        private void LoadLanguage()
        {
            _lang = SharedUtilities.LoadLanguageDictionary();
        }
        private const int MAX_WIDTH = 20000;
        private const int MAX_HEIGHT = 15000;

        private List<NavigationViewItem> _allMenuItems = new();
        private List<NavigationViewItem> _allFooterItems = new();


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



        // Method to refresh global hotkeys when settings change
        public void RefreshGlobalHotkeys()
        {
            try
            {
                _globalHotkeyManager?.RefreshHotkeys();
                Logger.LogInfo("Global hotkeys refreshed");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to refresh global hotkeys", ex);
            }
        }

        // Window procedure for handling hotkey messages and tray icon
        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
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
                ToolTipService.SetToolTip(OpenModLibraryButton, SharedUtilities.GetTranslation(_lang, "Open_ModLibrary_Tooltip"));
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
                // Try Assets folder path first
                appWindow.SetIcon("Assets/app.ico");
            }
            catch
            {
                try
                {
                    // Fallback to full path
                    var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                    if (File.Exists(iconPath))
                    {
                        appWindow.SetIcon(iconPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to set window icon", ex);
                }
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
                        SaveWindowState();
                        
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
                if (!_isClosingToTray)
                {
                    SaveWindowState();
                    _globalHotkeyManager?.Dispose();
                    
                    // Clean up tray icon
                    if (_trayIconCreated)
                    {
                        Shell_NotifyIcon(NIM_DELETE, ref _notifyIconData);
                        _trayIconCreated = false;
                    }
                    
                    // Dispose icon
                    _trayIcon?.Dispose();
                    _trayIcon = null;
                }
            };

            nvSample.Loaded += NvSample_Loaded;
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
                
                // Restore last position after UI is fully loaded (only if game is selected)
                if (SettingsManager.Current.SelectedGameIndex > 0)
                {
                    RestoreLastPosition();
                }
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
            
            // Add global keyboard handler for hotkeys - handle at content level
            if (this.Content is FrameworkElement contentElement)
            {
                contentElement.KeyDown += MainWindow_KeyDown;
            }

            // Initialize global hotkey manager for system-wide hotkeys
            try
            {
                _globalHotkeyManager = new GlobalHotkeyManager(this);
                
                // Subclass the window to handle WM_HOTKEY messages
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
                _wndProcDelegate = WndProc;
                _originalWndProc = SetWindowLongPtr(windowHandle, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
                
                _globalHotkeyManager.RegisterAllHotkeys();
                Logger.LogInfo("Global hotkey manager initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize global hotkey manager", ex);
            }
        }

        // Hotkey methods moved to MainWindow.Hotkeys.cs partial class

        // UI Management methods moved to MainWindow.UIManagement.cs

        private void SetSearchBoxPlaceholder()
        {
            if (SearchBox != null)
                SearchBox.PlaceholderText = SharedUtilities.GetTranslation(_lang, "Search_Placeholder");
        }

        private void SetFooterMenuTranslations()
        {
            if (OtherModsPageItem is NavigationViewItem otherMods)
                otherMods.Content = SharedUtilities.GetTranslation(_lang, "Other_Mods");
            if (FunctionsPageItem is NavigationViewItem functions)
                functions.Content = SharedUtilities.GetTranslation(_lang, "Functions");
            if (SettingsPageItem is NavigationViewItem settings)
                settings.Content = SharedUtilities.GetTranslation(_lang, "SettingsPage_Title");
            
            var presetsItem = nvSample.FooterMenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag as string == "PresetsUserControl");
            if (presetsItem != null)
                presetsItem.Content = SharedUtilities.GetTranslation(_lang, "Presets");
            if (AllModsButton != null)
                AllModsButton.Content = SharedUtilities.GetTranslation(_lang, "All_Mods");
            if (ReloadModsButton != null)
                ToolTipService.SetToolTip(ReloadModsButton, SharedUtilities.GetTranslation(_lang, "Reload_Mods_Tooltip"));
            if (OpenModLibraryButton != null)
                ToolTipService.SetToolTip(OpenModLibraryButton, SharedUtilities.GetTranslation(_lang, "Open_ModLibrary_Tooltip"));
            if (LauncherFabBorder != null)
                ToolTipService.SetToolTip(LauncherFabBorder, SharedUtilities.GetTranslation(_lang, "Launcher_Tooltip"));
            if (RestartAppButton != null)
                ToolTipService.SetToolTip(RestartAppButton, SharedUtilities.GetTranslation(_lang, "SettingsPage_RestartApp_Tooltip"));

            
            // Update view mode tooltip based on current state
            if (ViewModeToggleButton?.Content is FontIcon icon)
            {
                bool isCategoriesView = icon.Glyph == "\uE8B3";
                UpdateViewModeTooltip(isCategoriesView);
            }
        }



        // Navigation methods moved to MainWindow.Navigation.cs partial class

        // SearchBox methods moved to MainWindow.Navigation.cs

        // Window control button methods moved to MainWindow.WindowManagement.cs

        // Event handler methods moved to MainWindow.EventHandlers.cs

        private FlairX_Mod_Manager.Pages.ModGridPage.ViewMode GetCurrentViewMode()
        {
            if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
            {
                return modGridPage.CurrentViewMode;
            }
            
            // Default to mods view
            return FlairX_Mod_Manager.Pages.ModGridPage.ViewMode.Mods;
        }

        private string GetCurrentViewModeString()
        {
            // Use SettingsManager ViewMode instead of ModGridPage CurrentViewMode
            // because ModGridPage CurrentViewMode can be temporarily changed
            return SettingsManager.Current.ViewMode;
        }

        private void SaveCurrentPositionBeforeReload()
        {
            // Save current position before reload to ensure we return to the same place
            if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
            {
                var currentViewMode = GetCurrentViewModeString();
                
                // Get current category from CategoryTitle or check if we're showing categories
                var categoryTitle = modGridPage.GetCategoryTitleText();
                var langDict = SharedUtilities.LoadLanguageDictionary();
                var allModsText = SharedUtilities.GetTranslation(langDict, "Category_All_Mods");
                var allCategoriesText = SharedUtilities.GetTranslation(langDict, "All_Categories");
                
                // Check if we're in a specific category (not "All Mods" or "All Categories")
                if (!string.IsNullOrEmpty(categoryTitle) && 
                    categoryTitle != allModsText && 
                    categoryTitle != allCategoriesText)
                {
                    // We're in a specific category - save it
                    SettingsManager.SaveLastPosition(categoryTitle, "ModGridPage", currentViewMode);
                }
                else if (modGridPage.CurrentViewMode == FlairX_Mod_Manager.Pages.ModGridPage.ViewMode.Categories)
                {
                    // We're in category mode but showing all categories
                    SettingsManager.SaveLastPosition(null, "Categories", "Categories");
                }
                else
                {
                    // We're in default mode showing all mods
                    SettingsManager.SaveLastPosition(null, "ModGridPage", "Mods");
                }
            }
        }

        private bool DoesCategoryExist(string category)
        {
            try
            {
                var modLibraryPath = SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                
                if (!Directory.Exists(modLibraryPath))
                    return false;
                
                var categoryPath = Path.Combine(modLibraryPath, category);
                return Directory.Exists(categoryPath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking if category exists: {category}", ex);
                return false;
            }
        }

        private void RestoreLastPosition()
        {
            // Check if a game is selected first
            bool gameSelected = SettingsManager.Current.SelectedGameIndex > 0;
            
            if (!gameSelected)
            {
                // No game selected - show welcome page
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.WelcomePage));
                return;
            }
            
            var (lastCategory, lastPage, lastViewMode) = SettingsManager.GetLastPosition();
            
            if (!string.IsNullOrEmpty(lastCategory))
            {
                if (lastCategory == "Active")
                {
                    // Navigate to Active mods
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Active", new DrillInNavigationTransitionInfo());
                    // Don't select any menu item for Active mods
                }
                else
                {
                    // Check if the category still exists before navigating to it
                    bool categoryExists = DoesCategoryExist(lastCategory);
                    
                    if (categoryExists)
                    {
                        // Navigate to specific category with view mode consideration
                        if (lastViewMode == "Categories")
                        {
                            // In category mode, navigate with CategoryInCategoryMode parameter
                            contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), $"CategoryInCategoryMode:{lastCategory}", new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            // In default mode, navigate with Category parameter
                            contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), $"Category:{lastCategory}", new DrillInNavigationTransitionInfo());
                        }
                        
                        // Try to select the corresponding menu item
                        var categoryMenuItem = nvSample.MenuItems.OfType<NavigationViewItem>()
                            .FirstOrDefault(item => item.Tag?.ToString() == $"Category_{lastCategory}");
                        if (categoryMenuItem != null)
                        {
                            nvSample.SelectedItem = categoryMenuItem;
                        }
                        else if (lastCategory == "Other")
                        {
                            // Select Other Mods footer item
                            var otherMenuItem = nvSample.FooterMenuItems.OfType<NavigationViewItem>()
                                .FirstOrDefault(item => item.Tag?.ToString() == "OtherModsPage");
                            if (otherMenuItem != null)
                            {
                                nvSample.SelectedItem = otherMenuItem;
                            }
                        }
                    }
                    else
                    {
                        // Category doesn't exist anymore - use fallback based on view mode
                        var currentViewMode = SettingsManager.Current.ViewMode;
                        if (currentViewMode == "Categories" || lastViewMode == "Categories")
                        {
                            // Fallback to All Categories in category mode
                            contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Categories", new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            // Fallback to All Mods in default mode
                            contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
                        }
                        
                        // Clear the invalid position
                        SettingsManager.ClearLastPosition();
                    }
                }
            }
            else if (lastPage == "Categories")
            {
                // Navigate to All Categories view
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Categories", new DrillInNavigationTransitionInfo());
            }
            else if (lastPage == "FunctionsUserControl")
            {
                // Don't auto-open Functions panel on startup
                // Instead, navigate to default page and let user open Functions manually
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
            }
            else if (lastPage == "SettingsUserControl")
            {
                // Don't start on Settings page to avoid category list duplicates
                // Instead, navigate to All Mods based on current view mode
                var currentViewMode = SettingsManager.Current.ViewMode;
                if (currentViewMode == "Categories")
                {
                    // Navigate to All Categories in category mode
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Categories", new DrillInNavigationTransitionInfo());
                }
                else
                {
                    // Navigate to All Mods in default mode
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
                }
            }
            else
            {
                // Default based on view mode - use current ViewMode from settings
                var currentViewMode = SettingsManager.Current.ViewMode;
                if (currentViewMode == "Categories" || lastViewMode == "Categories")
                {
                    // Default to All Categories in category mode
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Categories", new DrillInNavigationTransitionInfo());
                }
                else
                {
                    // Default to All Mods in default mode
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
                }
            }
        }

        // ViewMode and GameSelection methods moved to MainWindow.EventHandlers.cs



        public void UpdateViewModeButtonIcon(bool isCategoriesView)
        {
            if (ViewModeToggleButton?.Content is FontIcon icon)
            {
                icon.Glyph = isCategoriesView ? "\uE8B3" : "\uE8A9";
            }
            
            // Update the All Mods button text
            UpdateAllModsButtonText(isCategoriesView);
            
            // Update the tooltip
            UpdateViewModeTooltip(isCategoriesView);
        }

        public bool IsCurrentlyInCategoryMode()
        {
            if (ViewModeToggleButton?.Content is FontIcon icon)
            {
                return icon.Glyph == "\uE8B3";
            }
            return false;
        }

        private void InitializeViewModeFromSettings()
        {
            // Initialize view mode button from settings
            bool isCategoriesView = SettingsManager.Current.ViewMode == "Categories";
            
            if (ViewModeToggleButton?.Content is FontIcon icon)
            {
                icon.Glyph = isCategoriesView ? "\uE8B3" : "\uE8A9";
            }
            
            UpdateAllModsButtonText(isCategoriesView);
            UpdateViewModeTooltip(isCategoriesView);
            UpdateMenuItemsEnabledState(isCategoriesView);
        }

        public void RestoreViewModeButtonFromSettings()
        {
            // Restore view mode button from settings (call this when button gets corrupted)
            InitializeViewModeFromSettings();
        }

        private void UpdateViewModeTooltip(bool isCategoriesView)
        {
            if (ViewModeToggleButton != null)
            {
                var tooltipKey = isCategoriesView ? "ViewModeToggle_CategoryMode_Tooltip" : "ViewModeToggle_DefaultMode_Tooltip";
                var tooltip = SharedUtilities.GetTranslation(_lang, tooltipKey);
                ToolTipService.SetToolTip(ViewModeToggleButton, tooltip);
            }
        }

        private void UpdateMenuItemsEnabledState(bool isCategoriesView)
        {
            // Hide/show scrollable menu entries based on view mode
            foreach (var item in nvSample.MenuItems.OfType<NavigationViewItem>())
            {
                if (item.Tag is string tag && tag.StartsWith("Category_"))
                {
                    item.Visibility = isCategoriesView ? Visibility.Collapsed : Visibility.Visible;
                }
            }
        }

        private void UpdateAllModsButtonText(bool isCategoriesView)
        {
            if (AllModsButton != null)
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                if (isCategoriesView)
                {
                    AllModsButton.Content = SharedUtilities.GetTranslation(lang, "All_Categories");
                }
                else
                {
                    AllModsButton.Content = SharedUtilities.GetTranslation(lang, "All_Mods");
                }
            }
        }

        public void UpdateAllModsButtonState()
        {
            if (AllModsButton != null)
            {
                // Enable button only if a game is selected
                bool gameSelected = SettingsManager.Current.SelectedGameIndex > 0;
                AllModsButton.IsEnabled = gameSelected;
                
                // Restore button text based on current view mode
                bool isCategoriesView = SettingsManager.Current.ViewMode == "Categories";
                UpdateAllModsButtonText(isCategoriesView);
            }
        }

        // Enhanced InfoBar methods for better user feedback
        public void ShowInfoBar(string title, string message, Microsoft.UI.Xaml.Controls.InfoBarSeverity severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational, int autoCloseDelayMs = 0)
        {
            Logger.LogInfo($"Showing InfoBar: {severity} - {title}: {message}");
            
            if (MainInfoBar != null)
            {
                MainInfoBar.Title = title;
                MainInfoBar.Message = message;
                MainInfoBar.Severity = severity;
                MainInfoBar.IsOpen = true;
                
                // Auto-close after delay if specified
                if (autoCloseDelayMs > 0)
                {
                    var timer = new System.Threading.Timer(_ => 
                    {
                        DispatcherQueue.TryEnqueue(() => MainInfoBar.IsOpen = false);
                    }, null, autoCloseDelayMs, System.Threading.Timeout.Infinite);
                }
            }
        }

        public void ShowSuccessInfo(string message, int autoCloseDelayMs = 3000)
        {
            ShowInfoBar("Success", message, Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success, autoCloseDelayMs);
        }

        public void ShowWarningInfo(string message, int autoCloseDelayMs = 5000)
        {
            ShowInfoBar("Warning", message, Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning, autoCloseDelayMs);
        }

        public void ShowErrorInfo(string message, int autoCloseDelayMs = 0)
        {
            ShowInfoBar("Error", message, Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error, autoCloseDelayMs);
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
                _notifyIconData.szTip = "FlairX Mod Manager";
                
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
