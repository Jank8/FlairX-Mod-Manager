using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
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
        public Visibility ThumbnailVisibility => Thumbnail != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FallbackVisibility => Thumbnail == null ? Visibility.Visible : Visibility.Collapsed;
        public string FallbackGlyph { get; set; } = "\uEA8C"; // Default character icon
        
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

        public SolidColorBrush SelectionBackground
        {
            get
            {
                if (!IsSelected) return new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
                var accentColor = (Windows.UI.Color)Application.Current.Resources["SystemAccentColor"];
                return new SolidColorBrush(Windows.UI.Color.FromArgb(40, accentColor.R, accentColor.G, accentColor.B));
            }
        }

        public SolidColorBrush SelectionBorder
        {
            get
            {
                if (!IsSelected) return new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
                var accentColor = (Windows.UI.Color)Application.Current.Resources["SystemAccentColor"];
                return new SolidColorBrush(accentColor);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string name) => 
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
                    OnPropertyChanged(nameof(ActiveVisibility));
                    OnPropertyChanged(nameof(InactiveVisibility));
                }
            }
        }

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
                    OnPropertyChanged(nameof(SelectionBorderBrush));
                }
            }
        }

        public SolidColorBrush StatusColor => IsActive 
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 76, 175, 80))  // Green
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 96, 96, 96));  // Gray

        public Visibility ActiveVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;
        public Visibility InactiveVisibility => IsActive ? Visibility.Collapsed : Visibility.Visible;
        
        public SolidColorBrush SelectionBorderBrush => IsSelected 
            ? new SolidColorBrush((Windows.UI.Color)Application.Current.Resources["SystemAccentColor"])
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));  // Transparent

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string name) => 
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
        
        // Theme change handler reference for cleanup
        private TypedEventHandler<FrameworkElement, object>? _themeChangedHandler;

        // Event for mod toggle requests
        public event Action<string>? ModToggleRequested;
        
        // Event for window closed notification
        public event EventHandler? WindowClosed;
        
        // Event for window hidden notification (when overlay is toggled off)
        public event EventHandler? WindowHidden;
        
        // Flags to prevent concurrent loading
        private bool _isLoadingMods;
        private bool _isLoadingCategories;
        
        // Filter state
        private bool _showActiveOnly;
        
        // Held buttons for combo detection
        private HashSet<string> _heldButtons = new();

        public OverlayWindow(MainWindow mainWindow)
        {
            try
            {
                Logger.LogInfo("OverlayWindow: Starting constructor");
                InitializeComponent();
                Logger.LogInfo("OverlayWindow: InitializeComponent done");
                _mainWindow = mainWindow;
                
                Logger.LogInfo("OverlayWindow: SetupWindow starting");
                SetupWindow();
                Logger.LogInfo("OverlayWindow: SetupWindow done");
                
                Logger.LogInfo("OverlayWindow: ApplyWindowStyle starting");
                ApplyWindowStyle();
                Logger.LogInfo("OverlayWindow: ApplyWindowStyle done");
                
                Logger.LogInfo("OverlayWindow: LoadCurrentCategoryMods starting");
                LoadCurrentCategoryMods();
                Logger.LogInfo("OverlayWindow: LoadCurrentCategoryMods done");
                
                Logger.LogInfo("OverlayWindow: UpdateUITexts starting");
                UpdateUITexts();
                Logger.LogInfo("OverlayWindow: UpdateUITexts done");
                
                Logger.LogInfo("OverlayWindow: UpdateHotkeyHint starting");
                UpdateHotkeyHint();
                Logger.LogInfo("OverlayWindow: UpdateHotkeyHint done");
                
                // Subscribe to settings changes
                WindowStyleHelper.SettingsChanged += OnSettingsChanged;
                
                // Handle window closing to clean up resources
                this.Closed += OverlayWindow_Closed;
                
                Logger.LogInfo("OverlayWindow: InitializeGamepad starting");
                // Initialize gamepad if enabled
                InitializeGamepad();
                Logger.LogInfo("OverlayWindow: Constructor completed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("OverlayWindow: Constructor failed", ex);
                throw;
            }
        }

        private void OverlayWindow_Closed(object sender, WindowEventArgs args)
        {
            // Unsubscribe from events
            WindowStyleHelper.SettingsChanged -= OnSettingsChanged;
            
            // Unsubscribe from theme changes
            if (_themeChangedHandler != null && Content is FrameworkElement rootElement)
            {
                rootElement.ActualThemeChanged -= _themeChangedHandler;
                _themeChangedHandler = null;
            }
            
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
                _gamepadManager.ButtonReleased -= OnGamepadButtonReleased;
                _gamepadManager.LeftThumbstickMoved -= OnLeftThumbstickMoved;
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
                var width = settings.OverlayWidth > 0 ? settings.OverlayWidth : 1200;
                var height = settings.OverlayHeight > 0 ? settings.OverlayHeight : 720;
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

                // Restore saved position or center on screen
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
                        // Default to center of screen
                        _appWindow.Move(new Windows.Graphics.PointInt32(
                            (workArea.Width - width) / 2,
                            (workArea.Height - height) / 2
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
                    _themeChangedHandler = (s, e) =>
                    {
                        SetConfigurationSourceTheme();
                        RefreshUIForThemeChange();
                    };
                    rootElement.ActualThemeChanged += _themeChangedHandler;
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

        private void RefreshUIForThemeChange()
        {
            // Force refresh of category and mod items to update colors
            foreach (var cat in OverlayCategories)
            {
                cat.OnPropertyChanged(nameof(cat.SelectionBackground));
                cat.OnPropertyChanged(nameof(cat.SelectionBorder));
            }
            
            foreach (var mod in OverlayMods)
            {
                mod.OnPropertyChanged(nameof(mod.SelectionBorderBrush));
                mod.OnPropertyChanged(nameof(mod.StatusColor));
            }
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            // Re-apply window style when settings change
            DispatcherQueue.TryEnqueue(() => ApplyWindowStyle());
        }

        private void UpdateHotkeyHint()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("Overlay");
            var settings = SettingsManager.Current;
            
            // Keyboard hotkey hint
            var hotkey = settings.ToggleOverlayHotkey ?? "Alt+W";
            var gamepadCombo = settings.GamepadToggleOverlayCombo ?? "Back+Start";
            var gamepadHideHint = settings.GamepadEnabled ? $" | {gamepadCombo}" : "";
            
            // Format: "{hotkey} | {gamepadCombo} to hide" or "{hotkey} to hide"
            var hotkeyString = $"{hotkey}{gamepadHideHint}";
            var hintTemplate = SharedUtilities.GetTranslation(lang, "HotkeyHint");
            
            if (HotkeyHintText != null)
            {
                HotkeyHintText.Text = string.Format(hintTemplate, hotkeyString);
            }
            
            if (ClickToToggleText != null)
            {
                ClickToToggleText.Text = SharedUtilities.GetTranslation(lang, "ClickToToggle");
            }
            
            // Gamepad controls hint (only shown when gamepad is enabled)
            if (settings.GamepadEnabled)
            {
                var selectBtn = settings.GamepadSelectButton ?? "A";
                var prevCatBtn = settings.GamepadPrevCategoryButton ?? "LB";
                var nextCatBtn = settings.GamepadNextCategoryButton ?? "RB";
                var backBtn = settings.GamepadBackButton ?? "B";
                
                var gamepadHintTemplate = SharedUtilities.GetTranslation(lang, "GamepadHint");
                
                if (GamepadHintText != null)
                {
                    GamepadHintText.Text = string.Format(gamepadHintTemplate, selectBtn, prevCatBtn, nextCatBtn, backBtn);
                    GamepadHintText.Visibility = Visibility.Visible;
                }
                if (GamepadSeparator != null)
                {
                    GamepadSeparator.Visibility = Visibility.Visible;
                }
            }
            else
            {
                if (GamepadHintText != null)
                {
                    GamepadHintText.Visibility = Visibility.Collapsed;
                }
                if (GamepadSeparator != null)
                {
                    GamepadSeparator.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Updates all UI texts with translations
        /// </summary>
        private void UpdateUITexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("Overlay");
            
            // Update window title
            Title = SharedUtilities.GetTranslation(lang, "WindowTitle");
            
            // Update title bar text
            if (TitleBarTextBlock != null)
            {
                TitleBarTextBlock.Text = SharedUtilities.GetTranslation(lang, "TitleBarText");
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
                _gamepadManager.ButtonReleased += OnGamepadButtonReleased;
                _gamepadManager.LeftThumbstickMoved += OnLeftThumbstickMoved;
                _gamepadManager.ControllerConnected += (s, e) =>
                {
                    Logger.LogInfo("Gamepad connected - overlay navigation enabled");
                    DispatcherQueue.TryEnqueue(() => _gamepadManager?.Vibrate(20000, 20000, 300));
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
                    // Ignore input if overlay is not visible
                    if (!IsOverlayVisible) return;
                    
                    var settings = SettingsManager.Current;
                    var buttonName = e.GetButtonDisplayName();
                    _heldButtons.Add(buttonName);

                    Logger.LogInfo($"Overlay gamepad button: {buttonName}");
                    
                    // Check for filter active combo
                    var filterCombo = settings.GamepadFilterActiveCombo ?? "Back+A";
                    var filterComboButtons = new HashSet<string>(filterCombo.Split('+'));
                    if (_heldButtons.SetEquals(filterComboButtons))
                    {
                        ToggleActiveOnlyFilter();
                        _gamepadManager?.Vibrate(0, 30000, 300);
                        _heldButtons.Clear();
                        return;
                    }
                    
                    // Navigate in grid - D-Pad for 4-direction movement (only if left stick is disabled)
                    if (!settings.GamepadUseLeftStick)
                    {
                        if (IsButtonMatch(buttonName, settings.GamepadDPadUp))
                        {
                            Logger.LogInfo("Navigate UP");
                            NavigateModGrid(0, -1);
                            return;
                        }
                        else if (IsButtonMatch(buttonName, settings.GamepadDPadDown))
                        {
                            Logger.LogInfo("Navigate DOWN");
                            NavigateModGrid(0, 1);
                            return;
                        }
                        else if (IsButtonMatch(buttonName, settings.GamepadDPadLeft))
                        {
                            Logger.LogInfo("Navigate LEFT");
                            NavigateModGrid(-1, 0);
                            return;
                        }
                        else if (IsButtonMatch(buttonName, settings.GamepadDPadRight))
                        {
                            Logger.LogInfo("Navigate RIGHT");
                            NavigateModGrid(1, 0);
                            return;
                        }
                    }
                    
                    // Select/Toggle mod
                    if (IsButtonMatch(buttonName, settings.GamepadSelectButton))
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

        private void OnGamepadButtonReleased(object? sender, GamepadButtonEventArgs e)
        {
            var buttonName = e.GetButtonDisplayName();
            _heldButtons.Remove(buttonName);
        }

        private void OnLeftThumbstickMoved(object? sender, ThumbstickEventArgs e)
        {
            // Only handle if left stick navigation is enabled and overlay is visible
            if (!SettingsManager.Current.GamepadUseLeftStick) return;
            
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    // Ignore input if overlay is not visible
                    if (!IsOverlayVisible) return;
                    
                    if (e.IsUp)
                    {
                        Logger.LogInfo("Left stick UP");
                        NavigateModGrid(0, -1);
                    }
                    else if (e.IsDown)
                    {
                        Logger.LogInfo("Left stick DOWN");
                        NavigateModGrid(0, 1);
                    }
                    else if (e.IsLeft)
                    {
                        Logger.LogInfo("Left stick LEFT");
                        NavigateModGrid(-1, 0);
                    }
                    else if (e.IsRight)
                    {
                        Logger.LogInfo("Left stick RIGHT");
                        NavigateModGrid(1, 0);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error handling left thumbstick input", ex);
                }
            });
        }

        private bool IsButtonMatch(string pressedButton, string configuredButton)
        {
            return string.Equals(pressedButton, configuredButton, StringComparison.OrdinalIgnoreCase);
        }

        private int GetItemsPerRow()
        {
            try
            {
                // Calculate items per row based on grid width
                // Tile width is 208 + 16 spacing = 224 (from UniformGridLayout MinItemWidth=220)
                var scrollViewer = ModsRepeater?.Parent as ScrollViewer;
                if (scrollViewer == null) return 2; // Default fallback
                
                var availableWidth = scrollViewer.ActualWidth - 8; // Margin
                if (availableWidth <= 0) return 2; // Window not yet loaded
                
                var itemWidth = 220.0; // MinItemWidth from UniformGridLayout
                if (itemWidth <= 0) return 2; // Safety check
                
                var result = (int)(availableWidth / itemWidth);
                return Math.Max(1, result);
            }
            catch
            {
                return 2; // Safe fallback
            }
        }

        private void NavigateModGrid(int deltaX, int deltaY)
        {
            if (OverlayMods.Count == 0) return;

            // Deselect previous
            if (_selectedModIndex >= 0 && _selectedModIndex < OverlayMods.Count)
            {
                OverlayMods[_selectedModIndex].IsSelected = false;
            }

            // Initialize selection if none
            if (_selectedModIndex < 0)
            {
                _selectedModIndex = 0;
                if (OverlayMods.Count > 0)
                    OverlayMods[_selectedModIndex].IsSelected = true;
                return;
            }

            var itemsPerRow = GetItemsPerRow();
            if (itemsPerRow <= 0) itemsPerRow = 1; // Safety: prevent division by zero
            
            var currentRow = _selectedModIndex / itemsPerRow;
            var currentCol = _selectedModIndex % itemsPerRow;
            var totalRows = (OverlayMods.Count + itemsPerRow - 1) / itemsPerRow;
            if (totalRows <= 0) totalRows = 1; // Safety

            // Calculate new position
            var newCol = currentCol + deltaX;
            var newRow = currentRow + deltaY;

            // Wrap columns
            if (newCol < 0)
            {
                newCol = itemsPerRow - 1;
                newRow--; // Move to previous row
            }
            else if (newCol >= itemsPerRow)
            {
                newCol = 0;
                newRow++; // Move to next row
            }

            // Wrap rows
            if (newRow < 0)
                newRow = totalRows - 1;
            else if (newRow >= totalRows)
                newRow = 0;

            // Calculate new index
            var newIndex = newRow * itemsPerRow + newCol;
            
            // Clamp to valid range
            if (newIndex >= OverlayMods.Count)
            {
                // If we went past the end, go to last item
                newIndex = OverlayMods.Count - 1;
            }
            
            _selectedModIndex = Math.Max(0, Math.Min(newIndex, OverlayMods.Count - 1));

            // Select new
            OverlayMods[_selectedModIndex].IsSelected = true;
            
            // Scroll selected mod into view
            ScrollModIntoView(_selectedModIndex);
            
            // Vibrate on navigation if enabled
            if (SettingsManager.Current.GamepadVibrateOnNavigation)
            {
                _gamepadManager?.Vibrate(0, 15000, 50);
            }
            
            Logger.LogInfo($"Gamepad grid navigation: index {_selectedModIndex} (row {newRow}, col {newCol})");
        }
        
        private void ScrollModIntoView(int index)
        {
            if (ModsScrollViewer == null || ModsRepeater == null) return;
            if (index < 0 || index >= OverlayMods.Count) return;
            
            try
            {
                var element = ModsRepeater.TryGetElement(index);
                if (element == null) return;
                
                var transform = element.TransformToVisual(ModsScrollViewer);
                var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                
                var elementHeight = element.ActualSize.Y;
                var viewportHeight = ModsScrollViewer.ViewportHeight;
                var currentOffset = ModsScrollViewer.VerticalOffset;
                
                // If element is above viewport, scroll up
                if (position.Y < 0)
                {
                    ModsScrollViewer.ChangeView(null, currentOffset + position.Y - 8, null);
                }
                // If element is below viewport, scroll down
                else if (position.Y + elementHeight > viewportHeight)
                {
                    var scrollAmount = position.Y + elementHeight - viewportHeight + 8;
                    ModsScrollViewer.ChangeView(null, currentOffset + scrollAmount, null);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ScrollModIntoView error: {ex.Message}");
            }
        }

        private void ToggleSelectedMod()
        {
            if (_selectedModIndex >= 0 && _selectedModIndex < OverlayMods.Count)
            {
                var mod = OverlayMods[_selectedModIndex];
                ToggleMod(mod, vibrate: true);
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
            
            // Scroll to make selected category visible
            ScrollCategoryIntoView(newIndex);
            
            // Reset mod selection
            _selectedModIndex = -1;
            
            // Vibrate on category change (right motor only - small)
            _gamepadManager?.Vibrate(0, 30000, 300);
        }
        
        private void ScrollCategoryIntoView(int index)
        {
            try
            {
                // Try to get the actual element from ItemsRepeater
                var element = CategoriesRepeater.TryGetElement(index);
                if (element != null)
                {
                    // Get element's position relative to the ScrollViewer
                    var transform = element.TransformToVisual(CategoriesScrollViewer);
                    var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                    
                    var elementHeight = element.ActualSize.Y;
                    var currentOffset = CategoriesScrollViewer.VerticalOffset;
                    var viewportHeight = CategoriesScrollViewer.ViewportHeight;
                    
                    // Calculate absolute position in scrollable content
                    var absoluteTop = currentOffset + position.Y;
                    var absoluteBottom = absoluteTop + elementHeight;
                    
                    // Check if item is above visible area
                    if (absoluteTop < currentOffset)
                    {
                        CategoriesScrollViewer.ChangeView(null, absoluteTop, null);
                    }
                    // Check if item is below visible area
                    else if (absoluteBottom > currentOffset + viewportHeight)
                    {
                        var newOffset = absoluteBottom - viewportHeight;
                        CategoriesScrollViewer.ChangeView(null, newOffset, null);
                    }
                }
                else
                {
                    // Fallback: estimate position based on index
                    // Each category item is approximately 56px tall
                    const double itemHeight = 56;
                    var targetOffset = index * itemHeight;
                    CategoriesScrollViewer.ChangeView(null, targetOffset, null);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to scroll category into view", ex);
            }
        }

        #endregion


        /// <summary>
        /// Load categories and mods
        /// </summary>
        public async void LoadCurrentCategoryMods()
        {
            try
            {
                Logger.LogInfo("LoadCurrentCategoryMods: Starting");
                await LoadCategoriesAsync();
                Logger.LogInfo($"LoadCurrentCategoryMods: Loaded {OverlayCategories.Count} categories");
                
                // Load mods from first category if none selected
                if (_selectedCategoryPath == null && OverlayCategories.Count > 0)
                {
                    Logger.LogInfo("LoadCurrentCategoryMods: Selecting first category");
                    SelectCategory(OverlayCategories[0]);
                }
                else if (_selectedCategoryPath != null)
                {
                    // Find and select the category matching the path
                    var existingCategory = OverlayCategories.FirstOrDefault(c => c.Directory == _selectedCategoryPath);
                    if (existingCategory != null)
                    {
                        Logger.LogInfo($"LoadCurrentCategoryMods: Selecting existing category {existingCategory.Name}");
                        SelectCategory(existingCategory);
                    }
                    else if (OverlayCategories.Count > 0)
                    {
                        Logger.LogInfo("LoadCurrentCategoryMods: Category not found, selecting first");
                        SelectCategory(OverlayCategories[0]);
                    }
                }
                Logger.LogInfo("LoadCurrentCategoryMods: Completed");
            }
            catch (Exception ex)
            {
                Logger.LogError("LoadCurrentCategoryMods: Failed", ex);
            }
        }

        private async System.Threading.Tasks.Task LoadCategoriesAsync()
        {
            // Prevent concurrent loading
            if (_isLoadingCategories) return;
            _isLoadingCategories = true;
            
            try
            {
                // Clear old thumbnails to prevent memory leak
                foreach (var cat in OverlayCategories)
                {
                    cat.Thumbnail = null;
                }
                OverlayCategories.Clear();

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
                        Thumbnail = await LoadCategoryThumbnailAsync(categoryDir),
                        IsSelected = false // Don't set selection here, let SelectCategory handle it
                    };
                    
                    OverlayCategories.Add(item);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load overlay categories", ex);
            }
            finally
            {
                _isLoadingCategories = false;
            }
        }

        private async void LoadModsFromCategory(string categoryPath, bool activeOnly = false)
        {
            // Prevent concurrent loading
            if (_isLoadingMods) return;
            _isLoadingMods = true;
            
            try
            {
                // Clear old thumbnails to prevent memory leak
                foreach (var mod in OverlayMods)
                {
                    mod.Thumbnail = null;
                }
                OverlayMods.Clear();

                if (!System.IO.Directory.Exists(categoryPath))
                    return;

                var modDirs = System.IO.Directory.GetDirectories(categoryPath);
                
                // Build list of mods with their active status
                var modItems = new List<(string dir, string name, bool isActive)>();
                foreach (var modDir in modDirs)
                {
                    var modName = Path.GetFileName(modDir);
                    var isActive = !modName.StartsWith("DISABLED", StringComparison.OrdinalIgnoreCase);
                    
                    // Skip inactive mods if filtering
                    if (activeOnly && !isActive)
                        continue;
                    
                    modItems.Add((modDir, modName, isActive));
                }
                
                // Sort: active mods first, then alphabetically by name
                var sortedMods = modItems
                    .OrderByDescending(m => m.isActive)
                    .ThenBy(m => m.isActive ? m.name : m.name.Substring(8).TrimStart('_', '-', ' '))
                    .ToList();
                
                foreach (var (modDir, modName, isActive) in sortedMods)
                {
                    // Clean name for display
                    var displayName = isActive ? modName : modName.Substring(8).TrimStart('_', '-', ' ');
                    
                    var item = new OverlayModItem
                    {
                        Name = displayName,
                        Directory = modDir,
                        IsActive = isActive,
                        Thumbnail = await LoadThumbnailAsync(modDir)
                    };
                    
                    OverlayMods.Add(item);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load overlay mods", ex);
            }
            finally
            {
                _isLoadingMods = false;
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
            LoadModsFromCategory(category.Directory, _showActiveOnly);
        }
        
        /// <summary>
        /// Toggle filter to show only active mods from all categories
        /// </summary>
        public void ToggleActiveOnlyFilter()
        {
            _showActiveOnly = !_showActiveOnly;
            
            if (_showActiveOnly)
            {
                // Load active mods from ALL categories
                LoadAllActiveMods();
            }
            else
            {
                // Reload current category normally
                if (_selectedCategoryPath != null)
                {
                    LoadModsFromCategory(_selectedCategoryPath, false);
                }
            }
            
            // Reset mod selection
            _selectedModIndex = -1;
            
            Logger.LogInfo($"Overlay filter: show active only = {_showActiveOnly}");
        }
        
        /// <summary>
        /// Load all active mods from all categories
        /// </summary>
        private async void LoadAllActiveMods()
        {
            // Prevent concurrent loading
            if (_isLoadingMods) return;
            _isLoadingMods = true;
            
            try
            {
                // Clear old thumbnails to prevent memory leak
                foreach (var mod in OverlayMods)
                {
                    mod.Thumbnail = null;
                }
                OverlayMods.Clear();

                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !System.IO.Directory.Exists(modsPath))
                    return;

                // Collect all active mods from all categories
                var allActiveMods = new List<(string dir, string name)>();
                
                foreach (var categoryDir in System.IO.Directory.GetDirectories(modsPath))
                {
                    var categoryName = Path.GetFileName(categoryDir);
                    if (categoryName.Equals("Other", StringComparison.OrdinalIgnoreCase))
                        continue; // Skip Other category
                    
                    // Get all mods in this category
                    foreach (var modDir in System.IO.Directory.GetDirectories(categoryDir))
                    {
                        var modName = Path.GetFileName(modDir);
                        var isActive = !modName.StartsWith("DISABLED", StringComparison.OrdinalIgnoreCase);
                        
                        // Only show active mods
                        if (!isActive)
                            continue;
                        
                        allActiveMods.Add((modDir, modName));
                    }
                }
                
                // Sort alphabetically by name
                var sortedMods = allActiveMods.OrderBy(m => m.name).ToList();
                
                foreach (var (modDir, modName) in sortedMods)
                {
                    var item = new OverlayModItem
                    {
                        Name = modName,
                        Directory = modDir,
                        IsActive = true,
                        Thumbnail = await LoadThumbnailAsync(modDir)
                    };
                    
                    OverlayMods.Add(item);
                }
                
                Logger.LogInfo($"Loaded {OverlayMods.Count} active mods from all categories");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load all active mods", ex);
            }
            finally
            {
                _isLoadingMods = false;
            }
        }

        private void CategoriesRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Element is Button button && args.Index < OverlayCategories.Count)
            {
                button.DataContext = OverlayCategories[args.Index];
            }
        }

        private void ModsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Element is Button button && args.Index < OverlayMods.Count)
            {
                button.DataContext = OverlayMods[args.Index];
            }
        }

        private void CategoryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is OverlayCategoryItem item)
            {
                SelectCategory(item);
            }
        }

        private async System.Threading.Tasks.Task<BitmapImage?> LoadCategoryThumbnailAsync(string categoryDir)
        {
            try
            {
                // Only use catprev.jpg, fallback to icon if not found
                var thumbPath = Path.Combine(categoryDir, "catprev.jpg");
                
                if (File.Exists(thumbPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.DecodePixelWidth = 64;  // 2x for high DPI
                    bitmap.DecodePixelHeight = 64;
                    
                    using (var stream = File.OpenRead(thumbPath))
                    {
                        await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
                    }
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load category thumbnail for {categoryDir}", ex);
            }
            
            return null;
        }

        private async System.Threading.Tasks.Task<BitmapImage?> LoadThumbnailAsync(string modDir)
        {
            try
            {
                // Only use minitile.jpg like in main grid
                var thumbPath = Path.Combine(modDir, "minitile.jpg");
                
                if (File.Exists(thumbPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.DecodePixelWidth = 208;
                    bitmap.DecodePixelHeight = 250;
                    
                    using (var stream = File.OpenRead(thumbPath))
                    {
                        await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
                    }
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load mod thumbnail for {modDir}", ex);
            }
            
            return null;
        }

        private void ModItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is OverlayModItem item)
            {
                ToggleMod(item);
            }
        }

        private void ToggleMod(OverlayModItem item, bool vibrate = false)
        {
            try
            {
                var dir = item.Directory;
                var parentDir = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parentDir)) return;
                
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
                    
                    // Vibrate on toggle (only for gamepad)
                    if (vibrate)
                    {
                        _gamepadManager?.Vibrate(30000, 30000, 300);
                    }
                    
                    // Notify main window
                    ModToggleRequested?.Invoke(newPath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to toggle mod from overlay: {item.Name}", ex);
            }
        }

        public void Show(bool vibrate = false)
        {
            try
            {
                Logger.LogInfo("OverlayWindow.Show: Starting");
                _appWindow?.Show();
                Logger.LogInfo("OverlayWindow.Show: Window shown");
                LoadCurrentCategoryMods(); // Refresh on show
                Logger.LogInfo("OverlayWindow.Show: Categories loaded");
                UpdateHotkeyHint();
                Logger.LogInfo("OverlayWindow.Show: Hotkey hint updated");
                
                // Vibrate on show (only for gamepad)
                if (vibrate)
                {
                    _gamepadManager?.Vibrate(0, 25000, 400);
                }
                Logger.LogInfo("OverlayWindow.Show: Completed");
            }
            catch (Exception ex)
            {
                Logger.LogError("OverlayWindow.Show: Failed", ex);
            }
        }

        public void Hide()
        {
            _appWindow?.Hide();
            
            // Notify that window was hidden
            WindowHidden?.Invoke(this, EventArgs.Empty);
            Logger.LogInfo("OverlayWindow hidden, WindowHidden event fired");
        }

        public void Toggle(bool vibrate = false)
        {
            if (_appWindow?.IsVisible == true)
                Hide();
            else
                Show(vibrate);
        }

        /// <summary>
        /// Check if overlay window is currently visible
        /// </summary>
        public bool IsOverlayVisible => _appWindow?.IsVisible == true;

        #region Tile Hover Effects

        private void ModTile_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                try
                {
                    var tileBorder = FindChildByName<Border>(button, "TileBorder");
                    
                    // Show border on hover using system accent color
                    if (tileBorder != null)
                    {
                        var accentColor = (Windows.UI.Color)Application.Current.Resources["SystemAccentColor"];
                        tileBorder.BorderBrush = new SolidColorBrush(accentColor);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in ModTile_PointerEntered", ex);
                }
            }
        }

        private void ModTile_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                try
                {
                    var tileBorder = FindChildByName<Border>(button, "TileBorder");
                    
                    // Restore border based on selection state
                    if (tileBorder != null && button.DataContext is OverlayModItem item)
                    {
                        tileBorder.BorderBrush = item.SelectionBorderBrush;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in ModTile_PointerExited", ex);
                }
            }
        }

        private T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
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

        #endregion
    }
}
