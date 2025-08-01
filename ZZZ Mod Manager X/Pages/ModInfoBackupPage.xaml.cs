using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

namespace ZZZ_Mod_Manager_X.Pages
{
    public sealed partial class ModInfoBackupPage : Page
    {
        private Dictionary<string, string> _lang = new();
        private string ModLibraryPath => ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
        private const int MaxBackups = 3;

        public ModInfoBackupPage()
        {
            this.InitializeComponent();
            LoadLanguage();
            UpdateTexts();
            UpdateBackupInfo();
        }

        private void LoadLanguage()
        {
            try
            {
                var langFile = ZZZ_Mod_Manager_X.SettingsManager.Current?.LanguageFile ?? "en.json";
                var langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "ModInfoBackup", langFile);
                if (!File.Exists(langPath))
                    langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "ModInfoBackup", "en.json");
                if (File.Exists(langPath))
                {
                    var json = File.ReadAllText(langPath);
                    _lang = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            }
            catch
            {
                _lang = new Dictionary<string, string>();
            }
        }

        private string T(string key)
        {
            return _lang.TryGetValue(key, out var value) ? value : key;
        }

        private void UpdateTexts()
        {
            CreateBackupsText.Text = T("ModInfoBackup_BackupAll");
            RestoreBackup1Text.Text = T("ModInfoBackup_Restore1");
            RestoreBackup2Text.Text = T("ModInfoBackup_Restore2");
            RestoreBackup3Text.Text = T("ModInfoBackup_Restore3");
            ToolTipService.SetToolTip(DeleteBackup1Button, T("ModInfoBackup_Delete"));
            ToolTipService.SetToolTip(DeleteBackup2Button, T("ModInfoBackup_Delete"));
            ToolTipService.SetToolTip(DeleteBackup3Button, T("ModInfoBackup_Delete"));
        }

        private async void CreateBackupsButton_Click(object sender, RoutedEventArgs e)
        {
            CreateBackupsButton.IsEnabled = false;
            CreateBackupsProgressBar.Visibility = Visibility.Visible;
            
            int count = await Task.Run(() => CreateAllBackups());
            
            CreateBackupsProgressBar.Visibility = Visibility.Collapsed;
            CreateBackupsButton.IsEnabled = true;
            
            await ShowDialog(T("Title"), string.Format(T("ModInfoBackup_BackupComplete"), count), T("OK"));
            UpdateBackupInfo();
        }

        private int CreateAllBackups()
        {
            int count = 0;
            if (!Directory.Exists(ModLibraryPath)) return count;
            
            var modDirs = Directory.GetDirectories(ModLibraryPath);
            
            foreach (var dir in modDirs)
            {
                var modJson = Path.Combine(dir, "mod.json");
                var previewJpg = Path.Combine(dir, "preview.jpg");
                var minitilePng = Path.Combine(dir, "minitile.png");
                
                if (!File.Exists(modJson)) continue;
                
                // Create inline ZIP backup alongside mod files
                var modName = new DirectoryInfo(dir).Name;
                var backupZip = Path.Combine(dir, $"{modName}.mib.zip");
                
                // Skip if backup already exists (prevent duplicates)
                if (File.Exists(backupZip)) continue;
                
                try
                {
                    using (var zipArchive = ZipFile.Open(backupZip, ZipArchiveMode.Create))
                    {
                        // Always add mod.json
                        zipArchive.CreateEntryFromFile(modJson, "mod.json");
                        
                        // Add preview.jpg if it exists
                        if (File.Exists(previewJpg))
                        {
                            zipArchive.CreateEntryFromFile(previewJpg, "preview.jpg");
                        }
                        
                        // Add minitile.png if it exists
                        if (File.Exists(minitilePng))
                        {
                            zipArchive.CreateEntryFromFile(minitilePng, "minitile.png");
                        }
                    }
                    count++;
                }
                catch (Exception)
                {
                    // Skip this mod if there's an error creating the zip
                    if (File.Exists(backupZip)) File.Delete(backupZip);
                }
            }
            return count;
        }

