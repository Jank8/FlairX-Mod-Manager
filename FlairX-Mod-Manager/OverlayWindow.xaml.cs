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
        public ObservableCollection<OverlayModItem> OverlayMods { get; } = new();
        
        private AppWindow? _appWindow;
        private bool _isAlwaysOnTop = true;
        private MainWindow? _mainWindow;
        
        // Backdrop controllers (same as MainWindow)
        private DesktopAcrylicController? _acrylicController;
        private MicaController? _micaController;
        private SystemBackdropConfiguration? _configurationSource;

        // Event for mod toggle requests
        public event Action<string>? ModToggleRequested;

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
        }

        private void SetupWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow != null)
            {
                // Set size
                _appWindow.Resize(new Windows.Graphics.SizeInt32(300, 450));
                
                // Extend title bar into content
                _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                
                // Set drag region for custom title bar
                SetTitleBar(AppTitleBar);
                
                // Configure presenter
                if (_appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = _isAlwaysOnTop;
                    presenter.IsResizable = true;
                    presenter.IsMinimizable = false;
                    presenter.IsMaximizable = false;
                }

                // Position in top-right corner
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var workArea = displayArea.WorkArea;
                    _appWindow.Move(new Windows.Graphics.PointInt32(
                        workArea.Width - 320,
                        100
                    ));
                }

                // Set icon
                WindowStyleHelper.SetWindowIcon(this);
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
            if (HotkeyHintText != null)
            {
                HotkeyHintText.Text = $"{hotkey} to hide";
            }
        }


        /// <summary>
        /// Load mods from current category in main window
        /// </summary>
        public void LoadCurrentCategoryMods()
        {
            OverlayMods.Clear();

            try
            {
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !System.IO.Directory.Exists(modsPath))
                    return;

                // Get all mod directories
                var categories = System.IO.Directory.GetDirectories(modsPath);
                
                foreach (var category in categories.Take(20)) // Limit for performance
                {
                    var modDirs = System.IO.Directory.GetDirectories(category);
                    
                    foreach (var modDir in modDirs.Take(10))
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
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load overlay mods", ex);
            }
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

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            _isAlwaysOnTop = !_isAlwaysOnTop;
            
            if (_appWindow?.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = _isAlwaysOnTop;
            }
            
            PinButton.Content = _isAlwaysOnTop ? "📌" : "📍";
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
