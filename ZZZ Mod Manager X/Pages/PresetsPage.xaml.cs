using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using System.Threading.Tasks;

namespace ZZZ_Mod_Manager_X.Pages
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
                    return Path.Combine(System.AppContext.BaseDirectory, "Settings", "Presets");
                }
                return Path.Combine(System.AppContext.BaseDirectory, gameSpecificPath);
            }
        }
        private const string SelectedPresetKey = "SelectedPreset";

        private List<string> _presetNames = new();

        public PresetsPage()
        {
            this.InitializeComponent();
            UpdateTexts();
            LoadPresetsToComboBox();
            PresetModsListView.ItemClick += PresetModsListView_ItemClick;
            PresetModsListView.IsItemClickEnabled = true;
        }

        private void UpdateTexts()
        {
            PresetsTitle.Text = LanguageManager.Instance.T("Presets");
            PresetComboBox.PlaceholderText = LanguageManager.Instance.T("PresetsPage_ComboBox_Placeholder");
            PresetNameTextBox.PlaceholderText = LanguageManager.Instance.T("PresetsPage_NewPreset_Placeholder");
            SavePresetButtonText.Text = LanguageManager.Instance.T("PresetsPage_SavePresetButton");
            LoadPresetButtonText.Text = LanguageManager.Instance.T("PresetsPage_LoadPresetButton");
            DeletePresetButtonText.Text = LanguageManager.Instance.T("PresetsPage_DeletePresetButton");
        }

        private void LoadPresetsToComboBox()
        {
            PresetComboBox.Items.Clear();
            EnsurePresetsDir();
            _presetNames.Clear();
            PresetComboBox.Items.Add(LanguageManager.Instance.T("Default_Preset"));
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
            int selectedIndex = ZZZ_Mod_Manager_X.SettingsManager.Current.SelectedPresetIndex;
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
                var activeModsPath = Path.Combine(System.AppContext.BaseDirectory, "Settings", "ActiveMods.json");
                Dictionary<string, bool> activeMods = new();
                if (File.Exists(activeModsPath))
                {
                    try
                    {
                        var json = File.ReadAllText(activeModsPath);
                        var absMods = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
                        foreach (var kv in absMods)
                        {
                            string modName = Path.GetFileName(kv.Key);
                            activeMods[modName] = kv.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        await ShowDialog(LanguageManager.Instance.T("Error_Title"), ex.Message);
                        return;
                    }
                }
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(activeMods, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(presetPath, json);
                }
                catch (Exception ex)
                {
                    await ShowDialog(LanguageManager.Instance.T("Error_Title"), ex.Message);
                    return;
                }
                LoadPresetsToComboBox();
                await ShowDialog(LanguageManager.Instance.T("Success_Title"), LanguageManager.Instance.T("Preset_Saved"));
            }
        }

        private async void LoadPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (PresetComboBox.SelectedItem is string presetName)
            {
                var fileName = GetPresetFileNameFromComboBox(presetName);
                try
                {
                    ZZZ_Mod_Manager_X.Pages.ModGridPage.ApplyPreset(fileName);
                    await ShowDialog(LanguageManager.Instance.T("Success_Title"), LanguageManager.Instance.T("Preset_Loaded"));
                }
                catch (Exception ex)
                {
                    await ShowDialog(LanguageManager.Instance.T("Error_Title"), ex.Message);
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
                    await ShowDialog(LanguageManager.Instance.T("Success_Title"), LanguageManager.Instance.T("Preset_Deleted"));
                }
                catch (Exception ex)
                {
                    await ShowDialog(LanguageManager.Instance.T("Error_Title"), ex.Message);
                }
            }
        }

        private string GetPresetFileNameFromComboBox(object? item)
        {
            if (item is string str && str == LanguageManager.Instance.T("Default_Preset"))
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
            ZZZ_Mod_Manager_X.SettingsManager.Current.SelectedPresetIndex = selectedIndex;
            ZZZ_Mod_Manager_X.SettingsManager.Save();
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
                    catch { }
                }
            }
        }

        private void PresetModsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is string modInfo)
            {
                var modName = modInfo.Split('(')[0].Trim();
                var mainWindow = (App.Current as App)?.MainWindow as ZZZ_Mod_Manager_X.MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.GetContentFrame()?.Navigate(typeof(ZZZ_Mod_Manager_X.Pages.ModDetailPage), modName);
                }
            }
        }
    }
}
