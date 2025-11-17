using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
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
        private ListView _archiveContentsListView;
        private TextBox _categoryTextBox;
        private ProgressBar _progressBar;
        private TextBlock _statusText;
        private ObservableCollection<ArchiveFileItem> _archiveFiles = new();
        private string? _downloadedArchivePath;
        private string? _modProfileUrl;

        public class ArchiveFileItem : INotifyPropertyChanged
        {
            private bool _isSelected = true;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected != value)
                    {
                        _isSelected = value;
                        OnPropertyChanged(nameof(IsSelected));
                    }
                }
            }

            public string FileName { get; set; } = "";
            public string FullPath { get; set; } = "";
            public long FileSize { get; set; }
            public string FileSizeFormatted => FormatFileSize(FileSize);

            private static string FormatFileSize(long bytes)
            {
                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = bytes;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public GameBananaFileExtractionDialog(
            List<Models.GameBananaFileViewModel> selectedFiles,
            string modName,
            string gameTag,
            string? modProfileUrl = null,
            string? authorName = null,
            int modId = 0,
            long dateUpdatedTimestamp = 0)
        {
            _selectedFiles = selectedFiles;
            _modName = modName;
            _gameTag = gameTag;
            _modProfileUrl = modProfileUrl;
            _authorName = authorName ?? "unknown";
            _modId = modId;
            _dateUpdatedTimestamp = dateUpdatedTimestamp;

            Title = "Download and Install Mod";
            PrimaryButtonText = "Download and Extract";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;

            // Create content
            var stackPanel = new StackPanel { Spacing = 16 };

            // Info text
            var infoText = new TextBlock
            {
                Text = $"Downloading: {modName}\nSelected files: {selectedFiles.Count}",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stackPanel.Children.Add(infoText);

            // Category selection
            var categoryLabel = new TextBlock
            {
                Text = "Install to category:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 4)
            };
            stackPanel.Children.Add(categoryLabel);

            _categoryTextBox = new TextBox
            {
                PlaceholderText = "Enter category name (e.g., Characters, Weapons)",
                Text = "Characters"
            };
            stackPanel.Children.Add(_categoryTextBox);

            // Archive contents (will be populated after download)
            var archiveLabel = new TextBlock
            {
                Text = "Archive contents (select files to extract):",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 16, 0, 4),
                Visibility = Visibility.Collapsed
            };
            stackPanel.Children.Add(archiveLabel);

            _archiveContentsListView = new ListView
            {
                MaxHeight = 300,
                SelectionMode = ListViewSelectionMode.None,
                Visibility = Visibility.Collapsed,
                ItemsSource = _archiveFiles
            };
            stackPanel.Children.Add(_archiveContentsListView);

            // Progress
            _statusText = new TextBlock
            {
                Text = "Ready to download...",
                FontSize = 12,
                Opacity = 0.7,
                Margin = new Thickness(0, 16, 0, 4)
            };
            stackPanel.Children.Add(_statusText);

            _progressBar = new ProgressBar
            {
                IsIndeterminate = false,
                Value = 0,
                Maximum = 100
            };
            stackPanel.Children.Add(_progressBar);

            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 600
            };

            // Handle primary button click
            PrimaryButtonClick += OnPrimaryButtonClick;
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
                    _statusText.Text = $"Downloading {file.FileName} ({i + 1}/{_selectedFiles.Count})...";

                    var tempFilePath = Path.Combine(tempDir, file.FileName);
                    var progress = new Progress<double>(value =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            _progressBar.Value = value;
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

                    // If it's an archive, show contents
                    if (IsArchiveFile(file.FileName))
                    {
                        _downloadedArchivePath = tempFilePath;
                        await ShowArchiveContents(tempFilePath);
                        
                        // Change button text
                        PrimaryButtonText = "Extract Selected Files";
                        _statusText.Text = "Select files to extract from the archive";
                        _progressBar.Value = 0;
                        
                        // Re-enable buttons for extraction step
                        IsPrimaryButtonEnabled = true;
                        IsSecondaryButtonEnabled = true;
                        
                        // Remove the click handler and add new one for extraction
                        PrimaryButtonClick -= OnPrimaryButtonClick;
                        PrimaryButtonClick += OnExtractButtonClick;
                        return;
                    }
                }

                // If no archives, just move files
                await InstallFiles(tempDir, category);

                // Success
                _statusText.Text = "Installation complete!";
                _progressBar.Value = 100;
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

        private async void OnExtractButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;

            try
            {
                IsPrimaryButtonEnabled = false;
                IsSecondaryButtonEnabled = false;

                var category = _categoryTextBox.Text.Trim();
                var selectedFiles = _archiveFiles.Where(f => f.IsSelected).ToList();

                if (selectedFiles.Count == 0)
                {
                    await ShowError("Please select at least one file to extract.");
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                    return;
                }

                _statusText.Text = "Extracting files...";
                _progressBar.IsIndeterminate = true;

                // Extract selected files
                var modLibraryPath = SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");

                var categoryPath = Path.Combine(modLibraryPath, category);
                Directory.CreateDirectory(categoryPath);

                var modFolderName = SanitizeFileName(_modName);
                var modPath = Path.Combine(categoryPath, modFolderName);

                // If mod folder exists, create a new one with number suffix
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

                // Extract selected files
                using (var archive = ZipFile.OpenRead(_downloadedArchivePath!))
                {
                    foreach (var selectedFile in selectedFiles)
                    {
                        var entry = archive.Entries.FirstOrDefault(e => e.FullName == selectedFile.FullPath);
                        if (entry != null)
                        {
                            var destPath = Path.Combine(modPath, entry.Name);
                            entry.ExtractToFile(destPath, true);
                        }
                    }
                }

                // Create mod.json
                await CreateModJson(modPath);

                _statusText.Text = "Installation complete!";
                _progressBar.IsIndeterminate = false;
                _progressBar.Value = 100;
                await Task.Delay(1000);

                Hide();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to extract files", ex);
                await ShowError($"Extraction failed: {ex.Message}");
                IsPrimaryButtonEnabled = true;
                IsSecondaryButtonEnabled = true;
            }
        }

        private async Task ShowArchiveContents(string archivePath)
        {
            try
            {
                _archiveFiles.Clear();

                using (var archive = ZipFile.OpenRead(archivePath))
                {
                    foreach (var entry in archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)))
                    {
                        _archiveFiles.Add(new ArchiveFileItem
                        {
                            FileName = entry.Name,
                            FullPath = entry.FullName,
                            FileSize = entry.Length,
                            IsSelected = true
                        });
                    }
                }

                // Show the list
                _archiveContentsListView.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to read archive contents", ex);
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
            return ext == ".zip" || ext == ".rar" || ext == ".7z";
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
    }
}
