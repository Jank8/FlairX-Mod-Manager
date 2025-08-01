using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System;

namespace ZZZ_Mod_Manager_X.Pages
{
    public sealed partial class ModDetailPage : Page
    {
        public class HotkeyDisplay
        {
            public string Key { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        private string? _modJsonPath;

        private List<string> _allModDirs = new List<string>();
        private int _currentModIndex = -1;

        private string? _categoryParam;

        public ModDetailPage()
        {
            this.InitializeComponent();
            this.Loaded += ModDetailPage_Loaded;
            this.ActualThemeChanged += ModDetailPage_ActualThemeChanged;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Set translations for labels
            ModDateCheckedLabel.Text = LanguageManager.Instance.T("ModDetailPage_DateChecked");
            ModDateUpdatedLabel.Text = LanguageManager.Instance.T("ModDetailPage_DateUpdated");
            ModAuthorLabel.Text = LanguageManager.Instance.T("ModDetailPage_Author");
            ModCharacterLabel.Text = LanguageManager.Instance.T("ModDetailPage_Character");
            ModHotkeysLabel.Text = LanguageManager.Instance.T("ModDetailPage_Hotkeys");
            ModUrlLabel.Text = LanguageManager.Instance.T("ModDetailPage_URL");
            
            // Set tooltip for OpenUrlButton
            ToolTipService.SetToolTip(OpenUrlButton, LanguageManager.Instance.T("ModDetailPage_OpenURL_Tooltip"));
            string modName = "";
            string? modDir = null;
            if (e.Parameter is ModDetailNav nav)
            {
                modDir = nav.ModDirectory;
                _categoryParam = nav.Category;
            }
            else if (e.Parameter is string modDirStr)
            {
                modDir = modDirStr;
                _categoryParam = null;
            }
            string modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? string.Empty;
            if (string.IsNullOrEmpty(modLibraryPath))
            {
                modLibraryPath = Path.Combine(System.AppContext.BaseDirectory, "ModLibrary");
            }
            
            if (Directory.Exists(modLibraryPath))
            {
                try
                {
                    _allModDirs = Directory.GetDirectories(modLibraryPath).Select(Path.GetFileName).Where(x => x != null).Select(x => x!).OrderBy(x => x).ToList();
                    if (modDir != null)
                    {
                        _currentModIndex = _allModDirs.FindIndex(x => x == modDir);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading mod directories: {ex.Message}");
                    _allModDirs = new List<string>();
                    _currentModIndex = -1;
                }
            }
            else
            {
                _allModDirs = new List<string>();
                _currentModIndex = -1;
            }
            if (modDir != null && !string.IsNullOrEmpty(modLibraryPath))
            {
                try
                {
                    string fullModDir = Path.IsPathRooted(modDir) ? modDir : Path.Combine(modLibraryPath, modDir);
                    if (Directory.Exists(fullModDir))
                    {
                        modName = Path.GetFileName(fullModDir);
                        _modJsonPath = Path.Combine(fullModDir, "mod.json");
                        if (File.Exists(_modJsonPath))
                    {
                        var json = File.ReadAllText(_modJsonPath);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        string author = root.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? "" : "";
                        string character = root.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "" : "";
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
                        ModCharacterTextBox.Text = character;
                        ModUrlTextBox.Text = url;
                        ModVersionTextBox.Text = version;
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
                        ModImage.Source = null;
                }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading mod details: {ex.Message}");
                    // Set default values on error
                    ModImage.Source = null;
                    ModHotkeysList.ItemsSource = null;
                }
            }
            if (string.IsNullOrWhiteSpace(modName))
                ModDetailTitle.Text = LanguageManager.Instance.T("ModDetailPage_Title");
            else
                ModDetailTitle.Text = modName;
            UpdateNavButtons();
        }

        private void UpdateNavButtons()
        {
            PrevModButton.IsEnabled = _allModDirs != null && _currentModIndex > 0;
            NextModButton.IsEnabled = _allModDirs != null && _currentModIndex < (_allModDirs?.Count ?? 1) - 1 && _currentModIndex >= 0;
        }

        private void ModDetailPage_ActualThemeChanged(FrameworkElement sender, object args)
        {
            UpdateBorderColor();
        }

        private void ModDetailPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateBorderColor();
        }

        private void UpdateBorderColor()
        {
            if (ModImageBorder != null)
            {
                var theme = this.ActualTheme;
                if (theme == ElementTheme.Dark)
                {
                    ModImageBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 34, 34, 34));
                }
                else
                {
                    ModImageBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_categoryParam))
            {
                Frame.Navigate(typeof(ModGridPage), _categoryParam);
            }
            else
            {
                Frame.Navigate(typeof(ModGridPage), null);
            }
        }

