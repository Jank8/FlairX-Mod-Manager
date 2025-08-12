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

        private bool _isShowActiveModsHovered = false;
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
            ToolTipService.SetToolTip(ShowActiveModsButton, SharedUtilities.GetTranslation(_lang, "ShowActiveModsButton_Tooltip"));
            
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
                var progressBar = GetOrangeAnimationProgressBar();
                if (progressBar != null)
                {
                    progressBar.Opacity = FlairX_Mod_Manager.SettingsManager.Current.ShowOrangeAnimation ? 1 : 0;
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
            };
            SetSearchBoxPlaceholder();
            SetFooterMenuTranslations();
            _ = GenerateModCharacterMenuAsync();

            // Update All Mods button state based on settings
            UpdateAllModsButtonState();

            // Set main page based on ViewMode setting
            if (SettingsManager.Current.ViewMode == "Categories")
            {
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Categories");
            }
            else
            {
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), null);
            }
            
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
            var progressBar = GetOrangeAnimationProgressBar();
            if (progressBar != null)
            {
                progressBar.Opacity = FlairX_Mod_Manager.SettingsManager.Current.ShowOrangeAnimation ? 1 : 0;
            }
            
            // Add global keyboard handler for hotkeys - handle at content level
            if (this.Content is FrameworkElement contentElement)
            {
                contentElement.KeyDown += MainWindow_KeyDown;
            }
        }

        // Global keyboard handler for hotkeys
        private async void MainWindow_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            try
            {
                // Only handle hotkeys if a game is selected
                if (SettingsManager.Current.SelectedGameIndex <= 0)
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

                // Shuffle active mods hotkey (placeholder for future implementation)
                if (!string.IsNullOrEmpty(settings.ShuffleActiveModsHotkey) && 
                    string.Equals(currentHotkey, settings.ShuffleActiveModsHotkey, StringComparison.OrdinalIgnoreCase))
                {
                    e.Handled = true;
                    ExecuteShuffleActiveModsHotkey();
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in global hotkey handler", ex);
            }
        }

        // Execute optimize previews hotkey action
        private async Task ExecuteOptimizePreviewsHotkey()
        {
            try
            {
                Logger.LogInfo("Optimize previews hotkey triggered");
                
                // Navigate to settings page if not already there
                if (!(contentFrame.Content is FlairX_Mod_Manager.Pages.SettingsPage))
                {
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.SettingsPage), null, new DrillInNavigationTransitionInfo());
                    // Wait a moment for navigation to complete
                    await Task.Delay(100);
                }

                // Get the settings page and trigger optimize previews
                if (contentFrame.Content is FlairX_Mod_Manager.Pages.SettingsPage settingsPage)
                {
                    // Use reflection to call the private OptimizePreviewsButton_Click method
                    var optimizeMethod = settingsPage.GetType().GetMethod("OptimizePreviewsButton_Click", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (optimizeMethod != null)
                    {
                        optimizeMethod.Invoke(settingsPage, new object[] { settingsPage, new RoutedEventArgs() });
                        Logger.LogInfo("Optimize previews started via hotkey");
                    }
                    else
                    {
                        Logger.LogError("Could not find OptimizePreviewsButton_Click method");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error executing optimize previews hotkey", ex);
            }
        }

        // Execute reload manager hotkey action
        private async Task ExecuteReloadManagerHotkey()
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

        // Execute shuffle active mods hotkey action (placeholder for future implementation)
        private void ExecuteShuffleActiveModsHotkey()
        {
            try
            {
                Logger.LogInfo("Shuffle active mods hotkey triggered (not yet implemented)");
                
                // Show a notification that this feature will be implemented later
                var lang = SharedUtilities.LoadLanguageDictionary();
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "Information"),
                    Content = "Shuffle active mods functionality will be implemented in a future update.",
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                    XamlRoot = this.Content.XamlRoot
                };
                _ = dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error executing shuffle active mods hotkey", ex);
            }
        }

        private void UpdateGameSelectionComboBoxTexts()
        {
            if (GameSelectionComboBox?.Items != null && GameSelectionComboBox.Items.Count > 0)
            {
                // Update the first item (SELECT GAME) with translation
                if (GameSelectionComboBox.Items[0] is ComboBoxItem selectGameItem)
                {
                    if (selectGameItem.Content is StackPanel stackPanel)
                    {
                        var textBlock = stackPanel.Children.OfType<TextBlock>().FirstOrDefault();
                        if (textBlock != null)
                        {
                            textBlock.Text = SharedUtilities.GetTranslation(_lang, "SelectGame_Placeholder");
                        }
                    }
                }
            }
        }

        private void CenterWindow(AppWindow appWindow)
        {
            var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest)?.WorkArea;
            if (area == null) return;
            appWindow.Move(new Windows.Graphics.PointInt32(
                (area.Value.Width - appWindow.Size.Width) / 2,
                (area.Value.Height - appWindow.Size.Height) / 2));
        }

        private string GetXXMILauncherPath()
        {
            // Try to derive launcher path from XXMI Mods Directory setting
            var xxmiModsDir = FlairX_Mod_Manager.SettingsManager.Current.XXMIModsDirectory;
            
            if (!string.IsNullOrEmpty(xxmiModsDir))
            {
                // XXMI Mods Directory is typically: XXMI\ZZMI\Mods
                // Launcher is at: XXMI\Resources\Bin\XXMI Launcher.exe
                // So we need to go up from Mods -> ZZMI -> XXMI, then down to Resources\Bin
                
                var xxmiModsPath = Path.IsPathRooted(xxmiModsDir) ? xxmiModsDir : PathManager.GetAbsolutePath(xxmiModsDir);
                
                // Navigate up to find XXMI root directory
                var currentDir = new DirectoryInfo(xxmiModsPath);
                while (currentDir != null && currentDir.Name != "XXMI")
                {
                    currentDir = currentDir.Parent;
                }
                
                if (currentDir != null && currentDir.Name == "XXMI")
                {
                    var launcherPath = Path.Combine(currentDir.FullName, "Resources", "Bin", "XXMI Launcher.exe");
                    if (File.Exists(launcherPath))
                    {
                        return launcherPath;
                    }
                }
            }
            
            // Fallback to default hardcoded path
            return PathManager.GetAbsolutePath(PathManager.CombinePath("XXMI", "Resources", "Bin", "XXMI Launcher.exe"));
        }

        private StackPanel CreateXXMIDownloadContent(string exePath)
        {
            var stackPanel = new StackPanel { Spacing = 12 };
            
            var fileNotFoundText = new TextBlock
            {
                Text = string.Format(SharedUtilities.GetTranslation(_lang, "FileNotFound"), exePath),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            
            var downloadText = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "XXMI_Download_Required"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };
            
            var urlText = new TextBlock
            {
                Text = "https://github.com/SpectrumQT/XXMI-Launcher/releases",
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.CornflowerBlue),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            
            var instructionText = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "XXMI_Download_Instructions"),
                TextWrapping = TextWrapping.Wrap,
                FontStyle = Windows.UI.Text.FontStyle.Italic
            };
            
            stackPanel.Children.Add(fileNotFoundText);
            stackPanel.Children.Add(downloadText);
            stackPanel.Children.Add(urlText);
            stackPanel.Children.Add(instructionText);
            
            return stackPanel;
        }

        private static void LogToGridLog(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logPath = PathManager.GetSettingsPath("GridLog.log");
                var settingsDir = System.IO.Path.GetDirectoryName(logPath);
                
                if (!string.IsNullOrEmpty(settingsDir) && !Directory.Exists(settingsDir))
                {
                    Directory.CreateDirectory(settingsDir);
                }
                
                var logEntry = $"[{timestamp}] {message}\n";
                File.AppendAllText(logPath, logEntry, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write to GridLog: {ex.Message}");
            }
        }

        private void NvSample_Loaded(object sender, RoutedEventArgs e)
        {
            _allMenuItems = nvSample.MenuItems.OfType<NavigationViewItem>().ToList();
            _allFooterItems = nvSample.FooterMenuItems.OfType<NavigationViewItem>().ToList();
            SetFooterMenuTranslations();
        }

        private void MainRoot_Loaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app)
            {
                app.ShowStartupNtfsWarningIfNeeded();
            }
            
            // Restore saved game selection
            RestoreGameSelection();
        }

        private void RestoreGameSelection()
        {
            // Check if settings file exists to determine if this is first launch
            string settingsPath = PathManager.GetSettingsPath();
            bool isFirstLaunch = !File.Exists(settingsPath);
            
            int savedIndex = SettingsManager.Current.SelectedGameIndex;
            System.Diagnostics.Debug.WriteLine($"RestoreGameSelection: isFirstLaunch = {isFirstLaunch}, savedIndex = {savedIndex}");
            
            if (GameSelectionComboBox == null)
            {
                System.Diagnostics.Debug.WriteLine("RestoreGameSelection: ERROR - GameSelectionComboBox is null!");
                return;
            }
            
            // For first launch, keep the default XAML selection (index 0 - "SELECT GAME")
            // For returning users, restore their saved selection
            if (isFirstLaunch)
            {
                System.Diagnostics.Debug.WriteLine("RestoreGameSelection: First launch - keeping default selection (SELECT GAME)");
                // ComboBox already has default selection from XAML, just update UI state
                UpdateUIForGameSelection(false); // No game selected
                return;
            }
            
            // Returning user - restore saved game selection
            System.Diagnostics.Debug.WriteLine($"RestoreGameSelection: Returning user - restoring savedIndex = {savedIndex}");
            
            // Set ComboBox selection without triggering the event
            GameSelectionComboBox.SelectionChanged -= GameSelectionComboBox_SelectionChanged;
            GameSelectionComboBox.SelectedIndex = savedIndex;
            GameSelectionComboBox.SelectionChanged += GameSelectionComboBox_SelectionChanged;
            
            // Update UI state based on whether a game is selected
            bool gameSelected = savedIndex > 0;
            UpdateUIForGameSelection(gameSelected);
            
            // If a game is selected, perform full game initialization
            if (gameSelected)
            {
                string gameTag = SettingsManager.GetGameTagFromIndex(savedIndex);
                System.Diagnostics.Debug.WriteLine($"Initializing restored game: index {savedIndex} (tag: {gameTag})");
                
                // Ensure mod.json files exist in the selected game's directory
                _ = (App.Current as App)?.EnsureModJsonInModLibrary();
                
                // Create default preset for the game if it doesn't exist
                var gridPage = new FlairX_Mod_Manager.Pages.ModGridPage();
                gridPage.SaveDefaultPresetAllInactive();
                
                // Regenerate character menu for the selected game
                System.Diagnostics.Debug.WriteLine("Regenerating character menu for restored game...");
                _ = GenerateModCharacterMenuAsync();
                
                // Start StatusKeeper watcher if enabled
                if (SettingsManager.Current.StatusKeeperDynamicSyncEnabled)
                {
                    System.Diagnostics.Debug.WriteLine("Starting StatusKeeper watcher for restored game...");
                    FlairX_Mod_Manager.Pages.StatusKeeperSyncPage.StartWatcherStatic();
                }
                
                System.Diagnostics.Debug.WriteLine($"Game restored successfully. Current paths:");
                System.Diagnostics.Debug.WriteLine($"  Mods: '{SettingsManager.Current.XXMIModsDirectory}'");
                System.Diagnostics.Debug.WriteLine($"  ModLibrary: '{SettingsManager.Current.ModLibraryDirectory}'");
            }
            
            System.Diagnostics.Debug.WriteLine("RestoreGameSelection: Completed");
            
            // Mark initialization as complete to allow SelectionChanged events
            _isInitializationComplete = true;
        }

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
            if (ShowActiveModsButton != null)
                ToolTipService.SetToolTip(ShowActiveModsButton, SharedUtilities.GetTranslation(_lang, "ShowActiveModsButton_Tooltip"));
            
            // Update view mode tooltip based on current state
            if (ViewModeToggleButton?.Content is FontIcon icon)
            {
                bool isCategoriesView = icon.Glyph == "\uE8B3";
                UpdateViewModeTooltip(isCategoriesView);
            }
        }

        public void UpdateShowActiveModsButtonIcon()
        {
            if (ShowActiveModsButton == null) return;
            var heartEmpty = ShowActiveModsButton.FindName("HeartEmptyIcon") as FontIcon;
            var heartFull = ShowActiveModsButton.FindName("HeartFullIcon") as FontIcon;
            var heartHover = ShowActiveModsButton.FindName("HeartHoverIcon") as FontIcon;
            bool isActivePage = false;
            if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
            {
                isActivePage = modGridPage.GetCategoryTitleText() == SharedUtilities.GetTranslation(_lang, "Category_Active_Mods");
            }
            if (_isShowActiveModsHovered)
            {
                if (heartEmpty != null) heartEmpty.Visibility = Visibility.Collapsed;
                if (heartFull != null) heartFull.Visibility = Visibility.Collapsed;
                if (heartHover != null) heartHover.Visibility = Visibility.Visible;
            }
            else if (isActivePage)
            {
                if (heartEmpty != null) heartEmpty.Visibility = Visibility.Collapsed;
                if (heartFull != null) heartFull.Visibility = Visibility.Visible;
                if (heartHover != null) heartHover.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (heartEmpty != null) heartEmpty.Visibility = Visibility.Visible;
                if (heartFull != null) heartFull.Visibility = Visibility.Collapsed;
                if (heartHover != null) heartHover.Visibility = Visibility.Collapsed;
            }
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer is NavigationViewItem selectedItem)
            {
                string? selectedTag = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(selectedTag))
                {
                    if (selectedTag.StartsWith("Category_"))
                    {
                        var category = selectedTag.Substring("Category_".Length);
                        
                        // If we're already on ModGridPage, just load the category without navigating
                        if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                        {
                            // SEPARATE NAVIGATION BASED ON CURRENT VIEW MODE
                            if (modGridPage.CurrentViewMode == FlairX_Mod_Manager.Pages.ModGridPage.ViewMode.Categories)
                            {
                                modGridPage.LoadCategoryInCategoryMode(category);
                            }
                            else
                            {
                                modGridPage.LoadCategoryInDefaultMode(category);
                            }
                        }
                        else
                        {
                            contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), $"Category:{category}", new DrillInNavigationTransitionInfo());
                        }
                    }
                    else if (selectedTag == "OtherModsPage")
                    {
                        contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Other", new DrillInNavigationTransitionInfo());
                    }
                    else if (selectedTag == "FunctionsPage")
                    {
                        contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.FunctionsPage), null, new DrillInNavigationTransitionInfo());
                    }
                    else if (selectedTag == "SettingsPage")
                    {
                        contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.SettingsPage), null, new DrillInNavigationTransitionInfo());
                        // Force restore view mode from settings when entering settings
                        RestoreViewModeButtonFromSettings();
                        // Also force update button state
                        UpdateAllModsButtonState();
                    }
                    else
                    {
                        var pageType = Type.GetType($"FlairX_Mod_Manager.Pages.{selectedTag}");
                        if (pageType != null)
                        {
                            contentFrame.Navigate(pageType, null, new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Navigation failed: Page type for tag '{selectedTag}' not found.");
                        }
                    }
                }
            }
            UpdateShowActiveModsButtonIcon();
        }

        private void NavigationView_PaneClosing(NavigationView sender, object args)
        {
            // Ukryj tytuł i elementy gdy panel się zamyka
            if (PaneTitleText != null)
                PaneTitleText.Visibility = Visibility.Collapsed;
            if (OrangeAnimationProgressBar != null)
                OrangeAnimationProgressBar.Visibility = Visibility.Collapsed;
            if (PaneContentGrid != null)
                PaneContentGrid.Visibility = Visibility.Collapsed;
            if (PaneButtonsGrid != null)
                PaneButtonsGrid.Visibility = Visibility.Collapsed;
        }

        private void NavigationView_PaneOpening(NavigationView sender, object args)
        {
            // Pokaż tytuł i elementy gdy panel się otwiera
            if (PaneTitleText != null)
                PaneTitleText.Visibility = Visibility.Visible;
            if (OrangeAnimationProgressBar != null)
                OrangeAnimationProgressBar.Visibility = Visibility.Visible;
            if (PaneContentGrid != null)
                PaneContentGrid.Visibility = Visibility.Visible;
            if (PaneButtonsGrid != null)
                PaneButtonsGrid.Visibility = Visibility.Visible;
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            string query = sender.Text.Trim().ToLower();

            // If search is empty, restore all menu items by refreshing the character categories
            // and also clear any existing filter on ModGridPage
            if (string.IsNullOrEmpty(query))
            {
                // First clear the mod filter if we're on ModGridPage
                if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                {
                    modGridPage.FilterMods("");
                }
                
                // Then regenerate the menu on the UI thread (not in Task.Run)
                _ = GenerateModCharacterMenuAsync();
                return;
            }

            // For any non-empty search, we need to regenerate the full menu first
            // then filter it, to ensure we're always filtering from the complete menu
            Task.Run(async () =>
            {
                // First regenerate the complete menu
                await GenerateModCharacterMenuAsync();
                
                // Then apply the filter on the UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    // Get current menu items (now the complete character categories)
                    var currentMenuItems = nvSample.MenuItems.OfType<NavigationViewItem>().ToList();
                    var currentFooterItems = nvSample.FooterMenuItems.OfType<NavigationViewItem>().ToList();

                    nvSample.MenuItems.Clear();
                    nvSample.FooterMenuItems.Clear();

                    // Filter menu items based on search query
                    foreach (var item in currentMenuItems)
                    {
                        var tag = item.Tag?.ToString();
                        if (tag == "OtherModsPage" || tag == "FunctionsPage" || tag == "PresetsPage" || tag == "SettingsPage")
                        {
                            nvSample.FooterMenuItems.Add(item);
                            continue;
                        }
                        if (item.Content?.ToString()?.ToLower().Contains(query) ?? false)
                        {
                            nvSample.MenuItems.Add(item);
                        }
                    }
                    
                    // Always add footer items
                    foreach (var item in currentFooterItems)
                    {
                        var tag = item.Tag?.ToString();
                        if (tag == "OtherModsPage" || tag == "FunctionsPage" || tag == "PresetsPage" || tag == "SettingsPage")
                        {
                            if (!nvSample.FooterMenuItems.Contains(item))
                                nvSample.FooterMenuItems.Add(item);
                        }
                    }
                });
            });

            // Dynamic mod filtering only if enabled in settings and query has at least 3 characters
            if (FlairX_Mod_Manager.SettingsManager.Current.DynamicModSearchEnabled)
            {
                if (!string.IsNullOrEmpty(query) && query.Length >= 3)
                {
                    // Check if we're already on ModGridPage to avoid unnecessary navigation
                    if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                    {
                        // Just apply filter without navigation to preserve focus
                        modGridPage.FilterMods(query);
                    }
                    else
                    {
                        // Navigate to ModGridPage only if we're not already there
                        contentFrame.Navigate(
                            typeof(FlairX_Mod_Manager.Pages.ModGridPage),
                            null,
                            new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
                        
                        // Apply filter after navigation and restore focus to SearchBox
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage newModGridPage)
                            {
                                newModGridPage.FilterMods(query);
                                // Restore focus to search box after navigation
                                RestoreSearchBoxFocus();
                            }
                        });
                    }
                }
                else if (string.IsNullOrEmpty(query))
                {
                    // Clear search - only filter if we're already on ModGridPage
                    if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                    {
                        modGridPage.FilterMods(query);
                    }
                }
            }
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string query = sender.Text.Trim().ToLower();
            
            // Static search requires at least 2 characters
            if (!string.IsNullOrEmpty(query) && query.Length >= 2)
            {
                // Check if we're already on ModGridPage to avoid unnecessary navigation
                if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                {
                    // Just apply filter without navigation to preserve focus
                    modGridPage.FilterMods(query);
                }
                else
                {
                    // Navigate to ModGridPage only if we're not already there
                    contentFrame.Navigate(
                        typeof(FlairX_Mod_Manager.Pages.ModGridPage),
                        null,
                        new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
                    
                    // Apply filter after navigation and restore focus
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage newModGridPage)
                        {
                            newModGridPage.FilterMods(query);
                            // Restore focus to search box after navigation
                            RestoreSearchBoxFocus();
                        }
                    });
                }
            }
            else if (string.IsNullOrEmpty(query))
            {
                // Clear search - only if we're already on ModGridPage
                if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                {
                    modGridPage.FilterMods(query);
                }
            }
        }

        private void SearchBox_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Add logic here if needed
        }

        private void SearchBox_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // Add logic here if needed
        }

        private void RestoreSearchBoxFocus()
        {
            // Restore focus to search box with a small delay to ensure navigation is complete
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (SearchBox != null)
                {
                    SearchBox.Focus(FocusState.Programmatic);
                }
            });
        }

        private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var presenter = appWindow.Presenter as OverlappedPresenter;
            presenter?.Maximize();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var presenter = appWindow.Presenter as OverlappedPresenter;
            presenter?.Minimize();
        }

        private async void RestoreBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await RestoreButtonAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in RestoreBtn_Click", ex);
            }
        }
        
        private async Task RestoreButtonAsync()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var presenter = appWindow.Presenter as OverlappedPresenter;
            presenter?.Minimize();
            await Task.Delay(3000);
            presenter?.Restore();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        private async void ReloadModsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ReloadModsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ReloadModsButton_Click", ex);
            }
        }
        
        private async Task ReloadModsAsync()
        {
            // Show loading window during refresh
            var loadingWindow = new LoadingWindow();
            loadingWindow.Activate();
            
            await Task.Run(async () =>
            {
                try
                {
                    loadingWindow.UpdateStatus("Refreshing manager...");
                    await Task.Delay(100);
                    
                    // Clear JSON cache first to ensure fresh data loading
                    loadingWindow.UpdateStatus("Clearing JSON cache...");
                    LogToGridLog("REFRESH: Clearing JSON cache");
                    FlairX_Mod_Manager.Pages.ModGridPage.ClearJsonCache();
                    LogToGridLog("REFRESH: JSON cache cleared");
                    await Task.Delay(100);
                    
                    // Update mod.json files with namespace info (for new/updated mods)
                    loadingWindow.UpdateStatus("Scanning mod configurations...");
                    LogToGridLog("REFRESH: Updating mod.json files with namespace info");
                    var app = App.Current as App;
                    if (app != null)
                    {
                        await app.EnsureModJsonInModLibrary();
                    }
                    LogToGridLog("REFRESH: Mod.json files updated");
                    await Task.Delay(100);
                    
                    // Preload images again
                    loadingWindow.UpdateStatus("Reloading images...");
                    await PreloadModImages(loadingWindow);
                    
                    loadingWindow.UpdateStatus("Finalizing refresh...");
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error during refresh", ex);
                }
            });
            
            // Update UI on main thread
            this.DispatcherQueue.TryEnqueue(() =>
            {
                SetSearchBoxPlaceholder();
                SetFooterMenuTranslations();
                UpdateGameSelectionComboBoxTexts();
                _ = GenerateModCharacterMenuAsync();
                
                // Recreate symlinks to ensure they match current active mods state
                FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
                Logger.LogInfo("Symlinks recreated during manager reload");
                
                // Update All Mods button state after reload
                UpdateAllModsButtonState();
                
                nvSample.SelectedItem = null; // Unselect active button
                
                // Navigate to All Mods
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
                
                UpdateShowActiveModsButtonIcon();
                loadingWindow.Close();
            });
        }

        private void AllModsButton_Click(object sender, RoutedEventArgs e)
        {
            // Unselect selected menu item
            nvSample.SelectedItem = null;
            
            // TYLKO ViewMode z ustawień decyduje o wszystkim
            bool isCategoriesView = SettingsManager.Current.ViewMode == "Categories";
            
            // Zawsze nawiguj do ModGridPage z odpowiednim parametrem
            if (isCategoriesView)
            {
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Categories", new DrillInNavigationTransitionInfo());
            }
            else
            {
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
            }
            
            // Update heart button after a short delay to ensure page has loaded
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => UpdateShowActiveModsButtonIcon());
        }

        private FlairX_Mod_Manager.Pages.ModGridPage.ViewMode GetCurrentViewMode()
        {
            if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
            {
                return modGridPage.CurrentViewMode;
            }
            
            // Default to mods view
            return FlairX_Mod_Manager.Pages.ModGridPage.ViewMode.Mods;
        }

        private void ViewModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle view mode and update icon
            if (ViewModeToggleButton?.Content is FontIcon icon)
            {
                bool isCurrentlyCategories = icon.Glyph == "\uE8B3";
                
                if (isCurrentlyCategories)
                {
                    // Switch to mods view
                    icon.Glyph = "\uE8A9";
                    
                    // Save view mode to settings
                    SettingsManager.Current.ViewMode = "Mods";
                    SettingsManager.Save();
                    SettingsManager.Load(); // Force reload
                    
                    // Update UI based on settings
                    bool isCategoriesView = SettingsManager.Current.ViewMode == "Categories";
                    UpdateAllModsButtonText(isCategoriesView);
                    UpdateViewModeTooltip(isCategoriesView);
                    UpdateMenuItemsEnabledState(isCategoriesView);
                    
                    // Navigate to ModGridPage if not already there, or change mode if already there
                    if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                    {
                        modGridPage.CurrentViewMode = FlairX_Mod_Manager.Pages.ModGridPage.ViewMode.Mods;
                    }
                    else
                    {
                        // Navigate to ModGridPage with all mods
                        contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
                    }
                }
                else
                {
                    // Switch to categories view
                    icon.Glyph = "\uE8B3";
                    
                    // Save view mode to settings
                    SettingsManager.Current.ViewMode = "Categories";
                    SettingsManager.Save();
                    SettingsManager.Load(); // Force reload
                    
                    // Update UI based on settings
                    bool isCategoriesView = SettingsManager.Current.ViewMode == "Categories";
                    UpdateAllModsButtonText(isCategoriesView);
                    UpdateViewModeTooltip(isCategoriesView);
                    UpdateMenuItemsEnabledState(isCategoriesView);
                    
                    // Navigate to ModGridPage if not already there, or change mode if already there
                    if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                    {
                        modGridPage.CurrentViewMode = FlairX_Mod_Manager.Pages.ModGridPage.ViewMode.Categories;
                    }
                    else
                    {
                        // Navigate to ModGridPage with categories
                        contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Categories", new DrillInNavigationTransitionInfo());
                    }
                }
                
                // Unselect menu item since we're going to all mods/categories
                nvSample.SelectedItem = null;
            }
        }



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

        private void ShowActiveModsButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            _isShowActiveModsHovered = true;
            UpdateShowActiveModsButtonIcon();
        }

        private void ShowActiveModsButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _isShowActiveModsHovered = false;
            UpdateShowActiveModsButtonIcon();
        }

        private void ShowActiveModsButton_Click(object sender, RoutedEventArgs e)
        {
            nvSample.SelectedItem = null; // Unselect active button in menu
            contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Active", new DrillInNavigationTransitionInfo());
            UpdateShowActiveModsButtonIcon();
        }

        public async Task GenerateModCharacterMenuAsync()
        {
            string modLibraryPath = SharedUtilities.GetSafeModLibraryPath();
            if (!System.IO.Directory.Exists(modLibraryPath)) return;
            var categorySet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var modCategoryMap = new Dictionary<string, List<string>>(); // category -> list of mod folders
            
            // Run the file system scanning in background
            await Task.Run(() =>
            {
                // Process category directories (1st level) and mod directories (2nd level)
                foreach (var categoryDir in System.IO.Directory.GetDirectories(modLibraryPath))
                {
                    if (!System.IO.Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = System.IO.Path.GetFileName(categoryDir);
                    var modDirs = System.IO.Directory.GetDirectories(categoryDir);
                    
                    // Only add category if it has mod directories
                    if (modDirs.Length > 0)
                    {
                        categorySet.Add(categoryName);
                        if (!modCategoryMap.ContainsKey(categoryName))
                            modCategoryMap[categoryName] = new List<string>();
                        
                        foreach (var modDir in modDirs)
                        {
                            var folderName = System.IO.Path.GetFileName(modDir);
                            modCategoryMap[categoryName].Add(folderName);
                        }
                    }
                }
            });
            
            // Execute UI updates on the UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                // Remove old dynamic menu items
                var staticTags = new HashSet<string> { "OtherModsPage", "FunctionsPage", "PresetsPage" };
                var toRemove = nvSample.MenuItems.OfType<NavigationViewItem>().Where(i => i.Tag is string tag && !staticTags.Contains(tag)).ToList();
                foreach (var item in toRemove)
                    nvSample.MenuItems.Remove(item);
                // Add new items
                foreach (var category in categorySet)
                {
                    var item = new NavigationViewItem
                    {
                        Content = category,
                        Tag = $"Category_{category}",
                        Icon = new FontIcon { Glyph = "\uE8D4" } // Moving list icon
                    };
                    
                    // Disable menu items in category mode
                    bool isCategoryMode = false;
                    if (ViewModeToggleButton?.Content is FontIcon icon)
                    {
                        isCategoryMode = icon.Glyph == "\uE8B3";
                    }
                    item.IsEnabled = !isCategoryMode;
                    
                    nvSample.MenuItems.Add(item);
                }
                // Set icon (FontIcon) for Other Mods
                if (OtherModsPageItem is NavigationViewItem otherMods)
                    otherMods.Icon = new FontIcon { Glyph = "\uF4A5" }; // SpecialEffectSize
                // Set icon (FontIcon) for Functions
                var functionsMenuItem = nvSample.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag as string == "FunctionsPage");
                if (functionsMenuItem != null)
                    functionsMenuItem.Icon = new FontIcon { Glyph = "\uE95F" };
                // Set icon (FontIcon) for Other Mods (duplicate)
                var otherModsMenuItem = nvSample.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag as string == "OtherModsPage");
                if (otherModsMenuItem != null)
                    otherModsMenuItem.Icon = new FontIcon { Glyph = "\uF4A5" };
                // Add Presets button under Other Mods
                if (nvSample.FooterMenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag as string == "PresetsPage") == null)
                {
                    var presets = new NavigationViewItem
                    {
                        Content = SharedUtilities.GetTranslation(_lang, "Presets"),
                        Tag = "PresetsPage",
                        Icon = new FontIcon { Glyph = "\uE728" } // Presets icon
                    };
                    int otherModsIndex = nvSample.FooterMenuItems.IndexOf(OtherModsPageItem);
                    if (otherModsIndex >= 0)
                        nvSample.FooterMenuItems.Insert(otherModsIndex + 1, presets);
                    else
                        nvSample.FooterMenuItems.Add(presets);
                }
            });
        }

        private void EnsurePresetsMenuItemExists()
        {
            // Ensure Presets menu item exists in FooterMenuItems
            if (nvSample?.FooterMenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag as string == "PresetsPage") == null)
            {
                var presets = new NavigationViewItem
                {
                    Content = SharedUtilities.GetTranslation(_lang, "Presets"),
                    Tag = "PresetsPage",
                    Icon = new FontIcon { Glyph = "\uE728" } // Presets icon
                };
                
                // Find OtherModsPageItem and insert after it, or add to end
                if (nvSample?.FooterMenuItems != null)
                {
                    var otherModsItem = nvSample.FooterMenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x?.Tag?.ToString() == "OtherModsPage");
                    if (otherModsItem != null)
                    {
                        int otherModsIndex = nvSample.FooterMenuItems.IndexOf(otherModsItem);
                        if (otherModsIndex >= 0)
                            nvSample.FooterMenuItems.Insert(otherModsIndex + 1, presets);
                        else
                            nvSample.FooterMenuItems.Add(presets);
                    }
                    else
                    {
                        nvSample.FooterMenuItems.Add(presets);
                    }
                }
            }
        }

        private void SetPaneButtonTooltips()
        {
            // Placeholder: UI-dependent implementation if needed
        }
        private void SetCategoryTitles()
        {
            // Placeholder: UI-dependent implementation if needed
        }

        private async Task PreloadModImages(LoadingWindow loadingWindow)
        {
            var modLibraryPath = SharedUtilities.GetSafeModLibraryPath();
            if (!Directory.Exists(modLibraryPath)) return;
            
            var totalMods = 0;
            var processedMods = 0;
            
            // Count total mods first
            foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
            {
                if (Directory.Exists(categoryDir))
                {
                    totalMods += Directory.GetDirectories(categoryDir).Length;
                }
            }
            
            foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
            {
                if (!Directory.Exists(categoryDir)) continue;
                
                foreach (var modDir in Directory.GetDirectories(categoryDir))
                {
                    try
                    {
                        var modJsonPath = System.IO.Path.Combine(modDir, "mod.json");
                        if (!File.Exists(modJsonPath)) continue;
                        
                        var previewPath = System.IO.Path.Combine(modDir, "preview.jpg");
                        if (File.Exists(previewPath))
                        {
                            var dirName = System.IO.Path.GetFileName(modDir);
                        
                        // Load image into cache
                        var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                        using (var stream = File.OpenRead(previewPath))
                        {
                            bitmap.SetSource(stream.AsRandomAccessStream());
                        }
                        
                        // Cache the image
                        ImageCacheManager.CacheImage(previewPath, bitmap);
                        ImageCacheManager.CacheRamImage(dirName, bitmap);
                    }
                    
                    processedMods++;
                    var progress = (double)processedMods / totalMods * 100;
                    
                    loadingWindow.SetIndeterminate(false);
                    loadingWindow.SetProgress(progress);
                    loadingWindow.UpdateStatus($"Loading images... {processedMods}/{totalMods}");
                    
                    // Small delay to prevent overwhelming the system
                    await Task.Delay(10);
                }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error preloading image for {modDir}: {ex.Message}");
                    }
                }
            }
            
            loadingWindow.UpdateStatus("Images loaded successfully!");
            await Task.Delay(500); // Brief pause to show completion
        }

        public void RefreshUIAfterLanguageChange()
        {
            // First reload our own language dictionary
            LoadLanguage();
            
            SetSearchBoxPlaceholder();
            SetFooterMenuTranslations();
            UpdateGameSelectionComboBoxTexts();
            SetPaneButtonTooltips();
            SetCategoryTitles();
            UpdateAllModsButtonState();
            _ = GenerateModCharacterMenuAsync();
            // Refresh page if it's ModGridPage or PresetsPage
            if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
            {
                modGridPage.RefreshUIAfterLanguageChange();
            }
            else if (contentFrame.Content is FlairX_Mod_Manager.Pages.PresetsPage presetsPage)
            {
                var updateTexts = presetsPage.GetType().GetMethod("UpdateTexts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                updateTexts?.Invoke(presetsPage, null);
            }
            else if (contentFrame.Content is FlairX_Mod_Manager.Pages.SettingsPage settingsPage)
            {
                var loadLanguageMethod = settingsPage.GetType().GetMethod("LoadLanguage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var updateTextsMethod = settingsPage.GetType().GetMethod("UpdateTexts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                loadLanguageMethod?.Invoke(settingsPage, null);
                updateTextsMethod?.Invoke(settingsPage, null);
            }
            else if (contentFrame.Content is FlairX_Mod_Manager.Pages.StatusKeeperSyncPage statusKeeperSyncPage)
            {
                var loadLanguageMethod = statusKeeperSyncPage.GetType().GetMethod("LoadLanguage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var updateTextsMethod = statusKeeperSyncPage.GetType().GetMethod("UpdateTexts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                loadLanguageMethod?.Invoke(statusKeeperSyncPage, null);
                updateTextsMethod?.Invoke(statusKeeperSyncPage, null);
            }
            else if (contentFrame.Content is FlairX_Mod_Manager.Pages.StatusKeeperBackupPage statusKeeperBackupPage)
            {
                var loadLanguageMethod = statusKeeperBackupPage.GetType().GetMethod("LoadLanguage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var updateTextsMethod = statusKeeperBackupPage.GetType().GetMethod("UpdateTexts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                loadLanguageMethod?.Invoke(statusKeeperBackupPage, null);
                updateTextsMethod?.Invoke(statusKeeperBackupPage, null);
            }
            else if (contentFrame.Content is FlairX_Mod_Manager.Pages.StatusKeeperLogsPage statusKeeperLogsPage)
            {
                var loadLanguageMethod = statusKeeperLogsPage.GetType().GetMethod("LoadLanguage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var updateTextsMethod = statusKeeperLogsPage.GetType().GetMethod("UpdateTexts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                loadLanguageMethod?.Invoke(statusKeeperLogsPage, null);
                updateTextsMethod?.Invoke(statusKeeperLogsPage, null);
            }
            else if (contentFrame.Content is FlairX_Mod_Manager.Pages.HotkeyFinderPage hotkeyFinderPage)
            {
                var loadLanguageMethod = hotkeyFinderPage.GetType().GetMethod("LoadLanguage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var updateTextsMethod = hotkeyFinderPage.GetType().GetMethod("UpdateTexts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                loadLanguageMethod?.Invoke(hotkeyFinderPage, null);
                updateTextsMethod?.Invoke(hotkeyFinderPage, null);
            }
            else if (contentFrame.Content is FlairX_Mod_Manager.Pages.GBAuthorUpdatePage gbAuthorUpdatePage)
            {
                var loadLanguageMethod = gbAuthorUpdatePage.GetType().GetMethod("LoadLanguage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var updateTextsMethod = gbAuthorUpdatePage.GetType().GetMethod("UpdateTexts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                loadLanguageMethod?.Invoke(gbAuthorUpdatePage, null);
                updateTextsMethod?.Invoke(gbAuthorUpdatePage, null);
            }
            else if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModInfoBackupPage modInfoBackupPage)
            {
                var loadLanguageMethod = modInfoBackupPage.GetType().GetMethod("LoadLanguage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var updateTextsMethod = modInfoBackupPage.GetType().GetMethod("UpdateTexts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                loadLanguageMethod?.Invoke(modInfoBackupPage, null);
                updateTextsMethod?.Invoke(modInfoBackupPage, null);
            }
            else if (contentFrame.Content is FlairX_Mod_Manager.Pages.WelcomePage welcomePage)
            {
                var updateTextsMethod = welcomePage.GetType().GetMethod("UpdateTexts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                updateTextsMethod?.Invoke(welcomePage, null);
            }
        }

        private void OpenModLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            var modLibraryPath = SettingsManager.Current.ModLibraryDirectory;
            
            // If no game selected or path is empty, fall back to root ModLibrary
            if (string.IsNullOrEmpty(modLibraryPath))
            {
                modLibraryPath = SharedUtilities.GetSafeModLibraryPath();
            }
            
            if (!Directory.Exists(modLibraryPath))
                Directory.CreateDirectory(modLibraryPath);
            System.Diagnostics.Process.Start("explorer.exe", modLibraryPath);
        }

        private void LauncherFabBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                var exePath = GetXXMILauncherPath();
                if (File.Exists(exePath))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(exePath) ?? PathManager.GetAbsolutePath(".")
                    };
                    using var process = System.Diagnostics.Process.Start(psi);
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(_lang, "LauncherNotFound"),
                        Content = CreateXXMIDownloadContent(exePath),
                        PrimaryButtonText = SharedUtilities.GetTranslation(_lang, "Download_XXMI"),
                        CloseButtonText = SharedUtilities.GetTranslation(_lang, "OK"),
                        XamlRoot = this.Content.XamlRoot
                    };
                    
                    dialog.PrimaryButtonClick += (s, e) =>
                    {
                        try
                        {
                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "https://github.com/SpectrumQT/XXMI-Launcher/releases",
                                UseShellExecute = true
                            };
                            System.Diagnostics.Process.Start(psi);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Failed to start process", ex);
                        }
                    };
                    
                    _ = dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                _ = dialog.ShowAsync();
            }
        }

        private void LauncherFabBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            LauncherFabIcon.Glyph = "\uF5B0";
        }

        private void LauncherFabBorder_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            LauncherFabIcon.Glyph = "\uE768";
        }

        private void LauncherFabBorder_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            // Don't handle wheel events - let them bubble through to the page content below
            e.Handled = false;
        }

        private void ZoomIndicatorBorder_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            // Don't handle wheel events - let them bubble through to the page content below
            e.Handled = false;
        }

        public void UpdateZoomIndicator(double zoomLevel)
        {
            if (ZoomIndicatorText != null && ZoomIndicatorBorder != null)
            {
                ZoomIndicatorText.Text = $"{(int)(zoomLevel * 100)}%";
                
                // Hide indicator at 100% zoom, show at other levels
                ZoomIndicatorBorder.Visibility = Math.Abs(zoomLevel - 1.0) < 0.001 ? 
                    Microsoft.UI.Xaml.Visibility.Collapsed : 
                    Microsoft.UI.Xaml.Visibility.Visible;
            }
        }

        public void RestartAppButton_Click(object? sender, RoutedEventArgs? e)
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? PathManager.GetAbsolutePath(".")
                };
                System.Diagnostics.Process.Start(psi);
            }
            Application.Current.Exit();
        }

        public void ApplyThemeToTitleBar(string theme)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (this.Content is FrameworkElement root)
            {
                if (theme == "Light")
                {
                    appWindow.TitleBar.ButtonForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonHoverForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonPressedForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(100, 230, 230, 230);
                    appWindow.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(150, 210, 210, 210);
                }
                else if (theme == "Dark")
                {
                    appWindow.TitleBar.ButtonForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 50, 50, 50);
                    appWindow.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30);
                }
                else
                {
                    appWindow.TitleBar.ButtonForegroundColor = null;
                    appWindow.TitleBar.ButtonHoverForegroundColor = null;
                    appWindow.TitleBar.ButtonPressedForegroundColor = null;
                    appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = null;
                    appWindow.TitleBar.ButtonPressedBackgroundColor = null;
                }
            }
        }

        public void ApplyBackdropEffect(string backdropEffect)
        {
            // Dispose current backdrop controllers
            acrylicController?.Dispose();
            acrylicController = null;
            micaController?.Dispose();
            micaController = null;

            // Clear any background when using backdrop effects (except None)
            if (backdropEffect != "None" && MainRoot != null)
            {
                MainRoot.Background = null;
            }

            // Apply new backdrop effect
            switch (backdropEffect)
            {
                case "Mica":
                    if (MicaController.IsSupported())
                    {
                        if (wsdqHelper == null)
                        {
                            wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                            wsdqHelper.EnsureWindowsSystemDispatcherQueueController();
                        }
                        if (configurationSource == null)
                        {
                            configurationSource = new SystemBackdropConfiguration();
                            Activated += Window_Activated;
                            Closed += Window_Closed;
                            ((FrameworkElement)Content).ActualThemeChanged += Window_ThemeChanged;
                            configurationSource.IsInputActive = true;
                            SetConfigurationSourceTheme();
                        }
                        micaController = new MicaController();
                        micaController.Kind = MicaKind.Base;
                        micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                        micaController.SetSystemBackdropConfiguration(configurationSource);
                    }
                    break;
                case "MicaAlt":
                    if (MicaController.IsSupported())
                    {
                        if (wsdqHelper == null)
                        {
                            wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                            wsdqHelper.EnsureWindowsSystemDispatcherQueueController();
                        }
                        if (configurationSource == null)
                        {
                            configurationSource = new SystemBackdropConfiguration();
                            Activated += Window_Activated;
                            Closed += Window_Closed;
                            ((FrameworkElement)Content).ActualThemeChanged += Window_ThemeChanged;
                            configurationSource.IsInputActive = true;
                            SetConfigurationSourceTheme();
                        }
                        micaController = new MicaController();
                        micaController.Kind = MicaKind.BaseAlt;
                        micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                        micaController.SetSystemBackdropConfiguration(configurationSource);
                    }
                    break;
                case "Acrylic":
                    TrySetAcrylicBackdrop(false); // Base acrylic
                    break;
                case "AcrylicThin":
                    TrySetAcrylicBackdrop(true); // Thin acrylic
                    break;
                case "None":
                    // No backdrop effect - set appropriate background based on theme
                    SetNoneBackgroundForTheme();
                    break;
                default:
                    TrySetAcrylicBackdrop(true); // Default to thin acrylic
                    break;
            }
        }

        public Frame? GetContentFrame() => contentFrame;
        public ProgressBar? GetOrangeAnimationProgressBar() => PaneStackPanel.FindName("OrangeAnimationProgressBar") as ProgressBar;
        
        private void CleanupSymlinksForGameSwitch()
        {
            try
            {
                // Get current XXMI mods directory (before switching)
                var currentModsDir = SettingsManager.Current.XXMIModsDirectory;
                if (string.IsNullOrWhiteSpace(currentModsDir))
                    return;
                
                if (!Directory.Exists(currentModsDir))
                    return;
                
                // Remove all symlinks from current game's directory
                foreach (var dir in Directory.GetDirectories(currentModsDir))
                {
                    if (FlairX_Mod_Manager.Pages.ModGridPage.IsSymlinkStatic(dir))
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                            System.Diagnostics.Debug.WriteLine($"Removed symlink: {dir}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to remove symlink {dir}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during symlink cleanup: {ex.Message}");
            }
        }
        

        
        private void UpdateUIForGameSelection(bool gameSelected)
        {
            try
            {
                // Always keep the game selection ComboBox enabled FIRST
                if (GameSelectionComboBox != null) GameSelectionComboBox.IsEnabled = true;
                
                // Ensure Presets menu item exists before updating UI state
                EnsurePresetsMenuItemExists();
                
                // Enable/disable navigation menu items (but not the entire navigation view)
                if (nvSample != null)
                {
                    // Disable menu items instead of the entire navigation view
                    foreach (var item in nvSample.MenuItems.OfType<NavigationViewItem>())
                    {
                        item.IsEnabled = gameSelected;
                    }
                    foreach (var item in nvSample.FooterMenuItems.OfType<NavigationViewItem>())
                    {
                        item.IsEnabled = gameSelected;
                    }
                }
                
                // Enable/disable search and buttons
                if (SearchBox != null) SearchBox.IsEnabled = gameSelected;
                if (ReloadModsButton != null) ReloadModsButton.IsEnabled = gameSelected;
                if (RestartAppButton != null) RestartAppButton.IsEnabled = gameSelected;
                if (AllModsButton != null) AllModsButton.IsEnabled = gameSelected;
                if (OpenModLibraryButton != null) OpenModLibraryButton.IsEnabled = gameSelected;
                if (ShowActiveModsButton != null) ShowActiveModsButton.IsEnabled = gameSelected;
                
                // Enable/disable launcher FAB
                if (LauncherFabBorder != null) LauncherFabBorder.IsHitTestVisible = gameSelected;
                
                // Enable/disable zoom indicator
                if (ZoomIndicatorBorder != null) ZoomIndicatorBorder.IsHitTestVisible = gameSelected;
                
                // Ensure the game selection ComboBox stays enabled (double-check)
                if (GameSelectionComboBox != null) GameSelectionComboBox.IsEnabled = true;
                
                // If no game selected, show welcome page
                if (!gameSelected && contentFrame != null)
                {
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.WelcomePage));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating UI for game selection: {ex.Message}");
            }
        }

        private void GameSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ignore selection changes during initialization
            if (!_isInitializationComplete)
            {
                System.Diagnostics.Debug.WriteLine("GameSelectionComboBox_SelectionChanged: Ignoring event during initialization");
                return;
            }
            
            if (sender is ComboBox comboBox)
            {
                int selectedIndex = comboBox.SelectedIndex;
                
                // Skip if same game index
                if (selectedIndex == SettingsManager.Current.SelectedGameIndex)
                    return;
                
                bool gameSelected = selectedIndex > 0;
                string gameTag = SettingsManager.GetGameTagFromIndex(selectedIndex);
                
                if (!gameSelected)
                {
                    System.Diagnostics.Debug.WriteLine("Switching to no game selected - clearing paths and disabling UI");
                    UpdateUIForGameSelection(false); // Disable UI first
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Switching to game index {selectedIndex} (tag: {gameTag})");
                    UpdateUIForGameSelection(true); // Enable UI
                }
                
                // Clean up symlinks from previous game before switching
                System.Diagnostics.Debug.WriteLine("Cleaning up symlinks from previous game...");
                CleanupSymlinksForGameSwitch();
                
                // Switch game paths using index
                SettingsManager.SwitchGame(selectedIndex);
                
                // Only restart StatusKeeper watcher if a game is selected
                if (gameSelected && SettingsManager.Current.StatusKeeperDynamicSyncEnabled)
                {
                    System.Diagnostics.Debug.WriteLine("Restarting StatusKeeper watcher for new game...");
                    FlairX_Mod_Manager.Pages.StatusKeeperSyncPage.StopWatcherStatic();
                    FlairX_Mod_Manager.Pages.StatusKeeperSyncPage.StartWatcherStatic();
                }
                else if (!gameSelected)
                {
                    // Stop StatusKeeper watcher when no game is selected
                    FlairX_Mod_Manager.Pages.StatusKeeperSyncPage.StopWatcherStatic();
                }
                
                // Only refresh pages if a game is selected
                if (gameSelected)
                {
                    // Ensure mod.json files exist in the new game's directory
                    _ = (App.Current as App)?.EnsureModJsonInModLibrary();
                    
                    // Create default preset for the new game if it doesn't exist
                    var gridPage = new FlairX_Mod_Manager.Pages.ModGridPage();
                    gridPage.SaveDefaultPresetAllInactive();
                    
                    // Regenerate character menu for the new game
                    System.Diagnostics.Debug.WriteLine("Regenerating character menu for new game...");
                    _ = GenerateModCharacterMenuAsync();
                    
                    // Recreate symlinks for the new game based on active mods
                    System.Diagnostics.Debug.WriteLine("Recreating symlinks for new game...");
                    FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
                    
                    // Always navigate to All Mods when a game is selected
                    // This handles all cases including ModDetailPage (since the current mod might not exist in new game)
                    System.Diagnostics.Debug.WriteLine("Game selected - navigating to All Mods view");
                    AllModsButton_Click(AllModsButton, new RoutedEventArgs());
                }
                
                System.Diagnostics.Debug.WriteLine($"Game switched successfully. New paths:");
                System.Diagnostics.Debug.WriteLine($"  Mods: '{SettingsManager.Current.XXMIModsDirectory}'");
                System.Diagnostics.Debug.WriteLine($"  ModLibrary: '{SettingsManager.Current.ModLibraryDirectory}'");
                System.Diagnostics.Debug.WriteLine($"  D3DX INI: '{SettingsManager.Current.StatusKeeperD3dxUserIniPath}'");
            }
        }

        // Backdrop methods
        bool TrySetAcrylicBackdrop(bool useAcrylicThin)
        {
            if (DesktopAcrylicController.IsSupported())
            {
                wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // Hooking up the policy object
                configurationSource = new SystemBackdropConfiguration();
                Activated += Window_Activated;
                Closed += Window_Closed;
                ((FrameworkElement)Content).ActualThemeChanged += Window_ThemeChanged;

                // Initial configuration state.
                configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                acrylicController = new DesktopAcrylicController();
                acrylicController.Kind = useAcrylicThin ? DesktopAcrylicKind.Thin : DesktopAcrylicKind.Base;

                // Enable the system backdrop.
                acrylicController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                acrylicController.SetSystemBackdropConfiguration(configurationSource);
                return true; // Succeeded.
            }

            return false; // Acrylic is not supported on this system.
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (configurationSource != null)
                configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed
            acrylicController?.Dispose();
            acrylicController = null;
            micaController?.Dispose();
            micaController = null;
            Activated -= Window_Activated;
            configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
            
            // Update background if using "None" effect
            string currentBackdrop = SettingsManager.Current.BackdropEffect ?? "AcrylicThin";
            if (currentBackdrop == "None")
            {
                SetNoneBackgroundForTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            if (configurationSource != null)
            {
                switch (((FrameworkElement)this.Content).ActualTheme)
                {
                    case ElementTheme.Dark: configurationSource.Theme = SystemBackdropTheme.Dark; break;
                    case ElementTheme.Light: configurationSource.Theme = SystemBackdropTheme.Light; break;
                    case ElementTheme.Default: configurationSource.Theme = SystemBackdropTheme.Default; break;
                }
            }
        }

        private void SetNoneBackgroundForTheme()
        {
            if (this.Content is FrameworkElement root && MainRoot != null)
            {
                var currentTheme = root.ActualTheme;
                if (currentTheme == ElementTheme.Light)
                {
                    // Light theme - use a clean white/light gray background
                    MainRoot.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 248, 248, 248));
                }
                else if (currentTheme == ElementTheme.Dark)
                {
                    // Dark theme - use a proper dark background that matches WinUI 3 dark theme
                    MainRoot.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 23, 23, 23));
                }
                else
                {
                    // Auto theme - use system resource
                    MainRoot.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"];
                }
            }
        }

        // Window state management methods
        private void SaveWindowState()
        {
            try
            {
                var appWindow = GetAppWindowForCurrentWindow();
                if (appWindow != null)
                {
                    var settings = SettingsManager.Current;
                    settings.WindowMaximized = appWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped && 
                                              ((OverlappedPresenter)appWindow.Presenter).State == OverlappedPresenterState.Maximized;
                    
                    if (!settings.WindowMaximized)
                    {
                        settings.WindowWidth = appWindow.Size.Width;
                        settings.WindowHeight = appWindow.Size.Height;
                        settings.WindowX = appWindow.Position.X;
                        settings.WindowY = appWindow.Position.Y;
                    }
                    SettingsManager.Save();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save window state: {ex.Message}");
            }
        }
        
        private void RestoreWindowState()
        {
            try
            {
                var appWindow = GetAppWindowForCurrentWindow();
                if (appWindow != null)
                {
                    var settings = SettingsManager.Current;
                    var width = Math.Max(MIN_WIDTH, Math.Min(MAX_WIDTH, (int)settings.WindowWidth));
                    var height = Math.Max(MIN_HEIGHT, Math.Min(MAX_HEIGHT, (int)settings.WindowHeight));
                    var x = settings.WindowX >= 0 ? (int)settings.WindowX : -1;
                    var y = settings.WindowY >= 0 ? (int)settings.WindowY : -1;
                    
                    System.Diagnostics.Debug.WriteLine($"RestoreWindowState: Width={width}, Height={height}, X={x}, Y={y}, Maximized={settings.WindowMaximized}");
                    
                    if (x >= 0 && y >= 0)
                    {
                        appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
                    }
                    else
                    {
                        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
                        CenterWindowInternal();
                    }
                    
                    if (settings.WindowMaximized && appWindow.Presenter is OverlappedPresenter presenter)
                    {
                        presenter.Maximize();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to restore window state: {ex.Message}");
            }
        }
        
        private void CenterWindowInternal()
        {
            try
            {
                var appWindow = GetAppWindowForCurrentWindow();
                if (appWindow != null)
                {
                    var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
                    if (displayArea != null)
                    {
                        var centerX = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                        var centerY = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
                        appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to center window: {ex.Message}");
            }
        }
        
        private AppWindow? GetAppWindowForCurrentWindow()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(windowId);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_STYLE = -16;
        private const int WS_MAXIMIZEBOX = 0x10000;

        private void DisableMaximizeOnDoubleClick(IntPtr hwnd)
        {
            try
            {
                // Get current window style
                int style = GetWindowLong(hwnd, GWL_STYLE);
                
                // Remove the maximize box style to disable double-click maximize
                style &= ~WS_MAXIMIZEBOX;
                
                // Set the new window style
                SetWindowLong(hwnd, GWL_STYLE, style);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to disable maximize on double-click: {ex.Message}");
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
