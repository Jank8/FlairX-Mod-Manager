using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Composition;
using WinRT;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// MainWindow partial class - Window management, theming and backdrop effects
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_STYLE = -16;
        private const int WS_MAXIMIZEBOX = 0x10000;

        private void DisableMaximizeOnDoubleClick(IntPtr hwnd)
        {
            try
            {
                var style = GetWindowLong(hwnd, GWL_STYLE);
                SetWindowLong(hwnd, GWL_STYLE, style & ~WS_MAXIMIZEBOX);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to disable maximize on double click", ex);
            }
        }

        private void RestoreWindowState()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                if (appWindow != null)
                {
                    var settings = SettingsManager.Current;
                    
                    // Validate and restore window size with fallbacks
                    int targetWidth = MIN_WIDTH;
                    int targetHeight = MIN_HEIGHT;
                    
                    if (settings.WindowWidth >= MIN_WIDTH && settings.WindowHeight >= MIN_HEIGHT)
                    {
                        targetWidth = (int)settings.WindowWidth;
                        targetHeight = (int)settings.WindowHeight;
                    }
                    else
                    {
                        // Reset corrupted values to defaults
                        settings.WindowWidth = MIN_WIDTH;
                        settings.WindowHeight = MIN_HEIGHT;
                        SettingsManager.Save();
                    }
                    
                    appWindow.Resize(new Windows.Graphics.SizeInt32(targetWidth, targetHeight));
                    
                    // Restore window position (only if valid and on-screen)
                    if (settings.WindowX >= 0 && settings.WindowY >= 0 && IsPositionOnScreen(settings.WindowX, settings.WindowY))
                    {
                        appWindow.Move(new Windows.Graphics.PointInt32(
                            (int)settings.WindowX, 
                            (int)settings.WindowY));
                    }
                    else
                    {
                        // Center window if no saved position or position is off-screen
                        CenterWindow(appWindow);
                    }
                    
                    // Restore maximized state
                    if (settings.WindowMaximized && appWindow.Presenter is OverlappedPresenter maximizePresenter)
                    {
                        maximizePresenter.Maximize();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to restore window state", ex);
                // Fallback to default size on any error
                SetDefaultWindowSize();
            }
        }

        private bool IsPositionOnScreen(double x, double y)
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
                
                if (displayArea?.WorkArea != null)
                {
                    var workArea = displayArea.WorkArea;
                    return x >= workArea.X && y >= workArea.Y && 
                           x < workArea.X + workArea.Width && y < workArea.Y + workArea.Height;
                }
            }
            catch
            {
                // If we can't determine, assume it's valid
            }
            return true;
        }

        private void SetDefaultWindowSize()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                
                if (appWindow != null)
                {
                    appWindow.Resize(new Windows.Graphics.SizeInt32(MIN_WIDTH, MIN_HEIGHT));
                    CenterWindow(appWindow);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to set default window size", ex);
            }
        }

        private void SaveWindowState()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                if (appWindow != null)
                {
                    var settings = SettingsManager.Current;
                    
                    // Save window size and position
                    settings.WindowWidth = appWindow.Size.Width;
                    settings.WindowHeight = appWindow.Size.Height;
                    settings.WindowX = appWindow.Position.X;
                    settings.WindowY = appWindow.Position.Y;
                    
                    // Save maximized state
                    if (appWindow.Presenter is OverlappedPresenter presenter)
                    {
                        settings.WindowMaximized = presenter.State == OverlappedPresenterState.Maximized;
                    }
                    
                    SettingsManager.Save();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save window state", ex);
            }
        }

        private void CenterWindow(AppWindow appWindow)
        {
            var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest)?.WorkArea;
            if (area == null) return;
            appWindow.Move(new Windows.Graphics.PointInt32(
                (area.Value.Width - appWindow.Size.Width) / 2,
                (area.Value.Height - appWindow.Size.Height) / 2));
        }

        public void ApplyThemeToTitleBar(string theme)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (this.Content is FrameworkElement root)
            {
                if (theme == "Light")
                {
                    appWindow.TitleBar.ButtonForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonHoverForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonPressedForegroundColor = Colors.Black;
                    appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(100, 230, 230, 230);
                    appWindow.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(150, 210, 210, 210);
                }
                else if (theme == "Dark")
                {
                    root.RequestedTheme = ElementTheme.Dark;
                    appWindow.TitleBar.ButtonForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
                    appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 50, 50, 50);
                    appWindow.TitleBar.ButtonPressedForegroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30);
                }
                else
                {
                    root.RequestedTheme = ElementTheme.Default;
                    appWindow.TitleBar.ButtonForegroundColor = null;
                    appWindow.TitleBar.ButtonHoverForegroundColor = null;
                    appWindow.TitleBar.ButtonPressedForegroundColor = null;
                    appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    appWindow.TitleBar.ButtonHoverBackgroundColor = null;
                    appWindow.TitleBar.ButtonPressedBackgroundColor = null;
                }
            }
        }

        public void ApplyBackdropEffect(string backdropEffect)
        {
            // Dispose current backdrop controllers
            acrylicController?.Dispose();
            acrylicController = null;
            micaController?.Dispose();
            micaController = null;

            // Clear any background when using backdrop effects (except None)
            if (backdropEffect != "None" && MainRoot != null)
            {
                MainRoot.Background = null;
            }

            // Apply new backdrop effect
            switch (backdropEffect)
            {
                case "Mica":
                    if (MicaController.IsSupported())
                    {
                        if (wsdqHelper == null)
                        {
                            wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                            wsdqHelper.EnsureWindowsSystemDispatcherQueueController();
                        }
                        if (configurationSource == null)
                        {
                            configurationSource = new SystemBackdropConfiguration();
                            Activated += Window_Activated;
                            Closed += Window_Closed;
                            ((FrameworkElement)Content).ActualThemeChanged += Window_ThemeChanged;
                            configurationSource.IsInputActive = true;
                            SetConfigurationSourceTheme();
                        }
                        micaController = new MicaController();
                        micaController.Kind = MicaKind.Base;
                        micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                        micaController.SetSystemBackdropConfiguration(configurationSource);
                    }
                    break;
                case "MicaAlt":
                    if (MicaController.IsSupported())
                    {
                        if (wsdqHelper == null)
                        {
                            wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                            wsdqHelper.EnsureWindowsSystemDispatcherQueueController();
                        }
                        if (configurationSource == null)
                        {
                            configurationSource = new SystemBackdropConfiguration();
                            Activated += Window_Activated;
                            Closed += Window_Closed;
                            ((FrameworkElement)Content).ActualThemeChanged += Window_ThemeChanged;
                            configurationSource.IsInputActive = true;
                            SetConfigurationSourceTheme();
                        }
                        micaController = new MicaController();
                        micaController.Kind = MicaKind.BaseAlt;
                        micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                        micaController.SetSystemBackdropConfiguration(configurationSource);
                    }
                    break;
                case "Acrylic":
                    TrySetAcrylicBackdrop(false); // Base acrylic
                    break;
                case "AcrylicThin":
                    TrySetAcrylicBackdrop(true); // Thin acrylic
                    break;
                case "None":
                    // No backdrop effect - set appropriate background based on theme
                    SetNoneBackgroundForTheme();
                    break;
                default:
                    TrySetAcrylicBackdrop(true); // Default to thin acrylic
                    break;
            }
        }

        private bool TrySetAcrylicBackdrop(bool useThinMaterial)
        {
            if (DesktopAcrylicController.IsSupported())
            {
                if (wsdqHelper == null)
                {
                    wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                    wsdqHelper.EnsureWindowsSystemDispatcherQueueController();
                }
                if (configurationSource == null)
                {
                    configurationSource = new SystemBackdropConfiguration();
                    Activated += Window_Activated;
                    Closed += Window_Closed;
                    ((FrameworkElement)Content).ActualThemeChanged += Window_ThemeChanged;
                    configurationSource.IsInputActive = true;
                    SetConfigurationSourceTheme();
                }
                acrylicController = new DesktopAcrylicController();
                acrylicController.Kind = useThinMaterial ? DesktopAcrylicKind.Thin : DesktopAcrylicKind.Base;
                acrylicController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                acrylicController.SetSystemBackdropConfiguration(configurationSource);
                return true;
            }
            return false;
        }

        private void SetNoneBackgroundForTheme()
        {
            if (MainRoot != null)
            {
                var theme = SettingsManager.Current.Theme;
                if (theme == "Light")
                {
                    MainRoot.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
                }
                else if (theme == "Dark")
                {
                    MainRoot.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black);
                }
                else
                {
                    // Auto theme - use system default
                    MainRoot.Background = null;
                }
            }
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (configurationSource != null)
            {
                configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
            // use this closed window.
            if (micaController != null)
            {
                micaController.Dispose();
                micaController = null;
            }
            if (acrylicController != null)
            {
                acrylicController.Dispose();
                acrylicController = null;
            }
            Activated -= Window_Activated;
            configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            if (configurationSource != null)
            {
                switch (((FrameworkElement)Content).ActualTheme)
                {
                    case ElementTheme.Dark:
                        configurationSource.Theme = SystemBackdropTheme.Dark;
                        break;
                    case ElementTheme.Light:
                        configurationSource.Theme = SystemBackdropTheme.Light;
                        break;
                    case ElementTheme.Default:
                        configurationSource.Theme = SystemBackdropTheme.Default;
                        break;
                }
            }
        }

        // Window control button event handlers
        private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var presenter = appWindow.Presenter as OverlappedPresenter;
            presenter?.Maximize();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var presenter = appWindow.Presenter as OverlappedPresenter;
            presenter?.Minimize();
        }

        private async void RestoreBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await RestoreButtonAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in RestoreBtn_Click", ex);
            }
        }
        
        private async System.Threading.Tasks.Task RestoreButtonAsync()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var presenter = appWindow.Presenter as OverlappedPresenter;
            presenter?.Minimize();
            await System.Threading.Tasks.Task.Delay(3000);
            presenter?.Restore();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }
    }
}