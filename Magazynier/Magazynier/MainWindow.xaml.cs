using System;
using System.Threading.Tasks;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Magazynier.Controls;
using Magazynier.Pages;
using WinRT;

namespace Magazynier
{
    public sealed partial class MainWindow : Window
    {
        private MicaController? _micaController;
        private DesktopAcrylicController? _acrylicController;
        private SystemBackdropConfiguration? _backdropConfig;

        public static MainWindow? Instance { get; private set; }

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
            SetupWindow();
            LocalizationService.Load();
            ApplyLocalization();
            SetupBackdrop();
        }

        // ==================== WINDOW SETUP ====================

        private void SetupWindow()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Title = "Magazynier";
            appWindow.Resize(new Windows.Graphics.SizeInt32(
                SettingsManager.Current.WindowWidth,
                SettingsManager.Current.WindowHeight));

            this.SizeChanged += MainWindow_SizeChanged;
            this.Closed += MainWindow_Closed;

            // Apply saved theme immediately
            var theme = SettingsManager.Current.Theme;
            if (this.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark"  => ElementTheme.Dark,
                    _       => ElementTheme.Default
                };
            }

            MainNotificationPopup.AttachToWindow(this);
        }

        private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            int currentW = (int)args.Size.Width;
            int currentH = (int)args.Size.Height;
            int w = Math.Max(currentW, AppConstants.MIN_WINDOW_WIDTH);
            int h = Math.Max(currentH, AppConstants.MIN_WINDOW_HEIGHT);
            if (w != currentW || h != currentH)
                appWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            SettingsManager.Current.WindowWidth = appWindow.Size.Width;
            SettingsManager.Current.WindowHeight = appWindow.Size.Height;
            SettingsManager.Save();

            _micaController?.Dispose();
            _acrylicController?.Dispose();
        }

        // ==================== LOCALIZATION ====================

        private void ApplyLocalization()
        {
            AppVersionText.Text = $"v{AppConstants.APP_VERSION}";
            NavDashboard.Content  = LocalizationService.Get("Nav_Dashboard");
            NavAssets.Content     = LocalizationService.Get("Nav_Assets");
            NavUsers.Content      = LocalizationService.Get("Nav_Users");
            NavAssignments.Content = LocalizationService.Get("Nav_Assignments");
            NavCategories.Content = LocalizationService.Get("Nav_Categories");
            NavSettings.Content   = LocalizationService.Get("Nav_Settings");
        }

        /// <summary>
        /// Reloads localization and refreshes all visible UI — called after language change.
        /// Mirrors FlairX MainWindow.RefreshUIAfterLanguageChange().
        /// </summary>
        public void RefreshUIAfterLanguageChange()
        {
            // Reload dictionary (already done by caller, but safe to repeat)
            LocalizationService.Load();

            // Refresh nav items
            ApplyLocalization();

            // Re-navigate to current page so it picks up new strings
            if (ContentFrame.Content != null)
            {
                var currentType = ContentFrame.Content.GetType();
                ContentFrame.Navigate(currentType, null, new SuppressNavigationTransitionInfo());
            }
        }

        // ==================== NAVIGATION ====================

        private void MainRoot_Loaded(object sender, RoutedEventArgs e)
        {
            NavView.SelectedItem = NavDashboard;
            ContentFrame.Navigate(typeof(DashboardPage));
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                Type? pageType = item.Tag?.ToString() switch
                {
                    "Dashboard"   => typeof(DashboardPage),
                    "Assets"      => typeof(AssetsPage),
                    "Users"       => typeof(UsersPage),
                    "Assignments" => typeof(AssignmentsPage),
                    "Categories"  => typeof(CategoriesPage),
                    "Settings"    => typeof(SettingsPage),
                    _             => null
                };

                if (pageType != null)
                    ContentFrame.Navigate(pageType, null, new EntranceNavigationTransitionInfo());
            }
        }

        // ==================== BACKDROP — identyczny mechanizm jak FlairX ====================

        private void SetupBackdrop()
        {
            TrySetBackdrop(SettingsManager.Current.Backdrop);
        }

        public void TrySetBackdrop(string backdropType)
        {
            // Dispose previous controllers
            _micaController?.Dispose();
            _micaController = null;
            _acrylicController?.Dispose();
            _acrylicController = null;

            if (backdropType == "None") return;

            // Build config
            _backdropConfig = new SystemBackdropConfiguration
            {
                IsInputActive = true,
                Theme = SettingsManager.Current.Theme switch
                {
                    "Light" => SystemBackdropTheme.Light,
                    "Dark"  => SystemBackdropTheme.Dark,
                    _       => SystemBackdropTheme.Default
                }
            };

            // Keep config in sync with window activation
            this.Activated += (s, e) =>
            {
                if (_backdropConfig != null)
                    _backdropConfig.IsInputActive =
                        e.WindowActivationState != WindowActivationState.Deactivated;
            };

            var target = this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>();

            if ((backdropType == "Mica" || backdropType == "MicaAlt") && MicaController.IsSupported())
            {
                _micaController = new MicaController
                {
                    Kind = backdropType == "MicaAlt" ? MicaKind.BaseAlt : MicaKind.Base
                };
                _micaController.AddSystemBackdropTarget(target);
                _micaController.SetSystemBackdropConfiguration(_backdropConfig);
            }
            else if (DesktopAcrylicController.IsSupported())
            {
                _acrylicController = new DesktopAcrylicController();
                _acrylicController.AddSystemBackdropTarget(target);
                _acrylicController.SetSystemBackdropConfiguration(_backdropConfig);
            }
        }

        // ==================== NOTIFICATIONS ====================

        public void ShowNotification(string message,
            NotificationSeverity severity = NotificationSeverity.Info,
            int autoCloseMs = 3000)
        {
            MainNotificationPopup.Show(message, severity, autoCloseMs);
        }
    }
}
