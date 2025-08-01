using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Media;
using System;
using System.Text.Json;

namespace ZZZ_Mod_Manager_X.Pages
{
    public sealed partial class FunctionsPage : Page
    {
        public class FunctionInfo
        {
            public string FileName { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool Enabled { get; set; }
        }

        private ObservableCollection<FunctionInfo> _functionInfos = new();

        public FunctionsPage()
        {
            this.InitializeComponent();
            UpdateTexts();
            LoadFunctionsList();
            PopulateSelectorBar();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            UpdateTexts();
        }

        private void UpdateTexts()
        {
            FunctionsTitle.Text = LanguageManager.Instance.T("FunctionsPage_Title");
            foreach (var func in _functionInfos)
            {
                if (func.FileName == "GBAuthorUpdate")
                {
                    // Get translation from GBAuthorUpdate language file
                    var langFile = ZZZ_Mod_Manager_X.SettingsManager.Current.LanguageFile ?? "en.json";
                    var langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "GBAuthorUpdate", langFile);
                    if (!File.Exists(langPath))
                        langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "GBAuthorUpdate", "en.json");
                    if (File.Exists(langPath))
                    {
                        var json = File.ReadAllText(langPath);
                        var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        if (dict != null && dict.TryGetValue("GameBananaAuthorUpdate_Function", out var gbName))
                        {
                            func.Name = gbName;
                        }
                    }
                }
            }
            PopulateSelectorBar();
        }

        private void SaveFunctionSettings(FunctionInfo function)
        {
            string settingsDir = Path.Combine(System.AppContext.BaseDirectory, "Settings", "Functions");
            if (!Directory.Exists(settingsDir))
                Directory.CreateDirectory(settingsDir);
            string jsonPath = Path.Combine(settingsDir, function.FileName + ".json");
            var json = JsonSerializer.Serialize(new { function.Name, function.Enabled });
            File.WriteAllText(jsonPath, json);
        }

        private void LoadFunctionsList()
        {
            _functionInfos.Clear();
            string settingsDir = Path.Combine(System.AppContext.BaseDirectory, "Settings", "Functions");

            // Add GameBanana author update function
            var gbAuthorUpdateFunction = new FunctionInfo
            {
                FileName = "GBAuthorUpdate",
                Name = GetGameBananaFunctionName(),
                Enabled = true
            };
            string gbJsonPath = Path.Combine(settingsDir, gbAuthorUpdateFunction.FileName + ".json");
            if (File.Exists(gbJsonPath))
            {
                try
                {
                    var json = File.ReadAllText(gbJsonPath);
                    var loaded = JsonSerializer.Deserialize<FunctionInfo>(json);
                    if (loaded != null)
                    {
                        gbAuthorUpdateFunction.Enabled = loaded.Enabled;
                        gbAuthorUpdateFunction.Name = loaded.Name;
                    }
                }
                catch { }
            }
            _functionInfos.Add(gbAuthorUpdateFunction);

            // Add Hotkey Finder function
            var hotkeyFinderFunction = new FunctionInfo
            {
                FileName = "HotkeyFinder",
                Name = GetHotkeyFinderFunctionName(),
                Enabled = true
            };
            string hkJsonPath = Path.Combine(settingsDir, hotkeyFinderFunction.FileName + ".json");
            if (File.Exists(hkJsonPath))
            {
                try
                {
                    var json = File.ReadAllText(hkJsonPath);
                    var loaded = JsonSerializer.Deserialize<FunctionInfo>(json);
                    if (loaded != null)
                    {
                        hotkeyFinderFunction.Enabled = loaded.Enabled;
                    }
                }
                catch { /* Settings loading failed - use defaults */ }
            }
            _functionInfos.Add(hotkeyFinderFunction);

            // Add StatusKeeper function
            var statusKeeperFunction = new FunctionInfo
            {
                FileName = "StatusKeeperPage",
                Name = GetStatusKeeperFunctionName(),
                Enabled = true
            };
            string skJsonPath = Path.Combine(settingsDir, statusKeeperFunction.FileName + ".json");
            if (File.Exists(skJsonPath))
            {
                try
                {
                    var json = File.ReadAllText(skJsonPath);
                    var loaded = JsonSerializer.Deserialize<FunctionInfo>(json);
                    if (loaded != null)
                    {
                        statusKeeperFunction.Enabled = loaded.Enabled;
                        statusKeeperFunction.Name = loaded.Name;
                    }
                }
                catch { }
            }
            _functionInfos.Add(statusKeeperFunction);

            // Add ModInfoBackup function
            var modInfoBackupFunction = new FunctionInfo
            {
                FileName = "ModInfoBackup",
                Name = GetModInfoBackupFunctionName(),
                Enabled = true
            };
            string mibJsonPath = Path.Combine(settingsDir, modInfoBackupFunction.FileName + ".json");
            if (File.Exists(mibJsonPath))
            {
                try
                {
                    var json = File.ReadAllText(mibJsonPath);
                    var loaded = JsonSerializer.Deserialize<FunctionInfo>(json);
                    if (loaded != null)
                    {
                        modInfoBackupFunction.Enabled = loaded.Enabled;
                        modInfoBackupFunction.Name = loaded.Name;
                    }
                }
                catch { }
            }
            _functionInfos.Add(modInfoBackupFunction);

            PopulateSelectorBar();
        }

