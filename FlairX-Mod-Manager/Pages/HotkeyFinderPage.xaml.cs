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

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class HotkeyFinderPage : Page
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isProcessing = false;
        
        // Singleton instance for access from other classes
        private static HotkeyFinderPage? _instance;
        public static HotkeyFinderPage? Instance => _instance;

        public HotkeyFinderPage()
        {
            this.InitializeComponent();
            _instance = this;
            UpdateTexts();
            LoadSettings();
        }

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("HotkeyFinder");
            AutoDetectLabel.Text = SharedUtilities.GetTranslation(lang, "AutoDetectLabel");
            RefreshAllLabel.Text = SharedUtilities.GetTranslation(lang, "RefreshAllLabel");
            RefreshButtonText.Text = _isProcessing ? SharedUtilities.GetTranslation(lang, "CancelButton") : SharedUtilities.GetTranslation(lang, "RefreshButton");
            RefreshIcon.Visibility = _isProcessing ? Visibility.Collapsed : Visibility.Visible;
            CancelIcon.Visibility = _isProcessing ? Visibility.Visible : Visibility.Collapsed;
            ToolTipService.SetToolTip(AutoDetectToggle, SharedUtilities.GetTranslation(lang, "AutoDetectLabel"));
            ToolTipService.SetToolTip(RefreshAllToggle, SharedUtilities.GetTranslation(lang, "RefreshAllLabel"));
            ToolTipService.SetToolTip(RefreshButton, SharedUtilities.GetTranslation(lang, "RefreshButton"));
        }

        private void LoadSettings()
        {
            var settings = SharedUtilities.LoadJsonSettings<HotkeyFinderSettings>("HotkeyFinder.json");
            if (settings != null)
            {
                AutoDetectToggle.IsOn = settings.AutoDetectEnabled;
                RefreshAllToggle.IsOn = settings.RefreshAllEnabled;
            }
        }

        private void SaveSettings()
        {
            var settings = new HotkeyFinderSettings
            {
                AutoDetectEnabled = AutoDetectToggle.IsOn,
                // RefreshAllEnabled is not saved because it turns off after update starts
            };

            SharedUtilities.SaveJsonSettings("HotkeyFinder.json", settings);
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
            try
            {
                await RefreshButtonClickAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in RefreshButton_Click", ex);
                ShowInfoBar("Error", ex.Message);
            }
        }
        
        private async Task RefreshButtonClickAsync()
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
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to find INI files in {modDir}", ex);
                    }
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
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to parse INI file {iniFile}", ex);
                    }
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
            var lang = SharedUtilities.LoadLanguageDictionary("HotkeyFinder");
            RefreshButtonText.Text = SharedUtilities.GetTranslation(lang, "CancelButton");
            RefreshIcon.Visibility = Visibility.Collapsed;
            CancelIcon.Visibility = Visibility.Visible;
            RefreshProgressBar.Visibility = Visibility.Visible;
            RefreshAllToggle.IsOn = false;
            SaveSettings();

            int processedCount = 0, successCount = 0, errorCount = 0;

            try
            {
                var modLibraryPath = SharedUtilities.GetSafeModLibraryPath();
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
                catch (Exception ex)
                {
                    Logger.LogError("Failed to detect and update hotkeys", ex);
                    errorCount = processedCount;
                }

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var completeMessage = $"{SharedUtilities.GetTranslation(lang, "RefreshCompleteMessage")}\n{SharedUtilities.GetTranslation(lang, "Processed")}: {processedCount}\n{SharedUtilities.GetTranslation(lang, "Success")}: {successCount}\n{SharedUtilities.GetTranslation(lang, "Errors")}: {errorCount}";
                    ShowInfoBar("Info", completeMessage);
                }
                else
                {
                    ShowInfoBar("Info", SharedUtilities.GetTranslation(lang, "RefreshCancelledMessage"));
                }
            }
            catch (Exception ex)
            {
                ShowInfoBar("FatalError", SharedUtilities.GetTranslation(lang, "FatalErrorMessage") + "\n" + ex.Message);
            }
            finally
            {
                _isProcessing = false;
                RefreshButtonText.Text = SharedUtilities.GetTranslation(lang, "RefreshButton");
                RefreshIcon.Visibility = Visibility.Visible;
                CancelIcon.Visibility = Visibility.Collapsed;
                RefreshProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        // Refactoring of the automatic method (instance)
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

        // Refactoring of the automatic method (static)
        public static async Task AutoDetectHotkeysForModStaticAsync(string modPath)
        {
            try
            {
                bool autoDetectEnabled = false;
                var settingsPath = PathManager.GetSettingsPath("HotkeyFinder.json");
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
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to load HotkeyFinder settings for static method", ex);
                    }
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
            var lang = SharedUtilities.LoadLanguageDictionary("HotkeyFinder");
            string translatedTitle = SharedUtilities.GetTranslation(lang, title);
            
            // If message contains newlines and colons, it's likely already translated/formatted
            string displayMessage;
            if (message.Contains('\n') && message.Contains(':'))
            {
                displayMessage = message; // Use as-is, already formatted
            }
            else
            {
                displayMessage = SharedUtilities.GetTranslation(lang, message); // Try to translate
            }
            
            var dialog = new ContentDialog
            {
                Title = translatedTitle,
                Content = displayMessage,
                CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                XamlRoot = (App.Current as App)?.MainWindow is MainWindow mainWindow && mainWindow.Content is FrameworkElement fe ? fe.XamlRoot : this.XamlRoot
            };
            _ = dialog.ShowAsync();
        }

        // Key mapping
        private static readonly Dictionary<string, string> KeyMapping = new()
        {
            // Arrow keys
            ["VK_UP"] = "↑", ["VK_DOWN"] = "↓", ["VK_LEFT"] = "←", ["VK_RIGHT"] = "→",
            ["UP"] = "↑", ["DOWN"] = "↓", ["LEFT"] = "←", ["RIGHT"] = "→",
            
            // Numeric keys
            ["VK_NUMPAD0"] = "NUM 0", ["VK_NUMPAD1"] = "NUM 1", ["VK_NUMPAD2"] = "NUM 2",
            ["VK_NUMPAD3"] = "NUM 3", ["VK_NUMPAD4"] = "NUM 4", ["VK_NUMPAD5"] = "NUM 5",
            ["VK_NUMPAD6"] = "NUM 6", ["VK_NUMPAD7"] = "NUM 7", ["VK_NUMPAD8"] = "NUM 8",
            ["VK_NUMPAD9"] = "NUM 9", ["NUMPAD0"] = "NUM 0", ["NUMPAD1"] = "NUM 1",
            ["NUMPAD2"] = "NUM 2", ["NUMPAD3"] = "NUM 3", ["NUMPAD4"] = "NUM 4",
            ["NUMPAD5"] = "NUM 5", ["NUMPAD6"] = "NUM 6", ["NUMPAD7"] = "NUM 7",
            ["NUMPAD8"] = "NUM 8", ["NUMPAD9"] = "NUM 9",
            ["VK_MULTIPLY"] = "NUM *", ["VK_ADD"] = "NUM +", ["VK_SUBTRACT"] = "NUM -",
            ["VK_DECIMAL"] = "NUM .", ["VK_DIVIDE"] = "NUM /",
            
            // Mouse buttons
            ["VK_LBUTTON"] = "LMB", ["VK_RBUTTON"] = "RMB", ["VK_MBUTTON"] = "MMB",
            ["VK_XBUTTON1"] = "X1", ["VK_XBUTTON2"] = "X2",
            
            // Modifier keys
            ["VK_ALT"] = "ALT", ["VK_CTRL"] = "CTRL", ["CONTROL"] = "CTRL", ["VK_CONTROL"] = "CTRL",
            ["VK_LCONTROL"] = "L-CTRL", ["VK_RCONTROL"] = "R-CTRL", ["LCTRL"] = "L-CTRL", ["RCTRL"] = "R-CTRL",
            ["VK_SHIFT"] = "SHIFT", ["VK_LSHIFT"] = "L-SHIFT", ["VK_RSHIFT"] = "R-SHIFT",
            ["LSHIFT"] = "L-SHIFT", ["RSHIFT"] = "R-SHIFT",
            ["VK_MENU"] = "ALT", ["VK_LMENU"] = "L-ALT", ["VK_RMENU"] = "R-ALT",
            ["LALT"] = "L-ALT", ["RALT"] = "R-ALT",
            
            // Special keys
            ["VK_OEM_MINUS"] = "-", ["VK_OEM_PLUS"] = "+", ["VK_BACKSPACE"] = "BACKSPACE",
            ["DELETE"] = "DEL", ["VK_DELETE"] = "DEL", ["VK_ESCAPE"] = "ESC",
            ["VK_RETURN"] = "ENTER", ["VK_TAB"] = "TAB", ["VK_SPACE"] = "SPACE",
            
            // Special characters
            ["VK_OEM_1"] = ";", ["VK_OEM_2"] = "/", ["VK_OEM_3"] = "`",
            ["VK_OEM_4"] = "[", ["VK_OEM_5"] = "\\", ["VK_OEM_6"] = "]",
            ["VK_OEM_7"] = "'", ["VK_OEM_8"] = "§", ["VK_OEM_COMMA"] = ",",
            ["VK_OEM_PERIOD"] = ".",
            
            // Xbox controller buttons
            ["XB_A"] = "XB A", ["XB_B"] = "XB B", ["XB_X"] = "XB X", ["XB_Y"] = "XB Y",
            ["XB_LEFT_SHOULDER"] = "XB LB", ["XB_RIGHT_SHOULDER"] = "XB RB",
            ["XB_LEFT_TRIGGER"] = "XB LT", ["XB_RIGHT_TRIGGER"] = "XB RT",
            ["XB_LEFT_THUMB"] = "XB LS", ["XB_RIGHT_THUMB"] = "XB RS",
            ["XB_START"] = "XB Start", ["XB_BACK"] = "XB Back",
            ["XB_DPAD_UP"] = "XB ↑", ["XB_DPAD_DOWN"] = "XB ↓",
            ["XB_DPAD_LEFT"] = "XB ←", ["XB_DPAD_RIGHT"] = "XB →",
            
            // PlayStation controller buttons
            ["PS_SQUARE"] = "□", ["PS_CROSS"] = "×", ["PS_CIRCLE"] = "○", ["PS_TRIANGLE"] = "△",
            ["PS_L1"] = "L1", ["PS_R1"] = "R1", ["PS_L2"] = "L2", ["PS_R2"] = "R2",
            ["PS_L3"] = "L3", ["PS_R3"] = "R3", ["PS_SHARE"] = "Share",
            ["PS_OPTIONS"] = "Options", ["PS_TOUCHPAD"] = "Touchpad",
            ["PS_DPAD_UP"] = "PS ↑", ["PS_DPAD_DOWN"] = "PS ↓",
            ["PS_DPAD_LEFT"] = "PS ←", ["PS_DPAD_RIGHT"] = "PS →",
            
            // Function keys
            ["VK_F1"] = "F1", ["VK_F2"] = "F2", ["VK_F3"] = "F3", ["VK_F4"] = "F4",
            ["VK_F5"] = "F5", ["VK_F6"] = "F6", ["VK_F7"] = "F7", ["VK_F8"] = "F8",
            ["VK_F9"] = "F9", ["VK_F10"] = "F10", ["VK_F11"] = "F11", ["VK_F12"] = "F12",
            
            // Navigation keys
            ["VK_HOME"] = "HOME", ["VK_END"] = "END", ["VK_PRIOR"] = "PAGE UP",
            ["VK_NEXT"] = "PAGE DOWN", ["VK_INSERT"] = "INS",
            
            // Lock keys
            ["VK_CAPITAL"] = "CAPS", ["VK_NUMLOCK"] = "NUM LOCK", ["VK_SCROLL"] = "SCROLL LOCK",
            
            // Windows keys
            ["VK_LWIN"] = "WIN", ["VK_RWIN"] = "WIN", ["VK_APPS"] = "MENU"
        };

        // Helper classes
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
            catch (Exception ex)
            {
                Logger.LogError($"Directory access failed for {folderPath}", ex);
            }
        }
        
        // Static version of ParseIniFileAsync method
        private static async Task<List<HotkeyInfo>> ParseIniFileStaticAsync(string iniFilePath)
        {
            return await ParseIniFileCommonAsync(iniFilePath);
        }
        
        // Static version of ParseKeyValue method
        private static HotkeyInfo? ParseKeyValueStatic(string keyValue, string keyType, bool isBack = false)
        {
            return ParseKeyValueCommon(keyValue, keyType, isBack);
        }
        
        // Static version of UpdateModJsonWithHotkeysAsync method
        private static async Task UpdateModJsonWithHotkeysStaticAsync(string modPath, List<HotkeyInfo> hotkeys)
        {
            await UpdateModJsonWithHotkeysCommonAsync(modPath, hotkeys);
        }
        
        // Common method for parsing INI files
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
                                
                                // Filter key description similar to the original JS plugin
                                // Recursively remove prefixes
                                bool changed;
                                do {
                                    changed = false;
                                    string keyTypeLower = keyType.ToLowerInvariant();
                                    
                                    // Check and remove prefixes in order from longest to shortest
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
                                
                                // If description is empty after removing prefixes, set it to "Toggle"
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
            catch (Exception ex)
            {
                Logger.LogError($"File parsing failed for {iniFilePath}", ex);
            }

            return hotkeys;
        }

        // Common method for processing key values
        private static HotkeyInfo? ParseKeyValueCommon(string keyValue, string keyType, bool isBack = false)
        {
            var parts = keyValue.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            // Find the main key (last element that does not start with "no_")
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
            
            // If main key not found, use last element
            if (string.IsNullOrEmpty(mainKey))
            {
                mainKey = parts[parts.Length - 1].ToUpperInvariant();
                mainKeyIndex = parts.Length - 1;
            }
            
            // Check if key is in mapping
            if (!KeyMapping.TryGetValue(mainKey, out var mappedMainKey))
                mappedMainKey = mainKey;

            var modifiers = new List<string>();
            
            // Set of excluded modifiers (for NO_XXX)
            var excludedModifiers = new HashSet<string>();
            
            // First, identify excluded modifiers
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i].ToLowerInvariant();
                if (part.StartsWith("no_"))
                {
                    // Extract modifier name without 'no_' prefix
                    var excludedMod = part.Substring(3).ToUpperInvariant();
                    excludedModifiers.Add(excludedMod);
                    // Also add mapped version if it exists
                    if (KeyMapping.TryGetValue(excludedMod, out var mappedExcluded))
                    {
                        excludedModifiers.Add(mappedExcluded);
                    }
                }
            }
            
            // Then add non-excluded modifiers
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

            // Add "(Back)" suffix if it's a back-type key
            var backSuffix = isBack ? " (Back)" : "";
            
            return new HotkeyInfo
            {
                Key = keyDisplay,
                Description = keyType + backSuffix
            };
        }

        // Common method for updating mod.json file
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
            catch (Exception ex)
            {
                Logger.LogError($"File update failed for {modJsonPath}", ex);
            }
        }


    }
}