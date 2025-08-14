using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        
        // Compiled regex patterns for better performance
        private static readonly Regex[] _authorPatterns = new[]
        {
            new Regex("<a class=\"Uploader[^\"]*\" href=\"[^\"]+\">([^<]+)</a>", RegexOptions.Compiled),
            new Regex("<span class=\"UserName[^\"]*\">([^<]+)</span>", RegexOptions.Compiled),
            new Regex("<a class=\"UserName[^\"]*\" href=\"[^\"]+\">([^<]+)</a>", RegexOptions.Compiled),
            new Regex("<meta name=\"author\" content=\"([^\"]+)\"", RegexOptions.Compiled),
            new Regex("\\\"author\\\":\\\"([^\\\"]+)\\\"", RegexOptions.Compiled)
        };
        
        private static readonly Regex _jsonLdPattern = new("<script type=\"application/ld\\+json\">(.*?)</script>", RegexOptions.Compiled | RegexOptions.Singleline);

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            UpdateAuthorsLabel.Text = SharedUtilities.GetTranslation(lang, "UpdateAuthorsLabel");
            SmartUpdateLabel.Text = SharedUtilities.GetTranslation(lang, "SmartUpdateLabel");
            SafetyLockLabel.Text = SharedUtilities.GetTranslation(lang, "SafetyLockLabel");
            // Set constant button width to prevent resizing when text changes
            UpdateButtonText.Text = _isUpdatingAuthors ? SharedUtilities.GetTranslation(lang, "CancelButton") : SharedUtilities.GetTranslation(lang, "UpdateButton");
            UpdateButton.MinWidth = 160;
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
            ProgressChanged += OnProgressChanged;
            UpdateAuthorsSwitch.Toggled += UpdateAuthorsSwitch_Toggled;
            SmartUpdateSwitch.Toggled += SmartUpdateSwitch_Toggled;
            // By default only one active
            if (UpdateAuthorsSwitch.IsOn && SmartUpdateSwitch.IsOn)
                SmartUpdateSwitch.IsOn = false;
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
            if (SafetyLockSwitch.IsOn == false)
            {
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "SafetyLockTitle"),
                    Content = SharedUtilities.GetTranslation(lang, "SafetyLockContent"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }
            SafetyLockSwitch.IsOn = false;
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
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            UpdateButtonText.Text = SharedUtilities.GetTranslation(lang, "CancelButton");
            UpdateIcon.Visibility = Visibility.Collapsed;
            CancelIcon.Visibility = Visibility.Visible;
            UpdateButton.IsEnabled = true;
            UpdateProgressBar.Visibility = Visibility.Visible;
        }

        private void ResetButtonToUpdateState()
        {
            _isUpdatingAuthors = false;
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            UpdateButtonText.Text = SharedUtilities.GetTranslation(lang, "UpdateButton");
            UpdateIcon.Visibility = Visibility.Visible;
            CancelIcon.Visibility = Visibility.Collapsed;
            UpdateButton.IsEnabled = true;
            UpdateProgressBar.Visibility = Visibility.Collapsed;
        }

        private async Task UpdateAuthorsAsync(CancellationToken token)
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                string modLibraryPath = SharedUtilities.GetSafeModLibraryPath();
                
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
                foreach (var dir in allModDirs)
                {
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
                    var modJsonPath = Path.Combine(dir, "mod.json");
                    var modName = Path.GetFileName(dir);
                    if (!File.Exists(modJsonPath)) { SafeIncrementSkip(); SafeAddSkippedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "NoModJson")}"); processed++; lock (_lockObject) { _progressValue = (double)processed / _totalMods; } NotifyProgressChanged(); continue; }
                    var json = File.ReadAllText(modJsonPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("url", out var urlProp) || urlProp.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(urlProp.GetString()) || !urlProp.GetString()!.Contains("gamebanana.com")) { SafeIncrementSkip(); SafeAddSkippedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "InvalidUrl")}"); processed++; lock (_lockObject) { _progressValue = (double)processed / _totalMods; } NotifyProgressChanged(); continue; }
                    string currentAuthor = root.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? string.Empty : string.Empty;
                    bool shouldUpdate = string.IsNullOrWhiteSpace(currentAuthor) || currentAuthor.Equals("unknown", StringComparison.OrdinalIgnoreCase);
                    
                    // For smart update, skip mods that already have known authors
                    if (IsSmartUpdate && !shouldUpdate)
                    {
                        SafeIncrementSkip();
                        SafeAddSkippedMod($"{modName}: Already has author ({currentAuthor})");
                        processed++;
                        lock (_lockObject) { _progressValue = (double)processed / _totalMods; }
                        NotifyProgressChanged();
                        continue;
                    }
                    
                    string url = urlProp.GetString()!;
                    if (string.IsNullOrWhiteSpace(url) || !url.Contains("gamebanana.com")) { _skip++; _skippedMods.Add($"{modName}: {SharedUtilities.GetTranslation(lang, "InvalidUrl")}"); processed++; _progressValue = (double)processed / _totalMods; NotifyProgressChanged(); continue; }
                    bool urlWorks = false;
                    try
                    {
                        var response = await _httpClient.GetAsync(url, token);
                        urlWorks = response.IsSuccessStatusCode;
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to check URL availability: {url}", ex);
                        urlWorks = false;
                    }
                    if (!urlWorks) { _fail++; _failedMods.Add($"{modName}: {SharedUtilities.GetTranslation(lang, "UrlUnavailable")}"); processed++; _progressValue = (double)processed / _totalMods; NotifyProgressChanged(); continue; }
                    try
                    {
                        var html = await FetchHtml(url, token);
                        var author = GetAuthorFromHtml(html);
                        if (!string.IsNullOrWhiteSpace(author))
                        {
                            if (IsFullUpdate)
                            {
                                // Full: count only if GameBanana author is different from current (i.e. we're changing value),
                                // or if it was empty/unknown and got updated
                                if (!author.Equals(currentAuthor, StringComparison.Ordinal))
                                {
                                    // Add path validation for security
                                    var modDirName = Path.GetFileName(dir);
                                    if (!SecurityValidator.IsValidModDirectoryName(modDirName))
                                    {
                                        SafeIncrementSkip();
                                        SafeAddSkippedMod($"{modName}: Invalid directory name");
                                        continue;
                                    }
                                    
                                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                                    dict["author"] = author;
                                    File.WriteAllText(modJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
                                    lock (_lockObject) { _success++; }
                                }
                                // if author is the same, don't count
                            }
                            else if (IsSmartUpdate)
                            {
                                // Smart: check ONLY mods with empty/unknown author
                                if (shouldUpdate)
                                {
                                    if (!string.IsNullOrWhiteSpace(author) && !author.Equals(currentAuthor, StringComparison.Ordinal))
                                    {
                                        // Add path validation for security
                                        var modDirName = Path.GetFileName(dir);
                                        if (!SecurityValidator.IsValidModDirectoryName(modDirName))
                                        {
                                            SafeIncrementSkip();
                                            SafeAddSkippedMod($"{modName}: Invalid directory name");
                                            continue;
                                        }
                                        
                                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                                        dict["author"] = author;
                                        File.WriteAllText(modJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
                                        lock (_lockObject) { _success++; }
                                    }
                                    // if not updated, don't count
                                }
                                // if not shouldUpdate, don't count and don't check
                            }
                        }
                        else
                        {
                            _skip++;
                        }
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to fetch author for {modName}", ex);
                        SafeIncrementFail();
                        SafeAddFailedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "AuthorFetchError")}");
                    }
                    processed++;
                    _progressValue = (double)processed / _totalMods;
                    NotifyProgressChanged();
                }
                
                // Successful completion - reset button state and show summary
                ResetButtonToUpdateState();
                string summary = string.Format(SharedUtilities.GetTranslation(lang, "SuccessCount"), _success) + "\n\n" +
                                SharedUtilities.GetTranslation(lang, "SkippedMods") + "\n" + (_skippedMods.Count > 0 ? string.Join("\n", _skippedMods) : SharedUtilities.GetTranslation(lang, "None")) +
                                "\n\n" + SharedUtilities.GetTranslation(lang, "Errors") + "\n" + (_failedMods.Count > 0 ? string.Join("\n", _failedMods) : SharedUtilities.GetTranslation(lang, "None")) +
                                "\n\n" + string.Format(SharedUtilities.GetTranslation(lang, "TotalChecked"), _totalMods);
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

        private async Task<string> FetchHtml(string url, CancellationToken token)
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            return await _httpClient.GetStringAsync(url, token);
        }

        private string? GetAuthorFromHtml(string html)
        {
            // Use compiled regex patterns for better performance
            foreach (var pattern in _authorPatterns)
            {
                var match = pattern.Match(html);
                if (match.Success) return match.Groups[1].Value.Trim();
            }
            
            // JSON-LD parsing using compiled regex
            var jsonLdMatch = _jsonLdPattern.Match(html);
            if (jsonLdMatch.Success)
            {
                try
                {
                    var json = jsonLdMatch.Groups[1].Value;
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("author", out var authorProp))
                    {
                        if (authorProp.ValueKind == JsonValueKind.Object && authorProp.TryGetProperty("name", out var nameProp))
                            return nameProp.GetString();
                        if (authorProp.ValueKind == JsonValueKind.String)
                            return authorProp.GetString();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("JSON-LD parsing failed", ex);
                }
            }
            return null;
        }

        private void UpdateProgressBarUI()
        {
            if (UpdateProgressBar != null)
            {
                if (_isUpdatingAuthors)
                {
                    UpdateProgressBar.Visibility = Visibility.Visible;
                    UpdateProgressBar.IsIndeterminate = false;
                    UpdateProgressBar.Value = _progressValue * 100;
                    // Ensure icons are in correct state during update
                    if (UpdateIcon != null && CancelIcon != null)
                    {
                        UpdateIcon.Visibility = Visibility.Collapsed;
                        CancelIcon.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    UpdateProgressBar.Value = 0;
                    UpdateProgressBar.IsIndeterminate = false;
                    UpdateProgressBar.Visibility = Visibility.Collapsed;
                    // Ensure icons are reset to default state when not updating
                    if (UpdateIcon != null && CancelIcon != null)
                    {
                        UpdateIcon.Visibility = Visibility.Visible;
                        CancelIcon.Visibility = Visibility.Collapsed;
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
        }

        public bool IsSmartUpdate => CurrentUpdateMode == UpdateMode.Smart;
        public bool IsFullUpdate => CurrentUpdateMode == UpdateMode.Full;





        public class ModJson
        {
            public string? author { get; set; }
            public string? url { get; set; }
            // ...other fields...
        }
    }
}
