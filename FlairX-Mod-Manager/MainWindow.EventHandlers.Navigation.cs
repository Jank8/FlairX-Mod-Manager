using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager
{
    public sealed partial class MainWindow
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
            
            // Check both .NET and Windows App Runtime versions in one dialog
            _ = CheckRuntimeVersionsAsync();
            
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
                        try
                        {
                            await Task.Delay(100); // Small delay to ensure menu items are loaded
                            UpdateMenuItemsEnabledState(isCategoriesView);
                        }
                        catch (System.OperationCanceledException)
                        {
                            Logger.LogDebug("MainWindow.ViewModeToggleButton_Click failed: canceled");
                        }
                        catch (System.Exception ex)
                        {
                            Logger.LogError("MainWindow.ViewModeToggleButton_Click failed", ex);
                        }
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

        private void ClassicFormatButton_Click(object sender, RoutedEventArgs e)
        {
            // Switch to classic tile format
            SettingsManager.Current.UseWideTileFormat = false;
            SettingsManager.Save();
            
            // Update button styles
            UpdateTileFormatButtonStyles();
            
            // Refresh current page
            RefreshCurrentModGridPage();
        }

        private void WideFormatButton_Click(object sender, RoutedEventArgs e)
        {
            // Switch to wide tile format
            SettingsManager.Current.UseWideTileFormat = true;
            SettingsManager.Save();
            
            // Update button styles
            UpdateTileFormatButtonStyles();
            
            // Refresh current page
            RefreshCurrentModGridPage();
        }

        private void UpdateTileFormatButtonStyles()
        {
            bool isWideFormat = SettingsManager.Current.UseWideTileFormat;
            
            if (ClassicFormatButton != null && WideFormatButton != null)
            {
                if (isWideFormat)
                {
                    // Wide format active
                    ClassicFormatButton.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    WideFormatButton.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                }
                else
                {
                    // Classic format active
                    ClassicFormatButton.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                    WideFormatButton.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                }
            }
        }

        private void RefreshCurrentModGridPage()
        {
            if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
            {
                // Update TileWidth for all existing tiles immediately
                DispatcherQueue.TryEnqueue(() =>
                {
                    modGridPage.UpdateAllTileWidths();
                    // Update grid item sizes to reflect new tile width
                    modGridPage.UpdateGridItemSizes();
                });
                
                // Then trigger full refresh
                DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        await modGridPage.RefreshCurrentViewAsync();
                    }
                    catch (System.OperationCanceledException)
                    {
                        Logger.LogDebug("MainWindow.RefreshCurrentModGridPage failed: canceled");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("MainWindow.RefreshCurrentModGridPage failed", ex);
                    }
                });
            }
        }

        private void GameSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ignore selection changes during initialization
            if (!_isInitializationComplete)
            {
                Logger.LogDebug("GameSelectionComboBox_SelectionChanged: Ignoring event during initialization");
                return;
            }
            
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                // Get game tag from the selected item's Tag property
                string gameTag = selectedItem.Tag?.ToString() ?? "";
                int selectedIndex = SettingsManager.GetIndexFromGameTag(gameTag);
                
                // Skip if same game index
                if (selectedIndex == SettingsManager.Current.SelectedGameIndex)
                    return;
                
                bool gameSelected = selectedIndex > 0;
                
                if (!gameSelected)
                {
                    Logger.LogDebug("Switching to no game selected - clearing paths and disabling UI");
                    UpdateUIForGameSelection(false); // Disable UI first
                }
                else
                {
                    Logger.LogDebug($"Switching to game index {selectedIndex} (tag: {gameTag})");
                    UpdateUIForGameSelection(true); // Enable UI
                }
                
                // No symlink cleanup needed - using DISABLED_ prefix system
                Logger.LogDebug("Switching games - no cleanup needed...");
                
                // Switch game paths using index
                SettingsManager.SwitchGame(selectedIndex);
                
                // Only restart StatusKeeper watcher if a game is selected
                if (gameSelected && SettingsManager.Current.StatusKeeperDynamicSyncEnabled)
                {
                    Logger.LogDebug("Restarting StatusKeeper watcher for new game...");
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
                    FlairX_Mod_Manager.Pages.ModGridPage.SaveDefaultPresetAllInactive();
                    
                    // Regenerate character menu for the new game
                    Logger.LogDebug("Regenerating character menu for new game...");
                    _ = GenerateModCharacterMenuAsync();
                    
                    // No symlink recreation needed - using DISABLED_ prefix system
                    Logger.LogDebug("Mod state maintained for new game...");
                    
                    // Always navigate to All Mods when a game is selected
                    // This handles all cases including ModDetailPage (since the current mod might not exist in new game)
                    Logger.LogDebug("Game selected - navigating to All Mods view");
                    AllModsButton_Click(AllModsButton, new RoutedEventArgs());
                    
                    // Show Starter Pack dialog if available and not dismissed
                    _ = ShowStarterPackDialogIfNeededAsync(gameTag);
                }
                else
                {
                    // No game selected - navigate to welcome page
                    Logger.LogDebug("No game selected - navigating to welcome page");
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.WelcomePage));
                    
                    // Clear navigation selection
                    nvSample.SelectedItem = null;
                }
                
                Logger.LogDebug($"Game switched successfully. New paths:");
                Logger.LogDebug($"  Mods: '{SettingsManager.GetCurrentXXMIModsDirectory()}'");
                Logger.LogDebug($"  D3DX INI: '{SettingsManager.Current.StatusKeeperD3dxUserIniPath}'");
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
                
                var result = await MainWindow.ShowDialogAsync(dialog);
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
                    // Get current process ID
                    var currentProcessId = Process.GetCurrentProcess().Id;
                    
                    // Use PowerShell to restart - kill immediately and start new instance
                    var psCommand = $"Stop-Process -Id {currentProcessId} -Force; Start-Process -FilePath '{exePath}'";
                    
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{psCommand}\"",
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    
                    Logger.LogInfo($"Restart: Starting restart script (PID: {currentProcessId})...");
                    Process.Start(psi);
                    
                    Logger.LogInfo($"Restart: PowerShell script started, restarting now...");
                }
                else
                {
                    Logger.LogError($"Restart: Could not find executable at path: {exePath}");
                    
                    // Show error to user
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    ShowErrorInfo(SharedUtilities.GetTranslation(lang, "Notify_RestartExeMissing"), 3000);
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
            try
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
                    ShowErrorInfo(ex.Message);
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("MainWindow.LauncherFabBorder_PointerPressed failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("MainWindow.LauncherFabBorder_PointerPressed failed", ex);
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
    }
}
