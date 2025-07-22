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

namespace ZZZ_Mod_Manager_X
{
    public sealed partial class MainWindow : Window
    {
        private const int MIN_WIDTH = 1280;
        private const int MIN_HEIGHT = 720;
        private const int MAX_WIDTH = 20000;
        private const int MAX_HEIGHT = 15000;

        private List<NavigationViewItem> _allMenuItems = new();
        private List<NavigationViewItem> _allFooterItems = new();

        private bool _isShowActiveModsHovered = false;

        public MainWindow()
        {
            InitializeComponent();

            // Set AllModsButton translation
            AllModsButton.Content = LanguageManager.Instance.T("All_Mods");
            // Set button tooltip translations
            ToolTipService.SetToolTip(ReloadModsButton, LanguageManager.Instance.T("Reload_Mods_Tooltip"));
            ToolTipService.SetToolTip(OpenModLibraryButton, LanguageManager.Instance.T("Open_ModLibrary_Tooltip"));
            ToolTipService.SetToolTip(LauncherFabBorder, LanguageManager.Instance.T("Launcher_Tooltip"));
            ToolTipService.SetToolTip(ShowActiveModsButton, LanguageManager.Instance.T("ShowActiveModsButton_Tooltip"));

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            // Set window icon on taskbar
            appWindow.SetIcon("Assets\\appicon.png");
            


            // Force theme on startup according to user settings
            var theme = ZZZ_Mod_Manager_X.SettingsManager.Current.Theme;
            if (this.Content is FrameworkElement root)
            {
                if (theme == "Light")
                {
                    root.RequestedTheme = ElementTheme.Light;
                    appWindow.TitleBar.ButtonForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonHoverForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonPressedForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonBackgroundColor = Colors.White;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 230, 230, 230); // Light gray
                    appWindow.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 210, 210, 210); // Lighter gray
                }
                else if (theme == "Dark")
                {
                    root.RequestedTheme = ElementTheme.Dark;
                    appWindow.TitleBar.ButtonForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 50, 50, 50); // Dark gray
                    appWindow.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30); // Darker gray
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

            nvSample.Loaded += NvSample_Loaded;
            nvSample.Loaded += (s, e) =>
            {
                var progressBar = GetOrangeAnimationProgressBar();
                if (progressBar != null)
                {
                    progressBar.Opacity = ZZZ_Mod_Manager_X.SettingsManager.Current.ShowOrangeAnimation ? 1 : 0;
                }
            };
            MainRoot.Loaded += MainRoot_Loaded;
            SetSearchBoxPlaceholder();
            SetFooterMenuTranslations();
            _ = GenerateModCharacterMenuAsync();

            // Update All Mods button state based on settings
            UpdateAllModsButtonState();

            // Set main page to All Mods (only if not disabled)
            if (!ZZZ_Mod_Manager_X.SettingsManager.Current.DisableAllModsView)
            {
                contentFrame.Navigate(typeof(ZZZ_Mod_Manager_X.Pages.ModGridPage), null);
            }
            else
            {
                // Navigate to Active mods instead if All Mods is disabled
                contentFrame.Navigate(typeof(ZZZ_Mod_Manager_X.Pages.ModGridPage), "Active");
            }

            appWindow.Resize(new Windows.Graphics.SizeInt32(1650, 820));
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

            // Center window on screen
            CenterWindow(appWindow);

            // Set animation based on settings
            var progressBar = GetOrangeAnimationProgressBar();
            if (progressBar != null)
            {
                progressBar.Opacity = ZZZ_Mod_Manager_X.SettingsManager.Current.ShowOrangeAnimation ? 1 : 0;
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
        }

        private void SetSearchBoxPlaceholder()
        {
            if (SearchBox != null)
                SearchBox.PlaceholderText = LanguageManager.Instance.T("Search_Placeholder");
        }

