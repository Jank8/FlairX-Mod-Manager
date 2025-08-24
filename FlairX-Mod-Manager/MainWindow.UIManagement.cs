using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private volatile bool _isGeneratingMenu = false;
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
            return Path.Combine(AppContext.BaseDirectory, @"XXMI\Resources\Bin\XXMI Launcher.exe");
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
            // Prevent race conditions caused by multiple asynchronous operations
            if (_isGeneratingMenu)
                return;
                
            _isGeneratingMenu = true;
            
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
                                                   item.Tag?.ToString() == "FunctionsPage" || 
                                                   item.Tag?.ToString() == "PresetsPage" || 
                                                   item.Tag?.ToString() == "SettingsPage")
                                    .ToList();
                                
                                nvSample.FooterMenuItems.Clear();
                                
                                // Re-add the footer items
                                foreach (var item in existingFooterItems)
                                {
                                    nvSample.FooterMenuItems.Add(item);
                                }
                                
                                // Add character categories
                                foreach (var category in categories)
                                {
                                    var menuItem = new NavigationViewItem
                                    {
                                        Content = category,
                                        Tag = $"Category_{category}",
                                        Icon = await CreateCategoryIconAsync(category, modLibraryPath),
                                        Style = (Style)Application.Current.Resources["CategoryAvatarNavigationViewItem"]
                                    };
                                    
                                    // Add the menu item first
                                    nvSample.MenuItems.Add(menuItem);
                                    
                                    // Wait for the template to be applied, then find and attach hover events to the icon border
                                    menuItem.Loaded += async (s, e) => 
                                    {
                                        // Small delay to ensure template is fully applied
                                        await Task.Delay(50);
                                        AttachIconHoverEvents(menuItem, category, modLibraryPath);
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
                    });
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error generating mod character menu", ex);
            }
            finally
            {
                _isGeneratingMenu = false;
            }
        }

        private async Task<IconElement> CreateCategoryIconAsync(string categoryName, string modLibraryPath)
        {
            try
            {
                var categoryPath = Path.Combine(modLibraryPath, categoryName);
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
            // Only generate menu if a game is selected
            if (SettingsManager.Current?.SelectedGameIndex > 0)
            {
                _ = GenerateModCharacterMenuAsync();
            }
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

        private void AttachIconHoverEvents(NavigationViewItem menuItem, string categoryName, string modLibraryPath)
        {
            try
            {
                // Find the IconBorder in the template
                var iconBorder = FindChildByName<Border>(menuItem, "IconBorder");
                if (iconBorder != null)
                {
                    // Store category info in the border's tag for the event handlers
                    iconBorder.Tag = new { CategoryName = categoryName, ModLibraryPath = modLibraryPath };
                    
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
            if (sender is Border iconBorder && iconBorder.Tag != null)
            {
                var categoryInfo = iconBorder.Tag;
                var categoryName = categoryInfo.GetType().GetProperty("CategoryName")?.GetValue(categoryInfo) as string;
                var modLibraryPath = categoryInfo.GetType().GetProperty("ModLibraryPath")?.GetValue(categoryInfo) as string;
                
                if (string.IsNullOrEmpty(categoryName) || string.IsNullOrEmpty(modLibraryPath))
                    return;
                    
                var categoryPath = Path.Combine(modLibraryPath, categoryName);
                var categoryPreviewPath = Path.Combine(categoryPath, "catprev.jpg");
                
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
            if (CategoryPreviewPopup != null)
            {
                CategoryPreviewPopup.IsOpen = false;
            }
        }
    }
}