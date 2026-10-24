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

        private async void CategoryIcon_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
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
                    var categoryPreviewWebpPath = Path.Combine(categoryPath, "catprev.webp");
                    var categoryPreviewJpgPath = Path.Combine(categoryPath, "catprev.jpg");
                    var categoryMiniWebpPath = Path.Combine(categoryPath, "catmini.webp");
                    var categoryMiniJpgPath = Path.Combine(categoryPath, "catmini.jpg");
                    var iconPath = Path.Combine(categoryPath, "icon.png");

                    // Try catprev (webp/jpg) first, then catmini (webp/jpg)
                    // Skip icon.png for preview popup as it's too small
                    string? previewPath = null;
                    if (File.Exists(categoryPreviewWebpPath))
                        previewPath = categoryPreviewWebpPath;
                    else if (File.Exists(categoryPreviewJpgPath))
                        previewPath = categoryPreviewJpgPath;
                    else if (File.Exists(categoryMiniWebpPath))
                        previewPath = categoryMiniWebpPath;
                    else if (File.Exists(categoryMiniJpgPath))
                        previewPath = categoryMiniJpgPath;

                    // Only show popup if we have catprev or catmini (not icon.png)
                    if (previewPath != null && CategoryPreviewPopup != null && CategoryPreviewImage != null)
                    {
                        try
                        {
                            // Load the category preview image
                            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                            // Use UriSource for WebP support via Windows codecs
                            // IMPORTANT: Must use absolute path for WebP to work
                            var absolutePath = Path.GetFullPath(previewPath);
                            bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);

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
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("MainWindow.CategoryIcon_PointerEntered failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("MainWindow.CategoryIcon_PointerEntered failed", ex);
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
                    
                    // Find the Grid that contains the StarPresenter and get its column
                    var parentGrid = starPresenter.Parent as Grid;
                    ColumnDefinition? starColumn = null;
                    
                    if (parentGrid != null && parentGrid.ColumnDefinitions.Count > 0)
                    {
                        // StarPresenter is typically in the last column (column 2)
                        var columnIndex = Grid.GetColumn(starPresenter);
                        if (columnIndex < parentGrid.ColumnDefinitions.Count)
                        {
                            starColumn = parentGrid.ColumnDefinitions[columnIndex];
                        }
                    }
                    
                    // Update dictionary with presenter and column reference
                    var categoryName = (string)starButton.Tag;
                    if (_categoryStarButtons.TryGetValue(categoryName, out var starData))
                    {
                        _categoryStarButtons[categoryName] = (starData.button, starData.icon, starPresenter, starColumn);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error attaching star button to menu item", ex);
            }
        }
        
        private void AttachContextMenuToMenuItem(NavigationViewItem menuItem, string categoryName)
        {
            try
            {
                var menuFlyout = new MenuFlyout();
                var lang = SharedUtilities.LoadLanguageDictionary();
                
                // Open Folder
                var openFolderItem = new MenuFlyoutItem
                {
                    Text = SharedUtilities.GetTranslation(lang, "ContextMenu_OpenFolder"),
                    Icon = new SymbolIcon(Symbol.Folder),
                    Tag = categoryName
                };
                openFolderItem.Click += CategoryContextMenu_OpenFolder_Click;
                menuFlyout.Items.Add(openFolderItem);
                
                menuFlyout.Items.Add(new MenuFlyoutSeparator());
                
                // Copy Name
                var copyNameItem = new MenuFlyoutItem
                {
                    Text = SharedUtilities.GetTranslation(lang, "ContextMenu_CopyName"),
                    Icon = new SymbolIcon(Symbol.Copy),
                    Tag = categoryName
                };
                copyNameItem.Click += CategoryContextMenu_CopyName_Click;
                menuFlyout.Items.Add(copyNameItem);
                
                // Rename
                var renameItem = new MenuFlyoutItem
                {
                    Text = SharedUtilities.GetTranslation(lang, "ContextMenu_Rename"),
                    Icon = new SymbolIcon(Symbol.Rename),
                    Tag = categoryName
                };
                renameItem.Click += CategoryContextMenu_Rename_Click;
                menuFlyout.Items.Add(renameItem);
                
                menuFlyout.Items.Add(new MenuFlyoutSeparator());
                
                // Check if category is pinned
                var gameTag = SettingsManager.CurrentSelectedGame;
                var pinnedCategories = !string.IsNullOrEmpty(gameTag) 
                    ? SettingsManager.GetPinnedCategories(gameTag) 
                    : new List<string>();
                
                bool isPinned = pinnedCategories.Contains(categoryName);
                
                // Pin/Unpin option
                var pinItem = new MenuFlyoutItem
                {
                    Text = isPinned 
                        ? SharedUtilities.GetTranslation(lang, "ContextMenu_UnpinCategory") 
                        : SharedUtilities.GetTranslation(lang, "ContextMenu_PinCategory"),
                    Icon = new FontIcon { Glyph = isPinned ? "\uE77A" : "\uE718" }, // Unpin : Pin
                    Tag = categoryName
                };
                pinItem.Click += CategoryContextMenu_PinUnpin_Click;
                menuFlyout.Items.Add(pinItem);

                // Download thumbnail
                menuFlyout.Items.Add(new MenuFlyoutSeparator());

                var downloadThumbItem = new MenuFlyoutItem
                {
                    Text = SharedUtilities.GetTranslation(lang, "ContextMenu_DownloadThumbnail") ?? "Download Thumbnail",
                    Icon = new FontIcon { Glyph = "\uE8C9" },
                    Tag = categoryName
                };
                downloadThumbItem.Click += CategoryContextMenu_DownloadThumbnail_Click;
                menuFlyout.Items.Add(downloadThumbItem);

                // Delete option (always show, will auto-unpin if needed)
                menuFlyout.Items.Add(new MenuFlyoutSeparator());
                
                var deleteItem = new MenuFlyoutItem
                {
                    Text = SharedUtilities.GetTranslation(lang, "ContextMenu_DeleteCategory"),
                    Icon = new SymbolIcon(Symbol.Delete),
                    Tag = categoryName
                };
                deleteItem.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                deleteItem.Click += CategoryContextMenu_Delete_Click;
                menuFlyout.Items.Add(deleteItem);
                
                menuItem.ContextFlyout = menuFlyout;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error attaching context menu to menu item", ex);
            }
        }
        
        private void CategoryContextMenu_OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string categoryName)
            {
                try
                {
                    var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                    if (string.IsNullOrEmpty(modsPath))
                    {
                        modsPath = PathManager.GetModsPath();
                    }
                    
                    var categoryPath = System.IO.Path.Combine(modsPath, categoryName);
                    if (Directory.Exists(categoryPath))
                    {
                        OpenFolder(categoryPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to open category folder: {categoryName}", ex);
                }
            }
        }
        
        private void CategoryContextMenu_CopyName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string categoryName)
            {
                try
                {
                    var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    dataPackage.SetText(categoryName);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to copy category name: {categoryName}", ex);
                }
            }
        }
        
        private async void CategoryContextMenu_Rename_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is string oldCategoryName)
                {
                    try
                    {
                        var lang = SharedUtilities.LoadLanguageDictionary();
                        var dialog = new ContentDialog
                        {
                            Title = SharedUtilities.GetTranslation(lang, "RenameDialog_Category_Title"),
                            PrimaryButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                            CloseButtonText = SharedUtilities.GetTranslation(lang, "Cancel"),
                            DefaultButton = ContentDialogButton.Primary,
                            XamlRoot = this.Content.XamlRoot
                        };

                        var textBox = new TextBox
                        {
                            Text = oldCategoryName,
                            SelectionStart = 0,
                            SelectionLength = oldCategoryName.Length
                        };
                        dialog.Content = textBox;

                        var result = await dialog.ShowAsync();
                        if (result == ContentDialogResult.Primary)
                        {
                            var newCategoryName = textBox.Text.Trim();
                            if (string.IsNullOrEmpty(newCategoryName))
                            {
                                return;
                            }

                            var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                            if (string.IsNullOrEmpty(modsPath))
                            {
                                modsPath = PathManager.GetModsPath();
                            }

                            var oldPath = System.IO.Path.Combine(modsPath, oldCategoryName);
                            var newPath = System.IO.Path.Combine(modsPath, newCategoryName);

                            if (Directory.Exists(oldPath) && !Directory.Exists(newPath))
                            {
                                Directory.Move(oldPath, newPath);
                                Logger.LogInfo($"Renamed category: {oldCategoryName} -> {newCategoryName}");

                                // Regenerate menu
                                await GenerateModCharacterMenuAsync();

                                // Refresh ModGridPage if showing categories
                                if (contentFrame.Content is Pages.ModGridPage modGridPage)
                                {
                                    if (modGridPage.CurrentViewMode == Pages.ModGridPage.ViewMode.Categories)
                                    {
                                        modGridPage.LoadCategories();
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to rename category: {oldCategoryName}", ex);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("MainWindow.CategoryContextMenu_Rename_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("MainWindow.CategoryContextMenu_Rename_Click failed", ex);
            }
        }
        
        private async void CategoryContextMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is string categoryName)
                {
                    try
                    {
                        var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                        if (string.IsNullOrEmpty(modsPath))
                        {
                            modsPath = PathManager.GetModsPath();
                        }

                        var categoryPath = System.IO.Path.Combine(modsPath, categoryName);

                        if (!Directory.Exists(categoryPath))
                        {
                            Logger.LogWarning($"Category directory not found: {categoryPath}");
                            return;
                        }

                        // Count mods in category
                        int modCount = Directory.GetDirectories(categoryPath).Length;

                        // Show confirmation dialog
                        bool confirmed = await Dialogs.CategoryDeleteDialog.ShowAsync(categoryName, modCount, this.Content.XamlRoot);

                        if (!confirmed)
                        {
                            return;
                        }

                        // Check if category is pinned and unpin it first
                        var gameTag = SettingsManager.CurrentSelectedGame;
                        if (!string.IsNullOrEmpty(gameTag))
                        {
                            var pinnedCategories = SettingsManager.GetPinnedCategories(gameTag);
                            if (pinnedCategories.Contains(categoryName))
                            {
                                SettingsManager.RemovePinnedCategory(gameTag, categoryName);
                                Logger.LogInfo($"Automatically unpinned category before deletion: {categoryName}");
                            }
                        }

                        // Delete the category directory
                        Directory.Delete(categoryPath, true);
                        Logger.LogInfo($"Deleted category: {categoryName} with {modCount} mods");

                        // Force menu regeneration even if suppressed (e.g., when settings panel is open)
                        var wasSuppressed = _suppressMenuRegeneration;
                        _suppressMenuRegeneration = false;
                        await GenerateModCharacterMenuAsync();
                        _suppressMenuRegeneration = wasSuppressed;

                        // Refresh ModGridPage if needed
                        if (contentFrame.Content is Pages.ModGridPage modGridPage)
                        {
                            if (modGridPage.CurrentViewMode == Pages.ModGridPage.ViewMode.Categories)
                            {
                                modGridPage.LoadCategories();
                            }
                            else
                            {
                                // If we're viewing the deleted category, go back to all mods
                                var currentCategory = modGridPage.GetType().GetField("_currentCategory",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(modGridPage) as string;

                                if (currentCategory == categoryName)
                                {
                                    modGridPage.LoadAllModsPublic();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to delete category: {categoryName}", ex);

                        var lang = SharedUtilities.LoadLanguageDictionary();
                        ShowErrorInfo(string.Format(
                            SharedUtilities.GetTranslation(lang, "Notify_CategoryDeleteFailed"), ex.Message));
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("MainWindow.CategoryContextMenu_Delete_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("MainWindow.CategoryContextMenu_Delete_Click failed", ex);
            }
        }
        
        private void CategoryContextMenu_PinUnpin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is string categoryName)
                {
                    try
                    {
                        var gameTag = SettingsManager.CurrentSelectedGame;
                        if (string.IsNullOrEmpty(gameTag))
                        {
                            Logger.LogWarning("No game selected, cannot pin/unpin category");
                            return;
                        }

                        var pinnedCategories = SettingsManager.GetPinnedCategories(gameTag);
                        bool isPinned = pinnedCategories.Contains(categoryName);

                        if (isPinned)
                        {
                            // Unpin category
                            SettingsManager.RemovePinnedCategory(gameTag, categoryName);
                            Logger.LogInfo($"Unpinned category: {categoryName}");
                        }
                        else
                        {
                            // Pin category
                            SettingsManager.AddPinnedCategory(gameTag, categoryName);
                            Logger.LogInfo($"Pinned category: {categoryName}");
                        }

                        // Defer menu/grid rebuild until the flyout is closed. The old
                        // direct await rebuilt the NavigationView during the click that
                        // owned the flyout and could end in a native WinUI crash.
                        DispatcherQueue.TryEnqueue(
                            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                            async () =>
                            {
                                try
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

                                    if (contentFrame.Content is Pages.ModGridPage modGridPage &&
                                        modGridPage.CurrentViewMode == Pages.ModGridPage.ViewMode.Categories)
                                    {
                                        modGridPage.LoadCategories();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError($"Failed to refresh category UI after pin/unpin: {categoryName}", ex);
                                }
                            });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to pin/unpin category: {categoryName}", ex);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("MainWindow.CategoryContextMenu_PinUnpin_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("MainWindow.CategoryContextMenu_PinUnpin_Click failed", ex);
            }
        }
        
        private async void CategoryContextMenu_DownloadThumbnail_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not MenuFlyoutItem item || item.Tag is not string categoryName) return;

                var lang = SharedUtilities.LoadLanguageDictionary();
                var gameTag = SettingsManager.CurrentSelectedGame;

                if (string.IsNullOrEmpty(gameTag))
                {
                    Logger.LogWarning("No game selected, cannot download thumbnail");
                    return;
                }

                var loadingDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "ContextMenu_DownloadThumbnail") ?? "Download Thumbnail",
                    Content = new ProgressRing { IsActive = true, Width = 40, Height = 40 },
                    XamlRoot = this.Content.XamlRoot
                };

                _ = loadingDialog.ShowAsync();

                List<Services.GameBananaService.CategoryRecord>? gbCategories = null;
                try
                {
                    gbCategories = await Services.GameBananaService.GetCharacterCategoriesAsync(gameTag);
                }
                finally
                {
                    loadingDialog.Hide();
                }

                if (gbCategories == null || gbCategories.Count == 0)
                {
                    ShowErrorInfo(SharedUtilities.GetTranslation(lang, "CategoryThumbnail_FetchFailed") ?? "Failed to fetch categories from GameBanana.");
                    return;
                }

                var searchBox = new TextBox
                {
                    PlaceholderText = SharedUtilities.GetTranslation(lang, "Search_Placeholder") ?? "Search...",
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var listView = new ListView
                {
                    Height = 420,
                    SelectionMode = ListViewSelectionMode.Single
                };

                void PopulateList(string filter)
                {
                    listView.Items.Clear();
                    var filtered = string.IsNullOrWhiteSpace(filter)
                        ? gbCategories
                        : gbCategories.Where(c => c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

                    foreach (var cat in filtered)
                    {
                        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Tag = cat };
                        if (!string.IsNullOrEmpty(cat.IconUrl))
                        {
                            row.Children.Add(new Image
                            {
                                Width = 24,
                                Height = 24,
                                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(cat.IconUrl))
                            });
                        }
                        row.Children.Add(new TextBlock { Text = cat.Name, VerticalAlignment = VerticalAlignment.Center });
                        listView.Items.Add(row);
                    }

                    for (int i = 0; i < listView.Items.Count; i++)
                    {
                        if (listView.Items[i] is StackPanel p && p.Tag is Services.GameBananaService.CategoryRecord r &&
                            r.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                        {
                            listView.SelectedIndex = i;
                            listView.ScrollIntoView(listView.Items[i]);
                            break;
                        }
                    }
                }

                PopulateList("");
                searchBox.TextChanged += (s, args) => PopulateList(searchBox.Text);

                var dialogPanel = new StackPanel { Spacing = 4 };
                dialogPanel.Children.Add(searchBox);
                dialogPanel.Children.Add(listView);

                var selectionDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "ContextMenu_DownloadThumbnail") ?? "Download Thumbnail",
                    Content = dialogPanel,
                    PrimaryButtonText = SharedUtilities.GetTranslation(lang, "Confirm") ?? "Confirm",
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "Cancel") ?? "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await selectionDialog.ShowAsync();
                if (result != ContentDialogResult.Primary) return;

                if (listView.SelectedItem is not StackPanel selectedPanel ||
                    selectedPanel.Tag is not Services.GameBananaService.CategoryRecord selectedCat) return;

                var iconUrl = selectedCat.GetIconUrl();
                if (string.IsNullOrEmpty(iconUrl))
                {
                    ShowErrorInfo(SharedUtilities.GetTranslation(lang, "CategoryThumbnail_NoIconUrl") ?? "No icon URL available for selected category.");
                    return;
                }

                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                var categoryPath = System.IO.Path.Combine(modsPath, categoryName);

                var success = await Services.GameBananaService.DownloadCategoryIconAsync(iconUrl, categoryPath);

                if (success)
                {
                    var downloadedFormat = SharedUtilities.GetTranslation(lang, "CategoryThumbnail_Downloaded") ?? "Downloaded icon for '{0}' from '{1}'.";
                    ShowSuccessInfo(string.Format(downloadedFormat, categoryName, selectedCat.Name), 3000);
                    await RefreshCategoryIconInMenuAsync(categoryName);
                    if (contentFrame.Content is Pages.ModGridPage modGridPage &&
                        modGridPage.CurrentViewMode == Pages.ModGridPage.ViewMode.Categories)
                        modGridPage.LoadCategories();
                }
                else
                {
                    ShowErrorInfo(SharedUtilities.GetTranslation(lang, "CategoryThumbnail_DownloadFailed") ?? "Failed to download icon.");
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("MainWindow.CategoryContextMenu_DownloadThumbnail_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("MainWindow.CategoryContextMenu_DownloadThumbnail_Click failed", ex);
            }
        }

        private async Task RefreshCategoryIconInMenuAsync(string categoryName)
        {
            try
            {
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                var menuItem = nvSample.MenuItems
                    .OfType<NavigationViewItem>()
                    .FirstOrDefault(i => i.Tag?.ToString() == $"Category_{categoryName}");

                if (menuItem == null) return;

                var newIcon = await CreateCategoryIconAsync(categoryName, modsPath);
                menuItem.Icon = newIcon;
                Logger.LogInfo($"Refreshed nav icon for category: {categoryName}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to refresh category icon for {categoryName}", ex);
            }
        }
    }
}
