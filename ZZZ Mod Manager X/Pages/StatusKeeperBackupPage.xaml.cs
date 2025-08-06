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

namespace ZZZ_Mod_Manager_X.Pages
{
    public sealed partial class StatusKeeperBackupPage : Page
    {
        private Dictionary<string, string> _lang = new();
        private StatusKeeperSettings _settings = new();

        public StatusKeeperBackupPage()
        {
            this.InitializeComponent();
            LoadLanguage();
            UpdateTexts();
        }

        private void LoadLanguage()
        {
            _lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
        }

        private string T(string key)
        {
            return SharedUtilities.GetTranslation(_lang, key);
        }

        private void UpdateTexts()
        {
            CreateBackupLabel.Text = T("StatusKeeper_CreateBackup_Label");
            CreateBackupButtonText.Text = T("StatusKeeper_CreateBackup_Button");
            SafetyOverrideLabel.Text = T("StatusKeeper_SafetyOverride_Label");
            RestoreBackupLabel.Text = T("StatusKeeper_RestoreBackup_Label");
            RestoreBackupButtonText.Text = T("StatusKeeper_RestoreBackup_Button");
            DeleteBackupsLabel.Text = T("StatusKeeper_DeleteBackups_Label");
            DeleteBackupsButtonText.Text = T("StatusKeeper_DeleteBackups_Button");
            CheckBackupButtonText.Text = T("StatusKeeper_CheckBackups_Button");
            // Set window title to translation
            if (Window.Current is not null)
            {
                if (Window.Current is Microsoft.UI.Xaml.Window win)
                {
                    win.Title = T("Title");
                }
            }
        }

        private void LoadSettingsToUI()
        {
            // Load from SettingsManager instead of local _settings
            SafetyOverride1Toggle.IsOn = SettingsManager.Current.StatusKeeperBackupOverride1Enabled;
            SafetyOverride2Toggle.IsOn = SettingsManager.Current.StatusKeeperBackupOverride2Enabled;
            SafetyOverride3Toggle.IsOn = SettingsManager.Current.StatusKeeperBackupOverride3Enabled;
            
            // Update _settings to match
            _settings.BackupOverride1Enabled = SettingsManager.Current.StatusKeeperBackupOverride1Enabled;
            _settings.BackupOverride2Enabled = SettingsManager.Current.StatusKeeperBackupOverride2Enabled;
            _settings.BackupOverride3Enabled = SettingsManager.Current.StatusKeeperBackupOverride3Enabled;
            
            // Enable/disable buttons based on safety toggles
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool allSafetyTogglesOn = SafetyOverride1Toggle.IsOn && SafetyOverride2Toggle.IsOn && SafetyOverride3Toggle.IsOn;
            RestoreBackupButton.IsEnabled = allSafetyTogglesOn;
            DeleteBackupsButton.IsEnabled = allSafetyTogglesOn;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is StatusKeeperSettings settings)
            {
                _settings = settings;
                LoadSettingsToUI();
            }
        }

        private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CreateBackupButton.IsEnabled = false;
                CreateBackupProgressBar.Visibility = Visibility.Visible;
                CreateBackupButtonText.Text = T("StatusKeeper_Creating_Backup");

                int backupCount = 0;
                int skipCount = 0;

