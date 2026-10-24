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
    public sealed partial class OverlayWindow
    {
        
        private AppWindow? _appWindow;
        private MainWindow? _mainWindow;

        /// <summary>Public accessor for AppWindow — used by MainWindow for pre-init focus trick.</summary>
        public AppWindow? GetAppWindow() => _appWindow;
        
        // Backdrop controllers (same as MainWindow)
        private DesktopAcrylicController? _acrylicController;
        private MicaController? _micaController;
        private SystemBackdropConfiguration? _configurationSource;
        
        // Theme change handler reference for cleanup
        private TypedEventHandler<FrameworkElement, object>? _themeChangedHandler;

        // Animation state management
        private DispatcherTimer? _hideTimer;
        private bool _isAnimatingIn;
        private bool _isAnimatingOut;

        private void SetupWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow != null)
            {
                // Configure presenter first - always on top for overlay
                if (_appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = true;
                    presenter.IsResizable = true;
                    presenter.IsMinimizable = false;
                    presenter.IsMaximizable = false;
                    
                    // Ensure window is not maximized
                    if (presenter.State == OverlappedPresenterState.Maximized)
                    {
                        presenter.Restore();
                    }
                }

                // Restore saved size or use defaults
                var settings = SettingsManager.Current;
                var width = settings.OverlayWidth > 0 ? settings.OverlayWidth : 1500;
                var height = settings.OverlayHeight > 0 ? settings.OverlayHeight : 800;
                _appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
                
                // Extend title bar into content
                _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                
                // Set drag region for custom title bar
                SetTitleBar(AppTitleBar);

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

        /// <summary>
        /// Activates the WinUI3 window and sets focus on root control.
        /// SetForegroundWindow is already called from hotkey thread before Show() —
        /// here we just finalize the WinUI3-level activation.
        /// </summary>
        private void StealFocusWithClick()
        {
            try
            {
                // Activate WinUI3 window (triggers Activated event)
                this.Activate();
                Logger.LogInfo("StealFocusWithClick: Activate() called");
            }
            catch (Exception ex)
            {
                Logger.LogError("StealFocusWithClick failed", ex);
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
    }
}
