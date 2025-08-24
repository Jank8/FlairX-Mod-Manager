using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// MainWindow partial class - UI event handlers and button clicks
    /// </summary>
    public sealed partial class MainWindow : Window
    {
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
            
            // Restore saved game selection first
            RestoreGameSelection();
            
            // Mark initialization as complete after game selection is restored
            _isInitializationComplete = true;
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
                
                // Save current position before restoring (to handle category mode properly)
                SaveCurrentPositionBeforeReload();
                
                // Restore last position instead of always going to All Mods
                RestoreLastPosition();
                
                loadingWindow.Close();
            });
        }

        private void AllModsButton_Click(object sender, RoutedEventArgs e)
        {
            // Unselect selected menu item
            nvSample.SelectedItem = null;
            
            // Use ViewMode from settings to determine navigation
            bool isCategoriesView = SettingsManager.Current.ViewMode == "Categories";
            
            // Save position for reload
            if (isCategoriesView)
            {
                SettingsManager.SaveLastPosition(null, "Categories", "Categories");
            }
            else
            {
                SettingsManager.SaveLastPosition(null, "ModGridPage", "Mods");
            }
            
            // Always navigate to ModGridPage with appropriate parameter
            if (isCategoriesView)
            {
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Categories", new DrillInNavigationTransitionInfo());
            }
            else
            {
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
            }
            
            // Update context menu after navigation (will be handled by contentFrame.Navigated event)
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
                    
                    // Update menu items enabled state with a small delay to ensure menu is fully loaded
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(100); // Small delay to ensure menu items are loaded
                        UpdateMenuItemsEnabledState(isCategoriesView);
                    });
                    
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
                
                // Handle navigation based on game selection
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
                else
                {
                    // No game selected - navigate to welcome page
                    System.Diagnostics.Debug.WriteLine("No game selected - navigating to welcome page");
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.WelcomePage));
                    
                    // Clear navigation selection
                    nvSample.SelectedItem = null;
                }
                
                System.Diagnostics.Debug.WriteLine($"Game switched successfully. New paths:");
                System.Diagnostics.Debug.WriteLine($"  Mods: '{SettingsManager.GetCurrentXXMIModsDirectory()}'");
                System.Diagnostics.Debug.WriteLine($"  ModLibrary: '{SettingsManager.GetCurrentModLibraryDirectory()}'");
                System.Diagnostics.Debug.WriteLine($"  D3DX INI: '{SettingsManager.Current.StatusKeeperD3dxUserIniPath}'");
            }
        }

        private void LauncherFabBorder_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            try
            {
                string exePath = GetXXMILauncherPath();
                
                if (File.Exists(exePath))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(exePath) ?? PathManager.GetAbsolutePath(".")
                    };
                    Process.Start(psi);
                }
                else
                {
                    // Show download info dialog
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    var dialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "XXMI_Launcher_Not_Found"),
                        Content = CreateXXMIDownloadContent(exePath),
                        CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                        XamlRoot = this.Content.XamlRoot
                    };
                    _ = dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error launching XXMI Launcher", ex);
            }
        }

        private void OpenModLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string modLibraryPath = SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrWhiteSpace(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                
                if (!Directory.Exists(modLibraryPath))
                {
                    Directory.CreateDirectory(modLibraryPath);
                }
                
                Process.Start("explorer.exe", modLibraryPath);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error opening mod library", ex);
            }
        }

        private async void RestartAppButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "SettingsPage_RestartApp_Title"),
                    Content = SharedUtilities.GetTranslation(lang, "SettingsPage_RestartApp_Content"),
                    PrimaryButtonText = SharedUtilities.GetTranslation(lang, "SettingsPage_RestartApp_Confirm"),
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "Cancel"),
                    XamlRoot = this.Content.XamlRoot
                };
                
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    RestartApplication();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in RestartAppButton_Click", ex);
            }
        }

        private void RestartApplication()
        {
            try
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(exePath) ?? PathManager.GetAbsolutePath(".")
                    };
                    Process.Start(psi);
                }
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error restarting application", ex);
                // Fallback - just exit
                Application.Current.Exit();
            }
        }

        private void LauncherFabBorder_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
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

        private void LauncherFabBorder_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            LauncherFabIcon.Glyph = "\uF5B0";
        }

        private void LauncherFabBorder_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            LauncherFabIcon.Glyph = "\uE768";
        }

        private void LauncherFabBorder_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Don't handle wheel events - let them bubble through to the page content below
            e.Handled = false;
        }

        private void ZoomIndicatorBorder_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Don't handle wheel events - let them bubble through to the page content below
            e.Handled = false;
        }

        public void UpdateZoomIndicator(double zoomLevel)
        {
            if (ZoomIndicatorText != null && ZoomIndicatorBorder != null)
            {
                ZoomIndicatorText.Text = $"{(int)(zoomLevel * 100)}%";
                
                // Only show zoom indicator if zoom is enabled in settings
                bool zoomEnabled = SettingsManager.Current.ModGridZoomEnabled;
                bool gameSelected = SettingsManager.Current.SelectedGameIndex > 0;
                
                // Hide indicator if zoom is disabled, no game selected, or at 100% zoom
                bool shouldShow = zoomEnabled && gameSelected && Math.Abs(zoomLevel - 1.0) >= 0.001;
                
                ZoomIndicatorBorder.Visibility = shouldShow ? 
                    Microsoft.UI.Xaml.Visibility.Visible : 
                    Microsoft.UI.Xaml.Visibility.Collapsed;
            }
        }

        private void CleanupSymlinksForGameSwitch()
        {
            try
            {
                // Get current XXMI mods directory (before switching)
                var currentModsDir = SettingsManager.GetCurrentXXMIModsDirectory();
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
    }
}