        private void PopulateSelectorBar()
        {
            FunctionSelectorBar.Items.Clear();
            
            foreach (var function in _functionInfos)
            {
                if (function.Enabled)
                {
                    var selectorItem = new SelectorBarItem
                    {
                        Text = function.Name,
                        Tag = function,
                        FontSize = 22
                    };
                    FunctionSelectorBar.Items.Add(selectorItem);
                }
            }
            
            // Select the first item by default
            if (FunctionSelectorBar.Items.Count > 0)
            {
                FunctionSelectorBar.SelectedItem = FunctionSelectorBar.Items[0];
                var firstFunction = (FunctionSelectorBar.Items[0] as SelectorBarItem)?.Tag as FunctionInfo;
                if (firstFunction != null)
                {
                    NavigateToFunction(firstFunction);
                }
            }
        }

        private void FunctionSelectorBar_SelectionChanged(object sender, SelectorBarSelectionChangedEventArgs e)
        {
            if (sender is SelectorBar selectorBar && selectorBar.SelectedItem is SelectorBarItem selectedItem && selectedItem.Tag is FunctionInfo function)
            {
                NavigateToFunction(function);
            }
        }
        
        private void NavigateToFunction(FunctionInfo function)
        {
            switch (function.FileName)
            {
                case "GBAuthorUpdate":
                    FunctionContentFrame.Navigate(typeof(GBAuthorUpdatePage));
                    break;
                case "HotkeyFinder":
                    FunctionContentFrame.Navigate(typeof(HotkeyFinderPage));
                    break;
                case "StatusKeeperPage":
                    FunctionContentFrame.Navigate(typeof(StatusKeeperPage));
                    break;
                case "ModInfoBackup":
                    FunctionContentFrame.Navigate(typeof(ModInfoBackupPage));
                    break;
                default:
                    // Fallback to the generic settings page
                    FunctionContentFrame.Navigate(typeof(SettingsFunctionPage), function.FileName);
                    break;
            }
        }



        private string GetGameBananaFunctionName()
        {
            // Get translation from GBAuthorUpdate language file
            var langFile = ZZZ_Mod_Manager_X.SettingsManager.Current.LanguageFile ?? "en.json";
            var langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "GBAuthorUpdate", langFile);
            if (!File.Exists(langPath))
                langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "GBAuthorUpdate", "en.json");
            
            if (File.Exists(langPath))
            {
                try
                {
                    var json = File.ReadAllText(langPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null && dict.TryGetValue("Title", out var gbTitle))
                    {
                        return gbTitle;
                    }
                }
                catch { /* Language file parsing failed - use fallback */ }
            }
            
            // Fallback to main language manager
            return LanguageManager.Instance.T("GameBananaAuthorUpdate_Function");
        }

        private string GetHotkeyFinderFunctionName()
        {
            // Get translation from HotkeyFinder language file
            var langFile = ZZZ_Mod_Manager_X.SettingsManager.Current.LanguageFile ?? "en.json";
            var langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "HotkeyFinder", langFile);
            if (!File.Exists(langPath))
                langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "HotkeyFinder", "en.json");
            
            if (File.Exists(langPath))
            {
                try
                {
                    var json = File.ReadAllText(langPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null && dict.TryGetValue("Title", out var hkName))
                    {
                        return hkName;
                    }
                }
                catch { /* Language file parsing failed - use fallback */ }
            }
            
            // Fallback to default
            return "Hotkey Finder";
        }

        private string GetStatusKeeperFunctionName()
        {
            // Get translation from StatusKeeper language file
            var langFile = ZZZ_Mod_Manager_X.SettingsManager.Current.LanguageFile ?? "en.json";
            var langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "StatusKeeper", langFile);
            if (!File.Exists(langPath))
                langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "StatusKeeper", "en.json");
            if (File.Exists(langPath))
            {
                try
                {
                    var json = File.ReadAllText(langPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null && dict.TryGetValue("Title", out var skName))
                    {
                        return skName;
                    }
                }
                catch { /* Language file parsing failed - use fallback */ }
            }
            // Fallback to default
            return "Status Keeper";
        }
        
        private string GetModInfoBackupFunctionName()
        {
            // Get translation from ModInfoBackup language file
            var langFile = ZZZ_Mod_Manager_X.SettingsManager.Current.LanguageFile ?? "en.json";
            var langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "ModInfoBackup", langFile);
            if (!File.Exists(langPath))
                langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "ModInfoBackup", "en.json");
            
            if (File.Exists(langPath))
            {
                try
                {
                    var json = File.ReadAllText(langPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null && dict.TryGetValue("Title", out var mibName))
                    {
                        return mibName;
                    }
                }
                catch { /* Language file parsing failed - use fallback */ }
            }
            
            // Fallback to default
            return "ModInfo Backup";
        }
    }
}