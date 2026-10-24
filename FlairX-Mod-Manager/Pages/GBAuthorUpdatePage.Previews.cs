using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class GBAuthorUpdatePage
    {

        // Fetch Versions functionality






        // Fetch Versions functionality (kept for compatibility)
        // This now just calls the main FetchAllData functionality







        // Fetch All Previews functionality
        private CancellationTokenSource? _ctsAllPreviews;

        private async void FetchAllPreviewsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await FetchAllPreviewsButtonClickAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in FetchAllPreviewsButton_Click", ex);
                ResetAllPreviewsButtonToFetchState();
            }
        }

        private async Task FetchAllPreviewsButtonClickAsync()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            var mainLang = SharedUtilities.LoadLanguageDictionary();

            if (_isFetchingAllPreviews)
            {
                _ctsAllPreviews?.Cancel();
                _isFetchingAllPreviews = false;
                ResetAllPreviewsButtonToFetchState();
                if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "CancelledContent"));
                return;
            }

            var confirmDialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(lang, "PreviewModeDialogTitle"),
                Content = SharedUtilities.GetTranslation(lang, "PreviewModeDialogContent"),
                PrimaryButtonText = SharedUtilities.GetTranslation(lang, "PreviewModeReplace"),
                SecondaryButtonText = SharedUtilities.GetTranslation(lang, "PreviewModeMerge"),
                CloseButtonText = SharedUtilities.GetTranslation(mainLang, "Cancel"),
                XamlRoot = this.XamlRoot
            };
            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.None)
            {
                return;
            }

            bool combinePreviews = result == ContentDialogResult.Secondary;

            _ctsAllPreviews = new CancellationTokenSource();
            _isFetchingAllPreviews = true;
            
            // Reset ImageOptimizationService cancellation flag before starting
            Services.ImageOptimizationService.ResetCancellation();
            
            lock (_lockObject)
            {
                _success = 0; _fail = 0; _skip = 0;
                _skippedMods.Clear();
                _failedMods.Clear();
                _progressValue = 0;
                _totalMods = 0;
            }
            NotifyProgressChanged(); // This will update button states via UpdateButtonStates()
            await FetchPreviewsAsync(_ctsAllPreviews.Token, fetchAll: true, combinePreviews: combinePreviews);
            ResetAllPreviewsButtonToFetchState();
        }

        private void ResetAllPreviewsButtonToFetchState()
        {
            _isFetchingAllPreviews = false;
            NotifyProgressChanged(); // This will call UpdateButtonStates() via UpdateProgressBarUI()
        }

        // Fetch Missing Previews functionality
        private CancellationTokenSource? _ctsMissingPreviews;

        private async void FetchMissingPreviewsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await FetchMissingPreviewsButtonClickAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in FetchMissingPreviewsButton_Click", ex);
                ResetMissingPreviewsButtonToFetchState();
            }
        }

        private async Task FetchMissingPreviewsButtonClickAsync()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            var mainLang = SharedUtilities.LoadLanguageDictionary();

            if (_isFetchingMissingPreviews)
            {
                _ctsMissingPreviews?.Cancel();
                _isFetchingMissingPreviews = false;
                ResetMissingPreviewsButtonToFetchState();
                if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "CancelledContent"));
                return;
            }

            var confirmDialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(lang, "FetchMissingPreviewsButton"),
                Content = SharedUtilities.GetTranslation(lang, "ConfirmFetchMissingPreviews"),
                PrimaryButtonText = SharedUtilities.GetTranslation(mainLang, "Continue"),
                CloseButtonText = SharedUtilities.GetTranslation(mainLang, "Cancel"),
                XamlRoot = this.XamlRoot
            };
            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            _ctsMissingPreviews = new CancellationTokenSource();
            _isFetchingMissingPreviews = true;
            
            // Reset ImageOptimizationService cancellation flag before starting
            Services.ImageOptimizationService.ResetCancellation();
            
            lock (_lockObject)
            {
                _success = 0; _fail = 0; _skip = 0;
                _skippedMods.Clear();
                _failedMods.Clear();
                _progressValue = 0;
                _totalMods = 0;
            }
            NotifyProgressChanged(); // This will update button states via UpdateButtonStates()
            await FetchPreviewsAsync(_ctsMissingPreviews.Token, fetchAll: false, combinePreviews: false);
            ResetMissingPreviewsButtonToFetchState();
        }

        private void ResetMissingPreviewsButtonToFetchState()
        {
            _isFetchingMissingPreviews = false;
            NotifyProgressChanged(); // This will call UpdateButtonStates() via UpdateProgressBarUI()
        }

        private async Task FetchPreviewsAsync(CancellationToken token, bool fetchAll, bool combinePreviews)
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                string modLibraryPath = SharedUtilities.GetSafeXXMIModsPath();

                var allModDirs = new List<string>();
                foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
                {
                    if (Directory.Exists(categoryDir))
                    {
                        allModDirs.AddRange(Directory.GetDirectories(categoryDir));
                    }
                }

                _totalMods = allModDirs.Count;
                int processed = 0;

                // Process sequentially to avoid overwhelming the server and prevent multiple UI dialogs
                var semaphore = new SemaphoreSlim(1); // Changed from 3 to 1 to process mods one by one
                var tasks = allModDirs.Select(async dir =>
                {
                    await semaphore.WaitAsync(token);
                    try
                    {
                        if (token.IsCancellationRequested) return;

                        var modJsonPath = Path.Combine(dir, "mod.json");
                        var modFolderName = Path.GetFileName(dir);

                        if (!File.Exists(modJsonPath))
                        {
                            Interlocked.Increment(ref processed);
                            lock (_lockObject) { _progressValue = (double)processed / _totalMods; }
                            NotifyProgressChanged();
                            return;
                        }

                        var json = await Services.FileAccessQueue.ReadAllTextAsync(modJsonPath, token);
                        string? url = null;
                        
                        // Remove DISABLED_ prefix from folder name for display
                        var displayFolderName = modFolderName.StartsWith("DISABLED_") 
                            ? modFolderName.Substring("DISABLED_".Length) 
                            : modFolderName;
                        string modName = displayFolderName;

                        using (var doc = JsonDocument.Parse(json))
                        {
                            var root = doc.RootElement;

                            modName = root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString())
                                ? nameProp.GetString()!
                                : displayFolderName;

                            // Update current processing mod status
                            SafeSetCurrentProcessingMod(modName);
                            NotifyProgressChanged();

                            if (!root.TryGetProperty("url", out var urlProp) || urlProp.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(urlProp.GetString()) || !urlProp.GetString()!.Contains("gamebanana.com"))
                            {
                                SafeIncrementSkip();
                                SafeAddSkippedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "InvalidUrl")}");
                                Interlocked.Increment(ref processed);
                                lock (_lockObject) { _progressValue = (double)processed / _totalMods; }
                                SafeSetCurrentProcessingMod("");
                                NotifyProgressChanged();
                                return;
                            }

                            url = urlProp.GetString()!;
                        }

                        // Check if mod already has preview images
                        var existingPreviews = Directory.GetFiles(dir)
                            .Where(f =>
                            {
                                var fileName = Path.GetFileName(f).ToLower();
                                return fileName.StartsWith("preview") &&
                                       IsImageFile(f);
                            }).ToList();

                        // If not fetchAll mode, skip mods that already have previews
                        if (!fetchAll && existingPreviews.Count > 0)
                        {
                            SafeIncrementSkip();
                            SafeAddSkippedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "AlreadyHasPreviews")}");
                            Interlocked.Increment(ref processed);
                            lock (_lockObject) { _progressValue = (double)processed / _totalMods; }
                            SafeSetCurrentProcessingMod("");
                            NotifyProgressChanged();
                            return;
                        }

                        int startIndex = 0;

                        // If fetchAll mode, handle existing previews based on combinePreviews flag
                        if (fetchAll && existingPreviews.Count > 0)
                        {
                            if (!combinePreviews)
                            {
                                // Replace mode - delete existing previews AND minitile
                                var allPreviewFiles = Directory.GetFiles(dir)
                                    .Where(f =>
                                    {
                                        var fileName = Path.GetFileName(f).ToLower();
                                        return (fileName.StartsWith("preview") || IsMinitileFile(fileName)) &&
                                               IsImageFile(f);
                                    });
                                foreach (var preview in allPreviewFiles)
                                {
                                    try { File.Delete(preview); } catch { }
                                }
                            }
                            else
                            {
                                // Merge mode - find next available preview number
                                var existingNumbers = new List<int>();
                                foreach (var preview in existingPreviews)
                                {
                                    var fileName = Path.GetFileName(preview);
                                    var match = System.Text.RegularExpressions.Regex.Match(fileName, @"preview-?(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                    if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                                    {
                                        existingNumbers.Add(num);
                                    }
                                    else if (fileName.ToLower() == "preview.jpg" || fileName.ToLower() == "preview.png")
                                    {
                                        existingNumbers.Add(0);
                                    }
                                }
                                startIndex = existingNumbers.Count > 0 ? existingNumbers.Max() + 1 : 0;
                            }
                        }

                        try
                        {
                            var downloadedCount = await DownloadPreviewsFromApi(url, dir, token, startIndex);
                            
                            if (downloadedCount > 0)
                            {
                                SafeIncrementSuccess();
                                
                                // Check for cancellation before optimization
                                if (token.IsCancellationRequested)
                                {
                                    return;
                                }
                                
                                // Run optimization for downloaded previews
                                try
                                {
                                    var context = Services.ImageOptimizationService.GetOptimizationContext(
                                        Services.OptimizationTrigger.GameBananaDownload);
                                    
                                    // GameBanana download never uses reoptimize - only process new files
                                    context.Reoptimize = false;
                                    
                                    await Services.ImageOptimizationService.ProcessModPreviewImagesAsync(dir, context);
                                    Logger.LogInfo($"Optimized previews for: {modName}");
                                    
                                    // Refresh mod tile in UI
                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        try
                                        {
                                            var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                                            mainWindow?.CurrentModGridPage?.RefreshModTileImage(dir);
                                        }
                                        catch { }
                                    });
                                }
                                catch (OperationCanceledException)
                                {
                                    // User clicked "Stop" in minitile selection or crop panel
                                    // Cancel the entire download process
                                    Logger.LogInfo($"User stopped optimization from UI panel - cancelling download process");
                                    
                                    // Clean up downloaded preview files from current mod
                                    // so that "fetch missing" will process this mod again
                                    try
                                    {
                                        var previewFilesToDelete = Directory.GetFiles(dir)
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
                                        Logger.LogInfo($"Cleaned up preview files from cancelled mod: {modName}");
                                    }
                                    catch (Exception cleanupEx)
                                    {
                                        Logger.LogError($"Failed to clean up preview files for {modName}", cleanupEx);
                                    }
                                    
                                    if (_isFetchingAllPreviews)
                                    {
                                        _ctsAllPreviews?.Cancel();
                                    }
                                    if (_isFetchingMissingPreviews)
                                    {
                                        _ctsMissingPreviews?.Cancel();
                                    }
                                    return;
                                }
                                catch (Exception optEx)
                                {
                                    Logger.LogError($"Failed to optimize previews for {modName}", optEx);
                                }
                            }
                            else if (downloadedCount == 0)
                            {
                                // No previews available
                                SafeIncrementSkip();
                                SafeAddSkippedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "NoPreviewsAvailable")}");
                            }
                            else // downloadedCount < 0
                            {
                                // Error fetching previews
                                SafeIncrementFail();
                                SafeAddFailedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "PreviewFetchError")}");
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to fetch previews for {modName}", ex);
                            SafeIncrementFail();
                            SafeAddFailedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "PreviewFetchError")}");
                        }

                        Interlocked.Increment(ref processed);
                        lock (_lockObject) { _progressValue = (double)processed / _totalMods; }
                        NotifyProgressChanged();
                    }
                    finally
                    {
                        SafeSetCurrentProcessingMod("");
                        semaphore.Release();
                    }
                }).ToList();

                await Task.WhenAll(tasks);

                if (token.IsCancellationRequested)
                {
                    // Reset button states when cancelled
                    if (_isFetchingAllPreviews)
                    {
                        ResetAllPreviewsButtonToFetchState();
                    }
                    if (_isFetchingMissingPreviews)
                    {
                        ResetMissingPreviewsButtonToFetchState();
                    }
                    
                    // Show cancellation dialog
                    if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "CancelledContent"));
                    return;
                }

                string summary = string.Format(SharedUtilities.GetTranslation(lang, "TotalChecked"), _totalMods) + "\n" +
                                string.Format(SharedUtilities.GetTranslation(lang, "SuccessCount"), _success) + "\n" +
                                string.Format(SharedUtilities.GetTranslation(lang, "SkippedCount"), _skip);

                var allIssues = new List<string>();
                // Only show mods without previews available or with errors (not "already has previews")
                foreach (var skipped in _skippedMods)
                {
                    if (!skipped.Contains(SharedUtilities.GetTranslation(lang, "AlreadyHasPreviews")))
                    {
                        allIssues.Add(skipped);
                    }
                }
                allIssues.AddRange(_failedMods);

                if (allIssues.Count > 0)
                {
                    summary += "\n\n" + SharedUtilities.GetTranslation(lang, "ModsWithIssues") + "\n" + string.Join("\n", allIssues);
                }

                var textBlock = new TextBlock
                {
                    Text = summary,
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                    Width = 500
                };

                var scrollViewer = new ScrollViewer
                {
                    Content = textBlock,
                    VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Disabled,
                    MaxHeight = 400,
                    Padding = new Microsoft.UI.Xaml.Thickness(10)
                };

                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "PreviewsSummaryTitle"),
                    Content = scrollViewer,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                
                // Reload mods to update ModListManager lists and refresh thumbnails
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    Logger.LogInfo("Reloading mods after previews download");
                    await mainWindow.ReloadModsAsync();
                    Logger.LogInfo("Mods reloaded successfully");
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled - reset button states
                if (_isFetchingAllPreviews)
                {
                    ResetAllPreviewsButtonToFetchState();
                }
                if (_isFetchingMissingPreviews)
                {
                    ResetMissingPreviewsButtonToFetchState();
                }
                
                // Show cancellation dialog
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "CancelledContent"));
            }
            catch (Exception ex)
            {
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(ex.Message);
            }
        }

        private async Task<int> DownloadPreviewsFromApi(string url, string modPath, CancellationToken token, int startIndex = 0)
        {
            try
            {
                var match = _urlPattern.Match(url);
                if (!match.Success)
                {
                    Logger.LogError($"Failed to parse GameBanana URL: {url}");
                    return -1; // Return -1 to indicate error
                }

                string itemType = match.Groups[1].Value;
                string itemId = match.Groups[2].Value;
                itemType = char.ToUpper(itemType[0]) + itemType.Substring(1).TrimEnd('s');

                string apiUrl = $"https://gamebanana.com/apiv11/{itemType}/{itemId}?_csvProperties=_aPreviewMedia";

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                var response = await _httpClient.GetStringAsync(apiUrl, token);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                // API call succeeded - return 0 for no previews

                if (!root.TryGetProperty("_aPreviewMedia", out var previewMedia))
                {
                    return 0;
                }

                if (!previewMedia.TryGetProperty("_aImages", out var images) || images.ValueKind != JsonValueKind.Array)
                {
                    return 0;
                }

                var screenshots = new List<(string baseUrl, string file)>();
                foreach (var img in images.EnumerateArray())
                {
                    if (img.TryGetProperty("_sType", out var typeProp) && typeProp.GetString() == "screenshot")
                    {
                        if (img.TryGetProperty("_sBaseUrl", out var baseUrlProp) &&
                            img.TryGetProperty("_sFile", out var fileProp))
                        {
                            screenshots.Add((baseUrlProp.GetString()!, fileProp.GetString()!));
                        }
                    }
                }

                if (screenshots.Count == 0)
                {
                    return 0;
                }

                int downloaded = 0;
                for (int i = 0; i < screenshots.Count; i++)
                {
                    // Check for cancellation before downloading each image
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }
                    
                    var (baseUrl, file) = screenshots[i];
                    var imageUrl = $"{baseUrl}/{file}";

                    var urlPath = new Uri(imageUrl).AbsolutePath;
                    var fileExtension = Path.GetExtension(urlPath);
                    if (string.IsNullOrEmpty(fileExtension))
                    {
                        fileExtension = ".jpg";
                    }

                    var fileName = $"preview{(startIndex + i + 1):D3}{fileExtension}";
                    var filePath = Path.Combine(modPath, fileName);

                    try
                    {
                        var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl, token);
                        await File.WriteAllBytesAsync(filePath, imageBytes, token);
                        downloaded++;
                        Logger.LogInfo($"Downloaded preview: {fileName} for {Path.GetFileName(modPath)}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to download preview image {i + 1} for {Path.GetFileName(modPath)}", ex);
                    }
                }

                return downloaded;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to fetch previews from API for URL: {url}", ex);
                return -1; // Return -1 for any error
            }
        }
    }
}
