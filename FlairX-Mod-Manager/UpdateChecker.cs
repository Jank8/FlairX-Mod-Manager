using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace FlairX_Mod_Manager
{
    public class UpdateChecker
    {
        private const string GITHUB_REPO_OWNER = "Jank8";
        private const string GITHUB_REPO_NAME = "FlairX-Mod-Manager";
        private const string CURRENT_VERSION = "3.0.0";
        
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        static UpdateChecker()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FlairX-Mod-Manager");
        }

        public static async Task<(bool updateAvailable, string latestVersion, string downloadUrl)?> CheckForUpdatesAsync()
        {
            try
            {
                var url = $"https://api.github.com/repos/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/releases/latest";
                Logger.LogInfo($"Checking for updates at: {url}");
                
                var response = await _httpClient.GetStringAsync(url);
                Logger.LogInfo($"Received response from GitHub API");
                
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                
                // Try to get version from name first (e.g., "Release 3.0.0"), fallback to tag_name
                string? versionSource = null;
                if (root.TryGetProperty("name", out var nameProperty))
                {
                    versionSource = nameProperty.GetString();
                    Logger.LogInfo($"Latest release name: {versionSource}");
                }
                
                if (string.IsNullOrEmpty(versionSource))
                {
                    versionSource = root.GetProperty("tag_name").GetString();
                    Logger.LogInfo($"Latest release tag: {versionSource}");
                }
                
                if (string.IsNullOrEmpty(versionSource))
                {
                    Logger.LogWarning("Release name and tag are empty");
                    return null;
                }
                
                // Extract version number from name or tag
                // Supports formats: "3.0.0", "v3.0.0", "Release 3.0.0"
                var latestVersion = ExtractVersionFromTag(versionSource);
                if (string.IsNullOrEmpty(latestVersion))
                {
                    Logger.LogWarning($"Could not extract version from: {versionSource}");
                    return null;
                }
                Logger.LogInfo($"Extracted version: {latestVersion}");
                
                // Get download URL for .7z asset (FlairX.Mod.Manager.7z)
                string? downloadUrl = null;
                if (root.TryGetProperty("assets", out var assets))
                {
                    Logger.LogInfo($"Found {assets.GetArrayLength()} assets");
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString();
                        Logger.LogInfo($"Asset: {name}");
                        if (name?.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            Logger.LogInfo($"Found .7z asset: {downloadUrl}");
                            break;
                        }
                    }
                }
                else
                {
                    Logger.LogWarning("No assets found in release");
                }
                
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Logger.LogWarning("No .7z download URL found");
                    return null;
                }
                
                // Compare versions
                var updateAvailable = IsNewerVersion(CURRENT_VERSION, latestVersion);
                Logger.LogInfo($"Current version: {CURRENT_VERSION}, Latest version: {latestVersion}, Update available: {updateAvailable}");
                
                return (updateAvailable, latestVersion, downloadUrl);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError($"HTTP error checking for updates: {ex.Message}", ex);
                return null;
            }
            catch (JsonException ex)
            {
                Logger.LogError($"JSON parsing error: {ex.Message}", ex);
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unexpected error checking for updates: {ex.Message}", ex);
                return null;
            }
        }

        private static string? ExtractVersionFromTag(string tag)
        {
            try
            {
                // Remove common prefixes
                tag = tag.Trim();
                
                // Remove "Release " prefix if present
                if (tag.StartsWith("Release ", StringComparison.OrdinalIgnoreCase))
                {
                    tag = tag.Substring(8).Trim();
                }
                
                // Remove 'v' prefix if present
                tag = tag.TrimStart('v', 'V');
                
                // Try to parse as version to validate format
                if (Version.TryParse(tag, out _))
                {
                    return tag;
                }
                
                // If parsing failed, try to extract version pattern (X.X.X)
                var match = System.Text.RegularExpressions.Regex.Match(tag, @"(\d+\.\d+\.\d+)");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsNewerVersion(string currentVersion, string latestVersion)
        {
            try
            {
                var current = Version.Parse(currentVersion);
                var latest = Version.Parse(latestVersion);
                return latest > current;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, IProgress<int>? progress = null)
        {
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "FlairX_Update");
                var archivePath = Path.Combine(tempDir, "update.7z");
                
                // Create temp directory
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);
                
                // Download update
                Logger.LogInfo($"Downloading update from: {downloadUrl}");
                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var downloadedBytes = 0L;
                    
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                    
                    var buffer = new byte[8192];
                    int bytesRead;
                    
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        downloadedBytes += bytesRead;
                        
                        if (totalBytes > 0)
                        {
                            var progressPercentage = (int)((downloadedBytes * 100) / totalBytes);
                            progress?.Report(progressPercentage);
                        }
                    }
                }
                
                Logger.LogInfo("Download completed, extracting update...");
                
                // Extract to temp directory using SharpCompress
                var extractPath = Path.Combine(tempDir, "extracted");
                Directory.CreateDirectory(extractPath);
                
                using (var archive = ArchiveFactory.Open(archivePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.IsDirectory)
                        {
                            entry.WriteToDirectory(extractPath, new ExtractionOptions
                            {
                                ExtractFullPath = true,
                                Overwrite = true
                            });
                        }
                    }
                }
                
                // Create update script
                var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExePath))
                {
                    Logger.LogError("Failed to get current executable path");
                    return false;
                }
                
                var currentDir = Path.GetDirectoryName(currentExePath);
                if (string.IsNullOrEmpty(currentDir))
                {
                    Logger.LogError("Failed to get current directory");
                    return false;
                }
                
                // Create batch script to replace files after app closes
                var scriptPath = Path.Combine(tempDir, "update.bat");
                var script = $@"@echo off
timeout /t 2 /nobreak > nul
echo Applying update...
xcopy /E /I /Y ""{extractPath}\*"" ""{currentDir}""
if errorlevel 1 (
    echo Update failed!
    pause
    exit /b 1
)
echo Update completed!
timeout /t 2 /nobreak > nul
start """" ""{currentExePath}""
rmdir /s /q ""{tempDir}""
";
                
                File.WriteAllText(scriptPath, script);
                
                // Start update script and close application
                Logger.LogInfo("Starting update script...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                
                // Close application
                Environment.Exit(0);
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to download and install update", ex);
                return false;
            }
        }

        public static string GetCurrentVersion()
        {
            return CURRENT_VERSION;
        }
    }
}