        private void ModAuthorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateModJsonField("author", ModAuthorTextBox.Text);
        }
        private void ModCharacterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateModJsonField("character", ModCharacterTextBox.Text);
        }
        private void ModUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateModJsonField("url", ModUrlTextBox.Text);
        }

        private void ModVersionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateModJsonField("version", ModVersionTextBox.Text);
        }

        private void ModDateCheckedPicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            var dateValue = args.NewDate?.ToString("yyyy-MM-dd") ?? "";
            UpdateModJsonField("dateChecked", dateValue);
        }

        private void ModDateUpdatedPicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            var dateValue = args.NewDate?.ToString("yyyy-MM-dd") ?? "";
            UpdateModJsonField("dateUpdated", dateValue);
        }

        private void OpenUrlButton_Click(object sender, RoutedEventArgs e)
        {
            var url = ModUrlTextBox.Text;
            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    var uri = new System.Uri(url);
                    var ignored = Windows.System.Launcher.LaunchUriAsync(uri);
                }
                catch { }
            }
        }

        private void UpdateModJsonField(string field, string value)
        {
            if (string.IsNullOrEmpty(_modJsonPath)) return;
            if (!File.Exists(_modJsonPath))
            {
                var dict = new Dictionary<string, object?>
                {
                    {"author", ""},
                    {"character", ""},
                    {"url", ""},
                    {"version", ""},
                    {"dateChecked", ""},
                    {"dateUpdated", ""},
                    {"hotkeys", new List<object>()}
                };
                dict[field] = value;
                var newJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_modJsonPath, newJson);
                return;
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
                    else if (prop.Name == "author" || prop.Name == "character" || prop.Name == "url" || prop.Name == "version" || prop.Name == "dateChecked" || prop.Name == "dateUpdated")
                        dict[prop.Name] = prop.Value.GetString();
                    else
                        dict[prop.Name] = prop.Value.Deserialize<object>();
                }
                if (!dict.ContainsKey(field))
                    dict[field] = value;
                var newJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_modJsonPath, newJson);
            }
            catch { }
        }

        private void PrevModButton_Click(object sender, RoutedEventArgs e)
        {
            if (_allModDirs != null && _currentModIndex > 0)
            {
                Frame.Navigate(typeof(ModDetailPage), _allModDirs[_currentModIndex - 1]);
            }
        }

        private void NextModButton_Click(object sender, RoutedEventArgs e)
        {
            if (_allModDirs != null && _currentModIndex < _allModDirs.Count - 1 && _currentModIndex >= 0)
            {
                Frame.Navigate(typeof(ModDetailPage), _allModDirs[_currentModIndex + 1]);
            }
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

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_modJsonPath))
            {
                var modDirectory = Path.GetDirectoryName(_modJsonPath);
                if (!string.IsNullOrEmpty(modDirectory) && Directory.Exists(modDirectory))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = modDirectory,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            }
        }

        private void OpenFolderButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
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
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, OpenFolderIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
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
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
            };
            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, OpenFolderIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, OpenFolderIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(scaleAnimation);
            storyboard.Children.Add(scaleAnimationY);
            storyboard.Begin();
        }

        public class ModDetailNav
        {
            public string ModDirectory { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
        }
    }
}
