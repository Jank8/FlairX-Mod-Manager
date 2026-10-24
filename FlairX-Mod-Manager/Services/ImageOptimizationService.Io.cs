using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using FlairX_Mod_Manager.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Png;

namespace FlairX_Mod_Manager.Services
{
    public static partial class ImageOptimizationService
    {
        
        /// <summary>
        /// Helper method to crop and resize image with ImageSharp (replaces GDI+ Graphics operations)
        /// </summary>
        private static Image<Rgba32> CropAndResize(Image<Rgba32> sourceImage, Rectangle cropRect, int targetWidth, int targetHeight)
        {
            return sourceImage.Clone(ctx => ctx
                .Crop(cropRect)
                .Resize(new ResizeOptions
                {
                    Size = new Size(targetWidth, targetHeight),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.Bicubic  // Equivalent to HighQualityBicubic
                }));
        }

        /// <summary>
        /// Clean up only NEW files when optimization is cancelled (preserves already optimized files)
        /// </summary>
        private static void CleanupNewFilesOnly(List<string> newFiles, List<string> processedFiles, string directory)
        {
            Logger.LogInfo($"Cleaning up {newFiles.Count} new files and {processedFiles.Count} processed files in: {directory}");
            
            // Delete the raw new files (preview001, preview002, etc.)
            foreach (var file in newFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                        Logger.LogInfo($"Cleaned up new file: {Path.GetFileName(file)}");
                    }
                    
                    // Also delete any _original copies we created for these new files
                    var dir = Path.GetDirectoryName(file)!;
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
                    var ext = Path.GetExtension(file);
                    var originalPath = Path.Combine(dir, $"{nameWithoutExt}_original{ext}");
                    if (File.Exists(originalPath))
                    {
                        File.Delete(originalPath);
                        Logger.LogInfo($"Cleaned up original copy: {Path.GetFileName(originalPath)}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to clean up file: {file}", ex);
                }
            }
            
            // Delete any processed output files (preview.jpg, preview-01.jpg created from new files)
            foreach (var file in processedFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                        Logger.LogInfo($"Cleaned up processed file: {Path.GetFileName(file)}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to clean up processed file: {file}", ex);
                }
            }
            
            // Clean up minitile if it was created during this operation
            var minitilePath = Path.Combine(directory, GetMinitileFilename());
            try
            {
                // Only delete minitile if we were adding new files (not reoptimizing)
                // This is a heuristic - if there were new files, we might have created/updated minitile
                if (newFiles.Count > 0 && File.Exists(minitilePath))
                {
                    File.Delete(minitilePath);
                    Logger.LogInfo($"Cleaned up: minitile.jpg");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to clean up minitile: {minitilePath}", ex);
            }
        }
        
        /// <summary>
        /// Clean up partially created files when optimization is cancelled
        /// </summary>
        private static void CleanupProcessedFiles(List<string> processedFiles, string directory)
        {
            if (processedFiles == null || processedFiles.Count == 0) return;
            
            Logger.LogInfo($"Cleaning up {processedFiles.Count} partially created files in: {directory}");
            
            foreach (var file in processedFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                        Logger.LogInfo($"Cleaned up: {Path.GetFileName(file)}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to clean up file: {file}", ex);
                }
            }
            
            // Also clean up minitile if it was created
            var minitilePath = Path.Combine(directory, GetMinitileFilename());
            try
            {
                if (File.Exists(minitilePath))
                {
                    File.Delete(minitilePath);
                    Logger.LogInfo($"Cleaned up: minitile.jpg");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to clean up minitile: {minitilePath}", ex);
            }
            
            // Restore _original files back to their original names
            RestoreOriginalFiles(directory);
        }
        
