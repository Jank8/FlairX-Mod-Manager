using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Storage.Streams;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// MainWindow partial class - UI management, menu generation, and utility functions
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly SemaphoreSlim _menuGenerationLock = new SemaphoreSlim(1, 1);
        private volatile bool _suppressMenuRegeneration = false;
        
        // Dictionary to store star buttons for each category
        private readonly Dictionary<string, (Button button, FontIcon icon)> _categoryStarButtons = new Dictionary<string, (Button, FontIcon)>();
        
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
            // Get XXMI root for current game (or default if no game selected)
            var xxmiRoot = SettingsManager.GetCurrentGameXXMIRoot();
            
            // If path is already absolute, use it directly; otherwise combine with base directory
            if (Path.IsPathRooted(xxmiRoot))
            {
                return Path.Combine(xxmiRoot, @"Resources\Bin\XXMI Launcher.exe");
            }
            else
            {
                return Path.Combine(AppContext.BaseDirectory, xxmiRoot, @"Resources\Bin\XXMI Launcher.exe");
            }
        }

        private StackPanel CreateXXMIDownloadContent(string expectedPath)
        {
            var stackPanel = new StackPanel { Spacing = 10 };
            
            var text1 = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "LauncherNotFoundDescription"),
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
            };
            
            // Always show simple relative path for XXMI Launcher (it's always in the same place)
            var xxmiRoot = SettingsManager.GetCurrentGameXXMIRoot();
            var displayPath = Path.Combine(xxmiRoot, @"Resources\Bin\XXMI Launcher.exe");
            
            var text2 = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "ExpectedPath") + ": " + displayPath,
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
            // Debug logging to track when menu regeneration is triggered
            Logger.LogDebug($"GenerateModCharacterMenuAsync called");
            
            // Skip menu regeneration if suppressed
            if (_suppressMenuRegeneration)
            {
                Logger.LogDebug("Menu regeneration suppressed");
                return;
            }
            
            // Use SemaphoreSlim for proper async synchronization
            if (!await _menuGenerationLock.WaitAsync(0))
            {
                Logger.LogDebug("Menu generation already in progress, skipping");
                return;
            }
            
            try
            {
                // Only generate menu items if a game is selected
                bool gameSelected = SettingsManager.Current?.SelectedGameIndex > 0;
                if (!gameSelected)
                {
                    // Clear menu items if no game is selected
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (nvSample?.MenuItems != null)
                        {
                            nvSample.MenuItems.Clear();
                        }
                    });
                    return;
                }

                await Task.Run(async () =>
                {
                    var modsPath = SharedUtilities.GetSafeXXMIModsPath();
                    if (!Directory.Exists(modsPath))
                    {
                        Logger.LogWarning($"Mod library path does not exist: {modsPath}");
                        return;
                    }

                    var categories = new List<string>();
                    
                    // Get all category directories, excluding "Other"
                    foreach (var categoryDir in Directory.GetDirectories(modsPath))
                    {
                        var categoryName = Path.GetFileName(categoryDir);
                        if (!string.Equals(categoryName, "Other", StringComparison.OrdinalIgnoreCase))
                        {
                            categories.Add(categoryName);
                        }
                    }
                    
                    // Sort categories: favorites first, then alphabetically
                    var gameTag = SettingsManager.CurrentSelectedGame;
                    var sortedCategories = categories
                        .OrderByDescending(cat => !string.IsNullOrEmpty(gameTag) && SettingsManager.IsCategoryFavorite(gameTag, cat))
                        .ThenBy(cat => cat, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    
                    // Update UI on main thread
                    var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        try
                        {
                            if (nvSample?.MenuItems != null)
                            {
                                // Clear existing menu items
                                nvSample.MenuItems.Clear();
                                
                                // Also clear and rebuild footer items to prevent duplication
                                var existingFooterItems = nvSample.FooterMenuItems.OfType<NavigationViewItem>()
                                    .Where(item => item.Tag?.ToString() == "OtherModsPage" || 
                                                   item.Tag?.ToString() == "FunctionsUserControl" || 
                                                   item.Tag?.ToString() == "PresetsUserControl" || 
                                                   item.Tag?.ToString() == "SettingsUserControl")
                                    .ToList();
                                
                                nvSample.FooterMenuItems.Clear();
                                
                                // Re-add the footer items
                                foreach (var item in existingFooterItems)
                                {
                                    nvSample.FooterMenuItems.Add(item);
                                }
                                
                                // Clear star buttons dictionary
                                _categoryStarButtons.Clear();
                                
                                // Add character categories (already sorted)
                                foreach (var category in sortedCategories)
                                {
                                    var gameTag = SettingsManager.CurrentSelectedGame ?? "";
                                    bool isFavorite = SettingsManager.IsCategoryFavorite(gameTag, category);
                                    
                                    // Create star icon
                                    var starIcon = new FontIcon
                                    {
                                        Glyph = isFavorite ? "\uE735" : "\uE734",
                                        FontSize = 14,
                                        Foreground = isFavorite 
                                            ? new SolidColorBrush(Microsoft.UI.Colors.Gold) 
                                            : new SolidColorBrush(Microsoft.UI.Colors.White)
                                    };
                                    
                                    // Create star button for the first column
                                    var starButton = new Button
                                    {
                                        Content = starIcon,
                                        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                                        BorderThickness = new Thickness(0),
                                        Padding = new Thickness(0),
                                        VerticalAlignment = VerticalAlignment.Center,
                                        HorizontalAlignment = HorizontalAlignment.Center,
                                        Width = 28,
                                        Height = 28,
                                        Tag = category
                                    };
                                    
                                    // Store in dictionary for later access
                                    _categoryStarButtons[category] = (starButton, starIcon);
                                    
                                    var menuItem = new NavigationViewItem
                                    {
                                        Content = category,
                                        Tag = $"Category_{category}",
                                        Icon = await CreateCategoryIconAsync(category, modsPath),
                                        Style = (Style)Application.Current.Resources["CategoryAvatarNavigationViewItem"]
                                    };
                                    
                                    // Handle star click
                                    starButton.Click += (s, e) =>
                                    {
                                        var catName = (string)((Button)s).Tag;
                                        var currentGameTag = SettingsManager.CurrentSelectedGame ?? "";
                                        
                                        SettingsManager.ToggleCategoryFavorite(currentGameTag, catName);
                                        bool newFavoriteState = SettingsManager.IsCategoryFavorite(currentGameTag, catName);
                                        
                                        // Update icon
                                        starIcon.Glyph = newFavoriteState ? "\uE735" : "\uE734";
                                        starIcon.Foreground = newFavoriteState 
                                            ? new SolidColorBrush(Microsoft.UI.Colors.Gold) 
                                            : new SolidColorBrush(Microsoft.UI.Colors.White);
                                        
                                        // Re-sort menu items with animation
                                        SortMenuItemsByFavoritesAnimated();
                                        
                                        // Only update ModGridPage if it's showing categories view
                                        if (contentFrame.Content is Pages.ModGridPage modGridPage && 
                                            modGridPage.CurrentViewMode == Pages.ModGridPage.ViewMode.Categories)
                                        {
                                            modGridPage.RefreshCategoryFavoritesAnimated();
                                        }
                                        
                                        // Refresh overlay if it exists
                                        if (OverlayWindow != null)
                                        {
                                            OverlayWindow.RefreshOverlayData();
                                        }
                                        
                                        Logger.LogInfo($"Toggled favorite for category in menu: {catName}, IsFavorite: {newFavoriteState}");
                                    };
                                    
                                    // Add the menu item
                                    nvSample.MenuItems.Add(menuItem);
                                    
                                    // Wait for the template to be applied, then attach star button and hover events
                                    menuItem.Loaded += async (s, e) => 
                                    {
                                        await Task.Delay(50);
                                        AttachIconHoverEvents(menuItem, category, modsPath);
                                        AttachStarButtonToMenuItem(menuItem, starButton);
                                    };
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
                        finally
                        {
                            // Signal completion so callers can rely on menu being populated
                            tcs.TrySetResult(true);
                        }
                    });

                    // Wait for the UI update to complete (or timeout after a short interval)
                    var delayTask = Task.Delay(2000);
                    var completed = await Task.WhenAny(tcs.Task, delayTask);
                    if (completed != tcs.Task)
                    {
                        Logger.LogWarning("UI update for menu generation timed out");
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error generating mod character menu", ex);
            }
            finally
            {
                _menuGenerationLock.Release();
            }
        }

        private async Task<IconElement> CreateCategoryIconAsync(string categoryName, string modsPath)
        {
            try
            {
                var categoryPath = Path.Combine(modsPath, categoryName);
                var categoryPreviewPath = Path.Combine(categoryPath, "catprev.jpg");
                
                // Check if category preview image exists
                if (File.Exists(categoryPreviewPath))
                {
                    // Create a high-quality bitmap from the category image
                    var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    
                    // Set decode pixel dimensions for optimal quality at 32x32
                    bitmap.DecodePixelWidth = 64;  // 2x for high DPI displays
                    bitmap.DecodePixelHeight = 64; // 2x for high DPI displays
                    
                    using (var stream = File.OpenRead(categoryPreviewPath))
                    {
                        await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
                    }
                    
                    // Create ImageIcon with the category image
                    // The NavigationView will handle appropriate styling
                    var imageIcon = new ImageIcon
                    {
                        Source = bitmap,
                        Width = 32,
                        Height = 32
                    };
                    
                    return imageIcon;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create category icon for {categoryName}", ex);
            }
            
            // Fallback to default character icon if image doesn't exist or fails to load
            return new FontIcon { Glyph = "\uEA8C" };
        }

        private void EnsurePresetsMenuItemExists()
        {
            // Ensure Presets menu item exists in FooterMenuItems
            if (nvSample?.FooterMenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag as string == "PresetsUserControl") == null)
            {
                var presets = new NavigationViewItem
                {
                    Content = SharedUtilities.GetTranslation(_lang, "Presets"),
                    Tag = "PresetsUserControl",
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
            var modsPath = SharedUtilities.GetSafeXXMIModsPath();
            if (!Directory.Exists(modsPath)) return;
            
            loadingWindow.UpdateStatus("Scanning for images...");
            loadingWindow.SetIndeterminate(true);
            
            // Quick scan: collect only paths with existing minitile.jpg
            var imagesToLoad = new List<(string minitilePath, string dirName)>();
            
            foreach (var categoryDir in Directory.GetDirectories(modsPath))
            {
                if (!Directory.Exists(categoryDir)) continue;
                
                foreach (var modDir in Directory.GetDirectories(categoryDir))
                {
                    var minitilePath = System.IO.Path.Combine(modDir, "minitile.jpg");
                    if (File.Exists(minitilePath))
                    {
                        imagesToLoad.Add((minitilePath, System.IO.Path.GetFileName(modDir)));
                    }
                }
            }
            
            if (imagesToLoad.Count == 0)
            {
                loadingWindow.UpdateStatus("No images to load");
                await Task.Delay(200);
                return;
            }
            
            // Now load only the images that exist
            loadingWindow.SetIndeterminate(false);
            var lastUpdateTime = DateTime.Now;
            
            for (int i = 0; i < imagesToLoad.Count; i++)
            {
                var (minitilePath, dirName) = imagesToLoad[i];
                
                try
                {
                    var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    using (var stream = File.OpenRead(minitilePath))
                    {
                        bitmap.SetSource(stream.AsRandomAccessStream());
                    }
                    
                    ImageCacheManager.CacheImage(minitilePath, bitmap);
                    ImageCacheManager.CacheRamImage(dirName, bitmap);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug($"Error preloading image {minitilePath}: {ex.Message}");
                }
                
                // Update UI every 50ms
                var now = DateTime.Now;
                if ((now - lastUpdateTime).TotalMilliseconds >= 50)
                {
                    var progress = (double)(i + 1) / imagesToLoad.Count * 100;
                    loadingWindow.SetProgress(progress);
                    loadingWindow.UpdateStatus($"Loading images... {i + 1}/{imagesToLoad.Count}");
                    lastUpdateTime = now;
                    await Task.Delay(1);
                }
            }
            
            loadingWindow.SetProgress(100);
            loadingWindow.UpdateStatus($"Loaded {imagesToLoad.Count} images");
            await Task.Delay(200);
        }

        public void RefreshUIAfterLanguageChange()
        {
            RefreshUIAfterLanguageChange(true);
        }

        public void RefreshUIAfterLanguageChange(bool regenerateMenu = true)
        {
            // First reload our own language dictionary
            LoadLanguage();
            
            SetSearchBoxPlaceholder();
            SetFooterMenuTranslations();
            UpdateGameSelectionComboBoxTexts();
            SetPaneButtonTooltips();
            SetCategoryTitles();
            UpdateAllModsButtonState();
            // Only generate menu if a game is selected and regenerateMenu is true
            if (regenerateMenu && SettingsManager.Current?.SelectedGameIndex > 0)
            {
                _ = GenerateModCharacterMenuAsync();
            }
            
            // Refresh page if it's ModGridPage or UserControls
            if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
            {
                modGridPage.RefreshUIAfterLanguageChange();
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
            // HotkeyFinderPage no longer has UI - removed language update
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
                
                // Clear or populate menu items based on game selection
                if (nvSample != null)
                {
                    if (gameSelected)
                    {
                        // Enable menu items when game is selected
                        foreach (var item in nvSample.MenuItems.OfType<NavigationViewItem>())
                        {
                            item.IsEnabled = true;
                        }
                        foreach (var item in nvSample.FooterMenuItems.OfType<NavigationViewItem>())
                        {
                            item.IsEnabled = true;
                        }
                    }
                    else
                    {
                        // Clear menu items when no game is selected to hide them completely
                        nvSample.MenuItems.Clear();
                        
                        // Keep footer items but disable them
                        foreach (var item in nvSample.FooterMenuItems.OfType<NavigationViewItem>())
                        {
                            item.IsEnabled = false;
                        }
                    }
                }
                
                // Enable/disable search and buttons
                if (SearchBox != null) SearchBox.IsEnabled = gameSelected;
                if (ReloadModsButton != null) ReloadModsButton.IsEnabled = gameSelected;
                if (RestartAppButton != null) RestartAppButton.IsEnabled = gameSelected;
                if (AllModsButton != null) AllModsButton.IsEnabled = gameSelected;
                if (ViewModeToggleButton != null) ViewModeToggleButton.IsEnabled = gameSelected;
                if (BrowseGameBananaButton != null) BrowseGameBananaButton.IsEnabled = gameSelected;
                
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
                Logger.LogError($"Error updating UI for game selection: {ex.Message}");
            }
        }

        public void UpdateOrangeAnimationVisibility(bool isVisible)
        {
            if (OrangeAnimationProgressBar != null)
            {
                OrangeAnimationProgressBar.Opacity = isVisible ? 1 : 0;
            }
        }

        private void AttachIconHoverEvents(NavigationViewItem menuItem, string categoryName, string modsPath)
        {
            try
            {
                // Find the IconBorder in the template
                var iconBorder = FindChildByName<Border>(menuItem, "IconBorder");
                if (iconBorder != null)
                {
                    // Store category info in the border's tag for the event handlers
                    iconBorder.Tag = new { CategoryName = categoryName, modsPath = modsPath };
                    
                    // Ensure the border can receive pointer events
                    iconBorder.Background = iconBorder.Background ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    
                    // Attach hover events to the icon border only
                    iconBorder.PointerEntered += CategoryIcon_PointerEntered;
                    iconBorder.PointerExited += CategoryIcon_PointerExited;
                }
                else
                {
                    Logger.LogWarning($"IconBorder not found for category {categoryName}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to attach hover events for category {categoryName}", ex);
            }
        }

        private T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;

            // Check if the parent itself matches
            if (parent is T parentElement && parentElement.Name == name)
            {
                return parentElement;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T element && element.Name == name)
                {
                    return element;
                }
                
                var result = FindChildByName<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        private async void CategoryIcon_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _isPointerOverCategoryIcon = true;
            
            // Cancel any pending close timer
            if (_categoryPreviewCloseTimer != null)
            {
                _categoryPreviewCloseTimer.Stop();
            }
            
            if (sender is Border iconBorder && iconBorder.Tag != null)
            {
                var categoryInfo = iconBorder.Tag;
                var categoryName = categoryInfo.GetType().GetProperty("CategoryName")?.GetValue(categoryInfo) as string;
                var modsPath = categoryInfo.GetType().GetProperty("modsPath")?.GetValue(categoryInfo) as string;
                
                if (string.IsNullOrEmpty(categoryName) || string.IsNullOrEmpty(modsPath))
                    return;
                    
                var categoryPath = Path.Combine(modsPath, categoryName);
                var categoryPreviewPath = Path.Combine(categoryPath, "catprev.jpg");
                
                // Popup uses catprev.jpg (722x722 square)
                if (File.Exists(categoryPreviewPath) && CategoryPreviewPopup != null && CategoryPreviewImage != null)
                {
                    try
                    {
                        // Load the category preview image
                        var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                        using (var stream = File.OpenRead(categoryPreviewPath))
                        {
                            await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
                        }
                        
                        CategoryPreviewImage.Source = bitmap;
                        
                        // Calculate popup position using the icon border position
                        var bounds = iconBorder.TransformToVisual(MainRoot).TransformBounds(new Windows.Foundation.Rect(0, 0, iconBorder.ActualWidth, iconBorder.ActualHeight));
                        var windowWidth = MainRoot.ActualWidth;
                        var windowHeight = MainRoot.ActualHeight;
                        
                        const double popupWidth = 400;
                        const double popupHeight = 400;
                        const double margin = 15;
                        
                        // Position to the right of the navigation pane
                        double horizontalOffset = bounds.Right + margin;
                        
                        // If it would go off screen, position to the left
                        if (horizontalOffset + popupWidth > windowWidth)
                        {
                            horizontalOffset = Math.Max(margin, bounds.Left - popupWidth - margin);
                        }
                        
                        // Center vertically relative to the icon
                        double verticalOffset = Math.Max(margin, Math.Min(bounds.Top - (popupHeight - bounds.Height) / 2, windowHeight - popupHeight - margin));
                        
                        CategoryPreviewPopup.HorizontalOffset = horizontalOffset;
                        CategoryPreviewPopup.VerticalOffset = verticalOffset;
                        CategoryPreviewPopup.IsOpen = true;
                        
                        // Fade in animation
                        if (CategoryPreviewBorder != null)
                        {
                            var fadeIn = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                            {
                                From = 0,
                                To = 1,
                                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                            };
                            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeIn, CategoryPreviewBorder);
                            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeIn, "Opacity");
                            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                            storyboard.Children.Add(fadeIn);
                            storyboard.Begin();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to show category preview for {categoryName}", ex);
                    }
                }
            }
        }

        private void CategoryIcon_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _isPointerOverCategoryIcon = false;
            
            // Start timer to close popup after 1 second
            if (_categoryPreviewCloseTimer == null)
            {
                _categoryPreviewCloseTimer = new DispatcherTimer();
                _categoryPreviewCloseTimer.Interval = TimeSpan.FromSeconds(1);
                _categoryPreviewCloseTimer.Tick += (s, args) =>
                {
                    _categoryPreviewCloseTimer?.Stop();
                    
                    // Only close if pointer is still not over the icon
                    if (!_isPointerOverCategoryIcon && CategoryPreviewPopup != null && CategoryPreviewBorder != null)
                    {
                        // Fade out animation
                        var fadeOut = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                        {
                            From = 1,
                            To = 0,
                            Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn }
                        };
                        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeOut, CategoryPreviewBorder);
                        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeOut, "Opacity");
                        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                        storyboard.Children.Add(fadeOut);
                        storyboard.Completed += (sender, e) =>
                        {
                            CategoryPreviewPopup.IsOpen = false;
                        };
                        storyboard.Begin();
                    }
                };
            }
            
            _categoryPreviewCloseTimer.Start();
        }
        
        private void AttachStarButtonToMenuItem(NavigationViewItem menuItem, Button starButton)
        {
            try
            {
                // Find the StarPresenter in the template and set its content
                var starPresenter = FindChildByName<ContentPresenter>(menuItem, "StarPresenter");
                if (starPresenter != null)
                {
                    starPresenter.Content = starButton;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error attaching star button to menu item", ex);
            }
        }
        
        private void SortMenuItemsByFavorites()
        {
            try
            {
                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag)) return;
                
                var menuItems = nvSample.MenuItems.OfType<NavigationViewItem>().ToList();
                
                // Sort: favorites first, then alphabetically
                // Get category name from Tag (format: "Category_Name")
                var sortedItems = menuItems
                    .OrderByDescending(item => 
                    {
                        var tag = item.Tag?.ToString() ?? "";
                        var catName = tag.StartsWith("Category_") ? tag.Substring(9) : item.Content?.ToString() ?? "";
                        return SettingsManager.IsCategoryFavorite(gameTag, catName);
                    })
                    .ThenBy(item => 
                    {
                        var tag = item.Tag?.ToString() ?? "";
                        var catName = tag.StartsWith("Category_") ? tag.Substring(9) : item.Content?.ToString() ?? "";
                        return catName;
                    }, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                
                // Clear and re-add
                nvSample.MenuItems.Clear();
                foreach (var item in sortedItems)
                {
                    nvSample.MenuItems.Add(item);
                }
                
                Logger.LogInfo("Menu items sorted by favorites");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sorting menu items by favorites", ex);
            }
        }
        
        private async void SortMenuItemsByFavoritesAnimated()
        {
            try
            {
                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag)) return;
                
                var menuItems = nvSample.MenuItems.OfType<NavigationViewItem>().ToList();
                if (menuItems.Count == 0) return;
                
                // Calculate new order
                var sortedItems = menuItems
                    .OrderByDescending(item => 
                    {
                        var tag = item.Tag?.ToString() ?? "";
                        var catName = tag.StartsWith("Category_") ? tag.Substring(9) : item.Content?.ToString() ?? "";
                        return SettingsManager.IsCategoryFavorite(gameTag, catName);
                    })
                    .ThenBy(item => 
                    {
                        var tag = item.Tag?.ToString() ?? "";
                        var catName = tag.StartsWith("Category_") ? tag.Substring(9) : item.Content?.ToString() ?? "";
                        return catName;
                    }, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                
                // Check if order changed
                bool orderChanged = false;
                for (int i = 0; i < menuItems.Count; i++)
                {
                    if (menuItems[i] != sortedItems[i])
                    {
                        orderChanged = true;
                        break;
                    }
                }
                if (!orderChanged) return;
                
                // Fade out animation
                var fadeOutStoryboard = new Storyboard();
                foreach (var item in menuItems)
                {
                    var fadeOut = new DoubleAnimation
                    {
                        From = 1,
                        To = 0.3,
                        Duration = new Duration(TimeSpan.FromMilliseconds(100))
                    };
                    Storyboard.SetTarget(fadeOut, item);
                    Storyboard.SetTargetProperty(fadeOut, "Opacity");
                    fadeOutStoryboard.Children.Add(fadeOut);
                }
                
                fadeOutStoryboard.Begin();
                await Task.Delay(100);
                
                // Reorder
                nvSample.MenuItems.Clear();
                foreach (var item in sortedItems)
                {
                    item.Opacity = 0.3;
                    nvSample.MenuItems.Add(item);
                }
                
                // Fade in animation
                await Task.Delay(50);
                var fadeInStoryboard = new Storyboard();
                foreach (var item in sortedItems)
                {
                    var fadeIn = new DoubleAnimation
                    {
                        From = 0.3,
                        To = 1,
                        Duration = new Duration(TimeSpan.FromMilliseconds(150))
                    };
                    Storyboard.SetTarget(fadeIn, item);
                    Storyboard.SetTargetProperty(fadeIn, "Opacity");
                    fadeInStoryboard.Children.Add(fadeIn);
                }
                
                fadeInStoryboard.Begin();
                
                Logger.LogInfo("Menu items sorted by favorites with animation");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sorting menu items by favorites with animation", ex);
                SortMenuItemsByFavorites();
            }
        }
        
        public void UpdateMenuStarForCategory(string categoryName, bool isFavorite)
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    // Use dictionary to find star button
                    if (_categoryStarButtons.TryGetValue(categoryName, out var starData))
                    {
                        starData.icon.Glyph = isFavorite ? "\uE735" : "\uE734";
                        starData.icon.Foreground = isFavorite 
                            ? new SolidColorBrush(Microsoft.UI.Colors.Gold) 
                            : new SolidColorBrush(Microsoft.UI.Colors.White);
                        
                        // Re-sort menu
                        SortMenuItemsByFavorites();
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error updating menu star for category: {categoryName}", ex);
            }
        }
        
        public void UpdateMenuStarForCategoryAnimated(string categoryName, bool isFavorite)
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    // Use dictionary to find star button
                    if (_categoryStarButtons.TryGetValue(categoryName, out var starData))
                    {
                        starData.icon.Glyph = isFavorite ? "\uE735" : "\uE734";
                        starData.icon.Foreground = isFavorite 
                            ? new SolidColorBrush(Microsoft.UI.Colors.Gold) 
                            : new SolidColorBrush(Microsoft.UI.Colors.White);
                        
                        // Re-sort menu with animation
                        SortMenuItemsByFavoritesAnimated();
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error updating menu star for category with animation: {categoryName}", ex);
            }
        }
    }
}