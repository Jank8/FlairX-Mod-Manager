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

namespace ZZZ_Mod_Manager_X.Pages
{
    public sealed partial class SettingsPage : Page
    {
        private readonly string LanguageFolderPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Language");
        private Dictionary<string, string> _languages = new(); // displayName, filePath
        private Dictionary<string, string> _fileNameByDisplayName = new();
        private static bool _isOptimizingPreviews = false;
        private CancellationTokenSource? _previewCts;
        private FontIcon? _optimizePreviewsButtonIcon;

        // Set BreadcrumbBar to path segments with icon at the beginning
        private void SetBreadcrumbBar(BreadcrumbBar bar, string path)
        {
            var items = new List<object>();
            // Default path: only dot or empty string
            if (path == "." || string.IsNullOrWhiteSpace(path))
            {
                items.Add(new FontIcon { Glyph = "\uE80F" });
            }
            else
            {
                items.Add(new FontIcon { Glyph = "\uE80F" });
                var segments = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var seg in segments)
                    items.Add(seg);
            }
            bar.ItemsSource = items;
        }

        // Improved breadcrumb path aggregation
        private string GetBreadcrumbPath(BreadcrumbBar bar)
        {
            var items = bar.ItemsSource as IEnumerable<object>;
            if (items == null) return string.Empty;
            var segments = items.Skip(1).OfType<string>(); // skip icon
            return string.Join(Path.DirectorySeparatorChar.ToString(), segments);
        }

        public SettingsPage()
        {
            this.InitializeComponent();
            _optimizePreviewsButtonIcon = OptimizePreviewsButton.Content as FontIcon;
            SettingsManager.Load();
            LoadLanguages();
            UpdateTexts();
            AboutButtonText.Text = LanguageManager.Instance.T("AboutButton_Label");
            AboutButtonIcon.Glyph = "\uE946";
            // Set ComboBox to selected language from settings or default (English)
            string? selectedFile = SettingsManager.Current.LanguageFile ?? "auto";
            string displayName = string.Empty;
            if (selectedFile == "auto")
            {
                displayName = LanguageManager.Instance.T("Auto_Language");
            }
            else
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
            // Set BreadcrumbBar in constructor
            SetBreadcrumbBar(XXMIModsDirectoryBreadcrumb, SettingsManager.XXMIModsDirectorySafe);
            SetBreadcrumbBar(ModLibraryDirectoryBreadcrumb, SettingsManager.Current.ModLibraryDirectory ?? string.Empty);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Refresh the state of the bar and button if optimization is in progress
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
            // Restore toggle states from settings
            DynamicModSearchToggle.IsOn = SettingsManager.Current.DynamicModSearchEnabled;
            GridLoggingToggle.IsOn = SettingsManager.Current.GridLoggingEnabled;
            ShowOrangeAnimationToggle.IsOn = SettingsManager.Current.ShowOrangeAnimation;
        }

