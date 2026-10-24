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

        /// <summary>
        /// Get all character categories for a specific game using WebView2 to render JavaScript
        /// </summary>
        // Cache for category trees per game tag
        private static readonly Dictionary<string, List<CategoryRecord>> _categoryTreeCache = new();

        public static void ClearCategoryTreeCache() => _categoryTreeCache.Clear();

        /// <summary>
        /// Parse and flatten GameBanana's nested category response in pre-order.
        /// Duplicate category IDs are ignored after their first occurrence.
        /// </summary>
        internal static List<CategoryRecord> ParseCategoryTreeFromJson(string json)
        {
            var roots = JsonSerializer.Deserialize<List<CategoryRecord>>(json) ?? new List<CategoryRecord>();
            var categories = new List<CategoryRecord>();
            var seenIds = new HashSet<int>();

            void AddCategoryAndChildren(CategoryRecord category)
            {
                if (seenIds.Add(category.Id))
                {
                    categories.Add(category);
                }

                if (category.Children == null)
                {
                    return;
                }

                foreach (var child in category.Children)
                {
                    AddCategoryAndChildren(child);
                }
            }

            foreach (var root in roots)
            {
                AddCategoryAndChildren(root);
            }

            return categories;
        }

        internal static async Task<List<CategoryRecord>?> FetchCategoryTreeWithFallbackAsync(
            int gameId,
            Func<string, Task<string>> fetchJsonAsync,
            Func<Task<List<CategoryRecord>?>> fetchFallbackAsync)
        {
            var apiUrl = $"https://gamebanana.com/apiv13/Util/ModCategory/NestedStructure?_idGameRow={gameId}";

            try
            {
                Logger.LogInfo($"Fetching category tree from GameBanana JSON API: {apiUrl}");
                var json = await fetchJsonAsync(apiUrl);
                var categories = ParseCategoryTreeFromJson(json);

                if (categories.Count > 0)
                {
                    Logger.LogInfo($"GameBanana JSON API returned {categories.Count} categories for game {gameId}");
                    return categories;
                }

                Logger.LogWarning($"GameBanana JSON API returned no categories for game {gameId}; using WebView2 fallback");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"GameBanana JSON API failed for game {gameId}: {ex.Message}; using WebView2 fallback");
            }

            return await fetchFallbackAsync();
        }

        public static async Task<List<CategoryRecord>?> GetCharacterCategoriesAsync(string gameTag)
        {
            if (_categoryTreeCache.TryGetValue(gameTag, out var cached))
            {
                Logger.LogInfo($"GetCharacterCategoriesAsync: returning cached {cached.Count} categories for {gameTag}");
                return cached;
            }

            try
            {
                var gameId = GetGameId(gameTag);
                if (gameId == 0)
                {
                    Logger.LogError($"Unknown game tag: {gameTag}");
                    return null;
                }

                var categories = await FetchCategoryTreeWithFallbackAsync(
                    gameId,
                    url => _httpClient.GetStringAsync(url),
                    () => FetchCategoryTreeWithWebView2Async(gameTag, gameId));

                if (categories == null || categories.Count == 0)
                {
                    Logger.LogError($"Failed to fetch any categories for {gameTag}");
                    return null;
                }

                _categoryTreeCache[gameTag] = categories;
                return categories;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to fetch categories for {gameTag}: {ex.Message}", ex);
                return null;
            }
        }

        private static async Task<List<CategoryRecord>?> FetchCategoryTreeWithWebView2Async(string gameTag, int gameId)
        {
            var url = $"https://gamebanana.com/games/cattree/{gameId}";
            Logger.LogInfo($"Fetching category tree through WebView2 fallback: {url}");

            var html = await RenderPageWithWebView2Async(url);
            if (string.IsNullOrEmpty(html))
            {
                Logger.LogError("Failed to render cattree page with WebView2");
                return null;
            }

            Logger.LogInfo($"Received rendered HTML length: {html.Length} characters");
            var categories = ParseCategoryTreeFromHtml(html);
            Logger.LogInfo($"Found {categories.Count} categories for {gameTag} from WebView2 cattree fallback");

            // Preserve the legacy supplemental pages when the JSON API is unavailable.
            var additionalCatUrls = new Dictionary<string, List<string>>
            {
                { "GIMI", new List<string> { "https://gamebanana.com/mods/cats/18140" } },
                { "EFMI", new List<string> { "https://gamebanana.com/mods/cats/42770" } },
                { "HIMI", new List<string> { "https://gamebanana.com/mods/cats/23620" } },
                { "SRMI", new List<string> { "https://gamebanana.com/mods/cats/22832" } },
            };

            if (additionalCatUrls.TryGetValue(gameTag, out var extraUrls))
            {
                var existingIds = new HashSet<int>(categories.Select(c => c.Id));
                foreach (var extraUrl in extraUrls)
                {
                    Logger.LogInfo($"{gameTag}: also fetching fallback categories from {extraUrl}");
                    var extraHtml = await RenderPageWithWebView2Async(extraUrl);
                    if (string.IsNullOrEmpty(extraHtml))
                    {
                        Logger.LogWarning($"{gameTag}: failed to fetch fallback categories from {extraUrl}");
                        continue;
                    }

                    var extraCategories = ParseCategoryTreeFromHtml(extraHtml);
                    Logger.LogInfo($"{gameTag}: found {extraCategories.Count} fallback entries from {extraUrl}");
                    foreach (var category in extraCategories)
                    {
                        if (existingIds.Add(category.Id))
                        {
                            categories.Add(category);
                            Logger.LogInfo($"{gameTag}: added missing fallback category: {category.Name} (ID: {category.Id})");
                        }
                    }
                }

                Logger.LogInfo($"{gameTag}: total fallback categories after merge: {categories.Count}");
            }

            return categories.Count > 0 ? categories : null;
        }

        /// <summary>
        /// Parse categories from cattree HTML preserving parent/child hierarchy.
        /// Level-1 nodes are top-level categories; level-2 nodes are their children.
        /// Structure per node:
        ///   &lt;li aria-label="Name" aria-level="1|2" ...&gt;
        ///     ...&lt;img src="iconUrl"&gt;...&lt;a href=".../cats/ID"&gt;Name&lt;/a&gt;...
        /// </summary>
        private static List<CategoryRecord> ParseCategoryTreeFromHtml(string html)
        {
            var all = new List<CategoryRecord>();
            var seenIds = new HashSet<int>();
            try
            {
                // Format 1: cattree page — <div class="Cluster"><img src="..."><a href=".../cats/ID">Name</a>
                var clusterPattern = @"<div class=""Cluster""><img src=""(https://images\.gamebanana\.com/img/ico/ModCategory/[^""]+)""[^>]*><a href=""https://gamebanana\.com/mods/cats/(\d+)""[^>]*>([^<]+)</a>";
                var matches = System.Text.RegularExpressions.Regex.Matches(html, clusterPattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                Logger.LogInfo($"ParseCategoryTreeFromHtml: found {matches.Count} Cluster entries");

                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    var iconUrl = m.Groups[1].Value;
                    if (!int.TryParse(m.Groups[2].Value, out var id)) continue;
                    var name = System.Net.WebUtility.HtmlDecode(m.Groups[3].Value.Trim());
                    if (seenIds.Add(id))
                    {
                        all.Add(new CategoryRecord { Id = id, Name = name, IconUrl = iconUrl, ProfileUrl = $"https://gamebanana.com/mods/cats/{id}" });
                        Logger.LogInfo($"Parsed (Cluster): {name} (ID: {id})");
                    }
                }

                // Format 2: mods/cats page — <record>...<recordcell class="Icon"><a href=".../cats/ID"><img src="..."></a></recordcell><recordcell class="Info"><a href=".../cats/ID">Name</a>...
                var recordPattern = @"<record[^>]*>.*?<recordcell[^>]*class=""Icon""[^>]*>.*?<img[^>]*src=""([^""]+)""[^>]*>.*?</recordcell>\s*<recordcell[^>]*class=""Info""[^>]*>\s*<a[^>]*href=""https://gamebanana\.com/mods/cats/(\d+)""[^>]*>([^<]+)</a>";
                var recordMatches = System.Text.RegularExpressions.Regex.Matches(html, recordPattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

                Logger.LogInfo($"ParseCategoryTreeFromHtml: found {recordMatches.Count} record entries");

                foreach (System.Text.RegularExpressions.Match m in recordMatches)
                {
                    var iconUrl = m.Groups[1].Value;
                    if (!int.TryParse(m.Groups[2].Value, out var id)) continue;
                    var name = System.Net.WebUtility.HtmlDecode(m.Groups[3].Value.Trim());
                    if (seenIds.Add(id))
                    {
                        all.Add(new CategoryRecord { Id = id, Name = name, IconUrl = iconUrl, ProfileUrl = $"https://gamebanana.com/mods/cats/{id}" });
                        Logger.LogInfo($"Parsed (record): {name} (ID: {id})");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ParseCategoryTreeFromHtml failed: {ex.Message}", ex);
            }
            return all;
        }

        /// <summary>
        /// Parse character categories from HTML (legacy - old mods/cats page)
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
                    
                    // Extract icon URL from img src (handle loading="lazy" attribute) - may not exist for all games
                    string? iconUrl = null;
                    var iconMatch = System.Text.RegularExpressions.Regex.Match(recordContent, @"<img[^>]*\s+src=""([^""]+)""");
                    if (iconMatch.Success)
                    {
                        iconUrl = iconMatch.Groups[1].Value;
                    }
                    else
                    {
                        Logger.LogInfo($"No icon found for category ID {idStr} (this is normal for some games)");
                    }
                    
                    // Extract name from alt attribute or from Info cell
                    string? name = null;
                    
                    // Try alt attribute first (if icon exists)
                    if (iconUrl != null)
                    {
                        var altMatch = System.Text.RegularExpressions.Regex.Match(recordContent, @"alt=""([^""]+)\s+category\s+icon""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (altMatch.Success)
                        {
                            name = altMatch.Groups[1].Value.Trim();
                        }
                    }
                    
                    // Fallback: get name from the Info cell <a> tag content
                    if (name == null)
                    {
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
                        Logger.LogInfo($"Parsed category: {name} (ID: {id}, Icon: {iconUrl ?? "none"})");
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
        /// Download category icon and save as gbicon.png (converted from downloaded icon.png)
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

                var tempIconPath = Path.Combine(destinationFolder, "icon_temp");
                // gbicon should be PNG or WebP (with alpha channel support)
                var imageFormat = SettingsManager.Current.ImageFormat;
                var imageExtension = imageFormat.Equals("WebP", StringComparison.OrdinalIgnoreCase) ? ".webp" : ".png";
                var finalIconPath = Path.Combine(destinationFolder, $"gbicon{imageExtension}");
                
                Logger.LogInfo($"Downloading icon from {iconUrl} to {tempIconPath}");

                // Download the icon to temporary location (without extension)
                var success = await DownloadFileAsync(iconUrl, tempIconPath);
                
                if (success)
                {
                    Logger.LogInfo($"Successfully downloaded icon to {tempIconPath}");
                    
                    // Verify the downloaded file is valid before conversion
                    if (!File.Exists(tempIconPath))
                    {
                        Logger.LogError("Downloaded file does not exist");
                        return false;
                    }
                    
                    var fileInfo = new FileInfo(tempIconPath);
                    if (fileInfo.Length == 0)
                    {
                        Logger.LogError("Downloaded file is empty");
                        File.Delete(tempIconPath);
                        return false;
                    }
                    
                    Logger.LogInfo($"Downloaded file size: {fileInfo.Length} bytes");
                    
                    // Convert downloaded image to selected format using ImageSharp
                    try
                    {
                        // ImageSharp can load any image format (PNG, JPEG, WebP, GIF, BMP, etc.)
                        using (var image = SixLabors.ImageSharp.Image.Load(tempIconPath))
                        {
                            Logger.LogInfo($"Detected image format, Size: {image.Width}x{image.Height}");
                            
                            // Save in selected format (PNG or WebP - both support alpha)
                            
                            if (imageExtension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
                            {
                                // Use lossless for icons to preserve alpha channel
                                var encoder = new SixLabors.ImageSharp.Formats.Webp.WebpEncoder
                                {
                                    Quality = 100,
                                    FileFormat = SixLabors.ImageSharp.Formats.Webp.WebpFileFormatType.Lossless,
                                    Method = SixLabors.ImageSharp.Formats.Webp.WebpEncodingMethod.BestQuality
                                };
                                image.Save(finalIconPath, encoder);
                                Logger.LogInfo($"Converted image to WebP (lossless) and saved as {finalIconPath}");
                            }
                            else
                            {
                                var encoder = new PngEncoder
                                {
                                    CompressionLevel = PngCompressionLevel.BestCompression,
                                    ColorType = PngColorType.RgbWithAlpha
                                };
                                image.Save(finalIconPath, encoder);
                                Logger.LogInfo($"Converted image to PNG and saved as {finalIconPath}");
                            }
                        }
                        
                        // Delete the temporary file
                        if (File.Exists(tempIconPath))
                        {
                            File.Delete(tempIconPath);
                            Logger.LogInfo($"Deleted temporary file {tempIconPath}");
                        }
                        
                        return true;
                    }
                    catch (Exception conversionEx)
                    {
                        Logger.LogError($"Failed to convert icon: {conversionEx.Message}", conversionEx);
                        
                        // Delete the temporary file
                        if (File.Exists(tempIconPath))
                        {
                            try
                            {
                                File.Delete(tempIconPath);
                                Logger.LogInfo($"Deleted temporary file after conversion failure");
                            }
                            catch (Exception deleteEx)
                            {
                                Logger.LogWarning($"Failed to delete temporary file: {deleteEx.Message}");
                            }
                        }
                        
                        return false;
                    }
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
