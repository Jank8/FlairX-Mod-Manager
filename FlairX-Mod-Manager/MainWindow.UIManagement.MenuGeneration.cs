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
    public sealed partial class MainWindow
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
                        
                        // Get game tag from saved index
                        string gameTag = SettingsManager.GetGameTagFromIndex(settings.SelectedGameIndex);
                        
                        // Find the ComboBoxItem with matching Tag
                        ComboBoxItem? itemToSelect = null;
                        foreach (var item in GameSelectionComboBox.Items.OfType<ComboBoxItem>())
                        {
                            if (item.Tag?.ToString() == gameTag)
                            {
                                itemToSelect = item;
                                break;
                            }
                        }
                        
                        // Select the item (or first item if not found)
                        if (itemToSelect != null)
                        {
                            GameSelectionComboBox.SelectedItem = itemToSelect;
                        }
                        else
                        {
                            GameSelectionComboBox.SelectedIndex = 0; // Select "no game" option
                        }
                        
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

        /// <summary>
        /// Regenerates the mod character menu, bypassing the suppression flag.
        /// Use this when a setting change requires an immediate menu refresh even while the settings panel is open.
        /// </summary>
        public async Task ForceGenerateModCharacterMenuAsync()
        {
            var wasSuppressed = _suppressMenuRegeneration;
            _suppressMenuRegeneration = false;
            try
            {
                await GenerateModCharacterMenuAsync();
            }
            finally
            {
                _suppressMenuRegeneration = wasSuppressed;
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
                    
                    // Get all category directories
                    foreach (var categoryDir in Directory.GetDirectories(modsPath))
                    {
                        var categoryName = Path.GetFileName(categoryDir);
                        if (!string.IsNullOrEmpty(categoryName))
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
                                    .Where(item => item.Tag?.ToString() == "PresetsUserControl" || 
                                                   item.Tag?.ToString() == "SettingsUserControl")
                                    .ToList();
                                
                                // Only clear if we successfully captured the items
                                if (existingFooterItems.Count > 0)
                                {
                                    nvSample.FooterMenuItems.Clear();
                                    
                                    // Re-add the footer items
                                    foreach (var item in existingFooterItems)
                                    {
                                        nvSample.FooterMenuItems.Add(item);
                                    }
                                    
                                    // Update cache after restoring
                                    _allFooterItems = nvSample.FooterMenuItems.OfType<NavigationViewItem>().ToList();
                                }
                                else
                                {
                                    // If no items were found, ensure they exist
                                    EnsurePresetsMenuItemExists();
                                }
                                
                                // Generate pinned categories in footer menu
                                await GeneratePinnedCategoriesAsync();
                                
                                // Get pinned and hidden categories for filtering
                                var gameTag = SettingsManager.Current != null 
                                    ? SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex) 
                                    : null;
                                var pinnedCategories = gameTag != null ? SettingsManager.GetPinnedCategories(gameTag) : new List<string>();
                                var hiddenCategories = gameTag != null ? SettingsManager.GetHiddenCategories(gameTag) : new List<string>();
                                
                                // Clear star buttons dictionary
                                _categoryStarButtons.Clear();
                                
                                // Add character categories (already sorted), excluding pinned and hidden
                                if (sortedCategories != null && pinnedCategories != null && hiddenCategories != null)
                                {
                                    // Separate favorites and non-favorites
                                    var favoriteCategories = new List<string>();
                                    var regularCategories = new List<string>();
                                    
                                    foreach (var category in sortedCategories)
                                    {
                                        // Skip null or empty categories
                                        if (string.IsNullOrEmpty(category))
                                            continue;
                                        
                                        // Skip if category is pinned or hidden
                                        if (pinnedCategories.Contains(category) || hiddenCategories.Contains(category))
                                            continue;

                                        // Skip empty categories if setting is enabled
                                        if (SettingsManager.Current?.HideEmptyCategories == true)
                                        {
                                            var categoryPath = Path.Combine(modsPath, category);
                                            if (Directory.Exists(categoryPath) && Directory.GetDirectories(categoryPath).Length == 0)
                                                continue;
                                        }
                                        
                                        // Check if favorite
                                        var currentGameTag = SettingsManager.CurrentSelectedGame ?? "";
                                        bool isFavorite = SettingsManager.IsCategoryFavorite(currentGameTag, category);
                                        
                                        if (isFavorite)
                                            favoriteCategories.Add(category);
                                        else
                                            regularCategories.Add(category);
                                    }
                                    
                                    // Add Favourites section if there are any favorites
                                    if (favoriteCategories.Count > 0)
                                    {
                                        // Create Favourites header
                                        var brush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 215, 0)); // Gold color for favorites
                                        
                                        var headerText = new TextBlock
                                        {
                                            Text = $"────── {SharedUtilities.GetTranslation(_lang, "Favourites")} ──────",
                                            TextAlignment = TextAlignment.Center,
                                            HorizontalAlignment = HorizontalAlignment.Center,
                                            Foreground = brush
                                        };
                                        
                                        var headerItem = new NavigationViewItem
                                        {
                                            Content = headerText,
                                            IsEnabled = true,
                                            SelectsOnInvoked = false,
                                            HorizontalContentAlignment = HorizontalAlignment.Center,
                                            Tag = "__SECTION_HEADER_FAVOURITES__"
                                        };
                                        
                                        nvSample.MenuItems.Add(headerItem);
                                        
                                        // Add favorite categories
                                        foreach (var category in favoriteCategories.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
                                        {
                                            await AddCategoryToMenu(category, modsPath, true);
                                        }
                                    }
                                    
                                    // Group regular (non-favorite) categories by first letter for alphabetical sections
                                    var categoryGroups = new Dictionary<char, List<string>>();
                                    
                                    foreach (var category in regularCategories)
                                    {
                                        // Get first character
                                        char firstChar = category[0];
                                        char sectionKey;
                                        
                                        // Latin letters (A-Z) - uppercase
                                        if ((firstChar >= 'A' && firstChar <= 'Z') || (firstChar >= 'a' && firstChar <= 'z'))
                                        {
                                            sectionKey = char.ToUpperInvariant(firstChar);
                                        }
                                        // Digits (0-9) - keep as is
                                        else if (firstChar >= '0' && firstChar <= '9')
                                        {
                                            sectionKey = firstChar;
                                        }
                                        // Everything else (kanji, cyrillic, symbols, etc.) goes to '#'
                                        else
                                        {
                                            sectionKey = '#';
                                        }
                                        
                                        if (!categoryGroups.ContainsKey(sectionKey))
                                            categoryGroups[sectionKey] = new List<string>();
                                        
                                        categoryGroups[sectionKey].Add(category);
                                    }
                                    
                                    // Sort group keys: A-Z, then 0-9, then # at the end
                                    var sortedGroupKeys = categoryGroups.Keys
                                        .OrderBy(k => k == '#' ? "~~" : k.ToString()) // # last (~ sorts after all alphanumerics)
                                        .ToList();
                                    
                                    // Add categories with section headers
                                    foreach (var letter in sortedGroupKeys)
                                    {
                                        // Create centered header with TextBlock and fixed gray color
                                        var brush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)); // Fixed gray
                                        
                                        var headerText = new TextBlock
                                        {
                                            Text = $"────── {letter} ──────",
                                            TextAlignment = TextAlignment.Center,
                                            HorizontalAlignment = HorizontalAlignment.Center,
                                            Foreground = brush
                                        };
                                        
                                        // Use ENABLED NavigationViewItem (disabled forces gray which changes)
                                        var headerItem = new NavigationViewItem
                                        {
                                            Content = headerText,
                                            IsEnabled = true, // Keep enabled to prevent automatic color override
                                            SelectsOnInvoked = false,
                                            HorizontalContentAlignment = HorizontalAlignment.Center,
                                            Tag = "__SECTION_HEADER__" // Mark as header so it won't be selected
                                        };
                                        
                                        nvSample.MenuItems.Add(headerItem);
                                        
                                        // Add categories in this section (sorted alphabetically)
                                        foreach (var category in categoryGroups[letter].OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
                                        {
                                            await AddCategoryToMenu(category, modsPath, false);
                                        }
                                    }
                                }
                                
                                // Update menu items state based on current view mode
                                var isCategoriesView = SettingsManager.Current?.ViewMode == "Categories";
                                UpdateMenuItemsEnabledState(isCategoriesView);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Error updating menu UI", ex);
                            
                            // Ensure footer items exist even if there was an error
                            try
                            {
                                EnsurePresetsMenuItemExists();
                            }
                            catch (Exception innerEx)
                            {
                                Logger.LogError("Error ensuring presets menu item exists", innerEx);
                            }
                        }
                        finally
                        {
                            // Ensure footer items exist before completing
                            try
                            {
                                EnsurePresetsMenuItemExists();
                            }
                            catch (Exception innerEx)
                            {
                                Logger.LogError("Error ensuring presets menu item exists in finally", innerEx);
                            }
                            
                            // Update star buttons visibility based on current display mode
                            UpdateStarButtonsVisibility();
                            
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
                var categoryPreviewWebpPath = Path.Combine(categoryPath, "catprev.webp");
                var categoryPreviewJpgPath = Path.Combine(categoryPath, "catprev.jpg");
                var categoryMiniWebpPath = Path.Combine(categoryPath, "catmini.webp");
                var categoryMiniJpgPath = Path.Combine(categoryPath, "catmini.jpg");
                var gbIconWebpPath = Path.Combine(categoryPath, "gbicon.webp");
                var gbIconPngPath = Path.Combine(categoryPath, "gbicon.png");
                
                // Try catprev (webp/jpg) first, then catmini (webp/jpg), then gbicon (webp/png only - needs alpha)
                string? imagePath = null;
                if (File.Exists(categoryPreviewWebpPath))
                    imagePath = categoryPreviewWebpPath;
                else if (File.Exists(categoryPreviewJpgPath))
                    imagePath = categoryPreviewJpgPath;
                else if (File.Exists(categoryMiniWebpPath))
                    imagePath = categoryMiniWebpPath;
                else if (File.Exists(categoryMiniJpgPath))
                    imagePath = categoryMiniJpgPath;
                else if (File.Exists(gbIconWebpPath))
                    imagePath = gbIconWebpPath;
                else if (File.Exists(gbIconPngPath))
                    imagePath = gbIconPngPath;
                
                // Check if any category image exists
                if (imagePath != null)
                {
                    // Create a high-quality bitmap from the category image
                    var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    
                    // Set decode pixel dimensions for optimal quality at 32x32
                    bitmap.DecodePixelWidth = 64;  // 2x for high DPI displays
                    bitmap.DecodePixelHeight = 64; // 2x for high DPI displays
                    
                    // Convert to absolute path for UriSource
                    var absolutePath = Path.GetFullPath(imagePath);
                    // Use UriSource for WebP support via Windows codecs
                    bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
                    
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
                
                // Add to footer menu (before Settings)
                if (nvSample?.FooterMenuItems != null)
                {
                    var settingsItem = nvSample.FooterMenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x?.Tag?.ToString() == "SettingsUserControl");
                    if (settingsItem != null)
                    {
                        int settingsIndex = nvSample.FooterMenuItems.IndexOf(settingsItem);
                        if (settingsIndex >= 0)
                            nvSample.FooterMenuItems.Insert(settingsIndex, presets);
                        else
                            nvSample.FooterMenuItems.Add(presets);
                    }
                    else
                    {
                        nvSample.FooterMenuItems.Add(presets);
                    }
                    
                    // Update cache after adding
                    _allFooterItems = nvSample.FooterMenuItems.OfType<NavigationViewItem>().ToList();
                }
            }
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
        
        private async Task GeneratePinnedCategoriesAsync()
        {
            try
            {
                var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                if (string.IsNullOrEmpty(gameTag))
                    return;
                    
                var pinnedCategories = SettingsManager.GetPinnedCategories(gameTag);
                if (pinnedCategories == null || pinnedCategories.Count == 0)
                    return;
                    
                // Use the same path as delete operation
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath))
                {
                    modsPath = PathManager.GetModsPath();
                }
                
                if (!Directory.Exists(modsPath))
                    return;
                    
                // Get all available categories from filesystem
                var categories = Directory.GetDirectories(modsPath)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();
                
                Logger.LogInfo($"GeneratePinnedCategoriesAsync: Found {categories.Count} categories in filesystem, {pinnedCategories.Count} pinned");
                
                // Add pinned categories at the beginning of footer (before Presets and Settings)
                int insertIndex = 0;
                
                // Add pinned categories
                foreach (var categoryName in pinnedCategories)
                {
                    if (!categories.Contains(categoryName))
                    {
                        Logger.LogInfo($"GeneratePinnedCategoriesAsync: Skipping pinned category '{categoryName}' - not found in filesystem");
                        continue; // Category doesn't exist
                    }
                        
                    var menuItem = new NavigationViewItem
                    {
                        Content = categoryName,
                        Tag = $"Category:{categoryName}",
                        Icon = new FontIcon { Glyph = "\uE8B7" } // Folder icon
                    };
                    
                    // Attach context menu to pinned category
                    menuItem.Loaded += (s, e) =>
                    {
                        AttachContextMenuToMenuItem(menuItem, categoryName);
                    };
                    
                    nvSample.FooterMenuItems.Insert(insertIndex, menuItem);
                    insertIndex++; // Increment for next item
                    Logger.LogInfo($"GeneratePinnedCategoriesAsync: Added pinned category '{categoryName}' at index {insertIndex - 1}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error generating pinned categories", ex);
            }
        }
        
        /// <summary>
        /// Helper method to add a category to the navigation menu
        /// </summary>
        private async Task AddCategoryToMenu(string category, string modsPath, bool isFavorite)
        {
            var currentGameTag = SettingsManager.CurrentSelectedGame ?? "";
            
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
            
            // Store in dictionary for later access (presenter and column will be added when item loads)
            _categoryStarButtons[category] = (starButton, starIcon, null, null);
            
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
                var gameTag = SettingsManager.CurrentSelectedGame ?? "";
                
                SettingsManager.ToggleCategoryFavorite(gameTag, catName);
                bool newFavoriteState = SettingsManager.IsCategoryFavorite(gameTag, catName);
                
                // Update icon
                starIcon.Glyph = newFavoriteState ? "\uE735" : "\uE734";
                starIcon.Foreground = newFavoriteState 
                    ? new SolidColorBrush(Microsoft.UI.Colors.Gold) 
                    : new SolidColorBrush(Microsoft.UI.Colors.White);
                
                // Regenerate menu to reorganize sections
                _ = GenerateModCharacterMenuAsync();
                
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
            
            // Wait for the template to be applied, then attach star button, hover events, and context menu
            menuItem.Loaded += async (s, e) => 
            {
                try
                {
                    await Task.Delay(50);
                    AttachIconHoverEvents(menuItem, category, modsPath);
                    AttachStarButtonToMenuItem(menuItem, starButton);
                    AttachContextMenuToMenuItem(menuItem, category);
                }
                catch (System.OperationCanceledException)
                {
                    Logger.LogDebug("MainWindow.AddCategoryToMenu failed: canceled");
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("MainWindow.AddCategoryToMenu failed", ex);
                }
            };
        }
    }
}
