using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class DevToolsPage : Page
    {
        public DevToolsPage()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => RefreshDiagnostics();
        }

        private void RefreshDiagnostics()
        {
            try
            {
                DiagVersion.Text = AppConstants.APP_VERSION;
                DiagGame.Text = SettingsManager.CurrentSelectedGame ?? "None";

                var allMods = ModListManager.GetAllMods();
                DiagModCount.Text = allMods.Count.ToString();
                DiagActiveCount.Text = allMods.Count(m => m.IsActive).ToString();

                var (imgCount, ramCount, imgMB, ramMB) = ImageCacheManager.GetCacheSizes();
                DiagImageCache.Text = $"{imgCount} items ({imgMB} MB)";
                DiagRamCache.Text = $"{ramCount} items ({ramMB} MB)";

                var memMB = GC.GetTotalMemory(false) / (1024 * 1024);
                DiagMemory.Text = $"{memMB} MB";

                // Paths
                var baseDir = System.IO.Path.GetFullPath(AppContext.BaseDirectory);
                PathAppDir.Text = baseDir;
                PathSettings.Text = System.IO.Path.GetFullPath(PathManager.GetSettingsPath(""));
                PathMods.Text = SettingsManager.GetCurrentXXMIModsDirectory() ?? "Not set";
                PathLog.Text = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "Settings", AppConstants.APPLICATION_LOG_FILE));
                PathLanguage.Text = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(AppContext.BaseDirectory, AppConstants.LANGUAGE_FOLDER));

                // Hotkeys
                HotkeysList.Children.Clear();
                if (App.Current is App app && app.MainWindow is MainWindow mw)
                {
                    var hotkeys = mw.GetRegisteredHotkeyInfo();
                    if (hotkeys.Count == 0)
                    {
                        HotkeysList.Children.Add(new TextBlock { Text = "No hotkeys registered", Opacity = 0.5 });
                    }
                    else
                    {
                        foreach (var (name, hotkey) in hotkeys)
                        {
                            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                            row.Children.Add(new TextBlock { Text = name, Width = 200, Opacity = 0.7 });
                            row.Children.Add(new TextBlock { Text = hotkey, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                            HotkeysList.Children.Add(row);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("DevToolsPage: Failed to refresh diagnostics", ex);
            }
        }

        private void RefreshDiag_Click(object sender, RoutedEventArgs e) => RefreshDiagnostics();

        private void ClearImageCache_Click(object sender, RoutedEventArgs e)
        {
            ImageCacheManager.ClearAllCaches();
            RefreshDiagnostics();
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowSuccessInfo("Image cache cleared");
        }

        private void ClearCategoryCache_Click(object sender, RoutedEventArgs e)
        {
            Services.GameBananaService.ClearCategoryTreeCache();
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowSuccessInfo("Category tree cache cleared");
        }

        private void RebuildModLists_Click(object sender, RoutedEventArgs e)
        {
            ModListManager.RebuildAllLists();
            RefreshDiagnostics();
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowSuccessInfo("Mod lists rebuilt");
        }

        private void OpenAppDir_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("explorer.exe", System.IO.Path.GetFullPath(AppContext.BaseDirectory));
        }

        private void OpenSettingsDir_Click(object sender, RoutedEventArgs e)
        {
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "Settings"));
            if (System.IO.Directory.Exists(path))
                System.Diagnostics.Process.Start("explorer.exe", path);
        }

        private void OpenModsDir_Click(object sender, RoutedEventArgs e)
        {
            var path = SettingsManager.GetCurrentXXMIModsDirectory();
            if (!string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path))
                System.Diagnostics.Process.Start("explorer.exe", path);
        }

        private void SendF10_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app && app.MainWindow is MainWindow mw)
            {
                mw.SendF10KeyPress();
                mw.ShowSuccessInfo("F10 sent");
            }
        }

        private void ForceGC_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            RefreshDiagnostics();
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowSuccessInfo("Garbage Collection completed");
        }

        private void DevTestSuccess_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowSuccessInfo("Mod installed successfully!");
        }

        private void DevTestWarning_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowWarningInfo("Something might be wrong with this operation.");
        }

        private void DevTestError_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowErrorInfo("Operation failed: could not complete the requested action.");
        }

        private void DevTestInfo_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowInfoBar("", "This is an informational notification.");
        }

        private void DevTestLong_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowWarningInfo("This is a very long notification message that should wrap to multiple lines because it contains a lot of text that exceeds the maximum width of the popup notification bar.");
        }
    }
}
