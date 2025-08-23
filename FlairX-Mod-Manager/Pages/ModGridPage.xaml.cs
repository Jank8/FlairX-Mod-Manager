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
using System.Runtime.InteropServices.WindowsRuntime;
using System.IO.Compression;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModGridPage : Page
    {
        public enum ViewMode
        {
            Mods,
            Categories
        }

        public enum SortMode
        {
            None,
            NameAZ,
            NameZA,
            CategoryAZ,
            CategoryZA,
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
            public string Name { get; set; } = "";
            public string ImagePath { get; set; } = "";
            public string Directory { get; set; } = ""; // Store only the directory name
            public bool IsCategory { get; set; } = false; // New property to distinguish categories from mods
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
        private string? _currentCategory; // Track current category for back navigation
        private string? _previousCategory; // Track category before search for restoration
        private bool _isSearchActive = false; // Track if we're currently in search mode
        private bool _wasInCategoryMode = false; // Track if we were in category mode before navigation
        private ViewMode _previousViewMode = ViewMode.Mods; // Track view mode before navigation
        

        
        // Virtualized loading - store all mod data but only create visible ModTiles
        private List<ModData> _allModData = new();
        
        // Thread-safe JSON Caching System
        private static readonly Dictionary<string, ModData> _modJsonCache = new();
        private static readonly Dictionary<string, DateTime> _modFileTimestamps = new();
        private static readonly object _cacheLock = new object();
        


        private void OnViewModeChanged()
        {
            if (CurrentViewMode == ViewMode.Categories)
            {
                LoadCategories();
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
            
            // Update context menu visibility
            UpdateContextMenuVisibility();
        }

        public void LoadAllCategories()
        {
            // Force load all categories regardless of current view mode
            LoadCategories();
            
            // Hide back button
            CategoryBackButton.Visibility = Visibility.Collapsed;
            
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
            
            // Update context menu visibility
            UpdateContextMenuVisibility();
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
            
            // Update context menu visibility
            UpdateContextMenuVisibility();
        }

        public ModGridPage()
        {
            this.InitializeComponent();
            LoadActiveMods();
            LoadSymlinkState();
            (App.Current as FlairX_Mod_Manager.App)?.EnsureModJsonInModLibrary();
            this.Loaded += ModGridPage_Loaded;
            
            // Initialize context menu translations
            UpdateContextMenuTranslations();
            
            // Use AddHandler with handledEventsToo to catch mouse back button even if handled by child elements
            this.AddHandler(PointerPressedEvent, new PointerEventHandler(ModGridPage_PointerPressed), handledEventsToo: true);
            
            // Load saved zoom level from settings
            _zoomFactor = FlairX_Mod_Manager.SettingsManager.Current.ZoomLevel;
            
            // Handle container generation to apply scaling to new items
            ModsGrid.ContainerContentChanging += ModsGrid_ContainerContentChanging;
            
            StartBackgroundLoadingIfNeeded();
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
            // Two separate navigation schemes based on view mode
            
            if (CurrentViewMode == ViewMode.Categories)
            {
                // CATEGORY MODE: Go back to all categories
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
            
            // Update context menu visibility
            UpdateContextMenuVisibility();
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
            // Odświeżenie listy kategorii modów w menu nawigacji
            var mainWindow = ((App)Application.Current).MainWindow as FlairX_Mod_Manager.MainWindow;
            if (mainWindow != null)
            {
                _ = mainWindow.GenerateModCharacterMenuAsync();
            }
            // Check mod directories and create mod.json in level 1 directories
            (App.Current as FlairX_Mod_Manager.App)?.EnsureModJsonInModLibrary();
            LoadAllMods();
        }

        // Add function to display path with single slashes moved to ModGridPage.StaticUtilities.cs

        // GetModUrl method moved to ModGridPage.ContextMenu.cs

        // Context menu methods moved to ModGridPage.ContextMenu.cs
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