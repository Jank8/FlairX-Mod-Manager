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
        // Queue for mod activation/deactivation operations
        private static readonly System.Threading.SemaphoreSlim _modOperationSemaphore = new System.Threading.SemaphoreSlim(1, 1);

        private async void ModActiveButton_Click(object sender, RoutedEventArgs e)
        {
            try
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

                    // Queue the operation to prevent race conditions when clicking rapidly
                    await _modOperationSemaphore.WaitAsync();
                    try
                    {
                        await ProcessModActivationAsync(mod);
                    }
                    finally
                    {
                        _modOperationSemaphore.Release();
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModGridPage.ModActiveButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModGridPage.ModActiveButton_Click failed", ex);
            }
        }

        private async Task ProcessModActivationAsync(ModTile mod)
        {
            // Check if Left Shift is held — skip conflict dialog/auto-deactivate, just activate
                bool leftShiftHeld = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.LeftShift) 
                    & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

                // NEW SYSTEM: Toggle activation by renaming with DISABLED_ prefix
                // Use mod.IsActive (UI state) to determine action instead of folder state
                if (!mod.IsActive)
                {
                    // Activate mod - check for category conflicts first
                    Logger.LogInfo($"ENTERING ACTIVATION BRANCH - Activating mod: {mod.Directory}");
                    
                    // Get category name and check if it's excluded from conflict detection
                    var categoryName = GetModCategory(mod.Directory);
                    var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                    var conflictExcludedCategories = SettingsManager.GetConflictExcludedCategories(gameTag);
                    bool isCategoryExcludedFromConflicts = conflictExcludedCategories.Contains(categoryName);
                    
                    // Check if there are already active mods in the same category (skip if category is excluded)
                    var activeModsInCategory = !isCategoryExcludedFromConflicts ? GetActiveModsInCategory(mod.Directory) : new List<string>();
                    
                    if (activeModsInCategory.Count > 0)
                    {
                        if (leftShiftHeld)
                        {
                            // Left Shift held: skip conflict handling entirely, just activate
                            Logger.LogInfo($"Left Shift held — skipping conflict dialog/auto-deactivate, activating directly: {mod.Directory}");
                        }
                        // Check if auto-deactivation is enabled
                        else if (SettingsManager.Current.AutoDeactivateConflictingMods)
                        {
                            // Auto-deactivate conflicting mods
                            Logger.LogInfo($"Auto-deactivating {activeModsInCategory.Count} conflicting mods in category '{categoryName}'");
                            
                            await DeactivateModsInCategory(categoryName, mod.Directory);
                            
                            // Update UI for deactivated mods
                            foreach (var activeModName in activeModsInCategory)
                            {
                                // Update _activeMods dictionary using clean name
                                var cleanName = GetCleanModName(activeModName);
                                _activeMods[cleanName] = false;
                                
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
                            // Check if user has chosen "don't ask again"
                            if (!SettingsManager.Current.DontAskCategoryConflictAgain)
                            {
                                // Show category conflict dialog
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
                                
                                // Save "don't ask again" preference if user confirmed and checked the box
                                if (dialog.DontAskAgain)
                                {
                                    SettingsManager.Current.DontAskCategoryConflictAgain = true;
                                    SettingsManager.Save();
                                    Logger.LogInfo("User selected 'Don't ask again' for category conflicts");
                                }
                                
                                Logger.LogInfo($"User confirmed mod activation despite category conflict: {mod.Directory}");
                            }
                            else
                            {
                                Logger.LogInfo($"Skipping category conflict dialog (user chose 'don't ask again'): {mod.Directory}");
                            }
                        }
                    }
                    else if (isCategoryExcludedFromConflicts)
                    {
                        Logger.LogInfo($"Category '{categoryName}' is excluded from conflict detection - allowing multiple active mods");
                    }
                    
                    // Proceed with activation
                    bool success = false;
                    while (!success)
                    {
                        if (ActivateModByRename(mod.Directory))
                        {
                            var cleanName = GetCleanModName(mod.Directory);
                            _activeMods[cleanName] = true;
                            mod.IsActive = true;
                            Logger.LogInfo($"Mod activated successfully: {mod.Directory}");
                            success = true;
                            
                            // Refresh GameBanana browser installation status
                            RefreshGameBananaBrowserStatus();
                        }
                        else
                        {
                            Logger.LogError($"Failed to activate mod: {mod.Directory}");
                            
                            // Show retry dialog
                            var dialog = new ContentDialog
                            {
                                XamlRoot = this.XamlRoot,
                                Title = "Folder Locked",
                                Content = $"The folder '{mod.Name}' is locked by another process (possibly Windows Explorer).\n\nClose any windows viewing this folder and try again.",
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
                }
                else
                {
                    // Deactivate mod - add DISABLED_ prefix
                    Logger.LogInfo($"Deactivating mod: {mod.Directory}");
                    
                    bool success = false;
                    while (!success)
                    {
                        if (DeactivateModByRename(mod.Directory, out string newModName))
                        {
                            var cleanName = GetCleanModName(mod.Directory);
                            _activeMods[cleanName] = false;
                            mod.IsActive = false;
                            
                            // Update the mod directory name if it changed (due to duplicate handling)
                            if (newModName != mod.Directory)
                            {
                                // Update the tile's directory and name
                                mod.Directory = newModName;
                                mod.Name = GetCleanModName(newModName); // Keep clean name for display
                                Logger.LogInfo($"Updated mod tile name to: {newModName}");
                            }
                            
                            Logger.LogInfo($"Mod deactivated successfully: {mod.Directory}");
                            success = true;
                            
                            // Refresh GameBanana browser installation status
                            RefreshGameBananaBrowserStatus();
                        }
                        else
                        {
                            Logger.LogError($"Failed to deactivate mod: {mod.Directory}");
                            
                            // Show retry dialog
                            var dialog = new ContentDialog
                            {
                                XamlRoot = this.XamlRoot,
                                Title = "Folder Locked",
                                Content = $"The folder '{mod.Name}' is locked by another process (possibly Windows Explorer).\n\nClose any windows viewing this folder and try again.",
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
                }
                
                // Send F10 to reload mods in game if enabled (debounced to prevent spam)
                if (SettingsManager.Current.SendF10OnModChange)
                {
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                        mainWindow.SendF10KeyPressDebounced();
                }
                
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
                    if (OpenFolder(folderPath))
                    {
                        Logger.LogInfo($"Successfully opened folder: {folderPath}");
                    }
                    else
                    {
                        Logger.LogWarning($"Could not open folder: {folderPath}");
                    }
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
    }
}
