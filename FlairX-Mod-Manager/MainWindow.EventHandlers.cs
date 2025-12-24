using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using FlairX_Mod_Manager.Services;

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
            // Restore saved game selection first
            RestoreGameSelection();
            
            // Mark initialization as complete after game selection is restored
            _isInitializationComplete = true;
            
            // Auto-detect hotkeys for all mods in background on startup
            _ = Task.Run(async () =>
            {
                try
                {
                    Logger.LogInfo("Starting full hotkey detection on startup");
                    await Pages.HotkeyFinderPage.RefreshAllModsHotkeysStaticAsync();
                    Logger.LogInfo("Completed full hotkey detection on startup");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to auto-detect hotkeys on startup", ex);
                }
            });
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

        public async Task ReloadModsAsync()
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
                        await app.EnsureModJsonInXXMIMods();
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
                
                // No symlink recreation needed - using DISABLED_ prefix system
                Logger.LogInfo("Mod state maintained during manager reload");
                
                // Update All Mods button state after reload
                UpdateAllModsButtonState();
                
                nvSample.SelectedItem = null; // Unselect active button
                
                // Save current position before restoring (to handle category mode properly)
                SaveCurrentPositionBeforeReload();
                
                // Restore last position instead of always going to All Mods
                RestoreLastPosition();
                
                // Auto-detect hotkeys for all mods in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        Logger.LogInfo("Starting full hotkey detection after reload");
                        await Pages.HotkeyFinderPage.RefreshAllModsHotkeysStaticAsync();
                        Logger.LogInfo("Completed full hotkey detection after reload");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to auto-detect hotkeys after reload", ex);
                    }
                });
                
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
                    
                    // Refresh navigation menu to reload category icons and hover previews
                    _ = GenerateModCharacterMenuAsync();
                    
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
                    
                    // Refresh navigation menu to reload category icons and hover previews
                    _ = GenerateModCharacterMenuAsync();
                    
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
                
                // No symlink cleanup needed - using DISABLED_ prefix system
                System.Diagnostics.Debug.WriteLine("Switching games - no cleanup needed...");
                
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
                    _ = (App.Current as App)?.EnsureModJsonInXXMIMods();
                    
                    // Create default preset for the new game if it doesn't exist
                    var gridPage = new FlairX_Mod_Manager.Pages.ModGridPage();
                    gridPage.SaveDefaultPresetAllInactive();
                    
                    // Regenerate character menu for the new game
                    System.Diagnostics.Debug.WriteLine("Regenerating character menu for new game...");
                    _ = GenerateModCharacterMenuAsync();
                    
                    // No symlink recreation needed - using DISABLED_ prefix system
                    System.Diagnostics.Debug.WriteLine("Mod state maintained for new game...");
                    
                    // Always navigate to All Mods when a game is selected
                    // This handles all cases including ModDetailPage (since the current mod might not exist in new game)
                    System.Diagnostics.Debug.WriteLine("Game selected - navigating to All Mods view");
                    AllModsButton_Click(AllModsButton, new RoutedEventArgs());
                    
                    // Show Starter Pack dialog if available and not dismissed
                    _ = ShowStarterPackDialogIfNeededAsync(gameTag);
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
                System.Diagnostics.Debug.WriteLine($"  D3DX INI: '{SettingsManager.Current.StatusKeeperD3dxUserIniPath}'");
            }
        }



        // OpenModLibraryButton removed - mods are now in XXMI/Mods directly
        
        private void OpenGameBananaBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag))
                {
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    ShowInfoBar(
                        SharedUtilities.GetTranslation(lang, "Error"),
                        "Please select a game first.",
                        InfoBarSeverity.Warning
                    );
                    return;
                }

                ShowGameBananaBrowserPanel(gameTag);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error opening GameBanana browser", ex);
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
                // Try multiple methods to get the executable path
                string? exePath = null;
                
                // Method 1: Get from current process
                try
                {
                    exePath = Process.GetCurrentProcess().MainModule?.FileName;
                }
                catch { }
                
                // Method 2: Get from Environment
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Environment.ProcessPath;
                }
                
                // Method 3: Get from AppDomain
                if (string.IsNullOrEmpty(exePath))
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var possibleExe = Path.Combine(baseDir, "FlairX Mod Manager.exe");
                    if (File.Exists(possibleExe))
                    {
                        exePath = possibleExe;
                    }
                }
                
                Logger.LogInfo($"Restart: Executable path = {exePath}");
                
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory
                    };
                    
                    Logger.LogInfo($"Restart: Starting new process...");
                    Process.Start(psi);
                    
                    Logger.LogInfo($"Restart: Exiting current process...");
                    Application.Current.Exit();
                }
                else
                {
                    Logger.LogError($"Restart: Could not find executable at path: {exePath}");
                    
                    // Show error to user
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    ShowErrorInfo("Could not restart - executable not found", 3000);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error restarting application", ex);
                
                // Try fallback with cmd
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var exePath = Path.Combine(baseDir, "FlairX Mod Manager.exe");
                    
                    if (File.Exists(exePath))
                    {
                        // Use cmd to start the app after a delay
                        var psi = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c timeout /t 1 /nobreak >nul && start \"\" \"{exePath}\"",
                            UseShellExecute = true,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        Process.Start(psi);
                        Application.Current.Exit();
                    }
                }
                catch (Exception ex2)
                {
                    Logger.LogError("Fallback restart also failed", ex2);
                }
            }
        }

        private async void LauncherFabBorder_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Logger.LogInfo("LauncherFabBorder_PointerPressed called");
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

                    // Check if Skip XXMI Launcher is enabled
                    Logger.LogInfo($"Skip XXMI Launcher setting: {SettingsManager.Current.SkipXXMILauncherEnabled}");
                    Logger.LogInfo($"Selected game index: {SettingsManager.Current.SelectedGameIndex}");
                    
                    if (SettingsManager.Current.SkipXXMILauncherEnabled)
                    {
                        // Get current game tag (button is only active when game is selected)
                        string gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                        psi.Arguments = $"--nogui --xxmi {gameTag}";
                        Logger.LogInfo($"Launching XXMI Launcher with arguments: {psi.Arguments}");
                    }
                    else
                    {
                        Logger.LogInfo("Launching XXMI Launcher without arguments (normal GUI mode)");
                    }

                    using var process = System.Diagnostics.Process.Start(psi);
                }
                else
                {
                    await ShowXXMIDownloadDialogAsync(exePath);
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

        private async Task ShowXXMIDownloadDialogAsync(string expectedPath)
        {
            var progressBar = new ProgressBar
            {
                IsIndeterminate = false,
                Value = 0,
                Minimum = 0,
                Maximum = 100,
                Width = 300,
                Visibility = Visibility.Collapsed
            };
            
            var statusText = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "LauncherNotFoundDescription"),
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            
            var contentPanel = new StackPanel { Spacing = 8 };
            contentPanel.Children.Add(statusText);
            contentPanel.Children.Add(progressBar);

            var dialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(_lang, "LauncherNotFound"),
                Content = contentPanel,
                PrimaryButtonText = SharedUtilities.GetTranslation(_lang, "Download_XXMI"),
                CloseButtonText = SharedUtilities.GetTranslation(_lang, "Cancel"),
                XamlRoot = this.Content.XamlRoot
            };

            bool isDownloading = false;
            bool downloadSuccess = false;

            dialog.CloseButtonClick += (s, e) =>
            {
                if (isDownloading)
                {
                    XXMIDownloader.CancelDownload();
                }
            };

            dialog.PrimaryButtonClick += async (s, e) =>
            {
                var deferral = e.GetDeferral();
                
                try
                {
                    isDownloading = true;
                    
                    dialog.IsPrimaryButtonEnabled = false;
                    progressBar.Visibility = Visibility.Visible;
                    progressBar.IsIndeterminate = true;
                    statusText.Text = SharedUtilities.GetTranslation(_lang, "XXMI_Download_Starting") ?? "Starting download...";

                    var progress = new Progress<(int percent, string status)>(p =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            progressBar.IsIndeterminate = false;
                            progressBar.Value = p.percent;
                            statusText.Text = p.status;
                        });
                    });

                    downloadSuccess = await XXMIDownloader.DownloadAndInstallAsync(progress, _lang);
                    
                    if (downloadSuccess)
                    {
                        statusText.Text = SharedUtilities.GetTranslation(_lang, "XXMI_Download_Success") ?? "XXMI Launcher installed successfully!";
                        progressBar.Value = 100;
                    }
                    else
                    {
                        statusText.Text = SharedUtilities.GetTranslation(_lang, "XXMI_Download_Failed") ?? "Failed to download XXMI Launcher";
                        dialog.IsPrimaryButtonEnabled = true;
                    }
                }
                finally
                {
                    isDownloading = false;
                    deferral.Complete();
                }
            };

            await dialog.ShowAsync();
            
            if (downloadSuccess)
            {
                ShowSuccessInfo(SharedUtilities.GetTranslation(_lang, "XXMI_Download_Success") ?? "XXMI Launcher installed successfully!", 3000);
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

        // Symlink cleanup removed - no longer needed with DISABLED_ prefix system

        /// <summary>
        /// Show Starter Pack dialog if available for the game and not dismissed
        /// </summary>
        private async Task ShowStarterPackDialogIfNeededAsync(string gameTag)
        {
            try
            {
                // Check if Starter Pack is available for this game
                if (!Dialogs.StarterPackDialog.IsStarterPackAvailable(gameTag))
                {
                    System.Diagnostics.Debug.WriteLine($"No Starter Pack available for {gameTag}");
                    return;
                }

                // Check if user has dismissed the dialog for this game
                if (Dialogs.StarterPackDialog.IsStarterPackDismissed(gameTag))
                {
                    System.Diagnostics.Debug.WriteLine($"Starter Pack dialog dismissed for {gameTag}");
                    return;
                }

                // Check if mods folder already has content (excluding Other category)
                if (!Dialogs.StarterPackDialog.IsModsFolderEmpty(gameTag))
                {
                    System.Diagnostics.Debug.WriteLine($"Mods folder not empty for {gameTag}, skipping Starter Pack dialog");
                    return;
                }

                // Small delay to let the UI settle after game switch
                await Task.Delay(500);

                // Show the dialog
                var dialog = new Dialogs.StarterPackDialog(gameTag);
                dialog.XamlRoot = this.Content.XamlRoot;
                
                // Subscribe to installation complete event to reload mods
                dialog.InstallationComplete += async (s, e) =>
                {
                    await ReloadModsAsync();
                };
                
                var result = await dialog.ShowAsync();
                
                // If user clicked "No thanks" with checkbox checked, it's already handled in the dialog
                // If user clicked "No thanks" without checkbox, we don't dismiss permanently
                if (result == ContentDialogResult.None && dialog.DontShowAgain)
                {
                    Dialogs.StarterPackDialog.DismissStarterPack(gameTag);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show Starter Pack dialog for {gameTag}", ex);
            }
        }
    }
}