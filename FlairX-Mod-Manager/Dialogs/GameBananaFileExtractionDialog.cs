using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlairX_Mod_Manager.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace FlairX_Mod_Manager.Dialogs
{
    public class GameBananaFileExtractionDialog : ContentDialog
    {
        private List<Models.GameBananaFileViewModel> _selectedFiles;
        private string _modName;
        private string _gameTag;
        private string _authorName;
        private int _modId;
        private long _dateUpdatedTimestamp;
        private TextBox _categoryTextBox = null!;
        private ProgressBar _progressBar = null!;
        private TextBlock _statusText = null!;
        private string? _downloadedArchivePath = null;
        private string? _modProfileUrl;
        private TextBox _modNameTextBox = null!;
        private ProgressBar _downloadProgressBar = null!;
        private TextBlock _downloadStatusText = null!;
        private ProgressBar _extractProgressBar = null!;
        private TextBlock _extractStatusText = null!;
        private bool _downloadPreviews = false;
        private bool _cleanInstall = false;
        private bool _createBackup = false;
        private bool _keepPreviews = true;
        private bool _combinePreviews = false;
        private GameBananaService.PreviewMedia? _previewMedia;
        private string? _installedModPath = null;
        private CheckBox? _cleanInstallCheckBox;
        private CheckBox? _createBackupCheckBox;
        private CheckBox? _keepPreviewsCheckBox;
        private CheckBox? _combinePreviewsCheckBox;
        private Grid? _updateOptionsGrid;
        private System.Collections.Generic.Dictionary<string, string> _lang = new();
        private bool _isNSFW = false;
        private string? _version;
        private string? _existingModPathForPreviewsOnly = null;

        // Event fired when mod is successfully installed
        public event EventHandler<ModInstalledEventArgs>? ModInstalled;
        
        public class ModInstalledEventArgs : EventArgs
        {
            public string? ModProfileUrl { get; set; }
            public int ModId { get; set; }
            public string? ModPath { get; set; }
            public string? Category { get; set; }
        }

        // Helper methods for image format
        private static bool IsImageFile(string filePath)
        {
            return filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
        }
        
        private static bool IsMinitileFile(string fileName)
        {
            var name = Path.GetFileName(fileName).ToLower();
            return name == "minitile.jpg" || name == "minitile.webp";
        }

        public GameBananaFileExtractionDialog(
            List<Models.GameBananaFileViewModel> selectedFiles,
            string modName,
            string gameTag,
            string? modProfileUrl = null,
            string? authorName = null,
            int modId = 0,
            long dateUpdatedTimestamp = 0,
            string? categoryName = null,
            GameBananaService.PreviewMedia? previewMedia = null,
            bool isNSFW = false,
            string? version = null,
            string? existingModPath = null)
        {
            _selectedFiles = selectedFiles;
            _modName = modName;
            _gameTag = gameTag;
            _modProfileUrl = modProfileUrl;
            _authorName = authorName ?? "unknown";
            _modId = modId;
            _dateUpdatedTimestamp = dateUpdatedTimestamp;
            _previewMedia = previewMedia;
            _isNSFW = isNSFW;
            _version = version;
            _existingModPathForPreviewsOnly = existingModPath; // Use provided path if available

            // Load language
            _lang = SharedUtilities.LoadLanguageDictionary("GameBananaBrowser");

            Title = SharedUtilities.GetTranslation(_lang, "DownloadAndInstallMod");
            PrimaryButtonText = SharedUtilities.GetTranslation(_lang, "Start");
            SecondaryButtonText = SharedUtilities.GetTranslation(_lang, "DownloadPreviewsOnly");
            CloseButtonText = SharedUtilities.GetTranslation(_lang, "Cancel");
            DefaultButton = ContentDialogButton.Primary;
            
            // Check if this is preview-only mode (empty file list)
            bool isPreviewOnlyMode = selectedFiles.Count == 0;
            
            if (isPreviewOnlyMode)
            {
                // Preview-only mode: hide primary button, make secondary button the main action
                Title = SharedUtilities.GetTranslation(_lang, "DownloadPreviewsOnly");
                PrimaryButtonText = SharedUtilities.GetTranslation(_lang, "Cancel");
                SecondaryButtonText = SharedUtilities.GetTranslation(_lang, "DownloadPreviewsOnly");
                CloseButtonText = ""; // Hide close button to avoid double cancel
                DefaultButton = ContentDialogButton.Secondary;
                
                // Enable "Download Previews Only" button if previews are available
                IsSecondaryButtonEnabled = _previewMedia?.Images != null && _previewMedia.Images.Any(img => img.Type == "screenshot");
                IsPrimaryButtonEnabled = true; // Cancel button
            }
            else
            {
                // Normal mode: Enable "Download Previews Only" button if previews are available
                IsSecondaryButtonEnabled = _previewMedia?.Images != null && _previewMedia.Images.Any(img => img.Type == "screenshot");
            }

            // Create content
            var stackPanel = new StackPanel { Spacing = 16 };

            if (!isPreviewOnlyMode)
            {
                // Mod Name (only show in normal mode)
                var modNameLabel = new TextBlock
                {
                    Text = SharedUtilities.GetTranslation(_lang, "ModName"),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                stackPanel.Children.Add(modNameLabel);

                _modNameTextBox = new TextBox
                {
                    Text = modName,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                _modNameTextBox.BeforeTextChanging += ModNameTextBox_BeforeTextChanging;
                _modNameTextBox.TextChanged += (s, e) => ValidateInputs();
                stackPanel.Children.Add(_modNameTextBox);

                // Category selection (only show in normal mode)
                var categoryLabel = new TextBlock
                {
                    Text = SharedUtilities.GetTranslation(_lang, "Category"),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                stackPanel.Children.Add(categoryLabel);

                _categoryTextBox = new TextBox
                {
                    PlaceholderText = SharedUtilities.GetTranslation(_lang, "EnterCategoryName"),
                    Text = categoryName ?? "Characters",
                    Margin = new Thickness(0, 0, 0, 8)
                };
                _categoryTextBox.BeforeTextChanging += CategoryTextBox_BeforeTextChanging;
                _categoryTextBox.TextChanged += (s, e) => ValidateInputs();
                stackPanel.Children.Add(_categoryTextBox);
            }
            else
            {
                // Preview-only mode: create hidden textboxes with default values
                _modNameTextBox = new TextBox { Text = modName, Visibility = Visibility.Collapsed };
                _categoryTextBox = new TextBox { Text = categoryName ?? "Characters", Visibility = Visibility.Collapsed };
                
                // Show info message for preview-only mode
                var infoMessage = new TextBlock
                {
                    Text = SharedUtilities.GetTranslation(_lang, "PreviewOnlyMode_Info"),
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 16),
                    TextWrapping = TextWrapping.Wrap
                };
                stackPanel.Children.Add(infoMessage);
            }

            // Download previews checkbox (only visible in normal mode, always enabled in preview-only mode)
            if (!isPreviewOnlyMode)
            {
                var downloadPreviewsCheckBox = new CheckBox
                {
                    Content = SharedUtilities.GetTranslation(_lang, "DownloadPreviews"),
                    IsChecked = false,
                    Margin = new Thickness(0, 0, 0, 16)
                };
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(downloadPreviewsCheckBox, 
                    SharedUtilities.GetTranslation(_lang, "DownloadPreviews_Tooltip"));
                downloadPreviewsCheckBox.Checked += (s, e) => _downloadPreviews = true;
                downloadPreviewsCheckBox.Unchecked += (s, e) => _downloadPreviews = false;
                stackPanel.Children.Add(downloadPreviewsCheckBox);
            }
            else
            {
                // In preview-only mode, always download previews
                _downloadPreviews = true;
                
                // Show only "Combine Previews" option in preview-only mode
                var combinePreviewsCheckBox = new CheckBox
                {
                    Content = SharedUtilities.GetTranslation(_lang, "CombinePreviews"),
                    IsChecked = false,
                    Margin = new Thickness(0, 0, 0, 16)
                };
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(combinePreviewsCheckBox, 
                    SharedUtilities.GetTranslation(_lang, "CombinePreviews_Tooltip"));
                combinePreviewsCheckBox.Checked += (s, e) => _combinePreviews = true;
                combinePreviewsCheckBox.Unchecked += (s, e) => _combinePreviews = false;
                stackPanel.Children.Add(combinePreviewsCheckBox);
                
                // Store reference for later use
                _combinePreviewsCheckBox = combinePreviewsCheckBox;
            }

            // Two-column layout for update-only checkboxes (only in normal mode)
            if (!isPreviewOnlyMode)
            {
                _updateOptionsGrid = new Grid
                {
                    ColumnSpacing = 16,
                    Margin = new Thickness(0, 0, 0, 16),
                    Visibility = Visibility.Collapsed // Will be shown if update is detected
                };
                _updateOptionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                _updateOptionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Left column - Installation options
                var leftColumn = new StackPanel { Spacing = 4 };
                
                // Clean install checkbox
                _cleanInstallCheckBox = new CheckBox
                {
                    Content = SharedUtilities.GetTranslation(_lang, "CleanInstall"),
                    IsChecked = false
                };
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(_cleanInstallCheckBox, 
                    SharedUtilities.GetTranslation(_lang, "CleanInstall_Tooltip"));
                _cleanInstallCheckBox.Checked += (s, e) => _cleanInstall = true;
                _cleanInstallCheckBox.Unchecked += (s, e) => _cleanInstall = false;
                leftColumn.Children.Add(_cleanInstallCheckBox);

                // Create backup checkbox
                _createBackupCheckBox = new CheckBox
                {
                    Content = SharedUtilities.GetTranslation(_lang, "CreateBackup"),
                    IsChecked = false
                };
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(_createBackupCheckBox, 
                    SharedUtilities.GetTranslation(_lang, "CreateBackup_Tooltip"));
                _createBackupCheckBox.Checked += (s, e) => _createBackup = true;
                _createBackupCheckBox.Unchecked += (s, e) => _createBackup = false;
                leftColumn.Children.Add(_createBackupCheckBox);

                Grid.SetColumn(leftColumn, 0);
                _updateOptionsGrid.Children.Add(leftColumn);

                // Right column - Preview options
                var rightColumn = new StackPanel { Spacing = 4 };

                // Keep previews checkbox
                _keepPreviewsCheckBox = new CheckBox
                {
                    Content = SharedUtilities.GetTranslation(_lang, "KeepPreviews"),
                    IsChecked = true
                };
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(_keepPreviewsCheckBox, 
                    SharedUtilities.GetTranslation(_lang, "KeepPreviews_Tooltip"));
                _keepPreviewsCheckBox.Checked += (s, e) => _keepPreviews = true;
                _keepPreviewsCheckBox.Unchecked += (s, e) => _keepPreviews = false;
                rightColumn.Children.Add(_keepPreviewsCheckBox);

                // Combine previews checkbox
                _combinePreviewsCheckBox = new CheckBox
                {
                    Content = SharedUtilities.GetTranslation(_lang, "CombinePreviews"),
                    IsChecked = false
                };
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(_combinePreviewsCheckBox, 
                    SharedUtilities.GetTranslation(_lang, "CombinePreviews_Tooltip"));
                _combinePreviewsCheckBox.Checked += (s, e) => _combinePreviews = true;
                _combinePreviewsCheckBox.Unchecked += (s, e) => _combinePreviews = false;
                rightColumn.Children.Add(_combinePreviewsCheckBox);

                Grid.SetColumn(rightColumn, 1);
                _updateOptionsGrid.Children.Add(rightColumn);

                stackPanel.Children.Add(_updateOptionsGrid);
            }

            // Download Progress
            _downloadStatusText = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "Download"),
                FontSize = 12,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stackPanel.Children.Add(_downloadStatusText);

            _downloadProgressBar = new ProgressBar
            {
                IsIndeterminate = false,
                Value = 0,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stackPanel.Children.Add(_downloadProgressBar);

            // Extract Progress (hide in preview-only mode)
            _extractStatusText = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "Extract"),
                FontSize = 12,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 4),
                Visibility = isPreviewOnlyMode ? Visibility.Collapsed : Visibility.Visible
            };
            stackPanel.Children.Add(_extractStatusText);

            _extractProgressBar = new ProgressBar
            {
                IsIndeterminate = false,
                Value = 0,
                Visibility = isPreviewOnlyMode ? Visibility.Collapsed : Visibility.Visible
            };
            stackPanel.Children.Add(_extractProgressBar);

            // Keep old progress for compatibility (hidden)
            _statusText = new TextBlock { Visibility = Visibility.Collapsed };
            _progressBar = new ProgressBar { Visibility = Visibility.Collapsed };

            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 600,
                Width = 500
            };

            // Check if this is an update and show appropriate checkboxes
            // Normalize category name for folder lookup
            var normalizedCategory = (categoryName ?? "Characters").Replace("/", "-");
            if (normalizedCategory.Equals("Other", StringComparison.OrdinalIgnoreCase) || 
                normalizedCategory.Equals("Other-Misc", StringComparison.OrdinalIgnoreCase))
            {
                normalizedCategory = "Other";
            }
            CheckIfUpdateAndShowOptions(normalizedCategory);

            // Handle primary button click
            PrimaryButtonClick += OnPrimaryButtonClick;
            
            // Handle secondary button click (Download Previews Only)
            SecondaryButtonClick += OnSecondaryButtonClick;
        }

        private void CheckIfUpdateAndShowOptions(string category)
        {
            try
            {
                // Check if this is preview-only mode (empty file list)
                bool isPreviewOnlyMode = _selectedFiles.Count == 0;
                
                // If existingModPath was provided in constructor, use it directly
                if (!string.IsNullOrEmpty(_existingModPathForPreviewsOnly) && Directory.Exists(_existingModPathForPreviewsOnly))
                {
                    // Show update-specific options grid only in normal mode
                    if (!isPreviewOnlyMode && _updateOptionsGrid != null)
                        _updateOptionsGrid.Visibility = Visibility.Visible;
                    
                    Title = isPreviewOnlyMode ? 
                        SharedUtilities.GetTranslation(_lang, "DownloadPreviewsOnly") :
                        SharedUtilities.GetTranslation(_lang, "DownloadAndUpdateMod");
                    Logger.LogInfo($"Update detected for mod (from provided path): {_existingModPathForPreviewsOnly}");
                    
                    // Use existing folder name instead of GameBanana name
                    var existingFolderName = Path.GetFileName(_existingModPathForPreviewsOnly);
                    // Remove DISABLED_ prefix if present
                    if (existingFolderName.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase))
                    {
                        existingFolderName = existingFolderName.Substring(9);
                    }
                    _modNameTextBox.Text = existingFolderName;
                    Logger.LogInfo($"Using existing folder name: {existingFolderName}");
                    
                    // Update category to where mod actually is
                    var existingCategory = Path.GetFileName(Path.GetDirectoryName(_existingModPathForPreviewsOnly));
                    if (!string.IsNullOrEmpty(existingCategory))
                    {
                        _categoryTextBox.Text = existingCategory;
                        Logger.LogInfo($"Using existing category: {existingCategory}");
                    }
                    
                    // Enable "Download Previews Only" button if previews are available
                    if (_previewMedia?.Images != null && _previewMedia.Images.Any(img => img.Type == "screenshot"))
                    {
                        IsSecondaryButtonEnabled = true;
                        Logger.LogInfo($"Previews available for download-only option");
                    }
                    return;
                }
                
                // Get mod library path
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
                    return;

                // Search ALL categories for existing mod by ID (user may have moved it)
                string? existingModPath = null;
                string? foundInCategory = null;
                
                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    var found = FindExistingModPathByModId(categoryDir, _modProfileUrl);
                    if (!string.IsNullOrEmpty(found))
                    {
                        existingModPath = found;
                        foundInCategory = Path.GetFileName(categoryDir);
                        break;
                    }
                }

                // Check if mod exists - this is an update
                if (!string.IsNullOrEmpty(existingModPath) && Directory.Exists(existingModPath))
                {
                    // Show update-specific options grid only in normal mode
                    if (!isPreviewOnlyMode && _updateOptionsGrid != null)
                        _updateOptionsGrid.Visibility = Visibility.Visible;
                    
                    Title = isPreviewOnlyMode ? 
                        SharedUtilities.GetTranslation(_lang, "DownloadPreviewsOnly") :
                        SharedUtilities.GetTranslation(_lang, "DownloadAndUpdateMod");
                    Logger.LogInfo($"Update detected for mod: {existingModPath}");
                    
                    // Use existing folder name instead of GameBanana name
                    var existingFolderName = Path.GetFileName(existingModPath);
                    // Remove DISABLED_ prefix if present
                    if (existingFolderName.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase))
                    {
                        existingFolderName = existingFolderName.Substring(9);
                    }
                    _modNameTextBox.Text = existingFolderName;
                    Logger.LogInfo($"Using existing folder name: {existingFolderName}");
                    
                    // Update category to where mod actually is
                    if (!string.IsNullOrEmpty(foundInCategory))
                    {
                        _categoryTextBox.Text = foundInCategory;
                        Logger.LogInfo($"Using existing category: {foundInCategory}");
                    }
                    
                    // Enable "Download Previews Only" button if previews are available
                    if (_previewMedia?.Images != null && _previewMedia.Images.Any(img => img.Type == "screenshot"))
                    {
                        _existingModPathForPreviewsOnly = existingModPath;
                        IsSecondaryButtonEnabled = true;
                        Logger.LogInfo($"Previews available for download-only option");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to check if update", ex);
            }
        }

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Check if this is preview-only mode (empty file list)
            bool isPreviewOnlyMode = _selectedFiles.Count == 0;
            
            if (isPreviewOnlyMode)
            {
                // In preview-only mode, primary button is Cancel
                Hide();
                return;
            }
            
            // Prevent dialog from closing immediately
            args.Cancel = true;

            try
            {
                IsPrimaryButtonEnabled = false;
                IsSecondaryButtonEnabled = false;

                var category = _categoryTextBox.Text.Trim();
                // Validation is handled by ValidateInputs() - button should be disabled if invalid
                if (string.IsNullOrWhiteSpace(category) || IsReservedWindowsName(category))
                {
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                    return;
                }

                var modName = _modNameTextBox.Text.Trim();
                if (IsReservedWindowsName(modName))
                {
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                    return;
                }

                // Determine folder name: replace "/" with "-" to prevent subfolder creation
                string categoryFolderName = category.Replace("/", "-");
                
                // Special case: "Other" and "Other-Misc" both go to "Other" folder
                if (categoryFolderName.Equals("Other", StringComparison.OrdinalIgnoreCase) || 
                    categoryFolderName.Equals("Other-Misc", StringComparison.OrdinalIgnoreCase))
                {
                    categoryFolderName = "Other";
                }

                // Download files
                var tempDir = Path.Combine(Path.GetTempPath(), "FlairX_Downloads", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                var downloadedFiles = new List<(string filePath, string fileName)>();

                for (int i = 0; i < _selectedFiles.Count; i++)
                {
                    var file = _selectedFiles[i];
                    _downloadStatusText.Text = string.Format(SharedUtilities.GetTranslation(_lang, "DownloadingFile"), 
                        file.FileName, i + 1, _selectedFiles.Count);

                    var tempFilePath = Path.Combine(tempDir, file.FileName);
                    var progress = new Progress<double>(value =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            _downloadProgressBar.Value = value;
                        });
                    });

                    var success = await GameBananaService.DownloadFileAsync(file.DownloadUrl, tempFilePath, progress);

                    if (!success)
                    {
                        await ShowError(string.Format(SharedUtilities.GetTranslation(_lang, "FailedToDownload"), file.FileName));
                        Directory.Delete(tempDir, true);
                        IsPrimaryButtonEnabled = true;
                        IsSecondaryButtonEnabled = true;
                        return;
                    }
                    
                    _downloadProgressBar.Value = 100;
                    downloadedFiles.Add((tempFilePath, file.FileName));
                }

                // Extract archives based on count
                bool hasArchives = downloadedFiles.Any(f => IsArchiveFile(f.fileName));
                
                if (hasArchives)
                {
                    if (_selectedFiles.Count == 1)
                    {
                        // Single file - extract directly to mod folder (old behavior)
                        _downloadedArchivePath = downloadedFiles[0].filePath;
                        await ExtractArchiveAsync(categoryFolderName);
                    }
                    else
                    {
                        // Multiple files - extract each archive to its own subfolder
                        await ExtractMultipleArchivesAsync(categoryFolderName, downloadedFiles);
                    }
                }
                else
                {
                    // No archives, just move files
                    await InstallFiles(tempDir, categoryFolderName);
                }

                // Download preview images if enabled
                if (_downloadPreviews && _previewMedia?.Images != null && !string.IsNullOrEmpty(_installedModPath))
                {
                    await DownloadPreviewImagesAsync(_installedModPath);
                }

                // Success
                _downloadStatusText.Text = SharedUtilities.GetTranslation(_lang, "DownloadComplete");
                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractComplete");
                _downloadProgressBar.Value = 100;
                _extractProgressBar.Value = 100;
                await Task.Delay(1000);

                // Fire event to notify that mod was installed - only if installation was successful
                if (!string.IsNullOrEmpty(_installedModPath) && Directory.Exists(_installedModPath))
                {
                    Logger.LogInfo($"[DIALOG] Firing ModInstalled event for: {_installedModPath}");
                    ModInstalled?.Invoke(this, new ModInstalledEventArgs 
                    { 
                        ModProfileUrl = _modProfileUrl,
                        ModId = _modId,
                        ModPath = _installedModPath,
                        Category = _categoryTextBox.Text.Trim()
                    });
                    Logger.LogInfo($"[DIALOG] ModInstalled event fired");
                }
                else
                {
                    await ShowError(SharedUtilities.GetTranslation(_lang, "InstallationFailed"));
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                    return;
                }

                Logger.LogInfo($"[DIALOG] Closing dialog with Hide()");
                // Close dialog - optimization will run in background after dialog closes
                Hide();
                Logger.LogInfo($"[DIALOG] Dialog closed");
                
                // Run optimization AFTER dialog closes (on UI thread for crop panel support)
                if (_downloadPreviews && !string.IsNullOrEmpty(_installedModPath))
                {
                    Logger.LogInfo($"[DIALOG] Starting OptimizeDownloadedPreviewsAsync for: {_installedModPath}");
                    // Don't use Task.Run - we need to stay on UI thread for crop panel
                    _ = OptimizeDownloadedPreviewsAsync(_installedModPath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to download and install mod", ex);
                await ShowError(string.Format(SharedUtilities.GetTranslation(_lang, "InstallationFailed"), ex.Message));
                IsPrimaryButtonEnabled = true;
                IsSecondaryButtonEnabled = true;
            }
        }

        private async void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Prevent dialog from closing immediately
            args.Cancel = true;

            try
            {
                IsPrimaryButtonEnabled = false;
                IsSecondaryButtonEnabled = false;

                // Determine target mod path
                string modPath;
                
                if (!string.IsNullOrEmpty(_existingModPathForPreviewsOnly))
                {
                    // Use existing mod path
                    modPath = _existingModPathForPreviewsOnly;
                }
                else
                {
                    // Create new mod folder for previews only
                    var category = _categoryTextBox.Text.Trim();
                    if (string.IsNullOrWhiteSpace(category))
                        category = "Characters";
                    
                    // Normalize category name
                    string categoryFolderName = category.Replace("/", "-");
                    if (categoryFolderName.Equals("Other", StringComparison.OrdinalIgnoreCase) || 
                        categoryFolderName.Equals("Other-Misc", StringComparison.OrdinalIgnoreCase))
                    {
                        categoryFolderName = "Other";
                    }
                    
                    var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                    var categoryPath = Path.Combine(modsPath, categoryFolderName);
                    Directory.CreateDirectory(categoryPath);
                    
                    var cleanModName = SanitizeFileName(_modNameTextBox.Text.Trim());
                    modPath = Path.Combine(categoryPath, "DISABLED_" + cleanModName);
                    
                    // Check if already exists
                    var existingPath = FindExistingModPath(categoryPath, cleanModName);
                    if (!string.IsNullOrEmpty(existingPath))
                    {
                        modPath = existingPath;
                    }
                    else
                    {
                        Directory.CreateDirectory(modPath);
                        // Create mod.json for new mod
                        await CreateModJson(modPath);
                    }
                }

                // Download preview images only
                _downloadStatusText.Text = SharedUtilities.GetTranslation(_lang, "DownloadingPreviews");
                _downloadProgressBar.IsIndeterminate = true;

                await DownloadPreviewImagesAsync(modPath);

                // Success
                _downloadStatusText.Text = SharedUtilities.GetTranslation(_lang, "DownloadComplete");
                _downloadProgressBar.Value = 100;
                await Task.Delay(1000);

                Logger.LogInfo($"[DIALOG] Closing dialog (preview-only mode) with Hide()");
                // Close dialog
                Hide();
                Logger.LogInfo($"[DIALOG] Dialog closed (preview-only mode)");

                Logger.LogInfo($"[DIALOG] Starting OptimizeDownloadedPreviewsAsync (preview-only) for: {modPath}");
                // Run optimization AFTER dialog closes (on UI thread for crop panel support)
                // Don't use Task.Run - we need to stay on UI thread for crop panel
                _ = OptimizeDownloadedPreviewsAsync(modPath);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to download previews only", ex);
                await ShowError(string.Format(SharedUtilities.GetTranslation(_lang, "DownloadPreviewsFailed"), ex.Message));
                IsPrimaryButtonEnabled = true;
                IsSecondaryButtonEnabled = true;
            }
        }

        private async Task ExtractArchiveAsync(string category)
        {
            try
            {
                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractPreparing");
                _extractProgressBar.IsIndeterminate = true;

                // Get mod library path
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();

                var categoryPath = Path.Combine(modsPath, category);
                Directory.CreateDirectory(categoryPath);

                // Check if mod already exists (with or without DISABLED_ prefix)
                var cleanModName = SanitizeFileName(_modNameTextBox.Text.Trim());
                var existingModPath = FindExistingModPath(categoryPath, cleanModName);
                
                bool isUpdate = false;
                bool wasActive = false;
                string modPath;
                
                if (!string.IsNullOrEmpty(existingModPath) && Directory.Exists(existingModPath))
                {
                    // Mod exists - check if it's an update
                    modPath = existingModPath;
                    
                    // Check if mod is currently active (no DISABLED_ prefix)
                    var folderName = Path.GetFileName(modPath);
                    wasActive = !folderName.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase);
                    
                    var modJsonPath = Path.Combine(modPath, "mod.json");
                    if (File.Exists(modJsonPath))
                    {
                        try
                        {
                            var modJsonContent = await Services.FileAccessQueue.ReadAllTextAsync(modJsonPath);
                            var modJson = System.Text.Json.JsonDocument.Parse(modJsonContent);
                            if (modJson.RootElement.TryGetProperty("url", out var urlProp))
                            {
                                var existingUrl = urlProp.GetString();
                                // If URLs match, this is an update
                                if (existingUrl == _modProfileUrl)
                                {
                                    isUpdate = true;
                                    Logger.LogInfo($"Updating existing mod at: {modPath} (was active: {wasActive})");
                                    
                                    // If mod is active, temporarily deactivate it for update
                                    if (wasActive)
                                    {
                                        var tempDisabledPath = Path.Combine(categoryPath, "DISABLED_" + cleanModName);
                                        
                                        // Check if DISABLED_ version already exists
                                        if (Directory.Exists(tempDisabledPath))
                                        {
                                            throw new InvalidOperationException($"Cannot update active mod: A disabled version already exists at {tempDisabledPath}");
                                        }
                                        
                                        // Temporarily deactivate mod for update
                                        Directory.Move(modPath, tempDisabledPath);
                                        modPath = tempDisabledPath;
                                        Logger.LogInfo($"Temporarily deactivated mod for update: {modPath}");
                                    }
                                    
                                    // Show update-specific options grid only in normal mode
                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        bool isPreviewOnlyMode = _selectedFiles.Count == 0;
                                        if (!isPreviewOnlyMode && _updateOptionsGrid != null)
                                            _updateOptionsGrid.Visibility = Visibility.Visible;
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Failed to read mod.json for update check", ex);
                        }
                    }
                    
                    // If not an update, create new folder with suffix
                    if (!isUpdate)
                    {
                        int counter = 1;
                        var basePath = modPath;
                        while (Directory.Exists($"{basePath}_{counter}"))
                        {
                            counter++;
                        }
                        modPath = $"{basePath}_{counter}";
                        Logger.LogInfo($"Installing new mod at: {modPath}");
                    }
                }
                else
                {
                    // New mod - add DISABLED_ prefix
                    modPath = Path.Combine(categoryPath, "DISABLED_" + cleanModName);
                    Logger.LogInfo($"Installing new mod (disabled by default) at: {modPath}");
                }

                // Create directory if it doesn't exist (for new installs)
                if (!Directory.Exists(modPath))
                {
                    Directory.CreateDirectory(modPath);
                }

                // Create backup if enabled and updating
                if (_createBackup && isUpdate && Directory.Exists(modPath))
                {
                    await CreateBackupAsync(modPath);
                }

                // Clean install - remove all files except backups
                if (_cleanInstall && Directory.Exists(modPath))
                {
                    _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractCleaningOldFiles");
                    CleanModFolder(modPath);
                }

                // Extract all files with directory structure using SharpSevenZip
                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractExtractingFiles");
                
                // Extract to temp directory first to handle root folder skipping
                var tempExtractPath = Path.Combine(Path.GetTempPath(), $"fxmm_extract_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempExtractPath);
                
                try
                {
                    // Extract with progress
                    var progress = new Progress<int>(percent =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            _extractProgressBar.IsIndeterminate = false;
                            _extractProgressBar.Value = percent;
                            _extractStatusText.Text = string.Format(SharedUtilities.GetTranslation(_lang, "ExtractingProgress"), percent, 100);
                        });
                    });
                    
                    // Try extraction with timeout
                    var extractTask = Task.Run(() => ArchiveHelper.ExtractToDirectory(_downloadedArchivePath!, tempExtractPath, progress));
                    if (!extractTask.Wait(TimeSpan.FromMinutes(5)))
                    {
                        Logger.LogWarning("Archive extraction timed out after 5 minutes, trying fallback method");
                        // Fallback to System.IO.Compression for problematic archives
                        if (_downloadedArchivePath!.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            System.IO.Compression.ZipFile.ExtractToDirectory(_downloadedArchivePath!, tempExtractPath, true);
                        }
                        else
                        {
                            throw new TimeoutException(SharedUtilities.GetTranslation(_lang, "ExtractionTimeout"));
                        }
                    }
                    
                    // Check if all files are in a single root folder
                    var topLevelItems = Directory.GetFileSystemEntries(tempExtractPath);
                    string sourceDir = tempExtractPath;
                    
                    if (topLevelItems.Length == 1 && Directory.Exists(topLevelItems[0]))
                    {
                        // All files are in a single root folder, skip it
                        sourceDir = topLevelItems[0];
                        Logger.LogInfo($"Skipping root folder: {Path.GetFileName(sourceDir)}");
                    }
                    
                    // Move files to final destination
                    foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(sourceDir, file);
                        var destPath = Path.Combine(modPath, relativePath);
                        
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }
                        
                        File.Move(file, destPath, true);
                    }
                    
                    // If mod was active before update, reactivate it
                    if (isUpdate && wasActive)
                    {
                        var finalActivePath = Path.Combine(categoryPath, cleanModName);
                        
                        // Check if active path is available
                        if (Directory.Exists(finalActivePath))
                        {
                            Logger.LogWarning($"Cannot reactivate mod: Active path already exists at {finalActivePath}");
                        }
                        else
                        {
                            Directory.Move(modPath, finalActivePath);
                            modPath = finalActivePath;
                            Logger.LogInfo($"Reactivated mod after update: {modPath}");
                        }
                    }
                }
                finally
                {
                    // Cleanup temp directory
                    try
                    {
                        if (Directory.Exists(tempExtractPath))
                        {
                            Directory.Delete(tempExtractPath, true);
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        Logger.LogWarning($"Failed to cleanup temp extraction directory: {cleanupEx.Message}");
                    }
                }

                // Create mod.json
                await CreateModJson(modPath);
                
                // Save mod path for preview download
                _installedModPath = modPath;
                
                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractComplete");
                _extractProgressBar.Value = 100;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to extract files", ex);
                throw;
            }
        }

        private async Task ExtractMultipleArchivesAsync(string category, List<(string filePath, string fileName)> downloadedFiles)
        {
            try
            {
                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractPreparing");
                _extractProgressBar.IsIndeterminate = true;

                // Get mod library path
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();

                var categoryPath = Path.Combine(modsPath, category);
                Directory.CreateDirectory(categoryPath);

                // New mods are disabled by default
                var modFolderName = "DISABLED_" + SanitizeFileName(_modNameTextBox.Text.Trim());
                var modPath = Path.Combine(categoryPath, modFolderName);

                // Create main mod directory
                Directory.CreateDirectory(modPath);

                // Extract each archive to its own subfolder
                int totalFiles = downloadedFiles.Count(f => IsArchiveFile(f.fileName));
                int currentFile = 0;

                foreach (var (filePath, fileName) in downloadedFiles)
                {
                    if (!IsArchiveFile(fileName))
                    {
                        // Copy non-archive files to main mod folder
                        var destPath = Path.Combine(modPath, fileName);
                        File.Copy(filePath, destPath, true);
                        continue;
                    }

                    currentFile++;
                    _extractStatusText.Text = string.Format(SharedUtilities.GetTranslation(_lang, "ExtractExtractingFiles") + " ({0}/{1})", currentFile, totalFiles);

                    // Create subfolder for this archive (without extension)
                    var archiveNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    var archiveSubfolder = Path.Combine(modPath, SanitizeFileName(archiveNameWithoutExt));
                    
                    // Extract to temp directory first to handle root folder skipping
                    var tempExtractPath = Path.Combine(Path.GetTempPath(), $"fxmm_multi_{Guid.NewGuid()}");
                    Directory.CreateDirectory(tempExtractPath);
                    
                    try
                    {
                        // Try extraction with timeout
                        var extractTask = Task.Run(() => ArchiveHelper.ExtractToDirectory(filePath, tempExtractPath));
                        if (!extractTask.Wait(TimeSpan.FromMinutes(5)))
                        {
                            Logger.LogWarning($"Archive extraction timed out for {fileName}, trying fallback method");
                            // Fallback to System.IO.Compression for problematic archives
                            if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                System.IO.Compression.ZipFile.ExtractToDirectory(filePath, tempExtractPath, true);
                            }
                            else
                            {
                                throw new TimeoutException(string.Format(SharedUtilities.GetTranslation(_lang, "ExtractionTimeoutFile"), fileName));
                            }
                        }
                        
                        // Check if all files are in a single root folder
                        var topLevelItems = Directory.GetFileSystemEntries(tempExtractPath);
                        string sourceDir = tempExtractPath;
                        
                        if (topLevelItems.Length == 1 && Directory.Exists(topLevelItems[0]))
                        {
                            // All files are in a single root folder, skip it
                            sourceDir = topLevelItems[0];
                            Logger.LogInfo($"Skipping root folder: {Path.GetFileName(sourceDir)}");
                        }
                        
                        // Move files to final destination
                        Directory.CreateDirectory(archiveSubfolder);
                        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                        {
                            var relativePath = Path.GetRelativePath(sourceDir, file);
                            var destPath = Path.Combine(archiveSubfolder, relativePath);
                            
                            var destDir = Path.GetDirectoryName(destPath);
                            if (!string.IsNullOrEmpty(destDir))
                            {
                                Directory.CreateDirectory(destDir);
                            }
                            
                            File.Move(file, destPath, true);
                        }
                    }
                    finally
                    {
                        // Cleanup temp directory
                        try
                        {
                            if (Directory.Exists(tempExtractPath))
                            {
                                Directory.Delete(tempExtractPath, true);
                            }
                        }
                        catch (Exception cleanupEx)
                        {
                            Logger.LogWarning($"Failed to cleanup temp extraction directory: {cleanupEx.Message}");
                        }
                    }
                }

                // Create mod.json in main mod folder
                await CreateModJson(modPath);
                
                // Save mod path for preview download
                _installedModPath = modPath;
                
                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractComplete");
                _extractProgressBar.Value = 100;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to extract multiple archives", ex);
                throw;
            }
        }

        private async Task InstallFiles(string sourceDir, string category)
        {
            try
            {
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();

                var categoryPath = Path.Combine(modsPath, category);
                Directory.CreateDirectory(categoryPath);

                // Use the sanitized mod name from the text box instead of _modName
                var cleanModName = SanitizeFileName(_modNameTextBox.Text.Trim());
                
                // New mods are disabled by default - add DISABLED_ prefix
                var modFolderName = "DISABLED_" + cleanModName;
                var modPath = Path.Combine(categoryPath, modFolderName);

                if (Directory.Exists(modPath))
                {
                    int counter = 1;
                    while (Directory.Exists($"{modPath}_{counter}"))
                    {
                        counter++;
                    }
                    modPath = $"{modPath}_{counter}";
                }

                Directory.CreateDirectory(modPath);

                // Copy all files
                foreach (var file in Directory.GetFiles(sourceDir))
                {
                    var destFile = Path.Combine(modPath, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }

                // Create mod.json
                await CreateModJson(modPath);

                // Save mod path for preview download and event firing
                _installedModPath = modPath;

                // Cleanup
                Directory.Delete(sourceDir, true);
            }
            catch (Exception ex)
            {
                Logger.LogError($"InstallFiles failed", ex);
                throw;
            }
        }

        private async Task CreateModJson(string modPath)
        {
            var modJsonPath = Path.Combine(modPath, "mod.json");
            
            // Use version from API (_sVersion)
            string version = _version ?? "";
            
            // Convert timestamp to date string
            string dateUpdated = "0000-00-00";
            if (_dateUpdatedTimestamp > 0)
            {
                try
                {
                    var date = DateTimeOffset.FromUnixTimeSeconds(_dateUpdatedTimestamp).DateTime;
                    dateUpdated = date.ToString("yyyy-MM-dd");
                }
                catch
                {
                    dateUpdated = "0000-00-00";
                }
            }
            
            var modJson = new
            {
                author = _authorName,
                url = _modProfileUrl ?? "https://",
                version = string.IsNullOrWhiteSpace(version) ? " " : version,
                dateChecked = DateTime.Now.ToString("yyyy-MM-dd"),
                dateUpdated = dateUpdated,
                isNSFW = _isNSFW,
                hotkeys = new object[] { }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(modJson, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await Services.FileAccessQueue.WriteAllTextAsync(modJsonPath, json);
        }
        
        private async Task<string> FetchVersionFromGameBanana(string url)
        {
            try
            {
                // Parse GameBanana URL to extract item type and ID
                var urlPattern = new System.Text.RegularExpressions.Regex(@"gamebanana\.com/(\w+)/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var match = urlPattern.Match(url);
                if (!match.Success)
                {
                    return "";
                }

                string itemType = match.Groups[1].Value; // e.g., "mods", "tools"
                string itemId = match.Groups[2].Value;   // e.g., "574763"

                // Capitalize first letter for API (Mod, Tool, etc.)
                itemType = char.ToUpper(itemType[0]) + itemType.Substring(1).TrimEnd('s');

                // Build API URL to get updates
                string apiUrl = $"https://gamebanana.com/apiv11/{itemType}/{itemId}/Updates?_nPage=1&_nPerpage=1&_csvProperties=_sVersion";

                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                var response = await httpClient.GetStringAsync(apiUrl);
                using var doc = System.Text.Json.JsonDocument.Parse(response);
                var root = doc.RootElement;

                // Get the first update's version
                if (root.ValueKind == System.Text.Json.JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var firstUpdate = root[0];
                    if (firstUpdate.TryGetProperty("_sVersion", out var versionProp))
                    {
                        return versionProp.GetString() ?? "";
                    }
                }

                return "";
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to fetch version from GameBanana API for URL: {url}", ex);
                return "";
            }
        }

        private bool IsArchiveFile(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".tar" || 
                   ext == ".gz" || ext == ".bz2" || ext == ".xz" || ext == ".tar.gz" || 
                   ext == ".tar.bz2" || ext == ".tar.xz";
        }

        private string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }

        private async Task ShowError(string message)
        {
            var dialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(_lang, "Error"),
                Content = message,
                CloseButtonText = SharedUtilities.GetTranslation(_lang, "OK"),
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async Task DownloadPreviewImagesAsync(string modPath)
        {
            try
            {
                if (_previewMedia?.Images == null) return;

                _downloadStatusText.Text = SharedUtilities.GetTranslation(_lang, "DownloadingPreviews");
                _downloadProgressBar.IsIndeterminate = true;

                int startIndex = 0;

                // Delete old preview images (all variants) unless combining
                if (!_combinePreviews)
                {
                    try
                    {
                        var oldPreviews = Directory.GetFiles(modPath)
                            .Where(f => 
                            {
                                var fileName = Path.GetFileName(f).ToLower();
                                return (fileName.StartsWith("preview") || IsMinitileFile(fileName)) && 
                                       IsImageFile(f);
                            });
                        
                        foreach (var oldPreview in oldPreviews)
                        {
                            try
                            {
                                File.Delete(oldPreview);
                                Logger.LogInfo($"Deleted old preview: {oldPreview}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Failed to delete old preview: {oldPreview}", ex);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to delete old previews", ex);
                    }
                }
                else
                {
                    // Find next available preview number
                    var existingPreviews = Directory.GetFiles(modPath)
                        .Where(f => 
                        {
                            var fileName = Path.GetFileName(f).ToLower();
                            return fileName.StartsWith("preview") && 
                                   IsImageFile(f);
                        })
                        .Select(f => Path.GetFileName(f))
                        .ToList();

                    // Extract numbers from existing preview files
                    var existingNumbers = new List<int>();
                    foreach (var preview in existingPreviews)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(preview, @"preview-?(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                        {
                            existingNumbers.Add(num);
                        }
                        else if (preview.ToLower() == "preview.jpg" || preview.ToLower() == "preview.png")
                        {
                            existingNumbers.Add(0);
                        }
                    }

                    startIndex = existingNumbers.Count > 0 ? existingNumbers.Max() + 1 : 0;
                    Logger.LogInfo($"Combining previews - starting from index {startIndex}");
                }

                var screenshots = _previewMedia.Images.Where(img => img.Type == "screenshot").ToList();
                if (screenshots.Count == 0) return;

                // Download all images in their original format
                _downloadProgressBar.IsIndeterminate = false;
                _downloadProgressBar.Value = 0;

                using var httpClient = new System.Net.Http.HttpClient();

                for (int i = 0; i < screenshots.Count; i++)
                {
                    var screenshot = screenshots[i];
                    var imageUrl = $"{screenshot.BaseUrl}/{screenshot.File}";
                    
                    // Get original file extension from URL or use jpg as default
                    var urlPath = new Uri(imageUrl).AbsolutePath;
                    var fileExtension = Path.GetExtension(urlPath);
                    if (string.IsNullOrEmpty(fileExtension))
                    {
                        fileExtension = ".jpg"; // Default if no extension in URL
                    }
                    
                    var fileName = $"preview{(startIndex + i + 1):D3}{fileExtension}";
                    var filePath = Path.Combine(modPath, fileName);

                    try
                    {
                        _downloadStatusText.Text = string.Format(SharedUtilities.GetTranslation(_lang, "DownloadingPreviews_Progress"), 
                            i + 1, screenshots.Count);
                        _downloadProgressBar.Value = (double)i / screenshots.Count * 100;

                        // Download image in original format
                        var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                        await File.WriteAllBytesAsync(filePath, imageBytes);
                        Logger.LogInfo($"Downloaded: {fileName}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to download preview image {i + 1}", ex);
                    }
                }

                _downloadProgressBar.Value = 100;
                _downloadStatusText.Text = SharedUtilities.GetTranslation(_lang, "DownloadComplete");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to download preview images", ex);
            }
        }

        private async Task OptimizeImagesInBackground(string modPath)
        {
            try
            {
                Logger.LogInfo($"Starting image optimization for downloaded mod in: {modPath}");
                
                // Get optimization context for GameBanana download
                var context = Services.ImageOptimizationService.GetOptimizationContext(
                    Services.OptimizationTrigger.GameBananaDownload);
                
                Logger.LogInfo($"Using AutoDownloadMode: {context.Mode}, InspectAndEdit: {context.InspectAndEditEnabled}, CropStrategy: {context.CropStrategy}");
                
                // Run optimization sequentially on UI thread to allow crop inspection panel
                // This blocks the dialog, which is intentional - user reviews each crop one by one
                await Services.ImageOptimizationService.ProcessModPreviewImagesAsync(modPath, context);
                
                Logger.LogInfo($"Completed image optimization for: {modPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to optimize images", ex);
            }
        }

        private async Task CreateBackupAsync(string modPath)
        {
            try
            {
                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractCreatingBackup");
                _extractProgressBar.IsIndeterminate = true;

                // Find next available backup number inside mod folder
                int backupNumber = 1;
                string backupPath;
                do
                {
                    backupPath = Path.Combine(modPath, $"fxmm-backup-{backupNumber:D3}.zip");
                    backupNumber++;
                } while (File.Exists(backupPath));

                // Create backup without compression (store only)
                var files = Directory.GetFiles(modPath, "*", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("fxmm-backup")) // Skip existing backups
                    .ToList();

                var filesToBackup = new Dictionary<string, string>();
                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(modPath, file);
                    filesToBackup[relativePath] = file;
                }

                if (filesToBackup.Count > 0)
                {
                    ArchiveHelper.CreateArchiveFromFiles(backupPath, filesToBackup);
                }

                Logger.LogInfo($"Created backup: {backupPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to create backup", ex);
                // Don't throw - continue with installation even if backup fails
            }
        }

        private void CleanModFolder(string modPath)
        {
            try
            {
                // Delete all files and folders except backups (and optionally previews)
                foreach (var file in Directory.GetFiles(modPath, "*", SearchOption.AllDirectories))
                {
                    // Skip backups
                    if (file.Contains("fxmm-backup"))
                        continue;

                    // Skip previews if Keep previews is enabled
                    if (_keepPreviews)
                    {
                        var fileName = Path.GetFileName(file).ToLower();
                        bool isPreview = (fileName.StartsWith("preview") || IsMinitileFile(fileName)) && 
                                       IsImageFile(file);
                        if (isPreview)
                            continue;
                    }

                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to delete file: {file}", ex);
                    }
                }

                // Delete empty directories
                foreach (var dir in Directory.GetDirectories(modPath, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                {
                    try
                    {
                        if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                        {
                            Directory.Delete(dir);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to delete directory: {dir}", ex);
                    }
                }

                Logger.LogInfo($"Cleaned mod folder: {modPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to clean mod folder", ex);
            }
        }
        
        /// <summary>
        /// Find existing mod path by mod ID extracted from URL (handles renamed folders)
        /// </summary>
        private string? FindExistingModPathByModId(string categoryPath, string? url)
        {
            if (string.IsNullOrEmpty(url) || !Directory.Exists(categoryPath))
                return null;
            
            // Extract mod ID from the URL we're looking for
            var targetModId = ExtractModIdFromUrl(url);
            if (string.IsNullOrEmpty(targetModId))
                return null;
            
            try
            {
                foreach (var modDir in Directory.GetDirectories(categoryPath))
                {
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (!File.Exists(modJsonPath))
                        continue;
                    
                    try
                    {
                        var modJsonContent = Services.FileAccessQueue.ReadAllText(modJsonPath);
                        var modJson = System.Text.Json.JsonDocument.Parse(modJsonContent);
                        if (modJson.RootElement.TryGetProperty("url", out var urlProp))
                        {
                            var existingUrl = urlProp.GetString();
                            var existingModId = ExtractModIdFromUrl(existingUrl);
                            
                            if (!string.IsNullOrEmpty(existingModId) && existingModId == targetModId)
                            {
                                Logger.LogInfo($"Found existing mod by ID {targetModId}: {modDir}");
                                return modDir;
                            }
                        }
                    }
                    catch
                    {
                        // Skip invalid mod.json files
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error searching for mod by ID", ex);
            }
            
            return null;
        }

        /// <summary>
        /// Extract mod ID from GameBanana URL
        /// </summary>
        private string? ExtractModIdFromUrl(string? url)
        {
            if (string.IsNullOrEmpty(url))
                return null;
            
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(url, @"gamebanana\.com/mods/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            catch { }
            
            return null;
        }

        /// <summary>
        /// Find existing mod path (with or without DISABLED_ prefix)
        /// </summary>
        private string? FindExistingModPath(string categoryPath, string modName)
        {
            // Check for exact match
            var exactPath = Path.Combine(categoryPath, modName);
            if (Directory.Exists(exactPath))
                return exactPath;
            
            // Check for DISABLED_ version
            var disabledPath = Path.Combine(categoryPath, "DISABLED_" + modName);
            if (Directory.Exists(disabledPath))
                return disabledPath;
            
            return null;
        }

        private void ModNameTextBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            // Replace forward slash with dash
            if (args.NewText.Contains('/'))
            {
                args.Cancel = true;
                sender.Text = args.NewText.Replace("/", "-");
                sender.SelectionStart = sender.Text.Length;
                return;
            }

            // Block other invalid filename characters (except /)
            var invalidChars = System.IO.Path.GetInvalidFileNameChars().Where(c => c != '/').ToArray();
            if (args.NewText.Any(c => invalidChars.Contains(c)))
            {
                args.Cancel = true;
                return;
            }

            // Validate and update button state
            ValidateInputs();
        }

        private void CategoryTextBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            // Replace forward slash with dash
            if (args.NewText.Contains('/'))
            {
                args.Cancel = true;
                sender.Text = args.NewText.Replace("/", "-");
                sender.SelectionStart = sender.Text.Length;
                return;
            }

            // Block other invalid filename characters (except /)
            var invalidChars = System.IO.Path.GetInvalidFileNameChars().Where(c => c != '/').ToArray();
            if (args.NewText.Any(c => invalidChars.Contains(c)))
            {
                args.Cancel = true;
                return;
            }

            // Validate and update button state
            ValidateInputs();
        }

        private void ValidateInputs()
        {
            // Check if mod name or category is a reserved Windows name
            var modName = _modNameTextBox.Text.Trim();
            var category = _categoryTextBox.Text.Trim();

            bool isValid = !string.IsNullOrWhiteSpace(category) &&
                          !IsReservedWindowsName(modName) && 
                          !IsReservedWindowsName(category);
            IsPrimaryButtonEnabled = isValid;
        }

        private bool IsReservedWindowsName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // Remove extension if present
            var nameWithoutExt = name.Split('.')[0].ToUpperInvariant();

            // Windows reserved names
            var reserved = new[] { "CON", "PRN", "AUX", "NUL",
                                   "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", 
                                   "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

            return reserved.Contains(nameWithoutExt);
        }

        private async Task OptimizeDownloadedPreviewsAsync(string modPath)
        {
            try
            {
                Logger.LogInfo($"Starting preview optimization for downloaded mod in: {modPath}");
                
                // Reset cancellation flag before starting new optimization
                // This ensures previous cancellations don't affect new downloads
                Services.ImageOptimizationService.ResetCancellation();
                
                // Get optimization context for GameBanana download
                var context = Services.ImageOptimizationService.GetOptimizationContext(
                    Services.OptimizationTrigger.GameBananaDownload);
                
                // GameBanana download never uses reoptimize - only process new files
                context.Reoptimize = false;
                
                Logger.LogInfo($"Using AutoDownloadMode: {context.Mode}, InspectAndEdit: {context.InspectAndEditEnabled}, CropStrategy: {context.CropStrategy}");
                
                // Optimize sequentially (with crop inspection if enabled)
                await Services.ImageOptimizationService.ProcessModPreviewImagesAsync(modPath, context);
                
                Logger.LogInfo($"Completed preview optimization for: {modPath}");
                
                // Refresh the mod tile in UI after optimization
                RefreshModTileInUI(modPath);
            }
            catch (OperationCanceledException)
            {
                // User clicked "Stop" in minitile selection or crop panel
                // Clean up downloaded preview files so next download attempt will work
                Logger.LogInfo($"User stopped optimization from UI panel for: {modPath}");
                try
                {
                    var previewFilesToDelete = Directory.GetFiles(modPath)
                        .Where(f =>
                        {
                            var fileName = Path.GetFileName(f).ToLower();
                            return (fileName.StartsWith("preview") || IsMinitileFile(fileName)) &&
                                   IsImageFile(f);
                        });
                    foreach (var file in previewFilesToDelete)
                    {
                        try { File.Delete(file); } catch { }
                    }
                    Logger.LogInfo($"Cleaned up preview files from cancelled download: {modPath}");
                }
                catch (Exception cleanupEx)
                {
                    Logger.LogError($"Failed to clean up preview files for {modPath}", cleanupEx);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to optimize downloaded preview images", ex);
            }
        }

        private void RefreshModTileInUI(string modPath)
        {
            try
            {
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                mainWindow?.CurrentModGridPage?.RefreshModTileImage(modPath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to refresh mod tile in UI: {modPath}", ex);
            }
        }
    }
}
