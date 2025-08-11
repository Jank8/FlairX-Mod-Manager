using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModInfoBackupPage : Page
    {
        private string ModLibraryPath => SharedUtilities.GetSafeModLibraryPath();
        private const int MaxBackups = 3;

        public ModInfoBackupPage()
        {
            this.InitializeComponent();
            UpdateTexts();
            UpdateBackupInfo();
        }

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
            CreateBackupsText.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_BackupAll");
            RestoreBackup1Text.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restore1");
            RestoreBackup2Text.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restore2");
            RestoreBackup3Text.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restore3");
            ToolTipService.SetToolTip(DeleteBackup1Button, SharedUtilities.GetTranslation(lang, "ModInfoBackup_Delete"));
            ToolTipService.SetToolTip(DeleteBackup2Button, SharedUtilities.GetTranslation(lang, "ModInfoBackup_Delete"));
            ToolTipService.SetToolTip(DeleteBackup3Button, SharedUtilities.GetTranslation(lang, "ModInfoBackup_Delete"));
        }

        private async void CreateBackupsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await CreateBackupsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in CreateBackupsButton_Click", ex);
                CreateBackupsProgressBar.Visibility = Visibility.Collapsed;
                CreateBackupsButton.IsEnabled = true;
                var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
                await ShowDialog(SharedUtilities.GetTranslation(lang, "Error"), ex.Message, SharedUtilities.GetTranslation(lang, "OK"));
            }
        }
        
        private async Task CreateBackupsAsync()
        {
            CreateBackupsButton.IsEnabled = false;
            CreateBackupsProgressBar.Visibility = Visibility.Visible;
            
            int count = await Task.Run(() => CreateAllBackups());
            
            CreateBackupsProgressBar.Visibility = Visibility.Collapsed;
            CreateBackupsButton.IsEnabled = true;
            
            var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
            await ShowDialog(SharedUtilities.GetTranslation(lang, "Title"), string.Format(SharedUtilities.GetTranslation(lang, "ModInfoBackup_BackupComplete"), count), SharedUtilities.GetTranslation(lang, "OK"));
            UpdateBackupInfo();
        }

        private int CreateAllBackups()
        {
            int count = 0;
            if (!Directory.Exists(ModLibraryPath)) return count;
            
            // Get all mod directories from all categories
            var modDirs = new List<string>();
            foreach (var categoryDir in Directory.GetDirectories(ModLibraryPath))
            {
                if (Directory.Exists(categoryDir))
                {
                    modDirs.AddRange(Directory.GetDirectories(categoryDir));
                }
            }
            
            foreach (var dir in modDirs)
            {
                var modJson = Path.Combine(dir, "mod.json");
                
                if (!File.Exists(modJson)) continue;
                
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
                    using (var zipArchive = ZipFile.Open(backup1, ZipArchiveMode.Create))
                    {
                        // Always add mod.json
                        zipArchive.CreateEntryFromFile(modJson, "mod.json");
                        
                        // Add only essential files
                        var essentialFiles = new[]
                        {
                            "preview.jpg",
                            "minitile.jpg"
                        };
                        
                        foreach (var essentialFile in essentialFiles)
                        {
                            var filePath = Path.Combine(dir, essentialFile);
                            if (File.Exists(filePath))
                            {
                                zipArchive.CreateEntryFromFile(filePath, essentialFile);
                            }
                        }
                    }
                    count++;
                    Logger.LogInfo($"Created backup for mod: {modName}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to create backup for mod {modName}", ex);
                }
            }
            return count;
        }

        private async void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await RestoreBackupAsync(sender);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in RestoreBackupButton_Click", ex);
                if (sender is Button btn)
                {
                    btn.IsEnabled = true;
                }
                var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
                await ShowDialog(SharedUtilities.GetTranslation(lang, "Error"), ex.Message, SharedUtilities.GetTranslation(lang, "OK"));
            }
        }
        
        private async Task RestoreBackupAsync(object sender)
        {
            if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int backupNum) && backupNum >= 1 && backupNum <= MaxBackups)
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

                btn.IsEnabled = false;
                
                // Create progress bar for this button
                ProgressBar? progressBar = null;
                if (btn == RestoreBackup1Button) progressBar = CreateProgressBarAfter(btn, 4);
                else if (btn == RestoreBackup2Button) progressBar = CreateProgressBarAfter(btn, 4);
                else if (btn == RestoreBackup3Button) progressBar = CreateProgressBarAfter(btn, 4);
                
                if (progressBar != null) progressBar.Visibility = Visibility.Visible;
                
                int count = await Task.Run(() => RestoreAllBackups(backupNum));
                
                if (progressBar != null) progressBar.Visibility = Visibility.Collapsed;
                btn.IsEnabled = true;
                
                await ShowDialog(SharedUtilities.GetTranslation(lang, "Title"), string.Format(SharedUtilities.GetTranslation(lang, "ModInfoBackup_RestoreComplete"), backupNum, count), SharedUtilities.GetTranslation(lang, "OK"));
                UpdateBackupInfo();
            }
        }
        
        private ProgressBar? CreateProgressBarAfter(Button button, int column)
        {
            // Find the parent grid
            if (button.Parent is FrameworkElement parent && parent.Parent is Grid grid)
            {
                // Check if we already have a progress bar
                ProgressBar? existingBar = grid.Children.OfType<ProgressBar>().FirstOrDefault();
                if (existingBar != null)
                {
                    return existingBar;
                }
                
                // Create a new progress bar
                ProgressBar progressBar = new ProgressBar
                {
                    IsIndeterminate = true,
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Collapsed,
                    Margin = new Thickness(10, 0, 0, 0)
                };
                
                // Add it to the grid in the specified column
                Grid.SetColumn(progressBar, column);
                grid.Children.Add(progressBar);
                
                return progressBar;
            }
            
            return null;
        }

        private int RestoreAllBackups(int backupNum)
        {
            int count = 0;
            if (!Directory.Exists(ModLibraryPath)) return count;
            
            // Get all mod directories from all categories
            var modDirs = new List<string>();
            foreach (var categoryDir in Directory.GetDirectories(ModLibraryPath))
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
                
                if (!File.Exists(backupZip)) continue;
                
                try
                {
                    using (var archive = ZipFile.OpenRead(backupZip))
                    {
                        // Extract all entries from the backup
                        foreach (var entry in archive.Entries)
                        {
                            if (!string.IsNullOrEmpty(entry.Name))
                            {
                                var extractPath = Path.Combine(dir, entry.Name);
                                try
                                {
                                    // Ensure directory exists
                                    var extractDir = Path.GetDirectoryName(extractPath);
                                    if (!string.IsNullOrEmpty(extractDir) && !Directory.Exists(extractDir))
                                    {
                                        Directory.CreateDirectory(extractDir);
                                    }
                                    
                                    entry.ExtractToFile(extractPath, true);
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError($"Failed to extract {entry.Name} for mod {modName}", ex);
                                }
                            }
                        }
                    }
                    count++;
                    Logger.LogInfo($"Restored backup {backupNum} for mod: {modName}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to restore backup {backupNum} for mod {modName}", ex);
                    continue;
                }
            }
            return count;
        }

        private async void DeleteBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int backupNum) && backupNum >= 1 && backupNum <= MaxBackups)
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
                
                btn.IsEnabled = false;
                
                // Create progress bar for this button
                ProgressBar? progressBar = null;
                if (btn == DeleteBackup1Button) progressBar = CreateProgressBarAfter(btn, 4);
                else if (btn == DeleteBackup2Button) progressBar = CreateProgressBarAfter(btn, 4);
                else if (btn == DeleteBackup3Button) progressBar = CreateProgressBarAfter(btn, 4);
                
                if (progressBar != null) progressBar.Visibility = Visibility.Visible;
                
                int count = await Task.Run(() => DeleteAllBackups(backupNum));
                
                if (progressBar != null) progressBar.Visibility = Visibility.Collapsed;
                btn.IsEnabled = true;
                
                await ShowDialog(SharedUtilities.GetTranslation(lang, "Title"), string.Format(SharedUtilities.GetTranslation(lang, "ModInfoBackup_DeleteComplete"), backupNum, count), SharedUtilities.GetTranslation(lang, "OK"));
                UpdateBackupInfo();
            }
        }

        private int DeleteAllBackups(int backupNum)
        {
            int count = 0;
            if (!Directory.Exists(ModLibraryPath)) return count;
            
            // Get all mod directories from all categories
            var modDirs = new List<string>();
            foreach (var categoryDir in Directory.GetDirectories(ModLibraryPath))
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
                
                if (!File.Exists(backupZip)) continue;
                
                try
                {
                    File.Delete(backupZip);
                    count++;
                    Logger.LogInfo($"Deleted backup {backupNum} for mod: {modName}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to delete backup {backupNum} for mod {modName}", ex);
                    continue;
                }
            }
            return count;
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
