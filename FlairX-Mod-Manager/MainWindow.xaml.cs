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
        private const int MIN_WIDTH = 1280;
        private const int MIN_HEIGHT = 720;
        private Dictionary<string, string> _lang = new();

        private void LoadLanguage()
        {
            _lang = SharedUtilities.LoadLanguageDictionary();
        }
        private const int MAX_WIDTH = 20000;
        private const int MAX_HEIGHT = 15000;

        private List<NavigationViewItem> _allMenuItems = new();
        private List<NavigationViewItem> _allFooterItems = new();


        private bool _isInitializationComplete = false;

        // Backdrop fields
        WindowsSystemDispatcherQueueHelper? wsdqHelper;
        DesktopAcrylicController? acrylicController;
        MicaController? micaController;
        SystemBackdropConfiguration? configurationSource;

        public MainWindow()
        {
            InitializeComponent();
            LoadLanguage();

            // Set button tooltip translations
            ToolTipService.SetToolTip(ReloadModsButton, SharedUtilities.GetTranslation(_lang, "Reload_Mods_Tooltip"));
            ToolTipService.SetToolTip(OpenModLibraryButton, SharedUtilities.GetTranslation(_lang, "Open_ModLibrary_Tooltip"));
            ToolTipService.SetToolTip(LauncherFabBorder, SharedUtilities.GetTranslation(_lang, "Launcher_Tooltip"));
            
            // Initialize view mode from settings
            InitializeViewModeFromSettings();

            // Update game selection ComboBox text
            UpdateGameSelectionComboBoxTexts();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            // Set window icon on taskbar
            appWindow.SetIcon("Assets\\appicon.png");
            
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
                    }
                };
            }
            
            // Save window state when closing
            this.Closed += (sender, args) => SaveWindowState();

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
                if (e.Content is FlairX_Mod_Manager.Pages.ModGridPage)
                {
                    // Restore view mode button from settings when returning to ModGridPage
                    RestoreViewModeButtonFromSettings();
                }
            };
            MainRoot.Loaded += MainRoot_Loaded;
            MainRoot.Loaded += (s, e) =>
            {
                // Ensure settings are loaded before initializing game selection
                SettingsManager.Load();
                
                // Restore last position after UI is fully loaded
                RestoreLastPosition();
            };
            SetSearchBoxPlaceholder();
            SetFooterMenuTranslations();
            _ = GenerateModCharacterMenuAsync();

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
                OrangeAnimationProgressBar.Opacity = FlairX_Mod_Manager.SettingsManager.Current.ShowOrangeAnimation ? 1 : 0;
            }
            
            // Add global keyboard handler for hotkeys - handle at content level
            if (this.Content is FrameworkElement contentElement)
            {
                contentElement.KeyDown += MainWindow_KeyDown;
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
            
            var presetsItem = nvSample.FooterMenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag as string == "PresetsPage");
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
            else if (lastPage == "FunctionsPage")
            {
                // Navigate to Functions page
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.FunctionsPage), null, new DrillInNavigationTransitionInfo());
                var functionsMenuItem = nvSample.FooterMenuItems.OfType<NavigationViewItem>()
                    .FirstOrDefault(item => item.Tag?.ToString() == "FunctionsPage");
                if (functionsMenuItem != null)
                {
                    nvSample.SelectedItem = functionsMenuItem;
                }
            }
            else if (lastPage == "SettingsPage")
            {
                // Navigate to Settings page
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.SettingsPage), null, new DrillInNavigationTransitionInfo());
                // Settings is handled by NavigationView automatically
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
            // Disable/enable scrollable menu entries based on view mode
            foreach (var item in nvSample.MenuItems.OfType<NavigationViewItem>())
            {
                if (item.Tag is string tag && tag.StartsWith("Category_"))
                {
                    item.IsEnabled = !isCategoriesView; // Disable in category mode
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
            // All Mods button is now always enabled since we removed the disable functionality
            if (AllModsButton != null)
            {
                AllModsButton.IsEnabled = true;
                
                // Restore button text based on current view mode
                bool isCategoriesView = SettingsManager.Current.ViewMode == "Categories";
                UpdateAllModsButtonText(isCategoriesView);
            }
        }



        // GenerateModCharacterMenuAsync method moved to MainWindow.UIManagement.cs

        // EnsurePresetsMenuItemExists method moved to MainWindow.UIManagement.cs

        // Menu generation and UI refresh methods moved to MainWindow.UIManagement.cs

        // Launcher FAB and UI events moved to MainWindow.EventHandlers.cs
        // Game selection methods moved to MainWindow.UIManagement.cs

        // Window management and backdrop methods moved to MainWindow.WindowManagement.cs
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
