using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace ZZZ_Mod_Manager_X
{
    public class LanguageManager
    {
        private static LanguageManager? _instance;
        public static LanguageManager Instance => _instance ??= new LanguageManager();

        private Dictionary<string, string> _translations = new();
        public string? CurrentLanguage { get; private set; }
        public static readonly string LanguageFolder = Path.Combine(System.AppContext.BaseDirectory, "Language");

        public void LoadLanguage(string fileName)
        {
            var filePath = Path.Combine(LanguageFolder, fileName);
            if (!File.Exists(filePath)) return;
            var json = File.ReadAllText(filePath, Encoding.UTF8); // Force UTF-8
            _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            CurrentLanguage = Path.GetFileNameWithoutExtension(filePath);
        }

        public string T(string key)
        {
            if (_translations.TryGetValue(key, out var value))
                return value;
            return key;
        }

        // Returns list of available languages based on .json files in Language folder
        public IEnumerable<string> GetAvailableLanguages()
        {
            if (!Directory.Exists(LanguageFolder))
                yield break;
            foreach (var file in Directory.GetFiles(LanguageFolder, "*.json"))
                yield return Path.GetFileNameWithoutExtension(file);
        }
    }
}
