using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class StatusKeeperBackupPage : Page
    {
        private StatusKeeperSettings _settings = new();
        
        // Operation state tracking
        private static volatile bool _isCreatingBackup = false;
        private static volatile bool _isCheckingBackup = false;
        private static volatile bool _isRestoringBackup = false;
        private static volatile bool _isDeletingBackups = false;

        public StatusKeeperBackupPage()
        {
            this.InitializeComponent();
            UpdateTexts();
            UpdateButtonStates();
        }

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
            
            // Create backup card
            CreateBackupLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_CreateBackup_Label");
            CreateBackupDescription.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_CreateBackup_Description");
            // Button text is handled by UpdateButtonStates()
            
            // Check backup card
            CheckBackupLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_CheckBackup_Label");
            CheckBackupDescription.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_CheckBackup_Description");
            // Button text is handled by UpdateButtonStates()
            
            // Restore backup card
            RestoreBackupLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_RestoreBackup_Label");
            RestoreBackupDescription.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_RestoreBackup_Description");
            // Button text is handled by UpdateButtonStates()
            
            // Delete backups card
            DeleteBackupsLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_DeleteBackups_Label");
            DeleteBackupsDescription.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_DeleteBackups_Description");
            // Button text is handled by UpdateButtonStates()
        }
        
        /// <summary>
        /// Update button states - disable all buttons when any operation is running
        /// </summary>
        private void UpdateButtonStates()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
            var mainLang = SharedUtilities.LoadLanguageDictionary();
            
            // Check if any operation is currently running
            bool anyOperationRunning = _isCreatingBackup || _isCheckingBackup || _isRestoringBackup || _isDeletingBackups;
            
            // Create Backup button
            if (CreateBackupButton != null && CreateBackupButtonText != null)
            {
                CreateBackupButton.IsEnabled = !anyOperationRunning || _isCreatingBackup;
                CreateBackupButtonText.Text = _isCreatingBackup 
                    ? SharedUtilities.GetTranslation(lang, "StatusKeeper_Creating_Backup")
                    : SharedUtilities.GetTranslation(lang, "StatusKeeper_CreateBackup_Button");
            }
            
            // Check Backup button
            if (CheckBackupButton != null && CheckBackupButtonText != null)
            {
                CheckBackupButton.IsEnabled = !anyOperationRunning || _isCheckingBackup;
                CheckBackupButtonText.Text = _isCheckingBackup 
                    ? SharedUtilities.GetTranslation(lang, "StatusKeeper_Checking")
                    : SharedUtilities.GetTranslation(lang, "StatusKeeper_CheckBackups_Button");
            }
            
            // Restore Backup button
            if (RestoreBackupButton != null && RestoreBackupButtonText != null)
            {
                RestoreBackupButton.IsEnabled = !anyOperationRunning || _isRestoringBackup;
                RestoreBackupButtonText.Text = _isRestoringBackup 
                    ? SharedUtilities.GetTranslation(lang, "StatusKeeper_Restoring")
                    : SharedUtilities.GetTranslation(lang, "StatusKeeper_RestoreBackup_Button");
            }
            
            // Delete Backups button
            if (DeleteBackupsButton != null && DeleteBackupsButtonText != null)
            {
                DeleteBackupsButton.IsEnabled = !anyOperationRunning || _isDeletingBackups;
                DeleteBackupsButtonText.Text = _isDeletingBackups 
                    ? SharedUtilities.GetTranslation(lang, "StatusKeeper_Deleting")
                    : SharedUtilities.GetTranslation(lang, "StatusKeeper_DeleteBackups_Button");
            }
            
            // Update progress bars and status texts
            UpdateProgressBars();
        }
        
        /// <summary>
        /// Update progress bars and status texts visibility
        /// </summary>
        private void UpdateProgressBars()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
            
            // Create Backup progress
            if (CreateBackupProgressBar != null && CreateBackupStatusText != null)
            {
                CreateBackupProgressBar.Visibility = _isCreatingBackup ? Visibility.Visible : Visibility.Collapsed;
                CreateBackupStatusText.Visibility = _isCreatingBackup ? Visibility.Visible : Visibility.Collapsed;
                CreateBackupStatusText.Text = _isCreatingBackup ? SharedUtilities.GetTranslation(lang, "StatusKeeper_Creating_Backup") : "";
            }
            
            // Check Backup progress
            if (CheckBackupProgressBar != null && CheckBackupStatusText != null)
            {
                CheckBackupProgressBar.Visibility = _isCheckingBackup ? Visibility.Visible : Visibility.Collapsed;
                CheckBackupStatusText.Visibility = _isCheckingBackup ? Visibility.Visible : Visibility.Collapsed;
                CheckBackupStatusText.Text = _isCheckingBackup ? SharedUtilities.GetTranslation(lang, "StatusKeeper_Checking") : "";
            }
            
            // Restore Backup progress
            if (RestoreBackupProgressBar != null && RestoreBackupStatusText != null)
            {
                RestoreBackupProgressBar.Visibility = _isRestoringBackup ? Visibility.Visible : Visibility.Collapsed;
                RestoreBackupStatusText.Visibility = _isRestoringBackup ? Visibility.Visible : Visibility.Collapsed;
                RestoreBackupStatusText.Text = _isRestoringBackup ? SharedUtilities.GetTranslation(lang, "StatusKeeper_Restoring") : "";
            }
            
            // Delete Backups progress
            if (DeleteBackupsProgressBar != null && DeleteBackupsStatusText != null)
            {
                DeleteBackupsProgressBar.Visibility = _isDeletingBackups ? Visibility.Visible : Visibility.Collapsed;
                DeleteBackupsStatusText.Visibility = _isDeletingBackups ? Visibility.Visible : Visibility.Collapsed;
                DeleteBackupsStatusText.Text = _isDeletingBackups ? SharedUtilities.GetTranslation(lang, "StatusKeeper_Deleting") : "";
            }
        }



        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is StatusKeeperSettings settings)
            {
                _settings = settings;
            }
        }

        private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                
                _isCreatingBackup = true;
                UpdateButtonStates();

                int backupCount = 0;
                int skipCount = 0;

                var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                

                // Count existing backups before operation
                int beforeBackup = 0;
                if (Directory.Exists(modLibraryPath))
                {
                    beforeBackup = Directory.GetFiles(modLibraryPath, "*.msk", SearchOption.AllDirectories).Length;
                }

                await Task.Run(() => BackupIniFiles(modLibraryPath, ref backupCount, ref skipCount));

                // Count backups after operation
                int afterBackup = 0;
                if (Directory.Exists(modLibraryPath))
                {
                    afterBackup = Directory.GetFiles(modLibraryPath, "*.msk", SearchOption.AllDirectories).Length;
                }

                int newBackups = afterBackup - beforeBackup;
                int existingBackups = afterBackup - newBackups;

                // Message in 3 lines
                var message = string.Format(SharedUtilities.GetTranslation(lang, "StatusKeeper_CreateBackup_Dialog_Message"), newBackups, afterBackup).Replace("\\n", "\n");
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "StatusKeeper_CreateBackup_Dialog_Title"),
                    Content = message,
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();

                Logger.LogInfo($"Backup complete! Created {backupCount} .msk files, skipped {skipCount} existing/disabled files");
            }
            catch (Exception error)
            {
                Logger.LogError($"Backup failed: {error.Message}");
            }
            finally
            {
                _isCreatingBackup = false;
                UpdateButtonStates();
            }
        }

        private async void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.LogDebug("RestoreBackupButton clicked");
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                var mainLang = SharedUtilities.LoadLanguageDictionary();

                Logger.LogDebug("Showing confirmation dialog");
                // Show confirmation dialog
                var confirmDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "StatusKeeper_ConfirmRestore_Title"),
                    Content = SharedUtilities.GetTranslation(lang, "StatusKeeper_ConfirmRestore_Message"),
                    PrimaryButtonText = SharedUtilities.GetTranslation(mainLang, "Continue"),
                    CloseButtonText = SharedUtilities.GetTranslation(mainLang, "Cancel"),
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();
                Logger.LogDebug($"Dialog result: {result}");
                if (result != ContentDialogResult.Primary)
                    return;

                _isRestoringBackup = true;
                UpdateButtonStates();

                int restoreCount = 0;
                int skipCount = 0;

                var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                

                await Task.Run(() => RestoreFromBackups(modLibraryPath, ref restoreCount, ref skipCount));

                // Show success dialog with results
                var message = string.Format(SharedUtilities.GetTranslation(lang, "StatusKeeper_RestoreBackup_Dialog_Message"), restoreCount, skipCount);
                var successDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "StatusKeeper_RestoreBackup_Dialog_Title"),
                    Content = message,
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                    XamlRoot = this.XamlRoot
                };
                await successDialog.ShowAsync();

                Logger.LogInfo($"Restore complete! Restored {restoreCount} files, failed {skipCount} files");
            }
            catch (Exception error)
            {
                Logger.LogError($"Restore failed: {error.Message}");
            }
            finally
            {
                _isRestoringBackup = false;
                UpdateButtonStates();
            }
        }

        private async void DeleteBackupsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.LogDebug("DeleteBackupsButton clicked");
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                var mainLang = SharedUtilities.LoadLanguageDictionary();

                Logger.LogDebug("Showing confirmation dialog");
                // Show confirmation dialog
                var confirmDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "StatusKeeper_ConfirmDelete_Title"),
                    Content = SharedUtilities.GetTranslation(lang, "StatusKeeper_ConfirmDelete_Message"),
                    PrimaryButtonText = SharedUtilities.GetTranslation(mainLang, "Continue"),
                    CloseButtonText = SharedUtilities.GetTranslation(mainLang, "Cancel"),
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();
                Logger.LogDebug($"Dialog result: {result}");
                if (result != ContentDialogResult.Primary)
                    return;

                _isDeletingBackups = true;
                UpdateButtonStates();

                int deleteCount = 0;

                var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                

                await Task.Run(() => DeleteBackups(modLibraryPath, ref deleteCount));

                // Show success dialog with results
                var message = string.Format(SharedUtilities.GetTranslation(lang, "StatusKeeper_DeleteBackups_Dialog_Message"), deleteCount);
                var successDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "StatusKeeper_DeleteBackups_Dialog_Title"),
                    Content = message,
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                    XamlRoot = this.XamlRoot
                };
                await successDialog.ShowAsync();

                Logger.LogInfo($"Deletion complete! Deleted {deleteCount} backup files");
            }
            catch (Exception error)
            {
                Logger.LogError($"Delete failed: {error.Message}");
            }
            finally
            {
                _isDeletingBackups = false;
                UpdateButtonStates();
            }
        }

        private async void CheckBackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                
                _isCheckingBackup = true;
                UpdateButtonStates();
                
                var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                
                int iniCount = 0;
                int incompleteCount = 0;

                await Task.Run(() =>
                {
                    if (Directory.Exists(modLibraryPath))
                    {
                        // Get all .ini files (excluding those with "disabled", "_lod1.ini" and "_lod2.ini" in name)
                        var iniFiles = Directory.GetFiles(modLibraryPath, "*.ini", SearchOption.AllDirectories)
                            .Where(f => {
                                var fileName = Path.GetFileName(f).ToLower();
                                return !fileName.Contains("disabled") && 
                                       !fileName.Contains("_lod1.ini") && 
                                       !fileName.Contains("_lod2.ini") &&
                                       !fileName.Contains("_lod");
                            })
                            .ToArray();
                        
                        iniCount = iniFiles.Length;
                        foreach (var ini in iniFiles)
                        {
                            var backup = ini + ".msk";
                            if (!File.Exists(backup))
                            {
                                incompleteCount++;
                            }
                        }
                    }
                });

                // Use language keys for dialog title and content
                string message = string.Format(SharedUtilities.GetTranslation(lang, "StatusKeeper_CheckBackup_Dialog_Message"), 
                    iniCount, iniCount - incompleteCount, incompleteCount);

                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "StatusKeeper_CheckBackup_Dialog_Title"),
                    Content = message,
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            finally
            {
                _isCheckingBackup = false;
                UpdateButtonStates();
            }
        }

        // ==================== BACKUP OPERATIONS ====================
        
        private void BackupIniFiles(string dir, ref int backupCount, ref int skipCount)
        {
            if (!Directory.Exists(dir)) return;

            try
            {
                var items = Directory.GetFileSystemEntries(dir);

                foreach (var item in items)
                {
                    if (Directory.Exists(item))
                    {
                        // Process all directories including DISABLED_ (backup everything)
                        BackupIniFiles(item, ref backupCount, ref skipCount);
                    }
                    else if (File.Exists(item) && item.ToLower().EndsWith(".ini"))
                    {
                        var fileName = Path.GetFileName(item);

                        // Skip files with "disabled", "_lod1.ini", or "_lod2.ini" in the name (case-insensitive)
                        var lowerFileName = fileName.ToLower();
                        if (lowerFileName.Contains("disabled") || 
                            lowerFileName.Contains("_lod1.ini") || 
                            lowerFileName.Contains("_lod2.ini") ||
                            lowerFileName.Contains("_lod"))
                        {
                            skipCount++;
                            continue;
                        }

                        // Generate backup filename with .msk extension
                        var backupFileName = fileName + ".msk";
                        var backupFilePath = Path.Combine(Path.GetDirectoryName(item) ?? "", backupFileName);

                        // Skip if backup already exists (prevent duplicates)
                        if (File.Exists(backupFilePath))
                        {
                            skipCount++;
                            continue;
                        }

                        try
                        {
                            // Copy INI file to .msk backup file
                            File.Copy(item, backupFilePath);
                            backupCount++;
                        }
                        catch (Exception err)
                        {
                            Logger.LogStatusKeeperError($"Failed to backup {item}: {err.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogStatusKeeperError($"Error in BackupIniFiles for {dir}: {ex.Message}");
            }
        }

        private void RestoreFromBackups(string dir, ref int restoreCount, ref int skipCount)
        {
            if (!Directory.Exists(dir)) return;

            try
            {
                var items = Directory.GetFileSystemEntries(dir);

                foreach (var item in items)
                {
                    if (Directory.Exists(item))
                    {
                        // Restore ALL backups, including in DISABLED_ directories
                        RestoreFromBackups(item, ref restoreCount, ref skipCount);
                    }
                    else if (File.Exists(item) && item.ToLower().EndsWith(".msk"))
                    {
                        // Calculate original INI filename by removing .msk extension
                        var originalFileName = Path.GetFileNameWithoutExtension(item);
                        var originalFilePath = Path.Combine(Path.GetDirectoryName(item) ?? "", originalFileName);

                        try
                        {
                            // Copy .msk file back to original .ini filename
                            File.Copy(item, originalFilePath, true);
                            restoreCount++;
                        }
                        catch (Exception err)
                        {
                            Logger.LogStatusKeeperError($"Failed to restore {item}: {err.Message}");
                            skipCount++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogStatusKeeperError($"Error in RestoreFromBackups for {dir}: {ex.Message}");
            }
        }

        private void DeleteBackups(string dir, ref int deleteCount)
        {
            if (!Directory.Exists(dir)) return;

            try
            {
                var items = Directory.GetFileSystemEntries(dir);

                foreach (var item in items)
                {
                    if (Directory.Exists(item))
                    {
                        // Delete ALL backups, including in DISABLED_ directories
                        DeleteBackups(item, ref deleteCount);
                    }
                    else if (File.Exists(item) && item.ToLower().EndsWith(".msk"))
                    {
                        try
                        {
                            // Permanently delete the backup file
                            File.Delete(item);
                            deleteCount++;
                        }
                        catch (Exception err)
                        {
                            Logger.LogStatusKeeperError($"Failed to delete {item}: {err.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogStatusKeeperError($"Error in DeleteBackups for {dir}: {ex.Message}");
            }
        }


    }
}
