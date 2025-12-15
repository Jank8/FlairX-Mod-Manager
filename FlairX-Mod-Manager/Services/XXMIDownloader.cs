using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Services
{
    public static class XXMIDownloader
    {
        private const string GITHUB_API_URL = "https://api.github.com/repos/SpectrumQT/XXMI-Launcher/releases/latest";
        
        private static readonly HttpClient _httpClient = CreateHttpClient();
        private static CancellationTokenSource? _cancellationTokenSource;

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            
            client.DefaultRequestHeaders.Add("User-Agent", $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) FlairX-Mod-Manager/{AppConstants.APP_VERSION}");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            
            return client;
        }

        /// <summary>
        /// Gets the download URL for portable XXMI Launcher
        /// </summary>
        public static async Task<string?> GetPortableDownloadUrlAsync()
        {
            try
            {
                Logger.LogInfo("Fetching XXMI Launcher releases...");
                var response = await _httpClient.GetStringAsync(GITHUB_API_URL);
                
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString();
                        // Look for portable .zip version
                        if (name != null && 
                            name.Contains("portable", StringComparison.OrdinalIgnoreCase) && 
                            name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            var downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            Logger.LogInfo($"Found portable XXMI: {name} -> {downloadUrl}");
                            return downloadUrl;
                        }
                    }
                }
                
                Logger.LogWarning("No portable .zip found in XXMI releases");
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to get XXMI download URL", ex);
                return null;
            }
        }

        /// <summary>
        /// Cancel ongoing download
        /// </summary>
        public static void CancelDownload()
        {
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Downloads and extracts XXMI Launcher to the XXMI directory
        /// </summary>
        public static async Task<bool> DownloadAndInstallAsync(IProgress<(int percent, string status)>? progress = null, Dictionary<string, string>? lang = null)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;
            
            try
            {
                string GetText(string key, string fallback) => 
                    lang != null && lang.TryGetValue(key, out var val) ? val : fallback;

                progress?.Report((0, GetText("XXMI_Download_GettingUrl", "Getting download URL...")));
                
                var downloadUrl = await GetPortableDownloadUrlAsync();
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Logger.LogError("Could not find portable XXMI download URL");
                    return false;
                }

                token.ThrowIfCancellationRequested();

                var tempDir = Path.Combine(Path.GetTempPath(), "FlairX_XXMI_Download");
                var archivePath = Path.Combine(tempDir, "xxmi_portable.zip");
                
                // Clean temp directory
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                // Download
                var downloadingText = GetText("XXMI_Download_Downloading", "Downloading...");
                progress?.Report((5, downloadingText));
                Logger.LogInfo($"Downloading XXMI from: {downloadUrl}");
                
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    response.EnsureSuccessStatusCode();
                    
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var downloadedBytes = 0L;
                    
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                    
                    var buffer = new byte[8192];
                    int bytesRead;
                    
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                        downloadedBytes += bytesRead;
                        
                        if (totalBytes > 0)
                        {
                            var percent = (int)((downloadedBytes * 70) / totalBytes) + 5; // 5-75%
                            var mb = downloadedBytes / (1024.0 * 1024.0);
                            var totalMb = totalBytes / (1024.0 * 1024.0);
                            progress?.Report((percent, $"{downloadingText} {mb:F1} / {totalMb:F1} MB"));
                        }
                    }
                }
                
                token.ThrowIfCancellationRequested();
                
                Logger.LogInfo("Download completed, extracting...");
                progress?.Report((75, GetText("XXMI_Download_Extracting", "Extracting...")));

                // Extract to temp
                var extractPath = Path.Combine(tempDir, "extracted");
                Directory.CreateDirectory(extractPath);
                ArchiveHelper.ExtractToDirectory(archivePath, extractPath);

                token.ThrowIfCancellationRequested();

                // Find the actual content (skip root folder if exists)
                var sourcePath = extractPath;
                var topLevelItems = Directory.GetFileSystemEntries(extractPath);
                if (topLevelItems.Length == 1 && Directory.Exists(topLevelItems[0]))
                {
                    sourcePath = topLevelItems[0];
                    Logger.LogInfo($"Using root folder: {Path.GetFileName(sourcePath)}");
                }

                // Get XXMI destination directory
                var xxmiDir = PathManager.GetAbsolutePath(PathManager.XXMI_DIR);
                Logger.LogInfo($"Installing to: {xxmiDir}");
                
                progress?.Report((85, GetText("XXMI_Download_Installing", "Installing...")));

                // Copy files to XXMI directory
                CopyDirectory(sourcePath, xxmiDir);

                // Cleanup
                progress?.Report((95, GetText("XXMI_Download_Cleanup", "Cleaning up...")));
                try { Directory.Delete(tempDir, true); } catch { }

                progress?.Report((100, GetText("XXMI_Download_Done", "Done!")));
                Logger.LogInfo("XXMI Launcher installed successfully");
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInfo("XXMI download cancelled by user");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to download and install XXMI", ex);
                return false;
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }
    }
}
