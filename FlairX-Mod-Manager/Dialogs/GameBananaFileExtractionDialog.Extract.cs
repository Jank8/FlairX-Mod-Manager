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

        private async Task ExtractArchiveAsync(string category)
        {
            try
            {
                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractPreparing");
                _extractProgressBar.IsIndeterminate = true;

                // Get mod library path
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();

                // Sanitize category name to remove illegal characters (including "/")
                var normalizedCategory = SanitizeCategoryName(category);
                var categoryPath = Path.Combine(modsPath, normalizedCategory);
                Directory.CreateDirectory(categoryPath);

                // Check if mod already exists (with or without DISABLED_ prefix)
                var cleanModName = SanitizeFileName(_modNameTextBox.Text.Trim());
                var existingModPath = FindExistingModPath(categoryPath, cleanModName);
                
                bool isUpdate = false;
                bool wasActive = false;
                string modPath;
                
                if (!string.IsNullOrEmpty(existingModPath) && Directory.Exists(existingModPath))
                {
                    // Mod exists - check if it's an update
                    modPath = existingModPath;
                    
                    // Check if mod is currently active (no DISABLED_ prefix)
                    var folderName = Path.GetFileName(modPath);
                    wasActive = !folderName.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase);
                    
                    var modJsonPath = Path.Combine(modPath, "mod.json");
                    if (File.Exists(modJsonPath))
                    {
                        try
                        {
                            var modJsonContent = await Services.FileAccessQueue.ReadAllTextAsync(modJsonPath);
                            var modJson = System.Text.Json.JsonDocument.Parse(modJsonContent);
                            if (modJson.RootElement.TryGetProperty("url", out var urlProp))
                            {
                                var existingUrl = urlProp.GetString();
                                // If URLs match, this is an update
                                if (existingUrl == _modProfileUrl)
                                {
                                    isUpdate = true;
                                    _isNewModInstall = false; // Mark as update, not new install
                                    Logger.LogInfo($"Updating existing mod at: {modPath} (was active: {wasActive})");
                                    
                                    // If mod is active, temporarily deactivate it for update
                                    if (wasActive)
                                    {
                                        var tempDisabledPath = Path.Combine(categoryPath, "DISABLED_" + cleanModName);
                                        
                                        // Check if DISABLED_ version already exists
                                        if (Directory.Exists(tempDisabledPath))
                                        {
                                            throw new InvalidOperationException($"Cannot update active mod: A disabled version already exists at {tempDisabledPath}");
                                        }
                                        
                                        // Temporarily deactivate mod for update
                                        Services.FileAccessQueue.MoveDirectory(modPath, tempDisabledPath);
                                        modPath = tempDisabledPath;
                                        Logger.LogInfo($"Temporarily deactivated mod for update: {modPath}");
                                    }
                                    
                                    // Show update-specific options grid only in normal mode
                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        bool isPreviewOnlyMode = _selectedFiles.Count == 0;
                                        if (!isPreviewOnlyMode && _updateOptionsGrid != null)
                                            _updateOptionsGrid.Visibility = Visibility.Visible;
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Failed to read mod.json for update check", ex);
                        }
                    }
                    
                    // If not an update, create new folder with suffix
                    if (!isUpdate)
                    {
                        int counter = 1;
                        var basePath = modPath;
                        while (Directory.Exists($"{basePath}_{counter}"))
                        {
                            counter++;
                        }
                        modPath = $"{basePath}_{counter}";
                        Logger.LogInfo($"Installing new mod at: {modPath}");
                    }
                }
                else
                {
                    // New mod - add DISABLED_ prefix
                    modPath = Path.Combine(categoryPath, "DISABLED_" + cleanModName);
                    Logger.LogInfo($"Installing new mod (disabled by default) at: {modPath}");
                }

                // Create directory if it doesn't exist (for new installs)
                if (!Directory.Exists(modPath))
                {
                    Directory.CreateDirectory(modPath);
                }

                // Create backup if enabled and updating
                if (_createBackup && isUpdate && Directory.Exists(modPath))
                {
                    await CreateBackupAsync(modPath);
                }

                // Clean install - remove all files except backups
                if (_cleanInstall && Directory.Exists(modPath))
                {
                    _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractCleaningOldFiles");
                    CleanModFolder(modPath);
                }

                // Extract all files with directory structure using SharpSevenZip
                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractExtractingFiles");
                
                // Extract to temp directory first to handle root folder skipping
                var tempExtractPath = Path.Combine(PathManager.GetTempDirectory(), $"fxmm_extract_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempExtractPath);
                
                try
                {
                    // Extract with progress
                    var progress = new Progress<int>(percent =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            _extractProgressBar.IsIndeterminate = false;
                            _extractProgressBar.Value = percent;
                            _extractStatusText.Text = string.Format(SharedUtilities.GetTranslation(_lang, "ExtractingProgress"), percent, 100);
                        });
                    });

                    // Password retry loop
                    string? archivePassword = null;
                    bool firstAttempt = true;
                    while (true)
                    {
                        try
                        {
                            var extractTask = archivePassword == null
                                ? Task.Run(() => ArchiveHelper.ExtractToDirectory(_downloadedArchivePath!, tempExtractPath, progress))
                                : Task.Run(() => ArchiveHelper.ExtractToDirectory(_downloadedArchivePath!, tempExtractPath, archivePassword, progress));

                            if (!extractTask.Wait(TimeSpan.FromMinutes(5)))
                            {
                                Logger.LogWarning("Archive extraction timed out after 5 minutes, trying fallback method");
                                if (_downloadedArchivePath!.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                                    System.IO.Compression.ZipFile.ExtractToDirectory(_downloadedArchivePath!, tempExtractPath, true);
                                else
                                    throw new TimeoutException(SharedUtilities.GetTranslation(_lang, "ExtractionTimeout"));
                            }

                            // Unwrap AggregateException to check for ArchiveEncryptedException
                            if (extractTask.Exception != null)
                                throw extractTask.Exception;

                            break; // Success
                        }
                        catch (AggregateException ae) when (ae.InnerException is ArchiveEncryptedException)
                        {
                            // Show password panel and wait for user input
                            if (!firstAttempt)
                            {
                                // Wrong password — show red error with fade-out
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    IsPrimaryButtonEnabled = true;
                                    _extractProgressBar.IsIndeterminate = false;
                                    _extractProgressBar.Value = 0;
                                    _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractExtractingFiles");
                                });
                                _ = ShowPasswordErrorAsync(SharedUtilities.GetTranslation(_lang, "ArchivePassword_Wrong"));
                            }
                            firstAttempt = false;

                            archivePassword = await RequestPasswordAsync();

                            if (archivePassword == null)
                            {
                                // User cancelled — abort extraction
                                throw new OperationCanceledException(SharedUtilities.GetTranslation(_lang, "ArchivePassword_Cancelled"));
                            }

                            // Hide password panel, show progress again
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                _extractProgressBar.IsIndeterminate = true;
                                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractExtractingFiles");
                                IsPrimaryButtonEnabled = false;
                            });

                            // Clear temp dir for retry
                            foreach (var f in Directory.GetFiles(tempExtractPath, "*", SearchOption.AllDirectories))
                                File.Delete(f);
                            foreach (var d in Directory.GetDirectories(tempExtractPath).Reverse())
                                Directory.Delete(d, true);
                        }
                    }
                    
                    // Check if all files are in a single root folder
                    var topLevelItems = Directory.GetFileSystemEntries(tempExtractPath);
                    string sourceDir = tempExtractPath;
                    
                    if (topLevelItems.Length == 1 && Directory.Exists(topLevelItems[0]))
                    {
                        // All files are in a single root folder, skip it
                        sourceDir = topLevelItems[0];
                        Logger.LogInfo($"Skipping root folder: {Path.GetFileName(sourceDir)}");
                    }
                    
                    // Move files to final destination
                    foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(sourceDir, file);
                        var destPath = Path.Combine(modPath, relativePath);
                        
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }
                        
                        File.Move(file, destPath, true);
                    }
                    
                    // If mod was active before update, reactivate it
                    if (isUpdate && wasActive)
                    {
                        var finalActivePath = Path.Combine(categoryPath, cleanModName);
                        
                        // Check if active path is available
                        if (Directory.Exists(finalActivePath))
                        {
                            Logger.LogWarning($"Cannot reactivate mod: Active path already exists at {finalActivePath}");
                        }
                        else
                        {
                            Services.FileAccessQueue.MoveDirectory(modPath, finalActivePath);
                            modPath = finalActivePath;
                            Logger.LogInfo($"Reactivated mod after update: {modPath}");
                        }
                    }
                }
                finally
                {
                    // Cleanup temp directory
                    try
                    {
                        if (Directory.Exists(tempExtractPath))
                        {
                            Directory.Delete(tempExtractPath, true);
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        Logger.LogWarning($"Failed to cleanup temp extraction directory: {cleanupEx.Message}");
                    }
                }

                // Create mod.json
                await CreateModJson(modPath, _categoryComboBox.Text.Trim());
                
                // Save mod path for preview download
                _installedModPath = modPath;
                
                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractComplete");
                _extractProgressBar.Value = 100;
            }
            catch (OperationCanceledException ex)
            {
                Logger.LogInfo($"Archive extraction cancelled by user: {ex.Message}");
                throw; // Let OnPrimaryButtonClick show the cancellation message
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to extract files", ex);
                throw;
            }
        }

        private async Task ExtractMultipleArchivesAsync(string category, List<(string filePath, string fileName)> downloadedFiles)
        {
            try
            {
                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractPreparing");
                _extractProgressBar.IsIndeterminate = true;

                // Get mod library path
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();

                // Sanitize category name to remove illegal characters (including "/")
                var normalizedCategory = SanitizeCategoryName(category);
                var categoryPath = Path.Combine(modsPath, normalizedCategory);
                Directory.CreateDirectory(categoryPath);

                // New mods are disabled by default
                var cleanModName = SanitizeFileName(_modNameTextBox.Text.Trim());
                var existingModPath = FindExistingModPath(categoryPath, cleanModName);

                string modPath;
                bool wasActive = false;
                if (!string.IsNullOrEmpty(existingModPath) && Directory.Exists(existingModPath))
                {
                    modPath = existingModPath;
                    // Ensure mod is disabled during update
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
                }

                // Create main mod directory
                Directory.CreateDirectory(modPath);

                // Clean install - remove all files
                if (_cleanInstall && Directory.Exists(modPath))
                {
                    _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractCleaningOldFiles");
                    CleanModFolder(modPath);
                }

                // Extract each archive to its own subfolder
                int totalFiles = downloadedFiles.Count(f => IsArchiveFile(f.fileName));
                int currentFile = 0;

                foreach (var (filePath, fileName) in downloadedFiles)
                {
                    if (!IsArchiveFile(fileName))
                    {
                        // Copy non-archive files to main mod folder
                        var destPath = Path.Combine(modPath, fileName);
                        File.Copy(filePath, destPath, true);
                        continue;
                    }

                    currentFile++;
                    _extractStatusText.Text = string.Format(SharedUtilities.GetTranslation(_lang, "ExtractExtractingFiles") + " ({0}/{1})", currentFile, totalFiles);

                    // Create subfolder for this archive (without extension)
                    var archiveNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    var archiveSubfolder = Path.Combine(modPath, SanitizeFileName(archiveNameWithoutExt));
                    
                    // Extract to temp directory first to handle root folder skipping
                    var tempExtractPath = Path.Combine(PathManager.GetTempDirectory(), $"fxmm_multi_{Guid.NewGuid()}");
                    Directory.CreateDirectory(tempExtractPath);
                    
                    try
                    {
                        // Try extraction with timeout
                        var extractTask = Task.Run(() => ArchiveHelper.ExtractToDirectory(filePath, tempExtractPath));
                        if (!extractTask.Wait(TimeSpan.FromMinutes(5)))
                        {
                            Logger.LogWarning($"Archive extraction timed out for {fileName}, trying fallback method");
                            // Fallback to System.IO.Compression for problematic archives
                            if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                System.IO.Compression.ZipFile.ExtractToDirectory(filePath, tempExtractPath, true);
                            }
                            else
                            {
                                throw new TimeoutException(string.Format(SharedUtilities.GetTranslation(_lang, "ExtractionTimeoutFile"), fileName));
                            }
                        }
                        
                        // Check if all files are in a single root folder
                        var topLevelItems = Directory.GetFileSystemEntries(tempExtractPath);
                        string sourceDir = tempExtractPath;
                        
                        if (topLevelItems.Length == 1 && Directory.Exists(topLevelItems[0]))
                        {
                            // All files are in a single root folder, skip it
                            sourceDir = topLevelItems[0];
                            Logger.LogInfo($"Skipping root folder: {Path.GetFileName(sourceDir)}");
                        }
                        
                        // Move files to final destination
                        Directory.CreateDirectory(archiveSubfolder);
                        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                        {
                            var relativePath = Path.GetRelativePath(sourceDir, file);
                            var destPath = Path.Combine(archiveSubfolder, relativePath);
                            
                            var destDir = Path.GetDirectoryName(destPath);
                            if (!string.IsNullOrEmpty(destDir))
                            {
                                Directory.CreateDirectory(destDir);
                            }
                            
                            File.Move(file, destPath, true);
                        }
                    }
                    finally
                    {
                        // Cleanup temp directory
                        try
                        {
                            if (Directory.Exists(tempExtractPath))
                            {
                                Directory.Delete(tempExtractPath, true);
                            }
                        }
                        catch (Exception cleanupEx)
                        {
                            Logger.LogWarning($"Failed to cleanup temp extraction directory: {cleanupEx.Message}");
                        }
                    }
                }

                // Create mod.json in main mod folder
                await CreateModJson(modPath, _categoryComboBox.Text.Trim());

                // If mod was active before update, reactivate it
                if (wasActive)
                {
                    var finalActivePath = Path.Combine(categoryPath, cleanModName);
                    if (!Directory.Exists(finalActivePath))
                    {
                        Services.FileAccessQueue.MoveDirectory(modPath, finalActivePath);
                        modPath = finalActivePath;
                        Logger.LogInfo($"Reactivated mod after update: {modPath}");
                    }
                }

                // Save mod path for preview download
                _installedModPath = modPath;
                
                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractComplete");
                _extractProgressBar.Value = 100;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to extract multiple archives", ex);
                throw;
            }
        }

        /// <summary>
        /// Shows the password panel and waits for the user to submit a password via the primary button.
        /// Returns null if the user cancels (empty password submitted).
        /// </summary>
        private Task<string?> RequestPasswordAsync()
        {
            _passwordTcs = new TaskCompletionSource<string?>();

            DispatcherQueue.TryEnqueue(() =>
            {
                _passwordPanel.Visibility = Visibility.Visible;
                _passwordBox.Password = string.Empty;
                _passwordBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                PrimaryButtonText = SharedUtilities.GetTranslation(_lang, "Extract");
                IsPrimaryButtonEnabled = true;
            });

            return _passwordTcs.Task;
        }

        /// <summary>
        /// Called by the primary button handler when the password panel is visible.
        /// Returns true if it consumed the click (password flow active).
        /// </summary>
        private bool TrySubmitPassword()
        {
            if (_passwordTcs == null || _passwordPanel.Visibility != Visibility.Visible)
                return false;

            var tcs = _passwordTcs;
            _passwordTcs = null;
            tcs.TrySetResult(string.IsNullOrEmpty(_passwordBox.Password) ? null : _passwordBox.Password);
            return true;
        }
    }
}

