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

            // Show "in progress" notification (no auto-close — stays until done)
            if (App.Current is App _appStart && _appStart.MainWindow is MainWindow _mwStart)
                _mwStart.ShowInfoBar("", SharedUtilities.GetTranslation(lang, "UpdateInProgress"),
                    Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational, autoCloseDelayMs: 0);

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
                
                var progress = new Progress<(int current, int total)>(p =>
                {
                    // Update progress on UI thread
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        lock (_lockObject)
                        {
                            _processedCount = p.current;
                            _totalCount = p.total;
                            // Calculate progress value (0.0 to 1.0)
                            _progressValue = p.total > 0 ? (double)p.current / p.total : 0;
                        }
                        NotifyProgressChanged();
                    });
                });
                
                var (success, failed, skipped, failedMods, skippedMods) = 
                    await Services.GameBananaAutoUpdateService.FetchAllDataAsync(token, silent: false, smartUpdate: IsSmartUpdate, progress: progress);

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
                    mainWindow.ShowSuccessInfo(SharedUtilities.GetTranslation(lang, "UpdateSuccess"));
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
                if (App.Current is App _app && _app.MainWindow is MainWindow _mw)
                    _mw.ShowErrorInfo(SharedUtilities.GetTranslation(lang, "UpdateFailed") + " " + ex.Message);
            }
        }
    }
}
