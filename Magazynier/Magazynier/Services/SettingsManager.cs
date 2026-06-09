using System;
using System.IO;
using System.Text.Json;

namespace Magazynier
{
    public class AppSettings
    {
        public string? LanguageFile { get; set; }
        public string Theme { get; set; } = "Auto";
        public string Backdrop { get; set; } = "Mica";
        public int WindowWidth { get; set; } = AppConstants.DEFAULT_WINDOW_WIDTH;
        public int WindowHeight { get; set; } = AppConstants.DEFAULT_WINDOW_HEIGHT;
    }

    public static class SettingsManager
    {
        private static AppSettings _current = new();
        public static AppSettings Current => _current;

        private static string SettingsPath =>
            Path.Combine(AppContext.BaseDirectory, AppConstants.SETTINGS_FOLDER, AppConstants.SETTINGS_FILE);

        public static void Load()
        {
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, AppConstants.SETTINGS_FOLDER);
                Directory.CreateDirectory(dir);

                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath, System.Text.Encoding.UTF8);
                    _current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                _current = new AppSettings();
            }
        }

        public static void Save()
        {
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, AppConstants.SETTINGS_FOLDER);
                Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json, System.Text.Encoding.UTF8);
            }
            catch { /* non-critical */ }
        }
    }
}
