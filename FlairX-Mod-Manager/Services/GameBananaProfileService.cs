using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Identity of the GameBanana account behind the stored session.
    /// </summary>
    public sealed class GameBananaProfile
    {
        public bool IsLoggedIn { get; init; }
        public int MemberId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string AvatarUrl { get; init; } = string.Empty;

        public static GameBananaProfile Anonymous { get; } = new GameBananaProfile { IsLoggedIn = false };
    }

    /// <summary>
    /// Resolves who the stored GameBanana session belongs to.
    /// </summary>
    /// <remarks>
    /// GameBanana's public API has no "current user" endpoint, but the site's own
    /// <c>apiv13/Member/UiConfig</c> answers that question when called with the session cookies:
    /// it returns <c>_bIsLoggedIn</c>, <c>_idMemberRow</c> and <c>_sAvatarUrl</c>. The display name
    /// then comes from the member data endpoint by id.
    ///
    /// The cookie header and the User-Agent must come from the same capture: <c>cf_clearance</c>
    /// is bound to the exact User-Agent, and a mismatch makes the site report "not logged in"
    /// even though the session cookie is still valid.
    /// </remarks>
    public static class GameBananaProfileService
    {
        private const string UaFallback =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

        private static readonly HttpClient Http = CreateClient();
        private static readonly SemaphoreSlim Gate = new(1, 1);

        private static GameBananaProfile? _cached;

        /// <summary>Last resolved profile, or null when it has not been resolved yet.</summary>
        public static GameBananaProfile? Cached => _cached;

        private static HttpClient CreateClient()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                ConnectTimeout = TimeSpan.FromSeconds(15),
            };

            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return client;
        }

        /// <summary>Drops the cached profile, e.g. after logout.</summary>
        public static void Invalidate()
        {
            _cached = null;
        }

        /// <summary>
        /// Resolves the profile behind the stored session, caching the result.
        /// Returns <see cref="GameBananaProfile.Anonymous"/> when there is no valid session.
        /// </summary>
        public static async Task<GameBananaProfile> GetAsync(bool forceRefresh = false, CancellationToken token = default)
        {
            if (!forceRefresh && _cached != null)
            {
                return _cached;
            }

            // One resolution at a time - the overlay and the comments panel can both ask.
            if (!await Gate.WaitAsync(TimeSpan.FromSeconds(10), token).ConfigureAwait(false))
            {
                return _cached ?? GameBananaProfile.Anonymous;
            }

            try
            {
                if (!forceRefresh && _cached != null)
                {
                    return _cached;
                }

                _cached = await ResolveAsync(token).ConfigureAwait(false);
                return _cached;
            }
            catch (Exception ex)
            {
                Logger.LogError("GameBananaProfileService: failed to resolve profile", ex);
                return GameBananaProfile.Anonymous;
            }
            finally
            {
                Gate.Release();
            }
        }

        private static async Task<GameBananaProfile> ResolveAsync(CancellationToken token)
        {
            var cookies = CloudflareBypassService.GetCachedCookies();
            if (string.IsNullOrEmpty(cookies))
            {
                return GameBananaProfile.Anonymous;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://gamebanana.com/apiv13/Member/UiConfig");
            request.Headers.TryAddWithoutValidation("Cookie", cookies);
            request.Headers.TryAddWithoutValidation("User-Agent", CloudflareBypassService.GetCachedUserAgent() ?? UaFallback);

            using var response = await Http.SendAsync(request, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return GameBananaProfile.Anonymous;
            }

            var json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var isLoggedIn = root.TryGetProperty("_bIsLoggedIn", out var loggedIn) && loggedIn.ValueKind == JsonValueKind.True;

            var memberId = root.TryGetProperty("_idMemberRow", out var idElement) && idElement.TryGetInt32(out var id) ? id : 0;

            var avatarUrl = root.TryGetProperty("_sAvatarUrl", out var avatar) && avatar.ValueKind == JsonValueKind.String
                ? avatar.GetString() ?? string.Empty
                : string.Empty;

            if (!isLoggedIn || memberId == 0)
            {
                return GameBananaProfile.Anonymous;
            }

            var name = await ResolveNameAsync(memberId, token).ConfigureAwait(false);

            Logger.LogInfo($"GameBananaProfileService: resolved member {memberId} ('{name}')");
            return new GameBananaProfile
            {
                IsLoggedIn = true,
                MemberId = memberId,
                Name = name,
                AvatarUrl = avatarUrl,
            };
        }

        private static async Task<string> ResolveNameAsync(int memberId, CancellationToken token)
        {
            // This endpoint needs no cookies.
            var url = $"https://api.gamebanana.com/Core/Item/Data?itemtype=Member&itemid={memberId}&fields=name&return_keys=1";

            try
            {
                using var response = await Http.GetAsync(url, token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return string.Empty;
                }

                var json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                using var document = JsonDocument.Parse(json);

                return document.RootElement.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"GameBananaProfileService: could not resolve name for member {memberId}: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
