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

namespace ZZZ_Mod_Manager_X.Pages
{
    public sealed partial class GBAuthorUpdatePage : Page
    {
        private Dictionary<string, string> _lang = new();
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
            UpdateAuthorsLabel.Text = T("UpdateAuthorsLabel");
            SmartUpdateLabel.Text = T("SmartUpdateLabel");
            SafetyLockLabel.Text = T("SafetyLockLabel");
            // Set constant button width to prevent resizing when text changes
            UpdateButtonText.Text = _isUpdatingAuthors ? T("CancelButton") : T("UpdateButton");
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
            LoadLanguage();
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

        private void LoadLanguage()
        {
            // Get selected language from manager
            var langFile = ZZZ_Mod_Manager_X.SettingsManager.Current.LanguageFile ?? "en.json";
            var langPath = Path.Combine(AppContext.BaseDirectory, "Language", "GBAuthorUpdate", langFile);
            if (!File.Exists(langPath)) langPath = Path.Combine(AppContext.BaseDirectory, "Language", "GBAuthorUpdate", "en.json");
            if (File.Exists(langPath))
            {
                try
                {
                    var json = File.ReadAllText(langPath);
                    _lang = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
                catch
                {
                    // If language file is corrupted, use empty dictionary (keys will be returned as-is)
                    _lang = new Dictionary<string, string>();
                }
            }
        }

        private string T(string key)
        {
            return _lang.TryGetValue(key, out var value) ? value : key;
        }



        // Thread-safe static fields
        private static bool _isUpdatingAuthors = false;
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
        private CancellationTokenSource? _cts;

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingAuthors)
            {
                _cts?.Cancel();
                _isUpdatingAuthors = false;
                NotifyProgressChanged();
                UpdateButtonText.Text = T("UpdateButton");
                UpdateIcon.Visibility = Visibility.Visible;
                CancelIcon.Visibility = Visibility.Collapsed;
                // Add immediate dialog after clicking Cancel
                var cancelDialog = new ContentDialog
                {
                    Title = T("CancelledTitle"),
                    Content = T("CancelledContent"),
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
                    Title = T("SafetyLockTitle"),
                    Content = T("SafetyLockContent"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }
            SafetyLockSwitch.IsOn = false;
            _cts = new CancellationTokenSource();
            UpdateButtonText.Text = T("CancelButton");
            UpdateIcon.Visibility = Visibility.Collapsed;
            CancelIcon.Visibility = Visibility.Visible;
            UpdateButton.IsEnabled = true;
            UpdateProgressBar.Visibility = Visibility.Visible;
            _isUpdatingAuthors = true;
            _success = 0; _fail = 0; _skip = 0;
            _skippedMods.Clear();
            _failedMods.Clear();
            _progressValue = 0;
            _totalMods = 0;
            NotifyProgressChanged();
            await UpdateAuthorsAsync(_cts.Token);
            UpdateButtonText.Text = T("UpdateButton");
            UpdateButton.IsEnabled = true;
        }

        private async Task UpdateAuthorsAsync(CancellationToken token)
        {
            try
            {
                string? modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory;
                if (string.IsNullOrWhiteSpace(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                var modDirs = Directory.GetDirectories(modLibraryPath);
                _totalMods = modDirs.Length;
                int processed = 0;
                foreach (var dir in modDirs)
                {
                    if (token.IsCancellationRequested)
                    {
                        _isUpdatingAuthors = false;
                        NotifyProgressChanged();
                        UpdateButtonText.Text = T("UpdateButton");
                        UpdateButton.IsEnabled = true;
                        UpdateProgressBar.Visibility = Visibility.Collapsed;
                        var cancelDialog = new ContentDialog
                        {
                            Title = T("CancelledTitle"),
                            Content = T("CancelledContent"),
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await cancelDialog.ShowAsync();
                        return;
                    }
                    var modJsonPath = Path.Combine(dir, "mod.json");
                    var modName = Path.GetFileName(dir);
                    if (!File.Exists(modJsonPath)) { _skip++; _skippedMods.Add($"{modName}: {T("NoModJson")}"); processed++; _progressValue = (double)processed / _totalMods; NotifyProgressChanged(); continue; }
                    var json = File.ReadAllText(modJsonPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("url", out var urlProp) || urlProp.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(urlProp.GetString()) || !urlProp.GetString()!.Contains("gamebanana.com")) { _skip++; _skippedMods.Add($"{modName}: {T("InvalidUrl")}"); processed++; _progressValue = (double)processed / _totalMods; NotifyProgressChanged(); continue; }
                    string currentAuthor = root.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? string.Empty : string.Empty;
                    bool shouldUpdate = string.IsNullOrWhiteSpace(currentAuthor) || currentAuthor.Equals("unknown", StringComparison.OrdinalIgnoreCase);
                    string url = urlProp.GetString()!;
                    if (string.IsNullOrWhiteSpace(url) || !url.Contains("gamebanana.com")) { _skip++; _skippedMods.Add($"{modName}: {T("InvalidUrl")}"); processed++; _progressValue = (double)processed / _totalMods; NotifyProgressChanged(); continue; }
                    bool urlWorks = false;
                    try
                    {
                        var response = await _httpClient.GetAsync(url, token);
                        urlWorks = response.IsSuccessStatusCode;
                    }
                    catch (OperationCanceledException) { return; }
                    catch { urlWorks = false; }
                    if (!urlWorks) { _fail++; _failedMods.Add($"{modName}: {T("UrlUnavailable")}"); processed++; _progressValue = (double)processed / _totalMods; NotifyProgressChanged(); continue; }
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
                                    if (!IsValidModDirectoryName(modDirName))
                                    {
                                        _skip++;
                                        _skippedMods.Add($"{modName}: Invalid directory name");
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
                                        if (!IsValidModDirectoryName(modDirName))
                                        {
                                            _skip++;
                                            _skippedMods.Add($"{modName}: Invalid directory name");
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
                    catch { _fail++; _failedMods.Add($"{modName}: {T("AuthorFetchError")}"); }
                    processed++;
                    _progressValue = (double)processed / _totalMods;
                    NotifyProgressChanged();
                }
                _isUpdatingAuthors = false;
                NotifyProgressChanged();
                string summary = string.Format(T("SuccessCount"), _success) + "\n\n" +
                                 T("SkippedMods") + "\n" + (_skippedMods.Count > 0 ? string.Join("\n", _skippedMods) : T("None")) +
                                 "\n\n" + T("Errors") + "\n" + (_failedMods.Count > 0 ? string.Join("\n", _failedMods) : T("None")) +
                                 "\n\n" + string.Format(T("TotalChecked"), _totalMods);
                var dialog = new ContentDialog
                {
                    Title = T("SummaryTitle"),
                    Content = summary,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (OperationCanceledException)
            {
                _isUpdatingAuthors = false;
                NotifyProgressChanged();
                UpdateButtonText.Text = T("UpdateButton");
                UpdateButton.IsEnabled = true;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                var dialog = new ContentDialog
                {
                    Title = T("CancelledTitle"),
                    Content = T("CancelledContent"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                _isUpdatingAuthors = false;
                NotifyProgressChanged();
                var dialog = new ContentDialog
                {
                    Title = T("ErrorTitle"),
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
                catch { /* JSON parsing failed - continue */ }
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
                }
                else
                {
                    UpdateProgressBar.Value = 0;
                    UpdateProgressBar.IsIndeterminate = false;
                    UpdateProgressBar.Visibility = Visibility.Collapsed;
                }
            }
            // Button deactivation removed
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

        // Validate mod directory name for security (same as in ModGridPage)
        private static bool IsValidModDirectoryName(string? directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName))
                return false;

            // Check for path traversal attempts
            if (directoryName.Contains("..") || directoryName.Contains("/") || directoryName.Contains("\\"))
                return false;

            // Check for absolute path attempts
            if (Path.IsPathRooted(directoryName))
                return false;

            // Check for invalid filename characters
            var invalidChars = Path.GetInvalidFileNameChars();
            if (directoryName.IndexOfAny(invalidChars) >= 0)
                return false;

            // Check for reserved names (Windows)
            var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            if (Array.IndexOf(reservedNames, directoryName.ToUpperInvariant()) >= 0)
                return false;

            return true;
        }



        public class ModJson
        {
            public string? author { get; set; }
            public string? url { get; set; }
            // ...other fields...
        }
    }
}
