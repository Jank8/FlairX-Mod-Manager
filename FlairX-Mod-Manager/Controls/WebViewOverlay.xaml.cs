using FlairX_Mod_Manager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Controls
{
    /// <summary>
    /// Full-window, acrylic-backed minimal browser used for flows that need a real web session
    /// (GameBanana login, Cloudflare bypass).
    /// </summary>
    /// <remarks>
    /// Additive for now: the existing ContentDialog-based implementations are still in use, so
    /// this control can be reviewed and reverted without touching the rest of the application.
    /// </remarks>
    public sealed partial class WebViewOverlay : UserControl
    {
        private const string DefaultCookieScope = "https://gamebanana.com";

        private Microsoft.UI.Xaml.Controls.WebView2? _webView;
        private TaskCompletionSource<string?>? _completion;
        private string _cookieScope = DefaultCookieScope;

        /// <summary>
        /// The User-Agent used by this session. Cloudflare binds its clearance cookie to it,
        /// so callers must persist both values together.
        /// </summary>
        public string? UserAgent => _webView?.CoreWebView2?.Settings.UserAgent;

        public WebViewOverlay()
        {
            this.InitializeComponent();
            ApplyTranslations();
            this.IsTabStop = true;
            this.KeyDown += WebViewOverlay_KeyDown;
        }

        /// <summary>
        /// Pulls every visible string from the GameBananaBrowser language file.
        /// </summary>
        private void ApplyTranslations()
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary("GameBananaBrowser");

                ReloadButton.SetValue(ToolTipService.ToolTipProperty, SharedUtilities.GetTranslation(lang, "Comments_Reload"));
                ZoomLabel.Text = SharedUtilities.GetTranslation(lang, "Comments_Zoom");
                CancelButton.Content = SharedUtilities.GetTranslation(lang, "Comments_Cancel");
                ContinueButton.Content = SharedUtilities.GetTranslation(lang, "Comments_Continue");
                DontAskAgainCheckBox.Content = SharedUtilities.GetTranslation(lang, "Comments_DontAskAgain");
                GetCookiesButton.Content = SharedUtilities.GetTranslation(lang, "Comments_GetCookies");
            }
            catch (Exception ex)
            {
                Logger.LogError("WebViewOverlay: failed to apply translations", ex);
            }
        }

        /// <summary>
        /// True when the user asked not to be prompted again. Reset on every show.
        /// </summary>
        public bool DontAskAgain => DontAskAgainCheckBox?.IsChecked == true;

        /// <summary>
        /// Shows the overlay and navigates to <paramref name="url"/>.
        /// </summary>
        /// <returns>
        /// The captured cookie header on Continue, or <c>null</c> when cancelled.
        /// </returns>
        public async Task<string?> ShowAsync(
            string url,
            string? title = null,
            bool showDontAskAgain = true,
            string? cookieScope = null,
            bool showSessionActions = true)
        {
            if (_completion != null)
            {
                // Already showing - do not stack another request on top of the first.
                return null;
            }

            _completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _cookieScope = string.IsNullOrWhiteSpace(cookieScope) ? DefaultCookieScope : cookieScope;

            TitleText.Text = title ?? "GameBanana";
            AddressText.Text = url;
            StatusText.Text = string.Empty;
            DontAskAgainCheckBox.IsChecked = false;
            DontAskAgainCheckBox.Visibility = showDontAskAgain ? Visibility.Visible : Visibility.Collapsed;

            // Plain browsing (e.g. an author profile) only needs a close button; the
            // cookie-capture actions would be confusing there.
            ContinueButton.Visibility = showSessionActions ? Visibility.Visible : Visibility.Collapsed;
            GetCookiesButton.Visibility = showSessionActions ? Visibility.Visible : Visibility.Collapsed;

            // Browsing is not a cancellable operation - label the button "Close" instead.
            if (showSessionActions)
            {
                var browserLang = SharedUtilities.LoadLanguageDictionary("GameBananaBrowser");
                CancelButton.Content = SharedUtilities.GetTranslation(browserLang, "Comments_Cancel");
            }
            else
            {
                var baseLang = SharedUtilities.LoadLanguageDictionary();
                CancelButton.Content = SharedUtilities.GetTranslation(baseLang, "Close");
            }

            RootGrid.Visibility = Visibility.Visible;
            this.Focus(FocusState.Programmatic);

            await EnsureWebViewAsync();

            if (_webView?.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Navigate(url);
                ApplyZoom(ZoomSlider.Value);
            }
            else
            {
                StatusText.Text = "WebView2 is not available.";
                // Never leave the caller awaiting a completion that can no longer happen.
                Close(null);
            }

            return await _completion.Task;
        }

        private async Task EnsureWebViewAsync()
        {
            if (_webView != null)
            {
                return;
            }

            try
            {
                if (!await DispatcherQueueExtensions.EnsureWebView2Available(this.XamlRoot))
                {
                    StatusText.Text = "WebView2 runtime is not installed.";
                    return;
                }

                _webView = new Microsoft.UI.Xaml.Controls.WebView2
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                };

                WebViewHost.Children.Add(_webView);
                await _webView.EnsureCoreWebView2Async();

                if (_webView.CoreWebView2 == null)
                {
                    return;
                }

                _webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                _webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                _webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

                // Reuse the stored session, otherwise this panel always opens as a stranger and the
                // user sees themselves logged out even though the app already holds valid cookies.
                var cachedUserAgent = CloudflareBypassService.GetCachedUserAgent();
                if (!string.IsNullOrEmpty(cachedUserAgent))
                {
                    _webView.CoreWebView2.Settings.UserAgent = cachedUserAgent;
                }
                else
                {
                    // A real browser user agent is required for the reCAPTCHA widget to render.
                    _webView.CoreWebView2.Settings.UserAgent =
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
                }

                // Cookies are already in the shared WebView2 profile; do not copy them
                // through the app in a plaintext cache.
            }
            catch (Exception ex)
            {
                Logger.LogError("WebViewOverlay: failed to initialize WebView2", ex);
                StatusText.Text = "Failed to start the browser.";
            }
        }

        private void CoreWebView2_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            if (!WebViewNavigationPolicy.IsAllowed(args.Uri))
            {
                args.Cancel = true;
                Logger.LogWarning($"Blocked WebView navigation outside GameBanana: {args.Uri}");
            }
        }

        private void CoreWebView2_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            args.Handled = true;

            if (WebViewNavigationPolicy.IsAllowed(args.Uri))
            {
                sender.Navigate(args.Uri);
            }
            else
            {
                Logger.LogWarning($"Blocked WebView popup outside GameBanana: {args.Uri}");
            }
        }

        private void CoreWebView2_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess)
            {
                StatusText.Text = $"Navigation failed: {args.WebErrorStatus}";
                return;
            }

            if (_webView?.CoreWebView2 != null)
            {
                AddressText.Text = _webView.CoreWebView2.Source;

                // The shared WebView2 profile owns the cookies; capture the in-memory copy
                // used by HttpClient after the page has settled.
                _ = CloudflareBypassService.CaptureFromWebViewAsync(_webView.CoreWebView2);

                // The DOM zoom is lost on every navigation - re-apply the slider value.
                ApplyZoom(ZoomSlider.Value);
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_webView?.CoreWebView2 == null)
            {
                return;
            }

            StatusText.Text = "Reloading...";
            _webView.CoreWebView2.Reload();
        }

        private void ZoomSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            ApplyZoom(e.NewValue);
        }

        private void ApplyZoom(double percent)
        {
            // Fires once during InitializeComponent, before the named elements exist.
            if (ZoomText == null)
            {
                return;
            }

            ZoomText.Text = string.Format(CultureInfo.InvariantCulture, "{0:F0}%", percent);

            if (_webView?.CoreWebView2 == null)
            {
                return;
            }

            try
            {
                // The WinUI WebView2 control exposes no ZoomFactor, so scale through the DOM.
                var factor = Math.Clamp(percent / 100.0, 0.25, 5.0);
                _ = _webView.CoreWebView2.ExecuteScriptAsync(
                    $"document.documentElement.style.zoom = '{factor.ToString("0.##", CultureInfo.InvariantCulture)}';");
            }
            catch (Exception ex)
            {
                Logger.LogError("WebViewOverlay: failed to apply zoom", ex);
            }
        }

        private async void GetCookiesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cookies = await CaptureCookiesAsync();

                if (cookies == null)
                {
                    StatusText.Text = "No cookies yet - please log in first.";
                    return;
                }

                try
                {
                    // Same behaviour as the previous login dialog: persist the session immediately, so
                    // it is usable even when the user closes the panel without pressing Continue.
                    var userAgent = _webView?.CoreWebView2?.Settings.UserAgent ?? string.Empty;
                    CloudflareBypassService.SetCookiesManually(cookies, userAgent);

                    DontAskAgainCheckBox.IsChecked = true;
                    StatusText.Text = $"✓ Got {cookies.Count(c => c == ';') + 1} cookies";
                    Logger.LogInfo($"WebViewOverlay: captured and stored {cookies.Count(c => c == ';') + 1} cookies");
                }
                catch (Exception ex)
                {
                    Logger.LogError("WebViewOverlay: failed to store cookies", ex);
                    StatusText.Text = "Failed to store cookies.";
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("WebViewOverlay.GetCookiesButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("WebViewOverlay.GetCookiesButton_Click failed", ex);
            }
        }

        private async void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cookies = await CaptureCookiesAsync();

                if (cookies == null)
                {
                    StatusText.Text = "No cookies yet - please log in first.";
                    return;
                }

                Close(cookies);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("WebViewOverlay.ContinueButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("WebViewOverlay.ContinueButton_Click failed", ex);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close(null);
        }

        private void WebViewOverlay_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape && RootGrid.Visibility == Visibility.Visible)
            {
                e.Handled = true;
                Close(null);
            }
        }

        private async Task<string?> CaptureCookiesAsync()
        {
            if (_webView?.CoreWebView2 == null)
            {
                return null;
            }

            try
            {
                var cookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync(_cookieScope);
                if (cookies == null || cookies.Count == 0)
                {
                    return null;
                }

                // Names only - values are session data and must not end up in a log file.
                Logger.LogInfo($"WebViewOverlay: cookie names [{string.Join(", ", cookies.Select(c => c.Name))}] count={cookies.Count}");

                return string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
            }
            catch (Exception ex)
            {
                Logger.LogError("WebViewOverlay: failed to capture cookies", ex);
                return null;
            }
        }

        private void Close(string? result)
        {
            RootGrid.Visibility = Visibility.Collapsed;

            var completion = _completion;
            _completion = null;
            completion?.TrySetResult(result);
        }

        /// <summary>
        /// Releases the browser. Call when the host is being torn down.
        /// </summary>
        public void Shutdown()
        {
            try
            {
                if (_webView?.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                }

                WebViewHost.Children.Clear();
                _webView?.Close();
                _webView = null;
            }
            catch (Exception ex)
            {
                Logger.LogError("WebViewOverlay: failed to shut down WebView2", ex);
            }

            Close(null);
        }
    }
}
