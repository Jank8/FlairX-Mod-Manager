using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
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
        private string? _viewModeParam;

        public ModDetailPage()
        {
            this.InitializeComponent();
            this.Loaded += ModDetailPage_Loaded;
            this.ActualThemeChanged += ModDetailPage_ActualThemeChanged;
            
            // Try multiple event types for maximum reliability
            this.AddHandler(PointerPressedEvent, new PointerEventHandler(ModDetailPage_PointerPressed), handledEventsToo: true);
            this.AddHandler(PointerReleasedEvent, new PointerEventHandler(ModDetailPage_PointerReleased), handledEventsToo: true);
            
            // Also add to the main grid after it's loaded
            this.Loaded += (s, e) => 
            {
                if (MainGrid != null)
                {
                    MainGrid.AddHandler(PointerPressedEvent, new PointerEventHandler(MainGrid_PointerPressed), handledEventsToo: true);
                    MainGrid.AddHandler(PointerReleasedEvent, new PointerEventHandler(MainGrid_PointerReleased), handledEventsToo: true);
                }
            };
        }

        /// <summary>
        /// Finds the full path to a mod folder in the category-based structure
        /// </summary>
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

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            // Load language translations and set labels
            var lang = SharedUtilities.LoadLanguageDictionary();
            ModDateCheckedLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_DateChecked");
            ModDateUpdatedLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_DateUpdated");
            ModAuthorLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_Author");

            ModHotkeysLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_Hotkeys");
            ModUrlLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_URL");
            
            // Set tooltip for OpenUrlButton
            ToolTipService.SetToolTip(OpenUrlButton, SharedUtilities.GetTranslation(lang, "ModDetailPage_OpenURL_Tooltip"));
            
            string modName = "";
            string? modDir = null;
            if (e.Parameter is ModDetailNav nav)
            {
                modDir = nav.ModDirectory;
                _categoryParam = nav.Category;
                _viewModeParam = nav.ViewMode;
            }
            else if (e.Parameter is string modDirStr)
            {
                modDir = modDirStr;
                _categoryParam = null;
            }
            string modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
            if (string.IsNullOrEmpty(modLibraryPath))
            {
                modLibraryPath = PathManager.GetModLibraryPath();
            }
            
            System.Diagnostics.Debug.WriteLine($"ModDetailPage: Using mod library path: {modLibraryPath}");
            System.Diagnostics.Debug.WriteLine($"ModDetailPage: Current game: {SettingsManager.CurrentSelectedGame}");
            System.Diagnostics.Debug.WriteLine($"ModDetailPage: Requested mod directory: {modDir}");
            
            if (Directory.Exists(modLibraryPath))
            {
                try
                {
                    // Load all mod directories from all categories
                    _allModDirs = new List<string>();
                    foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
                    {
                        if (Directory.Exists(categoryDir))
                        {
                            var modDirs = Directory.GetDirectories(categoryDir)
                                .Select(Path.GetFileName)
                                .Where(x => x != null)
                                .Select(x => x!);
                            _allModDirs.AddRange(modDirs);
                        }
                    }
                    _allModDirs = _allModDirs.OrderBy(x => x).ToList();
                    
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
                    string? fullModDir = null;
                    
                    if (Path.IsPathRooted(modDir))
                    {
                        fullModDir = modDir;
                    }
                    else
                    {
                        // Find the mod in the category-based structure
                        System.Diagnostics.Debug.WriteLine($"ModDetailPage: Searching for mod '{modDir}' in library: {modLibraryPath}");
                        fullModDir = FindModFolderPath(modLibraryPath, modDir);
                        System.Diagnostics.Debug.WriteLine($"ModDetailPage: FindModFolderPath returned: {fullModDir ?? "null"}");
                    }
                    
                    if (!string.IsNullOrEmpty(fullModDir) && Directory.Exists(fullModDir))
                    {
                        modName = Path.GetFileName(fullModDir);
                        _modJsonPath = Path.Combine(fullModDir, "mod.json");
                        
                        System.Diagnostics.Debug.WriteLine($"ModDetailPage: Found mod directory: {fullModDir}");
                        System.Diagnostics.Debug.WriteLine($"ModDetailPage: Looking for mod.json at: {_modJsonPath}");
                        
                        if (File.Exists(_modJsonPath))
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
                    else
                    {
                        // mod.json doesn't exist - clear all fields and disable editing
                        ModAuthorTextBox.Text = "";
                        ModUrlTextBox.Text = "";
                        ModVersionTextBox.Text = "";
                        ModDateCheckedPicker.Date = null;
                        ModDateUpdatedPicker.Date = null;
                        ModHotkeysList.ItemsSource = null;
                        
                        System.Diagnostics.Debug.WriteLine($"ModDetailPage: mod.json not found at {_modJsonPath}");
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
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not find mod directory: {modDir} in library: {modLibraryPath}");
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
                        
                        // Set default values when mod not found
                        ModImage.Source = null;
                        ModHotkeysList.ItemsSource = null;
                        ModAuthorTextBox.Text = "";
                        ModUrlTextBox.Text = "";
                        ModVersionTextBox.Text = "";
                        ModDateCheckedPicker.Date = null;
                        ModDateUpdatedPicker.Date = null;
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
            {
                var lang2 = SharedUtilities.LoadLanguageDictionary();
                ModDetailTitle.Text = SharedUtilities.GetTranslation(lang2, "ModDetailPage_Title");
            }
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

        private void ModDetailPage_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Check if it's the back button on the mouse (XButton1)
            if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
            {
                var properties = e.GetCurrentPoint(this).Properties;
                if (properties.IsXButton1Pressed)
                {
                    // Use dispatcher to ensure it runs on UI thread and doesn't get blocked
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
                    {
                        BackButton_Click(this, new RoutedEventArgs());
                    });
                    e.Handled = true;
                }
            }
        }

        private void ModDetailPage_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Try handling on pointer released instead of pressed
            if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
            {
                var properties = e.GetCurrentPoint(this).Properties;
                if (properties.IsXButton1Pressed)
                {
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
                    {
                        BackButton_Click(this, new RoutedEventArgs());
                    });
                    e.Handled = true;
                }
            }
        }

        private void MainGrid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Additional handler on the main grid to catch mouse back button
            if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
            {
                var properties = e.GetCurrentPoint(MainGrid).Properties;
                if (properties.IsXButton1Pressed)
                {
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
                    {
                        BackButton_Click(this, new RoutedEventArgs());
                    });
                    e.Handled = true;
                }
            }
        }

        private void MainGrid_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Additional handler on the main grid to catch mouse back button on release
            if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
            {
                var properties = e.GetCurrentPoint(MainGrid).Properties;
                if (properties.IsXButton1Pressed)
                {
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
                    {
                        BackButton_Click(this, new RoutedEventArgs());
                    });
                    e.Handled = true;
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // FUCK NAVIGATION PARAMETERS - DIRECTLY CALL THE RIGHT METHOD
            
            // Navigate to ModGridPage first
            Frame.Navigate(typeof(ModGridPage));
            
            // Then directly call the correct method after navigation
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                if (Frame.Content is ModGridPage modGridPage)
                {
                    if (_viewModeParam == "Categories")
                    {
                        // CATEGORY MODE: ALWAYS go back to the specific category
                        if (!string.IsNullOrEmpty(_categoryParam))
                        {
                            modGridPage.LoadCategoryInCategoryMode(_categoryParam);
                        }
                        else
                        {
                            modGridPage.LoadAllCategories();
                        }
                    }
                    else
                    {
                        // DEFAULT MODE: Use default mode method
                        if (!string.IsNullOrEmpty(_categoryParam))
                        {
                            modGridPage.LoadCategoryInDefaultMode(_categoryParam);
                        }
                        else
                        {
                            modGridPage.LoadAllModsPublic();
                        }
                    }
                }
            });
        }

        private void ModAuthorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateModJsonField("author", ModAuthorTextBox.Text);
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
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to launch URL: {url}", ex);
                }
            }
        }

        private void UpdateModJsonField(string field, string value)
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

        private void PrevModButton_Click(object sender, RoutedEventArgs e)
        {
            if (_allModDirs != null && _currentModIndex > 0)
            {
                var prevModDir = _allModDirs[_currentModIndex - 1];
                var navParam = new ModDetailNav
                {
                    ModDirectory = prevModDir,
                    Category = _categoryParam ?? string.Empty,
                    ViewMode = _viewModeParam ?? string.Empty
                };
                Frame.Navigate(typeof(ModDetailPage), navParam);
            }
        }

        private void NextModButton_Click(object sender, RoutedEventArgs e)
        {
            if (_allModDirs != null && _currentModIndex < _allModDirs.Count - 1 && _currentModIndex >= 0)
            {
                var nextModDir = _allModDirs[_currentModIndex + 1];
                var navParam = new ModDetailNav
                {
                    ModDirectory = nextModDir,
                    Category = _categoryParam ?? string.Empty,
                    ViewMode = _viewModeParam ?? string.Empty
                };
                Frame.Navigate(typeof(ModDetailPage), navParam);
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
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to update mod image", ex);
                    }
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
            public string ViewMode { get; set; } = string.Empty; // "Categories" or "Mods"
        }
    }
}
