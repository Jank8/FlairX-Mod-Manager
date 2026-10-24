using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml.Controls;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Keeps the GameBanana session in WebView2's encrypted profile.
    /// Only an in-memory cookie header is cached for HttpClient calls; no cookie is
    /// written to the portable folder. The User-Agent is not secret and is persisted
    /// in Settings so cf_clearance stays bound to the UA that obtained it.
    /// </summary>
    public class CloudflareBypassService
    {
        private static string? _cookies;
        private static string? _userAgent;
        private static string? _legacyUserAgent;
        private static bool _profileRefreshAttempted;
        private static readonly string LegacyCookieFilePath = Path.Combine(
            PathManager.GetAbsolutePath(PathManager.SETTINGS_DIR),
            "gamebanana_cookies.txt");

        static CloudflareBypassService()
        {
            RemoveLegacyPlaintextFile();
        }

        public static async Task<(string? cookies, string? userAgent)> BypassCloudflareAsync()
        {
            try
            {
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
                    PersistUserAgent(panelUserAgent);

                    // Values are session data - log only the shape, never the cookie itself.
                    Logger.LogInfo(
                        $"Cloudflare bypass successful. Cookie header length: {_cookies.Length}, " +
                        $"cookie count: {_cookies.Split(';').Length}");
                    Logger.LogInfo($"User-Agent: {_userAgent ?? "DEFAULT"}");

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

        /// <summary>
        /// Reads the cookies that WebView2 already persisted in its profile. This is the only
        /// source of truth; nothing is copied into a text file.
        /// </summary>
        public static async Task CaptureFromWebViewAsync(CoreWebView2 coreWebView)
        {
            if (coreWebView == null)
            {
                return;
            }

            try
            {
                var cookies = await coreWebView.CookieManager.GetCookiesAsync("https://gamebanana.com");
                if (cookies == null || cookies.Count == 0)
                {
                    return;
                }

                _cookies = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));

                // Only adopt a UA when none is known yet. A hidden WebView that used the
                // default UA must not overwrite the UA that originally obtained cf_clearance.
                if (string.IsNullOrEmpty(GetCachedUserAgent()) && !string.IsNullOrEmpty(coreWebView.Settings.UserAgent))
                {
                    _userAgent = coreWebView.Settings.UserAgent;
                    PersistUserAgent(_userAgent);
                }

                Logger.LogInfo(
                    $"GameBanana session captured from WebView2. " +
                    $"Cookie count: {cookies.Count}, header length: {_cookies.Length}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to capture GameBanana cookies from WebView2", ex);
            }
        }

        /// <summary>
        /// Loads the persistent WebView2 profile once, so API calls made before the first
        /// visible browser panel still see an existing login.
        /// </summary>
        public static async Task EnsureProfileSessionLoadedAsync(MainWindow? mainWindow)
        {
            if (!string.IsNullOrEmpty(_cookies) || _profileRefreshAttempted || mainWindow == null)
            {
                return;
            }

            _profileRefreshAttempted = true;
            Microsoft.UI.Xaml.Controls.WebView2? webView = null;
            try
            {
                var container = mainWindow.GetHiddenWebViewContainer();
                if (container == null)
                {
                    return;
                }

                webView = new Microsoft.UI.Xaml.Controls.WebView2();
                container.Children.Add(webView);
                await webView.EnsureCoreWebView2Async();
                if (webView.CoreWebView2 != null)
                {
                    // Keep the same UA that obtained cf_clearance.
                    var cachedUserAgent = GetCachedUserAgent();
                    if (!string.IsNullOrEmpty(cachedUserAgent))
                    {
                        webView.CoreWebView2.Settings.UserAgent = cachedUserAgent;
                    }

                    await CaptureFromWebViewAsync(webView.CoreWebView2);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load the GameBanana profile session", ex);
            }
            finally
            {
                if (webView != null)
                {
                    try
                    {
                        webView.Close();
                    }
                    catch { }
                    if (webView.Parent is Panel parent)
                    {
                        parent.Children.Remove(webView);
                    }
                }
            }
        }

        public static string? GetCachedCookies() => _cookies;

        public static string? GetCachedUserAgent()
        {
            if (!string.IsNullOrEmpty(_userAgent))
            {
                return _userAgent;
            }

            if (!string.IsNullOrEmpty(_legacyUserAgent))
            {
                return _legacyUserAgent;
            }

            return SettingsManager.Current.GameBananaUserAgent;
        }

        public static void SetCookiesManually(string cookies, string? userAgent)
        {
            _cookies = cookies;
            _userAgent = userAgent;
            PersistUserAgent(userAgent);
            Logger.LogInfo("GameBanana session updated in memory");
        }

        public static void ClearCookies()
        {
            _cookies = null;
            _userAgent = null;
            _legacyUserAgent = null;
            _profileRefreshAttempted = false;

            SettingsManager.Current.GameBananaUserAgent = null;
            SettingsManager.Save();

            RemoveLegacyPlaintextFile();
            Logger.LogInfo("GameBanana in-memory session cleared");
        }

        public static void CleanupLegacyPlaintextFile() => RemoveLegacyPlaintextFile();

        /// <summary>
        /// Called after SettingsManager.Load(): moves the non-secret User-Agent from the old
        /// plaintext cookie file into Settings, then the file is gone for good.
        /// </summary>
        public static void PersistLegacyUserAgentIfNeeded()
        {
            if (string.IsNullOrEmpty(_legacyUserAgent) ||
                !string.IsNullOrEmpty(SettingsManager.Current.GameBananaUserAgent))
            {
                return;
            }

            SettingsManager.Current.GameBananaUserAgent = _legacyUserAgent;
            SettingsManager.Save();
        }

        private static void PersistUserAgent(string? userAgent)
        {
            if (string.IsNullOrEmpty(userAgent) ||
                string.Equals(SettingsManager.Current.GameBananaUserAgent, userAgent, StringComparison.Ordinal))
            {
                return;
            }

            SettingsManager.Current.GameBananaUserAgent = userAgent;
            SettingsManager.Save();
        }

        private static void RemoveLegacyPlaintextFile()
        {
            try
            {
                if (File.Exists(LegacyCookieFilePath))
                {
                    // Keep only the non-secret User-Agent from the old file, so an existing
                    // cf_clearance cookie stays valid. The cookie line itself is discarded.
                    // Do not touch Settings here: this runs during App startup, before
                    // SettingsManager has necessarily loaded the user's Settings.json.
                    var lines = File.ReadAllLines(LegacyCookieFilePath);
                    if (lines.Length >= 2)
                    {
                        _legacyUserAgent = lines[1];
                    }

                    File.Delete(LegacyCookieFilePath);
                    Logger.LogInfo("Removed legacy plaintext GameBanana cookie file");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not remove legacy cookie file: {ex.Message}");
            }
        }
    }
}
