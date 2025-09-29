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
using System.IO.Compression;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.UI.Xaml.Data;
using Windows.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using Microsoft.UI.Xaml.Media.Animation;

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

        public class ModTile : INotifyPropertyChanged
        {
            private string _name = "";
            public string Name 
            { 
                get => _name; 
                set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } 
            }
            
            private string _directory = "";
            public string Directory 
            { 
                get => _directory; 
                set { if (_directory != value) { _directory = value; OnPropertyChanged(nameof(Directory)); } } 
            }
            
            public string ImagePath { get; set; } = "";
            public bool IsCategory { get; set; } = false; // New property to distinguish categories from mods
            public string Category { get; set; } = "";
            public string Author { get; set; } = "";
            public string Url { get; set; } = "";
            public DateTime LastChecked { get; set; } = DateTime.MinValue;
            public DateTime LastUpdated { get; set; } = DateTime.MinValue;
            
            public string LastCheckedFormatted => LastChecked == DateTime.MinValue ? "Never" : LastChecked.ToShortDateString();
            public string LastUpdatedFormatted => LastUpdated == DateTime.MinValue ? "Never" : LastUpdated.ToShortDateString();
            private BitmapImage? _imageSource;
            public BitmapImage? ImageSource
            {
                get => _imageSource;
                set { if (_imageSource != value) { _imageSource = value; OnPropertyChanged(nameof(ImageSource)); } }
            }
            private bool _isActive;
            public bool IsActive
            {
                get => _isActive;
                set { if (_isActive != value) { _isActive = value; OnPropertyChanged(nameof(IsActive)); } }
            }
            private bool _isHovered;
            public bool IsHovered
            {
                get => _isHovered;
                set { if (_isHovered != value) { _isHovered = value; OnPropertyChanged(nameof(IsHovered)); } }
            }
            private bool _isFolderHovered;
            public bool IsFolderHovered
            {
                get => _isFolderHovered;
                set { if (_isFolderHovered != value) { _isFolderHovered = value; OnPropertyChanged(nameof(IsFolderHovered)); } }
            }
            private bool _isDeleteHovered;
            public bool IsDeleteHovered
            {
                get => _isDeleteHovered;
                set { if (_isDeleteHovered != value) { _isDeleteHovered = value; OnPropertyChanged(nameof(IsDeleteHovered)); } }
            }
            private bool _isVisible = true;
            public bool IsVisible
            {
                get => _isVisible;
                set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); } }
            }
            private bool _isBeingDeleted = false;
            public bool IsBeingDeleted
            {
                get => _isBeingDeleted;
                set { if (_isBeingDeleted != value) { _isBeingDeleted = value; OnPropertyChanged(nameof(IsBeingDeleted)); } }
            }
            // Removed IsInViewport - using new scroll-based lazy loading instead
            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private static string ActiveModsStatePath 
        {
            get
            {
                return PathManager.GetActiveModsPath();
            }
        }
        private static string SymlinkStatePath => Path.Combine(AppContext.BaseDirectory, "Settings", "SymlinkState.json");
        private Dictionary<string, bool> _activeMods = new();
        private string? _lastSymlinkTarget;
        private ObservableCollection<ModTile> _allMods = new();
        
        // Animation system for tile tilt effects
        private readonly Dictionary<Button, (double tiltX, double tiltY)> _tileTiltTargets = new();
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
                System.Diagnostics.Debug.WriteLine($"Error in RefreshContextMenuGlobally: {ex.Message}");
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
                            System.Diagnostics.Debug.WriteLine($"Failed to load image {item.ImagePath}: {ex.Message}");
                            
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
                    string modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                    if (string.IsNullOrEmpty(modLibraryPath))
                    {
                        modLibraryPath = PathManager.GetModLibraryPath();
                    }
                    
                    string? fullModDir = FindModFolderPath(modLibraryPath, modTile.Directory);
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
            System.Diagnostics.Debug.WriteLine($"LoadCategoryInCategoryMode called with category: {category}");
            // CATEGORY MODE ONLY: Load specific category mods in category mode
            _currentViewMode = ViewMode.Categories; // Force category mode
            _currentCategory = category;
            CategoryTitle.Text = category;
            LoadModsByCategory(category);
            CategoryBackButton.Visibility = Visibility.Visible;
            CategoryOpenFolderButton.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine($"CategoryOpenFolderButton visibility set to Visible for category: {category}");
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
                
                Logger.LogInfo("Loading symlink state");
                LoadSymlinkState();
                
                Logger.LogInfo("Ensuring mod.json files exist in ModLibrary");
                (App.Current as FlairX_Mod_Manager.App)?.EnsureModJsonInModLibrary();
                
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

                var modLibraryPath = SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");

                var categoryPath = Path.Combine(modLibraryPath, _currentCategory);
                
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
                System.Diagnostics.Debug.WriteLine($"Failed to load image {imagePath}: {ex.Message}");
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
            
            // Check mod directories and create mod.json in level 1 directories - CRITICAL for mod integrity
            (App.Current as FlairX_Mod_Manager.App)?.EnsureModJsonInModLibrary();
            
            // LoadActiveMods() removed - this was likely causing the grid reload during language changes
            // Active mod state doesn't change when switching languages, no need to reload from disk
            
            // LoadAllMods() removed - unnecessary when just changing language, mods are already loaded
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





        private void CalculateTileTiltTarget(Button btn, PointerRoutedEventArgs e)
        {
            try
            {
                // Get the pointer position relative to the button
                var position = e.GetCurrentPoint(btn);
                var buttonWidth = btn.ActualWidth;
                var buttonHeight = btn.ActualHeight;
                
                if (buttonWidth > 0 && buttonHeight > 0)
                {
                    // Calculate tilt angles based on pointer position
                    var centerX = buttonWidth / 2;
                    var centerY = buttonHeight / 2;
                    var offsetX = (position.Position.X - centerX) / centerX; // -1 to 1
                    var offsetY = (position.Position.Y - centerY) / centerY; // -1 to 1
                    
                    // Tile tilt (max 8 degrees - nice visible effect)
                    var maxTilt = 8.0;
                    var targetTiltX = offsetY * maxTilt; // Y offset affects X rotation
                    var targetTiltY = -offsetX * maxTilt; // X offset affects Y rotation (inverted)
                    
                    _tileTiltTargets[btn] = (targetTiltX, targetTiltY);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CalculateTileTiltTarget error: {ex.Message}");
            }
        }

        private void UpdateTileTiltSmooth(Button btn)
        {
            try
            {
                var tileBorder = FindTileBorder(btn);
                if (tileBorder == null) return;
                
                var projection = GetOrCreateTileProjection(tileBorder);
                if (projection == null) return;
                
                // Use smooth interpolation instead of instant change
                var currentTiltX = projection.RotationX;
                var currentTiltY = projection.RotationY;
                
                var (targetTiltX, targetTiltY) = _tileTiltTargets.GetValueOrDefault(btn, (0, 0));
                
                // Interpolation factor (0.2 = 20% towards target each frame)
                var lerpFactor = 0.2;
                var newTiltX = currentTiltX + ((targetTiltX - currentTiltX) * lerpFactor);
                var newTiltY = currentTiltY + ((targetTiltY - currentTiltY) * lerpFactor);
                
                // Set new values directly (no animation needed as we're interpolating)
                projection.RotationX = newTiltX;
                projection.RotationY = newTiltY;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateTileTiltSmooth error: {ex.Message}");
            }
        }

        private void AnimateTileTilt(Button btn, double tiltX, double tiltY)
        {
            try
            {
                var tileBorder = FindTileBorder(btn);
                if (tileBorder == null) return;
                
                var projection = GetOrCreateTileProjection(tileBorder);
                if (projection == null) return;
                
                // Create optimized storyboard
                var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                var easing = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase 
                { 
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut 
                };
                var duration = TimeSpan.FromMilliseconds(150);
                
                // X rotation animation
                var rotXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = tiltX,
                    Duration = duration,
                    EasingFunction = easing
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotXAnim, projection);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotXAnim, "RotationX");
                storyboard.Children.Add(rotXAnim);
                
                // Y rotation animation
                var rotYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = tiltY,
                    Duration = duration,
                    EasingFunction = easing
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotYAnim, projection);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotYAnim, "RotationY");
                storyboard.Children.Add(rotYAnim);
                
                storyboard.Begin();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AnimateTileTilt error: {ex.Message}");
            }
        }

        private void ResetTileTilt(Button btn)
        {
            try
            {
                var tileBorder = FindTileBorder(btn);
                if (tileBorder?.Projection is not Microsoft.UI.Xaml.Media.PlaneProjection projection) return;
                
                var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                var easing = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase();
                var duration = TimeSpan.FromMilliseconds(250);
                
                // X rotation reset
                var rotXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = 0,
                    Duration = duration,
                    EasingFunction = easing
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotXAnim, projection);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotXAnim, "RotationX");
                storyboard.Children.Add(rotXAnim);
                
                // Y rotation reset
                var rotYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = 0,
                    Duration = duration,
                    EasingFunction = easing
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotYAnim, projection);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotYAnim, "RotationY");
                storyboard.Children.Add(rotYAnim);
                
                storyboard.Begin();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResetTileTilt error: {ex.Message}");
            }
        }

        private Microsoft.UI.Xaml.Media.PlaneProjection? GetOrCreateTileProjection(Border tileBorder)
        {
            if (tileBorder.Projection is not Microsoft.UI.Xaml.Media.PlaneProjection projection)
            {
                projection = new Microsoft.UI.Xaml.Media.PlaneProjection
                {
                    CenterOfRotationX = 0.5,
                    CenterOfRotationY = 0.5
                };
                tileBorder.Projection = projection;
            }
            
            return projection;
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