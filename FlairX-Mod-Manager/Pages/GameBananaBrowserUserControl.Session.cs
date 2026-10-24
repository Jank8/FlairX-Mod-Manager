using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class GameBananaBrowserUserControl
    {

        /// <summary>
        /// Updates the header session widget from the stored GameBanana session.
        /// </summary>
        private async Task RefreshSessionWidgetAsync(bool forceRefresh = false)
        {
            try
            {
                var profile = await GameBananaProfileService.GetAsync(forceRefresh);
                var loginText = SharedUtilities.GetTranslation(_lang, "Comments_Login");

                // Trust the widget only when both agree: a resolved profile *and* cookies that
                // still exist. After logout the profile request can be served from a stale
                // response, which used to leave the previous account on screen.
                var hasCookies = !string.IsNullOrEmpty(CloudflareBypassService.GetCachedCookies());
                var isLoggedIn = profile.IsLoggedIn && hasCookies;

                if (isLoggedIn)
                {
                    SessionNameText.Text = string.IsNullOrEmpty(profile.Name) ? loginText : profile.Name;
                    SessionAvatar.DisplayName = profile.Name;

                    // BitmapImage loads http(s) URIs directly, so no extra download plumbing is needed.
                    if (!string.IsNullOrEmpty(profile.AvatarUrl) &&
                        Uri.TryCreate(profile.AvatarUrl, UriKind.Absolute, out var avatarUri))
                    {
                        SessionAvatar.ProfilePicture = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(avatarUri);
                    }
                    else
                    {
                        SessionAvatar.ProfilePicture = null;
                    }

                    SessionChevron.Visibility = Visibility.Visible;
                }
                else
                {
                    SessionNameText.Text = loginText;
                    SessionAvatar.DisplayName = string.Empty;
                    // Without this the previous user's picture stayed on screen after logout,
                    // which made it look as though the session had not been cleared at all.
                    SessionAvatar.ProfilePicture = null;
                    SessionChevron.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to refresh the GameBanana session widget", ex);
            }
        }

        private MenuFlyout? _sessionMenu;
        private MenuFlyoutItem? _authorModsMenuItem;
        private MenuFlyoutItem? _logoutMenuItem;

        private async void SessionProfileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var profile = GameBananaProfileService.Cached;

                if (profile?.IsLoggedIn != true)
                {
                    await ShowLoginOverlayAsync();
                    await RefreshSessionWidgetAsync(forceRefresh: true);
                    return;
                }

                EnsureSessionMenu();

                if (_sessionMenu == null)
                {
                    return;
                }

                _authorModsMenuItem!.Text = SharedUtilities.GetTranslation(_lang, "AuthorModsTitle");
                _logoutMenuItem!.Text = SharedUtilities.GetTranslation(_lang, "Comments_Logout");

                _sessionMenu.ShowAt(SessionProfileButton);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.SessionProfileButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.SessionProfileButton_Click failed", ex);
            }
        }

        private void EnsureSessionMenu()
        {
            if (_sessionMenu != null)
            {
                return;
            }

            _authorModsMenuItem = new MenuFlyoutItem { Icon = new FontIcon { Glyph = "\uEA86" } };
            _authorModsMenuItem.Click += AuthorModsMenuItem_Click;

            _logoutMenuItem = new MenuFlyoutItem { Icon = new FontIcon { Glyph = "\uE7E8" } };
            _logoutMenuItem.Click += LogoutMenuItem_Click;

            _sessionMenu = new MenuFlyout();
            _sessionMenu.Items.Add(_authorModsMenuItem);
            _sessionMenu.Items.Add(_logoutMenuItem);
        }

        private async void AuthorModsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var profile = GameBananaProfileService.Cached;
                if (profile?.IsLoggedIn != true || string.IsNullOrEmpty(profile.Name))
                {
                    return;
                }

                // ShowAuthorModsAsync is the full entry point: it sets the title, navigation state and
                // grid source before fetching. LoadAuthorModsAsync only loads the next page.
                _currentAuthorName = profile.Name;
                _currentAuthorId = profile.MemberId;
                await ShowAuthorModsAsync();
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.AuthorModsMenuItem_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.AuthorModsMenuItem_Click failed", ex);
            }
        }

        private async void LogoutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CloudflareBypassService.ClearCookies();
                GameBananaProfileService.Invalidate();

                SessionAvatar.ProfilePicture = null;
                SessionAvatar.DisplayName = string.Empty;
                SessionNameText.Text = SharedUtilities.GetTranslation(_lang, "Comments_Login");
                SessionChevron.Visibility = Visibility.Collapsed;

                await ClearBrowserSessionAsync();

                Logger.LogInfo("GameBanana session: logged out, cookies, browser storage and profile cache cleared");

                await RefreshSessionWidgetAsync(forceRefresh: true);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.LogoutMenuItem_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.LogoutMenuItem_Click failed", ex);
            }
        }

        /// <summary>
        /// Wipes the WebView2 cookie store, not just the cookies this app saved.
        /// </summary>
        /// <remarks>
        /// The panels and the hidden comment browser share one WebView2 profile. Clearing only
        /// <see cref="CloudflareBypassService"/> left the session alive in that profile, so the
        /// site still considered the user signed in and refused a fresh login.
        /// </remarks>
        private static async Task ClearBrowserSessionAsync()
        {
            try
            {
                if (App.Current is not App app || app.MainWindow is not MainWindow mainWindow)
                {
                    return;
                }

                var container = mainWindow.GetHiddenWebViewContainer();
                if (container == null)
                {
                    return;
                }

                var webView = new Microsoft.UI.Xaml.Controls.WebView2();
                container.Children.Add(webView);

                try
                {
                    await webView.EnsureCoreWebView2Async();

                    if (webView.CoreWebView2 == null)
                    {
                        return;
                    }

                    webView.CoreWebView2.CookieManager.DeleteAllCookies();

                    try
                    {
                        await webView.CoreWebView2.Profile.ClearBrowsingDataAsync(
                            Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.Cookies);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Could not clear the WebView2 cookie profile: {ex.Message}");
                    }
                }
                finally
                {
                    container.Children.Remove(webView);
                    try { webView.Close(); } catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to clear the WebView2 session", ex);
            }
        }

        /// <summary>
        /// Opens the GameBanana login as a slide-out panel and stores the captured cookies.
        /// </summary>
        /// <returns>True when cookies were captured, false when the user cancelled.</returns>
        private async Task<bool> ShowLoginOverlayAsync()
        {
            const string loginUserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

            if (App.Current is not App app || app.MainWindow is not MainWindow mainWindow)
            {
                return false;
            }

            var (cookies, userAgent, dontAskAgain) = await mainWindow.ShowLoginPanelAsync(
                "https://gamebanana.com/members/account/login",
                "GameBanana Login");

            // "Don't ask again" applies whether the user logged in or cancelled.
            if (dontAskAgain)
            {
                SettingsManager.Current.CommentsLoginPromptDismissed = true;
                SettingsManager.Save();
            }

            if (string.IsNullOrEmpty(cookies))
            {
                // The header widget must still reflect reality when the user cancelled.
                await RefreshSessionWidgetAsync(forceRefresh: true);
                return false;
            }

            // Store the session before refreshing the header: the profile request uses the
            // cached cookies, so refreshing first always reported "logged out".
            CloudflareBypassService.SetCookiesManually(cookies, userAgent ?? loginUserAgent);

            await RefreshSessionWidgetAsync(forceRefresh: true);
            return true;
        }
    }
}
