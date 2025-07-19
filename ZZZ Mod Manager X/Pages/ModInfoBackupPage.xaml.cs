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
            Title.Text = T("Title");
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
            int count = await Task.Run(() => CreateAllBackups());
            CreateBackupsButton.IsEnabled = true;
            await ShowDialog(T("Title"), string.Format(T("ModInfoBackup_BackupComplete"), count), T("OK"));
            UpdateBackupInfo();
        }

        private string GetBackupDirectory()
        {
            var backupDir = Path.Combine(AppContext.BaseDirectory, "ModBackups");
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }
            return backupDir;
        }

        private int CreateAllBackups()
        {
            int count = 0;
            if (!Directory.Exists(ModLibraryPath)) return count;
            
            var backupDir = GetBackupDirectory();
            var modDirs = Directory.GetDirectories(ModLibraryPath);
            
            foreach (var dir in modDirs)
            {
                var modName = new DirectoryInfo(dir).Name;
                var modJson = Path.Combine(dir, "mod.json");
                var previewJpg = Path.Combine(dir, "preview.jpg");
                
                if (!File.Exists(modJson)) continue;
                
                // Create backup file names
                var bak1 = Path.Combine(backupDir, $"{modName}.bak1.zip");
                var bak2 = Path.Combine(backupDir, $"{modName}.bak2.zip");
                var bak3 = Path.Combine(backupDir, $"{modName}.bak3.zip");
                
                // Shift backups
                if (File.Exists(bak2))
                {
                    if (File.Exists(bak3)) File.Delete(bak3);
                    File.Move(bak2, bak3, true);
                }
                if (File.Exists(bak1))
                {
                    File.Move(bak1, bak2, true);
                }
                
                // Create new backup zip
                try
                {
                    using (var zipArchive = ZipFile.Open(bak1, ZipArchiveMode.Create))
                    {
                        // Always add mod.json
                        zipArchive.CreateEntryFromFile(modJson, "mod.json");
                        
                        // Add preview.jpg if it exists
                        if (File.Exists(previewJpg))
                        {
                            zipArchive.CreateEntryFromFile(previewJpg, "preview.jpg");
                        }
                    }
                    count++;
                }
                catch (Exception)
                {
                    // Skip this mod if there's an error creating the zip
                    if (File.Exists(bak1)) File.Delete(bak1);
                }
            }
            return count;
        }

        private async void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int backupNum) && backupNum >= 1 && backupNum <= MaxBackups)
            {
                btn.IsEnabled = false;
                int count = await Task.Run(() => RestoreAllBackups(backupNum));
                btn.IsEnabled = true;
                await ShowDialog(T("Title"), string.Format(T("ModInfoBackup_RestoreComplete"), backupNum, count), T("OK"));
                UpdateBackupInfo();
            }
        }

        private int RestoreAllBackups(int backupNum)
        {
            int count = 0;
            if (!Directory.Exists(ModLibraryPath)) return count;
            
            var backupDir = GetBackupDirectory();
            if (!Directory.Exists(backupDir)) return count;
            
            var modDirs = Directory.GetDirectories(ModLibraryPath);
            foreach (var dir in modDirs)
            {
                var modName = new DirectoryInfo(dir).Name;
                var modJson = Path.Combine(dir, "mod.json");
                var previewJpg = Path.Combine(dir, "preview.jpg");
                
                var bakZip = Path.Combine(backupDir, $"{modName}.bak{backupNum}.zip");
                
                if (!File.Exists(bakZip)) continue;
                
                try
                {
                    using (var archive = ZipFile.OpenRead(bakZip))
                    {
                        // Extract mod.json
                        var modJsonEntry = archive.GetEntry("mod.json");
                        if (modJsonEntry != null)
                        {
                            // Create backup of current file if it exists
                            if (File.Exists(modJson))
                            {
                                File.Copy(modJson, modJson + ".current", true);
                            }
                            
                            // Extract the file
                            modJsonEntry.ExtractToFile(modJson, true);
                        }
                        
                        // Extract preview.jpg if it exists in the archive
                        var previewEntry = archive.GetEntry("preview.jpg");
                        if (previewEntry != null)
                        {
                            // Create backup of current file if it exists
                            if (File.Exists(previewJpg))
                            {
                                File.Copy(previewJpg, previewJpg + ".current", true);
                            }
                            
                            // Extract the file
                            previewEntry.ExtractToFile(previewJpg, true);
                        }
                    }
                    count++;
                }
                catch (Exception)
                {
                    // Skip this mod if there's an error extracting the zip
                }
            }
            return count;
        }

        private async void DeleteBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int backupNum) && backupNum >= 1 && backupNum <= MaxBackups)
            {
                btn.IsEnabled = false;
                int count = await Task.Run(() => DeleteAllBackups(backupNum));
                btn.IsEnabled = true;
                await ShowDialog(T("Title"), string.Format(T("ModInfoBackup_DeleteComplete"), backupNum, count), T("OK"));
                UpdateBackupInfo();
            }
        }

        private int DeleteAllBackups(int backupNum)
        {
            int count = 0;
            var backupDir = GetBackupDirectory();
            if (!Directory.Exists(backupDir)) return count;
            
            // Look for all zip backup files with the specified backup number
            var backupFiles = Directory.GetFiles(backupDir, $"*.bak{backupNum}.zip");
            foreach (var bakFile in backupFiles)
            {
                try
                {
                    File.Delete(bakFile);
                    count++;
                }
                catch
                {
                    // Skip if we can't delete the file
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        private void UpdateBackupInfo()
        {
            UpdateBackupInfoFor(1, Backup1Info, RestoreBackup1Button, DeleteBackup1Button);
            UpdateBackupInfoFor(2, Backup2Info, RestoreBackup2Button, DeleteBackup2Button);
            UpdateBackupInfoFor(3, Backup3Info, RestoreBackup3Button, DeleteBackup3Button);
        }

        private void UpdateBackupInfoFor(int backupNum, TextBlock infoBlock, Button restoreButton, Button deleteButton)
        {
            var backupDir = GetBackupDirectory();
            if (!Directory.Exists(backupDir))
            {
                infoBlock.Text = "-";
                restoreButton.IsEnabled = false;
                deleteButton.IsEnabled = false;
                return;
            }
            
            // Look for all zip backup files with the specified backup number
            var backupFiles = Directory.GetFiles(backupDir, $"*.bak{backupNum}.zip");
            int count = backupFiles.Length;
            DateTime? newest = null;
            
            foreach (var bakFile in backupFiles)
            {
                var dt = File.GetCreationTime(bakFile);
                if (newest == null || dt > newest)
                    newest = dt;
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

        private void BackButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.2,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
            };
            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.2,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(scaleAnimation);
            storyboard.Children.Add(scaleAnimationY);
            storyboard.Begin();
        }

        private void BackButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
            };
            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            storyboard.Children.Add(scaleAnimation);
            storyboard.Children.Add(scaleAnimationY);
            storyboard.Begin();
        }
    }
}
