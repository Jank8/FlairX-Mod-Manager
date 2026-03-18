using System;
using System.IO;
using System.Linq;
using SharpSevenZip;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Helper class for archive operations using SharpSevenZip
    /// </summary>
    public static class ArchiveHelper
    {
        /// <summary>
        /// Returns the SharpSevenZip InArchiveFormat for a given file extension, or null if unknown.
        /// </summary>
        private static InArchiveFormat? GetFormatFromExtension(string archivePath)
        {
            return Path.GetExtension(archivePath).ToLowerInvariant() switch
            {
                ".zip"  => InArchiveFormat.Zip,
                ".7z"   => InArchiveFormat.SevenZip,
                ".rar"  => InArchiveFormat.Rar,
                ".tar"  => InArchiveFormat.Tar,
                ".gz"   => InArchiveFormat.GZip,
                ".bz2"  => InArchiveFormat.BZip2,
                ".xz"   => InArchiveFormat.Xz,
                _       => null
            };
        }

        /// <summary>
        /// Extract archive to directory
        /// </summary>
        public static void ExtractToDirectory(string archivePath, string destinationPath)
        {
            ExtractToDirectory(archivePath, destinationPath, null);
        }
        
        /// <summary>
        /// Extract archive with progress reporting
        /// </summary>
        public static void ExtractToDirectory(string archivePath, string destinationPath, IProgress<int>? progress)
        {
            try
            {
                using var extractor = new SharpSevenZipExtractor(archivePath);
                
                if (progress != null)
                    extractor.Extracting += (sender, e) => progress.Report(e.PercentDone);
                
                extractor.ExtractArchive(destinationPath);
            }
            catch (SharpSevenZip.Exceptions.SharpSevenZipArchiveException ex)
            {
                Logger.LogWarning($"SharpSevenZip failed to open archive (format detection issue), retrying with explicit format hint: {ex.Message}");
                ExtractWithFormatHint(archivePath, destinationPath, progress);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to extract archive with SharpSevenZip: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Retry extraction with an explicit format hint derived from the file extension.
        /// Falls back to System.IO.Compression for .zip files.
        /// </summary>
        private static void ExtractWithFormatHint(string archivePath, string destinationPath, IProgress<int>? progress)
        {
            // .zip fallback — System.IO.Compression is always available and reliable
            if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInfo("Falling back to System.IO.Compression for .zip archive");
                System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, destinationPath, true);
                return;
            }

            var format = GetFormatFromExtension(archivePath);
            if (format == null)
            {
                throw new NotSupportedException($"Unsupported or unrecognised archive format: {Path.GetExtension(archivePath)}");
            }

            try
            {
                using var extractor = new SharpSevenZipExtractor(archivePath, format.Value);

                if (progress != null)
                    extractor.Extracting += (sender, e) => progress.Report(e.PercentDone);

                extractor.ExtractArchive(destinationPath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to extract archive with explicit format hint ({format}): {ex.Message}", ex);
                throw;
            }
        }
        
        /// <summary>
        /// Extract password-protected archive with progress reporting
        /// </summary>
        public static void ExtractToDirectory(string archivePath, string destinationPath, string password, IProgress<int>? progress)
        {
            try
            {
                using var extractor = new SharpSevenZipExtractor(archivePath, password);
                
                if (progress != null)
                    extractor.Extracting += (sender, e) => progress.Report(e.PercentDone);
                
                extractor.ExtractArchive(destinationPath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to extract password-protected archive with SharpSevenZip: {ex.Message}", ex);
                throw;
            }
        }
        
        /// <summary>
        /// Create archive from directory
        /// </summary>
        public static void CreateFromDirectory(string sourceDirectory, string archivePath)
        {
            try
            {
                var compressor = new SharpSevenZipCompressor();
                compressor.CompressDirectory(sourceDirectory, archivePath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create archive with SharpSevenZip: {ex.Message}", ex);
                throw;
            }
        }
        
        /// <summary>
        /// Create archive from multiple files
        /// </summary>
        public static void CreateArchiveFromFiles(string archivePath, System.Collections.Generic.Dictionary<string, string> files)
        {
            try
            {
                var compressor = new SharpSevenZipCompressor();
                compressor.CompressFileDictionary(files, archivePath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create archive with SharpSevenZip: {ex.Message}", ex);
                throw;
            }
        }
    }
}

