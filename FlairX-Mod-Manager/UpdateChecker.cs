using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        static UpdateChecker()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) FlairX-Mod-Manager/{AppConstants.APP_VERSION}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        public static async Task<(bool updateAvailable, string latestVersion, string downloadUrl)?> CheckForUpdatesAsync()
        {
            Logger.LogInfo("=== UPDATE CHECK STARTED ===");
            try
            {
                var url = $"https://api.github.com/repos/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/releases/latest";
                Logger.LogInfo($"Checking for updates at: {url}");
                
                var response = await _httpClient.GetStringAsync(url);
                Logger.LogInfo($"Received response from GitHub API");
                
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                
                // Use tag_name for version
                string? versionSource = root.GetProperty("tag_name").GetString();
                Logger.LogInfo($"Latest release tag: {versionSource}");
                
                if (string.IsNullOrEmpty(versionSource))
                {
                    Logger.LogWarning("Release tag is empty");
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
                
                // Get download URL for .zip asset
                string? downloadUrl = null;
                if (root.TryGetProperty("assets", out var assets))
                {
                    Logger.LogInfo($"Found {assets.GetArrayLength()} assets");
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString();
                        Logger.LogInfo($"Asset: {name}");
                        if (name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            Logger.LogInfo($"Found .zip asset: {downloadUrl}");
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
                    Logger.LogWarning("No .zip download URL found");
                    return null;
                }
                
                // Compare versions
                var updateAvailable = IsNewerVersion(AppConstants.APP_VERSION, latestVersion);
                Logger.LogInfo($"Current version: {AppConstants.APP_VERSION}, Latest version: {latestVersion}, Update available: {updateAvailable}");
                
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
                var archivePath = Path.Combine(tempDir, "update.zip");
                
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
                
                try
                {
                    Logger.LogInfo($"Opening archive: {archivePath}");
                    using (var archive = ArchiveFactory.Open(archivePath))
                    {
                        Logger.LogInfo("Archive opened, reading entries...");
                        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
                        Logger.LogInfo($"Found {entries.Count} files in archive");
                        
                        // Check if all files are in a single root folder
                        string? commonRootFolder = null;
                        bool hasSingleRootFolder = true;
                        
                        foreach (var entry in entries)
                        {
                            if (string.IsNullOrEmpty(entry.Key)) continue;
                            
                            var parts = entry.Key.Split('/', '\\');
                            if (parts.Length > 1)
                            {
                                var rootFolder = parts[0];
                                if (commonRootFolder == null)
                                {
                                    commonRootFolder = rootFolder;
                                }
                                else if (commonRootFolder != rootFolder)
                                {
                                    hasSingleRootFolder = false;
                                    break;
                                }
                            }
                            else
                            {
                                hasSingleRootFolder = false;
                                break;
                            }
                        }
                        
                        Logger.LogInfo($"Archive structure: hasSingleRootFolder={hasSingleRootFolder}, rootFolder={commonRootFolder}");
                        
                        // Extract files, skipping root folder if needed
                        int extractedCount = 0;
                        foreach (var entry in entries)
                        {
                            try
                            {
                                if (string.IsNullOrEmpty(entry.Key)) continue;
                                
                                var pathParts = entry.Key.Split('/', '\\').ToList();
                                
                                // Skip root folder if all files are in one folder
                                if (hasSingleRootFolder && !string.IsNullOrEmpty(commonRootFolder) && pathParts.Count > 1)
                                {
                                    pathParts.RemoveAt(0);
                                }
                                
                                var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), pathParts);
                                var destPath = Path.Combine(extractPath, relativePath);
                                
                                Logger.LogInfo($"Extracting: {entry.Key} -> {relativePath}");
                                
                                var destDir = Path.GetDirectoryName(destPath);
                                if (!string.IsNullOrEmpty(destDir))
                                {
                                    Directory.CreateDirectory(destDir);
                                }
                                
                                using (var entryStream = entry.OpenEntryStream())
                                using (var fileStream = File.Create(destPath))
                                {
                                    entryStream.CopyTo(fileStream);
                                }
                                
                                extractedCount++;
                                if (extractedCount % 10 == 0)
                                {
                                    Logger.LogInfo($"Extracted {extractedCount}/{entries.Count} files...");
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Failed to extract {entry.Key}", ex);
                                throw;
                            }
                        }
                        
                        Logger.LogInfo($"Extraction completed: {extractedCount} files");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Archive extraction failed", ex);
                    throw;
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
                
                // Go up one directory (from app/ to root with launcher)
                var parentDir = Path.GetDirectoryName(currentDir);
                if (string.IsNullOrEmpty(parentDir))
                {
                    Logger.LogError("Failed to get parent directory");
                    return false;
                }
                
                Logger.LogInfo($"Current exe: {currentExePath}");
                Logger.LogInfo($"Current dir: {currentDir}");
                Logger.LogInfo($"Parent dir (target): {parentDir}");
                
                // Create batch script to replace files after app closes
                var scriptPath = Path.Combine(tempDir, "update.bat");
                
                var script = $@"@echo off
cd /d ""{parentDir}""
timeout /t 2 /nobreak > nul
echo Applying update...
xcopy /E /I /Y ""{extractPath}\*"" .
if errorlevel 1 (
    echo Update failed!
    pause
    exit /b 1
)
echo Update completed!
timeout /t 2 /nobreak > nul
start """" "".\FlairX Mod Manager Launcher.exe""
cd /d ""{Path.GetDirectoryName(tempDir)}""
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
            return AppConstants.APP_VERSION;
        }
    }
}
