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
    public sealed partial class ModInfoBackupPage : Page
    {
        private string ModLibraryPath => SharedUtilities.GetSafeXXMIModsPath();
        private const int MaxBackups = 3;
        
        // Operation state tracking
        private static volatile bool _isCreatingBackup = false;
        private static volatile bool _isRestoringBackup1 = false;
        private static volatile bool _isRestoringBackup2 = false;
        private static volatile bool _isRestoringBackup3 = false;
        private static volatile bool _isDeletingBackup1 = false;
        private static volatile bool _isDeletingBackup2 = false;
        private static volatile bool _isDeletingBackup3 = false;

        public ModInfoBackupPage()
        {
            this.InitializeComponent();
            UpdateTexts();
            UpdateBackupInfo();
            UpdateButtonStates();
        }

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
            
            // Create backup section
            CreateBackupsTitle.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_CreateTitle");
            CreateBackupsDescription.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_CreateDescription");
            // Button text is handled by UpdateButtonStates()
            
            // Backups header
            BackupsHeader.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_BackupsHeader");
            
            // Backup titles
            RestoreBackup1Title.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Backup1Title");
            RestoreBackup2Title.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Backup2Title");
            RestoreBackup3Title.Text = SharedUtilities.GetTranslation(lang, "ModInfoBackup_Backup3Title");
            
            // Restore buttons - text is handled by UpdateButtonStates()
            
            // Delete button tooltips
            ToolTipService.SetToolTip(DeleteBackup1Button, SharedUtilities.GetTranslation(lang, "ModInfoBackup_Delete"));
            ToolTipService.SetToolTip(DeleteBackup2Button, SharedUtilities.GetTranslation(lang, "ModInfoBackup_Delete"));
            ToolTipService.SetToolTip(DeleteBackup3Button, SharedUtilities.GetTranslation(lang, "ModInfoBackup_Delete"));
        }
        
        /// <summary>
        /// Update button states - disable all buttons when any operation is running
        /// </summary>
        private void UpdateButtonStates()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
            
            // Check if any operation is currently running
            bool anyOperationRunning = _isCreatingBackup || _isRestoringBackup1 || _isRestoringBackup2 || _isRestoringBackup3 ||
                                     _isDeletingBackup1 || _isDeletingBackup2 || _isDeletingBackup3;
            
            // Create Backup button
            if (CreateBackupsButton != null && CreateBackupsText != null)
            {
                CreateBackupsButton.IsEnabled = !anyOperationRunning || _isCreatingBackup;
                CreateBackupsText.Text = _isCreatingBackup 
                    ? SharedUtilities.GetTranslation(lang, "ModInfoBackup_Creating")
                    : SharedUtilities.GetTranslation(lang, "Create");
            }
            
            // Restore Backup 1 button
            if (RestoreBackup1Button != null && RestoreBackup1Text != null)
            {
                RestoreBackup1Button.IsEnabled = (!anyOperationRunning || _isRestoringBackup1) && RestoreBackup1Button.Tag != null;
                RestoreBackup1Text.Text = _isRestoringBackup1 
                    ? SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restoring")
                    : SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restore");
            }
            
            // Restore Backup 2 button
            if (RestoreBackup2Button != null && RestoreBackup2Text != null)
            {
                RestoreBackup2Button.IsEnabled = (!anyOperationRunning || _isRestoringBackup2) && RestoreBackup2Button.Tag != null;
                RestoreBackup2Text.Text = _isRestoringBackup2 
                    ? SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restoring")
                    : SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restore");
            }
            
            // Restore Backup 3 button
            if (RestoreBackup3Button != null && RestoreBackup3Text != null)
            {
                RestoreBackup3Button.IsEnabled = (!anyOperationRunning || _isRestoringBackup3) && RestoreBackup3Button.Tag != null;
                RestoreBackup3Text.Text = _isRestoringBackup3 
                    ? SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restoring")
                    : SharedUtilities.GetTranslation(lang, "ModInfoBackup_Restore");
            }
            
            // Delete buttons
            if (DeleteBackup1Button != null)
            {
                DeleteBackup1Button.IsEnabled = !anyOperationRunning || _isDeletingBackup1;
            }
            
            if (DeleteBackup2Button != null)
            {
                DeleteBackup2Button.IsEnabled = !anyOperationRunning || _isDeletingBackup2;
            }
            
            if (DeleteBackup3Button != null)
            {
                DeleteBackup3Button.IsEnabled = !anyOperationRunning || _isDeletingBackup3;
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

        private async void CreateBackupsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await CreateBackupsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in CreateBackupsButton_Click", ex);
                _isCreatingBackup = false;
                UpdateButtonStates();
                var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
                await ShowDialog(SharedUtilities.GetTranslation(lang, "Error"), ex.Message, SharedUtilities.GetTranslation(lang, "OK"));
            }
        }
        
        private async Task CreateBackupsAsync()
        {
            _isCreatingBackup = true;
            UpdateButtonStates();
            
            int count = await Task.Run(() => CreateAllBackups());
            
            _isCreatingBackup = false;
            UpdateButtonStates();
            
            var lang = SharedUtilities.LoadLanguageDictionary("ModInfoBackup");
            await ShowDialog(SharedUtilities.GetTranslation(lang, "Title"), string.Format(SharedUtilities.GetTranslation(lang, "ModInfoBackup_BackupComplete"), count), SharedUtilities.GetTranslation(lang, "OK"));
            UpdateBackupInfo();
        }

        private int CreateAllBackups()
        {
            int count = 0;
            if (!Directory.Exists(ModLibraryPath)) return count;
            
            // First, backup category preview images
            try
            {
                foreach (var categoryDir in Directory.GetDirectories(ModLibraryPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    var categoryName = new DirectoryInfo(categoryDir).Name;
                    
                    // Check if category has any files to backup
                    var catprevPath = Path.Combine(categoryDir, "catprev.jpg");
                    var catminiPath = Path.Combine(categoryDir, "catmini.jpg");
                    
                    bool hasCatprev = File.Exists(catprevPath);
                    bool hasCatmini = File.Exists(catminiPath);
                    
                    // Only create backup if there are files to backup
                    if (!hasCatprev && !hasCatmini)
                    {
                        Logger.LogInfo($"Skipping category backup for {categoryName} - no catprev.jpg or catmini.jpg found");
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
                        filesToBackup["catprev.jpg"] = catprevPath;
                        Logger.LogInfo($"Added catprev.jpg to backup for category: {categoryName}");
                    }
                    
                    if (hasCatmini)
                    {
                        filesToBackup["catmini.jpg"] = catminiPath;
                        Logger.LogInfo($"Added catmini.jpg to backup for category: {categoryName}");
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
                    var filesToBackup = new Dictionary<string, string>
                    {
                        ["mod.json"] = modJson
                    };
                    
                    // Add minitile.jpg
                    var minitilePath = Path.Combine(dir, "minitile.jpg");
                    if (File.Exists(minitilePath))
                    {
                        filesToBackup["minitile.jpg"] = minitilePath;
                    }
                    
                    // Add all preview images (preview.jpg, preview-01.jpg, preview-02.jpg, etc.)
                    var previewFiles = Directory.GetFiles(dir, "preview*.jpg")
                        .Concat(Directory.GetFiles(dir, "preview*.jpeg"))
                        .Concat(Directory.GetFiles(dir, "preview*.png"))
                        .ToList();
                    
                    Logger.LogInfo($"Found {previewFiles.Count} preview files for mod: {modName}");
                    
                    foreach (var previewFile in previewFiles)
                    {
                        var fileName = Path.GetFileName(previewFile);
                        filesToBackup[fileName] = previewFile;
                        Logger.LogInfo($"Added {fileName} to backup for mod: {modName}");
                    }
                    
                    ArchiveHelper.CreateArchiveFromFiles(backup1, filesToBackup);
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
                    
                    int count = await Task.Run(() => RestoreAllBackups(backupNum));
                    
                    // Reset operation state
                    switch (backupNum)
                    {
                        case 1: _isRestoringBackup1 = false; break;
                        case 2: _isRestoringBackup2 = false; break;
                        case 3: _isRestoringBackup3 = false; break;
                    }
                    UpdateButtonStates();
                    
                    // Show success dialog
                    await ShowDialog(SharedUtilities.GetTranslation(lang, "Title"), string.Format(SharedUtilities.GetTranslation(lang, "ModInfoBackup_RestoreComplete"), backupNum, count), SharedUtilities.GetTranslation(lang, "OK"));
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
                    await ShowDialog(SharedUtilities.GetTranslation(lang, "Error"), ex.Message, SharedUtilities.GetTranslation(lang, "OK"));
                }
            }
        }
        


        private int RestoreAllBackups(int backupNum)
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
            
            foreach (var dir in modDirs)
            {
                var modName = new DirectoryInfo(dir).Name;
                var backupZip = Path.Combine(dir, $"{modName}.mib{backupNum}.zip");
                
                if (!File.Exists(backupZip)) continue;
                
                try
                {
                    ArchiveHelper.ExtractToDirectory(backupZip, dir);
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
                    
                    int count = await Task.Run(() => DeleteAllBackups(backupNum));
                    
                    // Reset operation state
                    switch (backupNum)
                    {
                        case 1: _isDeletingBackup1 = false; break;
                        case 2: _isDeletingBackup2 = false; break;
                        case 3: _isDeletingBackup3 = false; break;
                    }
                    UpdateButtonStates();
                    
                    // Show success dialog
                    await ShowDialog(SharedUtilities.GetTranslation(lang, "Title"), string.Format(SharedUtilities.GetTranslation(lang, "ModInfoBackup_DeleteComplete"), backupNum, count), SharedUtilities.GetTranslation(lang, "OK"));
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
                    await ShowDialog(SharedUtilities.GetTranslation(lang, "Error"), ex.Message, SharedUtilities.GetTranslation(lang, "OK"));
                }
            }
        }

        private int DeleteAllBackups(int backupNum)
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
