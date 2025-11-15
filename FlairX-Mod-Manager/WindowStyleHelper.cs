using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using WinRT;
using WinRT.Interop;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Helper class to apply consistent theme and backdrop settings to windows
    /// </summary>
    public static class WindowStyleHelper
    {
        // Event for notifying windows about settings changes
        public static event EventHandler? SettingsChanged;

        /// <summary>
        /// Notify all subscribed windows that settings have changed
        /// </summary>
        public static void NotifySettingsChanged()
        {
            SettingsChanged?.Invoke(null, EventArgs.Empty);
        }
        /// <summary>
        /// Apply theme and backdrop from settings to a window
        /// </summary>
        public static void ApplySettingsToWindow(Window window, ref MicaController? micaController, ref DesktopAcrylicController? acrylicController)
        {
            if (window == null) return;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow == null) return;

            // Extend title bar into content area
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            // Apply theme
            var theme = SettingsManager.Current.Theme;
            if (window.Content is FrameworkElement root)
            {
                if (theme == "Light")
                {
                    root.RequestedTheme = ElementTheme.Light;
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
                    appWindow.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30);
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

            // Apply backdrop
            var backdropEffect = SettingsManager.Current.BackdropEffect ?? "AcrylicThin";
            Logger.LogInfo($"Applying backdrop: {backdropEffect}, Theme: {theme}");
            ApplyBackdrop(window, backdropEffect, ref micaController, ref acrylicController);
        }

        private static SystemBackdropTheme GetSystemBackdropTheme(Window window)
        {
            if (window.Content is FrameworkElement root)
            {
                // Use RequestedTheme instead of ActualTheme because ActualTheme might not be updated yet
                var theme = root.RequestedTheme;
                if (theme == ElementTheme.Default)
                {
                    // If default, check system theme
                    var systemTheme = Application.Current.RequestedTheme;
                    return systemTheme == ApplicationTheme.Light ? SystemBackdropTheme.Light : SystemBackdropTheme.Dark;
                }
                
                return theme switch
                {
                    ElementTheme.Light => SystemBackdropTheme.Light,
                    ElementTheme.Dark => SystemBackdropTheme.Dark,
                    _ => SystemBackdropTheme.Default
                };
            }
            return SystemBackdropTheme.Default;
        }

        private static void ApplyBackdrop(Window window, string backdropEffect, ref MicaController? micaController, ref DesktopAcrylicController? acrylicController)
        {
            try
            {
                // Clear background for backdrop effects (except None)
                if (window.Content is Panel panel && backdropEffect != "None")
                {
                    panel.Background = null;
                }

                switch (backdropEffect)
                {
                    case "Mica":
                        if (MicaController.IsSupported())
                        {
                            micaController = new MicaController { Kind = MicaKind.Base };
                            micaController.AddSystemBackdropTarget(window.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                            micaController.SetSystemBackdropConfiguration(new SystemBackdropConfiguration());
                        }
                        break;

                    case "MicaAlt":
                        if (MicaController.IsSupported())
                        {
                            micaController = new MicaController { Kind = MicaKind.BaseAlt };
                            micaController.AddSystemBackdropTarget(window.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                            micaController.SetSystemBackdropConfiguration(new SystemBackdropConfiguration());
                        }
                        break;

                    case "Acrylic":
                        if (DesktopAcrylicController.IsSupported())
                        {
                            acrylicController = new DesktopAcrylicController 
                            { 
                                Kind = DesktopAcrylicKind.Base 
                            };
                            acrylicController.AddSystemBackdropTarget(window.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                            acrylicController.SetSystemBackdropConfiguration(new SystemBackdropConfiguration());
                        }
                        break;

                    case "AcrylicThin":
                        if (DesktopAcrylicController.IsSupported())
                        {
                            acrylicController = new DesktopAcrylicController 
                            { 
                                Kind = DesktopAcrylicKind.Thin 
                            };
                            acrylicController.AddSystemBackdropTarget(window.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                            acrylicController.SetSystemBackdropConfiguration(new SystemBackdropConfiguration());
                        }
                        break;

                    case "None":
                        // Set solid background based on theme
                        if (window.Content is Panel panel2)
                        {
                            var theme = panel2.ActualTheme;
                            Logger.LogInfo($"Setting None backdrop, ActualTheme: {theme}");
                            if (theme == ElementTheme.Light)
                            {
                                panel2.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 243, 243, 243));
                                Logger.LogInfo("Applied light background");
                            }
                            else if (theme == ElementTheme.Dark)
                            {
                                panel2.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 32, 32, 32));
                                Logger.LogInfo("Applied dark background");
                            }
                            else
                            {
                                // Default theme - use system theme
                                var systemTheme = Application.Current.RequestedTheme;
                                Logger.LogInfo($"Using system theme: {systemTheme}");
                                if (systemTheme == ApplicationTheme.Light)
                                {
                                    panel2.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 243, 243, 243));
                                    Logger.LogInfo("Applied light background (system)");
                                }
                                else
                                {
                                    panel2.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 32, 32, 32));
                                    Logger.LogInfo("Applied dark background (system)");
                                }
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply backdrop effect: {backdropEffect}", ex);
            }
        }

        /// <summary>
        /// Set window icon
        /// </summary>
        public static void SetWindowIcon(Window window)
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                if (appWindow != null)
                {
                    try
                    {
                        appWindow.SetIcon("Assets/app.ico");
                    }
                    catch
                    {
                        var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                        if (System.IO.File.Exists(iconPath))
                        {
                            appWindow.SetIcon(iconPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to set window icon", ex);
            }
        }
    }
}
