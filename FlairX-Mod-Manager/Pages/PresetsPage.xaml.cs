using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class PresetsPage : Page
    {
        private static string PresetsDir 
        {
            get
            {
                string gameSpecificPath = AppConstants.GameConfig.GetPresetsPath(SettingsManager.CurrentSelectedGame);
                if (string.IsNullOrEmpty(gameSpecificPath))
                {
                    // Fallback to root presets directory when no game selected
                    return PathManager.GetSettingsPath("Presets");
                }
                return PathManager.GetAbsolutePath(gameSpecificPath);
            }
        }
        private const string SelectedPresetKey = "SelectedPreset";

        private List<string> _presetNames = new();

        public PresetsPage()
        {
            this.InitializeComponent();
            UpdateTexts();
            LoadPresetsToComboBox();
        }

        private void UpdateTexts()
        {
            var langDict = SharedUtilities.LoadLanguageDictionary();
            PresetsTitle.Text = SharedUtilities.GetTranslation(langDict, "Presets");
            PresetComboBox.PlaceholderText = SharedUtilities.GetTranslation(langDict, "PresetsPage_ComboBox_Placeholder");
            PresetNameTextBox.PlaceholderText = SharedUtilities.GetTranslation(langDict, "PresetsPage_NewPreset_Placeholder");
            SavePresetButtonText.Text = SharedUtilities.GetTranslation(langDict, "PresetsPage_SavePresetButton");
            LoadPresetButtonText.Text = SharedUtilities.GetTranslation(langDict, "PresetsPage_LoadPresetButton");
            DeletePresetButtonText.Text = SharedUtilities.GetTranslation(langDict, "PresetsPage_DeletePresetButton");
        }

        private void LoadPresetsToComboBox()
        {
            PresetComboBox.Items.Clear();
            EnsurePresetsDir();
            _presetNames.Clear();
            var langDict = SharedUtilities.LoadLanguageDictionary();
            PresetComboBox.Items.Add(SharedUtilities.GetTranslation(langDict, "Default_Preset"));
            _presetNames.Add("Default Preset");
            var presets = Directory.GetFiles(PresetsDir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Where(name => name != "Default Preset")
                .ToList();
            presets = presets.AsParallel().ToList();
            foreach (var preset in presets)
            {
                PresetComboBox.Items.Add(preset);
                _presetNames.Add(preset);
            }
            // Przywr�� wybrany preset z ustawie�
            int selectedIndex = FlairX_Mod_Manager.SettingsManager.Current.SelectedPresetIndex;
            if (selectedIndex >= 0 && selectedIndex < PresetComboBox.Items.Count)
                PresetComboBox.SelectedIndex = selectedIndex;
            else
                PresetComboBox.SelectedIndex = 0;
        }

        private void EnsurePresetsDir()
        {
            if (!Directory.Exists(PresetsDir))
                Directory.CreateDirectory(PresetsDir);
        }

        private async void SavePresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(PresetNameTextBox.Text))
            {
                EnsurePresetsDir();
                var presetName = PresetNameTextBox.Text.Trim();
                var presetPath = Path.Combine(PresetsDir, presetName + ".json");
                
                // Use game-specific ActiveMods file name
                var activeModsFileName = AppConstants.GameConfig.GetActiveModsFilename(SettingsManager.CurrentSelectedGame);
                var activeModsPath = PathManager.GetSettingsPath(activeModsFileName);
                
                // Get current active mods state
                var activeMods = new Dictionary<string, bool>();
                if (File.Exists(activeModsPath))
                {
                    try
                    {
                        var json = File.ReadAllText(activeModsPath);
                        var currentMods = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new Dictionary<string, bool>();
                        foreach (var kv in currentMods)
                        {
                            string modName = Path.GetFileName(kv.Key);
                            activeMods[modName] = kv.Value; // Save CURRENT state (true for active, false for inactive)
                        }
                    }
                    catch (Exception ex)
                    {
                        var saveLangDict = SharedUtilities.LoadLanguageDictionary();
                        await ShowDialog(SharedUtilities.GetTranslation(saveLangDict, "Error_Title"), ex.Message);
                        return;
                    }
                }
                
                try
                {
                    // Save the current state to preset (includes both active=true and inactive=false mods)
                    var json = System.Text.Json.JsonSerializer.Serialize(activeMods, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(presetPath, json);
                }
                catch (Exception ex)
                {
                    var errorLangDict = SharedUtilities.LoadLanguageDictionary();
                    await ShowDialog(SharedUtilities.GetTranslation(errorLangDict, "Error_Title"), ex.Message);
                    return;
                }
                LoadPresetsToComboBox();
                var langDict = SharedUtilities.LoadLanguageDictionary();
                await ShowDialog(SharedUtilities.GetTranslation(langDict, "Success_Title"), SharedUtilities.GetTranslation(langDict, "Preset_Saved"));
            }
        }

        private async void LoadPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (PresetComboBox.SelectedItem is string presetName)
            {
                var fileName = GetPresetFileNameFromComboBox(presetName);
                try
                {
                    FlairX_Mod_Manager.Pages.ModGridPage.ApplyPreset(fileName);
                    var langDict = SharedUtilities.LoadLanguageDictionary();
                    await ShowDialog(SharedUtilities.GetTranslation(langDict, "Success_Title"), SharedUtilities.GetTranslation(langDict, "Preset_Loaded"));
                }
                catch (Exception ex)
                {
                    var langDict = SharedUtilities.LoadLanguageDictionary();
                    await ShowDialog(SharedUtilities.GetTranslation(langDict, "Error_Title"), ex.Message);
                }
            }
        }

        private async void DeletePresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (PresetComboBox.SelectedItem is string presetName)
            {
                var fileName = GetPresetFileNameFromComboBox(presetName);
                var path = Path.Combine(PresetsDir, fileName + ".json");
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    LoadPresetsToComboBox();
                    var langDict = SharedUtilities.LoadLanguageDictionary();
                    await ShowDialog(SharedUtilities.GetTranslation(langDict, "Success_Title"), SharedUtilities.GetTranslation(langDict, "Preset_Deleted"));
                }
                catch (Exception ex)
                {
                    var langDict = SharedUtilities.LoadLanguageDictionary();
                    await ShowDialog(SharedUtilities.GetTranslation(langDict, "Error_Title"), ex.Message);
                }
            }
        }

        private string GetPresetFileNameFromComboBox(object? item)
        {
            var langDict = SharedUtilities.LoadLanguageDictionary();
            if (item is string str && str == SharedUtilities.GetTranslation(langDict, "Default_Preset"))
                return "Default Preset";
            return item?.ToString() ?? string.Empty;
        }

        public void CreateDefaultPresetAllInactive_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = (Application.Current as App)?.MainWindow as Window;
            if (mainWindow is not null && mainWindow is Microsoft.UI.Xaml.Window win)
            {
                var frame = win.Content as Frame;
                if (frame?.Content is ModGridPage modGridPage)
                {
                    modGridPage.SaveDefaultPresetAllInactive();
                    LoadPresetsToComboBox();
                }
            }
        }

        private async Task ShowDialog(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PresetModsListView.Items.Clear();
            int selectedIndex = PresetComboBox.SelectedIndex;
            FlairX_Mod_Manager.SettingsManager.Current.SelectedPresetIndex = selectedIndex;
            FlairX_Mod_Manager.SettingsManager.Save();
            if (selectedIndex >= 0 && selectedIndex < _presetNames.Count)
            {
                var fileName = _presetNames[selectedIndex];
                var path = Path.Combine(PresetsDir, fileName + ".json");
                if (File.Exists(path))
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        var mods = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                        if (mods != null)
                        {
                            foreach (var mod in mods)
                            {
                                if (mod.Value)
                                    PresetModsListView.Items.Add(mod.Key);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to parse preset file: {path}", ex);
                    }
                }
            }
        }


    }
}
