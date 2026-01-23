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

namespace FlairX_Mod_Manager.Pages
{
    /// <summary>
    /// ModGridPage partial class - Mod operations (activation, deletion, folder operations)
    /// </summary>
    public sealed partial class ModGridPage : Page
    {

        
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

        private async void ModActiveButton_Click(object sender, RoutedEventArgs e)
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

                // NEW SYSTEM: Toggle activation by renaming with DISABLED_ prefix
                if (!_activeMods.TryGetValue(mod.Directory, out var isActive) || !isActive)
                {
                    // Activate mod - check for category conflicts first
                    Logger.LogInfo($"Activating mod: {mod.Directory}");
                    
                    // Check if there are already active mods in the same category
                    var activeModsInCategory = GetActiveModsInCategory(mod.Directory);
                    if (activeModsInCategory.Count > 0)
                    {
                        // Check if auto-deactivation is enabled
                        if (SettingsManager.Current.AutoDeactivateConflictingMods)
                        {
                            // Auto-deactivate conflicting mods
                            var categoryName = GetModCategory(mod.Directory);
                            Logger.LogInfo($"Auto-deactivating {activeModsInCategory.Count} conflicting mods in category '{categoryName}'");
                            
                            await DeactivateModsInCategory(categoryName, mod.Directory);
                            
                            // Update UI for deactivated mods
                            foreach (var activeModName in activeModsInCategory)
                            {
                                // Update _activeMods dictionary
                                _activeMods[activeModName] = false;
                                
                                // Find and update the ModTile in current view
                                var deactivatedTile = _allMods?.FirstOrDefault(m => GetCleanModName(m.Directory) == GetCleanModName(activeModName));
                                if (deactivatedTile != null)
                                {
                                    deactivatedTile.IsActive = false;
                                    // Update directory name if it was renamed with DISABLED_ prefix
                                    var newName = DISABLED_PREFIX + GetCleanModName(activeModName);
                                    if (deactivatedTile.Directory != newName)
                                    {
                                        deactivatedTile.Directory = newName;
                                        deactivatedTile.Name = GetCleanModName(activeModName); // Keep clean name for display
                                    }
                                }
                                
                                // Update table view if it exists
                                if (_originalTableItems != null)
                                {
                                    var tableItem = _originalTableItems.FirstOrDefault(x => GetCleanModName(x.Directory) == GetCleanModName(activeModName));
                                    if (tableItem != null)
                                    {
                                        tableItem.IsActive = false;
                                        var newName = DISABLED_PREFIX + GetCleanModName(activeModName);
                                        if (tableItem.Directory != newName)
                                        {
                                            tableItem.Directory = newName;
                                            tableItem.Name = GetCleanModName(activeModName); // Keep clean name for display
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Show category conflict dialog
                            var categoryName = GetModCategory(mod.Directory);
                            var dialog = new FlairX_Mod_Manager.Dialogs.CategoryConflictDialog(
                                categoryName, 
                                mod.Name, 
                                activeModsInCategory);
                            
                            dialog.XamlRoot = this.Content.XamlRoot;
                            
                            var result = await dialog.ShowAsync();
                            if (result != ContentDialogResult.Primary)
                            {
                                Logger.LogInfo($"User cancelled mod activation due to category conflict: {mod.Directory}");
                                return; // User cancelled activation
                            }
                            
                            Logger.LogInfo($"User confirmed mod activation despite category conflict: {mod.Directory}");
                        }
                    }
                    
                    // Proceed with activation
                    if (ActivateModByRename(mod.Directory))
                    {
                        _activeMods[mod.Directory] = true;
                        mod.IsActive = true;
                        Logger.LogInfo($"Mod activated successfully: {mod.Directory}");
                    }
                    else
                    {
                        Logger.LogError($"Failed to activate mod: {mod.Directory}");
                        return;
                    }
                }
                else
                {
                    // Deactivate mod - add DISABLED_ prefix
                    Logger.LogInfo($"Deactivating mod: {mod.Directory}");
                    if (DeactivateModByRename(mod.Directory, out string newModName))
                    {
                        _activeMods[mod.Directory] = false;
                        mod.IsActive = false;
                        
                        // Update the mod directory name if it changed (due to duplicate handling)
                        if (newModName != mod.Directory)
                        {
                            // Remove old entry and add new one
                            _activeMods.Remove(mod.Directory);
                            _activeMods[newModName] = false;
                            
                            // Update the tile's directory and name
                            mod.Directory = newModName;
                            mod.Name = GetCleanModName(newModName); // Keep clean name for display
                            Logger.LogInfo($"Updated mod tile name to: {newModName}");
                        }
                        
                        Logger.LogInfo($"Mod deactivated successfully: {mod.Directory}");
                    }
                    else
                    {
                        Logger.LogError($"Failed to deactivate mod: {mod.Directory}");
                        return;
                    }
                }
                
                SaveActiveMods();
                
                // Update table view if it exists - sync the IsActive state and directory name
                if (_originalTableItems != null)
                {
                    var tableItem = _originalTableItems.FirstOrDefault(x => x.Directory == mod.Directory);
                    if (tableItem != null)
                    {
                        tableItem.IsActive = mod.IsActive;
                        // Directory name is already updated in mod object, so tableItem should reflect the same
                    }
                }
                
                // Also update the currently displayed table items if search is active
                if (ModsTableList?.ItemsSource is IEnumerable<ModTile> currentTableItems && currentTableItems != _originalTableItems)
                {
                    var currentItem = currentTableItems.FirstOrDefault(x => x.Directory == mod.Directory);
                    if (currentItem != null)
                    {
                        currentItem.IsActive = mod.IsActive;
                        // Directory name is already updated in mod object, so currentItem should reflect the same
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

        // Tile hover effects - scale only the image
        private void TileButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                try
                {
                    var modImage = FindChildByName<Image>(button, "ModImage");
                    if (modImage == null) return;
                    
                    // Create scale transform if it doesn't exist
                    if (modImage.RenderTransform is not ScaleTransform)
                    {
                        modImage.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                        modImage.RenderTransform = new ScaleTransform();
                    }
                    
                    var scaleTransform = (ScaleTransform)modImage.RenderTransform;
                    
                    // Animate scale to 1.10 (10% larger)
                    var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    
                    var scaleXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 1.10,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleXAnim, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
                    storyboard.Children.Add(scaleXAnim);
                    
                    var scaleYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 1.10,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleYAnim, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");
                    storyboard.Children.Add(scaleYAnim);
                    
                    storyboard.Begin();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in TileButton_PointerEntered", ex);
                }
            }
        }
        
        private T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                {
                    return element;
                }
                var result = FindChildByName<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        private void TileButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                try
                {
                    var modImage = FindChildByName<Image>(button, "ModImage");
                    if (modImage?.RenderTransform is not ScaleTransform scaleTransform) return;
                    
                    // Animate scale back to 1.0
                    var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    
                    var scaleXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleXAnim, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
                    storyboard.Children.Add(scaleXAnim);
                    
                    var scaleYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleYAnim, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");
                    storyboard.Children.Add(scaleYAnim);
                    
                    storyboard.Begin();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in TileButton_PointerExited", ex);
                }
            }
        }

        // Activate button hover effects - show button and hide name text
        private void ActivateButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button activateBtn)
            {
                try
                {
                    // Find parent tile button to get name texts
                    var parent = activateBtn.Parent;
                    while (parent != null && !(parent is Button tileButton && tileButton.Name == "TileButton"))
                    {
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                    
                    if (parent is Button tileBtn)
                    {
                        var nameText = FindChildByName<TextBlock>(tileBtn, "ModNameText");
                        var nameTextActive = FindChildByName<TextBlock>(tileBtn, "ModNameTextActive");
                        
                        // Fade out name texts
                        if (nameText != null)
                        {
                            var fadeOut = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                            {
                                To = 0,
                                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                            };
                            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeOut, nameText);
                            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeOut, "Opacity");
                            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                            storyboard.Children.Add(fadeOut);
                            storyboard.Begin();
                        }
                        
                        if (nameTextActive != null)
                        {
                            var fadeOut = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                            {
                                To = 0,
                                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                            };
                            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeOut, nameTextActive);
                            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeOut, "Opacity");
                            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                            storyboard.Children.Add(fadeOut);
                            storyboard.Begin();
                        }
                    }
                    
