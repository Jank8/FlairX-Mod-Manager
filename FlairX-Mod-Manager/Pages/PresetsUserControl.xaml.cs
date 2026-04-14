using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class PresetsUserControl : UserControl
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

        public event EventHandler? CloseRequested; // Event to notify parent to close

        public PresetsUserControl()
        {
            this.InitializeComponent();
            UpdateTexts();
            LoadPresetsToComboBox();
            
            // Subscribe to theme changes to update this UserControl immediately
            this.Loaded += PresetsUserControl_Loaded;
        }
        
        private void PresetsUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Subscribe to ActualThemeChanged when loaded
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                if (mainWindow.Content is FrameworkElement root)
                {
                    root.ActualThemeChanged += Root_ActualThemeChanged;
                }
            }
        }
        
        private void Root_ActualThemeChanged(FrameworkElement sender, object args)
        {
            // Update this UserControl's theme to match the root
            this.RequestedTheme = sender.ActualTheme;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Notify parent to close the panel
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BackButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.1,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };

            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.1,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };

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
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };

            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };

            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");

            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(scaleAnimation);
            storyboard.Children.Add(scaleAnimationY);
            storyboard.Begin();
        }

        private void UpdateTexts()
        {
            var langDict = SharedUtilities.LoadLanguageDictionary();
            PresetsTitle.Text = SharedUtilities.GetTranslation(langDict, "Presets");
            
            // Load Preset Card
            LoadPresetLabel.Text = SharedUtilities.GetTranslation(langDict, "PresetsPage_LoadPresetLabel");
            PresetComboBox.PlaceholderText = SharedUtilities.GetTranslation(langDict, "PresetsPage_ComboBox_Placeholder");
            LoadPresetButtonText.Text = SharedUtilities.GetTranslation(langDict, "PresetsPage_LoadPresetButton");
            
            // Save Preset Card
            SavePresetLabel.Text = SharedUtilities.GetTranslation(langDict, "PresetsPage_SavePresetLabel");
            PresetNameTextBox.PlaceholderText = SharedUtilities.GetTranslation(langDict, "PresetsPage_NewPreset_Placeholder");
            SavePresetButtonText.Text = SharedUtilities.GetTranslation(langDict, "PresetsPage_SavePresetButton");
            
            // Delete Preset Card
            DeletePresetLabel.Text = SharedUtilities.GetTranslation(langDict, "PresetsPage_DeletePresetLabel");
            DeletePresetDescription.Text = SharedUtilities.GetTranslation(langDict, "PresetsPage_DeletePresetDescription");
            DeletePresetButtonText.Text = SharedUtilities.GetTranslation(langDict, "PresetsPage_DeletePresetButton");
            
            // Preset Mods List
            PresetModsHeader.Text = SharedUtilities.GetTranslation(langDict, "PresetsPage_ModsInPreset");
        }

        private void EnsurePresetsDir()
        {
            if (!Directory.Exists(PresetsDir))
                Directory.CreateDirectory(PresetsDir);
        }

        private string GetPresetFileNameFromComboBox(object? item)
        {
            var langDict = SharedUtilities.LoadLanguageDictionary();
            if (item is string str && str == SharedUtilities.GetTranslation(langDict, "Default_Preset"))
                return "Default Preset";
            return item?.ToString() ?? string.Empty;
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
            // Przywróć wybrany preset z ustawień
            int selectedIndex = FlairX_Mod_Manager.SettingsManager.Current.SelectedPresetIndex;
            if (selectedIndex >= 0 && selectedIndex < PresetComboBox.Items.Count)
                PresetComboBox.SelectedIndex = selectedIndex;
            else
                PresetComboBox.SelectedIndex = 0;
                
            // Hide the mods section if default preset is selected (index 0)
            bool isDefaultPreset = PresetComboBox.SelectedIndex == 0;
            PresetModsCard.Visibility = isDefaultPreset ? Visibility.Collapsed : Visibility.Visible;
        }

        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PresetModsListView.Items.Clear();
            int selectedIndex = PresetComboBox.SelectedIndex;
            FlairX_Mod_Manager.SettingsManager.Current.SelectedPresetIndex = selectedIndex;
            FlairX_Mod_Manager.SettingsManager.Save();
            
            // Hide the mods section if default preset is selected (index 0)
            bool isDefaultPreset = selectedIndex == 0;
            PresetModsCard.Visibility = isDefaultPreset ? Visibility.Collapsed : Visibility.Visible;
            
            if (selectedIndex >= 0 && selectedIndex < _presetNames.Count && !isDefaultPreset)
            {
                var fileName = _presetNames[selectedIndex];
                var path = Path.Combine(PresetsDir, fileName + ".json");
                if (File.Exists(path))
                {
                    try
                    {
                        var json = Services.FileAccessQueue.ReadAllText(path);
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



        private async void LoadPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (PresetComboBox.SelectedItem is string presetName)
            {
                var fileName = GetPresetFileNameFromComboBox(presetName);
                try
                {
                    // Default Preset = deactivate all (respects shuffle-excluded categories)
                    if (fileName == "Default Preset")
                    {
                        if (App.Current is App appD && appD.MainWindow is MainWindow mwD)
                        {
                            mwD.ExecuteDeactivateAllModsHotkey();
                            var langDictD = SharedUtilities.LoadLanguageDictionary();
                            if (App.Current is App app2 && app2.MainWindow is MainWindow mw2)
                                mw2.ShowSuccessInfo(SharedUtilities.GetTranslation(langDictD, "Preset_Loaded"));
                        }
                        return;
                    }

                    FlairX_Mod_Manager.Pages.ModGridPage.ApplyPreset(fileName);
                    
                    // Reload manager to refresh the view
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                    {
                        await mainWindow.ReloadModsAsync();
                    }
                    
                    var langDict = SharedUtilities.LoadLanguageDictionary();
                    if (App.Current is App appN && appN.MainWindow is MainWindow mwN)
                        mwN.ShowSuccessInfo(SharedUtilities.GetTranslation(langDict, "Preset_Loaded"));
                }
                catch (Exception ex)
                {
                    if (App.Current is App appE && appE.MainWindow is MainWindow mwE)
                        mwE.ShowErrorInfo(ex.Message);
                }
            }
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
                
                // Get current active mods state, excluding shuffle-excluded categories
                var activeMods = new Dictionary<string, bool>();
                var gameTag = SettingsManager.CurrentSelectedGame ?? "";
                var excludedCategories = SettingsManager.GetShuffleExcludedCategories(gameTag);
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();

                if (!string.IsNullOrEmpty(modsPath) && Directory.Exists(modsPath))
                {
                    foreach (var categoryDir in Directory.GetDirectories(modsPath))
                    {
                        var categoryName = Path.GetFileName(categoryDir);
                        if (excludedCategories.Contains(categoryName, StringComparer.OrdinalIgnoreCase))
                            continue;

                        foreach (var modDir in Directory.GetDirectories(categoryDir))
                        {
                            var modFolderName = Path.GetFileName(modDir);
                            var cleanName = FlairX_Mod_Manager.Pages.ModGridPage.GetCleanModName(modFolderName);
                            activeMods[cleanName] = !modFolderName.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
                
                try
                {
                    // Save the current state to preset (includes both active=true and inactive=false mods)
                    var json = System.Text.Json.JsonSerializer.Serialize(activeMods, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    Services.FileAccessQueue.WriteAllText(presetPath, json);
                }
                catch (Exception ex)
                {
                    if (App.Current is App _a1 && _a1.MainWindow is MainWindow _mw1) _mw1.ShowErrorInfo(ex.Message);
                    return;
                }
                LoadPresetsToComboBox();
                var langDict = SharedUtilities.LoadLanguageDictionary();
                if (App.Current is App _a2 && _a2.MainWindow is MainWindow _mw2) _mw2.ShowSuccessInfo(SharedUtilities.GetTranslation(langDict, "Preset_Saved"));
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
                    if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowSuccessInfo(SharedUtilities.GetTranslation(langDict, "Preset_Deleted"));
                }
                catch (Exception ex)
                {
                    var langDict = SharedUtilities.LoadLanguageDictionary();
                    if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(ex.Message);
                }
            }
        }

        private List<string> GetCurrentActiveMods()
        {
            var activeMods = new List<string>();
            
            try
            {
                // Use game-specific ActiveMods file name
                var activeModsFileName = AppConstants.GameConfig.GetActiveModsFilename(SettingsManager.CurrentSelectedGame);
                var activeModsPath = PathManager.GetSettingsPath(activeModsFileName);
                
                if (File.Exists(activeModsPath))
                {
                    var json = Services.FileAccessQueue.ReadAllText(activeModsPath);
                    var currentMods = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new Dictionary<string, bool>();
                    foreach (var kv in currentMods)
                    {
                        string modName = Path.GetFileName(kv.Key);
                        if (kv.Value) // Only add active mods
                        {
                            activeMods.Add(modName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error getting current active mods", ex);
            }
            
            return activeMods;
        }

        private async Task ShowDialog(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private async Task<bool> ShowConfirmDialog(string message)
        {
            var langDict = SharedUtilities.LoadLanguageDictionary();
            var dialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(langDict, "Confirm_Title"),
                Content = message,
                PrimaryButtonText = SharedUtilities.GetTranslation(langDict, "Yes"),
                SecondaryButtonText = SharedUtilities.GetTranslation(langDict, "No"),
                DefaultButton = ContentDialogButton.Secondary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        public void CreateDefaultPresetAllInactive_Click(object sender, RoutedEventArgs e)
        {
            // Create a new instance of ModGridPage to call the method
            // This follows the pattern used in other parts of the application
            var gridPage = new FlairX_Mod_Manager.Pages.ModGridPage();
            gridPage.SaveDefaultPresetAllInactive();
            LoadPresetsToComboBox();
        }

        public void RefreshContent()
        {
            UpdateTexts();
            LoadPresetsToComboBox();
        }

        private void PresetModsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is string modName)
            {
                // Navigate to mod details WITHOUT closing the presets panel
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.NavigateToModDetails(modName);
                }
            }
        }
    }
}