using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using FlairX_Mod_Manager;

namespace FlairX_Mod_Manager.Services
{
    public partial class GameBananaService
    {

        internal static string? GetListPreviewImageUrl(ImageInfo image)
        {
            var fileName = !string.IsNullOrWhiteSpace(image.File530) ? image.File530
                : !string.IsNullOrWhiteSpace(image.File220) ? image.File220
                : !string.IsNullOrWhiteSpace(image.File100) ? image.File100
                : image.File;

            if (string.IsNullOrWhiteSpace(image.BaseUrl) || string.IsNullOrWhiteSpace(fileName))
                return null;

            return $"{image.BaseUrl.TrimEnd('/')}/{fileName}";
        }

        internal static string GetSortParam(CategorySortOrder sort) => sort switch
        {
            CategorySortOrder.Newest         => "_tsDateAdded,DESC",
            CategorySortOrder.Oldest         => "_tsDateAdded,ASC",
            CategorySortOrder.MostLiked      => "_nLikeCount,DESC",
            CategorySortOrder.MostViewed     => "_nViewCount,DESC",
            CategorySortOrder.MostDownloaded => "_nDownloadCount,DESC",
            CategorySortOrder.MostCommented  => "_nPostCount,DESC",
            _                                => "_tsDateUpdated,DESC",
        };

