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

                DiagModsPath.Text = SettingsManager.GetCurrentXXMIModsDirectory() ?? "Not set";
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
