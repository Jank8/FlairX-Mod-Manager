using FlairX_Mod_Manager;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Dialogs
{
    public sealed partial class GameBananaLoginDialog : ContentDialog
    {
        private Microsoft.UI.Xaml.Controls.WebView2? _webView;

        public bool DontAskAgain => DontAskAgainCheckBox.IsChecked == true;
        public bool LoginSucceeded { get; private set; } = false;

        public GameBananaLoginDialog()
        {
            this.InitializeComponent();
            this.Loaded += GameBananaLoginDialog_Loaded;

            var lang = SharedUtilities.LoadLanguageDictionary("GameBananaBrowser");

            Title = SharedUtilities.GetTranslation(lang, "Comments_LoginTitle") ?? "GameBanana Login";
            NsfwInfoText.Text = SharedUtilities.GetTranslation(lang, "Comments_LoginInfo")
                ?? "Log in to GameBanana to view comments on age-restricted mods. Log in below, then click Continue.";
            GetCookiesButtonText.Text = SharedUtilities.GetTranslation(lang, "Comments_GetCookies") ?? "Get Cookies";
            DontAskAgainCheckBox.Content = SharedUtilities.GetTranslation(lang, "Comments_DontAskAgain") ?? "Don't ask again";
        }

        private async void GameBananaLoginDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                if (!await DispatcherQueueExtensions.EnsureWebView2Available(this.XamlRoot))
                {
                    this.Hide();
                    return;
                }

                _webView = new Microsoft.UI.Xaml.Controls.WebView2();
                WebViewContainer.Children.Add(_webView);
                await _webView.EnsureCoreWebView2Async();

                // Auto-detect login success on navigation
                _webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

                // Set a real browser User-Agent so reCAPTCHA renders correctly
                _webView.CoreWebView2.Settings.UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

                _webView.CoreWebView2.Navigate("https://gamebanana.com/members/account/login");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize WebView2 for GB login dialog", ex);
            }
        }

        private async void CoreWebView2_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess || _webView?.CoreWebView2 == null) return;

            try
            {
                var url = _webView.CoreWebView2.Source;
                Logger.LogInfo($"GB login dialog nav: {url}");

                // Auto-detect: redirected away from login page = logged in
                if (!url.Contains("/account/login") && url.Contains("gamebanana.com"))
                {
                    await Task.Delay(500);
                    var captured = await TryCaptureCookiesAsync();
                    if (captured)
                    {
                        StatusText.Text = "✓ Logged in! Click Continue.";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("GB login nav error", ex);
            }
        }

        private async void GetCookiesButton_Click(object sender, RoutedEventArgs e)
        {
            await TryCaptureCookiesAsync();
        }

        private async Task<bool> TryCaptureCookiesAsync()
        {
            if (_webView?.CoreWebView2 == null) return false;

            try
            {
                StatusText.Text = "Getting cookies...";
                var cookieMgr = _webView.CoreWebView2.CookieManager;
                var cookies = await cookieMgr.GetCookiesAsync("https://gamebanana.com");

                if (cookies.Count > 0)
                {
                    var cookieStr = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
                    var ua = _webView.CoreWebView2.Settings.UserAgent;
                    Services.CloudflareBypassService.SetCookiesManually(cookieStr, ua);
                    LoginSucceeded = true;
                    DontAskAgainCheckBox.IsChecked = true;
                    StatusText.Text = $"✓ Got {cookies.Count} cookies";
                    Logger.LogInfo($"GB login dialog: captured {cookies.Count} cookies");
                    return true;
                }
                else
                {
                    StatusText.Text = "No cookies yet — please log in first.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                Logger.LogError("Failed to capture cookies in GB login dialog", ex);
                return false;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Allow closing even without login — SFW mods work without cookies
        }

        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            LoginSucceeded = false;
        }
    }
}