        /// <summary>
        /// Get list of mods filtered by a specific category (e.g. Character Skins).
        /// Browse mode (no search): uses apiv6/Mod/ByCategory — supports sorting.
        /// Search mode: uses apiv11/Game/Subfeed with _aCategoryRowIds[] — supports name filtering.
        /// </summary>
        public static async Task<ModListResponse?> GetModsByCategoryAsync(
            string gameTag,
            int categoryId,
            int page = 1,
            string? search = null,
            CategorySortOrder sort = CategorySortOrder.LatestUpdated)
        {
            // When searching, switch to apiv11 Subfeed which supports _sName filtering in a category.
            // apiv6 ByCategory ignores any name filter parameter.
            if (!string.IsNullOrEmpty(search))
            {
                return await GetModsByCategorySearchAsync(gameTag, categoryId, page, search);
            }

            try
            {
                // apiv6 ByCategory — supports sorting, no name filtering
                // Note: _bHasContentRatings does NOT exist in apiv6, use _bIsNsfw instead
                var url = $"https://gamebanana.com/apiv6/Mod/ByCategory" +
                    $"?_csvProperties=_idRow,_sName,_sProfileUrl,_tsDateAdded,_tsDateUpdated" +
                    $",_aPreviewMedia,_aSubmitter,_bIsNsfw,_nLikeCount,_nViewCount,_nDownloadCount,_nPostCount" +
                    $"&_nPerpage=50" +
                    $"&_nPage={page}" +
                    $"&_aCategoryRowIds[]={categoryId}" +
                    $"&_sOrderBy={GetSortParam(sort)}";

                Logger.LogInfo($"GameBanana ByCategory API URL: {url}");

                var cookies = CloudflareBypassService.GetCachedCookies();
                var userAgent = CloudflareBypassService.GetCachedUserAgent();

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", userAgent ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                if (!string.IsNullOrEmpty(cookies))
                {
                    request.Headers.Add("Cookie", cookies);
                }

                var httpResponse = await _httpClient.SendAsync(request);

                Logger.LogInfo($"ByCategory response status: {httpResponse.StatusCode}");

                if (httpResponse.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                    httpResponse.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    Logger.LogInfo("Got blocked by Cloudflare on ByCategory request");
                    return null;
                }

                httpResponse.EnsureSuccessStatusCode();

                var content = await httpResponse.Content.ReadAsStringAsync();

                // apiv6 ByCategory returns a bare JSON array, not { _aRecords, _aMetadata }.
                // Total record count is in the response header: X-Gbapi-Metadata_nrecordcount
                var v6Records = JsonSerializer.Deserialize<List<ModRecordV6>>(content);
                if (v6Records == null) return null;

                // Read total count from header to compute whether more pages exist
                int totalCount = 0;
                if (httpResponse.Headers.TryGetValues("X-Gbapi-Metadata_nrecordcount", out var countValues))
                {
                    int.TryParse(countValues.FirstOrDefault(), out totalCount);
                }

                int perPage = 50;
                bool isComplete = totalCount > 0
                    ? (page * perPage) >= totalCount
                    : v6Records.Count < perPage;

                // Convert to the shared ModRecord / ModListResponse model
                var records = v6Records.Select(v6 => new ModRecord
                {
                    Id = v6.Id,
                    Name = v6.Name,
                    ProfileUrl = v6.ProfileUrl,
                    DateAdded = v6.DateAdded,
                    DateUpdated = v6.DateUpdated,
                    IsNsfw = v6.IsNsfw,
                    LikeCount = v6.LikeCount,
                    ViewCount = v6.ViewCount,
                    DownloadCount = v6.DownloadCount,
                    PostCount = v6.PostCount,
                    Submitter = v6.Submitter,
                    PreviewMedia = v6.PreviewImages != null
                        ? new PreviewMedia { Images = v6.PreviewImages }
                        : null
                }).ToList();

                return new ModListResponse
                {
                    Records = records,
                    Metadata = new ResponseMetadata
                    {
                        RecordCount = totalCount > 0 ? totalCount : v6Records.Count,
                        PerPage = perPage,
                        IsComplete = isComplete
                    }
                };
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                Logger.LogError($"Timeout while fetching mods by category from GameBanana", ex);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogError($"Request cancelled while fetching mods by category from GameBanana", ex);
                return null;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError($"Network error while fetching mods by category: {ex.Message}", ex);
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to fetch mods by category: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Search mods by name within a specific category using apiv11 Subfeed + _aCategoryRowIds[].
        /// Sorting is not supported by this endpoint (Subfeed ignores _sOrderBy when using _aCategoryRowIds).
        /// </summary>
        private static async Task<ModListResponse?> GetModsByCategorySearchAsync(
            string gameTag,
            int categoryId,
            int page,
            string search)
        {
            try
            {
                var gameId = GetGameId(gameTag);
                if (gameId == 0) return null;

                var url = $"https://gamebanana.com/apiv11/Game/{gameId}/Subfeed" +
                    $"?_csvModelInclusions=Mod" +
                    $"&_csvProperties=_idRow,_sName,_sProfileUrl,_tsDateAdded,_tsDateUpdated,_aPreviewMedia,_aSubmitter,_bHasContentRatings" +
                    $"&_nPerpage=50" +
                    $"&_nPage={page}" +
                    $"&_aCategoryRowIds[]={categoryId}" +
                    $"&_sName={Uri.EscapeDataString(search)}";

                Logger.LogInfo($"GameBanana ByCategory search URL: {url}");

                var cookies = CloudflareBypassService.GetCachedCookies();
                var userAgent = CloudflareBypassService.GetCachedUserAgent();

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", userAgent ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                if (!string.IsNullOrEmpty(cookies))
                    request.Headers.Add("Cookie", cookies);

                var httpResponse = await _httpClient.SendAsync(request);

                if (httpResponse.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                    httpResponse.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                    return null;

                httpResponse.EnsureSuccessStatusCode();

                var content = await httpResponse.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ModListResponse>(content);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                Logger.LogError("Timeout while searching mods by category", ex);
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to search mods by category: {ex.Message}", ex);
                return null;
            }
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
            // Never mutate _httpClient.DefaultRequestHeaders here. That client carries the
            // browser-like headers used to get past Cloudflare; clearing them broke every later
            // request in the process, permanently, and raced with concurrent requests.
            var partPath = destinationPath + ".part";

            try
            {
                // Use multi-threaded downloader if enabled
                if (SettingsManager.GetFastDownloadEnabled())
                {
                    var maxConnections = SettingsManager.GetMaxDownloadConnections();
                    return await MultiThreadDownloader.DownloadFileAsync(downloadUrl, destinationPath, progress, maxConnections);
                }

                TryDeletePartFile(partPath);

                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1 && progress != null;

                long totalRead;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new System.IO.FileStream(partPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    totalRead = 0L;
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
                }

                // A truncated transfer used to be reported as success.
                if (totalBytes > 0 && totalRead != totalBytes)
                {
                    Logger.LogError($"Download truncated: expected {totalBytes:N0} bytes, got {totalRead:N0}");
                    TryDeletePartFile(partPath);
                    return false;
                }

                System.IO.File.Move(partPath, destinationPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to download file from GameBanana: {ex.Message}", ex);
                TryDeletePartFile(partPath);
                return false;
            }
        }

        private static void TryDeletePartFile(string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
            catch
            {
                // Best effort.
            }
        }

        /// <summary>
        /// Extract mod ID from GameBanana URL
        /// </summary>
        public static int? ExtractModIdFromUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

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
    }
}

