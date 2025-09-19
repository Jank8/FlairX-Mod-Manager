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

namespace FlairX_Mod_Manager
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
        /// <param name="hwnd">Handle okna nadrz�dnego</param>
        /// <param name="title">Tytu� dialogu</param>
        /// <returns>�cie�ka wybranego folderu lub null je�li anulowano</returns>
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
        /// Sprawdza czy �cie�ka znajduje si� na systemie plik�w NTFS
        /// </summary>
        /// <param name="path">�cie�ka do sprawdzenia</param>
        /// <returns>True je�li NTFS, false w przeciwnym razie</returns>
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
        /// Bezpiecznie otwiera plik lub URL z domy�ln� aplikacj�
        /// </summary>
        /// <param name="path">�cie�ka do pliku lub URL</param>
        /// <returns>True je�li operacja powiod�a si�</returns>
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
        /// Pobiera handle g��wnego okna aplikacji
        /// </summary>
        /// <returns>Handle okna lub IntPtr.Zero w przypadku b��du</returns>
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
        /// Ustawia elementy BreadcrumbBar na podstawie �cie�ki
        /// </summary>
        /// <param name="bar">BreadcrumbBar do zaktualizowania</param>
        /// <param name="path">�cie�ka do wy�wietlenia</param>
        public static void SetBreadcrumbBarPath(BreadcrumbBar bar, string path)
        {
            try
            {
                var items = new List<object>();
                
                // Domy�lna �cie�ka: tylko kropka lub pusty string
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
        /// Pobiera �cie�k� z BreadcrumbBar
        /// </summary>
        /// <param name="bar">BreadcrumbBar do odczytania</param>
        /// <returns>�cie�ka jako string</returns>
        public static string GetBreadcrumbBarPath(BreadcrumbBar bar)
        {
            try
            {
                if (bar.ItemsSource is IEnumerable<object> items)
                {
                    var segments = items.Skip(1).OfType<string>(); // pomijamy ikon�
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
            var dictionary = new Dictionary<string, string>();
            
            try
            {
                var langFile = SettingsManager.Current?.LanguageFile ?? "en.json";
                
                // Determine the exact language file path
                string langPath;
                if (!string.IsNullOrEmpty(subfolder))
                {
                    // Module-specific language file
                    langPath = PathManager.GetLanguagePath(langFile, subfolder);
                }
                else
                {
                    // Main language file
                    langPath = PathManager.GetLanguagePath(langFile);
                }
                
                // Add debug logging to track when this is called
                Logger.LogDebug($"LoadLanguageDictionary called - Path: {PathManager.GetRelativePath(langPath)}, Subfolder: {subfolder ?? "main"}");
                
                // Load the specified file
                if (File.Exists(langPath))
                {
                    var json = File.ReadAllText(langPath, System.Text.Encoding.UTF8);
                    var loadedDictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    
                    if (loadedDictionary != null)
                    {
                        // Add all keys except Language_DisplayName
                        foreach (var kvp in loadedDictionary)
                        {
                            if (kvp.Key != "Language_DisplayName")
                            {
                                dictionary[kvp.Key] = kvp.Value;
                            }
                        }
                        Logger.LogInfo($"Loaded language file: {PathManager.GetRelativePath(langPath)}");
                    }
                }
                else
                {
                    Logger.LogError($"Required language file not found: {PathManager.GetRelativePath(langPath)}");
                    throw new FileNotFoundException($"Language file not found: {langPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load language from subfolder: {subfolder}", ex);
                throw;
            }
            
            return dictionary;
        }
        
        /// <summary>
        /// Gets translated string, returns key if not found (for debugging missing translations)
        /// </summary>
        public static string GetTranslation(Dictionary<string, string> dictionary, string key)
        {
            if (dictionary == null)
                return $"[NULL_DICT:{key}]";
            
            if (string.IsNullOrEmpty(key))
                return "[EMPTY_KEY]";
                
            if (dictionary.TryGetValue(key, out var value))
                return value;
            
            // Show missing key instead of throwing exception
            return $"[MISSING:{key}]";
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