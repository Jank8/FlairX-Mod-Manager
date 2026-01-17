using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Dialogs
{
    public class StarterPackDialog : ContentDialog
    {
        // GameBanana Tool IDs for each game's Starter Pack
        private static readonly Dictionary<string, int> StarterPackToolIds = new()
        {
            { "ZZMI", 20322 },
            { "WWMI", 20485 }, // Wuthering Waves
            { "SRMI", 20487 }, // Honkai: Star Rail
            { "GIMI", 20486 }, // Genshin Impact
            { "HIMI", 20491 }, // Honkai Impact 3rd
        };

        private ProgressBar _progressBar;
        private TextBlock _statusText;
        private CheckBox _dontShowAgainCheckBox;
        private Button _cancelButton;
        private string _gameTag;
        private List<(string url, long size, string filename)> _downloadFiles = new();
        private long _totalSize;
        private Dictionary<string, string> _lang;
        private CancellationTokenSource? _cancellationTokenSource;

        public bool DontShowAgain => _dontShowAgainCheckBox?.IsChecked ?? false;
        
        /// <summary>
        /// Event raised when Starter Pack installation is complete and reload is needed
        /// </summary>
        public event EventHandler? InstallationComplete;

        public StarterPackDialog(string gameTag)
        {
            _gameTag = gameTag;
            _lang = SharedUtilities.LoadLanguageDictionary();

            Title = SharedUtilities.GetTranslation(_lang, "StarterPack_Title") ?? "Starter Pack Available";
            PrimaryButtonText = SharedUtilities.GetTranslation(_lang, "StarterPack_Download") ?? "Download & Install";
            CloseButtonText = SharedUtilities.GetTranslation(_lang, "StarterPack_NoThanks") ?? "No, thanks";
            DefaultButton = ContentDialogButton.Primary;

            var stackPanel = new StackPanel { Spacing = 12, MinWidth = 400 };

            // Description
            var descriptionText = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "StarterPack_Description") ?? 
                    "Would you like to download the Starter Pack? It contains a curated collection of essential mods to get you started.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stackPanel.Children.Add(descriptionText);

            // Status text
            _statusText = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "StarterPack_Ready") ?? "Ready to download.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.7,
                Margin = new Thickness(0, 8, 0, 4)
            };
            stackPanel.Children.Add(_statusText);

            // Progress bar
            _progressBar = new ProgressBar
            {
                Visibility = Visibility.Collapsed,
                Value = 0,
                Maximum = 100,
                Margin = new Thickness(0, 4, 0, 8)
            };
            stackPanel.Children.Add(_progressBar);

            // Cancel button (shown during download)
            _cancelButton = new Button
            {
                Content = SharedUtilities.GetTranslation(_lang, "Cancel") ?? "Cancel",
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _cancelButton.Click += CancelButton_Click;
            stackPanel.Children.Add(_cancelButton);

            // Don't show again checkbox
            _dontShowAgainCheckBox = new CheckBox
            {
                Content = SharedUtilities.GetTranslation(_lang, "StarterPack_DontShowAgain") ?? "Don't show this again for this game",
                Margin = new Thickness(0, 8, 0, 0)
            };
            stackPanel.Children.Add(_dontShowAgainCheckBox);

            Content = stackPanel;

            PrimaryButtonClick += OnPrimaryButtonClick;
            CloseButtonClick += OnCloseButtonClick;
        }

        /// <summary>
        /// Check if Starter Pack is available for the given game
        /// </summary>
        public static bool IsStarterPackAvailable(string gameTag)
        {
            return StarterPackToolIds.ContainsKey(gameTag);
        }

        /// <summary>
        /// Check if user has dismissed the Starter Pack dialog for this game
        /// </summary>
        public static bool IsStarterPackDismissed(string gameTag)
        {
            var dismissedGames = SettingsManager.Current.StarterPackDismissedGames ?? "";
            return dismissedGames.Contains(gameTag);
        }

        /// <summary>
        /// Mark Starter Pack as dismissed for this game
        /// </summary>
        public static void DismissStarterPack(string gameTag)
        {
            var dismissedGames = SettingsManager.Current.StarterPackDismissedGames ?? "";
            if (!dismissedGames.Contains(gameTag))
            {
                if (string.IsNullOrEmpty(dismissedGames))
                    SettingsManager.Current.StarterPackDismissedGames = gameTag;
                else
                    SettingsManager.Current.StarterPackDismissedGames = $"{dismissedGames},{gameTag}";
                SettingsManager.Save();
            }
        }

        /// <summary>
        /// Check if mods folder has no mods (ignoring "Other" category)
        /// </summary>
        public static bool IsModsFolderEmpty(string gameTag)
        {
            try
            {
                var modsPath = AppConstants.GameConfig.GetModsPath(gameTag);
                var fullPath = PathManager.GetAbsolutePath(modsPath);
                
                if (!Directory.Exists(fullPath))
                    return true;
                
                // Check all category directories (except "Other")
                var categoryDirs = Directory.GetDirectories(fullPath);
                foreach (var categoryDir in categoryDirs)
                {
                    var categoryName = Path.GetFileName(categoryDir);
                    
                    // Skip "Other" category
                    if (string.Equals(categoryName, "Other", StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    // Check if this category has any mod subdirectories
                    var modDirs = Directory.GetDirectories(categoryDir);
                    if (modDirs.Length > 0)
                        return false; // Found mods
                }
                
                return true; // No mods found (excluding Other)
            }
            catch
            {
                return true;
            }
        }

        private void OnCloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Cancel any ongoing download
            _cancellationTokenSource?.Cancel();
            
            // If checkbox is checked, dismiss for this game
            if (_dontShowAgainCheckBox.IsChecked == true)
            {
                DismissStarterPack(_gameTag);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _statusText.Text = SharedUtilities.GetTranslation(_lang, "StarterPack_Cancelled") ?? "Download cancelled.";
            _cancelButton.IsEnabled = false;
        }

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true; // Prevent dialog from closing

            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;
            var tempFiles = new List<string>();

            try
            {
                IsPrimaryButtonEnabled = false;
                IsSecondaryButtonEnabled = false;
                CloseButtonText = "";
                _dontShowAgainCheckBox.IsEnabled = false;
                _cancelButton.Visibility = Visibility.Visible;
                _cancelButton.IsEnabled = true;

                // Fetch download info from GameBanana API
                _statusText.Text = SharedUtilities.GetTranslation(_lang, "StarterPack_FetchingInfo") ?? "Fetching download info...";
                _progressBar.Visibility = Visibility.Visible;
                _progressBar.IsIndeterminate = true;

                var hasFiles = await FetchDownloadInfoAsync(cancellationToken);
                if (!hasFiles)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        ResetToInitialState();
                        return;
                    }
                    _statusText.Text = SharedUtilities.GetTranslation(_lang, "StarterPack_FetchFailed") ?? "Failed to fetch download info.";
                    _progressBar.Visibility = Visibility.Collapsed;
                    _cancelButton.Visibility = Visibility.Collapsed;
                    IsPrimaryButtonEnabled = true;
                    CloseButtonText = SharedUtilities.GetTranslation(_lang, "Close") ?? "Close";
                    _dontShowAgainCheckBox.IsEnabled = true;
                    return;
                }

                // Download all files
                var totalSizeMB = _totalSize / (1024.0 * 1024.0);
                _statusText.Text = string.Format(
                    SharedUtilities.GetTranslation(_lang, "StarterPack_Downloading") ?? "Downloading {0} files... ({1:F1} MB total)",
                    _downloadFiles.Count, totalSizeMB);
                _progressBar.IsIndeterminate = false;
                _progressBar.Value = 0;

                long totalDownloaded = 0;
                
                for (int i = 0; i < _downloadFiles.Count; i++)
                {
                    var (url, size, filename) = _downloadFiles[i];
                    
                    _statusText.Text = string.Format(
                        SharedUtilities.GetTranslation(_lang, "StarterPack_DownloadingFile") ?? "Downloading file {0}/{1}: {2}",
                        i + 1, _downloadFiles.Count, filename);
                    
                    var tempPath = Path.Combine(Path.GetTempPath(), $"starterpack_{_gameTag}_{i}_{Guid.NewGuid()}_{filename}");
                    tempFiles.Add(tempPath);
                    
                    var success = await DownloadFileAsync(url, tempPath, size, totalDownloaded, cancellationToken);
                    if (!success)
                    {
                        // Clean up partial downloads
                        foreach (var temp in tempFiles)
                        {
                            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                        }
                        
                        if (cancellationToken.IsCancellationRequested)
                        {
                            ResetToInitialState();
                            return;
                        }
                        _statusText.Text = SharedUtilities.GetTranslation(_lang, "StarterPack_DownloadFailed") ?? "Download failed.";
                        _progressBar.Visibility = Visibility.Collapsed;
                        _cancelButton.Visibility = Visibility.Collapsed;
                        IsPrimaryButtonEnabled = true;
                        CloseButtonText = SharedUtilities.GetTranslation(_lang, "Close") ?? "Close";
                        _dontShowAgainCheckBox.IsEnabled = true;
                        return;
                    }
                    
                    totalDownloaded += size;
                }

                // Disable cancel during extraction (can't cancel easily)
                _cancelButton.IsEnabled = false;

                // Extract all files to mods folder
                _statusText.Text = SharedUtilities.GetTranslation(_lang, "StarterPack_Extracting") ?? "Extracting...";
                _progressBar.IsIndeterminate = false;
                _progressBar.Value = 0;

                var modsPath = AppConstants.GameConfig.GetModsPath(_gameTag);
                var fullModsPath = PathManager.GetAbsolutePath(modsPath);

                for (int i = 0; i < tempFiles.Count; i++)
                {
                    var tempPath = tempFiles[i];
                    var filename = _downloadFiles[i].filename;
                    
                    _statusText.Text = string.Format(
                        SharedUtilities.GetTranslation(_lang, "StarterPack_ExtractingFile") ?? "Extracting file {0}/{1}: {2}",
                        i + 1, tempFiles.Count, filename);
                    
                    _progressBar.Value = (double)i / tempFiles.Count * 100;
                    
                    // Ensure mods directory exists
                    Directory.CreateDirectory(fullModsPath);

                    var extractProgress = new Progress<int>(percent =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            var overallProgress = ((double)i / tempFiles.Count * 100) + (percent / tempFiles.Count);
                            _progressBar.Value = Math.Min(overallProgress, 100);
                        });
                    });

                    await Task.Run(() => ArchiveHelper.ExtractToDirectory(tempPath, fullModsPath, extractProgress));

                    // Clean up temp file
                    try { File.Delete(tempPath); } catch { }
                }

                // Mark as dismissed so we don't show again
                DismissStarterPack(_gameTag);

                _statusText.Text = SharedUtilities.GetTranslation(_lang, "StarterPack_Complete") ?? "Starter Pack installed successfully!";
                _progressBar.Value = 100;
                _cancelButton.Visibility = Visibility.Collapsed;

                // Raise event to trigger reload
                InstallationComplete?.Invoke(this, EventArgs.Empty);

                // Close dialog after short delay
                await Task.Delay(1500);
                Hide();
            }
            catch (OperationCanceledException)
            {
                // Clean up partial downloads
                foreach (var temp in tempFiles)
                {
                    try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                }
                ResetToInitialState();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to download/install Starter Pack", ex);
                _statusText.Text = $"{SharedUtilities.GetTranslation(_lang, "StarterPack_Error") ?? "Error"}: {ex.Message}";
                _progressBar.Visibility = Visibility.Collapsed;
                _cancelButton.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = true;
                CloseButtonText = SharedUtilities.GetTranslation(_lang, "Close") ?? "Close";
                _dontShowAgainCheckBox.IsEnabled = true;
                
                // Clean up temp files
                foreach (var temp in tempFiles)
                {
                    try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                }
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void ResetToInitialState()
        {
            _statusText.Text = SharedUtilities.GetTranslation(_lang, "StarterPack_Ready") ?? "Ready to download.";
            _progressBar.Visibility = Visibility.Collapsed;
            _progressBar.Value = 0;
            _cancelButton.Visibility = Visibility.Collapsed;
            IsPrimaryButtonEnabled = true;
            CloseButtonText = SharedUtilities.GetTranslation(_lang, "StarterPack_NoThanks") ?? "No, thanks";
            _dontShowAgainCheckBox.IsEnabled = true;
        }

        private async Task<bool> FetchDownloadInfoAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!StarterPackToolIds.TryGetValue(_gameTag, out int toolId))
                    return false;

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "FlairX-Mod-Manager");

                var apiUrl = $"https://gamebanana.com/apiv11/Tool/{toolId}?_csvProperties=_aFiles";
                var response = await httpClient.GetStringAsync(apiUrl, cancellationToken);

                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (root.TryGetProperty("_aFiles", out var files) && files.GetArrayLength() > 0)
                {
                    _downloadFiles.Clear();
                    _totalSize = 0;
                    
                    foreach (var file in files.EnumerateArray())
                    {
                        var downloadUrl = file.GetProperty("_sDownloadUrl").GetString();
                        var fileSize = file.GetProperty("_nFilesize").GetInt64();
                        var filename = file.GetProperty("_sFile").GetString();

                        if (!string.IsNullOrEmpty(downloadUrl) && !string.IsNullOrEmpty(filename))
                        {
                            _downloadFiles.Add((downloadUrl, fileSize, filename));
                            _totalSize += fileSize;
                        }
                    }
                    
                    Logger.LogInfo($"Found {_downloadFiles.Count} files for Starter Pack, total size: {_totalSize / (1024.0 * 1024.0):F1} MB");
                    return _downloadFiles.Count > 0;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to fetch Starter Pack info from GameBanana", ex);
            }

            return false;
        }

        private async Task<bool> DownloadFileAsync(string url, string destinationPath, long fileSize, long totalDownloadedSoFar, CancellationToken cancellationToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "FlairX-Mod-Manager");

                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? fileSize;
                var buffer = new byte[8192];
                long bytesRead = 0;

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                int read;
                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                    bytesRead += read;

                    if (_totalSize > 0)
                    {
                        var totalProgress = totalDownloadedSoFar + bytesRead;
                        var percent = (int)((totalProgress * 100) / _totalSize);
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            _progressBar.Value = Math.Min(percent, 100);
                        });
                    }
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to download file from {url}", ex);
                return false;
            }
        }
    }
}
