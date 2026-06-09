using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Magazynier.Pages
{
    public sealed partial class SettingsPage : Page
    {
        private bool _isLoading = true;

        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _isLoading = true;
            ApplyLocalization();
            LoadSettings();
            _isLoading = false;
        }

        private void ApplyLocalization()
        {
            PageTitle.Text = LocalizationService.Get("Settings_Title");
            AppearanceHeader.Text = LocalizationService.Get("Settings_AppearanceHeader");
            ThemeLabel.Text = LocalizationService.Get("Settings_Theme");
            ThemeDescription.Text = LocalizationService.Get("Settings_Theme_Description");
            ThemeAutoText.Text = LocalizationService.Get("Settings_Theme_Auto");
            ThemeLightText.Text = LocalizationService.Get("Settings_Theme_Light");
            ThemeDarkText.Text = LocalizationService.Get("Settings_Theme_Dark");
            BackdropLabel.Text = LocalizationService.Get("Settings_Backdrop");
            BackdropDescription.Text = LocalizationService.Get("Settings_Backdrop_Description");
            LanguageLabel.Text = LocalizationService.Get("Settings_Language");
            LanguageDescription.Text = LocalizationService.Get("Settings_Language_Description");
            VersionLabel.Text = "Magazynier";
            VersionValue.Text = $"v{AppConstants.APP_VERSION}";
        }

        private void LoadSettings()
        {
            // Theme selector
            var theme = SettingsManager.Current.Theme;
            foreach (var item in ThemeSelectorBar.Items)
            {
                if (item is SelectorBarItem sbi && sbi.Tag?.ToString() == theme)
                {
                    ThemeSelectorBar.SelectedItem = sbi;
                    break;
                }
            }

            // Backdrop
            var backdropOptions = new List<BackdropOption>
            {
                new() { Key = "Mica",    Label = LocalizationService.Get("Settings_Backdrop_Mica") },
                new() { Key = "MicaAlt", Label = LocalizationService.Get("Settings_Backdrop_MicaAlt") },
                new() { Key = "Acrylic", Label = LocalizationService.Get("Settings_Backdrop_Acrylic") },
                new() { Key = "None",    Label = LocalizationService.Get("Settings_Backdrop_None") },
            };
            BackdropComboBox.ItemsSource = backdropOptions;
            BackdropComboBox.DisplayMemberPath = "Label";
            var currentBackdrop = SettingsManager.Current.Backdrop;
            BackdropComboBox.SelectedItem = backdropOptions.FirstOrDefault(b => b.Key == currentBackdrop) ?? backdropOptions[0];

            // Language
            var langFolder = Path.Combine(AppContext.BaseDirectory, AppConstants.LANGUAGE_FOLDER);
            var langFiles = Directory.Exists(langFolder)
                ? Directory.GetFiles(langFolder, "*.json").Select(f => Path.GetFileName(f)).ToList()
                : new List<string> { "en.json", "pl.json" };

            var langOptions = langFiles.Select(f => new LanguageOption
            {
                FileName = f,
                DisplayName = GetLanguageDisplayName(f)
            }).OrderBy(l => l.DisplayName).ToList();

            LanguageComboBox.ItemsSource = langOptions;
            LanguageComboBox.DisplayMemberPath = "DisplayName";
            var currentLang = SettingsManager.Current.LanguageFile ?? "en.json";
            LanguageComboBox.SelectedItem = langOptions.FirstOrDefault(l => l.FileName == currentLang) ?? langOptions.FirstOrDefault();
        }

        private static string GetLanguageDisplayName(string fileName)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, AppConstants.LANGUAGE_FOLDER, fileName);
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null && dict.TryGetValue("Language_DisplayName", out var name))
                        return name;
                }
            }
            catch { }
            return Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
        }

        // ==================== THEME — identyczny mechanizm jak FlairX ====================

        private async void ThemeSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            if (_isLoading) return;
            if (sender.SelectedItem is SelectorBarItem item && item.Tag is string theme)
            {
                SettingsManager.Current.Theme = theme;
                SettingsManager.Save();

                if (MainWindow.Instance?.Content is FrameworkElement root)
                {
                    root.RequestedTheme = theme switch
                    {
                        "Light" => ElementTheme.Light,
                        "Dark"  => ElementTheme.Dark,
                        _       => ElementTheme.Default
                    };

                    // Small delay to let theme take effect before refreshing backdrop
                    await Task.Delay(10);
                    MainWindow.Instance?.TrySetBackdrop(SettingsManager.Current.Backdrop);
                }
            }
        }

        // ==================== BACKDROP ====================

        private void BackdropComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (BackdropComboBox.SelectedItem is BackdropOption opt)
            {
                SettingsManager.Current.Backdrop = opt.Key;
                SettingsManager.Save();
                MainWindow.Instance?.TrySetBackdrop(opt.Key);
            }
        }

        // ==================== LANGUAGE — live reload jak FlairX ====================

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (LanguageComboBox.SelectedItem is LanguageOption lang)
            {
                SettingsManager.Current.LanguageFile = lang.FileName;
                SettingsManager.Save();

                // Reload dictionary immediately — no restart needed
                LocalizationService.Load();

                // Refresh the entire window UI
                MainWindow.Instance?.RefreshUIAfterLanguageChange();

                // Re-apply our own texts since we're the current page
                _isLoading = true;
                ApplyLocalization();
                _isLoading = false;
            }
        }

        private class BackdropOption
        {
            public string Key { get; set; } = "";
            public string Label { get; set; } = "";
        }

        private class LanguageOption
        {
            public string FileName { get; set; } = "";
            public string DisplayName { get; set; } = "";
        }
    }
}
