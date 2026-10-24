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
        private static volatile bool _isRestoringBackup1 = false;
        private static volatile bool _isRestoringBackup2 = false;
        private static volatile bool _isRestoringBackup3 = false;
        private static volatile bool _isDeletingBackup1 = false;
        private static volatile bool _isDeletingBackup2 = false;
        private static volatile bool _isDeletingBackup3 = false;
        
        /// <summary>
        /// Update button states - disable all buttons when any operation is running
        /// </summary>
        private void UpdateButtonStates()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
            
            // Check if any operation is currently running
            bool anyOperationRunning = _isCreatingBackup || _isRestoringBackup1 || _isRestoringBackup2 || _isRestoringBackup3 ||
                                     _isDeletingBackup1 || _isDeletingBackup2 || _isDeletingBackup3;
            
            // Create Backup button - disable when creating, show as Cancel (red)
            if (CreateBackupsButton != null && CreateBackupsText != null)
            {
                if (_isCreatingBackup)
                {
                    CreateBackupsButton.IsEnabled = true;
                    CreateBackupsButton.Style = (Style)this.Resources["RedCancelButtonStyle"];
                    CreateBackupsText.Text = SharedUtilities.GetTranslation(lang, "Cancel");
                }
                else
                {
                    CreateBackupsButton.IsEnabled = true;
                    CreateBackupsButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                    CreateBackupsText.Text = SharedUtilities.GetTranslation(lang, "Create");
                }
            }
            
            // Restore Backup 1 button
            if (RestoreBackup1Button != null && RestoreBackup1Text != null)
            {
                RestoreBackup1Button.IsEnabled = !anyOperationRunning && RestoreBackup1Button.Tag != null;
                RestoreBackup1Text.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restore");
            }
            
            // Restore Backup 2 button
            if (RestoreBackup2Button != null && RestoreBackup2Text != null)
            {
                RestoreBackup2Button.IsEnabled = !anyOperationRunning && RestoreBackup2Button.Tag != null;
                RestoreBackup2Text.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restore");
            }
            
            // Restore Backup 3 button
            if (RestoreBackup3Button != null && RestoreBackup3Text != null)
            {
                RestoreBackup3Button.IsEnabled = !anyOperationRunning && RestoreBackup3Button.Tag != null;
                RestoreBackup3Text.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restore");
            }
            
            // Delete buttons
            if (DeleteBackup1Button != null)
            {
                DeleteBackup1Button.IsEnabled = !anyOperationRunning;
            }
            
            if (DeleteBackup2Button != null)
            {
                DeleteBackup2Button.IsEnabled = !anyOperationRunning;
            }
            
            if (DeleteBackup3Button != null)
            {
                DeleteBackup3Button.IsEnabled = !anyOperationRunning;
            }
            
            // Update progress bars and status texts
            UpdateProgressBars();
        }
        
        /// <summary>
        /// Update progress bars and status texts visibility
        /// </summary>
        private void UpdateProgressBars()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
            
            // Create Backup progress
            if (CreateBackupsProgressBar != null && CreateBackupsStatusText != null)
            {
                CreateBackupsProgressBar.Visibility = _isCreatingBackup ? Visibility.Visible : Visibility.Collapsed;
                CreateBackupsStatusText.Visibility = _isCreatingBackup ? Visibility.Visible : Visibility.Collapsed;
                CreateBackupsStatusText.Text = _isCreatingBackup ? SharedUtilities.GetTranslation(lang, "ModInfoBackup_Creating") : "";
            }
            
            // Restore Backup 1 progress
            if (RestoreBackup1ProgressBar != null && RestoreBackup1StatusText != null)
            {
                RestoreBackup1ProgressBar.Visibility = (_isRestoringBackup1 || _isDeletingBackup1) ? Visibility.Visible : Visibility.Collapsed;
                RestoreBackup1StatusText.Visibility = (_isRestoringBackup1 || _isDeletingBackup1) ? Visibility.Visible : Visibility.Collapsed;
                if (_isRestoringBackup1)
                    RestoreBackup1StatusText.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restoring");
                else if (_isDeletingBackup1)
                    RestoreBackup1StatusText.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Deleting");
                else
                    RestoreBackup1StatusText.Text = "";
            }
            
            // Restore Backup 2 progress
            if (RestoreBackup2ProgressBar != null && RestoreBackup2StatusText != null)
            {
                RestoreBackup2ProgressBar.Visibility = (_isRestoringBackup2 || _isDeletingBackup2) ? Visibility.Visible : Visibility.Collapsed;
                RestoreBackup2StatusText.Visibility = (_isRestoringBackup2 || _isDeletingBackup2) ? Visibility.Visible : Visibility.Collapsed;
                if (_isRestoringBackup2)
                    RestoreBackup2StatusText.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restoring");
                else if (_isDeletingBackup2)
                    RestoreBackup2StatusText.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Deleting");
                else
                    RestoreBackup2StatusText.Text = "";
            }
            
            // Restore Backup 3 progress
            if (RestoreBackup3ProgressBar != null && RestoreBackup3StatusText != null)
            {
                RestoreBackup3ProgressBar.Visibility = (_isRestoringBackup3 || _isDeletingBackup3) ? Visibility.Visible : Visibility.Collapsed;
                RestoreBackup3StatusText.Visibility = (_isRestoringBackup3 || _isDeletingBackup3) ? Visibility.Visible : Visibility.Collapsed;
                if (_isRestoringBackup3)
                    RestoreBackup3StatusText.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restoring");
                else if (_isDeletingBackup3)
                    RestoreBackup3StatusText.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Deleting");
                else
                    RestoreBackup3StatusText.Text = "";
            }
        }

        private async void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int backupNum) && backupNum >= 1 && backupNum <= MaxBackups)
                {
                    try
                    {
                        // Show confirmation dialog before restoring
                        var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
                        ContentDialog confirmDialog = new ContentDialog
                        {
                            Title = SharedUtilities.GetTranslation(lang, "ModInfoBackup_RestoreConfirm_Title"),
                            Content = string.Format(SharedUtilities.GetTranslation(lang, "ModInfoBackup_RestoreConfirm_Message"), backupNum),
                            PrimaryButtonText = SharedUtilities.GetTranslation(lang, "Yes"),
                            CloseButtonText = SharedUtilities.GetTranslation(lang, "No"),
                            XamlRoot = this.XamlRoot
                        };

                        ContentDialogResult result = await confirmDialog.ShowAsync();
                        if (result != ContentDialogResult.Primary)
                        {
                            return;
                        }

                        // Set operation state based on backup number
                        switch (backupNum)
                        {
                            case 1: _isRestoringBackup1 = true; break;
                            case 2: _isRestoringBackup2 = true; break;
                            case 3: _isRestoringBackup3 = true; break;
                        }
                        UpdateButtonStates();

                        // Progress reporting
                        var progress = new Progress<(int current, int total)>(value =>
                        {
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                ProgressBar? progressBar = backupNum switch
                                {
                                    1 => RestoreBackup1ProgressBar,
                                    2 => RestoreBackup2ProgressBar,
                                    3 => RestoreBackup3ProgressBar,
                                    _ => null
                                };

                                TextBlock? statusText = backupNum switch
                                {
                                    1 => RestoreBackup1StatusText,
                                    2 => RestoreBackup2StatusText,
                                    3 => RestoreBackup3StatusText,
                                    _ => null
                                };

                                if (progressBar != null && statusText != null)
                                {
                                    var percentage = value.total > 0 ? (double)value.current / value.total * 100 : 0;
                                    progressBar.Value = percentage;
                                    statusText.Text = $"{value.current}/{value.total}";
                                }
                            });
                        });

                        int count = await Task.Run(() => RestoreAllBackups(backupNum, progress));

                        // Reset operation state
                        switch (backupNum)
                        {
                            case 1: _isRestoringBackup1 = false; break;
                            case 2: _isRestoringBackup2 = false; break;
                            case 3: _isRestoringBackup3 = false; break;
                        }
                        UpdateButtonStates();

                        // Show success dialog
                        if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowSuccessInfo(string.Format(SharedUtilities.GetTranslation(lang, "ModInfoBackup_RestoreComplete"), backupNum, count));
                        UpdateBackupInfo();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error in RestoreBackupButton_Click", ex);

                        // Reset operation state on error
                        switch (backupNum)
                        {
                            case 1: _isRestoringBackup1 = false; break;
                            case 2: _isRestoringBackup2 = false; break;
                            case 3: _isRestoringBackup3 = false; break;
                        }
                        UpdateButtonStates();

                        var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
                        if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(ex.Message);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModInfoBackupPage.RestoreBackupButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModInfoBackupPage.RestoreBackupButton_Click failed", ex);
            }
        }
        


        private int RestoreAllBackups(int backupNum, IProgress<(int current, int total)> progress)
        {
            int count = 0;
            if (!Directory.Exists(ModLibraryPath)) return count;
            
            // First, restore category preview images
            try
            {
                foreach (var categoryDir in Directory.GetDirectories(ModLibraryPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = new DirectoryInfo(categoryDir).Name;
                    var catBackupZip = Path.Combine(categoryDir, $"{categoryName}_category.mib{backupNum}.zip");
                    
                    if (!File.Exists(catBackupZip)) continue;
                    
                    try
                    {
                        ArchiveHelper.ExtractToDirectory(catBackupZip, categoryDir);
                        Logger.LogInfo($"Restored category backup {backupNum} for: {categoryName}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to restore category backup {backupNum} for {categoryName}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to restore category backups", ex);
            }
            
            // Get all mod directories from all categories
            var modDirs = new List<string>();
            foreach (var categoryDir in Directory.GetDirectories(ModLibraryPath))
            {
                if (Directory.Exists(categoryDir))
                {
                    modDirs.AddRange(Directory.GetDirectories(categoryDir));
                }
            }
            
            // Use parallel processing for faster restore
            var successCount = 0;
            var processedCount = 0;
            var totalCount = modDirs.Count;
            var lockObj = new object();
            
            System.Threading.Tasks.Parallel.ForEach(modDirs, dir =>
            {
                var modName = new DirectoryInfo(dir).Name;
                var backupZip = Path.Combine(dir, $"{modName}.mib{backupNum}.zip");
                
                if (!File.Exists(backupZip))
                {
                    lock (lockObj)
                    {
                        processedCount++;
                        progress?.Report((processedCount, totalCount));
                    }
                    return;
                }
                
                try
                {
                    ArchiveHelper.ExtractToDirectory(backupZip, dir);
                    
                    lock (lockObj)
                    {
                        successCount++;
                        processedCount++;
                        progress?.Report((processedCount, totalCount));
                    }
                    
                    Logger.LogInfo($"Restored backup {backupNum} for mod: {modName}");
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        processedCount++;
                        progress?.Report((processedCount, totalCount));
                    }
                    Logger.LogError($"Failed to restore backup {backupNum} for mod {modName}", ex);
                }
            });
            
            return successCount;
        }

        private async void DeleteBackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int backupNum) && backupNum >= 1 && backupNum <= MaxBackups)
                {
                    try
                    {
                        // Show confirmation dialog before deleting
                        var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
                        ContentDialog confirmDialog = new ContentDialog
                        {
                            Title = SharedUtilities.GetTranslation(lang, "ModInfoBackup_DeleteConfirm_Title"),
                            Content = string.Format(SharedUtilities.GetTranslation(lang, "ModInfoBackup_DeleteConfirm_Message"), backupNum),
                            PrimaryButtonText = SharedUtilities.GetTranslation(lang, "Yes"),
                            CloseButtonText = SharedUtilities.GetTranslation(lang, "No"),
                            XamlRoot = this.XamlRoot
                        };

                        ContentDialogResult result = await confirmDialog.ShowAsync();
                        if (result != ContentDialogResult.Primary)
                        {
                            return;
                        }

                        // Set operation state based on backup number
                        switch (backupNum)
                        {
                            case 1: _isDeletingBackup1 = true; break;
                            case 2: _isDeletingBackup2 = true; break;
                            case 3: _isDeletingBackup3 = true; break;
                        }
                        UpdateButtonStates();

                        // Progress reporting
                        var progress = new Progress<(int current, int total)>(value =>
                        {
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                ProgressBar? progressBar = backupNum switch
                                {
                                    1 => RestoreBackup1ProgressBar,
                                    2 => RestoreBackup2ProgressBar,
                                    3 => RestoreBackup3ProgressBar,
                                    _ => null
                                };

                                TextBlock? statusText = backupNum switch
                                {
                                    1 => RestoreBackup1StatusText,
                                    2 => RestoreBackup2StatusText,
                                    3 => RestoreBackup3StatusText,
                                    _ => null
                                };

                                if (progressBar != null && statusText != null)
                                {
                                    var percentage = value.total > 0 ? (double)value.current / value.total * 100 : 0;
                                    progressBar.Value = percentage;
                                    statusText.Text = $"{value.current}/{value.total}";
                                }
                            });
                        });

                        int count = await Task.Run(() => DeleteAllBackups(backupNum, progress));

                        // Reset operation state
                        switch (backupNum)
                        {
                            case 1: _isDeletingBackup1 = false; break;
                            case 2: _isDeletingBackup2 = false; break;
                            case 3: _isDeletingBackup3 = false; break;
                        }
                        UpdateButtonStates();

                        // Show success dialog
                        if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowSuccessInfo(string.Format(SharedUtilities.GetTranslation(lang, "ModInfoBackup_DeleteComplete"), backupNum, count));
                        UpdateBackupInfo();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error in DeleteBackupButton_Click", ex);

                        // Reset operation state on error
                        switch (backupNum)
                        {
                            case 1: _isDeletingBackup1 = false; break;
                            case 2: _isDeletingBackup2 = false; break;
                            case 3: _isDeletingBackup3 = false; break;
                        }
                        UpdateButtonStates();

                        var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
                        if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(ex.Message);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModInfoBackupPage.DeleteBackupButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModInfoBackupPage.DeleteBackupButton_Click failed", ex);
            }
        }

        private int DeleteAllBackups(int backupNum, IProgress<(int current, int total)> progress)
        {
            int count = 0;
            if (!Directory.Exists(ModLibraryPath)) return count;
            
            // First, delete category backups
            try
            {
                foreach (var categoryDir in Directory.GetDirectories(ModLibraryPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = new DirectoryInfo(categoryDir).Name;
                    var catBackupZip = Path.Combine(categoryDir, $"{categoryName}_category.mib{backupNum}.zip");
                    
                    if (File.Exists(catBackupZip))
                    {
                        try
                        {
                            File.Delete(catBackupZip);
                            Logger.LogInfo($"Deleted category backup {backupNum} for: {categoryName}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to delete category backup {backupNum} for {categoryName}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to delete category backups", ex);
            }
            
            // Get all mod directories from all categories
            var modDirs = new List<string>();
            foreach (var categoryDir in Directory.GetDirectories(ModLibraryPath))
            {
                if (Directory.Exists(categoryDir))
                {
                    modDirs.AddRange(Directory.GetDirectories(categoryDir));
                }
            }
            
            // Use parallel processing for faster deletion
            var successCount = 0;
            var processedCount = 0;
            var totalCount = modDirs.Count;
            var lockObj = new object();
            
            System.Threading.Tasks.Parallel.ForEach(modDirs, dir =>
            {
                var modName = new DirectoryInfo(dir).Name;
                var backupZip = Path.Combine(dir, $"{modName}.mib{backupNum}.zip");
                
                if (!File.Exists(backupZip))
                {
                    lock (lockObj)
                    {
                        processedCount++;
                        progress?.Report((processedCount, totalCount));
                    }
                    return;
                }
                
                try
                {
                    File.Delete(backupZip);
                    
                    lock (lockObj)
                    {
                        successCount++;
                        processedCount++;
                        progress?.Report((processedCount, totalCount));
                    }
                    
                    Logger.LogInfo($"Deleted backup {backupNum} for mod: {modName}");
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        processedCount++;
                        progress?.Report((processedCount, totalCount));
                    }
                    Logger.LogError($"Failed to delete backup {backupNum} for mod {modName}", ex);
                }
            });
            
            return successCount;
        }
    }
}
