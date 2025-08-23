using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Storage.Streams;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// MainWindow partial class - UI management, menu generation, and utility functions
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private void UpdateGameSelectionComboBoxTexts()
        {
            if (GameSelectionComboBox?.Items != null && _lang != null)
            {
                // Update the SelectGameText TextBlock directly by name
                if (SelectGameText != null)
                {
                    SelectGameText.Text = SharedUtilities.GetTranslation(_lang, "SelectGame_Placeholder");
                }
            }
        }

        private void CenterWindow()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            
            var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest)?.WorkArea;
            if (area == null) return;
            appWindow.Move(new Windows.Graphics.PointInt32(
                (area.Value.Width - appWindow.Size.Width) / 2,
                (area.Value.Height - appWindow.Size.Height) / 2));
        }

        private string GetXXMILauncherPath()
        {
            var gameTag = SettingsManager.CurrentSelectedGame;
            return gameTag switch
            {
                "GIMI" => PathManager.GetAbsolutePath(@".\XXMI\GIMI\GIMI.exe"),
                "HIMI" => PathManager.GetAbsolutePath(@".\XXMI\HIMI\HIMI.exe"),
                "SRMI" => PathManager.GetAbsolutePath(@".\XXMI\SRMI\SRMI.exe"),
                "WWMI" => PathManager.GetAbsolutePath(@".\XXMI\WWMI\WWMI.exe"),
                "ZZMI" => PathManager.GetAbsolutePath(@".\XXMI\ZZMI\ZZMI.exe"),
                _ => PathManager.GetAbsolutePath(@".\XXMI\ZZMI\ZZMI.exe")
            };
        }

        private StackPanel CreateXXMIDownloadContent(string expectedPath)
        {
            var stackPanel = new StackPanel { Spacing = 10 };
            
            var text1 = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "LauncherNotFoundDescription"),
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
            };
            
            var text2 = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "ExpectedPath") + ": " + expectedPath,
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                FontSize = 12
            };
            
            stackPanel.Children.Add(text1);
            stackPanel.Children.Add(text2);
            
            return stackPanel;
        }

        private void LogToGridLog(string message)
        {
            try
            {
                FlairX_Mod_Manager.Pages.ModGridPage.LogToGridLog(message);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to log to grid: {message}", ex);
            }
        }

        private void RestoreGameSelection()
        {
            try
            {
                if (GameSelectionComboBox != null)
                {
                    var settings = SettingsManager.Current;
                    if (settings != null)
                    {
                        // Temporarily disable initialization flag to prevent event firing during restore
                        bool wasInitComplete = _isInitializationComplete;
                        _isInitializationComplete = false;
                        
                        GameSelectionComboBox.SelectedIndex = settings.SelectedGameIndex;
                        
                        // Restore initialization flag
                        _isInitializationComplete = wasInitComplete;
                        
                        // Update UI based on whether a game is selected
                        bool gameSelected = settings.SelectedGameIndex > 0;
                        UpdateUIForGameSelection(gameSelected);
                        
                        // Navigate to appropriate page based on game selection
                        if (contentFrame != null)
                        {
                            if (gameSelected)
                            {
                                // If game is selected, restore last position will be called later
                                // Don't navigate here to avoid double navigation
                            }
                            else
                            {
                                // Navigate to welcome page if no game selected
                                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.WelcomePage));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to restore game selection", ex);
            }
        }

        public async Task GenerateModCharacterMenuAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var modLibraryPath = SharedUtilities.GetSafeModLibraryPath();
                    if (!Directory.Exists(modLibraryPath))
                    {
                        Logger.LogWarning($"Mod library path does not exist: {modLibraryPath}");
                        return;
                    }

                    var categories = new List<string>();
                    
                    // Get all category directories, excluding "Other"
                    foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
                    {
                        var categoryName = Path.GetFileName(categoryDir);
                        if (!string.Equals(categoryName, "Other", StringComparison.OrdinalIgnoreCase))
                        {
                            categories.Add(categoryName);
                        }
                    }
                    
                    // Sort categories alphabetically
                    categories.Sort(StringComparer.OrdinalIgnoreCase);
                    
                    // Update UI on main thread
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            if (nvSample?.MenuItems != null)
                            {
                                // Clear existing menu items (but preserve footer items)
                                nvSample.MenuItems.Clear();
                                
                                // Add character categories
                                foreach (var category in categories)
                                {
                                    var menuItem = new NavigationViewItem
                                    {
                                        Content = category,
                                        Tag = $"Category_{category}",
                                        Icon = new FontIcon { Glyph = "\uEA8C" } // Character icon
                                    };
                                    nvSample.MenuItems.Add(menuItem);
                                }
                                
                                // Update menu items state based on current view mode
                                var isCategoriesView = SettingsManager.Current?.ViewMode == "Categories";
                                UpdateMenuItemsEnabledState(isCategoriesView);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Error updating menu UI", ex);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error generating mod character menu", ex);
            }
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

        private void UpdateUIForGameSelection(bool gameSelected)
        {
            try
            {
                // Always keep the game selection ComboBox enabled FIRST
                if (GameSelectionComboBox != null) GameSelectionComboBox.IsEnabled = true;
                
                // Ensure Presets menu item exists before updating UI state
                EnsurePresetsMenuItemExists();
                
                // // Enable/disable navigation menu items (but not the entire navigation view)
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
                if (ViewModeToggleButton != null) ViewModeToggleButton.IsEnabled = gameSelected;
                
                // Enable/disable launcher FAB
                if (LauncherFabBorder != null) LauncherFabBorder.IsHitTestVisible = gameSelected;
                
                // Enable/disable zoom indicator based on game selection and zoom settings
                if (ZoomIndicatorBorder != null) 
                {
                    ZoomIndicatorBorder.IsHitTestVisible = gameSelected;
                    // Also update visibility based on zoom settings
                    bool zoomEnabled = SettingsManager.Current.ModGridZoomEnabled;
                    if (!gameSelected || !zoomEnabled)
                    {
                        ZoomIndicatorBorder.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    }
                }
                
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

        public void UpdateOrangeAnimationVisibility(bool isVisible)
        {
            if (OrangeAnimationProgressBar != null)
            {
                OrangeAnimationProgressBar.Opacity = isVisible ? 1 : 0;
            }
        }
    }
}