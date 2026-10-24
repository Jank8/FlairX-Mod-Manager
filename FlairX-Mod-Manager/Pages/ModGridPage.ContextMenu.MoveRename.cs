using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModGridPage
    {


        private async void ContextMenu_Move_Click(object sender, RoutedEventArgs e)
                {
                    try
                    {
                        if (sender is MenuFlyoutItem menuItem && menuItem.Tag is ModTile modTile)
                        {
                            if (modTile.IsCategory)
                            {
                                return; // Cannot move categories
                            }

                            var lang = SharedUtilities.LoadLanguageDictionary();
                            var modsPath = SharedUtilities.GetSafeXXMIModsPath();

                            // Get all categories
                            var categories = Directory.GetDirectories(modsPath)
                                .Select(d => Path.GetFileName(d))
                                .Where(d => !string.IsNullOrEmpty(d))
                                .OrderBy(d => d)
                                .ToList();

                            // Find current category
                            string currentCategory = modTile.Category;

                            var dialog = new ContentDialog
                            {
                                XamlRoot = this.XamlRoot,
                                Title = SharedUtilities.GetTranslation(lang, "MoveDialog_Title") ?? "Move Mod",
                                PrimaryButtonText = SharedUtilities.GetTranslation(lang, "OK") ?? "OK",
                                CloseButtonText = SharedUtilities.GetTranslation(lang, "Cancel") ?? "Cancel",
                                DefaultButton = ContentDialogButton.Primary
                            };

                            var mainPanel = new StackPanel { Spacing = 10 };

                            // Selection UI
                            var selectionPanel = new StackPanel { Spacing = 10 };

                            var textBlock = new TextBlock
                            {
                                Text = string.Format(SharedUtilities.GetTranslation(lang, "MoveDialog_SelectCategory") ?? "Select target category for '{0}':", modTile.Name),
                                TextWrapping = TextWrapping.Wrap
                            };
                            selectionPanel.Children.Add(textBlock);

                            var comboBox = new ComboBox
                            {
                                ItemsSource = categories,
                                SelectedItem = currentCategory,
                                HorizontalAlignment = HorizontalAlignment.Stretch
                            };
                            selectionPanel.Children.Add(comboBox);

                            mainPanel.Children.Add(selectionPanel);

                            // Progress UI (initially hidden)
                            var progressPanel = new StackPanel { Spacing = 10, Visibility = Visibility.Collapsed };

                            var progressText = new TextBlock
                            {
                                Text = SharedUtilities.GetTranslation(lang, "MoveDialog_Moving") ?? "Moving mod...",
                                TextWrapping = TextWrapping.Wrap
                            };
                            progressPanel.Children.Add(progressText);

                            var progressBar = new ProgressBar
                            {
                                IsIndeterminate = true,
                                Height = 4
                            };
                            progressPanel.Children.Add(progressBar);

                            mainPanel.Children.Add(progressPanel);

                            dialog.Content = mainPanel;

                            // Handle primary button click to show progress and perform move
                            dialog.PrimaryButtonClick += async (s, args) =>
                            {
                                try
                                {
                                    // Prevent dialog from closing
                                    args.Cancel = true;

                                    if (comboBox.SelectedItem is string targetCategory)
                                    {
                                        if (targetCategory == currentCategory)
                                        {
                                            // Same category, close dialog
                                            dialog.Hide();
                                            return;
                                        }

                                        // Switch to progress view
                                        selectionPanel.Visibility = Visibility.Collapsed;
                                        progressPanel.Visibility = Visibility.Visible;
                                        dialog.IsPrimaryButtonEnabled = false;
                                        dialog.IsSecondaryButtonEnabled = false;

                                        // Small delay to ensure UI updates
                                        await Task.Delay(50);

                                        try
                                        {
                                            await MoveModToCategoryAsync(modTile, currentCategory, targetCategory);

                                            // Small delay to show completion
                                            await Task.Delay(300);

                                            // Full reload to reflect the changes
                                            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                                            {
                                                // Close dialog before reload
                                                dialog.Hide();

                                                await mainWindow.ReloadModsAsync();
                                            }
                                            else
                                            {
                                                dialog.Hide();
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            // Close dialog on error
                                            dialog.Hide();
                                            Logger.LogError($"Error in ContextMenu_Move_Click", ex);
                                        }
                                    }
                                }
                                catch (System.OperationCanceledException)
                                {
                                    Logger.LogDebug("ModGridPage.ContextMenu_Move_Click failed: canceled");
                                }
                                catch (System.Exception ex)
                                {
                                    Logger.LogError("ModGridPage.ContextMenu_Move_Click failed", ex);
                                }
                            };

                            await dialog.ShowAsync();
                        }
                    }
                    catch (System.OperationCanceledException)
                    {
                        Logger.LogDebug("ModGridPage.ContextMenu_Move_Click failed: canceled");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("ModGridPage.ContextMenu_Move_Click failed", ex);
                    }
                }


        private async Task MoveModToCategoryAsync(ModTile modTile, string sourceCategory, string targetCategory)
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            
            try
            {
                var modsPath = SharedUtilities.GetSafeXXMIModsPath();
                var sourcePath = Path.Combine(modsPath, sourceCategory, modTile.Directory);
                var targetCategoryPath = Path.Combine(modsPath, targetCategory);
                
                if (!Directory.Exists(sourcePath))
                {
                    await ShowErrorDialog(SharedUtilities.GetTranslation(lang, "Error_Title") ?? "Error", SharedUtilities.GetTranslation(lang, "MoveDialog_SourceNotFound") ?? "Source mod directory not found.");
                    return;
                }

                if (!Directory.Exists(targetCategoryPath))
                {
                    await ShowErrorDialog(SharedUtilities.GetTranslation(lang, "Error_Title") ?? "Error", SharedUtilities.GetTranslation(lang, "MoveDialog_TargetNotFound") ?? "Target category not found.");
                    return;
                }

                // Check if mod with same name already exists in target category
                var cleanName = GetCleanModName(modTile.Directory);
                var isActive = !modTile.Directory.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase);
                
                string finalDirectoryName = modTile.Directory;
                var targetPath = Path.Combine(targetCategoryPath, finalDirectoryName);
                
                // Check for duplicates in target category
                bool foundDuplicate = false;
                foreach (var existingModDir in Directory.GetDirectories(targetCategoryPath))
                {
                    var existingModName = Path.GetFileName(existingModDir);
                    var existingCleanName = GetCleanModName(existingModName);
                    
                    if (string.Equals(existingCleanName, cleanName, StringComparison.OrdinalIgnoreCase))
                    {
                        foundDuplicate = true;
                        break;
                    }
                }

                // If duplicate found, add _duplicate suffix
                if (foundDuplicate)
                {
                    if (isActive)
                    {
                        finalDirectoryName = cleanName + "_duplicate";
                    }
                    else
                    {
                        finalDirectoryName = "DISABLED_" + cleanName + "_duplicate";
                    }
                    targetPath = Path.Combine(targetCategoryPath, finalDirectoryName);
                    Logger.LogInfo($"Duplicate found in target category, renaming to: {finalDirectoryName}");
                }

                // Perform the move with retry loop
                bool success = false;
                while (!success)
                {
                    try
                    {
                        Services.FileAccessQueue.MoveDirectory(sourcePath, targetPath);
                        Logger.LogInfo($"Successfully moved mod from '{sourcePath}' to '{targetPath}'");
                        success = true;
                    }
                    catch (IOException ex)
                    {
                        Logger.LogError($"Failed to move directory - IOException: {ex.Message}");
                        
                        // Show retry dialog
                        var dialog = new ContentDialog
                        {
                            XamlRoot = this.XamlRoot,
                            Title = SharedUtilities.GetTranslation(lang, "MoveDialog_FolderLocked_Title") ?? "Folder Locked",
                            Content = string.Format(SharedUtilities.GetTranslation(lang, "MoveDialog_FolderLocked_Content") ?? "The folder '{0}' is locked by another process (possibly Windows Explorer).\n\nClose any windows viewing this folder and try again.", modTile.Name),
                            PrimaryButtonText = SharedUtilities.GetTranslation(lang, "MoveDialog_Retry") ?? "Retry",
                            CloseButtonText = SharedUtilities.GetTranslation(lang, "Cancel") ?? "Cancel",
                            DefaultButton = ContentDialogButton.Primary
                        };
                        
                        var result = await dialog.ShowAsync();
                        if (result != ContentDialogResult.Primary)
                        {
                            // User cancelled
                            return;
                        }
                        // Loop will retry
                    }
                }

                // Update ModTile
                modTile.Directory = finalDirectoryName;
                modTile.Category = targetCategory;

                // Save current scroll position
                double currentScrollPosition = 0;
                if (ModsScrollViewer != null)
                {
                    currentScrollPosition = ModsScrollViewer.VerticalOffset;
                }

                // Refresh UI
                if (CurrentViewMode == ViewMode.Table)
                {
                    var originalItem = _originalTableItems.FirstOrDefault(x => x == modTile);
                    if (originalItem != null && originalItem != modTile)
                    {
                        originalItem.Directory = finalDirectoryName;
                        originalItem.Category = targetCategory;
                    }
                }

                // Refresh the mod tile image
                try
                {
                    RefreshModTileImage(targetPath);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to refresh tile image after move", ex);
                }

                // Restore scroll position
                if (ModsScrollViewer != null && currentScrollPosition > 0)
                {
                    await Task.Delay(100);
                    ModsScrollViewer.ScrollToVerticalOffset(currentScrollPosition);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to move mod '{modTile.Name}' to category '{targetCategory}'", ex);
                await ShowErrorDialog(SharedUtilities.GetTranslation(lang, "Error_Title") ?? "Error", string.Format(SharedUtilities.GetTranslation(lang, "MoveDialog_Failed") ?? "Failed to move mod: {0}", ex.Message));
            }
        }

        private async void ContextMenu_Rename_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile)
                {
                    var lang = SharedUtilities.LoadLanguageDictionary();

                    // ALWAYS deactivate mod before renaming (simplification)
                    if (!modTile.IsCategory && modTile.IsActive)
                    {
                        Logger.LogInfo($"Auto-deactivating mod '{modTile.Name}' before rename");
                        if (DeactivateModByRename(modTile.Directory, out string newModName))
                        {
                            modTile.IsActive = false;
                            modTile.Directory = newModName;
                            var cleanName = GetCleanModName(modTile.Directory);
                            _activeMods[cleanName] = false;
                        }
                        else
                        {
                            await ShowErrorDialog(
                                SharedUtilities.GetTranslation(lang, "Error_Title"),
                                SharedUtilities.GetTranslation(lang, "Notify_RenameDeactivateFailed"));
                            return;
                        }
                    }

                    var textBox = new TextBox
                    {
                        Text = modTile.Name,
                        SelectionStart = 0,
                        SelectionLength = modTile.Name.Length
                    };

                    var dialog = new ContentDialog
                    {
                        Title = modTile.IsCategory ? SharedUtilities.GetTranslation(lang, "RenameDialog_Category_Title") : SharedUtilities.GetTranslation(lang, "RenameDialog_Mod_Title"),
                        Content = textBox,
                        PrimaryButtonText = SharedUtilities.GetTranslation(lang, "OK") ?? "OK",
                        CloseButtonText = SharedUtilities.GetTranslation(lang, "Cancel") ?? "Cancel",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = this.XamlRoot
                    };

                    var result = await dialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        var newName = textBox.Text.Trim();

                        if (string.IsNullOrEmpty(newName))
                        {
                            await ShowErrorDialog(SharedUtilities.GetTranslation(lang, "Error_Title"), SharedUtilities.GetTranslation(lang, "RenameDialog_EmptyName_Error"));
                            return;
                        }

                        if (newName == modTile.Name)
                        {
                            // No change - just refresh the tile image
                            Logger.LogInfo($"Name unchanged for {(modTile.IsCategory ? "category" : "mod")} '{modTile.Name}' - refreshing tile image");

                            try
                            {
                                var modsPath = SharedUtilities.GetSafeXXMIModsPath();
                                string? fullModPath = null;

                                if (modTile.IsCategory)
                                {
                                    fullModPath = Path.Combine(modsPath, modTile.Directory);
                                }
                                else
                                {
                                    foreach (var categoryDir in Directory.GetDirectories(modsPath))
                                    {
                                        var modPath = Path.Combine(categoryDir, modTile.Directory);
                                        if (Directory.Exists(modPath))
                                        {
                                            fullModPath = modPath;
                                            break;
                                        }
                                    }
                                }

                                if (fullModPath != null)
                                {
                                    RefreshModTileImage(fullModPath);
                                    Logger.LogInfo($"Refreshed tile image: {fullModPath}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Failed to refresh mod tile image for '{modTile.Name}'", ex);
                            }

                            return;
                        }

                        if (!SecurityValidator.IsValidModDirectoryName(newName))
                        {
                            await ShowErrorDialog(SharedUtilities.GetTranslation(lang, "Error_Title"), SharedUtilities.GetTranslation(lang, "RenameDialog_InvalidName_Error"));
                            return;
                        }

                        await RenameItemAsync(modTile, newName);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModGridPage.ContextMenu_Rename_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModGridPage.ContextMenu_Rename_Click failed", ex);
            }
        }
        
        private async Task RenameItemAsync(ModTile modTile, string newName)
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            try
            {
                Logger.LogInfo($"RenameItemAsync called - modTile.Name: '{modTile.Name}', modTile.Directory: '{modTile.Directory}', newName: '{newName}'");
                
                var modsPath = SharedUtilities.GetSafeXXMIModsPath();
                string? currentPath = null;
                string? parentPath = null;
                
                if (modTile.IsCategory)
                {
                    // Renaming category
                    currentPath = Path.Combine(modsPath, modTile.Directory);
                    parentPath = modsPath;
                }
                else
                {
                    // Renaming mod - find it in categories (always DISABLED_ now)
                    foreach (var categoryDir in Directory.GetDirectories(modsPath))
                    {
                        var modPath = Path.Combine(categoryDir, modTile.Directory);
                        if (Directory.Exists(modPath))
                        {
                            currentPath = modPath;
                            parentPath = categoryDir;
                            break;
                        }
                    }
                }
                
                if (currentPath == null || parentPath == null)
                {
                    await ShowErrorDialog(
                        SharedUtilities.GetTranslation(lang, "Error_Title"),
                        string.Format(
                            SharedUtilities.GetTranslation(lang, "Notify_RenameDirectoryMissing"),
                            modTile.IsCategory ? "category" : "mod"));
                    return;
                }
                
                Logger.LogInfo($"Found item at: '{currentPath}'");
                
                // New directory name (mods are always DISABLED_ at this point)
                string newDirectoryName = modTile.IsCategory ? newName : "DISABLED_" + newName;
                var newPath = Path.Combine(parentPath, newDirectoryName);
                
                Logger.LogInfo($"Will rename: '{currentPath}' -> '{newPath}'");
                
                // Safety check
                if (string.Equals(currentPath, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInfo("Source and destination are the same - nothing to do");
                    return;
                }
                
                // Check if target already exists
                if (Directory.Exists(newPath))
                {
                    await ShowErrorDialog(
                        SharedUtilities.GetTranslation(lang, "Error_Title"),
                        string.Format(
                            SharedUtilities.GetTranslation(lang, "Notify_RenameAlreadyExists"),
                            modTile.IsCategory ? "category" : "mod",
                            newName));
                    return;
                }
                
                // Perform the rename with retry loop
                bool success = false;
                while (!success)
                {
                    try
                    {
                        Services.FileAccessQueue.MoveDirectory(currentPath, newPath);
                        Logger.LogInfo($"Successfully renamed directory");
                        success = true;
                    }
                    catch (IOException ex)
                    {
                        Logger.LogError($"Failed to rename directory - IOException: {ex.Message}");
                        
                        // Show retry dialog
                        var dialog = new ContentDialog
                        {
                            XamlRoot = this.XamlRoot,
                            Title = "Folder Locked",
                            Content = $"The folder '{modTile.Name}' is locked by another process (possibly Windows Explorer).\n\nClose any windows viewing this folder and try again.",
                            PrimaryButtonText = "Retry",
                            CloseButtonText = "Cancel",
                            DefaultButton = ContentDialogButton.Primary
                        };
                        
                        var result = await dialog.ShowAsync();
                        if (result != ContentDialogResult.Primary)
                        {
                            // User cancelled
                            return;
                        }
                        // Loop will retry
                    }
                }
                
                // Update the ModTile object
                var oldDirectory = modTile.Directory;
                modTile.Name = newName;
                modTile.Directory = newDirectoryName;
                
                Logger.LogInfo($"Updated ModTile: Name='{modTile.Name}', Directory='{modTile.Directory}'");
                
                // Update _activeMods dictionary (mod is always inactive after rename)
                if (!modTile.IsCategory)
                {
                    var cleanOldName = GetCleanModName(oldDirectory);
                    var cleanNewName = GetCleanModName(newDirectoryName);
                    
                    if (_activeMods.ContainsKey(cleanOldName))
                    {
                        _activeMods.Remove(cleanOldName);
                    }
                    _activeMods[cleanNewName] = false;
                }
                
                Logger.LogInfo($"Successfully renamed {(modTile.IsCategory ? "category" : "mod")} from '{oldDirectory}' to '{newDirectoryName}'");
                
                // Save current scroll position
                double currentScrollPosition = 0;
                if (ModsScrollViewer != null)
                {
                    currentScrollPosition = ModsScrollViewer.VerticalOffset;
                }
                
                // Refresh the UI - PropertyChanged events handle the name update automatically
                // Just update _originalTableItems for search functionality if in table view
                if (CurrentViewMode == ViewMode.Table)
                {
                    var originalItem = _originalTableItems.FirstOrDefault(x => x == modTile);
                    if (originalItem != null && originalItem != modTile)
                    {
                        originalItem.Name = newName;
                        originalItem.Directory = newDirectoryName;
                    }
                }
                
                // Refresh the mod tile image
                try
                {
                    RefreshModTileImage(newPath);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to refresh tile image after rename", ex);
                }
                
                // Restore scroll position
                if (ModsScrollViewer != null && currentScrollPosition > 0)
                {
                    await Task.Delay(100);
                    ModsScrollViewer.ScrollToVerticalOffset(currentScrollPosition);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to rename {(modTile.IsCategory ? "category" : "mod")} '{modTile.Name}' to '{newName}'", ex);
                await ShowErrorDialog(
                    SharedUtilities.GetTranslation(lang, "Error_Title"),
                    string.Format(SharedUtilities.GetTranslation(lang, "Notify_RenameFailed"), ex.Message));
            }
        }
    }
}
