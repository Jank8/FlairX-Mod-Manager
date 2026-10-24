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

        public static async Task<(string? cookies, string? userAgent)> BypassCloudflareAsync()
        {
            try
            {
                // The login and the Cloudflare challenge share one slide-out WebView panel.
                // The old ContentDialog duplicated WebView2 lifetime handling and leaked a
                // renderer (plus its session) on every attempt.
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    var (panelCookies, panelUserAgent) = await mainWindow.ShowCloudflareBypassPanelAsync();
                    if (string.IsNullOrEmpty(panelCookies))
                    {
                        Logger.LogInfo("Cloudflare bypass cancelled or no cookies were captured");
                        return (null, null);
                    }

                    _cookies = panelCookies;
                    _userAgent = panelUserAgent;

                    // Values are session data - log only the shape, never the cookie itself.
                    Logger.LogInfo(
                        $"Cloudflare bypass successful. Cookie header length: {_cookies.Length}, " +
                        $"cookie count: {_cookies.Split(';').Length}");
                    Logger.LogInfo($"User-Agent: {_userAgent ?? "DEFAULT"}");

                    SaveCookiesToFile();
                    return (_cookies, _userAgent);
                }

                Logger.LogError("Cloudflare bypass requested before the main window existed");
                return (null, null);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to show Cloudflare bypass", ex);
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

        public static void SetCookiesManually(string cookies, string? userAgent)
        {
            _cookies = cookies;
            _userAgent = userAgent;
            SaveCookiesToFile();
            Logger.LogInfo("Cookies set manually and saved");
        }
        
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
