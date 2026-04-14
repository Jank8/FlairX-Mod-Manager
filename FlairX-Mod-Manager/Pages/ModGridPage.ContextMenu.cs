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
            ShowBrokenItem.Text = SharedUtilities.GetTranslation(langDict, "ShowBroken");
            HideBrokenItem.Text = SharedUtilities.GetTranslation(langDict, "HideBroken");
            HideBrokenItem.IsChecked = SettingsManager.Current.HideBrokenMods;
            OpenModsFolderItem.Text = SharedUtilities.GetTranslation(langDict, "OpenModsFolder");
        }

        public void UpdateContextMenuVisibility()
        {
            // Check if we're in category mode showing all categories
            bool isInCategoryModeShowingCategories = (CurrentViewMode == ViewMode.Categories && string.IsNullOrEmpty(_currentCategory));
            
            // Check if we're in a specific category (not "All Mods", "Active", or "Broken")
            bool isInSpecificCategory = !string.IsNullOrEmpty(_currentCategory) && 
                                       _currentCategory != "Active" && 
                                       _currentCategory != "Broken" && 
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

        private void ShowBroken_Click(object sender, RoutedEventArgs e)
        {
            // Exit table view if active and clear sorting
            if (CurrentViewMode == ViewMode.Table)
            {
                _currentSortMode = SortMode.None;
                CurrentViewMode = ViewMode.Mods;
            }
            
            // Show broken mods functionality
            var langDict = SharedUtilities.LoadLanguageDictionary();
            CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_Broken_Mods");
            _currentCategory = "Broken"; // Set current category to Broken
            LoadBrokenModsOnly();
            CategoryBackButton.Visibility = Visibility.Visible;
            CategoryOpenFolderButton.Visibility = Visibility.Collapsed; // Hide folder button for Broken mods
        }

        private void HideBroken_Click(object sender, RoutedEventArgs e)
        {
            // Toggle the setting
            SettingsManager.Current.HideBrokenMods = !SettingsManager.Current.HideBrokenMods;
            SettingsManager.Save();
            
            // Update the toggle state
            HideBrokenItem.IsChecked = SettingsManager.Current.HideBrokenMods;
            
            Logger.LogInfo($"Hide Broken Mods filter toggled: {SettingsManager.Current.HideBrokenMods}");
            
            // Reload current view to apply filter
            if (!string.IsNullOrEmpty(_currentCategory))
            {
                if (_currentCategory == "Active")
                {
                    LoadActiveModsOnly();
                }
                else if (_currentCategory == "Broken")
                {
                    // Don't reload if we're in the "Show Broken" view - that would be confusing
                    return;
                }
                else if (_currentCategory == "Outdated")
                {
                    LoadOutdatedModsOnly();
                }
                else
                {
                    // Specific category
                    LoadModsByCategory(_currentCategory);
                }
            }
            else if (CurrentViewMode == ViewMode.Categories)
            {
                // In categories view - no reload needed
                return;
            }
            else
            {
                // All mods view
                LoadAllMods();
            }
        }

        private void OpenModsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get current game's mods folder path
                string modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                
                if (Directory.Exists(modsPath))
                {
                    // Open mods folder in Windows Explorer
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = modsPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    // Create folder if it doesn't exist and then open it
                    Directory.CreateDirectory(modsPath);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = modsPath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to open mods folder", ex);
            }
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
                        sortedItems = sortedItems.OrderByDescending(m => m.IsFavorite)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.NameZA:
                        sortedItems = sortedItems.OrderByDescending(m => m.IsFavorite)
                                                .ThenByDescending(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.CategoryAZ:
                        sortedItems = sortedItems.OrderByDescending(m => m.IsFavorite)
                                                .ThenBy(m => m.Category, StringComparer.OrdinalIgnoreCase)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.CategoryZA:
                        sortedItems = sortedItems.OrderByDescending(m => m.IsFavorite)
                                                .ThenByDescending(m => m.Category, StringComparer.OrdinalIgnoreCase)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.ActiveFirst:
                        sortedItems = sortedItems.OrderByDescending(m => m.IsFavorite)
                                                .ThenByDescending(m => m.IsActive)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.InactiveFirst:
                        sortedItems = sortedItems.OrderByDescending(m => m.IsFavorite)
                                                .ThenBy(m => m.IsActive)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.LastCheckedNewest:
                        sortedItems = sortedItems.OrderByDescending(m => m.IsFavorite)
                                                .ThenByDescending(m => m.LastChecked)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.LastCheckedOldest:
                        sortedItems = sortedItems.OrderByDescending(m => m.IsFavorite)
                                                .ThenBy(m => m.LastChecked)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.LastUpdatedNewest:
                        sortedItems = sortedItems.OrderByDescending(m => m.IsFavorite)
                                                .ThenByDescending(m => m.LastUpdated)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case SortMode.LastUpdatedOldest:
                        sortedItems = sortedItems.OrderByDescending(m => m.IsFavorite)
                                                .ThenBy(m => m.LastUpdated)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    default:
                        sortedItems = sortedItems.OrderByDescending(m => m.IsFavorite)
                                                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
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
                // We're showing category tiles - sort them directly using _allMods
                if (_allMods == null || _allMods.Count == 0)
                    return;

                var categoryList = _allMods.ToList();
                
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

                // Update _allMods with sorted categories
                _allMods.Clear();
                foreach (var category in categoryList)
                {
                    _allMods.Add(category);
                }
                
                UpdateEmptyState();
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
            
            // Load broken mods list once if we're in Broken view
            HashSet<string>? brokenModsList = _currentCategory == "Broken" ? ModListManager.LoadBrokenModsList() : null;
            
            foreach (var modData in _allModData)
            {
                // Apply current view filter (active mods, category, etc.)
                bool shouldInclude = true;
                
                // Check if we're in active mods view
                if (_currentCategory == "Active" && !modData.IsActive)
                    shouldInclude = false;
                
                // Check if we're in broken mods view (use persistent list for fast lookup)
                if (_currentCategory == "Broken" && brokenModsList != null && !brokenModsList.Contains(modData.Name))
                    shouldInclude = false;
                
                // Check if we're in specific category view
                if (!string.IsNullOrEmpty(_currentCategory) && _currentCategory != "Active" && _currentCategory != "Broken" && 
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
                        IsBroken = modData.IsBroken,
                        IsNSFW = modData.IsNSFW,
                        ImageSource = null // Lazy load when visible
                    };
                    modTiles.Add(modTile);
                }
            }

            // Use _allMods collection for consistency
            DispatcherQueue.TryEnqueue(() =>
            {
                _allMods.Clear();
                foreach (var mod in modTiles)
                {
                    _allMods.Add(mod);
                }
                
                UpdateEmptyState();
            });
            
            // Reload visible images after sorting
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in ApplySorting: {ex.Message}");
                Logger.LogDebug($"Stack trace: {ex.StackTrace}");
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
                    
                    // Check for Updates option (only if mod has GameBanana URL)
                    var modId = FlairX_Mod_Manager.Services.GameBananaService.ExtractModIdFromUrl(modUrl ?? "");
                    if (modId.HasValue)
                    {
                        var checkUpdatesItem = new MenuFlyoutItem
                        {
                            Text = SharedUtilities.GetTranslation(lang, "ContextMenu_CheckUpdates"),
                            Icon = new FontIcon { Glyph = "\uE895" },
                            Tag = modTile
                        };
                        checkUpdatesItem.Click += ContextMenu_CheckUpdates_Click;
                        menuFlyout.Items.Add(checkUpdatesItem);
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


        private async void ContextMenu_Move_Click(object sender, RoutedEventArgs e)
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
                        };

                        await dialog.ShowAsync();
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
                        // No change - just refresh the tile image instead of renaming
                        Logger.LogInfo($"Name unchanged for {(modTile.IsCategory ? "category" : "mod")} '{modTile.Name}' - refreshing tile image");
                        
                        // Refresh the mod tile image
                        try
                        {
                            // Find the full path to the mod
                            var modsPath = SharedUtilities.GetSafeXXMIModsPath();
                            string? fullModPath = null;
                            
                            if (modTile.IsCategory)
                            {
                                fullModPath = Path.Combine(modsPath, modTile.Directory);
                            }
                            else
                            {
                                // Find mod in categories - modTile.Directory already contains the actual folder name
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
                Logger.LogInfo($"RenameItemAsync called - modTile.Name: '{modTile.Name}', modTile.Directory: '{modTile.Directory}', newName: '{newName}'");
                
                var modsPath = SharedUtilities.GetSafeXXMIModsPath();
                string? currentPath = null;
                string? parentPath = null;
                bool isActive = false;
                
                if (modTile.IsCategory)
                {
                    // Renaming category
                    currentPath = Path.Combine(modsPath, modTile.Directory);
                    parentPath = modsPath;
                }
                else
                {
                    // Renaming mod - find it in categories
                    foreach (var categoryDir in Directory.GetDirectories(modsPath))
                    {
                        var modPath = Path.Combine(categoryDir, modTile.Directory);
                        if (Directory.Exists(modPath))
                        {
                            currentPath = modPath;
                            parentPath = categoryDir;
                            isActive = !modTile.Directory.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase);
                            break;
                        }
                    }
                }
                
                if (currentPath == null || parentPath == null)
                {
                    await ShowErrorDialog("Error", $"Could not find {(modTile.IsCategory ? "category" : "mod")} directory.");
                    return;
                }
                
                Logger.LogInfo($"Found mod at: '{currentPath}', isActive: {isActive}");
                
                // Determine new directory name (preserve active/inactive state)
                string newDirectoryName;
                if (modTile.IsCategory)
                {
                    newDirectoryName = newName;
                }
                else
                {
                    newDirectoryName = isActive ? newName : "DISABLED_" + newName;
                }
                
                var newPath = Path.Combine(parentPath, newDirectoryName);
                
                Logger.LogInfo($"Will rename: '{currentPath}' -> '{newPath}'");
                
                // Safety check - don't rename to same path
                if (string.Equals(currentPath, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInfo("Source and destination are the same - nothing to do");
                    return;
                }
                
                // Check if target already exists
                if (Directory.Exists(newPath))
                {
                    await ShowErrorDialog("Error", $"A {(modTile.IsCategory ? "category" : "mod")} with the name '{newName}' already exists.");
                    return;
                }
                
                // Also check the opposite state (active/inactive) to prevent conflicts
                if (!modTile.IsCategory)
                {
                    var oppositeDirectoryName = isActive ? "DISABLED_" + newName : newName;
                    var oppositePath = Path.Combine(parentPath, oppositeDirectoryName);
                    if (Directory.Exists(oppositePath))
                    {
                        await ShowErrorDialog("Error", $"A mod with the name '{newName}' already exists in a different state.");
                        return;
                    }
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
                
                // Update active mods file if needed
                if (!modTile.IsCategory && isActive)
                {
                    var activeModsPath = PathManager.GetActiveModsPath();
                    if (File.Exists(activeModsPath))
                    {
                        try
                        {
                            var cleanOldName = GetCleanModName(oldDirectory);
                            await Services.FileAccessQueue.ExecuteAsync(activeModsPath, async () =>
                            {
                                var json = await File.ReadAllTextAsync(activeModsPath);
                                var activeMods = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
                                
                                if (activeMods.ContainsKey(cleanOldName))
                                {
                                    activeMods.Remove(cleanOldName);
                                    activeMods[newName] = true;
                                    
                                    var newJson = JsonSerializer.Serialize(activeMods, new JsonSerializerOptions { WriteIndented = true });
                                    await File.WriteAllTextAsync(activeModsPath, newJson);
                                    Logger.LogInfo($"Updated active mods file: '{cleanOldName}' -> '{newName}'");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to update active mods file after rename", ex);
                        }
                    }
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
                await ShowErrorDialog("Error", $"Failed to rename: {ex.Message}");
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
                                SaveActiveMods();
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

        private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile)
            {
                // Create a fake button with the ModTile as Tag to reuse existing logic
                var fakeButton = new Button { Tag = modTile };
                DeleteModButton_Click(fakeButton, e);
            }
        }

        private async void ContextMenu_DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile && modTile.IsCategory)
            {
                try
                {
                    var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                    if (string.IsNullOrEmpty(modsPath))
                    {
                        modsPath = PathManager.GetModsPath();
                    }

                    var categoryPath = System.IO.Path.Combine(modsPath, modTile.Name);
                    
                    if (!Directory.Exists(categoryPath))
                    {
                        Logger.LogWarning($"Category directory not found: {categoryPath}");
                        return;
                    }

                    // Count mods in category
                    int modCount = Directory.GetDirectories(categoryPath).Length;

                    // Show confirmation dialog
                    bool confirmed = await Dialogs.CategoryDeleteDialog.ShowAsync(modTile.Name, modCount, this.XamlRoot);
                    
                    if (!confirmed)
                    {
                        return;
                    }

                    // Check if category is pinned and unpin it first
                    var gameTag = SettingsManager.CurrentSelectedGame;
                    if (!string.IsNullOrEmpty(gameTag))
                    {
                        var pinnedCategories = SettingsManager.GetPinnedCategories(gameTag);
                        if (pinnedCategories.Contains(modTile.Name))
                        {
                            SettingsManager.RemovePinnedCategory(gameTag, modTile.Name);
                            Logger.LogInfo($"Automatically unpinned category before deletion: {modTile.Name}");
                        }
                    }

                    // Delete the category directory
                    Directory.Delete(categoryPath, true);
                    Logger.LogInfo($"Deleted category: {modTile.Name} with {modCount} mods");

                    // Refresh the UI
                    var mainWindow = (Application.Current as App)?.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        // Regenerate menu
                        await mainWindow.GenerateModCharacterMenuAsync();
                        
                        // Reload current view
                        if (CurrentViewMode == ViewMode.Categories)
                        {
                            LoadCategories();
                        }
                        else
                        {
                            // If we're viewing the deleted category, go back to all mods
                            if (_currentCategory == modTile.Name)
                            {
                                LoadAllModsPublic();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to delete category: {modTile.Name}", ex);
                    await ShowErrorDialog($"Failed to delete category: {ex.Message}");
                }
            }
        }

        private async void ContextMenu_PinUnpinCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile && modTile.IsCategory)
            {
                try
                {
                    var gameTag = SettingsManager.CurrentSelectedGame;
                    if (string.IsNullOrEmpty(gameTag))
                    {
                        Logger.LogWarning("No game selected, cannot pin/unpin category");
                        return;
                    }

                    var pinnedCategories = SettingsManager.GetPinnedCategories(gameTag);
                    bool isPinned = pinnedCategories.Contains(modTile.Name);

                    if (isPinned)
                    {
                        // Unpin category
                        SettingsManager.RemovePinnedCategory(gameTag, modTile.Name);
                        Logger.LogInfo($"Unpinned category: {modTile.Name}");
                    }
                    else
                    {
                        // Pin category
                        SettingsManager.AddPinnedCategory(gameTag, modTile.Name);
                        Logger.LogInfo($"Pinned category: {modTile.Name}");
                    }

                    // Refresh the UI
                    var mainWindow = (Application.Current as App)?.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        // Regenerate menu to update pinned categories in footer
                        await mainWindow.GenerateModCharacterMenuAsync();
                    }

                    // Refresh current view if showing categories
                    if (CurrentViewMode == ViewMode.Categories)
                    {
                        LoadCategories();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to pin/unpin category: {modTile.Name}", ex);
                }
            }
        }

        private void ContextMenu_CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is ModTile modTile)
            {
                var modUrl = GetModUrl(modTile);
                if (!string.IsNullOrEmpty(modUrl))
                {
                    // Check if it's a GameBanana URL
                    if (modUrl.Contains("gamebanana.com", StringComparison.OrdinalIgnoreCase))
                    {
                        // Get game tag from current game
                        var gameTag = SettingsManager.CurrentSelectedGame;
                        if (!string.IsNullOrEmpty(gameTag))
                        {
                            // Open GameBanana browser with mod URL and mod path
                            var mainWindow = (Application.Current as App)?.MainWindow as MainWindow;
                            mainWindow?.ShowGameBananaBrowserPanel(gameTag, modUrl, modTile.Directory);
                        }
                        else
                        {
                            Logger.LogWarning("No game tag found, cannot open GameBanana browser");
                        }
                    }
                    else
                    {
                        // For non-GameBanana URLs, open in external browser
                        try
                        {
                            if (!modUrl.StartsWith("http://") && !modUrl.StartsWith("https://"))
                                modUrl = "https://" + modUrl;
                            
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = modUrl,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Failed to open URL", ex);
                        }
                    }
                }
            }
        }

        private async System.Threading.Tasks.Task ShowErrorDialog(string message)
        {
            if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(message);
        }
    }
}