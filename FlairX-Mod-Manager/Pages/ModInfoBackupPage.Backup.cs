using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModInfoBackupPage
    {
        
        // Operation state tracking
        private static volatile bool _isCreatingBackup = false;
        
        // Cancellation token for backup creation
        private System.Threading.CancellationTokenSource? _backupCancellationToken;

        private async void CreateBackupsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // If already creating, cancel it
                if (_isCreatingBackup && _backupCancellationToken != null)
                {
                    _backupCancellationToken.Cancel();
                    return;
                }
                
                await CreateBackupsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in CreateBackupsButton_Click", ex);
                _isCreatingBackup = false;
                _backupCancellationToken?.Dispose();
                _backupCancellationToken = null;
                UpdateButtonStates();
                var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
                if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(ex.Message);
            }
        }
        
        private async Task CreateBackupsAsync()
        {
            _isCreatingBackup = true;
            _backupCancellationToken = new System.Threading.CancellationTokenSource();
            UpdateButtonStates();
            
            // Progress reporting
            var progress = new Progress<(int current, int total)>(value =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (CreateBackupsProgressBar != null && CreateBackupsStatusText != null)
                    {
                        var percentage = value.total > 0 ? (double)value.current / value.total * 100 : 0;
                        CreateBackupsProgressBar.Value = percentage;
                        CreateBackupsStatusText.Text = $"{value.current}/{value.total}";
                    }
                });
            });
            
            try
            {
                int count = await Task.Run(() => CreateAllBackups(_backupCancellationToken.Token, progress), _backupCancellationToken.Token);
                
                _isCreatingBackup = false;
                UpdateButtonStates();
                
                var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
                if (App.Current is App _a && _a.MainWindow is MainWindow _mw) 
                    _mw.ShowSuccessInfo(string.Format(SharedUtilities.GetTranslation(lang, "ModInfoBackup_BackupComplete"), count));
                UpdateBackupInfo();
            }
            catch (OperationCanceledException)
            {
                _isCreatingBackup = false;
                UpdateButtonStates();
                
                var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
                if (App.Current is App _a && _a.MainWindow is MainWindow _mw) 
                    _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "Cancelled"));
                UpdateBackupInfo();
            }
            finally
            {
                _backupCancellationToken?.Dispose();
                _backupCancellationToken = null;
            }
        }

        private int CreateAllBackups(System.Threading.CancellationToken cancellationToken, IProgress<(int current, int total)> progress)
        {
            int count = 0;
            if (!Directory.Exists(ModLibraryPath)) return count;
            
            // First, backup category preview images
            try
            {
                foreach (var categoryDir in Directory.GetDirectories(ModLibraryPath))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = new DirectoryInfo(categoryDir).Name;
                    
                    // Check if category has any files to backup
                    var catprevJpgPath = Path.Combine(categoryDir, "catprev.jpg");
                    var catprevWebpPath = Path.Combine(categoryDir, "catprev.webp");
                    var catminiJpgPath = Path.Combine(categoryDir, "catmini.jpg");
                    var catminiWebpPath = Path.Combine(categoryDir, "catmini.webp");
                    
                    bool hasCatprev = File.Exists(catprevJpgPath) || File.Exists(catprevWebpPath);
                    bool hasCatmini = File.Exists(catminiJpgPath) || File.Exists(catminiWebpPath);
                    bool hasCatprevWide = File.Exists(Path.Combine(categoryDir, "catprev-wide.jpg")) || File.Exists(Path.Combine(categoryDir, "catprev-wide.webp"));
                    bool hasCatminiWide = File.Exists(Path.Combine(categoryDir, "catmini-wide.jpg")) || File.Exists(Path.Combine(categoryDir, "catmini-wide.webp"));
                    
                    // Only create backup if there are files to backup
                    if (!hasCatprev && !hasCatmini && !hasCatprevWide && !hasCatminiWide)
                    {
                        Logger.LogInfo($"Skipping category backup for {categoryName} - no catprev or catmini found");
                        continue;
                    }
                    
                    // Shift existing category backups: 2->3, 1->2, new->1
                    var catBackup3 = Path.Combine(categoryDir, $"{categoryName}_category.mib3.zip");
                    var catBackup2 = Path.Combine(categoryDir, $"{categoryName}_category.mib2.zip");
                    var catBackup1 = Path.Combine(categoryDir, $"{categoryName}_category.mib1.zip");
                    
                    if (File.Exists(catBackup3)) File.Delete(catBackup3);
                    if (File.Exists(catBackup2)) File.Move(catBackup2, catBackup3);
                    if (File.Exists(catBackup1)) File.Move(catBackup1, catBackup2);
                    
                    // Create new category backup
                    var filesToBackup = new Dictionary<string, string>();
                    if (hasCatprev)
                    {
                        if (File.Exists(catprevWebpPath))
                        {
                            filesToBackup["catprev.webp"] = catprevWebpPath;
                            Logger.LogInfo($"Added catprev.webp to backup for category: {categoryName}");
                        }
                        else if (File.Exists(catprevJpgPath))
                        {
                            filesToBackup["catprev.jpg"] = catprevJpgPath;
                            Logger.LogInfo($"Added catprev.jpg to backup for category: {categoryName}");
                        }
                    }
                    
                    if (hasCatmini)
                    {
                        if (File.Exists(catminiWebpPath))
                        {
                            filesToBackup["catmini.webp"] = catminiWebpPath;
                            Logger.LogInfo($"Added catmini.webp to backup for category: {categoryName}");
                        }
                        else if (File.Exists(catminiJpgPath))
                        {
                            filesToBackup["catmini.jpg"] = catminiJpgPath;
                            Logger.LogInfo($"Added catmini.jpg to backup for category: {categoryName}");
                        }
                    }
                    
                    // Add catprev-wide (check both formats)
                    var catprevWideWebpPath = Path.Combine(categoryDir, "catprev-wide.webp");
                    var catprevWideJpgPath = Path.Combine(categoryDir, "catprev-wide.jpg");
                    if (File.Exists(catprevWideWebpPath))
                    {
                        filesToBackup["catprev-wide.webp"] = catprevWideWebpPath;
                        Logger.LogInfo($"Added catprev-wide.webp to backup for category: {categoryName}");
                    }
                    else if (File.Exists(catprevWideJpgPath))
                    {
                        filesToBackup["catprev-wide.jpg"] = catprevWideJpgPath;
                        Logger.LogInfo($"Added catprev-wide.jpg to backup for category: {categoryName}");
                    }

                    // Add catmini-wide (check both formats)
                    var catminiWideWebpPath = Path.Combine(categoryDir, "catmini-wide.webp");
                    var catminiWideJpgPath = Path.Combine(categoryDir, "catmini-wide.jpg");
                    if (File.Exists(catminiWideWebpPath))
                    {
                        filesToBackup["catmini-wide.webp"] = catminiWideWebpPath;
                        Logger.LogInfo($"Added catmini-wide.webp to backup for category: {categoryName}");
                    }
                    else if (File.Exists(catminiWideJpgPath))
                    {
                        filesToBackup["catmini-wide.jpg"] = catminiWideJpgPath;
                        Logger.LogInfo($"Added catmini-wide.jpg to backup for category: {categoryName}");
                    }
                    
                    if (filesToBackup.Count > 0)
                    {
                        ArchiveHelper.CreateArchiveFromFiles(catBackup1, filesToBackup);
                    }
                    
                    Logger.LogInfo($"Created category backup for: {categoryName}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to create category backups", ex);
            }
            
            // Get all mod directories from all categories
            var modDirs = new List<string>();
            foreach (var categoryDir in Directory.GetDirectories(ModLibraryPath))
            {
                if (Directory.Exists(categoryDir))
                {
                    // Include both regular and DISABLED_ prefixed directories
                    modDirs.AddRange(Directory.GetDirectories(categoryDir));
                }
            }
            
            // Use parallel processing for faster backup creation
            var successCount = 0;
            var processedCount = 0;
            var totalCount = modDirs.Count;
            var lockObj = new object();
            
            var parallelOptions = new System.Threading.Tasks.ParallelOptions
            {
                CancellationToken = cancellationToken
            };
            
            System.Threading.Tasks.Parallel.ForEach(modDirs, parallelOptions, dir =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var modJson = Path.Combine(dir, "mod.json");
                
                if (!File.Exists(modJson))
                {
                    lock (lockObj)
                    {
                        processedCount++;
                        progress?.Report((processedCount, totalCount));
                    }
                    return;
                }
                
                // Use actual folder name (with DISABLED_ prefix if present)
                var modName = new DirectoryInfo(dir).Name;
                
                // Shift existing backups: 2->3, 1->2, new->1
                try
                {
                    var backup3 = Path.Combine(dir, $"{modName}.mib3.zip");
                    var backup2 = Path.Combine(dir, $"{modName}.mib2.zip");
                    var backup1 = Path.Combine(dir, $"{modName}.mib1.zip");
                    
                    // Delete oldest backup (3) if it exists
                    if (File.Exists(backup3))
                    {
                        File.Delete(backup3);
                    }
                    
                    // Move backup2 to backup3
                    if (File.Exists(backup2))
                    {
                        File.Move(backup2, backup3);
                    }
                    
                    // Move backup1 to backup2
                    if (File.Exists(backup1))
                    {
                        File.Move(backup1, backup2);
                    }
                    
                    // Create new backup1
                    var filesToBackup = new Dictionary<string, string>
                    {
                        ["mod.json"] = modJson
                    };
                    
                    // Add minitile (check both formats)
                    var minitileWebpPath = Path.Combine(dir, "minitile.webp");
                    var minitileJpgPath = Path.Combine(dir, "minitile.jpg");
                    if (File.Exists(minitileWebpPath))
                    {
                        filesToBackup["minitile.webp"] = minitileWebpPath;
                    }
                    else if (File.Exists(minitileJpgPath))
                    {
                        filesToBackup["minitile.jpg"] = minitileJpgPath;
                    }
                    
                    // Add minitile-wide (check both formats)
                    var minitileWideWebpPath = Path.Combine(dir, "minitile-wide.webp");
                    var minitileWideJpgPath = Path.Combine(dir, "minitile-wide.jpg");
                    if (File.Exists(minitileWideWebpPath))
                    {
                        filesToBackup["minitile-wide.webp"] = minitileWideWebpPath;
                    }
                    else if (File.Exists(minitileWideJpgPath))
                    {
                        filesToBackup["minitile-wide.jpg"] = minitileWideJpgPath;
                    }
                    
                    // Add all preview images (preview.jpg/webp, preview-01.jpg/webp, etc.)
                    var previewFiles = Directory.GetFiles(dir, "preview*.jpg")
                        .Concat(Directory.GetFiles(dir, "preview*.jpeg"))
                        .Concat(Directory.GetFiles(dir, "preview*.png"))
                        .Concat(Directory.GetFiles(dir, "preview*.webp"))
                        .ToList();
                    
                    Logger.LogInfo($"Found {previewFiles.Count} preview files for mod: {modName}");
                    
                    foreach (var previewFile in previewFiles)
                    {
                        var fileName = Path.GetFileName(previewFile);
                        filesToBackup[fileName] = previewFile;
                        Logger.LogInfo($"Added {fileName} to backup for mod: {modName}");
                    }
                    
                    ArchiveHelper.CreateArchiveFromFiles(backup1, filesToBackup);
                    
                    lock (lockObj)
                    {
                        successCount++;
                        processedCount++;
                        progress?.Report((processedCount, totalCount));
                    }
                    
                    Logger.LogInfo($"Created backup for mod: {modName}");
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        processedCount++;
                        progress?.Report((processedCount, totalCount));
                    }
                    Logger.LogError($"Failed to create backup for mod {modName}", ex);
                }
            });
            
            return successCount;
        }

        private async Task ShowDialog(string title, string content, string closeText)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = closeText,
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }



        private void UpdateBackupInfo()
        {
            // Update each backup slot with its specific backup info
            UpdateBackupInfoFor(1, Backup1Info, RestoreBackup1Button, DeleteBackup1Button);
            UpdateBackupInfoFor(2, Backup2Info, RestoreBackup2Button, DeleteBackup2Button);
            UpdateBackupInfoFor(3, Backup3Info, RestoreBackup3Button, DeleteBackup3Button);
        }

        private void UpdateBackupInfoFor(int backupNum, TextBlock infoBlock, Button restoreButton, Button deleteButton)
        {
            var currentModLibPath = ModLibraryPath;
            if (!Directory.Exists(currentModLibPath))
            {
                infoBlock.Text = $"- (Path: {Path.GetFileName(currentModLibPath)})";
                restoreButton.IsEnabled = false;
                deleteButton.IsEnabled = false;
                return;
            }
            
            // Count specific backup files (mib1.zip, mib2.zip, mib3.zip)
            int count = 0;
            DateTime? newest = null;
            
            try
            {
                // Get all mod directories from all categories
                var modDirs = new List<string>();
                foreach (var categoryDir in Directory.GetDirectories(currentModLibPath))
                {
                    if (Directory.Exists(categoryDir))
                    {
                        modDirs.AddRange(Directory.GetDirectories(categoryDir));
                    }
                }
                
                foreach (var dir in modDirs)
                {
                    var modName = new DirectoryInfo(dir).Name;
                    var backupZip = Path.Combine(dir, $"{modName}.mib{backupNum}.zip");
                    
                    if (File.Exists(backupZip))
                    {
                        count++;
                        var dt = File.GetCreationTime(backupZip);
                        if (newest == null || dt > newest)
                            newest = dt;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to scan backup {backupNum} files", ex);
                infoBlock.Text = $"Error: {ex.Message}";
                restoreButton.IsEnabled = false;
                deleteButton.IsEnabled = false;
                return;
            }
            
            if (count > 0 && newest != null)
            {
                var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
                infoBlock.Text = string.Format(SharedUtilities.GetTranslation(lang, "ModInfoBackup_BackupInfo"), $"{newest:yyyy-MM-dd HH:mm}", count);
                restoreButton.IsEnabled = true;
                deleteButton.IsEnabled = true;
            }
            else
            {
                // Use translation instead of hardcoded string
                var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
                infoBlock.Text = $"- ({string.Format(SharedUtilities.GetTranslation(lang, "ModInfoBackup_NoBackup"), backupNum, Path.GetFileName(currentModLibPath))})";
                restoreButton.IsEnabled = false;
                deleteButton.IsEnabled = false;
            }
        }
    }
}