        private async void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int backupNum) && backupNum >= 1 && backupNum <= MaxBackups)
            {
                // Show confirmation dialog before restoring
                ContentDialog confirmDialog = new ContentDialog
                {
                    Title = T("ModInfoBackup_RestoreConfirm_Title"),
                    Content = string.Format(T("ModInfoBackup_RestoreConfirm_Message"), backupNum),
                    PrimaryButtonText = T("Yes"),
                    CloseButtonText = T("No"),
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
                
                await ShowDialog(T("Title"), string.Format(T("ModInfoBackup_RestoreComplete"), backupNum, count), T("OK"));
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
            // Note: backupNum parameter is ignored in new inline system
            // We only have one backup level (.mib.zip files)
            
            int count = 0;
            if (!Directory.Exists(ModLibraryPath)) return count;
            
            var modDirs = Directory.GetDirectories(ModLibraryPath);
            foreach (var dir in modDirs)
            {
                var modName = new DirectoryInfo(dir).Name;
                var backupZip = Path.Combine(dir, $"{modName}.mib.zip");
                
                if (!File.Exists(backupZip)) continue;
                
                try
                {
                    using (var archive = ZipFile.OpenRead(backupZip))
                    {
                        // Extract mod.json
                        var modJsonEntry = archive.GetEntry("mod.json");
                        if (modJsonEntry != null)
                        {
                            var modJsonPath = Path.Combine(dir, "mod.json");
                            modJsonEntry.ExtractToFile(modJsonPath, true);
                        }
                        
                        // Extract preview.jpg if it exists in the archive
                        var previewEntry = archive.GetEntry("preview.jpg");
                        if (previewEntry != null)
                        {
                            var previewPath = Path.Combine(dir, "preview.jpg");
                            previewEntry.ExtractToFile(previewPath, true);
                        }
                        
                        // Extract minitile.png if it exists in the archive
                        var minitileEntry = archive.GetEntry("minitile.png");
                        if (minitileEntry != null)
                        {
                            var minitilePath = Path.Combine(dir, "minitile.png");
                            minitileEntry.ExtractToFile(minitilePath, true);
                        }
                    }
                    count++;
                }
                catch (Exception)
                {
                    // Skip this mod if there's an error extracting the zip
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
                ContentDialog confirmDialog = new ContentDialog
                {
                    Title = T("ModInfoBackup_DeleteConfirm_Title"),
                    Content = string.Format(T("ModInfoBackup_DeleteConfirm_Message"), backupNum),
                    PrimaryButtonText = T("Yes"),
                    CloseButtonText = T("No"),
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
                
                await ShowDialog(T("Title"), string.Format(T("ModInfoBackup_DeleteComplete"), backupNum, count), T("OK"));
                UpdateBackupInfo();
            }
        }

        private int DeleteAllBackups(int backupNum)
        {
            // Note: backupNum parameter is ignored in new inline system
            // We delete all .mib.zip files
            
            int count = 0;
            if (!Directory.Exists(ModLibraryPath)) return count;
            
            var modDirs = Directory.GetDirectories(ModLibraryPath);
            foreach (var dir in modDirs)
            {
                var modName = new DirectoryInfo(dir).Name;
                var backupZip = Path.Combine(dir, $"{modName}.mib.zip");
                
                if (!File.Exists(backupZip)) continue;
                
                try
                {
                    File.Delete(backupZip);
                    count++;
                }
                catch (Exception)
                {
                    // Skip if we can't delete the file
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
            // In the new inline system, we only have one backup level
            // Update all three buttons to show the same info (for UI consistency)
            UpdateBackupInfoFor(1, Backup1Info, RestoreBackup1Button, DeleteBackup1Button);
            UpdateBackupInfoFor(2, Backup2Info, RestoreBackup2Button, DeleteBackup2Button);
            UpdateBackupInfoFor(3, Backup3Info, RestoreBackup3Button, DeleteBackup3Button);
        }

        private void UpdateBackupInfoFor(int backupNum, TextBlock infoBlock, Button restoreButton, Button deleteButton)
        {
            if (!Directory.Exists(ModLibraryPath))
            {
                infoBlock.Text = "-";
                restoreButton.IsEnabled = false;
                deleteButton.IsEnabled = false;
                return;
            }
            
            // Count all .mib.zip backup files across all mod directories
            int count = 0;
            DateTime? newest = null;
            
            var modDirs = Directory.GetDirectories(ModLibraryPath);
            foreach (var dir in modDirs)
            {
                var modName = new DirectoryInfo(dir).Name;
                var backupZip = Path.Combine(dir, $"{modName}.mib.zip");
                
                if (File.Exists(backupZip))
                {
                    count++;
                    var dt = File.GetCreationTime(backupZip);
                    if (newest == null || dt > newest)
                        newest = dt;
                }
            }
            
            if (count > 0 && newest != null)
            {
                infoBlock.Text = string.Format(T("ModInfoBackup_BackupInfo"), $"{newest:yyyy-MM-dd HH:mm}", count);
                restoreButton.IsEnabled = true;
                deleteButton.IsEnabled = true;
            }
            else
            {
                infoBlock.Text = "-";
                restoreButton.IsEnabled = false;
                deleteButton.IsEnabled = false;
            }
        }


    }
}
