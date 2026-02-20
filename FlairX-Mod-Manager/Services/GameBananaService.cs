using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace FlairX_Mod_Manager.Services
{
    public class GameBananaService
    {
        private static readonly HttpClient _httpClient = CreateHttpClient();
        
        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli,
                UseCookies = true,
                AllowAutoRedirect = true
            };
            
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            
            // Mimic a real browser to bypass Cloudflare
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            client.DefaultRequestHeaders.Add("DNT", "1");
            client.DefaultRequestHeaders.Add("Connection", "keep-alive");
            client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
            client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
            client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
            
            return client;
        }
        
        // Game ID mapping
        private static readonly Dictionary<string, int> GameIds = new()
        {
            { "ZZMI", 19567 },  // Zenless Zone Zero
            { "GIMI", 8552 },   // Genshin Impact
            { "HIMI", 10349 },  // Honkai Impact 3rd
            { "WWMI", 20357 },  // Wuthering Waves
            { "SRMI", 18366 },  // Honkai Star Rail
            { "EFMI", 21842 }   // Arknights: Endfield
        };

        // Character category ID mapping (parent categories containing character subcategories)
        private static readonly Dictionary<string, int> CharacterCategoryIds = new()
        {
            { "ZZMI", 30305 },  // Zenless Zone Zero - Characters
            { "GIMI", 18140 },  // Genshin Impact - Characters
            { "HIMI", 23620 },  // Honkai Impact 3rd - Characters
            { "WWMI", 29524 },  // Wuthering Waves - Skins
            { "SRMI", 22832 },  // Honkai Star Rail - Characters
            { "EFMI", 42770 }   // Arknights: Endfield - Operators
        };

        public static int GetGameId(string gameTag)
        {
            return GameIds.TryGetValue(gameTag, out var id) ? id : 0;
        }

        public static int GetCharacterCategoryId(string gameTag)
        {
            return CharacterCategoryIds.TryGetValue(gameTag, out var id) ? id : 0;
        }

        // Mod list response models
        public class ModListResponse
        {
            [JsonPropertyName("_aRecords")]
            public List<ModRecord>? Records { get; set; }
            
            [JsonPropertyName("_aMetadata")]
            public ResponseMetadata? Metadata { get; set; }
            
            [JsonPropertyName("_nRecordCount")]
            public int RecordCount { get; set; }
            
            [JsonPropertyName("_nPerpage")]
            public int PerPage { get; set; }
        }

        public class ResponseMetadata
        {
            [JsonPropertyName("_nRecordCount")]
            public int RecordCount { get; set; }
            
            [JsonPropertyName("_nPerpage")]
            public int PerPage { get; set; }
            
            [JsonPropertyName("_bIsComplete")]
            public bool IsComplete { get; set; }
        }

        public class ModRecord
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sProfileUrl")]
            public string ProfileUrl { get; set; } = "";
            
            [JsonPropertyName("_tsDateAdded")]
            public long DateAdded { get; set; }
            
            [JsonPropertyName("_tsDateModified")]
            public long DateModified { get; set; }
            
            [JsonPropertyName("_tsDateUpdated")]
            public long DateUpdated { get; set; }
            
            [JsonPropertyName("_nViewCount")]
            public int ViewCount { get; set; }
            
            [JsonPropertyName("_nLikeCount")]
            public int LikeCount { get; set; }
            
            [JsonPropertyName("_nDownloadCount")]
            public int DownloadCount { get; set; }
            
            [JsonPropertyName("_nPostCount")]
            public int PostCount { get; set; }
            
            [JsonPropertyName("_aPreviewMedia")]
            public PreviewMedia? PreviewMedia { get; set; }
            
            [JsonPropertyName("_aSubmitter")]
            public Submitter? Submitter { get; set; }
            
            [JsonPropertyName("_sText")]
            public string? Description { get; set; }
            
            [JsonPropertyName("_aMetadata")]
            public Dictionary<string, object>? Metadata { get; set; }
            
            [JsonPropertyName("_aRootCategory")]
            public RootCategory? RootCategory { get; set; }
            
            [JsonPropertyName("_bHasContentRatings")]
            public bool HasContentRatings { get; set; }
            
            [JsonPropertyName("_bHasRipe")]
            public bool HasRipe { get; set; }
            
            // Helper properties to get stats from metadata if main properties are 0
            public int GetDownloadCount()
            {
                if (DownloadCount > 0) return DownloadCount;
                if (Metadata != null && Metadata.TryGetValue("_nDownloadCount", out var value))
                {
                    if (value is JsonElement element && element.ValueKind == JsonValueKind.Number)
                        return element.GetInt32();
                }
                return 0;
            }
            
            public int GetViewCount()
            {
                if (ViewCount > 0) return ViewCount;
                if (Metadata != null && Metadata.TryGetValue("_nViewCount", out var value))
                {
                    if (value is JsonElement element && element.ValueKind == JsonValueKind.Number)
                        return element.GetInt32();
                }
                return 0;
            }
            
            public int GetLikeCount()
            {
                if (LikeCount > 0) return LikeCount;
                if (Metadata != null && Metadata.TryGetValue("_nLikeCount", out var value))
                {
                    if (value is JsonElement element && element.ValueKind == JsonValueKind.Number)
                        return element.GetInt32();
                }
                return 0;
            }
        }

        public class PreviewMedia
        {
            [JsonPropertyName("_aImages")]
            public List<ImageInfo>? Images { get; set; }
        }

        public class ImageInfo
        {
            [JsonPropertyName("_sType")]
            public string Type { get; set; } = "";
            
            [JsonPropertyName("_sBaseUrl")]
            public string BaseUrl { get; set; } = "";
            
            [JsonPropertyName("_sFile")]
            public string File { get; set; } = "";
            
            [JsonPropertyName("_sFile530")]
            public string? File530 { get; set; }
            
            [JsonPropertyName("_sFile220")]
            public string? File220 { get; set; }
            
            [JsonPropertyName("_sFile100")]
            public string? File100 { get; set; }
        }

        public class ModCategory
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sModelName")]
            public string? ModelName { get; set; }
            
            [JsonPropertyName("_sProfileUrl")]
            public string? ProfileUrl { get; set; }
            
            [JsonPropertyName("_sIconUrl")]
            public string? IconUrl { get; set; }
        }

        public class Submitter
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sProfileUrl")]
            public string ProfileUrl { get; set; } = "";
            
            [JsonPropertyName("_sAvatarUrl")]
            public string? AvatarUrl { get; set; }
        }

        public class RootCategory
        {
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sProfileUrl")]
            public string ProfileUrl { get; set; } = "";
            
            [JsonPropertyName("_sIconUrl")]
            public string? IconUrl { get; set; }
        }

        public class Game
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sAbbreviation")]
            public string Abbreviation { get; set; } = "";
        }

        public class AuthorMatchResponse
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }
            
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("avatar")]
            public string? Avatar { get; set; }
        }

        // Mod details response
        public class ModDetailsResponse
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sText")]
            public string? Description { get; set; }
            
            [JsonPropertyName("_sProfileUrl")]
            public string ProfileUrl { get; set; } = "";
            
            [JsonPropertyName("_aSubmitter")]
            public Submitter? Submitter { get; set; }
            
            [JsonPropertyName("_aFiles")]
            public List<ModFile>? Files { get; set; }
            
            [JsonPropertyName("_aPreviewMedia")]
            public PreviewMedia? PreviewMedia { get; set; }
            
            [JsonPropertyName("_tsDateAdded")]
            public long? DateAdded { get; set; }
            
            [JsonPropertyName("_tsDateModified")]
            public long? DateModified { get; set; }
            
            [JsonPropertyName("_tsDateUpdated")]
            public long? DateUpdated { get; set; }
            
            [JsonPropertyName("_nViewCount")]
            public int? ViewCount { get; set; }
            
            [JsonPropertyName("_nLikeCount")]
            public int? LikeCount { get; set; }
            
            [JsonPropertyName("_nDownloadCount")]
            public int? DownloadCount { get; set; }
            
            [JsonPropertyName("_nPostCount")]
            public int? PostCount { get; set; }
            
            [JsonPropertyName("_aCategory")]
            public ModCategory? Category { get; set; }
            
            [JsonPropertyName("_aGame")]
            public Game? Game { get; set; }
            
            [JsonPropertyName("_sVersion")]
            public string? Version { get; set; }
            
            [JsonPropertyName("_bIsObsolete")]
            public bool? IsObsolete { get; set; }
            
            [JsonPropertyName("_aRootCategory")]
            public RootCategory? RootCategory { get; set; }
            
            /// <summary>
            /// Check if mod data is valid (not private/removed)
            /// </summary>
            public bool IsAvailable => Submitter != null && Files != null;
            
            /// <summary>
            /// Get download count with fallback to 0
            /// </summary>
            public int GetDownloadCount() => DownloadCount ?? 0;
            
            /// <summary>
            /// Get view count with fallback to 0
            /// </summary>
            public int GetViewCount() => ViewCount ?? 0;
            
            /// <summary>
            /// Get like count with fallback to 0
            /// </summary>
            public int GetLikeCount() => LikeCount ?? 0;
            
            /// <summary>
            /// Check if mod has NSFW content
            /// Note: This information is not reliably available in mod details API
            /// Use the IsRated field from mod list instead when available
            /// </summary>
            public bool IsNSFW() => false; // Not available in details API, use IsRated from list
        }

        public class ModFile
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sFile")]
            public string FileName { get; set; } = "";
            
            [JsonPropertyName("_nFilesize")]
            public long FileSize { get; set; }
            
            [JsonPropertyName("_sDescription")]
            public string? Description { get; set; }
            
            [JsonPropertyName("_sDownloadUrl")]
            public string DownloadUrl { get; set; } = "";
            
            [JsonPropertyName("_tsDateAdded")]
            public long DateAdded { get; set; }
            
            [JsonPropertyName("_nDownloadCount")]
            public int DownloadCount { get; set; }
        }

        /// <summary>
        /// Get list of mods for a specific game using proper API endpoints
        /// </summary>
        public static async Task<ModListResponse?> GetModsAsync(
            string gameTag, 
            int page = 1, 
            string? search = null, 
            string? sort = null,
            string? feedType = null,
            List<string>? includeSections = null,
            List<string>? excludeSections = null,
            List<string>? includeTags = null,
            List<string>? excludeTags = null)
        {
            try
            {
                var gameId = GetGameId(gameTag);
                if (gameId == 0)
                {
                    Logger.LogError($"Unknown game tag: {gameTag}");
                    return null;
                }

                // Build API URL - use Subfeed with proper properties
                var url = $"https://gamebanana.com/apiv11/Game/{gameId}/Subfeed?_nPage={page}&_nPerpage=50&_csvModelInclusions=Mod&_csvProperties=_idRow,_sName,_sProfileUrl,_tsDateAdded,_tsDateModified,_tsDateUpdated,_nViewCount,_nLikeCount,_nDownloadCount,_aPreviewMedia,_aSubmitter,_bHasContentRatings";
                
                if (!string.IsNullOrEmpty(search))
                {
                    url += $"&_sName={Uri.EscapeDataString(search)}";
                }
                
                // Sort by feed type
                if (!string.IsNullOrEmpty(feedType))
                {
                    switch (feedType)
                    {
                        case "new":
                            url += "&_sOrderBy=_tsDateAdded,DESC";
                            break;
                        case "updated":
                            url += "&_sOrderBy=_tsDateUpdated,DESC";
                            break;
                    }
                }

                // Log the URL for debugging
                Logger.LogInfo($"GameBanana API URL: {url}");

                // Use cached cookies if available
                var cookies = CloudflareBypassService.GetCachedCookies();
                var userAgent = CloudflareBypassService.GetCachedUserAgent();

                Logger.LogInfo($"Using cookies: {(string.IsNullOrEmpty(cookies) ? "NONE" : cookies.Substring(0, Math.Min(50, cookies.Length)) + "...")}");
                Logger.LogInfo($"Using User-Agent: {userAgent ?? "DEFAULT"}");

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", userAgent ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                if (!string.IsNullOrEmpty(cookies))
                {
                    request.Headers.Add("Cookie", cookies);
                }

                var response = await _httpClient.SendAsync(request);
                
                Logger.LogInfo($"Response status: {response.StatusCode}");
                
                // If we get 403/503, return null to trigger dialog in UI layer
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || 
                    response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    Logger.LogInfo("Got blocked by Cloudflare, need user verification");
                    return null;
                }
                
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ModListResponse>(content);
                return result;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                Logger.LogError($"Timeout while fetching mods from GameBanana", ex);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogError($"Request cancelled while fetching mods from GameBanana", ex);
                return null;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError($"Network error while fetching mods from GameBanana: {ex.Message}", ex);
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to fetch mods from GameBanana: {ex.Message}", ex);
                return null;
            }
        }


        




        /// <summary>
        /// Get detailed information about a specific mod
        /// </summary>
        public static async Task<ModDetailsResponse?> GetModDetailsAsync(int modId)
        {
            try
            {
                var url = $"https://gamebanana.com/apiv11/Mod/{modId}?_csvProperties=_idRow,_sName,_sText,_sProfileUrl,_aSubmitter,_aFiles,_aPreviewMedia,_tsDateAdded,_tsDateModified,_tsDateUpdated,_nViewCount,_nLikeCount,_nDownloadCount,_nPostCount,_aCategory,_aGame,_sVersion,_bIsObsolete,_aRootCategory";

                var cookies = CloudflareBypassService.GetCachedCookies();
                var userAgent = CloudflareBypassService.GetCachedUserAgent();

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", userAgent ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                if (!string.IsNullOrEmpty(cookies))
                {
                    request.Headers.Add("Cookie", cookies);
                }

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ModDetailsResponse>(content);

                return result;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                Logger.LogError($"Timeout while fetching mod details from GameBanana", ex);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogError($"Request cancelled while fetching mod details from GameBanana", ex);
                return null;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError($"Network error while fetching mod details from GameBanana: {ex.Message}", ex);
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to fetch mod details from GameBanana: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Get files for a specific mod (for update checking)
        /// </summary>
        public static async Task<List<ModFile>?> GetModFilesAsync(int modId)
        {
            try
            {
                var details = await GetModDetailsAsync(modId);
                return details?.Files;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to fetch mod files from GameBanana: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Download a file from GameBanana using multi-threaded downloader
        /// </summary>
        public static async Task<bool> DownloadFileAsync(string downloadUrl, string destinationPath, IProgress<double>? progress = null)
        {
            try
            {
                // Use multi-threaded downloader if enabled
                if (SettingsManager.GetFastDownloadEnabled())
                {
                    var maxConnections = SettingsManager.GetMaxDownloadConnections();
                    return await MultiThreadDownloader.DownloadFileAsync(downloadUrl, destinationPath, progress, maxConnections);
                }
                
                // Fallback to single connection download
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", $"FlairX-Mod-Manager/{AppConstants.APP_VERSION}");

                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1 && progress != null;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new System.IO.FileStream(destinationPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalRead = 0L;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (canReportProgress)
                    {
                        progress!.Report((double)totalRead / totalBytes * 100);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to download file from GameBanana: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Extract mod ID from GameBanana URL
        /// </summary>
        public static int? ExtractModIdFromUrl(string url)
        {
            try
            {
                // Pattern: gamebanana.com/mods/123456
                var match = System.Text.RegularExpressions.Regex.Match(url, @"gamebanana\.com/mods/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var modId))
                {
                    return modId;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Generic method for making API calls to GameBanana
        /// </summary>
        public static async Task<T?> MakeApiCallAsync<T>(string url, string? cookies = null)
        {
            try
            {
                Logger.LogInfo($"Making API call to: {url}");
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                
                if (!string.IsNullOrEmpty(cookies))
                {
                    request.Headers.Add("Cookie", cookies);
                }

                var response = await _httpClient.SendAsync(request);
                
                // If we get 403/503, return default to trigger dialog in UI layer
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || 
                    response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    Logger.LogInfo("Got blocked by Cloudflare, need user verification");
                    return default(T);
                }
                
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<T>(content);
                return result;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                Logger.LogError($"Timeout while making API call to {url}", ex);
                return default(T);
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogError($"Request cancelled while making API call to {url}", ex);
                return default(T);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError($"Network error while making API call to {url}: {ex.Message}", ex);
                return default(T);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to make API call to {url}: {ex.Message}", ex);
                return default(T);
            }
        }

        /// <summary>
        /// Get author ID by username
        /// </summary>
        public static async Task<int?> GetAuthorIdByUsernameAsync(string username)
        {
            try
            {
                var url = $"https://api.gamebanana.com/Core/Member/Match?username={Uri.EscapeDataString(username)}";
                Logger.LogInfo($"Looking up author ID for username: {username}");
                Logger.LogInfo($"API URL: {url}");
                
                var response = await MakeApiCallAsync<AuthorMatchResponse[]>(url);
                
                Logger.LogInfo($"API response received: {response?.Length ?? 0} results");
                
                if (response != null && response.Length > 0)
                {
                    var author = response[0];
                    Logger.LogInfo($"First result - ID: {author.Id}, Name: '{author.Name}', Avatar: {author.Avatar}");
                    
                    if (author.Id > 0)
                    {
                        Logger.LogInfo($"Found author ID {author.Id} for username {username} (display name: {author.Name})");
                        return author.Id;
                    }
                    else
                    {
                        Logger.LogWarning($"Author ID is 0 or negative for username: {username}");
                    }
                }
                
                Logger.LogInfo($"No valid author found for username: {username}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to get author ID for username {username}: {ex.Message}", ex);
                Logger.LogError($"Exception details: {ex}");
                return null;
            }
        }

        // Category response models
        public class CategoryListResponse
        {
            [JsonPropertyName("_aRecords")]
            public List<CategoryRecord>? Records { get; set; }
            
            [JsonPropertyName("_aMetadata")]
            public ResponseMetadata? Metadata { get; set; }
            
            [JsonPropertyName("_nRecordCount")]
            public int RecordCount { get; set; }
        }

        public class CategoryRecord
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sProfileUrl")]
            public string ProfileUrl { get; set; } = "";
            
            [JsonPropertyName("_sIconUrl")]
            public string? IconUrl { get; set; }
            
            [JsonPropertyName("_aPreviewMedia")]
            public PreviewMediaCategory? PreviewMedia { get; set; }
            
            [JsonPropertyName("_aGame")]
            public Game? Game { get; set; }
            
            [JsonPropertyName("_bHasFiles")]
            public bool HasFiles { get; set; }
            
            [JsonPropertyName("_tsDateAdded")]
            public long? DateAdded { get; set; }
            
            /// <summary>
            /// Get icon URL from either _sIconUrl or _aPreviewMedia
            /// </summary>
            public string? GetIconUrl()
            {
                // Try _sIconUrl first
                if (!string.IsNullOrEmpty(IconUrl))
                    return IconUrl;
                
                // Try _aPreviewMedia._aImages[0]._sUrl
                if (PreviewMedia?.Images != null && PreviewMedia.Images.Count > 0)
                {
                    var iconImage = PreviewMedia.Images.FirstOrDefault(i => i.Type == "icon");
                    if (iconImage != null)
                        return iconImage.Url;
                }
                
                return null;
            }
        }

        public class PreviewMediaCategory
        {
            [JsonPropertyName("_aImages")]
            public List<CategoryImage>? Images { get; set; }
        }

        public class CategoryImage
        {
            [JsonPropertyName("_sType")]
            public string Type { get; set; } = "";
            
            [JsonPropertyName("_sUrl")]
            public string Url { get; set; } = "";
        }

        /// <summary>
        /// Get all character categories for a specific game using WebView2 to render JavaScript
        /// </summary>
        public static async Task<List<CategoryRecord>?> GetCharacterCategoriesAsync(string gameTag)
        {
            try
            {
                var categoryId = GetCharacterCategoryId(gameTag);
                if (categoryId == 0)
                {
                    Logger.LogError($"Unknown game tag or no character category defined: {gameTag}");
                    return null;
                }

                var url = $"https://gamebanana.com/mods/cats/{categoryId}";
                Logger.LogInfo($"Fetching character categories from: {url}");

                // Use WebView2 to render the page and get the HTML
                var html = await RenderPageWithWebView2Async(url);
                
                if (string.IsNullOrEmpty(html))
                {
                    Logger.LogError("Failed to render page with WebView2");
                    return null;
                }

                Logger.LogInfo($"Received rendered HTML length: {html.Length} characters");

                // Parse HTML to extract subcategories
                var categories = ParseCharacterCategoriesFromHtml(html);

                Logger.LogInfo($"Found {categories.Count} character categories for {gameTag}");
                return categories;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to fetch character categories for {gameTag}: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Render a page using WebView2 and return the HTML after JavaScript execution
        /// </summary>
        private static async Task<string?> RenderPageWithWebView2Async(string url)
        {
            try
            {
                Logger.LogInfo($"Initializing WebView2 for URL: {url}");
                
                var tcs = new TaskCompletionSource<string?>();
                Microsoft.UI.Xaml.Controls.WebView2? webView = null;

                // Must run on UI thread - get from App.Current
                var app = App.Current as App;
                var mainWindow = app?.MainWindow as MainWindow;
                
                if (mainWindow == null)
                {
                    Logger.LogError("Could not get main window for WebView2");
                    return null;
                }

                mainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        webView = new Microsoft.UI.Xaml.Controls.WebView2();
                        
                        // Initialize WebView2
                        await webView.EnsureCoreWebView2Async();
                        
                        Logger.LogInfo("WebView2 initialized, navigating to URL");
                        
                        // Set up navigation completed handler
                        webView.CoreWebView2.NavigationCompleted += async (sender, args) =>
                        {
                            try
                            {
                                if (args.IsSuccess)
                                {
                                    Logger.LogInfo("Navigation completed successfully, waiting for content to load");
                                    
                                    // Wait for JavaScript to render content
                                    await Task.Delay(3000);
                                    
                                    // Get the rendered HTML
                                    var renderedHtml = await webView.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");
                                    
                                    // Remove JSON string quotes
                                    if (!string.IsNullOrEmpty(renderedHtml) && renderedHtml.StartsWith("\"") && renderedHtml.EndsWith("\""))
                                    {
                                        renderedHtml = System.Text.Json.JsonSerializer.Deserialize<string>(renderedHtml);
                                    }
                                    
                                    Logger.LogInfo($"Retrieved rendered HTML, length: {renderedHtml?.Length ?? 0}");
                                    tcs.SetResult(renderedHtml);
                                }
                                else
                                {
                                    Logger.LogError($"Navigation failed: {args.WebErrorStatus}");
                                    tcs.SetResult(null);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Error in NavigationCompleted handler: {ex.Message}", ex);
                                tcs.SetResult(null);
                            }
                        };
                        
                        // Navigate to the URL
                        webView.CoreWebView2.Navigate(url);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error initializing WebView2: {ex.Message}", ex);
                        tcs.SetResult(null);
                    }
                });

                // Wait for the result with timeout
                var timeoutTask = Task.Delay(15000);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    Logger.LogError("WebView2 rendering timed out");
                    return null;
                }

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to render page with WebView2: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Parse character categories from HTML
        /// </summary>
        private static List<CategoryRecord> ParseCharacterCategoriesFromHtml(string html)
        {
            var categories = new List<CategoryRecord>();

            try
            {
                Logger.LogInfo("Starting HTML parsing for character categories");
                
                // Split by <record> tags to process each record individually
                var recordPattern = @"<record[^>]*>(.*?)</record>";
                var recordMatches = System.Text.RegularExpressions.Regex.Matches(html, recordPattern, System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                Logger.LogInfo($"Found {recordMatches.Count} record elements in HTML");

                foreach (System.Text.RegularExpressions.Match recordMatch in recordMatches)
                {
                    var recordContent = recordMatch.Groups[1].Value;
                    
                    // Extract category ID from href
                    var idMatch = System.Text.RegularExpressions.Regex.Match(recordContent, @"href=""https://gamebanana\.com/mods/cats/(\d+)""");
                    if (!idMatch.Success)
                    {
                        Logger.LogWarning("Could not find category ID in record");
                        continue;
                    }
                    var idStr = idMatch.Groups[1].Value;
                    
                    // Extract icon URL from img src
                    var iconMatch = System.Text.RegularExpressions.Regex.Match(recordContent, @"<img[^>]*src=""([^""]+)""");
                    if (!iconMatch.Success)
                    {
                        Logger.LogWarning($"Could not find icon URL for category ID {idStr}");
                        continue;
                    }
                    var iconUrl = iconMatch.Groups[1].Value;
                    
                    // Extract name from alt attribute
                    var altMatch = System.Text.RegularExpressions.Regex.Match(recordContent, @"alt=""([^""]+)\s+category\s+icon""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    string name;
                    if (altMatch.Success)
                    {
                        name = altMatch.Groups[1].Value.Trim();
                    }
                    else
                    {
                        // Fallback: try to get name from the second <a> tag content
                        var nameMatch = System.Text.RegularExpressions.Regex.Match(recordContent, @"<recordcell[^>]*class=""Info""[^>]*>.*?<a[^>]*>([^<]+)</a>", System.Text.RegularExpressions.RegexOptions.Singleline);
                        if (nameMatch.Success)
                        {
                            name = nameMatch.Groups[1].Value.Trim();
                        }
                        else
                        {
                            Logger.LogWarning($"Could not find name for category ID {idStr}");
                            continue;
                        }
                    }

                    if (int.TryParse(idStr, out var id))
                    {
                        var category = new CategoryRecord
                        {
                            Id = id,
                            Name = name,
                            IconUrl = iconUrl,
                            ProfileUrl = $"https://gamebanana.com/mods/cats/{id}"
                        };

                        categories.Add(category);
                        Logger.LogInfo($"Parsed category: {name} (ID: {id}, Icon: {iconUrl})");
                    }
                    else
                    {
                        Logger.LogWarning($"Failed to parse category ID: {idStr}");
                    }
                }

                Logger.LogInfo($"Successfully parsed {categories.Count} categories from HTML");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to parse categories from HTML: {ex.Message}", ex);
            }

            return categories;
        }

        /// <summary>
        /// Filter categories to get only character/skin categories (exclude generic categories)
        /// </summary>
        private static List<CategoryRecord> FilterCharacterCategories(List<CategoryRecord> categories)
        {
            // Blacklist of generic category names to exclude
            var blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Characters", "Skins", "Other/Misc", "Other", "Misc",
                "Weapons", "UI", "Maps", "Textures", "Sounds", "Audio",
                "Scripts", "Tools", "Utilities", "Patches", "Fixes",
                "Translations", "Translation", "Mods", "Modpacks",
                "Save Files", "Saves", "Config", "Configuration",
                "Effects", "Particles", "Shaders", "Models", "Animations",
                "HUD", "Menu", "Interface", "Icons", "Fonts",
                "Music", "Voice", "SFX", "Gameplay", "Balance",
                "Cheats", "Trainers", "Mods & Maps", "Input Icons",
                "House Exteriors", "Patch"
            };

            return categories
                .Where(c =>
                {
                    // Must have an icon
                    if (string.IsNullOrEmpty(c.GetIconUrl()))
                        return false;

                    // Must not be in blacklist
                    if (blacklist.Contains(c.Name))
                        return false;

                    // Optionally: must have files (uncomment if needed)
                    // if (!c.HasFiles)
                    //     return false;

                    return true;
                })
                .OrderBy(c => c.Name)
                .ToList();
        }

        /// <summary>
        /// Download category icon and save as icon.png
        /// </summary>
        public static async Task<bool> DownloadCategoryIconAsync(string iconUrl, string destinationFolder)
        {
            try
            {
                if (string.IsNullOrEmpty(iconUrl))
                {
                    Logger.LogWarning("Icon URL is empty, skipping download");
                    return false;
                }

                // Create destination folder if it doesn't exist
                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                var iconPath = Path.Combine(destinationFolder, "icon.png");
                
                Logger.LogInfo($"Downloading icon from {iconUrl} to {iconPath}");

                // Download the icon
                var success = await DownloadFileAsync(iconUrl, iconPath);
                
                if (success)
                {
                    Logger.LogInfo($"Successfully downloaded icon to {iconPath}");
                }
                else
                {
                    Logger.LogWarning($"Failed to download icon from {iconUrl}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to download category icon: {ex.Message}", ex);
                return false;
            }
        }
    }
}
