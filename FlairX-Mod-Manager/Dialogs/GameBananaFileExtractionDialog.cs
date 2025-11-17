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
        private GameBananaService.PreviewMedia? _previewMedia;
        private string? _installedModPath = null;
        private CheckBox? _cleanInstallCheckBox;
        private CheckBox? _createBackupCheckBox;

        public GameBananaFileExtractionDialog(
            List<Models.GameBananaFileViewModel> selectedFiles,
            string modName,
            string gameTag,
            string? modProfileUrl = null,
            string? authorName = null,
            int modId = 0,
            long dateUpdatedTimestamp = 0,
            string? categoryName = null,
            GameBananaService.PreviewMedia? previewMedia = null)
        {
            _selectedFiles = selectedFiles;
            _modName = modName;
            _gameTag = gameTag;
            _modProfileUrl = modProfileUrl;
            _authorName = authorName ?? "unknown";
            _modId = modId;
            _dateUpdatedTimestamp = dateUpdatedTimestamp;
            _previewMedia = previewMedia;

            Title = "Download and Install Mod";
            PrimaryButtonText = "Start";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;

            // Create content
            var stackPanel = new StackPanel { Spacing = 16 };

            // Mod Name
            var modNameLabel = new TextBlock
            {
                Text = "Mod Name:",
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
                Text = "Category:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stackPanel.Children.Add(categoryLabel);

            _categoryTextBox = new TextBox
            {
                PlaceholderText = "Enter category name",
                Text = categoryName ?? "Characters",
                Margin = new Thickness(0, 0, 0, 16)
            };
            stackPanel.Children.Add(_categoryTextBox);

            // Download previews checkbox
            var downloadPreviewsCheckBox = new CheckBox
            {
                Content = "Download previews",
                IsChecked = false,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(downloadPreviewsCheckBox, 
                "Download preview images from GameBanana and optimize them");
            downloadPreviewsCheckBox.Checked += (s, e) => _downloadPreviews = true;
            downloadPreviewsCheckBox.Unchecked += (s, e) => _downloadPreviews = false;
            stackPanel.Children.Add(downloadPreviewsCheckBox);

            // Clean install checkbox (visible only during updates)
            _cleanInstallCheckBox = new CheckBox
            {
                Content = "Clean install",
                IsChecked = false,
                Margin = new Thickness(0, 0, 0, 4),
                Visibility = Visibility.Collapsed // Will be shown if update is detected
            };
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(_cleanInstallCheckBox, 
                "Remove all existing files before installing (except backups)");
            _cleanInstallCheckBox.Checked += (s, e) => _cleanInstall = true;
            _cleanInstallCheckBox.Unchecked += (s, e) => _cleanInstall = false;
            stackPanel.Children.Add(_cleanInstallCheckBox);

            // Create backup checkbox (visible only during updates)
            _createBackupCheckBox = new CheckBox
            {
                Content = "Create backup",
                IsChecked = false,
                Margin = new Thickness(0, 0, 0, 16),
                Visibility = Visibility.Collapsed // Will be shown if update is detected
            };
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(_createBackupCheckBox, 
                "Create a backup of existing files before updating");
            _createBackupCheckBox.Checked += (s, e) => _createBackup = true;
            _createBackupCheckBox.Unchecked += (s, e) => _createBackup = false;
            stackPanel.Children.Add(_createBackupCheckBox);

            // Download Progress
            _downloadStatusText = new TextBlock
            {
                Text = "Download:",
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
                Text = "Extract:",
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
                var modLibraryPath = SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");

                var categoryPath = Path.Combine(modLibraryPath, category);
                if (!Directory.Exists(categoryPath))
                    return;

                var modFolderName = SanitizeFileName(_modName);
                var modPath = Path.Combine(categoryPath, modFolderName);

                // Check if mod exists and has same URL
                if (Directory.Exists(modPath))
                {
                    var modJsonPath = Path.Combine(modPath, "mod.json");
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
                                    // Show update-specific options
                                    if (_cleanInstallCheckBox != null)
                                        _cleanInstallCheckBox.Visibility = Visibility.Visible;
                                    if (_createBackupCheckBox != null)
                                        _createBackupCheckBox.Visibility = Visibility.Visible;
                                    
                                    Title = "Download and Update Mod";
                                    Logger.LogInfo($"Update detected for mod: {modPath}");
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
                    await ShowError("Please enter a category name.");
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                    return;
                }

                // Download files
                var tempDir = Path.Combine(Path.GetTempPath(), "FlairX_Downloads", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                for (int i = 0; i < _selectedFiles.Count; i++)
                {
                    var file = _selectedFiles[i];
                    _downloadStatusText.Text = $"Download: {file.FileName} ({i + 1}/{_selectedFiles.Count})";

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
                        await ShowError($"Failed to download {file.FileName}");
                        Directory.Delete(tempDir, true);
                        IsPrimaryButtonEnabled = true;
                        IsSecondaryButtonEnabled = true;
                        return;
                    }
                    
                    _downloadProgressBar.Value = 100;
                    
                    // Check if it's an archive
                    if (IsArchiveFile(file.FileName))
                    {
                        _downloadedArchivePath = tempFilePath;
                    }
                }

                // Extract archive if downloaded
                if (!string.IsNullOrEmpty(_downloadedArchivePath))
                {
                    await ExtractArchiveAsync(category);
                }
                else
                {
                    // If no archives, just move files
                    await InstallFiles(tempDir, category);
                }

                // Download preview images if enabled
                if (_downloadPreviews && _previewMedia?.Images != null && !string.IsNullOrEmpty(_installedModPath))
                {
                    await DownloadPreviewImagesAsync(_installedModPath);
                }

                // Success
                _downloadStatusText.Text = "Download: Complete";
                _extractStatusText.Text = "Extract: Complete";
                _downloadProgressBar.Value = 100;
                _extractProgressBar.Value = 100;
                await Task.Delay(1000);

                // Close dialog
                Hide();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to download and install mod", ex);
                await ShowError($"Installation failed: {ex.Message}");
                IsPrimaryButtonEnabled = true;
                IsSecondaryButtonEnabled = true;
            }
        }

        private async Task ExtractArchiveAsync(string category)
        {
            try
            {
                _extractStatusText.Text = "Extract: Preparing...";
                _extractProgressBar.IsIndeterminate = true;

                // Get mod library path
                var modLibraryPath = SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");

                var categoryPath = Path.Combine(modLibraryPath, category);
                Directory.CreateDirectory(categoryPath);

                // Use mod name from textbox
                var modFolderName = SanitizeFileName(_modNameTextBox.Text.Trim());
                var modPath = Path.Combine(categoryPath, modFolderName);

                // Check if this is an update (folder exists with same mod URL)
                bool isUpdate = false;
                if (Directory.Exists(modPath))
                {
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
                                    
                                    // Show update-specific options
                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        if (_cleanInstallCheckBox != null)
                                            _cleanInstallCheckBox.Visibility = Visibility.Visible;
                                        if (_createBackupCheckBox != null)
                                            _createBackupCheckBox.Visibility = Visibility.Visible;
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
                        while (Directory.Exists($"{modPath}_{counter}"))
                        {
                            counter++;
                        }
                        modPath = $"{modPath}_{counter}";
                        Logger.LogInfo($"Installing new mod at: {modPath}");
                    }
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
                    _extractStatusText.Text = "Extract: Cleaning old files...";
                    CleanModFolder(modPath);
                }

                // Extract all files with directory structure using SharpCompress
                _extractStatusText.Text = "Extract: Extracting files...";
                
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
                        _extractStatusText.Text = $"Extract: {current}/{total} files";
                    }
                }

                // Create mod.json
                await CreateModJson(modPath);
                
                // Auto-detect hotkeys in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Pages.HotkeyFinderPage.AutoDetectHotkeysForModStaticAsync(modPath);
                        Logger.LogInfo($"Auto-detected hotkeys for mod: {modPath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to auto-detect hotkeys for mod: {modPath}", ex);
                    }
                });
                
                // Save mod path for preview download
                _installedModPath = modPath;
                
                _extractStatusText.Text = "Extract: Complete";
                _extractProgressBar.Value = 100;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to extract files", ex);
                throw;
            }
        }

        private async Task InstallFiles(string sourceDir, string category)
        {
            var modLibraryPath = SettingsManager.GetCurrentModLibraryDirectory();
            if (string.IsNullOrEmpty(modLibraryPath))
                modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");

            var categoryPath = Path.Combine(modLibraryPath, category);
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
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async Task DownloadPreviewImagesAsync(string modPath)
        {
            try
            {
                if (_previewMedia?.Images == null) return;

                _downloadStatusText.Text = "Download: Downloading previews...";
                _downloadProgressBar.IsIndeterminate = true;

                // Delete old preview images (all variants)
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

                var screenshots = _previewMedia.Images.Where(img => img.Type == "screenshot").ToList();
                if (screenshots.Count == 0) return;

                int downloaded = 0;
                using var httpClient = new System.Net.Http.HttpClient();

                for (int i = 0; i < screenshots.Count; i++)
                {
                    var screenshot = screenshots[i];
                    var imageUrl = $"{screenshot.BaseUrl}/{screenshot.File}";
                    var fileName = $"preview{(i + 1):D3}.jpg";
                    var filePath = Path.Combine(modPath, fileName);

                    try
                    {
                        var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                        await File.WriteAllBytesAsync(filePath, imageBytes);
                        downloaded++;
                        
                        _downloadProgressBar.IsIndeterminate = false;
                        _downloadProgressBar.Value = (double)downloaded / screenshots.Count * 100;
                        _downloadStatusText.Text = $"Download: Previews {downloaded}/{screenshots.Count}";
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

                _downloadStatusText.Text = "Download: Complete";
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
                Logger.LogInfo($"Starting background image optimization for downloaded previews in: {modPath}");
                
                // Run the global optimize previews function
                await Pages.SettingsUserControl.OptimizePreviewsDirectAsync();
                
                Logger.LogInfo($"Completed background image optimization");
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
                _extractStatusText.Text = "Extract: Creating backup...";
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
                // Delete all files and folders except backups
                foreach (var file in Directory.GetFiles(modPath, "*", SearchOption.AllDirectories))
                {
                    if (!file.Contains("fxmm-backup"))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to delete file: {file}", ex);
                        }
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
    }
}
