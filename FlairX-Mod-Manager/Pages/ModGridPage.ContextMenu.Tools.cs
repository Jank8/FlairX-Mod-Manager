using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModGridPage
    {

        private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile)
            {
                // Create a fake button with the ModTile as Tag to reuse existing logic
                var fakeButton = new Button { Tag = modTile };
                DeleteModButton_Click(fakeButton, e);
            }
        }

        private async void ContextMenu_DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile && modTile.IsCategory)
                {
                    try
                    {
                        var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                        if (string.IsNullOrEmpty(modsPath))
                        {
                            modsPath = PathManager.GetModsPath();
                        }

                        var categoryPath = System.IO.Path.Combine(modsPath, modTile.Name);

                        if (!Directory.Exists(categoryPath))
                        {
                            Logger.LogWarning($"Category directory not found: {categoryPath}");
                            return;
                        }

                        // Count mods in category
                        int modCount = Directory.GetDirectories(categoryPath).Length;

                        // Show confirmation dialog
                        bool confirmed = await Dialogs.CategoryDeleteDialog.ShowAsync(modTile.Name, modCount, this.XamlRoot);

                        if (!confirmed)
                        {
                            return;
                        }

                        // Check if category is pinned and unpin it first
                        var gameTag = SettingsManager.CurrentSelectedGame;
                        if (!string.IsNullOrEmpty(gameTag))
                        {
                            var pinnedCategories = SettingsManager.GetPinnedCategories(gameTag);
                            if (pinnedCategories.Contains(modTile.Name))
                            {
                                SettingsManager.RemovePinnedCategory(gameTag, modTile.Name);
                                Logger.LogInfo($"Automatically unpinned category before deletion: {modTile.Name}");
                            }
                        }

                        // Delete the category directory
                        Directory.Delete(categoryPath, true);
                        Logger.LogInfo($"Deleted category: {modTile.Name} with {modCount} mods");

                        // Refresh the UI
                        var mainWindow = (Application.Current as App)?.MainWindow as MainWindow;
                        if (mainWindow != null)
                        {
                            // Regenerate menu
                            await mainWindow.GenerateModCharacterMenuAsync();

                            // Reload current view
                            if (CurrentViewMode == ViewMode.Categories)
                            {
                                LoadCategories();
                            }
                            else
                            {
                                // If we're viewing the deleted category, go back to all mods
                                if (_currentCategory == modTile.Name)
                                {
                                    LoadAllModsPublic();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to delete category: {modTile.Name}", ex);
                        await ShowErrorDialog($"Failed to delete category: {ex.Message}");
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModGridPage.ContextMenu_DeleteCategory_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModGridPage.ContextMenu_DeleteCategory_Click failed", ex);
            }
        }

        private void ContextMenu_PinUnpinCategory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile && modTile.IsCategory)
                {
                    try
                    {
                        var gameTag = SettingsManager.CurrentSelectedGame;
                        if (string.IsNullOrEmpty(gameTag))
                        {
                            Logger.LogWarning("No game selected, cannot pin/unpin category");
                            return;
                        }

                        var pinnedCategories = SettingsManager.GetPinnedCategories(gameTag);
                        bool isPinned = pinnedCategories.Contains(modTile.Name);

                        if (isPinned)
                        {
                            // Unpin category
                            SettingsManager.RemovePinnedCategory(gameTag, modTile.Name);
                            Logger.LogInfo($"Unpinned category: {modTile.Name}");
                        }
                        else
                        {
                            // Pin category
                            SettingsManager.AddPinnedCategory(gameTag, modTile.Name);
                            Logger.LogInfo($"Pinned category: {modTile.Name}");
                        }

                        // Defer the rebuild until the context flyout has closed. Rebuilding
                        // the NavigationView and the category grid inside the click that
                        // owns the flyout can tear WinUI down with a native empty-stack
                        // exception, which is exactly what happened after pin/unpin.
                        DispatcherQueue.TryEnqueue(
                            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                            async () =>
                            {
                                try
                                {
                                    var mainWindow = (Application.Current as App)?.MainWindow as MainWindow;
                                    if (mainWindow != null)
                                    {
                                        await mainWindow.GenerateModCharacterMenuAsync();
                                    }

                                    if (CurrentViewMode == ViewMode.Categories)
                                    {
                                        LoadCategories();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError($"Failed to refresh category UI after pin/unpin: {modTile.Name}", ex);
                                }
                            });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to pin/unpin category: {modTile.Name}", ex);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModGridPage.ContextMenu_PinUnpinCategory_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModGridPage.ContextMenu_PinUnpinCategory_Click failed", ex);
            }
        }

        private async void ContextMenu_CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile)
                {
                    var modUrl = GetModUrl(modTile);
                    if (!string.IsNullOrEmpty(modUrl))
                    {
                        // Check if it's a GameBanana URL
                        if (modUrl.Contains("gamebanana.com", StringComparison.OrdinalIgnoreCase))
                        {
                            // Extract mod ID from URL
                            var modId = FlairX_Mod_Manager.Services.GameBananaService.ExtractModIdFromUrl(modUrl);
                            if (!modId.HasValue)
                            {
                                Logger.LogWarning("Failed to extract mod ID from URL");
                                return;
                            }

                            // Get game tag from current game
                            var gameTag = SettingsManager.CurrentSelectedGame;
                            if (string.IsNullOrEmpty(gameTag))
                            {
                                Logger.LogWarning("No game tag found, cannot quick update");
                                return;
                            }

                            // Show loading notification
                            var lang = SharedUtilities.LoadLanguageDictionary();
                            if (App.Current is App _app && _app.MainWindow is MainWindow _mainWin)
                                _mainWin.ShowInfoBar("", SharedUtilities.GetTranslation(lang, "Loading") ?? "Loading...", Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational, 3000);

                            try
                            {
                                // Fetch mod details from GameBanana API
                                var modDetails = await Services.GameBananaService.GetModDetailsAsync(modId.Value);

                                if (modDetails == null || !modDetails.IsAvailable)
                                {
                                    Logger.LogError("Failed to get mod details from API or mod is unavailable");
                                    if (App.Current is App _a && _a.MainWindow is MainWindow _mw)
                                        _mw.ShowErrorInfo(SharedUtilities.GetTranslation(lang, "ConnectionErrorMessage") ?? "Connection error");
                                    return;
                                }

                                // Check if mod has files
                                if (modDetails.Files == null || modDetails.Files.Count == 0)
                                {
                                    Logger.LogWarning("Mod has no files available");
                                    if (App.Current is App _a && _a.MainWindow is MainWindow _mw)
                                        _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "NoFilesAvailable") ?? "No files available");
                                    return;
                                }

                                // Convert ModFile to GameBananaFileViewModel
                                var fileViewModels = modDetails.Files.Select(f => new Models.GameBananaFileViewModel
                                {
                                    Id = f.Id,
                                    FileName = f.FileName,
                                    Description = f.Description,
                                    FileSize = f.FileSize,
                                    DateAdded = f.DateAdded,
                                    DownloadCount = f.DownloadCount,
                                    DownloadUrl = f.DownloadUrl,
                                    IsSelected = false // User will select in dialog
                                }).ToList();

                                // Prepare data for dialog
                                var authorName = modDetails.Submitter?.Name ?? "unknown";
                                var dateUpdated = (modDetails.DateUpdated ?? 0) > 0 ? modDetails.DateUpdated ?? 0 : modDetails.DateAdded ?? 0;
                                var categoryName = modDetails.Category?.Name;
                                var modName = modDetails.Name;
                                var existingModPath = modTile.Directory; // Use current mod path

                                // Create and show file extraction dialog
                                var extractDialog = new Dialogs.GameBananaFileExtractionDialog(
                                    fileViewModels,
                                    modName,
                                    gameTag,
                                    modDetails.ProfileUrl,
                                    authorName,
                                    modDetails.Id,
                                    dateUpdated,
                                    categoryName,
                                    modDetails.PreviewMedia,
                                    modTile.IsNSFW,
                                    modDetails.Version,
                                    existingModPath,
                                    showFileSelection: true); // Quick Update shows file selection

                                // Set XamlRoot for dialog
                                if (App.Current is App _appx && _appx.MainWindow is MainWindow _mainWinx && _mainWinx.Content is FrameworkElement fe)
                                {
                                    extractDialog.XamlRoot = fe.XamlRoot;
                                }

                                // Track if mod was installed
                                bool modWasInstalled = false;
                                extractDialog.ModInstalled += (s, e) => { modWasInstalled = true; };

                                var result = await extractDialog.ShowAsync();

                                // Reload mod grid only if mod was actually installed
                                if (modWasInstalled)
                                {
                                    var mainWindow = (Application.Current as App)?.MainWindow as MainWindow;
                                    if (mainWindow != null)
                                    {
                                        await mainWindow.ReloadModsAsync();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Failed to quick update mod: {ex.Message}", ex);
                                if (App.Current is App _a && _a.MainWindow is MainWindow _mw)
                                    _mw.ShowErrorInfo(SharedUtilities.GetTranslation(lang, "Error_Generic") ?? "Error");
                            }
                        }
                        else
                        {
                            // For non-GameBanana URLs, open in external browser
                            try
                            {
                                if (!modUrl.StartsWith("http://") && !modUrl.StartsWith("https://"))
                                    modUrl = "https://" + modUrl;

                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = modUrl,
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError("Failed to open URL", ex);
                            }
                        }
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModGridPage.ContextMenu_CheckUpdates_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModGridPage.ContextMenu_CheckUpdates_Click failed", ex);
            }
        }

        private async void ContextMenu_RegenerateMinitile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile)
                {
                    try
                    {
                        var gameTag = SettingsManager.CurrentSelectedGame;
                        if (string.IsNullOrEmpty(gameTag))
                        {
                            Logger.LogWarning("No game selected");
                            return;
                        }

                        // Get mod path using the same logic as OpenFolder
                        var modLibraryDir = SettingsManager.GetCurrentXXMIModsDirectory();
                        var fullModPath = FindModFolderPath(modLibraryDir, modTile.Directory);

                        if (string.IsNullOrEmpty(fullModPath) || !Directory.Exists(fullModPath))
                        {
                            Logger.LogWarning($"Mod directory not found: {modTile.Directory}");
                            return;
                        }

                        // Show loading info
                        var lang = SharedUtilities.LoadLanguageDictionary();
                        if (App.Current is App _app && _app.MainWindow is MainWindow _mw)
                            _mw.ShowInfoBar("", SharedUtilities.GetTranslation(lang, "Loading") ?? "Loading...", Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational, 3000);

                        // Delete existing minitile files to force re-cropping
                        var ext = SettingsManager.GetImageExtension();
                        var filesToDelete = new[]
                        {
                                Path.Combine(fullModPath, $"minitile{ext}"),
                                Path.Combine(fullModPath, $"minitile.jpg"),
                                Path.Combine(fullModPath, $"minitile.webp"),
                                Path.Combine(fullModPath, $"minitile-wide{ext}"),
                                Path.Combine(fullModPath, $"minitile-wide.jpg"),
                                Path.Combine(fullModPath, $"minitile-wide.webp"),
                            };
                        foreach (var f in filesToDelete)
                            if (File.Exists(f)) File.Delete(f);

                        // Now regenerate - will trigger crop UI since minitiles are missing
                        await Services.ImageOptimizationService.ProcessModPreviewImagesAsync(
                            fullModPath,
                            Models.OptimizationMode.Standard);

                        // Refresh the tile image after regeneration
                        await Task.Delay(300); // Small delay to ensure files are written
                        RefreshModTileImage(fullModPath);

                        Logger.LogInfo($"Regenerated minitile for mod: {modTile.Directory}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to regenerate minitile", ex);
                        await ShowErrorDialog(SharedUtilities.GetTranslation(
                            SharedUtilities.LoadLanguageDictionary(), "Notify_RegenerateMinitileFailed"));
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModGridPage.ContextMenu_RegenerateMinitile_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModGridPage.ContextMenu_RegenerateMinitile_Click failed", ex);
            }
        }
        
        private async void ContextMenu_RegenerateThumbnails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile)
                {
                    try
                    {
                        var gameTag = SettingsManager.CurrentSelectedGame;
                        if (string.IsNullOrEmpty(gameTag))
                        {
                            Logger.LogWarning("No game selected");
                            return;
                        }

                        // Get category path
                        var modLibraryDir = SettingsManager.GetCurrentXXMIModsDirectory();
                        var categoryPath = Path.Combine(modLibraryDir, modTile.Name);

                        if (!Directory.Exists(categoryPath))
                        {
                            Logger.LogWarning($"Category directory not found: {categoryPath}");
                            return;
                        }

                        // Show loading info
                        var lang = SharedUtilities.LoadLanguageDictionary();
                        if (App.Current is App _app && _app.MainWindow is MainWindow _mw)
                            _mw.ShowInfoBar("", SharedUtilities.GetTranslation(lang, "Loading") ?? "Loading...", Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational, 3000);

                        // Regenerate category thumbnails (all 4 formats: catprev, catprev-wide, catmini, catmini-wide)
                        await Services.ImageOptimizationService.ProcessCategoryPreviewAsync(
                            categoryPath,
                            Models.OptimizationMode.CategoryFull);

                        // Refresh category view
                        await Task.Delay(500); // Small delay to ensure files are written
                        if (CurrentViewMode == ViewMode.Categories && _currentCategory == null)
                        {
                            LoadCategories();
                        }

                        Logger.LogInfo($"Regenerated thumbnails for category: {modTile.Name}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to regenerate category thumbnails", ex);
                        await ShowErrorDialog(SharedUtilities.GetTranslation(
                            SharedUtilities.LoadLanguageDictionary(), "Notify_RegenerateThumbnailsFailed"));
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModGridPage.ContextMenu_RegenerateThumbnails_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModGridPage.ContextMenu_RegenerateThumbnails_Click failed", ex);
            }
        }
    }
}
