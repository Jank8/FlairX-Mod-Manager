using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
        private string? _currentModDirectory;
        private List<string> _availablePreviewImages = new List<string>();
        private int _currentImageIndex = 0;
        public event EventHandler? CloseRequested; // Event to notify parent to close

        // Animation throttling
        private DateTime _lastAnimationUpdate = DateTime.MinValue;
        private const int ANIMATION_THROTTLE_MS = 16; // ~60 FPS
        private Microsoft.UI.Xaml.DispatcherTimer? _animationTimer;
        private double _targetTiltX = 0;
        private double _targetTiltY = 0;

        public ModDetailUserControl()
        {
            this.InitializeComponent();
            this.Loaded += ModDetailUserControl_Loaded;
            this.ActualThemeChanged += ModDetailUserControl_ActualThemeChanged;
            this.Unloaded += ModDetailUserControl_Unloaded;
        }

        private void ModDetailUserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Cleanup animation timer
            _animationTimer?.Stop();
            _animationTimer = null;
            
            // Unsubscribe from pointer events
            if (ModImageCoordinateField != null)
            {
                ModImageCoordinateField.PointerMoved -= ModImageCoordinateField_PointerMoved;
            }
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
                UpdateAvailableNotification.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_UpdateAvailable");

                // Set tooltip for OpenUrlButton
                ToolTipService.SetToolTip(OpenUrlButton, SharedUtilities.GetTranslation(lang, "ModDetailPage_OpenURL_Tooltip"));
                
                // Set tooltips for Today buttons
                ToolTipService.SetToolTip(TodayDateCheckedButton, SharedUtilities.GetTranslation(lang, "ModDetailPage_SetToday_Tooltip"));
                ToolTipService.SetToolTip(TodayDateUpdatedButton, SharedUtilities.GetTranslation(lang, "ModDetailPage_SetToday_Tooltip"));

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
                        ModDetailTitle.Text = modName;
                        _modJsonPath = Path.Combine(fullModDir, "mod.json");
                        _currentModDirectory = fullModDir;
                        
                        System.Diagnostics.Debug.WriteLine($"ModDetailUserControl: Found mod directory: {fullModDir}");
                        System.Diagnostics.Debug.WriteLine($"ModDetailUserControl: Looking for mod.json at: {_modJsonPath}");
                        
                        // Load mod.json data
                        LoadModJsonData();
                        
                        // Load preview images
                        LoadPreviewImages(fullModDir);
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
                
                // Check for available updates
                CheckForUpdates(root);
                
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

        private void LoadPreviewImages(string fullModDir)
        {
            try
            {
                _availablePreviewImages.Clear();
                _currentImageIndex = 0;

                // Check for main preview.jpg first
                var mainPreviewPath = Path.Combine(fullModDir, "preview.jpg");
                if (File.Exists(mainPreviewPath))
                {
                    _availablePreviewImages.Add(mainPreviewPath);
                }

                // Check for preview-01.jpg through preview-99.jpg
                for (int i = 1; i <= 99; i++)
                {
                    var previewPath = Path.Combine(fullModDir, $"preview-{i:D2}.jpg");
                    if (File.Exists(previewPath))
                    {
                        _availablePreviewImages.Add(previewPath);
                    }
                }

                // Update UI based on available images
                UpdateImageNavigation();
                LoadCurrentImage();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading preview images from {fullModDir}", ex);
                ModImage.Source = null;
                UpdateImageNavigation();
            }
        }

        private void LoadCurrentImage()
        {
            try
            {
                if (_availablePreviewImages.Count > 0 && _currentImageIndex >= 0 && _currentImageIndex < _availablePreviewImages.Count)
                {
                    var imagePath = _availablePreviewImages[_currentImageIndex];
                    var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    byte[] imageData = File.ReadAllBytes(imagePath);
                    using (var memStream = new MemoryStream(imageData))
                    {
                        bitmap.SetSource(memStream.AsRandomAccessStream());
                    }
                    
                    // Change the image source first
                    ModImage.Source = bitmap;
                    
                    // Create elastic scale animation
                    var elasticScaleX = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        From = 0.8,
                        To = 1.0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(600)),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.ElasticEase 
                        { 
                            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
                            Oscillations = 2,
                            Springiness = 8
                        }
                    };
                    
                    var elasticScaleY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        From = 0.8,
                        To = 1.0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(600)),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.ElasticEase 
                        { 
                            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
                            Oscillations = 2,
                            Springiness = 8
                        }
                    };
                    
                    // Ensure the image has a ScaleTransform
                    if (ModImage.RenderTransform == null || !(ModImage.RenderTransform is Microsoft.UI.Xaml.Media.ScaleTransform))
                    {
                        ModImage.RenderTransform = new Microsoft.UI.Xaml.Media.ScaleTransform();
                        ModImage.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5); // Center the scaling
                    }
                    
                    var scaleTransform = (Microsoft.UI.Xaml.Media.ScaleTransform)ModImage.RenderTransform;
                    
                    // Create storyboard and apply animations
                    var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(elasticScaleX, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(elasticScaleX, "ScaleX");
                    
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(elasticScaleY, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(elasticScaleY, "ScaleY");
                    
                    storyboard.Children.Add(elasticScaleX);
                    storyboard.Children.Add(elasticScaleY);
                    
                    storyboard.Begin();
                }
                else
                {
                    ModImage.Source = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading current image", ex);
                ModImage.Source = null;
            }
        }

        private void UpdateImageNavigation()
        {
            bool hasMultipleImages = _availablePreviewImages.Count > 1;
            
            // Show/hide navigation buttons
            PrevImageButton.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            NextImageButton.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            
            // Show/hide image counter
            ImageCounterBorder.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            
            if (hasMultipleImages)
            {
                ImageCounterText.Text = $"{_currentImageIndex + 1} / {_availablePreviewImages.Count}";
                
                // Enable/disable buttons based on current position using opacity and pointer capture
                PrevImageButton.Opacity = _currentImageIndex > 0 ? 1.0 : 0.5;
                PrevImageButton.IsHitTestVisible = _currentImageIndex > 0;
                
                NextImageButton.Opacity = _currentImageIndex < _availablePreviewImages.Count - 1 ? 1.0 : 0.5;
                NextImageButton.IsHitTestVisible = _currentImageIndex < _availablePreviewImages.Count - 1;
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
            
            // Reset image navigation
            _availablePreviewImages.Clear();
            _currentImageIndex = 0;
            UpdateImageNavigation();
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
                    // Check if it's a GameBanana URL
                    if (url.Contains("gamebanana.com", StringComparison.OrdinalIgnoreCase))
                    {
                        // Get game tag from current game
                        var gameTag = SettingsManager.CurrentSelectedGame;
                        if (!string.IsNullOrEmpty(gameTag))
                        {
                            // Open GameBanana browser with mod URL
                            var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                            mainWindow?.ShowGameBananaBrowserPanel(gameTag, url);
                        }
                        else
                        {
                            Logger.LogWarning("No game tag found, cannot open GameBanana browser");
                        }
                    }
                    else
                    {
                        // Open in external browser for non-GameBanana URLs
                        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                            url = "https://" + url;
                        
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error opening URL", ex);
            }
        }

        private void TodayDateCheckedButton_Click(object sender, RoutedEventArgs e)
        {
            ModDateCheckedPicker.Date = DateTimeOffset.Now;
        }

        private void TodayDateUpdatedButton_Click(object sender, RoutedEventArgs e)
        {
            ModDateUpdatedPicker.Date = DateTimeOffset.Now;
        }

        private void CheckForUpdates(JsonElement root)
        {
            try
            {
                // Get gbChangeDate and dateUpdated from mod.json
                string? gbChangeDate = root.TryGetProperty("gbChangeDate", out var gbChangeProp) ? gbChangeProp.GetString() : null;
                string? dateUpdated = root.TryGetProperty("dateUpdated", out var dateUpdatedProp) ? dateUpdatedProp.GetString() : null;

                // Check if both dates exist and gbChangeDate is newer than dateUpdated
                if (!string.IsNullOrWhiteSpace(gbChangeDate) && !string.IsNullOrWhiteSpace(dateUpdated))
                {
                    if (DateTime.TryParse(gbChangeDate, out var gbDate) && DateTime.TryParse(dateUpdated, out var updatedDate))
                    {
                        if (gbDate > updatedDate)
                        {
                            UpdateAvailableNotification.Visibility = Visibility.Visible;
                            return;
                        }
                    }
                }

                // Hide notification if no update available
                UpdateAvailableNotification.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error checking for updates", ex);
                UpdateAvailableNotification.Visibility = Visibility.Collapsed;
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
                
                // Re-check for updates after saving dateUpdated
                if (field == "dateUpdated")
                {
                    var updatedDoc = JsonDocument.Parse(newJson);
                    CheckForUpdates(updatedDoc.RootElement);
                }
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

        private void PrevImageButton_Click(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_currentImageIndex > 0)
            {
                _currentImageIndex--;
                LoadCurrentImage();
                UpdateImageNavigation();
            }
        }

        private void NextImageButton_Click(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_currentImageIndex < _availablePreviewImages.Count - 1)
            {
                _currentImageIndex++;
                LoadCurrentImage();
                UpdateImageNavigation();
            }
        }

        // MAIN HOVER TILT EFFECT - 6 degrees for entire preview area
        private void ModImageCoordinateField_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                // Only process if sender is the main coordinate field, ignore buttons
                if (!ReferenceEquals(sender, ModImageCoordinateField)) return;
                
                // Subscribe to pointer moved events for dynamic tilt
                ModImageCoordinateField.PointerMoved += ModImageCoordinateField_PointerMoved;
                
                // Calculate initial target tilt
                CalculateTargetTilt(e);
                
                // Apply initial tilt with smooth animation
                AnimateMainTilt(_targetTiltX, _targetTiltY);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ModImageCoordinateField_PointerEntered", ex);
            }
        }

        private void ModImageCoordinateField_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                // Only process if sender is the main coordinate field, ignore buttons
                if (!ReferenceEquals(sender, ModImageCoordinateField)) return;
                
                // Check if mouse is still within the entire preview area (including buttons)
                var position = e.GetCurrentPoint(ModImageCoordinateField.Parent as FrameworkElement);
                var container = ModImageCoordinateField.Parent as FrameworkElement;
                
                if (container != null)
                {
                    var bounds = new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight);
                    
                    // Only reset tilt if mouse truly left the entire preview area
                    if (!bounds.Contains(position.Position))
                    {
                        // Unsubscribe from pointer moved events
                        ModImageCoordinateField.PointerMoved -= ModImageCoordinateField_PointerMoved;
                        
                        // Reset target values
                        _targetTiltX = 0;
                        _targetTiltY = 0;
                        
                        // Reset tilt projection with smooth animation - reset entire container
                        ResetMainTiltEffect();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ModImageCoordinateField_PointerExited", ex);
            }
        }

        private void ModImageCoordinateField_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                // Only process if sender is the main coordinate field, ignore buttons
                if (!ReferenceEquals(sender, ModImageCoordinateField)) return;
                
                // Throttle animation updates for smoother performance
                var now = DateTime.Now;
                if ((now - _lastAnimationUpdate).TotalMilliseconds < ANIMATION_THROTTLE_MS)
                    return;
                
                _lastAnimationUpdate = now;
                
                // Calculate target tilt values
                CalculateTargetTilt(e);
                
                // Use smooth interpolation instead of instant response
                UpdateMainTiltEffectSmooth();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ModImageCoordinateField_PointerMoved", ex);
            }
        }

        private void CalculateTargetTilt(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                // Get the pointer position relative to the coordinate field
                var position = e.GetCurrentPoint(ModImageCoordinateField);
                var fieldWidth = ModImageCoordinateField.ActualWidth;
                var fieldHeight = ModImageCoordinateField.ActualHeight;
                
                if (fieldWidth > 0 && fieldHeight > 0)
                {
                    // Calculate tilt angles based on pointer position
                    var centerX = fieldWidth / 2;
                    var centerY = fieldHeight / 2;
                    var offsetX = (position.Position.X - centerX) / centerX; // -1 to 1
                    var offsetY = (position.Position.Y - centerY) / centerY; // -1 to 1
                    
                    // Main tilt for entire area (max 6 degrees)
                    var maxTilt = 6.0;
                    _targetTiltX = offsetY * maxTilt; // Y offset affects X rotation
                    _targetTiltY = -offsetX * maxTilt; // X offset affects Y rotation (inverted)
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in CalculateTargetTilt", ex);
            }
        }

        private void UpdateMainTiltEffectSmooth()
        {
            try
            {
                var container = ModImageCoordinateField.Parent as Grid;
                var projection = GetOrCreateProjection(container);
                if (projection == null) return;
                
                // Use smooth interpolation instead of instant change
                var currentTiltX = projection.RotationX;
                var currentTiltY = projection.RotationY;
                
                // Interpolation factor (0.2 = 20% towards target each frame)
                var lerpFactor = 0.2;
                var newTiltX = currentTiltX + ((_targetTiltX - currentTiltX) * lerpFactor);
                var newTiltY = currentTiltY + ((_targetTiltY - currentTiltY) * lerpFactor);
                
                // Set new values directly (no animation needed as we're interpolating)
                projection.RotationX = newTiltX;
                projection.RotationY = newTiltY;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in UpdateMainTiltEffectSmooth", ex);
            }
        }

        private void UpdateMainTiltEffect(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e, bool useAnimation = false)
        {
            try
            {
                // Get the pointer position relative to the coordinate field
                var position = e.GetCurrentPoint(ModImageCoordinateField);
                var fieldWidth = ModImageCoordinateField.ActualWidth;
                var fieldHeight = ModImageCoordinateField.ActualHeight;
                
                if (fieldWidth > 0 && fieldHeight > 0)
                {
                    // Calculate tilt angles based on pointer position
                    var centerX = fieldWidth / 2;
                    var centerY = fieldHeight / 2;
                    var offsetX = (position.Position.X - centerX) / centerX; // -1 to 1
                    var offsetY = (position.Position.Y - centerY) / centerY; // -1 to 1
                    
                    // Main tilt for entire area (max 6 degrees)
                    var maxTilt = 6.0;
                    var tiltX = offsetY * maxTilt; // Y offset affects X rotation
                    var tiltY = -offsetX * maxTilt; // X offset affects Y rotation (inverted)
                    
                    if (useAnimation)
                    {
                        // Smooth animation for entry
                        AnimateMainTilt(tiltX, tiltY);
                    }
                    else
                    {
                        // Instant response for movement
                        SetMainTilt(tiltX, tiltY);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in UpdateMainTiltEffect", ex);
            }
        }

        private void AnimateMainTilt(double tiltX, double tiltY)
        {
            try
            {
                var container = ModImageCoordinateField.Parent as Grid;
                var projection = GetOrCreateProjection(container);
                if (projection == null) return;
                
                // Create optimized storyboard
                var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                var easing = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase 
                { 
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut 
                };
                var duration = TimeSpan.FromMilliseconds(150);
                
                // X rotation animation
                var rotXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = tiltX,
                    Duration = duration,
                    EasingFunction = easing
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotXAnim, projection);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotXAnim, "RotationX");
                storyboard.Children.Add(rotXAnim);
                
                // Y rotation animation
                var rotYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = tiltY,
                    Duration = duration,
                    EasingFunction = easing
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotYAnim, projection);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotYAnim, "RotationY");
                storyboard.Children.Add(rotYAnim);
                
                storyboard.Begin();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in AnimateMainTilt", ex);
            }
        }

        private void SetMainTilt(double tiltX, double tiltY)
        {
            try
            {
                var container = ModImageCoordinateField.Parent as Grid;
                var projection = GetOrCreateProjection(container);
                if (projection == null) return;
                
                // Set immediately - no animation
                projection.RotationX = tiltX;
                projection.RotationY = tiltY;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in SetMainTilt", ex);
            }
        }

        private void ResetMainTiltEffect()
        {
            try
            {
                var container = ModImageCoordinateField.Parent as Grid;
                if (container?.Projection is not Microsoft.UI.Xaml.Media.PlaneProjection projection) return;
                
                var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                var easing = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase();
                var duration = TimeSpan.FromMilliseconds(250);
                
                // X rotation reset
                var rotXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = 0,
                    Duration = duration,
                    EasingFunction = easing
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotXAnim, projection);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotXAnim, "RotationX");
                storyboard.Children.Add(rotXAnim);
                
                // Y rotation reset
                var rotYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = 0,
                    Duration = duration,
                    EasingFunction = easing
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotYAnim, projection);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotYAnim, "RotationY");
                storyboard.Children.Add(rotYAnim);
                
                storyboard.Begin();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ResetMainTiltEffect", ex);
            }
        }

        // Helper method to get or create PlaneProjection
        private Microsoft.UI.Xaml.Media.PlaneProjection? GetOrCreateProjection(Grid? container)
        {
            if (container == null) return null;
            
            if (container.Projection is not Microsoft.UI.Xaml.Media.PlaneProjection projection)
            {
                projection = new Microsoft.UI.Xaml.Media.PlaneProjection
                {
                    CenterOfRotationX = 0.5,
                    CenterOfRotationY = 0.5
                };
                container.Projection = projection;
            }
            
            return projection;
        }
    }
}