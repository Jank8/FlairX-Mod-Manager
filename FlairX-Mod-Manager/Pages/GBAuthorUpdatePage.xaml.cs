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
            UpdateButton.Content = _isUpdatingAuthors ? SharedUtilities.GetTranslation(mainLang, "Cancel") : SharedUtilities.GetTranslation(lang, "Start");
            
            // Fetch dates card
            FetchDatesTitle.Text = SharedUtilities.GetTranslation(lang, "FetchDatesButton");
            FetchDatesDescription.Text = SharedUtilities.GetTranslation(lang, "FetchDatesButton_Tooltip");
            FetchDatesButton.Content = _isFetchingDates ? SharedUtilities.GetTranslation(mainLang, "Cancel") : SharedUtilities.GetTranslation(lang, "Start");
            
            // Fetch versions card
            FetchVersionsTitle.Text = SharedUtilities.GetTranslation(lang, "FetchVersionsButton");
            FetchVersionsDescription.Text = SharedUtilities.GetTranslation(lang, "FetchVersionsButton_Tooltip");
            FetchVersionsButton.Content = _isFetchingVersions ? SharedUtilities.GetTranslation(mainLang, "Cancel") : SharedUtilities.GetTranslation(lang, "Start");
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
        private static int _success = 0, _fail = 0, _skip = 0;
        private static List<string> _skippedMods = new();
        private static List<string> _failedMods = new();
        private static double _progressValue = 0;
        private static int _totalMods = 0;
        
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
            SetButtonToCancelState();
            _isUpdatingAuthors = true;
            lock (_lockObject)
            {
                _success = 0; _fail = 0; _skip = 0;
                _skippedMods.Clear();
                _failedMods.Clear();
                _progressValue = 0;
                _totalMods = 0;
            }
            NotifyProgressChanged();
            await UpdateAuthorsAsync(_cts.Token);
            // Final cleanup - ensure button is reset to update state
            ResetButtonToUpdateState();
        }

        private void SetButtonToCancelState()
        {
            var mainLang = SharedUtilities.LoadLanguageDictionary();
            UpdateButton.Content = SharedUtilities.GetTranslation(mainLang, "Cancel");
            UpdateButton.IsEnabled = true;
            UpdateProgressBar.Visibility = Visibility.Visible;
        }

        private void ResetButtonToUpdateState()
        {
            _isUpdatingAuthors = false;
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            UpdateButton.Content = SharedUtilities.GetTranslation(lang, "Start");
            UpdateButton.IsEnabled = true;
            UpdateProgressBar.Visibility = Visibility.Collapsed;
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
                        
                        var json = File.ReadAllText(modJsonPath);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        
                        // Get mod name from mod.json, fallback to folder name
                        string modName = root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString()) 
                            ? nameProp.GetString()! 
                            : modFolderName;
                        
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
                        
                        string currentAuthor = root.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? string.Empty : string.Empty;
                        bool shouldUpdate = string.IsNullOrWhiteSpace(currentAuthor) || currentAuthor.Equals("unknown", StringComparison.OrdinalIgnoreCase);
                        
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
                        
                        string url = urlProp.GetString()!;
                        
                        try
                        {
                            var author = await FetchAuthorFromApi(url, token);
                            if (!string.IsNullOrWhiteSpace(author))
                            {
                                if (isFullUpdate)
                                {
                                    if (!author.Equals(currentAuthor, StringComparison.Ordinal))
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
                                        
                                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                                        dict["author"] = author;
                                        await Services.FileAccessQueue.WriteAllTextAsync(modJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
                                        lock (_lockObject) { _success++; }
                                    }
                                }
                                else if (isSmartUpdate && shouldUpdate)
                                {
                                    if (!string.IsNullOrWhiteSpace(author) && !author.Equals(currentAuthor, StringComparison.Ordinal))
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
                                        
                                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                                        dict["author"] = author;
                                        await Services.FileAccessQueue.WriteAllTextAsync(modJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
                                        lock (_lockObject) { _success++; }
                                    }
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
                Logger.LogError($"Failed to fetch author from API for URL: {url}", ex);
                return null;
            }
        }

        private void UpdateProgressBarUI()
        {
            if (UpdateProgressBar != null)
            {
                if (_isUpdatingAuthors)
                {
                    UpdateProgressBar.Visibility = Visibility.Visible;
                    UpdateProgressBar.IsIndeterminate = false;
                    UpdateProgressBar.Value = _progressValue * 100;                }
                else
                {
                    UpdateProgressBar.Value = 0;
                    UpdateProgressBar.IsIndeterminate = false;
                    UpdateProgressBar.Visibility = Visibility.Collapsed;                }
            }
            
            if (FetchDatesProgressBar != null)
            {
                if (_isFetchingDates)
                {
                    FetchDatesProgressBar.Visibility = Visibility.Visible;
                    FetchDatesProgressBar.IsIndeterminate = false;
                    FetchDatesProgressBar.Value = _progressValue * 100;                }
                else
                {
                    FetchDatesProgressBar.Value = 0;
                    FetchDatesProgressBar.IsIndeterminate = false;
                    FetchDatesProgressBar.Visibility = Visibility.Collapsed;                }
            }
            
            if (FetchVersionsProgressBar != null)
            {
                if (_isFetchingVersions)
                {
                    FetchVersionsProgressBar.Visibility = Visibility.Visible;
                    FetchVersionsProgressBar.IsIndeterminate = false;
                    FetchVersionsProgressBar.Value = _progressValue * 100;                }
                else
                {
                    FetchVersionsProgressBar.Value = 0;
                    FetchVersionsProgressBar.IsIndeterminate = false;
                    FetchVersionsProgressBar.Visibility = Visibility.Collapsed;                }
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
            SetDatesButtonToCancelState();
            _isFetchingDates = true;
            lock (_lockObject)
            {
                _success = 0; _fail = 0; _skip = 0;
                _skippedMods.Clear();
                _failedMods.Clear();
                _progressValue = 0;
                _totalMods = 0;
            }
            NotifyProgressChanged();
            await FetchDatesAsync(_ctsDates.Token);
            ResetDatesButtonToFetchState();
        }

        private void SetDatesButtonToCancelState()
        {
            var mainLang = SharedUtilities.LoadLanguageDictionary();
            FetchDatesButton.Content = SharedUtilities.GetTranslation(mainLang, "Cancel");
            FetchDatesButton.IsEnabled = true;
            FetchDatesProgressBar.Visibility = Visibility.Visible;
        }

        private void ResetDatesButtonToFetchState()
        {
            _isFetchingDates = false;
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            FetchDatesButton.Content = SharedUtilities.GetTranslation(lang, "Start");
            FetchDatesButton.IsEnabled = true;
            FetchDatesProgressBar.Visibility = Visibility.Collapsed;
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

                        var json = File.ReadAllText(modJsonPath);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        string modName = root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString())
                            ? nameProp.GetString()!
                            : modFolderName;

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

                        string url = urlProp.GetString()!;

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

                                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                                dict["gbChangeDate"] = dateUpdated;
                                await Services.FileAccessQueue.WriteAllTextAsync(modJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
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
                if (root.TryGetProperty("_tsDateUpdated", out var dateUpdated))
                {
                    timestamp = dateUpdated.GetInt64();
                }
                else if (root.TryGetProperty("_tsDateAdded", out var dateAdded))
                {
                    timestamp = dateAdded.GetInt64();
                }

                if (timestamp > 0)
                {
                    var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                    return date.ToString("yyyy-MM-dd");
                }

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
            SetVersionsButtonToCancelState();
            _isFetchingVersions = true;
            lock (_lockObject)
            {
                _success = 0; _fail = 0; _skip = 0;
                _skippedMods.Clear();
                _failedMods.Clear();
                _progressValue = 0;
                _totalMods = 0;
            }
            NotifyProgressChanged();
            await FetchVersionsAsync(_ctsVersions.Token);
            ResetVersionsButtonToFetchState();
        }

        private void SetVersionsButtonToCancelState()
        {
            var mainLang = SharedUtilities.LoadLanguageDictionary();
            FetchVersionsButton.Content = SharedUtilities.GetTranslation(mainLang, "Cancel");
            FetchVersionsButton.IsEnabled = true;
            FetchVersionsProgressBar.Visibility = Visibility.Visible;
        }

        private void ResetVersionsButtonToFetchState()
        {
            _isFetchingVersions = false;
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            FetchVersionsButton.Content = SharedUtilities.GetTranslation(lang, "Start");
            FetchVersionsButton.IsEnabled = true;
            FetchVersionsProgressBar.Visibility = Visibility.Collapsed;
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

                        var json = File.ReadAllText(modJsonPath);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        string modName = root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString())
                            ? nameProp.GetString()!
                            : modFolderName;

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

                        string url = urlProp.GetString()!;

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

                            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                            // Use space if version is null/empty, otherwise use the fetched version
                            dict["version"] = string.IsNullOrWhiteSpace(version) ? " " : version;
                            await Services.FileAccessQueue.WriteAllTextAsync(modJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
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

    }
}
