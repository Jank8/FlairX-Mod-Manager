using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Multi-threaded file downloader with automatic fallback to single connection
    /// </summary>
    public static class MultiThreadDownloader
    {
        /// <summary>
        /// Shared client. Creating one per segment (the old code did) meant four connection
        /// pools, TLS handshakes and DNS lookups per file, and the shared instance had its
        /// <c>DefaultRequestHeaders</c> cleared on every download - a race whenever two downloads
        /// overlapped (which the auto-updater does).
        /// </summary>
        private static readonly HttpClient _httpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                AutomaticDecompression = DecompressionMethods.All,
                ConnectTimeout = TimeSpan.FromSeconds(30),
            };

            var client = new HttpClient(handler)
            {
                // ResponseHeadersRead means this bounds the header phase only; the body is
                // governed by the CancellationToken.
                Timeout = TimeSpan.FromMinutes(30),
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd($"FlairX-Mod-Manager/{AppConstants.APP_VERSION}");
            return client;
        }

        public class DownloadSegment
        {
            public long Start { get; set; }
            public long End { get; set; }
            public int Index { get; set; }
            public string TempFilePath { get; set; } = string.Empty;
            public long BytesDownloaded; // Changed to field for Interlocked operations

            public long ExpectedLength => End - Start + 1;

            public DownloadSegment(long start, long end, int index)
            {
                Start = start;
                End = end;
                Index = index;
            }
        }

        public class DownloadProgress
        {
            public long TotalBytes { get; set; }
            public long DownloadedBytes { get; set; }
            public double Percentage => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;
        }

        /// <summary>
        /// Download file using multiple connections with automatic fallback
        /// </summary>
        public static async Task<bool> DownloadFileAsync(
            string downloadUrl,
            string destinationPath,
            IProgress<double>? progress = null,
            int maxConnections = 4,
            CancellationToken cancellationToken = default)
        {
            // Download to a side file first: a failed or interrupted transfer must never leave a
            // truncated archive where callers expect a complete one.
            var partPath = destinationPath + ".part";

            try
            {
                TryDelete(partPath);

                // Check if server supports range requests
                var (supportsRanges, fileSize) = await CheckRangeSupport(downloadUrl, cancellationToken);

                // Use single connection if:
                // - Server doesn't support ranges
                // - File is small (< 5MB)
                // - Only 1 connection requested
                if (!supportsRanges || fileSize < 5 * 1024 * 1024 || maxConnections <= 1)
                {
                    Logger.LogInfo($"Using single connection download (ranges: {supportsRanges}, size: {fileSize:N0} bytes)");

                    if (!await DownloadSingleConnection(downloadUrl, partPath, progress, cancellationToken))
                    {
                        TryDelete(partPath);
                        return false;
                    }

                    return CommitDownload(partPath, destinationPath, expectedSize: null);
                }

                Logger.LogInfo($"Using multi-threaded download with {maxConnections} connections (size: {fileSize:N0} bytes)");

                var ok = await DownloadMultiConnection(downloadUrl, partPath, fileSize, maxConnections, progress, cancellationToken);
                if (!ok)
                {
                    TryDelete(partPath);
                    return false;
                }

                return CommitDownload(partPath, destinationPath, expectedSize: fileSize);
            }
            catch (OperationCanceledException)
            {
                TryDelete(partPath);
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Download failed: {ex.Message}", ex);
                TryDelete(partPath);
                return false;
            }
        }

        private static bool CommitDownload(string partPath, string destinationPath, long? expectedSize)
        {
            try
            {
                var actualSize = new FileInfo(partPath).Length;

                if (expectedSize.HasValue && actualSize != expectedSize.Value)
                {
                    Logger.LogError($"Download size mismatch: expected {expectedSize.Value:N0} bytes, got {actualSize:N0}");
                    TryDelete(partPath);
                    return false;
                }

                if (actualSize == 0)
                {
                    Logger.LogError("Download produced an empty file");
                    TryDelete(partPath);
                    return false;
                }

                File.Move(partPath, destinationPath, overwrite: true);
                Logger.LogInfo($"Download committed: {Path.GetFileName(destinationPath)} ({actualSize:N0} bytes)");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to commit download: {ex.Message}", ex);
                TryDelete(partPath);
                return false;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best effort.
            }
        }

        private static async Task<(bool supportsRanges, long fileSize)> CheckRangeSupport(string url, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return (false, 0);

                var supportsRanges = response.Headers.AcceptRanges?.Contains("bytes") == true;
                var fileSize = response.Content.Headers.ContentLength ?? 0;

                return (supportsRanges, fileSize);
            }
            catch
            {
                return (false, 0);
            }
        }

        private static async Task<bool> DownloadSingleConnection(
            string downloadUrl,
            string destinationPath,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1 && progress != null;

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalRead = 0L;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalRead += bytesRead;

                    if (canReportProgress)
                    {
                        progress!.Report((double)totalRead / totalBytes * 100);
                    }
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Single connection download failed: {ex.Message}", ex);
                return false;
            }
        }

        private static async Task<bool> DownloadMultiConnection(
            string downloadUrl,
            string destinationPath,
            long fileSize,
            int maxConnections,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            var tempDir = Path.Combine(PathManager.GetTempDirectory(), $"fxmm_download_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create segments
                var segmentSize = fileSize / maxConnections;
                var segments = new List<DownloadSegment>();

                for (int i = 0; i < maxConnections; i++)
                {
                    var start = i * segmentSize;
                    var end = (i == maxConnections - 1) ? fileSize - 1 : start + segmentSize - 1;
                    var segment = new DownloadSegment(start, end, i)
                    {
                        TempFilePath = Path.Combine(tempDir, $"segment_{i}.tmp")
                    };
                    segments.Add(segment);
                }

                // Progress tracking with timer for smooth updates
                var progressTimer = new Timer(state =>
                {
                    if (progress != null)
                    {
                        var totalDownloaded = segments.Sum(s => Interlocked.Read(ref s.BytesDownloaded));
                        var percentage = (double)totalDownloaded / fileSize * 100;
                        progress.Report(percentage);
                    }
                }, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));

                try
                {
                    // Download segments in parallel
                    var tasks = segments.Select(segment =>
                        DownloadSegmentAsync(downloadUrl, segment, cancellationToken)).ToArray();

                    var results = await Task.WhenAll(tasks);

                    // Check if all segments downloaded successfully
                    if (!results.All(r => r))
                    {
                        Logger.LogWarning("Some segments failed, falling back to single connection");
                        return await DownloadSingleConnection(downloadUrl, destinationPath, progress, cancellationToken);
                    }

                    // Final progress update
                    progress?.Report(100.0);

                    // Merge segments
                    await MergeSegments(segments, destinationPath, cancellationToken);

                    Logger.LogInfo("Multi-threaded download completed successfully");
                    return true;
                }
                finally
                {
                    progressTimer?.Dispose();
                }
            }
            finally
            {
                // Cleanup temp directory
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to cleanup temp directory: {ex.Message}");
                }
            }
        }

        private static async Task<bool> DownloadSegmentAsync(
            string downloadUrl,
            DownloadSegment segment,
            CancellationToken cancellationToken)
        {
            try
            {
                // Range is set per request: mutating the shared client's DefaultRequestHeaders
                // would leak the range into every other in-flight request.
                using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                request.Headers.Range = new RangeHeaderValue(segment.Start, segment.End);

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.StatusCode != HttpStatusCode.PartialContent)
                {
                    Logger.LogWarning($"Segment {segment.Index}: Expected 206 Partial Content, got {response.StatusCode}");
                    return false;
                }

                // Confirm the server honoured the exact range we asked for.
                var contentRange = response.Content.Headers.ContentRange;
                if (contentRange?.From is long actualFrom && actualFrom != segment.Start)
                {
                    Logger.LogWarning($"Segment {segment.Index}: server started at {actualFrom}, expected {segment.Start}");
                    return false;
                }

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(segment.TempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);

                    // Thread-safe update of bytes downloaded
                    Interlocked.Add(ref segment.BytesDownloaded, bytesRead);
                }

                // A short segment used to be accepted, silently producing a corrupt archive.
                var received = Interlocked.Read(ref segment.BytesDownloaded);
                if (received != segment.ExpectedLength)
                {
                    Logger.LogWarning($"Segment {segment.Index}: expected {segment.ExpectedLength:N0} bytes, got {received:N0}");
                    return false;
                }

                Logger.LogInfo($"Segment {segment.Index} completed: {received:N0} bytes");
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Segment {segment.Index} failed: {ex.Message}", ex);
                return false;
            }
        }

        private static async Task MergeSegments(List<DownloadSegment> segments, string destinationPath, CancellationToken cancellationToken)
        {
            using var outputStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            // Sort segments by index to ensure correct order
            var sortedSegments = segments.OrderBy(s => s.Index).ToList();

            foreach (var segment in sortedSegments)
            {
                if (!File.Exists(segment.TempFilePath))
                {
                    throw new FileNotFoundException($"Segment file not found: {segment.TempFilePath}");
                }

                using var segmentStream = new FileStream(segment.TempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
                await segmentStream.CopyToAsync(outputStream, 8192, cancellationToken);
            }

            Logger.LogInfo($"Merged {segments.Count} segments into {destinationPath}");
        }
    }
}
