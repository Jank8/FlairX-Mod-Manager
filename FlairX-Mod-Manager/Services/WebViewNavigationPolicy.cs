using System;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Main-frame navigation allowlist for the embedded browser. GameBanana pages and the
    /// Cloudflare/reCAPTCHA frames needed by its sign-in are allowed; everything else stays
    /// inside the app instead of opening arbitrary sites.
    /// </summary>
    public static class WebViewNavigationPolicy
    {
        public static bool IsAllowed(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                return false;
            }

            var host = uri.Host;
            if (host.Equals("gamebanana.com", StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith(".gamebanana.com", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Cloudflare Turnstile and the reCAPTCHA frame are required by the login page.
            if (host.Equals("challenges.cloudflare.com", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (host.Equals("recaptcha.google.com", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (host.Equals("www.google.com", StringComparison.OrdinalIgnoreCase) &&
                uri.AbsolutePath.StartsWith("/recaptcha", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
