using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        public Window? MainWindow => _window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            RequireAdmin();
            InitializeComponent();
            // Register WinUI 3 system notifications
            AppNotificationManager.Default.Register();
            // Remaining logic moved to OnLaunched
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            SettingsManager.Load(); // Load settings before creating window
            // AUTOMATIC LANGUAGE DETECTION on first start
            var langFile = SettingsManager.Current.LanguageFile;
            if (string.IsNullOrEmpty(langFile) || langFile == "auto")
            {
                var systemCulture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                var languageFolder = PathManager.GetAbsolutePath("Language");
                var available = System.IO.Directory.Exists(languageFolder)
                    ? System.IO.Directory.GetFiles(languageFolder, "*.json").Select(f => System.IO.Path.GetFileName(f)).ToList()
                    : new List<string>();
                langFile = available.FirstOrDefault(f => f.StartsWith(systemCulture, StringComparison.OrdinalIgnoreCase)) ?? "en.json";
                SettingsManager.Current.LanguageFile = langFile;
                SettingsManager.Save();
            }
            // Set culture and font for Asian and RTL languages
            if (langFile.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("zh-CN");
                System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("zh-CN");
                var chineseFont = Application.Current.Resources["ChineseFont"] as Microsoft.UI.Xaml.Media.FontFamily;
                if (chineseFont != null)
                    Application.Current.Resources["AppFontFamily"] = chineseFont;
            }
            else if (langFile.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ja-JP");
                System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("ja-JP");
                var japaneseFont = Application.Current.Resources["JapaneseFont"] as Microsoft.UI.Xaml.Media.FontFamily;
                if (japaneseFont != null)
                    Application.Current.Resources["AppFontFamily"] = japaneseFont;
            }
            else if (langFile.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ko-KR");
                System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("ko-KR");
                var koreanFont = Application.Current.Resources["KoreanFont"] as Microsoft.UI.Xaml.Media.FontFamily;
                if (koreanFont != null)
                    Application.Current.Resources["AppFontFamily"] = koreanFont;
            }
            else if (langFile.StartsWith("ar", StringComparison.OrdinalIgnoreCase))
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
                System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("ar-SA");
                var arabicFont = Application.Current.Resources["ArabicFont"] as Microsoft.UI.Xaml.Media.FontFamily;
                if (arabicFont != null)
                    Application.Current.Resources["AppFontFamily"] = arabicFont;
            }
            else if (langFile.StartsWith("he", StringComparison.OrdinalIgnoreCase))
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("he-IL");
                System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("he-IL");
                var hebrewFont = Application.Current.Resources["HebrewFont"] as Microsoft.UI.Xaml.Media.FontFamily;
                if (hebrewFont != null)
                    Application.Current.Resources["AppFontFamily"] = hebrewFont;
            }
            else if (langFile.StartsWith("hi", StringComparison.OrdinalIgnoreCase))
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("hi-IN");
                System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("hi-IN");
                var hindiFont = Application.Current.Resources["HindiFont"] as Microsoft.UI.Xaml.Media.FontFamily;
                if (hindiFont != null)
                    Application.Current.Resources["AppFontFamily"] = hindiFont;
            }
            else if (langFile.StartsWith("th", StringComparison.OrdinalIgnoreCase))
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("th-TH");
                System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("th-TH");
                var thaiFont = Application.Current.Resources["ThaiFont"] as Microsoft.UI.Xaml.Media.FontFamily;
                if (thaiFont != null)
                    Application.Current.Resources["AppFontFamily"] = thaiFont;
            }
            else if (langFile.StartsWith("szl", StringComparison.OrdinalIgnoreCase))
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("szl");
                System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("szl");
                var silesianFont = new Microsoft.UI.Xaml.Media.FontFamily("ms-appx:///Assets/Fonts/NotoSans.ttf#Noto Sans");
                Application.Current.Resources["AppFontFamily"] = silesianFont;
            }
            // Language loading is now handled by SharedUtilities in each component
            _ = EnsureModJsonInModLibrary();
            EnsureDefaultDirectories();
            // Always generate default preset on app startup
            FlairX_Mod_Manager.Pages.ModGridPage gridPage = new();
            gridPage.SaveDefaultPresetAllInactive();
            
            // Ensure symlinks are properly validated and recreated for active mods on startup
            Logger.LogInfo("Validating and recreating symlinks for active mods on application startup");
            FlairX_Mod_Manager.Pages.ModGridPage.ValidateAndFixSymlinks();
            FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
            Logger.LogInfo("Symlink validation and recreation completed on startup");
            // Removed: ZIP thumbnail cache generation on startup

            // Show loading window first
            _ = ShowLoadingWindowAndInitialize();


            
            // Start StatusKeeperSync (watcher + timer) if dynamic synchronization is enabled
            if (SettingsManager.Current.StatusKeeperDynamicSyncEnabled)
            {
                FlairX_Mod_Manager.Pages.StatusKeeperSyncPage.StartWatcherStatic();
                FlairX_Mod_Manager.Pages.StatusKeeperSyncPage.StartPeriodicSyncStatic();
            }
        }

        private void EnsureDefaultDirectories()
        {
            var xxmiDir = FlairX_Mod_Manager.SettingsManager.Current.XXMIModsDirectory;
            if (!string.IsNullOrWhiteSpace(xxmiDir))
            {
                try
                {
                    Directory.CreateDirectory(xxmiDir);
                    if (!IsNtfs(xxmiDir))
                    {
                        ShowNtfsWarning(xxmiDir, "XXMI");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"XXMI directory creation failed: {ex.Message}");
                    // Directory creation failed - not critical for app startup
                }
            }
            var modLibDir = FlairX_Mod_Manager.SettingsManager.Current.ModLibraryDirectory;
            if (!string.IsNullOrWhiteSpace(modLibDir))
            {
                try
                {
                    Directory.CreateDirectory(modLibDir);
                    if (!IsNtfs(modLibDir))
                    {
                        ShowNtfsWarning(modLibDir, "ModLibrary");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ModLibrary directory creation failed: {ex.Message}");
                    // Directory creation failed - not critical for app startup
                }
            }
        }

        private bool IsNtfs(string path)
        {
            try
            {
                var fullPath = System.IO.Path.GetFullPath(path);
                var root = System.IO.Path.GetPathRoot(fullPath);
                if (string.IsNullOrEmpty(root)) return false;
                var drive = new DriveInfo(root!);
                return string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NTFS check failed: {ex.Message}");
                return false;
            }
        }

        private void ShowNtfsWarning(string path, string label)
        {
            var langDict = SharedUtilities.LoadLanguageDictionary();
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = SharedUtilities.GetTranslation(langDict, "Ntfs_Warning_Title"),
                Content = string.Format(SharedUtilities.GetTranslation(langDict, "Ntfs_Warning_Content"), label, path),
                CloseButtonText = "OK",
                XamlRoot = _window?.Content?.XamlRoot
            };
            _ = dialog.ShowAsync();
        }

        // Changed to public so MainWindow can call this method
        public void ShowStartupNtfsWarningIfNeeded()
        {
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var root = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(exePath));
                    if (!string.IsNullOrEmpty(root))
                    {
                        var drive = new DriveInfo(root!);
                        if (!string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
                        {
                            var langDict = SharedUtilities.LoadLanguageDictionary();
                            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                            {
                                Title = SharedUtilities.GetTranslation(langDict, "Ntfs_Warning_Title"),
                                Content = SharedUtilities.GetTranslation(langDict, "Ntfs_Startup_Warning_Content"),
                                CloseButtonText = "OK",
                                XamlRoot = _window?.Content?.XamlRoot
                            };
                            _ = dialog.ShowAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to check NTFS on startup: {ex.Message}");
            }
        }

        // Removed duplicate MainWindow property

        public async Task EnsureModJsonInModLibrary()
        {
            await Task.Run(async () =>
            {
                // Only run if a game is selected (not index 0)
                if (SettingsManager.Current.SelectedGameIndex == 0)
                {
                    System.Diagnostics.Debug.WriteLine("EnsureModJsonInModLibrary: Skipping - no game selected");
                    return;
                }
                
                // Use current path from settings with validation
                string modLibraryPath = SharedUtilities.GetSafeModLibraryPath();
                if (!System.IO.Directory.Exists(modLibraryPath)) 
                {
                    System.Diagnostics.Debug.WriteLine($"EnsureModJsonInModLibrary: Skipping - mod library path does not exist: {modLibraryPath}");
                    return;
                }
            
            // List of newly created/updated mod.json files
            var newlyCreatedModPaths = new List<string>();
            var updatedModPaths = new List<string>();
            
            // Process category directories (1st level) and mod directories (2nd level)
            foreach (var categoryDir in System.IO.Directory.GetDirectories(modLibraryPath, "*", SearchOption.TopDirectoryOnly))
            {
                // Skip if this is not a directory or is a special directory
                if (!System.IO.Directory.Exists(categoryDir)) continue;
                
                // Process each mod directory within the category
                foreach (var modDir in System.IO.Directory.GetDirectories(categoryDir, "*", SearchOption.TopDirectoryOnly))
                {
                    var modJsonPath = System.IO.Path.Combine(modDir, "mod.json");
                    bool isNewFile = !System.IO.File.Exists(modJsonPath);
                    bool needsUpdate = false;
                    
                    // Scan for namespace info
                    var (hasNamespace, namespaceMap) = ScanModForNamespace(modDir);
                
                Dictionary<string, object> modData;
                
                if (isNewFile)
                {
                    // Create new mod.json with complete info (removed category field)
                    modData = new Dictionary<string, object>
                    {
                        ["author"] = "unknown",
                        ["character"] = "!unknown!",
                        ["url"] = "https://",
                        ["version"] = "",
                        ["dateChecked"] = "0000-00-00",
                        ["dateUpdated"] = "0000-00-00",
                        ["hotkeys"] = new List<object>()
                    };
                    
                    // Add sync method info
                    if (hasNamespace && namespaceMap.Count > 0)
                    {
                        modData["syncMethod"] = "namespace";
                        var namespaceList = new List<object>();
                        
                        foreach (var kvp in namespaceMap)
                        {
                            namespaceList.Add(new Dictionary<string, object>
                            {
                                ["namespace"] = kvp.Key,
                                ["iniFiles"] = kvp.Value.ToArray()
                            });
                        }
                        
                        modData["namespaces"] = namespaceList;
                    }
                    else
                    {
                        modData["syncMethod"] = "classic";
                    }
                    
                    newlyCreatedModPaths.Add(modDir);
                }
                else
                {
                    // Read existing mod.json
                    try
                    {
                        var jsonContent = System.IO.File.ReadAllText(modJsonPath);
                        modData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent) ?? new();
                        
                        // Check if sync method info is missing or outdated
                        if (!modData.ContainsKey("syncMethod"))
                        {
                            if (hasNamespace && namespaceMap.Count > 0)
                            {
                                modData["syncMethod"] = "namespace";
                                var namespaceList = new List<object>();
                                
                                foreach (var kvp in namespaceMap)
                                {
                                    namespaceList.Add(new Dictionary<string, object>
                                    {
                                        ["namespace"] = kvp.Key,
                                        ["iniFiles"] = kvp.Value.ToArray()
                                    });
                                }
                                
                                modData["namespaces"] = namespaceList;
                            }
                            else
                            {
                                modData["syncMethod"] = "classic";
                            }
                            needsUpdate = true;
                            updatedModPaths.Add(modDir);
                        }
                    }
                    catch
                    {
                        // If JSON is corrupted, recreate it (removed category field)
                        modData = new Dictionary<string, object>
                        {
                            ["author"] = "unknown",
                            ["character"] = "!unknown!",
                            ["url"] = "https://",
                            ["hotkeys"] = new List<object>(),
                            ["syncMethod"] = hasNamespace ? "namespace" : "classic"
                        };
                        
                        if (hasNamespace && namespaceMap.Count > 0)
                        {
                            var namespaceList = new List<object>();
                            
                            foreach (var kvp in namespaceMap)
                            {
                                namespaceList.Add(new Dictionary<string, object>
                                {
                                    ["namespace"] = kvp.Key,
                                    ["iniFiles"] = kvp.Value.ToArray()
                                });
                            }
                            
                            modData["namespaces"] = namespaceList;
                        }
                        
                        needsUpdate = true;
                        updatedModPaths.Add(modDir);
                    }
                }
                
                // Save mod.json (for new files or updated files)
                if (isNewFile || needsUpdate)
                {
                    var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                    var jsonContent = System.Text.Json.JsonSerializer.Serialize(modData, jsonOptions);
                    System.IO.File.WriteAllText(modJsonPath, jsonContent);
                }
                }
            }
            
            // Automatically detect hotkeys for newly created mod.json files
            if (newlyCreatedModPaths.Count > 0)
            {
                foreach (var modPath in newlyCreatedModPaths)
                {
                    // Use static method that doesn't require HotkeyFinderPage instance
                    await FlairX_Mod_Manager.Pages.HotkeyFinderPage.AutoDetectHotkeysForModStaticAsync(modPath);
                }
            }
            
                // Log results
                if (newlyCreatedModPaths.Count > 0 || updatedModPaths.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Mod.json processing complete: {newlyCreatedModPaths.Count} created, {updatedModPaths.Count} updated");
                }
            });
        }



        private (bool hasNamespace, Dictionary<string, List<string>> namespaceMap) ScanModForNamespace(string modDir)
        {
            var namespaceMap = new Dictionary<string, List<string>>();
            
            try
            {
                // Look for .ini files in the mod directory and subdirectories
                var iniFiles = System.IO.Directory.GetFiles(modDir, "*.ini", SearchOption.AllDirectories);

                foreach (var iniFile in iniFiles)
                {
                    var content = System.IO.File.ReadAllText(iniFile, System.Text.Encoding.UTF8);
                    var lines = content.Split('\n');

                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();

                        // Look for namespace declaration: namespace = AnbyDangerousBeast
                        var match = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"^namespace\s*=\s*(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var namespacePath = match.Groups[1].Value.Trim();
                            var relativeIniPath = System.IO.Path.GetRelativePath(modDir, iniFile);

                            // Add to namespace map
                            if (!namespaceMap.ContainsKey(namespacePath))
                            {
                                namespaceMap[namespacePath] = new List<string>();
                            }
                            namespaceMap[namespacePath].Add(relativeIniPath);
                            
                            break; // Only need first namespace declaration per file
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning mod {modDir} for namespace: {ex.Message}");
            }

            return (namespaceMap.Count > 0, namespaceMap);
        }

        private async Task ShowLoadingWindowAndInitialize()
        {
            var loadingWindow = new LoadingWindow();
            loadingWindow.Activate();
            
            try
            {
                loadingWindow.UpdateStatus("Loading mod library...");
                await Task.Delay(100);
                
                // No need for startup image loading - ModGridPage will load images when first accessed
                loadingWindow.UpdateStatus("Preparing application...");
                
                loadingWindow.UpdateStatus("Initializing main window...");
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during preloading: {ex.Message}");
                LogToGridLog($"STARTUP: Error during preloading: {ex.Message}");
            }
            
            // Create and show main window on UI thread
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                _window = new MainWindow();
                
                // Add window close handling - remove symlinks
                _window.Closed += (s, e) =>
                {
                    FlairX_Mod_Manager.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
                    var modsDir = FlairX_Mod_Manager.SettingsManager.Current.XXMIModsDirectory;
                    if (string.IsNullOrWhiteSpace(modsDir))
                        modsDir = SharedUtilities.GetSafeXXMIModsPath();
                    if (System.IO.Directory.Exists(modsDir))
                    {
                        foreach (var dir in System.IO.Directory.GetDirectories(modsDir))
                        {
                            if (FlairX_Mod_Manager.Pages.ModGridPage.IsSymlinkStatic(dir))
                            {
                                System.IO.Directory.Delete(dir, true);
                            }
                        }
                    }
                };
                
                _window.Activate();
                loadingWindow.Close();
            });
        }
        
        // Removed PreloadModImages - no longer needed since ModGridPage stays in memory



        private static void LogToGridLog(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logPath = PathManager.GetSettingsPath("GridLog.log");
                var settingsDir = System.IO.Path.GetDirectoryName(logPath);
                
                if (!string.IsNullOrEmpty(settingsDir) && !Directory.Exists(settingsDir))
                {
                    Directory.CreateDirectory(settingsDir);
                }
                
                var logEntry = $"[{timestamp}] {message}\n";
                File.AppendAllText(logPath, logEntry, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write to GridLog: {ex.Message}");
            }
        }

        private void RequireAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                var exeName = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exeName))
                {
                    var startInfo = new ProcessStartInfo(exeName)
                    {
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    try
                    {
                        Process.Start(startInfo);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to restart as admin: {ex.Message}");
                        // User cancelled UAC or other error occurred
                    }
                }
                Environment.Exit(0);
            }
        }
    }
}
