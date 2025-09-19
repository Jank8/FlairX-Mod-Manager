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
            // Animate content sliding in from right with fade
            var slideTransform = new Microsoft.UI.Xaml.Media.TranslateTransform();
            MainGrid.RenderTransform = slideTransform;
            
            // Start off-screen to the right and invisible
            slideTransform.X = 300;
            MainGrid.Opacity = 0;
            
            var slideAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 300,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };
            
            var fadeAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };
            
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideAnimation, slideTransform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideAnimation, "X");
            
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeAnimation, MainGrid);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeAnimation, "Opacity");
            
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(slideAnimation);
            storyboard.Children.Add(fadeAnimation);
            storyboard.Begin();
            
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
            PresetComboBox.PlaceholderText = SharedUtilities.GetTranslation(langDict, "PresetsPage_ComboBox_Placeholder");
            PresetNameTextBox.PlaceholderText = SharedUtilities.GetTranslation(langDict, "PresetsPage_NewPreset_Placeholder");
            SavePresetButtonText.Text = SharedUtilities.GetTranslation(langDict, "PresetsPage_SavePresetButton");
            LoadPresetButtonText.Text = SharedUtilities.GetTranslation(langDict, "PresetsPage_LoadPresetButton");
            DeletePresetButtonText.Text = SharedUtilities.GetTranslation(langDict, "PresetsPage_DeletePresetButton");
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
                    var json = File.ReadAllText(activeModsPath);
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
    }
}