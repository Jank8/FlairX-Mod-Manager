using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Storage.Streams;

namespace FlairX_Mod_Manager
{
    public sealed partial class MainWindow
    {

        private void CenterWindow()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            
            var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest)?.WorkArea;
            if (area == null) return;
            appWindow.Move(new Windows.Graphics.PointInt32(
                (area.Value.Width - appWindow.Size.Width) / 2,
                (area.Value.Height - appWindow.Size.Height) / 2));
        }

        private string GetXXMILauncherPath()
        {
            // Get XXMI root for current game (or default if no game selected)
            var xxmiRoot = SettingsManager.GetCurrentGameXXMIRoot();
            
            // If path is already absolute, use it directly; otherwise combine with base directory
            if (Path.IsPathRooted(xxmiRoot))
            {
                return Path.Combine(xxmiRoot, @"Resources\Bin\XXMI Launcher.exe");
            }
            else
            {
                return Path.Combine(AppContext.BaseDirectory, xxmiRoot, @"Resources\Bin\XXMI Launcher.exe");
            }
        }

        private StackPanel CreateXXMIDownloadContent(string expectedPath)
        {
            var stackPanel = new StackPanel { Spacing = 10 };
            
            var text1 = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "LauncherNotFoundDescription"),
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
            };
            
            // Always show simple relative path for XXMI Launcher (it's always in the same place)
            var xxmiRoot = SettingsManager.GetCurrentGameXXMIRoot();
            var displayPath = Path.Combine(xxmiRoot, @"Resources\Bin\XXMI Launcher.exe");
            
            var text2 = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "ExpectedPath") + ": " + displayPath,
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                FontSize = 12
            };
            
            stackPanel.Children.Add(text1);
            stackPanel.Children.Add(text2);
            
            return stackPanel;
        }

        private void LogToGridLog(string message)
        {
            try
            {
                FlairX_Mod_Manager.Pages.ModGridPage.LogToGridLog(message);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to log to grid: {message}", ex);
            }
        }

        private void SetPaneButtonTooltips()
        {
            // Placeholder: UI-dependent implementation if needed
        }
        
        private void SetCategoryTitles()
        {
            // Placeholder: UI-dependent implementation if needed
        }

        private Task PreloadModImages()
        {
            // Disabled on purpose. This ran inside Task.Run and constructed BitmapImage /
            // called SetSource there, which throws in WinUI 3 because XAML imaging is
            // thread-affine. The exception was swallowed into LogDebug, so in practice this
            // scanned the whole library (two File.Exists per mod) and cached nothing.
            // Thumbnails are loaded lazily and cached by ModGridPage.LoadImageAsync instead.
            Logger.LogDebug("PreloadModImages skipped - thumbnails are loaded lazily");
            return Task.CompletedTask;
        }

        private void UpdateUIForGameSelection(bool gameSelected)
        {
            try
            {
                // Always keep the game selection ComboBox enabled FIRST
                if (GameSelectionComboBox != null) GameSelectionComboBox.IsEnabled = true;
                
                // Ensure Presets menu item exists before updating UI state
                EnsurePresetsMenuItemExists();
                
                // Clear or populate menu items based on game selection
                if (nvSample != null)
                {
                    if (gameSelected)
                    {
                        // Enable menu items when game is selected
                        foreach (var item in nvSample.MenuItems.OfType<NavigationViewItem>())
                        {
                            item.IsEnabled = true;
                        }
                        foreach (var item in nvSample.FooterMenuItems.OfType<NavigationViewItem>())
                        {
                            item.IsEnabled = true;
                        }
                    }
                    else
                    {
                        // Clear menu items when no game is selected to hide them completely
                        nvSample.MenuItems.Clear();
                        
                        // Keep footer items but disable them
                        foreach (var item in nvSample.FooterMenuItems.OfType<NavigationViewItem>())
                        {
                            item.IsEnabled = false;
                        }
                    }
                }
                
                // Enable/disable search and buttons
                if (SearchBox != null) SearchBox.IsEnabled = gameSelected;
                if (ReloadModsButton != null) ReloadModsButton.IsEnabled = gameSelected;
                if (RestartAppButton != null) RestartAppButton.IsEnabled = gameSelected;
                if (AllModsButton != null) AllModsButton.IsEnabled = gameSelected;
                if (ViewModeToggleButton != null) ViewModeToggleButton.IsEnabled = gameSelected;
                if (BrowseGameBananaButton != null) BrowseGameBananaButton.IsEnabled = gameSelected;
                
                // Enable/disable launcher FAB
                if (LauncherFabBorder != null) LauncherFabBorder.IsHitTestVisible = gameSelected;
                
                // Ensure the game selection ComboBox stays enabled (double-check)
                if (GameSelectionComboBox != null) GameSelectionComboBox.IsEnabled = true;
                
                // If no game selected, show welcome page
                if (!gameSelected && contentFrame != null)
                {
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.WelcomePage));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error updating UI for game selection: {ex.Message}");
            }
        }

        public void UpdateOrangeAnimationVisibility(bool isVisible)
        {
            if (OrangeAnimationProgressBar != null)
            {
                OrangeAnimationProgressBar.Opacity = isVisible ? 1 : 0;
            }
        }
    }
}
