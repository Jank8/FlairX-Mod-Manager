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

        // Helper methods for image format
        private static bool IsImageFile(string filePath)
        {
            return filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
        }
        
        private static bool IsMinitileFile(string fileName)
        {
            var name = Path.GetFileName(fileName).ToLower();
            return name == "minitile.jpg" || name == "minitile.webp";
        }

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
            
            // Fetch all data card (formerly authors)
            FetchAuthorsTitle.Text = SharedUtilities.GetTranslation(lang, "FetchAuthorsButton");
            FetchAuthorsDescription.Text = SharedUtilities.GetTranslation(lang, "FetchAuthorsDescription");
            // Button text is now handled by UpdateButtonStates()
            

            
            // Auto-Update settings card
            AutoUpdateSettingsHeader.Text = SharedUtilities.GetTranslation(lang, "AutoUpdateSettingsHeader") ?? "Auto-Update Settings";
            AutoUpdateEnabledLabel.Text = SharedUtilities.GetTranslation(lang, "AutoUpdateEnabledLabel") ?? "Enable Auto-Update";
            AutoUpdateEnabledDescription.Text = SharedUtilities.GetTranslation(lang, "AutoUpdateEnabledDescription") ?? "Automatically fetch versions and dates on startup";
            AutoUpdateIntervalLabel.Text = SharedUtilities.GetTranslation(lang, "AutoUpdateIntervalLabel") ?? "Update Interval";
            AutoUpdateIntervalDescription.Text = SharedUtilities.GetTranslation(lang, "AutoUpdateIntervalDescription") ?? "How often to check for updates";
            
            // Fetch all previews card
            FetchAllPreviewsTitle.Text = SharedUtilities.GetTranslation(lang, "FetchAllPreviewsButton");
            FetchAllPreviewsDescription.Text = SharedUtilities.GetTranslation(lang, "FetchAllPreviewsButton_Tooltip");
            // Button text is now handled by UpdateButtonStates()
            
            // Fetch missing previews card
            FetchMissingPreviewsTitle.Text = SharedUtilities.GetTranslation(lang, "FetchMissingPreviewsButton");
            FetchMissingPreviewsDescription.Text = SharedUtilities.GetTranslation(lang, "FetchMissingPreviewsButton_Tooltip");
            // Button text is now handled by UpdateButtonStates()
            
            // Create category folders card
            CreateCategoryFoldersTitle.Text = SharedUtilities.GetTranslation(lang, "CreateCategoryFoldersButton");
            CreateCategoryFoldersDescription.Text = SharedUtilities.GetTranslation(lang, "CreateCategoryFoldersButton_Tooltip");
            // Button text is now handled by UpdateButtonStates()
            
            // Skip invalid URLs card
            SkipInvalidUrlsLabel.Text = SharedUtilities.GetTranslation(lang, "SkipInvalidUrlsLabel");
            SkipInvalidUrlsDescription.Text = SharedUtilities.GetTranslation(lang, "SkipInvalidUrlsDescription");
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
            if (SkipInvalidUrlsToggleLabel != null && SkipInvalidUrlsSwitch != null)
                SkipInvalidUrlsToggleLabel.Text = SkipInvalidUrlsSwitch.IsOn ? onText : offText;
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
            LoadSettings(); // Load auto-update settings
            ProgressChanged += OnProgressChanged;
            UpdateAuthorsSwitch.Toggled += UpdateAuthorsSwitch_Toggled;
            SmartUpdateSwitch.Toggled += SmartUpdateSwitch_Toggled;
            SkipInvalidUrlsSwitch.Toggled += SkipInvalidUrlsSwitch_Toggled;
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
            get { lock (_lockObject) { return _isFetchingVersions; } }
            private set { lock (_lockObject) { _isFetchingVersions = value; } }
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

        /// <summary>
        /// Mark a mod's URL as invalid in mod.json
        /// </summary>
        private static async Task MarkUrlAsInvalidAsync(string modJsonPath, CancellationToken token = default)
        {
            try
            {
                await Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                {
                    var currentJson = await File.ReadAllTextAsync(modJsonPath, token);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(currentJson) ?? new();
                    dict["urlInvalid"] = true;
                    await File.WriteAllTextAsync(modJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }), token);
                }, token);
                Logger.LogInfo($"Marked URL as invalid in: {modJsonPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to mark URL as invalid in {modJsonPath}", ex);
            }
        }

        /// <summary>
        /// Clear the invalid URL flag from mod.json
        /// </summary>
        private static async Task ClearUrlInvalidFlagAsync(string modJsonPath, CancellationToken token = default)
        {
            try
            {
                await Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                {
                    var currentJson = await File.ReadAllTextAsync(modJsonPath, token);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(currentJson) ?? new();
                    if (dict.ContainsKey("urlInvalid"))
                    {
                        dict.Remove("urlInvalid");
                        await File.WriteAllTextAsync(modJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }), token);
                        Logger.LogInfo($"Cleared invalid URL flag from: {modJsonPath}");
                    }
                }, token);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to clear invalid URL flag from {modJsonPath}", ex);
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
            bool anyOperationRunning = _isFetchingVersions || 
                                     _isFetchingAllPreviews || _isFetchingMissingPreviews || _isCreatingCategoryFolders;
            
            // Update All Data button (formerly Update Authors)
            if (UpdateButton != null && UpdateButtonText != null)
            {
                if (_isFetchingVersions)
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
            
            // Create Category Folders button
            if (CreateCategoryFoldersButton != null && CreateCategoryFoldersButtonText != null)
            {
                if (_isCreatingCategoryFolders)
                {
                    CreateCategoryFoldersButton.IsEnabled = true; // Keep enabled so user can cancel
                    CreateCategoryFoldersButtonText.Text = SharedUtilities.GetTranslation(mainLang, "Cancel");
                }
                else
                {
                    CreateCategoryFoldersButton.IsEnabled = !anyOperationRunning; // Disable if any other operation is running
                    CreateCategoryFoldersButtonText.Text = SharedUtilities.GetTranslation(lang, "Start");
                }
            }
        }

        private void UpdateProgressBarUI()
        {
            // Update button states - disable all buttons when any operation is running
            UpdateButtonStates();
            
            if (UpdateProgressBar != null)
            {
                if (_isFetchingVersions)
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
            
            if (CreateCategoryFoldersProgressBar != null)
            {
                if (_isCreatingCategoryFolders)
                {
                    CreateCategoryFoldersProgressBar.Visibility = Visibility.Visible;
                    CreateCategoryFoldersProgressBar.IsIndeterminate = false;
                    CreateCategoryFoldersProgressBar.Value = _progressValue * 100;
                    
                    // Update status text
                    if (CreateCategoryFoldersStatusText != null)
                    {
                        CreateCategoryFoldersStatusText.Visibility = Visibility.Visible;
                        if (!string.IsNullOrEmpty(_currentProcessingMod))
                        {
                            CreateCategoryFoldersStatusText.Text = _currentProcessingMod;
                        }
                        else
                        {
                            CreateCategoryFoldersStatusText.Text = "";
                        }
                    }
                }
                else
                {
                    CreateCategoryFoldersProgressBar.Value = 0;
                    CreateCategoryFoldersProgressBar.IsIndeterminate = false;
                    CreateCategoryFoldersProgressBar.Visibility = Visibility.Collapsed;
                    
                    if (CreateCategoryFoldersStatusText != null)
                    {
                        CreateCategoryFoldersStatusText.Visibility = Visibility.Collapsed;
                        CreateCategoryFoldersStatusText.Text = "";
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

        private void SkipInvalidUrlsSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateToggleLabels();
        }

        public bool IsSmartUpdate => CurrentUpdateMode == UpdateMode.Smart;
        public bool IsFullUpdate => CurrentUpdateMode == UpdateMode.Full;

        // Update All Data functionality (Authors + Versions + Dates)
        private CancellationTokenSource? _ctsUpdate;

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await UpdateButtonClickAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in UpdateButton_Click", ex);
                ResetUpdateButtonToFetchState();
            }
        }

        private async Task UpdateButtonClickAsync()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            var mainLang = SharedUtilities.LoadLanguageDictionary();

            if (_isFetchingVersions)
            {
                _ctsUpdate?.Cancel();
                _isFetchingVersions = false;
                ResetUpdateButtonToFetchState();
                if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "CancelledContent"));
                return;
            }

            // Add confirmation dialog
            var confirmDialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(lang, "FetchAuthorsButton"),
                Content = SharedUtilities.GetTranslation(lang, "ConfirmFetchAllData"),
                PrimaryButtonText = SharedUtilities.GetTranslation(mainLang, "Continue"),
                CloseButtonText = SharedUtilities.GetTranslation(mainLang, "Cancel"),
                XamlRoot = this.XamlRoot
            };
            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            _ctsUpdate = new CancellationTokenSource();
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
            await UpdateAllDataAsync(_ctsUpdate.Token);
            ResetUpdateButtonToFetchState();
        }

        private void ResetUpdateButtonToFetchState()
        {
            _isFetchingVersions = false;
            NotifyProgressChanged();
        }

        private async Task UpdateAllDataAsync(CancellationToken token)
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                
                var (success, failed, skipped, failedMods, skippedMods) = 
                    await Services.GameBananaAutoUpdateService.FetchAllDataAsync(token, silent: false, smartUpdate: IsSmartUpdate);

                lock (_lockObject)
                {
                    _success = success;
                    _fail = failed;
                    _skip = skipped;
                    _failedMods.Clear();
                    _failedMods.AddRange(failedMods);
                    _skippedMods.Clear();
                    _skippedMods.AddRange(skippedMods);
                }

                if (token.IsCancellationRequested)
                {
                    ResetUpdateButtonToFetchState();
                    if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "CancelledContent"));
                    return;
                }

                // Save last run date and refresh the display
                SettingsManager.Current.GameBananaLastAutoUpdate = DateTime.Now;
                SettingsManager.Save();
                UpdateLastRunText();

                ResetUpdateButtonToFetchState();
                string summary = string.Format(SharedUtilities.GetTranslation(lang, "TotalChecked"), _success + _fail + _skip) + "\n" +
                                string.Format(SharedUtilities.GetTranslation(lang, "SuccessCount"), _success) + "\n" +
                                string.Format(SharedUtilities.GetTranslation(lang, "SkippedCount"), _skip);

                // For smart update, only show skipped items with errors/issues (not "already has author")
                // For full update, show all skipped items
                List<string> skippedToShow = new List<string>();
                if (IsSmartUpdate)
                {
                    // Smart update: only show skipped items that are NOT "already has author"
                    string alreadyHasAuthorText = SharedUtilities.GetTranslation(lang, "AlreadyHasAuthor");
                    foreach (var skippedMod in _skippedMods)
                    {
                        if (!skippedMod.Contains(alreadyHasAuthorText))
                        {
                            skippedToShow.Add(skippedMod);
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
                    Title = SharedUtilities.GetTranslation(lang, "AllDataSummaryTitle"),
                    Content = scrollViewer,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                
                // Reload mods to update ModListManager lists
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    Logger.LogInfo("Reloading mods after all data update");
                    await mainWindow.ReloadModsAsync();
                    Logger.LogInfo("Mods reloaded successfully");
                }
            }
            catch (OperationCanceledException)
            {
                ResetUpdateButtonToFetchState();
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "CancelledContent"));
            }
            catch (Exception ex)
            {
                ResetUpdateButtonToFetchState();
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(ex.Message);
            }
        }

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

                            // Check if URL is marked as invalid and skip if option is enabled
                            if (SkipInvalidUrlsSwitch.IsOn && root.TryGetProperty("urlInvalid", out var urlInvalidProp) && 
                                urlInvalidProp.ValueKind == JsonValueKind.True)
                            {
                                SafeIncrementSkip();
                                SafeAddSkippedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "UrlUnavailable")}");
                                Interlocked.Increment(ref processed);
                                lock (_lockObject) { _progressValue = (double)processed / _totalMods; }
                                SafeSetCurrentProcessingMod("");
                                NotifyProgressChanged();
                                return;
                            }

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
                                // Clear any previous invalid URL flag since we successfully fetched data
                                await ClearUrlInvalidFlagAsync(modJsonPath, token);
                                
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
                            else
                            {
                                // Mark URL as invalid when we can't download previews
                                await MarkUrlAsInvalidAsync(modJsonPath, token);
                                SafeIncrementSkip();
                                SafeAddSkippedMod($"{modName}: {SharedUtilities.GetTranslation(lang, "UrlUnavailable")}");
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to fetch previews for {modName}", ex);
                            // Mark URL as invalid on any fetch error
                            await MarkUrlAsInvalidAsync(modJsonPath, token);
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

        // Create Category Folders functionality
        private static volatile bool _isCreatingCategoryFolders = false;
        private CancellationTokenSource? _ctsCategoryFolders;

        private async void CreateCategoryFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await CreateCategoryFoldersButtonClickAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in CreateCategoryFoldersButton_Click", ex);
                ResetCategoryFoldersButtonToCreateState();
            }
        }

        private async Task CreateCategoryFoldersButtonClickAsync()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            var mainLang = SharedUtilities.LoadLanguageDictionary();

            if (_isCreatingCategoryFolders)
            {
                _ctsCategoryFolders?.Cancel();
                _isCreatingCategoryFolders = false;
                ResetCategoryFoldersButtonToCreateState();
                if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "CancelledContent"));
                return;
            }

            // Get current game tag
            var gameTag = SettingsManager.CurrentSelectedGame;
            if (string.IsNullOrEmpty(gameTag))
            {
                if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(SharedUtilities.GetTranslation(lang, "NoGameSelected"));
                return;
            }

            // Check if game has character categories defined
            var categoryId = Services.GameBananaService.GetCharacterCategoryId(gameTag);
            if (categoryId == 0)
            {
                if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(string.Format(SharedUtilities.GetTranslation(lang, "CategoryNotSupported"), gameTag));
                return;
            }

            // Add confirmation dialog
            var confirmDialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(lang, "CreateCategoryFoldersButton"),
                Content = string.Format(SharedUtilities.GetTranslation(lang, "ConfirmCreateCategoryFolders"), gameTag),
                PrimaryButtonText = SharedUtilities.GetTranslation(mainLang, "Continue"),
                CloseButtonText = SharedUtilities.GetTranslation(mainLang, "Cancel"),
                XamlRoot = this.XamlRoot
            };
            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            _ctsCategoryFolders = new CancellationTokenSource();
            _isCreatingCategoryFolders = true;
            lock (_lockObject)
            {
                _success = 0; _fail = 0; _skip = 0;
                _skippedMods.Clear();
                _failedMods.Clear();
                _progressValue = 0;
                _totalMods = 0;
                _currentProcessingMod = "";
            }
            NotifyProgressChanged();
            await CreateCategoryFoldersAsync(gameTag, _ctsCategoryFolders.Token);
            ResetCategoryFoldersButtonToCreateState();
        }

        private void ResetCategoryFoldersButtonToCreateState()
        {
            _isCreatingCategoryFolders = false;
            NotifyProgressChanged();
        }

        private async Task CreateCategoryFoldersAsync(string gameTag, CancellationToken token)
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");

                // Fetch character categories
                var categories = await Services.GameBananaService.GetCharacterCategoriesAsync(gameTag);
                if (categories == null || categories.Count == 0)
                {
                    if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(SharedUtilities.GetTranslation(lang, "FailedToFetchCategories"));
                    return;
                }

                _totalMods = categories.Count;
                int processed = 0;

                // Get mods path
                string modsPath = SharedUtilities.GetSafeXXMIModsPath();

                // Process each category
                foreach (var category in categories)
                {
                    if (token.IsCancellationRequested) break;

                    SafeSetCurrentProcessingMod(category.Name);
                    NotifyProgressChanged();

                    try
                    {
                        // Create category folder
                        var categoryPath = Path.Combine(modsPath, category.Name);
                        if (!Directory.Exists(categoryPath))
                        {
                            Directory.CreateDirectory(categoryPath);
                            Logger.LogInfo($"Created category folder: {category.Name}");
                        }

                        // Download icon if URL is available
                        var iconUrl = category.GetIconUrl();
                        if (!string.IsNullOrEmpty(iconUrl))
                        {
                            var success = await Services.GameBananaService.DownloadCategoryIconAsync(iconUrl, categoryPath);
                            if (success)
                            {
                                SafeIncrementSuccess();
                                Logger.LogInfo($"Downloaded icon for category: {category.Name}");
                            }
                            else
                            {
                                SafeIncrementFail();
                                SafeAddFailedMod($"{category.Name}: {SharedUtilities.GetTranslation(lang, "FailedToDownloadIcon")}");
                            }
                        }
                        else
                        {
                            SafeIncrementSkip();
                            SafeAddSkippedMod($"{category.Name}: {SharedUtilities.GetTranslation(lang, "NoIconUrlAvailable")}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to create category folder for {category.Name}", ex);
                        SafeIncrementFail();
                        SafeAddFailedMod($"{category.Name}: {ex.Message}");
                    }

                    processed++;
                    lock (_lockObject) { _progressValue = (double)processed / _totalMods; }
                    NotifyProgressChanged();
                }

                if (token.IsCancellationRequested)
                {
                    ResetCategoryFoldersButtonToCreateState();
                    if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "CancelledContent"));
                    return;
                }

                // Show summary
                ResetCategoryFoldersButtonToCreateState();
                string summary = string.Format(SharedUtilities.GetTranslation(lang, "TotalCategories"), _totalMods) + "\n" +
                                string.Format(SharedUtilities.GetTranslation(lang, "Success"), _success) + "\n" +
                                string.Format(SharedUtilities.GetTranslation(lang, "Failed"), _fail) + "\n" +
                                string.Format(SharedUtilities.GetTranslation(lang, "Skipped"), _skip);

                if (_failedMods.Count > 0 || _skippedMods.Count > 0)
                {
                    var allIssues = new List<string>();
                    allIssues.AddRange(_failedMods);
                    allIssues.AddRange(_skippedMods);
                    summary += "\n\n" + SharedUtilities.GetTranslation(lang, "Issues") + "\n" + string.Join("\n", allIssues);
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
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    MaxHeight = 400,
                    Padding = new Microsoft.UI.Xaml.Thickness(10)
                };

                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "CategoryFoldersCreatedTitle"),
                    Content = scrollViewer,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();

                // Reload mods to show new folders
                try
                {
                    var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        Logger.LogInfo("Reloading mods after category folders creation");
                        await mainWindow.ReloadModsAsync();
                        Logger.LogInfo("Mods reloaded successfully");
                    }
                }
                catch (Exception reloadEx)
                {
                    Logger.LogError("Failed to reload mods after category folders creation", reloadEx);
                }
            }
            catch (OperationCanceledException)
            {
                ResetCategoryFoldersButtonToCreateState();
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "CancelledContent"));
            }
            catch (Exception ex)
            {
                ResetCategoryFoldersButtonToCreateState();
                var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
                if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(ex.Message);
            }
        }

        private void LoadSettings()
        {
            AutoUpdateEnabledSwitch.IsOn = SettingsManager.Current.GameBananaAutoUpdateEnabled;
            
            // Set interval TextBox
            var intervalDays = SettingsManager.Current.GameBananaAutoUpdateIntervalDays;
            AutoUpdateIntervalTextBox.Text = intervalDays.ToString();
            
            // Show last auto-update run date
            UpdateLastRunText();
            
            SkipInvalidUrlsSwitch.IsOn = SettingsManager.Current.GameBananaSkipInvalidUrls;
        }

        private void UpdateLastRunText()
        {
            var lastRun = SettingsManager.Current.GameBananaLastAutoUpdate;
            AutoUpdateLastRunText.Text = lastRun == DateTime.MinValue
                ? ""
                : lastRun.ToString("g");
        }

        private void AutoUpdateEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.GameBananaAutoUpdateEnabled = AutoUpdateEnabledSwitch.IsOn;
            SettingsManager.Save();
        }

        private void AutoUpdateIntervalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Only validate, don't save automatically
            if (int.TryParse(AutoUpdateIntervalTextBox.Text, out int days) && days >= 1 && days <= 365)
            {
                // Remove any error styling
                AutoUpdateIntervalTextBox.BorderBrush = null;
                AutoUpdateIntervalConfirmButton.IsEnabled = true;
            }
            else if (!string.IsNullOrEmpty(AutoUpdateIntervalTextBox.Text))
            {
                // Show error styling for invalid values
                AutoUpdateIntervalTextBox.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                AutoUpdateIntervalConfirmButton.IsEnabled = false;
            }
            else
            {
                // Empty text - disable confirm button
                AutoUpdateIntervalTextBox.BorderBrush = null;
                AutoUpdateIntervalConfirmButton.IsEnabled = false;
            }
        }

        private void AutoUpdateIntervalConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(AutoUpdateIntervalTextBox.Text, out int days) && days >= 1 && days <= 365)
            {
                SettingsManager.Current.GameBananaAutoUpdateIntervalDays = days;
                SettingsManager.Save();
                
                // Visual feedback - briefly change button appearance
                var button = sender as Button;
                if (button?.Content is TextBlock textBlock)
                {
                    var originalText = textBlock.Text;
                    textBlock.Text = "✓"; // Keep checkmark
                    
                    // Reset after short delay
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
                    timer.Tick += (s, args) =>
                    {
                        textBlock.Text = originalText;
                        timer.Stop();
                    };
                    timer.Start();
                }
            }
        }
    }
}