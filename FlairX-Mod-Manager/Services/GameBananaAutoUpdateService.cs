using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace FlairX_Mod_Manager.Services
{
    public static class GameBananaAutoUpdateService
    {
        private static readonly object _lockObject = new object();
        private static int _success = 0;
        private static int _fail = 0;
        private static int _skip = 0;
        private static readonly List<string> _skippedMods = new List<string>();
        private static readonly List<string> _failedMods = new List<string>();

        /// <summary>
        /// Check if auto-update should run based on settings
        /// </summary>
        public static bool ShouldRunAutoUpdate()
        {
            if (!SettingsManager.Current.GameBananaAutoUpdateEnabled)
                return false;

            var lastUpdate = SettingsManager.Current.GameBananaLastAutoUpdate;
            var intervalDays = SettingsManager.Current.GameBananaAutoUpdateIntervalDays;
            
            return DateTime.Now.Subtract(lastUpdate).TotalDays >= intervalDays;
        }

        /// <summary>
        /// Run auto-update in background (silent mode)
        /// </summary>
        public static async Task RunAutoUpdateAsync()
        {
            try
            {
                Logger.LogInfo("Starting GameBanana auto-update...");
                
                // Reset counters
                lock (_lockObject)
                {
                    _success = 0;
                    _fail = 0;
                    _skip = 0;
                    _skippedMods.Clear();
                    _failedMods.Clear();
                }

                await FetchAllDataAsync(CancellationToken.None, silent: true, smartUpdate: false);
                
                // Update last run time
                SettingsManager.Current.GameBananaLastAutoUpdate = DateTime.Now;
                SettingsManager.Save();
                
                Logger.LogInfo($"GameBanana auto-update completed - Success: {_success}, Failed: {_fail}, Skipped: {_skip}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during GameBanana auto-update", ex);
            }
        }

        /// <summary>
        /// Combined function to fetch authors, versions and dates from GameBanana API
        /// </summary>
        public static async Task<(int success, int failed, int skipped, List<string> failedMods, List<string> skippedMods)> 
            FetchAllDataAsync(CancellationToken token, bool silent = false, bool smartUpdate = false)
        {
            return await FetchAllDataInternalAsync(token, silent, smartUpdate);
        }

        /// <summary>
        /// Backward compatibility method for versions and dates only
        /// </summary>
        public static async Task<(int success, int failed, int skipped, List<string> failedMods, List<string> skippedMods)> 
            FetchVersionsAndDatesAsync(CancellationToken token, bool silent = false)
        {
            return await FetchAllDataInternalAsync(token, silent, smartUpdate: false);
        }

        /// <summary>
        /// Internal method that does the actual work
        /// </summary>
        private static async Task<(int success, int failed, int skipped, List<string> failedMods, List<string> skippedMods)> 
            FetchAllDataInternalAsync(CancellationToken token, bool silent = false, bool smartUpdate = false)
        {
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            string modLibraryPath = SharedUtilities.GetSafeXXMIModsPath();

            var allModDirs = new List<string>();
            foreach (var categoryDir in Directory.GetDirectories(modLibraryPath))
            {
                if (Directory.Exists(categoryDir))
                {
                    allModDirs.AddRange(Directory.GetDirectories(categoryDir));
                }
            }

            int totalMods = allModDirs.Count;
            int processed = 0;

            // Reset counters
            lock (_lockObject)
            {
                _success = 0;
                _fail = 0;
                _skip = 0;
                _skippedMods.Clear();
                _failedMods.Clear();
            }

            // Process in parallel with max 5 concurrent requests
            var semaphore = new SemaphoreSlim(5);
            var tasks = allModDirs.Select(async dir =>
            {
                await semaphore.WaitAsync(token);
                try
                {
                    if (token.IsCancellationRequested) return;

                    var modJsonPath = Path.Combine(dir, "mod.json");
                    var modFolderName = Path.GetFileName(dir);

                    if (!File.Exists(modJsonPath))
                    {
                        Interlocked.Increment(ref processed);
                        return;
                    }

                    // Read mod.json through queue
                    var json = await Services.FileAccessQueue.ReadAllTextAsync(modJsonPath, token);
                    string? url = null;
                    string modName = modFolderName;
                    string displayName = modFolderName;
                    
                    using (var doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;

                        modName = root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString())
                            ? nameProp.GetString()!
                            : modFolderName;

                        // Remove DISABLED_ prefix from folder name for display
                        var displayFolderName = modFolderName.StartsWith("DISABLED_") 
                            ? modFolderName.Substring("DISABLED_".Length) 
                            : modFolderName;
                        
                        displayName = root.TryGetProperty("name", out var displayNameProp) && !string.IsNullOrWhiteSpace(displayNameProp.GetString()) 
                            ? displayNameProp.GetString()! 
                            : displayFolderName;

                        // Get current author for smart update logic
                        string currentAuthor = root.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? string.Empty : string.Empty;
                        bool shouldUpdateAuthor = string.IsNullOrWhiteSpace(currentAuthor) || currentAuthor.Equals("unknown", StringComparison.OrdinalIgnoreCase);

                        // For smart update, skip mods that already have known authors
                        if (smartUpdate && !shouldUpdateAuthor)
                        {
                            SafeIncrementSkip();
                            SafeAddSkippedMod($"{displayName}: {SharedUtilities.GetTranslation(lang, "AlreadyHasAuthor")} ({currentAuthor})");
                            Interlocked.Increment(ref processed);
                            return;
                        }

                        // Check if URL is marked as invalid and skip if option is enabled
                        if (SettingsManager.Current.GameBananaSkipInvalidUrls && 
                            root.TryGetProperty("urlInvalid", out var urlInvalidProp) && 
                            urlInvalidProp.ValueKind == JsonValueKind.True)
                        {
                            SafeIncrementSkip();
                            SafeAddSkippedMod($"{displayName}: {SharedUtilities.GetTranslation(lang, "UrlUnavailable")}");
                            Interlocked.Increment(ref processed);
                            return;
                        }

                        if (!root.TryGetProperty("url", out var urlProp) || urlProp.ValueKind != JsonValueKind.String ||
                            string.IsNullOrWhiteSpace(urlProp.GetString()) || !urlProp.GetString()!.Contains("gamebanana.com"))
                        {
                            SafeIncrementSkip();
                            SafeAddSkippedMod($"{displayName}: {SharedUtilities.GetTranslation(lang, "InvalidUrl")}");
                            Interlocked.Increment(ref processed);
                            return;
                        }

                        url = urlProp.GetString()!;
                    }

                    try
                    {
                        // Fetch author, version and dates in one API call
                        var (author, version, dateAdded, dateUpdated) = await FetchAllDataFromApi(url, token);
                        
                        // Clear any previous invalid URL flag since we successfully fetched data
                        await ClearUrlInvalidFlagAsync(modJsonPath, token);
                        
                        if (!SecurityValidator.IsValidModDirectoryName(modFolderName))
                        {
                            SafeIncrementSkip();
                            SafeAddSkippedMod($"{displayName}: Invalid directory name");
                            Interlocked.Increment(ref processed);
                            return;
                        }

                        // Atomic read-modify-write operation
                        await Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                        {
                            var currentJson = await File.ReadAllTextAsync(modJsonPath, token);
                            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(currentJson) ?? new();
                            
                            // Update author
                            if (!string.IsNullOrWhiteSpace(author))
                                dict["author"] = author;
                            
                            // Update version
                            dict["version"] = string.IsNullOrWhiteSpace(version) ? " " : version;
                            
                            // Update dates
                            if (dateAdded.HasValue)
                                dict["dateAdded"] = dateAdded.Value.ToString("yyyy-MM-dd");
                            if (dateUpdated.HasValue)
                                dict["dateUpdated"] = dateUpdated.Value.ToString("yyyy-MM-dd");
                            
                            await File.WriteAllTextAsync(modJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }), token);
                        }, token);
                        
                        SafeIncrementSuccess();
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to fetch data for {displayName}", ex);
                        // Mark URL as invalid on any fetch error
                        await MarkUrlAsInvalidAsync(modJsonPath, token);
                        SafeIncrementFail();
                        SafeAddFailedMod($"{displayName}: {SharedUtilities.GetTranslation(lang, "AuthorFetchError")}");
                    }

                    Interlocked.Increment(ref processed);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);

            return (_success, _fail, _skip, new List<string>(_failedMods), new List<string>(_skippedMods));
        }

        /// <summary>
        /// Fetch author, version and dates from GameBanana API in one call
        /// </summary>
        private static async Task<(string? author, string? version, DateTime? dateAdded, DateTime? dateUpdated)> FetchAllDataFromApi(string url, CancellationToken token)
        {
            try
            {
                // Parse GameBanana URL to get item type and ID
                var uri = new Uri(url);
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                
                if (segments.Length < 2)
                    return (null, null, null, null);

                string itemType = segments[0].ToLower();
                if (!int.TryParse(segments[1], out int itemId))
                    return (null, null, null, null);

                // Capitalize first letter and remove 's' from end for API
                itemType = char.ToUpper(itemType[0]) + itemType.Substring(1).TrimEnd('s');

                // Combined API call to get author, version, dateAdded, and dateUpdated
                string apiUrl = $"https://gamebanana.com/apiv11/{itemType}/{itemId}?_csvProperties=_aSubmitter,_sVersion,_tsDateAdded,_tsDateUpdated";

                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                
                var response = await httpClient.GetAsync(apiUrl, token);
                response.EnsureSuccessStatusCode();
                
                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                string? author = null;
                string? version = null;
                DateTime? dateAdded = null;
                DateTime? dateUpdated = null;

                // Extract author
                if (root.TryGetProperty("_aSubmitter", out var submitterProp) && submitterProp.ValueKind == JsonValueKind.Object)
                {
                    if (submitterProp.TryGetProperty("_sName", out var authorNameProp) && authorNameProp.ValueKind == JsonValueKind.String)
                    {
                        author = authorNameProp.GetString();
                    }
                }

                // Extract version
                if (root.TryGetProperty("_sVersion", out var versionProp) && versionProp.ValueKind == JsonValueKind.String)
                {
                    version = versionProp.GetString();
                }

                // Extract dateAdded
                if (root.TryGetProperty("_tsDateAdded", out var dateAddedProp) && dateAddedProp.ValueKind == JsonValueKind.Number)
                {
                    var timestamp = dateAddedProp.GetInt64();
                    dateAdded = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                }

                // Extract dateUpdated
                if (root.TryGetProperty("_tsDateUpdated", out var dateUpdatedProp) && dateUpdatedProp.ValueKind == JsonValueKind.Number)
                {
                    var timestamp = dateUpdatedProp.GetInt64();
                    dateUpdated = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                }

                return (author, version, dateAdded, dateUpdated);
            }
            catch (Exception ex)
            {
                Logger.LogError($"API request failed for {url}", ex);
                throw;
            }
        }

        private static async Task ClearUrlInvalidFlagAsync(string modJsonPath, CancellationToken token)
        {
            try
            {
                await Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                {
                    var json = await File.ReadAllTextAsync(modJsonPath, token);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                    dict.Remove("urlInvalid");
                    await File.WriteAllTextAsync(modJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }), token);
                }, token);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to clear urlInvalid flag for {modJsonPath}", ex);
            }
        }

        private static async Task MarkUrlAsInvalidAsync(string modJsonPath, CancellationToken token)
        {
            try
            {
                await Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                {
                    var json = await File.ReadAllTextAsync(modJsonPath, token);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                    dict["urlInvalid"] = true;
                    await File.WriteAllTextAsync(modJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }), token);
                }, token);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to mark URL as invalid for {modJsonPath}", ex);
            }
        }

        private static void SafeIncrementSuccess()
        {
            lock (_lockObject) { _success++; }
        }

        private static void SafeIncrementFail()
        {
            lock (_lockObject) { _fail++; }
        }

        private static void SafeIncrementSkip()
        {
            lock (_lockObject) { _skip++; }
        }

        private static void SafeAddSkippedMod(string mod)
        {
            lock (_lockObject) { _skippedMods.Add(mod); }
        }

        private static void SafeAddFailedMod(string mod)
        {
            lock (_lockObject) { _failedMods.Add(mod); }
        }
    }
}