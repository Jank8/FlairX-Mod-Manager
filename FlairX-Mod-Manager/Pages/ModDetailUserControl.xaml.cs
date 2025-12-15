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
        
        // Hotkey editing with gamepad support
        private GamepadManager? _hotkeyGamepad;
        private TextBox? _activeHotkeyEditBox;
        private List<string> _recordedGamepadButtons = new();
        private bool _isSettingHotkeyText = false;
        private bool _previousHotkeysEnabled = true;

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
            
            // Cleanup gamepad
            _hotkeyGamepad?.Dispose();
            _hotkeyGamepad = null;
            
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
                ModDateCheckedDesc.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_DateChecked_Desc");
                ModDateUpdatedLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_DateUpdated");
                ModDateUpdatedDesc.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_DateUpdated_Desc");
                ModAuthorLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_Author");
                ModAuthorDesc.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_Author_Desc");
                ModHotkeysLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_Hotkeys");
                ModUrlLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_URL");
                ModUrlDesc.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_URL_Desc");
                ModNSFWLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_NSFW_Label");
                ModNSFWDesc.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_NSFW_Desc");
                ModStatusKeeperSyncLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_StatusKeeperSync_Label");
                ModStatusKeeperSyncDesc.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_StatusKeeperSync_Desc");
                ModPreviewHeader.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_Preview_Header");
                ModDetailsHeader.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_Details_Header");
                UpdateAvailableNotification.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_UpdateAvailable");

                // Set tooltip for OpenUrlButton
                ToolTipService.SetToolTip(OpenUrlButton, SharedUtilities.GetTranslation(lang, "ModDetailPage_OpenURL_Tooltip"));
                
                // Set tooltips for Today buttons
                ToolTipService.SetToolTip(TodayDateCheckedButton, SharedUtilities.GetTranslation(lang, "ModDetailPage_SetToday_Tooltip"));
                ToolTipService.SetToolTip(TodayDateUpdatedButton, SharedUtilities.GetTranslation(lang, "ModDetailPage_SetToday_Tooltip"));
                
                // Set placeholder text for date pickers
                ModDateCheckedPicker.PlaceholderText = SharedUtilities.GetTranslation(lang, "ModDetailPage_SelectDate_Placeholder");
                ModDateUpdatedPicker.PlaceholderText = SharedUtilities.GetTranslation(lang, "ModDetailPage_SelectDate_Placeholder");

                string modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                {
                    modLibraryPath = PathManager.GetModsPath();
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
                        // Remove DISABLED_ prefix from display name
                        if (modName.StartsWith("DISABLED_"))
                        {
                            modName = modName.Substring("DISABLED_".Length);
                        }
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
                    // Check for exact match
                    var modPath = Path.Combine(categoryDir, modDirectoryName);
                    if (Directory.Exists(modPath))
                    {
                        return Path.GetFullPath(modPath);
                    }
                    
                    // Check for DISABLED_ version
                    var disabledPath = Path.Combine(categoryDir, "DISABLED_" + modDirectoryName);
                    if (Directory.Exists(disabledPath))
                    {
                        return Path.GetFullPath(disabledPath);
                    }
                    
                    // Check if modDirectoryName already has DISABLED_ prefix, try without it
                    if (modDirectoryName.StartsWith("DISABLED_"))
                    {
                        var cleanName = modDirectoryName.Substring("DISABLED_".Length);
                        var cleanPath = Path.Combine(categoryDir, cleanName);
                        if (Directory.Exists(cleanPath))
                        {
                            return Path.GetFullPath(cleanPath);
                        }
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
                
                // Set URL with placeholder logic
                if (string.IsNullOrWhiteSpace(url))
                {
                    ModUrlTextBox.Text = "https://";
                    ModUrlTextBox.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray);
                }
                else
                {
                    ModUrlTextBox.Text = url;
                    ModUrlTextBox.ClearValue(TextBox.ForegroundProperty); // Use default theme color
                }
                
                ModVersionTextBox.Text = version;
                
                // Check if mod is NSFW and show badge + set checkbox
                bool isNSFW = root.TryGetProperty("isNSFW", out var nsfwProp) && nsfwProp.ValueKind == JsonValueKind.True;
                NSFWBadge.Visibility = isNSFW ? Visibility.Visible : Visibility.Collapsed;
                ModNSFWCheckBox.IsChecked = isNSFW;
                
                // Load StatusKeeper sync setting (default true if not present)
                bool statusKeeperSync = !root.TryGetProperty("statusKeeperSync", out var syncProp) || syncProp.ValueKind != JsonValueKind.False;
                ModStatusKeeperSyncCheckBox.IsChecked = statusKeeperSync;
                
                // Check for available updates
                CheckForUpdates(root);
                
                // Load favorite hotkeys first
                _favoriteHotkeys.Clear();
                if (root.TryGetProperty("favoriteHotkeys", out var favHotkeysProp) && favHotkeysProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var fav in favHotkeysProp.EnumerateArray())
                    {
                        var favKey = fav.GetString();
                        if (!string.IsNullOrEmpty(favKey))
                        {
                            _favoriteHotkeys.Add(favKey);
                        }
                    }
                }
                
                // Load hotkeys
                ModHotkeysPanel.Children.Clear();
                _originalHotkeyOrder.Clear();
                if (root.TryGetProperty("hotkeys", out var hotkeysProp) && hotkeysProp.ValueKind == JsonValueKind.Array)
                {
                    // First pass: collect all hotkeys with original index and find max key width
                    var hotkeyList = new List<(string key, string desc, int originalIndex)>();
                    int index = 0;
                    foreach (var hotkey in hotkeysProp.EnumerateArray())
                    {
                        string? key = null;
                        string? desc = null;
                        
                        if (hotkey.ValueKind == JsonValueKind.Object)
                        {
                            key = hotkey.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : null;
                            desc = hotkey.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
                        }
                        else if (hotkey.ValueKind == JsonValueKind.String)
                        {
                            key = hotkey.GetString() ?? "";
                        }
                        
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            hotkeyList.Add((key, desc ?? string.Empty, index));
                            _originalHotkeyOrder.Add(key);
                        }
                        index++;
                    }
                    
                    // Sort: favorites first (maintaining original order within each group)
                    hotkeyList = hotkeyList
                        .OrderByDescending(x => _favoriteHotkeys.Contains(x.key))
                        .ThenBy(x => x.originalIndex)
                        .ToList();
                    
                    // Create rows - column width will auto-adjust
                    foreach (var (key, desc, origIdx) in hotkeyList)
                    {
                        var hotkeyRow = CreateHotkeyRow(key, desc, origIdx);
                        ModHotkeysPanel.Children.Add(hotkeyRow);
                    }
                    
                    // Show hotkeys section if there are any hotkeys
                    HotkeysSection.Visibility = hotkeyList.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    // Hide hotkeys section if no hotkeys property
                    HotkeysSection.Visibility = Visibility.Collapsed;
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
                // Infinite carousel - buttons always active
            }
        }



        private void SetDefaultValues()
        {
            ModImage.Source = null;
            ModHotkeysPanel.Children.Clear();
            HotkeysSection.Visibility = Visibility.Collapsed;
            ModAuthorTextBox.Text = "";
            ModUrlTextBox.Text = "https://";
            ModUrlTextBox.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray);
            ModVersionTextBox.Text = "";
            ModDateCheckedPicker.Date = null;
            ModDateUpdatedPicker.Date = null;
            
            // Reset image navigation
            _availablePreviewImages.Clear();
            _currentImageIndex = 0;
            UpdateImageNavigation();
        }
        
        private HashSet<string> _favoriteHotkeys = new HashSet<string>();
        private List<string> _originalHotkeyOrder = new List<string>(); // Preserve original order
        
        private Border CreateHotkeyRow(string keyCombo, string description, int originalIndex)
        {
            var keyBackground = (Brush)Application.Current.Resources["HotkeyKeyBackground"];
            bool isFavorite = _favoriteHotkeys.Contains(keyCombo);
            
            var rowBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 6),
                Tag = new HotkeyRowData { Key = keyCombo, Description = description, OriginalIndex = originalIndex },
                RenderTransform = new CompositeTransform()
            };
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Keys - auto width
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Spacer
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Description
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Buttons
            
            // Create keys panel
            var keysPanel = CreateKeysPanelFromCombo(keyCombo, keyBackground);
            Grid.SetColumn(keysPanel, 0);
            grid.Children.Add(keysPanel);
            
            // Description
            var descText = new TextBlock
            {
                Text = description,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.9
            };
            Grid.SetColumn(descText, 2);
            grid.Children.Add(descText);
            
            // Buttons panel (3 buttons in same style)
            var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            
            var accentColor = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            var accentBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(accentColor.A, accentColor.R, accentColor.G, accentColor.B));
            var defaultBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.6 };
            
            // Favorite button
            var favoriteBtn = CreateHotkeyActionButton(isFavorite ? "\uE735" : "\uE734", isFavorite ? accentBrush : defaultBrush);
            favoriteBtn.Tag = new HotkeyButtonData { KeyCombo = keyCombo, Description = description, OriginalIndex = originalIndex };
            favoriteBtn.PointerPressed += FavoriteButton_PointerPressed;
            buttonsPanel.Children.Add(favoriteBtn);
            
            // Edit button
            var editBtn = CreateHotkeyActionButton("\uE70F", defaultBrush);
            editBtn.Tag = new HotkeyButtonData { KeyCombo = keyCombo, Description = description, OriginalIndex = originalIndex };
            editBtn.PointerPressed += EditButton_PointerPressed;
            buttonsPanel.Children.Add(editBtn);
            
            // Rec button (always visible - for gamepad recording)
            var recBtn = CreateHotkeyActionButton("\uE7C8", defaultBrush);
            recBtn.Tag = new HotkeyButtonData { KeyCombo = keyCombo, Description = description, OriginalIndex = originalIndex };
            recBtn.PointerPressed += RecButton_PointerPressed;
            buttonsPanel.Children.Add(recBtn);
            
            // Save button (initially hidden, shown during edit)
            var saveBtn = CreateHotkeyActionButton("\uE73E", accentBrush);
            saveBtn.Tag = new HotkeyButtonData { KeyCombo = keyCombo, Description = description, OriginalIndex = originalIndex };
            saveBtn.Visibility = Visibility.Collapsed;
            saveBtn.PointerPressed += SaveButton_PointerPressed;
            buttonsPanel.Children.Add(saveBtn);
            
            Grid.SetColumn(buttonsPanel, 3);
            grid.Children.Add(buttonsPanel);
            
            rowBorder.Child = grid;
            return rowBorder;
        }
        
        private const string PromptFontFamily = "ms-appx:///Assets/promptfont/promptfont.ttf#promptfont";
        
        private static readonly Dictionary<string, string> HotkeyIconMap = new()
        {
            // Xbox
            ["XB A"] = "\u21D3", ["XB B"] = "\u21D2", ["XB X"] = "\u21D0", ["XB Y"] = "\u21D1",
            ["XB LS"] = "\u21BA", ["XB RS"] = "\u21BB", ["XB LT"] = "\u2196", ["XB RT"] = "\u2197",
            ["XB LB"] = "\u2198", ["XB RB"] = "\u2199", ["XB Start"] = "\u21FB", ["XB Back"] = "\u21FA",
            ["XB Home"] = "\u21F9", ["XB ↑"] = "\u227B", ["XB ↓"] = "\u227D", ["XB ←"] = "\u227A", ["XB →"] = "\u227C",
            ["XB LT Pull"] = "\u21DC", ["XB RT Pull"] = "\u21DD", ["XB DPAD"] = "\u2284",
            ["XB ↑↓"] = "\u227F", ["XB ←→"] = "\u227E", ["XB ↖"] = "\u2280", ["XB ↗"] = "\u2281", ["XB ↙"] = "\u2282", ["XB ↘"] = "\u2283",
            // PlayStation
            ["PS ×"] = "\u21E3", ["PS ○"] = "\u21E2", ["PS □"] = "\u21E0", ["PS △"] = "\u21E1",
            ["PS L1"] = "\u21B0", ["PS R1"] = "\u21B1", ["PS L2"] = "\u21B2", ["PS R2"] = "\u21B3",
            ["PS L3"] = "\u21EF", ["PS R3"] = "\u21F0", ["PS Share"] = "\u21E6", ["PS Options"] = "\u21E8", ["PS Touchpad"] = "\u21E7",
            ["PS DS Share"] = "\u2206", ["PS DS Touchpad"] = "\u2207", ["PS DS Options"] = "\u2208",
            // Nintendo
            ["NIN A"] = "\u21A7", ["NIN B"] = "\u21A6", ["NIN X"] = "\u21A4", ["NIN Y"] = "\u21A5",
            ["NIN L"] = "\u219C", ["NIN R"] = "\u219D", ["NIN ZL"] = "\u219A", ["NIN ZR"] = "\u219B",
            ["NIN +"] = "\u21FE", ["NIN -"] = "\u21FD", ["NIN Z"] = "\u21E9", ["NIN ZT"] = "\u21EA",
            ["NIN C"] = "\u21EB", ["NIN Zb"] = "\u21EC", ["NIN 1"] = "\u21ED", ["NIN 2"] = "\u21EE",
            ["NIN ↑"] = "\u2200", ["NIN ↓"] = "\u2202", ["NIN ←"] = "\u21FF", ["NIN →"] = "\u2201",
            ["NIN SL"] = "\u2203", ["NIN SR"] = "\u2204",
            // Generic gamepad
            ["DPAD ↑"] = "\u219F", ["DPAD ↓"] = "\u21A1", ["DPAD ←"] = "\u219E", ["DPAD →"] = "\u21A0",
            ["DPAD"] = "\u21CE", ["L STICK"] = "\u21CB", ["R STICK"] = "\u21CC",
            ["SELECT"] = "\u21F7", ["START"] = "\u21F8", ["HOME"] = "\u21F9",
            ["DPAD ↑↓"] = "\u21A3", ["DPAD ←→"] = "\u21A2", ["DPAD ↙"] = "\u21B4", ["DPAD ↘"] = "\u21DE", ["DPAD ↖"] = "\u21DF",
            ["GP A"] = "\u21A7", ["GP B"] = "\u21A6", ["GP X"] = "\u21A4", ["GP Y"] = "\u21A5", ["GP BTNS"] = "\u21A8",
            ["GP X+B"] = "\u2225", ["GP A+Y"] = "\u2226", ["GP X+Y"] = "\u2227", ["GP B+Y"] = "\u2228", ["GP A+B"] = "\u2229", ["GP X+A"] = "\u222A",
            ["GP M1"] = "\u2212", ["GP M2"] = "\u2213", ["GP M3"] = "\u2214", ["GP Y1"] = "\u2215", ["GP Y2"] = "\u2216", ["GP Y3"] = "\u2217",
            ["GP L4"] = "\u2276", ["GP R4"] = "\u2277", ["GP L5"] = "\u2278", ["GP R5"] = "\u2279",
            // Analog sticks
            ["ANALOG"] = "\u21CD", ["ANALOG L"] = "\u21CB", ["ANALOG R"] = "\u21CC",
            ["ANALOG ↑"] = "\u21C8", ["ANALOG ↓"] = "\u21CA", ["ANALOG ←"] = "\u21C7", ["ANALOG →"] = "\u21C9",
            ["ANALOG ↑↓"] = "\u21D5", ["ANALOG ←→"] = "\u21D4", ["ANALOG ↖"] = "\u21D6", ["ANALOG ↗"] = "\u21D7", ["ANALOG ↙"] = "\u21D9", ["ANALOG ↘"] = "\u21D8",
            ["ANALOG L↑"] = "\u21BE", ["ANALOG L↓"] = "\u21C2", ["ANALOG L←"] = "\u21BC", ["ANALOG L→"] = "\u21C0",
            ["ANALOG R↑"] = "\u21BF", ["ANALOG R↓"] = "\u21C3", ["ANALOG R←"] = "\u21BD", ["ANALOG R→"] = "\u21C1",
            ["ANALOG L↑↓"] = "\u21C5", ["ANALOG L←→"] = "\u21C4", ["ANALOG R↑↓"] = "\u21F5", ["ANALOG R←→"] = "\u21C6",
            ["ANALOG L ANY"] = "\u21F1", ["ANALOG R ANY"] = "\u21F2", ["ANALOG ANY"] = "\u21F3",
            ["ANALOG CW"] = "\u21B6", ["ANALOG CCW"] = "\u21B7", ["ANALOG L CW"] = "\u21A9", ["ANALOG L CCW"] = "\u21AA",
            ["ANALOG R CW"] = "\u21AB", ["ANALOG R CCW"] = "\u21AC", ["ANALOG LR CW"] = "\u21AD", ["ANALOG LR CCW"] = "\u21AE",
            ["ANALOG CLICK"] = "\u21B9", ["ANALOG L CLICK"] = "\u21BA", ["ANALOG R CLICK"] = "\u21BB", ["ANALOG STICK CLICK"] = "\u21B8",
            ["ANALOG L TOUCH"] = "\u21DA", ["ANALOG R TOUCH"] = "\u21DB",
            // Trackpads
            ["TRACKPAD L"] = "\u2264", ["TRACKPAD R"] = "\u2265", ["TRACKPAD L CLICK"] = "\u2266", ["TRACKPAD R CLICK"] = "\u2267",
            ["TRACKPAD L TOUCH"] = "\u2268", ["TRACKPAD R TOUCH"] = "\u2269",
            ["TRACKPAD L←→"] = "\u226A", ["TRACKPAD L↑↓"] = "\u226B", ["TRACKPAD R←→"] = "\u226C", ["TRACKPAD R↑↓"] = "\u226D",
            ["TRACKPAD L←"] = "\u226E", ["TRACKPAD R←"] = "\u226F", ["TRACKPAD L↑"] = "\u2270", ["TRACKPAD R↑"] = "\u2271",
            ["TRACKPAD L→"] = "\u2272", ["TRACKPAD R→"] = "\u2273", ["TRACKPAD L↓"] = "\u2274", ["TRACKPAD R↓"] = "\u2275",
            // Button actions
            ["BTN PRESS"] = "\u222B", ["BTN 2X"] = "\u222C", ["BTN HOLD PRESS"] = "\u222D", ["BTN HOLD REL"] = "\u222E", ["BTN HOLD"] = "\u222F",
            // Steam
            ["STEAM MENU"] = "\u21E4", ["STEAM OPTIONS"] = "\u21E5",
            // Handheld devices
            ["LEGION"] = "\u2205", ["AYANEO LC"] = "\u2209", ["AYANEO RC"] = "\u220A", ["AYANEO WAVE"] = "\u220B",
            ["AYN HOME"] = "\u220C", ["AYN LCC"] = "\u220D", ["GPD C1"] = "\u220E", ["GPD C2"] = "\u220F", ["GPD MENU"] = "\u221A",
            ["ONEX KB"] = "\u2210", ["ONEX TURBO"] = "\u2211", ["ONEX FN"] = "\u2218", ["ONEX HOME"] = "\u2219",
            ["ORANGEPI CTRL"] = "\u221B", ["ORANGEPI HOME"] = "\u221C",
            ["ZOTAC LOGO"] = "\u221D", ["ZOTAC MENU"] = "\u221E",
            ["ROG ARMOURY"] = "\uE005", ["ROG CMD"] = "\uE006",
            ["MSI CENTER"] = "\uE010", ["MSI QUICK"] = "\uE011",
            // Android
            ["ANDROID TABS"] = "\u23CD", ["ANDROID BACK"] = "\u23CE", ["ANDROID HOME"] = "\u23CF",
            ["ANDROID HDOTS"] = "\u23D0", ["ANDROID VDOTS"] = "\u23D1", ["ANDROID MENU"] = "\u23D2",
            // Keyboard arrows
            ["↑"] = "\u23F6", ["↓"] = "\u23F7", ["←"] = "\u23F4", ["→"] = "\u23F5",
            ["WASD"] = "\u2423", ["ARROWS"] = "\u2424", ["IJKL"] = "\u2425", ["84562"] = "\u2422",
            // Keyboard modifiers
            ["CTRL"] = "\u2427", ["ALT"] = "\u2428", ["SHIFT"] = "\u2429", ["TAB"] = "\u242B",
            ["ENTER"] = "\u242E", ["ESC"] = "\u242F", ["SPACE"] = "\u243A", ["BACKSPACE"] = "\u242D",
            ["DEL"] = "\u2437", ["INS"] = "\u2434", ["KB HOME"] = "\u2435", ["END"] = "\u2438",
            ["PAGE UP"] = "\u2436", ["PAGE DOWN"] = "\u2439", ["CAPS"] = "\u242C",
            ["FN"] = "\u2426", ["PRINT"] = "\u2430", ["SCROLL"] = "\u2431", ["PAUSE"] = "\u2432", ["NUM"] = "\u2433",
            ["SUPER"] = "\u242A", ["ALT GR"] = "\u244A", ["ALT L"] = "\u244B", ["ALT R"] = "\u244C",
            ["CTRL L"] = "\u244D", ["CTRL R"] = "\u244E", ["SHIFT L"] = "\u244F", ["SHIFT R"] = "\u2450",
            ["OPTION"] = "\u2451", ["CMD"] = "\u2452", ["KEY"] = "\u248F",
            // Numpad
            ["NUM0"] = "\u247D", ["NUM1"] = "\u2474", ["NUM2"] = "\u2475", ["NUM3"] = "\u2476", ["NUM4"] = "\u2477",
            ["NUM5"] = "\u2478", ["NUM6"] = "\u2479", ["NUM7"] = "\u247A", ["NUM8"] = "\u247B", ["NUM9"] = "\u247C",
            ["NUM."] = "\u247E", ["NUM ENTER"] = "\u247F", ["NUM-"] = "\u2480", ["NUM+"] = "\u2481",
            ["NUM/"] = "\u2482", ["NUM*"] = "\u2483", ["NUM="] = "\u2484",
            // Function keys (icon style)
            ["F1"] = "\u2460", ["F2"] = "\u2461", ["F3"] = "\u2462", ["F4"] = "\u2463", ["F5"] = "\u2464", ["F6"] = "\u2465",
            ["F7"] = "\u2466", ["F8"] = "\u2467", ["F9"] = "\u2468", ["F10"] = "\u2469", ["F11"] = "\u246A", ["F12"] = "\u246B",
            // Letters A-Z (keyboard icon style 0xFF21-0xFF3A)
            ["A"] = "\uFF21", ["B"] = "\uFF22", ["C"] = "\uFF23", ["D"] = "\uFF24", ["E"] = "\uFF25", ["F"] = "\uFF26",
            ["G"] = "\uFF27", ["H"] = "\uFF28", ["I"] = "\uFF29", ["J"] = "\uFF2A", ["K"] = "\uFF2B", ["L"] = "\uFF2C",
            ["M"] = "\uFF2D", ["N"] = "\uFF2E", ["O"] = "\uFF2F", ["P"] = "\uFF30", ["Q"] = "\uFF31", ["R"] = "\uFF32",
            ["S"] = "\uFF33", ["T"] = "\uFF34", ["U"] = "\uFF35", ["V"] = "\uFF36", ["W"] = "\uFF37", ["X"] = "\uFF38",
            ["Y"] = "\uFF39", ["Z"] = "\uFF3A",
            // Lowercase a-z
            ["a"] = "a", ["b"] = "b", ["c"] = "c", ["d"] = "d", ["e"] = "e", ["f"] = "f",
            ["g"] = "g", ["h"] = "h", ["i"] = "i", ["j"] = "j", ["k"] = "k", ["l"] = "l",
            ["m"] = "m", ["n"] = "n", ["o"] = "o", ["p"] = "p", ["q"] = "q", ["r"] = "r",
            ["s"] = "s", ["t"] = "t", ["u"] = "u", ["v"] = "v", ["w"] = "w", ["x"] = "x",
            ["y"] = "y", ["z"] = "z",
            // Numbers 0-9 (keyboard icon style 0xFF10-0xFF19)
            ["0"] = "\uFF10", ["1"] = "\uFF11", ["2"] = "\uFF12", ["3"] = "\uFF13", ["4"] = "\uFF14",
            ["5"] = "\uFF15", ["6"] = "\uFF16", ["7"] = "\uFF17", ["8"] = "\uFF18", ["9"] = "\uFF19",
            // Mouse
            ["LMB"] = "\u27F5", ["RMB"] = "\u27F6", ["MMB"] = "\u27F7",
            ["MOUSE1"] = "\u278A", ["MOUSE2"] = "\u278B", ["MOUSE3"] = "\u278C", ["MOUSE4"] = "\u278D",
            ["MOUSE5"] = "\u278E", ["MOUSE6"] = "\u278F", ["MOUSE7"] = "\u2790", ["MOUSE8"] = "\u2791",
            ["SCROLL↑"] = "\u27F0", ["SCROLL↓"] = "\u27F1", ["SCROLL↑↓"] = "\u27F2",
            ["SCROLL←"] = "\u27EE", ["SCROLL→"] = "\u27EF", ["SCROLL←→"] = "\u27F3", ["SCROLL ANY"] = "\u27F4",
            ["MOUSE←"] = "\u27F8", ["MOUSE↑"] = "\u27F9", ["MOUSE→"] = "\u27FD", ["MOUSE↓"] = "\u27FE",
            ["MOUSE←→"] = "\u27FA", ["MOUSE↑↓"] = "\u27FB", ["MOUSE ANY"] = "\u27FC",
            // ASCII punctuation (keyboard icon style 0xFF01-0xFF0F, 0xFF1A-0xFF20, 0xFF3B-0xFF40, 0xFF5B-0xFF5E)
            ["!"] = "\uFF01", ["\""] = "\uFF02", ["#"] = "\uFF03", ["$"] = "\uFF04", ["%"] = "\uFF05", ["&"] = "\uFF06",
            ["'"] = "\uFF07", ["("] = "\uFF08", [")"] = "\uFF09", ["*"] = "\uFF0A", ["+"] = "\uFF0B", [","] = "\uFF0C",
            ["-"] = "\uFF0D", ["."] = "\uFF0E", ["/"] = "\uFF0F", [":"] = "\uFF1A", [";"] = "\uFF1B", ["<"] = "\uFF1C",
            ["="] = "\uFF1D", [">"] = "\uFF1E", ["?"] = "\uFF1F", ["@"] = "\uFF20", ["["] = "\uFF3B", ["\\"] = "\uFF3C",
            ["]"] = "\uFF3D", ["^"] = "\uFF3E", ["_"] = "\uFF3F", ["`"] = "\uFF40", ["{"] = "\uFF5B", ["|"] = "\uFF5C",
            ["}"] = "\uFF5D", ["~"] = "\uFF5E",
            // Devices
            ["DEV GAMEPAD"] = "\u243C", ["DEV KEYBOARD"] = "\u243D", ["DEV MOUSE"] = "\u243E", ["DEV MOUSE+KB"] = "\u243F",
            ["DEV DS4"] = "\u2440", ["DEV DUALSENSE"] = "\u2441", ["DEV X360"] = "\u2442", ["DEV NUMPAD"] = "\u2443",
            ["DEV DANCE PAD"] = "\U0001F483", ["DEV PHONE"] = "\U0001F4F1", ["DEV LIGHT GUN"] = "\U0001F52B",
            ["DEV WHEEL"] = "\U0001F578", ["DEV JOYSTICK"] = "\U0001F579", ["DEV VR"] = "\U0001F57B",
            ["DEV VR CTRL"] = "\U0001F57C", ["DEV FLIGHT"] = "\U0001F57D",
            // Platform icons
            ["ICON PS"] = "\uE000", ["ICON XBOX"] = "\uE001", ["ICON SWITCH"] = "\uE002", ["ICON AYANEO"] = "\uE003",
            ["ICON LEGION"] = "\uE004", ["ICON MAC"] = "\uE007", ["ICON WIN"] = "\uE008", ["ICON LINUX"] = "\uE009",
            ["ICON BSD"] = "\uE00A", ["ICON STEAM"] = "\uE00B", ["ICON ITCH"] = "\uE00C", ["ICON HUMBLE"] = "\uE00D",
            ["ICON EPIC"] = "\uE00E", ["ICON GOG"] = "\uE00F", ["ICON META"] = "\uE012",
            // Icons
            ["ICON EXCHANGE"] = "\u2194", ["ICON REVERSE"] = "\u2195", ["ICON PIN"] = "\u2316", ["ICON BOX"] = "\u2B1B",
            ["ICON SUN"] = "\u2600", ["ICON STAR"] = "\u2605", ["ICON STAR EMPTY"] = "\u2606", ["ICON SKULL"] = "\u2620",
            ["ICON FROWN"] = "\u2639", ["ICON SMILE"] = "\u263A", ["ICON FLAG"] = "\u2691", ["ICON GEARS"] = "\u2699",
            ["ICON CROSS"] = "\u2717", ["ICON SPARK"] = "\u2726", ["ICON ?"] = "\u2753", ["ICON !"] = "\u2757",
            ["ICON SPADE"] = "\u2660", ["ICON HEART"] = "\u2665", ["ICON DIAMOND"] = "\u2666", ["ICON CLUB"] = "\u2663",
            ["ICON D4"] = "\u2673", ["ICON D6"] = "\u2674", ["ICON D8"] = "\u2675", ["ICON D10"] = "\u2676", ["ICON D12"] = "\u2677", ["ICON D20"] = "\u2678",
            ["ICON 1"] = "\u24F5", ["ICON 2"] = "\u24F6", ["ICON 3"] = "\u24F7", ["ICON 4"] = "\u24F8", ["ICON 5"] = "\u24F9",
            ["ICON 6"] = "\u24FA", ["ICON 7"] = "\u24FB", ["ICON 8"] = "\u24FC", ["ICON 9"] = "\u24FD", ["ICON 0"] = "\u24FF",
            ["ICON MOON"] = "\U0001F319", ["ICON HEADPHONES"] = "\U0001F3A7", ["ICON MUSIC"] = "\U0001F3B6", ["ICON FISH"] = "\U0001F41F",
            ["ICON LAPTOP"] = "\U0001F4BB", ["ICON DISKETTE"] = "\U0001F4BE", ["ICON WRITE"] = "\U0001F4DD",
            ["ICON WEBCAM"] = "\U0001F4F7", ["ICON CAMERA"] = "\U0001F4F8", ["ICON SPEAKER"] = "\U0001F508", ["ICON NOISE"] = "\U0001F56C",
            ["ICON CPU"] = "\U0001F5A5", ["ICON NET"] = "\U0001F5A7", ["ICON GPU"] = "\U0001F5A8", ["ICON RAM"] = "\U0001F5AA",
            ["ICON USB"] = "\U0001F5AB", ["ICON DB"] = "\U0001F5AC", ["ICON HDD"] = "\U0001F5B4", ["ICON SCREEN"] = "\U0001F5B5",
            ["ICON TEXT"] = "\U0001F5B9", ["ICON IMAGE"] = "\U0001F5BC", ["ICON SPEAK"] = "\U0001F5E3", ["ICON LANG"] = "\U0001F5E9",
            ["ICON EXIT"] = "\U0001F6AA", ["ICON INFO"] = "\U0001F6C8", ["ICON CART"] = "\U0001F6D2", ["ICON APERTURE"] = "\U0001F789"
        };
        
        private StackPanel CreateKeysPanelFromCombo(string keyCombo, Brush keyBackground)
        {
            var keysPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var keyParts = keyCombo.Split('+');
            
            for (int i = 0; i < keyParts.Length; i++)
            {
                var keyPart = keyParts[i].Trim();
                if (string.IsNullOrEmpty(keyPart)) continue;
                
                if (HotkeyIconMap.TryGetValue(keyPart, out var glyph))
                {
                    var icon = new FontIcon
                    {
                        Glyph = glyph,
                        FontFamily = new FontFamily(PromptFontFamily),
                        FontSize = 24,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    keysPanel.Children.Add(icon);
                }
                else
                {
                    var keyText = new TextBlock
                    {
                        Text = keyPart,
                        FontFamily = new FontFamily(PromptFontFamily),
                        FontSize = 24,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    keysPanel.Children.Add(keyText);
                }
                
                if (i < keyParts.Length - 1)
                {
                    var plusText = new TextBlock
                    {
                        Text = "+",
                        FontFamily = new FontFamily(PromptFontFamily),
                        FontSize = 18,
                        VerticalAlignment = VerticalAlignment.Center,
                        Opacity = 0.6
                    };
                    keysPanel.Children.Add(plusText);
                }
            }
            return keysPanel;
        }
        
        private Border CreateHotkeyActionButton(string glyph, Brush foreground)
        {
            var border = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            
            var icon = new FontIcon
            {
                Glyph = glyph,
                FontSize = 16,
                Foreground = foreground,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                RenderTransform = new ScaleTransform { ScaleX = 1, ScaleY = 1 },
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5)
            };
            
            border.Child = icon;
            
            border.PointerEntered += (s, e) => {
                border.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
                if (icon.RenderTransform is ScaleTransform st) { st.ScaleX = 1.1; st.ScaleY = 1.1; }
            };
            border.PointerExited += (s, e) => {
                border.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                if (icon.RenderTransform is ScaleTransform st) { st.ScaleX = 1.0; st.ScaleY = 1.0; }
            };
            
            return border;
        }
        
        private class HotkeyButtonData
        {
            public string KeyCombo { get; set; } = "";
            public string Description { get; set; } = "";
            public int OriginalIndex { get; set; }
        }
        
        private async void FavoriteButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border && border.Tag is HotkeyButtonData data && border.Child is FontIcon icon)
            {
                var accentColor = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
                var accentBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(accentColor.A, accentColor.R, accentColor.G, accentColor.B));
                var defaultBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.6 };
                
                bool wasFavorite = _favoriteHotkeys.Contains(data.KeyCombo);
                
                if (wasFavorite)
                {
                    _favoriteHotkeys.Remove(data.KeyCombo);
                    icon.Glyph = "\uE734";
                    icon.Foreground = defaultBrush;
                    await SaveFavoriteHotkeys();
                    AnimateHotkeyFromFavorites(data.KeyCombo);
                }
                else
                {
                    _favoriteHotkeys.Add(data.KeyCombo);
                    icon.Glyph = "\uE735";
                    icon.Foreground = accentBrush;
                    await SaveFavoriteHotkeys();
                    AnimateHotkeyToFavorites(data.KeyCombo);
                }
            }
        }
        
        private void EditButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border editBorder && editBorder.Tag is HotkeyButtonData data)
            {
                var parent = editBorder.Parent as StackPanel;
                if (parent == null) return;
                
                var grid = parent.Parent as Grid;
                if (grid == null) return;
                
                // Show rec and save buttons, hide edit button (buttons: fav[0], edit[1], rec[2], save[3])
                editBorder.Visibility = Visibility.Collapsed;
                if (parent.Children.Count > 2 && parent.Children[2] is Border recBtn)
                    recBtn.Visibility = Visibility.Visible;
                if (parent.Children.Count > 3 && parent.Children[3] is Border saveBtn)
                    saveBtn.Visibility = Visibility.Visible;
                
                // Replace keys panel with editable TextBox
                var keysPanel = grid.Children.OfType<StackPanel>().FirstOrDefault();
                if (keysPanel != null)
                {
                    var editBox = new TextBox
                    {
                        Text = data.KeyCombo,
                        FontFamily = new FontFamily("Consolas"),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 14,
                        MinWidth = 120,
                        Tag = "HotkeyEditBox",
                        PlaceholderText = "Press keys..."
                    };
                    editBox.PreviewKeyDown += HotkeyEditBox_PreviewKeyDown;
                    editBox.TextChanged += HotkeyEditBox_TextChanged;
                    
                    int idx = grid.Children.IndexOf(keysPanel);
                    Grid.SetColumn(editBox, 0);
                    grid.Children.RemoveAt(idx);
                    grid.Children.Insert(idx, editBox);
                    editBox.Focus(FocusState.Programmatic);
                }
            }
        }
        
        private void RecButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Border recBorder || recBorder.Tag is not HotkeyButtonData data) return;
            
            var parent = recBorder.Parent as StackPanel;
            if (parent == null) return;
            
            var grid = parent.Parent as Grid;
            if (grid == null) return;
            
            if (_isRecordingGamepad)
            {
                StopGamepadListening();
                return;
            }
            
            // Check if already in edit mode
            var editBox = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Tag as string == "HotkeyEditBox");
            
            if (editBox == null)
            {
                // Enter edit mode first - show save button, hide edit button (buttons: fav[0], edit[1], rec[2], save[3])
                if (parent.Children.Count > 1 && parent.Children[1] is Border editBtn)
                    editBtn.Visibility = Visibility.Collapsed;
                if (parent.Children.Count > 3 && parent.Children[3] is Border saveBtn)
                    saveBtn.Visibility = Visibility.Visible;
                
                // Replace keys panel with TextBox
                var keysPanel = grid.Children.OfType<StackPanel>().FirstOrDefault();
                if (keysPanel != null)
                {
                    editBox = new TextBox
                    {
                        Text = data.KeyCombo,
                        FontFamily = new FontFamily("Consolas"),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 14,
                        MinWidth = 120,
                        Tag = "HotkeyEditBox",
                        PlaceholderText = "Press keys..."
                    };
                    editBox.PreviewKeyDown += HotkeyEditBox_PreviewKeyDown;
                    editBox.TextChanged += HotkeyEditBox_TextChanged;
                    
                    int idx = grid.Children.IndexOf(keysPanel);
                    Grid.SetColumn(editBox, 0);
                    grid.Children.RemoveAt(idx);
                    grid.Children.Insert(idx, editBox);
                }
            }
            
            if (editBox != null)
            {
                StartGamepadListening(editBox, recBorder);
            }
        }
        
        private bool _isRecordingGamepad = false;
        private Border? _activeRecBorder;
        private DispatcherTimer? _holdTimer;
        private int _holdCountdown = 3;
        private string _pendingCombo = "";
        
        private void StartGamepadListening(TextBox editBox, Border recBorder)
        {
            _activeHotkeyEditBox = editBox;
            _activeRecBorder = recBorder;
            _recordedGamepadButtons.Clear();
            _pendingCombo = "";
            _holdCountdown = 3;
            
            if (_hotkeyGamepad == null)
                _hotkeyGamepad = new GamepadManager();
            
            if (!_hotkeyGamepad.CheckConnection())
            {
                editBox.PlaceholderText = "No controller";
                return;
            }
            
            _isRecordingGamepad = true;
            _previousHotkeysEnabled = SettingsManager.Current.HotkeysEnabled;
            SettingsManager.Current.HotkeysEnabled = false;
            
            editBox.Text = "";
            editBox.PlaceholderText = "Press & hold gamepad...";
            if (recBorder.Child is FontIcon icon)
                icon.Glyph = "\uE71A"; // Stop icon
            
            _hotkeyGamepad.ButtonPressed += OnHotkeyGamepadButtonPressed;
            _hotkeyGamepad.ButtonReleased += OnHotkeyGamepadButtonReleased;
            _hotkeyGamepad.StartPolling();
        }
        
        private void StopGamepadListening()
        {
            _isRecordingGamepad = false;
            _holdTimer?.Stop();
            _holdTimer = null;
            _holdCountdown = 3;
            _pendingCombo = "";
            
            SettingsManager.Current.HotkeysEnabled = _previousHotkeysEnabled;
            
            if (_hotkeyGamepad != null)
            {
                _hotkeyGamepad.ButtonPressed -= OnHotkeyGamepadButtonPressed;
                _hotkeyGamepad.ButtonReleased -= OnHotkeyGamepadButtonReleased;
                _hotkeyGamepad.StopPolling();
            }
            
            if (_activeRecBorder?.Child is FontIcon icon)
                icon.Glyph = "\uE7C8"; // Rec icon
            
            if (_activeHotkeyEditBox != null)
                _activeHotkeyEditBox.PlaceholderText = "Press keys...";
            
            _activeHotkeyEditBox = null;
            _activeRecBorder = null;
            _recordedGamepadButtons.Clear();
        }
        
        private void OnHotkeyGamepadButtonReleased(object? sender, GamepadButtonEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _holdTimer?.Stop();
                _holdTimer = null;
                _holdCountdown = 3;
                
                if (_activeHotkeyEditBox != null)
                {
                    _isSettingHotkeyText = true;
                    
                    if (_recordedGamepadButtons.Count <= 1)
                    {
                        // Single button - keep it, clear for next input
                        _activeHotkeyEditBox.Text = _pendingCombo;
                    }
                    else
                    {
                        // Combo released before 3s - clear it
                        _activeHotkeyEditBox.Text = "";
                        _pendingCombo = "";
                    }
                    
                    // Always clear recorded buttons after release - next press starts fresh
                    _recordedGamepadButtons.Clear();
                    
                    _isSettingHotkeyText = false;
                }
            });
        }
        
        private void HotkeyEditBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSettingHotkeyText) return;
            if (sender is TextBox editBox && string.IsNullOrEmpty(editBox.Text))
            {
                _recordedGamepadButtons.Clear();
            }
        }
        
        private void OnHotkeyGamepadButtonPressed(object? sender, GamepadButtonEventArgs e)
        {
            var buttonName = e.GetButtonDisplayName();
            
            if (!_recordedGamepadButtons.Contains("XB " + buttonName))
                _recordedGamepadButtons.Add("XB " + buttonName);
            
            _pendingCombo = string.Join("+", _recordedGamepadButtons);
            _holdCountdown = 3;
            
            DispatcherQueue.TryEnqueue(() =>
            {
                _holdTimer?.Stop();
                
                if (_activeHotkeyEditBox != null)
                {
                    _isSettingHotkeyText = true;
                    
                    if (_recordedGamepadButtons.Count == 1)
                    {
                        // Single button - no countdown, just show it
                        _activeHotkeyEditBox.Text = _pendingCombo;
                    }
                    else
                    {
                        // Combo - start countdown timer
                        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                        _holdTimer.Tick += HoldTimer_Tick;
                        _holdTimer.Start();
                        _activeHotkeyEditBox.Text = $"{_pendingCombo} ({_holdCountdown})";
                    }
                    
                    _isSettingHotkeyText = false;
                }
            });
        }
        
        private void HoldTimer_Tick(object? sender, object e)
        {
            _holdCountdown--;
            
            if (_holdCountdown <= 0)
            {
                _holdTimer?.Stop();
                _holdTimer = null;
                
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_activeHotkeyEditBox != null)
                    {
                        _isSettingHotkeyText = true;
                        _activeHotkeyEditBox.Text = _pendingCombo;
                        _isSettingHotkeyText = false;
                    }
                    StopGamepadListening();
                });
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_activeHotkeyEditBox != null)
                    {
                        _isSettingHotkeyText = true;
                        _activeHotkeyEditBox.Text = $"{_pendingCombo} ({_holdCountdown})";
                        _isSettingHotkeyText = false;
                    }
                });
            }
        }
        
        private void HotkeyEditBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (sender is not TextBox editBox) return;
            
            // Ignore keyboard during gamepad recording
            if (_isRecordingGamepad)
            {
                e.Handled = true;
                return;
            }
            
            e.Handled = true;
            _recordedGamepadButtons.Clear();
            
            var modifiers = new List<string>();
            
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                modifiers.Add("CTRL");
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                modifiers.Add("SHIFT");
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                modifiers.Add("ALT");
            
            var key = e.Key;
            
            // Skip modifier-only keys
            if (key == Windows.System.VirtualKey.Control || key == Windows.System.VirtualKey.Shift ||
                key == Windows.System.VirtualKey.Menu || key == Windows.System.VirtualKey.LeftWindows ||
                key == Windows.System.VirtualKey.RightWindows || key == Windows.System.VirtualKey.LeftControl ||
                key == Windows.System.VirtualKey.RightControl || key == Windows.System.VirtualKey.LeftShift ||
                key == Windows.System.VirtualKey.RightShift || key == Windows.System.VirtualKey.LeftMenu ||
                key == Windows.System.VirtualKey.RightMenu)
                return;
            
            // Convert key to display string
            string keyStr = ConvertVirtualKeyToString(key);
            
            if (modifiers.Count > 0)
                editBox.Text = string.Join("+", modifiers) + "+" + keyStr;
            else
                editBox.Text = keyStr;
        }
        
        private string ConvertVirtualKeyToString(Windows.System.VirtualKey key)
        {
            return key switch
            {
                >= Windows.System.VirtualKey.A and <= Windows.System.VirtualKey.Z => key.ToString(),
                >= Windows.System.VirtualKey.Number0 and <= Windows.System.VirtualKey.Number9 => ((int)key - (int)Windows.System.VirtualKey.Number0).ToString(),
                >= Windows.System.VirtualKey.NumberPad0 and <= Windows.System.VirtualKey.NumberPad9 => "NUM " + ((int)key - (int)Windows.System.VirtualKey.NumberPad0),
                >= Windows.System.VirtualKey.F1 and <= Windows.System.VirtualKey.F12 => "F" + ((int)key - (int)Windows.System.VirtualKey.F1 + 1),
                Windows.System.VirtualKey.Up => "↑",
                Windows.System.VirtualKey.Down => "↓",
                Windows.System.VirtualKey.Left => "←",
                Windows.System.VirtualKey.Right => "→",
                Windows.System.VirtualKey.Space => "SPACE",
                Windows.System.VirtualKey.Enter => "ENTER",
                Windows.System.VirtualKey.Tab => "TAB",
                Windows.System.VirtualKey.Escape => "ESC",
                Windows.System.VirtualKey.Back => "BACKSPACE",
                Windows.System.VirtualKey.Delete => "DEL",
                Windows.System.VirtualKey.Insert => "INS",
                Windows.System.VirtualKey.Home => "HOME",
                Windows.System.VirtualKey.End => "END",
                Windows.System.VirtualKey.PageUp => "PAGE UP",
                Windows.System.VirtualKey.PageDown => "PAGE DOWN",
                _ => key.ToString().ToUpper()
            };
        }
        
        private async void SaveButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            StopGamepadListening();
            
            if (sender is Border saveBorder && saveBorder.Tag is HotkeyButtonData data)
            {
                var parent = saveBorder.Parent as StackPanel;
                if (parent == null) return;
                
                var grid = parent.Parent as Grid;
                if (grid == null) return;
                
                // Find edit TextBox directly in grid
                var editBox = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Tag as string == "HotkeyEditBox");
                if (editBox == null) return;
                
                // Remove countdown suffix if present
                string newKeyCombo = editBox.Text.Trim().ToUpper();
                var countdownMatch = System.Text.RegularExpressions.Regex.Match(newKeyCombo, @"\s*\(\d+S\)$");
                if (countdownMatch.Success)
                    newKeyCombo = newKeyCombo.Substring(0, countdownMatch.Index).Trim();
                if (newKeyCombo.EndsWith("(RELEASED)"))
                    newKeyCombo = newKeyCombo.Replace("(RELEASED)", "").Trim();
                if (string.IsNullOrEmpty(newKeyCombo)) newKeyCombo = data.KeyCombo;
                
                // Update data
                data.KeyCombo = newKeyCombo;
                
                // Replace TextBox with keys panel
                var keyBackground = (Brush)Application.Current.Resources["HotkeyKeyBackground"];
                var keysPanel = CreateKeysPanelFromCombo(newKeyCombo, keyBackground);
                
                int idx = grid.Children.IndexOf(editBox);
                Grid.SetColumn(keysPanel, 0);
                grid.Children.RemoveAt(idx);
                grid.Children.Insert(idx, keysPanel);
                
                // Update row border tag
                var rowBorder = grid.Parent as Border;
                if (rowBorder?.Tag is HotkeyRowData rowData)
                {
                    rowData.Key = newKeyCombo;
                }
                
                // Update all button tags
                foreach (var btn in parent.Children.OfType<Border>())
                {
                    if (btn.Tag is HotkeyButtonData btnData)
                        btnData.KeyCombo = newKeyCombo;
                }
                
                // Hide save, show edit (buttons: fav[0], edit[1], rec[2], save[3]) - rec stays visible
                saveBorder.Visibility = Visibility.Collapsed;
                if (parent.Children.Count > 1 && parent.Children[1] is Border editBtn)
                    editBtn.Visibility = Visibility.Visible;
                
                // Save to mod.json
                await SaveHotkeyChange(data.OriginalIndex, newKeyCombo, data.Description);
            }
        }
        
        private async Task SaveHotkeyChange(int index, string newKey, string description)
        {
            if (string.IsNullOrEmpty(_modJsonPath) || !File.Exists(_modJsonPath)) return;
            
            try
            {
                var json = await File.ReadAllTextAsync(_modJsonPath);
                var modData = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                
                if (modData.TryGetValue("hotkeys", out var hotkeysObj) && hotkeysObj is JsonElement hotkeysElement)
                {
                    var hotkeys = new List<Dictionary<string, string>>();
                    foreach (var h in hotkeysElement.EnumerateArray())
                    {
                        hotkeys.Add(new Dictionary<string, string>
                        {
                            ["key"] = h.GetProperty("key").GetString() ?? "",
                            ["description"] = h.GetProperty("description").GetString() ?? ""
                        });
                    }
                    
                    if (index >= 0 && index < hotkeys.Count)
                    {
                        hotkeys[index]["key"] = newKey;
                    }
                    
                    modData["hotkeys"] = hotkeys;
                    
                    var newJson = JsonSerializer.Serialize(modData, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(_modJsonPath, newJson);
                    Logger.LogInfo($"Hotkey updated: {newKey} - {description}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save hotkey change", ex);
            }
        }
        
        // Helper class to store hotkey row data
        private class HotkeyRowData
        {
            public string Key { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int OriginalIndex { get; set; }
        }
        
        private void AnimateHotkeyToFavorites(string keyCombo)
        {
            // Find the border with this key
            Border? targetBorder = null;
            int currentIndex = -1;
            
            for (int i = 0; i < ModHotkeysPanel.Children.Count; i++)
            {
                if (ModHotkeysPanel.Children[i] is Border border && border.Tag is HotkeyRowData data && data.Key == keyCombo)
                {
                    targetBorder = border;
                    currentIndex = i;
                    break;
                }
            }
            
            if (targetBorder == null || currentIndex <= 0)
            {
                // Already at top or not found - just reorder
                ReorderHotkeys();
                return;
            }
            
            // Create slide-up animation for the target
            var transform = targetBorder.RenderTransform as CompositeTransform ?? new CompositeTransform();
            targetBorder.RenderTransform = transform;
            
            // Find target position (after existing favorites, maintaining original order)
            int targetPosition = 0;
            int targetOriginalIndex = -1;
            if (targetBorder.Tag is HotkeyRowData targetData)
            {
                targetOriginalIndex = targetData.OriginalIndex;
            }
            
            // Count how many favorites with lower original index exist
            for (int i = 0; i < ModHotkeysPanel.Children.Count; i++)
            {
                if (ModHotkeysPanel.Children[i] is Border border && border.Tag is HotkeyRowData data)
                {
                    if (_favoriteHotkeys.Contains(data.Key) && data.Key != keyCombo && data.OriginalIndex < targetOriginalIndex)
                    {
                        targetPosition++;
                    }
                }
            }
            
            // Calculate distance to move to target position
            double totalHeight = 0;
            for (int i = targetPosition; i < currentIndex; i++)
            {
                if (ModHotkeysPanel.Children[i] is FrameworkElement elem)
                {
                    totalHeight += elem.ActualHeight + 6; // 6 is margin
                }
            }
            
            if (totalHeight <= 0)
            {
                ReorderHotkeys();
                return;
            }
            
            var slideUp = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = -totalHeight,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase 
                { 
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut 
                }
            };
            
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideUp, transform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideUp, "TranslateY");
            storyboard.Children.Add(slideUp);
            
            // Animate items between target position and current position sliding down
            for (int i = targetPosition; i < currentIndex; i++)
            {
                if (ModHotkeysPanel.Children[i] is Border otherBorder)
                {
                    var otherTransform = otherBorder.RenderTransform as CompositeTransform ?? new CompositeTransform();
                    otherBorder.RenderTransform = otherTransform;
                    
                    var slideDown = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        From = 0,
                        To = targetBorder.ActualHeight + 6,
                        Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase 
                        { 
                            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut 
                        }
                    };
                    
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideDown, otherTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideDown, "TranslateY");
                    storyboard.Children.Add(slideDown);
                }
            }
            
            storyboard.Completed += (s, args) =>
            {
                // Reset transforms and reorder
                foreach (var child in ModHotkeysPanel.Children)
                {
                    if (child is Border border && border.RenderTransform is CompositeTransform ct)
                    {
                        ct.TranslateY = 0;
                    }
                }
                ReorderHotkeys();
            };
            
            storyboard.Begin();
        }
        
        private void AnimateHotkeyFromFavorites(string keyCombo)
        {
            // Find the border with this key
            Border? targetBorder = null;
            int currentIndex = -1;
            int targetOriginalIndex = -1;
            
            for (int i = 0; i < ModHotkeysPanel.Children.Count; i++)
            {
                if (ModHotkeysPanel.Children[i] is Border border && border.Tag is HotkeyRowData data && data.Key == keyCombo)
                {
                    targetBorder = border;
                    currentIndex = i;
                    targetOriginalIndex = data.OriginalIndex;
                    break;
                }
            }
            
            if (targetBorder == null)
            {
                ReorderHotkeys();
                return;
            }
            
            // Calculate target position (where it should go based on original index, after all favorites)
            int favoritesCount = 0;
            int targetPosition = 0;
            
            foreach (var child in ModHotkeysPanel.Children)
            {
                if (child is Border border && border.Tag is HotkeyRowData data)
                {
                    if (_favoriteHotkeys.Contains(data.Key))
                    {
                        favoritesCount++;
                    }
                }
            }
            
            // Find position among non-favorites based on original index
            targetPosition = favoritesCount;
            foreach (var child in ModHotkeysPanel.Children)
            {
                if (child is Border border && border.Tag is HotkeyRowData data)
                {
                    if (!_favoriteHotkeys.Contains(data.Key) && data.Key != keyCombo && data.OriginalIndex < targetOriginalIndex)
                    {
                        targetPosition++;
                    }
                }
            }
            
            if (targetPosition <= currentIndex)
            {
                // Already in correct position or moving up (shouldn't happen when removing from favorites)
                ReorderHotkeys();
                return;
            }
            
            // Calculate distance to move down
            double totalHeight = 0;
            for (int i = currentIndex + 1; i <= targetPosition && i < ModHotkeysPanel.Children.Count; i++)
            {
                if (ModHotkeysPanel.Children[i] is FrameworkElement elem)
                {
                    totalHeight += elem.ActualHeight + 6;
                }
            }
            
            if (totalHeight <= 0)
            {
                ReorderHotkeys();
                return;
            }
            
            var transform = targetBorder.RenderTransform as CompositeTransform ?? new CompositeTransform();
            targetBorder.RenderTransform = transform;
            
            var slideDown = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = totalHeight,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase 
                { 
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut 
                }
            };
            
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideDown, transform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideDown, "TranslateY");
            storyboard.Children.Add(slideDown);
            
            // Animate items between current position and target position sliding up
            for (int i = currentIndex + 1; i <= targetPosition && i < ModHotkeysPanel.Children.Count; i++)
            {
                if (ModHotkeysPanel.Children[i] is Border otherBorder)
                {
                    var otherTransform = otherBorder.RenderTransform as CompositeTransform ?? new CompositeTransform();
                    otherBorder.RenderTransform = otherTransform;
                    
                    var slideUp = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        From = 0,
                        To = -(targetBorder.ActualHeight + 6),
                        Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase 
                        { 
                            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut 
                        }
                    };
                    
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideUp, otherTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideUp, "TranslateY");
                    storyboard.Children.Add(slideUp);
                }
            }
            
            storyboard.Completed += (s, args) =>
            {
                // Reset transforms and reorder
                foreach (var child in ModHotkeysPanel.Children)
                {
                    if (child is Border border && border.RenderTransform is CompositeTransform ct)
                    {
                        ct.TranslateY = 0;
                    }
                }
                ReorderHotkeys();
            };
            
            storyboard.Begin();
        }
        
        private void ReorderHotkeys()
        {
            // Collect all hotkey borders with their data
            var hotkeyItems = new List<(Border border, string key, int originalIndex, bool isFavorite)>();
            
            foreach (var child in ModHotkeysPanel.Children)
            {
                if (child is Border border && border.Tag is HotkeyRowData data)
                {
                    hotkeyItems.Add((border, data.Key, data.OriginalIndex, _favoriteHotkeys.Contains(data.Key)));
                }
            }
            
            // Sort: favorites first (maintaining original order within favorites), then non-favorites (maintaining original order)
            var sorted = hotkeyItems
                .OrderByDescending(x => x.isFavorite)
                .ThenBy(x => x.originalIndex)
                .Select(x => x.border)
                .ToList();
            
            // Clear and re-add in sorted order
            ModHotkeysPanel.Children.Clear();
            foreach (var border in sorted)
            {
                ModHotkeysPanel.Children.Add(border);
            }
        }
        
        private async Task SaveFavoriteHotkeys()
        {
            if (string.IsNullOrEmpty(_modJsonPath) || !File.Exists(_modJsonPath)) return;
            
            try
            {
                await Services.FileAccessQueue.ExecuteAsync(_modJsonPath, async () =>
                {
                    var json = await File.ReadAllTextAsync(_modJsonPath);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var dict = new Dictionary<string, object?>();
                    
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Name == "favoriteHotkeys")
                            continue; // Will be replaced
                        else if (prop.Name == "author" || prop.Name == "url" || prop.Name == "version" || 
                                 prop.Name == "dateChecked" || prop.Name == "dateUpdated")
                            dict[prop.Name] = prop.Value.GetString();
                        else if (prop.Name == "isNSFW" || prop.Name == "statusKeeperSync")
                            dict[prop.Name] = prop.Value.GetBoolean();
                        else
                            dict[prop.Name] = prop.Value.Deserialize<object>();
                    }
                    
                    // Add favorite hotkeys array
                    dict["favoriteHotkeys"] = _favoriteHotkeys.ToList();
                    
                    var newJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(_modJsonPath, newJson);
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save favorite hotkeys", ex);
            }
        }
        
        private void ModUrlTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (ModUrlTextBox.Text == "https://")
            {
                ModUrlTextBox.Text = "";
                ModUrlTextBox.ClearValue(TextBox.ForegroundProperty); // Use default theme color
            }
        }
        
        private void ModUrlTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ModUrlTextBox.Text))
            {
                ModUrlTextBox.Text = "https://";
                ModUrlTextBox.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray);
            }
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
            // Don't save the placeholder text
            var urlValue = ModUrlTextBox.Text == "https://" ? "" : ModUrlTextBox.Text;
            await UpdateModJsonField("url", urlValue);
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

        private async void ModNSFWCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            await UpdateModJsonFieldBool("isNSFW", true);
            NSFWBadge.Visibility = Visibility.Visible;
        }

        private async void ModNSFWCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            await UpdateModJsonFieldBool("isNSFW", false);
            NSFWBadge.Visibility = Visibility.Collapsed;
        }

        private async void ModStatusKeeperSyncCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            await UpdateModJsonFieldBool("statusKeeperSync", true);
        }

        private async void ModStatusKeeperSyncCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            await UpdateModJsonFieldBool("statusKeeperSync", false);
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
                Logger.LogError($"mod.json does not exist at: {_modJsonPath}");
                return;
            }
            try
            {
                string? newJson = null;
                await Services.FileAccessQueue.ExecuteAsync(_modJsonPath, async () =>
                {
                    var json = await File.ReadAllTextAsync(_modJsonPath);
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
                    newJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(_modJsonPath, newJson);
                });
                
                // Re-check for updates after saving dateUpdated
                if (field == "dateUpdated" && newJson != null)
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

        private async Task UpdateModJsonFieldBool(string field, bool value)
        {
            if (string.IsNullOrEmpty(_modJsonPath)) return;
            if (!File.Exists(_modJsonPath))
            {
                Logger.LogError($"mod.json does not exist at: {_modJsonPath}");
                return;
            }
            try
            {
                await Services.FileAccessQueue.ExecuteAsync(_modJsonPath, async () =>
                {
                    var json = await File.ReadAllTextAsync(_modJsonPath);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var dict = new Dictionary<string, object?>();
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Name == field)
                            dict[field] = value;
                        else if (prop.Name == "author" || prop.Name == "url" || prop.Name == "version" || prop.Name == "dateChecked" || prop.Name == "dateUpdated")
                            dict[prop.Name] = prop.Value.GetString();
                        else if (prop.Name == "isNSFW" || prop.Name == "statusKeeperSync")
                            dict[prop.Name] = prop.Value.GetBoolean();
                        else
                            dict[prop.Name] = prop.Value.Deserialize<object>();
                    }
                    if (!dict.ContainsKey(field))
                        dict[field] = value;
                    var newJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(_modJsonPath, newJson);
                });
                
                // Trigger NSFW filtering if enabled
                if (field == "isNSFW")
                {
                    var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                    if (mainWindow?.CurrentModGridPage != null)
                    {
                        mainWindow.CurrentModGridPage.FilterNSFWMods(SettingsManager.Current.BlurNSFWThumbnails);
                    }
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
            e.Handled = true;
            if (_availablePreviewImages.Count == 0) return;
            
            // Infinite carousel - wrap to last image
            _currentImageIndex = _currentImageIndex > 0 
                ? _currentImageIndex - 1 
                : _availablePreviewImages.Count - 1;
            LoadCurrentImage();
            UpdateImageNavigation();
        }

        private void NextImageButton_Click(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            e.Handled = true;
            if (_availablePreviewImages.Count == 0) return;
            
            // Infinite carousel - wrap to first image
            _currentImageIndex = _currentImageIndex < _availablePreviewImages.Count - 1 
                ? _currentImageIndex + 1 
                : 0;
            LoadCurrentImage();
            UpdateImageNavigation();
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
        
        // Image Preview - opens sliding panel
        private void ModImage_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(ModImageCoordinateField);
            if (point.Properties.IsLeftButtonPressed && _availablePreviewImages.Count > 0)
            {
                // Open image preview in sliding panel
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                var modName = ModDetailTitle.Text ?? "Preview";
                mainWindow?.ShowImagePreviewPanel(_availablePreviewImages, _currentImageIndex, modName);
            }
        }
    }
}