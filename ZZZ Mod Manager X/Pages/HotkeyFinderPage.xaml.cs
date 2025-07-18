using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace ZZZ_Mod_Manager_X.Pages
{
    public sealed partial class HotkeyFinderPage : Page
    {
        private Dictionary<string, string> _lang = new();
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isProcessing = false;
        
        // Singleton instance for access from other classes
        private static HotkeyFinderPage? _instance;
        public static HotkeyFinderPage? Instance => _instance;

        public HotkeyFinderPage()
        {
            this.InitializeComponent();
            _instance = this;
            LoadLanguage();
            UpdateTexts();
            LoadSettings();
        }

        private void LoadLanguage()
        {
            try
            {
                var langFile = ZZZ_Mod_Manager_X.SettingsManager.Current?.LanguageFile ?? "en.json";
                var langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "HotkeyFinder", langFile);
                if (!File.Exists(langPath))
                    langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "HotkeyFinder", "en.json");
                
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
            Title.Text = T("Title");
            AutoDetectLabel.Text = T("AutoDetectLabel");
            RefreshAllLabel.Text = T("RefreshAllLabel");
            RefreshButton.Content = _isProcessing ? T("CancelButton") : T("RefreshButton");
            ToolTipService.SetToolTip(AutoDetectToggle, T("AutoDetectLabel"));
            ToolTipService.SetToolTip(RefreshAllToggle, T("RefreshAllLabel"));
            ToolTipService.SetToolTip(RefreshButton, T("RefreshButton"));
        }

        private void LoadSettings()
        {
            try
            {
                var settingsPath = Path.Combine(System.AppContext.BaseDirectory, "Settings", "HotkeyFinder.json");
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<HotkeyFinderSettings>(json);
                    if (settings != null)
                    {
                        AutoDetectToggle.IsOn = settings.AutoDetectEnabled;
                        RefreshAllToggle.IsOn = settings.RefreshAllEnabled;
                    }
                }
            }
            catch { /* Settings loading failed - use defaults */ }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new HotkeyFinderSettings
                {
                    AutoDetectEnabled = AutoDetectToggle.IsOn,
                    // RefreshAllEnabled nie jest zapisywane, ponieważ wyłącza się po rozpoczęciu aktualizacji
                };

                var settingsDir = Path.Combine(System.AppContext.BaseDirectory, "Settings");
                Directory.CreateDirectory(settingsDir);
                var settingsPath = Path.Combine(settingsDir, "HotkeyFinder.json");
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, json);
            }
            catch { /* Settings save failed - not critical */ }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        private void AutoDetectToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void RefreshAllToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing)
            {
                _cancellationTokenSource?.Cancel();
                return;
            }

            if (!RefreshAllToggle.IsOn)
            {
                ShowInfoBar("Warning", "EnableConfirmFirst");
                return;
            }

            await RefreshAllModsAsync();
        }

        // Shared method for detecting and updating hotkeys for mods
        private static async Task DetectAndUpdateHotkeysAsync(
            IEnumerable<string> modDirs,
            bool recursive,
            CancellationToken token = default)
        {
            foreach (var modDir in modDirs)
            {
                if (token.IsCancellationRequested)
                    break;

                // Collect INI files
                var iniFiles = new List<string>();
                if (recursive)
                {
                    FindIniFilesStaticRecursive(modDir, iniFiles);
                }
                else
                {
                    try
                    {
                        var files = Directory.GetFiles(modDir, "*.ini");
                        foreach (var file in files)
                        {
                            var fileName = Path.GetFileName(file);
                            if (!string.Equals(fileName, "desktop.ini", StringComparison.OrdinalIgnoreCase) &&
                                !fileName.ToLowerInvariant().StartsWith("disabled"))
                            {
                                iniFiles.Add(file);
                            }
                        }
                    }
                    catch { }
                }

                // Parse INI files and collect hotkeys
                var hotkeys = new List<HotkeyInfo>();
                foreach (var iniFile in iniFiles)
                {
                    if (token.IsCancellationRequested)
                        break;
                    try
                    {
                        var fileHotkeys = await ParseIniFileCommonAsync(iniFile, token);
                        hotkeys.AddRange(fileHotkeys);
                    }
                    catch { }
                }

                // Remove duplicates
                hotkeys = hotkeys.GroupBy(h => h.Key + "|" + h.Description)
                                 .Select(g => g.First())
                                 .ToList();

                if (hotkeys.Count > 0)
                {
                    await UpdateModJsonWithHotkeysCommonAsync(modDir, hotkeys, token);
                }
            }
        }

        private async Task RefreshAllModsAsync()
        {
            _isProcessing = true;
            _cancellationTokenSource = new CancellationTokenSource(); // Ensure it's always initialized
            RefreshButton.Content = T("CancelButton");
            RefreshProgressBar.Visibility = Visibility.Visible;
            RefreshAllToggle.IsOn = false;
            SaveSettings();

            int processedCount = 0, successCount = 0, errorCount = 0;

            try
            {
                var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ??
                    Path.Combine(System.AppContext.BaseDirectory, "ModLibrary");
                if (!Directory.Exists(modLibraryPath))
                {
                    ShowInfoBar("Error", "ModLibraryNotFound");
                    return;
                }
                var modDirectories = Directory.GetDirectories(modLibraryPath);
                processedCount = modDirectories.Length;
                try
                {
                    await DetectAndUpdateHotkeysAsync(modDirectories, true, _cancellationTokenSource.Token);
                    successCount = processedCount;
                }
                catch { errorCount = processedCount; }

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    ShowInfoBar("Info", $"{T("RefreshCompleteMessage")}\n{T("Processed")}: {processedCount}\n{T("Success")}: {successCount}\n{T("Errors")}: {errorCount}");
                }
                else
                {
                    ShowInfoBar("Info", T("RefreshCancelledMessage"));
                }
            }
            catch (Exception ex)
            {
                ShowInfoBar("FatalError", T("FatalErrorMessage") + "\n" + ex.Message);
            }
            finally
            {
                _isProcessing = false;
                RefreshButton.Content = T("RefreshButton");
                RefreshProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        // Refaktoryzacja metody automatycznej (instancyjna)
        public async Task AutoDetectHotkeysForModAsync(string modPath)
        {
            if (!AutoDetectToggle.IsOn)
                return;
            try
            {
                await DetectAndUpdateHotkeysAsync(new[] { modPath }, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error auto-detecting hotkeys: {ex.Message}");
            }
        }

        // Refaktoryzacja metody automatycznej (statyczna)
        public static async Task AutoDetectHotkeysForModStaticAsync(string modPath)
        {
            try
            {
                bool autoDetectEnabled = false;
                var settingsPath = Path.Combine(System.AppContext.BaseDirectory, "Settings", "HotkeyFinder.json");
                if (File.Exists(settingsPath))
                {
                    try
                    {
                        var json = File.ReadAllText(settingsPath);
                        var settings = JsonSerializer.Deserialize<HotkeyFinderSettings>(json);
                        if (settings != null)
                        {
                            autoDetectEnabled = settings.AutoDetectEnabled;
                        }
                    }
                    catch { }
                }
                if (!autoDetectEnabled)
                    return;
                await DetectAndUpdateHotkeysAsync(new[] { modPath }, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error auto-detecting hotkeys: {ex.Message}");
            }
        }

        private void ShowInfoBar(string title, string message)
        {
            // Try to translate both title and message, fallback to original if not found
            string translatedTitle = _lang.TryGetValue(title, out var tTitle) ? tTitle : title;
            string translatedMessage = _lang.TryGetValue(message, out var tMsg) ? tMsg : message;
            var dialog = new ContentDialog
            {
                Title = translatedTitle,
                Content = translatedMessage,
                CloseButtonText = T("OK"),
                XamlRoot = (App.Current as App)?.MainWindow is MainWindow mainWindow && mainWindow.Content is FrameworkElement fe ? fe.XamlRoot : this.XamlRoot
            };
            _ = dialog.ShowAsync();
        }

        // Mapowanie klawiszy
        private static readonly Dictionary<string, string> KeyMapping = new()
        {
            // Klawisze strzałek
            ["VK_UP"] = "↑", ["VK_DOWN"] = "↓", ["VK_LEFT"] = "←", ["VK_RIGHT"] = "→",
            ["UP"] = "↑", ["DOWN"] = "↓", ["LEFT"] = "←", ["RIGHT"] = "→",
            
            // Klawisze numeryczne
            ["VK_NUMPAD0"] = "NUM 0", ["VK_NUMPAD1"] = "NUM 1", ["VK_NUMPAD2"] = "NUM 2",
            ["VK_NUMPAD3"] = "NUM 3", ["VK_NUMPAD4"] = "NUM 4", ["VK_NUMPAD5"] = "NUM 5",
            ["VK_NUMPAD6"] = "NUM 6", ["VK_NUMPAD7"] = "NUM 7", ["VK_NUMPAD8"] = "NUM 8",
            ["VK_NUMPAD9"] = "NUM 9", ["NUMPAD0"] = "NUM 0", ["NUMPAD1"] = "NUM 1",
            ["NUMPAD2"] = "NUM 2", ["NUMPAD3"] = "NUM 3", ["NUMPAD4"] = "NUM 4",
            ["NUMPAD5"] = "NUM 5", ["NUMPAD6"] = "NUM 6", ["NUMPAD7"] = "NUM 7",
            ["NUMPAD8"] = "NUM 8", ["NUMPAD9"] = "NUM 9",
            ["VK_MULTIPLY"] = "NUM *", ["VK_ADD"] = "NUM +", ["VK_SUBTRACT"] = "NUM -",
            ["VK_DECIMAL"] = "NUM .", ["VK_DIVIDE"] = "NUM /",
            
            // Przyciski myszy
            ["VK_LBUTTON"] = "LMB", ["VK_RBUTTON"] = "RMB", ["VK_MBUTTON"] = "MMB",
            ["VK_XBUTTON1"] = "X1", ["VK_XBUTTON2"] = "X2",
            
            // Klawisze modyfikujące
            ["VK_ALT"] = "ALT", ["VK_CTRL"] = "CTRL", ["CONTROL"] = "CTRL", ["VK_CONTROL"] = "CTRL",
            ["VK_LCONTROL"] = "L-CTRL", ["VK_RCONTROL"] = "R-CTRL", ["LCTRL"] = "L-CTRL", ["RCTRL"] = "R-CTRL",
            ["VK_SHIFT"] = "SHIFT", ["VK_LSHIFT"] = "L-SHIFT", ["VK_RSHIFT"] = "R-SHIFT",
            ["LSHIFT"] = "L-SHIFT", ["RSHIFT"] = "R-SHIFT",
            ["VK_MENU"] = "ALT", ["VK_LMENU"] = "L-ALT", ["VK_RMENU"] = "R-ALT",
            ["LALT"] = "L-ALT", ["RALT"] = "R-ALT",
            
            // Klawisze specjalne
            ["VK_OEM_MINUS"] = "-", ["VK_OEM_PLUS"] = "+", ["VK_BACKSPACE"] = "BACKSPACE",
            ["DELETE"] = "DEL", ["VK_DELETE"] = "DEL", ["VK_ESCAPE"] = "ESC",
            ["VK_RETURN"] = "ENTER", ["VK_TAB"] = "TAB", ["VK_SPACE"] = "SPACE",
            
            // Znaki specjalne
            ["VK_OEM_1"] = ";", ["VK_OEM_2"] = "/", ["VK_OEM_3"] = "`",
            ["VK_OEM_4"] = "[", ["VK_OEM_5"] = "\\", ["VK_OEM_6"] = "]",
            ["VK_OEM_7"] = "'", ["VK_OEM_8"] = "§", ["VK_OEM_COMMA"] = ",",
            ["VK_OEM_PERIOD"] = ".",
            
            // Przyciski kontrolera Xbox
            ["XB_A"] = "XB A", ["XB_B"] = "XB B", ["XB_X"] = "XB X", ["XB_Y"] = "XB Y",
            ["XB_LEFT_SHOULDER"] = "XB LB", ["XB_RIGHT_SHOULDER"] = "XB RB",
            ["XB_LEFT_TRIGGER"] = "XB LT", ["XB_RIGHT_TRIGGER"] = "XB RT",
            ["XB_LEFT_THUMB"] = "XB LS", ["XB_RIGHT_THUMB"] = "XB RS",
            ["XB_START"] = "XB Start", ["XB_BACK"] = "XB Back",
            ["XB_DPAD_UP"] = "XB ↑", ["XB_DPAD_DOWN"] = "XB ↓",
            ["XB_DPAD_LEFT"] = "XB ←", ["XB_DPAD_RIGHT"] = "XB →",
            
            // Przyciski kontrolera PlayStation
            ["PS_SQUARE"] = "□", ["PS_CROSS"] = "×", ["PS_CIRCLE"] = "○", ["PS_TRIANGLE"] = "△",
            ["PS_L1"] = "L1", ["PS_R1"] = "R1", ["PS_L2"] = "L2", ["PS_R2"] = "R2",
            ["PS_L3"] = "L3", ["PS_R3"] = "R3", ["PS_SHARE"] = "Share",
            ["PS_OPTIONS"] = "Options", ["PS_TOUCHPAD"] = "Touchpad",
            ["PS_DPAD_UP"] = "PS ↑", ["PS_DPAD_DOWN"] = "PS ↓",
            ["PS_DPAD_LEFT"] = "PS ←", ["PS_DPAD_RIGHT"] = "PS →",
            
            // Klawisze funkcyjne
            ["VK_F1"] = "F1", ["VK_F2"] = "F2", ["VK_F3"] = "F3", ["VK_F4"] = "F4",
            ["VK_F5"] = "F5", ["VK_F6"] = "F6", ["VK_F7"] = "F7", ["VK_F8"] = "F8",
            ["VK_F9"] = "F9", ["VK_F10"] = "F10", ["VK_F11"] = "F11", ["VK_F12"] = "F12",
            
            // Klawisze nawigacyjne
            ["VK_HOME"] = "HOME", ["VK_END"] = "END", ["VK_PRIOR"] = "PAGE UP",
            ["VK_NEXT"] = "PAGE DOWN", ["VK_INSERT"] = "INS",
            
            // Klawisze blokujące
            ["VK_CAPITAL"] = "CAPS", ["VK_NUMLOCK"] = "NUM LOCK", ["VK_SCROLL"] = "SCROLL LOCK",
            
            // Klawisze Windows
            ["VK_LWIN"] = "WIN", ["VK_RWIN"] = "WIN", ["VK_APPS"] = "MENU"
        };

        // Klasy pomocnicze
        public class HotkeyInfo
        {
            public string Key { get; set; } = "";
            public string Description { get; set; } = "";
        }

        public class HotkeyFinderSettings
        {
            public bool AutoDetectEnabled { get; set; } = false;
            public bool RefreshAllEnabled { get; set; } = false;
        }
        
        // Helper class for recursively finding INI files
        private static void FindIniFilesStaticRecursive(string folderPath, List<string> iniFiles)
        {
            try
            {
                // First, find INI files in the current directory
                var files = Directory.GetFiles(folderPath, "*.ini");
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    if (!string.Equals(fileName, "desktop.ini", StringComparison.OrdinalIgnoreCase) &&
                        !fileName.ToLowerInvariant().StartsWith("disabled"))
                    {
                        iniFiles.Add(file);
                    }
                }
                // Then, search subdirectories, ignoring those with the "disabled" prefix
                var directories = Directory.GetDirectories(folderPath);
                foreach (var dir in directories)
                {
                    var dirName = Path.GetFileName(dir);
                    if (!dirName.ToLowerInvariant().StartsWith("disabled"))
                    {
                        FindIniFilesStaticRecursive(dir, iniFiles);
                    }
                }
            }
            catch { /* Directory access failed - skip */ }
        }
        
        // Statyczna wersja metody ParseIniFileAsync
        private static async Task<List<HotkeyInfo>> ParseIniFileStaticAsync(string iniFilePath)
        {
            return await ParseIniFileCommonAsync(iniFilePath);
        }
        
        // Statyczna wersja metody ParseKeyValue
        private static HotkeyInfo? ParseKeyValueStatic(string keyValue, string keyType, bool isBack = false)
        {
            return ParseKeyValueCommon(keyValue, keyType, isBack);
        }
        
        // Statyczna wersja metody UpdateModJsonWithHotkeysAsync
        private static async Task UpdateModJsonWithHotkeysStaticAsync(string modPath, List<HotkeyInfo> hotkeys)
        {
            await UpdateModJsonWithHotkeysCommonAsync(modPath, hotkeys);
        }
        
        // Wspólna metoda do parsowania plików INI
        private static async Task<List<HotkeyInfo>> ParseIniFileCommonAsync(string iniFilePath, CancellationToken token = default)
        {
            var hotkeys = new List<HotkeyInfo>();
            
            try
            {
                var lines = await File.ReadAllLinesAsync(iniFilePath, token);
                var currentSection = "";
                var isKeySection = false;
                var keyType = "";

                var sectionPattern = new Regex(@"\[(.*?)\]");
                var keyPattern = new Regex(@"^key\s*=\s*(.+)", RegexOptions.IgnoreCase);
                var backPattern = new Regex(@"^back\s*=\s*(.+)", RegexOptions.IgnoreCase);

                foreach (var line in lines)
                {
                    if (token.IsCancellationRequested)
                        break;

                    var trimmedLine = line.Trim();
                    
                    if (trimmedLine.StartsWith(";") || string.IsNullOrEmpty(trimmedLine))
                        continue;

                    if (trimmedLine.StartsWith("["))
                    {
                        var sectionMatch = sectionPattern.Match(trimmedLine);
                        if (sectionMatch.Success)
                        {
                            currentSection = sectionMatch.Groups[1].Value;
                            if (currentSection.ToLowerInvariant().StartsWith("key"))
                            {
                                isKeySection = true;
                                keyType = currentSection.Substring(3).Trim();
                                
                                // Filtrowanie opisu klucza podobnie jak w oryginalnym pluginie JS
                                // Rekurencyjne usuwanie prefiksów
                                bool changed;
                                do {
                                    changed = false;
                                    string keyTypeLower = keyType.ToLowerInvariant();
                                    
                                    // Sprawdzamy i usuwamy prefiksy w kolejności od najdłuższego do najkrótszego
                                    if (keyTypeLower.StartsWith("swapswapvar")) {
                                        keyType = keyType.Substring(11).Trim();
                                        changed = true;
                                    }
                                    else if (keyTypeLower.StartsWith("keyswapvar")) {
                                        keyType = keyType.Substring(10).Trim();
                                        changed = true;
                                    }
                                    else if (keyTypeLower.StartsWith("keyswap") || keyTypeLower.StartsWith("swapvar")) {
                                        keyType = keyType.Substring(7).Trim();
                                        changed = true;
                                    }
                                    else if (keyTypeLower.StartsWith("key")) {
                                        keyType = keyType.Substring(3).Trim();
                                        changed = true;
                                    }
                                    else if (keyTypeLower.StartsWith("swap")) {
                                        keyType = keyType.Substring(4).Trim();
                                        changed = true;
                                    }
                                } while (changed && !string.IsNullOrWhiteSpace(keyType));
                                
                                // Jeśli opis jest pusty po usunięciu prefiksów, ustaw go na "Toggle"
                                if (string.IsNullOrWhiteSpace(keyType))
                                    keyType = "Toggle";
                            }
                            else
                            {
                                isKeySection = false;
                            }
                        }
                        continue;
                    }

                    if (!isKeySection)
                        continue;

                    var keyMatch = keyPattern.Match(trimmedLine);
                    var backMatch = backPattern.Match(trimmedLine);
                    var isBack = false;
                    
                    if (backMatch.Success)
                    {
                        keyMatch = backMatch;
                        isBack = true;
                    }
                    
                    if (keyMatch.Success)
                    {
                        var keyValue = keyMatch.Groups[1].Value.Trim();
                        var hotkeyInfo = ParseKeyValueCommon(keyValue, keyType, isBack);
                        if (hotkeyInfo != null)
                        {
                            hotkeys.Add(hotkeyInfo);
                        }
                    }
                }
            }
            catch { /* File parsing failed - return empty list */ }

            return hotkeys;
        }

        // Wspólna metoda do przetwarzania wartości klawiszy
        private static HotkeyInfo? ParseKeyValueCommon(string keyValue, string keyType, bool isBack = false)
        {
            var parts = keyValue.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            // Znajdź główny klawisz (ostatni element, który nie zaczyna się od "no_")
            var mainKey = "";
            var mainKeyIndex = -1;
            
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (!parts[i].ToLowerInvariant().StartsWith("no_"))
                {
                    mainKey = parts[i].ToUpperInvariant();
                    mainKeyIndex = i;
                    break;
                }
            }
            
            // Jeśli nie znaleziono głównego klawisza, użyj ostatniego elementu
            if (string.IsNullOrEmpty(mainKey))
            {
                mainKey = parts[parts.Length - 1].ToUpperInvariant();
                mainKeyIndex = parts.Length - 1;
            }
            
            // Sprawdź czy klucz jest w mapowaniu
            if (!KeyMapping.TryGetValue(mainKey, out var mappedMainKey))
                mappedMainKey = mainKey;

            var modifiers = new List<string>();
            
            // Zbiór wykluczonych modyfikatorów (dla NO_XXX)
            var excludedModifiers = new HashSet<string>();
            
            // Najpierw identyfikujemy wykluczone modyfikatory
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i].ToLowerInvariant();
                if (part.StartsWith("no_"))
                {
                    // Wyodrębnij nazwę modyfikatora bez prefiksu 'no_'
                    var excludedMod = part.Substring(3).ToUpperInvariant();
                    excludedModifiers.Add(excludedMod);
                    // Dodaj również zmapowaną wersję, jeśli istnieje
                    if (KeyMapping.TryGetValue(excludedMod, out var mappedExcluded))
                    {
                        excludedModifiers.Add(mappedExcluded);
                    }
                }
            }
            
            // Następnie dodajemy niewykluczone modyfikatory
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i].ToUpperInvariant();
                if (!part.StartsWith("NO_"))
                {
                    var mappedModifier = KeyMapping.TryGetValue(part, out var mappedMod) ? mappedMod : part;
                    if (!modifiers.Contains(mappedModifier) && !excludedModifiers.Contains(mappedModifier))
                    {
                        modifiers.Add(mappedModifier);
                    }
                }
            }

            var keyDisplay = modifiers.Count > 0 
                ? $"{string.Join("+", modifiers)}+{mappedMainKey}"
                : mappedMainKey;

            // Dodaj sufiks "(Back)" jeśli to klawisz typu "back"
            var backSuffix = isBack ? " (Back)" : "";
            
            return new HotkeyInfo
            {
                Key = keyDisplay,
                Description = keyType + backSuffix
            };
        }

        // Wspólna metoda do aktualizacji pliku mod.json
        private static async Task UpdateModJsonWithHotkeysCommonAsync(string modPath, List<HotkeyInfo> hotkeys, CancellationToken token = default)
        {
            var modJsonPath = Path.Combine(modPath, "mod.json");
            
            try
            {
                Dictionary<string, object> modData;
                
                if (File.Exists(modJsonPath))
                {
                    var existingJson = await File.ReadAllTextAsync(modJsonPath, token);
                    modData = JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson) ?? new();
                }
                else
                {
                    modData = new Dictionary<string, object>
                    {
                        ["author"] = "unknown",
                        ["character"] = "!unknown!",
                        ["url"] = "https://"
                    };
                }

                var hotkeyArray = hotkeys.Select(h => new Dictionary<string, string>
                {
                    ["key"] = h.Key,
                    ["description"] = h.Description
                }).ToArray();

                modData["hotkeys"] = hotkeyArray;

                var json = JsonSerializer.Serialize(modData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(modJsonPath, json, token);
            }
            catch { /* File update failed - skip */ }
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
    }
}