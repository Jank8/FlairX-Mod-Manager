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

    // Converter for star icon glyph (favorite categories)
    public class BoolToStarGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (bool)value ? "\uE735" : "\uE734"; // Filled star : Empty star
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    // Converter for star icon color (favorite categories)
    public class BoolToStarColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((bool)value)
            {
                // Use gold/yellow color for favorite stars
                return new SolidColorBrush(Colors.Gold);
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    
    // Converter for NSFW overlay visibility
    // DISABLED: Blur mechanism not used - mods are hidden instead
    /*
    public class NSFWOverlayVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // Show overlay if mod is NSFW AND BlurNSFWThumbnails setting is enabled
            bool isNSFW = value is bool b && b;
            bool blurEnabled = SettingsManager.Current.BlurNSFWThumbnails;
            return (isNSFW && blurEnabled) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    */



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
        private int _lastLoadedModDataIndex = 0; // Track last processed index in _allModData for incremental loading
        
        // Cached persistent lists for fast filtering (loaded once per session)
        private HashSet<string>? _cachedNSFWMods = null;
        private HashSet<string>? _cachedBrokenMods = null;
        private HashSet<string>? _cachedOutdatedMods = null;
        


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
                
                // Initialize ItemsSource with empty collection
                ModsGrid.ItemsSource = _allMods;
                
                // Initialize WrapGrid ItemWidth/Height early to ensure correct tile sizes from the start
                // Try multiple times with increasing delays to catch when ItemsPanelRoot becomes available
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () => InitializeGridItemSizes());
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => InitializeGridItemSizes());
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => InitializeGridItemSizes());
                
                Logger.LogInfo("Loading active mods state");
                LoadActiveMods();
                
                Logger.LogInfo("Ensuring mod.json files exist in ModLibrary");
                // EnsureModJsonInModLibrary removed - no longer needed
                
                this.Loaded += ModGridPage_Loaded;
                
                Logger.LogInfo("Updating context menu translations");
                UpdateContextMenuTranslations();
                
                Logger.LogInfo("Setting up pointer event handlers");
                this.AddHandler(PointerPressedEvent, new PointerEventHandler(ModGridPage_PointerPressed), handledEventsToo: true);
                
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
            public bool IsBroken { get; set; } = false;
            public bool IsFavorite { get; set; } = false;
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
                    OpenFolder(categoryPath);
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
            else if (_currentCategory == "NoPreviews")
            {
                CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_No_Previews");
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

        private void UpdateEmptyState()
        {
            if (EmptyStatePanel == null || ModsGrid == null) return;
            
            var langDict = SharedUtilities.LoadLanguageDictionary();
            bool isFilterView = _currentCategory == "Active" || _currentCategory == "Broken" || _currentCategory == "Outdated";
            bool isAllModsView = string.IsNullOrEmpty(_currentCategory);
            bool hasNoItems = ModsGrid.ItemsSource == null || 
                             (ModsGrid.ItemsSource is System.Collections.IEnumerable enumerable && 
                              !enumerable.Cast<object>().Any());
            
            // Determine if we should show empty state
            bool shouldShowEmptyState = CurrentViewMode == ViewMode.Mods && hasNoItems;
            
            if (shouldShowEmptyState)
            {
                // Different messages for filter views vs category views
                if (isFilterView || isAllModsView)
                {
                    // Filter views (Active, Broken, Outdated, All Mods) - show "No results" without button
                    if (EmptyStateText != null)
                    {
                        EmptyStateText.Text = SharedUtilities.GetTranslation(langDict, "EmptyState_NoResults_Text");
                    }
                    if (EmptyStateBrowseButton != null)
                    {
                        EmptyStateBrowseButton.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    // Category views - show "Nothing here, download mods" with GameBanana button
                    if (EmptyStateText != null)
                    {
                        EmptyStateText.Text = SharedUtilities.GetTranslation(langDict, "EmptyState_NoMods_Text");
                    }
                    if (EmptyStateBrowseButton != null)
                    {
                        EmptyStateBrowseButton.Visibility = Visibility.Visible;
                    }
                    if (EmptyStateBrowseButtonText != null)
                    {
                        EmptyStateBrowseButtonText.Text = SharedUtilities.GetTranslation(langDict, "EmptyState_Browse_Button");
                    }
                }
            }
            
            EmptyStatePanel.Visibility = shouldShowEmptyState ? Visibility.Visible : Visibility.Collapsed;
            ModsGrid.Visibility = shouldShowEmptyState ? Visibility.Collapsed : Visibility.Visible;

            // Update mod count display
            if (ModCountText != null)
            {
                int count;
                if (string.IsNullOrEmpty(_currentCategory))
                {
                    // "All Mods" view - get total count from ModListManager
                    count = ModListManager.GetAllMods().Count;
                }
                else if (_currentCategory == "Active")
                {
                    // Active mods count
                    count = ModListManager.GetAllMods().Count(m => m.IsActive);
                }
                else if (_currentCategory == "Broken")
                {
                    // Broken mods count
                    count = ModListManager.GetBrokenMods().Count;
                }
                else if (_currentCategory == "Outdated")
                {
                    // Outdated mods count
                    count = ModListManager.GetOutdatedMods().Count;
                }
                else if (_currentCategory == "NoPreviews")
                {
                    // No previews count
                    count = ModListManager.GetAllMods().Count(m => string.IsNullOrEmpty(m.ImagePath));
                }
                else if (_currentCategory == "Duplicates")
                {
                    // Duplicates count - count loaded data
                    count = _allModData.Count > 0 ? _allModData.Count : _allMods.Count;
                }
                else
                {
                    // Specific category - get count from ModListManager
                    count = ModListManager.GetModsByCategory(_currentCategory).Count;
                }
                
                ModCountText.Text = count > 0 ? $"- {count}" : "";
            }
        }
        
        private void EmptyStateBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentCategory)) return;
            
            // Get current game tag
            var gameTag = SettingsManager.CurrentSelectedGame;
            if (string.IsNullOrEmpty(gameTag)) return;
            
            // Open GameBanana browser with category name as search query
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                var browserControl = mainWindow.ShowGameBananaBrowserPanel(gameTag);
                
                // Set initial search to category name after a small delay to let the panel initialize
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // Small delay for panel to load
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        browserControl.SetInitialSearch(_currentCategory);
                    });
                });
            }
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
