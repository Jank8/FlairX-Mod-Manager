using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
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
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using WinRT;
using WinRT.Interop;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Animation direction for hotkeys panel
    /// </summary>
    public enum AnimationDirection
    {
        None,
        Left,
        Right,
        Up,
        Down
    }

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

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged(nameof(IsFavorite));
                    OnPropertyChanged(nameof(FavoriteStarGlyph));
                    OnPropertyChanged(nameof(FavoriteStarColor));
                }
            }
        }

        public string FavoriteStarGlyph => IsFavorite ? "\uE735" : "\uE734";
        public SolidColorBrush FavoriteStarColor => IsFavorite 
            ? new SolidColorBrush(Colors.Gold) 
            : new SolidColorBrush(Colors.White);

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
        public string Category { get; set; } = "Other"; // Add Category property
        public BitmapImage? Thumbnail { get; set; }
        public List<(string Key, string Description, string IniFile)> Hotkeys { get; set; } = new();
        
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

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged(nameof(IsFavorite));
                    OnPropertyChanged(nameof(FavoriteStarGlyph));
                    OnPropertyChanged(nameof(FavoriteStarColor));
                }
            }
        }

        public string FavoriteStarGlyph => IsFavorite ? "\uE735" : "\uE734";
        public SolidColorBrush FavoriteStarColor => IsFavorite 
            ? new SolidColorBrush(Colors.Gold) 
            : new SolidColorBrush(Colors.White);

        private bool _isBroken;
        public bool IsBroken
        {
            get => _isBroken;
            set
            {
                if (_isBroken != value)
                {
                    _isBroken = value;
                    OnPropertyChanged(nameof(IsBroken));
                    OnPropertyChanged(nameof(BrokenIconGlyph));
                    OnPropertyChanged(nameof(BrokenIconColor));
                }
            }
        }

        public string BrokenIconGlyph => "\uE7BA"; // Warning icon
        public SolidColorBrush BrokenIconColor => IsBroken
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 100, 100))
            : new SolidColorBrush(Colors.White);

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

        // Event for mod toggle requests
        public event Action<string>? ModToggleRequested;
        
        // Event for window closed notification
        public event EventHandler? WindowClosed;
        
        // Event for window hidden notification (when overlay is toggled off)
        public event EventHandler? WindowHidden;

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
                
                // Initialize separator opacity
                if (HotkeysSeparator != null)
                {
                    HotkeysSeparator.Opacity = 0.3; // Start dimmed
                }
                
                Logger.LogInfo("OverlayWindow: UpdateUITexts starting");
                UpdateUITexts();
                Logger.LogInfo("OverlayWindow: UpdateUITexts done");
                
                Logger.LogInfo("OverlayWindow: UpdateHotkeyHint starting");
                UpdateHotkeyHint();
                Logger.LogInfo("OverlayWindow: UpdateHotkeyHint done");
                
                // Start with opacity 0 to prevent black flash
                if (MainRoot != null)
                {
                    MainRoot.Opacity = 0.0;
                }
                
                // Subscribe to settings changes
                WindowStyleHelper.SettingsChanged += OnSettingsChanged;
                
                // Handle window closing to clean up resources
                this.Closed += OverlayWindow_Closed;

                // Set focus on first control when window activates (required for WinRT gamepad)
                this.Activated += (s, e) =>
                {
                    if (e.WindowActivationState != WindowActivationState.Deactivated)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            // Focus the root panel so WinRT gamepad input flows in
                            if (MainRoot != null)
                                MainRoot.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                        });
                    }
                };
                
                Logger.LogInfo("OverlayWindow: InitializeGamepad starting");
                // Use shared gamepad manager from MainWindow (avoids dual XInput polling conflict)
                AttachGamepad(mainWindow.GlobalGamepadManager);
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
            // Cancel the close and hide instead (keep overlay in memory)
            args.Handled = true;
            Hide();
            
            Logger.LogInfo("OverlayWindow: Close cancelled, window hidden instead");
        }

        public async void Show(bool vibrate = false)
        {
            try
            {
                Logger.LogInfo("OverlayWindow.Show: Starting");
                
                // Block all gamepad input during opening
                _ignoreInputAfterShow = true;
                
                // Set opacity to 0 BEFORE showing window (prevents black flash on first show)
                if (MainRoot != null)
                {
                    MainRoot.Opacity = 0.0;
                }
                
                _appWindow?.Show();
                Logger.LogInfo("OverlayWindow.Show: Window shown");

                // Switch gamepad to WinRT mode — overlay now has focus, game stops getting input
                if (_gamepadManager != null)
                    _gamepadManager.IsOverlayActive = true;
                
                // Small delay to let backdrop render before fading in
                await Task.Delay(50);

                // Simulate a mouse click in the center of the overlay to force WinRT focus
                // WinRT Gamepad only delivers input to the foreground window — this ensures it
                StealFocusWithClick();

                // Fade in the content
                if (MainRoot != null)
                {
                    MainRoot.Opacity = 1.0;
                }
                
                // Only restore state after first show (data already loaded in constructor)
                if (!_isFirstShow)
                {
                    RestoreOverlayState();
                    Logger.LogInfo("OverlayWindow.Show: State restored");
                }
                else
                {
                    _isFirstShow = false;
                    Logger.LogInfo("OverlayWindow.Show: First show - skipping state restore (already loaded in constructor)");
                }
                
                UpdateHotkeyHint();
                Logger.LogInfo("OverlayWindow.Show: Hotkey hint updated");
                
                // Vibrate on show (only for gamepad)
                if (vibrate)
                {
                    _gamepadManager?.Vibrate(0, 25000, 400);
                }
                
                // Re-enable gamepad input now that overlay is fully shown
                _ignoreInputAfterShow = false;
                _heldButtons.Clear(); // Clear any buttons held during opening
                
                Logger.LogInfo("OverlayWindow.Show: Completed");
            }
            catch (Exception ex)
            {
                Logger.LogError("OverlayWindow.Show: Failed", ex);
            }
        }

        public void Hide()
        {
            // A held button keeps a 10 Hz repeat loop alive; it must not keep navigating a
            // window the user can no longer see.
            StopRepeat();

            // Switch gamepad back to XInput mode — game will get focus back
            if (_gamepadManager != null)
                _gamepadManager.IsOverlayActive = false;

            // Fade out the content
            if (MainRoot != null)
            {
                MainRoot.Opacity = 0.0;
            }
            
            // Save state before hiding
            SaveOverlayState();
            
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
        /// Force close the overlay window (used when main window closes)
        /// </summary>
        public void ForceClose()
        {
            try
            {
                Logger.LogInfo("OverlayWindow: Force closing");
                
                // Save state before closing
                SaveOverlayState();
                
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
                
                // Clean up animation timers
                _hideTimer?.Stop();
                _hideTimer = null;
                
                // Clean up gamepad (detach only — MainWindow owns the instance)
                StopRepeat();
                DetachGamepad();
                
                // Clean up backdrop controllers
                if (_acrylicController != null)
                {
                    _acrylicController.Dispose();
                    _acrylicController = null;
                }
                if (_micaController != null)
                {
                    _micaController.Dispose();
                    _micaController = null;
                }
                _configurationSource = null;
                
                // Notify that window is being closed
                WindowClosed?.Invoke(this, EventArgs.Empty);
                
                // Destroy the AppWindow to ensure process can exit
                if (_appWindow != null)
                {
                    _appWindow.Destroy();
                    _appWindow = null;
                }
                
                Logger.LogInfo("OverlayWindow: Force closed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("OverlayWindow: Force close failed", ex);
            }
        }


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
                    
                    // Show hotkeys for hovered mod
                    if (button.DataContext is OverlayModItem item)
                    {
                        UpdateHotkeysPanel(item);
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
                    
                    // Restore hotkeys to selected mod or hide if no selection
                    UpdateHotkeysPanel(_selectedMod);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in ModTile_PointerExited", ex);
                }
            }
        }
        

    }
}
