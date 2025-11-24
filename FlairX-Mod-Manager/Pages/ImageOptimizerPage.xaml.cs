using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlairX_Mod_Manager.Models;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ImageOptimizerPage : Page
    {
        private bool _isInitialized = false;
        private bool _isOptimizing = false;
        private CancellationTokenSource? _cts;
        
        private int _jpegQuality = 80;
        private int _threadCount = 4;
        private bool _createBackups = false;
        private bool _keepOriginals = false;
        
        private OptimizationMode _manualMode = OptimizationMode.Full;
        private OptimizationMode _dragDropModMode = OptimizationMode.Full;
        private OptimizationMode _dragDropCategoryMode = OptimizationMode.Full;
        private OptimizationMode _autoDownloadMode = OptimizationMode.Miniatures;

        public ImageOptimizerPage()
        {
            this.InitializeComponent();
            this.Loaded += ImageOptimizerPage_Loaded;
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
        }

        private void LoadSettings()
        {
            // Load from SettingsManager
            _jpegQuality = SettingsManager.Current.ImageOptimizerJpegQuality;
            _threadCount = SettingsManager.Current.ImageOptimizerThreadCount;
            _createBackups = SettingsManager.Current.ImageOptimizerCreateBackups;
            _keepOriginals = SettingsManager.Current.ImageOptimizerKeepOriginals;
            
            _manualMode = ParseMode(SettingsManager.Current.ImageOptimizerManualMode);
            _dragDropModMode = ParseMode(SettingsManager.Current.ImageOptimizerDragDropModMode);
            _dragDropCategoryMode = ParseMode(SettingsManager.Current.ImageOptimizerDragDropCategoryMode);
            _autoDownloadMode = ParseMode(SettingsManager.Current.ImageOptimizerAutoDownloadMode);
        }

        private void InitializeUI()
        {
            // Set slider values
            JpegQualitySlider.Value = _jpegQuality;
            JpegQualityValue.Text = $"{_jpegQuality}%";
            
            // Set thread count based on CPU cores
            int logicalCores = Environment.ProcessorCount;
            int recommendedThreads = Math.Max(1, Math.Min(logicalCores - 1, 8)); // Leave 1 core free, max 8
            ThreadCountSlider.Maximum = Math.Min(logicalCores, 16);
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
            
            // Translate UI first (before setting combo box selections)
            TranslateUI();
            
            // Set combo boxes (after translation so items have content)
            SetComboBoxSelection(ManualModeComboBox, _manualMode);
            SetComboBoxSelection(DragDropModComboBox, _dragDropModMode);
            SetComboBoxSelection(DragDropCategoryComboBox, _dragDropCategoryMode);
            SetComboBoxSelection(AutoDownloadComboBox, _autoDownloadMode);
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
            PerformanceHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_PerformanceHeader");
            BackupHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_BackupHeader");
            OptimizationModesHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_OptimizationModesHeader");
            ManualModeHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_ManualModeHeader");
            DragDropModHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_DragDropModHeader");
            DragDropCategoryHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_DragDropCategoryHeader");
            AutoDownloadHeader.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_AutoDownloadHeader");
            
            // Labels
            JpegQualityLabel.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_JpegQuality");
            JpegQualityDescription.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_JpegQuality_Description");
            ThreadCountLabel.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_ThreadCount");
            ThreadCountDescription.Text = string.Format(SharedUtilities.GetTranslation(lang, "ImageOptimizer_ThreadCount_Description"), Environment.ProcessorCount);
            
            // Backup options
            CreateBackupsLabel.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_CreateBackups");
            BackupDescription.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_CreateBackups_Description");
            KeepOriginalsLabel.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_KeepOriginals");
            KeepOriginalsDescription.Text = SharedUtilities.GetTranslation(lang, "ImageOptimizer_KeepOriginals_Description");
            
            // ComboBox items
            TranslateComboBoxItems(lang);
        }

        private void TranslateComboBoxItems(Dictionary<string, string> lang)
        {
            // Manual mode
            ManualModeFullItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Full");
            ManualModeNoResizeItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_NoResize");
            ManualModeMiniaturesItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Miniatures");
            ManualModeRenameItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Rename");
            ManualModeDisabledItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Disabled");
            
            // Drag & Drop Mod
            DragDropModFullItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Full");
            DragDropModNoResizeItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_NoResize");
            DragDropModMiniaturesItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Miniatures");
            DragDropModRenameItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Rename");
            DragDropModDisabledItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Disabled");
            
            // Drag & Drop Category
            DragDropCategoryFullItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Full");
            DragDropCategoryNoResizeItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_NoResize");
            DragDropCategoryMiniaturesItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Miniatures");
            DragDropCategoryRenameItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Rename");
            DragDropCategoryDisabledItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Disabled");
            
            // Auto Download
            AutoDownloadFullItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Full");
            AutoDownloadNoResizeItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_NoResize");
            AutoDownloadMiniaturesItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Miniatures");
            AutoDownloadRenameItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Rename");
            AutoDownloadDisabledItem.Content = SharedUtilities.GetTranslation(lang, "ImageOptimizer_Mode_Disabled");
        }

        private void SetComboBoxSelection(ComboBox comboBox, OptimizationMode mode)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag is string tag && tag == mode.ToString())
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private OptimizationMode ParseMode(string modeString)
        {
            if (Enum.TryParse<OptimizationMode>(modeString, out var mode))
                return mode;
            return OptimizationMode.Full;
        }

        private void JpegQualitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            _jpegQuality = (int)e.NewValue;
            JpegQualityValue.Text = $"{_jpegQuality}%";
            SaveSettings();
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
        }

        private void ManualModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ManualModeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _manualMode = ParseMode(tag);
                UpdateModeDescription(ManualModeDescription, _manualMode);
                SaveSettings();
            }
        }

        private void DragDropModComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DragDropModComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _dragDropModMode = ParseMode(tag);
                UpdateModeDescription(DragDropModDescription, _dragDropModMode);
                SaveSettings();
            }
        }

        private void DragDropCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DragDropCategoryComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _dragDropCategoryMode = ParseMode(tag);
                UpdateModeDescription(DragDropCategoryDescription, _dragDropCategoryMode);
                SaveSettings();
            }
        }

        private void AutoDownloadComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AutoDownloadComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _autoDownloadMode = ParseMode(tag);
                UpdateModeDescription(AutoDownloadDescription, _autoDownloadMode);
                SaveSettings();
            }
        }

        private void UpdateAllDescriptions()
        {
            UpdateModeDescription(ManualModeDescription, _manualMode);
            UpdateModeDescription(DragDropModDescription, _dragDropModMode);
            UpdateModeDescription(DragDropCategoryDescription, _dragDropCategoryMode);
            UpdateModeDescription(AutoDownloadDescription, _autoDownloadMode);
        }

        private void UpdateModeDescription(TextBlock textBlock, OptimizationMode mode)
        {
            var lang = SharedUtilities.LoadLanguageDictionary("ImageOptimizer");
            string key = mode switch
            {
                OptimizationMode.Full => "ImageOptimizer_Mode_Full_Description",
                OptimizationMode.NoResize => "ImageOptimizer_Mode_NoResize_Description",
                OptimizationMode.Miniatures => "ImageOptimizer_Mode_Miniatures_Description",
                OptimizationMode.Rename => "ImageOptimizer_Mode_Rename_Description",
                OptimizationMode.Disabled => "ImageOptimizer_Mode_Disabled_Description",
                _ => "ImageOptimizer_Mode_Full_Description"
            };
            
            textBlock.Text = SharedUtilities.GetTranslation(lang, key);
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
            SettingsManager.Current.ImageOptimizerThreadCount = _threadCount;
            SettingsManager.Current.ImageOptimizerCreateBackups = _createBackups;
            SettingsManager.Current.ImageOptimizerKeepOriginals = _keepOriginals;
            
            SettingsManager.Current.ImageOptimizerManualMode = _manualMode.ToString();
            SettingsManager.Current.ImageOptimizerDragDropModMode = _dragDropModMode.ToString();
            SettingsManager.Current.ImageOptimizerDragDropCategoryMode = _dragDropCategoryMode.ToString();
            SettingsManager.Current.ImageOptimizerAutoDownloadMode = _autoDownloadMode.ToString();
            
            SettingsManager.Save();
        }

        private async void OptimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isOptimizing)
            {
                // Cancel operation
                _cts?.Cancel();
                return;
            }

            // Show confirmation dialog
            var lang = SharedUtilities.LoadLanguageDictionary();
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

            // Start optimization
            _isOptimizing = true;
            _cts = new CancellationTokenSource();
            
            OptimizeButton.IsEnabled = true;
            OptimizeProgressBar.Visibility = Visibility.Visible;
            OptimizeButtonText.Text = SharedUtilities.GetTranslation(lang, "Cancel");

            try
            {
                await SettingsUserControl.OptimizePreviewsDirectAsync();
                
                // Show success dialog
                var successDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "Success_Title"),
                    Content = SharedUtilities.GetTranslation(lang, "OptimizePreviews_Completed"),
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                    XamlRoot = this.XamlRoot
                };
                await successDialog.ShowAsync();
            }
            catch (OperationCanceledException)
            {
                // Show cancelled dialog
                var cancelDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "Error_Generic"),
                    Content = SharedUtilities.GetTranslation(lang, "OptimizePreviews_Cancelled"),
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                    XamlRoot = this.XamlRoot
                };
                await cancelDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during image optimization", ex);
                
                var errorDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "Error_Generic"),
                    Content = ex.Message,
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
            finally
            {
                _isOptimizing = false;
                _cts = null;
                OptimizeButton.IsEnabled = true;
                OptimizeProgressBar.Visibility = Visibility.Collapsed;
                OptimizeButtonText.Text = SharedUtilities.GetTranslation(lang, "Start");
            }
        }
    }
}