        private void LoadLanguages()
        {
            LanguageComboBox.Items.Clear();
            _languages.Clear();
            _fileNameByDisplayName.Clear();
            // Add AUTO option at the beginning of the list
            string autoDisplayName = LanguageManager.Instance.T("Auto_Language");
            LanguageComboBox.Items.Add(autoDisplayName);
            _languages[autoDisplayName] = "auto";
            _fileNameByDisplayName[autoDisplayName] = "auto";
            if (Directory.Exists(LanguageFolderPath))
            {
                var files = Directory.GetFiles(LanguageFolderPath, "*.json");
                foreach (var file in files)
                {
                    string displayName = System.IO.Path.GetFileNameWithoutExtension(file);
                    try
                    {
                        var json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        if (dict != null && dict.TryGetValue("Language_DisplayName", out var langName) && !string.IsNullOrWhiteSpace(langName))
                        {
                            displayName = langName;
                        }
                    }
                    catch { }
                    LanguageComboBox.Items.Add(displayName);
                    _languages[displayName] = file;
                    _fileNameByDisplayName[displayName] = file;
                }
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is string displayName && _fileNameByDisplayName.TryGetValue(displayName, out var filePath))
            {
                if (filePath == "auto")
                {
                    SettingsManager.Current.LanguageFile = "auto";
                    SettingsManager.Save();
                    // Force app restart so language detection works
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.RestartAppButton_Click(null, null);
                    }
                    return;
                }
                var fileName = Path.GetFileName(filePath);
                LanguageManager.Instance.LoadLanguage(fileName);
                SettingsManager.Current.LanguageFile = fileName;
                SettingsManager.Save();
                UpdateTexts();
                // Refresh the entire UI in MainWindow
                if (App.Current is App app2 && app2.MainWindow is MainWindow mainWindow2)
                {
                    mainWindow2.RefreshUIAfterLanguageChange();
                    var frame = mainWindow2.GetContentFrame();
                    if (frame != null)
                        frame.Navigate(typeof(SettingsPage), null);
                }
            }
        }

        private void XXMIModsDirectoryDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            var defaultPath = AppConstants.DEFAULT_XXMI_MODS_PATH;
            var currentPath = SettingsManager.Current.XXMIModsDirectory;
            
            // If already default, do nothing
            if (string.IsNullOrWhiteSpace(currentPath) || 
                string.Equals(Path.GetFullPath(currentPath), Path.GetFullPath(defaultPath), StringComparison.OrdinalIgnoreCase))
                return;

            // Clean up symlinks from current (non-default) directory before switching
            var currentFullPath = Path.GetFullPath(currentPath);
            if (Directory.Exists(currentFullPath))
            {
                Logger.LogInfo($"Cleaning up symlinks from current directory: {currentFullPath}");
                foreach (var dir in Directory.GetDirectories(currentFullPath))
                {
                    if (ZZZ_Mod_Manager_X.Pages.ModGridPage.IsSymlinkStatic(dir))
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

            // Restore to default and recreate symlinks in default location
            SettingsManager.RestoreDefaults();
            SetBreadcrumbBar(XXMIModsDirectoryBreadcrumb, SettingsManager.XXMIModsDirectorySafe);
            ZZZ_Mod_Manager_X.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
            
            Logger.LogInfo($"Restored XXMI directory to default: {defaultPath}");
        }

        private void ModLibraryDirectoryDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            // If already default, do nothing
            var defaultPath = AppConstants.DEFAULT_MOD_LIBRARY_PATH;
            var currentPath = SettingsManager.Current.ModLibraryDirectory;
            
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
            var activeModsPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Settings", "ActiveMods.json");
            if (System.IO.File.Exists(activeModsPath))
            {
                var allMods = new Dictionary<string, bool>();
                var currentMods = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(System.IO.File.ReadAllText(activeModsPath)) ?? new Dictionary<string, bool>();
                foreach (var key in currentMods.Keys)
                {
                    allMods[key] = false;
                }
                var json = System.Text.Json.JsonSerializer.Serialize(allMods, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(activeModsPath, json);
            }
            ZZZ_Mod_Manager_X.Pages.ModGridPage.RecreateSymlinksFromActiveMods();

            SettingsManager.RestoreDefaults();
            SetBreadcrumbBar(ModLibraryDirectoryBreadcrumb, SettingsManager.Current.ModLibraryDirectory ?? string.Empty);

            // Refresh manager
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshUIAfterLanguageChange();
            }
        }

        private void UpdateTexts()
        {
            SettingsTitle.Text = LanguageManager.Instance.T("SettingsPage_Title");
            LanguageLabel.Text = LanguageManager.Instance.T("SettingsPage_Language");
            DynamicModSearchLabel.Text = LanguageManager.Instance.T("SettingsPage_DynamicModSearch_Label");
            GridLoggingLabel.Text = LanguageManager.Instance.T("SettingsPage_GridLogging_Label");
            ShowOrangeAnimationLabel.Text = LanguageManager.Instance.T("SettingsPage_ShowOrangeAnimation_Label");
            // Update SelectorBar texts
            ThemeSelectorAutoText.Text = LanguageManager.Instance.T("SettingsPage_Theme_Auto");
            ThemeSelectorLightText.Text = LanguageManager.Instance.T("SettingsPage_Theme_Light");
            ThemeSelectorDarkText.Text = LanguageManager.Instance.T("SettingsPage_Theme_Dark");
            XXMIModsDirectoryLabel.Text = LanguageManager.Instance.T("SettingsPage_XXMIModsDirectory");
            ModLibraryDirectoryLabel.Text = LanguageManager.Instance.T("SettingsPage_ModLibraryDirectory");
            ToolTipService.SetToolTip(XXMIModsDirectoryDefaultButton, LanguageManager.Instance.T("SettingsPage_RestoreDefault_Tooltip"));
            ToolTipService.SetToolTip(ModLibraryDirectoryDefaultButton, LanguageManager.Instance.T("SettingsPage_RestoreDefault_Tooltip"));
            ToolTipService.SetToolTip(OptimizePreviewsButton, LanguageManager.Instance.T("SettingsPage_OptimizePreviews_Tooltip"));
            OptimizePreviewsLabel.Text = LanguageManager.Instance.T("SettingsPage_OptimizePreviews_Label");
            ToolTipService.SetToolTip(XXMIModsDirectoryPickButton, LanguageManager.Instance.T("PickFolderDialog_Title"));
            ToolTipService.SetToolTip(ModLibraryDirectoryPickButton, LanguageManager.Instance.T("PickFolderDialog_Title"));
            ToolTipService.SetToolTip(DynamicModSearchToggle, LanguageManager.Instance.T("SettingsPage_DynamicModSearch_Tooltip"));
            // Removed XXMIModsDirectoryDefaultButton.ToolTip and ModLibraryDirectoryDefaultButton.ToolTip, as WinUI 3 doesn't have this property
        }

        private async Task OptimizePreviewsAsync(CancellationToken token)
        {
            await Task.Run(() =>
            {
                var modLibraryPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "ModLibrary");
                if (!Directory.Exists(modLibraryPath)) return;
                foreach (var dir in Directory.GetDirectories(modLibraryPath))
                {
                    if (token.IsCancellationRequested)
                        break;
                    var jpgPath = Path.Combine(dir, "preview.jpg");
                    // Check if we need to create JPEG minitile
                    var minitileJpgPath = Path.Combine(dir, "minitile.jpg");
                    bool needsMinitile = !File.Exists(minitileJpgPath);
                    
                    // If preview.jpg exists with size 1000x1000, check if we need minitile
                    if (File.Exists(jpgPath))
                    {
                        try
                        {
                            using (var img = System.Drawing.Image.FromFile(jpgPath))
                            {
                                // Only skip if image is already square (1:1 ratio) and not larger than 1000x1000
                                if (img.Width == img.Height && img.Width <= 1000)
                                {
                                    // preview.jpg is already optimized (1000x1000 square), but create minitile if missing
                                    if (!needsMinitile)
                                        continue; // Both files exist and are correct
                                    
                                    // Create minitile from existing preview.jpg (600x600 for high DPI displays)
                                    using (var thumbBmp = new System.Drawing.Bitmap(600, 600))
                                    using (var g3 = System.Drawing.Graphics.FromImage(thumbBmp))
                                    {
                                        g3.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                        g3.CompositingQuality = CompositingQuality.HighQuality;
                                        g3.SmoothingMode = SmoothingMode.HighQuality;
                                        g3.PixelOffsetMode = PixelOffsetMode.HighQuality;
                                        var thumbRect = new System.Drawing.Rectangle(0, 0, 600, 600);
                                        g3.DrawImage(img, thumbRect);
                                        
                                        // Save as JPEG minitile
                                        var jpegEncoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                                        if (jpegEncoder != null)
                                        {
                                            var jpegParams = new EncoderParameters(1);
                                            jpegParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
                                            thumbBmp.Save(minitileJpgPath, jpegEncoder, jpegParams);
                                        }
                                    }
                                    continue; // Done processing this directory
                                }
                            }
                        }
                        catch { }
                    }
                    // Search for preview.*.png/jpg regardless of case
                    var files = Directory.GetFiles(dir)
                        .Where(f => Path.GetFileName(f).StartsWith("preview", StringComparison.OrdinalIgnoreCase) &&
                                    (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    var jpgFile = files.FirstOrDefault(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase));
                    var pngFile = files.FirstOrDefault(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
                    string? sourcePath = jpgFile ?? pngFile;
                    if (string.IsNullOrEmpty(sourcePath)) continue;
                    try
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
                                    finalBmp.Save(jpgPath, encoder, encParams);
                                }
                            }
                            
                            // Dispose cropped image if we created one
                            if (needsCrop && squareImage != src)
                            {
                                squareImage.Dispose();
                            }
                            
                            // Now create 600x600 JPEG minitile from the newly created preview.jpg
                            using (var previewImg = System.Drawing.Image.FromFile(jpgPath))
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
                                    thumbBmp.Save(minitileJpgPath, jpegEncoder, jpegParams);
                                }
                            }
                            // Dispose is handled in the using block above
                        }
                        // Remove all preview.* (PNG/JPG/JPEG) files with other names
                        foreach (var f in files)
                        {
                            if (!string.Equals(f, jpgPath, StringComparison.OrdinalIgnoreCase))
                            {
                                try { File.Delete(f); } catch { }
                            }
                        }
                    }
                    catch { }
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
            var confirmDialog = new ContentDialog
            {
                Title = LanguageManager.Instance.T("OptimizePreviews_Confirm_Title"),
                Content = LanguageManager.Instance.T("OptimizePreviews_Confirm_Message"),
                PrimaryButtonText = LanguageManager.Instance.T("Continue"),
                CloseButtonText = LanguageManager.Instance.T("Cancel"),
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
                        Title = LanguageManager.Instance.T("Error_Generic"),
                        Content = LanguageManager.Instance.T("OptimizePreviews_Cancelled"),
                        CloseButtonText = LanguageManager.Instance.T("OK"),
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
                        Title = LanguageManager.Instance.T("Success_Title"),
                        Content = LanguageManager.Instance.T("OptimizePreviews_Completed"),
                        CloseButtonText = LanguageManager.Instance.T("OK"),
                        XamlRoot = mainWindow.Content.XamlRoot
                    };
                    _ = dialog.ShowAsync();
                }
            }
        }

        private string? PickFolderWin32Dialog(nint hwnd)
        {
            var bi = new BROWSEINFO
            {
                hwndOwner = hwnd,
                lpszTitle = LanguageManager.Instance.T("PickFolderDialog_Title"),
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

        private bool IsNtfs(string path)
        {
            try
            {
                var fullPath = System.IO.Path.GetFullPath(path);
                var root = System.IO.Path.GetPathRoot(fullPath);
                if (string.IsNullOrEmpty(root)) return false;
                var drive = new DriveInfo(root);
                return string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private void ShowNtfsWarning(string path, string label)
        {
            var dialog = new ContentDialog
            {
                Title = LanguageManager.Instance.T("Ntfs_Warning_Title"),
                Content = string.Format(LanguageManager.Instance.T("Ntfs_Warning_Content"), label, path),
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
            var activeModsPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Settings", "ActiveMods.json");
            if (System.IO.File.Exists(activeModsPath))
            {
                var allMods = new Dictionary<string, bool>();
                var currentMods = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(System.IO.File.ReadAllText(activeModsPath)) ?? new Dictionary<string, bool>();
                foreach (var key in currentMods.Keys)
                {
                    allMods[key] = false;
                }
                var json = System.Text.Json.JsonSerializer.Serialize(allMods, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(activeModsPath, json);
                Logger.LogInfo($"Deactivated {currentMods.Count} mods");
            }
            
            // Explicitly remove all symlinks from XXMI directory
            var xxmiDir = SettingsManager.Current.XXMIModsDirectory;
            if (string.IsNullOrWhiteSpace(xxmiDir))
                xxmiDir = Path.Combine(AppContext.BaseDirectory, "XXMI", "ZZMI", "Mods");
            
            if (Directory.Exists(xxmiDir))
            {
                var removedCount = 0;
                foreach (var dir in Directory.GetDirectories(xxmiDir))
                {
                    if (ZZZ_Mod_Manager_X.Pages.ModGridPage.IsSymlinkStatic(dir))
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
            ZZZ_Mod_Manager_X.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
            Logger.LogInfo("Safety cleanup completed");
        }

        private async Task XXMIModsDirectoryPickButton_ClickAsync(Button senderButton)
        {
            senderButton.IsEnabled = false;
            try
            {
                var appWindow = (App.Current as ZZZ_Mod_Manager_X.App)?.MainWindow;
                if (appWindow == null)
                {
                    senderButton.IsEnabled = true;
                    return;
                }
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(appWindow);
                var folderPath = await PickFolderWin32DialogSTA(hwnd);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    if (!IsNtfs(folderPath))
                        ShowNtfsWarning(folderPath, "XXMI");
                    // Clean up symlinks from current directory before switching to new one
                    var currentPath = SettingsManager.Current.XXMIModsDirectory;
                    if (!string.IsNullOrWhiteSpace(currentPath))
                    {
                        var currentFullPath = Path.GetFullPath(currentPath);
                        var newFullPath = Path.GetFullPath(folderPath);
                        
                        // Only clean up if we're actually changing to a different directory
                        if (!string.Equals(currentFullPath, newFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            if (Directory.Exists(currentFullPath))
                            {
                                Logger.LogInfo($"Cleaning up symlinks from current XXMI directory: {currentFullPath}");
                                foreach (var dir in Directory.GetDirectories(currentFullPath))
                                {
                                    if (ZZZ_Mod_Manager_X.Pages.ModGridPage.IsSymlinkStatic(dir))
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
                        }
                    }
                    
                    // Update settings and recreate symlinks in new location
                    SettingsManager.Current.XXMIModsDirectory = folderPath;
                    SettingsManager.Save();
                    SetBreadcrumbBar(XXMIModsDirectoryBreadcrumb, folderPath);
                    ZZZ_Mod_Manager_X.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
                    
                    Logger.LogInfo($"Changed XXMI directory to: {folderPath}");
                }
            }
            catch { }
            senderButton.IsEnabled = true;
        }

        private async void XXMIModsDirectoryPickButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button senderButton)
                await XXMIModsDirectoryPickButton_ClickAsync(senderButton);
        }

        private async Task ModLibraryDirectoryPickButton_ClickAsync(Button senderButton)
        {
            senderButton.IsEnabled = false;
            try
            {
                var appWindow = (App.Current as ZZZ_Mod_Manager_X.App)?.MainWindow;
                if (appWindow == null)
                {
                    senderButton.IsEnabled = true;
                    return;
                }
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(appWindow);
                var folderPath = await PickFolderWin32DialogSTA(hwnd);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    if (!IsNtfs(folderPath))
                        ShowNtfsWarning(folderPath, "ModLibrary");

                    // Deactivate all mods and remove symlinks
                    var activeModsPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Settings", "ActiveMods.json");
                    if (System.IO.File.Exists(activeModsPath))
                    {
                        var allMods = new Dictionary<string, bool>();
                        var currentMods = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(System.IO.File.ReadAllText(activeModsPath)) ?? new Dictionary<string, bool>();
                        foreach (var key in currentMods.Keys)
                        {
                            allMods[key] = false;
                        }
                        var json = System.Text.Json.JsonSerializer.Serialize(allMods, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        System.IO.File.WriteAllText(activeModsPath, json);
                    }
                    ZZZ_Mod_Manager_X.Pages.ModGridPage.RecreateSymlinksFromActiveMods();

                    SettingsManager.Current.ModLibraryDirectory = folderPath;
                    SettingsManager.Save();
                    SetBreadcrumbBar(ModLibraryDirectoryBreadcrumb, folderPath);

                    // Create default mod.json in subdirectories
                    (App.Current as ZZZ_Mod_Manager_X.App)?.EnsureModJsonInModLibrary();

                    // Refresh manager
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.RefreshUIAfterLanguageChange();
                    }
                }
            }
            catch { }
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

        private async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
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
                Text = "ZZZ Mod Manager X",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 22,
                Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,12)
            };
            stackPanel.Children.Add(titleBlock);
            stackPanel.Children.Add(new TextBlock { Text = LanguageManager.Instance.T("AboutDialog_Author"), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,4) });
            stackPanel.Children.Add(new HyperlinkButton { Content = "Jank8", NavigateUri = new Uri("https://github.com/Jank8"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,8) });
            stackPanel.Children.Add(new TextBlock { Text = LanguageManager.Instance.T("AboutDialog_AI"), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,4) });
            
            // Create AI section with Kiro and GitHub Copilot
            var aiPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,8) };
            aiPanel.Children.Add(new HyperlinkButton { Content = "Kiro", NavigateUri = new Uri("https://kiro.dev/"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            aiPanel.Children.Add(new TextBlock { Text = " " + LanguageManager.Instance.T("AboutDialog_With") + " ", VerticalAlignment = VerticalAlignment.Center, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            aiPanel.Children.Add(new HyperlinkButton { Content = "GitHub Copilot", NavigateUri = new Uri("https://github.com/features/copilot"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0) });
            stackPanel.Children.Add(aiPanel);
            stackPanel.Children.Add(new TextBlock { Text = LanguageManager.Instance.T("AboutDialog_Fonts"), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,4) });
            stackPanel.Children.Add(new HyperlinkButton { Content = "Noto Fonts", NavigateUri = new Uri("https://notofonts.github.io/"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,8) });
            stackPanel.Children.Add(new TextBlock { Text = LanguageManager.Instance.T("AboutDialog_Thanks"), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,4) });
            var thanksPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            thanksPanel.Children.Add(new StackPanel {
                Orientation = Orientation.Vertical,
                Children = {
                    new HyperlinkButton { Content = "XLXZ", NavigateUri = new Uri("https://github.com/XiaoLinXiaoZhu"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0), HorizontalAlignment = HorizontalAlignment.Left },
                }
            });
            thanksPanel.Children.Add(new TextBlock { Text = LanguageManager.Instance.T("AboutDialog_For"), VerticalAlignment = VerticalAlignment.Center, Margin = new Microsoft.UI.Xaml.Thickness(8,0,8,0) });
            thanksPanel.Children.Add(new StackPanel {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Children = {
                    new HyperlinkButton { Content = "Source code", NavigateUri = new Uri("https://github.com/XiaoLinXiaoZhu/XX-Mod-Manager/blob/main/plugins/recognizeModInfoPlugin.js"), Margin = new Microsoft.UI.Xaml.Thickness(0,0,0,0), HorizontalAlignment = HorizontalAlignment.Left },
                }
            });
            stackPanel.Children.Add(thanksPanel);
            var gplPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Microsoft.UI.Xaml.Thickness(0,16,0,0) };
            gplPanel.Children.Add(new HyperlinkButton { Content = LanguageManager.Instance.T("AboutDialog_License"), NavigateUri = new Uri("https://www.gnu.org/licenses/gpl-3.0.html#license-text") });
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
                var progressBar = mainWindow.GetOrangeAnimationProgressBar();
                if (progressBar != null)
                {
                    progressBar.Opacity = ShowOrangeAnimationToggle.IsOn ? 1 : 0;
                }
            }
        }
    }
}