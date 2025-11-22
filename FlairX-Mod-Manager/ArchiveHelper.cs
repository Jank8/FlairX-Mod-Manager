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
        /// Extract archive to directory
        /// </summary>
        public static void ExtractToDirectory(string archivePath, string destinationPath)
        {
            
            try
            {
                using var extractor = new SharpSevenZipExtractor(archivePath);
                extractor.ExtractArchive(destinationPath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to extract archive with SharpSevenZip: {ex.Message}", ex);
                throw;
            }
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
                {
                    extractor.Extracting += (sender, e) => progress.Report(e.PercentDone);
                }
                
                extractor.ExtractArchive(destinationPath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to extract archive with SharpSevenZip: {ex.Message}", ex);
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
