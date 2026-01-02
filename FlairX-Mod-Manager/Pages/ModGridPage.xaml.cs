﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using Microsoft.UI.Xaml.Controls;
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
    // Converter for heart icon glyph
    public class BoolToHeartGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (bool)value ? "\uEB52" : "\uEB51"; // Filled heart : Empty heart
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    // Converter for heart icon color
    public class BoolToHeartColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((bool)value)
            {
                // Use system accent color for active hearts
                var accentColor = (Color)Application.Current.Resources["SystemAccentColor"];
                return new SolidColorBrush(accentColor);
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }



    public sealed partial class ModGridPage : Page
    {
        public enum ViewMode
        {
            Mods,
            Categories,
            Table
        }

        public enum SortMode
        {
            None,
            NameAZ,
            NameZA,
            CategoryAZ,
            CategoryZA,
            ActiveFirst,
            InactiveFirst,
            LastCheckedNewest,
            LastCheckedOldest,
            LastUpdatedNewest,
            LastUpdatedOldest
        }

        private ViewMode _currentViewMode = ViewMode.Mods;
        private SortMode _currentSortMode = SortMode.None;
        public ViewMode CurrentViewMode
        {
            get => _currentViewMode;
            set
            {
                if (_currentViewMode != value)
                {
                    _currentViewMode = value;
                    OnViewModeChanged();
                }
            }
        }

        private static string ActiveModsStatePath 
        {
            get
            {
                return PathManager.GetActiveModsPath();
            }
        }
        private Dictionary<string, bool> _activeMods = new();
        private ObservableCollection<ModTile> _allMods = new();
        
        private string? _currentCategoryField; // Track current category for back navigation
        private string? _currentCategory
        {
            get => _currentCategoryField;
            set
            {
                if (_currentCategoryField != value)
                {
                    _currentCategoryField = value;
                    // Global context menu refresh on category change
                    RefreshContextMenuGlobally();
                    // Update context flyout based on current category
                    UpdateContextFlyout();
                }
            }
        }
        private string? _previousCategory; // Track category before search for restoration
        private bool _isSearchActive = false; // Track if we're currently in search mode
        private bool _wasInCategoryMode = false; // Track if we were in category mode before navigation
        private ViewMode _previousViewMode = ViewMode.Mods; // Track view mode before navigation
        private string? _previousTableCategory; // Track category before table view for restoration
        

        
        // Virtualized loading - store all mod data but only create visible ModTiles
        private List<ModData> _allModData = new();
        
        // Thread-safe JSON Caching System
        private static readonly Dictionary<string, ModData> _modJsonCache = new();
        private static readonly Dictionary<string, DateTime> _modFileTimestamps = new();
        private static readonly object _cacheLock = new object();
        


        private void OnViewModeChanged()
        {
            // Update UI visibility based on view mode
            ModsGrid.Visibility = (CurrentViewMode == ViewMode.Table) ? Visibility.Collapsed : Visibility.Visible;
            ModsTable.Visibility = (CurrentViewMode == ViewMode.Table) ? Visibility.Visible : Visibility.Collapsed;
            
            // Search box is only visible when sorting is enabled (table view)
            UpdateSearchBoxVisibility();
            
            if (CurrentViewMode == ViewMode.Categories)
            {
                LoadCategories();
            }
            else if (CurrentViewMode == ViewMode.Table)
            {
                // Load table view with current data
                LoadTableView();
            }
            else
            {
                // When switching back to mods view, load all mods
                _currentCategory = null; // Clear current category to show all mods
                var langDict = SharedUtilities.LoadLanguageDictionary();
                CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_All_Mods");
                LoadAllMods();
                
                // Hide back button in all mods view
                CategoryBackButton.Visibility = Visibility.Collapsed;
            }
            
            // Update MainWindow button text
            UpdateMainWindowButtonText();
            
            // Global context menu refresh on view mode change
            RefreshContextMenuGlobally();
            
            // Update context flyout based on current mode
            UpdateContextFlyout();
        }

        private void UpdateMainWindowButtonText()
        {
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                // Use MainWindow's actual view mode, not ModGridPage's view mode
                bool isCategoryMode = mainWindow.IsCurrentlyInCategoryMode();
                mainWindow.UpdateViewModeButtonIcon(isCategoryMode);
            }
        }

        /// <summary>
        /// Global context menu refresh - called whenever view state changes
        /// This ensures context menu is always in sync with current view
        /// </summary>
        private void RefreshContextMenuGlobally()
        {
            try
            {
                // Use a small delay to ensure all state changes are complete
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    UpdateContextMenuVisibility();
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in RefreshContextMenuGlobally: {ex.Message}");
            }
        }

        /// <summary>
        /// Public method to refresh context menu (for external calls like language changes)
        /// </summary>
        public void RefreshContextMenu()
        {
            RefreshContextMenuGlobally();
        }

        // TABLE VIEW METHODS
        
        private void LoadTableView()
        {
            if (_allModData == null || _allModData.Count == 0)
            {
                LoadAllModData(); // Ensure we have data
            }
            
            var tableItems = new List<ModTile>();
            foreach (var modData in _allModData ?? new List<ModData>())
            {
                // Apply current filter if any
                bool shouldInclude = true;
                
                if (_currentCategory == "Active" && !modData.IsActive)
                    shouldInclude = false;
                
                if (!string.IsNullOrEmpty(_currentCategory) && _currentCategory != "Active" && 
                    !string.Equals(modData.Category, _currentCategory, StringComparison.OrdinalIgnoreCase))
                    shouldInclude = false;

                if (shouldInclude)
                {
                    var modTile = new ModTile 
                    { 
                        Name = modData.Name, 
                        ImagePath = modData.ImagePath, 
                        Directory = modData.Directory, 
                        IsActive = _activeMods.TryGetValue(modData.Directory, out var isActive) && isActive,
                        Category = modData.Category,
                        Author = modData.Author,
                        Url = modData.Url,
                        LastChecked = modData.LastChecked,
                        LastUpdated = modData.LastUpdated,
                        HasUpdate = CheckForUpdateLive(modData.Directory), // Live check without cache
                        IsVisible = true,
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
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.DecodePixelWidth = 48; // Optimize for table view size
                            bitmap.DecodePixelHeight = 48;
                            
                            var file = await StorageFile.GetFileFromPathAsync(item.ImagePath);
                            using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                            await bitmap.SetSourceAsync(stream);
                            
                            // Ensure UI update happens on UI thread
                            DispatcherQueue.TryEnqueue(() => 
                            {
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
                            });
                        }
                        catch (Exception ex)
                        {
                            // Image loading failed - could be file not found, access denied, etc.
                            Logger.LogDebug($"Failed to load image {item.ImagePath}: {ex.Message}");
                            
                            // Set a default/placeholder image on UI thread
                            DispatcherQueue.TryEnqueue(() => 
                            {
                                // You could set a default image here if needed
                                // item.ImageSource = defaultImage;
                            });
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
                    TableSearchResultsText.Text = "No results found";
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
                    // Category context menu: Open Folder, Copy Name, Rename
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

        // Public methods for MainWindow to call
        public void LoadAllModsPublic()
        {
            // Force load all mods regardless of current view mode
            _currentCategory = null; // Clear current category to show all mods
            var langDict = SharedUtilities.LoadLanguageDictionary();
            CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_All_Mods");
            LoadAllMods();
            
            // Hide back button
            CategoryBackButton.Visibility = Visibility.Collapsed;
        }

        public void LoadAllCategories()
        {
            // Force load all categories regardless of current view mode
            _currentCategory = null; // Clear current category
            LoadCategories();
            
            // Hide back button
            CategoryBackButton.Visibility = Visibility.Collapsed;
            
            // Ensure context menu is properly disabled for categories view
            UpdateContextFlyout();
            
            // Don't update MainWindow button - let MainWindow manage its own state
        }

        // SEPARATE NAVIGATION SYSTEMS FOR EACH MODE
        
        public void LoadCategoryInDefaultMode(string category)
        {
            // DEFAULT MODE ONLY: Load specific category mods
            _currentCategory = category;
            CategoryTitle.Text = category;
            LoadModsByCategory(category);
            CategoryBackButton.Visibility = Visibility.Visible;
            CategoryOpenFolderButton.Visibility = Visibility.Visible;
        }
        
        public void LoadCategoryInCategoryMode(string category)
        {
            Logger.LogDebug($"LoadCategoryInCategoryMode called with category: {category}");
            // CATEGORY MODE ONLY: Load specific category mods in category mode
            _currentViewMode = ViewMode.Categories; // Force category mode
            _currentCategory = category;
            CategoryTitle.Text = category;
            LoadModsByCategory(category);
            CategoryBackButton.Visibility = Visibility.Visible;
            CategoryOpenFolderButton.Visibility = Visibility.Visible;
            Logger.LogDebug($"CategoryOpenFolderButton visibility set to Visible for category: {category}");
        }

        public ModGridPage()
        {
            Logger.LogMethodEntry("ModGridPage constructor starting");
            try
            {
                Logger.LogInfo("Initializing ModGridPage components");
                this.InitializeComponent();
                
                Logger.LogInfo("Loading active mods state");
                LoadActiveMods();
                
                Logger.LogInfo("Ensuring mod.json files exist in ModLibrary");
                // EnsureModJsonInModLibrary removed - no longer needed
                
                this.Loaded += ModGridPage_Loaded;
                
                Logger.LogInfo("Updating context menu translations");
                UpdateContextMenuTranslations();
                
                Logger.LogInfo("Setting up pointer event handlers");
                this.AddHandler(PointerPressedEvent, new PointerEventHandler(ModGridPage_PointerPressed), handledEventsToo: true);
                
                Logger.LogInfo($"Loading zoom settings - Current zoom factor: {FlairX_Mod_Manager.SettingsManager.Current.ZoomLevel}");
                _zoomFactor = FlairX_Mod_Manager.SettingsManager.Current.ZoomLevel;
                
                Logger.LogInfo("Setting up container content changing handler");
                ModsGrid.ContainerContentChanging += ModsGrid_ContainerContentChanging;
                
                Logger.LogInfo("Starting background loading");
                StartBackgroundLoadingIfNeeded();
                
                Logger.LogMethodExit("ModGridPage constructor completed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during ModGridPage initialization", ex);
                throw;
            }
        }

        // Background loading moved to ModGridPage.Loading.cs

        // Loading methods moved to ModGridPage.Loading.cs

        // Loading methods moved to ModGridPage.Loading.cs

        // Navigation methods moved to ModGridPage.Navigation.cs

        // Lightweight mod data for virtualized loading
        private class ModData
        {
            public string Name { get; set; } = "";
            public string ImagePath { get; set; } = "";
            public string Directory { get; set; } = "";
            public bool IsActive { get; set; }
            public string Character { get; set; } = "";
            public string Author { get; set; } = "";
            public string Url { get; set; } = "";
            public string Category { get; set; } = "";
            public DateTime LastChecked { get; set; } = DateTime.MinValue;
            public DateTime LastUpdated { get; set; } = DateTime.MinValue;
            public bool HasUpdate { get; set; } = false;
            public bool IsNSFW { get; set; } = false;
        }

        // Check for updates without cache - always reads fresh from mod.json
        private static bool CheckForUpdateLive(string modDirectory)
        {
            try
            {
                var modsPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                
                // Find the mod.json file
                string? modJsonPath = null;
                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    var potentialPath = Path.Combine(categoryDir, modDirectory, "mod.json");
                    if (File.Exists(potentialPath))
                    {
                        modJsonPath = potentialPath;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(modJsonPath) || !File.Exists(modJsonPath))
                {
                    return false;
                }

                // Read fresh data from mod.json (no cache)
                var json = Services.FileAccessQueue.ReadAllText(modJsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Check if gbChangeDate > dateUpdated
                string? gbChangeDateStr = null;
                string? dateUpdatedStr = null;
                
                if (root.TryGetProperty("gbChangeDate", out var gbChangeProp) && gbChangeProp.ValueKind == JsonValueKind.String)
                {
                    gbChangeDateStr = gbChangeProp.GetString();
                }
                
                if (root.TryGetProperty("dateUpdated", out var dateUpdProp) && dateUpdProp.ValueKind == JsonValueKind.String)
                {
                    dateUpdatedStr = dateUpdProp.GetString();
                }
                
                if (!string.IsNullOrEmpty(gbChangeDateStr) && !string.IsNullOrEmpty(dateUpdatedStr))
                {
                    if (DateTime.TryParse(gbChangeDateStr, out var gbDate) &&
                        DateTime.TryParse(dateUpdatedStr, out var updatedDate))
                    {
                        var hasUpdate = gbDate > updatedDate;
                        if (hasUpdate)
                        {
                            Logger.LogInfo($"CheckForUpdateLive: {modDirectory} has update - gbChangeDate={gbChangeDateStr}, dateUpdated={dateUpdatedStr}");
                        }
                        return hasUpdate;
                    }
                    else
                    {
                        Logger.LogWarning($"CheckForUpdateLive: Failed to parse dates for {modDirectory} - gbChangeDate={gbChangeDateStr}, dateUpdated={dateUpdatedStr}");
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"CheckForUpdateLive error for {modDirectory}", ex);
                return false;
            }
        }

        private void CategoryBackButton_Click(object sender, RoutedEventArgs e)
        {
            // Exit table view if active - do exactly what ExitTableView_Click does
            if (CurrentViewMode == ViewMode.Table)
            {
                ExitTableView_Click(sender, e);
                return;
            }
            
            // Two separate navigation schemes based on view mode
            if (CurrentViewMode == ViewMode.Categories)
            {
                // CATEGORY MODE: Go back to all categories
                _currentCategory = null; // Clear current category
                LoadCategories();
            }
            else
            {
                // DEFAULT MODE: Always go back to All Mods
                _currentCategory = null;
                var langDict = SharedUtilities.LoadLanguageDictionary();
                CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_All_Mods");
                LoadAllMods();
            }
            
            // Hide back button and folder button
            CategoryBackButton.Visibility = Visibility.Collapsed;
            CategoryOpenFolderButton.Visibility = Visibility.Collapsed;
        }

        private void CategoryOpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentCategory))
                    return;

                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                

                var categoryPath = Path.Combine(modsPath, _currentCategory);
                
                if (Directory.Exists(categoryPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = categoryPath,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error opening category folder", ex);
            }
        }

        private void ModGridPage_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Check if it's the back button on the mouse (XButton1) and back button is visible
            if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
            {
                var properties = e.GetCurrentPoint(this).Properties;
                if (properties.IsXButton1Pressed && CategoryBackButton.Visibility == Visibility.Visible)
                {
                    // Use dispatcher to ensure it runs on UI thread and doesn't get blocked
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
                    {
                        CategoryBackButton_Click(this, new RoutedEventArgs());
                    });
                    e.Handled = true;
                }
            }
        }

        private BitmapImage CreateBitmapImage(string imagePath)
        {
            var bitmap = new BitmapImage();
            try
            {
                if (File.Exists(imagePath))
                {
                    // Read file into memory steam to avoid file locking issues
                    byte[] imageData = File.ReadAllBytes(imagePath);
                    using (var memStream = new MemoryStream(imageData))
                    {
                        bitmap.SetSource(memStream.AsRandomAccessStream());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to load image {imagePath}: {ex.Message}");
            }
            return bitmap;
        }

        public string GetCategoryTitleText()
        {
            return CategoryTitle?.Text ?? string.Empty;
        }

        // Static helper for symlink creation moved to ModGridPage.StaticUtilities.cs

        // Instance method for UI refresh (already present, but ensure public)
        public void RefreshUIAfterLanguageChange()
        {
            // Menu regeneration removed - unnecessary when using sliding panels instead of page navigation
            // var mainWindow = ((App)Application.Current).MainWindow as FlairX_Mod_Manager.MainWindow;
            // if (mainWindow != null)
            // {
            //     _ = mainWindow.GenerateModCharacterMenuAsync();
            // }
            
            // EnsureModJsonInModLibrary removed - no longer needed
            
            // Update translations for UI elements
            UpdateTranslations();
            UpdateContextMenuTranslations();
            
            // Update CategoryTitle based on current view
            UpdateCategoryTitle();
            
            // LoadActiveMods() removed - this was likely causing the grid reload during language changes
            // Active mod state doesn't change when switching languages, no need to reload from disk
            
            // LoadAllMods() removed - unnecessary when just changing language, mods are already loaded
        }
        
        private void UpdateCategoryTitle()
        {
            var langDict = SharedUtilities.LoadLanguageDictionary();
            
            if (string.IsNullOrEmpty(_currentCategory))
            {
                // All Mods view
                CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_All_Mods");
            }
            else if (_currentCategory == "Active")
            {
                // Active Mods view
                CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_Active_Mods");
            }
            else if (_currentCategory == "Outdated")
            {
                // Outdated Mods view
                CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_Outdated_Mods");
            }
            else if (_currentCategory.Equals("other", StringComparison.OrdinalIgnoreCase))
            {
                // Other Mods view
                CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_Other_Mods");
            }
            else
            {
                // Specific category - keep the category name as is
                CategoryTitle.Text = _currentCategory;
            }
        }

        // Add function to display path with single slashes moved to ModGridPage.StaticUtilities.cs

        // GetModUrl method moved to ModGridPage.ContextMenu.cs

        // Context menu methods moved to ModGridPage.ContextMenu.cs

        // Tile hover handlers for tilt effects only
        


        private Border? FindTileBorder(Button btn)
        {
            try
            {
                if (btn.Content is Border directBorder)
                    return directBorder;

                if (btn.ContentTemplateRoot is FrameworkElement root)
                {
                    var found = root.FindName("TileBorder") as Border;
                    if (found != null) return found;
                }

                // Fallback: search visual tree for a Border named TileBorder
                return FindChildBorderByName(btn, "TileBorder");
            }
            catch
            {
                return null;
            }
        }

        private Border? FindChildBorderByName(DependencyObject parent, string name)
        {
            int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is Border b && b.Name == name)
                    return b;
                var result = FindChildBorderByName(child, name);
                if (result != null) return result;
            }
            return null;
        }



        // Dynamic NSFW filtering
        public void FilterNSFWMods(bool hideNSFW)
        {
            if (ModsGrid?.ItemsSource is not ObservableCollection<ModTile> mods) return;
            
            // Don't filter in category tiles view
            if (CurrentViewMode == ViewMode.Categories)
                return;
            
            Logger.LogGrid($"FilterNSFWMods called: hideNSFW={hideNSFW}, current mods count={mods.Count}");
            
            var modsToRemove = new List<ModTile>();
            var modsToAdd = new List<ModTile>();
            
            if (hideNSFW)
            {
                // Hide NSFW mods - find mods with IsNSFW in _allModData
                foreach (var mod in mods.ToList())
                {
                    // Skip category tiles
                    if (mod.IsCategory) continue;
                    
                    var modData = _allModData.FirstOrDefault(m => m.Directory == mod.Directory);
                    if (modData != null && modData.IsNSFW)
                    {
                        modsToRemove.Add(mod);
                    }
                }
            }
            else
            {
                // Show NSFW mods - add back mods that were hidden
                foreach (var modData in _allModData)
                {
                    // If in category mode, only show mods from current category
                    if (!string.IsNullOrEmpty(_currentCategory) && modData.Category != _currentCategory)
                        continue;
                    
                    if (modData.IsNSFW && !mods.Any(m => m.Directory == modData.Directory))
                    {
                        var modTile = new ModTile
                        {
                            Name = modData.Name,
                            ImagePath = modData.ImagePath,
                            Directory = modData.Directory,
                            IsActive = modData.IsActive,
                            Category = modData.Category,
                            Author = modData.Author,
                            Url = modData.Url,
                            LastChecked = modData.LastChecked,
                            LastUpdated = modData.LastUpdated,
                            HasUpdate = modData.HasUpdate,
                            IsVisible = true,
                            ImageSource = null
                        };
                        modsToAdd.Add(modTile);
                    }
                }
            }
            
            // Remove mods with animation
            foreach (var mod in modsToRemove)
            {
                mod.IsBeingDeleted = true;
            }
            
            _ = Task.Run(async () =>
            {
                await Task.Delay(500); // Wait for animation
                DispatcherQueue.TryEnqueue(() =>
                {
                    foreach (var mod in modsToRemove)
                    {
                        mods.Remove(mod);
                    }
                    
                    // Add back mods if showing NSFW
                    foreach (var mod in modsToAdd)
                    {
                        mods.Add(mod);
                    }
                    
                    // Sort if needed
                    if (SettingsManager.Current.ActiveModsToTopEnabled)
                    {
                        var sorted = mods.OrderByDescending(m => m.IsActive).ThenBy(m => m.Name).ToList();
                        mods.Clear();
                        foreach (var m in sorted)
                        {
                            mods.Add(m);
                        }
                    }
                    
                    // Load visible images for newly added mods
                    if (modsToAdd.Count > 0)
                    {
                        LoadVisibleImages();
                    }
                });
            });
        }

        // Drag & Drop Event Handlers
        private async Task HandleCategoryImageDrop(Button btn, ModTile tile, List<StorageFile> imageFiles)
        {
            var dropOverlay = FindVisualChild<Border>(btn, "DragDropOverlay");
            var dropIcon = dropOverlay != null ? FindVisualChild<FontIcon>(dropOverlay, "DropIcon") : null;
            var dropText = dropOverlay != null ? FindVisualChild<TextBlock>(dropOverlay, "DropText") : null;
            var lang = SharedUtilities.LoadLanguageDictionary();
            
            try
            {
                // For categories, only allow 1 image
                if (imageFiles.Count > 1)
                {
                    Logger.LogInfo($"Multiple images dropped on category ({imageFiles.Count}), rejecting");
                    if (dropText != null)
                    {
                        dropText.Text = string.Format(SharedUtilities.GetTranslation(lang, "DragDrop_LimitReached"), 1);
                    }
                    if (dropIcon != null)
                    {
                        dropIcon.Glyph = "\uE711"; // Cross/Cancel icon
                        dropIcon.FontSize = 48;
                    }
                    
                    await Task.Delay(2000);
                    
                    // Fade out overlay
                    if (dropOverlay != null)
                    {
                        var fadeOut = new DoubleAnimation
                        {
                            From = 1,
                            To = 0,
                            Duration = new Duration(TimeSpan.FromMilliseconds(300))
                        };
                        var storyboard = new Storyboard();
                        Storyboard.SetTarget(fadeOut, dropOverlay);
                        Storyboard.SetTargetProperty(fadeOut, "Opacity");
                        storyboard.Children.Add(fadeOut);
                        storyboard.Completed += (s, args) =>
                        {
                            dropOverlay.Visibility = Visibility.Collapsed;
                            dropOverlay.Opacity = 0;
                            if (dropText != null) dropText.Text = SharedUtilities.GetTranslation(lang, "DragDrop_DropImagesHere");
                            if (dropIcon != null)
                            {
                                dropIcon.Glyph = "\uE896";
                                dropIcon.FontSize = 48;
                            }
                        };
                        storyboard.Begin();
                    }
                    return;
                }
                
                var imageFile = imageFiles[0];
                Logger.LogInfo($"Processing category image drop: {imageFile.Name}");
                Logger.LogInfo($"Category tile.Directory: {tile.Directory}");
                Logger.LogInfo($"Category tile.Name: {tile.Name}");
                
                // Get category folder path
                var gameTag = SettingsManager.CurrentSelectedGame;
                var gameModsPath = AppConstants.GameConfig.GetModsPath(gameTag);
                string modsPath = PathManager.GetAbsolutePath(gameModsPath);
                
                Logger.LogInfo($"Mods path: {modsPath}");
                
                // For categories, the Directory is just the category name, not a full path
                string categoryFolderPath = Path.Combine(modsPath, tile.Directory);
                
                Logger.LogInfo($"Category folder path: {categoryFolderPath}");
                Logger.LogInfo($"Category folder exists: {Directory.Exists(categoryFolderPath)}");
                
                if (!Directory.Exists(categoryFolderPath))
                {
                    Logger.LogError($"Category folder does not exist: {categoryFolderPath}");
                    if (dropOverlay != null)
                    {
                        dropOverlay.Visibility = Visibility.Collapsed;
                        dropOverlay.Opacity = 0;
                    }
                    return;
                }
                
                // Show processing state
                if (dropText != null)
                {
                    dropText.Text = string.Format(SharedUtilities.GetTranslation(lang, "DragDrop_Copying"), 1);
                }
                if (dropIcon != null)
                {
                    dropIcon.Glyph = "\uE895"; // Sync icon
                }
                
                // Copy image to category folder as preview.jpg (overwriting if exists)
                string targetPath = Path.Combine(categoryFolderPath, "preview.jpg");
                
                // Delete existing preview files (preview.*, catprev.jpg, catmini.jpg)
                var filesToDelete = new List<string>();
                
                // Add preview.* files
                filesToDelete.AddRange(Directory.GetFiles(categoryFolderPath, "preview.*")
                    .Where(f => 
                    {
                        var ext = Path.GetExtension(f).ToLower();
                        return ext == ".jpg" || ext == ".jpeg" || ext == ".png";
                    }));
                
                // Add catprev.jpg and catmini.jpg if they exist
                var catprevPath = Path.Combine(categoryFolderPath, "catprev.jpg");
                var catminiPath = Path.Combine(categoryFolderPath, "catmini.jpg");
                if (File.Exists(catprevPath)) filesToDelete.Add(catprevPath);
                if (File.Exists(catminiPath)) filesToDelete.Add(catminiPath);
                
                foreach (var existingFile in filesToDelete)
                {
                    try
                    {
                        File.Delete(existingFile);
                        Logger.LogInfo($"Deleted existing file: {existingFile}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to delete existing file: {existingFile}", ex);
                    }
                }
                
                // Copy new image using File.Copy (more reliable than StorageFolder)
                File.Copy(imageFile.Path, targetPath, overwrite: true);
                Logger.LogInfo($"Copied image to: {targetPath}");
                
                // Run image optimizer in category mode using DragDropCategoryMode setting
                var context = Services.ImageOptimizationService.GetOptimizationContext(
                    Services.OptimizationTrigger.DragDropCategory);
                Logger.LogInfo($"Running image optimizer in category mode (Mode: {context.Mode})");
                await Task.Run(() =>
                {
                    try
                    {
                        Logger.LogInfo($"Calling ProcessCategoryPreview for: {categoryFolderPath}");
                        Services.ImageOptimizationService.ProcessCategoryPreview(categoryFolderPath, context);
                        Logger.LogInfo("Category optimization complete");
                        
                        // Delete preview.jpg after optimization (keep only catprev.jpg and catmini.jpg) - if KeepOriginals is disabled
                        if (!context.KeepOriginals && File.Exists(targetPath))
                        {
                            File.Delete(targetPath);
                            Logger.LogInfo($"Deleted original preview.jpg after optimization");
                        }
                        else if (context.KeepOriginals)
                        {
                            Logger.LogInfo($"Keeping original preview.jpg (KeepOriginals enabled)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Category optimization failed", ex);
                    }
                });
                
                // Show success
                if (dropText != null)
                {
                    dropText.Text = SharedUtilities.GetTranslation(lang, "DragDrop_ImagesAdded");
                }
                if (dropIcon != null)
                {
                    dropIcon.Glyph = "\uE73E"; // Checkmark
                    dropIcon.FontSize = 48;
                }
                
                await Task.Delay(2000);
                
                // Fade out overlay
                if (dropOverlay != null)
                {
                    var fadeOut = new DoubleAnimation
                    {
                        From = 1,
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(300))
                    };
                    var storyboard = new Storyboard();
                    Storyboard.SetTarget(fadeOut, dropOverlay);
                    Storyboard.SetTargetProperty(fadeOut, "Opacity");
                    storyboard.Children.Add(fadeOut);
                    storyboard.Completed += (s, args) =>
                    {
                        dropOverlay.Visibility = Visibility.Collapsed;
                        dropOverlay.Opacity = 0;
                        if (dropText != null) dropText.Text = SharedUtilities.GetTranslation(lang, "DragDrop_DropImagesHere");
                        if (dropIcon != null) dropIcon.Glyph = "\uE896";
                    };
                    storyboard.Begin();
                }
                
                // Reload the category image
                await Task.Delay(500); // Wait for animation
                DispatcherQueue.TryEnqueue(() =>
                {
                    tile.ImageSource = null;
                    LoadVisibleImages();
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in category drag & drop handler", ex);
                if (dropOverlay != null)
                {
                    dropOverlay.Visibility = Visibility.Collapsed;
                    dropOverlay.Opacity = 0;
                }
            }
        }
        
        private OptimizationMode ParseOptimizationMode(string modeString)
        {
            if (Enum.TryParse<OptimizationMode>(modeString, out var mode))
                return mode;
            return OptimizationMode.Standard;
        }
        

        private void Tile_DragEnter(object sender, DragEventArgs e)
        {
            Logger.LogInfo("Tile_DragEnter called");
            
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                Logger.LogInfo("Contains storage items");
                e.AcceptedOperation = DataPackageOperation.Copy;
                
                if (sender is Button btn && btn.DataContext is ModTile tile)
                {
                    Logger.LogInfo($"Drag enter on {(tile.IsCategory ? "category" : "mod")}: {tile.Name}");
                    var overlay = FindVisualChild<Border>(btn, "DragDropOverlay");
                    if (overlay != null)
                    {
                        Logger.LogInfo("Found overlay, showing it");
                        overlay.Visibility = Visibility.Visible;
                        
                        // Update text for category vs mod
                        var dropText = FindVisualChild<TextBlock>(overlay, "DropText");
                        if (dropText != null && tile.IsCategory)
                        {
                            var lang = SharedUtilities.LoadLanguageDictionary();
                            dropText.Text = SharedUtilities.GetTranslation(lang, "DragDrop_DropImagesHere");
                        }
                        
                        var fadeIn = new DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = new Duration(TimeSpan.FromMilliseconds(200))
                        };
                        var storyboard = new Storyboard();
                        Storyboard.SetTarget(fadeIn, overlay);
                        Storyboard.SetTargetProperty(fadeIn, "Opacity");
                        storyboard.Children.Add(fadeIn);
                        storyboard.Begin();
                    }
                    else
                    {
                        Logger.LogError("Overlay not found!");
                    }
                }
                else
                {
                    Logger.LogInfo($"Sender type: {sender?.GetType().Name}");
                }
            }
            else
            {
                Logger.LogInfo("Does not contain storage items");
            }
        }
        
        private void Tile_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Button btn)
            {
                var overlay = FindVisualChild<Border>(btn, "DragDropOverlay");
                if (overlay != null && overlay.Visibility == Visibility.Visible)
                {
                    var fadeOut = new DoubleAnimation
                    {
                        From = overlay.Opacity,
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(200))
                    };
                    var storyboard = new Storyboard();
                    Storyboard.SetTarget(fadeOut, overlay);
                    Storyboard.SetTargetProperty(fadeOut, "Opacity");
                    storyboard.Children.Add(fadeOut);
                    storyboard.Completed += (s, args) => overlay.Visibility = Visibility.Collapsed;
                    storyboard.Begin();
                }
            }
        }
        
        private void Tile_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
        }
        
        private async void Tile_Drop(object sender, DragEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not ModTile tile)
                return;
                
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
                return;

            Border? dropOverlay = null;
            FontIcon? dropIcon = null;
            TextBlock? dropText = null;
                
            try
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var imageFiles = items.OfType<StorageFile>()
                    .Where(f => f.FileType.ToLower() is ".jpg" or ".jpeg" or ".png")
                    .ToList();
                    
                if (!imageFiles.Any())
                {
                    // Hide overlay
                    var overlay = FindVisualChild<Border>(btn, "DragDropOverlay");
                    if (overlay != null)
                    {
                        overlay.Visibility = Visibility.Collapsed;
                        overlay.Opacity = 0;
                    }
                    return;
                }
                
                Logger.LogInfo($"Drop received: {imageFiles.Count} image files");
                
                // Handle category drop differently
                if (tile.IsCategory)
                {
                    await HandleCategoryImageDrop(btn, tile, imageFiles);
                    return;
                }
                
                // Get UI elements
                dropOverlay = FindVisualChild<Border>(btn, "DragDropOverlay");
                dropIcon = dropOverlay != null ? FindVisualChild<FontIcon>(dropOverlay, "DropIcon") : null;
                dropText = dropOverlay != null ? FindVisualChild<TextBlock>(dropOverlay, "DropText") : null;
                
                // Get mod folder path first to check existing images
                var gameTag = SettingsManager.CurrentSelectedGame;
                var gameModsPath = AppConstants.GameConfig.GetModsPath(gameTag);
                string modsPath = PathManager.GetAbsolutePath(gameModsPath);
                
                string? modFolderPath = FindModFolderPath(modsPath, tile.Directory);
                if (string.IsNullOrEmpty(modFolderPath))
                {
                    Logger.LogError($"Could not find mod folder for: {tile.Directory}");
                    if (dropOverlay != null)
                    {
                        dropOverlay.Visibility = Visibility.Collapsed;
                        dropOverlay.Opacity = 0;
                    }
                    return;
                }
                
                // Check existing preview images count
                const int MAX_IMAGES = AppConstants.MAX_PREVIEW_IMAGES;
                var existingPreviews = Directory.Exists(modFolderPath) 
                    ? Directory.GetFiles(modFolderPath, "preview*.*")
                        .Where(f => 
                        {
                            var ext = Path.GetExtension(f).ToLower();
                            return ext == ".jpg" || ext == ".jpeg" || ext == ".png";
                        })
                        .Count()
                    : 0;
                
                Logger.LogInfo($"Existing preview images: {existingPreviews}/{MAX_IMAGES}");
                
                // Load language dictionary once for all feedback messages
                var lang = SharedUtilities.LoadLanguageDictionary();
                
                // Check if limit reached
                if (existingPreviews >= MAX_IMAGES)
                {
                    Logger.LogInfo("Image limit reached, showing error feedback");
                    if (dropText != null)
                    {
                        dropText.Text = string.Format(SharedUtilities.GetTranslation(lang, "DragDrop_LimitReached"), AppConstants.MAX_PREVIEW_IMAGES);
                    }
                    if (dropIcon != null)
                    {
                        dropIcon.Glyph = "\uE711"; // Cross/Cancel icon
                        dropIcon.FontSize = 48; // Same size as checkmark
                    }
                    
                    await Task.Delay(2000);
                    
                    // Fade out overlay
                    if (dropOverlay != null)
                    {
                        var fadeOut = new DoubleAnimation
                        {
                            From = 1,
                            To = 0,
                            Duration = new Duration(TimeSpan.FromMilliseconds(300))
                        };
                        var storyboard = new Storyboard();
                        Storyboard.SetTarget(fadeOut, dropOverlay);
                        Storyboard.SetTargetProperty(fadeOut, "Opacity");
                        storyboard.Children.Add(fadeOut);
                        storyboard.Completed += (s, args) =>
                        {
                            dropOverlay.Visibility = Visibility.Collapsed;
                            dropOverlay.Opacity = 0;
                            if (dropText != null) dropText.Text = "Drop images here";
                            if (dropIcon != null)
                            {
                                dropIcon.Glyph = "\uE896";
                                dropIcon.FontSize = 48;
                            }
                        };
                        storyboard.Begin();
                    }
                    return;
                }
                
                // Calculate how many images we can add
                int availableSlots = MAX_IMAGES - existingPreviews;
                int filesToCopy = Math.Min(imageFiles.Count, availableSlots);
                int skippedCount = imageFiles.Count - filesToCopy;
                
                Logger.LogInfo($"Available slots: {availableSlots}, Files to copy: {filesToCopy}, Skipped: {skippedCount}");
                
                // Show processing state
                if (dropText != null)
                {
                    dropText.Text = string.Format(SharedUtilities.GetTranslation(lang, "DragDrop_Copying"), filesToCopy);
                    Logger.LogInfo("Updated drop text to copying");
                }
                if (dropIcon != null)
                {
                    dropIcon.Glyph = "\uE895"; // Sync icon
                }
                
                // modFolderPath already retrieved above
                Logger.LogInfo($"Target folder: {modFolderPath}");
                
                // Copy images with preview naming (only up to available slots)
                int copiedCount = 0;
                Logger.LogInfo($"Starting copy of {filesToCopy} files (out of {imageFiles.Count})");
                for (int i = 0; i < filesToCopy; i++)
                {
                    var imageFile = imageFiles[i];
                    try
                    {
                        Logger.LogInfo($"Processing file: {imageFile.Name} (Path: {imageFile.Path})");
                        
                        // Generate preview name: preview001.jpg, preview002.jpg, etc.
                        var previewName = $"preview{copiedCount + 1:D3}{imageFile.FileType}";
                        var targetPath = Path.Combine(modFolderPath, previewName);
                        Logger.LogInfo($"Target path: {targetPath} (from original: {imageFile.Name})");
                        
                        // Check if source file exists
                        if (!File.Exists(imageFile.Path))
                        {
                            Logger.LogError($"Source file does not exist: {imageFile.Path}");
                            continue;
                        }
                        
                        // Use simple file copy with preview naming
                        Logger.LogInfo("Using File.Copy with preview naming");
                        File.Copy(imageFile.Path, targetPath, true);
                        
                        copiedCount++;
                        Logger.LogInfo($"Successfully copied {imageFile.Name} as {previewName}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to copy {imageFile?.Name ?? "unknown"}", ex);
                    }
                }
                
                Logger.LogInfo($"Copy complete. Copied {copiedCount}/{imageFiles.Count} files with preview naming");
                
                // Optimize in background using DragDropModMode setting
                var context = Services.ImageOptimizationService.GetOptimizationContext(
                    Services.OptimizationTrigger.DragDropMod);
                Logger.LogInfo($"Starting optimization in background (Mode: {context.Mode})");
                await Task.Run(() =>
                {
                    try
                    {
                        Logger.LogInfo($"Calling ProcessModPreviewImages for: {modFolderPath}");
                        Services.ImageOptimizationService.ProcessModPreviewImages(modFolderPath, context);
                        Logger.LogInfo("Optimization complete");
                        
                        // Log what files exist after optimization
                        try
                        {
                            var files = Directory.GetFiles(modFolderPath)
                                .Select(f => Path.GetFileName(f))
                                .Where(f => f.StartsWith("preview", StringComparison.OrdinalIgnoreCase) || f.Equals("minitile.jpg", StringComparison.OrdinalIgnoreCase))
                                .ToList();
                            Logger.LogInfo($"Files after optimization: {string.Join(", ", files)}");
                        }
                        catch (Exception filesEx)
                        {
                            Logger.LogError("Failed to list files after optimization", filesEx);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Optimization failed", ex);
                    }
                });
                Logger.LogInfo("Background task started");
                
                // Show success with skip info if applicable
                if (dropText != null)
                {
                    if (skippedCount > 0)
                    {
                        dropText.Text = string.Format(SharedUtilities.GetTranslation(lang, "DragDrop_AddedSkipped"), copiedCount, skippedCount);
                        Logger.LogInfo($"Updated drop text to partial success: {copiedCount} added, {skippedCount} skipped");
                    }
                    else
                    {
                        dropText.Text = SharedUtilities.GetTranslation(lang, "DragDrop_ImagesAdded");
                        Logger.LogInfo("Updated drop text to success");
                    }
                }
                if (dropIcon != null)
                {
                    dropIcon.Glyph = "\uE73E"; // Checkmark
                    dropIcon.FontSize = 48; // Same size as cross
                }
                
                await Task.Delay(1500);
                
                // Fade out overlay
                if (dropOverlay != null)
                {
                    var fadeOut = new DoubleAnimation
                    {
                        From = 1,
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(300))
                    };
                    var storyboard = new Storyboard();
                    Storyboard.SetTarget(fadeOut, dropOverlay);
                    Storyboard.SetTargetProperty(fadeOut, "Opacity");
                    storyboard.Children.Add(fadeOut);
                    storyboard.Completed += (s, args) =>
                    {
                        dropOverlay.Visibility = Visibility.Collapsed;
                        dropOverlay.Opacity = 0;
                        if (dropText != null)
                        {
                            var resetLang = SharedUtilities.LoadLanguageDictionary();
                            dropText.Text = SharedUtilities.GetTranslation(resetLang, "DragDrop_DropImagesHere");
                        }
                        if (dropIcon != null) dropIcon.Glyph = "\uE896";
                    };
                    storyboard.Begin();
                    Logger.LogInfo("Started fade out animation");
                }
                
                // Refresh images
                LoadVisibleImages();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in drag & drop handler", ex);
                if (dropOverlay != null)
                {
                    dropOverlay.Visibility = Visibility.Collapsed;
                    dropOverlay.Opacity = 0;
                }
            }
        }
        
        private T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;
            
            try
            {
                int childCount = VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < childCount; i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    
                    if (child is T typedChild && typedChild.Name == name)
                    {
                        return typedChild;
                    }
                    
                    var result = FindVisualChild<T>(child, name);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"FindVisualChild error for {name}", ex);
            }
            
            return null;
        }


    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool>? _canExecute;
        public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T)parameter!) ?? true;
        public void Execute(object? parameter) => _execute((T)parameter!);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
