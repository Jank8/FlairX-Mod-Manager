using System;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;

namespace Magazynier
{
    public partial class App : Application
    {
        private Window? _window;
        public Window? MainWindow => _window;

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Load settings
            SettingsManager.Load();

            // Auto-detect language on first start
            var langFile = SettingsManager.Current.LanguageFile;
            if (string.IsNullOrEmpty(langFile) || langFile == "auto")
            {
                var systemCulture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                var languageFolder = Path.Combine(AppContext.BaseDirectory, AppConstants.LANGUAGE_FOLDER);
                var available = Directory.Exists(languageFolder)
                    ? Directory.GetFiles(languageFolder, "*.json").Select(f => Path.GetFileName(f)).ToList()
                    : new System.Collections.Generic.List<string>();

                langFile = available.FirstOrDefault(f => f.StartsWith(systemCulture, StringComparison.OrdinalIgnoreCase)) ?? "en.json";
                SettingsManager.Current.LanguageFile = langFile;
                SettingsManager.Save();
            }

            // Initialize database
            DatabaseService.Initialize();

            _window = new MainWindow();
            _window.Activate();
        }
    }
}
