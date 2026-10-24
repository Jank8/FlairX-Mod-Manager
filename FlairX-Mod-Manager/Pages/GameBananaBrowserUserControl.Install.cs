using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class GameBananaBrowserUserControl
    {

        private async void DetailDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentModDetails == null) return;

                var selectedFiles = _detailFiles.Where(f => f.IsSelected).ToList();

                if (selectedFiles.Count == 0)
                {
                    if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(SharedUtilities.GetTranslation(_lang, "SelectAtLeastOneFile"));
                    return;
                }

                // FORCE REFRESH: Always get fresh mod details from API before installation
                // This ensures we have the correct category and mod name even when navigating through author links
                Logger.LogInfo($"Refreshing mod details from API before installation for mod ID: {_currentModDetails.Id}");
                var freshModDetails = await GameBananaService.GetModDetailsAsync(_currentModDetails.Id);

                if (freshModDetails == null || !freshModDetails.IsAvailable)
                {
                    Logger.LogError("Failed to get fresh mod details from API or mod is unavailable");
                    if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage"));
                    return;
                }

                // UPDATE _currentModDetails with fresh data so dialog logic works correctly
                _currentModDetails = freshModDetails;

                // Use fresh data from API for installation dialog
                var authorName = freshModDetails.Submitter?.Name ?? "unknown";
                var dateUpdated = (freshModDetails.DateUpdated ?? 0) > 0 ? freshModDetails.DateUpdated ?? 0 : freshModDetails.DateAdded ?? 0;
                var categoryName = freshModDetails.Category?.Name; // Fresh category from API
                var modName = freshModDetails.Name; // Fresh mod name from API

                Logger.LogInfo($"Using fresh API data - Mod: '{modName}', Category: '{categoryName}', Author: '{authorName}'");

                // Use source mod path if provided (from mod library), otherwise search for installed mod
                var existingModPath = _sourceModPath;
                if (string.IsNullOrEmpty(existingModPath) && !string.IsNullOrEmpty(freshModDetails.ProfileUrl))
                {
                    existingModPath = GetInstalledModPath(freshModDetails.ProfileUrl);
                }

                var extractDialog = new Dialogs.GameBananaFileExtractionDialog(
                    selectedFiles,
                    modName, // Use fresh mod name from API
                    _gameTag,
                    freshModDetails.ProfileUrl,
                    authorName,
                    freshModDetails.Id,
                    dateUpdated,
                    categoryName, // Use fresh category from API
                    freshModDetails.PreviewMedia,
                    _currentModIsNSFW, // Use NSFW status from mod list (not available in details API)
                    freshModDetails.Version, // Pass version from API (_sVersion)
                    existingModPath); // Pass existing mod path if installed
                extractDialog.XamlRoot = XamlRoot;

                var installationCompleted = false;
                EventHandler<Dialogs.GameBananaFileExtractionDialog.ModInstalledEventArgs> installedHandler =
                    (dialogSender, eventArgs) =>
                    {
                        installationCompleted = true;
                        OnModInstalled(dialogSender, eventArgs);
                    };

                extractDialog.ModInstalled += installedHandler;
                try
                {
                    await extractDialog.ShowAsync();
                }
                finally
                {
                    extractDialog.ModInstalled -= installedHandler;
                }

                if (installationCompleted)
                {
                    await NavigateBackAsync();
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.DetailDownloadButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.DetailDownloadButton_Click failed", ex);
            }
        }
        
        private void OnModInstalled(object? sender, Dialogs.GameBananaFileExtractionDialog.ModInstalledEventArgs e)
        {
            // Track that a mod was installed
            _modWasInstalled = true;
            
            // Find the mod in the list and update IsInstalled
            DispatcherQueue.TryEnqueue(() =>
            {
                var mod = _mods.FirstOrDefault(m => m.ProfileUrl == e.ModProfileUrl || m.Id == e.ModId);
                if (mod != null)
                {
                    mod.IsInstalled = true;
                    Logger.LogInfo($"Updated IsInstalled for mod: {mod.Name}");
                }
            });
            
            // Update persistent mod lists based on mod.json properties
            if (!string.IsNullOrEmpty(e.ModPath) && Directory.Exists(e.ModPath))
            {
                try
                {
                    var modJsonPath = Path.Combine(e.ModPath, "mod.json");
                    if (File.Exists(modJsonPath))
                    {
                        var json = Services.FileAccessQueue.ReadAllText(modJsonPath);
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        
                        var isNSFW = root.TryGetProperty("isNSFW", out var nsfwProp) && nsfwProp.ValueKind == System.Text.Json.JsonValueKind.True;
                        var isBroken = root.TryGetProperty("modBroken", out var brokenProp) && brokenProp.ValueKind == System.Text.Json.JsonValueKind.True;
                        
                        // Check for updates
                        bool hasUpdate = false;
                        if (root.TryGetProperty("gbChangeDate", out var gbChangeProp) && gbChangeProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var gbChangeDateStr = gbChangeProp.GetString();
                            if (root.TryGetProperty("dateUpdated", out var dateUpdatedProp) && dateUpdatedProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                if (DateTime.TryParse(gbChangeDateStr, out var gbDate) && DateTime.TryParse(dateUpdatedProp.GetString(), out var lastUpdated))
                                {
                                    hasUpdate = gbDate > lastUpdated;
                                }
                            }
                        }
                        
                        var modDirName = Path.GetFileName(e.ModPath);
                        var isActive = !modDirName.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase);
                        var cleanName = isActive ? modDirName : modDirName.Substring(9);
                        
                        // Extract category from path
                        var categoryName = "Other";
                        try
                        {
                            var parent = Directory.GetParent(e.ModPath);
                            if (parent != null) categoryName = parent.Name;
                        }
                        catch { }
                        
                        ModListManager.UpdateModInLists(categoryName, cleanName, isActive, isNSFW, isBroken, hasUpdate);
                        Logger.LogInfo($"Updated persistent lists for installed mod: {cleanName} (Category: {categoryName}, Active: {isActive}, NSFW: {isNSFW}, Broken: {isBroken}, HasUpdate: {hasUpdate})");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to update persistent lists for installed mod: {e.ModPath}", ex);
                }
            }
        }

        private async void DetailDownloadPreviewsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentModDetails == null) return;

                try
                {
                    // Check if mod is installed
                    if (string.IsNullOrEmpty(_currentModDetails.ProfileUrl) || !IsModInstalled(_currentModDetails.ProfileUrl))
                    {
                        Logger.LogInfo("Cannot download previews: mod is not installed");
                        return;
                    }

                    // Get installed mod path
                    var installedModPath = GetInstalledModPath(_currentModDetails.ProfileUrl);
                    if (string.IsNullOrEmpty(installedModPath))
                    {
                        Logger.LogError("Cannot download previews: installed mod path not found");
                        return;
                    }

                    // Get fresh mod details from API
                    Logger.LogInfo($"Downloading previews for mod ID: {_currentModDetails.Id}");
                    var freshModDetails = await GameBananaService.GetModDetailsAsync(_currentModDetails.Id);

                    if (freshModDetails == null || !freshModDetails.IsAvailable)
                    {
                        Logger.LogError("Failed to get fresh mod details from API or mod is unavailable");
                        if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage"));
                        return;
                    }

                    // Check if previews are available
                    if (freshModDetails.PreviewMedia?.Images == null || !freshModDetails.PreviewMedia.Images.Any(img => img.Type == "screenshot"))
                    {
                        Logger.LogInfo("No preview images available for this mod");
                        return;
                    }

                    // Use fresh data from API
                    var authorName = freshModDetails.Submitter?.Name ?? "unknown";
                    var categoryName = freshModDetails.Category?.Name ?? "Unknown";
                    var modName = freshModDetails.Name ?? "Unknown Mod";

                    // Create installation dialog in preview-only mode
                    var extractDialog = new Dialogs.GameBananaFileExtractionDialog(
                        new List<Models.GameBananaFileViewModel>(), // Empty file list for preview-only mode
                        modName,
                        _gameTag,
                        freshModDetails.ProfileUrl,
                        authorName,
                        freshModDetails.Id,
                        freshModDetails.DateUpdated ?? freshModDetails.DateAdded ?? 0,
                        categoryName,
                        freshModDetails.PreviewMedia,
                        _currentModIsNSFW,
                        freshModDetails.Version,
                        installedModPath); // Pass installed mod path for preview-only installation

                    extractDialog.XamlRoot = XamlRoot;

                    // Show dialog - in preview-only mode, secondary button is the main action
                    var result = await extractDialog.ShowAsync();

                    if (result == ContentDialogResult.Secondary) // Secondary button is "Download Previews Only"
                    {
                        // Refresh the current image display if we're still viewing this mod
                        if (_currentModDetails?.Id == freshModDetails.Id)
                        {
                            // Reload preview images to show newly downloaded ones
                            await Task.Delay(1000); // Give time for files to be written
                            LoadCurrentDetailImage();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error downloading previews from browser", ex);
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.DetailDownloadPreviewsButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.DetailDownloadPreviewsButton_Click failed", ex);
            }
        }
    }
}
