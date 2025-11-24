using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Dialogs
{
    public sealed partial class CloudflareBypassDialog : ContentDialog
    {
        private Microsoft.UI.Xaml.Controls.WebView2? _webView;
        private string? _cookies;
        private string? _userAgent;
        private bool _isVerified = false;

        public string? Cookies => _cookies;
        public string? UserAgent => _userAgent;
        public bool IsVerified => _isVerified;

        public CloudflareBypassDialog()
        {
            this.InitializeComponent();
            this.Loaded += CloudflareBypassDialog_Loaded;
        }

        private async void CloudflareBypassDialog_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                _webView = new Microsoft.UI.Xaml.Controls.WebView2();
                WebViewContainer.Children.Add(_webView);

                await _webView.EnsureCoreWebView2Async();

                if (_webView.CoreWebView2 != null)
                {
                    // Navigate to GameBanana
                    _webView.CoreWebView2.Navigate("https://gamebanana.com");

                    // Monitor navigation to detect when Cloudflare is passed
                    _webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize WebView2 for Cloudflare bypass", ex);
            }
        }

        private async void CoreWebView2_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess && _webView?.CoreWebView2 != null)
            {
                try
                {
                    var url = _webView.CoreWebView2.Source;
                    
                    Logger.LogInfo($"Navigation completed to: {url}");
                    
                    // Wait a bit for any JavaScript to execute
                    await Task.Delay(2000);
                    
                    // Check if we're on gamebanana.com (not a challenge page)
                    if (url.Contains("gamebanana.com"))
                    {
                        // Get cookies
                        var cookieManager = _webView.CoreWebView2.CookieManager;
                        var cookies = await cookieManager.GetCookiesAsync("https://gamebanana.com");
                        
                        if (cookies.Count > 0)
                        {
                            var cookieString = string.Join("; ", 
                                System.Linq.Enumerable.Select(cookies, c => $"{c.Name}={c.Value}"));
                            
                            // Get user agent
                            var userAgent = _webView.CoreWebView2.Settings.UserAgent;
                            
                            _cookies = cookieString;
                            _userAgent = userAgent;
                            _isVerified = true;
                            
                            Logger.LogInfo($"Cloudflare bypass successful, obtained {cookies.Count} cookies");
                            Logger.LogInfo($"Cookie string length: {cookieString.Length}");
                        }
                        else
                        {
                            Logger.LogWarning("No cookies found yet");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to get cookies from WebView2", ex);
                }
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // User clicked Continue
            if (!_isVerified)
            {
                // Don't close if not verified yet
                args.Cancel = true;
            }
        }

        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // User cancelled
            _isVerified = false;
        }

        private async void RefreshButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (_webView?.CoreWebView2 != null)
            {
                try
                {
                    StatusText.Text = "Getting cookies...";
                    
                    var currentUrl = _webView.CoreWebView2.Source;
                    Logger.LogInfo($"Current URL: {currentUrl}");
                    
                    // Get cookies for all domains
                    var cookieManager = _webView.CoreWebView2.CookieManager;
                    var allCookies = await cookieManager.GetCookiesAsync(currentUrl);
                    
                    Logger.LogInfo($"Found {allCookies.Count} cookies for {currentUrl}");
                    
                    // Log each cookie
                    foreach (var cookie in allCookies)
                    {
                        Logger.LogInfo($"Cookie: {cookie.Name} = {cookie.Value.Substring(0, Math.Min(20, cookie.Value.Length))}... (Domain: {cookie.Domain})");
                    }
                    
                    if (allCookies.Count > 0)
                    {
                        var cookieString = string.Join("; ", 
                            System.Linq.Enumerable.Select(allCookies, c => $"{c.Name}={c.Value}"));
                        
                        // Get user agent
                        var userAgent = _webView.CoreWebView2.Settings.UserAgent;
                        
                        _cookies = cookieString;
                        _userAgent = userAgent;
                        _isVerified = true;
                        
                        StatusText.Text = $"✓ Got {allCookies.Count} cookies! Click Continue.";
                        Logger.LogInfo($"Manual cookie refresh: obtained {allCookies.Count} cookies");
                        Logger.LogInfo($"Cookie string: {cookieString.Substring(0, Math.Min(100, cookieString.Length))}...");
                    }
                    else
                    {
                        StatusText.Text = "No cookies found. Try refreshing the page.";
                        Logger.LogWarning("Manual cookie refresh: no cookies found");
                    }
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error: {ex.Message}";
                    Logger.LogError("Failed to manually get cookies", ex);
                }
            }
            else
            {
                StatusText.Text = "WebView not ready";
                Logger.LogError("WebView is null");
            }
        }
    }
}
