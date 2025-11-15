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
using FlairX_Mod_Manager.Pages;

namespace FlairX_Mod_Manager.Dialogs
{
    public class GameBananaUpdateDialog : ContentDialog
    {
        private int _modId;
        private ModGridPage.ModTile _modTile;
        private string _gameTag;
        private ListView _filesListView;
        private ProgressBar _progressBar;
        private TextBlock _statusText;
        private ObservableCollection<FileViewModel> _files = new();
        private ObservableCollection<ArchiveFileItem> _archiveFiles = new();
        private ListView _archiveContentsListView;
        private string? _downloadedArchivePath;
        private string? _modFolderPath;

        public class FileViewModel : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string FileName { get; set; } = "";
            public long FileSize { get; set; }
            public string? Description { get; set; }
            public string DownloadUrl { get; set; } = "";
            public int DownloadCount { get; set; }
            public DateTime DateAdded { get; set; }

            public string FileSizeFormatted => FormatFileSize(FileSize);
            public string DownloadCountFormatted => FormatCount(DownloadCount);
            public string DateAddedFormatted => DateAdded.ToString("yyyy-MM-dd");
            public Visibility HasDescription => string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;

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

            private static string FormatCount(int count)
            {
                if (count >= 1000000)
                    return $"{count / 1000000.0:F1}M";
                if (count >= 1000)
                    return $"{count / 1000.0:F1}K";
                return count.ToString();
            }

            #pragma warning disable CS0067
            public event PropertyChangedEventHandler? PropertyChanged;
            #pragma warning restore CS0067
        }

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

        public GameBananaUpdateDialog(int modId, ModGridPage.ModTile modTile, string gameTag)
        {
            _modId = modId;
            _modTile = modTile;
            _gameTag = gameTag;

            Title = "Check for Updates";
            PrimaryButtonText = "Download Selected";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;
            IsPrimaryButtonEnabled = false;

            // Create content
            var stackPanel = new StackPanel { Spacing = 16 };

            // Mod info
            var modInfoText = new TextBlock
            {
                Text = $"Mod: {modTile.Name}\nChecking for available updates...",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stackPanel.Children.Add(modInfoText);

            // Files list
            var filesLabel = new TextBlock
            {
                Text = "Available files:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 4)
            };
            stackPanel.Children.Add(filesLabel);

            _filesListView = new ListView
            {
                MaxHeight = 300,
                SelectionMode = ListViewSelectionMode.Multiple,
                ItemsSource = _files
            };
            stackPanel.Children.Add(_filesListView);

            // Archive contents (hidden initially)
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
                Text = "Loading...",
                FontSize = 12,
                Opacity = 0.7,
                Margin = new Thickness(0, 16, 0, 4)
            };
            stackPanel.Children.Add(_statusText);

            _progressBar = new ProgressBar
            {
                IsIndeterminate = true,
                Value = 0,
                Maximum = 100
            };
            stackPanel.Children.Add(_progressBar);

            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 600
            };

            // Load files
            Loaded += async (s, e) => await LoadFilesAsync();

            // Handle primary button click
            PrimaryButtonClick += OnPrimaryButtonClick;
        }

        private async Task LoadFilesAsync()
        {
            try
            {
                var files = await GameBananaService.GetModFilesAsync(_modId);

                if (files == null || files.Count == 0)
                {
                    _statusText.Text = "No files available for this mod.";
                    _progressBar.Visibility = Visibility.Collapsed;
                    return;
                }

                foreach (var file in files)
                {
                    _files.Add(new FileViewModel
                    {
                        Id = file.Id,
                        FileName = file.FileName,
                        FileSize = file.FileSize,
                        Description = file.Description,
                        DownloadUrl = file.DownloadUrl,
                        DownloadCount = file.DownloadCount,
                        DateAdded = DateTimeOffset.FromUnixTimeSeconds(file.DateAdded).DateTime
                    });
                }

                // Auto-select the newest file
                if (_files.Count > 0)
                {
                    _filesListView.SelectedIndex = 0;
                    IsPrimaryButtonEnabled = true;
                }

                _statusText.Text = $"Found {_files.Count} file(s). Select files to download.";
                _progressBar.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load mod files", ex);
                _statusText.Text = "Failed to load files. Please try again.";
                _progressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;

            try
            {
                IsPrimaryButtonEnabled = false;
                IsSecondaryButtonEnabled = false;

                var selectedFiles = _filesListView.SelectedItems.Cast<FileViewModel>().ToList();

                if (selectedFiles.Count == 0)
                {
                    await ShowError("Please select at least one file to download.");
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                    return;
                }

                // Find mod folder path
                var modLibraryPath = SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");

                _modFolderPath = FindModFolderPath(modLibraryPath, _modTile.Directory);

                if (string.IsNullOrEmpty(_modFolderPath) || !Directory.Exists(_modFolderPath))
                {
                    await ShowError("Mod folder not found.");
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                    return;
                }

                // Download files
                var tempDir = Path.Combine(Path.GetTempPath(), "FlairX_Updates", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                for (int i = 0; i < selectedFiles.Count; i++)
                {
                    var file = selectedFiles[i];
                    _statusText.Text = $"Downloading {file.FileName} ({i + 1}/{selectedFiles.Count})...";
                    _progressBar.IsIndeterminate = false;
                    _progressBar.Value = 0;

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

                // If no archives, just copy files
                await UpdateModFiles(tempDir);

                // Success
                _statusText.Text = "Update complete!";
                _progressBar.Value = 100;
                await Task.Delay(1000);

                Hide();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to download update", ex);
                await ShowError($"Update failed: {ex.Message}");
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

                // Extract selected files to mod folder
                using (var archive = ZipFile.OpenRead(_downloadedArchivePath!))
                {
                    foreach (var selectedFile in selectedFiles)
                    {
                        var entry = archive.Entries.FirstOrDefault(e => e.FullName == selectedFile.FullPath);
                        if (entry != null)
                        {
                            var destPath = Path.Combine(_modFolderPath!, entry.Name);
                            entry.ExtractToFile(destPath, true);
                        }
                    }
                }

                // Update mod.json dateUpdated
                await UpdateModJsonDate();

                _statusText.Text = "Update complete!";
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

        private async Task UpdateModFiles(string sourceDir)
        {
            // Copy all files to mod folder
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(_modFolderPath!, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            // Update mod.json dateUpdated
            await UpdateModJsonDate();

            // Cleanup
            Directory.Delete(sourceDir, true);
        }

        private async Task UpdateModJsonDate()
        {
            try
            {
                var modJsonPath = Path.Combine(_modFolderPath!, "mod.json");
                if (File.Exists(modJsonPath))
                {
                    var jsonContent = await File.ReadAllTextAsync(modJsonPath);
                    var modData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);

                    if (modData != null)
                    {
                        modData["dateUpdated"] = DateTime.Now.ToString("yyyy-MM-dd");
                        var json = System.Text.Json.JsonSerializer.Serialize(modData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        await File.WriteAllTextAsync(modJsonPath, json);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to update mod.json date", ex);
            }
        }

        private string? FindModFolderPath(string modLibraryPath, string modDirectory)
        {
            try
            {
                foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
                {
                    var modPath = Path.Combine(categoryDir, modDirectory);
                    if (Directory.Exists(modPath))
                    {
                        return modPath;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private bool IsArchiveFile(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".zip" || ext == ".rar" || ext == ".7z";
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