        private void SetFooterMenuTranslations()
        {
            if (OtherModsPageItem is NavigationViewItem otherMods)
                otherMods.Content = LanguageManager.Instance.T("Other_Mods");
            if (FunctionsPageItem is NavigationViewItem functions)
                functions.Content = LanguageManager.Instance.T("Functions");
            if (SettingsPageItem is NavigationViewItem settings)
                settings.Content = LanguageManager.Instance.T("SettingsPage_Title");
            
            var presetsItem = nvSample.FooterMenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag as string == "PresetsPage");
            if (presetsItem != null)
                presetsItem.Content = LanguageManager.Instance.T("Presets");
            if (AllModsButton != null)
                AllModsButton.Content = LanguageManager.Instance.T("All_Mods");
            if (ReloadModsButton != null)
                ToolTipService.SetToolTip(ReloadModsButton, LanguageManager.Instance.T("Reload_Mods_Tooltip"));
            if (OpenModLibraryButton != null)
                ToolTipService.SetToolTip(OpenModLibraryButton, LanguageManager.Instance.T("Open_ModLibrary_Tooltip"));
            if (LauncherFabBorder != null)
                ToolTipService.SetToolTip(LauncherFabBorder, LanguageManager.Instance.T("Launcher_Tooltip"));
            if (RestartAppButton != null)
                ToolTipService.SetToolTip(RestartAppButton, LanguageManager.Instance.T("SettingsPage_RestartApp_Tooltip"));
            if (ShowActiveModsButton != null)
                ToolTipService.SetToolTip(ShowActiveModsButton, LanguageManager.Instance.T("ShowActiveModsButton_Tooltip"));
        }

        public void UpdateShowActiveModsButtonIcon()
        {
            if (ShowActiveModsButton == null) return;
            var heartEmpty = ShowActiveModsButton.FindName("HeartEmptyIcon") as FontIcon;
            var heartFull = ShowActiveModsButton.FindName("HeartFullIcon") as FontIcon;
            var heartHover = ShowActiveModsButton.FindName("HeartHoverIcon") as FontIcon;
            bool isActivePage = false;
            if (contentFrame.Content is ZZZ_Mod_Manager_X.Pages.ModGridPage modGridPage)
            {
                isActivePage = modGridPage.GetCategoryTitleText() == LanguageManager.Instance.T("Category_Active_Mods");
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
                    if (selectedTag.StartsWith("Character_"))
                    {
                        var character = selectedTag.Substring("Character_".Length);
                        contentFrame.Navigate(typeof(ZZZ_Mod_Manager_X.Pages.ModGridPage), character, new DrillInNavigationTransitionInfo());
                    }
                    else if (selectedTag == "OtherModsPage")
                    {
                        contentFrame.Navigate(typeof(ZZZ_Mod_Manager_X.Pages.ModGridPage), "Other", new DrillInNavigationTransitionInfo());
                    }
                    else if (selectedTag == "FunctionsPage")
                    {
                        contentFrame.Navigate(typeof(ZZZ_Mod_Manager_X.Pages.FunctionsPage), null, new DrillInNavigationTransitionInfo());
                    }
                    else if (selectedTag == "SettingsPage")
                    {
                        contentFrame.Navigate(typeof(ZZZ_Mod_Manager_X.Pages.SettingsPage), null, new DrillInNavigationTransitionInfo());
                    }
                    else
                    {
                        var pageType = Type.GetType($"ZZZ_Mod_Manager_X.Pages.{selectedTag}");
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

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (_allMenuItems == null || _allFooterItems == null)
                return;

            string query = sender.Text.Trim().ToLower();

            nvSample.MenuItems.Clear();
            nvSample.FooterMenuItems.Clear();

            foreach (var item in _allMenuItems)
            {
                var tag = item.Tag?.ToString();
                if (tag == "OtherModsPage" || tag == "FunctionsPage" || tag == "PresetsPage" || tag == "SettingsPage")
                {
                    nvSample.FooterMenuItems.Add(item);
                    continue;
                }
                if (string.IsNullOrEmpty(query) || (item.Content?.ToString()?.ToLower().Contains(query) ?? false))
                {
                    nvSample.MenuItems.Add(item);
                }
            }
            foreach (var item in _allFooterItems)
            {
                var tag = item.Tag?.ToString();
                if (tag == "OtherModsPage" || tag == "FunctionsPage" || tag == "PresetsPage" || tag == "SettingsPage")
                {
                    if (!nvSample.FooterMenuItems.Contains(item))
                        nvSample.FooterMenuItems.Add(item);
                }
            }
            // Dynamic mod filtering only if enabled in settings and query has at least 3 characters
            if (ZZZ_Mod_Manager_X.SettingsManager.Current.DynamicModSearchEnabled)
            {
                if (!string.IsNullOrEmpty(query) && query.Length >= 3)
                {
                    // Always navigate with slide animation when starting search
                    contentFrame.Navigate(
                        typeof(ZZZ_Mod_Manager_X.Pages.ModGridPage),
                        null,
                        new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
                    
                    // Apply filter after navigation
                    if (contentFrame.Content is ZZZ_Mod_Manager_X.Pages.ModGridPage modGridPage)
                    {
                        modGridPage.FilterMods(query);
                    }
                }
                else if (string.IsNullOrEmpty(query))
                {
                    // Clear search - only filter if we're already on ModGridPage
                    if (contentFrame.Content is ZZZ_Mod_Manager_X.Pages.ModGridPage modGridPage)
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
                // Always navigate with slide animation when starting search
                contentFrame.Navigate(
                    typeof(ZZZ_Mod_Manager_X.Pages.ModGridPage),
                    null,
                    new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
                
                // Apply filter after navigation
                if (contentFrame.Content is ZZZ_Mod_Manager_X.Pages.ModGridPage modGridPage)
                {
                    modGridPage.FilterMods(query);
                }
            }
            else if (string.IsNullOrEmpty(query))
            {
                // Clear search - only if we're already on ModGridPage
                if (contentFrame.Content is ZZZ_Mod_Manager_X.Pages.ModGridPage modGridPage)
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
            SetSearchBoxPlaceholder();
            SetFooterMenuTranslations();
            await GenerateModCharacterMenuAsync();
            
            // Recreate symlinks to ensure they match current active mods state
            ZZZ_Mod_Manager_X.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
            Logger.LogInfo("Symlinks recreated during manager reload");
            
            // Update All Mods button state after reload
            UpdateAllModsButtonState();
            
            nvSample.SelectedItem = null; // Unselect active button
            
            // Navigate based on DisableAllModsView setting
            if (!ZZZ_Mod_Manager_X.SettingsManager.Current.DisableAllModsView)
            {
                contentFrame.Navigate(typeof(ZZZ_Mod_Manager_X.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
            }
            else
            {
                // Navigate to Active mods instead if All Mods is disabled
                contentFrame.Navigate(typeof(ZZZ_Mod_Manager_X.Pages.ModGridPage), "Active", new DrillInNavigationTransitionInfo());
            }
            
            UpdateShowActiveModsButtonIcon();
        }

        private void AllModsButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if All Mods view is disabled
            if (ZZZ_Mod_Manager_X.SettingsManager.Current.DisableAllModsView)
            {
                return; // Do nothing if disabled
            }
            
            // Unselect selected menu item
            nvSample.SelectedItem = null;
            // Navigate to ModGridPage without parameter to show all mods
            contentFrame.Navigate(typeof(ZZZ_Mod_Manager_X.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
            // Update heart button after a short delay to ensure page has loaded
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => UpdateShowActiveModsButtonIcon());
        }

        public void UpdateAllModsButtonState()
        {
            if (AllModsButton != null)
            {
                AllModsButton.IsEnabled = !ZZZ_Mod_Manager_X.SettingsManager.Current.DisableAllModsView;
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
            contentFrame.Navigate(typeof(ZZZ_Mod_Manager_X.Pages.ModGridPage), "Active", new DrillInNavigationTransitionInfo());
            UpdateShowActiveModsButtonIcon();
        }

        public async Task GenerateModCharacterMenuAsync()
        {
            string modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? System.IO.Path.Combine(System.AppContext.BaseDirectory, "ModLibrary");
            if (!System.IO.Directory.Exists(modLibraryPath)) return;
            var characterSet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var modFolders = System.IO.Directory.GetDirectories(modLibraryPath);
            var modCharacterMap = new Dictionary<string, List<string>>(); // character -> list of mod folders
            
            await Task.Run(() =>
            {
                foreach (var dir in modFolders)
                {
                    var modJsonPath = System.IO.Path.Combine(dir, "mod.json");
                    if (!System.IO.File.Exists(modJsonPath)) continue;
                    try
                    {
                        var json = System.IO.File.ReadAllText(modJsonPath);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var character = root.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "other" : "other";
                        var folderName = System.IO.Path.GetFileName(dir);
                        if (!modCharacterMap.ContainsKey(character))
                            modCharacterMap[character] = new List<string>();
                        modCharacterMap[character].Add(folderName);
                        if (!string.Equals(character, "other", StringComparison.OrdinalIgnoreCase))
                            characterSet.Add(character);
                    }
                    catch { /* JSON parsing failed for mod - skip this mod */ }
                }
            });
            // Remove old dynamic menu items
            var staticTags = new HashSet<string> { "OtherModsPage", "FunctionsPage", "PresetsPage" };
            var toRemove = nvSample.MenuItems.OfType<NavigationViewItem>().Where(i => i.Tag is string tag && !staticTags.Contains(tag)).ToList();
            foreach (var item in toRemove)
                nvSample.MenuItems.Remove(item);
            // Add new items
            foreach (var character in characterSet)
            {
                var item = new NavigationViewItem
                {
                    Content = character,
                    Tag = $"Character_{character}",
                    Icon = new FontIcon { Glyph = "\uE8D4" } // Moving list icon
                };
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
                    Content = LanguageManager.Instance.T("Presets"),
                    Tag = "PresetsPage",
                    Icon = new FontIcon { Glyph = "\uE728" } // Presets icon
                };
                int otherModsIndex = nvSample.FooterMenuItems.IndexOf(OtherModsPageItem);
                if (otherModsIndex >= 0)
                    nvSample.FooterMenuItems.Insert(otherModsIndex + 1, presets);
                else
                    nvSample.FooterMenuItems.Add(presets);
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

        public void RefreshUIAfterLanguageChange()
        {
            SetSearchBoxPlaceholder();
            SetFooterMenuTranslations();
            SetPaneButtonTooltips();
            SetCategoryTitles();
            UpdateAllModsButtonState();
            _ = GenerateModCharacterMenuAsync();
            // Refresh page if it's ModGridPage or PresetsPage
            if (contentFrame.Content is ZZZ_Mod_Manager_X.Pages.ModGridPage modGridPage)
            {
                modGridPage.RefreshUIAfterLanguageChange();
            }
            else if (contentFrame.Content is ZZZ_Mod_Manager_X.Pages.PresetsPage presetsPage)
            {
                var updateTexts = presetsPage.GetType().GetMethod("UpdateTexts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                updateTexts?.Invoke(presetsPage, null);
            }
            else if (contentFrame.Content is ZZZ_Mod_Manager_X.Pages.SettingsPage settingsPage)
            {
                var updateTexts = settingsPage.GetType().GetMethod("UpdateTexts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                updateTexts?.Invoke(settingsPage, null);
            }
        }

        private void OpenModLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            var modLibraryPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(modLibraryPath))
                Directory.CreateDirectory(modLibraryPath);
            System.Diagnostics.Process.Start("explorer.exe", modLibraryPath);
        }

        private void LauncherFabBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                var exePath = Path.Combine(AppContext.BaseDirectory, "XXMI", "Resources", "Bin", "XXMI Launcher.exe");
                if (File.Exists(exePath))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory
                    };
                    using var process = System.Diagnostics.Process.Start(psi);
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Launcher not found",
                        Content = $"File not found: {exePath}",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
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

        public void RestartAppButton_Click(object? sender, RoutedEventArgs? e)
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory
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
                    appWindow.TitleBar.ButtonBackgroundColor = Colors.White;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 230, 230, 230);
                    appWindow.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 210, 210, 210);
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

        public Frame? GetContentFrame() => contentFrame;
        public ProgressBar? GetOrangeAnimationProgressBar() => PaneStackPanel.FindName("OrangeAnimationProgressBar") as ProgressBar;


    }
}
