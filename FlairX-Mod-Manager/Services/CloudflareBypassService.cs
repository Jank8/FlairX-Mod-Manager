using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Services
{
    public class CloudflareBypassService
    {
        private static string? _cookies;
        private static string? _userAgent;
        private static readonly string CookieFilePath = Path.Combine(PathManager.GetAbsolutePath(PathManager.SETTINGS_DIR), "gamebanana_cookies.txt");

        static CloudflareBypassService()
        {
            // Load cookies from file on startup
            LoadCookiesFromFile();
        }

        public static async Task<(string? cookies, string? userAgent)> BypassCloudflareAsync(XamlRoot xamlRoot)
        {
            try
            {
                var dialog = new Dialogs.CloudflareBypassDialog
                {
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary && dialog.IsVerified)
                {
                    _cookies = dialog.Cookies;
                    _userAgent = dialog.UserAgent;
                    
                    Logger.LogInfo($"Cloudflare bypass successful. Cookies: {_cookies?.Substring(0, Math.Min(50, _cookies?.Length ?? 0))}...");
                    Logger.LogInfo($"User-Agent: {_userAgent}");
                    
                    // Save cookies to file
                    SaveCookiesToFile();
                    
                    return (_cookies, _userAgent);
                }

                return (null, null);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to show Cloudflare bypass dialog", ex);
                return (null, null);
            }
        }

        private static void SaveCookiesToFile()
        {
            try
            {
                if (!string.IsNullOrEmpty(_cookies) && !string.IsNullOrEmpty(_userAgent))
                {
                    File.WriteAllText(CookieFilePath, $"{_cookies}\n{_userAgent}");
                    Logger.LogInfo("Cookies saved to file");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save cookies to file", ex);
            }
        }

        private static void LoadCookiesFromFile()
        {
            try
            {
                if (File.Exists(CookieFilePath))
                {
                    var lines = File.ReadAllLines(CookieFilePath);
                    if (lines.Length >= 2)
                    {
                        _cookies = lines[0];
                        _userAgent = lines[1];
                        Logger.LogInfo("Cookies loaded from file");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load cookies from file", ex);
            }
        }

        public static string? GetCachedCookies() => _cookies;
        public static string? GetCachedUserAgent() => _userAgent;
        
        public static void ClearCookies()
        {
            _cookies = null;
            _userAgent = null;
            try
            {
                if (File.Exists(CookieFilePath))
                {
                    File.Delete(CookieFilePath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to delete cookie file", ex);
            }
        }
    }
}
