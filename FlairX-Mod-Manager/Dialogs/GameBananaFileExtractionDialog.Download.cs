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

        private void CheckIfUpdateAndShowOptions(string category)
        {
            try
            {
                // Check if this is preview-only mode (empty file list)
                bool isPreviewOnlyMode = _selectedFiles.Count == 0;
                
                // If existingModPath was provided in constructor, use it directly
                if (!string.IsNullOrEmpty(_existingModPathForPreviewsOnly) && Directory.Exists(_existingModPathForPreviewsOnly))
                {
                    // Show update-specific options grid only in normal mode
                    if (!isPreviewOnlyMode && _updateOptionsGrid != null)
                        _updateOptionsGrid.Visibility = Visibility.Visible;
                    
                    Title = isPreviewOnlyMode ? 
                        SharedUtilities.GetTranslation(_lang, "DownloadPreviewsOnly") :
                        SharedUtilities.GetTranslation(_lang, "DownloadAndUpdateMod");
                    Logger.LogInfo($"Update detected for mod (from provided path): {_existingModPathForPreviewsOnly}");
                    
                    // Use existing folder name instead of GameBanana name
                    var existingFolderName = Path.GetFileName(_existingModPathForPreviewsOnly);
                    // Remove DISABLED_ prefix if present
                    if (existingFolderName.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase))
                    {
                        existingFolderName = existingFolderName.Substring(9);
                    }
                    // Sanitize folder name to ensure it's valid
                    var sanitizedFolderName = SanitizeFileName(existingFolderName);
                    _modNameTextBox.Text = sanitizedFolderName;
                    ValidateInputs(); // Enable Start button
                    Logger.LogInfo($"Using existing folder name: {sanitizedFolderName}");
                    
                    // Update category to where mod actually is
                    var existingCategory = Path.GetFileName(Path.GetDirectoryName(_existingModPathForPreviewsOnly));
                    if (!string.IsNullOrEmpty(existingCategory))
                    {
                        _categoryComboBox.Text = existingCategory;
                        Logger.LogInfo($"Using existing category: {existingCategory}");
                    }
                    
                    // Enable "Download Previews Only" button if previews are available
                    if (_previewMedia?.Images != null && _previewMedia.Images.Any(img => img.Type == "screenshot"))
                    {
                        IsSecondaryButtonEnabled = true;
                        Logger.LogInfo($"Previews available for download-only option");
                    }
                    return;
                }
                
                // Get mod library path
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
                    return;

                // Search ALL categories for existing mod by ID (user may have moved it)
                string? existingModPath = null;
                string? foundInCategory = null;
                
                foreach (var categoryDir in Directory.GetDirectories(modsPath))
                {
                    var found = FindExistingModPathByModId(categoryDir, _modProfileUrl);
                    if (!string.IsNullOrEmpty(found))
                    {
                        existingModPath = found;
                        foundInCategory = Path.GetFileName(categoryDir);
                        break;
                    }
                }

                // Check if mod exists - this is an update
                if (!string.IsNullOrEmpty(existingModPath) && Directory.Exists(existingModPath))
                {
                    // Show update-specific options grid only in normal mode
                    if (!isPreviewOnlyMode && _updateOptionsGrid != null)
                        _updateOptionsGrid.Visibility = Visibility.Visible;
                    
                    Title = isPreviewOnlyMode ? 
                        SharedUtilities.GetTranslation(_lang, "DownloadPreviewsOnly") :
                        SharedUtilities.GetTranslation(_lang, "DownloadAndUpdateMod");
                    Logger.LogInfo($"Update detected for mod: {existingModPath}");
                    
                    // Use existing folder name instead of GameBanana name
                    var existingFolderName = Path.GetFileName(existingModPath);
                    // Remove DISABLED_ prefix if present
                    if (existingFolderName.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase))
                    {
                        existingFolderName = existingFolderName.Substring(9);
                    }
                    // Sanitize folder name to ensure it's valid
                    var sanitizedFolderName = SanitizeFileName(existingFolderName);
                    _modNameTextBox.Text = sanitizedFolderName;
                    ValidateInputs(); // Enable Start button
                    Logger.LogInfo($"Using existing folder name: {sanitizedFolderName}");
                    
                    // Update category to where mod actually is
                    if (!string.IsNullOrEmpty(foundInCategory))
                    {
                        _categoryComboBox.Text = foundInCategory;
                        Logger.LogInfo($"Using existing category: {foundInCategory}");
                    }
                    
                    // Enable "Download Previews Only" button if previews are available
                    if (_previewMedia?.Images != null && _previewMedia.Images.Any(img => img.Type == "screenshot"))
                    {
                        _existingModPathForPreviewsOnly = existingModPath;
                        IsSecondaryButtonEnabled = true;
                        Logger.LogInfo($"Previews available for download-only option");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to check if update", ex);
            }
        }

        private async Task DownloadPreviewImagesAsync(string modPath)
        {
            try
            {
                if (_previewMedia?.Images == null) return;

                _downloadStatusText.Text = SharedUtilities.GetTranslation(_lang, "DownloadingPreviews");
                _downloadProgressBar.IsIndeterminate = true;

                int startIndex = 0;

                // Delete old preview images (all variants) unless combining
                if (!_combinePreviews)
                {
                    try
                    {
                        var oldPreviews = Directory.GetFiles(modPath)
                            .Where(f => 
                            {
                                var fileName = Path.GetFileName(f).ToLower();
                                return (fileName.StartsWith("preview") || IsMinitileFile(fileName)) && 
                                       IsImageFile(f);
                            });
                        
                        foreach (var oldPreview in oldPreviews)
                        {
                            try
                            {
                                File.Delete(oldPreview);
                                Logger.LogInfo($"Deleted old preview: {oldPreview}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Failed to delete old preview: {oldPreview}", ex);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to delete old previews", ex);
                    }
                }
                else
                {
                    // Find next available preview number
                    var existingPreviews = Directory.GetFiles(modPath)
                        .Where(f => 
                        {
                            var fileName = Path.GetFileName(f).ToLower();
                            return fileName.StartsWith("preview") && 
                                   IsImageFile(f);
                        })
                        .Select(f => Path.GetFileName(f))
                        .ToList();

                    // Extract numbers from existing preview files
                    var existingNumbers = new List<int>();
                    foreach (var preview in existingPreviews)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(preview, @"preview-?(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                        {
                            existingNumbers.Add(num);
                        }
                        else if (preview.ToLower() == "preview.jpg" || preview.ToLower() == "preview.png")
                        {
                            existingNumbers.Add(0);
                        }
                    }

                    startIndex = existingNumbers.Count > 0 ? existingNumbers.Max() + 1 : 0;
                    Logger.LogInfo($"Combining previews - starting from index {startIndex}");
                }

                var screenshots = _previewMedia.Images.Where(img => img.Type == "screenshot").ToList();
                if (screenshots.Count == 0) return;

                _downloadProgressBar.IsIndeterminate = false;
                _downloadProgressBar.Value = 0;

                using var httpClient = new System.Net.Http.HttpClient();
                
                const int maxConcurrent = 4;
                using var semaphore = new System.Threading.SemaphoreSlim(maxConcurrent, maxConcurrent);
                int completed = 0;

                var tasks = screenshots.Select(async (screenshot, i) =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var imageUrl = $"{screenshot.BaseUrl}/{screenshot.File}";
                        var urlPath = new Uri(imageUrl).AbsolutePath;
                        var fileExtension = Path.GetExtension(urlPath);
                        if (string.IsNullOrEmpty(fileExtension))
                            fileExtension = ".jpg";

                        var fileName = $"preview{(startIndex + i + 1):D3}{fileExtension}";
                        var filePath = Path.Combine(modPath, fileName);

                        var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                        await File.WriteAllBytesAsync(filePath, imageBytes);
                        Logger.LogInfo($"Downloaded: {fileName}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to download preview image {i + 1}", ex);
                    }
                    finally
                    {
                        semaphore.Release();
                        var done = System.Threading.Interlocked.Increment(ref completed);
                        _downloadStatusText.DispatcherQueue.TryEnqueue(() =>
                        {
                            _downloadStatusText.Text = string.Format(
                                SharedUtilities.GetTranslation(_lang, "DownloadingPreviews_Progress"),
                                done, screenshots.Count);
                            _downloadProgressBar.Value = (double)done / screenshots.Count * 100;
                        });
                    }
                });

                await Task.WhenAll(tasks);

                _downloadProgressBar.Value = 100;
                _downloadStatusText.Text = SharedUtilities.GetTranslation(_lang, "DownloadComplete");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to download preview images", ex);
            }
        }

        private async Task OptimizeImagesInBackground(string modPath)
        {
            try
            {
                Logger.LogInfo($"Starting image optimization for downloaded mod in: {modPath}");
                
                // Get optimization context for GameBanana download
                var context = Services.ImageOptimizationService.GetOptimizationContext(
                    Services.OptimizationTrigger.GameBananaDownload);
                
                Logger.LogInfo($"Using AutoDownloadMode: {context.Mode}, InspectAndEdit: {context.InspectAndEditEnabled}, CropStrategy: {context.CropStrategy}");
                
                // Run optimization sequentially on UI thread to allow crop inspection panel
                // This blocks the dialog, which is intentional - user reviews each crop one by one
                await Services.ImageOptimizationService.ProcessModPreviewImagesAsync(modPath, context);
                
                Logger.LogInfo($"Completed image optimization for: {modPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to optimize images", ex);
            }
        }

        private async Task CreateBackupAsync(string modPath)
        {
            try
            {
                _extractStatusText.Text = SharedUtilities.GetTranslation(_lang, "ExtractCreatingBackup");
                _extractProgressBar.IsIndeterminate = true;

                // Find next available backup number inside mod folder
                int backupNumber = 1;
                string backupPath;
                do
                {
                    backupPath = Path.Combine(modPath, $"fxmm-backup-{backupNumber:D3}.zip");
                    backupNumber++;
                } while (File.Exists(backupPath));

                // Create backup without compression (store only)
                var files = Directory.GetFiles(modPath, "*", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("fxmm-backup")) // Skip existing backups
                    .ToList();

                var filesToBackup = new Dictionary<string, string>();
                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(modPath, file);
                    filesToBackup[relativePath] = file;
                }

                if (filesToBackup.Count > 0)
                {
                    ArchiveHelper.CreateArchiveFromFiles(backupPath, filesToBackup);
                }

                Logger.LogInfo($"Created backup: {backupPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to create backup", ex);
                // Don't throw - continue with installation even if backup fails
            }
        }

        private void CleanModFolder(string modPath)
        {
            try
            {
                Logger.LogInfo($"CleanModFolder: cleaning {modPath}");

                // Delete all subdirectories unconditionally
                foreach (var dir in Directory.GetDirectories(modPath, "*", SearchOption.AllDirectories)
                                             .OrderByDescending(d => d.Length))
                {
                    try
                    {
                        if (Directory.Exists(dir))
                        {
                            Directory.Delete(dir, recursive: true);
                            Logger.LogInfo($"CleanModFolder: deleted dir {dir}");
                        }
                    }
                    catch (Exception ex) { Logger.LogError($"Failed to delete directory: {dir}", ex); }
                }

                // Delete all files except mod.json (and previews if Keep previews is enabled)
                foreach (var file in Directory.GetFiles(modPath))
                {
                    var fileName = Path.GetFileName(file);

                    // Always keep mod.json
                    if (fileName.Equals("mod.json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Keep installation backups. "Clean install" used to delete the archive
                    // that was created moments earlier, destroying the only rollback copy.
                    if (fileName.StartsWith("fxmm-backup", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Keep previews if setting enabled
                    if (_keepPreviews)
                    {
                        var fileNameLower = fileName.ToLower();
                        bool isPreview = (fileNameLower.StartsWith("preview") || IsMinitileFile(fileNameLower)) &&
                                         IsImageFile(file);
                        if (isPreview) continue;
                    }

                    try { File.Delete(file); }
                    catch (Exception ex) { Logger.LogError($"Failed to delete file: {file}", ex); }
                }

                Logger.LogInfo($"Cleaned mod folder: {modPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to clean mod folder", ex);
            }
        }

        private async Task OptimizeDownloadedPreviewsAsync(string modPath, bool showSuccessInfoBar = false)
        {
            try
            {
                Logger.LogInfo($"Starting preview optimization for downloaded mod in: {modPath}");
                
                // Reset cancellation flag before starting new optimization
                // This ensures previous cancellations don't affect new downloads
                Services.ImageOptimizationService.ResetCancellation();
                
                // Get optimization context for GameBanana download
                var context = Services.ImageOptimizationService.GetOptimizationContext(
                    Services.OptimizationTrigger.GameBananaDownload);
                
                // GameBanana download never uses reoptimize - only process new files
                context.Reoptimize = false;
                
                Logger.LogInfo($"Using AutoDownloadMode: {context.Mode}, InspectAndEdit: {context.InspectAndEditEnabled}, CropStrategy: {context.CropStrategy}");
                
                // Optimize sequentially (with crop inspection if enabled)
                await Services.ImageOptimizationService.ProcessModPreviewImagesAsync(modPath, context);
                
                Logger.LogInfo($"Completed preview optimization for: {modPath}");

                // Show success InfoBar after optimization completes
                if (showSuccessInfoBar)
                {
                    var installedModName = Path.GetFileName(modPath);
                    if (installedModName.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase))
                        installedModName = installedModName.Substring(9);
                    if (App.Current is App app && app.MainWindow is MainWindow mw)
                        mw.ShowSuccessInfo(installedModName);
                }
                
                // Refresh the mod tile in UI after optimization
                RefreshModTileInUI(modPath);
            }
            catch (OperationCanceledException)
            {
                // User clicked "Stop" in minitile selection or crop panel
                // Clean up downloaded preview files so next download attempt will work
                Logger.LogInfo($"User stopped optimization from UI panel for: {modPath}");
                try
                {
                    var previewFilesToDelete = Directory.GetFiles(modPath)
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
                    Logger.LogInfo($"Cleaned up preview files from cancelled download: {modPath}");
                }
                catch (Exception cleanupEx)
                {
                    Logger.LogError($"Failed to clean up preview files for {modPath}", cleanupEx);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to optimize downloaded preview images", ex);
            }
        }
    }
}

