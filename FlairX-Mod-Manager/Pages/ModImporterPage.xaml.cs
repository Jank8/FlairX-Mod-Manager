using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModImporterPage : Page
    {
        private string? _selectedSourceFolder;
        private List<ModImportItem> _allMods = new();
        private List<ModImportItem> _autoAssignedMods = new();
        private ObservableCollection<ConflictItem> _conflicts = new();
        private bool _isNestedStructure = false;

        // Aliases: category name -> list of aliases
        private Dictionary<string, List<string>> _aliases = new();
        private ObservableCollection<AliasGroup> _aliasGroups = new();

        public ModImporterPage()
        {
            this.InitializeComponent();
            UpdateTexts();
            ConflictsList.ItemsSource = _conflicts;
            AliasesItemsControl.ItemsSource = _aliasGroups;
            LoadAliases();
            
            // Populate category combobox when page loads
            this.Loaded += (s, e) => RefreshAliasCategoryComboBox();
        }

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            
            WarningTitle.Text = SharedUtilities.GetTranslation(lang, "ModImporter_WarningTitle");
            WarningMessage.Text = SharedUtilities.GetTranslation(lang, "ModImporter_WarningMessage");
            
            CreateCategoriesTitle.Text = SharedUtilities.GetTranslation(lang, "ModImporter_CreateCategories");
            CreateCategoriesDescription.Text = SharedUtilities.GetTranslation(lang, "ModImporter_CreateCategoriesDesc");
            
            AliasesTitle.Text = SharedUtilities.GetTranslation(lang, "ModImporter_AliasesTitle");
            AliasesDescription.Text = SharedUtilities.GetTranslation(lang, "ModImporter_AliasesDesc");
            AddAliasButtonText.Text = SharedUtilities.GetTranslation(lang, "Add");
            
            SourceFolderLabel.Text = SharedUtilities.GetTranslation(lang, "ModImporter_SourceFolder");
            SelectFolderButtonText.Text = SharedUtilities.GetTranslation(lang, "ModImporter_SelectFolder");
            
            NestedStructureLabel.Text = SharedUtilities.GetTranslation(lang, "ModImporter_NestedStructure");
            NestedStructureDescription.Text = SharedUtilities.GetTranslation(lang, "ModImporter_NestedStructureDesc");
            
            StatusTitle.Text = SharedUtilities.GetTranslation(lang, "ModImporter_ScanResults");
            FoundModsLabel.Text = SharedUtilities.GetTranslation(lang, "ModImporter_FoundMods");
            AutoAssignedLabel.Text = SharedUtilities.GetTranslation(lang, "ModImporter_AutoAssigned");
            NeedsAttentionLabel.Text = SharedUtilities.GetTranslation(lang, "ModImporter_NeedsAttention");
            StartScanButtonText.Text = SharedUtilities.GetTranslation(lang, "ModImporter_ScanFolder");
            
            ResolveAndImportButtonText.Text = SharedUtilities.GetTranslation(lang, "ModImporter_ResolveAndImport");
            ProgressTitle.Text = SharedUtilities.GetTranslation(lang, "ModImporter_Importing");
        }

        private void NestedStructureCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _isNestedStructure = NestedStructureCheckBox.IsChecked == true;
            
            // Reset state when changing structure mode
            if (!string.IsNullOrEmpty(_selectedSourceFolder))
            {
                _allMods.Clear();
                _autoAssignedMods.Clear();
                _conflicts.Clear();
                ConflictsPanel.Visibility = Visibility.Collapsed;
                
                FoundModsCount.Text = "0";
                AutoAssignedCount.Text = "0";
                NeedsAttentionCount.Text = "0";
            }
        }

        #region Aliases

        private static string AliasesFilePath => PathManager.GetSettingsPath("mod_importer_aliases.json");

        private static string CurrentGameTag => SettingsManager.CurrentSelectedGame ?? "default";

        private void LoadAliases()
        {
            _aliases.Clear();
            try
            {
                if (File.Exists(AliasesFilePath))
                {
                    var json = File.ReadAllText(AliasesFilePath);
                    // Format: { "gameTag": { "category": ["alias1", "alias2"] } }
                    var allGames = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<string>>>>(json) ?? new();
                    if (allGames.TryGetValue(CurrentGameTag, out var gameAliases))
                        _aliases = gameAliases;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load aliases", ex);
            }
            RefreshAliasGroups();
        }

        private void SaveAliases()
        {
            try
            {
                // Load all games first to not overwrite other games
                Dictionary<string, Dictionary<string, List<string>>> allGames = new();
                if (File.Exists(AliasesFilePath))
                {
                    var existingJson = File.ReadAllText(AliasesFilePath);
                    allGames = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<string>>>>(existingJson) ?? new();
                }
                allGames[CurrentGameTag] = _aliases;
                var json = System.Text.Json.JsonSerializer.Serialize(allGames, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(AliasesFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save aliases", ex);
            }
        }

        private void RefreshAliasGroups()
        {
            _aliasGroups.Clear();
            foreach (var kvp in _aliases.Where(k => k.Value.Count > 0))
            {
                var group = new AliasGroup { CategoryName = kvp.Key };
                foreach (var alias in kvp.Value)
                    group.Aliases.Add(new AliasItem { CategoryName = kvp.Key, AliasName = alias });
                _aliasGroups.Add(group);
            }
        }

        private void RefreshAliasCategoryComboBox()
        {
            var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
            if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
                return;

            var categories = Directory.GetDirectories(modsPath)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Cast<string>()
                .OrderBy(n => n)
                .ToList();

            AliasCategoryComboBox.ItemsSource = categories;
        }

        private void AliasTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                AddAliasButton_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void AddAliasButton_Click(object sender, RoutedEventArgs e)
        {
            var category = AliasCategoryComboBox.SelectedItem as string;
            var alias = AliasTextBox.Text?.Trim();

            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(alias))
                return;

            if (!_aliases.ContainsKey(category))
                _aliases[category] = new List<string>();

            if (!_aliases[category].Contains(alias, StringComparer.OrdinalIgnoreCase))
            {
                _aliases[category].Add(alias);
                SaveAliases();
                RefreshAliasGroups();
            }

            AliasTextBox.Text = string.Empty;
        }

        private void RemoveAlias_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AliasItem item)
            {
                if (_aliases.TryGetValue(item.CategoryName, out var list))
                {
                    list.Remove(item.AliasName);
                    if (list.Count == 0)
                        _aliases.Remove(item.CategoryName);
                    SaveAliases();
                    RefreshAliasGroups();
                }
            }
        }

        #endregion

        private void CreateCategoriesButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to GBAuthorUpdate tab in Settings
            if (App.Current is App app && app.MainWindow is MainWindow mw)
            {
                mw.NavigateToGBAuthorUpdate();
            }
        }



        private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");

            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
            }

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                _selectedSourceFolder = folder.Path;
                SourceFolderPath.Text = _selectedSourceFolder;
                StatusCard.Visibility = Visibility.Visible;
                
                // Reset state
                _allMods.Clear();
                _autoAssignedMods.Clear();
                _conflicts.Clear();
                ConflictsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void StartScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedSourceFolder))
                return;

            // Reset previous results
            _allMods.Clear();
            _autoAssignedMods.Clear();
            _conflicts.Clear();
            ConflictsPanel.Visibility = Visibility.Collapsed;
            ResolvePanel.Visibility = Visibility.Collapsed;
            FoundModsCount.Text = "0";
            AutoAssignedCount.Text = "0";
            NeedsAttentionCount.Text = "0";

            StartScanButton.IsEnabled = false;
            
            try
            {
                await ScanAndAnalyzeModsAsync();
                
                // Update UI
                FoundModsCount.Text = _allMods.Count.ToString();
                AutoAssignedCount.Text = _autoAssignedMods.Count.ToString();
                NeedsAttentionCount.Text = _conflicts.Count.ToString();

                if (_conflicts.Count > 0)
                {
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    ConflictsTitle.Text = $"{SharedUtilities.GetTranslation(lang, "ModImporter_Conflicts")} ({_conflicts.Count})";
                    ConflictsPanel.Visibility = Visibility.Visible;
                    ResolvePanel.Visibility = Visibility.Visible;
                }
                else
                {
                    // No conflicts, can import directly
                    await ImportModsAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to scan mods", ex);
                if (App.Current is App app && app.MainWindow is MainWindow mw)
                    mw.ShowErrorInfo(ex.Message);
            }
            finally
            {
                StartScanButton.IsEnabled = true;
            }
        }

        private async Task ScanAndAnalyzeModsAsync()
        {
            if (string.IsNullOrEmpty(_selectedSourceFolder))
                return;

            var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
            if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
                return;

            // Get existing categories
            var existingCategories = Directory.GetDirectories(modsPath)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToList();

            if (existingCategories.Count == 0)
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                throw new InvalidOperationException(SharedUtilities.GetTranslation(lang, "ModImporter_NoCategories"));
            }

            if (_isNestedStructure)
            {
                // Nested structure: SourceFolder/CategoryFolder/ModFolder
                // Step 1: try to match by category folder name
                // Step 2: if no category match, try to match each mod by its name
                var categoryFolders = Directory.GetDirectories(_selectedSourceFolder);
                
                foreach (var categoryFolder in categoryFolders)
                {
                    var categoryName = Path.GetFileName(categoryFolder);
                    if (string.IsNullOrEmpty(categoryName))
                        continue;

                    var modFolders = Directory.GetDirectories(categoryFolder);
                    if (modFolders.Length == 0)
                        continue;

                    // Step 1: try to match category folder name
                    var matchingCategory = FindBestCategoryMatch(categoryName, existingCategories);
                    
                    if (!string.IsNullOrEmpty(matchingCategory))
                    {
                        // Category matched - auto-assign all mods in it
                        foreach (var modFolder in modFolders)
                        {
                            var modName = Path.GetFileName(modFolder);
                            if (string.IsNullOrEmpty(modName)) continue;

                            var item = new ModImportItem
                            {
                                SourcePath = modFolder,
                                ModName = modName,
                                TargetCategory = matchingCategory
                            };
                            _autoAssignedMods.Add(item);
                            _allMods.Add(item);
                        }
                    }
                    else
                    {
                        // Step 2: category folder didn't match - try each mod name individually
                        foreach (var modFolder in modFolders)
                        {
                            var modName = Path.GetFileName(modFolder);
                            if (string.IsNullOrEmpty(modName)) continue;

                            var item = new ModImportItem { SourcePath = modFolder, ModName = modName };
                            var matches = FindMatchingCategories(modName, existingCategories);

                            if (matches.Count == 1)
                            {
                                item.TargetCategory = matches[0];
                                _autoAssignedMods.Add(item);
                            }
                            else if (matches.Count > 1)
                            {
                                var lang = SharedUtilities.LoadLanguageDictionary();
                                _conflicts.Add(new ConflictItem
                                {
                                    ModName = $"{categoryName}/{modName}",
                                    IssueDescription = SharedUtilities.GetTranslation(lang, "ModImporter_MultipleMatches"),
                                    AvailableCategories = matches,
                                    SelectedCategory = matches[0],
                                    SourcePath = modFolder,
                                    IsDuplicate = Visibility.Collapsed
                                });
                            }
                            else
                            {
                                var lang = SharedUtilities.LoadLanguageDictionary();
                                _conflicts.Add(new ConflictItem
                                {
                                    ModName = $"{categoryName}/{modName}",
                                    IssueDescription = SharedUtilities.GetTranslation(lang, "ModImporter_NoMatch"),
                                    AvailableCategories = existingCategories,
                                    SourcePath = modFolder,
                                    IsDuplicate = Visibility.Collapsed
                                });
                            }

                            _allMods.Add(item);
                        }
                    }
                }
            }
            else
            {
                // Flat structure: SourceFolder/ModFolder
                var modFolders = Directory.GetDirectories(_selectedSourceFolder);
                
                foreach (var modFolder in modFolders)
                {
                    var modName = Path.GetFileName(modFolder);
                    if (string.IsNullOrEmpty(modName))
                        continue;

                    var item = new ModImportItem
                    {
                        SourcePath = modFolder,
                        ModName = modName
                    };

                    // Find matching categories
                    var matches = FindMatchingCategories(modName, existingCategories);
                    
                    if (matches.Count == 1)
                    {
                        // Single match - auto-assign
                        item.TargetCategory = matches[0];
                        _autoAssignedMods.Add(item);
                    }
                    else if (matches.Count > 1)
                    {
                        // Multiple matches - needs user decision
                        var lang = SharedUtilities.LoadLanguageDictionary();
                        _conflicts.Add(new ConflictItem
                        {
                            ModName = modName,
                            IssueDescription = SharedUtilities.GetTranslation(lang, "ModImporter_MultipleMatches"),
                            AvailableCategories = matches,
                            SelectedCategory = matches[0],
                            SourcePath = modFolder,
                            IsDuplicate = Visibility.Collapsed
                        });
                    }
                    else
                    {
                        // No match - needs user decision
                        var lang = SharedUtilities.LoadLanguageDictionary();
                        _conflicts.Add(new ConflictItem
                        {
                            ModName = modName,
                            IssueDescription = SharedUtilities.GetTranslation(lang, "ModImporter_NoMatch"),
                            AvailableCategories = existingCategories,
                            SourcePath = modFolder,
                            IsDuplicate = Visibility.Collapsed
                        });
                    }

                    _allMods.Add(item);
                }
            }

            // Check for duplicates
            foreach (var conflict in _conflicts.ToList())
            {
                if (!string.IsNullOrEmpty(conflict.SelectedCategory))
                {
                    var targetPath = Path.Combine(modsPath, conflict.SelectedCategory, conflict.ModName);
                    if (Directory.Exists(targetPath))
                    {
                        var lang = SharedUtilities.LoadLanguageDictionary();
                        conflict.IssueDescription += $" - {SharedUtilities.GetTranslation(lang, "ModImporter_AlreadyExists")}";
                        conflict.IsDuplicate = Visibility.Visible;
                    }
                }
            }
        }

        private string? FindBestCategoryMatch(string folderName, List<string> categories)
        {
            // Try exact match first (case-insensitive)
            var exactMatch = categories.FirstOrDefault(c => c.Equals(folderName, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
                return exactMatch;

            // Try substring match (words > 5 chars)
            var matches = FindMatchingCategories(folderName, categories);
            return matches.Count == 1 ? matches[0] : null;
        }

        private List<string> FindMatchingCategories(string modName, List<string> categories)
        {
            var matches = new List<string>();
            var modNameLower = modName.ToLower();

            // Split mod name into words for exact word matching
            var modWords = modNameLower
                .Split(new[] { ' ', '_', '-', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet();

            foreach (var category in categories)
            {
                // Check category name words
                var categoryWords = category.ToLower()
                    .Split(new[] { ' ', '_', '-', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

                bool matched = categoryWords.Any(word =>
                    word.Length > 5 ? modNameLower.Contains(word) : modWords.Contains(word));

                // Check aliases for this category
                if (!matched && _aliases.TryGetValue(category, out var aliasList))
                {
                    matched = aliasList.Any(alias =>
                    {
                        var aliasLower = alias.ToLower();
                        return aliasLower.Length > 5
                            ? modNameLower.Contains(aliasLower)
                            : modWords.Contains(aliasLower);
                    });
                }

                if (matched)
                    matches.Add(category);
            }

            return matches;
        }

        private async void ResolveAndImportButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate all conflicts have selections
            var lang = SharedUtilities.LoadLanguageDictionary();
            foreach (var conflict in _conflicts)
            {
                if (string.IsNullOrEmpty(conflict.SelectedCategory))
                {
                    if (App.Current is App app && app.MainWindow is MainWindow mw)
                        mw.ShowErrorInfo(SharedUtilities.GetTranslation(lang, "ModImporter_SelectAllCategories"));
                    return;
                }
            }

            // Add resolved conflicts to auto-assigned list
            foreach (var conflict in _conflicts)
            {
                if (!string.IsNullOrEmpty(conflict.SelectedCategory))
                {
                    _autoAssignedMods.Add(new ModImportItem
                    {
                        SourcePath = conflict.SourcePath,
                        ModName = conflict.ModName,
                        TargetCategory = conflict.SelectedCategory,
                        ShouldOverwrite = conflict.ShouldOverwrite
                    });
                }
            }

            await ImportModsAsync();
        }

        private async Task ImportModsAsync()
        {
            var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
            if (string.IsNullOrEmpty(modsPath))
                return;

            ProgressPanel.Visibility = Visibility.Visible;
            ConflictsPanel.Visibility = Visibility.Collapsed;
            StatusCard.Visibility = Visibility.Collapsed;

            int total = _autoAssignedMods.Count;
            int current = 0;
            int success = 0;
            int skipped = 0;
            int failed = 0;

            ImportProgressBar.Maximum = total;
            ImportProgressBar.Value = 0;

            foreach (var mod in _autoAssignedMods)
            {
                current++;
                var lang = SharedUtilities.LoadLanguageDictionary();
                ProgressStatus.Text = $"{SharedUtilities.GetTranslation(lang, "ModImporter_Copying")} {mod.ModName} ({current}/{total})";
                ImportProgressBar.Value = current;

                try
                {
                    var targetPath = Path.Combine(modsPath, mod.TargetCategory, mod.ModName);
                    
                    if (Directory.Exists(targetPath))
                    {
                        if (mod.ShouldOverwrite)
                        {
                            Directory.Delete(targetPath, true);
                        }
                        else
                        {
                            skipped++;
                            continue;
                        }
                    }

                    // Copy directory
                    CopyDirectory(mod.SourcePath, targetPath);
                    success++;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to import mod: {mod.ModName}", ex);
                    failed++;
                }

                await Task.Delay(10); // Allow UI to update
            }

            // Show summary
            var summaryLang = SharedUtilities.LoadLanguageDictionary();
            var summary = $"{SharedUtilities.GetTranslation(summaryLang, "ModImporter_ImportComplete")}\n\n" +
                         $"{SharedUtilities.GetTranslation(summaryLang, "Success")}: {success}\n" +
                         $"{SharedUtilities.GetTranslation(summaryLang, "Skipped")}: {skipped}\n" +
                         $"{SharedUtilities.GetTranslation(summaryLang, "Failed")}: {failed}";

            var summaryDialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(summaryLang, "ModImporter_Complete"),
                Content = summary,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };

            await summaryDialog.ShowAsync();

            // Reset UI
            ProgressPanel.Visibility = Visibility.Collapsed;
            StatusCard.Visibility = Visibility.Collapsed;
            ConflictsPanel.Visibility = Visibility.Collapsed;
            ResolvePanel.Visibility = Visibility.Collapsed;
            _selectedSourceFolder = null;
            SourceFolderPath.Text = SharedUtilities.GetTranslation(summaryLang, "ModImporter_NotSelected");
            _allMods.Clear();
            _autoAssignedMods.Clear();
            _conflicts.Clear();

            // Reload mods in main window
            if (App.Current is App app && app.MainWindow is MainWindow mw)
            {
                await mw.ReloadModsAsync();
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var targetFile = Path.Combine(targetDir, fileName);
                File.Copy(file, targetFile, true);
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(subDir);
                var targetSubDir = Path.Combine(targetDir, dirName);
                CopyDirectory(subDir, targetSubDir);
            }
        }
    }

    public class ModImportItem
    {
        public string SourcePath { get; set; } = "";
        public string ModName { get; set; } = "";
        public string TargetCategory { get; set; } = "";
        public bool ShouldOverwrite { get; set; } = false;
    }

    public class ConflictItem
    {
        public string ModName { get; set; } = "";
        public string IssueDescription { get; set; } = "";
        public List<string> AvailableCategories { get; set; } = new();
        public string? SelectedCategory { get; set; }
        public string SourcePath { get; set; } = "";
        public Visibility IsDuplicate { get; set; } = Visibility.Collapsed;
        public bool ShouldOverwrite { get; set; } = false;
    }

    public class AliasGroup
    {
        public string CategoryName { get; set; } = "";
        public ObservableCollection<AliasItem> Aliases { get; set; } = new();
    }

    public class AliasItem
    {
        public string CategoryName { get; set; } = "";
        public string AliasName { get; set; } = "";
    }
}