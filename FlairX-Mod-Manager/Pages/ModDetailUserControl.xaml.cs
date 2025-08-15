using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModDetailUserControl : UserControl
    {
        public class HotkeyDisplay
        {
            public string Key { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        private string? _modJsonPath;
        private string? _categoryParam;
        private string? _viewModeParam;
        public event EventHandler? CloseRequested; // Event to notify parent to close

        public ModDetailUserControl()
        {
            this.InitializeComponent();
            this.Loaded += ModDetailUserControl_Loaded;
            this.ActualThemeChanged += ModDetailUserControl_ActualThemeChanged;
        }
        
        public void LoadModDetails(string modDirectory, string category = "", string viewMode = "")
        {
            _categoryParam = category;
            _viewModeParam = viewMode;
            
            try
            {
                // Load language translations and set labels
                var lang = SharedUtilities.LoadLanguageDictionary();
                ModDateCheckedLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_DateChecked");
                ModDateUpdatedLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_DateUpdated");
                ModAuthorLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_Author");
                ModHotkeysLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_Hotkeys");
                ModUrlLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_URL");

                // Set tooltip for OpenUrlButton
                ToolTipService.SetToolTip(OpenUrlButton, SharedUtilities.GetTranslation(lang, "ModDetailPage_OpenURL_Tooltip"));

                string modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                {
                    modLibraryPath = PathManager.GetModLibraryPath();
                }

                System.Diagnostics.Debug.WriteLine($"ModDetailUserControl: Using mod library path: {modLibraryPath}");
                System.Diagnostics.Debug.WriteLine($"ModDetailUserControl: Current game: {SettingsManager.CurrentSelectedGame}");
                System.Diagnostics.Debug.WriteLine($"ModDetailUserControl: Requested mod directory: {modDirectory}");

                if (!string.IsNullOrEmpty(modDirectory) && !string.IsNullOrEmpty(modLibraryPath))
                {
                    string? fullModDir = null;
                    
                    if (Path.IsPathRooted(modDirectory))
                    {
                        fullModDir = modDirectory;
                    }
                    else
                    {
                        // Find the mod in the category-based structure
                        System.Diagnostics.Debug.WriteLine($"ModDetailUserControl: Searching for mod '{modDirectory}' in library: {modLibraryPath}");
                        fullModDir = FindModFolderPath(modLibraryPath, modDirectory);
                        System.Diagnostics.Debug.WriteLine($"ModDetailUserControl: FindModFolderPath returned: {fullModDir ?? "null"}");
                    }
                    
                    if (!string.IsNullOrEmpty(fullModDir) && Directory.Exists(fullModDir))
                    {
                        string modName = Path.GetFileName(fullModDir);
                        ModDetailTitle.Text = $"Mod Details - {modName}";
                        _modJsonPath = Path.Combine(fullModDir, "mod.json");
                        
                        System.Diagnostics.Debug.WriteLine($"ModDetailUserControl: Found mod directory: {fullModDir}");
                        System.Diagnostics.Debug.WriteLine($"ModDetailUserControl: Looking for mod.json at: {_modJsonPath}");
                        
                        // Load mod.json data
                        LoadModJsonData();
                        
                        // Load preview image
                        LoadPreviewImage(fullModDir);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not find mod directory: {modDirectory} in library: {modLibraryPath}");
                        System.Diagnostics.Debug.WriteLine($"Available categories in library:");
                        try
                        {
                            foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
                            {
                                System.Diagnostics.Debug.WriteLine($"  - {Path.GetFileName(categoryDir)}");
                                var modsInCategory = Directory.GetDirectories(categoryDir);
                                foreach (var modInCategory in modsInCategory)
                                {
                                    System.Diagnostics.Debug.WriteLine($"    - {Path.GetFileName(modInCategory)}");
                                }
                            }
                        }
                        catch (Exception debugEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error listing directories: {debugEx.Message}");
                        }
                        
                        // Mod not found - this shouldn't happen anymore since we check before opening
                        System.Diagnostics.Debug.WriteLine($"Mod directory not found: {modDirectory}");
                        SetDefaultValues();
                        return;
                    }
                }
                else
                {
                    SetDefaultValues();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading mod details: {ex.Message}");
                Logger.LogError($"Error loading mod details for {modDirectory}", ex);
                SetDefaultValues();
            }
        }

        private string? FindModFolderPath(string modLibraryDir, string modDirectoryName)
        {
            try
            {
                // Search through all category directories to find the mod
                foreach (var categoryDir in Directory.GetDirectories(modLibraryDir))
                {
                    var modPath = Path.Combine(categoryDir, modDirectoryName);
                    if (Directory.Exists(modPath))
                    {
                        return Path.GetFullPath(modPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error finding mod folder path for {modDirectoryName}", ex);
            }
            
            return null;
        }

        private void LoadModJsonData()
        {
            if (string.IsNullOrEmpty(_modJsonPath) || !File.Exists(_modJsonPath))
            {
                SetDefaultValues();
                return;
            }

            try
            {
                var json = File.ReadAllText(_modJsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                string author = root.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? "" : "";
                string url = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                string version = root.TryGetProperty("version", out var versionProp) ? versionProp.GetString() ?? "" : "";
                
                // Load date fields
                if (root.TryGetProperty("dateChecked", out var dateCheckedProp) && 
                    !string.IsNullOrEmpty(dateCheckedProp.GetString()) && 
                    DateTime.TryParse(dateCheckedProp.GetString(), out var dateChecked))
                {
                    ModDateCheckedPicker.Date = new DateTimeOffset(dateChecked);
                }
                else
                {
                    ModDateCheckedPicker.Date = null;
                }
                
                if (root.TryGetProperty("dateUpdated", out var dateUpdatedProp) && 
                    !string.IsNullOrEmpty(dateUpdatedProp.GetString()) && 
                    DateTime.TryParse(dateUpdatedProp.GetString(), out var dateUpdated))
                {
                    ModDateUpdatedPicker.Date = new DateTimeOffset(dateUpdated);
                }
                else
                {
                    ModDateUpdatedPicker.Date = null;
                }
                
                ModAuthorTextBox.Text = author;
                ModUrlTextBox.Text = url;
                ModVersionTextBox.Text = version;
                
                // Load hotkeys
                if (root.TryGetProperty("hotkeys", out var hotkeysProp) && hotkeysProp.ValueKind == JsonValueKind.Array)
                {
                    var hotkeyList = new List<HotkeyDisplay>();
                    foreach (var hotkey in hotkeysProp.EnumerateArray())
                    {
                        if (hotkey.ValueKind == JsonValueKind.Object)
                        {
                            var key = hotkey.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : null;
                            var desc = hotkey.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(desc))
                                hotkeyList.Add(new HotkeyDisplay { Key = key, Description = desc });
                            else if (!string.IsNullOrWhiteSpace(key))
                                hotkeyList.Add(new HotkeyDisplay { Key = key, Description = string.Empty });
                        }
                        else if (hotkey.ValueKind == JsonValueKind.String)
                        {
                            var keyStr = hotkey.GetString() ?? "";
                            hotkeyList.Add(new HotkeyDisplay { Key = keyStr, Description = string.Empty });
                        }
                    }
                    ModHotkeysList.ItemsSource = hotkeyList;
                }
                else
                {
                    ModHotkeysList.ItemsSource = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error parsing mod.json at {_modJsonPath}", ex);
                SetDefaultValues();
            }
        }

        private void LoadPreviewImage(string fullModDir)
        {
            try
            {
                var previewPathJpg = Path.Combine(fullModDir, "preview.jpg");
                if (File.Exists(previewPathJpg))
                {
                    var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    byte[] imageData = File.ReadAllBytes(previewPathJpg);
                    using (var memStream = new MemoryStream(imageData))
                    {
                        bitmap.SetSource(memStream.AsRandomAccessStream());
                    }
                    ModImage.Source = bitmap;
                }
                else
                {
                    ModImage.Source = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading preview image from {fullModDir}", ex);
                ModImage.Source = null;
            }
        }



        private void SetDefaultValues()
        {
            ModImage.Source = null;
            ModHotkeysList.ItemsSource = null;
            ModAuthorTextBox.Text = "";
            ModUrlTextBox.Text = "";
            ModVersionTextBox.Text = "";
            ModDateCheckedPicker.Date = null;
            ModDateUpdatedPicker.Date = null;
        }

        private void ModDetailUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateBorderColor();
        }

        private void ModDetailUserControl_ActualThemeChanged(FrameworkElement sender, object args)
        {
            UpdateBorderColor();
        }

        private void UpdateBorderColor()
        {
            if (ModImageBorder != null)
            {
                var currentTheme = this.ActualTheme;
                if (currentTheme == ElementTheme.Dark)
                {
                    ModImageBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 70, 70, 70));
                }
                else
                {
                    ModImageBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 200, 200, 200));
                }
            }
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

        private void MainGrid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Handle pointer events if needed
        }
        
        private void MainGrid_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Handle pointer events if needed
        }
        
        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_modJsonPath))
                {
                    var modDir = Path.GetDirectoryName(_modJsonPath);
                    if (!string.IsNullOrEmpty(modDir) && Directory.Exists(modDir))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", modDir);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error opening mod folder", ex);
            }
        }

        
        private async void ModVersionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            await UpdateModJsonField("version", ModVersionTextBox.Text);
        }

        private async void ModAuthorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            await UpdateModJsonField("author", ModAuthorTextBox.Text);
        }

        private async void ModUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            await UpdateModJsonField("url", ModUrlTextBox.Text);
        }

        private async void ModDateCheckedPicker_DateChanged(object sender, Microsoft.UI.Xaml.Controls.CalendarDatePickerDateChangedEventArgs e)
        {
            var dateValue = e.NewDate?.ToString("yyyy-MM-dd") ?? "";
            await UpdateModJsonField("dateChecked", dateValue);
        }

        private async void ModDateUpdatedPicker_DateChanged(object sender, Microsoft.UI.Xaml.Controls.CalendarDatePickerDateChangedEventArgs e)
        {
            var dateValue = e.NewDate?.ToString("yyyy-MM-dd") ?? "";
            await UpdateModJsonField("dateUpdated", dateValue);
        }

        private void OpenUrlButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = ModUrlTextBox.Text;
                if (!string.IsNullOrEmpty(url))
                {
                    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                        url = "https://" + url;
                    
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error opening URL", ex);
            }
        }

        private async Task UpdateModJsonField(string field, string value)
        {
            if (string.IsNullOrEmpty(_modJsonPath)) return;
            if (!File.Exists(_modJsonPath))
            {
                // If mod.json doesn't exist, ensure default mod.json is created first
                await Task.Run(() => (App.Current as FlairX_Mod_Manager.App)?.EnsureModJsonInModLibrary());
                
                // Check if mod.json was created successfully
                if (!File.Exists(_modJsonPath))
                {
                    Logger.LogError($"Failed to create default mod.json at: {_modJsonPath}");
                    return;
                }
            }
            try
            {
                var json = File.ReadAllText(_modJsonPath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var dict = new Dictionary<string, object?>();
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Name == field)
                        dict[field] = value;
                    else if (prop.Name == "author" || prop.Name == "url" || prop.Name == "version" || prop.Name == "dateChecked" || prop.Name == "dateUpdated")
                        dict[prop.Name] = prop.Value.GetString();
                    else
                        dict[prop.Name] = prop.Value.Deserialize<object>();
                }
                if (!dict.ContainsKey(field))
                    dict[field] = value;
                var newJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_modJsonPath, newJson);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save mod details", ex);
            }
        }

        private void OpenFolderButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.1,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };

            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, OpenFolderIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.1,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };

            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, OpenFolderIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");

            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(scaleAnimation);
            storyboard.Children.Add(scaleAnimationY);
            storyboard.Begin();
        }

        private void OpenFolderButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };

            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, OpenFolderIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };

            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, OpenFolderIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");

            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(scaleAnimation);
            storyboard.Children.Add(scaleAnimationY);
            storyboard.Begin();
        }


    }
}