using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Services
{
    public class GameBananaService
    {
        private static readonly HttpClient _httpClient = CreateHttpClient();
        
        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
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
            { "SRMI", 18366 }   // Honkai Star Rail
        };

        public static int GetGameId(string gameTag)
        {
            return GameIds.TryGetValue(gameTag, out var id) ? id : 0;
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

        // Mod details response
        public class ModDetailsResponse
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sText")]
            public string Description { get; set; } = "";
            
            [JsonPropertyName("_sProfileUrl")]
            public string ProfileUrl { get; set; } = "";
            
            [JsonPropertyName("_aSubmitter")]
            public Submitter? Submitter { get; set; }
            
            [JsonPropertyName("_aFiles")]
            public List<ModFile>? Files { get; set; }
            
            [JsonPropertyName("_aPreviewMedia")]
            public PreviewMedia? PreviewMedia { get; set; }
            
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
            
            [JsonPropertyName("_aCategory")]
            public ModCategory? Category { get; set; }
            
            [JsonPropertyName("_aGame")]
            public object? Game { get; set; }
            
            [JsonPropertyName("_sVersion")]
            public string? Version { get; set; }
            
            [JsonPropertyName("_bIsObsolete")]
            public bool IsObsolete { get; set; }
            
            [JsonPropertyName("_aRootCategory")]
            public RootCategory? RootCategory { get; set; }
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
        /// Download a file from GameBanana
        /// </summary>
        public static async Task<bool> DownloadFileAsync(string downloadUrl, string destinationPath, IProgress<double>? progress = null)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "FlairX-Mod-Manager/2.6.8");

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
    }
}
