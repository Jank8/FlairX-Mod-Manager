using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ZZZ_Mod_Manager_X.Pages
{
    public sealed partial class StatusKeeperPage : Page
    {
        private Dictionary<string, string> _lang = new();
        private StatusKeeperSettings _settings = new();

        public StatusKeeperPage()
        {
            this.InitializeComponent();
            LoadLanguage();
            UpdateTexts();
            LoadSettings();
            SelectorBar1.SelectedItem = SelectorBarItemSynchronizacja;
        }

        private void LoadLanguage()
        {
            try
            {
                var langFile = ZZZ_Mod_Manager_X.SettingsManager.Current?.LanguageFile ?? "en.json";
                var langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "StatusKeeper", langFile);
                if (!File.Exists(langPath))
                    langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "StatusKeeper", "en.json");
                if (File.Exists(langPath))
                {
                    var json = File.ReadAllText(langPath);
                    _lang = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            }
            catch
            {
                _lang = new Dictionary<string, string>();
            }
        }

        private string T(string key)
        {
            return _lang.TryGetValue(key, out var value) ? value : key;
        }

        private void UpdateTexts()
        {
            Title.Text = T("StatusKeeper_Function");
            SelectorBarItemSynchronizacja.Text = T("StatusKeeper_Tab_Synchronizacja");
            SelectorBarItemKopiaZapasowa.Text = T("StatusKeeper_Tab_KopiaZapasowa");
            SelectorBarItemLogi.Text = T("StatusKeeper_Tab_Logi");
        }

        private void LoadSettings()
        {
            try
            {
                // Load settings from SettingsManager instead of separate file
                _settings = new StatusKeeperSettings
                {
                    D3dxUserIniPath = SettingsManager.Current?.StatusKeeperD3dxUserIniPath ?? "",
                    DynamicSyncEnabled = SettingsManager.Current?.StatusKeeperDynamicSyncEnabled ?? false,
                    LoggingEnabled = SettingsManager.Current?.StatusKeeperLoggingEnabled ?? true,
                    BackupOverride1Enabled = SettingsManager.Current?.StatusKeeperBackupOverride1Enabled ?? false,
                    BackupOverride2Enabled = SettingsManager.Current?.StatusKeeperBackupOverride2Enabled ?? false,
                    BackupOverride3Enabled = SettingsManager.Current?.StatusKeeperBackupOverride3Enabled ?? false
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
                // Initialize with defaults if loading fails
                _settings = new StatusKeeperSettings();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        private void BackButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.2,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
            };
            
            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.2,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
            };
            
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
            
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(scaleAnimation);
            storyboard.Children.Add(scaleAnimationY);
            storyboard.Begin();
        }

        private void BackButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
            };
            
            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
            };
            
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
            
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(scaleAnimation);
            storyboard.Children.Add(scaleAnimationY);
            storyboard.Begin();
        }

        private void SelectorBar2_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            SelectorBarItem selectedItem = sender.SelectedItem;
            int currentSelectedIndex = sender.Items.IndexOf(selectedItem);
            Type pageType;
            switch (currentSelectedIndex)
            {
                case 0:
                    pageType = typeof(StatusKeeperSyncPage);
                    break;
                case 1:
                    pageType = typeof(StatusKeeperBackupPage);
                    break;
                case 2:
                    pageType = typeof(StatusKeeperLogsPage);
                    break;
                default:
                    pageType = typeof(StatusKeeperSyncPage);
                    break;
            }
            ContentFrame.Navigate(pageType, _settings);
        }
    }

    public class StatusKeeperSettings
    {
        public string D3dxUserIniPath { get; set; } = "";
        public bool DynamicSyncEnabled { get; set; } = false;
        public bool LoggingEnabled { get; set; } = true;
        public bool BackupOverride1Enabled { get; set; } = false;
        public bool BackupOverride2Enabled { get; set; } = false;
        public bool BackupOverride3Enabled { get; set; } = false;
        // Computed property - backup override is active only when all 3 are enabled
        public bool BackupOverrideEnabled => BackupOverride1Enabled && BackupOverride2Enabled && BackupOverride3Enabled;
    }
}