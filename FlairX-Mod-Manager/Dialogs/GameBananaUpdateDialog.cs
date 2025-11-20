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
using FlairX_Mod_Manager.Pages;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

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
        private string? _downloadedArchivePath;
        private string? _modFolderPath;
        private System.Collections.Generic.Dictionary<string, string> _lang = new();

        public class FileViewModel : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string FileName { get; set; } = "";
            public long FileSize { get; set; }
            public string? Description { get; set; }
            public string DownloadUrl { get; set; } = "";
            public int DownloadCount { get; set; }
            public DateTime DateAdded { get; set; }
            
            public string SizeLabel { get; set; } = "Size:";
            public string DownloadsLabel { get; set; } = "Downloads:";
            public string AddedLabel { get; set; } = "Added:";

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



        public GameBananaUpdateDialog(int modId, ModGridPage.ModTile modTile, string gameTag)
        {
            _modId = modId;
            _modTile = modTile;
            _gameTag = gameTag;

            // Load language
            _lang = SharedUtilities.LoadLanguageDictionary("GameBananaBrowser");

            Title = SharedUtilities.GetTranslation(_lang, "CheckForUpdates");
            PrimaryButtonText = SharedUtilities.GetTranslation(_lang, "DownloadSelected");
            CloseButtonText = SharedUtilities.GetTranslation(_lang, "Cancel");
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
                        DateAdded = DateTimeOffset.FromUnixTimeSeconds(file.DateAdded).DateTime,
                        SizeLabel = SharedUtilities.GetTranslation(_lang, "Size"),
                        DownloadsLabel = SharedUtilities.GetTranslation(_lang, "Downloads"),
                        AddedLabel = SharedUtilities.GetTranslation(_lang, "Added")
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
                    await ShowError(SharedUtilities.GetTranslation(_lang, "SelectAtLeastOneFile"));
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                    return;
                }

                // Find mod folder path
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath))
                {
                    await ShowError("XXMI Mods directory not found");
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                    return;
                }

                _modFolderPath = FindModFolderPath(modsPath, _modTile.Directory);

                if (string.IsNullOrEmpty(_modFolderPath) || !Directory.Exists(_modFolderPath))
                {
                    await ShowError(SharedUtilities.GetTranslation(_lang, "ModFolderNotFound"));
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
                    _statusText.Text = string.Format(SharedUtilities.GetTranslation(_lang, "DownloadingFile"), 
                        file.FileName, i + 1, selectedFiles.Count);
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
                        await ShowError(string.Format(SharedUtilities.GetTranslation(_lang, "FailedToDownload"), file.FileName));
                        Directory.Delete(tempDir, true);
                        IsPrimaryButtonEnabled = true;
                        IsSecondaryButtonEnabled = true;
                        return;
                    }

                    // Check if it's an archive
                    if (IsArchiveFile(file.FileName))
                    {
                        _downloadedArchivePath = tempFilePath;
                    }
                }

                // Extract archive or copy files
                if (!string.IsNullOrEmpty(_downloadedArchivePath))
                {
                    await ExtractArchiveToModFolder();
                }
                else
                {
                    await UpdateModFiles(tempDir);
                }

                // Success
                _statusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractComplete");
                _progressBar.Value = 100;
                await Task.Delay(1000);

                Hide();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to download update", ex);
                await ShowError(string.Format(SharedUtilities.GetTranslation(_lang, "UpdateFailed"), ex.Message));
                IsPrimaryButtonEnabled = true;
                IsSecondaryButtonEnabled = true;
            }
        }

        private async Task ExtractArchiveToModFolder()
        {
            try
            {
                _statusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractExtractingFiles");
                _progressBar.IsIndeterminate = true;

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
                        var destPath = Path.Combine(_modFolderPath!, relativePath);

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
                        _progressBar.IsIndeterminate = false;
                        _progressBar.Value = (double)current / total * 100;
                        _statusText.Text = string.Format(SharedUtilities.GetTranslation(_lang, "ExtractingProgress"), current, total);
                    }
                }

                // Update mod.json dateUpdated
                await UpdateModJsonDate();
                
                // Auto-detect hotkeys in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Pages.HotkeyFinderPage.AutoDetectHotkeysForModStaticAsync(_modFolderPath!);
                        Logger.LogInfo($"Auto-detected hotkeys for mod: {_modFolderPath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to auto-detect hotkeys for mod: {_modFolderPath}", ex);
                    }
                });

                _statusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractComplete");
                _progressBar.Value = 100;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to extract archive", ex);
                throw;
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

        private string? FindModFolderPath(string modsPath, string modDirectory)
        {
            try
            {
                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    // Check for exact match
                    var modPath = Path.Combine(categoryDir, modDirectory);
                    if (Directory.Exists(modPath))
                    {
                        return modPath;
                    }
                    
                    // Check for DISABLED_ version
                    var disabledPath = Path.Combine(categoryDir, "DISABLED_" + modDirectory);
                    if (Directory.Exists(disabledPath))
                    {
                        return disabledPath;
                    }
                    
                    // Check if modDirectory already has DISABLED_ prefix, try without it
                    if (modDirectory.StartsWith("DISABLED_"))
                    {
                        var cleanName = modDirectory.Substring("DISABLED_".Length);
                        var cleanPath = Path.Combine(categoryDir, cleanName);
                        if (Directory.Exists(cleanPath))
                        {
                            return cleanPath;
                        }
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
            return ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".tar" || 
                   ext == ".gz" || ext == ".bz2" || ext == ".xz" || ext == ".tar.gz" || 
                   ext == ".tar.bz2" || ext == ".tar.xz";
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
    }
}
