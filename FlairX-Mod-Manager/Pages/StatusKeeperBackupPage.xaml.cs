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

        public StatusKeeperBackupPage()
        {
            this.InitializeComponent();
            UpdateTexts();
        }

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
            CreateBackupLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_CreateBackup_Label");
            CreateBackupButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_CreateBackup_Button");
            RestoreBackupLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_RestoreBackup_Label");
            RestoreBackupButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_RestoreBackup_Button");
            DeleteBackupsLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_DeleteBackups_Label");
            DeleteBackupsButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_DeleteBackups_Button");
            CheckBackupButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_CheckBackups_Button");
            // Set window title to translation
            if (Window.Current is not null)
            {
                if (Window.Current is Microsoft.UI.Xaml.Window win)
                {
                    win.Title = SharedUtilities.GetTranslation(lang, "Title");
                }
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
                CreateBackupButton.IsEnabled = false;
                CreateBackupProgressBar.Visibility = Visibility.Visible;
                CreateBackupButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_Creating_Backup");

                int backupCount = 0;
                int skipCount = 0;

                var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");

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

                Debug.WriteLine($"Backup complete! Created {backupCount} .msk files, skipped {skipCount} existing/disabled files");
            }
            catch (Exception error)
            {
                Debug.WriteLine($"Backup failed: {error.Message}");
            }
            finally
            {
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                CreateBackupProgressBar.Visibility = Visibility.Collapsed;
                CreateBackupButton.IsEnabled = true;
                CreateBackupButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_CreateBackup_Button");
            }
        }

        private async void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("RestoreBackupButton clicked");
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                var mainLang = SharedUtilities.LoadLanguageDictionary();

                Debug.WriteLine("Showing confirmation dialog");
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
                Debug.WriteLine($"Dialog result: {result}");
                if (result != ContentDialogResult.Primary)
                    return;

                RestoreBackupButton.IsEnabled = false;
                RestoreBackupButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_Restoring");

                int restoreCount = 0;
                int skipCount = 0;

                var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");

                await Task.Run(() => RestoreFromBackups(modLibraryPath, ref restoreCount, ref skipCount));

                Debug.WriteLine($"Restore complete! Restored {restoreCount} files, failed {skipCount} files");
            }
            catch (Exception error)
            {
                Debug.WriteLine($"Restore failed: {error.Message}");
            }
            finally
            {
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                RestoreBackupButton.IsEnabled = true;
                RestoreBackupButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_RestoreBackup_Button");
            }
        }

        private async void DeleteBackupsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("DeleteBackupsButton clicked");
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                var mainLang = SharedUtilities.LoadLanguageDictionary();

                Debug.WriteLine("Showing confirmation dialog");
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
                Debug.WriteLine($"Dialog result: {result}");
                if (result != ContentDialogResult.Primary)
                    return;

                DeleteBackupsButton.IsEnabled = false;
                DeleteBackupsButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_Deleting");

                int deleteCount = 0;

                var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");

                await Task.Run(() => DeleteBackups(modLibraryPath, ref deleteCount));

                Debug.WriteLine($"Deletion complete! Deleted {deleteCount} backup files");
            }
            catch (Exception error)
            {
                Debug.WriteLine($"Delete failed: {error.Message}");
            }
            finally
            {
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                DeleteBackupsButton.IsEnabled = true;
                DeleteBackupsButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_DeleteBackups_Button");
            }
        }

        private async void CheckBackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                CheckBackupButton.IsEnabled = false;
                CheckBackupProgressBar.Visibility = Visibility.Visible;
                
                var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
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
                CheckBackupProgressBar.Visibility = Visibility.Collapsed;
                CheckBackupButton.IsEnabled = true;
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
                            Debug.WriteLine($"Failed to backup {item}: {err.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in BackupIniFiles for {dir}: {ex.Message}");
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
                            Debug.WriteLine($"Failed to restore {item}: {err.Message}");
                            skipCount++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in RestoreFromBackups for {dir}: {ex.Message}");
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
                            Debug.WriteLine($"Failed to delete {item}: {err.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DeleteBackups for {dir}: {ex.Message}");
            }
        }


    }
}
