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
        
        public async Task LoadModDetails(string modDirectory, string category = "", string viewMode = "")
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
                
                // Set tooltip for Restore Default Hotkeys button
                ToolTipService.SetToolTip(RestoreDefaultHotkeysButton, SharedUtilities.GetTranslation(lang, "ModDetailPage_RestoreDefaultHotkeys_Tooltip"));
                
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
                        await LoadModJsonData();
                        
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

        private async Task LoadModJsonData()
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
                
                // Load hotkeys from hotkeys.json (cleanup already done at startup by hotkey finder)
                await LoadHotkeysFromFile();
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
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 0, 6),
                Height = 60,
                Tag = new HotkeyRowData { Key = keyCombo, Description = description, OriginalIndex = originalIndex },
                RenderTransform = new CompositeTransform()
            };
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Keys - auto width
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Spacer
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Description
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Buttons
            
            // Create keys panel
            var keysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(keyCombo, keyBackground, 64);
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
            
            // Set tooltip for favorite button
            var lang = SharedUtilities.LoadLanguageDictionary();
            ToolTipService.SetToolTip(favoriteBtn, SharedUtilities.GetTranslation(lang, isFavorite ? "ModDetailPage_UnfavoriteHotkey_Tooltip" : "ModDetailPage_FavoriteHotkey_Tooltip"));
            
            buttonsPanel.Children.Add(favoriteBtn);
            
            // Edit button
            var editBtn = CreateHotkeyActionButton("\uE70F", defaultBrush);
            editBtn.Tag = new HotkeyButtonData { KeyCombo = keyCombo, Description = description, OriginalIndex = originalIndex };
            editBtn.PointerPressed += EditButton_PointerPressed;
            
            // Set tooltip for edit button
            ToolTipService.SetToolTip(editBtn, SharedUtilities.GetTranslation(lang, "ModDetailPage_EditHotkey_Tooltip"));
            
            buttonsPanel.Children.Add(editBtn);
            
            // Rec button (always visible - for gamepad recording)
            var recBtn = CreateHotkeyActionButton("\uE7C8", defaultBrush);
            recBtn.Tag = new HotkeyButtonData { KeyCombo = keyCombo, Description = description, OriginalIndex = originalIndex };
            recBtn.PointerPressed += RecButton_PointerPressed;
            
            // Set tooltip for rec button
            ToolTipService.SetToolTip(recBtn, SharedUtilities.GetTranslation(lang, "ModDetailPage_RecordHotkey_Tooltip"));
            
            buttonsPanel.Children.Add(recBtn);
            
            // Save button (initially hidden, shown during edit)
            var saveBtn = CreateHotkeyActionButton("\uE73E", accentBrush);
            saveBtn.Tag = new HotkeyButtonData { KeyCombo = keyCombo, Description = description, OriginalIndex = originalIndex };
            saveBtn.Visibility = Visibility.Collapsed;
            saveBtn.PointerPressed += SaveButton_PointerPressed;
            
            // Set tooltip for save button
            ToolTipService.SetToolTip(saveBtn, SharedUtilities.GetTranslation(lang, "ModDetailPage_SaveHotkey_Tooltip"));
            
            buttonsPanel.Children.Add(saveBtn);
            
            // Restore default button (always visible)
            var restoreBtn = CreateHotkeyActionButton("\uE777", defaultBrush);
            restoreBtn.Tag = new HotkeyButtonData { KeyCombo = keyCombo, Description = description, OriginalIndex = originalIndex };
            restoreBtn.PointerPressed += RestoreDefaultButton_PointerPressed;
            
            // Set tooltip for restore button
            ToolTipService.SetToolTip(restoreBtn, SharedUtilities.GetTranslation(lang, "ModDetailPage_RestoreDefaultHotkey_Tooltip"));
            buttonsPanel.Children.Add(restoreBtn);
            
            Grid.SetColumn(buttonsPanel, 3);
            grid.Children.Add(buttonsPanel);
            
            rowBorder.Child = grid;
            return rowBorder;
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
            // Block if in edit mode or recording gamepad
            if (_activeHotkeyEditBox != null || _isRecordingGamepad)
                return;
                
            if (sender is Border border && border.Tag is HotkeyButtonData data && border.Child is FontIcon icon)
            {
                var accentColor = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
                var accentBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(accentColor.A, accentColor.R, accentColor.G, accentColor.B));
                var defaultBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.6 };
                
                bool wasFavorite = _favoriteHotkeys.Contains(data.KeyCombo);
                
                var lang = SharedUtilities.LoadLanguageDictionary();
                
                if (wasFavorite)
                {
                    _favoriteHotkeys.Remove(data.KeyCombo);
                    icon.Glyph = "\uE734";
                    icon.Foreground = defaultBrush;
                    ToolTipService.SetToolTip(border, SharedUtilities.GetTranslation(lang, "ModDetailPage_FavoriteHotkey_Tooltip"));
                    await SaveFavoriteHotkeys();
                    AnimateHotkeyFromFavorites(data.KeyCombo);
                }
                else
                {
                    _favoriteHotkeys.Add(data.KeyCombo);
                    icon.Glyph = "\uE735";
                    icon.Foreground = accentBrush;
                    ToolTipService.SetToolTip(border, SharedUtilities.GetTranslation(lang, "ModDetailPage_UnfavoriteHotkey_Tooltip"));
                    await SaveFavoriteHotkeys();
                    AnimateHotkeyToFavorites(data.KeyCombo);
                }
            }
        }
        
        private void EditButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Block if already in edit mode or recording gamepad
            if (_activeHotkeyEditBox != null || _isRecordingGamepad)
                return;
                
            if (sender is Border editBorder && editBorder.Tag is HotkeyButtonData data)
            {
                var parent = editBorder.Parent as StackPanel;
                if (parent == null) return;
                
                var grid = parent.Parent as Grid;
                if (grid == null) return;
                
                // Show rec and save buttons, hide edit button (buttons: fav[0], edit[1], rec[2], save[3], restore[4])
                editBorder.Visibility = Visibility.Collapsed;
                if (parent.Children.Count > 2 && parent.Children[2] is Border recBtn)
                    recBtn.Visibility = Visibility.Visible;
                if (parent.Children.Count > 3 && parent.Children[3] is Border saveBtn)
                    saveBtn.Visibility = Visibility.Visible;
                
                // Replace keys panel with editable TextBox
                var keysPanel = grid.Children.OfType<StackPanel>().FirstOrDefault(sp => sp.Tag as string != "EditContainer");
                if (keysPanel != null)
                {
                    var editBox = new TextBox
                    {
                        Text = data.KeyCombo,
                        FontFamily = new FontFamily("Consolas"),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 14,
                        Height = 32,
                        MinWidth = 120,
                        VerticalAlignment = VerticalAlignment.Center,
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
                    
                    // Set active edit box to block other buttons
                    _activeHotkeyEditBox = editBox;
                    
                    // Disable restore all button during edit
                    RestoreDefaultHotkeysButton.IsEnabled = false;
                }
            }
        }
        
        private void RecButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Border recBorder || recBorder.Tag is not HotkeyButtonData data) return;
            
            // Block if ANY hotkey is in edit mode or recording (one operation at a time)
            if (_activeHotkeyEditBox != null || _isRecordingGamepad)
                return;
            
            var parent = recBorder.Parent as StackPanel;
            if (parent == null) return;
            
            var grid = parent.Parent as Grid;
            if (grid == null) return;
            
            // Check if this is the same hotkey row or different one
            var editBox = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Tag as string == "HotkeyEditBox");
            
            if (editBox == null)
            {
                // Enter edit mode first - show save button, hide edit button (buttons: fav[0], edit[1], rec[2], save[3], restore[4])
                if (parent.Children.Count > 1 && parent.Children[1] is Border editBtn)
                    editBtn.Visibility = Visibility.Collapsed;
                if (parent.Children.Count > 3 && parent.Children[3] is Border saveBtn)
                    saveBtn.Visibility = Visibility.Visible;
                
                // Replace keys panel with TextBox + countdown
                var keysPanel = grid.Children.OfType<StackPanel>().FirstOrDefault(sp => sp.Tag as string != "EditContainer");
                if (keysPanel != null)
                {
                    // Create container for TextBox and countdown
                    var editContainer = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = "EditContainer"
                    };
                    
                    editBox = new TextBox
                    {
                        Text = data.KeyCombo,
                        FontFamily = new FontFamily("Consolas"),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 14,
                        Height = 32,
                        MinWidth = 120,
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = "HotkeyEditBox",
                        PlaceholderText = "Press keys..."
                    };
                    editBox.PreviewKeyDown += HotkeyEditBox_PreviewKeyDown;
                    editBox.TextChanged += HotkeyEditBox_TextChanged;
                    editContainer.Children.Add(editBox);
                    
                    // Countdown TextBlock (hidden initially)
                    _countdownTextBlock = new TextBlock
                    {
                        FontFamily = new FontFamily("ms-appx:///Assets/KenneyFonts/kenney_input_keyboard_mouse.ttf#Kenney Input Keyboard & Mouse"),
                        FontSize = 32,
                        VerticalAlignment = VerticalAlignment.Center,
                        Visibility = Visibility.Collapsed
                    };
                    editContainer.Children.Add(_countdownTextBlock);
                    
                    int idx = grid.Children.IndexOf(keysPanel);
                    Grid.SetColumn(editContainer, 0);
                    grid.Children.RemoveAt(idx);
                    grid.Children.Insert(idx, editContainer);
                    
                    // Set active edit box and disable restore all button
                    _activeHotkeyEditBox = editBox;
                    RestoreDefaultHotkeysButton.IsEnabled = false;
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
        private TextBlock? _countdownTextBlock;
        
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
                // Stop any ongoing vibration before stopping polling
                _hotkeyGamepad.Vibrate(0, 0, 0);
                _hotkeyGamepad.StopPolling();
            }
            
            if (_activeHotkeyEditBox != null)
                _activeHotkeyEditBox.PlaceholderText = "Press keys...";
            
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
            
            // buttonName already contains the controller prefix (e.g., "PS △", "XB A", "NIN B")
            if (!_recordedGamepadButtons.Contains(buttonName))
                _recordedGamepadButtons.Add(buttonName);
            
            _pendingCombo = string.Join("+", _recordedGamepadButtons);
            _holdCountdown = 3;
            
            DispatcherQueue.TryEnqueue(() =>
            {
                _holdTimer?.Stop();
                
                if (_activeHotkeyEditBox != null)
                {
                    _isSettingHotkeyText = true;
                    _activeHotkeyEditBox.Text = _pendingCombo;
                    
                    if (_recordedGamepadButtons.Count == 1)
                    {
                        // Single button - no countdown
                        if (_countdownTextBlock != null)
                            _countdownTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        // Combo - start countdown timer
                        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                        _holdTimer.Tick += HoldTimer_Tick;
                        _holdTimer.Start();
                        
                        // Show countdown with Kenney font
                        if (_countdownTextBlock != null)
                        {
                            var countdownGlyph = _holdCountdown switch
                            {
                                3 => "\uE007", // "3" in Kenney Keyboard
                                2 => "\uE005", // "2" in Kenney Keyboard
                                1 => "\uE003", // "1" in Kenney Keyboard
                                _ => _holdCountdown.ToString()
                            };
                            _countdownTextBlock.Text = countdownGlyph;
                            _countdownTextBlock.Visibility = Visibility.Visible;
                        }
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
                
                DispatcherQueue.TryEnqueue(async () =>
                {
                    if (_activeHotkeyEditBox != null)
                    {
                        _isSettingHotkeyText = true;
                        _activeHotkeyEditBox.Text = _pendingCombo;
                        _isSettingHotkeyText = false;
                    }
                    
                    // Hide countdown when done
                    if (_countdownTextBlock != null)
                    {
                        _countdownTextBlock.Visibility = Visibility.Collapsed;
                    }
                    
                    // Confirmation vibration 400ms
                    _hotkeyGamepad?.Vibrate(30000, 30000, 400);
                    // Wait for vibration to finish before stopping
                    await Task.Delay(450);
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
                        _activeHotkeyEditBox.Text = _pendingCombo;
                        _isSettingHotkeyText = false;
                    }
                    
                    // Update countdown TextBlock
                    if (_countdownTextBlock != null)
                    {
                        var countdownGlyph = _holdCountdown switch
                        {
                            3 => "\uE007", // "3" in Kenney Keyboard
                            2 => "\uE005", // "2" in Kenney Keyboard
                            1 => "\uE003", // "1" in Kenney Keyboard
                            _ => _holdCountdown.ToString()
                        };
                        _countdownTextBlock.Text = countdownGlyph;
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
        
        private async void RestoreDefaultButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Block if in edit mode or recording gamepad
            if (_activeHotkeyEditBox != null || _isRecordingGamepad)
                return;
                
            if (sender is Border restoreBorder && restoreBorder.Tag is HotkeyButtonData data)
            {
                try
                {
                    // Load default hotkey from defaultHotkeys section in mod.json
                    var defaultHotkey = await GetDefaultHotkeyForDescription(data.Description);
                    if (!string.IsNullOrEmpty(defaultHotkey))
                    {
                        var parent = restoreBorder.Parent as StackPanel;
                        if (parent == null) return;
                        
                        var grid = parent.Parent as Grid;
                        if (grid == null) return;
                        
                        // Update the keys panel with default hotkey
                        var keyBackground = (Brush)Application.Current.Resources["HotkeyKeyBackground"];
                        var keysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(defaultHotkey, keyBackground, 64);
                        
                        // Find and replace existing keys panel
                        var existingKeysPanel = grid.Children.OfType<StackPanel>().FirstOrDefault();
                        if (existingKeysPanel != null)
                        {
                            int idx = grid.Children.IndexOf(existingKeysPanel);
                            Grid.SetColumn(keysPanel, 0);
                            grid.Children.RemoveAt(idx);
                            grid.Children.Insert(idx, keysPanel);
                        }
                        
                        // Update data
                        data.KeyCombo = defaultHotkey;
                        
                        // Update row border tag
                        var rowBorder = grid.Parent as Border;
                        if (rowBorder?.Tag is HotkeyRowData rowData)
                        {
                            rowData.Key = defaultHotkey;
                        }
                        
                        // Update all button tags in this row
                        foreach (var btn in parent.Children.OfType<Border>())
                        {
                            if (btn.Tag is HotkeyButtonData btnData)
                                btnData.KeyCombo = defaultHotkey;
                        }
                        
                        // Save to mod.json
                        await SaveHotkeyChange(data.OriginalIndex, defaultHotkey, data.Description);
                        
                        Logger.LogInfo($"Restored default hotkey: {defaultHotkey} - {data.Description}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to restore default hotkey", ex);
                }
            }
        }

        private async void SaveButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Logger.LogInfo("SaveButton_PointerPressed called");
            StopGamepadListening();
            
            if (sender is Border saveBorder && saveBorder.Tag is HotkeyButtonData data)
            {
                var parent = saveBorder.Parent as StackPanel;
                if (parent == null) return;
                
                var grid = parent.Parent as Grid;
                if (grid == null) return;
                
                // Find edit container (StackPanel with Tag "EditContainer") or TextBox directly
                TextBox? editBox = null;
                UIElement? elementToReplace = null;
                
                var editContainer = grid.Children.OfType<StackPanel>().FirstOrDefault(sp => sp.Tag as string == "EditContainer");
                if (editContainer != null)
                {
                    editBox = editContainer.Children.OfType<TextBox>().FirstOrDefault(t => t.Tag as string == "HotkeyEditBox");
                    elementToReplace = editContainer;
                }
                else
                {
                    editBox = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Tag as string == "HotkeyEditBox");
                    elementToReplace = editBox;
                }
                
                if (editBox == null || elementToReplace == null) return;
                
                // Clean up combo text
                string newKeyCombo = editBox.Text.Trim().ToUpper();
                var countdownMatch = System.Text.RegularExpressions.Regex.Match(newKeyCombo, @"\s*\(\d+S\)$");
                if (countdownMatch.Success)
                    newKeyCombo = newKeyCombo.Substring(0, countdownMatch.Index).Trim();
                if (newKeyCombo.EndsWith("(RELEASED)"))
                    newKeyCombo = newKeyCombo.Replace("(RELEASED)", "").Trim();
                if (string.IsNullOrEmpty(newKeyCombo)) newKeyCombo = data.KeyCombo;
                
                // Update data
                data.KeyCombo = newKeyCombo;
                
                // Replace edit container/TextBox with keys panel
                var keyBackground = (Brush)Application.Current.Resources["HotkeyKeyBackground"];
                var keysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(newKeyCombo, keyBackground, 64);
                
                int idx = grid.Children.IndexOf(elementToReplace);
                Grid.SetColumn(keysPanel, 0);
                grid.Children.RemoveAt(idx);
                grid.Children.Insert(idx, keysPanel);
                
                // Clear countdown reference
                _countdownTextBlock = null;
                
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
                
                // Hide save, show edit (buttons: fav[0], edit[1], rec[2], save[3], restore[4]) - rec and restore stay visible
                saveBorder.Visibility = Visibility.Collapsed;
                if (parent.Children.Count > 1 && parent.Children[1] is Border editBtn)
                    editBtn.Visibility = Visibility.Visible;
                
                // Save to mod.json
                await SaveHotkeyChange(data.OriginalIndex, newKeyCombo, data.Description);
                
                // Reset active edit box to unblock other buttons
                _activeHotkeyEditBox = null;
                
                // Re-enable restore all button
                RestoreDefaultHotkeysButton.IsEnabled = true;
            }
        }
        
        private async Task SaveHotkeyChange(int index, string newKey, string description)
        {
            if (string.IsNullOrEmpty(_currentModDirectory)) return;
            
            Logger.LogInfo($"SaveHotkeyChange called: index={index}, newKey={newKey}, description={description}, modDir={PathManager.GetRelativePath(_currentModDirectory)}");
            
            try
            {
                var hotkeysJsonPath = Path.Combine(_currentModDirectory, "hotkeys.json");
                
                if (!File.Exists(hotkeysJsonPath))
                {
                    Logger.LogWarning($"Hotkeys file not found: {hotkeysJsonPath}");
                    return;
                }
                
                var json = await File.ReadAllTextAsync(hotkeysJsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                var defaultHotkeys = new List<Dictionary<string, object>>();
                var hotkeys = new List<Dictionary<string, object>>();
                
                // Preserve default hotkeys with all properties
                if (root.TryGetProperty("defaultHotkeys", out var defaultProp) && defaultProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in defaultProp.EnumerateArray())
                    {
                        var hotkey = new Dictionary<string, object>
                        {
                            ["key"] = h.GetProperty("key").GetString() ?? "",
                            ["description"] = h.GetProperty("description").GetString() ?? ""
                        };
                        
                        // Preserve additional properties if they exist
                        if (h.TryGetProperty("iniFile", out var iniFileProp))
                            hotkey["iniFile"] = iniFileProp.GetString() ?? "";
                        if (h.TryGetProperty("section", out var sectionProp))
                            hotkey["section"] = sectionProp.GetString() ?? "";
                        if (h.TryGetProperty("keyName", out var keyNameProp))
                            hotkey["keyName"] = keyNameProp.GetString() ?? "";
                        if (h.TryGetProperty("originalValue", out var originalValueProp))
                            hotkey["originalValue"] = originalValueProp.GetString() ?? "";
                            
                        defaultHotkeys.Add(hotkey);
                    }
                }
                
                // Update current hotkeys with all properties
                if (root.TryGetProperty("hotkeys", out var hotkeysProp) && hotkeysProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in hotkeysProp.EnumerateArray())
                    {
                        var hotkey = new Dictionary<string, object>
                        {
                            ["key"] = h.GetProperty("key").GetString() ?? "",
                            ["description"] = h.GetProperty("description").GetString() ?? ""
                        };
                        
                        // Preserve additional properties if they exist
                        if (h.TryGetProperty("iniFile", out var iniFileProp))
                            hotkey["iniFile"] = iniFileProp.GetString() ?? "";
                        if (h.TryGetProperty("section", out var sectionProp))
                            hotkey["section"] = sectionProp.GetString() ?? "";
                        if (h.TryGetProperty("keyName", out var keyNameProp))
                            hotkey["keyName"] = keyNameProp.GetString() ?? "";
                        if (h.TryGetProperty("originalValue", out var originalValueProp))
                            hotkey["originalValue"] = originalValueProp.GetString() ?? "";
                            
                        hotkeys.Add(hotkey);
                    }
                }
                
                // Update the specific hotkey
                if (index >= 0 && index < hotkeys.Count)
                {
                    hotkeys[index]["key"] = newKey;
                }
                
                // Save updated hotkeys.json using FileAccessQueue
                await Services.FileAccessQueue.ExecuteAsync(hotkeysJsonPath, async () =>
                {
                    var hotkeysData = new
                    {
                        hotkeys = hotkeys,
                        defaultHotkeys = defaultHotkeys
                    };
                    
                    var newJson = JsonSerializer.Serialize(hotkeysData, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(hotkeysJsonPath, newJson);
                    Logger.LogInfo($"Hotkey updated in hotkeys.json: {newKey} - {description}");
                    Logger.LogInfo($"Updated hotkeys.json content written to: {PathManager.GetRelativePath(hotkeysJsonPath)}");
                });
                
                // Also save the change back to the original INI file
                await HotkeyFinderPage.SaveHotkeyToIniAsync(_currentModDirectory, description, newKey);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save hotkey change to hotkeys.json", ex);
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


        private async Task LoadHotkeysFromFile()
        {
            try
            {
                ModHotkeysPanel.Children.Clear();
                _originalHotkeyOrder.Clear();
                
                if (string.IsNullOrEmpty(_currentModDirectory))
                {
                    HotkeysSection.Visibility = Visibility.Collapsed;
                    return;
                }
                
                var hotkeysJsonPath = Path.Combine(_currentModDirectory, "hotkeys.json");
                
                // If hotkeys.json doesn't exist, hide section (hotkey finder will create it)
                if (!File.Exists(hotkeysJsonPath))
                {
                    HotkeysSection.Visibility = Visibility.Collapsed;
                    return;
                }
                
                var json = await File.ReadAllTextAsync(hotkeysJsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                var hotkeyList = new List<(string key, string desc, int originalIndex)>();
                
                // Load current hotkeys
                if (root.TryGetProperty("hotkeys", out var hotkeysProp) && hotkeysProp.ValueKind == JsonValueKind.Array)
                {
                    int index = 0;
                    foreach (var hotkey in hotkeysProp.EnumerateArray())
                    {
                        var key = hotkey.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : null;
                        var desc = hotkey.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
                        
                        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(desc))
                        {
                            hotkeyList.Add((key, desc, index));
                            _originalHotkeyOrder.Add(key);
                        }
                        index++;
                    }
                }
                
                if (hotkeyList.Count > 0)
                {
                    // Sort: favorites first (maintaining original order within each group)
                    hotkeyList = hotkeyList
                        .OrderByDescending(x => _favoriteHotkeys.Contains(x.key))
                        .ThenBy(x => x.originalIndex)
                        .ToList();
                    
                    // Create rows
                    foreach (var (key, desc, origIdx) in hotkeyList)
                    {
                        var hotkeyRow = CreateHotkeyRow(key, desc, origIdx);
                        ModHotkeysPanel.Children.Add(hotkeyRow);
                    }
                    
                    HotkeysSection.Visibility = Visibility.Visible;
                }
                else
                {
                    HotkeysSection.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load hotkeys from file", ex);
                HotkeysSection.Visibility = Visibility.Collapsed;
            }
        }





        private async Task<string?> GetDefaultHotkeyForDescription(string description)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentModDirectory))
                    return null;
                
                var hotkeysJsonPath = Path.Combine(_currentModDirectory, "hotkeys.json");
                if (!File.Exists(hotkeysJsonPath))
                    return null;
                
                var json = await File.ReadAllTextAsync(hotkeysJsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("defaultHotkeys", out var defaultHotkeysArray))
                {
                    foreach (var hotkey in defaultHotkeysArray.EnumerateArray())
                    {
                        var hotkeyDesc = hotkey.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
                        var hotkeyKey = hotkey.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : null;
                        
                        if (!string.IsNullOrEmpty(hotkeyDesc) && !string.IsNullOrEmpty(hotkeyKey) && 
                            hotkeyDesc.Equals(description, StringComparison.OrdinalIgnoreCase))
                        {
                            return hotkeyKey;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to get default hotkey for description", ex);
            }
            
            return null;
        }







        private async void RestoreDefaultHotkeysButton_Click(object sender, RoutedEventArgs e)
        {
            // Block if any hotkey is being edited
            if (_activeHotkeyEditBox != null || _isRecordingGamepad)
                return;
            
            try
            {
                if (string.IsNullOrEmpty(_currentModDirectory))
                {
                    Logger.LogWarning("No current mod directory available");
                    return;
                }
                
                var hotkeysJsonPath = Path.Combine(_currentModDirectory, "hotkeys.json");
                if (!File.Exists(hotkeysJsonPath))
                {
                    Logger.LogWarning("No hotkeys.json file found");
                    return;
                }
                
                var json = await File.ReadAllTextAsync(hotkeysJsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (!root.TryGetProperty("defaultHotkeys", out var defaultHotkeysArray))
                {
                    Logger.LogWarning("No defaultHotkeys section found in hotkeys.json");
                    return;
                }
                
                // Create mapping of description to default key from hotkeys.json
                var defaultMapping = new Dictionary<string, string>();
                foreach (var hotkey in defaultHotkeysArray.EnumerateArray())
                {
                    var desc = hotkey.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
                    var key = hotkey.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : null;
                    
                    if (!string.IsNullOrEmpty(desc) && !string.IsNullOrEmpty(key))
                    {
                        defaultMapping[desc] = key;
                    }
                }
                
                // Update all hotkey rows
                var keyBackground = (Brush)Application.Current.Resources["HotkeyKeyBackground"];
                var updatedHotkeys = new List<Dictionary<string, string>>();
                
                foreach (var child in ModHotkeysPanel.Children)
                {
                    if (child is Border rowBorder && rowBorder.Tag is HotkeyRowData rowData)
                    {
                        if (defaultMapping.TryGetValue(rowData.Description, out var defaultKey))
                        {
                            // Find the grid inside the border
                            if (rowBorder.Child is Grid grid)
                            {
                                // Update keys panel
                                var existingKeysPanel = grid.Children.OfType<StackPanel>().FirstOrDefault();
                                if (existingKeysPanel != null)
                                {
                                    var newKeysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(defaultKey, keyBackground, 64);
                                    int idx = grid.Children.IndexOf(existingKeysPanel);
                                    Grid.SetColumn(newKeysPanel, 0);
                                    grid.Children.RemoveAt(idx);
                                    grid.Children.Insert(idx, newKeysPanel);
                                }
                                
                                // Update row data
                                rowData.Key = defaultKey;
                                
                                // Update button tags
                                var buttonsPanel = grid.Children.OfType<StackPanel>().LastOrDefault();
                                if (buttonsPanel != null)
                                {
                                    foreach (var btn in buttonsPanel.Children.OfType<Border>())
                                    {
                                        if (btn.Tag is HotkeyButtonData btnData)
                                            btnData.KeyCombo = defaultKey;
                                    }
                                }
                            }
                            
                            // Add to updated hotkeys list for saving
                            updatedHotkeys.Add(new Dictionary<string, string>
                            {
                                ["key"] = defaultKey,
                                ["description"] = rowData.Description
                            });
                        }
                        else
                        {
                            // Keep existing hotkey if no default found
                            updatedHotkeys.Add(new Dictionary<string, string>
                            {
                                ["key"] = rowData.Key,
                                ["description"] = rowData.Description
                            });
                        }
                    }
                }
                
                // Save all updated hotkeys to mod.json
                await SaveAllHotkeys(updatedHotkeys);
                
                Logger.LogInfo("Restored all default hotkeys");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to restore all default hotkeys", ex);
            }
        }

        private async Task SaveAllHotkeys(List<Dictionary<string, string>> hotkeys)
        {
            if (string.IsNullOrEmpty(_currentModDirectory)) return;
            
            try
            {
                var hotkeysJsonPath = Path.Combine(_currentModDirectory, "hotkeys.json");
                if (!File.Exists(hotkeysJsonPath)) return;
                
                var json = await File.ReadAllTextAsync(hotkeysJsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                var defaultHotkeys = new List<Dictionary<string, string>>();
                
                // Preserve default hotkeys
                if (root.TryGetProperty("defaultHotkeys", out var defaultProp) && defaultProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in defaultProp.EnumerateArray())
                    {
                        defaultHotkeys.Add(new Dictionary<string, string>
                        {
                            ["key"] = h.GetProperty("key").GetString() ?? "",
                            ["description"] = h.GetProperty("description").GetString() ?? ""
                        });
                    }
                }
                
                // Save updated hotkeys.json using FileAccessQueue
                await Services.FileAccessQueue.ExecuteAsync(hotkeysJsonPath, async () =>
                {
                    var hotkeysData = new
                    {
                        hotkeys = hotkeys,
                        defaultHotkeys = defaultHotkeys
                    };
                    
                    var newJson = JsonSerializer.Serialize(hotkeysData, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(hotkeysJsonPath, newJson);
                    Logger.LogInfo("All hotkeys saved to hotkeys.json");
                });
                
                // Also save all changes back to the original INI files
                foreach (var hotkey in hotkeys)
                {
                    try
                    {
                        await HotkeyFinderPage.SaveHotkeyToIniAsync(_currentModDirectory, hotkey["description"], hotkey["key"]);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to save hotkey to INI: {hotkey["description"]}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save all hotkeys to hotkeys.json", ex);
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