        /// <summary>
        /// Restore _original files back to their original names after cancellation
        /// </summary>
        private static void RestoreOriginalFiles(string directory)
        {
            try
            {
                var originalFiles = Directory.GetFiles(directory)
                    .Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("_original", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                foreach (var originalFile in originalFiles)
                {
                    try
                    {
                        var dir = Path.GetDirectoryName(originalFile)!;
                        var fileName = Path.GetFileName(originalFile);
                        var ext = Path.GetExtension(originalFile);
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFile);
                        
                        // Remove _original suffix
                        var restoredName = nameWithoutExt.Substring(0, nameWithoutExt.Length - "_original".Length);
                        var restoredPath = Path.Combine(dir, $"{restoredName}{ext}");
                        
                        // Delete existing file if it exists (it's a partial/corrupted result)
                        if (File.Exists(restoredPath))
                        {
                            File.Delete(restoredPath);
                            Logger.LogInfo($"Deleted partial file: {Path.GetFileName(restoredPath)}");
                        }
                        
                        File.Move(originalFile, restoredPath);
                        Logger.LogInfo($"Restored: {fileName} -> {Path.GetFileName(restoredPath)}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to restore original file: {originalFile}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to restore original files in: {directory}", ex);
            }
        }
        
        /// <summary>
        /// Get standard filename for category preview
        /// </summary>
        private static string GetCatprevFilename() => $"catprev{SettingsManager.GetImageExtension()}";
        
        /// <summary>
        /// Get standard filename for category preview (wide format 16:9)
        /// </summary>
        private static string GetCatprevWideFilename() => $"catprev-wide{SettingsManager.GetImageExtension()}";
        
        /// <summary>
        /// Get standard filename for category mini thumbnail
        /// </summary>
        private static string GetCatminiFilename() => $"catmini{SettingsManager.GetImageExtension()}";
        
        /// <summary>
        /// Get standard filename for category mini thumbnail (wide format 16:9)
        /// </summary>
        private static string GetCatminiWideFilename() => $"catmini-wide{SettingsManager.GetImageExtension()}";
        
        /// <summary>
        /// Get standard filename for mod preview (index 0 = preview, 1+ = preview-01, preview-02, etc.)
        /// </summary>
        private static string GetPreviewFilename(int index) => index == 0 ? $"preview{SettingsManager.GetImageExtension()}" : $"preview-{index:D2}{SettingsManager.GetImageExtension()}";
        
        /// <summary>
        /// Get standard filename for mod minitile thumbnail
        /// </summary>
        private static string GetMinitileFilename() => $"minitile{SettingsManager.GetImageExtension()}";
        
        /// <summary>
        /// Get standard filename for mod minitile thumbnail (wide format 16:9)
        /// </summary>
        private static string GetMinitileWideFilename() => $"minitile-wide{SettingsManager.GetImageExtension()}";
        
        /// <summary>
        /// Get quality value for current format (WebP and JPEG use their respective user settings)
        /// </summary>
        private static int GetImageQuality()
        {
            var format = SettingsManager.Current.ImageFormat ?? "WebP";
            return format.Equals("WebP", StringComparison.OrdinalIgnoreCase) 
                ? SettingsManager.Current.ImageOptimizerWebPQuality 
                : SettingsManager.Current.ImageOptimizerJpegQuality;
        }
        
        /// <summary>
        /// Save image in the configured format (JPEG or WebP) - SYNCHRONOUS version
        /// Quality: 1-100 for lossy, 101+ for lossless
        /// </summary>
        private static void SaveImage(Image<Rgba32> image, string path, int quality)
        {
            var format = SettingsManager.Current.ImageFormat ?? "JPEG";
            if (format.Equals("WebP", StringComparison.OrdinalIgnoreCase))
            {
                // Quality 101+ = lossless, otherwise lossy
                var fileFormat = quality >= 101 
                    ? WebpFileFormatType.Lossless 
                    : WebpFileFormatType.Lossy;
                
                image.SaveAsWebp(path, new WebpEncoder 
                { 
                    Quality = quality >= 101 ? 100 : quality, // Lossless ignores quality parameter
                    FileFormat = fileFormat,
                    Method = WebpEncodingMethod.BestQuality
                });
            }
            else
            {
                image.SaveAsJpeg(path, new JpegEncoder { Quality = quality });
            }
        }
        
        /// <summary>
        /// Save image in the configured format (JPEG or WebP) - ASYNC version
        /// Quality: 1-100 for lossy, 101+ for lossless
        /// </summary>
        private static async Task SaveImageAsync(Image<Rgba32> image, string path, int quality)
        {
            var format = SettingsManager.Current.ImageFormat ?? "JPEG";
            if (format.Equals("WebP", StringComparison.OrdinalIgnoreCase))
            {
                // Quality 101+ = lossless, otherwise lossy
                var fileFormat = quality >= 101 
                    ? WebpFileFormatType.Lossless 
                    : WebpFileFormatType.Lossy;
                
                await image.SaveAsWebpAsync(path, new WebpEncoder 
                { 
                    Quality = quality >= 101 ? 100 : quality, // Lossless ignores quality parameter
                    FileFormat = fileFormat,
                    Method = WebpEncodingMethod.BestQuality
                });
            }
            else
            {
                await image.SaveAsJpegAsync(path, new JpegEncoder { Quality = quality });
            }
        }
        
        /// <summary>
        /// Save optimized image with crop (no resize for preview files) - ASYNC version
        /// </summary>
        private static async Task<bool> SaveOptimizedImageAsync(Image<Rgba32> sourceImage, string targetPath, Rectangle cropRect, int quality)
        {
            const int previewTargetSize = 1000;

            try
            {
                // Crop the image using ImageSharp
                using var cropped = sourceImage.Clone(ctx => ctx.Crop(cropRect));

                // The crop rectangle is computed for a 1000px preview, but this path used to save
                // without resizing - so a 4K source produced a ~2160x2160 "preview" (large files,
                // slow loading, more memory). Downscale only: small previews keep their size.
                if (cropped.Width > previewTargetSize || cropped.Height > previewTargetSize)
                {
                    using var resized = cropped.Clone(ctx => ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(previewTargetSize, previewTargetSize),
                        Mode = ResizeMode.Max,
                    }));

                    await SaveImageAsync(resized, targetPath, quality);
                    return true;
                }

                await SaveImageAsync(cropped, targetPath, quality);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save optimized image: {targetPath}", ex);
            }
            return false;
        }

        /// <summary>
        /// Save image without any cropping or resizing (No Crop mode) - ASYNC version
        /// </summary>
        private static async Task<bool> SaveAsJpegOnlyAsync(Image<Rgba32> sourceImage, string targetPath, int quality)
        {
            try
            {
                await SaveImageAsync(sourceImage, targetPath, quality);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save image as JPEG: {targetPath}", ex);
            }
            return false;
        }

        /// <summary>
        /// Create backup ZIP of image files
        /// </summary>
        public static void CreateBackup(string directory, List<string> filesToBackup)
        {
            try
            {
                if (filesToBackup == null || filesToBackup.Count == 0)
                {
                    Logger.LogWarning($"No files to backup in {directory}");
                    return;
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFileName = $"preview_backup_{timestamp}.zip";
                string backupPath = Path.Combine(directory, backupFileName);

                using (var zipArchive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
                {
                    foreach (var file in filesToBackup)
                    {
                        if (File.Exists(file))
                        {
                            string entryName = Path.GetFileName(file);
                            zipArchive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                            Logger.LogInfo($"Added to backup: {entryName}");
                        }
                    }
                }

                Logger.LogInfo($"Backup created: {backupPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create backup in {directory}", ex);
                throw;
            }
        }
    }
}



