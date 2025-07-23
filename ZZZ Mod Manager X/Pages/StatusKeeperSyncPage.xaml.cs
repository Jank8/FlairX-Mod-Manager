using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.Text;

namespace ZZZ_Mod_Manager_X.Pages
{
    public sealed partial class StatusKeeperSyncPage : Page
    {
        private Dictionary<string, string> _lang = new();
        private static FileSystemWatcher? _fileWatcher;
        private static Timer? _periodicSyncTimer;
        private static string? _logFile;
        


        public StatusKeeperSyncPage()
        {
            this.InitializeComponent();
            LoadLanguage();
            UpdateTexts();
            
            // Initialize logging if enabled
            if (SettingsManager.Current.StatusKeeperLoggingEnabled)
            {
                InitFileLogging(GetLogPath());
            }
            
            // Sync disabled message is now displayed in App.xaml.cs
        }

        private void LoadLanguage()
        {
            try
            {
                var langFile = ZZZ_Mod_Manager_X.SettingsManager.Current?.LanguageFile ?? "en.json";
                var langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "StatusKeeper", langFile);
                if (!File.Exists(langPath))
                    langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "StatusKeeper", "en.json");
                
                if (File.Exists(langPath))
                {
                    var json = File.ReadAllText(langPath);
                    _lang = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            }
            catch
            {
                _lang = new Dictionary<string, string>();
            }
        }

        private string T(string key)
        {
            return _lang.TryGetValue(key, out var value) ? value : key;
        }
        
        // Public static version of T() function for use from other classes
        public static string TStatic(string key)
        {
            // Load language dictionary if not already loaded
            var langFile = ZZZ_Mod_Manager_X.SettingsManager.Current?.LanguageFile ?? "en.json";
            var langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "StatusKeeper", langFile);
            if (!File.Exists(langPath))
                langPath = Path.Combine(System.AppContext.BaseDirectory, "Language", "StatusKeeper", "en.json");
            
            Dictionary<string, string> lang = new();
            if (File.Exists(langPath))
            {
                try
                {
                    var json = File.ReadAllText(langPath);
                    lang = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
                catch
                {
                    // Ignore errors
                }
            }
            
            return lang.TryGetValue(key, out var value) ? value : key;
        }

        private void UpdateTexts()
        {
            D3dxFilePathLabel.Text = T("StatusKeeper_D3dxFilePath_Label");
            DynamicSyncLabel.Text = T("StatusKeeper_DynamicSync_Label");
            BackupConfirmationLabel.Text = T("StatusKeeper_BackupConfirmation_Label");
            ManualSyncLabel.Text = T("StatusKeeper_ManualSync_Label");
            ManualSyncButtonText.Text = T("StatusKeeper_ManualSync_Button");

            
            // Setting tooltips from language files
            ToolTipService.SetToolTip(D3dxFilePathPickButton, T("StatusKeeper_Tooltip_D3dxFilePath"));
            ToolTipService.SetToolTip(D3dxFilePathDefaultButton, T("StatusKeeper_Tooltip_RestoreDefault"));
            ToolTipService.SetToolTip(DynamicSyncToggle, T("StatusKeeper_Tooltip_DynamicSync"));
            ToolTipService.SetToolTip(ManualSyncButton, T("StatusKeeper_Tooltip_ManualSync"));
        }



        // Method for saving the logging setting, call it where you want to change StatusKeeperLoggingEnabled
        private void SetLoggingEnabled(bool enabled)
        {
            SettingsManager.Current.StatusKeeperLoggingEnabled = enabled;
            SettingsManager.Save();
        }

        private void InitializeBreadcrumbBar()
        {
            var defaultPath = string.IsNullOrEmpty(SettingsManager.Current.StatusKeeperD3dxUserIniPath) 
                ? Path.Combine(".", "XXMI", "ZZMI", "d3dx_user.ini")
                : SettingsManager.Current.StatusKeeperD3dxUserIniPath;
            
            if (string.IsNullOrEmpty(SettingsManager.Current.StatusKeeperD3dxUserIniPath))
            {
                SettingsManager.Current.StatusKeeperD3dxUserIniPath = defaultPath;
                SettingsManager.Save();
            }
            SetBreadcrumbBar(D3dxFilePathBreadcrumb, SettingsManager.Current.StatusKeeperD3dxUserIniPath);
        }

        private void SetBreadcrumbBar(BreadcrumbBar bar, string path)
        {
            var items = new List<object>();
            if (path == "." || string.IsNullOrWhiteSpace(path))
            {
                items.Add(new FontIcon { Glyph = "\uE80F" });
            }
            else
            {
                items.Add(new FontIcon { Glyph = "\uE80F" });
                var segments = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var seg in segments)
                    items.Add(seg);
            }
            bar.ItemsSource = items;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            // Fast UI initialization - only read from settings (no I/O operations)
            BackupConfirmationToggle.IsOn = SettingsManager.Current.StatusKeeperBackupConfirmed;
            DynamicSyncToggle.IsOn = SettingsManager.Current.StatusKeeperDynamicSyncEnabled;
            InitializeBreadcrumbBar();
            
