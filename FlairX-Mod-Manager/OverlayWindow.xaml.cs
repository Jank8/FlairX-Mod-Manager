using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using WinRT;
using WinRT.Interop;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Overlay category item for display
    /// </summary>
    public class OverlayCategoryItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string Directory { get; set; } = "";
        public BitmapImage? Thumbnail { get; set; }
        
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                    OnPropertyChanged(nameof(SelectionBackground));
                    OnPropertyChanged(nameof(SelectionBorder));
                }
            }
        }

        public SolidColorBrush SelectionBackground => IsSelected 
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 120, 212))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        public SolidColorBrush SelectionBorder => IsSelected 
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Overlay mod item for display
    /// </summary>
    public class OverlayModItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string Directory { get; set; } = "";
        public BitmapImage? Thumbnail { get; set; }
        
        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged(nameof(IsActive));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        public SolidColorBrush StatusColor => IsActive 
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 76, 175, 80))  // Green
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 96, 96, 96));  // Gray

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Transparent overlay window for quick mod toggling
    /// </summary>
    public sealed partial class OverlayWindow : Window
    {
        public ObservableCollection<OverlayCategoryItem> OverlayCategories { get; } = new();
        public ObservableCollection<OverlayModItem> OverlayMods { get; } = new();
        
        private string? _selectedCategoryPath;
        private int _selectedModIndex = -1;
        
        private AppWindow? _appWindow;
        private MainWindow? _mainWindow;
        
        // Backdrop controllers (same as MainWindow)
        private DesktopAcrylicController? _acrylicController;
        private MicaController? _micaController;
        private SystemBackdropConfiguration? _configurationSource;
        
        // Gamepad support
        private GamepadManager? _gamepadManager;

        // Event for mod toggle requests
        public event Action<string>? ModToggleRequested;
        
        // Event for window closed notification
        public event EventHandler? WindowClosed;

        public OverlayWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            
            SetupWindow();
            ApplyWindowStyle();
            LoadCurrentCategoryMods();
            UpdateHotkeyHint();
            
            // Subscribe to settings changes
            WindowStyleHelper.SettingsChanged += OnSettingsChanged;
            
            // Handle window closing to clean up resources
            this.Closed += OverlayWindow_Closed;
            
            // Initialize gamepad if enabled
            InitializeGamepad();
        }

        private void OverlayWindow_Closed(object sender, WindowEventArgs args)
        {
            // Unsubscribe from events
            WindowStyleHelper.SettingsChanged -= OnSettingsChanged;
            
            // Unsubscribe from window changes
            if (_appWindow != null)
            {
                _appWindow.Changed -= OnAppWindowChanged;
            }
            
            // Save final window state
            SaveWindowState();
            
            // Clean up gamepad
            if (_gamepadManager != null)
            {
                _gamepadManager.ButtonPressed -= OnGamepadButtonPressed;
                _gamepadManager.Dispose();
                _gamepadManager = null;
            }
            
            // Clean up backdrop controllers
            try
            {
                _acrylicController?.Dispose();
                _acrylicController = null;
                _micaController?.Dispose();
                _micaController = null;
                _configurationSource = null;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error cleaning up overlay window resources", ex);
            }
            
            // Notify that window was closed
            WindowClosed?.Invoke(this, EventArgs.Empty);
        }

        private void SetupWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow != null)
            {
                // Restore saved size or use defaults
                var settings = SettingsManager.Current;
                var width = settings.OverlayWidth > 0 ? settings.OverlayWidth : 300;
                var height = settings.OverlayHeight > 0 ? settings.OverlayHeight : 450;
                _appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
                
                // Extend title bar into content
                _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                
                // Set drag region for custom title bar
                SetTitleBar(AppTitleBar);
                
                // Configure presenter - always on top for overlay
                if (_appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = true;
                    presenter.IsResizable = true;
                    presenter.IsMinimizable = false;
                    presenter.IsMaximizable = false;
                }

                // Restore saved position or use default (top-right corner)
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var workArea = displayArea.WorkArea;
                    
                    if (settings.OverlayX >= 0 && settings.OverlayY >= 0)
                    {
                        // Use saved position
                        _appWindow.Move(new Windows.Graphics.PointInt32(settings.OverlayX, settings.OverlayY));
                    }
                    else
                    {
                        // Default to top-right corner
                        _appWindow.Move(new Windows.Graphics.PointInt32(
                            workArea.Width - width - 20,
                            100
                        ));
                    }
                }

                // Set icon
                WindowStyleHelper.SetWindowIcon(this);
                
                // Subscribe to window changes to save state
                _appWindow.Changed += OnAppWindowChanged;
            }
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidSizeChange || args.DidPositionChange)
            {
                SaveWindowState();
            }
        }

        private void SaveWindowState()
        {
            if (_appWindow == null) return;
            
            try
            {
                var settings = SettingsManager.Current;
                settings.OverlayWidth = _appWindow.Size.Width;
                settings.OverlayHeight = _appWindow.Size.Height;
                settings.OverlayX = _appWindow.Position.X;
                settings.OverlayY = _appWindow.Position.Y;
                SettingsManager.Save();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save overlay window state", ex);
            }
        }

        private void ApplyWindowStyle()
        {
            // Apply theme and backdrop using WindowStyleHelper pattern
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow == null) return;

            // Apply theme from overlay settings (or main settings)
            var theme = SettingsManager.Current.OverlayTheme ?? SettingsManager.Current.Theme ?? "Auto";
            if (Content is FrameworkElement root)
            {
                if (theme == "Light")
                {
                    root.RequestedTheme = ElementTheme.Light;
                    appWindow.TitleBar.ButtonForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonHoverForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonPressedForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(100, 230, 230, 230);
                    appWindow.TitleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(150, 210, 210, 210);
                }
                else if (theme == "Dark")
                {
                    root.RequestedTheme = ElementTheme.Dark;
                    appWindow.TitleBar.ButtonForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(255, 50, 50, 50);
                    appWindow.TitleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(255, 30, 30, 30);
                }
                else
                {
                    root.RequestedTheme = ElementTheme.Default;
                    appWindow.TitleBar.ButtonForegroundColor = null;
                    appWindow.TitleBar.ButtonHoverForegroundColor = null;
                    appWindow.TitleBar.ButtonPressedForegroundColor = null;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = null;
                    appWindow.TitleBar.ButtonPressedBackgroundColor = null;
                }
            }

            // Apply backdrop from overlay settings (or main settings)
            var backdrop = SettingsManager.Current.OverlayBackdrop ?? SettingsManager.Current.BackdropEffect ?? "AcrylicThin";
            ApplyBackdrop(backdrop);
        }

        public void ApplyBackdrop(string backdropType)
        {
            // Clean up existing controllers
            _acrylicController?.Dispose();
            _acrylicController = null;
            _micaController?.Dispose();
            _micaController = null;

            try
            {
                // Clear background for backdrop effects (except None)
                if (Content is Panel panel && backdropType != "None")
                {
                    panel.Background = null;
                }

                _configurationSource = new SystemBackdropConfiguration();
                _configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                switch (backdropType)
                {
                    case "Mica":
                        if (MicaController.IsSupported())
                        {
                            _micaController = new MicaController { Kind = MicaKind.Base };
                            _micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                            _micaController.SetSystemBackdropConfiguration(_configurationSource);
                            Logger.LogInfo("Mica backdrop applied to overlay window");
                        }
                        break;
                    case "MicaAlt":
                        if (MicaController.IsSupported())
                        {
                            _micaController = new MicaController { Kind = MicaKind.BaseAlt };
                            _micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                            _micaController.SetSystemBackdropConfiguration(_configurationSource);
                            Logger.LogInfo("MicaAlt backdrop applied to overlay window");
                        }
                        break;
                    case "Acrylic":
                        if (DesktopAcrylicController.IsSupported())
                        {
                            _acrylicController = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Base };
                            _acrylicController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                            _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
                            Logger.LogInfo("Acrylic backdrop applied to overlay window");
                        }
                        break;
                    case "AcrylicThin":
                        if (DesktopAcrylicController.IsSupported())
                        {
                            _acrylicController = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Thin };
                            _acrylicController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                            _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
                            Logger.LogInfo("AcrylicThin backdrop applied to overlay window");
                        }
                        break;
                    case "None":
                    default:
                        // Set solid background based on theme
                        if (Content is Panel rootPanel)
                        {
                            var theme = rootPanel.ActualTheme;
                            if (theme == ElementTheme.Light)
                            {
                                rootPanel.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 243, 243, 243));
                            }
                            else if (theme == ElementTheme.Dark)
                            {
                                rootPanel.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 32, 32, 32));
                            }
                            else
                            {
                                var systemTheme = Application.Current.RequestedTheme;
                                rootPanel.Background = systemTheme == ApplicationTheme.Light
                                    ? new SolidColorBrush(ColorHelper.FromArgb(255, 243, 243, 243))
                                    : new SolidColorBrush(ColorHelper.FromArgb(255, 32, 32, 32));
                            }
                        }
                        Logger.LogInfo("No backdrop applied to overlay window");
                        break;
                }

                // Subscribe to theme changes
                if (Content is FrameworkElement rootElement)
                {
                    rootElement.ActualThemeChanged += (s, e) => SetConfigurationSourceTheme();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to setup {backdropType} backdrop", ex);
            }
        }

        private void SetConfigurationSourceTheme()
        {
            if (_configurationSource == null) return;
            
            var theme = SettingsManager.Current.OverlayTheme ?? SettingsManager.Current.Theme ?? "Auto";
            _configurationSource.Theme = theme switch
            {
                "Light" => SystemBackdropTheme.Light,
                "Dark" => SystemBackdropTheme.Dark,
                _ => SystemBackdropTheme.Default
            };
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            // Re-apply window style when settings change
            DispatcherQueue.TryEnqueue(() => ApplyWindowStyle());
        }

        private void UpdateHotkeyHint()
        {
            var hotkey = SettingsManager.Current.ToggleOverlayHotkey ?? "Alt+W";
            var gamepadCombo = SettingsManager.Current.GamepadToggleOverlayCombo ?? "Back+Start";
            var gamepadHint = SettingsManager.Current.GamepadEnabled ? $" | {gamepadCombo}" : "";
            if (HotkeyHintText != null)
            {
                HotkeyHintText.Text = $"{hotkey}{gamepadHint} to hide";
            }
        }

        #region Gamepad Support

        private void InitializeGamepad()
        {
            if (!SettingsManager.Current.GamepadEnabled)
            {
                Logger.LogInfo("Gamepad support disabled in settings");
                return;
            }

            try
            {
                _gamepadManager = new GamepadManager();
                _gamepadManager.ButtonPressed += OnGamepadButtonPressed;
                _gamepadManager.ControllerConnected += (s, e) =>
                {
                    Logger.LogInfo("Gamepad connected - overlay navigation enabled");
                    DispatcherQueue.TryEnqueue(() => _gamepadManager?.Vibrate(20000, 20000, 100));
                };
                _gamepadManager.ControllerDisconnected += (s, e) =>
                {
                    Logger.LogInfo("Gamepad disconnected");
                };
                _gamepadManager.StartPolling();
                Logger.LogInfo("Gamepad manager initialized for overlay");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize gamepad for overlay", ex);
            }
        }

        private void OnGamepadButtonPressed(object? sender, GamepadButtonEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var settings = SettingsManager.Current;
                    var buttonName = e.GetButtonDisplayName();

                    // Navigate up
                    if (IsButtonMatch(buttonName, settings.GamepadNavigateUpButton))
                    {
                        NavigateModSelection(-1);
                    }
                    // Navigate down
                    else if (IsButtonMatch(buttonName, settings.GamepadNavigateDownButton))
                    {
                        NavigateModSelection(1);
                    }
                    // Select/Toggle mod
                    else if (IsButtonMatch(buttonName, settings.GamepadSelectButton))
                    {
                        ToggleSelectedMod();
                    }
                    // Next category
                    else if (IsButtonMatch(buttonName, settings.GamepadNextCategoryButton))
                    {
                        NavigateCategory(1);
                    }
                    // Previous category
                    else if (IsButtonMatch(buttonName, settings.GamepadPrevCategoryButton))
                    {
                        NavigateCategory(-1);
                    }
                    // Back/Close
                    else if (IsButtonMatch(buttonName, settings.GamepadBackButton))
                    {
                        Hide();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error handling gamepad input in overlay", ex);
                }
            });
        }

        private bool IsButtonMatch(string pressedButton, string configuredButton)
        {
            return string.Equals(pressedButton, configuredButton, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(pressedButton, GamepadManager.GetButtonName(
                       Enum.TryParse<GamepadManager.GamepadButtons>(configuredButton, true, out var btn) ? btn : GamepadManager.GamepadButtons.None),
                       StringComparison.OrdinalIgnoreCase);
        }

        private void NavigateModSelection(int direction)
        {
            if (OverlayMods.Count == 0) return;

            _selectedModIndex += direction;
            
            // Wrap around
            if (_selectedModIndex < 0)
                _selectedModIndex = OverlayMods.Count - 1;
            else if (_selectedModIndex >= OverlayMods.Count)
                _selectedModIndex = 0;

            // Visual feedback - ItemsRepeater doesn't have selection, so we just track index
            // The actual selection is handled by ToggleSelectedMod
            Logger.LogInfo($"Gamepad navigation: mod index {_selectedModIndex}");
        }

        private void ToggleSelectedMod()
        {
            if (_selectedModIndex >= 0 && _selectedModIndex < OverlayMods.Count)
            {
                var mod = OverlayMods[_selectedModIndex];
                ToggleMod(mod);
                
                // Vibrate on toggle
                _gamepadManager?.Vibrate(30000, 30000, 80);
            }
        }

        private void NavigateCategory(int direction)
        {
            if (OverlayCategories.Count == 0) return;

            var currentIndex = OverlayCategories.ToList().FindIndex(c => c.IsSelected);
            var newIndex = currentIndex + direction;

            // Wrap around
            if (newIndex < 0)
                newIndex = OverlayCategories.Count - 1;
            else if (newIndex >= OverlayCategories.Count)
                newIndex = 0;

            SelectCategory(OverlayCategories[newIndex]);
            _selectedModIndex = -1; // Reset mod selection
            
            // Vibrate on category change
            _gamepadManager?.Vibrate(15000, 15000, 50);
        }

        #endregion


        /// <summary>
        /// Load categories and mods
        /// </summary>
        public void LoadCurrentCategoryMods()
        {
            LoadCategories();
            
            // Load mods from first category if none selected
            if (_selectedCategoryPath == null && OverlayCategories.Count > 0)
            {
                SelectCategory(OverlayCategories[0]);
            }
            else if (_selectedCategoryPath != null)
            {
                LoadModsFromCategory(_selectedCategoryPath);
            }
        }

        private void LoadCategories()
        {
            OverlayCategories.Clear();

            try
            {
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !System.IO.Directory.Exists(modsPath))
                    return;

                var categories = System.IO.Directory.GetDirectories(modsPath);
                
                foreach (var categoryDir in categories)
                {
                    var categoryName = Path.GetFileName(categoryDir);
                    if (categoryName.Equals("Other", StringComparison.OrdinalIgnoreCase))
                        continue; // Skip Other category
                    
                    var item = new OverlayCategoryItem
                    {
                        Name = categoryName,
                        Directory = categoryDir,
                        Thumbnail = LoadCategoryThumbnail(categoryDir),
                        IsSelected = categoryDir == _selectedCategoryPath
                    };
                    
                    OverlayCategories.Add(item);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load overlay categories", ex);
            }
        }

        private void LoadModsFromCategory(string categoryPath)
        {
            OverlayMods.Clear();

            try
            {
                if (!System.IO.Directory.Exists(categoryPath))
                    return;

                var modDirs = System.IO.Directory.GetDirectories(categoryPath);
                
                foreach (var modDir in modDirs)
                {
                    var modName = Path.GetFileName(modDir);
                    var isActive = !modName.StartsWith("DISABLED", StringComparison.OrdinalIgnoreCase);
                    
                    // Clean name for display
                    var displayName = isActive ? modName : modName.Substring(8).TrimStart('_', '-', ' ');
                    
                    var item = new OverlayModItem
                    {
                        Name = displayName,
                        Directory = modDir,
                        IsActive = isActive,
                        Thumbnail = LoadThumbnail(modDir)
                    };
                    
                    OverlayMods.Add(item);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load overlay mods", ex);
            }
        }

        private void SelectCategory(OverlayCategoryItem category)
        {
            // Deselect all
            foreach (var cat in OverlayCategories)
                cat.IsSelected = false;
            
            // Select this one
            category.IsSelected = true;
            _selectedCategoryPath = category.Directory;
            
            // Load mods
            LoadModsFromCategory(category.Directory);
        }

        private void CategoryItem_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is OverlayCategoryItem item)
            {
                SelectCategory(item);
            }
        }

        private BitmapImage? LoadCategoryThumbnail(string categoryDir)
        {
            try
            {
                // Try catmini first, then catprev, then preview
                var thumbPath = Path.Combine(categoryDir, "catmini.jpg");
                if (!File.Exists(thumbPath))
                    thumbPath = Path.Combine(categoryDir, "catprev.jpg");
                if (!File.Exists(thumbPath))
                    thumbPath = Path.Combine(categoryDir, "preview.jpg");
                
                if (File.Exists(thumbPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.UriSource = new Uri(thumbPath);
                    bitmap.DecodePixelWidth = 48;
                    return bitmap;
                }
            }
            catch { }
            
            return null;
        }

        private BitmapImage? LoadThumbnail(string modDir)
        {
            try
            {
                // Try minitile first, then preview
                var thumbPath = Path.Combine(modDir, "minitile.jpg");
                if (!File.Exists(thumbPath))
                    thumbPath = Path.Combine(modDir, "preview.jpg");
                
                if (File.Exists(thumbPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.UriSource = new Uri(thumbPath);
                    bitmap.DecodePixelWidth = 48;
                    return bitmap;
                }
            }
            catch { }
            
            return null;
        }

        private void ModItem_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is OverlayModItem item)
            {
                // Toggle mod
                ToggleMod(item);
            }
        }

        private void ToggleMod(OverlayModItem item)
        {
            try
            {
                var dir = item.Directory;
                var parentDir = Path.GetDirectoryName(dir);
                var currentName = Path.GetFileName(dir);
                
                string newName;
                bool newState;
                
                if (item.IsActive)
                {
                    // Disable: add DISABLED prefix
                    newName = "DISABLED_" + currentName;
                    newState = false;
                }
                else
                {
                    // Enable: remove DISABLED prefix
                    newName = currentName.StartsWith("DISABLED_") 
                        ? currentName.Substring(9) 
                        : currentName.TrimStart('D', 'I', 'S', 'A', 'B', 'L', 'E', '_');
                    newState = true;
                }
                
                var newPath = Path.Combine(parentDir!, newName);
                
                if (dir != newPath && !System.IO.Directory.Exists(newPath))
                {
                    System.IO.Directory.Move(dir, newPath);
                    item.Directory = newPath;
                    item.IsActive = newState;
                    
                    Logger.LogInfo($"Overlay toggled mod: {currentName} -> {newName}");
                    
                    // Notify main window
                    ModToggleRequested?.Invoke(newPath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to toggle mod from overlay: {item.Name}", ex);
            }
        }

        public void Show()
        {
            _appWindow?.Show();
            LoadCurrentCategoryMods(); // Refresh on show
            UpdateHotkeyHint();
        }

        public void Hide()
        {
            _appWindow?.Hide();
        }

        public void Toggle()
        {
            if (_appWindow?.IsVisible == true)
                Hide();
            else
                Show();
        }
    }
}
