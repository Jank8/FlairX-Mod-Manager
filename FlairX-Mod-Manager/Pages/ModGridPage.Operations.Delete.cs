using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModGridPage
    {

        private void DeleteModButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                Logger.LogInfo($"User clicked delete button for mod: {mod.Directory}");
                _ = DeleteModWithConfirmation(mod);
            }
        }

        private void DeleteModButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                mod.IsDeleteHovered = true;
            }
        }

        private void DeleteModButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                mod.IsDeleteHovered = false;
            }
        }

        private async Task DeleteModWithConfirmation(ModTile mod)
        {
            Logger.LogMethodEntry($"Attempting to delete mod: {mod.Directory}");
            try
            {
                Logger.LogInfo($"Showing delete confirmation dialog for: {mod.Directory}");
                // Show confirmation dialog
                var langDict = SharedUtilities.LoadLanguageDictionary();
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(langDict, "Delete_Mod_Confirm_Title"),
                    Content = string.Format(SharedUtilities.GetTranslation(langDict, "Delete_Mod_Confirm_Message"), mod.Name),
                    PrimaryButtonText = SharedUtilities.GetTranslation(langDict, "Delete"),
                    CloseButtonText = SharedUtilities.GetTranslation(langDict, "Cancel"),
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                {
                    Logger.LogInfo($"User cancelled deletion of mod: {mod.Directory}");
                    return; // User cancelled
                }
                
                Logger.LogInfo($"User confirmed deletion of mod: {mod.Directory}");

                // Show deletion effect immediately
                mod.IsBeingDeleted = true;
                await Task.Delay(500); // Show the effect for half a second

                // Validate mod directory name for security
                if (!SecurityValidator.IsValidModDirectoryName(mod.Directory))
                    return;

                // Get mod folder path using the new category-based structure
                var modLibraryDir = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                
                
                var modFolderPath = FindModFolderPath(modLibraryDir, mod.Directory);
                
                if (string.IsNullOrEmpty(modFolderPath) || !Directory.Exists(modFolderPath))
                {
                    Logger.LogError($"Could not find mod folder for deletion: {mod.Directory}");

                    // Silent before: the user pressed Delete and simply nothing happened.
                    if (App.Current is App deleteApp && deleteApp.MainWindow is MainWindow deleteWindow)
                    {
                        var deleteLang = SharedUtilities.LoadLanguageDictionary();
                        deleteWindow.ShowWarningInfo(
                            SharedUtilities.GetTranslation(deleteLang, "Notify_ModFolderNotFound"));
                    }

                    return; // Folder doesn't exist
                }

                // Move folder to recycle bin using Windows Shell API
                RecycleBinHelper.MoveToRecycleBin(modFolderPath, silent: false, fallbackToDelete: false);

                // Remove from active mods if it was active
                var cleanName = GetCleanModName(mod.Directory);
                if (mod.IsActive && _activeMods.ContainsKey(cleanName))
                {
                    _activeMods.Remove(cleanName);
                }

                // Remove from persistent lists
                var categoryName = mod.Category ?? "Other";
                ModListManager.RemoveFromAllLists(categoryName, cleanName);

                // No cache to remove - direct file reading

                // Remove the tile from both grid and table collections
                if (ModsGrid?.ItemsSource is System.Collections.ObjectModel.ObservableCollection<ModTile> gridCollection)
                {
                    var item = gridCollection.FirstOrDefault(x => x.Directory == mod.Directory);
                    if (item != null)
                    {
                        gridCollection.Remove(item); // Simply remove the tile - super smooth!
                    }
                }
                
                // Also remove from table collections
                if (ModsTableList?.ItemsSource is System.Collections.ObjectModel.ObservableCollection<ModTile> tableCollection)
                {
                    var item = tableCollection.FirstOrDefault(x => x.Directory == mod.Directory);
                    if (item != null)
                    {
                        tableCollection.Remove(item); // Remove from current table view
                    }
                }
                
                // Remove from original table items (the base collection for search/sort)
                var originalItem = _originalTableItems.FirstOrDefault(x => x.Directory == mod.Directory);
                if (originalItem != null)
                {
                    _originalTableItems.Remove(originalItem); // Remove from base table collection
                }

                LogToGridLog($"DELETED: Mod '{mod.Name}' moved to recycle bin");
            }
            catch (Exception ex)
            {
                LogToGridLog($"DELETE ERROR: Failed to delete mod '{mod.Name}': {ex.Message}");
                
                // Show error dialog
                var langDict = SharedUtilities.LoadLanguageDictionary();
                if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(ex.Message);
            }
        }

        private async Task RefreshGridWithScrollPosition(double scrollPosition)
        {
            // Reload the current view
            if (_currentCategory == null)
            {
                LoadAllMods();
            }
            else if (string.Equals(_currentCategory, "Active", StringComparison.OrdinalIgnoreCase))
            {
                LoadActiveModsOnly();
            }
            else if (string.Equals(_currentCategory, "Broken", StringComparison.OrdinalIgnoreCase))
            {
                LoadBrokenModsOnly();
            }
            else if (string.Equals(_currentCategory, "NoPreviews", StringComparison.OrdinalIgnoreCase))
            {
                LoadNoPreviewModsOnly();
            }
            else
            {
                LoadMods(_currentCategory);
            }

            // Wait longer for virtualized grid to fully load, then restore scroll position multiple times
            if (ModsScrollViewer != null && scrollPosition > 0)
            {
                // Try multiple times with increasing delays to handle virtualization
                await Task.Delay(200);
                ModsScrollViewer.ScrollToVerticalOffset(scrollPosition);
                
                await Task.Delay(100);
                ModsScrollViewer.ScrollToVerticalOffset(scrollPosition);
                
                await Task.Delay(100);
                ModsScrollViewer.ScrollToVerticalOffset(scrollPosition);
                
                // Final attempt with dispatcher priority
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    ModsScrollViewer.ScrollToVerticalOffset(scrollPosition);
                });
            }
        }

        /// <summary>
        /// Refreshes installation status in all open GameBanana browser instances
        /// </summary>
        private void RefreshGameBananaBrowserStatus()
        {
            try
            {
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.RefreshGameBananaBrowserInstallationStatus();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to refresh GameBanana browser installation status", ex);
            }
        }
    }
}
