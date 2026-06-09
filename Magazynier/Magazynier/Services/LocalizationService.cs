using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Magazynier
{
    /// <summary>
    /// Handles loading and retrieving localized strings from Language/*.json files.
    /// Mirrors the pattern used in FlairX Mod Manager.
    /// </summary>
    public static class LocalizationService
    {
        private static Dictionary<string, string> _dictionary = new();

        public static void Load()
        {
            var langFile = SettingsManager.Current.LanguageFile ?? "en.json";
            var langPath = Path.Combine(AppContext.BaseDirectory, AppConstants.LANGUAGE_FOLDER, langFile);

            if (!File.Exists(langPath))
            {
                // Fallback to English
                langPath = Path.Combine(AppContext.BaseDirectory, AppConstants.LANGUAGE_FOLDER, "en.json");
            }

            try
            {
                if (File.Exists(langPath))
                {
                    var json = File.ReadAllText(langPath, System.Text.Encoding.UTF8);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (loaded != null)
                    {
                        _dictionary = loaded;
                    }
                }
            }
            catch
            {
                _dictionary = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Returns the translated string for the given key.
        /// Returns [MISSING:key] if not found, to make missing translations visible.
        /// </summary>
        public static string Get(string key)
        {
            if (_dictionary.TryGetValue(key, out var value))
                return value;
            return $"[MISSING:{key}]";
        }

        /// <summary>
        /// Returns translated string with format arguments applied.
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            var template = Get(key);
            try { return string.Format(template, args); }
            catch { return template; }
        }

        public static IReadOnlyDictionary<string, string> GetAll() => _dictionary;
    }
}
