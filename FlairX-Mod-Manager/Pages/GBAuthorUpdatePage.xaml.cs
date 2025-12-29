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
    public sealed partial class GBAuthorUpdatePage : Page
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        
        // Regex pattern to parse GameBanana URLs
        private static readonly Regex _urlPattern = new Regex(@"gamebanana\.com/(\w+)/(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            var mainLang = SharedUtilities.LoadLanguageDictionary();
            
            // Update mode cards
            UpdateAuthorsLabel.Text = SharedUtilities.GetTranslation(lang, "UpdateAuthorsLabel");
            UpdateAuthorsDescription.Text = SharedUtilities.GetTranslation(lang, "UpdateAuthorsDescription");
            SmartUpdateLabel.Text = SharedUtilities.GetTranslation(lang, "SmartUpdateLabel");
            SmartUpdateDescription.Text = SharedUtilities.GetTranslation(lang, "SmartUpdateDescription");
            
            // Actions header
            ActionsHeader.Text = SharedUtilities.GetTranslation(lang, "ActionsHeader");
            
            // Fetch authors card
            FetchAuthorsTitle.Text = SharedUtilities.GetTranslation(lang, "FetchAuthorsButton");
            FetchAuthorsDescription.Text = SharedUtilities.GetTranslation(lang, "FetchAuthorsDescription");
            // Button text is now handled by UpdateButtonStates()
            
            // Fetch dates card
            FetchDatesTitle.Text = SharedUtilities.GetTranslation(lang, "FetchDatesButton");
            FetchDatesDescription.Text = SharedUtilities.GetTranslation(lang, "FetchDatesButton_Tooltip");
            // Button text is now handled by UpdateButtonStates()
            
            // Fetch versions card
            FetchVersionsTitle.Text = SharedUtilities.GetTranslation(lang, "FetchVersionsButton");
            FetchVersionsDescription.Text = SharedUtilities.GetTranslation(lang, "FetchVersionsButton_Tooltip");
            // Button text is now handled by UpdateButtonStates()
            
            // Fetch all previews card
            FetchAllPreviewsTitle.Text = SharedUtilities.GetTranslation(lang, "FetchAllPreviewsButton");
            FetchAllPreviewsDescription.Text = SharedUtilities.GetTranslation(lang, "FetchAllPreviewsButton_Tooltip");
            // Button text is now handled by UpdateButtonStates()
            
            // Fetch missing previews card
            FetchMissingPreviewsTitle.Text = SharedUtilities.GetTranslation(lang, "FetchMissingPreviewsButton");
            FetchMissingPreviewsDescription.Text = SharedUtilities.GetTranslation(lang, "FetchMissingPreviewsButton_Tooltip");
            // Button text is now handled by UpdateButtonStates()
        }
        
        private void UpdateToggleLabels()
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            var onText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_On");
            var offText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_Off");
            
            if (UpdateAuthorsToggleLabel != null && UpdateAuthorsSwitch != null)
                UpdateAuthorsToggleLabel.Text = UpdateAuthorsSwitch.IsOn ? onText : offText;
            if (SmartUpdateToggleLabel != null && SmartUpdateSwitch != null)
                SmartUpdateToggleLabel.Text = SmartUpdateSwitch.IsOn ? onText : offText;
        }

        // Thread-safe progress reporting
        private static readonly object _lockObject = new();
        private static event Action? ProgressChanged;

        private static void NotifyProgressChanged()
        {
            lock (_lockObject)
            {
                ProgressChanged?.Invoke();
            }
        }

        public GBAuthorUpdatePage()
        {
            this.InitializeComponent();
            UpdateTexts();
            UpdateToggleLabels();
            ProgressChanged += OnProgressChanged;
            UpdateAuthorsSwitch.Toggled += UpdateAuthorsSwitch_Toggled;
            SmartUpdateSwitch.Toggled += SmartUpdateSwitch_Toggled;
        }

        ~GBAuthorUpdatePage()
        {
            ProgressChanged -= OnProgressChanged;
        }

        private void OnProgressChanged()
        {
            DispatcherQueue.TryEnqueue(UpdateProgressBarUI);
        }

        // Thread-safe static fields with proper locking
        private static volatile bool _isUpdatingAuthors = false;
        private static volatile bool _isFetchingDates = false;
        private static volatile bool _isFetchingVersions = false;
        private static volatile bool _isFetchingAllPreviews = false;
        private static volatile bool _isFetchingMissingPreviews = false;
        private static int _success = 0, _fail = 0, _skip = 0;
        private static List<string> _skippedMods = new();
        private static List<string> _failedMods = new();
        private static string _currentProcessingMod = "";
        private static int _totalMods = 0;
        private static double _progressValue = 0;
        
        public static bool IsUpdatingAuthors 
        { 
            get { lock (_lockObject) { return _isUpdatingAuthors; } }
            private set { lock (_lockObject) { _isUpdatingAuthors = value; } }
        }
        
        public static double ProgressValue 
        { 
            get { lock (_lockObject) { return _progressValue; } }
            private set { lock (_lockObject) { _progressValue = value; } }
        }
        
        private static void SafeIncrementSuccess()
        {
            lock (_lockObject) { _success++; }
        }
        
        private static void SafeIncrementFail()
        {
            lock (_lockObject) { _fail++; }
        }
        
        private static void SafeIncrementSkip()
        {
            lock (_lockObject) { _skip++; }
        }
        
        private static void SafeAddSkippedMod(string mod)
        {
            lock (_lockObject) { _skippedMods.Add(mod); }
        }
        
        private static void SafeAddFailedMod(string mod)
        {
            lock (_lockObject) { _failedMods.Add(mod); }
        }
        
        private static void SafeSetCurrentProcessingMod(string modName)
        {
            lock (_lockObject)
            {
                _currentProcessingMod = modName;
            }
        }
        private CancellationTokenSource? _cts;

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await UpdateButtonClickAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in UpdateButton_Click", ex);
                // Ensure proper cleanup on error
                ResetButtonToUpdateState();
            }
        }
        
        private async Task UpdateButtonClickAsync()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            var mainLang = SharedUtilities.LoadLanguageDictionary();
            
            if (_isUpdatingAuthors)
            {
                _cts?.Cancel();
                _isUpdatingAuthors = false;
                NotifyProgressChanged();
                ResetButtonToUpdateState();
                // Add immediate dialog after clicking Cancel
                var cancelDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "CancelledTitle"),
                    Content = SharedUtilities.GetTranslation(lang, "CancelledContent"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await cancelDialog.ShowAsync();
                return;
            }
            
            // Add confirmation dialog
            var confirmDialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(lang, "FetchAuthorsButton"),
                Content = SharedUtilities.GetTranslation(lang, "ConfirmFetchAuthors"),
                PrimaryButtonText = SharedUtilities.GetTranslation(mainLang, "Continue"),
                CloseButtonText = SharedUtilities.GetTranslation(mainLang, "Cancel"),
                XamlRoot = this.XamlRoot
            };
            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }
            _cts = new CancellationTokenSource();
            _isUpdatingAuthors = true;
            lock (_lockObject)
            {
                _success = 0; _fail = 0; _skip = 0;
                _skippedMods.Clear();
                _failedMods.Clear();
                _progressValue = 0;
                _totalMods = 0;
            }
            NotifyProgressChanged(); // This will update button states via UpdateButtonStates()
            await UpdateAuthorsAsync(_cts.Token);
            // Final cleanup - ensure button is reset to update state
            ResetButtonToUpdateState();
        }

        private void ResetButtonToUpdateState()
        {
            _isUpdatingAuthors = false;
            NotifyProgressChanged(); // This will call UpdateButtonStates() via UpdateProgressBarUI()
        }

        private async Task UpdateAuthorsAsync(CancellationToken token)
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                string modLibraryPath = SharedUtilities.GetSafeXXMIModsPath();
                
                // Get all mod directories from all categories
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
                
                // Process in parallel with max 5 concurrent requests
                var semaphore = new SemaphoreSlim(5);
                var isSmartUpdate = IsSmartUpdate;
                var isFullUpdate = IsFullUpdate;
                
                var tasks = allModDirs.Select(async dir =>
                {
                    await semaphore.WaitAsync(token);
                    try
                    {
                        if (token.IsCancellationRequested) return;
                        
                        var modJsonPath = Path.Combine(dir, "mod.json");
                        var modFolderName = Path.GetFileName(dir);
                        
                        // Skip directories without mod.json
                        if (!File.Exists(modJsonPath)) 
                        { 
                            Interlocked.Increment(ref processed);
                            lock (_lockObject) { _progressValue = (double)processed / _totalMods; } 
                            NotifyProgressChanged(); 
                            return; 
                        }
                        
                        // Read mod.json through queue
                        var json = await Services.FileAccessQueue.ReadAllTextAsync(modJsonPath, token);
                        string? url = null;
                        string modName = modFolderName;
                        string currentAuthor = string.Empty;
                        bool shouldUpdate = false;
                        
                        using (var doc = JsonDocument.Parse(json))
                        {
                            var root = doc.RootElement;
                            
                            // Get mod name from mod.json, fallback to folder name
                            modName = root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString()) 
                                ? nameProp.GetString()! 
                                : modFolderName;
                            
                            // Remove DISABLED_ prefix from folder name for display
                            var displayFolderName = modFolderName.StartsWith("DISABLED_") 
                                ? modFolderName.Substring("DISABLED_".Length) 
                                : modFolderName;
                            
                            // Use clean name for display
                            var displayName = root.TryGetProperty("name", out var displayNameProp) && !string.IsNullOrWhiteSpace(displayNameProp.GetString()) 
                                ? displayNameProp.GetString()! 
                                : displayFolderName;
                            
                            // Update current processing mod status
                            SafeSetCurrentProcessingMod(displayName);
                            NotifyProgressChanged();
                            
                            if (!root.TryGetProperty("url", out var urlProp) || urlProp.ValueKind != JsonValueKind.String || 
                                string.IsNullOrWhiteSpace(urlProp.GetString()) || !urlProp.GetString()!.Contains("gamebanana.com")) 
                            { 
                                SafeIncrementSkip(); 
                                SafeAddSkippedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "InvalidUrl")}"); 
                                Interlocked.Increment(ref processed);
                                lock (_lockObject) { _progressValue = (double)processed / _totalMods; } 
                                NotifyProgressChanged(); 
                                return; 
                            }
                            
                            currentAuthor = root.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? string.Empty : string.Empty;
                            shouldUpdate = string.IsNullOrWhiteSpace(currentAuthor) || currentAuthor.Equals("unknown", StringComparison.OrdinalIgnoreCase);
                            url = urlProp.GetString()!;
                        }
                        
                        // For smart update, skip mods that already have known authors
                        if (isSmartUpdate && !shouldUpdate)
                        {
                            SafeIncrementSkip();
                            SafeAddSkippedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "AlreadyHasAuthor")} ({currentAuthor})");
                            Interlocked.Increment(ref processed);
                            lock (_lockObject) { _progressValue = (double)processed / _totalMods; }
                            NotifyProgressChanged();
                            return;
                        }
                        
                        try
                        {
                            var author = await FetchAuthorFromApi(url, token);
                            if (!string.IsNullOrWhiteSpace(author))
                            {
                                bool needsUpdate = false;
                                if (isFullUpdate && !author.Equals(currentAuthor, StringComparison.Ordinal))
                                {
                                    needsUpdate = true;
                                }
                                else if (isSmartUpdate && shouldUpdate && !string.IsNullOrWhiteSpace(author) && !author.Equals(currentAuthor, StringComparison.Ordinal))
                                {
                                    needsUpdate = true;
                                }
                                
                                if (needsUpdate)
                                {
                                    if (!SecurityValidator.IsValidModDirectoryName(modFolderName))
                                    {
                                        SafeIncrementSkip();
                                        SafeAddSkippedMod($"{modName}: Invalid directory name");
                                        Interlocked.Increment(ref processed);
                                        lock (_lockObject) { _progressValue = (double)processed / _totalMods; }
                                        NotifyProgressChanged();
                                        return;
                                    }
                                    
                                    // Atomic read-modify-write operation
                                    await Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                                    {
                                        var currentJson = await File.ReadAllTextAsync(modJsonPath, token);
                                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(currentJson) ?? new();
                                        dict["author"] = author;
                                        await File.WriteAllTextAsync(modJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }), token);
                                    }, token);
                                    lock (_lockObject) { _success++; }
                                }
                            }
                            else
                            {
                                SafeIncrementSkip();
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to fetch author for {modName}", ex);
                            SafeIncrementFail();
                            SafeAddFailedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "AuthorFetchError")}");
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
                    ResetButtonToUpdateState();
                    var cancelDialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "CancelledTitle"),
                        Content = SharedUtilities.GetTranslation(lang, "CancelledContent"),
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await cancelDialog.ShowAsync();
                    return;
                }
                
                // Successful completion - reset button state and show summary
                ResetButtonToUpdateState();
                string summary = string.Format(SharedUtilities.GetTranslation(lang, "TotalChecked"), _totalMods) + "\n" +
                                string.Format(SharedUtilities.GetTranslation(lang, "SuccessCount"), _success) + "\n" +
                                string.Format(SharedUtilities.GetTranslation(lang, "SkippedCount"), _skip);
                
                // For smart update, only show skipped items with errors/issues (not "already has author")
                // For full update, show all skipped items
                List<string> skippedToShow = new List<string>();
                if (IsSmartUpdate)
                {
                    // Smart update: only show skipped items that are NOT "already has author"
                    string alreadyHasAuthorText = SharedUtilities.GetTranslation(lang, "AlreadyHasAuthor");
                    foreach (var skipped in _skippedMods)
                    {
                        if (!skipped.Contains(alreadyHasAuthorText))
                        {
                            skippedToShow.Add(skipped);
                        }
                    }
                }
                else
                {
                    // Full update: show all skipped items
                    skippedToShow = _skippedMods;
                }
                
                // Add failed mods to skipped list for display
                skippedToShow.AddRange(_failedMods);
                
                // Show skipped items if there are any to show
                if (skippedToShow.Count > 0)
                {
                    string skippedHeader = IsSmartUpdate ? 
                        SharedUtilities.GetTranslation(lang, "SkippedModsWithIssues") : 
                        SharedUtilities.GetTranslation(lang, "SkippedMods");
                    summary += "\n\n" + skippedHeader + "\n" + string.Join("\n", skippedToShow);
                }
                // Create scrollable content
                var textBlock = new Microsoft.UI.Xaml.Controls.TextBlock
                {
                    Text = summary,
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                    Width = 500
                };

                var scrollViewer = new Microsoft.UI.Xaml.Controls.ScrollViewer
                {
                    Content = textBlock,
                    VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Disabled,
                    MaxHeight = 400,
                    Padding = new Microsoft.UI.Xaml.Thickness(10)
                };

                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "SummaryTitle"),
                    Content = scrollViewer,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (OperationCanceledException)
            {
                ResetButtonToUpdateState();
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "CancelledTitle"),
                    Content = SharedUtilities.GetTranslation(lang, "CancelledContent"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                ResetButtonToUpdateState();
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "ErrorTitle"),
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private async Task<string?> FetchAuthorFromApi(string url, CancellationToken token)
        {
            try
            {
                // Parse GameBanana URL to extract item type and ID
                var match = _urlPattern.Match(url);
                if (!match.Success)
                {
                    Logger.LogError($"Failed to parse GameBanana URL: {url}");
                    return null;
                }

                string itemType = match.Groups[1].Value; // e.g., "mods", "tools"
                string itemId = match.Groups[2].Value;   // e.g., "574763"

                // Capitalize first letter for API (Mod, Tool, etc.)
                itemType = char.ToUpper(itemType[0]) + itemType.Substring(1).TrimEnd('s');

                // Build API URL
                string apiUrl = $"https://gamebanana.com/apiv11/{itemType}/{itemId}?_csvProperties=_aSubmitter";

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                var response = await _httpClient.GetStringAsync(apiUrl, token);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                // Extract author name from _aSubmitter._sName
                if (root.TryGetProperty("_aSubmitter", out var submitter) &&
                    submitter.TryGetProperty("_sName", out var authorName))
                {
                    return authorName.GetString();
                }

                return null;
            }
            catch (Exception ex)
            {
                // Log as info since most failures are due to private/deleted mods or invalid URLs
                Logger.LogInfo($"Could not fetch author from API for URL: {url} - {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Update button states - disable all buttons when any operation is running
        /// </summary>
        private void UpdateButtonStates()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            var mainLang = SharedUtilities.LoadLanguageDictionary();
            
            // Check if any operation is currently running
            bool anyOperationRunning = _isUpdatingAuthors || _isFetchingDates || _isFetchingVersions || 
                                     _isFetchingAllPreviews || _isFetchingMissingPreviews;
            
            // Update Authors button
            if (UpdateButton != null && UpdateButtonText != null)
            {
                if (_isUpdatingAuthors)
                {
                    UpdateButton.IsEnabled = true; // Keep enabled so user can cancel
                    UpdateButtonText.Text = SharedUtilities.GetTranslation(mainLang, "Cancel");
                }
                else
                {
                    UpdateButton.IsEnabled = !anyOperationRunning; // Disable if any other operation is running
                    UpdateButtonText.Text = SharedUtilities.GetTranslation(lang, "Start");
                }
            }
            
            // Fetch Dates button
            if (FetchDatesButton != null && FetchDatesButtonText != null)
            {
                if (_isFetchingDates)
                {
                    FetchDatesButton.IsEnabled = true; // Keep enabled so user can cancel
                    FetchDatesButtonText.Text = SharedUtilities.GetTranslation(mainLang, "Cancel");
                }
                else
                {
                    FetchDatesButton.IsEnabled = !anyOperationRunning; // Disable if any other operation is running
                    FetchDatesButtonText.Text = SharedUtilities.GetTranslation(lang, "Start");
                }
            }
            
            // Fetch Versions button
            if (FetchVersionsButton != null && FetchVersionsButtonText != null)
            {
                if (_isFetchingVersions)
                {
                    FetchVersionsButton.IsEnabled = true; // Keep enabled so user can cancel
                    FetchVersionsButtonText.Text = SharedUtilities.GetTranslation(mainLang, "Cancel");
                }
                else
                {
                    FetchVersionsButton.IsEnabled = !anyOperationRunning; // Disable if any other operation is running
                    FetchVersionsButtonText.Text = SharedUtilities.GetTranslation(lang, "Start");
                }
            }
            
            // Fetch All Previews button
            if (FetchAllPreviewsButton != null && FetchAllPreviewsButtonText != null)
            {
                if (_isFetchingAllPreviews)
                {
                    FetchAllPreviewsButton.IsEnabled = true; // Keep enabled so user can cancel
                    FetchAllPreviewsButtonText.Text = SharedUtilities.GetTranslation(mainLang, "Cancel");
                }
                else
                {
                    FetchAllPreviewsButton.IsEnabled = !anyOperationRunning; // Disable if any other operation is running
                    FetchAllPreviewsButtonText.Text = SharedUtilities.GetTranslation(lang, "Start");
                }
            }
            
            // Fetch Missing Previews button
            if (FetchMissingPreviewsButton != null && FetchMissingPreviewsButtonText != null)
            {
                if (_isFetchingMissingPreviews)
                {
                    FetchMissingPreviewsButton.IsEnabled = true; // Keep enabled so user can cancel
                    FetchMissingPreviewsButtonText.Text = SharedUtilities.GetTranslation(mainLang, "Cancel");
                }
                else
                {
                    FetchMissingPreviewsButton.IsEnabled = !anyOperationRunning; // Disable if any other operation is running
                    FetchMissingPreviewsButtonText.Text = SharedUtilities.GetTranslation(lang, "Start");
                }
            }
        }

        private void UpdateProgressBarUI()
        {
            // Update button states - disable all buttons when any operation is running
            UpdateButtonStates();
            
            if (UpdateProgressBar != null)
            {
                if (_isUpdatingAuthors)
                {
                    UpdateProgressBar.Visibility = Visibility.Visible;
                    UpdateProgressBar.IsIndeterminate = false;
                    UpdateProgressBar.Value = _progressValue * 100;
                    
                    // Update status text
                    if (UpdateProgressStatusText != null)
                    {
                        UpdateProgressStatusText.Visibility = Visibility.Visible;
                        if (!string.IsNullOrEmpty(_currentProcessingMod))
                        {
                            UpdateProgressStatusText.Text = _currentProcessingMod;
                        }
                        else
                        {
                            UpdateProgressStatusText.Text = "";
                        }
                    }
                }
                else
                {
                    UpdateProgressBar.Value = 0;
                    UpdateProgressBar.IsIndeterminate = false;
                    UpdateProgressBar.Visibility = Visibility.Collapsed;
                    
                    if (UpdateProgressStatusText != null)
                    {
                        UpdateProgressStatusText.Visibility = Visibility.Collapsed;
                        UpdateProgressStatusText.Text = "";
                    }
                }
            }
            
            if (FetchDatesProgressBar != null)
            {
                if (_isFetchingDates)
                {
                    FetchDatesProgressBar.Visibility = Visibility.Visible;
                    FetchDatesProgressBar.IsIndeterminate = false;
                    FetchDatesProgressBar.Value = _progressValue * 100;
                    
                    // Update status text
                    if (FetchDatesStatusText != null)
                    {
                        FetchDatesStatusText.Visibility = Visibility.Visible;
                        if (!string.IsNullOrEmpty(_currentProcessingMod))
                        {
                            FetchDatesStatusText.Text = _currentProcessingMod;
                        }
                        else
                        {
                            FetchDatesStatusText.Text = "";
                        }
                    }
                }
                else
                {
                    FetchDatesProgressBar.Value = 0;
                    FetchDatesProgressBar.IsIndeterminate = false;
                    FetchDatesProgressBar.Visibility = Visibility.Collapsed;
                    
                    if (FetchDatesStatusText != null)
                    {
                        FetchDatesStatusText.Visibility = Visibility.Collapsed;
                        FetchDatesStatusText.Text = "";
                    }
                }
            }
            
            if (FetchVersionsProgressBar != null)
            {
                if (_isFetchingVersions)
                {
                    FetchVersionsProgressBar.Visibility = Visibility.Visible;
                    FetchVersionsProgressBar.IsIndeterminate = false;
                    FetchVersionsProgressBar.Value = _progressValue * 100;
                    
                    // Update status text
                    if (FetchVersionsStatusText != null)
                    {
                        FetchVersionsStatusText.Visibility = Visibility.Visible;
                        if (!string.IsNullOrEmpty(_currentProcessingMod))
                        {
                            FetchVersionsStatusText.Text = _currentProcessingMod;
                        }
                        else
                        {
                            FetchVersionsStatusText.Text = "";
                        }
                    }
                }
                else
                {
                    FetchVersionsProgressBar.Value = 0;
                    FetchVersionsProgressBar.IsIndeterminate = false;
                    FetchVersionsProgressBar.Visibility = Visibility.Collapsed;
                    
                    if (FetchVersionsStatusText != null)
                    {
                        FetchVersionsStatusText.Visibility = Visibility.Collapsed;
                        FetchVersionsStatusText.Text = "";
                    }
                }
            }
            
            if (FetchAllPreviewsProgressBar != null)
            {
                if (_isFetchingAllPreviews)
                {
                    FetchAllPreviewsProgressBar.Visibility = Visibility.Visible;
                    FetchAllPreviewsProgressBar.IsIndeterminate = false;
                    FetchAllPreviewsProgressBar.Value = _progressValue * 100;
                    
                    // Update status text
                    if (FetchAllPreviewsStatusText != null)
                    {
                        FetchAllPreviewsStatusText.Visibility = Visibility.Visible;
                        if (!string.IsNullOrEmpty(_currentProcessingMod))
                        {
                            FetchAllPreviewsStatusText.Text = _currentProcessingMod;
                        }
                        else
                        {
                            FetchAllPreviewsStatusText.Text = "";
                        }
                    }
                }
                else
                {
                    FetchAllPreviewsProgressBar.Value = 0;
                    FetchAllPreviewsProgressBar.IsIndeterminate = false;
                    FetchAllPreviewsProgressBar.Visibility = Visibility.Collapsed;
                    
                    if (FetchAllPreviewsStatusText != null)
                    {
                        FetchAllPreviewsStatusText.Visibility = Visibility.Collapsed;
                        FetchAllPreviewsStatusText.Text = "";
                    }
                }
            }
            
            if (FetchMissingPreviewsProgressBar != null)
            {
                if (_isFetchingMissingPreviews)
                {
                    FetchMissingPreviewsProgressBar.Visibility = Visibility.Visible;
                    FetchMissingPreviewsProgressBar.IsIndeterminate = false;
                    FetchMissingPreviewsProgressBar.Value = _progressValue * 100;
                    
                    // Update status text
                    if (FetchMissingPreviewsStatusText != null)
                    {
                        FetchMissingPreviewsStatusText.Visibility = Visibility.Visible;
                        if (!string.IsNullOrEmpty(_currentProcessingMod))
                        {
                            FetchMissingPreviewsStatusText.Text = _currentProcessingMod;
                        }
                        else
                        {
                            FetchMissingPreviewsStatusText.Text = "";
                        }
                    }
                }
                else
                {
                    FetchMissingPreviewsProgressBar.Value = 0;
                    FetchMissingPreviewsProgressBar.IsIndeterminate = false;
                    FetchMissingPreviewsProgressBar.Visibility = Visibility.Collapsed;
                    
                    if (FetchMissingPreviewsStatusText != null)
                    {
                        FetchMissingPreviewsStatusText.Visibility = Visibility.Collapsed;
                        FetchMissingPreviewsStatusText.Text = "";
                    }
                }
            }
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ProgressChanged += OnProgressChanged;
            // Restore switch states according to mode
            UpdateAuthorsSwitch.IsOn = (CurrentUpdateMode == UpdateMode.Full);
            SmartUpdateSwitch.IsOn = (CurrentUpdateMode == UpdateMode.Smart);
            UpdateProgressBarUI();
            // Refresh translations when navigating to this page
            UpdateTexts();
            UpdateToggleLabels();
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ProgressChanged -= OnProgressChanged;
        }

        private static UpdateMode _updateMode = UpdateMode.Full;
        private enum UpdateMode { Full, Smart }
        private static UpdateMode CurrentUpdateMode
        {
            get => _updateMode;
            set => _updateMode = value;
        }

        private void UpdateAuthorsSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (UpdateAuthorsSwitch.IsOn)
            {
                SmartUpdateSwitch.IsOn = false;
                CurrentUpdateMode = UpdateMode.Full;
            }
            else if (!SmartUpdateSwitch.IsOn)
            {
                SmartUpdateSwitch.IsOn = true;
                CurrentUpdateMode = UpdateMode.Smart;
            }
            UpdateToggleLabels();
        }

        private void SmartUpdateSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (SmartUpdateSwitch.IsOn)
            {
                UpdateAuthorsSwitch.IsOn = false;
                CurrentUpdateMode = UpdateMode.Smart;
            }
            else if (!UpdateAuthorsSwitch.IsOn)
            {
                UpdateAuthorsSwitch.IsOn = true;
                CurrentUpdateMode = UpdateMode.Full;
            }
            UpdateToggleLabels();
        }

        public bool IsSmartUpdate => CurrentUpdateMode == UpdateMode.Smart;
        public bool IsFullUpdate => CurrentUpdateMode == UpdateMode.Full;

        // Fetch Dates functionality
        private CancellationTokenSource? _ctsDates;

        private async void FetchDatesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await FetchDatesButtonClickAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in FetchDatesButton_Click", ex);
                ResetDatesButtonToFetchState();
            }
        }

        private async Task FetchDatesButtonClickAsync()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            var mainLang = SharedUtilities.LoadLanguageDictionary();

            if (_isFetchingDates)
            {
                _ctsDates?.Cancel();
                _isFetchingDates = false;
                ResetDatesButtonToFetchState();
                var cancelDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "CancelledTitle"),
                    Content = SharedUtilities.GetTranslation(lang, "CancelledContent"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await cancelDialog.ShowAsync();
                return;
            }
            
            // Add confirmation dialog
            var confirmDialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(lang, "FetchDatesButton"),
                Content = SharedUtilities.GetTranslation(lang, "ConfirmFetchDates"),
                PrimaryButtonText = SharedUtilities.GetTranslation(mainLang, "Continue"),
                CloseButtonText = SharedUtilities.GetTranslation(mainLang, "Cancel"),
                XamlRoot = this.XamlRoot
            };
            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            _ctsDates = new CancellationTokenSource();
            _isFetchingDates = true;
            lock (_lockObject)
            {
                _success = 0; _fail = 0; _skip = 0;
                _skippedMods.Clear();
                _failedMods.Clear();
                _progressValue = 0;
                _totalMods = 0;
            }
            NotifyProgressChanged(); // This will update button states via UpdateButtonStates()
            await FetchDatesAsync(_ctsDates.Token);
            ResetDatesButtonToFetchState();
        }

        private void ResetDatesButtonToFetchState()
        {
            _isFetchingDates = false;
            NotifyProgressChanged(); // This will call UpdateButtonStates() via UpdateProgressBarUI()
        }

        private async Task FetchDatesAsync(CancellationToken token)
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

                // Process in parallel with max 5 concurrent requests
                var semaphore = new SemaphoreSlim(5);
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

                        // Read mod.json to get URL (quick read, no queue needed for read-only)
                        var json = await Services.FileAccessQueue.ReadAllTextAsync(modJsonPath, token);
                        string? url = null;
                        string modName = modFolderName;
                        
                        using (var doc = JsonDocument.Parse(json))
                        {
                            var root = doc.RootElement;

                            modName = root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString())
                                ? nameProp.GetString()!
                                : modFolderName;

                            // Remove DISABLED_ prefix from folder name for display
                            var displayFolderName = modFolderName.StartsWith("DISABLED_") 
                                ? modFolderName.Substring("DISABLED_".Length) 
                                : modFolderName;
                            
                            // Use clean name for display
                            var displayName = root.TryGetProperty("name", out var displayNameProp) && !string.IsNullOrWhiteSpace(displayNameProp.GetString()) 
                                ? displayNameProp.GetString()! 
                                : displayFolderName;
                            
                            // Update current processing mod status
                            SafeSetCurrentProcessingMod(displayName);
                            NotifyProgressChanged();

                            if (!root.TryGetProperty("url", out var urlProp) || urlProp.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(urlProp.GetString()) || !urlProp.GetString()!.Contains("gamebanana.com"))
                            {
                                SafeIncrementSkip();
                                SafeAddSkippedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "InvalidUrl")}");
                                Interlocked.Increment(ref processed);
                                lock (_lockObject) { _progressValue = (double)processed / _totalMods; }
                                NotifyProgressChanged();
                                return;
                            }

                            url = urlProp.GetString()!;
                        }

                        try
                        {
                            var dateUpdated = await FetchDateFromApi(url, token);
                            if (!string.IsNullOrWhiteSpace(dateUpdated))
                            {
                                if (!SecurityValidator.IsValidModDirectoryName(modFolderName))
                                {
                                    SafeIncrementSkip();
                                    SafeAddSkippedMod($"{modName}: Invalid directory name");
                                    Interlocked.Increment(ref processed);
                                    lock (_lockObject) { _progressValue = (double)processed / _totalMods; }
                                    NotifyProgressChanged();
                                    return;
                                }

                                // Atomic read-modify-write operation
                                await Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                                {
                                    var currentJson = await File.ReadAllTextAsync(modJsonPath, token);
                                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(currentJson) ?? new();
                                    dict["gbChangeDate"] = dateUpdated;
                                    await File.WriteAllTextAsync(modJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }), token);
                                }, token);
                                SafeIncrementSuccess();
                            }
                            else
                            {
                                SafeIncrementSkip();
                                SafeAddSkippedMod($"{modName}: Nie udało się pobrać daty");
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to fetch date for {modName}", ex);
                            SafeIncrementFail();
                            SafeAddFailedMod($"{modName}: Błąd pobierania daty");
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
                    ResetDatesButtonToFetchState();
                    var cancelDialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "CancelledTitle"),
                        Content = SharedUtilities.GetTranslation(lang, "CancelledContent"),
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await cancelDialog.ShowAsync();
                    return;
                }

                ResetDatesButtonToFetchState();
                string summary = $"Łącznie sprawdzono: {_totalMods}\n" +
                                $"Pomyślnie zaktualizowano: {_success}\n" +
                                $"Pominięto: {_skip}";

                if (_failedMods.Count > 0 || _skippedMods.Count > 0)
                {
                    var allIssues = new List<string>();
                    allIssues.AddRange(_skippedMods);
                    allIssues.AddRange(_failedMods);
                    summary += "\n\nMody z problemami:\n" + string.Join("\n", allIssues);
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
                    Title = "Podsumowanie pobierania dat",
                    Content = scrollViewer,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (OperationCanceledException)
            {
                ResetDatesButtonToFetchState();
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "CancelledTitle"),
                    Content = SharedUtilities.GetTranslation(lang, "CancelledContent"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                ResetDatesButtonToFetchState();
                var dialog = new ContentDialog
                {
                    Title = "Błąd",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private async Task<string?> FetchDateFromApi(string url, CancellationToken token)
        {
            try
            {
                var match = _urlPattern.Match(url);
                if (!match.Success)
                {
                    Logger.LogError($"Failed to parse GameBanana URL: {url}");
                    return null;
                }

                string itemType = match.Groups[1].Value;
                string itemId = match.Groups[2].Value;
                itemType = char.ToUpper(itemType[0]) + itemType.Substring(1).TrimEnd('s');

                string apiUrl = $"https://gamebanana.com/apiv11/{itemType}/{itemId}?_csvProperties=_tsDateUpdated,_tsDateAdded";

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                var response = await _httpClient.GetStringAsync(apiUrl, token);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                // Try _tsDateUpdated first, fallback to _tsDateAdded
                long timestamp = 0;
                if (root.TryGetProperty("_tsDateUpdated", out var dateUpdated) && dateUpdated.ValueKind == JsonValueKind.Number)
                {
                    timestamp = dateUpdated.GetInt64();
                }
                
                // Fallback to _tsDateAdded if _tsDateUpdated is 0 or missing
                if (timestamp == 0 && root.TryGetProperty("_tsDateAdded", out var dateAdded) && dateAdded.ValueKind == JsonValueKind.Number)
                {
                    timestamp = dateAdded.GetInt64();
                }

                if (timestamp > 0)
                {
                    var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                    var result = date.ToString("yyyy-MM-dd");
                    Logger.LogInfo($"FetchDateFromApi: URL={url}, timestamp={timestamp}, date={result}");
                    return result;
                }

                Logger.LogWarning($"FetchDateFromApi: No valid timestamp found for URL={url}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to fetch date from API for URL: {url}", ex);
                return null;
            }
        }

        // Fetch Versions functionality
        private CancellationTokenSource? _ctsVersions;


        private async void FetchVersionsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await FetchVersionsButtonClickAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in FetchVersionsButton_Click", ex);
                ResetVersionsButtonToFetchState();
            }
        }

        private async Task FetchVersionsButtonClickAsync()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            var mainLang = SharedUtilities.LoadLanguageDictionary();

            if (_isFetchingVersions)
            {
                _ctsVersions?.Cancel();
                _isFetchingVersions = false;
                ResetVersionsButtonToFetchState();
                var cancelDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "CancelledTitle"),
                    Content = SharedUtilities.GetTranslation(lang, "CancelledContent"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await cancelDialog.ShowAsync();
                return;
            }
            
            // Add confirmation dialog
            var confirmDialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(lang, "FetchVersionsButton"),
                Content = SharedUtilities.GetTranslation(lang, "ConfirmFetchVersions"),
                PrimaryButtonText = SharedUtilities.GetTranslation(mainLang, "Continue"),
                CloseButtonText = SharedUtilities.GetTranslation(mainLang, "Cancel"),
                XamlRoot = this.XamlRoot
            };
            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            _ctsVersions = new CancellationTokenSource();
            _isFetchingVersions = true;
            lock (_lockObject)
            {
                _success = 0; _fail = 0; _skip = 0;
                _skippedMods.Clear();
                _failedMods.Clear();
                _progressValue = 0;
                _totalMods = 0;
            }
            NotifyProgressChanged(); // This will update button states via UpdateButtonStates()
            await FetchVersionsAsync(_ctsVersions.Token);
            ResetVersionsButtonToFetchState();
        }

        private void ResetVersionsButtonToFetchState()
        {
            _isFetchingVersions = false;
            NotifyProgressChanged(); // This will call UpdateButtonStates() via UpdateProgressBarUI()
        }

        private async Task FetchVersionsAsync(CancellationToken token)
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

                // Process in parallel with max 5 concurrent requests
                var semaphore = new SemaphoreSlim(5);
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

                        // Read mod.json through queue
                        var json = await Services.FileAccessQueue.ReadAllTextAsync(modJsonPath, token);
                        string? url = null;
                        string modName = modFolderName;
                        
                        using (var doc = JsonDocument.Parse(json))
                        {
                            var root = doc.RootElement;

                            modName = root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString())
                                ? nameProp.GetString()!
                                : modFolderName;

                            // Remove DISABLED_ prefix from folder name for display
                            var displayFolderName = modFolderName.StartsWith("DISABLED_") 
                                ? modFolderName.Substring("DISABLED_".Length) 
                                : modFolderName;
                            
                            // Use clean name for display
                            var displayName = root.TryGetProperty("name", out var displayNameProp) && !string.IsNullOrWhiteSpace(displayNameProp.GetString()) 
                                ? displayNameProp.GetString()! 
                                : displayFolderName;
                            
                            // Update current processing mod status
                            SafeSetCurrentProcessingMod(displayName);
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

                        try
                        {
                            var version = await FetchVersionFromApi(url, token);
                            
                            if (!SecurityValidator.IsValidModDirectoryName(modFolderName))
                            {
                                SafeIncrementSkip();
                                SafeAddSkippedMod($"{modName}: Invalid directory name");
                                Interlocked.Increment(ref processed);
                                lock (_lockObject) { _progressValue = (double)processed / _totalMods; }
                                NotifyProgressChanged();
                                return;
                            }

                            // Atomic read-modify-write operation
                            await Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                            {
                                var currentJson = await File.ReadAllTextAsync(modJsonPath, token);
                                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(currentJson) ?? new();
                                // Use space if version is null/empty, otherwise use the fetched version
                                dict["version"] = string.IsNullOrWhiteSpace(version) ? " " : version;
                                await File.WriteAllTextAsync(modJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }), token);
                            }, token);
                            SafeIncrementSuccess();
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to fetch version for {modName}", ex);
                            SafeIncrementFail();
                            SafeAddFailedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "VersionFetchError")}");
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
                    ResetVersionsButtonToFetchState();
                    var cancelDialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "CancelledTitle"),
                        Content = SharedUtilities.GetTranslation(lang, "CancelledContent"),
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await cancelDialog.ShowAsync();
                    return;
                }

                ResetVersionsButtonToFetchState();
                string summary = string.Format(SharedUtilities.GetTranslation(lang, "TotalChecked"), _totalMods) + "\n" +
                                string.Format(SharedUtilities.GetTranslation(lang, "SuccessCount"), _success) + "\n" +
                                string.Format(SharedUtilities.GetTranslation(lang, "SkippedCount"), _skip);

                if (_failedMods.Count > 0 || _skippedMods.Count > 0)
                {
                    var allIssues = new List<string>();
                    allIssues.AddRange(_skippedMods);
                    allIssues.AddRange(_failedMods);
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
                    Title = SharedUtilities.GetTranslation(lang, "VersionSummaryTitle"),
                    Content = scrollViewer,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (OperationCanceledException)
            {
                ResetVersionsButtonToFetchState();
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "CancelledTitle"),
                    Content = SharedUtilities.GetTranslation(lang, "CancelledContent"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                ResetVersionsButtonToFetchState();
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "ErrorTitle"),
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private async Task<string?> FetchVersionFromApi(string url, CancellationToken token)
        {
            try
            {
                var match = _urlPattern.Match(url);
                if (!match.Success)
                {
                    Logger.LogError($"Failed to parse GameBanana URL: {url}");
                    return null;
                }

                string itemType = match.Groups[1].Value;
                string itemId = match.Groups[2].Value;
                itemType = char.ToUpper(itemType[0]) + itemType.Substring(1).TrimEnd('s');

                string apiUrl = $"https://gamebanana.com/apiv11/{itemType}/{itemId}?_csvProperties=_sVersion";

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                var response = await _httpClient.GetStringAsync(apiUrl, token);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (root.TryGetProperty("_sVersion", out var version))
                {
                    return version.GetString();
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to fetch version from API for URL: {url}", ex);
                return null;
            }
        }

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
                var cancelDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "CancelledTitle"),
                    Content = SharedUtilities.GetTranslation(lang, "CancelledContent"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await cancelDialog.ShowAsync();
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
                var cancelDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "CancelledTitle"),
                    Content = SharedUtilities.GetTranslation(lang, "CancelledContent"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await cancelDialog.ShowAsync();
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
                                       (f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                        f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                        f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
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
                                        return (fileName.StartsWith("preview") || fileName == "minitile.jpg") &&
                                               (f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
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
                                                return (fileName.StartsWith("preview") || fileName == "minitile.jpg") &&
                                                       (f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                        f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                                        f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
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
                            else
                            {
                                SafeIncrementSkip();
                                SafeAddSkippedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "NoPreviewsAvailable")}");
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
                    var cancelDialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "CancelledTitle"),
                        Content = SharedUtilities.GetTranslation(lang, "CancelledContent"),
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await cancelDialog.ShowAsync();
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
                var cancelDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "CancelledTitle"),
                    Content = SharedUtilities.GetTranslation(lang, "CancelledContent"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await cancelDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "ErrorTitle"),
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
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
                    return 0;
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
                return 0;
            }
        }

    }
}
