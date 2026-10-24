using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Microsoft.UI;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.UI.Xaml.Data;
using Windows.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;
using FlairX_Mod_Manager.Models;
using Windows.Graphics.Imaging;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModGridPage
    {

        // TABLE VIEW METHODS
        
        private void LoadTableView()
        {
            if (_allModData == null || _allModData.Count == 0)
            {
                LoadAllModData(); // Ensure we have data
            }
            
            var tableItems = new List<ModTile>();
            
            // Load broken mods list once if we're in Broken view
            HashSet<string>? brokenModsList = _currentCategory == "Broken" ? ModListManager.LoadBrokenModsList() : null;
            
            foreach (var modData in _allModData ?? new List<ModData>())
            {
                // Apply current filter if any
                bool shouldInclude = true;
                
                if (_currentCategory == "Active" && !modData.IsActive)
                    shouldInclude = false;
                
                // Use persistent list for broken mods check (fast HashSet lookup)
                if (_currentCategory == "Broken" && brokenModsList != null && !brokenModsList.Contains(modData.Name))
                    shouldInclude = false;
                
                if (!string.IsNullOrEmpty(_currentCategory) && _currentCategory != "Active" && _currentCategory != "Broken" && _currentCategory != "NoPreviews" &&
                    !string.Equals(modData.Category, _currentCategory, StringComparison.OrdinalIgnoreCase))
                    shouldInclude = false;

                if (shouldInclude)
                {
                    var modTile = new ModTile 
                    { 
                        Name = modData.Name, 
                        ImagePath = modData.ImagePath, 
                        Directory = modData.Directory, 
                        IsActive = _activeMods.TryGetValue(GetCleanModName(modData.Directory), out var isActive) && isActive,
                        Category = modData.Category,
                        Author = modData.Author,
                        Url = modData.Url,
                        LastChecked = modData.LastChecked,
                        LastUpdated = modData.LastUpdated,
                        HasUpdate = CheckForUpdateLive(modData), // Live check without cache
                        IsVisible = true,
                        IsBroken = modData.IsBroken,
                        IsNSFW = modData.IsNSFW,
                        ImageSource = null // Will be loaded immediately below
                    };
                    
                    // Load image immediately for table view
                    if (!string.IsNullOrEmpty(modData.ImagePath) && File.Exists(modData.ImagePath))
                    {
                        modTile.ImageSource = CreateBitmapImage(modData.ImagePath);
                    }
                    tableItems.Add(modTile);
                }
            }
            
            // Store original items for search functionality
            _originalTableItems.Clear();
            foreach (var item in tableItems)
            {
                _originalTableItems.Add(item);
            }
            
            // Apply current search filter if any
            // Always set the table to use the observable collection
            ModsTableList.ItemsSource = _originalTableItems;
            
            // Apply search filter if active
            if (!string.IsNullOrWhiteSpace(_currentTableSearchQuery))
            {
                FilterTableResults();
            }
            
            // Update search UI
            UpdateSearchUI();
            
            // Load images for visible items
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                DispatcherQueue.TryEnqueue(() => LoadTableImages());
            });
        }
        
        private void LoadTableImages()
        {
            if (ModsTableList.ItemsSource is not IEnumerable<ModTile> items)
                return;
                
            foreach (var item in items.Take(100)) // Load first 100 images for table view
            {
                if (item.ImageSource == null && !string.IsNullOrEmpty(item.ImagePath))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.DecodePixelWidth = 48; // Optimize for table view size
                            bitmap.DecodePixelHeight = 48;
                            
                            // Convert to absolute path for UriSource
                            var absolutePath = Path.GetFullPath(item.ImagePath);
                            // Use UriSource for WebP support via Windows codecs
                            bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
                            
                            item.ImageSource = bitmap;
                            // Force UI refresh for this item
                            if (ModsTableList.ItemsSource is ObservableCollection<ModTile> collection)
                            {
                                var index = collection.IndexOf(item);
                                if (index >= 0)
                                {
                                    collection[index] = item;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Image loading failed - could be file not found, access denied, etc.
                            Logger.LogDebug($"Failed to load image {item.ImagePath}: {ex.Message}");
                        }
                    });
                }
            }
        }

        public void SwitchToTableView()
        {
            // Store the current view mode and category before switching to table
            _previousViewMode = CurrentViewMode;
            _previousTableCategory = _currentCategory;
            CurrentViewMode = ViewMode.Table;
            
            // Auto-focus search box when table view is activated
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                TableSearchBox?.Focus(FocusState.Programmatic);
                
                // Load images for table view
                _ = Task.Run(async () =>
                {
                    await Task.Delay(200);
                    DispatcherQueue.TryEnqueue(() => LoadTableImages());
                });
            });
        }

        // TABLE SEARCH FUNCTIONALITY
        
        private System.Collections.ObjectModel.ObservableCollection<ModTile> _originalTableItems = new System.Collections.ObjectModel.ObservableCollection<ModTile>();
        private string _currentTableSearchQuery = string.Empty;
        
        private void TableSearchBox_TextChanged(object sender, AutoSuggestBoxTextChangedEventArgs e)
        {
            if (sender is AutoSuggestBox searchBox)
            {
                _currentTableSearchQuery = searchBox.Text;
                FilterTableResults();
                UpdateSearchUI();
            }
        }
        

        
        private void FilterTableResults()
        {
            if (_originalTableItems == null || _originalTableItems.Count == 0)
                return;
                
            if (string.IsNullOrWhiteSpace(_currentTableSearchQuery) || _currentTableSearchQuery.Length < 3)
            {
                // No search query or less than 3 characters - show all items
                if (ModsTableList.ItemsSource != _originalTableItems)
                {
                    ModsTableList.ItemsSource = _originalTableItems;
                }
            }
            else
            {
                // Filter items by name, author, and URL (case-insensitive)
                var filteredItems = _originalTableItems
                    .Where(item => 
                        item.Name.Contains(_currentTableSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(item.Author) && item.Author.Contains(_currentTableSearchQuery, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(item.Url) && item.Url.Contains(_currentTableSearchQuery, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                    
                ModsTableList.ItemsSource = filteredItems;
            }
            
            // Load images for visible items after filtering
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                DispatcherQueue.TryEnqueue(() => LoadTableImages());
            });
        }
        
        private void UpdateSearchUI()
        {
            bool hasSearchQuery = !string.IsNullOrWhiteSpace(_currentTableSearchQuery);
            bool isSearchActive = hasSearchQuery && _currentTableSearchQuery.Length >= 3;
            
            // Update search results info
            if (isSearchActive)
            {
                var currentItems = ModsTableList.ItemsSource as IEnumerable<ModTile>;
                int resultCount = currentItems?.Count() ?? 0;
                
                if (resultCount == 0)
                {
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    TableSearchResultsText.Text = SharedUtilities.GetTranslation(lang, "No_Results_Found");
                }
                else
                {
                    TableSearchResultsText.Text = $"{resultCount} result{(resultCount == 1 ? "" : "s")} found";
                }
                
                TableSearchResultsInfo.Visibility = Visibility.Visible;
            }
            else if (hasSearchQuery && _currentTableSearchQuery.Length < 3)
            {
                TableSearchResultsText.Text = "Type at least 3 characters to search";
                TableSearchResultsInfo.Visibility = Visibility.Visible;
            }
            else
            {
                TableSearchResultsInfo.Visibility = Visibility.Collapsed;
            }
        }
        
        private void ClearTableSearch()
        {
            TableSearchBox.Text = string.Empty;
            _currentTableSearchQuery = string.Empty;
            _originalTableItems.Clear();
            UpdateSearchUI();
        }
        
        private void TableSearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                // Clear search on Escape
                TableSearchBox.Text = string.Empty;
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Enter)
            {
                // Focus table on Enter (remove focus from search box)
                ModsTableList.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }

        // TABLE VIEW EVENT HANDLERS
        
        private void SortByName_HeaderClick(object sender, RoutedEventArgs e)
        {
            _currentSortMode = (_currentSortMode == SortMode.NameAZ) ? SortMode.NameZA : SortMode.NameAZ;
            ApplySorting();
            UpdateSortIcons();
        }
        
        private void SortByCategory_HeaderClick(object sender, RoutedEventArgs e)
        {
            _currentSortMode = (_currentSortMode == SortMode.CategoryAZ) ? SortMode.CategoryZA : SortMode.CategoryAZ;
            ApplySorting();
            UpdateSortIcons();
        }
        
        private void SortByActive_HeaderClick(object sender, RoutedEventArgs e)
        {
            _currentSortMode = (_currentSortMode == SortMode.ActiveFirst) ? SortMode.InactiveFirst : SortMode.ActiveFirst;
            ApplySorting();
            UpdateSortIcons();
        }
        
        private void SortByLastChecked_HeaderClick(object sender, RoutedEventArgs e)
        {
            _currentSortMode = (_currentSortMode == SortMode.LastCheckedNewest) ? SortMode.LastCheckedOldest : SortMode.LastCheckedNewest;
            ApplySorting();
            UpdateSortIcons();
        }
        
        private void SortByLastUpdated_HeaderClick(object sender, RoutedEventArgs e)
        {
            _currentSortMode = (_currentSortMode == SortMode.LastUpdatedNewest) ? SortMode.LastUpdatedOldest : SortMode.LastUpdatedNewest;
            ApplySorting();
            UpdateSortIcons();
        }
        
        private void UpdateSortIcons()
        {
            // Reset all icons
            NameSortIcon.Visibility = Visibility.Collapsed;
            CategorySortIcon.Visibility = Visibility.Collapsed;
            ActiveSortIcon.Visibility = Visibility.Collapsed;
            LastCheckedSortIcon.Visibility = Visibility.Collapsed;
            LastUpdatedSortIcon.Visibility = Visibility.Collapsed;
            
            // Show appropriate icon
            switch (_currentSortMode)
            {
                case SortMode.NameAZ:
                    NameSortIcon.Glyph = "\uE70E"; // Up arrow
                    NameSortIcon.Visibility = Visibility.Visible;
                    break;
                case SortMode.NameZA:
                    NameSortIcon.Glyph = "\uE70D"; // Down arrow
                    NameSortIcon.Visibility = Visibility.Visible;
                    break;
                case SortMode.CategoryAZ:
                    CategorySortIcon.Glyph = "\uE70E";
                    CategorySortIcon.Visibility = Visibility.Visible;
                    break;
                case SortMode.CategoryZA:
                    CategorySortIcon.Glyph = "\uE70D";
                    CategorySortIcon.Visibility = Visibility.Visible;
                    break;
                case SortMode.ActiveFirst:
                    ActiveSortIcon.Glyph = "\uE70D"; // Down arrow (active first)
                    ActiveSortIcon.Visibility = Visibility.Visible;
                    break;
                case SortMode.InactiveFirst:
                    ActiveSortIcon.Glyph = "\uE70E"; // Up arrow (inactive first)
                    ActiveSortIcon.Visibility = Visibility.Visible;
                    break;
                case SortMode.LastCheckedNewest:
                    LastCheckedSortIcon.Glyph = "\uE70D";
                    LastCheckedSortIcon.Visibility = Visibility.Visible;
                    break;
                case SortMode.LastCheckedOldest:
                    LastCheckedSortIcon.Glyph = "\uE70E";
                    LastCheckedSortIcon.Visibility = Visibility.Visible;
                    break;
                case SortMode.LastUpdatedNewest:
                    LastUpdatedSortIcon.Glyph = "\uE70D";
                    LastUpdatedSortIcon.Visibility = Visibility.Visible;
                    break;
                case SortMode.LastUpdatedOldest:
                    LastUpdatedSortIcon.Glyph = "\uE70E";
                    LastUpdatedSortIcon.Visibility = Visibility.Visible;
                    break;
            }
        }
        
        private void ExitTableView_Click(object sender, RoutedEventArgs e)
        {
            // Clear search and return to previous view mode and category
            ClearTableSearch();
            _currentSortMode = SortMode.None;
            
            // Restore previous category context BEFORE changing view mode
            _currentCategory = _previousTableCategory;
            
            // Manually update UI visibility to avoid OnViewModeChanged automatic behavior
            ModsGrid.Visibility = Visibility.Visible;
            ModsTable.Visibility = Visibility.Collapsed;
            _currentViewMode = _previousViewMode; // Set directly without triggering setter
            
            // Load the correct content based on restored category
            if (_currentCategory == null)
            {
                var langDict = SharedUtilities.LoadLanguageDictionary();
                CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_All_Mods");
                LoadAllMods();
                CategoryBackButton.Visibility = Visibility.Collapsed;
            }
            else if (string.Equals(_currentCategory, "Active", StringComparison.OrdinalIgnoreCase))
            {
                var langDict = SharedUtilities.LoadLanguageDictionary();
                CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_Active_Mods");
                LoadActiveModsOnly();
                CategoryBackButton.Visibility = Visibility.Visible;
            }
            else if (string.Equals(_currentCategory, "Broken", StringComparison.OrdinalIgnoreCase))
            {
                var langDict = SharedUtilities.LoadLanguageDictionary();
                CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_Broken_Mods");
                LoadBrokenModsOnly();
                CategoryBackButton.Visibility = Visibility.Visible;
            }
            else if (string.Equals(_currentCategory, "NoPreviews", StringComparison.OrdinalIgnoreCase))
            {
                var langDict = SharedUtilities.LoadLanguageDictionary();
                CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_No_Previews");
                LoadNoPreviewModsOnly();
                CategoryBackButton.Visibility = Visibility.Visible;
            }
            else
            {
                CategoryTitle.Text = _currentCategory;
                LoadModsByCategory(_currentCategory);
                CategoryBackButton.Visibility = Visibility.Visible;
            }
            
            // Update MainWindow button text and refresh context menu
            UpdateMainWindowButtonText();
            RefreshContextMenuGlobally();
            
            // Clear the stored category
            _previousTableCategory = null;
        }
        
        private void TablePreview_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Image image && image.Tag is ModTile modTile && modTile.ImageSource != null)
            {
                PreviewImage.Source = modTile.ImageSource;
                
                // Get image bounds and window dimensions
                var bounds = image.TransformToVisual(this).TransformBounds(new Windows.Foundation.Rect(0, 0, image.ActualWidth, image.ActualHeight));
                var windowWidth = this.ActualWidth;
                var windowHeight = this.ActualHeight;
                
                const double popupWidth = 600;
                const double popupHeight = 600;
                const double margin = 15;
                
                // Calculate horizontal position - prefer right side, fallback to left
                double horizontalOffset;
                if (bounds.Right + margin + popupWidth <= windowWidth)
                {
                    // Show on right side
                    horizontalOffset = bounds.Right + margin;
                }
                else
                {
                    // Show on left side
                    horizontalOffset = Math.Max(margin, bounds.Left - popupWidth - margin);
                }
                
                // Calculate vertical position - keep fully visible
                double verticalOffset = Math.Max(margin, Math.Min(bounds.Top - 100, windowHeight - popupHeight - margin));
                
                ImagePreviewPopup.HorizontalOffset = horizontalOffset;
                ImagePreviewPopup.VerticalOffset = verticalOffset;
                
                ImagePreviewPopup.IsOpen = true;
            }
        }
        
        private void TablePreview_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ImagePreviewPopup.IsOpen = false;
        }
        
        private void SortingContextMenu_Opening(object sender, object e)
        {
            // Context menu logic is handled by UpdateContextFlyout method
        }
        
        private void UpdateContextFlyout()
        {
            if (ModsScrollViewer == null) return;
            
            // Disable context menu for empty space when in categories view showing category tiles
            if (CurrentViewMode == ViewMode.Categories && string.IsNullOrEmpty(_currentCategory))
            {
                // We're showing category tiles - disable empty space context menu
                ModsScrollViewer.ContextFlyout = null;
            }
            else
            {
                // Enable context menu for other modes
                ModsScrollViewer.ContextFlyout = SortingContextMenu;
            }
        }
        
        private void UpdateSearchBoxVisibility()
        {
            if (TableSearchBox == null) return;
            
            // Search box is only visible when sorting is enabled (table view)
            TableSearchBox.Visibility = (CurrentViewMode == ViewMode.Table) ? Visibility.Visible : Visibility.Collapsed;
        }
        
        private void TableContextMenu_Opening(object sender, object e)
        {
            if (sender is MenuFlyout menuFlyout && menuFlyout.Target is Border border && border.DataContext is ModTile modTile)
            {
                // Check if mod directory exists before showing context menu (skip for categories)
                if (!modTile.IsCategory)
                {
                    string modsPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                    if (string.IsNullOrEmpty(modsPath))
                    {
                        modsPath = PathManager.GetModsPath();
                    }
                    
                    string? fullModDir = FindModFolderPath(modsPath, modTile.Directory);
                    if (string.IsNullOrEmpty(fullModDir) || !Directory.Exists(fullModDir))
                    {
                        // Mod directory not found - cancel context menu opening
                        return;
                    }
                }
                
                menuFlyout.Items.Clear();
                var lang = SharedUtilities.LoadLanguageDictionary();
                
                if (modTile.IsCategory)
                {
                    // Category context menu: Open Folder, Copy Name, Rename, Pin/Unpin, Delete
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_OpenFolder"),
                        Icon = new SymbolIcon(Symbol.Folder),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[0]).Click += ContextMenu_OpenFolder_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutSeparator());
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_CopyName"),
                        Icon = new SymbolIcon(Symbol.Copy),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[2]).Click += ContextMenu_CopyName_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_Rename"),
                        Icon = new SymbolIcon(Symbol.Rename),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[3]).Click += ContextMenu_Rename_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutSeparator());
                    
                    // Check if category is pinned
                    var gameTag = SettingsManager.CurrentSelectedGame;
                    var pinnedCategories = !string.IsNullOrEmpty(gameTag) 
                        ? SettingsManager.GetPinnedCategories(gameTag) 
                        : new System.Collections.Generic.List<string>();
                    
                    bool isPinned = pinnedCategories.Contains(modTile.Name);
                    
                    // Pin/Unpin option
                    var pinItem = new MenuFlyoutItem
                    {
                        Text = isPinned 
                            ? SharedUtilities.GetTranslation(lang, "ContextMenu_UnpinCategory") 
                            : SharedUtilities.GetTranslation(lang, "ContextMenu_PinCategory"),
                        Icon = new FontIcon { Glyph = isPinned ? "\uE77A" : "\uE718" }, // Unpin : Pin
                        Tag = modTile
                    };
                    pinItem.Click += ContextMenu_PinUnpinCategory_Click;
                    menuFlyout.Items.Add(pinItem);
                    
                    // Delete option (always show, will auto-unpin if needed)
                    menuFlyout.Items.Add(new MenuFlyoutSeparator());
                    
                    var deleteCategoryItem = new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_DeleteCategory"),
                        Icon = new SymbolIcon(Symbol.Delete),
                        Tag = modTile
                    };
                    deleteCategoryItem.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                    deleteCategoryItem.Click += ContextMenu_DeleteCategory_Click;
                    menuFlyout.Items.Add(deleteCategoryItem);
                }
                else
                {
                    // Mod context menu: Dynamic Activate/Deactivate, Open Folder, View Details, Open URL, Copy Name, Rename, Delete
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = modTile.IsActive ? SharedUtilities.GetTranslation(lang, "ContextMenu_Deactivate") : SharedUtilities.GetTranslation(lang, "ContextMenu_Activate"),
                        Icon = new SymbolIcon(modTile.IsActive ? Symbol.Remove : Symbol.Accept),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[0]).Click += ContextMenu_ActivateDeactivate_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_OpenFolder"),
                        Icon = new SymbolIcon(Symbol.Folder),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[1]).Click += ContextMenu_OpenFolder_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_ViewDetails"),
                        Icon = new SymbolIcon(Symbol.View),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[2]).Click += ContextMenu_ViewDetails_Click;
                    
                    var modUrl = GetModUrl(modTile);
                    var openUrlItem = new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_OpenURL"),
                        Icon = new SymbolIcon(Symbol.Globe),
                        Tag = modTile,
                        IsEnabled = !string.IsNullOrEmpty(modUrl)
                    };
                    openUrlItem.Click += ContextMenu_OpenUrl_Click;
                    menuFlyout.Items.Add(openUrlItem);
                    
                    menuFlyout.Items.Add(new MenuFlyoutSeparator());
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_CopyName"),
                        Icon = new SymbolIcon(Symbol.Copy),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[5]).Click += ContextMenu_CopyName_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_Rename"),
                        Icon = new SymbolIcon(Symbol.Rename),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[6]).Click += ContextMenu_Rename_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutSeparator());
                    
                    var deleteItem = new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_Delete"),
                        Icon = new SymbolIcon(Symbol.Delete),
                        Tag = modTile
                    };
                    deleteItem.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                    deleteItem.Click += ContextMenu_Delete_Click;
                    menuFlyout.Items.Add(deleteItem);
                }
                
                // Add exit table view option
                menuFlyout.Items.Add(new MenuFlyoutSeparator());
                var langDict = SharedUtilities.LoadLanguageDictionary();
                menuFlyout.Items.Add(new MenuFlyoutItem
                {
                    Text = SharedUtilities.GetTranslation(langDict, "Exit_Table_View"),
                    Icon = new SymbolIcon(Symbol.Back)
                });
                ((MenuFlyoutItem)menuFlyout.Items[menuFlyout.Items.Count - 1]).Click += ExitTableView_Click;
            }
        }

        // Check for updates using cached persistent list (fast HashSet lookup)
        private bool CheckForUpdateLive(ModData modData)
        {
            try
            {
                if (_cachedOutdatedMods == null || string.IsNullOrEmpty(modData.Name))
                    return false;

                // The category is already on the item, so no scan of _allModData is needed.
                // This used to run a linear search over the whole library for every tile that
                // was about to be created - O(n^2) over 1500 mods per view.
                return _cachedOutdatedMods.Contains($"{modData.Category ?? "Other"}|{modData.Name}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"CheckForUpdateLive error for {modData.Directory}", ex);
                return false;
            }
        }

        private BitmapImage CreateBitmapImage(string imagePath)
        {
            var bitmap = new BitmapImage();
            try
            {
                if (File.Exists(imagePath))
                {
                    Logger.LogDebug($"CreateBitmapImage: Loading {imagePath}");
                    // Convert to absolute path for UriSource
                    var absolutePath = Path.GetFullPath(imagePath);
                    Logger.LogDebug($"CreateBitmapImage: Absolute path {absolutePath}");
                    // Use UriSource for direct file loading - this uses Windows codecs including WebP
                    bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
                    Logger.LogDebug($"CreateBitmapImage: UriSource set successfully for {absolutePath}");
                }
                else
                {
                    Logger.LogDebug($"CreateBitmapImage: File not found {imagePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load image {imagePath}", ex);
            }
            return bitmap;
        }
    }
}
