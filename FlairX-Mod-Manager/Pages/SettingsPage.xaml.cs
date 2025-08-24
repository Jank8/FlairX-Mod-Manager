using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Runtime.InteropServices;
using System.Threading;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class SettingsPage : Page
    {
        private readonly string LanguageFolderPath = PathManager.GetAbsolutePath("Language");
        private Dictionary<string, string> _languages = new(); // displayName, filePath
        private Dictionary<string, string> _fileNameByDisplayName = new();
        private static bool _isOptimizingPreviews = false;
        private CancellationTokenSource? _previewCts;
        private FontIcon? _optimizePreviewsButtonIcon;

        // Set BreadcrumbBar to path segments with icon at the beginning
        private void SetBreadcrumbBar(BreadcrumbBar bar, string path)
        {
            SharedUtilities.SetBreadcrumbBarPath(bar, path);
        }

        // Improved breadcrumb path aggregation
        private string GetBreadcrumbPath(BreadcrumbBar bar)
        {
            return SharedUtilities.GetBreadcrumbBarPath(bar);
        }

        public SettingsPage()
        {
            this.InitializeComponent();
            _optimizePreviewsButtonIcon = OptimizePreviewsButton.Content as FontIcon;
            SettingsManager.Load();
            LoadLanguages();
            InitializeUIState();
        }
        
        private void InitializeUIState()
        {
            // Set ComboBox to selected language from settings
            string? selectedFile = SettingsManager.Current.LanguageFile;
            string displayName = string.Empty;
            
            // Find the display name for the current language file
            if (!string.IsNullOrEmpty(selectedFile) && selectedFile != "auto")
            {
                displayName = _fileNameByDisplayName.FirstOrDefault(x => System.IO.Path.GetFileName(x.Value) == selectedFile).Key ?? string.Empty;
            }
            
            if (!string.IsNullOrEmpty(displayName))
                LanguageComboBox.SelectedItem = displayName;
            else if (LanguageComboBox.Items.Count > 0)
                LanguageComboBox.SelectedIndex = 0;
                
            // Set theme SelectorBar to selected from settings
            string theme = SettingsManager.Current.Theme ?? "Auto";
            foreach (SelectorBarItem item in ThemeSelectorBar.Items)
            {
                if ((string)item.Tag == theme)
                {
                    ThemeSelectorBar.SelectedItem = item;
                    break;
                }
            }
            
            // Set backdrop SelectorBar to selected from settings without triggering event
            string backdrop = SettingsManager.Current.BackdropEffect ?? "AcrylicThin";
            BackdropSelectorBar.SelectionChanged -= BackdropSelectorBar_SelectionChanged;
            foreach (SelectorBarItem item in BackdropSelectorBar.Items)
            {
                if ((string)item.Tag == backdrop)
                {
                    BackdropSelectorBar.SelectedItem = item;
                    break;
                }
            }
            BackdropSelectorBar.SelectionChanged += BackdropSelectorBar_SelectionChanged;
            
            // Set toggle states from settings
            DynamicModSearchToggle.IsOn = SettingsManager.Current.DynamicModSearchEnabled;
            GridLoggingToggle.IsOn = SettingsManager.Current.GridLoggingEnabled;

            ShowOrangeAnimationToggle.IsOn = SettingsManager.Current.ShowOrangeAnimation;
            ModGridZoomToggle.IsOn = SettingsManager.Current.ModGridZoomEnabled;
            ActiveModsToTopToggle.IsOn = SettingsManager.Current.ActiveModsToTopEnabled;
            
            // Set BreadcrumbBar paths
            SetBreadcrumbBar(XXMIRootDirectoryBreadcrumb, SettingsManager.GetCurrentGameXXMIRoot());
            SetBreadcrumbBar(ModLibraryDirectoryBreadcrumb, SettingsManager.GetCurrentModLibraryDirectory());
            
            // Set hotkey values from settings
            OptimizePreviewsHotkeyTextBox.Text = SettingsManager.Current.OptimizePreviewsHotkey;
            ReloadManagerHotkeyTextBox.Text = SettingsManager.Current.ReloadManagerHotkey;
            ShuffleActiveModsHotkeyTextBox.Text = SettingsManager.Current.ShuffleActiveModsHotkey;
            DeactivateAllModsHotkeyTextBox.Text = SettingsManager.Current.DeactivateAllModsHotkey;

            
            // Update all texts and icons once at the end
            UpdateTexts();
            var lang = SharedUtilities.LoadLanguageDictionary();
            AboutButtonText.Text = SharedUtilities.GetTranslation(lang, "AboutButton_Label");
            AboutButtonIcon.Glyph = "\uE946";
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            // Only refresh optimization progress state on navigation
            if (_isOptimizingPreviews)
            {
                if (OptimizePreviewsProgressBar != null)
                    OptimizePreviewsProgressBar.Visibility = Visibility.Visible;
                if (OptimizePreviewsButton != null)
                    OptimizePreviewsButton.IsEnabled = false;
            }
            else
            {
                if (OptimizePreviewsProgressBar != null)
                    OptimizePreviewsProgressBar.Visibility = Visibility.Collapsed;
                if (OptimizePreviewsButton != null)
                    OptimizePreviewsButton.IsEnabled = true;
            }
        }

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
                        
                        LanguageComboBox.Items.Add(displayName);
                        _languages[displayName] = file;
                        _fileNameByDisplayName[displayName] = file;
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

        private void XXMIRootDirectoryDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            string gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
            if (string.IsNullOrEmpty(gameTag))
                return;

            var currentModsPath = SettingsManager.GetCurrentXXMIModsDirectory();
            var defaultXXMIRoot = @".\XXMI";
            var defaultModsPath = AppConstants.GameConfig.GetModsPath(gameTag);
            
            // If already using default, do nothing
            if (string.Equals(Path.GetFullPath(currentModsPath), Path.GetFullPath(defaultModsPath), StringComparison.OrdinalIgnoreCase))
                return;

            // Clean up symlinks from current directory before switching
            if (Directory.Exists(currentModsPath))
            {
                Logger.LogInfo($"Cleaning up symlinks from current directory: {currentModsPath}");
                foreach (var dir in Directory.GetDirectories(currentModsPath))
                {
                    if (FlairX_Mod_Manager.Pages.ModGridPage.IsSymlinkStatic(dir))
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                            Logger.LogInfo($"Removed symlink: {dir}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to remove symlink: {dir}", ex);
                        }
                    }
                }
            }

            // Remove custom XXMI root for current game to use default
            SettingsManager.Current.GameXXMIRootPaths.Remove(gameTag);
            SettingsManager.Save();
            
            SetBreadcrumbBar(XXMIRootDirectoryBreadcrumb, defaultXXMIRoot);
            FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
            
            Logger.LogInfo($"Restored XXMI root to default: {defaultXXMIRoot}");
        }

        private void ModLibraryDirectoryDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            // If already default, do nothing
            var defaultPath = AppConstants.DEFAULT_MOD_LIBRARY_PATH;
            var currentPath = SettingsManager.GetCurrentModLibraryDirectory();
            
            Logger.LogInfo($"Restore default mod library button clicked. Current: '{currentPath}', Default: '{defaultPath}'");
            
            // If already default, do nothing
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                Logger.LogInfo("Current mod library path is null/empty, already using default - no action needed");
                return;
            }
            
            try
            {
                var currentFullPath = Path.GetFullPath(currentPath);
                var defaultFullPath = Path.GetFullPath(defaultPath);
                
                if (string.Equals(currentFullPath, defaultFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInfo($"Mod library is already using default path ({currentFullPath}) - no action needed");
                    return;
                }
                
                Logger.LogInfo($"Mod library path needs to be changed from '{currentFullPath}' to '{defaultFullPath}'");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to compare mod library paths", ex);
                return;
            }

            // Deactivate all mods and remove symlinks
            var activeModsPath = PathManager.GetSettingsPath("ActiveMods.json");
            if (File.Exists(activeModsPath))
            {
                var allMods = new Dictionary<string, bool>();
                var currentMods = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(activeModsPath)) ?? new Dictionary<string, bool>();
                foreach (var key in currentMods.Keys)
                {
                    allMods[key] = false;
                }
                var json = System.Text.Json.JsonSerializer.Serialize(allMods, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(activeModsPath, json);
            }
            FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();

            // Remove custom mod library path for current game to use default
            string gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
            if (!string.IsNullOrEmpty(gameTag))
            {
                SettingsManager.Current.GameModLibraryPaths.Remove(gameTag);
                SettingsManager.Save();
                
                var newDefaultPath = AppConstants.GameConfig.GetModLibraryPath(gameTag);
                SetBreadcrumbBar(ModLibraryDirectoryBreadcrumb, newDefaultPath);
            }

            // Refresh manager
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshUIAfterLanguageChange();
            }
        }

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            SettingsTitle.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Title");
            BackdropLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Backdrop");
            LanguageLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Language");
            DynamicModSearchLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DynamicModSearch_Label");
            GridLoggingLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_GridLogging_Label");
            ShowOrangeAnimationLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ShowOrangeAnimation_Label");
            ModGridZoomLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ModGridZoom_Label");
            ActiveModsToTopLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ActiveModsToTop_Label");
            ToolTipService.SetToolTip(ModGridZoomToggle, SharedUtilities.GetTranslation(lang, "SettingsPage_ModGridZoom_Tooltip"));
            ToolTipService.SetToolTip(GridLoggingToggle, SharedUtilities.GetTranslation(lang, "SettingsPage_GridLogging_Tooltip"));
            ToolTipService.SetToolTip(ActiveModsToTopToggle, SharedUtilities.GetTranslation(lang, "ActiveModsToTop_Tooltip"));
            // Update SelectorBar texts
            ThemeSelectorAutoText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Theme_Auto");
            ThemeSelectorLightText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Theme_Light");
            ThemeSelectorDarkText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Theme_Dark");
            // Update Backdrop SelectorBar texts
            if (BackdropSelectorMicaText != null)
                BackdropSelectorMicaText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Backdrop_Mica");
            if (BackdropSelectorMicaAltText != null)
                BackdropSelectorMicaAltText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Backdrop_MicaAlt");
            if (BackdropSelectorAcrylicText != null)
                BackdropSelectorAcrylicText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Backdrop_Acrylic");
            if (BackdropSelectorAcrylicThinText != null)
                BackdropSelectorAcrylicThinText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_Backdrop_AcrylicThin");
            if (BackdropSelectorNoneText != null)
            {
                BackdropSelectorNoneText.Text = SharedUtilities.GetTranslation(lang, "None");
                System.Diagnostics.Debug.WriteLine($"Set BackdropSelectorNoneText to: {BackdropSelectorNoneText.Text}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("BackdropSelectorNoneText is null!");
            }
            XXMIRootDirectoryLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_XXMIRootDirectory");
            ModLibraryDirectoryLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ModLibraryDirectory");
            ToolTipService.SetToolTip(XXMIRootDirectoryDefaultButton, SharedUtilities.GetTranslation(lang, "SettingsPage_RestoreDefault_Tooltip"));
            ToolTipService.SetToolTip(ModLibraryDirectoryDefaultButton, SharedUtilities.GetTranslation(lang, "SettingsPage_RestoreDefault_Tooltip"));
            ToolTipService.SetToolTip(OptimizePreviewsButton, SharedUtilities.GetTranslation(lang, "SettingsPage_OptimizePreviews_Tooltip"));
            OptimizePreviewsLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_OptimizePreviews_Label");
            ToolTipService.SetToolTip(XXMIRootDirectoryPickButton, SharedUtilities.GetTranslation(lang, "PickFolderDialog_Title"));
            ToolTipService.SetToolTip(ModLibraryDirectoryPickButton, SharedUtilities.GetTranslation(lang, "PickFolderDialog_Title"));
            ToolTipService.SetToolTip(XXMIRootDirectoryBreadcrumb, SharedUtilities.GetTranslation(lang, "OpenDirectory_Tooltip"));
            ToolTipService.SetToolTip(ModLibraryDirectoryBreadcrumb, SharedUtilities.GetTranslation(lang, "OpenDirectory_Tooltip"));
            ToolTipService.SetToolTip(DynamicModSearchToggle, SharedUtilities.GetTranslation(lang, "SettingsPage_DynamicModSearch_Tooltip"));
            // Update About button text
            AboutButtonText.Text = SharedUtilities.GetTranslation(lang, "AboutButton_Label");
            
            // Update hotkey labels using existing translation keys
            OptimizePreviewsHotkeyLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_OptimizePreviews_Label");
            ReloadManagerHotkeyLabel.Text = SharedUtilities.GetTranslation(lang, "Reload_Mods_Tooltip");
            ShuffleActiveModsHotkeyLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ShuffleActiveMods_Label");
            DeactivateAllModsHotkeyLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_DeactivateAllMods_Label");
        }

        private async Task OptimizePreviewsAsync(CancellationToken token)
        {
            await Task.Run(() =>
            {
                var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath) || !Directory.Exists(modLibraryPath)) return;
                foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
                {
                    if (token.IsCancellationRequested)
                        break;
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    // Process category preview (if exists) to create category minitile
                    ProcessCategoryPreview(categoryDir, token);
                    if (token.IsCancellationRequested)
                        break;
                    
                    foreach (var modDir in Directory.GetDirectories(categoryDir))
                    {
                        if (token.IsCancellationRequested)
                            break;
                        
                        ProcessModPreviewImages(modDir);
                    }
                }
            }, token);
        }

        private bool _wasPreviewCancelled = false;

        private async void OptimizePreviewsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isOptimizingPreviews)
            {
                if (_previewCts != null)
                {
                    _wasPreviewCancelled = true;
                    _previewCts.Cancel();
                }
                return;
            }

            // Show confirmation dialog before starting optimization
            var lang = SharedUtilities.LoadLanguageDictionary();
            var confirmDialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(lang, "OptimizePreviews_Confirm_Title"),
                Content = SharedUtilities.GetTranslation(lang, "OptimizePreviews_Confirm_Message"),
                PrimaryButtonText = SharedUtilities.GetTranslation(lang, "Continue"),
                CloseButtonText = SharedUtilities.GetTranslation(lang, "Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return; // User cancelled
            }
            _isOptimizingPreviews = true;
            _wasPreviewCancelled = false;
            _previewCts = new CancellationTokenSource();
            if (_optimizePreviewsButtonIcon != null)
                _optimizePreviewsButtonIcon.Glyph = "\uE711";
            OptimizePreviewsButton.IsEnabled = true;
            OptimizePreviewsProgressBar.Visibility = Visibility.Visible;
            try
            {
                await OptimizePreviewsAsync(_previewCts.Token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _isOptimizingPreviews = false;
                _previewCts = null;
                if (_optimizePreviewsButtonIcon != null)
                    _optimizePreviewsButtonIcon.Glyph = "\uE89E";
                OptimizePreviewsButton.IsEnabled = true;
                OptimizePreviewsProgressBar.Visibility = Visibility.Collapsed;
            }
            if (_wasPreviewCancelled)
            {
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    var dialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "Error_Generic"),
                        Content = SharedUtilities.GetTranslation(lang, "OptimizePreviews_Cancelled"),
                        CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                        XamlRoot = mainWindow.Content.XamlRoot
                    };
                    _ = dialog.ShowAsync();
                }
            }
            else
            {
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    var dialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "Success_Title"),
                        Content = SharedUtilities.GetTranslation(lang, "OptimizePreviews_Completed"),
                        CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                        XamlRoot = mainWindow.Content.XamlRoot
                    };
                    _ = dialog.ShowAsync();
                }
            }
        }

        private string? PickFolderWin32Dialog(nint hwnd)
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            var bi = new BROWSEINFO
            {
                hwndOwner = hwnd,
                lpszTitle = SharedUtilities.GetTranslation(lang, "PickFolderDialog_Title"),
                ulFlags = 0x00000040 // BIF_NEWDIALOGSTYLE
            };
            IntPtr pidl = SHBrowseForFolder(ref bi);
            if (pidl == IntPtr.Zero)
                return null;
            var sb = new System.Text.StringBuilder(MAX_PATH);
            if (SHGetPathFromIDList(pidl, sb))
                return sb.ToString();
            return null;
        }

        private Task<string?> PickFolderWin32DialogSTA(nint hwnd)
        {
            var tcs = new TaskCompletionSource<string?>();
            var thread = new Thread(() =>
            {
                try
                {
                    var result = PickFolderWin32Dialog(hwnd);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }

        private void ProcessCategoryPreview(string categoryDir, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            
            var catprevJpgPath = Path.Combine(categoryDir, "catprev.jpg");
            
            // Skip if catprev.jpg already exists
            if (File.Exists(catprevJpgPath)) return;
            
            // Look for preview files in category directory (catpreview.*, preview.*, etc.)
            var previewFiles = Directory.GetFiles(categoryDir)
                .Where(f => 
                {
                    var fileName = Path.GetFileName(f).ToLower();
                    return (fileName.StartsWith("catpreview") || fileName.StartsWith("preview")) &&
                           (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                })
                .ToArray();
            
            if (previewFiles.Length == 0) return;
            
            var previewPath = previewFiles[0]; // Take first preview file found
            
            try
            {
                using (var img = System.Drawing.Image.FromFile(previewPath))
                {
                    // Create catprev.jpg (600x600 for category tiles)
                    using (var thumbBmp = new System.Drawing.Bitmap(600, 600))
                    using (var g = System.Drawing.Graphics.FromImage(thumbBmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        
                        // Calculate crop rectangle to make it square (center crop)
                        int size = Math.Min(img.Width, img.Height);
                        int x = (img.Width - size) / 2;
                        int y = (img.Height - size) / 2;
                        var srcRect = new System.Drawing.Rectangle(x, y, size, size);
                        var destRect = new System.Drawing.Rectangle(0, 0, 600, 600);
                        
                        g.DrawImage(img, destRect, srcRect, GraphicsUnit.Pixel);
                        
                        // Save as JPEG catprev
                        var jpegEncoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                        if (jpegEncoder != null)
                        {
                            var jpegParams = new EncoderParameters(1);
                            jpegParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
                            thumbBmp.Save(catprevJpgPath, jpegEncoder, jpegParams);
                        }
                    }
                }
                
                // Move the original preview file to recycle bin after processing
                if (!previewPath.Equals(catprevJpgPath, StringComparison.OrdinalIgnoreCase))
                {
                    MoveToRecycleBin(previewPath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process category preview for {Path.GetFileName(categoryDir)}", ex);
            }
        }

        private void ProcessModPreviewImages(string modDir)
        {
            try
            {
                // Find all preview*.png and preview*.jpg files
                var previewFiles = Directory.GetFiles(modDir)
                    .Where(f => 
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("preview") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .OrderBy(f => f) // Sort to ensure consistent ordering
                    .ToList();

                if (previewFiles.Count == 0) return;

                var minitileJpgPath = Path.Combine(modDir, "minitile.jpg");
                bool needsMinitile = !File.Exists(minitileJpgPath);

                // Process each preview file
                for (int i = 0; i < previewFiles.Count && i < 100; i++) // Max 100 images (preview.jpg + preview-01.jpg to preview-99.jpg)
                {
                    var sourceFile = previewFiles[i];
                    string targetFileName = i == 0 ? "preview.jpg" : $"preview-{i:D2}.jpg";
                    var targetPath = Path.Combine(modDir, targetFileName);

                    // Skip if target already exists and is optimized
                    if (File.Exists(targetPath) && IsImageOptimized(targetPath))
                    {
                        // Only create minitile for main preview if missing
                        if (i == 0 && needsMinitile)
                        {
                            CreateMinitile(targetPath, minitileJpgPath);
                        }
                        continue;
                    }

                    // Optimize and save the image
                    OptimizePreviewImage(sourceFile, targetPath);

                    // Create minitile only for the main preview (index 0)
                    if (i == 0)
                    {
                        CreateMinitile(targetPath, minitileJpgPath);
                    }

                    // Move original file to recycle bin if it has a different name
                    if (!sourceFile.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        MoveToRecycleBin(sourceFile);
                    }
                }

                // Clean up any extra preview files beyond the 100 limit
                var existingPreviews = Directory.GetFiles(modDir, "preview*.jpg")
                    .Where(f => 
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        if (name == "preview") return false; // Keep main preview
                        if (name.StartsWith("preview-"))
                        {
                            var suffix = name.Substring(8); // Remove "preview-"
                            return !int.TryParse(suffix, out int num) || num > 99; // Remove if not 01-99
                        }
                        return true; // Remove other preview files
                    })
                    .ToList();

                foreach (var extraFile in existingPreviews)
                {
                    MoveToRecycleBin(extraFile);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process preview images in {modDir}", ex);
            }
        }

        private bool IsImageOptimized(string imagePath)
        {
            try
            {
                using (var img = System.Drawing.Image.FromFile(imagePath))
                {
                    // Consider optimized if it's square and not larger than 1000x1000
                    return img.Width == img.Height && img.Width <= 1000;
                }
            }
            catch
            {
                return false;
            }
        }

        private void OptimizePreviewImage(string sourcePath, string targetPath)
        {
            using (var src = System.Drawing.Image.FromFile(sourcePath))
            {
                // Step 1: Crop to square (1:1 ratio) if needed
                int originalSize = Math.Min(src.Width, src.Height);
                int x = (src.Width - originalSize) / 2;
                int y = (src.Height - originalSize) / 2;
                bool needsCrop = src.Width != src.Height;
                
                System.Drawing.Image squareImage = src;
                if (needsCrop)
                {
                    var cropped = new System.Drawing.Bitmap(originalSize, originalSize);
                    using (var g = System.Drawing.Graphics.FromImage(cropped))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        var srcRect = new System.Drawing.Rectangle(x, y, originalSize, originalSize);
                        var destRect = new System.Drawing.Rectangle(0, 0, originalSize, originalSize);
                        g.DrawImage(src, destRect, srcRect, GraphicsUnit.Pixel);
                    }
                    squareImage = cropped;
                }
                
                // Step 2: Scale down only if larger than 1000x1000 (no upscaling)
                int finalSize = Math.Min(originalSize, 1000);
                
                using (var finalBmp = new System.Drawing.Bitmap(finalSize, finalSize))
                using (var g2 = System.Drawing.Graphics.FromImage(finalBmp))
                {
                    g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g2.CompositingQuality = CompositingQuality.HighQuality;
                    g2.SmoothingMode = SmoothingMode.HighQuality;
                    g2.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    var rect = new System.Drawing.Rectangle(0, 0, finalSize, finalSize);
                    g2.DrawImage(squareImage, rect);
                    
                    var encoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    if (encoder != null)
                    {
                        var encParams = new EncoderParameters(1);
                        encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
                        finalBmp.Save(targetPath, encoder, encParams);
                    }
                }
                
                // Dispose cropped image if we created one
                if (needsCrop && squareImage != src)
                {
                    squareImage.Dispose();
                }
            }
        }

        private void CreateMinitile(string previewPath, string minitilePath)
        {
            try
            {
                using (var previewImg = System.Drawing.Image.FromFile(previewPath))
                using (var thumbBmp = new System.Drawing.Bitmap(600, 600))
                using (var g3 = System.Drawing.Graphics.FromImage(thumbBmp))
                {
                    g3.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g3.CompositingQuality = CompositingQuality.HighQuality;
                    g3.SmoothingMode = SmoothingMode.HighQuality;
                    g3.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    var thumbRect = new System.Drawing.Rectangle(0, 0, 600, 600);
                    g3.DrawImage(previewImg, thumbRect);
                    
                    // Save as JPEG minitile
                    var jpegEncoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    if (jpegEncoder != null)
                    {
                        var jpegParams = new EncoderParameters(1);
                        jpegParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
                        thumbBmp.Save(minitilePath, jpegEncoder, jpegParams);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create minitile from {previewPath}", ex);
            }
        }

        private void MoveToRecycleBin(string path)
        {
            try
            {
                var shf = new SHFILEOPSTRUCT
                {
                    wFunc = FO_DELETE,
                    pFrom = path + '\0', // Must be null-terminated
                    fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION
                };
                
                int result = SHFileOperation(ref shf);
                if (result != 0)
                {
                    Logger.LogWarning($"Failed to move file to recycle bin (error {result}), falling back to permanent deletion: {path}");
                    File.Delete(path); // Fallback to permanent deletion
                }
                else
                {
                    Logger.LogInfo($"Moved file to recycle bin: {path}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to move file to recycle bin, falling back to permanent deletion: {path}. Error: {ex.Message}");
                try
                {
                    File.Delete(path); // Fallback to permanent deletion
                }
                catch (Exception deleteEx)
                {
                    Logger.LogError($"Failed to delete file permanently: {path}. Error: {deleteEx.Message}");
                }
            }
        }

        private bool IsNtfs(string path)
        {
            return SharedUtilities.IsNtfsFileSystem(path);
        }

        private void ShowNtfsWarning(string path, string label)
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            var dialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(lang, "Ntfs_Warning_Title"),
                Content = string.Format(SharedUtilities.GetTranslation(lang, "Ntfs_Warning_Content"), label, path),
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            _ = dialog.ShowAsync();
        }

        /// <summary>
        /// Safety mechanism: Deactivates all mods and removes all symlinks before changing mod library directory
        /// </summary>
        private void SafelyDeactivateAllModsAndCleanupSymlinks(string reason)
        {
            Logger.LogInfo($"Safety mechanism activated: {reason}");
            Logger.LogInfo("Deactivating all mods and cleaning up symlinks for safety");
            
            // Deactivate all mods
            var activeModsPath = PathManager.GetSettingsPath("ActiveMods.json");
            if (File.Exists(activeModsPath))
            {
                var allMods = new Dictionary<string, bool>();
                var currentMods = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(activeModsPath)) ?? new Dictionary<string, bool>();
                foreach (var key in currentMods.Keys)
                {
                    allMods[key] = false;
                }
                var json = System.Text.Json.JsonSerializer.Serialize(allMods, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(activeModsPath, json);
                Logger.LogInfo($"Deactivated {currentMods.Count} mods");
            }
            
            // Explicitly remove all symlinks from XXMI directory
            var xxmiDir = SettingsManager.GetCurrentXXMIModsDirectory();
            if (string.IsNullOrWhiteSpace(xxmiDir))
                xxmiDir = Path.Combine(AppContext.BaseDirectory, "XXMI", "ZZMI", "Mods");
            
            if (Directory.Exists(xxmiDir))
            {
                var removedCount = 0;
                foreach (var dir in Directory.GetDirectories(xxmiDir))
                {
                    if (FlairX_Mod_Manager.Pages.ModGridPage.IsSymlinkStatic(dir))
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                            removedCount++;
                            Logger.LogInfo($"Removed symlink: {dir}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to remove symlink: {dir}", ex);
                        }
                    }
                }
                Logger.LogInfo($"Removed {removedCount} symlinks from XXMI directory");
            }
            
            // Recreate symlinks (should be none since all mods are deactivated)
            FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
            Logger.LogInfo("Safety cleanup completed");
        }

        private async Task XXMIRootDirectoryPickButton_ClickAsync(Button senderButton)
        {
            senderButton.IsEnabled = false;
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                var hwnd = SharedUtilities.GetMainWindowHandle();
                var folderPath = await SharedUtilities.PickFolderAsync(hwnd, SharedUtilities.GetTranslation(lang, "PickFolderDialog_Title"));
                if (!string.IsNullOrEmpty(folderPath))
                {
                    if (!IsNtfs(folderPath))
                        ShowNtfsWarning(folderPath, "XXMI");
                    
                    // Clean up symlinks from current mods directory before switching
                    var currentModsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                    if (Directory.Exists(currentModsPath))
                    {
                        Logger.LogInfo($"Cleaning up symlinks from current XXMI mods directory: {currentModsPath}");
                        foreach (var dir in Directory.GetDirectories(currentModsPath))
                        {
                            if (FlairX_Mod_Manager.Pages.ModGridPage.IsSymlinkStatic(dir))
                            {
                                try
                                {
                                    Directory.Delete(dir, true);
                                    Logger.LogInfo($"Removed symlink: {dir}");
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError($"Failed to remove symlink: {dir}", ex);
                                }
                            }
                        }
                    }
                    
                    // Set XXMI root for current game
                    SettingsManager.SetCurrentGameXXMIRoot(folderPath);
                    SetBreadcrumbBar(XXMIRootDirectoryBreadcrumb, folderPath);
                    FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
                    
                    Logger.LogInfo($"Changed XXMI root directory to: {folderPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to change XXMI root directory", ex);
            }
            senderButton.IsEnabled = true;
        }

        private async void XXMIRootDirectoryPickButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button senderButton)
                await XXMIRootDirectoryPickButton_ClickAsync(senderButton);
        }

        private async Task ModLibraryDirectoryPickButton_ClickAsync(Button senderButton)
        {
            senderButton.IsEnabled = false;
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                var hwnd = SharedUtilities.GetMainWindowHandle();
                var folderPath = await SharedUtilities.PickFolderAsync(hwnd, SharedUtilities.GetTranslation(lang, "PickFolderDialog_Title"));
                if (!string.IsNullOrEmpty(folderPath))
                {
                    if (!IsNtfs(folderPath))
                        ShowNtfsWarning(folderPath, "ModLibrary");

                    // Deactivate all mods and remove symlinks
                    var activeModsPath = PathManager.GetSettingsPath("ActiveMods.json");
                    if (File.Exists(activeModsPath))
                    {
                        var allMods = new Dictionary<string, bool>();
                        var currentMods = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(activeModsPath)) ?? new Dictionary<string, bool>();
                        foreach (var key in currentMods.Keys)
                        {
                            allMods[key] = false;
                        }
                        var json = System.Text.Json.JsonSerializer.Serialize(allMods, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(activeModsPath, json);
                    }
                    FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();

                    SettingsManager.SetCurrentGameModLibrary(folderPath);
                    SetBreadcrumbBar(ModLibraryDirectoryBreadcrumb, folderPath);

                    // Create default mod.json in subdirectories
                    (App.Current as FlairX_Mod_Manager.App)?.EnsureModJsonInModLibrary();

                    // Refresh manager
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.RefreshUIAfterLanguageChange();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to change ModLibrary directory", ex);
            }
            senderButton.IsEnabled = true;
        }

        private async void ModLibraryDirectoryPickButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button senderButton)
                await ModLibraryDirectoryPickButton_ClickAsync(senderButton);
        }

        // Win32 API Folder Picker using SHBrowseForFolder
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO bi);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool SHGetPathFromIDList(IntPtr pidl, System.Text.StringBuilder pszPath);

        private const int MAX_PATH = 260;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct BROWSEINFO
        {
            public nint hwndOwner;
            public nint pidlRoot;
            public IntPtr pszDisplayName;
            public string lpszTitle;
            public uint ulFlags;
            public nint lpfn;
            public nint lParam;
            public int iImage;
        }

        // Win32 API for moving files to Recycle Bin
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        private const uint FO_DELETE = 0x0003;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOCONFIRMATION = 0x0010;

        private async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            var mainWindow = (App.Current as App)?.MainWindow;
            var xamlRoot = mainWindow is not null ? (mainWindow.Content as FrameworkElement)?.XamlRoot : this.XamlRoot;
            var dialog = new ContentDialog
            {
                CloseButtonText = "OK",
                XamlRoot = xamlRoot,
            };
            var stackPanel = new StackPanel();
            var titleBlock = new TextBlock
            {
                Text = "FlairX Mod Manager",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 22,
                Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,12)
            };
            stackPanel.Children.Add(titleBlock);
            stackPanel.Children.Add(new TextBlock { Text = SharedUtilities.GetTranslation(lang, "AboutDialog_Author"), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,4) });
            stackPanel.Children.Add(new HyperlinkButton { Content = "Jank8", NavigateUri = new Uri("https://github.com/Jank8"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,8) });
            stackPanel.Children.Add(new TextBlock { Text = SharedUtilities.GetTranslation(lang, "AboutDialog_AI"), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,4) });
            
            // Create AI section with Kiro and GitHub Copilot
            var aiPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,8) };
            aiPanel.Children.Add(new HyperlinkButton { Content = "Kiro", NavigateUri = new Uri("https://kiro.dev/"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            aiPanel.Children.Add(new TextBlock { Text = " " + SharedUtilities.GetTranslation(lang, "AboutDialog_With") + " ", VerticalAlignment = VerticalAlignment.Center, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            aiPanel.Children.Add(new HyperlinkButton { Content = "GitHub Copilot", NavigateUri = new Uri("https://github.com/features/copilot"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            stackPanel.Children.Add(aiPanel);
            stackPanel.Children.Add(new TextBlock { Text = SharedUtilities.GetTranslation(lang, "AboutDialog_Fonts"), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,4) });
            stackPanel.Children.Add(new HyperlinkButton { Content = "Noto Fonts", NavigateUri = new Uri("https://notofonts.github.io/"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,8) });
            stackPanel.Children.Add(new TextBlock { Text = SharedUtilities.GetTranslation(lang, "AboutDialog_Thanks"), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,4) });
            var thanksPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            thanksPanel.Children.Add(new StackPanel {
                Orientation = Orientation.Vertical,
                Children = {
                    new HyperlinkButton { Content = "XLXZ", NavigateUri = new Uri("https://github.com/XiaoLinXiaoZhu"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0), HorizontalAlignment = HorizontalAlignment.Left },
                }
            });
            thanksPanel.Children.Add(new TextBlock { Text = SharedUtilities.GetTranslation(lang, "AboutDialog_For"), VerticalAlignment = VerticalAlignment.Center, Margin = new Microsoft.UI.Xaml.Thickness(8,0,8,0) });
            thanksPanel.Children.Add(new StackPanel {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Children = {
                    new HyperlinkButton { Content = "Source code", NavigateUri = new Uri("https://github.com/XiaoLinXiaoZhu/XX-Mod-Manager/blob/main/plugins/recognizeModInfoPlugin.js"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0), HorizontalAlignment = HorizontalAlignment.Left },
                }
            });
            stackPanel.Children.Add(thanksPanel);
            var gplPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Microsoft.UI.Xaml.Thickness(0,16,0,0) };
            gplPanel.Children.Add(new HyperlinkButton { Content = SharedUtilities.GetTranslation(lang, "AboutDialog_License"), NavigateUri = new Uri("https://www.gnu.org/licenses/gpl-3.0.html#license-text") });
            stackPanel.Children.Add(gplPanel);
            dialog.Content = stackPanel;
            await dialog.ShowAsync();
        }

        // Add missing event handler methods for XAML
        private void ThemeSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            if (sender.SelectedItem is SelectorBarItem item && item.Tag is string theme)
            {
                SettingsManager.Current.Theme = theme;
                SettingsManager.Save();
                
                // Set application theme
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
                    }
                    
                    // Refresh backdrop effect if using "None" to update background color
                    string currentBackdrop = SettingsManager.Current.BackdropEffect ?? "AcrylicThin";
                    if (currentBackdrop == "None")
                    {
                        mainWindow.ApplyBackdropEffect("None");
                    }
                }
            }
        }
        private void BackdropSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            if (sender.SelectedItem is SelectorBarItem item && item.Tag is string backdrop)
            {
                SettingsManager.Current.BackdropEffect = backdrop;
                SettingsManager.Save();
                
                // Apply backdrop effect
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.ApplyBackdropEffect(backdrop);
                }
            }
        }

        private void DynamicModSearchToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.DynamicModSearchEnabled = DynamicModSearchToggle.IsOn;
            SettingsManager.Save();
        }

        private void GridLoggingToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.GridLoggingEnabled = GridLoggingToggle.IsOn;
            SettingsManager.Save();
            // No additional UI updates needed for grid logging
        }

        private void ShowOrangeAnimationToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.ShowOrangeAnimation = ShowOrangeAnimationToggle.IsOn;
            SettingsManager.Save();
            // Refresh animation in MainWindow
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                mainWindow.UpdateOrangeAnimationVisibility(ShowOrangeAnimationToggle.IsOn);
            }
        }

        private void ModGridZoomToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.ModGridZoomEnabled = ModGridZoomToggle.IsOn;
            SettingsManager.Save();
        }

        private void ActiveModsToTopToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.ActiveModsToTopEnabled = ActiveModsToTopToggle.IsOn;
            SettingsManager.Save();
        }



        // Hotkey handling methods
        private void OptimizePreviewsHotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                SettingsManager.Current.OptimizePreviewsHotkey = textBox.Text;
                SettingsManager.Save();
            }
        }

        private void ReloadManagerHotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                SettingsManager.Current.ReloadManagerHotkey = textBox.Text;
                SettingsManager.Save();
            }
        }

        private void ShuffleActiveModsHotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                SettingsManager.Current.ShuffleActiveModsHotkey = textBox.Text;
                SettingsManager.Save();
            }
        }

        private void DeactivateAllModsHotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                SettingsManager.Current.DeactivateAllModsHotkey = textBox.Text;
                SettingsManager.Save();
            }
        }



        private void HotkeyTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                e.Handled = true;
                
                var key = e.Key;
                var modifiers = new List<string>();
                
                // Check for modifier keys
                if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    modifiers.Add("Ctrl");
                if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    modifiers.Add("Shift");
                if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    modifiers.Add("Alt");
                
                // Skip modifier-only keys
                if (key == Windows.System.VirtualKey.Control || key == Windows.System.VirtualKey.Shift || 
                    key == Windows.System.VirtualKey.Menu || key == Windows.System.VirtualKey.LeftWindows || 
                    key == Windows.System.VirtualKey.RightWindows)
                    return;
                
                // Build hotkey string
                var hotkeyParts = new List<string>(modifiers);
                hotkeyParts.Add(key.ToString());
                
                textBox.Text = string.Join("+", hotkeyParts);
            }
        }

        private void XXMIRootDirectoryBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            try
            {
                // Only handle clicks on the home icon (first item)
                if (args.Index == 0)
                {
                    var xxmiRootPath = SettingsManager.GetCurrentGameXXMIRoot();
                    if (!string.IsNullOrEmpty(xxmiRootPath) && Directory.Exists(xxmiRootPath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = xxmiRootPath,
                            UseShellExecute = true
                        });
                        Logger.LogInfo($"Opened XXMI root directory: {xxmiRootPath}");
                    }
                    else
                    {
                        Logger.LogWarning($"XXMI root directory does not exist: {xxmiRootPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to open XXMI root directory", ex);
            }
        }

        private void ModLibraryDirectoryBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            try
            {
                // Only handle clicks on the home icon (first item)
                if (args.Index == 0)
                {
                    var modLibraryPath = SettingsManager.GetCurrentModLibraryDirectory();
                    if (!string.IsNullOrEmpty(modLibraryPath) && Directory.Exists(modLibraryPath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = modLibraryPath,
                            UseShellExecute = true
                        });
                        Logger.LogInfo($"Opened mod library directory: {modLibraryPath}");
                    }
                    else
                    {
                        Logger.LogWarning($"Mod library directory does not exist: {modLibraryPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to open mod library directory", ex);
            }
        }
    }
}