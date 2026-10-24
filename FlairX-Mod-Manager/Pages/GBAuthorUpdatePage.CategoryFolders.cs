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

        public async Task CreateCategoryFoldersButtonClickAsync(Microsoft.UI.Xaml.XamlRoot? xamlRoot = null)
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
                XamlRoot = xamlRoot ?? this.XamlRoot
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
                        // Sanitize category name to remove illegal characters (including "/")
                        var normalizedCategoryName = SanitizeCategoryName(category.Name);
                        var categoryPath = Path.Combine(modsPath, normalizedCategoryName);
                        if (!Directory.Exists(categoryPath))
                        {
                            Directory.CreateDirectory(categoryPath);
                            Logger.LogInfo($"Created category folder: {normalizedCategoryName}");
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
    }
}
