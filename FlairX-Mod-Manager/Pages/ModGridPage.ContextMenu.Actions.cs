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

        // Dynamic Context Menu
        private void ContextMenu_Opening(object sender, object e)
        {
            // Suppress context menu when Left Shift is held (used for force-activate shortcut)
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.LeftShift)
                & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
            {
                (sender as MenuFlyout)?.Hide();
                return;
            }

            if (sender is MenuFlyout menuFlyout)
            {
                ModTile? modTile = null;
                
                // Handle both Border (old) and Button (new) targets
                if (menuFlyout.Target is Border border && border.DataContext is ModTile borderTile)
                {
                    modTile = borderTile;
                }
                else if (menuFlyout.Target is Button button && button.DataContext is ModTile buttonTile)
                {
                    modTile = buttonTile;
                }
                
                if (modTile == null) return;
                // Check if mod directory exists before showing context menu (skip for categories)
                if (!modTile.IsCategory)
                {
                    string modsPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                    if (string.IsNullOrEmpty(modsPath))
                    {
                        modsPath = PathManager.GetModsPath();
                    }
                    
                    string? fullModDir = FindModFolderPath(modsPath, modTile.Directory);
                    if (string.IsNullOrEmpty(fullModDir) || !Directory.Exists(fullModDir))
                    {
                        // Mod directory not found - remove tile and cancel context menu
                        Logger.LogWarning($"Mod directory '{modTile.Directory}' not found during context menu, removing tile...");
                        
                        // Remove from UI collection
                        _allMods.Remove(modTile);
                        
                        // Also remove from active mods if it exists there
                        var cleanName = GetCleanModName(modTile.Directory);
                        if (_activeMods.ContainsKey(cleanName))
                        {
                            _activeMods.Remove(cleanName);
                        }
                        
                        // Cancel context menu opening
                        return;
                    }
                }
                
                menuFlyout.Items.Clear();
                var lang = SharedUtilities.LoadLanguageDictionary();
                
                if (modTile.IsCategory)
                {
                    // Category context menu: Open Folder, Copy Name, Rename, Pin/Unpin, Delete
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_OpenFolder"),
                        Icon = new SymbolIcon(Symbol.Folder),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[0]).Click += ContextMenu_OpenFolder_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutSeparator());
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_CopyName"),
                        Icon = new SymbolIcon(Symbol.Copy),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[2]).Click += ContextMenu_CopyName_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_Rename"),
                        Icon = new SymbolIcon(Symbol.Rename),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[3]).Click += ContextMenu_Rename_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutSeparator());
                    
                    // Check if category is pinned
                    var gameTag = SettingsManager.CurrentSelectedGame;
                    var pinnedCategories = !string.IsNullOrEmpty(gameTag) 
                        ? SettingsManager.GetPinnedCategories(gameTag) 
                        : new System.Collections.Generic.List<string>();
                    
                    bool isPinned = pinnedCategories.Contains(modTile.Name);
                    
                    // Pin/Unpin option
                    var pinItem = new MenuFlyoutItem
                    {
                        Text = isPinned 
                            ? SharedUtilities.GetTranslation(lang, "ContextMenu_UnpinCategory") 
                            : SharedUtilities.GetTranslation(lang, "ContextMenu_PinCategory"),
                        Icon = new FontIcon { Glyph = isPinned ? "\uE77A" : "\uE718" }, // Unpin : Pin
                        Tag = modTile
                    };
                    pinItem.Click += ContextMenu_PinUnpinCategory_Click;
                    menuFlyout.Items.Add(pinItem);
                    
                    // Regenerate Thumbnails option
                    var regenerateThumbnailsItem = new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_RegenerateThumbnails"),
                        Icon = new FontIcon { Glyph = "\uE91B" }, // Refresh icon
                        Tag = modTile
                    };
                    regenerateThumbnailsItem.Click += ContextMenu_RegenerateThumbnails_Click;
                    menuFlyout.Items.Add(regenerateThumbnailsItem);
                    
                    // Delete option (always show, will auto-unpin if needed)
                    menuFlyout.Items.Add(new MenuFlyoutSeparator());
                    
                    var deleteCategoryItem = new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_DeleteCategory"),
                        Icon = new SymbolIcon(Symbol.Delete),
                        Tag = modTile
                    };
                    deleteCategoryItem.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                    deleteCategoryItem.Click += ContextMenu_DeleteCategory_Click;
                    menuFlyout.Items.Add(deleteCategoryItem);
                }
                else
                {
                    // Mod context menu: Dynamic Activate/Deactivate, Open Folder, View Details, Copy Name, Rename, Delete
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = modTile.IsActive ? SharedUtilities.GetTranslation(lang, "ContextMenu_Deactivate") : SharedUtilities.GetTranslation(lang, "ContextMenu_Activate"),
                        Icon = new SymbolIcon(modTile.IsActive ? Symbol.Remove : Symbol.Accept),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[0]).Click += ContextMenu_ActivateDeactivate_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_OpenFolder"),
                        Icon = new SymbolIcon(Symbol.Folder),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[1]).Click += ContextMenu_OpenFolder_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_ViewDetails"),
                        Icon = new SymbolIcon(Symbol.View),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[2]).Click += ContextMenu_ViewDetails_Click;
                    
                    var modUrl = GetModUrl(modTile);
                    var openUrlItem = new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_OpenURL"),
                        Icon = new SymbolIcon(Symbol.Globe),
                        Tag = modTile,
                        IsEnabled = !string.IsNullOrEmpty(modUrl)
                    };
                    openUrlItem.Click += ContextMenu_OpenUrl_Click;
                    menuFlyout.Items.Add(openUrlItem);
                    
                    // Quick Update option (only if mod has GameBanana URL)
                    var modId = FlairX_Mod_Manager.Services.GameBananaService.ExtractModIdFromUrl(modUrl ?? "");
                    if (modId.HasValue)
                    {
                        var quickUpdateItem = new MenuFlyoutItem
                        {
                            Text = SharedUtilities.GetTranslation(lang, "ContextMenu_QuickUpdate"),
                            Icon = new FontIcon { Glyph = "\uE895" },
                            Tag = modTile
                        };
                        quickUpdateItem.Click += ContextMenu_CheckUpdates_Click;
                        menuFlyout.Items.Add(quickUpdateItem);
                    }
                    
                    menuFlyout.Items.Add(new MenuFlyoutSeparator());
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_CopyName"),
                        Icon = new SymbolIcon(Symbol.Copy),
                        Tag = modTile
                    });
                    var copyNameIndex = menuFlyout.Items.Count - 1;
                    ((MenuFlyoutItem)menuFlyout.Items[copyNameIndex]).Click += ContextMenu_CopyName_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_Rename"),
                        Icon = new SymbolIcon(Symbol.Rename),
                        Tag = modTile
                    });
                    var renameIndex = menuFlyout.Items.Count - 1;
                    ((MenuFlyoutItem)menuFlyout.Items[renameIndex]).Click += ContextMenu_Rename_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_Move"),
                        Icon = new FontIcon { Glyph = "\uE8DE" }, // MoveToFolder icon
                        Tag = modTile
                    });
                    var moveIndex = menuFlyout.Items.Count - 1;
                    ((MenuFlyoutItem)menuFlyout.Items[moveIndex]).Click += ContextMenu_Move_Click;
                    
                    // Regenerate Minitile option
                    var regenerateMinitileItem = new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_RegenerateMinitile"),
                        Icon = new FontIcon { Glyph = "\uE91B" }, // Refresh icon
                        Tag = modTile
                    };
                    regenerateMinitileItem.Click += ContextMenu_RegenerateMinitile_Click;
                    menuFlyout.Items.Add(regenerateMinitileItem);
                    
                    menuFlyout.Items.Add(new MenuFlyoutSeparator());
                    
                    var deleteItem = new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_Delete"),
                        Icon = new SymbolIcon(Symbol.Delete),
                        Tag = modTile
                    };
                    deleteItem.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                    deleteItem.Click += ContextMenu_Delete_Click;
                    menuFlyout.Items.Add(deleteItem);
                }
            }
        }

        // Context Menu Event Handlers
        private void ContextMenu_ActivateDeactivate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile)
            {
                // Create a fake button with the ModTile as Tag to reuse existing logic
                var fakeButton = new Button { Tag = modTile };
                ModActiveButton_Click(fakeButton, e);
            }
        }

        private void ContextMenu_OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile)
            {
                // Create a fake button with the ModTile as Tag to reuse existing logic
                var fakeButton = new Button { Tag = modTile };
                OpenModFolderButton_Click(fakeButton, e);
            }
        }

        private void ContextMenu_ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile)
            {
                // Create a fake button with the ModTile as Tag to reuse existing logic
                var fakeButton = new Button { Tag = modTile };
                ModName_Click(fakeButton, e);
            }
        }

        private async void ContextMenu_OpenUrl_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile)
            {
                var url = GetModUrl(modTile);
                if (!string.IsNullOrEmpty(url))
                {
                    try
                    {
                        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                            url = "https://" + url;

                        // Prefer the built-in mini-browser; it shares the GameBanana session
                        // and avoids the broken external-browser handoff.
                        if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                        {
                            await mainWindow.ShowBrowserPanelAsync(url, modTile.Name);
                            return;
                        }

                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error opening URL: {ex.Message}");
                    }
                }
            }
        }

        private void ContextMenu_CopyName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile)
            {
                try
                {
                    var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    dataPackage.SetText(modTile.Name);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                    Logger.LogInfo($"Copied mod name to clipboard: {modTile.Name}");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to copy mod name to clipboard", ex);
                }
            }
        }
        
        private async Task ShowErrorDialog(string title, string message)
        {
            if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(message);
        }

        private string? GetModUrl(ModTile modTile)
        {
            try
            {
                if (modTile.IsCategory) return null;
                
                var modLibraryDir = SharedUtilities.GetSafeXXMIModsPath();
                var modFolderPath = FindModFolderPath(modLibraryDir, modTile.Directory);
                
                if (string.IsNullOrEmpty(modFolderPath)) return null;
                
                var modJsonPath = Path.Combine(modFolderPath, "mod.json");
                if (!File.Exists(modJsonPath)) return null;
                
                var json = Services.FileAccessQueue.ReadAllText(modJsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("url", out var urlProp))
                {
                    return urlProp.GetString();
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        private void ModName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile tile)
            {
                if (tile.IsCategory)
                {
                    // Handle category click - USE DUAL NAVIGATION SYSTEM
                    if (CurrentViewMode == ViewMode.Categories)
                    {
                        // CATEGORY MODE: Enter the specific category and show mods within it (same as default mode)
                        _currentCategory = tile.Directory;
                        CategoryTitle.Text = tile.Directory;
                        LoadModsByCategory(tile.Directory);
                        CategoryBackButton.Visibility = Visibility.Visible;
                        CategoryOpenFolderButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        // DEFAULT MODE: Navigate to specific category mods
                        _currentCategory = tile.Directory;
                        CategoryTitle.Text = tile.Directory;
                        LoadModsByCategory(tile.Directory);
                        CategoryBackButton.Visibility = Visibility.Visible;
                        CategoryOpenFolderButton.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    // Validate mod directory name for security
                    if (!SecurityValidator.IsValidModDirectoryName(tile.Directory))
                        return;

                    try
                    {
                        // Check if mod directory exists before opening detail page
                        string modsPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                        if (string.IsNullOrEmpty(modsPath))
                        {
                            modsPath = PathManager.GetModsPath();
                        }
                        
                        string? fullModDir = FindModFolderPath(modsPath, tile.Directory);
                        if (string.IsNullOrEmpty(fullModDir) || !Directory.Exists(fullModDir))
                        {
                            // Mod directory not found - remove tile dynamically
                            Logger.LogWarning($"Mod directory '{tile.Directory}' not found, removing tile...");
                            
                            // Remove from UI collection
                            _allMods.Remove(tile);
                            
                            // Also remove from active mods if it exists there
                            var cleanName = GetCleanModName(tile.Directory);
                            if (_activeMods.ContainsKey(cleanName))
                            {
                                _activeMods.Remove(cleanName);
                            }
                            
                            return;
                        }
                        
                        // Use the new sliding panel implementation from MainWindow
                        if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                        {
                            mainWindow.ShowModDetailPanel(tile.Directory);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to show mod details for {tile.Name}", ex);
                    }
                }
            }
        }

        private Grid? FindParentGrid(FrameworkElement element)
        {
            if (element is Grid grid)
                return grid;
                
            if (element.Parent is FrameworkElement parent)
                return FindParentGrid(parent);
                
            return null;
        }

        private async System.Threading.Tasks.Task ShowErrorDialog(string message)
        {
            if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(message);
        }
    }
}
