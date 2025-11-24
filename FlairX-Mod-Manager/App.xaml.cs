using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Principal;
using System.Threading;
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
        private static Mutex? _instanceMutex;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            Logger.LogInfo("Application starting up");
            
            // Check for single instance
            bool createdNew;
            _instanceMutex = new Mutex(true, "FlairXModManager_SingleInstance_Mutex", out createdNew);
            
            if (!createdNew)
            {
                Logger.LogWarning("Another instance of FlairX Mod Manager is already running");
                
                // Show message and exit
                var messageDialog = new ContentDialog
                {
                    Title = "Already Running",
                    Content = "FlairX Mod Manager is already running. Please close the existing instance first.",
                    CloseButtonText = "OK"
                };
                
                // Exit the application
                Environment.Exit(0);
                return;
            }
            
            try
            {
                Logger.LogInfo("Initializing WinUI components");
                InitializeComponent();
                
                Logger.LogInfo("Registering system notifications");
                AppNotificationManager.Default.Register();
                
                Logger.LogInfo("App constructor completed successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Critical error during app initialization", ex);
                throw;
            }
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Logger.LogInfo("OnLaunched called - loading application settings");
            try
            {
                SettingsManager.Load(); // Load settings before creating window
                Logger.LogInfo($"Settings loaded - Selected game index: {SettingsManager.Current.SelectedGameIndex}");
                // AUTOMATIC LANGUAGE DETECTION on first start
                var langFile = SettingsManager.Current.LanguageFile;
                Logger.LogInfo($"Current language file: {langFile ?? "null"}");
                
                if (string.IsNullOrEmpty(langFile) || langFile == "auto")
                {
                    Logger.LogInfo("Performing automatic language detection");
                    var systemCulture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                    Logger.LogInfo($"System culture detected: {systemCulture}");
                    
                    var languageFolder = PathManager.GetAbsolutePath("Language");
                    var available = System.IO.Directory.Exists(languageFolder)
                        ? System.IO.Directory.GetFiles(languageFolder, "*.json").Select(f => System.IO.Path.GetFileName(f)).ToList()
                        : new List<string>();
                    
                    Logger.LogInfo($"Available language files: {string.Join(", ", available)}");
                    
                    langFile = available.FirstOrDefault(f => f.StartsWith(systemCulture, StringComparison.OrdinalIgnoreCase)) ?? "en.json";
                    Logger.LogInfo($"Selected language file: {langFile}");
                    
                    SettingsManager.Current.LanguageFile = langFile;
                    SettingsManager.Save();
                }
                // Set culture and font for Asian and RTL languages
                Logger.LogInfo($"Setting up culture and fonts for language: {langFile}");
                
                if (langFile.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInfo("Configuring Chinese language settings");
                    System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("zh-CN");
                    System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("zh-CN");
                    var chineseFont = Application.Current.Resources["ChineseFont"] as Microsoft.UI.Xaml.Media.FontFamily;
                    if (chineseFont != null)
                    {
                        Application.Current.Resources["AppFontFamily"] = chineseFont;
                        Logger.LogInfo("Chinese font applied successfully");
                    }
                    else
                    {
                        Logger.LogWarning("Chinese font resource not found");
                    }
                }
                else if (langFile.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInfo("Configuring Japanese language settings");
                    System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ja-JP");
                    System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("ja-JP");
                    var japaneseFont = Application.Current.Resources["JapaneseFont"] as Microsoft.UI.Xaml.Media.FontFamily;
                    if (japaneseFont != null)
                    {
                        Application.Current.Resources["AppFontFamily"] = japaneseFont;
                        Logger.LogInfo("Japanese font applied successfully");
                    }
                    else
                    {
                        Logger.LogWarning("Japanese font resource not found");
                    }
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
            _ = EnsureModJsonInXXMIMods();
            EnsureDefaultDirectories();
            // Always generate default preset on app startup
            FlairX_Mod_Manager.Pages.ModGridPage gridPage = new();
            gridPage.SaveDefaultPresetAllInactive();
            
            // No symlink validation needed - using DISABLED_ prefix system
            Logger.LogInfo("Mod state maintained using DISABLED_ prefix system");
            // Removed: ZIP thumbnail cache generation on startup

            // Show loading window first
            _ = ShowLoadingWindowAndInitialize();


            
                // Start StatusKeeperSync (watcher + timer) if dynamic synchronization is enabled
                if (SettingsManager.Current.StatusKeeperDynamicSyncEnabled)
                {
                    Logger.LogInfo("Starting StatusKeeper dynamic synchronization");
                    try
                    {
                        FlairX_Mod_Manager.Pages.StatusKeeperSyncPage.StartWatcherStatic();
                        FlairX_Mod_Manager.Pages.StatusKeeperSyncPage.StartPeriodicSyncStatic();
                        Logger.LogInfo("StatusKeeper sync started successfully");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to start StatusKeeper synchronization", ex);
                    }
                }
                else
                {
                    Logger.LogInfo("StatusKeeper dynamic sync is disabled");
                }
                
                Logger.LogInfo("Application launch completed successfully");
                Logger.LogInfo("Running on WinUI 3 SDK 1.8.0 (1.8.250907003)");
                
                // Show success notification using new InfoBar
                if (_window is MainWindow mainWindow)
                {
                    mainWindow.ShowSuccessInfo("FlairX Mod Manager loaded successfully", 2000);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Critical error during application launch", ex);
                throw;
            }
        }

        private void EnsureDefaultDirectories()
        {
            Logger.LogInfo("Ensuring default directories exist");
            
            var xxmiDir = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
            if (!string.IsNullOrWhiteSpace(xxmiDir))
            {
                Logger.LogInfo($"Creating XXMI directory: {xxmiDir}");
                try
                {
                    Directory.CreateDirectory(xxmiDir);
                    Logger.LogInfo("XXMI directory created successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"XXMI directory creation failed: {xxmiDir}", ex);
                }
            }
            else
            {
                Logger.LogWarning("XXMI directory path is empty or null");
            }
            
            // Mods are now stored directly in XXMI/Mods - no separate ModLibrary needed
            Logger.LogInfo("Mods will be stored in XXMI/Mods directory");
        }



        // Removed duplicate MainWindow property

        public async Task EnsureModJsonInXXMIMods()
        {
            await Task.Run(async () =>
            {
                // Only run if a game is selected (not index 0)
                if (SettingsManager.Current.SelectedGameIndex == 0)
                {
                    System.Diagnostics.Debug.WriteLine("EnsureModJsonInXXMIMods: Skipping - no game selected");
                    return;
                }
                
                // Use XXMI Mods directory
                string modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (!System.IO.Directory.Exists(modsPath)) 
                {
                    System.Diagnostics.Debug.WriteLine($"EnsureModJsonInXXMIMods: Skipping - mod library path does not exist: {modsPath}");
                    return;
                }
            
            // List of newly created/updated mod.json files
            var newlyCreatedModPaths = new List<string>();
            var updatedModPaths = new List<string>();
            
            // Process category directories (1st level) and mod directories (2nd level)
            foreach (var categoryDir in System.IO.Directory.GetDirectories(modsPath, "*", SearchOption.TopDirectoryOnly))
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
                        ["url"] = "https://",
                        ["version"] = "",
                        ["dateChecked"] = "0000-00-00",
                        ["dateUpdated"] = "0000-00-00",
                        ["hotkeys"] = new List<object>(),
                        ["statusKeeperSync"] = true
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
                    // Read existing mod.json - PRESERVE ALL EXISTING DATA
                    try
                    {
                        var jsonContent = System.IO.File.ReadAllText(modJsonPath);
                        modData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent) ?? new();
                        
                        // ONLY add missing syncMethod if it doesn't exist - never overwrite existing data
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
                        
                        // ONLY add missing hotkeys array if it doesn't exist
                        if (!modData.ContainsKey("hotkeys"))
                        {
                            modData["hotkeys"] = new List<object>();
                            needsUpdate = true;
                            if (!updatedModPaths.Contains(modDir))
                                updatedModPaths.Add(modDir);
                        }
                        
                        // ONLY add missing statusKeeperSync if it doesn't exist
                        if (!modData.ContainsKey("statusKeeperSync"))
                        {
                            modData["statusKeeperSync"] = true;
                            needsUpdate = true;
                            if (!updatedModPaths.Contains(modDir))
                                updatedModPaths.Add(modDir);
                        }
                        
                        // DO NOT add any other missing fields - preserve user data
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to read existing mod.json at {modJsonPath}: {ex.Message}");
                        // If JSON is corrupted, try to preserve what we can by creating minimal structure
                        modData = new Dictionary<string, object>
                        {
                            ["author"] = "unknown",
                            ["url"] = "https://",
                            ["hotkeys"] = new List<object>(),
                            ["syncMethod"] = hasNamespace ? "namespace" : "classic",
                            ["statusKeeperSync"] = true
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
                        System.Diagnostics.Debug.WriteLine($"Recreated corrupted mod.json at {modJsonPath}");
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
            
            // Clean up any mod.json files that shouldn't be in category directories
            foreach (var categoryDir in System.IO.Directory.GetDirectories(modsPath, "*", SearchOption.TopDirectoryOnly))
            {
                var categoryModJsonPath = System.IO.Path.Combine(categoryDir, "mod.json");
                if (System.IO.File.Exists(categoryModJsonPath))
                {
                    try
                    {
                        System.IO.File.Delete(categoryModJsonPath);
                        System.Diagnostics.Debug.WriteLine($"Removed incorrect mod.json from category directory: {categoryDir}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to remove mod.json from category directory {categoryDir}: {ex.Message}");
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
                
                // Add window close handling - remove s
                _window.Closed += (s, e) =>
                {
                    // No symlink recreation needed - using DISABLED_ prefix system
                    var modsDir = FlairX_Mod_Manager.SettingsManager.GetCurrentXXMIModsDirectory();
                    if (string.IsNullOrWhiteSpace(modsDir))
                        modsDir = SharedUtilities.GetSafeXXMIModsPath();
                    // No symlink cleanup needed - using DISABLED_ prefix system
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


    }
}
