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
            // Add more games here as they get Starter Packs:
            // { "SRMI", 12345 },
            // { "GIMI", 12346 },
            // { "WWMI", 12347 },
            // { "HIMI", 12348 },
        };

        private ProgressBar _progressBar;
        private TextBlock _statusText;
        private CheckBox _dontShowAgainCheckBox;
        private Button _cancelButton;
        private string _gameTag;
        private string? _downloadUrl;
        private long _fileSize;
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
            string? tempPath = null;

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

                var downloadInfo = await FetchDownloadInfoAsync(cancellationToken);
                if (downloadInfo == null)
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

                _downloadUrl = downloadInfo.Value.url;
                _fileSize = downloadInfo.Value.size;

                // Download the file
                var fileSizeMB = _fileSize / (1024.0 * 1024.0);
                _statusText.Text = string.Format(
                    SharedUtilities.GetTranslation(_lang, "StarterPack_Downloading") ?? "Downloading... ({0:F1} MB)",
                    fileSizeMB);
                _progressBar.IsIndeterminate = false;
                _progressBar.Value = 0;

                tempPath = Path.Combine(Path.GetTempPath(), $"starterpack_{_gameTag}_{Guid.NewGuid()}.7z");
                
                var success = await DownloadFileAsync(_downloadUrl, tempPath, cancellationToken);
                if (!success)
                {
                    // Clean up partial download
                    try { if (tempPath != null && File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                    
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

                // Disable cancel during extraction (can't cancel easily)
                _cancelButton.IsEnabled = false;

                // Extract to mods folder
                _statusText.Text = SharedUtilities.GetTranslation(_lang, "StarterPack_Extracting") ?? "Extracting...";
                _progressBar.IsIndeterminate = false;
                _progressBar.Value = 0;

                var modsPath = AppConstants.GameConfig.GetModsPath(_gameTag);
                var fullModsPath = PathManager.GetAbsolutePath(modsPath);
                
                // Ensure mods directory exists
                Directory.CreateDirectory(fullModsPath);

                var extractProgress = new Progress<int>(percent =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _progressBar.Value = percent;
                    });
                });

                await Task.Run(() => ArchiveHelper.ExtractToDirectory(tempPath, fullModsPath, extractProgress));

                // Clean up temp file
                try { File.Delete(tempPath); } catch { }

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
                // Clean up partial download
                try { if (tempPath != null && File.Exists(tempPath)) File.Delete(tempPath); } catch { }
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

        private async Task<(string url, long size)?> FetchDownloadInfoAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!StarterPackToolIds.TryGetValue(_gameTag, out int toolId))
                    return null;

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "FlairX-Mod-Manager");

                var apiUrl = $"https://gamebanana.com/apiv11/Tool/{toolId}?_csvProperties=_aFiles";
                var response = await httpClient.GetStringAsync(apiUrl, cancellationToken);

                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (root.TryGetProperty("_aFiles", out var files) && files.GetArrayLength() > 0)
                {
                    var firstFile = files[0];
                    var downloadUrl = firstFile.GetProperty("_sDownloadUrl").GetString();
                    var fileSize = firstFile.GetProperty("_nFilesize").GetInt64();

                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        return (downloadUrl, fileSize);
                    }
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

            return null;
        }

        private async Task<bool> DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "FlairX-Mod-Manager");

                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? _fileSize;
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

                    if (totalBytes > 0)
                    {
                        var percent = (int)((bytesRead * 100) / totalBytes);
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            _progressBar.Value = percent;
                        });
                    }
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to download file from {url}", ex);
                return false;
            }
        }
    }
}