            // Update button states
            DynamicSyncToggle.IsEnabled = BackupConfirmationToggle.IsOn;
            ManualSyncButton.IsEnabled = BackupConfirmationToggle.IsOn;
            
            // Check d3dx_user.ini in background
            _ = Task.Run(() =>
            {
                var d3dxUserPath = GetD3dxUserPathStatic();
                if (string.IsNullOrEmpty(d3dxUserPath) && DynamicSyncToggle.IsOn)
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        DynamicSyncToggle.IsOn = false;
                        SettingsManager.Current.StatusKeeperDynamicSyncEnabled = false;
                        SettingsManager.Save();
                    });
                    
                    this.DispatcherQueue.TryEnqueue(async () =>
                    {
                        await ShowInfoDialog(T("StatusKeeper_D3dxMissing_Title"), T("StatusKeeper_D3dxMissing_Message"));
                    });
                }
            });
            
            // Move heavy operations to the background to avoid blocking the UI
            _ = Task.Run(() =>
            {
                try
                {
                    
                    // Check backup status in the background (only if sync is enabled)
                    if (DynamicSyncToggle.IsOn)
                    {
                        ValidateAndHandleSyncState();
                    }
                }
                catch (Exception ex)
                {
                    LogStatic($"Background validation error: {ex.Message}", "ERROR");
                }
            });
        }
        
        private void ValidateAndHandleSyncState()
        {
            // Check d3dx_user.ini
            var d3dxUserPath = GetD3dxUserPathStatic();
            if (string.IsNullOrEmpty(d3dxUserPath))
            {
                // Disable sync on UI thread
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    DynamicSyncToggle.IsOn = false;
                    SettingsManager.Current.StatusKeeperDynamicSyncEnabled = false;
                    SettingsManager.Save();
                });
                
                // Show message
                this.DispatcherQueue.TryEnqueue(async () =>
                {
                    await ShowInfoDialog(T("StatusKeeper_D3dxMissing_Title"), T("StatusKeeper_D3dxMissing_Message"));
                });
                return;
            }
            
            // Perform automatic synchronization in the background
            if (DynamicSyncToggle.IsOn)
            {
                _ = SyncPersistentVariables();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            
            // Clear any heavy operations or timers to improve navigation performance
            // Note: We don't stop the static watcher/timer here as they should persist across navigation
            LogStatic("OnNavigatedFrom called - cleaning up page resources", "DEBUG");
        }



        private async void D3dxFilePathPickButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle((App.Current as App)?.MainWindow);
                var folderPath = await PickFolderWin32DialogSTA(hwnd);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var iniPath = Path.Combine(folderPath, "d3dx_user.ini");
                    if (File.Exists(iniPath))
                    {
                        SettingsManager.Current.StatusKeeperD3dxUserIniPath = iniPath;
                        SetBreadcrumbBar(D3dxFilePathBreadcrumb, iniPath);
                        SettingsManager.Save();
                        LogStatic($"D3DX User INI path set to: {iniPath}");

                        // Restart watcher if dynamic sync is enabled
                        if (DynamicSyncToggle.IsOn)
                        {
                            StopWatcher();
                            StopPeriodicSync();
                            StartWatcher();
                            StartPeriodicSync();
                        }
                    }
                    else
                    {
                        LogStatic($"d3dx_user.ini not found in {folderPath}", "ERROR");
                        var dialog = new ContentDialog
                        {
                            Title = "Error",
                            Content = "d3dx_user.ini not found in the selected directory.",
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await dialog.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                LogStatic($"Error picking directory: {ex.Message}", "ERROR");
            }
        }

        private void D3dxFilePathDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            var defaultPath = Path.Combine(".", "XXMI", "ZZMI", "d3dx_user.ini");
            SettingsManager.Current.StatusKeeperD3dxUserIniPath = defaultPath;
            SetBreadcrumbBar(D3dxFilePathBreadcrumb, defaultPath);
            SettingsManager.Save();
            LogStatic($"D3DX User INI path reset to default: {defaultPath}");

            // Restart watcher if dynamic sync is enabled
            if (DynamicSyncToggle.IsOn)
            {
                StopWatcher();
                StopPeriodicSync();
                StartWatcher();
                StartPeriodicSync();
            }
        }

        private void BackupConfirmationToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.StatusKeeperBackupConfirmed = BackupConfirmationToggle.IsOn;
            SettingsManager.Save();
            
            // Update button states based on backup confirmation
            UpdateSyncButtonStates();
            
            // If backup is not confirmed and sync is enabled, disable it
            if (!BackupConfirmationToggle.IsOn && DynamicSyncToggle.IsOn)
            {
                DynamicSyncToggle.IsOn = false;
                SettingsManager.Current.StatusKeeperDynamicSyncEnabled = false;
                SettingsManager.Save();
                StopWatcher();
                StopPeriodicSync();
                LogStatic("Dynamic sync disabled because backup confirmation was turned off");
            }
        }

        private void UpdateSyncButtonStates()
        {
            bool backupConfirmed = BackupConfirmationToggle.IsOn;
            
            // Disable sync functionality if backup is not confirmed
            DynamicSyncToggle.IsEnabled = backupConfirmed;
            ManualSyncButton.IsEnabled = backupConfirmed;
        }

        private void DynamicSyncToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // Check if backup is confirmed before allowing sync
            if (DynamicSyncToggle.IsOn && !BackupConfirmationToggle.IsOn)
            {
                DynamicSyncToggle.IsOn = false;
                LogStatic("Cannot enable dynamic sync: Backup not confirmed", "WARNING");
                return;
            }
            
            SettingsManager.Current.StatusKeeperDynamicSyncEnabled = DynamicSyncToggle.IsOn;
            SettingsManager.Save();

            if (DynamicSyncToggle.IsOn)
            {
                var d3dxUserPath = GetD3dxUserPathStatic();
                if (string.IsNullOrEmpty(d3dxUserPath))
                {
                    DynamicSyncToggle.IsOn = false;
                    SettingsManager.Current.StatusKeeperDynamicSyncEnabled = false;
                    SettingsManager.Save();
                    LogStatic("Cannot enable auto-sync: d3dx_user.ini path not set or file not found", "ERROR");
                    _ = ShowInfoDialog(T("StatusKeeper_D3dxMissing_Title"), T("StatusKeeper_D3dxMissing_Message"));
                    return;
                }

                StartWatcher();
                StartPeriodicSync();
                LogStatic("Auto-updater enabled. File monitoring active.");
                // Perform automatic synchronization immediately after enabling
                _ = SyncPersistentVariables();
            }
            else
            {
                StopWatcher();
                StopPeriodicSync();
                LogStatic("Auto-updater disabled. Background monitoring stopped.");
            }
        }

        private async void ManualSyncButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if backup is confirmed before allowing manual sync
            if (!BackupConfirmationToggle.IsOn)
            {
                LogStatic("Cannot sync: Backup not confirmed", "WARNING");
                return;
            }
            
            // Check if the d3dx_user.ini file exists
            var d3dxUserPath = GetD3dxUserPathStatic();
            if (string.IsNullOrEmpty(d3dxUserPath))
            {
                LogStatic("Cannot sync: d3dx_user.ini path not set or file not found", "ERROR");
                await ShowInfoDialog(T("StatusKeeper_D3dxMissing_Title"), T("StatusKeeper_D3dxMissing_Message"));
                return;
            }
            
            try
            {
                ManualSyncButton.IsEnabled = false;
                ManualSyncProgressBar.Visibility = Visibility.Visible;
                ManualSyncButtonText.Text = T("StatusKeeper_Syncing");

                LogStatic("Syncing persistent variables...");

                var result = await SyncPersistentVariables();

                var message = $"Sync complete! Updated {result.updateCount} variables in {result.fileCount} files";
                if (result.lodSyncCount > 0)
                {
                    message += $" and synced [Constants] to {result.lodSyncCount} LOD files";
                }

                LogStatic(message);
                
                // Show completion dialog to user
                await ShowInfoDialog(T("StatusKeeper_ManualSync_Complete_Title"), message);
            }
            catch (Exception error)
            {
                LogStatic($"Sync failed: {error.Message}", "ERROR");
            }
            finally
            {
                ManualSyncProgressBar.Visibility = Visibility.Collapsed;
                ManualSyncButton.IsEnabled = true;
                ManualSyncButtonText.Text = T("StatusKeeper_ManualSync_Button");
            }
        }



        // Simple info dialog (must be before use in code)
        private async Task ShowInfoDialog(string title, string message)
        {
            // Add logging before showing the dialog
            LogStatic($"Showing dialog: {title} - {message}", "DEBUG");
            
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot,
                // Set dialog priority to high so it appears on top
                DefaultButton = ContentDialogButton.Close
            };
            
            try
            {
                await dialog.ShowAsync();
                // Add logging after closing the dialog
                LogStatic($"Dialog closed: {title}", "DEBUG");
            }
            catch (Exception ex)
            {
                LogStatic($"Error showing dialog: {ex.Message}", "ERROR");
            }
        }

        // ==================== LOGGING SYSTEM ====================
        
        private void InitFileLogging(string logPath)
        {
            _logFile = logPath;
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd | HH:mm:ss");
            
            // Ensure Settings directory exists
            var settingsDir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(settingsDir) && !Directory.Exists(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }
            
            File.WriteAllText(_logFile, $"=== ModStatusKeeper Log Started at {timestamp} ===\n", System.Text.Encoding.UTF8);
        }

        private string GetLogPath()
        {
            return Path.Combine(AppContext.BaseDirectory, "Settings", "StatusKeeper.log");
        }

        private static void LogStatic(string message, string level = "INFO")
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd | HH:mm:ss");
            var logMessage = $"[{timestamp}] [{level}] {message}\n";

            // Always log to console for real-time debugging
            Debug.WriteLine(message);

            // Only log to file if logging is enabled and file is initialized
            if (_logFile != null && SettingsManager.Current.StatusKeeperLoggingEnabled)
            {
                try
                {
                    File.AppendAllText(_logFile, logMessage, System.Text.Encoding.UTF8);
                }
                catch (Exception error)
                {
                    Debug.WriteLine($"Failed to write to log file: {error}");
                }
            }
        }



        // ==================== D3DX USER INI PATH MANAGEMENT ====================
        
        private static string? GetD3dxUserPathStatic()
        {
            var defaultPath = SettingsManager.Current.StatusKeeperD3dxUserIniPath;
            if (string.IsNullOrEmpty(defaultPath))
            {
                LogStatic("d3dx_user.ini path is not set, attempting to find it automatically...", "WARNING");
            }
            
            if (!string.IsNullOrEmpty(defaultPath) && File.Exists(defaultPath))
            {
                return defaultPath;
            }

            // Try to find d3dx_user.ini automatically
            var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? 
                                Path.Combine(AppContext.BaseDirectory, "ModLibrary");
            var xxmiModsPath = ZZZ_Mod_Manager_X.SettingsManager.Current.XXMIModsDirectory ?? 
                              Path.Combine(AppContext.BaseDirectory, "XXMI", "ZZMI", "Mods");

            var tryPaths = new string[]
            {
                Path.Combine(Path.GetDirectoryName(xxmiModsPath) ?? string.Empty, "d3dx_user.ini"),
                Path.Combine(Path.GetDirectoryName(modLibraryPath) ?? string.Empty, "XXMI", "ZZMI", "d3dx_user.ini"),
            };

            foreach (var p in tryPaths)
            {
                LogStatic($"Checking for d3dx_user.ini at: {p}");
                if (File.Exists(p))
                {
                    LogStatic($"Found d3dx_user.ini at: {p}");
                    SettingsManager.Current.StatusKeeperD3dxUserIniPath = p;
                    SettingsManager.Save();
                    // Note: No SetBreadcrumbBar here since static and no UI instance
                    return p;
                }
            }

            LogStatic($"d3dx_user.ini not found at any expected location: {string.Join(", ", tryPaths)}", "ERROR");
            return null;
        }

        // ==================== PERSISTENT VARIABLES PARSING ====================
        
        private static Dictionary<string, Dictionary<string, string>> ParsePersistentVariables()
        {
            var d3dxUserPath = GetD3dxUserPathStatic();
            
            LogStatic($"Looking for d3dx_user.ini at: {d3dxUserPath}");

            if (string.IsNullOrEmpty(d3dxUserPath) || !File.Exists(d3dxUserPath))
            {
                LogStatic($"d3dx_user.ini not found at: {d3dxUserPath}", "ERROR");
                throw new Exception("d3dx_user.ini not found");
            }

            var content = File.ReadAllText(d3dxUserPath, System.Text.Encoding.UTF8);
            LogStatic($"Successfully read d3dx_user.ini ({content.Length} characters)");

            var allEntries = new Dictionary<string, Dictionary<string, string>>();
            var lines = content.Split('\n');
            LogStatic($"Parsing {lines.Length} lines from d3dx_user.ini");

            // Build namespace to files mapping for new format
            var namespaceToFiles = BuildNamespaceMapping();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";")) continue;

                // OLD FORMAT: $\mods\<path>\<file>.ini\<variable> = <value>
                var oldMatch = Regex.Match(trimmedLine, @"^\$\\mods\\(.+\.ini)\\([^=]+?)\s*=\s*(.+)$");
                if (oldMatch.Success)
                {
                    var fullIniPath = oldMatch.Groups[1].Value;
                    var varName = oldMatch.Groups[2].Value.Trim();
                    var value = oldMatch.Groups[3].Value.Trim();

                    LogStatic($"✅ OLD FORMAT: {fullIniPath} -> {varName} = {value}");

                    if (!allEntries.ContainsKey(fullIniPath))
                    {
                        allEntries[fullIniPath] = new Dictionary<string, string>();
                    }
                    allEntries[fullIniPath][varName] = value;
                    continue;
                }

                // NEW FORMAT: $\<namespace>\<variable> = <value>
                var newMatch = Regex.Match(trimmedLine, @"^\$\\([^\\]+(?:\\[^\\]+)*)\\([^=]+?)\s*=\s*(.+)$");
                if (newMatch.Success)
                {
                    var namespacePath = newMatch.Groups[1].Value;
                    var varName = newMatch.Groups[2].Value.Trim();
                    var value = newMatch.Groups[3].Value.Trim();

                    LogStatic($"✅ NEW FORMAT: namespace={namespacePath} -> {varName} = {value}");

                    // Find the .ini files that have this namespace
                    if (namespaceToFiles.TryGetValue(namespacePath, out var iniFilePaths))
                    {
                        LogStatic($"  Mapped to files: {string.Join(", ", iniFilePaths)}");
                        
                        // Add variable to ALL files with this namespace
                        foreach (var iniFilePath in iniFilePaths)
                        {
                            if (!allEntries.ContainsKey(iniFilePath))
                            {
                                allEntries[iniFilePath] = new Dictionary<string, string>();
                            }
                            allEntries[iniFilePath][varName] = value;
                        }
                    }
                    else
                    {
                        LogStatic($"⚠️ No .ini files found with namespace: {namespacePath}", "WARN");
                        
                        // Try case-insensitive search
                        var foundKey = namespaceToFiles.Keys.FirstOrDefault(k => 
                            string.Equals(k, namespacePath, StringComparison.OrdinalIgnoreCase));
                        
                        if (foundKey != null)
                        {
                            LogStatic($"  ✅ Found case-insensitive match: '{foundKey}' -> {string.Join(", ", namespaceToFiles[foundKey])}");
                            
                            // Add variable to ALL files with this namespace
                            foreach (var iniFilePath in namespaceToFiles[foundKey])
                            {
                                if (!allEntries.ContainsKey(iniFilePath))
                                {
                                    allEntries[iniFilePath] = new Dictionary<string, string>();
                                }
                                allEntries[iniFilePath][varName] = value;
                            }
                        }
                        else
                        {
                            LogStatic($"  ❌ No case-insensitive match found either", "ERROR");
                        }
                    }
                    continue;
                }

                // Log unmatched lines that look like they should be variables
                if (trimmedLine.StartsWith("$\\") && trimmedLine.Contains("="))
                {
                    LogStatic($"⚠️ Line didn't match any pattern: {trimmedLine}", "WARN");
                }
            }

            LogStatic($"Parsing complete: Found {allEntries.Count} files with persistent variables");
            return allEntries;
        }

        private static Dictionary<string, List<string>> BuildNamespaceMapping()
        {
            var namespaceToFiles = new Dictionary<string, List<string>>();
            
            try
            {
                var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? 
                                   Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                
                if (!Directory.Exists(modLibraryPath))
                {
                    LogStatic($"ModLibrary directory not found: {modLibraryPath}", "WARN");
                    return namespaceToFiles;
                }

                LogStatic($"Building namespace mapping from mod.json files in: {modLibraryPath}");

                // Read namespace info from mod.json files (much faster than scanning .ini files)
                foreach (var modDir in Directory.GetDirectories(modLibraryPath))
                {
                    try
                    {
                        var modJsonPath = Path.Combine(modDir, "mod.json");
                        if (!File.Exists(modJsonPath)) continue;

                        var jsonContent = File.ReadAllText(modJsonPath);
                        using var doc = JsonDocument.Parse(jsonContent);
                        var root = doc.RootElement;

                        // Check if this mod uses namespace sync method
                        if (root.TryGetProperty("syncMethod", out var syncMethodProp) && 
                            syncMethodProp.GetString() == "namespace")
                        {
                            if (root.TryGetProperty("namespaces", out var namespacesProp) && 
                                namespacesProp.ValueKind == JsonValueKind.Array)
                            {
                                var modFolderName = Path.GetFileName(modDir);
                                
                                foreach (var namespaceItem in namespacesProp.EnumerateArray())
                                {
                                    if (namespaceItem.TryGetProperty("namespace", out var namespaceProp) &&
                                        namespaceItem.TryGetProperty("iniFiles", out var iniFilesProp) &&
                                        iniFilesProp.ValueKind == JsonValueKind.Array)
                                    {
                                        var namespacePath = namespaceProp.GetString();
                                        if (string.IsNullOrEmpty(namespacePath)) continue;

                                        var iniFiles = new List<string>();
                                        foreach (var iniFileElement in iniFilesProp.EnumerateArray())
                                        {
                                            var iniFile = iniFileElement.GetString();
                                            if (!string.IsNullOrEmpty(iniFile))
                                            {
                                                var fullIniPath = Path.Combine(modFolderName, iniFile).Replace('\\', '/');
                                                iniFiles.Add(fullIniPath);
                                            }
                                        }

                                        if (iniFiles.Count > 0)
                                        {
                                            namespaceToFiles[namespacePath] = iniFiles;
                                            LogStatic($"  Found namespace: {namespacePath} -> {string.Join(", ", iniFiles)}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogStatic($"Error reading mod.json in {Path.GetFileName(modDir)}: {ex.Message}", "WARN");
                    }
                }

                LogStatic($"Namespace mapping complete: {namespaceToFiles.Count} namespaces found from mod.json files");
            }
            catch (Exception ex)
            {
                LogStatic($"Error building namespace mapping: {ex.Message}", "ERROR");
            }

            return namespaceToFiles;
        }

        // ==================== FILE SYNC OPERATIONS ====================
        
        private static string? ResolvePath(string d3dxPath)
        {
            try
            {
                var modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? 
                                   Path.Combine(AppContext.BaseDirectory, "ModLibrary");
                
                // For new format (namespace), path is already relative from ModLibrary: "ModFolder/KeySwaps.ini"
                // For old format, path needs case-insensitive resolution: "folder\file.ini"
                
                // Try direct path first (new format)
                var directPath = Path.Combine(modLibraryPath, d3dxPath.Replace('/', '\\'));
                if (File.Exists(directPath))
                {
                    return directPath;
                }
                
                // Fall back to case-insensitive resolution (old format)
                var currentPath = Path.GetFullPath(modLibraryPath);
                var pathParts = d3dxPath.Split('\\', '/').Where(part => !string.IsNullOrEmpty(part)).ToArray();

                foreach (var part in pathParts)
                {
                    if (Directory.Exists(currentPath))
                    {
                        var items = Directory.GetFileSystemEntries(currentPath);
                        var found = items.FirstOrDefault(item => 
                            Path.GetFileName(item).Equals(part, StringComparison.OrdinalIgnoreCase));
                        
                        if (found != null)
                        {
                            currentPath = found;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }

                return currentPath;
            }
            catch (Exception error)
            {
                LogStatic($"Error resolving path {d3dxPath}: {error.Message}", "ERROR");
                return null;
            }
        }

        private static (bool updated, int updateCount) UpdateVariablesInFile(string filePath, Dictionary<string, string> variables)
        {
            if (!File.Exists(filePath))
            {
                return (false, 0);
            }

            var content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            // Split ONLY on '\n', do not split on '\r'
            var lines = content.Split('\n');
            var newLines = new List<string>(lines.Length);
            int updateCount = 0;
            bool modified = false;
            bool inConstantsSection = false;

            foreach (var line in lines)
            {
                // Remove trailing '\r' if present
                var cleanLine = line.EndsWith("\r") ? line.Substring(0, line.Length - 1) : line;
                var trimmedLine = cleanLine.Trim();

                // Check for section headers
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2).ToLower();
                    inConstantsSection = sectionName == "constants";
                    newLines.Add(cleanLine);
                    continue;
                }

                // Only update variables if we're in the [Constants] section
                if (inConstantsSection && !string.IsNullOrEmpty(trimmedLine) &&
                    !trimmedLine.StartsWith(";") && trimmedLine.Contains("="))
                {
                    // Match variable assignment (no indentation, no adding new lines)
                    var match = Regex.Match(cleanLine, @"^(.*?\$([^=\s]+))\s*=\s*(.*)$");
                    if (match.Success)
                    {
                        var fullVarDeclaration = match.Groups[1].Value;
                        var varName = match.Groups[2].Value;
                        var currentValue = match.Groups[3].Value.Trim();

                        var matchingVarKey = variables.Keys.FirstOrDefault(key =>
                            key.Equals(varName, StringComparison.OrdinalIgnoreCase));

                        if (matchingVarKey != null)
                        {
                            var newValue = variables[matchingVarKey];
                            if (currentValue != newValue)
                            {
                                newLines.Add($"{fullVarDeclaration} = {newValue}");
                                updateCount++;
                                modified = true;
                                LogStatic($"✅ Updated {varName}: {currentValue} → {newValue} in {Path.GetFileName(filePath)} [Constants]");
                            }
                            else
                            {
                                newLines.Add(cleanLine);
                            }
                            continue;
                        }
                    }
                }

                newLines.Add(cleanLine);
            }

            if (modified)
            {
                // Join using '\n' only (no extra lines)
                var newContent = string.Join("\n", newLines);
                File.WriteAllText(filePath, newContent, System.Text.Encoding.UTF8);
            }

            return (modified, updateCount);
        }

        private static bool UpdateConstantsSection(string iniPath, Dictionary<string, string> constants)
        {
            if (!File.Exists(iniPath))
            {
                LogStatic($"File not found: {iniPath}", "WARN");
                return false;
            }

            var content = File.ReadAllText(iniPath, System.Text.Encoding.UTF8);
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var newLines = new List<string>();
            bool inConstantsSection = false;
            bool constantsSectionFound = false;
            bool constantsWereUpdated = false;
            var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();

                    if (inConstantsSection)
                    {
                        // Add any remaining constants that were not present in the file
                        foreach (var kvp in constants)
                        {
                            if (!usedKeys.Contains(kvp.Key))
                            {
                                newLines.Add($"    {kvp.Key} = {kvp.Value}");
                                constantsWereUpdated = true;
                            }
                        }
                        inConstantsSection = false;
                    }

                    if (sectionName.Equals("constants", StringComparison.OrdinalIgnoreCase))
                    {
                        inConstantsSection = true;
                        constantsSectionFound = true;
                        newLines.Add(line);
                        continue;
                    }
                }

                if (inConstantsSection && !string.IsNullOrEmpty(trimmedLine) &&
                    !trimmedLine.StartsWith(";") && trimmedLine.Contains("="))
                {
                    // Capture indentation
                    var match = Regex.Match(line, @"^(\s*)(.+?)\s*=\s*(.*)$");
                    if (match.Success)
                    {
                        var indentation = match.Groups[1].Value;
                        var key = match.Groups[2].Value.Trim();
                        var value = match.Groups[3].Value.Trim();
                        usedKeys.Add(key);
                        if (constants.TryGetValue(key, out var newValue))
                        {
                            if (value != newValue)
                            {
                                newLines.Add($"{indentation}{key} = {newValue}");
                                constantsWereUpdated = true;
                            }
                            else
                            {
                                newLines.Add(line);
                            }
                        }
                        else
                        {
                            newLines.Add(line);
                        }
                        continue;
                    }
                }

                newLines.Add(line);
            }

            if (inConstantsSection)
            {
                foreach (var kvp in constants)
                {
                    if (!usedKeys.Contains(kvp.Key))
                    {
                        newLines.Add($"    {kvp.Key} = {kvp.Value}");
                        constantsWereUpdated = true;
                    }
                }
            }

            if (!constantsSectionFound && constants.Count > 0)
            {
                newLines.Add("");
                newLines.Add("[Constants]");
                foreach (var kvp in constants)
                {
                    newLines.Add($"    {kvp.Key} = {kvp.Value}");
                    constantsWereUpdated = true;
                }
            }

            if (constantsWereUpdated)
            {
                var newContent = string.Join("\n", newLines);
                File.WriteAllText(iniPath, newContent, System.Text.Encoding.UTF8);
                LogStatic($"✅ Updated [Constants] section in {Path.GetFileName(iniPath)}");
                return true;
            }

            return false;
        }

        private static Task<(int updateCount, int fileCount, int lodSyncCount)> SyncPersistentVariables()
        {
            var allEntries = ParsePersistentVariables();

            if (allEntries.Count == 0)
            {
                return Task.FromResult((0, 0, 0));
            }

            int updateCount = 0;
            int fileCount = 0;
            var updatedMainFiles = new List<string>();

            foreach (var entry in allEntries)
            {
                var d3dxPath = entry.Key;
                var variables = entry.Value;

                var actualPath = ResolvePath(d3dxPath);
                if (string.IsNullOrEmpty(actualPath) || !File.Exists(actualPath))
                {
                    continue;
                }

                var fileName = Path.GetFileName(actualPath).ToLower();
                var isLodFile = fileName.Contains("_lod") || fileName.Contains("_lod1.ini") || fileName.Contains("_lod2.ini");
                if (isLodFile)
                {
                    LogStatic($"Skipping LOD file {Path.GetFileName(actualPath)} - will be updated via [Constants] sync from main file");
                    continue;
                }

                var result = UpdateVariablesInFile(actualPath, variables);
                if (result.updated)
                {
                    fileCount++;
                    updateCount += result.updateCount;
                    updatedMainFiles.Add(actualPath);
                }
            }

            // Sync [Constants] sections from updated main files to their LOD files
            int lodSyncCount = 0;
            if (updatedMainFiles.Count > 0)
            {
                LogStatic($"Starting LOD sync for {updatedMainFiles.Count} updated main files...");
            }

            foreach (var mainFilePath in updatedMainFiles)
            {
                var constants = ExtractConstantsSection(mainFilePath);
                if (constants == null || constants.Count == 0)
                {
                    LogStatic($"No [Constants] section found in {Path.GetFileName(mainFilePath)}, skipping LOD sync");
                    continue;
                }

                LogStatic($"Found {constants.Count} constants in {Path.GetFileName(mainFilePath)}");

                var mainDir = Path.GetDirectoryName(mainFilePath);
                if (string.IsNullOrEmpty(mainDir)) continue;

                try
                {
                    var dirItems = Directory.GetFiles(mainDir, "*.ini");
                    var lodFiles = dirItems.Where(item =>
                    {
                        var fileName = Path.GetFileName(item).ToLower();
                        return fileName.EndsWith(".ini") && 
                              (fileName.Contains("_lod") || fileName.Contains("_lod1.ini") || fileName.Contains("_lod2.ini"));
                    }).ToArray();

                    if (lodFiles.Length == 0)
                    {
                        LogStatic($"No LOD files found in directory: {Path.GetFileName(mainDir)}");
                    }
                    else
                    {
                        LogStatic($"Found {lodFiles.Length} LOD files in directory: {string.Join(", ", lodFiles.Select(Path.GetFileName))}");
                    }

                    foreach (var lodFile in lodFiles)
                    {
                        if (UpdateConstantsSection(lodFile, constants))
                        {
                            lodSyncCount++;
                            LogStatic($"🔄 Synced [Constants] from {Path.GetFileName(mainFilePath)} to {Path.GetFileName(lodFile)}");
                        }
                        else
                        {
                            LogStatic($"Failed to sync [Constants] to {Path.GetFileName(lodFile)}", "WARN");
                        }
                    }
                }
                catch (Exception error)
                {
                    LogStatic($"Failed to sync LOD files for {Path.GetFileName(mainFilePath)}: {error.Message}", "WARN");
                }
            }

            if (updateCount > 0 || lodSyncCount > 0)
            {
                LogStatic($"🎯 Sync complete: {updateCount} variables updated in {fileCount}/{allEntries.Count} main files");
                if (lodSyncCount > 0)
                {
                    LogStatic($"🔄 LOD sync complete: {lodSyncCount} LOD files updated with [Constants] sections");
                }
            }

            return Task.FromResult((updateCount, fileCount, lodSyncCount));
        }

        // ==================== FILE WATCHER ====================
        
        public static void StartWatcherStatic()
        {
            if (_fileWatcher != null) return;

            var d3dxUserPath = GetD3dxUserPathStatic();
            if (string.IsNullOrEmpty(d3dxUserPath))
            {
                LogStatic("Cannot start watcher: d3dx_user.ini path not found", "ERROR");
                return;
            }

            LogStatic($"Starting file watcher for: {d3dxUserPath}");

            if (!File.Exists(d3dxUserPath))
            {
                LogStatic($"d3dx_user.ini not found for watcher at: {d3dxUserPath}", "ERROR");
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(d3dxUserPath);
                var fileName = Path.GetFileName(d3dxUserPath);

                _fileWatcher = new FileSystemWatcher(directory ?? "", fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Changed += async (sender, e) =>
                {
                    LogStatic("d3dx_user.ini changed, triggering auto-sync...");
                    
                    await Task.Delay(100); // Small delay to avoid blocking
                    
                    try
                    {
                        var result = await SyncPersistentVariables();
                        var logMessage = $"Auto-sync completed: {result.updateCount} variables in {result.fileCount} files";
                        if (result.lodSyncCount > 0)
                        {
                            logMessage += $", {result.lodSyncCount} LOD files synced";
                        }
                        LogStatic(logMessage);
                    }
                    catch (Exception error)
                    {
                        LogStatic($"Auto-sync failed: {error.Message}", "ERROR");
                    }
                };

                LogStatic($"✅ File watcher started successfully for: {d3dxUserPath}");
            }
            catch (Exception error)
            {
                LogStatic($"Failed to start file watcher: {error.Message}", "ERROR");
            }
        }

        public static void StopWatcherStatic()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
                LogStatic("✅ File watcher stopped");
            }
        }
        
        private static void StopWatcher()
        {
            StopWatcherStatic();
        }

        public static void StartPeriodicSyncStatic()
        {
            if (_periodicSyncTimer != null) return;

            LogStatic("Starting periodic sync timer (every 10 seconds)...");

            _periodicSyncTimer = new Timer(async _ =>
            {
                try
                {
                    var d3dxUserPath = GetD3dxUserPathStatic();
                    if (string.IsNullOrEmpty(d3dxUserPath) || !File.Exists(d3dxUserPath))
                    {
                        return;
                    }

                    var result = await SyncPersistentVariables();

                    if (result.updateCount > 0 || result.lodSyncCount > 0)
                    {
                        var logMessage = $"Periodic sync completed: {result.updateCount} variables updated in {result.fileCount} files";
                        if (result.lodSyncCount > 0)
                        {
                            logMessage += $", {result.lodSyncCount} LOD files synced";
                        }
                        LogStatic(logMessage);
                    }
                }
                catch (Exception error)
                {
                    LogStatic($"Periodic sync failed: {error.Message}", "ERROR");
                }
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

            LogStatic("✅ Periodic sync timer started successfully");
        }

        public static void StopPeriodicSyncStatic()
        {
            if (_periodicSyncTimer != null)
            {
                _periodicSyncTimer.Dispose();
                _periodicSyncTimer = null;
                LogStatic("✅ Periodic sync timer stopped");
            }
        }
        
        private static void StopPeriodicSync()
        {
            StopPeriodicSyncStatic();
        }

        // Helper methods to call static watcher/sync methods from instance context
        private void StartWatcher()
        {
            StartWatcherStatic();
        }

        private void StartPeriodicSync()
        {
            StartPeriodicSyncStatic();
        }

        // Win32 API Folder Picker using SHBrowseForFolder
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO bi);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool SHGetPathFromIDList(IntPtr pidl, System.Text.StringBuilder pszPath);

        private const int MAX_PATH = 260;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct BROWSEINFO
        {
            public nint hwndOwner;
            public nint pidlRoot;
            public IntPtr pszDisplayName;
            public string lpszTitle;
            public uint ulFlags;
            public nint lpfn;
            public nint lParam;
            public int iImage;
        }

        private string? PickFolderWin32Dialog(nint hwnd)
        {
            var bi = new BROWSEINFO
            {
                hwndOwner = hwnd,
                lpszTitle = T("PickFolderDialog_Title"),
                ulFlags = 0x00000040 // BIF_NEWDIALOGSTYLE
            };
            IntPtr pidl = SHBrowseForFolder(ref bi);
            if (pidl == IntPtr.Zero)
                return null;
            var sb = new System.Text.StringBuilder(MAX_PATH);
            if (SHGetPathFromIDList(pidl, sb))
                return sb.ToString();
            return null;
        }

        private Task<string?> PickFolderWin32DialogSTA(nint hwnd)
        {
            var tcs = new TaskCompletionSource<string?>();
            var thread = new Thread(() =>
            {
                try
                {
                    var result = PickFolderWin32Dialog(hwnd);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }

        private static Dictionary<string, string>? ExtractConstantsSection(string iniPath)
        {
            if (!File.Exists(iniPath))
            {
                LogStatic($"File not found: {iniPath}", "WARN");
                return null;
            }

            var content = File.ReadAllText(iniPath, System.Text.Encoding.UTF8);
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var constants = new Dictionary<string, string>();
            bool inConstantsSection = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    var sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                    inConstantsSection = sectionName.Equals("constants", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (inConstantsSection && !string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith(";"))
                {
                    var match = Regex.Match(trimmedLine, @"^(.+?)\s*=\s*(.*)$");
                    if (match.Success)
                    {
                        var key = match.Groups[1].Value.Trim();
                        var value = match.Groups[2].Value.Trim();
                        constants[key] = value;
                    }
                }
            }

            return constants.Count > 0 ? constants : null;
        }
    }
}