                    // Fade in activate button
                    var fadeIn = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 1,
                        Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeIn, activateBtn);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeIn, "Opacity");
                    var storyboard2 = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    storyboard2.Children.Add(fadeIn);
                    storyboard2.Begin();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in ActivateButton_PointerEntered", ex);
                }
            }
        }

        private void ActivateButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button activateBtn)
            {
                try
                {
                    // Find parent tile button to get name texts
                    var parent = activateBtn.Parent;
                    while (parent != null && !(parent is Button tileButton && tileButton.Name == "TileButton"))
                    {
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                    
                    if (parent is Button tileBtn)
                    {
                        var nameText = FindChildByName<TextBlock>(tileBtn, "ModNameText");
                        var nameTextActive = FindChildByName<TextBlock>(tileBtn, "ModNameTextActive");
                        
                        // Fade in name texts
                        if (nameText != null)
                        {
                            var fadeIn = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                            {
                                To = 1,
                                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn }
                            };
                            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeIn, nameText);
                            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeIn, "Opacity");
                            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                            storyboard.Children.Add(fadeIn);
                            storyboard.Begin();
                        }
                        
                        if (nameTextActive != null)
                        {
                            var fadeIn = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                            {
                                To = 1,
                                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn }
                            };
                            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeIn, nameTextActive);
                            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeIn, "Opacity");
                            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                            storyboard.Children.Add(fadeIn);
                            storyboard.Begin();
                        }
                    }
                    
                    // Fade out activate button
                    var fadeOut = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeOut, activateBtn);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeOut, "Opacity");
                    var storyboard2 = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    storyboard2.Children.Add(fadeOut);
                    storyboard2.Begin();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in ActivateButton_PointerExited", ex);
                }
            }
        }

        // Open Directory button hover effects - show button and hide name text (for categories)
        private void OpenDirectoryButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button openDirBtn)
            {
                try
                {
                    // Find parent tile button to get name text
                    var parent = openDirBtn.Parent;
                    while (parent != null && !(parent is Button tileButton && tileButton.Name == "TileButton"))
                    {
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                    
                    if (parent is Button tileBtn)
                    {
                        var nameText = FindChildByName<TextBlock>(tileBtn, "ModNameText");
                        
                        // Fade out name text
                        if (nameText != null)
                        {
                            var fadeOut = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                            {
                                To = 0,
                                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                            };
                            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeOut, nameText);
                            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeOut, "Opacity");
                            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                            storyboard.Children.Add(fadeOut);
                            storyboard.Begin();
                        }
                    }
                    
                    // Fade in open directory button
                    var fadeIn = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 1,
                        Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeIn, openDirBtn);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeIn, "Opacity");
                    var storyboard2 = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    storyboard2.Children.Add(fadeIn);
                    storyboard2.Begin();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in OpenDirectoryButton_PointerEntered", ex);
                }
            }
        }

        private void OpenDirectoryButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button openDirBtn)
            {
                try
                {
                    // Find parent tile button to get name text
                    var parent = openDirBtn.Parent;
                    while (parent != null && !(parent is Button tileButton && tileButton.Name == "TileButton"))
                    {
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                    
                    if (parent is Button tileBtn)
                    {
                        var nameText = FindChildByName<TextBlock>(tileBtn, "ModNameText");
                        
                        // Fade in name text
                        if (nameText != null)
                        {
                            var fadeIn = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                            {
                                To = 1,
                                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn }
                            };
                            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeIn, nameText);
                            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeIn, "Opacity");
                            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                            storyboard.Children.Add(fadeIn);
                            storyboard.Begin();
                        }
                    }
                    
                    // Fade out open directory button
                    var fadeOut = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeOut, openDirBtn);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeOut, "Opacity");
                    var storyboard2 = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    storyboard2.Children.Add(fadeOut);
                    storyboard2.Begin();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in OpenDirectoryButton_PointerExited", ex);
                }
            }
        }



        /// <summary>
        /// Finds the full path to a mod folder in the category-based structure
        /// </summary>
        private string? FindModFolderPath(string modLibraryDir, string modDirectoryName)
        {
            return FindModFolderPathStatic(modLibraryDir, modDirectoryName);
        }

        // FindModFolderPathStatic moved to StaticUtilities.cs

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
                    
                    var gamemodsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                    folderPath = PathManager.GetAbsolutePath(Path.Combine(gamemodsPath, tile.Directory));
                }
                else
                {
                    // Find the mod folder in the new category-based structure
                    var modLibraryDir = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                    
                    
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

        private void FavoriteStarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile tile)
            {
                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag)) return;
                
                if (tile.IsCategory)
                {
                    // Handle category favorites
                    SettingsManager.ToggleCategoryFavorite(gameTag, tile.Name);
                    tile.IsFavorite = SettingsManager.IsCategoryFavorite(gameTag, tile.Name);
                    
                    Logger.LogInfo($"Toggled favorite for category: {tile.Name}, IsFavorite: {tile.IsFavorite}");
                    
                    // Re-sort categories with animation to move favorites to top
                    if (_currentViewMode == ViewMode.Categories)
                    {
                        SortCategoriesByFavoritesAnimated();
                    }
                    
                    // Update menu star in MainWindow with animation
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.UpdateMenuStarForCategoryAnimated(tile.Name, tile.IsFavorite);
                    }
                }
                else
                {
                    // Handle mod favorites
                    SettingsManager.ToggleModFavorite(gameTag, tile.Name);
                    tile.IsFavorite = SettingsManager.IsModFavorite(gameTag, tile.Name);
                    
                    Logger.LogInfo($"Toggled favorite for mod: {tile.Name}, IsFavorite: {tile.IsFavorite}");
                    
                    // Re-sort mods with animation to move favorites to top
                    if (_currentViewMode == ViewMode.Mods)
                    {
                        SortModsByFavoritesAnimated();
                    }
                }
            }
        }
        
        private async void SortCategoriesByFavoritesAnimated()
        {
            try
            {
                var items = ModsGrid.ItemsSource as ObservableCollection<ModTile>;
                if (items == null || items.Count == 0) return;
                
                // Get current order
                var currentOrder = items.ToList();
                
                // Calculate new order
                var sortedItems = items.OrderByDescending(m => m.IsFavorite)
                                      .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                                      .ToList();
                
                // Check if order changed
                bool orderChanged = false;
                for (int i = 0; i < currentOrder.Count; i++)
                {
                    if (currentOrder[i] != sortedItems[i])
                    {
                        orderChanged = true;
                        break;
                    }
                }
                if (!orderChanged) return;
                
                // Fade out the grid
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                var fadeOutStoryboard = new Storyboard();
                Storyboard.SetTarget(fadeOut, ModsGrid);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");
                fadeOutStoryboard.Children.Add(fadeOut);
                fadeOutStoryboard.Begin();
                
                // Wait for fade out to complete
                await Task.Delay(150);
                
                // Now do the refresh while invisible
                for (int targetIndex = 0; targetIndex < sortedItems.Count; targetIndex++)
                {
                    var item = sortedItems[targetIndex];
                    int currentIndex = items.IndexOf(item);
                    if (currentIndex != targetIndex && currentIndex >= 0)
                    {
                        items.Move(currentIndex, targetIndex);
                    }
                }
                
                // Small delay to ensure refresh is complete
                await Task.Delay(50);
                
                // Fade in the grid
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                var fadeInStoryboard = new Storyboard();
                Storyboard.SetTarget(fadeIn, ModsGrid);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");
                fadeInStoryboard.Children.Add(fadeIn);
                fadeInStoryboard.Begin();
                
                Logger.LogInfo("Categories sorted by favorites with animation");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sorting categories by favorites with animation", ex);
                // Reset opacity and fallback
                ModsGrid.Opacity = 1;
                SortCategoriesByFavorites();
            }
        }

        private void SortCategoriesByFavorites()
        {
            try
            {
                var items = ModsGrid.ItemsSource as ObservableCollection<ModTile>;
                if (items == null) return;
                
                // Sort: favorites first, then alphabetically
                var sortedItems = items.OrderByDescending(m => m.IsFavorite)
                                      .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                                      .ToList();
                
                // Clear and re-add in sorted order
                items.Clear();
                foreach (var item in sortedItems)
                {
                    items.Add(item);
                }
                
                Logger.LogInfo("Categories sorted by favorites");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sorting categories by favorites", ex);
            }
        }
        
        public void RefreshCategoryFavorites()
        {
            try
            {
                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag)) return;
                
                // Update favorite status for all category tiles
                var items = ModsGrid.ItemsSource as ObservableCollection<ModTile>;
                if (items != null)
                {
                    foreach (var item in items.Where(i => i.IsCategory))
                    {
                        item.IsFavorite = SettingsManager.IsCategoryFavorite(gameTag, item.Name);
                    }
                    
                    // Re-sort
                    SortCategoriesByFavorites();
                }
                
                Logger.LogInfo("Refreshed category favorites from menu");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error refreshing category favorites", ex);
            }
        }
        
        public async void RefreshCategoryFavoritesAnimated()
        {
            try
            {
                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag)) return;
                
                var items = ModsGrid.ItemsSource as ObservableCollection<ModTile>;
                if (items == null || items.Count == 0) return;
                
                // Update favorite status for all category tiles
                foreach (var item in items.Where(i => i.IsCategory))
                {
                    item.IsFavorite = SettingsManager.IsCategoryFavorite(gameTag, item.Name);
                }
                
                // Get current order
                var currentOrder = items.ToList();
                
                // Calculate new order
                var sortedItems = items.OrderByDescending(m => m.IsFavorite)
                                      .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                                      .ToList();
                
                // Check if order changed
                bool orderChanged = false;
                for (int i = 0; i < currentOrder.Count; i++)
                {
                    if (currentOrder[i] != sortedItems[i])
                    {
                        orderChanged = true;
                        break;
                    }
                }
                if (!orderChanged) return;
                
                // Fade out the grid
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                var fadeOutStoryboard = new Storyboard();
                Storyboard.SetTarget(fadeOut, ModsGrid);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");
                fadeOutStoryboard.Children.Add(fadeOut);
                fadeOutStoryboard.Begin();
                
                // Wait for fade out to complete
                await Task.Delay(150);
                
                // Now do the refresh while invisible
                for (int targetIndex = 0; targetIndex < sortedItems.Count; targetIndex++)
                {
                    var item = sortedItems[targetIndex];
                    int currentIndex = items.IndexOf(item);
                    if (currentIndex != targetIndex && currentIndex >= 0)
                    {
                        items.Move(currentIndex, targetIndex);
                    }
                }
                
                // Small delay to ensure refresh is complete
                await Task.Delay(50);
                
                // Fade in the grid
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                var fadeInStoryboard = new Storyboard();
                Storyboard.SetTarget(fadeIn, ModsGrid);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");
                fadeInStoryboard.Children.Add(fadeIn);
                fadeInStoryboard.Begin();
                
                Logger.LogInfo("Refreshed category favorites with animation");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error refreshing category favorites with animation", ex);
                // Reset opacity and fallback
                ModsGrid.Opacity = 1;
                RefreshCategoryFavorites();
            }
        }
        
        private async void SortModsByFavoritesAnimated()
        {
            try
            {
                var items = ModsGrid.ItemsSource as ObservableCollection<ModTile>;
                if (items == null || items.Count == 0) return;
                
                // Get current order
                var currentOrder = items.ToList();
                
                // Calculate new order - favorites first, then by current sort mode
                var sortedItems = items.OrderByDescending(m => m.IsFavorite)
                                      .ThenBy(m => GetSortKey(m))
                                      .ToList();
                
                // Check if order changed
                bool orderChanged = false;
                for (int i = 0; i < currentOrder.Count; i++)
                {
                    if (currentOrder[i] != sortedItems[i])
                    {
                        orderChanged = true;
                        break;
                    }
                }
                if (!orderChanged) return;
                
                // Fade out the grid
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                var fadeOutStoryboard = new Storyboard();
                Storyboard.SetTarget(fadeOut, ModsGrid);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");
                fadeOutStoryboard.Children.Add(fadeOut);
                fadeOutStoryboard.Begin();
                
                // Wait for fade out to complete
                await Task.Delay(150);
                
                // Now do the refresh while invisible
                for (int targetIndex = 0; targetIndex < sortedItems.Count; targetIndex++)
                {
                    var item = sortedItems[targetIndex];
                    int currentIndex = items.IndexOf(item);
                    if (currentIndex != targetIndex && currentIndex >= 0)
                    {
                        items.Move(currentIndex, targetIndex);
                    }
                }
                
                // Small delay to ensure refresh is complete
                await Task.Delay(50);
                
                // Fade in the grid
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                var fadeInStoryboard = new Storyboard();
                Storyboard.SetTarget(fadeIn, ModsGrid);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");
                fadeInStoryboard.Children.Add(fadeIn);
                fadeInStoryboard.Begin();
                
                Logger.LogInfo("Mods sorted by favorites with animation");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sorting mods by favorites with animation", ex);
                // Reset opacity and fallback
                ModsGrid.Opacity = 1;
                SortModsByFavorites();
            }
        }
        
        private void SortModsByFavorites()
        {
            try
            {
                var items = ModsGrid.ItemsSource as ObservableCollection<ModTile>;
                if (items == null) return;
                
                // Sort: favorites first, then by current sort mode
                var sortedItems = items.OrderByDescending(m => m.IsFavorite)
                                      .ThenBy(m => GetSortKey(m))
                                      .ToList();
                
                // Clear and re-add in sorted order
                items.Clear();
                foreach (var item in sortedItems)
                {
                    items.Add(item);
                }
                
                Logger.LogInfo("Mods sorted by favorites");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sorting mods by favorites", ex);
            }
        }
        
        private object GetSortKey(ModTile mod)
        {
            // Use current sort mode to determine secondary sort key
            return _currentSortMode switch
            {
                SortMode.NameAZ => mod.Name,
                SortMode.NameZA => mod.Name,
                SortMode.CategoryAZ => mod.Category,
                SortMode.CategoryZA => mod.Category,
                SortMode.LastCheckedNewest => mod.LastChecked,
                SortMode.LastCheckedOldest => mod.LastChecked,
                SortMode.LastUpdatedNewest => mod.LastUpdated,
                SortMode.LastUpdatedOldest => mod.LastUpdated,
                SortMode.ActiveFirst => mod.IsActive ? 0 : 1, // Active first
                SortMode.InactiveFirst => mod.IsActive ? 1 : 0, // Inactive first
                _ => mod.Name
            };
        }
        
        public void RefreshModFavorites()
        {
            try
            {
                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag)) return;
                
                // Update favorite status for all mod tiles
                var items = ModsGrid.ItemsSource as ObservableCollection<ModTile>;
                if (items != null)
                {
                    foreach (var item in items.Where(i => !i.IsCategory))
                    {
                        item.IsFavorite = SettingsManager.IsModFavorite(gameTag, item.Name);
                    }
                    
                    // Re-sort
                    SortModsByFavorites();
                }
                
                Logger.LogInfo("Refreshed mod favorites");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error refreshing mod favorites", ex);
            }
        }
        
        private void SetFavoriteTooltips()
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                
                // Find all favorite star buttons in the current view and set their tooltips
                if (ModsGrid?.ItemsSource is IEnumerable<ModTile> items)
                {
                    foreach (var item in items)
                    {
                        // The tooltip will be set when the button is created in the UI
                        // We'll handle this in the DataTemplate or when the button is loaded
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error setting favorite tooltips", ex);
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
                var modLibraryDir = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                
                
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
            else if (string.Equals(_currentCategory, "Broken", StringComparison.OrdinalIgnoreCase))
            {
                LoadBrokenModsOnly();
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

        // Symlink methods removed - using DISABLED_ prefix system instead
    }
}