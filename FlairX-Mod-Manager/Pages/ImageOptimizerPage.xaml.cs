using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlairX_Mod_Manager.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Webp;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ImageOptimizerPage : Page
    {
        private bool _isInitialized = false;
        private bool _isOptimizing = false;
        
        private int _jpegQuality = 80;
        private int _webpQuality = 100;
        private int _threadCount = 4;
        private bool _createBackups = false;
        private bool _keepOriginals = false;

        public ImageOptimizerPage()
        {
            this.InitializeComponent();
            this.Loaded += ImageOptimizerPage_Loaded;
            Services.ImageOptimizationService.OptimizationProgressChanged += OnOptimizationProgressChanged;
        }
        
        private void OnOptimizationProgressChanged()
        {
            DispatcherQueue.TryEnqueue(UpdateProgressBarUI);
        }
        
        private void UpdateProgressBarUI()
        {
            var isOptimizing = Services.ImageOptimizationService.IsOptimizing;
            var progress = Services.ImageOptimizationService.ProgressValue;
            var currentMod = Services.ImageOptimizationService.CurrentProcessingMod;
            
            _isOptimizing = isOptimizing;
            
            if (isOptimizing)
            {
                OptimizeProgressBar.Visibility = Visibility.Visible;
                OptimizeProgressBar.IsIndeterminate = false;
                OptimizeProgressBar.Value = progress * 100;
                
                // Update status text
                if (OptimizeStatusText != null)
                {
                    OptimizeStatusText.Visibility = Visibility.Visible;
                    OptimizeStatusText.Text = !string.IsNullOrEmpty(currentMod) ? currentMod : "";
                }
                
                var lang = SharedUtilities.LoadLanguageDictionary("ImageOptimizer");
                OptimizeButtonText.Text = SharedUtilities.GetTranslation(lang, "Cancel");
            }
            else
            {
                OptimizeProgressBar.Value = 0;
                OptimizeProgressBar.IsIndeterminate = false;
                OptimizeProgressBar.Visibility = Visibility.Collapsed;
                
                // Hide status text
                if (OptimizeStatusText != null)
                {
                    OptimizeStatusText.Visibility = Visibility.Collapsed;
                    OptimizeStatusText.Text = "";
                }
                
                var lang = SharedUtilities.LoadLanguageDictionary("ImageOptimizer");
                OptimizeButtonText.Text = SharedUtilities.GetTranslation(lang, "Start");
            }
        }

        private void ImageOptimizerPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            InitializeUI();
            UpdateAllDescriptions();
            UpdateToggleLabels();
            _isInitialized = true;
        }
        
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Refresh translations when navigating to this page
            if (_isInitialized)
            {
                TranslateUI();
                UpdateAllDescriptions();
                UpdateToggleLabels();
            }
            
            // Restore progress bar state if optimization is running
            UpdateProgressBarUI();
        }
        
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // Keep subscription active - don't unsubscribe so progress continues
        }

        private void LoadSettings()
        {
            // Load from SettingsManager
            _jpegQuality = SettingsManager.Current.ImageOptimizerJpegQuality;
            _webpQuality = SettingsManager.Current.ImageOptimizerWebPQuality;
            _threadCount = SettingsManager.Current.ImageOptimizerThreadCount;
            _createBackups = SettingsManager.Current.ImageOptimizerCreateBackups;
            _keepOriginals = SettingsManager.Current.ImageOptimizerKeepOriginals;
            
            // Load image format
            string imageFormat = SettingsManager.Current.ImageFormat ?? "WebP";
            ImageFormatComboBox.SelectionChanged -= ImageFormatComboBox_SelectionChanged;
            foreach (ComboBoxItem item in ImageFormatComboBox.Items)
            {
                if ((string)item.Tag == imageFormat)
                {
                    ImageFormatComboBox.SelectedItem = item;
                    break;
                }
            }
            ImageFormatComboBox.SelectionChanged += ImageFormatComboBox_SelectionChanged;
            
            // Update quality slider state and value based on format
            if (imageFormat.Equals("WebP", StringComparison.OrdinalIgnoreCase))
            {
                JpegQualitySlider.IsEnabled = true;
                JpegQualitySlider.Value = _webpQuality;
                JpegQualityValue.Text = $"{_webpQuality}%";
            }
            else
            {
                JpegQualitySlider.IsEnabled = true;
                JpegQualitySlider.Value = _jpegQuality;
                JpegQualityValue.Text = $"{_jpegQuality}%";
            }
            
            // Load crop settings
            string cropType = SettingsManager.Current.ImageCropType ?? "Center";
            ImageCropTypeComboBox.SelectionChanged -= ImageCropTypeComboBox_SelectionChanged;
            foreach (ComboBoxItem item in ImageCropTypeComboBox.Items)
            {
                if ((string)item.Tag == cropType)
                {
                    ImageCropTypeComboBox.SelectedItem = item;
                    break;
                }
            }
            ImageCropTypeComboBox.SelectionChanged += ImageCropTypeComboBox_SelectionChanged;
            
            PreviewBeforeCropToggle.IsOn = SettingsManager.Current.PreviewBeforeCrop;
            AutoCreateModThumbnailsToggle.IsOn = SettingsManager.Current.AutoCreateModThumbnails;
            ReoptimizeCheckBox.IsOn = SettingsManager.Current.ImageOptimizerReoptimize;
            
            // Load screenshot directory setting - expand environment variables
            string screenshotDir = SettingsManager.Current.ScreenshotCaptureDirectory;
            if (string.IsNullOrEmpty(screenshotDir))
            {
                screenshotDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures", "Screenshots");
            }
            else
            {
                screenshotDir = Environment.ExpandEnvironmentVariables(screenshotDir);
            }
            SharedUtilities.SetBreadcrumbBarPath(ScreenshotDirectoryBreadcrumb, screenshotDir);
        }

        private void InitializeUI()
        {
            // Set slider values based on current format
            string currentFormat = SettingsManager.Current.ImageFormat ?? "WebP";
            if (currentFormat.Equals("WebP", StringComparison.OrdinalIgnoreCase))
            {
                JpegQualitySlider.Maximum = 101; // WebP supports lossless (101)
                JpegQualitySlider.Value = _webpQuality;
                // Display "Lossless" for quality 101
                JpegQualityValue.Text = _webpQuality >= 101 ? "Lossless" : $"{_webpQuality}%";
            }
            else
            {
                JpegQualitySlider.Maximum = 100; // JPEG only supports up to 100
                JpegQualitySlider.Value = _jpegQuality;
                JpegQualityValue.Text = $"{_jpegQuality}%";
            }
            
            // Set thread count based on CPU cores
            int logicalCores = Environment.ProcessorCount;
            int recommendedThreads = Math.Max(1, logicalCores - 1); // Leave 1 core free
            ThreadCountSlider.Maximum = logicalCores;
            ThreadCountSlider.Value = _threadCount > 0 ? _threadCount : recommendedThreads;
            ThreadCountValue.Text = _threadCount.ToString();
            
            // Update thread description with CPU info
            var lang = SharedUtilities.LoadLanguageDictionary();
            ThreadCountDescription.Text = string.Format(
                SharedUtilities.GetTranslation(lang, "ImageOptimizer_ThreadCount_Description") ?? "More threads = faster processing ({0} logical cores detected)",
                logicalCores
            );
            
            // Set toggle switches
            CreateBackupsCheckBox.IsOn = _createBackups;
            KeepOriginalsCheckBox.IsOn = _keepOriginals;
            ReoptimizeCheckBox.IsOn = SettingsManager.Current.ImageOptimizerReoptimize;
            
            // Translate UI first
            TranslateUI();
        }

        private void TranslateUI()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("ImageOptimizer");
            
            // Top section
            ManualOptimizationTitle.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_ManualOptimizationTitle");
            OptimizeDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_OptimizePreviews_Description");
            OptimizeButtonText.Text = _isOptimizing ? SharedUtilities.GetTranslation(lang, "Cancel") : SharedUtilities.GetTranslation(lang, "Start");
            
            // Headers
            QualityHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_QualityHeader");
            CroppingHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_CroppingHeader");
            PerformanceHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_PerformanceHeader");
            BackupHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_BackupHeader");
            ScreenshotCaptureHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_ScreenshotCaptureHeader");
            OptimizationModesHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_OptimizationModesHeader");
            OptimizationModesDescription.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_OptimizationModesDescription");
            ManualModeHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_ManualModeHeader");
            DragDropModHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_DragDropModHeader");
            DragDropCategoryHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_DragDropCategoryHeader");
            AutoDownloadHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_AutoDownloadHeader");
            
            // Labels
            var mainLang = SharedUtilities.LoadLanguageDictionary();
            JpegQualityLabel.Text = SharedUtilities.GetTranslation(mainLang, "ImageOptimizer_ConversionQuality");
            
            // Update description based on current format
            var currentFormat = SettingsManager.Current.ImageFormat ?? "WebP";
            if (currentFormat.Equals("WebP", StringComparison.OrdinalIgnoreCase))
            {
                JpegQualityDescription.Text = SharedUtilities.GetTranslation(mainLang, "ImageOptimizer_WebP_Quality_Description");
            }
            else
            {
                JpegQualityDescription.Text = SharedUtilities.GetTranslation(mainLang, "ImageOptimizer_Quality_Description");
            }
            ThreadCountLabel.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_ThreadCount");
            ThreadCountDescription.Text = string.Format(SharedUtilities.GetTranslation(lang, "ImageOptimizer_ThreadCount_Description"), Environment.ProcessorCount);
            
            // Cropping options
            ImageCropTypeLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ImageCropType_Label");
            ImageCropTypeDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_ImageCropType_Description");
            PreviewBeforeCropLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_PreviewBeforeCrop_Label");
            PreviewBeforeCropDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_PreviewBeforeCrop_Description");
            AutoCreateModThumbnailsLabel.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_AutoCreateModThumbnails_Label");
            AutoCreateModThumbnailsDescription.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_AutoCreateModThumbnails_Description");
            ScreenshotDirectoryLabel.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_ScreenshotDirectory_Label");
            ScreenshotDirectoryDescription.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_ScreenshotDirectory_Description");
            
            // Crop type ComboBox items
            CropTypeCenterText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_CropType_Center");
            CropTypeSmartText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_CropType_Smart");
            CropTypeEntropyText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_CropType_Entropy");
            CropTypeAttentionText.Text = SharedUtilities.GetTranslation(lang, "SettingsPage_CropType_Attention");
            
            // Backup options
            CreateBackupsLabel.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_CreateBackups");
            BackupDescription.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_CreateBackups_Description");
            KeepOriginalsLabel.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_KeepOriginals");
            KeepOriginalsDescription.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_KeepOriginals_Description");
            ReoptimizeLabel.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Reoptimize");
            ReoptimizeDescription.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Reoptimize_Description");
        }

        private void JpegQualitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            int newValue = (int)e.NewValue;
            
            // Save to appropriate field based on current format
            var format = SettingsManager.Current.ImageFormat ?? "WebP";
            if (format.Equals("WebP", StringComparison.OrdinalIgnoreCase))
            {
                _webpQuality = newValue;
                // Display "Lossless" for quality 101
                if (JpegQualityValue != null)
                {
                    JpegQualityValue.Text = _webpQuality >= 101 ? "Lossless" : $"{_webpQuality}%";
                }
            }
            else
            {
                _jpegQuality = newValue;
                if (JpegQualityValue != null)
                {
                    JpegQualityValue.Text = $"{_jpegQuality}%";
                }
            }
            
            SaveSettings();
        }
        
        private async void ImageFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ImageFormatComboBox.SelectedItem is ComboBoxItem item && item.Tag is string format)
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                
                // Check if switching to WebP and codec is not installed
                if (format.Equals("WebP", StringComparison.OrdinalIgnoreCase) && 
                    !WebPCodecChecker.IsWebPCodecInstalled())
                {
                    var dialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "WebP_Codec_Required") ?? "WebP Codec Required",
                        Content = SharedUtilities.GetTranslation(lang, "WebP_Codec_Message") ?? 
                            "To display WebP images, you need to install the WebP Image Extensions from Microsoft Store.\n\nClick 'Install' to open Microsoft Store.",
                        PrimaryButtonText = SharedUtilities.GetTranslation(lang, "Install") ?? "Install",
                        CloseButtonText = SharedUtilities.GetTranslation(lang, "Cancel") ?? "Cancel",
                        XamlRoot = this.XamlRoot
                    };
                    
                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        // Open Microsoft Store to install WebP codec
                        var storeLink = WebPCodecChecker.GetWebPCodecStoreLink();
                        try
                        {
                            var uri = new Uri(storeLink);
                            await Windows.System.Launcher.LaunchUriAsync(uri);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Failed to open Microsoft Store", ex);
                        }
                    }
                }
                
                var oldFormat = SettingsManager.Current.ImageFormat ?? "JPEG";
                SettingsManager.Current.ImageFormat = format;
                
                // Ask if user wants to convert existing images when switching formats
                if (!format.Equals(oldFormat, StringComparison.OrdinalIgnoreCase))
                {
                    var convertDialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "ConvertImages_Title") ?? "Convert Existing Images?",
                        Content = SharedUtilities.GetTranslation(lang, "ConvertImages_Message") ?? 
                            "Would you like to convert all existing images to the new format?\n\nThis will:\n• Convert preview images, thumbnails, and category images\n• Keep original files if 'Keep Originals' is enabled\n\n⚠️ WARNING: Converting back and forth between formats multiple times will degrade image quality. Only convert once from your original files.",
                        PrimaryButtonText = SharedUtilities.GetTranslation(lang, "ConvertImages_Convert") ?? "Convert Now",
                        SecondaryButtonText = SharedUtilities.GetTranslation(lang, "ConvertImages_Later") ?? "Maybe Later",
                        CloseButtonText = SharedUtilities.GetTranslation(lang, "Cancel") ?? "Cancel",
                        DefaultButton = ContentDialogButton.Secondary,
                        XamlRoot = this.XamlRoot
                    };
                    
                    var convertResult = await convertDialog.ShowAsync();
                    if (convertResult == ContentDialogResult.Primary)
                    {
                        // User wants to convert - use existing optimization system
                        OptimizeButton_Click(this, new RoutedEventArgs());
                    }
                }
                
                // Update quality slider state and switch between JPEG/WebP quality values
                var mainLang = SharedUtilities.LoadLanguageDictionary();
                if (format.Equals("WebP", StringComparison.OrdinalIgnoreCase))
                {
                    JpegQualitySlider.IsEnabled = true;
                    JpegQualitySlider.Maximum = 101; // WebP supports lossless (101)
                    JpegQualitySlider.Value = _webpQuality;
                    // Display "Lossless" for quality 101
                    JpegQualityValue.Text = _webpQuality >= 101 ? "Lossless" : $"{_webpQuality}%";
                    JpegQualityDescription.Text = SharedUtilities.GetTranslation(mainLang, "ImageOptimizer_WebP_Quality_Description") 
                        ?? "WebP quality: 1-100 = lossy (smaller files), 101 = lossless (perfect quality, larger files)";
                }
                else
                {
                    JpegQualitySlider.IsEnabled = true;
                    JpegQualitySlider.Maximum = 100; // JPEG only supports up to 100
                    JpegQualitySlider.Value = _jpegQuality;
                    JpegQualityValue.Text = $"{_jpegQuality}%";
                    JpegQualityDescription.Text = SharedUtilities.GetTranslation(mainLang, "ImageOptimizer_Quality_Description") 
                        ?? "Higher quality = larger file size";
                }
                
                SaveSettings();
            }
        }


        private void ThreadCountSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            _threadCount = (int)e.NewValue;
            ThreadCountValue.Text = _threadCount.ToString();
            SaveSettings();
        }

        private void SettingChanged(object sender, RoutedEventArgs e)
        {
            UpdateToggleLabels();
            SaveSettings();
        }
        
        private void UpdateToggleLabels()
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            var onText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_On");
            var offText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_Off");
            
            if (CreateBackupsToggleLabel != null && CreateBackupsCheckBox != null)
                CreateBackupsToggleLabel.Text = CreateBackupsCheckBox.IsOn ? onText : offText;
            if (KeepOriginalsToggleLabel != null && KeepOriginalsCheckBox != null)
                KeepOriginalsToggleLabel.Text = KeepOriginalsCheckBox.IsOn ? onText : offText;
            if (PreviewBeforeCropToggleLabel != null && PreviewBeforeCropToggle != null)
                PreviewBeforeCropToggleLabel.Text = PreviewBeforeCropToggle.IsOn ? onText : offText;
            if (AutoCreateModThumbnailsToggleLabel != null && AutoCreateModThumbnailsToggle != null)
                AutoCreateModThumbnailsToggleLabel.Text = AutoCreateModThumbnailsToggle.IsOn ? onText : offText;
            if (ReoptimizeToggleLabel != null && ReoptimizeCheckBox != null)
                ReoptimizeToggleLabel.Text = ReoptimizeCheckBox.IsOn ? onText : offText;
        }

        private void UpdateAllDescriptions()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("ImageOptimizer");
            
            // Static descriptions for each optimization scenario
            ManualModeDescription.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_ManualMode_Description");
            DragDropModDescription.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_DragDropMod_Description");
            DragDropCategoryDescription.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_DragDropCategory_Description");
            AutoDownloadDescription.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_AutoDownload_Description");
        }

        private void SaveSettings()
        {
            // Don't save during initialization
            if (!_isInitialized) return;
            
            // Get toggle switch values
            _createBackups = CreateBackupsCheckBox.IsOn;
            _keepOriginals = KeepOriginalsCheckBox.IsOn;
            
            // Save to SettingsManager
            SettingsManager.Current.ImageOptimizerJpegQuality = _jpegQuality;
            SettingsManager.Current.ImageOptimizerWebPQuality = _webpQuality;
            SettingsManager.Current.ImageOptimizerThreadCount = _threadCount;
            SettingsManager.Current.ImageOptimizerCreateBackups = _createBackups;
            SettingsManager.Current.ImageOptimizerKeepOriginals = _keepOriginals;
            SettingsManager.Current.ImageOptimizerReoptimize = ReoptimizeCheckBox.IsOn;
            
            SettingsManager.Save();
        }

        private async void OptimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if already optimizing (from service state)
            if (Services.ImageOptimizationService.IsOptimizing)
            {
                // Request safe cancellation - will finish current tasks
                Services.ImageOptimizationService.RequestCancellation();
                return;
            }

            // Show confirmation dialog
            var lang = SharedUtilities.LoadLanguageDictionary("ImageOptimizer");
            var confirmDialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(lang, "SettingsPage_OptimizePreviews_Label"),
                Content = SharedUtilities.GetTranslation(lang, "SettingsPage_OptimizePreviews_Description"),
                PrimaryButtonText = SharedUtilities.GetTranslation(lang, "Continue"),
                CloseButtonText = SharedUtilities.GetTranslation(lang, "Cancel"),
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;

            // Run optimization on background thread to prevent UI freezing
            _ = Task.Run(async () =>
            {
                try
                {
                    await Services.ImageOptimizationService.OptimizeAllPreviewsAsync();
                    
                    // Show success dialog on UI thread
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        var successDialog = new ContentDialog
                        {
                            Title = SharedUtilities.GetTranslation(lang, "Success_Title"),
                            Content = SharedUtilities.GetTranslation(lang, "OptimizePreviews_Completed"),
                            CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                            XamlRoot = this.XamlRoot
                        };
                        await successDialog.ShowAsync();
                    });
                }
                catch (OperationCanceledException)
                {
                    // Show cancelled dialog on UI thread - use main language dictionary for common keys
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        var mainLang = SharedUtilities.LoadLanguageDictionary();
                        var cancelDialog = new ContentDialog
                        {
                            Title = SharedUtilities.GetTranslation(mainLang, "Cancelled_Title"),
                            Content = SharedUtilities.GetTranslation(lang, "OptimizePreviews_Cancelled"),
                            CloseButtonText = SharedUtilities.GetTranslation(mainLang, "OK"),
                            XamlRoot = this.XamlRoot
                        };
                        await cancelDialog.ShowAsync();
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error during image optimization", ex);
                    
                    // Show error dialog on UI thread
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = SharedUtilities.GetTranslation(lang, "Error_Generic"),
                            Content = ex.Message,
                            CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                            XamlRoot = this.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                    });
                }
            });
            // UI state is managed by UpdateProgressBarUI via event
        }

        private void ImageCropTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ImageCropTypeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string cropType)
            {
                SettingsManager.Current.ImageCropType = cropType;
                SettingsManager.Save();
                Logger.LogInfo($"Image crop type changed to: {cropType}");
            }
        }

        private void PreviewBeforeCropToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.PreviewBeforeCrop = PreviewBeforeCropToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();
        }

        private void AutoCreateModThumbnailsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.AutoCreateModThumbnails = AutoCreateModThumbnailsToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();
        }

        private void ScreenshotDirectoryBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            if (!_isInitialized) return;
            
            var path = SharedUtilities.GetBreadcrumbBarPath(sender);
            SettingsManager.Current.ScreenshotCaptureDirectory = path;
            SettingsManager.Save();
        }

        private async void BrowseScreenshotDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                folderPicker.FileTypeFilter.Add("*");

                // Get the current window handle for the picker
                var mainWindow = (App.Current as App)?.MainWindow;
                if (mainWindow != null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
                    WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
                }

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    SharedUtilities.SetBreadcrumbBarPath(ScreenshotDirectoryBreadcrumb, folder.Path);
                    SettingsManager.Current.ScreenshotCaptureDirectory = folder.Path;
                    SettingsManager.Save();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error browsing for screenshot directory", ex);
            }
        }
    }
}