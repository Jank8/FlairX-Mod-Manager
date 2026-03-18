using System;
using System.IO;
using System.Linq;
using SharpSevenZip;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Thrown when an archive requires a password to extract.
    /// </summary>
    public class ArchiveEncryptedException : Exception
    {
        public ArchiveEncryptedException() : base("Archive is encrypted and requires a password.") { }
    }

    /// <summary>
    /// Helper class for archive operations using SharpSevenZip
    /// </summary>
    public static class ArchiveHelper
    {
        /// <summary>
        /// Returns true if the archive is encrypted (requires a password).
        /// </summary>
        public static bool IsEncrypted(string archivePath)
        {
            try
            {
                var format = GetFormatFromExtension(archivePath);
                using var extractor = format.HasValue
                    ? new SharpSevenZipExtractor(archivePath, format.Value)
                    : new SharpSevenZipExtractor(archivePath);

                // ArchiveFileData throws if headers are encrypted; otherwise check IsEncrypted per entry
                var files = extractor.ArchiveFileData;
                return files.Any(f => f.Encrypted);
            }
            catch (SharpSevenZip.Exceptions.SharpSevenZipArchiveException ex)
            {
                // Encrypted headers — can't even read the file list without a password
                if (ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
                    return true;
                return false;
            }
            catch
            {
                return false;
            }
        }
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
                ".xz"   => InArchiveFormat.XZ,
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
                // Check if it's an encryption issue before trying format hint
                if (IsEncrypted(archivePath))
                    throw new ArchiveEncryptedException();

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
        /// Extract password-protected archive with progress reporting.
        /// Throws <see cref="ArchiveEncryptedException"/> if the password is wrong.
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
            catch (SharpSevenZip.Exceptions.SharpSevenZipArchiveException ex)
            {
                // Wrong password — archive is still encrypted
                if (ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                    IsEncrypted(archivePath))
                    throw new ArchiveEncryptedException();

                Logger.LogError($"Failed to extract password-protected archive with SharpSevenZip: {ex.Message}", ex);
                throw;
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

