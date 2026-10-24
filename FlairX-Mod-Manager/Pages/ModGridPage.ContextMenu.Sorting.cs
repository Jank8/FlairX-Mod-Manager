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
            ShowNoPreviewsItem.Text = SharedUtilities.GetTranslation(langDict, "ShowNoPreviews");
            ShowDuplicatesItem.Text = SharedUtilities.GetTranslation(langDict, "ShowDuplicates");
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
                                       _currentCategory != "NoPreviews" &&
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
                ShowNoPreviewsItem.Visibility = Visibility.Visible;
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
                ShowNoPreviewsItem.Visibility = Visibility.Visible;
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
                ShowNoPreviewsItem.Visibility = Visibility.Visible;
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

        private void ShowNoPreviews_Click(object sender, RoutedEventArgs e)
        {
            // Exit table view if active and clear sorting
            if (CurrentViewMode == ViewMode.Table)
            {
                _currentSortMode = SortMode.None;
                CurrentViewMode = ViewMode.Mods;
            }

            var langDict = SharedUtilities.LoadLanguageDictionary();
            CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_No_Previews");
            _currentCategory = "NoPreviews";
            LoadNoPreviewModsOnly();
            CategoryBackButton.Visibility = Visibility.Visible;
            CategoryOpenFolderButton.Visibility = Visibility.Collapsed;
        }

        private void ShowDuplicates_Click(object sender, RoutedEventArgs e)
        {
            // Exit table view if active and clear sorting
            if (CurrentViewMode == ViewMode.Table)
            {
                _currentSortMode = SortMode.None;
                CurrentViewMode = ViewMode.Mods;
            }

            var langDict = SharedUtilities.LoadLanguageDictionary();
            CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_Duplicates");
            _currentCategory = "Duplicates";
            LoadDuplicateModsOnly();
            CategoryBackButton.Visibility = Visibility.Visible;
            CategoryOpenFolderButton.Visibility = Visibility.Collapsed;
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
                else if (_currentCategory == "NoPreviews")
                {
                    LoadNoPreviewModsOnly();
                }
                else if (_currentCategory == "Duplicates")
                {
                    LoadDuplicateModsOnly();
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
                        HasUpdate = CheckForUpdateLive(modData), // Live check without cache
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
    }
}
