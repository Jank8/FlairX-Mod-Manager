using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlairX_Mod_Manager.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace FlairX_Mod_Manager.Dialogs
{
    public partial class GameBananaFileExtractionDialog
    {

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                // If password panel is active, this click submits the password
                if (TrySubmitPassword())
                {
                    args.Cancel = true;
                    return;
                }

                // Check if this is preview-only mode (empty file list)
                bool isPreviewOnlyMode = _selectedFiles.Count == 0;

                if (isPreviewOnlyMode)
                {
                    // In preview-only mode, primary button is Cancel
                    Hide();
                    return;
                }

                // Prevent dialog from closing immediately
                args.Cancel = true;

                try
                {
                    IsPrimaryButtonEnabled = false;
                    IsSecondaryButtonEnabled = false;

                    var category = _categoryComboBox.Text.Trim();
                    // Validation is handled by ValidateInputs() - button should be disabled if invalid
                    if (string.IsNullOrWhiteSpace(category) || IsReservedWindowsName(category))
                    {
                        IsPrimaryButtonEnabled = true;
                        IsSecondaryButtonEnabled = true;
                        return;
                    }

                    var modName = _modNameTextBox.Text.Trim();
                    if (IsReservedWindowsName(modName))
                    {
                        IsPrimaryButtonEnabled = true;
                        IsSecondaryButtonEnabled = true;
                        return;
                    }

                    // Determine folder name: sanitize to prevent illegal characters
                    string categoryFolderName = SanitizeCategoryName(category);

                    // Download files (only selected ones)
                    var tempDir = PathManager.GetTempDirectory("Downloads");

                    // Clean up if folder exists from previous interrupted session
                    if (Directory.Exists(tempDir))
                    {
                        try
                        {
                            Directory.Delete(tempDir, true);
                        }
                        catch (Exception cleanEx)
                        {
                            Logger.LogWarning($"Failed to cleanup existing temp dir: {cleanEx.Message}");
                        }
                    }

                    Directory.CreateDirectory(tempDir);
                    _currentTempDir = tempDir; // Store for cleanup

                    var downloadedFiles = new List<(string filePath, string fileName)>();

                    // Filter to only selected files
                    var filesToDownload = _selectedFiles.Where(f => f.IsSelected).ToList();

                    for (int i = 0; i < filesToDownload.Count; i++)
                    {
                        var file = filesToDownload[i];
                        _downloadStatusText.Text = string.Format(SharedUtilities.GetTranslation(_lang, "DownloadingFile"),
                            file.FileName, i + 1, filesToDownload.Count);

                        var tempFilePath = Path.Combine(tempDir, file.FileName);
                        var progress = new Progress<double>(value =>
                        {
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                _downloadProgressBar.Value = value;
                            });
                        });

                        var success = await GameBananaService.DownloadFileAsync(file.DownloadUrl, tempFilePath, progress);

                        if (!success)
                        {
                            await ShowError(string.Format(SharedUtilities.GetTranslation(_lang, "FailedToDownload"), file.FileName));
                            try
                            {
                                if (_currentTempDir != null && Directory.Exists(_currentTempDir))
                                    Directory.Delete(_currentTempDir, true);
                            }
                            catch { }
                            finally
                            {
                                _currentTempDir = null;
                            }
                            IsPrimaryButtonEnabled = true;
                            IsSecondaryButtonEnabled = true;
                            return;
                        }

                        _downloadProgressBar.Value = 100;
                        downloadedFiles.Add((tempFilePath, file.FileName));
                    }

                    // Extract archives based on count
                    bool hasArchives = downloadedFiles.Any(f => IsArchiveFile(f.fileName));

                    if (hasArchives)
                    {
                        if (filesToDownload.Count == 1)
                        {
                            // Single file - extract directly to mod folder (old behavior)
                            _downloadedArchivePath = downloadedFiles[0].filePath;
                            await ExtractArchiveAsync(categoryFolderName);
                        }
                        else
                        {
                            // Multiple files - extract each archive to its own subfolder
                            await ExtractMultipleArchivesAsync(categoryFolderName, downloadedFiles);
                        }
                    }
                    else
                    {
                        // No archives, just move files
                        await InstallFiles(tempDir, categoryFolderName);
                    }

                    // Download preview images if enabled
                    if (_downloadPreviews && _previewMedia?.Images != null && !string.IsNullOrEmpty(_installedModPath))
                    {
                        await DownloadPreviewImagesAsync(_installedModPath);
                    }

                    // Cleanup downloaded files after successful installation
                    try
                    {
                        if (_currentTempDir != null && Directory.Exists(_currentTempDir))
                        {
                            Directory.Delete(_currentTempDir, true);
                            Logger.LogInfo($"Cleaned up temp download directory: {_currentTempDir}");
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        Logger.LogWarning($"Failed to cleanup temp directory: {cleanupEx.Message}");
                    }
                    finally
                    {
                        _currentTempDir = null;
                    }

                    // Success
                    _downloadStatusText.Text = SharedUtilities.GetTranslation(_lang, "DownloadComplete");
                    _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractComplete");
                    _downloadProgressBar.Value = 100;
                    _extractProgressBar.Value = 100;
                    await Task.Delay(1000);

                    // Fire event to notify that mod was installed - only if installation was successful
                    if (!string.IsNullOrEmpty(_installedModPath) && Directory.Exists(_installedModPath))
                    {
                        Logger.LogInfo($"[DIALOG] Firing ModInstalled event for: {_installedModPath}");
                        ModInstalled?.Invoke(this, new ModInstalledEventArgs
                        {
                            ModProfileUrl = _modProfileUrl,
                            ModId = _modId,
                            ModPath = _installedModPath,
                            Category = _categoryComboBox.Text.Trim()
                        });
                        Logger.LogInfo($"[DIALOG] ModInstalled event fired");
                    }
                    else
                    {
                        await ShowError(SharedUtilities.GetTranslation(_lang, "InstallationFailed"));
                        IsPrimaryButtonEnabled = true;
                        IsSecondaryButtonEnabled = true;
                        return;
                    }

                    Logger.LogInfo($"[DIALOG] Closing dialog with Hide()");
                    // Close dialog - optimization will run in background after dialog closes
                    Hide();
                    Logger.LogInfo($"[DIALOG] Dialog closed");

                    // Run optimization AFTER dialog closes (on UI thread for crop panel support)
                    if (_downloadPreviews && !string.IsNullOrEmpty(_installedModPath))
                    {
                        Logger.LogInfo($"[DIALOG] Starting OptimizeDownloadedPreviewsAsync for: {_installedModPath}");
                        // Don't use Task.Run - we need to stay on UI thread for crop panel
                        _ = OptimizeDownloadedPreviewsAsync(_installedModPath, showSuccessInfoBar: true);
                    }
                    else
                    {
                        // No previews to download - show InfoBar after dialog animation
                        if (!string.IsNullOrEmpty(_installedModPath))
                        {
                            var installedModName = Path.GetFileName(_installedModPath);
                            if (installedModName.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase))
                                installedModName = installedModName.Substring(9);
                            await Task.Delay(300);
                            if (App.Current is App app2 && app2.MainWindow is MainWindow mw2)
                                mw2.ShowSuccessInfo(installedModName);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // User cancelled password entry — show info and re-enable buttons
                    try
                    {
                        if (_currentTempDir != null && Directory.Exists(_currentTempDir))
                            Directory.Delete(_currentTempDir, true);
                    }
                    catch { }
                    finally
                    {
                        _currentTempDir = null;
                    }
                    _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ArchivePassword_Cancelled");
                    _extractProgressBar.IsIndeterminate = false;
                    _extractProgressBar.Value = 0;
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                }
                catch (AggregateException ae) when (ae.InnerException is OperationCanceledException)
                {
                    try
                    {
                        if (_currentTempDir != null && Directory.Exists(_currentTempDir))
                            Directory.Delete(_currentTempDir, true);
                    }
                    catch { }
                    finally
                    {
                        _currentTempDir = null;
                    }
                    _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ArchivePassword_Cancelled");
                    _extractProgressBar.IsIndeterminate = false;
                    _extractProgressBar.Value = 0;
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to download and install mod", ex);

                    // Cleanup failed installation if it was a new mod
                    CleanupFailedInstallation();

                    // Cleanup downloaded files
                    try
                    {
                        if (_currentTempDir != null && Directory.Exists(_currentTempDir))
                            Directory.Delete(_currentTempDir, true);
                    }
                    catch { }
                    finally
                    {
                        _currentTempDir = null;
                    }

                    await ShowError(string.Format(SharedUtilities.GetTranslation(_lang, "InstallationFailed"), ex.Message));
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaFileExtractionDialog.OnPrimaryButtonClick failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaFileExtractionDialog.OnPrimaryButtonClick failed", ex);
            }
        }

        private async void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                // Prevent dialog from closing immediately
                args.Cancel = true;

                try
                {
                    IsPrimaryButtonEnabled = false;
                    IsSecondaryButtonEnabled = false;

                    // Determine target mod path
                    string modPath;

                    if (!string.IsNullOrEmpty(_existingModPathForPreviewsOnly))
                    {
                        // Use existing mod path
                        modPath = _existingModPathForPreviewsOnly;
                    }
                    else
                    {
                        // Create new mod folder for previews only
                        var category = _categoryComboBox.Text.Trim();
                        if (string.IsNullOrWhiteSpace(category))
                            category = "Characters";

                        // Sanitize category name to remove illegal characters
                        string categoryFolderName = SanitizeCategoryName(category);

                        var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                        var categoryPath = Path.Combine(modsPath, categoryFolderName);
                        Directory.CreateDirectory(categoryPath);

                        var cleanModName = SanitizeFileName(_modNameTextBox.Text.Trim());
                        modPath = Path.Combine(categoryPath, "DISABLED_" + cleanModName);

                        // Check if already exists
                        var existingPath = FindExistingModPath(categoryPath, cleanModName);
                        if (!string.IsNullOrEmpty(existingPath))
                        {
                            modPath = existingPath;
                        }
                        else
                        {
                            Directory.CreateDirectory(modPath);
                            // Create mod.json for new mod
                            await CreateModJson(modPath, _categoryComboBox.Text.Trim());
                        }
                    }

                    // Download preview images only
                    _downloadStatusText.Text = SharedUtilities.GetTranslation(_lang, "DownloadingPreviews");
                    _downloadProgressBar.IsIndeterminate = true;

                    await DownloadPreviewImagesAsync(modPath);

                    // Success
                    _downloadStatusText.Text = SharedUtilities.GetTranslation(_lang, "DownloadComplete");
                    _downloadProgressBar.Value = 100;
                    await Task.Delay(1000);

                    Logger.LogInfo($"[DIALOG] Closing dialog (preview-only mode) with Hide()");
                    // Close dialog
                    Hide();
                    Logger.LogInfo($"[DIALOG] Dialog closed (preview-only mode)");

                    Logger.LogInfo($"[DIALOG] Starting OptimizeDownloadedPreviewsAsync (preview-only) for: {modPath}");
                    // Run optimization AFTER dialog closes (on UI thread for crop panel support)
                    // Don't use Task.Run - we need to stay on UI thread for crop panel
                    _ = OptimizeDownloadedPreviewsAsync(modPath);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to download previews only", ex);
                    await ShowError(string.Format(SharedUtilities.GetTranslation(_lang, "DownloadPreviewsFailed"), ex.Message));
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaFileExtractionDialog.OnSecondaryButtonClick failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaFileExtractionDialog.OnSecondaryButtonClick failed", ex);
            }
        }

        private async Task InstallFiles(string sourceDir, string category)
        {
            try
            {
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();

                // Sanitize category name to remove illegal characters (including "/")
                var normalizedCategory = SanitizeCategoryName(category);
                var categoryPath = Path.Combine(modsPath, normalizedCategory);
                Directory.CreateDirectory(categoryPath);

                // Use the sanitized mod name from the text box instead of _modName
                var cleanModName = SanitizeFileName(_modNameTextBox.Text.Trim());
                
                // Check if mod already exists
                var existingModPath = FindExistingModPath(categoryPath, cleanModName);
                string modPath;
                bool wasActive = false;

                if (!string.IsNullOrEmpty(existingModPath) && Directory.Exists(existingModPath))
                {
                    modPath = existingModPath;
                    var folderName = Path.GetFileName(modPath);
                    wasActive = !folderName.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase);
                    if (wasActive)
                    {
                        var disabledPath = Path.Combine(categoryPath, "DISABLED_" + cleanModName);
                        Services.FileAccessQueue.MoveDirectory(modPath, disabledPath);
                        modPath = disabledPath;
                    }
                }
                else
                {
                    modPath = Path.Combine(categoryPath, "DISABLED_" + cleanModName);
                    if (Directory.Exists(modPath))
                    {
                        int counter = 1;
                        while (Directory.Exists($"{modPath}_{counter}")) counter++;
                        modPath = $"{modPath}_{counter}";
                    }
                }

                Directory.CreateDirectory(modPath);

                // Clean install
                if (_cleanInstall && Directory.Exists(modPath))
                    CleanModFolder(modPath);

                // Copy all files
                foreach (var file in Directory.GetFiles(sourceDir))
                {
                    var destFile = Path.Combine(modPath, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }

                // Create mod.json
                await CreateModJson(modPath, _categoryComboBox.Text.Trim());

                // Restore active status if mod was active before update
                if (wasActive)
                {
                    var finalActivePath = Path.Combine(categoryPath, cleanModName);
                    if (!Directory.Exists(finalActivePath))
                    {
                        Services.FileAccessQueue.MoveDirectory(modPath, finalActivePath);
                        modPath = finalActivePath;
                    }
                }

                // Save mod path for preview download and event firing
                _installedModPath = modPath;

                // Cleanup
                Directory.Delete(sourceDir, true);
            }
            catch (Exception ex)
            {
                Logger.LogError($"InstallFiles failed", ex);
                throw;
            }
        }

        private async Task CreateModJson(string modPath, string categoryName)
        {
            var modJsonPath = Path.Combine(modPath, "mod.json");
            
            // Use version from API (_version)
            string version = _version ?? "";
            
            // gbChangeDate = server's last update date (from GameBanana)
            string gbChangeDate = "0000-00-00";
            if (_dateUpdatedTimestamp > 0)
            {
                try
                {
                    var date = DateTimeOffset.FromUnixTimeSeconds(_dateUpdatedTimestamp).DateTime;
                    gbChangeDate = date.ToString("yyyy-MM-dd");
                }
                catch
                {
                    gbChangeDate = "0000-00-00";
                }
            }
            
            // dateUpdated = when user installed/updated the mod locally (now)
            string dateUpdated = DateTime.Now.ToString("yyyy-MM-dd");
            
            var modJson = new
            {
                author = _authorName,
                url = _modProfileUrl ?? "https://",
                version = string.IsNullOrWhiteSpace(version) ? " " : version,
                dateChecked = DateTime.Now.ToString("yyyy-MM-dd"),
                dateUpdated = dateUpdated,
                gbChangeDate = gbChangeDate,
                isNSFW = _isNSFW,
                hotkeys = new object[] { }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(modJson, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await Services.FileAccessQueue.WriteAllTextAsync(modJsonPath, json);
            
            // Remove from outdated list since we just installed/updated it
            var modName = Path.GetFileName(modPath);
            if (modName.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase))
                modName = modName.Substring(9);
            ModListManager.RemoveFromOutdatedList(categoryName, modName);
        }

        /// <summary>
        /// Clean up partially created mod folder if installation failed or was cancelled
        /// Only applies to NEW mod installations, not updates
        /// </summary>
        private void CleanupFailedInstallation()
        {
            try
            {
                // Only clean up new mod installations (not updates)
                if (!_isNewModInstall)
                {
                    Logger.LogInfo("Skipping cleanup - this was an update, not a new installation");
                    return;
                }
                
                // Only clean up if mod path was set
                if (string.IsNullOrEmpty(_installedModPath))
                    return;
                
                // If folder exists, delete it (extract failed = no partial content needed)
                if (Directory.Exists(_installedModPath))
                {
                    Logger.LogInfo($"Cleaning up failed installation: {_installedModPath}");
                    
                    try
                    {
                        Directory.Delete(_installedModPath, true);
                        Logger.LogInfo($"Successfully deleted failed installation folder: {_installedModPath}");
                    }
                    catch (Exception deleteEx)
                    {
                        Logger.LogWarning($"Failed to delete installation folder: {_installedModPath}, Error: {deleteEx.Message}");
                    }
                }
                
                // Reset installed mod path
                _installedModPath = null;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during cleanup of failed installation", ex);
            }
        }

        private void RefreshModTileInUI(string modPath)
        {
            try
            {
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                mainWindow?.CurrentModGridPage?.RefreshModTileImage(modPath);
                
                // Also refresh HasUpdate status by re-reading mod.json
                mainWindow?.CurrentModGridPage?.RefreshModTileStatus(modPath);
                
                // Force reload of outdated mods view if it's currently shown
                mainWindow?.CurrentModGridPage?.RefreshOutdatedView();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to refresh mod tile in UI: {modPath}", ex);
            }
        }
    }
}

