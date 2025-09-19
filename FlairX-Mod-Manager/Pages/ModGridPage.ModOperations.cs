using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    /// <summary>
    /// ModGridPage partial class - Mod operations (activation, deletion, folder operations)
    /// </summary>
    public sealed partial class ModGridPage : Page
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        private const uint FO_DELETE = 0x0003;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOCONFIRMATION = 0x0010;

        private void ModActiveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                Logger.LogInfo($"User clicked mod activation button for: {mod.Directory}");
                
                // Validate mod directory name for security
                if (!SecurityValidator.IsValidModDirectoryName(mod.Directory))
                {
                    Logger.LogWarning($"Invalid mod directory name rejected: {mod.Directory}");
                    return;
                }

                // Always use current path from settings
                var modsDir = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrWhiteSpace(modsDir))
                    modsDir = Path.Combine(AppContext.BaseDirectory, "XXMI", "ZZMI", "Mods");
                var modsDirFull = Path.GetFullPath(modsDir);
                var defaultModsDirFull = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "XXMI", "ZZMI", "Mods"));
                if (_lastSymlinkTarget != null && !_lastSymlinkTarget.Equals(modsDirFull, StringComparison.OrdinalIgnoreCase))
                {
                    RemoveAllSymlinks(_lastSymlinkTarget);
                }
                var linkPath = Path.Combine(modsDirFull, mod.Directory);
                
                // Find the mod folder in the new category-based structure
                var modLibraryDir = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryDir))
                    modLibraryDir = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                var absModDir = FindModFolderPath(modLibraryDir, mod.Directory);
                
                if (string.IsNullOrEmpty(absModDir))
                {
                    // Mod directory not found - remove tile dynamically
                    System.Diagnostics.Debug.WriteLine($"Mod directory '{mod.Directory}' not found during activation, removing tile...");
                    Logger.LogError($"Could not find mod folder for {mod.Directory}, removing from UI");
                    
                    // Remove from UI collection
                    _allMods.Remove(mod);
                    
                    // Also remove from active mods if it exists there
                    if (_activeMods.ContainsKey(mod.Directory))
                    {
                        _activeMods.Remove(mod.Directory);
                        SaveActiveMods();
                    }
                    
                    return;
                }
                // Remove double slashes in paths
                linkPath = CleanPath(linkPath);
                absModDir = CleanPath(absModDir);
                
                // Double-check that source directory still exists before creating symlink
                if (!Directory.Exists(absModDir))
                {
                    // Source directory disappeared - remove tile dynamically
                    System.Diagnostics.Debug.WriteLine($"Source mod directory '{absModDir}' no longer exists, removing tile...");
                    Logger.LogError($"Source mod directory for {mod.Directory} no longer exists, removing from UI");
                    
                    // Remove from UI collection
                    _allMods.Remove(mod);
                    
                    // Also remove from active mods if it exists there
                    if (_activeMods.ContainsKey(mod.Directory))
                    {
                        _activeMods.Remove(mod.Directory);
                        SaveActiveMods();
                    }
                    
                    return;
                }
                
                if (!_activeMods.TryGetValue(mod.Directory, out var isActive) || !isActive)
                {
                    Logger.LogInfo($"Activating mod: {mod.Directory}");
                    if (!Directory.Exists(modsDirFull))
                    {
                        Directory.CreateDirectory(modsDirFull);
                        Logger.LogInfo($"Created mods directory: {modsDirFull}");
                    }
                    if (!Directory.Exists(linkPath) && !File.Exists(linkPath))
                    {
                        CreateSymlink(linkPath, absModDir);
                    }
                    _activeMods[mod.Directory] = true;
                    mod.IsActive = true;
                    Logger.LogInfo($"Mod activated successfully: {mod.Directory}");
                }
                else
                {
                    Logger.LogInfo($"Deactivating mod: {mod.Directory}");
                    if ((Directory.Exists(linkPath) || File.Exists(linkPath)) && IsSymlink(linkPath))
                    {
                        Directory.Delete(linkPath, true);
                        Logger.LogInfo($"Removed symlink: {linkPath}");
                    }
                    _activeMods[mod.Directory] = false;
                    mod.IsActive = false;
                    Logger.LogInfo($"Mod deactivated successfully: {mod.Directory}");
                }
                SaveActiveMods();
                SaveSymlinkState(modsDirFull);
                
                // Update table view if it exists - sync the IsActive state
                if (_originalTableItems != null)
                {
                    var tableItem = _originalTableItems.FirstOrDefault(x => x.Directory == mod.Directory);
                    if (tableItem != null)
                    {
                        tableItem.IsActive = mod.IsActive;
                    }
                }
                
                // Also update the currently displayed table items if search is active
                if (ModsTableList?.ItemsSource is IEnumerable<ModTile> currentTableItems && currentTableItems != _originalTableItems)
                {
                    var currentItem = currentTableItems.FirstOrDefault(x => x.Directory == mod.Directory);
                    if (currentItem != null)
                    {
                        currentItem.IsActive = mod.IsActive;
                    }
                }
                
                // Reset hover only on clicked tile
                mod.IsHovered = false;
            }
        }

        private void ModActiveButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                mod.IsHovered = true;
            }
        }

        private void ModActiveButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                mod.IsHovered = false;
            }
        }

        /// <summary>
        /// Finds the full path to a mod folder in the category-based structure
        /// </summary>
        private string? FindModFolderPath(string modLibraryDir, string modDirectoryName)
        {
            return FindModFolderPathStatic(modLibraryDir, modDirectoryName);
        }

        /// <summary>
        /// Static version of FindModFolderPath for use in static methods
        /// </summary>
        private static string? FindModFolderPathStatic(string modLibraryDir, string modDirectoryName)
        {
            try
            {
                // Search through all category directories to find the mod
                foreach (var categoryDir in Directory.GetDirectories(modLibraryDir))
                {
                    var modPath = Path.Combine(categoryDir, modDirectoryName);
                    if (Directory.Exists(modPath))
                    {
                        return Path.GetFullPath(modPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error finding mod folder path for {modDirectoryName}", ex);
            }
            
            return null;
        }

        private void OpenModFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile tile)
            {
                Logger.LogInfo($"User clicked open folder button for: {tile.Directory}");
                
                // Validate directory name for security
                if (!SecurityValidator.IsValidModDirectoryName(tile.Directory))
                {
                    Logger.LogWarning($"Invalid directory name rejected for folder open: {tile.Directory}");
                    return;
                }

                string folderPath;
                
                if (tile.IsCategory)
                {
                    // Open category folder
                    var gameTag = SettingsManager.CurrentSelectedGame;
                    if (string.IsNullOrEmpty(gameTag)) return;
                    
                    var gameModLibraryPath = AppConstants.GameConfig.GetModLibraryPath(gameTag);
                    folderPath = PathManager.GetAbsolutePath(Path.Combine(gameModLibraryPath, tile.Directory));
                }
                else
                {
                    // Find the mod folder in the new category-based structure
                    var modLibraryDir = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                    if (string.IsNullOrWhiteSpace(modLibraryDir))
                        modLibraryDir = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                    
                    folderPath = FindModFolderPath(modLibraryDir, tile.Directory) ?? "";
                }
                
                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                {
                    Logger.LogInfo($"Opening folder in explorer: {folderPath}");
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{folderPath}\"",
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                    Logger.LogInfo($"Successfully opened folder: {folderPath}");
                }
                else
                {
                    Logger.LogWarning($"Folder not found or empty path: {folderPath}");
                }
            }
        }

        private void OpenModFolderButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                mod.IsFolderHovered = true;
            }
        }

        private void OpenModFolderButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                mod.IsFolderHovered = false;
            }
        }

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
                var modLibraryDir = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrWhiteSpace(modLibraryDir))
                    modLibraryDir = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                
                var modFolderPath = FindModFolderPath(modLibraryDir, mod.Directory);
                
                if (string.IsNullOrEmpty(modFolderPath) || !Directory.Exists(modFolderPath))
                {
                    Logger.LogError($"Could not find mod folder for deletion: {mod.Directory}");
                    return; // Folder doesn't exist
                }

                // Move folder to recycle bin using Windows Shell API
                MoveToRecycleBin(modFolderPath);

                // Remove from active mods if it was active
                if (mod.IsActive && _activeMods.ContainsKey(mod.Directory))
                {
                    _activeMods.Remove(mod.Directory);
                    SaveActiveMods();
                }

                // Remove from cache
                lock (_cacheLock)
                {
                    _modJsonCache.Remove(mod.Directory);
                    _modFileTimestamps.Remove(mod.Directory);
                }

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
                var errorDialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(langDict, "Error_Title"),
                    Content = ex.Message,
                    CloseButtonText = SharedUtilities.GetTranslation(langDict, "OK"),
                    XamlRoot = this.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
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

        private void MoveToRecycleBin(string path)
        {
            var shf = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = path + '\0', // Must be null-terminated
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION
            };
            
            int result = SHFileOperation(ref shf);
            if (result != 0)
            {
                throw new Exception($"Failed to move folder to recycle bin. Error code: {result}");
            }
        }

        private void CreateSymlink(string linkPath, string targetPath)
        {
            try
            {
                // Normalize paths to handle spaces and special characters properly
                linkPath = Path.GetFullPath(linkPath);
                targetPath = Path.GetFullPath(targetPath);
                
                // Ensure target directory exists
                if (!Directory.Exists(targetPath))
                {
                    Logger.LogError($"Target directory does not exist: {targetPath}");
                    return;
                }

                // Ensure parent directory of link exists
                var linkParent = Path.GetDirectoryName(linkPath);
                if (!string.IsNullOrEmpty(linkParent) && !Directory.Exists(linkParent))
                {
                    Directory.CreateDirectory(linkParent);
                }

                // Create the symbolic link
                bool success = CreateSymbolicLink(linkPath, targetPath, 1); // 1 = directory
                if (!success)
                {
                    var error = Marshal.GetLastWin32Error();
                    Logger.LogError($"Failed to create symlink from {linkPath} to {targetPath}. Win32 Error: {error}");
                }
                else
                {
                    Logger.LogInfo($"Created symlink: {linkPath} -> {targetPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception creating symlink from {linkPath} to {targetPath}", ex);
            }
        }

        private bool IsSymlink(string path)
        {
            var dirInfo = new DirectoryInfo(path);
            return dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }

        private void RemoveAllSymlinks(string modsDir)
        {
            if (!Directory.Exists(modsDir)) return;
            foreach (var dir in Directory.GetDirectories(modsDir))
            {
                if (IsSymlink(dir))
                    Directory.Delete(dir);
            }
        }

        private void SaveSymlinkState(string targetPath)
        {
            _lastSymlinkTarget = targetPath;
            SaveSymlinkState();
        }
    }
}