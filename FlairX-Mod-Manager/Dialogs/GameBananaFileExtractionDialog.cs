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
    public partial class GameBananaFileExtractionDialog : ContentDialog
    {
        private List<Models.GameBananaFileViewModel> _selectedFiles;
        private string _modName;
        private string _gameTag;
        private string _authorName;
        private int _modId;
        private long _dateUpdatedTimestamp;
        private ComboBox _categoryComboBox = null!;
        private ProgressBar _progressBar = null!;
        private TextBlock _statusText = null!;
        private string? _downloadedArchivePath = null;
        private string? _currentTempDir = null; // For cleanup only
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
        private bool _isNewModInstall = true; // Track if this is a new installation (not an update)
        private CheckBox? _cleanInstallCheckBox;
        private CheckBox? _createBackupCheckBox;
        private CheckBox? _keepPreviewsCheckBox;
        private CheckBox? _combinePreviewsCheckBox;
        private Grid? _updateOptionsGrid;
        private System.Collections.Generic.Dictionary<string, string> _lang = new();
        private bool _isNSFW = false;
        private string? _version;
        private string? _existingModPathForPreviewsOnly = null;

        // Password panel UI (shown when encrypted archive is detected)
        private StackPanel _passwordPanel = null!;
        private PasswordBox _passwordBox = null!;
        private TextBlock _passwordErrorText = null!;
        private TaskCompletionSource<string?>? _passwordTcs;

        // Event fired when mod is successfully installed
        public event EventHandler<ModInstalledEventArgs>? ModInstalled;
        
        public class ModInstalledEventArgs : EventArgs
        {
            public string? ModProfileUrl { get; set; }
            public int ModId { get; set; }
            public string? ModPath { get; set; }
            public string? Category { get; set; }
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
            string? existingModPath = null,
            bool showFileSelection = false)
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
            var stackPanel = new StackPanel 
            { 
                Spacing = 16,
                Padding = new Thickness(0, 0, 8, 0) // Add right padding
            };

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
                    Margin = new Thickness(0, 0, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                _modNameTextBox.BeforeTextChanging += ModNameTextBox_BeforeTextChanging;
                _modNameTextBox.TextChanged += (s, e) => ValidateInputs();
                stackPanel.Children.Add(_modNameTextBox);

                // File Selection (show only if showFileSelection is true and multiple files available)
                if (showFileSelection && selectedFiles != null && selectedFiles.Count > 0)
                {
                    var filesLabel = new TextBlock
                    {
                        Text = SharedUtilities.GetTranslation(_lang, "SelectFilesToDownload") ?? "Select Files to Download",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    stackPanel.Children.Add(filesLabel);

                    // Border wrapping file list (same as GameBanana browser)
                    var fileBorder = new Border
                    {
                        Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                        BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(8),
                        Margin = new Thickness(0, 0, 0, 8)
                    };

                    // Create ScrollViewer with StackPanel for file checkboxes
                    var fileScrollViewer = new ScrollViewer
                    {
                        MaxHeight = 300,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    };

                    var fileStack = new StackPanel { Spacing = 0 };

                    foreach (var file in selectedFiles)
                    {
                        var fileCheckBox = new CheckBox
                        {
                            IsChecked = file.IsSelected,
                            Padding = new Thickness(12, 8, 12, 8),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch
                        };

                        // Create content grid with 3 rows (like GameBanana browser)
                        var fileGrid = new Grid();
                        fileGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                        fileGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                        fileGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

                        // Row 0: File name
                        var fileNameText = new TextBlock
                        {
                            Text = file.FileName,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                        };
                        Grid.SetRow(fileNameText, 0);
                        fileGrid.Children.Add(fileNameText);

                        // Row 1: Description (if exists)
                        if (!string.IsNullOrEmpty(file.Description))
                        {
                            var descText = new TextBlock
                            {
                                Text = file.Description,
                                TextWrapping = TextWrapping.Wrap,
                                Opacity = 0.7,
                                FontSize = 12,
                                Margin = new Thickness(0, 4, 0, 0)
                            };
                            Grid.SetRow(descText, 1);
                            fileGrid.Children.Add(descText);
                        }

                        // Row 2: Metadata (Size, Downloads, Date Added)
                        var metadataStack = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 16,
                            Margin = new Thickness(0, 4, 0, 0)
                        };

                        // Size
                        var sizeText = new TextBlock
                        {
                            FontSize = 12,
                            Opacity = 0.6
                        };
                        sizeText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = SharedUtilities.GetTranslation(_lang, "Size") });
                        sizeText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = " " });
                        sizeText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = file.FileSizeFormatted });
                        metadataStack.Children.Add(sizeText);

                        // Downloads
                        var downloadsText = new TextBlock
                        {
                            FontSize = 12,
                            Opacity = 0.6
                        };
                        downloadsText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = SharedUtilities.GetTranslation(_lang, "Downloads") });
                        downloadsText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = " " });
                        downloadsText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = file.DownloadCount.ToString("N0") });
                        metadataStack.Children.Add(downloadsText);

                        // Date Added
                        var dateText = new TextBlock
                        {
                            FontSize = 12,
                            Opacity = 0.6
                        };
                        dateText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = SharedUtilities.GetTranslation(_lang, "Added") });
                        dateText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = " " });
                        var dateAdded = DateTimeOffset.FromUnixTimeSeconds(file.DateAdded).LocalDateTime;
                        dateText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = dateAdded.ToString("d MMM yyyy") });
                        metadataStack.Children.Add(dateText);

                        Grid.SetRow(metadataStack, 2);
                        fileGrid.Children.Add(metadataStack);

                        fileCheckBox.Content = fileGrid;

                        // Bind IsChecked to file.IsSelected
                        fileCheckBox.Checked += (s, e) => 
                        { 
                            file.IsSelected = true;
                            ValidateInputs(); // Revalidate when file is selected
                        };
                        fileCheckBox.Unchecked += (s, e) => 
                        { 
                            file.IsSelected = false;
                            ValidateInputs(); // Revalidate when file is unselected
                        };

                        fileStack.Children.Add(fileCheckBox);
                    }

                    fileScrollViewer.Content = fileStack;
                    fileBorder.Child = fileScrollViewer;
                    stackPanel.Children.Add(fileBorder);
                }

                // Category selection (only show in normal mode)
                var categoryLabel = new TextBlock
                {
                    Text = SharedUtilities.GetTranslation(_lang, "Category"),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                stackPanel.Children.Add(categoryLabel);

                // Load existing categories from Mods folder
                var existingCategories = new List<string>();
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (!string.IsNullOrEmpty(modsPath) && Directory.Exists(modsPath))
                {
                    existingCategories = Directory.GetDirectories(modsPath)
                        .Select(Path.GetFileName)
                        .Where(name => !string.IsNullOrEmpty(name))
                        .Cast<string>() // Cast to non-nullable after filtering
                        .OrderBy(name => name)
                        .ToList();
                }

                var initialCategory = categoryName ?? "Characters";
                
                _categoryComboBox = new ComboBox
                {
                    PlaceholderText = SharedUtilities.GetTranslation(_lang, "EnterCategoryName"),
                    IsEditable = true,
                    IsTextSearchEnabled = true,
                    ItemsSource = existingCategories,
                    Margin = new Thickness(0, 0, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                
                // Set Text or SelectedItem after ItemsSource is set
                if (existingCategories.Contains(initialCategory))
                {
                    _categoryComboBox.SelectedItem = initialCategory;
                }
                else
                {
                    _categoryComboBox.Text = initialCategory;
                }
                
                // Validate after setting category
                _categoryComboBox.SelectionChanged += (s, e) => ValidateInputs();
                
                // Force visual update by triggering Loaded event
                _categoryComboBox.Loaded += (s, e) =>
                {
                    // Ensure text is visible after control is loaded
                    if (!string.IsNullOrEmpty(_categoryComboBox.Text))
                    {
                        var text = _categoryComboBox.Text;
                        _categoryComboBox.Text = "";
                        _categoryComboBox.Text = text;
                    }
                    ValidateInputs(); // Validate after control is loaded
                };
                
                _categoryComboBox.TextSubmitted += (s, e) => 
                {
                    // Sanitize category name when text is submitted
                    if (!string.IsNullOrEmpty(_categoryComboBox.Text))
                    {
                        _categoryComboBox.Text = SanitizeCategoryName(_categoryComboBox.Text);
                    }
                    ValidateInputs();
                };
                stackPanel.Children.Add(_categoryComboBox);
            }
            else
            {
                // Preview-only mode: create hidden controls with default values
                var sanitizedModName = SanitizeFileName(modName);
                _modNameTextBox = new TextBox { Text = sanitizedModName, Visibility = Visibility.Collapsed };
                _categoryComboBox = new ComboBox { Text = categoryName ?? "Characters", Visibility = Visibility.Collapsed };
                
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
                // In preview-only mode, always download previews and replace the old set.
                // "Combine previews" belongs to the full install/update options; offering it
                // here only made the dialog ambiguous.
                _downloadPreviews = true;
                _combinePreviews = false;
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

            // Password panel (hidden until encrypted archive is detected)
            _passwordBox = new PasswordBox
            {
                PlaceholderText = SharedUtilities.GetTranslation(_lang, "ArchivePassword_Placeholder"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 4)
            };

            _passwordErrorText = new TextBlock
            {
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
                FontSize = 12,
                Opacity = 0,
                Margin = new Thickness(0, 0, 0, 0)
            };

            _passwordPanel = new StackPanel
            {
                Spacing = 4,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var passwordLabel = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "ArchivePassword_Label"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            var passwordDesc = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "ArchivePassword_Description"),
                FontSize = 12,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            };
            _passwordPanel.Children.Add(passwordLabel);
            _passwordPanel.Children.Add(passwordDesc);
            _passwordPanel.Children.Add(_passwordBox);
            _passwordPanel.Children.Add(_passwordErrorText);
            stackPanel.Children.Add(_passwordPanel);

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
                Margin = new Thickness(0, 0, 0, 16),
                HorizontalAlignment = HorizontalAlignment.Stretch
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
                Visibility = isPreviewOnlyMode ? Visibility.Collapsed : Visibility.Visible,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            stackPanel.Children.Add(_extractProgressBar);

            // Keep old progress for compatibility (hidden)
            _statusText = new TextBlock { Visibility = Visibility.Collapsed };
            _progressBar = new ProgressBar { Visibility = Visibility.Collapsed };

            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 600,
                Width = 500,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            // Force TextBox to update its display
            if (_modNameTextBox != null && !string.IsNullOrEmpty(modName))
            {
                // Sanitize the mod name to ensure it's valid for folder names
                var sanitizedModName = SanitizeFileName(modName);
                _modNameTextBox.Text = sanitizedModName;
                _modNameTextBox.UpdateLayout();
                ValidateInputs(); // Enable Start button after setting text
            }

            // Check if this is an update and show appropriate checkboxes
            // Sanitize category name for folder lookup
            var normalizedCategory = SanitizeCategoryName(categoryName ?? "Characters");
            CheckIfUpdateAndShowOptions(normalizedCategory);

            // Validate inputs on initialization
            ValidateInputs();

            // Handle primary button click
            PrimaryButtonClick += OnPrimaryButtonClick;
            
            // Handle secondary button click (Download Previews Only)
            SecondaryButtonClick += OnSecondaryButtonClick;
        }
    }
}

