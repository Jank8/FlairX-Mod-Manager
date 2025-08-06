using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ZZZ_Mod_Manager_X
{
    /// <summary>
    /// Shared utilities to eliminate code duplication
    /// </summary>
    public static class SharedUtilities
    {
        // ==================== WIN32 FOLDER PICKER ====================
        
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO bi);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder pszPath);

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

        /// <summary>
        /// Asynchronicznie otwiera dialog wyboru folderu Win32
        /// </summary>
        /// <param name="hwnd">Handle okna nadrzêdnego</param>
        /// <param name="title">Tytu³ dialogu</param>
        /// <returns>Œcie¿ka wybranego folderu lub null jeœli anulowano</returns>
        public static async Task<string?> PickFolderAsync(nint hwnd, string title)
        {
            var tcs = new TaskCompletionSource<string?>();
            var thread = new Thread(() =>
            {
                try
                {
                    var bi = new BROWSEINFO
                    {
                        hwndOwner = hwnd,
                        lpszTitle = title,
                        ulFlags = 0x00000040 // BIF_NEWDIALOGSTYLE
                    };
                    
                    IntPtr pidl = SHBrowseForFolder(ref bi);
                    if (pidl == IntPtr.Zero)
                    {
                        tcs.SetResult(null);
                        return;
                    }
                    
                    var sb = new StringBuilder(MAX_PATH);
                    if (SHGetPathFromIDList(pidl, sb))
                    {
                        tcs.SetResult(sb.ToString());
                    }
                    else
                    {
                        tcs.SetResult(null);
                    }
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return await tcs.Task;
        }

        // ==================== FILESYSTEM UTILITIES ====================
        
        /// <summary>
        /// Sprawdza czy œcie¿ka znajduje siê na systemie plików NTFS
        /// </summary>
        /// <param name="path">Œcie¿ka do sprawdzenia</param>
        /// <returns>True jeœli NTFS, false w przeciwnym razie</returns>
        public static bool IsNtfsFileSystem(string path)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                var root = Path.GetPathRoot(fullPath);
                if (string.IsNullOrEmpty(root)) return false;
                var drive = new DriveInfo(root);
                return string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to check NTFS for path: {path}", ex);
                return false;
            }
        }

        /// <summary>
        /// Bezpiecznie otwiera plik lub URL z domyœln¹ aplikacj¹
        /// </summary>
        /// <param name="path">Œcie¿ka do pliku lub URL</param>
        /// <returns>True jeœli operacja powiod³a siê</returns>
        public static bool OpenWithDefaultApp(string path)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                Process.Start(psi);
                Logger.LogInfo($"Opened with default app: {path}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to open with default app: {path}", ex);
                return false;
            }
        }

        /// <summary>
        /// Pobiera handle g³ównego okna aplikacji
        /// </summary>
        /// <returns>Handle okna lub IntPtr.Zero w przypadku b³êdu</returns>
        public static nint GetMainWindowHandle()
        {
            try
            {
                var mainWindow = (App.Current as App)?.MainWindow;
                return mainWindow != null ? WinRT.Interop.WindowNative.GetWindowHandle(mainWindow) : IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to get main window handle", ex);
                return IntPtr.Zero;
            }
        }

        // ==================== UI UTILITIES ====================
        
        /// <summary>
        /// Ustawia elementy BreadcrumbBar na podstawie œcie¿ki
        /// </summary>
        /// <param name="bar">BreadcrumbBar do zaktualizowania</param>
        /// <param name="path">Œcie¿ka do wyœwietlenia</param>
        public static void SetBreadcrumbBarPath(BreadcrumbBar bar, string path)
        {
            try
            {
                var items = new List<object>();
                
                // Domyœlna œcie¿ka: tylko kropka lub pusty string
                if (path == "." || string.IsNullOrWhiteSpace(path))
                {
                    items.Add(new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\uE80F" });
                }
                else
                {
                    items.Add(new Microsoft.UI.Xaml.Controls.FontIcon { Glyph = "\uE80F" });
                    var segments = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, 
                        StringSplitOptions.RemoveEmptyEntries);
                    foreach (var segment in segments)
                    {
                        items.Add(segment);
                    }
                }
                
                bar.ItemsSource = items;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to set breadcrumb bar path: {path}", ex);
            }
        }

        /// <summary>
        /// Pobiera œcie¿kê z BreadcrumbBar
        /// </summary>
        /// <param name="bar">BreadcrumbBar do odczytania</param>
        /// <returns>Œcie¿ka jako string</returns>
        public static string GetBreadcrumbBarPath(BreadcrumbBar bar)
        {
            try
            {
                if (bar.ItemsSource is IEnumerable<object> items)
                {
                    var segments = items.Skip(1).OfType<string>(); // pomijamy ikonê
                    return string.Join(Path.DirectorySeparatorChar.ToString(), segments);
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to get breadcrumb bar path", ex);
                return string.Empty;
            }
        }

        // ==================== LANGUAGE UTILITIES ====================
        
        /// <summary>
        /// Loads language dictionary from specified subfolder
        /// </summary>
        public static Dictionary<string, string> LoadLanguageDictionary(string? subfolder = null)
        {
            try
            {
                var langFile = SettingsManager.Current?.LanguageFile ?? "en.json";
                var langPath = PathManager.GetLanguagePath(langFile, subfolder);
                
                if (!File.Exists(langPath))
                {
                    // Fallback to English
                    langPath = PathManager.GetLanguagePath("en.json", subfolder);
                }
                
                if (File.Exists(langPath))
                {
                    var json = File.ReadAllText(langPath, System.Text.Encoding.UTF8);
                    var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    
                    if (dictionary != null)
                    {
                        Logger.LogInfo($"Loaded language file: {PathManager.GetRelativePath(langPath)}");
                        return dictionary;
                    }
                }
                
                Logger.LogWarning($"Language file not found: {PathManager.GetRelativePath(langPath)}");
                return new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load language from subfolder: {subfolder}", ex);
                return new Dictionary<string, string>();
            }
        }
        
        /// <summary>
        /// Gets translated string with fallback to key
        /// </summary>
        public static string GetTranslation(Dictionary<string, string> dictionary, string key)
        {
            if (dictionary == null || string.IsNullOrEmpty(key))
                return key ?? string.Empty;
                
            return dictionary.TryGetValue(key, out var value) ? value : key;
        }

        // ==================== DIALOG UTILITIES ====================
        
        /// <summary>
        /// Shows a standard error dialog
        /// </summary>
        public static async Task ShowErrorDialog(string title, string message, Microsoft.UI.Xaml.XamlRoot? xamlRoot)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = xamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show error dialog: {title}", ex);
            }
        }
        
        /// <summary>
        /// Shows a standard confirmation dialog
        /// </summary>
        public static async Task<bool> ShowConfirmationDialog(string title, string message, string confirmText, string cancelText, Microsoft.UI.Xaml.XamlRoot? xamlRoot)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = confirmText,
                    CloseButtonText = cancelText,
                    XamlRoot = xamlRoot
                };
                var result = await dialog.ShowAsync();
                return result == ContentDialogResult.Primary;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show confirmation dialog: {title}", ex);
                return false;
            }
        }

        /// <summary>
        /// Shows a standard info dialog
        /// </summary>
        public static async Task ShowInfoDialog(string title, string message, Microsoft.UI.Xaml.XamlRoot? xamlRoot)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = xamlRoot,
                    DefaultButton = ContentDialogButton.Close
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show info dialog: {title}", ex);
            }
        }

        // ==================== JSON SETTINGS ====================
        
        /// <summary>
        /// Safely loads JSON settings from file
        /// </summary>
        public static T? LoadJsonSettings<T>(string fileName) where T : class, new()
        {
            try
            {
                var settingsPath = PathManager.GetSettingsPath(fileName);
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    return JsonSerializer.Deserialize<T>(json) ?? new T();
                }
                return new T();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load JSON settings from {fileName}", ex);
                return new T();
            }
        }
        
        /// <summary>
        /// Safely saves JSON settings to file
        /// </summary>
        public static bool SaveJsonSettings<T>(string fileName, T settings) where T : class
        {
            try
            {
                PathManager.EnsureDirectoryExists("Settings");
                var settingsPath = PathManager.GetSettingsPath(fileName);
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, json);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save JSON settings to {fileName}", ex);
                return false;
            }
        }

        // ==================== PATH UTILITIES ====================
        
        /// <summary>
        /// Validates mod directory name using SecurityValidator
        /// </summary>
        public static bool IsValidModDirectory(string? directoryName)
        {
            return SecurityValidator.IsValidModDirectoryName(directoryName);
        }
        
        /// <summary>
        /// Gets mod library path safely
        /// </summary>
        public static string GetSafeModLibraryPath(string? subPath = null)
        {
            return PathManager.GetModLibraryPath(subPath);
        }
        
        /// <summary>
        /// Gets XXMI mods path safely
        /// </summary>
        public static string GetSafeXXMIModsPath(string? subPath = null)
        {
            return PathManager.GetXXMIModsPath(subPath);
        }
    }
}