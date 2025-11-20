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
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

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
            bool isNSFW = false)
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

            // Load language
            _lang = SharedUtilities.LoadLanguageDictionary("GameBananaBrowser");

            Title = SharedUtilities.GetTranslation(_lang, "DownloadAndInstallMod");
            PrimaryButtonText = SharedUtilities.GetTranslation(_lang, "Start");
            CloseButtonText = SharedUtilities.GetTranslation(_lang, "Cancel");
            DefaultButton = ContentDialogButton.Primary;

            // Create content
            var stackPanel = new StackPanel { Spacing = 16 };

            // Mod Name
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
            stackPanel.Children.Add(_modNameTextBox);

            // Category selection
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
            stackPanel.Children.Add(_categoryTextBox);

            // Download previews checkbox (always visible)
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

            // Two-column layout for update-only checkboxes
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

            // Extract Progress
            _extractStatusText = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "Extract"),
                FontSize = 12,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stackPanel.Children.Add(_extractStatusText);

            _extractProgressBar = new ProgressBar
            {
                IsIndeterminate = false,
                Value = 0
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
            CheckIfUpdateAndShowOptions(categoryName ?? "Characters");

            // Handle primary button click
            PrimaryButtonClick += OnPrimaryButtonClick;
        }

        private void CheckIfUpdateAndShowOptions(string category)
        {
            try
            {
                // Get mod library path
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();

                var categoryPath = Path.Combine(modsPath, category);
                if (!Directory.Exists(categoryPath))
                    return;

                // Check if mod already exists (with or without DISABLED_ prefix)
                var cleanModName = SanitizeFileName(_modName);
                var existingModPath = FindExistingModPath(categoryPath, cleanModName);

                // Check if mod exists and has same URL
                if (!string.IsNullOrEmpty(existingModPath) && Directory.Exists(existingModPath))
                {
                    var modJsonPath = Path.Combine(existingModPath, "mod.json");
                    if (File.Exists(modJsonPath))
                    {
                        try
                        {
                            var modJsonContent = File.ReadAllText(modJsonPath);
                            var modJson = System.Text.Json.JsonDocument.Parse(modJsonContent);
                            if (modJson.RootElement.TryGetProperty("url", out var urlProp))
                            {
                                var existingUrl = urlProp.GetString();
                                // If URLs match, this is an update
                                if (existingUrl == _modProfileUrl)
                                {
                                    // Show update-specific options grid
                                    if (_updateOptionsGrid != null)
                                        _updateOptionsGrid.Visibility = Visibility.Visible;
                                    
                                    Title = SharedUtilities.GetTranslation(_lang, "DownloadAndUpdateMod");
                                    Logger.LogInfo($"Update detected for mod: {existingModPath}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Failed to check for update in constructor", ex);
                        }
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
            // Prevent dialog from closing immediately
            args.Cancel = true;

            try
            {
                IsPrimaryButtonEnabled = false;
                IsSecondaryButtonEnabled = false;

                var category = _categoryTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(category))
                {
                    await ShowError(SharedUtilities.GetTranslation(_lang, "EnterCategoryNameError"));
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                    return;
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
                        await ExtractArchiveAsync(category);
                    }
                    else
                    {
                        // Multiple files - extract each archive to its own subfolder
                        await ExtractMultipleArchivesAsync(category, downloadedFiles);
                    }
                }
                else
                {
                    // No archives, just move files
                    await InstallFiles(tempDir, category);
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

                // Close dialog
                Hide();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to download and install mod", ex);
                await ShowError(string.Format(SharedUtilities.GetTranslation(_lang, "InstallationFailed"), ex.Message));
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
                string modPath;
                
                if (!string.IsNullOrEmpty(existingModPath) && Directory.Exists(existingModPath))
                {
                    // Mod exists - check if it's an update
                    modPath = existingModPath;
                    var modJsonPath = Path.Combine(modPath, "mod.json");
                    if (File.Exists(modJsonPath))
                    {
                        try
                        {
                            var modJsonContent = await File.ReadAllTextAsync(modJsonPath);
                            var modJson = System.Text.Json.JsonDocument.Parse(modJsonContent);
                            if (modJson.RootElement.TryGetProperty("url", out var urlProp))
                            {
                                var existingUrl = urlProp.GetString();
                                // If URLs match, this is an update
                                if (existingUrl == _modProfileUrl)
                                {
                                    isUpdate = true;
                                    Logger.LogInfo($"Updating existing mod at: {modPath}");
                                    
                                    // Show update-specific options grid
                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        if (_updateOptionsGrid != null)
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

                // Extract all files with directory structure using SharpCompress
                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractExtractingFiles");
                
                // Use ReaderOptions to preserve encoding
                var readerOptions = new ReaderOptions
                {
                    ArchiveEncoding = new ArchiveEncoding
                    {
                        Default = System.Text.Encoding.UTF8
                    }
                };
                
                using (var archive = ArchiveFactory.Open(_downloadedArchivePath!, readerOptions))
                {
                    var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
                    int current = 0;
                    int total = entries.Count;
                    
                    // Check if all files are in a single root folder
                    string? commonRootFolder = null;
                    bool hasSingleRootFolder = true;
                    
                    foreach (var entry in entries)
                    {
                        if (string.IsNullOrEmpty(entry.Key)) continue;
                        
                        var parts = entry.Key.Split('/', '\\');
                        if (parts.Length > 1)
                        {
                            var rootFolder = parts[0];
                            if (commonRootFolder == null)
                            {
                                commonRootFolder = rootFolder;
                            }
                            else if (commonRootFolder != rootFolder)
                            {
                                hasSingleRootFolder = false;
                                break;
                            }
                        }
                        else
                        {
                            // File in root, no single folder
                            hasSingleRootFolder = false;
                            break;
                        }
                    }
                    
                    // If all files are in a single root folder, skip it
                    int skipLevels = (hasSingleRootFolder && !string.IsNullOrEmpty(commonRootFolder)) ? 1 : 0;
                    
                    foreach (var entry in entries)
                    {
                        if (string.IsNullOrEmpty(entry.Key)) continue;
                        
                        // Get relative path and skip root folder if needed
                        var pathParts = entry.Key.Split('/', '\\').ToList();
                        if (skipLevels > 0 && pathParts.Count > skipLevels)
                        {
                            pathParts.RemoveRange(0, skipLevels);
                        }
                        
                        var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), pathParts);
                        var destPath = Path.Combine(modPath, relativePath);
                        
                        // Create directory if needed
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }
                        
                        // Extract file
                        using (var entryStream = entry.OpenEntryStream())
                        using (var fileStream = File.Create(destPath))
                        {
                            entryStream.CopyTo(fileStream);
                        }
                        
                        current++;
                        _extractProgressBar.IsIndeterminate = false;
                        _extractProgressBar.Value = (double)current / total * 100;
                        _extractStatusText.Text = string.Format(SharedUtilities.GetTranslation(_lang, "ExtractingProgress"), current, total);
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

                var readerOptions = new ReaderOptions
                {
                    ArchiveEncoding = new ArchiveEncoding
                    {
                        Default = System.Text.Encoding.UTF8
                    }
                };

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
                    Directory.CreateDirectory(archiveSubfolder);

                    using (var archive = ArchiveFactory.Open(filePath, readerOptions))
                    {
                        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
                        
                        // Check if all files are in a single root folder
                        string? commonRootFolder = null;
                        bool hasSingleRootFolder = true;

                        foreach (var entry in entries)
                        {
                            if (string.IsNullOrEmpty(entry.Key)) continue;

                            var parts = entry.Key.Split('/', '\\');
                            if (parts.Length > 1)
                            {
                                var rootFolder = parts[0];
                                if (commonRootFolder == null)
                                {
                                    commonRootFolder = rootFolder;
                                }
                                else if (commonRootFolder != rootFolder)
                                {
                                    hasSingleRootFolder = false;
                                    break;
                                }
                            }
                            else
                            {
                                hasSingleRootFolder = false;
                                break;
                            }
                        }

                        // If all files are in a single root folder, skip it
                        int skipLevels = (hasSingleRootFolder && !string.IsNullOrEmpty(commonRootFolder)) ? 1 : 0;

                        foreach (var entry in entries)
                        {
                            if (string.IsNullOrEmpty(entry.Key)) continue;

                            // Get relative path and skip root folder if needed
                            var pathParts = entry.Key.Split('/', '\\').ToList();
                            if (skipLevels > 0 && pathParts.Count > skipLevels)
                            {
                                pathParts.RemoveRange(0, skipLevels);
                            }

                            var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), pathParts);
                            var destPath = Path.Combine(archiveSubfolder, relativePath);

                            // Create directory if needed
                            var destDir = Path.GetDirectoryName(destPath);
                            if (!string.IsNullOrEmpty(destDir))
                            {
                                Directory.CreateDirectory(destDir);
                            }

                            // Extract file
                            using (var entryStream = entry.OpenEntryStream())
                            using (var fileStream = File.Create(destPath))
                            {
                                entryStream.CopyTo(fileStream);
                            }
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
            var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();

            var categoryPath = Path.Combine(modsPath, category);
            Directory.CreateDirectory(categoryPath);

            var modFolderName = SanitizeFileName(_modName);
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

            // Cleanup
            Directory.Delete(sourceDir, true);
        }

        private async Task CreateModJson(string modPath)
        {
            var modJsonPath = Path.Combine(modPath, "mod.json");
            
            // Try to fetch version from GameBanana API
            string version = "";
            if (_modId > 0 && !string.IsNullOrEmpty(_modProfileUrl))
            {
                try
                {
                    version = await FetchVersionFromGameBanana(_modProfileUrl);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to fetch version for mod {_modName}", ex);
                }
            }
            
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
            await File.WriteAllTextAsync(modJsonPath, json);
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
                                return (fileName.StartsWith("preview") || fileName == "minitile.jpg") && 
                                       (f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                        f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                        f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
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
                                   (f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
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

                int downloaded = 0;
                using var httpClient = new System.Net.Http.HttpClient();

                for (int i = 0; i < screenshots.Count; i++)
                {
                    var screenshot = screenshots[i];
                    var imageUrl = $"{screenshot.BaseUrl}/{screenshot.File}";
                    var fileName = $"preview{(startIndex + i + 1):D3}.jpg";
                    var filePath = Path.Combine(modPath, fileName);

                    try
                    {
                        var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                        await File.WriteAllBytesAsync(filePath, imageBytes);
                        downloaded++;
                        
                        _downloadProgressBar.IsIndeterminate = false;
                        _downloadProgressBar.Value = (double)downloaded / screenshots.Count * 100;
                        _downloadStatusText.Text = string.Format(SharedUtilities.GetTranslation(_lang, "DownloadingPreviews_Progress"), 
                            downloaded, screenshots.Count);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to download preview image {i + 1}", ex);
                    }
                }

                // Run image optimization in background
                if (downloaded > 0)
                {
                    _ = Task.Run(() => OptimizeImagesInBackground(modPath));
                }

                _downloadStatusText.Text = SharedUtilities.GetTranslation(_lang, "DownloadComplete");
                _downloadProgressBar.Value = 100;
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
                Logger.LogInfo($"Starting background image optimization for downloaded mod in: {modPath}");
                
                // Optimize only this specific mod directory
                await Task.Run(() => Pages.SettingsUserControl.ProcessModPreviewImagesStatic(modPath));
                
                Logger.LogInfo($"Completed background image optimization for: {modPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to optimize images in background", ex);
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
                using (var archive = System.IO.Compression.ZipFile.Open(backupPath, System.IO.Compression.ZipArchiveMode.Create))
                {
                    var files = Directory.GetFiles(modPath, "*", SearchOption.AllDirectories)
                        .Where(f => !f.Contains("fxmm-backup")); // Skip existing backups

                    foreach (var file in files)
                    {
                        var relativePath = Path.GetRelativePath(modPath, file);
                        var entry = archive.CreateEntry(relativePath, System.IO.Compression.CompressionLevel.NoCompression);
                        
                        using var fileStream = File.OpenRead(file);
                        using var entryStream = entry.Open();
                        await fileStream.CopyToAsync(entryStream);
                    }
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
                        bool isPreview = (fileName.StartsWith("preview") || fileName == "minitile.jpg") && 
                                       (file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                        file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                        file.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
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
    }
}
