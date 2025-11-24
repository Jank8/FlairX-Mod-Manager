using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Services
{
    public class CloudflareBypassService
    {
        private static string? _cookies;
        private static string? _userAgent;

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

        public static string? GetCachedCookies() => _cookies;
        public static string? GetCachedUserAgent() => _userAgent;
    }
}
