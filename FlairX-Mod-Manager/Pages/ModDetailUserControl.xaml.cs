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
        private string? _categoryParam;
        private string? _viewModeParam;
        private string? _currentModDirectory;
        public event EventHandler? CloseRequested; // Event to notify parent to close
        


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
                ModBrokenLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetail_Broken_Label");
                ModBrokenDesc.Text = SharedUtilities.GetTranslation(lang, "ModDetail_Broken_Desc");
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
                
                // Set tooltips for image buttons
                ToolTipService.SetToolTip(DeletePreviewButton, SharedUtilities.GetTranslation(lang, "ModDetailPage_DeletePreview_Simple_Tooltip"));
                ToolTipService.SetToolTip(CaptureScreenshotButton, SharedUtilities.GetTranslation(lang, "ModDetailPage_CaptureScreenshot_Simple_Tooltip"));
                
                // Set placeholder text for date pickers
                ModDateCheckedPicker.PlaceholderText = SharedUtilities.GetTranslation(lang, "ModDetailPage_SelectDate_Placeholder");
                ModDateUpdatedPicker.PlaceholderText = SharedUtilities.GetTranslation(lang, "ModDetailPage_SelectDate_Placeholder");

                string modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                {
                    modLibraryPath = PathManager.GetModsPath();
                }

                Logger.LogDebug($"ModDetailUserControl: Using mod library path: {modLibraryPath}");
                Logger.LogDebug($"ModDetailUserControl: Current game: {SettingsManager.CurrentSelectedGame}");
                Logger.LogDebug($"ModDetailUserControl: Requested mod directory: {modDirectory}");

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
                        Logger.LogDebug($"ModDetailUserControl: Searching for mod '{modDirectory}' in library: {modLibraryPath}");
                        fullModDir = FindModFolderPath(modLibraryPath, modDirectory);
                        Logger.LogDebug($"ModDetailUserControl: FindModFolderPath returned: {fullModDir ?? "null"}");
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
                        
                        Logger.LogDebug($"ModDetailUserControl: Found mod directory: {fullModDir}");
                        Logger.LogDebug($"ModDetailUserControl: Looking for mod.json at: {_modJsonPath}");
                        
                        // Load mod.json data
                        await LoadModJsonData();
                        
                        // Load preview images
                        LoadPreviewImages(fullModDir);
                    }
                    else
                    {
                        Logger.LogDebug($"Could not find mod directory: {modDirectory} in library: {modLibraryPath}");
                        Logger.LogDebug($"Available categories in library:");
                        try
                        {
                            foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
                            {
                                Logger.LogDebug($"  - {Path.GetFileName(categoryDir)}");
                                var modsInCategory = Directory.GetDirectories(categoryDir);
                                foreach (var modInCategory in modsInCategory)
                                {
                                    Logger.LogDebug($"    - {Path.GetFileName(modInCategory)}");
                                }
                            }
                        }
                        catch (Exception debugEx)
                        {
                            Logger.LogDebug($"Error listing directories: {debugEx.Message}");
                        }
                        
                        // Mod not found - this shouldn't happen anymore since we check before opening
                        Logger.LogDebug($"Mod directory not found: {modDirectory}");
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
                Logger.LogDebug($"Error loading mod details: {ex.Message}");
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
                var json = await Services.FileAccessQueue.ReadAllTextAsync(_modJsonPath);
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
                    ModUrlTextBox.Text = "";
                    ModUrlTextBox.PlaceholderText = "https://";
                    ModUrlTextBox.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray);
                }
                else
                {
                    ModUrlTextBox.Text = url;
                    ModUrlTextBox.ClearValue(TextBox.ForegroundProperty); // Use default theme color
                }
                
                ModVersionTextBox.Text = version;
                
                // Check if mod has GameBanana URL and show/hide Download Previews button
                CheckDownloadPreviewsButtonVisibility(url);
                
                // Check if mod is NSFW and show badge + set checkbox
                bool isNSFW = root.TryGetProperty("isNSFW", out var nsfwProp) && nsfwProp.ValueKind == JsonValueKind.True;
                NSFWBadge.Visibility = isNSFW ? Visibility.Visible : Visibility.Collapsed;
                ModNSFWCheckBox.IsChecked = isNSFW;
                
                // Load StatusKeeper sync setting (default true if not present)
                bool statusKeeperSync = !root.TryGetProperty("statusKeeperSync", out var syncProp) || syncProp.ValueKind != JsonValueKind.False;
                ModStatusKeeperSyncCheckBox.IsChecked = statusKeeperSync;
                
                // Load modBroken setting (default false if not present)
                bool modBroken = root.TryGetProperty("modBroken", out var brokenProp) && brokenProp.ValueKind == JsonValueKind.True;
                ModBrokenCheckBox.IsChecked = modBroken;
                
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
                        OpenFolder(modDir);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error opening mod folder", ex);
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