using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using WinRT.Interop;
using WinRT;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using System.Runtime.InteropServices;

namespace FlairX_Mod_Manager
{
    public sealed partial class LoadingWindow : Window
    {
        // Backdrop fields
        WindowsSystemDispatcherQueueHelper? wsdqHelper;
        DesktopAcrylicController? acrylicController;
        MicaController? micaController;
        SystemBackdropConfiguration? configurationSource;

        public LoadingWindow()
        {
            this.InitializeComponent();
            
            // Set window properties
            this.Title = "FlairX Mod Manager - Loading";
            
            // Apply theme from settings FIRST
            var theme = SettingsManager.Current.Theme ?? "Auto";
            if (this.Content is FrameworkElement root)
            {
                if (theme == "Light")
                    root.RequestedTheme = ElementTheme.Light;
                else if (theme == "Dark")
                    root.RequestedTheme = ElementTheme.Dark;
                else
                    root.RequestedTheme = ElementTheme.Default;
            }
            
            // Apply backdrop effect from settings AFTER theme is set
            string backdropEffect = SettingsManager.Current.BackdropEffect ?? "AcrylicThin";
            
            // Check Windows 10 compatibility and fix incompatible backdrop effects
            backdropEffect = EnsureBackdropCompatibility(backdropEffect);
            
            ApplyBackdropEffect(backdropEffect);
            
            // Configure window
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            
            if (appWindow != null)
            {
                // Set window size - larger to fit content properly
                appWindow.Resize(new Windows.Graphics.SizeInt32(500, 250));
                
                // Center the window
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var centerX = (displayArea.WorkArea.Width - 500) / 2;
                    var centerY = (displayArea.WorkArea.Height - 250) / 2;
                    appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
                }
                
                // Set window properties
                appWindow.SetIcon("app.ico");
                
                // Configure title bar
                if (appWindow.TitleBar != null)
                {
                    appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                    appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                    appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                }
            }
        }
        
        public void UpdateStatus(string status)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                StatusText.Text = status;
            });
        }
        
        public void SetProgress(double value)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                LoadingProgressBar.IsIndeterminate = false;
                LoadingProgressBar.Value = value;
            });
        }
        
        public void SetIndeterminate(bool isIndeterminate)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                LoadingProgressBar.IsIndeterminate = isIndeterminate;
            });
        }

        private string EnsureBackdropCompatibility(string backdropEffect)
        {
            // Check if running on Windows 10 (build < 22000 = Windows 11)
            bool isWindows10 = Environment.OSVersion.Version.Build < 22000;
            
            if (isWindows10 && (backdropEffect == "Mica" || backdropEffect == "MicaAlt"))
            {
                Logger.LogInfo($"Windows 10 detected in LoadingWindow - switching from {backdropEffect} to AcrylicThin for compatibility");
                
                // Update settings to compatible backdrop (but don't save here to avoid conflicts)
                return "AcrylicThin";
            }
            
            return backdropEffect;
        }

        public void ApplyBackdropEffect(string backdropEffect)
        {
            // Dispose current backdrop controllers
            acrylicController?.Dispose();
            acrylicController = null;
            micaController?.Dispose();
            micaController = null;

            // Clear any background when using backdrop effects (except None)
            if (backdropEffect != "None" && LoadingRoot != null)
            {
                LoadingRoot.Background = null;
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

        bool TrySetAcrylicBackdrop(bool useAcrylicThin)
        {
            if (DesktopAcrylicController.IsSupported())
            {
                wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // Hooking up the policy object
                configurationSource = new SystemBackdropConfiguration();
                Activated += Window_Activated;
                Closed += Window_Closed;
                ((FrameworkElement)Content).ActualThemeChanged += Window_ThemeChanged;

                // Initial configuration state.
                configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                acrylicController = new DesktopAcrylicController();
                acrylicController.Kind = useAcrylicThin ? DesktopAcrylicKind.Thin : DesktopAcrylicKind.Base;

                // Enable the system backdrop.
                acrylicController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                acrylicController.SetSystemBackdropConfiguration(configurationSource);
                return true; // Succeeded.
            }

            return false; // Acrylic is not supported on this system.
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (configurationSource != null)
                configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed
            acrylicController?.Dispose();
            acrylicController = null;
            micaController?.Dispose();
            micaController = null;
            Activated -= Window_Activated;
            configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
            
            // Update background if using "None" effect
            string currentBackdrop = SettingsManager.Current.BackdropEffect ?? "AcrylicThin";
            if (currentBackdrop == "None")
            {
                SetNoneBackgroundForTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            if (configurationSource != null)
            {
                switch (((FrameworkElement)this.Content).ActualTheme)
                {
                    case ElementTheme.Dark: configurationSource.Theme = SystemBackdropTheme.Dark; break;
                    case ElementTheme.Light: configurationSource.Theme = SystemBackdropTheme.Light; break;
                    case ElementTheme.Default: configurationSource.Theme = SystemBackdropTheme.Default; break;
                }
            }
        }

        private void SetNoneBackgroundForTheme()
        {
            if (this.Content is FrameworkElement root && LoadingRoot != null)
            {
                var currentTheme = root.ActualTheme;
                if (currentTheme == ElementTheme.Light)
                {
                    // Light theme - use a clean white/light gray background
                    LoadingRoot.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 248, 248, 248));
                }
                else if (currentTheme == ElementTheme.Dark)
                {
                    // Dark theme - use a proper dark background that matches WinUI 3 dark theme
                    LoadingRoot.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 23, 23, 23));
                }
                else
                {
                    // Auto theme - use system resource
                    LoadingRoot.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"];
                }
            }
        }

    }
}