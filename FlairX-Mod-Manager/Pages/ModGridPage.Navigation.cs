using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    /// <summary>
    /// ModGridPage partial class - Navigation and lifecycle management
    /// </summary>
    public sealed partial class ModGridPage : Page
    {
        public static void LogToGridLog(string message, [CallerMemberName] string? callerName = null, [CallerFilePath] string? callerFile = null)
        {
            // Only log if grid logging is enabled in settings
            if (!SettingsManager.Current.GridLoggingEnabled) return;
            
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logPath = PathManager.GetSettingsPath("GridLog.log");
                var settingsDir = Path.GetDirectoryName(logPath);
                
                if (!string.IsNullOrEmpty(settingsDir) && !Directory.Exists(settingsDir))
                {
                    Directory.CreateDirectory(settingsDir);
                }
                
                // Format message with caller information like main Logger
                var fileName = !string.IsNullOrEmpty(callerFile) ? Path.GetFileNameWithoutExtension(callerFile) : "Unknown";
                var methodName = !string.IsNullOrEmpty(callerName) ? callerName : "Unknown";
                var formattedMessage = $"[{fileName}.{methodName}] {message}";
                
                var logEntry = $"[{timestamp}] [GRID] {formattedMessage}\n";
                File.AppendAllText(logPath, logEntry, System.Text.Encoding.UTF8);
                
                // Also log to main logger for unified logging (but as debug level to avoid spam)
                Logger.LogDebug($"GRID: {message}", callerName, callerFile);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to write to GridLog", ex);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Logger.LogMethodEntry($"Navigation parameter: {e.Parameter?.ToString() ?? "null"}");
            base.OnNavigatedTo(e);
            
            Logger.LogInfo("Setting up translations for ModGridPage");
            UpdateTranslations();
            if (e.Parameter is string modName && !string.IsNullOrEmpty(modName))
            {
                // Open mod details for given name using new category-based structure
                var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentModLibraryDirectory();
                if (string.IsNullOrEmpty(modLibraryPath))
                    modLibraryPath = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                var modDir = FindModFolderPath(modLibraryPath, modName);
                if (!string.IsNullOrEmpty(modDir))
                {
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (File.Exists(modJsonPath))
                    {
                        var json = File.ReadAllText(modJsonPath);
                        CategoryTitle.Text = $"Mod details: {modName}";
                        // You can add mod details display in grid here
                        // Example: display JSON in TextBlock
                        ModsGrid.ItemsSource = new[] { json };
                        return;
                    }
                }
            }
            if (e.Parameter is string parameter && !string.IsNullOrEmpty(parameter))
            {
                // SEPARATE NAVIGATION SYSTEMS FOR EACH MODE
                
                if (parameter == "Categories")
                {
                    // ZAWSZE ładuj kategorie gdy parametr to "Categories"
                    _currentViewMode = ViewMode.Categories;
                    LoadCategories();
                    CategoryBackButton.Visibility = Visibility.Collapsed;
                    return;
                }
                else if (parameter.StartsWith("CategoryInCategoryMode:"))
                {
                    var category = parameter.Substring("CategoryInCategoryMode:".Length);
                    
                    // Force category mode and load specific category
                    _currentViewMode = ViewMode.Categories;
                    LoadCategoryInCategoryMode(category);
                    // Visibility is handled by LoadCategoryInCategoryMode
                }
                else if (parameter.StartsWith("Category:"))
                {
                    var category = parameter.Substring("Category:".Length);
                    
                    // PRESERVE THE CURRENT VIEW MODE - DON'T LET ANYTHING CHANGE IT
                    var preservedViewMode = CurrentViewMode;
                    
                    _currentCategory = category;
                    CategoryTitle.Text = category;
                    LoadModsByCategory(category);
                    CategoryBackButton.Visibility = Visibility.Visible;
                    
                    // FORCE THE VIEW MODE TO STAY THE SAME
                    _currentViewMode = preservedViewMode;
                    
                    // Don't update MainWindow button - let MainWindow manage its own button state
                }
                else
                {
                    // Legacy navigation - CHECK MAINWINDOW VIEW MODE
                    bool shouldLoadCategories = false;
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                    {
                        shouldLoadCategories = mainWindow.IsCurrentlyInCategoryMode();
                    }
                    
                    if (shouldLoadCategories && !string.Equals(parameter, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        // In category mode, ignore legacy navigation and load categories (except for Active)
                        _currentViewMode = ViewMode.Categories;
                        LoadCategories();
                        CategoryBackButton.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        // In default mode, handle legacy navigation
                        _currentViewMode = ViewMode.Mods;
                        var character = parameter;
                        _currentCategory = character;
                        if (string.Equals(character, "other", StringComparison.OrdinalIgnoreCase))
                        {
                            var langDict = SharedUtilities.LoadLanguageDictionary();
                            CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_Other_Mods");
                            LoadMods(character);
                        }
                        else if (string.Equals(character, "Active", StringComparison.OrdinalIgnoreCase))
                        {
                            var langDict = SharedUtilities.LoadLanguageDictionary();
                            CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_Active_Mods");
                            LoadActiveModsOnly();
                        }
                        else
                        {
                            CategoryTitle.Text = character;
                            LoadMods(character);
                        }
                        CategoryBackButton.Visibility = Visibility.Visible;
                    }
                }
            }
            else
            {
                // Check if we're returning from mod details and should restore previous state
                if (_wasInCategoryMode && !string.IsNullOrEmpty(_currentCategory))
                {
                    // Restore the previous view mode and category
                    CurrentViewMode = _previousViewMode;
                    
                    if (_previousViewMode == ViewMode.Categories)
                    {
                        // Return to category tiles view - CurrentViewMode setter will handle loading
                        CategoryBackButton.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        // Return to category mods view
                        CategoryTitle.Text = _currentCategory;
                        LoadModsByCategory(_currentCategory);
                        CategoryBackButton.Visibility = Visibility.Visible;
                    }
                    
                    _wasInCategoryMode = false; // Reset flag
                }
                else
                {
                    // Default navigation (null parameter) - CHECK MAINWINDOW VIEW MODE
                    _currentCategory = null;
                    
                    // Check MainWindow view mode to determine what to load
                    bool shouldLoadCategories = false;
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                    {
                        shouldLoadCategories = mainWindow.IsCurrentlyInCategoryMode();
                    }
                    
                    if (shouldLoadCategories)
                    {
                        // CATEGORY MODE: Load categories and set correct view mode
                        _currentViewMode = ViewMode.Categories;
                        LoadCategories();
                    }
                    else
                    {
                        // DEFAULT MODE: Load all mods and set correct view mode
                        _currentViewMode = ViewMode.Mods;
                        var langDict = SharedUtilities.LoadLanguageDictionary();
                        CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_All_Mods");
                        LoadAllMods();
                    }
                    CategoryBackButton.Visibility = Visibility.Collapsed;
                }
            }
            
            // Don't update MainWindow button - let MainWindow manage its own button state
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            
            // KEEP EVERYTHING IN MEMORY - don't clear grid or collections
            // This prevents memory spikes when navigating back to the page
            // The cached collection and images stay loaded for instant access
            LogToGridLog("NAVIGATION: Keeping ModGridPage data in memory for fast return");
        }

        public void FilterMods(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                // Clear search - return to appropriate view based on previous state
                _isSearchActive = false;
                
                if (_previousCategory != null)
                {
                    // Return to previous category if it exists
                    _currentCategory = _previousCategory;
                    if (string.Equals(_previousCategory, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        var langDict = SharedUtilities.LoadLanguageDictionary();
                        CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_Active_Mods");
                        LoadActiveModsOnly();
                    }
                    else if (string.Equals(_previousCategory, "other", StringComparison.OrdinalIgnoreCase))
                    {
                        var langDict = SharedUtilities.LoadLanguageDictionary();
                        CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_Other_Mods");
                        LoadMods(_previousCategory);
                    }
                    else
                    {
                        CategoryTitle.Text = _previousCategory;
                        LoadMods(_previousCategory);
                    }
                }
                else
                {
                    // Default to All Mods if available
                    _currentCategory = null;
                    var langDict = SharedUtilities.LoadLanguageDictionary();
                    CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Category_All_Mods");
                    LoadAllMods();
                }
                _previousCategory = null; // Clear previous category after restoration
                // Scroll view to top after clearing search
                ModsScrollViewer?.ChangeView(0, 0, 1);
            }
            else
            {
                // Start search - save current category if not already searching
                if (!_isSearchActive)
                {
                    _previousCategory = _currentCategory;
                    _isSearchActive = true;
                }
                
                // Set search title
                var langDict = SharedUtilities.LoadLanguageDictionary();
                CategoryTitle.Text = SharedUtilities.GetTranslation(langDict, "Search_Results");
                
                // Load all mod data for searching if not already loaded
                if (_allModData.Count == 0)
                {
                    LoadAllModData();
                }
                
                // Search through the lightweight ModData and create ModTiles for matches
                var filteredData = _allModData.Where(modData => 
                    modData.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    modData.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(modData.Url) && modData.Url.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
                var filteredMods = new List<ModTile>();
                
                foreach (var modData in filteredData)
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
                        IsVisible = true,
                        ImageSource = null // Start with no image - lazy load when visible
                    };
                    filteredMods.Add(modTile);
                }
                
                var filtered = new ObservableCollection<ModTile>(filteredMods);
                ModsGrid.ItemsSource = filtered;

                // Load visible images after filtering
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // Small delay to let the grid update
                    DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
                });

                // Scroll horizontally to first visible mod (with animation)
                if (filtered.Count > 0 && ModsGrid.ContainerFromIndex(0) is GridViewItem firstItem)
                {
                    firstItem.UpdateLayout();
                    var transform = firstItem.TransformToVisual(ModsScrollViewer);
                    var point = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                    // Scroll so that the first item is visible at the left
                    ModsScrollViewer.ChangeView(point.X, 0, 1, false);
                }
                else
                {
                    // Fallback: scroll to start
                    ModsScrollViewer?.ChangeView(0, 0, 1, false);
                }
            }
        }

        private void LoadActiveMods()
        {
            if (File.Exists(ActiveModsStatePath))
            {
                try
                {
                    var json = File.ReadAllText(ActiveModsStatePath);
                    _activeMods = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to load active mods", ex);
                    _activeMods = new();
                }
            }
        }

        private void SaveActiveMods()
        {
            try
            {
                var json = JsonSerializer.Serialize(_activeMods, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ActiveModsStatePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save active mods", ex);
            }
        }

        private void LoadSymlinkState()
        {
            if (File.Exists(SymlinkStatePath))
            {
                try
                {
                    var json = File.ReadAllText(SymlinkStatePath);
                    var state = JsonSerializer.Deserialize<SymlinkState>(json);
                    _lastSymlinkTarget = state?.TargetPath ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to load symlink state", ex);
                    _lastSymlinkTarget = string.Empty;
                }
            }
        }

        private void SaveSymlinkState()
        {
            try
            {
                var state = new SymlinkState { TargetPath = _lastSymlinkTarget ?? string.Empty };
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SymlinkStatePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save symlink state", ex);
            }
        }

        private void UpdateTranslations()
        {
            var langDict = SharedUtilities.LoadLanguageDictionary();
            
            // Update search box placeholder
            if (TableSearchBox != null)
            {
                TableSearchBox.PlaceholderText = SharedUtilities.GetTranslation(langDict, "Search_Placeholder");
            }
            
            // Update exit table view text
            if (ExitTableViewItem != null)
            {
                ExitTableViewItem.Text = SharedUtilities.GetTranslation(langDict, "Exit_Table_View");
            }
        }

        public class SymlinkState
        {
            public string TargetPath { get; set; } = string.Empty;
        }
    }
}