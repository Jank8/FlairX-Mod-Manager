using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Runtime.InteropServices;
using System.Threading;
using FlairX_Mod_Manager;
using FlairX_Mod_Manager.Models;
using FlairX_Mod_Manager.Services;
using XamlImage = Microsoft.UI.Xaml.Controls.Image;
using SharpImage = SixLabors.ImageSharp.Image;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class SettingsUserControl
    {

        private void LoadLanguages()
        {
            try
            {
                LanguageComboBox.Items.Clear();
                _languages.Clear();
                _fileNameByDisplayName.Clear();
                
                // Remove AUTO option - auto-detection happens automatically on first app start
                
                if (Directory.Exists(LanguageFolderPath))
                {
                    var languageList = new List<(string displayName, string filePath)>();
                    
                    var files = Directory.GetFiles(LanguageFolderPath, "*.json");
                    foreach (var file in files)
                    {
                        string displayName = System.IO.Path.GetFileNameWithoutExtension(file);
                        try
                        {
                            // Use faster file reading with smaller buffer for language files
                            var json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                            if (dict != null && dict.TryGetValue("Language_DisplayName", out var langName) && !string.IsNullOrWhiteSpace(langName))
                            {
                                displayName = langName;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to parse language file: {file}", ex);
                            // Continue with filename as display name
                        }
                        
                        languageList.Add((displayName, file));
                    }
                    
                    // Get current language file to prioritize it
                    string? currentLanguageFile = SettingsManager.Current.LanguageFile;
                    
                    // Sort: current language first, then alphabetically
                    var sortedLanguages = languageList.OrderBy(lang =>
                    {
                        var fileName = Path.GetFileName(lang.filePath);
                        // Current language gets priority 0, others get priority 1
                        return fileName == currentLanguageFile ? 0 : 1;
                    })
                    .ThenBy(lang => lang.displayName)
                    .ToList();
                    
                    // Add sorted languages to ComboBox
                    foreach (var (displayName, filePath) in sortedLanguages)
                    {
                        LanguageComboBox.Items.Add(displayName);
                        _languages[displayName] = filePath;
                        _fileNameByDisplayName[displayName] = filePath;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load languages", ex);
                // Ensure at least one language is available
                if (LanguageComboBox.Items.Count == 0)
                {
                    LanguageComboBox.Items.Add("English");
                    _languages["English"] = "en.json";
                    _fileNameByDisplayName["English"] = "en.json";
                }
            }
        }



        // Event handlers - copying from original SettingsPage
        private async void ThemeSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            try
            {
                Logger.LogInfo("ThemeSelectorBar_SelectionChanged called");

                if (sender.SelectedItem is SelectorBarItem item && item.Tag is string theme)
                {
                    Logger.LogInfo($"Theme selected: {theme}");
                    // Get current effective theme before change
                    ElementTheme currentEffectiveTheme = GetEffectiveTheme(SettingsManager.Current.Theme ?? "Auto");

                    SettingsManager.Current.Theme = theme;
                    SettingsManager.Save();

                    // Set application theme first
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.ApplyThemeToTitleBar(theme);
                        if (mainWindow.Content is FrameworkElement root)
                        {
                            if (theme == "Light")
                                root.RequestedTheme = ElementTheme.Light;
                            else if (theme == "Dark")
                                root.RequestedTheme = ElementTheme.Dark;
                            else
                                root.RequestedTheme = ElementTheme.Default;

                            // Small delay to let the theme change take effect
                            await Task.Delay(10);

                            // Now get the new effective theme after the change
                            ElementTheme newEffectiveTheme = GetEffectiveThemeAfterChange(theme, root);

                            // Refresh backdrop effect if using "None" to update background color
                            string currentBackdrop = SettingsManager.Current.BackdropEffect ?? "AcrylicThin";
                            if (currentBackdrop == "None")
                            {
                                mainWindow.ApplyBackdropEffect("None");
                            }

                            // Notify other windows about theme change
                            WindowStyleHelper.NotifySettingsChanged();

                            // Only update panel theme if the effective theme actually changed
                            if (currentEffectiveTheme != newEffectiveTheme)
                            {
                                Logger.LogInfo($"Theme changed from {currentEffectiveTheme} to {newEffectiveTheme}");

                                // Small delay to ensure theme change is fully applied
                                await Task.Delay(100);

                                // Update the sliding panel background instead of closing/reopening
                                Logger.LogInfo("Calling UpdateSlidingPanelTheme");
                                mainWindow.UpdateSlidingPanelTheme();
                            }
                            else
                            {
                                Logger.LogInfo($"Theme not changed - current: {currentEffectiveTheme}, new: {newEffectiveTheme}");
                            }
                        }
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("SettingsUserControl.ThemeSelectorBar_SelectionChanged failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("SettingsUserControl.ThemeSelectorBar_SelectionChanged failed", ex);
            }
        }
        
        private ElementTheme GetEffectiveThemeAfterChange(string themeSetting, FrameworkElement root)
        {
            if (themeSetting == "Light")
                return ElementTheme.Light;
            else if (themeSetting == "Dark")
                return ElementTheme.Dark;
            else // "Auto"
            {
                // For Auto, get the actual theme that will be applied
                return root.ActualTheme == ElementTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
            }
        }
        
        private ElementTheme GetEffectiveTheme(string themeSetting)
        {
            if (themeSetting == "Light")
                return ElementTheme.Light;
            else if (themeSetting == "Dark")
                return ElementTheme.Dark;
            else // "Auto"
            {
                // For Auto, determine based on current system/app theme
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    if (mainWindow.Content is FrameworkElement root)
                    {
                        // Get the actual current theme being displayed
                        return root.ActualTheme == ElementTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                    }
                }
                // Fallback to Light if we can't determine
                return ElementTheme.Light;
            }
        }

        private void BackdropSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            if (sender.SelectedItem is SelectorBarItem item && item.Tag is string backdrop)
            {
                SettingsManager.Current.BackdropEffect = backdrop;
                SettingsManager.Save();
                
                // Apply backdrop change
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.ApplyBackdropEffect(backdrop);
                    
                    // Notify other windows about settings change
                    WindowStyleHelper.NotifySettingsChanged();
                }
            }
        }

        private void PreviewEffectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PreviewEffectComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string effect)
            {
                SettingsManager.Current.PreviewEffect = effect;
                SettingsManager.Save();
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is string displayName && _fileNameByDisplayName.TryGetValue(displayName, out var filePath))
            {
                var fileName = Path.GetFileName(filePath);
                
                // IMPORTANT: Save to SettingsManager FIRST before loading language
                SettingsManager.Current.LanguageFile = fileName;
                SettingsManager.Save();
                
                // Update texts locally first to avoid flicker
                UpdateTexts();
                
                // Refresh the entire UI in MainWindow without re-navigating to settings
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.RefreshUIAfterLanguageChange();
                    // No need to navigate back to SettingsPage - we're already here and updated
                }
            }
        }
    }
}