                var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? 
                                   Path.Combine(AppContext.BaseDirectory, "ModLibrary");

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
                var message = string.Format(T("StatusKeeper_CreateBackup_Dialog_Message"), newBackups, afterBackup).Replace("\\n", "\n");
                var dialog = new ContentDialog
                {
                    Title = T("StatusKeeper_CreateBackup_Dialog_Title"),
                    Content = message,
                    CloseButtonText = T("OK"),
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
                CreateBackupProgressBar.Visibility = Visibility.Collapsed;
                CreateBackupButton.IsEnabled = true;
                CreateBackupButtonText.Text = T("StatusKeeper_CreateBackup_Button");
            }
        }

        private async void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_settings.BackupOverrideEnabled)
            {
                Debug.WriteLine("Please enable all safety toggles first");
                return;
            }

            try
            {
                RestoreBackupButton.IsEnabled = false;
                RestoreBackupButtonText.Text = T("StatusKeeper_Restoring");

                int restoreCount = 0;
                int skipCount = 0;

                var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? 
                                   Path.Combine(AppContext.BaseDirectory, "ModLibrary");

                await Task.Run(() => RestoreFromBackups(modLibraryPath, ref restoreCount, ref skipCount));

                // Auto-disable safety toggles after successful operation
                SafetyOverride1Toggle.IsOn = false;
                SafetyOverride2Toggle.IsOn = false;
                SafetyOverride3Toggle.IsOn = false;
                _settings.BackupOverride1Enabled = false;
                _settings.BackupOverride2Enabled = false;
                _settings.BackupOverride3Enabled = false;
                // Reset in SettingsManager as well
                SettingsManager.Current.StatusKeeperBackupOverride1Enabled = false;
                SettingsManager.Current.StatusKeeperBackupOverride2Enabled = false;
                SettingsManager.Current.StatusKeeperBackupOverride3Enabled = false;
                SettingsManager.Save();
                UpdateButtonStates();

                Debug.WriteLine($"Restore complete! Restored {restoreCount} files, failed {skipCount} files");
            }
            catch (Exception error)
            {
                Debug.WriteLine($"Restore failed: {error.Message}");
            }
            finally
            {
                // Don't re-enable button here - UpdateButtonStates() handles this based on safety toggles
                RestoreBackupButtonText.Text = T("StatusKeeper_RestoreBackup_Button");
            }
        }

        private async void DeleteBackupsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_settings.BackupOverrideEnabled)
            {
                Debug.WriteLine("Please enable all safety toggles first");
                return;
            }

            try
            {
                DeleteBackupsButton.IsEnabled = false;
                DeleteBackupsButtonText.Text = T("StatusKeeper_Deleting");

                int deleteCount = 0;

                var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? 
                                   Path.Combine(AppContext.BaseDirectory, "ModLibrary");

                await Task.Run(() => DeleteBackups(modLibraryPath, ref deleteCount));

                // Auto-disable safety toggles after successful operation
                SafetyOverride1Toggle.IsOn = false;
                SafetyOverride2Toggle.IsOn = false;
                SafetyOverride3Toggle.IsOn = false;
                _settings.BackupOverride1Enabled = false;
                _settings.BackupOverride2Enabled = false;
                _settings.BackupOverride3Enabled = false;
                // Reset in SettingsManager as well
                SettingsManager.Current.StatusKeeperBackupOverride1Enabled = false;
                SettingsManager.Current.StatusKeeperBackupOverride2Enabled = false;
                SettingsManager.Current.StatusKeeperBackupOverride3Enabled = false;
                SettingsManager.Save();
                UpdateButtonStates();

                Debug.WriteLine($"Deletion complete! Deleted {deleteCount} backup files");
            }
            catch (Exception error)
            {
                Debug.WriteLine($"Delete failed: {error.Message}");
            }
            finally
            {
                // Don't re-enable button here - UpdateButtonStates() handles this based on safety toggles
                DeleteBackupsButtonText.Text = T("StatusKeeper_DeleteBackups_Button");
            }
        }

        private async void CheckBackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckBackupButton.IsEnabled = false;
                CheckBackupProgressBar.Visibility = Visibility.Visible;
                
                var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
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
                string message = string.Format(T("StatusKeeper_CheckBackup_Dialog_Message"), 
                    iniCount, iniCount - incompleteCount, incompleteCount);

                var dialog = new ContentDialog
                {
                    Title = T("StatusKeeper_CheckBackup_Dialog_Title"),
                    Content = message,
                    CloseButtonText = T("OK"),
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

        private void SafetyOverride1Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            _settings.BackupOverride1Enabled = SafetyOverride1Toggle.IsOn;
            SettingsManager.Current.StatusKeeperBackupOverride1Enabled = SafetyOverride1Toggle.IsOn;
            SettingsManager.Save();
            UpdateButtonStates();
        }

        private void SafetyOverride2Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            _settings.BackupOverride2Enabled = SafetyOverride2Toggle.IsOn;
            SettingsManager.Current.StatusKeeperBackupOverride2Enabled = SafetyOverride2Toggle.IsOn;
            SettingsManager.Save();
            UpdateButtonStates();
        }

        private void SafetyOverride3Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            _settings.BackupOverride3Enabled = SafetyOverride3Toggle.IsOn;
            SettingsManager.Current.StatusKeeperBackupOverride3Enabled = SafetyOverride3Toggle.IsOn;
            SettingsManager.Save();
            UpdateButtonStates();
        }
    }
}
