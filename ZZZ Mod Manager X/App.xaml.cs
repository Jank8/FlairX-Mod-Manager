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

namespace ZZZ_Mod_Manager_X
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

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
            // AUTOMATIC LANGUAGE DETECTION
            var langFile = SettingsManager.Current.LanguageFile;
            if (string.IsNullOrEmpty(langFile) || langFile == "auto")
            {
                var systemCulture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                var languageFolder = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Language");
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
            // Default language loading from settings or en.json
            var langPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Language", langFile);
            if (System.IO.File.Exists(langPath))
                ZZZ_Mod_Manager_X.LanguageManager.Instance.LoadLanguage(langFile);
            _ = EnsureModJsonInModLibrary();
            EnsureDefaultDirectories();
            // Always generate default preset on app startup
            ZZZ_Mod_Manager_X.Pages.ModGridPage gridPage = new();
            gridPage.SaveDefaultPresetAllInactive();
            
            // Ensure symlinks are properly validated and recreated for active mods on startup
            Logger.LogInfo("Validating and recreating symlinks for active mods on application startup");
            ZZZ_Mod_Manager_X.Pages.ModGridPage.ValidateAndFixSymlinks();
            ZZZ_Mod_Manager_X.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
            Logger.LogInfo("Symlink validation and recreation completed on startup");
            // Removed: ZIP thumbnail cache generation on startup

            _window = new MainWindow();
            _window.Activate();
            // Add window close handling - remove symlinks
            if (_window != null)
            {
                _window.Closed += (s, e) =>
                {
                    ZZZ_Mod_Manager_X.Pages.ModGridPage.RecreateSymlinksFromActiveMods();
                    var modsDir = ZZZ_Mod_Manager_X.SettingsManager.Current.XXMIModsDirectory;
                    if (string.IsNullOrWhiteSpace(modsDir))
                        modsDir = System.IO.Path.Combine(System.AppContext.BaseDirectory, "XXMI", "ZZMI", "Mods");
                    if (System.IO.Directory.Exists(modsDir))
                    {
                        foreach (var dir in System.IO.Directory.GetDirectories(modsDir))
                        {
                            if (ZZZ_Mod_Manager_X.Pages.ModGridPage.IsSymlinkStatic(dir))
                            {
                                System.IO.Directory.Delete(dir, true);
                            }
                        }
                    }
                };
            }

            // Check synchronization status after one second from startup
            _window?.DispatcherQueue.TryEnqueue(async () =>
            {
                // Give time for UI to load
                await Task.Delay(1000);
                
                try
                {
                    // Check backup completeness in background
                    bool hasBackup = ZZZ_Mod_Manager_X.Pages.StatusKeeperSyncPage.HasBackupFilesStatic();
                    bool isBackupComplete = ZZZ_Mod_Manager_X.Pages.StatusKeeperSyncPage.IsFullBackupPresentStatic();
                    
                    // Check if backup is incomplete or missing
                    if (!hasBackup || !isBackupComplete)
                    {
                        // If synchronization is enabled, disable it
                        if (SettingsManager.Current.StatusKeeperDynamicSyncEnabled)
                        {
                            // Disable synchronization
                            SettingsManager.Current.StatusKeeperDynamicSyncEnabled = false;
                            SettingsManager.Save();
                            
                            // Stop watcher and timer
                            ZZZ_Mod_Manager_X.Pages.StatusKeeperSyncPage.StopWatcherStatic();
                            ZZZ_Mod_Manager_X.Pages.StatusKeeperSyncPage.StopPeriodicSyncStatic();
                        }
                        
                        // Display synchronization disabled message
                        var dialog = new ContentDialog
                        {
                            Title = ZZZ_Mod_Manager_X.Pages.StatusKeeperSyncPage.TStatic("StatusKeeper_SyncDisabled_Title"),
                            Content = ZZZ_Mod_Manager_X.Pages.StatusKeeperSyncPage.TStatic("StatusKeeper_SyncDisabled_Message"),
                            CloseButtonText = "OK",
                            XamlRoot = _window?.Content?.XamlRoot
                        };
                        
                        await dialog.ShowAsync();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking sync status: {ex.Message}");
                }
            });
            
            // Start StatusKeeperSync (watcher + timer) if dynamic synchronization is enabled
            if (SettingsManager.Current.StatusKeeperDynamicSyncEnabled)
            {
                ZZZ_Mod_Manager_X.Pages.StatusKeeperSyncPage.StartWatcherStatic();
                ZZZ_Mod_Manager_X.Pages.StatusKeeperSyncPage.StartPeriodicSyncStatic();
            }
        }

        private void EnsureDefaultDirectories()
        {
            var xxmiDir = ZZZ_Mod_Manager_X.SettingsManager.Current.XXMIModsDirectory;
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
            var modLibDir = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory;
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
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = LanguageManager.Instance.T("Ntfs_Warning_Title"),
                Content = string.Format(LanguageManager.Instance.T("Ntfs_Warning_Content"), label, path),
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
                            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                            {
                                Title = LanguageManager.Instance.T("Ntfs_Warning_Title"),
                                Content = LanguageManager.Instance.T("Ntfs_Startup_Warning_Content"),
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

        public Window? MainWindow => _window;

        public async Task EnsureModJsonInModLibrary()
        {
            // Use current path from settings
            string modLibraryPath = ZZZ_Mod_Manager_X.SettingsManager.Current.ModLibraryDirectory ?? System.IO.Path.Combine(System.AppContext.BaseDirectory, "ModLibrary");
            if (!System.IO.Directory.Exists(modLibraryPath)) return;
            string placeholderJson = "{\n    \"author\": \"unknown\",\n    \"character\": \"!unknown!\",\n    \"url\": \"https://\",\n    \"hotkeys\": []\n}";
            
            // List of newly created mod.json files
            var newlyCreatedModPaths = new List<string>();
            
            // Create mod.json only in level 1 directories
            foreach (var dir in System.IO.Directory.GetDirectories(modLibraryPath, "*", SearchOption.TopDirectoryOnly))
            {
                var modJsonPath = System.IO.Path.Combine(dir, "mod.json");
                if (!System.IO.File.Exists(modJsonPath))
                {
                    System.IO.File.WriteAllText(modJsonPath, placeholderJson);
                    newlyCreatedModPaths.Add(dir);
                }
            }
            
            // Automatically detect hotkeys for newly created mod.json files
            if (newlyCreatedModPaths.Count > 0)
            {
                foreach (var modPath in newlyCreatedModPaths)
                {
                    // Use static method that doesn't require HotkeyFinderPage instance
                    await ZZZ_Mod_Manager_X.Pages.HotkeyFinderPage.AutoDetectHotkeysForModStaticAsync(modPath);
                }
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
