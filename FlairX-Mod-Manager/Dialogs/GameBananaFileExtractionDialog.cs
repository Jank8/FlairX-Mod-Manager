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
            string? modProfileUrl = null)
        {
            _selectedFiles = selectedFiles;
            _modName = modName;
            _gameTag = gameTag;
            _modProfileUrl = modProfileUrl;

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
            var modJson = new
            {
                author = "unknown",
                url = _modProfileUrl ?? "https://",
                version = "",
                dateChecked = DateTime.Now.ToString("yyyy-MM-dd"),
                dateUpdated = DateTime.Now.ToString("yyyy-MM-dd"),
                hotkeys = new object[] { }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(modJson, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(modJsonPath, json);
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
