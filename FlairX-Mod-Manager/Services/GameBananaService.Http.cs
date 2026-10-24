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
    }
}
