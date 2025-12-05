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

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class StatusKeeperSyncPage : Page
    {
        private static readonly object _syncLock = new object();
        private static FileSystemWatcher? _fileWatcher;
        private static Timer? _periodicSyncTimer;
        private static string? _logFile;
        


        public StatusKeeperSyncPage()
        {
            this.InitializeComponent();
            UpdateTexts();
            
            // Initialize logging if enabled
            if (SettingsManager.Current.StatusKeeperLoggingEnabled)
            {
                InitFileLogging(GetLogPath());
            }
            
            // Sync disabled message is now displayed in App.xaml.cs
        }

        // Public static version for use from other classes
        public static string TStatic(string key)
        {
            var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
            return SharedUtilities.GetTranslation(lang, key);
        }

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
            
            // File path card
            D3dxFilePathLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxFilePath_Label");
            D3dxFilePathOpenButtonText.Text = SharedUtilities.GetTranslation(lang, "Browse");
            ToolTipService.SetToolTip(D3dxFilePathOpenButton, SharedUtilities.GetTranslation(lang, "StatusKeeper_Tooltip_D3dxFilePath"));
            
            // Backup confirmation card
            BackupConfirmationLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_BackupConfirmation_Label");
            BackupConfirmationDescription.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_BackupConfirmation_Description");
            
            // Dynamic sync card
            DynamicSyncLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_DynamicSync_Label");
            DynamicSyncDescription.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_DynamicSync_Description");
            ToolTipService.SetToolTip(DynamicSyncToggle, SharedUtilities.GetTranslation(lang, "StatusKeeper_Tooltip_DynamicSync"));
            
            // Manual sync card
            ManualSyncLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_ManualSync_Label");
            ManualSyncDescription.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_ManualSync_Description");
            ManualSyncButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_ManualSync_Button");
            ToolTipService.SetToolTip(ManualSyncButton, SharedUtilities.GetTranslation(lang, "StatusKeeper_Tooltip_ManualSync"));
        }
        
        private void UpdateToggleLabels()
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            var onText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_On");
            var offText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_Off");
            
            if (BackupConfirmationToggleLabel != null && BackupConfirmationToggle != null)
                BackupConfirmationToggleLabel.Text = BackupConfirmationToggle.IsOn ? onText : offText;
            if (DynamicSyncToggleLabel != null && DynamicSyncToggle != null)
                DynamicSyncToggleLabel.Text = DynamicSyncToggle.IsOn ? onText : offText;
        }



        // Method for saving the logging setting, call it where you want to change StatusKeeperLoggingEnabled
        private void SetLoggingEnabled(bool enabled)
        {
            SettingsManager.Current.StatusKeeperLoggingEnabled = enabled;
            SettingsManager.Save();
        }

        private void InitializeBreadcrumbBar()
        {
            var currentGame = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var defaultPath = string.IsNullOrEmpty(SettingsManager.Current.StatusKeeperD3dxUserIniPath) 
                ? Path.Combine(".", "XXMI", currentGame, "d3dx_user.ini")
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
            SharedUtilities.SetBreadcrumbBarPath(bar, path);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            // Fast UI initialization - only read from settings (no I/O operations)
            BackupConfirmationToggle.IsOn = SettingsManager.Current.StatusKeeperBackupConfirmed;
            DynamicSyncToggle.IsOn = SettingsManager.Current.StatusKeeperDynamicSyncEnabled;
            InitializeBreadcrumbBar();
            
            // Refresh translations when navigating to this page
            UpdateTexts();
            UpdateToggleLabels();
            
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
                        var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                        await ShowInfoDialog(SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Title"), SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Message"));
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
                    var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                    await ShowInfoDialog(SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Title"), SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Message"));
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



        private void D3dxFilePathOpenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var d3dxUserPath = SettingsManager.Current.StatusKeeperD3dxUserIniPath;
                if (!string.IsNullOrEmpty(d3dxUserPath))
                {
                    var directoryPath = Path.GetDirectoryName(d3dxUserPath);
                    if (!string.IsNullOrEmpty(directoryPath) && Directory.Exists(directoryPath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = directoryPath,
                            UseShellExecute = true
                        });
                        LogStatic($"Opened d3dx_user.ini directory: {directoryPath}");
                    }
                    else
                    {
                        LogStatic($"Directory does not exist: {directoryPath}", "WARNING");
                    }
                }
            }
            catch (Exception ex)
            {
                LogStatic($"Failed to open d3dx_user.ini directory: {ex.Message}", "ERROR");
            }
        }

        private void BackupConfirmationToggle_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateToggleLabels();
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
            UpdateToggleLabels();
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
                    var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                    _ = ShowInfoDialog(SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Title"), SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Message"));
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
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                await ShowInfoDialog(SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Title"), SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Message"));
                return;
            }
            
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                ManualSyncButton.IsEnabled = false;
                ManualSyncProgressBar.Visibility = Visibility.Visible;
                ManualSyncButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_Syncing");

                LogStatic("Syncing persistent variables...");

                var result = await SyncPersistentVariables();

                var message = $"Sync complete! Updated {result.updateCount} variables in {result.fileCount} files";
                if (result.lodSyncCount > 0)
                {
                    message += $" and synced [Constants] to {result.lodSyncCount} LOD files";
                }

                LogStatic(message);
                
                // Show completion dialog to user
                await ShowInfoDialog(SharedUtilities.GetTranslation(lang, "StatusKeeper_ManualSync_Complete_Title"), message);
            }
            catch (Exception error)
            {
                LogStatic($"Sync failed: {error.Message}", "ERROR");
            }
            finally
            {
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                ManualSyncProgressBar.Visibility = Visibility.Collapsed;
                ManualSyncButton.IsEnabled = true;
                ManualSyncButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_ManualSync_Button");
            }
        }



        // Simple info dialog using SharedUtilities
        private async Task ShowInfoDialog(string title, string message)
        {
            // Add logging before showing the dialog
            LogStatic($"Showing dialog: {title} - {message}", "DEBUG");
            
            try
            {
                await SharedUtilities.ShowInfoDialog(title, message, this.XamlRoot);
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
            return PathManager.GetSettingsPath("StatusKeeper.log");
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

            // Try to find d3dx_user.ini automatically using current game settings
            var currentGame = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var modLibraryPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
            
            var xxmiModsPath = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
            

            var tryPaths = new string[]
            {
                Path.Combine(Path.GetDirectoryName(xxmiModsPath) ?? string.Empty, "d3dx_user.ini"),
                Path.Combine(".", "XXMI", currentGame, "d3dx_user.ini"),
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

                    // Extract mod folder name from path (first part before any slash)
                    var modFolderName = fullIniPath.Split('\\', '/')[0];
                    
                    // Check if this mod has StatusKeeper sync enabled
                    if (!IsModSyncEnabled(modFolderName))
                    {
                        LogStatic($"⏭️ OLD FORMAT: Skipping {fullIniPath} -> {varName} (mod sync disabled)");
                        continue;
                    }

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

                    // Find the .ini files that have this namespace
                    if (namespaceToFiles.TryGetValue(namespacePath, out var iniFilePaths))
                    {
                        LogStatic($"✅ NEW FORMAT: namespace={namespacePath} -> {varName} = {value}");
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
                        // Try case-insensitive search
                        var foundKey = namespaceToFiles.Keys.FirstOrDefault(k => 
                            string.Equals(k, namespacePath, StringComparison.OrdinalIgnoreCase));
                        
                        if (foundKey != null)
                        {
                            LogStatic($"✅ NEW FORMAT: namespace={namespacePath} -> {varName} = {value}");
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
                            // Namespace not found - could be disabled mod, missing mod, or outdated entry
                            // Only log at DEBUG level to avoid spam
                            LogStatic($"⏭️ Skipped namespace '{namespacePath}' (mod not found or sync disabled)", "DEBUG");
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

        private static bool IsModSyncEnabled(string modFolderName)
        {
            try
            {
                var modLibraryPath = SharedUtilities.GetSafeXXMIModsPath();
                
                if (!Directory.Exists(modLibraryPath))
                {
                    return true; // Default to enabled if we can't check
                }

                // Search through category directories for the mod
                foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
                {
                    var modDir = Path.Combine(categoryDir, modFolderName);
                    if (Directory.Exists(modDir))
                    {
                        var modJsonPath = Path.Combine(modDir, "mod.json");
                        if (File.Exists(modJsonPath))
                        {
                            var jsonContent = File.ReadAllText(modJsonPath);
                            using var doc = JsonDocument.Parse(jsonContent);
                            var root = doc.RootElement;
                            
                            // Check if StatusKeeper sync is disabled
                            if (root.TryGetProperty("statusKeeperSync", out var syncProp) && 
                                syncProp.ValueKind == JsonValueKind.False)
                            {
                                return false;
                            }
                        }
                        return true; // Found mod, sync enabled (or not specified = default true)
                    }
                }
                
                return true; // Mod not found, default to enabled
            }
            catch (Exception ex)
            {
                LogStatic($"Error checking mod sync status for {modFolderName}: {ex.Message}", "WARN");
                return true; // Default to enabled on error
            }
        }

        private static Dictionary<string, List<string>> BuildNamespaceMapping()
        {
            var namespaceToFiles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            
            try
            {
                // Use SharedUtilities to get mod library path
                var modLibraryPath = SharedUtilities.GetSafeXXMIModsPath();
                
                if (!Directory.Exists(modLibraryPath))
                {
                    LogStatic($"ModLibrary directory not found: {modLibraryPath}", "WARN");
                    return namespaceToFiles;
                }

                LogStatic($"Building namespace mapping from mod.json files in: {modLibraryPath}");

                // Read namespace info from mod.json files (much faster than scanning .ini files)
                // Process category directories (1st level) and mod directories (2nd level)
                foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    
                    foreach (var modDir in Directory.GetDirectories(categoryDir))
                    {
                        // Skip DISABLED_ directories (inactive mods)
                        var modDirName = Path.GetFileName(modDir);
                        if (modDirName.StartsWith("DISABLED_", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        
                        try
                        {
                            var modJsonPath = Path.Combine(modDir, "mod.json");
                            if (!File.Exists(modJsonPath)) continue;

                        var jsonContent = File.ReadAllText(modJsonPath);
                        using var doc = JsonDocument.Parse(jsonContent);
                        var root = doc.RootElement;
                        
                        // Check if StatusKeeper sync is enabled for this mod
                        if (root.TryGetProperty("statusKeeperSync", out var syncProp) && 
                            syncProp.ValueKind == JsonValueKind.False)
                        {
                            LogStatic($"⏭️ Skipped: {modDirName} (sync disabled by user)");
                            continue;
                        }

                        var modFolderName = Path.GetFileName(modDir);
                        
                        // Check if this mod uses namespace sync method
                        if (root.TryGetProperty("syncMethod", out var syncMethodProp) && 
                            syncMethodProp.GetString() == "namespace")
                        {
                            if (root.TryGetProperty("namespaces", out var namespacesProp) && 
                                namespacesProp.ValueKind == JsonValueKind.Array)
                            {
                                bool needsUpdate = false;
                                var updatedNamespaces = new List<(string namespacePath, List<string> iniFiles)>();
                                
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
                                            // Validate namespace against actual .ini files
                                            var actualNamespace = ValidateNamespaceInIniFiles(modDir, iniFiles);
                                            
                                            if (string.IsNullOrEmpty(actualNamespace))
                                            {
                                                // Namespace no longer exists in .ini files - author switched to classic format
                                                LogStatic($"🔄 Auto-updating {modFolderName}: namespace removed from .ini files - converting to classic format", "INFO");
                                                ConvertModJsonToClassicSync(modJsonPath);
                                                needsUpdate = false; // Skip further processing for this mod
                                                break; // Exit namespace loop
                                            }
                                            else if (!actualNamespace.Equals(namespacePath, StringComparison.OrdinalIgnoreCase))
                                            {
                                                LogStatic($"⚠️ Namespace mismatch in {modFolderName}: mod.json has '{namespacePath}' but .ini files use '{actualNamespace}' - updating mod.json", "WARN");
                                                updatedNamespaces.Add((actualNamespace, iniFiles));
                                                needsUpdate = true;
                                            }
                                            else
                                            {
                                                updatedNamespaces.Add((namespacePath, iniFiles));
                                            }
                                            
                                            namespaceToFiles[namespacePath] = iniFiles;
                                            LogStatic($"  Found namespace: {namespacePath} -> {string.Join(", ", iniFiles)}");
                                        }
                                    }
                                }
                                
                                // Update mod.json if namespace mismatch was detected
                                if (needsUpdate)
                                {
                                    UpdateModJsonNamespaces(modJsonPath, updatedNamespaces);
                                }
                            }
                        }
                        else
                        {
                            // Mod doesn't have namespace sync method - check if .ini files have namespace declarations
                            // This handles mods that were updated from classic to namespace format
                            var (detectedNamespace, iniFilesWithNamespace) = DetectNamespaceInModWithFiles(modDir, modFolderName);
                            if (!string.IsNullOrEmpty(detectedNamespace) && iniFilesWithNamespace.Count > 0)
                            {
                                LogStatic($"� Auteo-updating {modFolderName}: detected namespace '{detectedNamespace}' - converting from classic to namespace sync method", "INFO");
                                
                                // Update mod.json to use namespace sync method
                                ConvertModJsonToNamespaceSync(modJsonPath, detectedNamespace, iniFilesWithNamespace);
                                
                                // Add to namespace mapping
                                namespaceToFiles[detectedNamespace] = iniFilesWithNamespace;
                                LogStatic($"  ✅ Converted to namespace sync: {detectedNamespace} -> {string.Join(", ", iniFilesWithNamespace)}");
                            }
                        }
                    }
                        catch (Exception ex)
                        {
                            LogStatic($"Error reading mod.json in {Path.GetFileName(modDir)}: {ex.Message}", "WARN");
                        }
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
                // Use XXMI Mods directory
                var xxmiModsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                
                
                var currentGame = SettingsManager.CurrentSelectedGame;
                LogStatic($"DEBUG: Current game: '{currentGame}', XXMI Mods: '{xxmiModsPath}'");
                LogStatic($"DEBUG: Resolving path '{d3dxPath}' from XXMI Mods: '{xxmiModsPath}'");
                
                // Try direct path first
                var directPath = Path.Combine(xxmiModsPath, d3dxPath.Replace('/', '\\'));
                LogStatic($"DEBUG: Trying direct path: '{directPath}'");
                if (File.Exists(directPath))
                {
                    LogStatic($"DEBUG: Found direct path: '{directPath}'");
                    return directPath;
                }
                
                // Fall back to case-insensitive resolution
                var currentPath = Path.GetFullPath(xxmiModsPath);
                var pathParts = d3dxPath.Split('\\', '/').Where(part => !string.IsNullOrEmpty(part)).ToArray();
                LogStatic($"DEBUG: Fallback search, path parts: [{string.Join(", ", pathParts)}]");

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
                            LogStatic($"DEBUG: Found part '{part}' -> '{currentPath}'");
                        }
                        else
                        {
                            LogStatic($"DEBUG: Part '{part}' not found in '{currentPath}'");
                            LogStatic($"DEBUG: Available items in '{currentPath}': [{string.Join(", ", Directory.GetFileSystemEntries(currentPath).Select(Path.GetFileName))}]");
                            return null;
                        }
                    }
                    else
                    {
                        LogStatic($"DEBUG: Directory does not exist: '{currentPath}'");
                        return null;
                    }
                }

                LogStatic($"DEBUG: Final resolved path: '{currentPath}'");
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
            LogStatic($"DEBUG: UpdateVariablesInFile called for: '{filePath}'");
            LogStatic($"DEBUG: Variables to update: [{string.Join(", ", variables.Select(kv => $"{kv.Key}={kv.Value}"))}]");
            
            if (!File.Exists(filePath))
            {
                LogStatic($"DEBUG: File does not exist: '{filePath}'");
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

        private static (string? namespaceName, List<string> iniFiles) DetectNamespaceInModWithFiles(string modDir, string modFolderName)
        {
            try
            {
                string? detectedNamespace = null;
                var iniFilesWithNamespace = new List<string>();
                
                // Scan all .ini files in mod directory for namespace declaration
                var iniFiles = Directory.GetFiles(modDir, "*.ini", SearchOption.AllDirectories);
                foreach (var iniPath in iniFiles)
                {
                    var content = File.ReadAllText(iniPath, System.Text.Encoding.UTF8);
                    var match = System.Text.RegularExpressions.Regex.Match(content, @"^\s*namespace\s*=\s*(.+)$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var namespaceName = match.Groups[1].Value.Trim();
                        
                        // First namespace found becomes the detected namespace
                        if (detectedNamespace == null)
                        {
                            detectedNamespace = namespaceName;
                        }
                        
                        // Only add files that use the same namespace
                        if (namespaceName.Equals(detectedNamespace, StringComparison.OrdinalIgnoreCase))
                        {
                            // Get relative path from mod directory
                            var relativePath = Path.GetRelativePath(modDir, iniPath).Replace('\\', '/');
                            var fullIniPath = Path.Combine(modFolderName, relativePath).Replace('\\', '/');
                            iniFilesWithNamespace.Add(relativePath);
                        }
                    }
                }
                
                return (detectedNamespace, iniFilesWithNamespace);
            }
            catch (Exception ex)
            {
                LogStatic($"Error detecting namespace in mod: {ex.Message}", "DEBUG");
                return (null, new List<string>());
            }
        }
        
        private static void ConvertModJsonToClassicSync(string modJsonPath)
        {
            try
            {
                Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                {
                    var jsonContent = await File.ReadAllTextAsync(modJsonPath);
                    using var doc = JsonDocument.Parse(jsonContent);
                    var root = doc.RootElement;
                    
                    // Rebuild mod.json without namespace sync method
                    var dict = new Dictionary<string, object?>();
                    foreach (var prop in root.EnumerateObject())
                    {
                        // Remove namespace-related fields
                        if (prop.Name == "syncMethod" || prop.Name == "namespaces")
                            continue;
                        
                        dict[prop.Name] = prop.Value.Deserialize<object>();
                    }
                    
                    var newJson = System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
                    await File.WriteAllTextAsync(modJsonPath, newJson, System.Text.Encoding.UTF8);
                    LogStatic($"✅ Converted mod.json to classic format (removed namespace sync): {modJsonPath}");
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                LogStatic($"Error converting mod.json to classic format: {ex.Message}", "ERROR");
            }
        }
        
        private static void ConvertModJsonToNamespaceSync(string modJsonPath, string namespaceName, List<string> iniFiles)
        {
            try
            {
                Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                {
                    var jsonContent = await File.ReadAllTextAsync(modJsonPath);
                    using var doc = JsonDocument.Parse(jsonContent);
                    var root = doc.RootElement;
                    
                    // Rebuild mod.json with namespace sync method
                    var dict = new Dictionary<string, object?>();
                    foreach (var prop in root.EnumerateObject())
                    {
                        // Skip old syncMethod if it exists
                        if (prop.Name == "syncMethod")
                            continue;
                        
                        dict[prop.Name] = prop.Value.Deserialize<object>();
                    }
                    
                    // Add namespace sync method
                    dict["syncMethod"] = "namespace";
                    dict["namespaces"] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            ["namespace"] = namespaceName,
                            ["iniFiles"] = iniFiles
                        }
                    };
                    
                    var newJson = System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
                    await File.WriteAllTextAsync(modJsonPath, newJson, System.Text.Encoding.UTF8);
                    LogStatic($"✅ Converted mod.json to namespace sync method: {modJsonPath}");
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                LogStatic($"Error converting mod.json to namespace sync: {ex.Message}", "ERROR");
            }
        }
        
        private static string? ValidateNamespaceInIniFiles(string modDir, List<string> iniFiles)
        {
            try
            {
                // Check first .ini file for namespace declaration
                var firstIniFile = iniFiles.FirstOrDefault();
                if (string.IsNullOrEmpty(firstIniFile)) return null;
                
                var iniPath = Path.Combine(modDir, firstIniFile.Replace('/', '\\'));
                if (!File.Exists(iniPath)) return null;
                
                var content = File.ReadAllText(iniPath, System.Text.Encoding.UTF8);
                
                // Look for namespace declaration: namespace = <value>
                var match = System.Text.RegularExpressions.Regex.Match(content, @"^\s*namespace\s*=\s*(.+)$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
                
                return null;
            }
            catch (Exception ex)
            {
                LogStatic($"Error validating namespace in .ini files: {ex.Message}", "WARN");
                return null;
            }
        }
        
        private static void UpdateModJsonNamespaces(string modJsonPath, List<(string namespacePath, List<string> iniFiles)> namespaces)
        {
            try
            {
                Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                {
                    var jsonContent = await File.ReadAllTextAsync(modJsonPath);
                    using var doc = JsonDocument.Parse(jsonContent);
                    var root = doc.RootElement;
                    
                    // Rebuild mod.json with updated namespaces
                    var dict = new Dictionary<string, object?>();
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Name == "namespaces")
                        {
                            // Replace with updated namespaces
                            var namespacesArray = namespaces.Select(ns => new Dictionary<string, object>
                            {
                                ["namespace"] = ns.namespacePath,
                                ["iniFiles"] = ns.iniFiles
                            }).ToList();
                            dict["namespaces"] = namespacesArray;
                        }
                        else
                        {
                            dict[prop.Name] = prop.Value.Deserialize<object>();
                        }
                    }
                    
                    var newJson = System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
                    await File.WriteAllTextAsync(modJsonPath, newJson, System.Text.Encoding.UTF8);
                    LogStatic($"✅ Updated mod.json with corrected namespaces: {modJsonPath}");
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                LogStatic($"Error updating mod.json namespaces: {ex.Message}", "ERROR");
            }
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