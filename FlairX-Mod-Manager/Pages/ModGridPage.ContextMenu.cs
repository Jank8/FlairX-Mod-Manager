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
    /// <summary>
    /// ModGridPage partial class - Context menu system and handlers
    /// </summary>
    public sealed partial class ModGridPage : Page
    {
        // Context Menu Translation Update
        private void UpdateContextMenuTranslations()
        {
            var langDict = SharedUtilities.LoadLanguageDictionary();
            
            SortByNameSubItem.Text = SharedUtilities.GetTranslation(langDict, "SortByName");
            SortByNameAZItem.Text = SharedUtilities.GetTranslation(langDict, "SortAZ");
            SortByNameZAItem.Text = SharedUtilities.GetTranslation(langDict, "SortZA");
            
            SortByCategorySubItem.Text = SharedUtilities.GetTranslation(langDict, "SortByCategory");
            SortByCategoryAZItem.Text = SharedUtilities.GetTranslation(langDict, "SortAZ");
            SortByCategoryZAItem.Text = SharedUtilities.GetTranslation(langDict, "SortZA");
            
            SortByLastCheckedSubItem.Text = SharedUtilities.GetTranslation(langDict, "SortByLastChecked");
            SortByLastCheckedNewestItem.Text = SharedUtilities.GetTranslation(langDict, "SortNewest");
            SortByLastCheckedOldestItem.Text = SharedUtilities.GetTranslation(langDict, "SortOldest");
            
            SortByLastUpdatedSubItem.Text = SharedUtilities.GetTranslation(langDict, "SortByLastUpdated");
            SortByLastUpdatedNewestItem.Text = SharedUtilities.GetTranslation(langDict, "SortNewest");
            SortByLastUpdatedOldestItem.Text = SharedUtilities.GetTranslation(langDict, "SortOldest");
            ShowOutdatedItem.Text = SharedUtilities.GetTranslation(langDict, "ShowOutdated");
            ShowActiveItem.Text = SharedUtilities.GetTranslation(langDict, "ShowActive");
        }

        public void UpdateContextMenuVisibility()
        {
            // Check if we're in category mode showing all categories
            bool isInCategoryModeShowingCategories = (CurrentViewMode == ViewMode.Categories && string.IsNullOrEmpty(_currentCategory));
            
            // Check if we're in a specific category (not "All Mods" or "Active")
            bool isInSpecificCategory = !string.IsNullOrEmpty(_currentCategory) && 
                                       _currentCategory != "Active" && 
                                       !_currentCategory.Equals("All Mods", StringComparison.OrdinalIgnoreCase);
            
            if (isInCategoryModeShowingCategories)
            {
                // In category mode showing all categories: hide everything except name sorting and show active
                SortByCategorySubItem.Visibility = Visibility.Collapsed;
                SortByLastCheckedSubItem.Visibility = Visibility.Collapsed;
                SortByLastUpdatedSubItem.Visibility = Visibility.Collapsed;
                SortByNameSubItem.Visibility = Visibility.Visible;
                ShowOutdatedItem.Visibility = Visibility.Visible;
                ShowActiveItem.Visibility = Visibility.Visible;
            }
            else if (isInSpecificCategory)
            {
                // In specific category: hide category sorting but show dates
                SortByCategorySubItem.Visibility = Visibility.Collapsed;
                SortByLastCheckedSubItem.Visibility = Visibility.Visible;
                SortByLastUpdatedSubItem.Visibility = Visibility.Visible;
                SortByNameSubItem.Visibility = Visibility.Visible;
                ShowOutdatedItem.Visibility = Visibility.Visible;
                ShowActiveItem.Visibility = Visibility.Visible;
            }
            else
            {
                // In all mods view: show everything
                SortByCategorySubItem.Visibility = Visibility.Visible;
                SortByLastCheckedSubItem.Visibility = Visibility.Visible;
                SortByLastUpdatedSubItem.Visibility = Visibility.Visible;
                SortByNameSubItem.Visibility = Visibility.Visible;
                ShowOutdatedItem.Visibility = Visibility.Visible;
                ShowActiveItem.Visibility = Visibility.Visible;
            }
        }
        
        private void ShowOutdated_Click(object sender, RoutedEventArgs e)
        {
            // Exit table view if active and clear sorting
            if (CurrentViewMode == ViewMode.Table)
            {
                _currentSortMode = SortMode.None;
                CurrentViewMode = ViewMode.Mods;
            }
            
            var langDict = SharedUtilities.LoadLanguageDictionary();
            CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_Outdated_Mods");
            _currentCategory = "Outdated";
            LoadOutdatedModsOnly();
            CategoryBackButton.Visibility = Visibility.Visible;
            CategoryOpenFolderButton.Visibility = Visibility.Collapsed;
        }
        
        private void ShowActive_Click(object sender, RoutedEventArgs e)
        {
            // Exit table view if active and clear sorting
            if (CurrentViewMode == ViewMode.Table)
            {
                _currentSortMode = SortMode.None;
                CurrentViewMode = ViewMode.Mods;
            }
            
            // Use existing show active functionality
            var langDict = SharedUtilities.LoadLanguageDictionary();
            CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_Active_Mods");
            _currentCategory = "Active"; // Set current category to Active
            LoadActiveModsOnly();
            CategoryBackButton.Visibility = Visibility.Visible;
            CategoryOpenFolderButton.Visibility = Visibility.Collapsed; // Hide folder button for Active mods
        }



        // Sorting event handlers - automatically enable table view
        private void SortByNameAZ_Click(object sender, RoutedEventArgs e)
        {
            _currentSortMode = SortMode.NameAZ;
            SwitchToTableView();
            ApplySorting();
        }

        private void SortByNameZA_Click(object sender, RoutedEventArgs e)
        {
            _currentSortMode = SortMode.NameZA;
            SwitchToTableView();
            ApplySorting();
        }

        private void SortByCategoryAZ_Click(object sender, RoutedEventArgs e)
        {
            _currentSortMode = SortMode.CategoryAZ;
            SwitchToTableView();
            ApplySorting();
        }

        private void SortByCategoryZA_Click(object sender, RoutedEventArgs e)
        {
            _currentSortMode = SortMode.CategoryZA;
            SwitchToTableView();
            ApplySorting();
        }

        private void SortByLastCheckedNewest_Click(object sender, RoutedEventArgs e)
        {
            _currentSortMode = SortMode.LastCheckedNewest;
            SwitchToTableView();
            ApplySorting();
        }

        private void SortByLastCheckedOldest_Click(object sender, RoutedEventArgs e)
        {
            _currentSortMode = SortMode.LastCheckedOldest;
            SwitchToTableView();
            ApplySorting();
        }

        private void SortByLastUpdatedNewest_Click(object sender, RoutedEventArgs e)
        {
            _currentSortMode = SortMode.LastUpdatedNewest;
            SwitchToTableView();
            ApplySorting();
        }

        private void SortByLastUpdatedOldest_Click(object sender, RoutedEventArgs e)
        {
            _currentSortMode = SortMode.LastUpdatedOldest;
            SwitchToTableView();
            ApplySorting();
        }

        private void ApplySorting()
        {
            try
            {
                // Only apply sorting if we're in table view
                if (CurrentViewMode != ViewMode.Table)
                    return;
                    
                // Handle table view sorting
                if (CurrentViewMode == ViewMode.Table)
                {
                    // Safety check for table list
                    if (ModsTableList?.ItemsSource is not IEnumerable<ModTile> tableItems)
                        return;

                var sortedItems = tableItems.ToList();
                
                switch (_currentSortMode)
                {
                    case SortMode.NameAZ:
                        sortedItems = sortedItems.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.NameZA:
                        sortedItems = sortedItems.OrderByDescending(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.CategoryAZ:
                        sortedItems = sortedItems.OrderBy(m => m.Category, StringComparer.OrdinalIgnoreCase)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.CategoryZA:
                        sortedItems = sortedItems.OrderByDescending(m => m.Category, StringComparer.OrdinalIgnoreCase)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.ActiveFirst:
                        sortedItems = sortedItems.OrderByDescending(m => m.IsActive)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.InactiveFirst:
                        sortedItems = sortedItems.OrderBy(m => m.IsActive)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.LastCheckedNewest:
                        sortedItems = sortedItems.OrderByDescending(m => m.LastChecked)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.LastCheckedOldest:
                        sortedItems = sortedItems.OrderBy(m => m.LastChecked)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.LastUpdatedNewest:
                        sortedItems = sortedItems.OrderByDescending(m => m.LastUpdated)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.LastUpdatedOldest:
                        sortedItems = sortedItems.OrderBy(m => m.LastUpdated)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    default:
                        sortedItems = sortedItems.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                }

                // Update original table items with sorted results
                _originalTableItems.Clear();
                foreach (var item in sortedItems)
                {
                    _originalTableItems.Add(item);
                }
                
                // Set table to use the observable collection
                ModsTableList.ItemsSource = _originalTableItems;
                
                // Update search results if search is active
                if (!string.IsNullOrWhiteSpace(_currentTableSearchQuery) && _currentTableSearchQuery.Length >= 3)
                {
                    FilterTableResults();
                }
                
                return;
            }
            
            // Check if we're currently showing categories
            if (CurrentViewMode == ViewMode.Categories && string.IsNullOrEmpty(_currentCategory))
            {
                // We're showing category tiles - sort them directly
                if (ModsGrid.ItemsSource is not IEnumerable<ModTile> currentCategories)
                    return;

                var categoryList = currentCategories.ToList();
                
                switch (_currentSortMode)
                {
                    case SortMode.NameAZ:
                        categoryList = categoryList.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.NameZA:
                        categoryList = categoryList.OrderByDescending(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    default:
                        categoryList = categoryList.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                }

                ModsGrid.ItemsSource = categoryList;
                return;
            }

            // Original logic for mods
            if (_allModData == null || _allModData.Count == 0)
                return;

            // Sort the entire dataset (_allModData) first
            List<ModData> sortedModData;
            
            switch (_currentSortMode)
            {
                case SortMode.NameAZ:
                    sortedModData = _allModData.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case SortMode.NameZA:
                    sortedModData = _allModData.OrderByDescending(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case SortMode.CategoryAZ:
                    sortedModData = _allModData.OrderBy(m => m.Category, StringComparer.OrdinalIgnoreCase)
                                              .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case SortMode.CategoryZA:
                    sortedModData = _allModData.OrderByDescending(m => m.Category, StringComparer.OrdinalIgnoreCase)
                                              .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case SortMode.LastCheckedNewest:
                    sortedModData = _allModData.OrderByDescending(m => m.LastChecked)
                                              .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case SortMode.LastCheckedOldest:
                    sortedModData = _allModData.OrderBy(m => m.LastChecked)
                                              .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case SortMode.LastUpdatedNewest:
                    sortedModData = _allModData.OrderByDescending(m => m.LastUpdated)
                                              .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case SortMode.LastUpdatedOldest:
                    sortedModData = _allModData.OrderBy(m => m.LastUpdated)
                                              .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                default:
                    sortedModData = _allModData.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    break;
            }

            // Update _allModData with sorted order
            _allModData = sortedModData;

            // Create ModTiles from sorted data and apply current filter (if any)
            var modTiles = new List<ModTile>();
            foreach (var modData in _allModData)
            {
                // Apply current view filter (active mods, category, etc.)
                bool shouldInclude = true;
                
                // Check if we're in active mods view
                if (_currentCategory == "Active" && !modData.IsActive)
                    shouldInclude = false;
                
                // Check if we're in specific category view
                if (!string.IsNullOrEmpty(_currentCategory) && _currentCategory != "Active" && 
                    !string.Equals(modData.Category, _currentCategory, StringComparison.OrdinalIgnoreCase))
                    shouldInclude = false;

                if (shouldInclude)
                {
                    var modTile = new ModTile 
                    { 
                        Name = modData.Name, 
                        ImagePath = modData.ImagePath, 
                        Directory = modData.Directory, 
                        IsActive = modData.IsActive,
                        Category = modData.Category,
                        Author = modData.Author,
                        Url = modData.Url,
                        LastChecked = modData.LastChecked,
                        LastUpdated = modData.LastUpdated,
                        HasUpdate = CheckForUpdateLive(modData.Directory), // Live check without cache
                        IsVisible = true,
                        ImageSource = null // Lazy load when visible
                    };
                    modTiles.Add(modTile);
                }
            }

            ModsGrid.ItemsSource = modTiles;
            
            // Reload visible images after sorting
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ApplySorting: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        // Dynamic Context Menu
        private void ContextMenu_Opening(object sender, object e)
        {
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
                    string modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                    if (string.IsNullOrEmpty(modLibraryPath))
                    {
                        modLibraryPath = PathManager.GetModLibraryPath();
                    }
                    
                    string? fullModDir = FindModFolderPath(modLibraryPath, modTile.Directory);
                    if (string.IsNullOrEmpty(fullModDir) || !Directory.Exists(fullModDir))
                    {
                        // Mod directory not found - remove tile and cancel context menu
                        System.Diagnostics.Debug.WriteLine($"Mod directory '{modTile.Directory}' not found during context menu, removing tile...");
                        
                        // Remove from UI collection
                        _allMods.Remove(modTile);
                        
                        // Also remove from active mods if it exists there
                        if (_activeMods.ContainsKey(modTile.Directory))
                        {
                            _activeMods.Remove(modTile.Directory);
                            SaveActiveMods();
                        }
                        
                        // Cancel context menu opening
                        return;
                    }
                }
                
                menuFlyout.Items.Clear();
                var lang = SharedUtilities.LoadLanguageDictionary();
                
                if (modTile.IsCategory)
                {
                    // Category context menu: Open Folder, Copy Name, Rename
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
                    
                    menuFlyout.Items.Add(new MenuFlyoutSeparator());
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_CopyName"),
                        Icon = new SymbolIcon(Symbol.Copy),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[5]).Click += ContextMenu_CopyName_Click;
                    
                    menuFlyout.Items.Add(new MenuFlyoutItem
                    {
                        Text = SharedUtilities.GetTranslation(lang, "ContextMenu_Rename"),
                        Icon = new SymbolIcon(Symbol.Rename),
                        Tag = modTile
                    });
                    ((MenuFlyoutItem)menuFlyout.Items[6]).Click += ContextMenu_Rename_Click;
                    
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

        private void ContextMenu_OpenUrl_Click(object sender, RoutedEventArgs e)
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
                        
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error opening URL: {ex.Message}");
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

        private async void ContextMenu_Rename_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile)
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                
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
                        return; // No change
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
        
        private async Task RenameItemAsync(ModTile modTile, string newName)
        {
            try
            {
                var modLibraryPath = SharedUtilities.GetSafeModLibraryPath();
                string? currentPath = null;
                string? parentPath = null;
                
                if (modTile.IsCategory)
                {
                    // Renaming category
                    currentPath = Path.Combine(modLibraryPath, modTile.Directory);
                    parentPath = modLibraryPath;
                }
                else
                {
                    // Renaming mod - find it in categories
                    foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
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
                    await ShowErrorDialog("Error", $"Could not find {(modTile.IsCategory ? "category" : "mod")} directory.");
                    return;
                }
                
                var newPath = Path.Combine(parentPath, newName);
                
                if (Directory.Exists(newPath))
                {
                    await ShowErrorDialog("Error", $"A {(modTile.IsCategory ? "category" : "mod")} with the name '{newName}' already exists.");
                    return;
                }
                
                // Rename the directory
                Directory.Move(currentPath, newPath);
                
                // Update the ModTile object (this will trigger PropertyChanged events)
                var oldName = modTile.Directory;
                modTile.Name = newName;
                modTile.Directory = newName;
                
                // For mods: Update active mods file if this mod was active
                if (!modTile.IsCategory && modTile.IsActive)
                {
                    var activeModsPath = PathManager.GetActiveModsPath();
                    if (File.Exists(activeModsPath))
                    {
                        var json = File.ReadAllText(activeModsPath);
                        var activeMods = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
                        
                        if (activeMods.ContainsKey(oldName))
                        {
                            activeMods.Remove(oldName);
                            activeMods[newName] = true;
                            
                            var newJson = JsonSerializer.Serialize(activeMods, new JsonSerializerOptions { WriteIndented = true });
                            File.WriteAllText(activeModsPath, newJson);
                        }
                    }
                    
                    // Recreate symlinks
                    RecreateSymlinksFromActiveMods();
                }
                
                Logger.LogInfo($"Successfully renamed {(modTile.IsCategory ? "category" : "mod")} from '{oldName}' to '{newName}'");
                
                // Save current scroll position
                double currentScrollPosition = 0;
                if (ModsScrollViewer != null)
                {
                    currentScrollPosition = ModsScrollViewer.VerticalOffset;
                }
                
                // Refresh the UI to show updated name - handle both grid and table views
                if (CurrentViewMode == ViewMode.Table)
                {
                    // Update table view: the ModTile object has already been updated above
                    // and since it implements INotifyPropertyChanged, the table should update automatically
                    // We just need to update the _originalTableItems for search functionality
                    var originalItem = _originalTableItems.FirstOrDefault(x => x.Directory == newName);
                    if (originalItem != null && originalItem != modTile)
                    {
                        originalItem.Name = newName;
                        originalItem.Directory = newName;
                    }
                }
                else
                {
                    // Update grid view
                    if (ModsGrid?.ItemsSource is System.Collections.ObjectModel.ObservableCollection<ModTile> collection)
                    {
                        // Find the item in the collection and trigger update
                        var item = collection.FirstOrDefault(x => x.Directory == newName);
                        if (item != null)
                        {
                            // Force UI refresh by temporarily removing and re-adding the item
                            var index = collection.IndexOf(item);
                            collection.RemoveAt(index);
                            collection.Insert(index, item);
                        }
                    }
                }
                
                // Restore scroll position after a short delay
                if (ModsScrollViewer != null && currentScrollPosition > 0)
                {
                    await Task.Delay(100); // Small delay to let UI update
                    ModsScrollViewer.ScrollToVerticalOffset(currentScrollPosition);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to rename {(modTile.IsCategory ? "category" : "mod")} '{modTile.Name}' to '{newName}'", ex);
                await ShowErrorDialog("Error", $"Failed to rename: {ex.Message}");
            }
        }
        
        private async Task ShowErrorDialog(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private string? GetModUrl(ModTile modTile)
        {
            try
            {
                if (modTile.IsCategory) return null;
                
                var modLibraryDir = SharedUtilities.GetSafeModLibraryPath();
                var modFolderPath = FindModFolderPath(modLibraryDir, modTile.Directory);
                
                if (string.IsNullOrEmpty(modFolderPath)) return null;
                
                var modJsonPath = Path.Combine(modFolderPath, "mod.json");
                if (!File.Exists(modJsonPath)) return null;
                
                var json = File.ReadAllText(modJsonPath);
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
                        string modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                        if (string.IsNullOrEmpty(modLibraryPath))
                        {
                            modLibraryPath = PathManager.GetModLibraryPath();
                        }
                        
                        string? fullModDir = FindModFolderPath(modLibraryPath, tile.Directory);
                        if (string.IsNullOrEmpty(fullModDir) || !Directory.Exists(fullModDir))
                        {
                            // Mod directory not found - remove tile dynamically
                            System.Diagnostics.Debug.WriteLine($"Mod directory '{tile.Directory}' not found, removing tile...");
                            
                            // Remove from UI collection
                            _allMods.Remove(tile);
                            
                            // Also remove from active mods if it exists there
                            if (_activeMods.ContainsKey(tile.Directory))
                            {
                                _activeMods.Remove(tile.Directory);
                                SaveActiveMods();
                            }
                            
                            return;
                        }
                        
                        // Create UserControl for mod details
                        var modDetailControl = new ModDetailUserControl();
                        modDetailControl.LoadModDetails(tile.Directory, _currentCategory ?? "", 
                            CurrentViewMode == ViewMode.Categories ? "Categories" : "Mods");

                        // Get current app theme and create appropriate background
                        string appTheme = FlairX_Mod_Manager.SettingsManager.Current.Theme ?? "Auto";
                        bool isDarkTheme = false;
                        
                        if (appTheme == "Dark")
                            isDarkTheme = true;
                        else if (appTheme == "Light")
                            isDarkTheme = false;
                        else if (this.XamlRoot.Content is FrameworkElement rootElement)
                            isDarkTheme = rootElement.ActualTheme == ElementTheme.Dark;

                        // Create transparent overlay (no background)
                        var overlay = new Grid
                        {
                            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch
                        };

                        // No size restrictions - UserControl will auto-size
                        
                        // Create dialog with acrylic background for blur effect
                        Microsoft.UI.Xaml.Media.AcrylicBrush dialogAcrylicBrush;
                        Brush borderBrush;
                        
                        if (isDarkTheme)
                        {
                            dialogAcrylicBrush = new Microsoft.UI.Xaml.Media.AcrylicBrush
                            {
                                TintColor = Microsoft.UI.ColorHelper.FromArgb(255, 32, 32, 32),
                                TintOpacity = 0.85,
                                FallbackColor = Microsoft.UI.ColorHelper.FromArgb(255, 32, 32, 32)
                            };
                            borderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(80, 255, 255, 255));
                        }
                        else
                        {
                            dialogAcrylicBrush = new Microsoft.UI.Xaml.Media.AcrylicBrush
                            {
                                TintColor = Microsoft.UI.ColorHelper.FromArgb(255, 248, 248, 248),
                                TintOpacity = 0.85,
                                FallbackColor = Microsoft.UI.ColorHelper.FromArgb(255, 248, 248, 248)
                            };
                            borderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(80, 0, 0, 0));
                        }

                        // Create panel sliding from right - full height, width to menu edge
                        var dialogContainer = new Border
                        {
                            Background = dialogAcrylicBrush,
                            CornerRadius = new CornerRadius(12, 0, 0, 0), // Rounded only on top-left
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Stretch,
                            Margin = new Thickness(320, 0, 0, 0), // Start after menu (320px menu width)
                            BorderBrush = borderBrush,
                            BorderThickness = new Thickness(1, 0, 0, 0) // Only left border
                        };

                        // Create main grid for content - fill entire container
                        var mainGrid = new Grid
                        {
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch
                        };
                        
                        // No close button - will be handled by back button in ModDetailUserControl

                        // Set UserControl to fill available space completely
                        modDetailControl.HorizontalAlignment = HorizontalAlignment.Stretch;
                        modDetailControl.VerticalAlignment = VerticalAlignment.Stretch;
                        modDetailControl.Margin = new Thickness(12); // Smaller margins for more space
                        // No RequestedTheme - let it inherit naturally
                        
                        // Remove size restrictions to allow full height usage
                        modDetailControl.Width = double.NaN; // Auto width
                        modDetailControl.Height = double.NaN; // Auto height - fill container

                        mainGrid.Children.Add(modDetailControl);
                        
                        dialogContainer.Child = mainGrid;
                        overlay.Children.Add(dialogContainer);

                        // Add slide-in animation from right
                        var slideTransform = new Microsoft.UI.Xaml.Media.TranslateTransform();
                        dialogContainer.RenderTransform = slideTransform;
                        
                        // Start off-screen to the right
                        slideTransform.X = 800;
                        
                        // Animate sliding in
                        var slideAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                        {
                            From = 800,
                            To = 0,
                            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                        };
                        
                        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideAnimation, slideTransform);
                        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideAnimation, "X");
                        
                        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                        storyboard.Children.Add(slideAnimation);
                        storyboard.Begin();

                        // Add overlay to parent grid for fullscreen effect
                        Grid? parentGrid = null;
                        
                        // Try to find the Frame's parent grid (fullscreen but not affecting menu)
                        var current = this.Parent;
                        while (current != null && !(current is Grid))
                        {
                            current = (current as FrameworkElement)?.Parent;
                        }
                        parentGrid = current as Grid;

                        if (parentGrid != null)
                        {
                            parentGrid.Children.Add(overlay);

                            // Function to close with slide-out animation
                            Action closeWithAnimation = () =>
                            {
                                // Create slide-out animation
                                var slideOutAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                                {
                                    From = 0,
                                    To = dialogContainer.ActualWidth > 0 ? dialogContainer.ActualWidth : 800,
                                    Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                                    EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn }
                                };
                                
                                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideOutAnimation, slideTransform);
                                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideOutAnimation, "X");
                                
                                var slideOutStoryboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                                slideOutStoryboard.Children.Add(slideOutAnimation);
                                
                                // Remove overlay when animation completes
                                slideOutStoryboard.Completed += (s, e) => parentGrid.Children.Remove(overlay);
                                slideOutStoryboard.Begin();
                            };

                            // Back button handler from UserControl
                            modDetailControl.CloseRequested += (s, args) => closeWithAnimation();
                            
                            // Click outside to close
                            overlay.Tapped += (s, args) =>
                            {
                                if (ReferenceEquals(args.OriginalSource, overlay))
                                    closeWithAnimation();
                            };

                            // Escape key handler
                            overlay.KeyDown += (s, args) =>
                            {
                                if (args.Key == Windows.System.VirtualKey.Escape)
                                {
                                    closeWithAnimation();
                                    args.Handled = true;
                                }
                            };

                            // Make overlay focusable to receive key events
                            overlay.IsTabStop = true;
                            overlay.Focus(FocusState.Programmatic);
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

        private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile)
            {
                // Create a fake button with the ModTile as Tag to reuse existing logic
                var fakeButton = new Button { Tag = modTile };
                DeleteModButton_Click(fakeButton, e);
            }
        }
    }
}