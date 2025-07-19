using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using System.IO.Compression;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace ZZZ_Mod_Manager_X.Pages
{
    public sealed partial class ModGridPage : Page
    {
        public class ModTile : INotifyPropertyChanged
        {
            public string Name { get; set; } = "";
            public string ImagePath { get; set; } = "";
            public string Directory { get; set; } = ""; // Store only the directory name
            private BitmapImage? _imageSource;
            public BitmapImage? ImageSource
            {
                get => _imageSource;
                set { if (_imageSource != value) { _imageSource = value; OnPropertyChanged(nameof(ImageSource)); } }
            }
            private bool _isActive;
            public bool IsActive
            {
                get => _isActive;
                set { if (_isActive != value) { _isActive = value; OnPropertyChanged(nameof(IsActive)); } }
            }
            private bool _isHovered;
            public bool IsHovered
            {
                get => _isHovered;
                set { if (_isHovered != value) { _isHovered = value; OnPropertyChanged(nameof(IsHovered)); } }
            }
            private bool _isFolderHovered;
            public bool IsFolderHovered
            {
                get => _isFolderHovered;
                set { if (_isFolderHovered != value) { _isFolderHovered = value; OnPropertyChanged(nameof(IsFolderHovered)); } }
            }
            private bool _isVisible = true;
            public bool IsVisible
            {
                get => _isVisible;
                set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); } }
            }
            private bool _isInViewport;
            public bool IsInViewport
            {
                get => _isInViewport;
                set { if (_isInViewport != value) { _isInViewport = value; OnPropertyChanged(nameof(IsInViewport)); } }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private static string ActiveModsStatePath => Path.Combine(AppContext.BaseDirectory, "Settings", "ActiveMods.json");
        private static string SymlinkStatePath => Path.Combine(AppContext.BaseDirectory, "Settings", "SymlinkState.json");
        private Dictionary<string, bool> _activeMods = new();
        private string? _lastSymlinkTarget;
        private ObservableCollection<ModTile> _allMods = new();
        // Removed static image caches - now using ImageCacheManager

        public ICommand ModImageIsInViewportChangedCommand { get; }

        public ModGridPage()
        {
            this.InitializeComponent();
            LoadActiveMods();
            LoadSymlinkState();
            ModImageIsInViewportChangedCommand = new RelayCommand<object>(OnModImageIsInViewportChanged);
            // Check mod directories and create mod.json in level 1 directories
            (App.Current as ZZZ_Mod_Manager_X.App)?.EnsureModJsonInModLibrary();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string modName && !string.IsNullOrEmpty(modName))
            {
                // Open mod details for given name
                var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                var modDir = Path.Combine(modLibraryPath, modName);
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
            if (e.Parameter is string character && !string.IsNullOrEmpty(character))
            {
                if (string.Equals(character, "other", StringComparison.OrdinalIgnoreCase))
                {
                    CategoryTitle.Text = LanguageManager.Instance.T("Category_Other_Mods");
                    LoadMods(character);
                }
                else if (string.Equals(character, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    CategoryTitle.Text = LanguageManager.Instance.T("Category_Active_Mods");
                    LoadActiveModsOnly();
                }
                else
                {
                    CategoryTitle.Text = character;
                    LoadMods(character);
                }
            }
            else
            {
                CategoryTitle.Text = LanguageManager.Instance.T("Category_All_Mods");
                LoadAllMods();
            }
            
            // Notify MainWindow to update heart button after category title is set
            NotifyMainWindowToUpdateHeartButton();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            
            // Clear the grid to reduce memory usage and improve navigation performance
            if (ModsGrid?.ItemsSource is IEnumerable<ModTile> currentMods)
            {
                // Clear image sources to free up memory
                foreach (var mod in currentMods)
                {
                    if (mod.ImageSource != null)
                    {
                        // Don't dispose cached images, just clear the reference
                        mod.ImageSource = null;
                    }
                }
            }
            
            // Clear the grid ItemsSource
            if (ModsGrid != null)
            {
                ModsGrid.ItemsSource = null;
            }
            
            // Clear the observable collection
            _allMods.Clear();
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
                catch { _activeMods = new(); }
            }
        }

        private void SaveActiveMods()
        {
            try
            {
                var json = JsonSerializer.Serialize(_activeMods, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ActiveModsStatePath, json);
            }
            catch { }
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
                    _lastSymlinkTarget = null;
                }
            }
        }

        private void SaveSymlinkState(string targetPath)
        {
            try
            {
                var state = new SymlinkState { TargetPath = targetPath };
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SymlinkStatePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save symlink state", ex);
            }
        }

        private class SymlinkState
        {
            public string? TargetPath { get; set; }
        }

        private BitmapImage GetOrCreateBitmapImage(string imagePath, string? modDirectory = null)
        {
            // Check RAM cache first if modDirectory is provided
            if (modDirectory != null)
            {
                var ramCached = ImageCacheManager.GetCachedRamImage(modDirectory);
                if (ramCached != null)
                    return ramCached;
            }
            
            // Check disk cache
            var diskCached = ImageCacheManager.GetCachedImage(imagePath);
            if (diskCached != null)
                return diskCached;
            
            // Create new bitmap
            var bitmap = new BitmapImage();
            try
            {
                using (var stream = File.OpenRead(imagePath))
                {
                    bitmap.SetSource(stream.AsRandomAccessStream());
                }
                ImageCacheManager.CacheImage(imagePath, bitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load image {imagePath}: {ex.Message}");
            }
            
            return bitmap;
        }

        private void LoadMods(string character)
        {
            var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(modLibraryPath)) return;
            var mods = new List<ModTile>();
            foreach (var dir in Directory.GetDirectories(modLibraryPath))
            {
                var modJsonPath = Path.Combine(dir, "mod.json");
                if (!File.Exists(modJsonPath)) continue;
                try
                {
                    var json = File.ReadAllText(modJsonPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var modCharacter = root.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "other" : "other";
                    if (!string.Equals(modCharacter, character, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var name = Path.GetFileName(dir);
                    string previewPath = Path.Combine(dir, "preview.jpg");
                    string imagePath = File.Exists(previewPath) ? previewPath : "Assets/placeholder.png";
                    var dirName = Path.GetFileName(dir);
                    var isActive = _activeMods.TryGetValue(dirName, out var active) && active;
                    var modTile = new ModTile { Name = name, ImagePath = imagePath, Directory = dirName, IsActive = isActive };
                    modTile.ImageSource = GetOrCreateBitmapImage(imagePath, dirName);
                    mods.Add(modTile);
                }
                catch { }
            }
            mods = mods
                .OrderByDescending(m => m.IsActive) // Active on top
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            ModsGrid.ItemsSource = mods;
        }

        private void LoadAllMods()
        {
            var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(modLibraryPath)) return;
            var mods = new ObservableCollection<ModTile>();
            foreach (var dir in Directory.GetDirectories(modLibraryPath))
            {
                var modJsonPath = Path.Combine(dir, "mod.json");
                if (!File.Exists(modJsonPath)) continue;
                try
                {
                    var json = File.ReadAllText(modJsonPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var modCharacter = root.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "other" : "other";
                    if (string.Equals(modCharacter, "other", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var name = Path.GetFileName(dir);
                    string previewPath = Path.Combine(dir, "preview.jpg");
                    string imagePath = File.Exists(previewPath) ? previewPath : "Assets/placeholder.png";
                    var dirName = Path.GetFileName(dir);
                    var isActive = _activeMods.TryGetValue(dirName, out var active) && active;
                    var modTile = new ModTile { Name = name, ImagePath = imagePath, Directory = dirName, IsActive = isActive, IsVisible = true };
                    modTile.ImageSource = GetOrCreateBitmapImage(imagePath, dirName);
                    mods.Add(modTile);
                }
                catch { }
            }
            var sorted = new ObservableCollection<ModTile>(
                mods.OrderByDescending(m => m.IsActive) // Active on top
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            );
            _allMods = sorted;
            ModsGrid.ItemsSource = _allMods;
        }

        public void FilterMods(string query)
        {
            LoadAllMods(); // Always refresh full mod list from disk
            ObservableCollection<ModTile> filtered;
            if (string.IsNullOrEmpty(query))
            {
                filtered = new ObservableCollection<ModTile>(_allMods);
            }
            else
            {
                filtered = new ObservableCollection<ModTile>(_allMods.Where(mod => mod.Name.Contains(query, StringComparison.OrdinalIgnoreCase)));
            }
            ModsGrid.ItemsSource = filtered;
            // Scroll view to top after filtering
            ModsScrollViewer?.ChangeView(0, 0, 1);
        }

        private void ModActiveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                // Validate mod directory name for security
                if (!IsValidModDirectoryName(mod.Directory))
                    return;

                // Always use current path from settings
                var modsDir = ZZZ_Mod_Manager_X.SettingsManager.Current.XXMIModsDirectory;
                if (string.IsNullOrWhiteSpace(modsDir))
                    modsDir = Path.Combine(AppContext.BaseDirectory, "XXMI", "ZZMI", "Mods");
                var modsDirFull = Path.GetFullPath(modsDir);
                if (_lastSymlinkTarget != null && !_lastSymlinkTarget.Equals(modsDirFull, StringComparison.OrdinalIgnoreCase))
                {
                    RemoveAllSymlinks(_lastSymlinkTarget);
                }
                var linkPath = Path.Combine(modsDirFull, mod.Directory);
                var absModDir = Path.Combine(ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary"), mod.Directory);
                // Remove double slashes in paths
                linkPath = CleanPath(linkPath);
                absModDir = CleanPath(absModDir);
                if (!_activeMods.TryGetValue(mod.Directory, out var isActive) || !isActive)
                {
                    if (!Directory.Exists(modsDirFull))
                        Directory.CreateDirectory(modsDirFull);
                    if (!Directory.Exists(linkPath) && !File.Exists(linkPath))
                    {
                        CreateSymlink(linkPath, absModDir);
                    }
                    _activeMods[mod.Directory] = true;
                    mod.IsActive = true;
                }
                else
                {
                    if ((Directory.Exists(linkPath) || File.Exists(linkPath)) && IsSymlink(linkPath))
                        Directory.Delete(linkPath, true);
                    _activeMods[mod.Directory] = false;
                    mod.IsActive = false;
                }
                SaveActiveMods();
                SaveSymlinkState(modsDirFull);
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

        private void OpenModFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                // Validate mod directory name for security
                if (!IsValidModDirectoryName(mod.Directory))
                    return;

                // Always use the current ModLibraryDirectory setting
                var modLibraryDir = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory;
                if (string.IsNullOrWhiteSpace(modLibraryDir))
                    modLibraryDir = Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                var folder = Path.GetFullPath(Path.Combine(modLibraryDir, mod.Directory));
                if (Directory.Exists(folder))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{folder}\"",
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
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

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);
        private void CreateSymlink(string linkPath, string targetPath)
        {
            try
            {
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

        private void ModName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile mod)
            {
                // Validate mod directory name for security
                if (!IsValidModDirectoryName(mod.Directory))
                    return;

                // Navigate to mod details page, pass Directory (folder name)
                var frame = this.Frame;
                frame?.Navigate(typeof(ZZZ_Mod_Manager_X.Pages.ModDetailPage), mod.Directory ?? string.Empty);
            }
        }

        public static void RecreateSymlinksFromActiveMods()
        {
            var modsDir = ZZZ_Mod_Manager_X.SettingsManager.Current.XXMIModsDirectory;
            var defaultModsDir = Path.Combine(AppContext.BaseDirectory, "XXMI", "ZZMI", "Mods");
            if (string.IsNullOrWhiteSpace(modsDir))
                modsDir = defaultModsDir;
            var modsDirFull = Path.GetFullPath(modsDir);
            var defaultModsDirFull = Path.GetFullPath(defaultModsDir);
            var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");

            // Remove symlinks from old location (SymlinkState)
            var symlinkStatePath = Path.Combine(AppContext.BaseDirectory, "Settings", "SymlinkState.json");
            string? lastSymlinkTarget = null;
            if (File.Exists(symlinkStatePath))
            {
                try
                {
                    var json = File.ReadAllText(symlinkStatePath);
                    var state = JsonSerializer.Deserialize<SymlinkState>(json);
                    lastSymlinkTarget = state?.TargetPath;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to read symlink state during recreation", ex);
                }
            }
            if (!string.IsNullOrWhiteSpace(lastSymlinkTarget) && !lastSymlinkTarget.Equals(modsDirFull, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(lastSymlinkTarget))
                {
                    foreach (var dir in Directory.GetDirectories(lastSymlinkTarget))
                    {
                        if ((Directory.Exists(dir) || File.Exists(dir)) && IsSymlinkStatic(dir))
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                }
            }
            // Remove symlinks from default location if NOT currently selected
            if (!defaultModsDirFull.Equals(modsDirFull, StringComparison.OrdinalIgnoreCase) && Directory.Exists(defaultModsDirFull))
            {
                foreach (var dir in Directory.GetDirectories(defaultModsDirFull))
                {
                    if ((Directory.Exists(dir) || File.Exists(dir)) && IsSymlinkStatic(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
            }
            // Remove symlinks from new location
            if (Directory.Exists(modsDirFull))
            {
                foreach (var dir in Directory.GetDirectories(modsDirFull))
                {
                    if ((Directory.Exists(dir) || File.Exists(dir)) && IsSymlinkStatic(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
            }

            var activeModsPath = Path.Combine(AppContext.BaseDirectory, "Settings", "ActiveMods.json");
            if (!File.Exists(activeModsPath)) return;
            try
            {
                var json = File.ReadAllText(activeModsPath);
                var relMods = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
                foreach (var kv in relMods)
                {
                    if (kv.Value)
                    {
                        var absModDir = Path.Combine(modLibraryPath, kv.Key);
                        var linkPath = Path.Combine(modsDirFull, kv.Key);
                        if (!Directory.Exists(linkPath) && !File.Exists(linkPath))
                        {
                            CreateSymlinkStatic(linkPath, absModDir);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to recreate symlinks from active mods", ex);
            }
        }

        public static void ApplyPreset(string presetName)
        {
            var presetPath = Path.Combine(AppContext.BaseDirectory, "Settings", "Presets", presetName + ".json");
            if (!File.Exists(presetPath)) return;
            try
            {
                RecreateSymlinksFromActiveMods();
                var json = File.ReadAllText(presetPath);
                var preset = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                if (preset != null)
                {
                    var activeModsPath = Path.Combine(AppContext.BaseDirectory, "Settings", "ActiveMods.json");
                    var presetJson = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(activeModsPath, presetJson);
                    RecreateSymlinksFromActiveMods();
                }
            }
            catch { }
        }

        public void SaveDefaultPresetAllInactive()
        {
            var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            var allMods = new Dictionary<string, bool>();
            if (Directory.Exists(modLibraryPath))
            {
                var dirs = Directory.GetDirectories(modLibraryPath);
                foreach (var dir in dirs)
                {
                    var modJsonPath = Path.Combine(dir, "mod.json");
                    if (File.Exists(modJsonPath))
                    {
                        try
                        {
                            var json = File.ReadAllText(modJsonPath);
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            var modCharacter = root.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "other" : "other";
                            if (string.Equals(modCharacter, "other", StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                        catch { continue; }
                        string modName = Path.GetFileName(dir);
                        allMods[modName] = false;
                    }
                }
            }
            var presetPath = Path.Combine(AppContext.BaseDirectory, "Settings", "Presets", "Default Preset.json");
            var presetDir = Path.GetDirectoryName(presetPath) ?? string.Empty;
            Directory.CreateDirectory(presetDir);
            try
            {
                var json = JsonSerializer.Serialize(allMods, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(presetPath, json);
            }
            catch { }
        }

        private void LoadAllImagesToRamCache()
        {
            // Clear RAM cache using ImageCacheManager
            ImageCacheManager.ClearAllCaches();
            
            var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(modLibraryPath)) return;
            
            foreach (var dir in Directory.GetDirectories(modLibraryPath))
            {
                var dirName = Path.GetFileName(dir);
                var previewPath = Path.Combine(dir, "preview.jpg");
                if (File.Exists(previewPath))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        using (var stream = File.OpenRead(previewPath))
                        {
                            bitmap.SetSource(stream.AsRandomAccessStream());
                        }
                        ImageCacheManager.CacheRamImage(dirName, bitmap);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load preview image for {dirName}: {ex.Message}");
                    }
                }
            }
        }

        private void RefreshModImagesInternal()
        {
            LoadAllImagesToRamCache();
            foreach (var mod in _allMods)
            {
                var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                string previewPath = Path.Combine(modLibraryPath, mod.Directory, "preview.jpg");
                string imagePath = File.Exists(previewPath) ? previewPath : "Assets/placeholder.png";
                mod.ImagePath = imagePath;
                mod.ImageSource = GetOrCreateBitmapImage(imagePath, mod.Directory);
            }
            ModsGrid.ItemsSource = null;
            ModsGrid.ItemsSource = _allMods;
        }

        private void ModImage_IsInViewportChanged(object sender, Microsoft.UI.Xaml.DependencyPropertyChangedEventArgs e)
        {
            if (sender is Microsoft.UI.Xaml.Controls.Image img && img.DataContext is ModTile mod)
            {
                bool isInViewport = (bool)e.NewValue;
                mod.IsInViewport = isInViewport;
                if (isInViewport)
                {
                    // Load image
                    if (File.Exists(mod.ImagePath))
                        mod.ImageSource = GetOrCreateBitmapImage(mod.ImagePath, mod.Directory);
                    else
                        mod.ImageSource = GetOrCreateBitmapImage("Assets/placeholder.png");
                }
                else
                {
                    // Set placeholder
                    mod.ImageSource = GetOrCreateBitmapImage("Assets/placeholder.png");
                }
            }
        }

        private void OnModImageIsInViewportChanged(object parameter)
        {
            if (parameter is ModTile mod)
            {
                // Check current IsInViewport state
                if (mod.IsInViewport)
                {
                    if (File.Exists(mod.ImagePath))
                        mod.ImageSource = GetOrCreateBitmapImage(mod.ImagePath, mod.Directory);
                    else
                        mod.ImageSource = GetOrCreateBitmapImage("Assets/placeholder.png");
                }
                else
                {
                    mod.ImageSource = GetOrCreateBitmapImage("Assets/placeholder.png");
                }
            }
        }

        private void LoadActiveModsOnly()
        {
            var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(modLibraryPath)) return;
            var mods = new List<ModTile>();
            foreach (var dir in Directory.GetDirectories(modLibraryPath))
            {
                var modJsonPath = Path.Combine(dir, "mod.json");
                if (!File.Exists(modJsonPath)) continue;
                var dirName = Path.GetFileName(dir);
                if (!_activeMods.TryGetValue(dirName, out var isActive) || !isActive)
                    continue;
                try
                {
                    var json = File.ReadAllText(modJsonPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var name = Path.GetFileName(dir);
                    string previewPath = Path.Combine(dir, "preview.jpg");
                    string imagePath = File.Exists(previewPath) ? previewPath : "Assets/placeholder.png";
                    var modTile = new ModTile { Name = name, ImagePath = imagePath, Directory = dirName, IsActive = true };
                    modTile.ImageSource = GetOrCreateBitmapImage(imagePath, dirName);
                    mods.Add(modTile);
                }
                catch { }
            }
            ModsGrid.ItemsSource = mods;
        }

        public string GetCategoryTitleText()
        {
            return CategoryTitle?.Text ?? string.Empty;
        }

        // Add function to clean double slashes
        private static string CleanPath(string path)
        {
            while (path.Contains("\\\\")) path = path.Replace("\\\\", "\\");
            while (path.Contains("//")) path = path.Replace("//", "/");
            return path;
        }

        // Validate mod directory name for security
        private static bool IsValidModDirectoryName(string? directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName))
                return false;

            // Check for path traversal attempts
            if (directoryName.Contains("..") || directoryName.Contains("/") || directoryName.Contains("\\"))
                return false;

            // Check for absolute path attempts
            if (Path.IsPathRooted(directoryName))
                return false;

            // Check for invalid filename characters
            var invalidChars = Path.GetInvalidFileNameChars();
            if (directoryName.IndexOfAny(invalidChars) >= 0)
                return false;

            // Check for reserved names (Windows)
            var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            if (reservedNames.Contains(directoryName.ToUpperInvariant()))
                return false;

            return true;
        }

        // Static helper for symlink creation
        private static void CreateSymlinkStatic(string linkPath, string targetPath)
        {
            // targetPath powinien by� zawsze pe�n� �cie�k� do katalogu moda w bibliotece mod�w
            // Je�li targetPath jest nazw� katalogu, zbuduj pe�n� �cie�k�
            var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            if (!Directory.Exists(targetPath))
            {
                targetPath = Path.Combine(modLibraryPath, Path.GetFileName(targetPath));
            }
            targetPath = Path.GetFullPath(targetPath);
            CreateSymbolicLink(linkPath, targetPath, 1); // 1 = directory
        }

        // Static helper for symlink check
        public static bool IsSymlinkStatic(string path)
        {
            try
            {
                if (!Directory.Exists(path) && !File.Exists(path))
                    return false;
                    
                var dirInfo = new DirectoryInfo(path);
                return dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to check if path is symlink: {path}", ex);
                return false;
            }
        }



        /// <summary>
        /// Validates and ensures symlinks are properly synchronized with active mods
        /// </summary>
        public static void ValidateAndFixSymlinks()
        {
            try
            {
                Logger.LogInfo("Starting symlink validation and repair");
                
                var modsDir = SettingsManager.Current.XXMIModsDirectory;
                if (string.IsNullOrWhiteSpace(modsDir))
                    modsDir = Path.Combine(AppContext.BaseDirectory, "XXMI", "ZZMI", "Mods");
                
                var modsDirFull = Path.GetFullPath(modsDir);
                var modLibraryPath = SettingsManager.Current.ModLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                
                // Load active mods
                var activeModsPath = Path.Combine(AppContext.BaseDirectory, "Settings", "ActiveMods.json");
                var activeMods = new Dictionary<string, bool>();
                
                if (File.Exists(activeModsPath))
                {
                    try
                    {
                        var json = File.ReadAllText(activeModsPath);
                        activeMods = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to load active mods for validation", ex);
                    }
                }
                
                // Check for orphaned symlinks (symlinks that shouldn't exist)
                if (Directory.Exists(modsDirFull))
                {
                    var existingDirs = Directory.GetDirectories(modsDirFull);
                    foreach (var dir in existingDirs)
                    {
                        if (IsSymlinkStatic(dir))
                        {
                            var dirName = Path.GetFileName(dir);
                            if (!activeMods.ContainsKey(dirName) || !activeMods[dirName])
                            {
                                // This symlink shouldn't exist - remove it
                                try
                                {
                                    Directory.Delete(dir, true);
                                    Logger.LogInfo($"Removed orphaned symlink: {dir}");
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError($"Failed to remove orphaned symlink: {dir}", ex);
                                }
                            }
                        }
                    }
                }
                
                // Check for missing symlinks (active mods without symlinks)
                foreach (var mod in activeMods.Where(m => m.Value))
                {
                    var linkPath = Path.Combine(modsDirFull, mod.Key);
                    var sourcePath = Path.Combine(modLibraryPath, mod.Key);
                    
                    if (!Directory.Exists(linkPath) && Directory.Exists(sourcePath))
                    {
                        // Missing symlink for active mod - create it
                        try
                        {
                            CreateSymlinkStatic(linkPath, sourcePath);
                            Logger.LogInfo($"Created missing symlink: {linkPath} -> {sourcePath}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to create missing symlink: {linkPath}", ex);
                        }
                    }
                    else if (Directory.Exists(linkPath) && !IsSymlinkStatic(linkPath))
                    {
                        // Directory exists but is not a symlink - this is problematic
                        Logger.LogWarning($"Directory exists but is not a symlink: {linkPath}");
                    }
                }
                
                Logger.LogInfo("Symlink validation and repair completed");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to validate and fix symlinks", ex);
            }
        }

        // Instance method for UI refresh (already present, but ensure public)
        public void RefreshUIAfterLanguageChange()
        {
            // Od�wie�enie listy kategorii mod�w w menu nawigacji
            var mainWindow = ((App)Application.Current).MainWindow as ZZZ_Mod_Manager_X.MainWindow;
            if (mainWindow != null)
            {
                _ = mainWindow.GenerateModCharacterMenuAsync();
            }
            // Check mod directories and create mod.json in level 1 directories
            (App.Current as ZZZ_Mod_Manager_X.App)?.EnsureModJsonInModLibrary();
            LoadAllMods();
        }

        // Add function to display path with single slashes
        public static string GetDisplayPath(string path)
        {
            return CleanPath(path);
        }

        // Notify MainWindow to update heart button
        private void NotifyMainWindowToUpdateHeartButton()
        {
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                // Use dispatcher to ensure UI update happens after page is fully loaded
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => 
                {
                    mainWindow.UpdateShowActiveModsButtonIcon();
                });
            }
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool>? _canExecute;
        public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T)parameter!) ?? true;
        public void Execute(object? parameter) => _execute((T)parameter!);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
