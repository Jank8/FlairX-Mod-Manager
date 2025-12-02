using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using FlairX_Mod_Manager.Models;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Delegate for crop inspection callback
    /// </summary>
    public delegate Task<CropInspectionResult?> CropInspectionHandler(Image sourceImage, Rectangle suggestedCrop, int targetWidth, int targetHeight, string imageType);

    /// <summary>
    /// Result from crop inspection
    /// </summary>
    public class CropInspectionResult
    {
        public bool Confirmed { get; set; }
        public Rectangle CropRectangle { get; set; }
    }

    /// <summary>
    /// Service for image optimization operations across all contexts (Manual, Drag&Drop, Auto)
    /// </summary>
    public static class ImageOptimizationService
    {
        /// <summary>
        /// Event raised when crop inspection is needed
        /// </summary>
        public static event CropInspectionHandler? CropInspectionRequested;
        /// <summary>
        /// Process category preview image with specified optimization mode (backward compatibility)
        /// </summary>
        public static Task ProcessCategoryPreviewAsync(string categoryDir, OptimizationMode mode)
        {
            // Create context from current settings
            var context = GetOptimizationContext(OptimizationTrigger.Manual);
            context.Mode = mode;
            return ProcessCategoryPreviewAsync(categoryDir, context);
        }

        /// <summary>
        /// Process category preview image with specified optimization context
        /// </summary>
        public static async Task ProcessCategoryPreviewAsync(string categoryDir, OptimizationContext context)
        {
            if (string.IsNullOrEmpty(categoryDir) || !Directory.Exists(categoryDir))
            {
                Logger.LogWarning($"Category directory not found: {categoryDir}");
                return;
            }

            // Handle different optimization modes
            switch (context.Mode)
            {
                case OptimizationMode.Full:
                    await ProcessCategoryPreviewFullAsync(categoryDir, context);
                    break;
                    
                case OptimizationMode.Lite:
                    ProcessCategoryPreviewLite(categoryDir, context);
                    break;
                    
                case OptimizationMode.Rename:
                    ProcessCategoryPreviewRename(categoryDir, context);
                    break;
                    
                case OptimizationMode.RenameOnly:
                    ProcessCategoryPreviewRenameOnly(categoryDir);
                    break;
                    
                default:
                    Logger.LogWarning($"Unknown optimization mode: {context.Mode}");
                    break;
            }
        }

        /// <summary>
        /// Synchronous wrapper for backward compatibility
        /// </summary>
        public static void ProcessCategoryPreview(string categoryDir, OptimizationContext context)
        {
            ProcessCategoryPreviewAsync(categoryDir, context).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Process category preview in Full mode - complete optimization with cropping and thumbnails
        /// </summary>
        private static async Task ProcessCategoryPreviewFullAsync(string categoryDir, OptimizationContext context)
        {
            try
            {
                Logger.LogInfo($"Processing category preview (Full mode) in: {categoryDir}");
                
                // Create backup if enabled
                if (context.CreateBackups)
                {
                    var filesToBackup = Directory.GetFiles(categoryDir)
                        .Where(f =>
                        {
                            var fileName = Path.GetFileName(f).ToLower();
                            return (fileName.StartsWith("preview") || fileName.StartsWith("catprev") || fileName.StartsWith("catmini")) &&
                                   (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                        })
                        .ToList();
                    
                    if (filesToBackup.Count > 0)
                    {
                        CreateBackup(categoryDir, filesToBackup);
                    }
                }
                
                // Look for existing catprev files and other preview files
                var catprevFiles = Directory.GetFiles(categoryDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("catprev") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .ToArray();
                
                var otherPreviewFiles = Directory.GetFiles(categoryDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return (fileName.StartsWith("catpreview") || fileName.StartsWith("preview")) &&
                               !fileName.StartsWith("catprev") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .ToArray();
                
                var allPreviewFiles = catprevFiles.Concat(otherPreviewFiles).ToArray();
                
                if (allPreviewFiles.Length == 0)
                {
                    Logger.LogInfo($"No preview files found in category: {categoryDir}");
                    return;
                }
                
                var previewPath = allPreviewFiles[0];
                var catprevPath = Path.Combine(categoryDir, "catprev.jpg");
                var catminiPath = Path.Combine(categoryDir, "catmini.jpg");
                
                using (var img = Image.FromFile(previewPath))
                {
                    // Get crop rectangle with optional inspection for catprev
                    var catprevCropRect = await GetCropRectangleWithInspectionAsync(
                        img, 600, 722, context, "catprev.jpg");
                    
                    if (catprevCropRect == null)
                    {
                        Logger.LogInfo($"Skipped catprev.jpg generation");
                        return; // User skipped
                    }
                    
                    // Generate catprev.jpg (600x722)
                    using (var catprev = new Bitmap(600, 722))
                    using (var g = Graphics.FromImage(catprev))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        
                        var destRect = new Rectangle(0, 0, 600, 722);
                        g.DrawImage(img, destRect, catprevCropRect.Value, GraphicsUnit.Pixel);
                        
                        var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                            .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                        if (jpegEncoder != null)
                        {
                            var jpegParams = new EncoderParameters(1);
                            jpegParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)context.JpegQuality);
                            catprev.Save(catprevPath, jpegEncoder, jpegParams);
                            Logger.LogInfo($"Generated catprev.jpg");
                        }
                    }
                    
                    // Get crop rectangle with optional inspection for catmini
                    var catminiCropRect = await GetCropRectangleWithInspectionAsync(
                        img, 600, 722, context, "catmini.jpg");
                    
                    if (catminiCropRect == null)
                    {
                        Logger.LogInfo($"Skipped catmini.jpg generation");
                        return; // User skipped
                    }
                    
                    // Generate catmini.jpg (600x722) - same dimensions as catprev
                    using (var catmini = new Bitmap(600, 722))
                    using (var g = Graphics.FromImage(catmini))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        
                        var destRect = new Rectangle(0, 0, 600, 722);
                        g.DrawImage(img, destRect, catminiCropRect.Value, GraphicsUnit.Pixel);
                        
                        var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                            .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                        if (jpegEncoder != null)
                        {
                            var jpegParams = new EncoderParameters(1);
                            jpegParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)context.JpegQuality);
                            catmini.Save(catminiPath, jpegEncoder, jpegParams);
                            Logger.LogInfo($"Generated catmini.jpg");
                        }
                    }
                }
                
                // Clean up original files if not keeping originals
                if (!context.KeepOriginals)
                {
                    foreach (var file in allPreviewFiles)
                    {
                        if (!file.Equals(catprevPath, StringComparison.OrdinalIgnoreCase) &&
                            !file.Equals(catminiPath, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                File.Delete(file);
                                Logger.LogInfo($"Deleted original: {Path.GetFileName(file)}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Failed to delete original file: {file}", ex);
                            }
                        }
                    }
                }
                
                Logger.LogInfo($"Category preview processing (Full mode) completed for: {categoryDir}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process category preview (Full mode) in {categoryDir}", ex);
            }
        }

        /// <summary>
        /// Process category preview in Lite mode - quality conversion without resizing or cropping
        /// </summary>
        private static void ProcessCategoryPreviewLite(string categoryDir, OptimizationContext context)
        {
            try
            {
                Logger.LogInfo($"Processing category preview (Lite mode) in: {categoryDir}");
                
                // Create backup if enabled
                if (context.CreateBackups)
                {
                    var filesToBackup = Directory.GetFiles(categoryDir)
                        .Where(f =>
                        {
                            var fileName = Path.GetFileName(f).ToLower();
                            return (fileName.StartsWith("preview") || fileName.StartsWith("catprev")) &&
                                   (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                        })
                        .ToList();
                    
                    if (filesToBackup.Count > 0)
                    {
                        CreateBackup(categoryDir, filesToBackup);
                    }
                }
                
                // Find preview files
                var previewFiles = Directory.GetFiles(categoryDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return (fileName.StartsWith("catprev") || fileName.StartsWith("preview")) &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .ToList();
                
                if (previewFiles.Count == 0)
                {
                    Logger.LogInfo($"No preview files found in category: {categoryDir}");
                    return;
                }
                
                var sourceFile = previewFiles[0];
                var catprevPath = Path.Combine(categoryDir, "catprev.jpg");
                
                using (var img = Image.FromFile(sourceFile))
                {
                    // Save as JPEG with quality setting, preserving original dimensions
                    var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                        .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    if (jpegEncoder != null)
                    {
                        var jpegParams = new EncoderParameters(1);
                        jpegParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)context.JpegQuality);
                        img.Save(catprevPath, jpegEncoder, jpegParams);
                        Logger.LogInfo($"Converted (Lite): {Path.GetFileName(sourceFile)} -> catprev.jpg");
                    }
                }
                
                // Do NOT generate catmini in Lite mode
                
                // Clean up original files if not keeping originals
                if (!context.KeepOriginals && !sourceFile.Equals(catprevPath, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(sourceFile);
                        Logger.LogInfo($"Deleted original: {Path.GetFileName(sourceFile)}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to delete original file: {sourceFile}", ex);
                    }
                }
                
                Logger.LogInfo($"Category preview processing (Lite mode) completed for: {categoryDir}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process category preview (Lite mode) in {categoryDir}", ex);
            }
        }

        /// <summary>
        /// Process category preview in Rename mode - only rename to standard names (no thumbnails)
        /// </summary>
        private static void ProcessCategoryPreviewRename(string categoryDir, OptimizationContext context)
        {
            try
            {
                Logger.LogInfo($"Processing category preview (Rename mode) in: {categoryDir}");
                
                // Find preview files
                var previewFiles = Directory.GetFiles(categoryDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return (fileName.StartsWith("catprev") || fileName.StartsWith("preview")) &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .ToList();
                
                if (previewFiles.Count == 0)
                {
                    Logger.LogInfo($"No preview files found in category: {categoryDir}");
                    return;
                }
                
                var sourceFile = previewFiles[0];
                var ext = Path.GetExtension(sourceFile);
                var catprevPath = Path.Combine(categoryDir, $"catprev{ext}");
                
                // Rename to catprev if needed
                if (!sourceFile.Equals(catprevPath, StringComparison.OrdinalIgnoreCase))
                {
                    var tempPath = Path.Combine(categoryDir, $"_temp_{Guid.NewGuid()}{ext}");
                    File.Move(sourceFile, tempPath);
                    File.Move(tempPath, catprevPath);
                    Logger.LogInfo($"Renamed: {Path.GetFileName(sourceFile)} -> catprev{ext}");
                }
                
                // Do NOT generate catmini in Rename mode for categories - only rename
                
                Logger.LogInfo($"Category preview processing (Rename mode) completed for: {categoryDir}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process category preview (Rename mode) in {categoryDir}", ex);
            }
        }

        /// <summary>
        /// Process category preview in RenameOnly mode - only rename files without any processing
        /// </summary>
        private static void ProcessCategoryPreviewRenameOnly(string categoryDir)
        {
            try
            {
                Logger.LogInfo($"Processing category preview (RenameOnly mode) in: {categoryDir}");
                
                // Find preview files
                var previewFiles = Directory.GetFiles(categoryDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return (fileName.StartsWith("catprev") || fileName.StartsWith("preview")) &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .ToList();
                
                if (previewFiles.Count == 0)
                {
                    Logger.LogInfo($"No preview files found in category: {categoryDir}");
                    return;
                }
                
                var sourceFile = previewFiles[0];
                var ext = Path.GetExtension(sourceFile);
                var catprevPath = Path.Combine(categoryDir, $"catprev{ext}");
                
                // Rename to catprev only
                if (!sourceFile.Equals(catprevPath, StringComparison.OrdinalIgnoreCase))
                {
                    var tempPath = Path.Combine(categoryDir, $"_temp_{Guid.NewGuid()}{ext}");
                    File.Move(sourceFile, tempPath);
                    File.Move(tempPath, catprevPath);
                    Logger.LogInfo($"Renamed: {Path.GetFileName(sourceFile)} -> catprev{ext}");
                }
                
                // Do NOT generate thumbnails in RenameOnly mode
                
                Logger.LogInfo($"Category preview processing (RenameOnly mode) completed for: {categoryDir}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process category preview (RenameOnly mode) in {categoryDir}", ex);
            }
        }

        /// <summary>
        /// Process mod preview images with specified optimization mode (backward compatibility)
        /// </summary>
        public static Task ProcessModPreviewImagesAsync(string modDir, OptimizationMode mode)
        {
            // Create context from current settings
            var context = GetOptimizationContext(OptimizationTrigger.Manual);
            context.Mode = mode;
            return ProcessModPreviewImagesAsync(modDir, context);
        }

        /// <summary>
        /// Process mod preview images with specified optimization context
        /// </summary>
        public static async Task ProcessModPreviewImagesAsync(string modDir, OptimizationContext context)
        {
            if (string.IsNullOrEmpty(modDir) || !Directory.Exists(modDir))
            {
                Logger.LogWarning($"Mod directory not found: {modDir}");
                return;
            }

            // Handle different optimization modes
            switch (context.Mode)
            {
                case OptimizationMode.Full:
                    await ProcessModPreviewImagesFullAsync(modDir, context);
                    break;
                    
                case OptimizationMode.Lite:
                    await ProcessModPreviewImagesLiteAsync(modDir, context);
                    break;
                    
                case OptimizationMode.Rename:
                    await ProcessModPreviewImagesRenameAsync(modDir, context);
                    break;
                    
                case OptimizationMode.RenameOnly:
                    ProcessModPreviewImagesRenameOnly(modDir);
                    break;
                    
                default:
                    Logger.LogWarning($"Unknown optimization mode: {context.Mode}");
                    break;
            }
        }

        /// <summary>
        /// Synchronous wrapper for backward compatibility
        /// </summary>
        public static void ProcessModPreviewImages(string modDir, OptimizationContext context)
        {
            ProcessModPreviewImagesAsync(modDir, context).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Process mod preview images in Full mode - complete optimization with cropping and thumbnails
        /// </summary>
        private static async Task ProcessModPreviewImagesFullAsync(string modDir, OptimizationContext context)
        {
            try
            {
                Logger.LogInfo($"Processing mod preview images (Full mode) in: {modDir}");
                
                // Create backup if enabled
                if (context.CreateBackups)
                {
                    var filesToBackup = Directory.GetFiles(modDir)
                        .Where(f =>
                        {
                            var fileName = Path.GetFileName(f).ToLower();
                            return fileName.StartsWith("preview") &&
                                   (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                        })
                        .ToList();
                    
                    if (filesToBackup.Count > 0)
                    {
                        CreateBackup(modDir, filesToBackup);
                    }
                }
                
                // Find all preview image files
                var previewFiles = Directory.GetFiles(modDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("preview") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .OrderBy(f => f)
                    .ToList();
                
                if (previewFiles.Count == 0)
                {
                    Logger.LogInfo($"No preview files found in: {modDir}");
                    return;
                }
                
                // Process each preview file
                var processedFiles = new List<string>();
                for (int i = 0; i < previewFiles.Count && i < AppConstants.MAX_PREVIEW_IMAGES; i++)
                {
                    var sourceFile = previewFiles[i];
                    string targetFileName = i == 0 ? "preview.jpg" : $"preview-{i:D2}.jpg";
                    var targetPath = Path.Combine(modDir, targetFileName);
                    
                    try
                    {
                        using (var img = Image.FromFile(sourceFile))
                        {
                            // For Full mode, we optimize the image (crop to square if needed, apply quality)
                            int targetSize = Math.Min(img.Width, img.Height);
                            
                            // Get crop rectangle with optional inspection
                            var squareCropRect = await GetCropRectangleWithInspectionAsync(
                                img, targetSize, targetSize, context, $"preview.jpg #{i + 1}");
                            
                            if (squareCropRect == null)
                            {
                                Logger.LogInfo($"Skipped: {Path.GetFileName(sourceFile)}");
                                continue; // User skipped this image
                            }
                            
                            using (var optimized = new Bitmap(targetSize, targetSize))
                            using (var g = Graphics.FromImage(optimized))
                            {
                                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                g.CompositingQuality = CompositingQuality.HighQuality;
                                g.SmoothingMode = SmoothingMode.HighQuality;
                                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                                
                                // Crop to square
                                var destRect = new Rectangle(0, 0, targetSize, targetSize);
                                g.DrawImage(img, destRect, squareCropRect.Value, GraphicsUnit.Pixel);
                                
                                // Save as JPEG with quality setting
                                var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                                    .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                                if (jpegEncoder != null)
                                {
                                    var jpegParams = new EncoderParameters(1);
                                    jpegParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)context.JpegQuality);
                                    optimized.Save(targetPath, jpegEncoder, jpegParams);
                                    processedFiles.Add(targetPath);
                                    Logger.LogInfo($"Optimized: {Path.GetFileName(sourceFile)} -> {targetFileName}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to process preview file: {sourceFile}", ex);
                    }
                }
                
                // Generate minitile.jpg (600x722 thumbnail)
                if (processedFiles.Count > 0)
                {
                    await GenerateMinitileAsync(modDir, processedFiles[0], context);
                }
                
                // Clean up original files if not keeping originals
                if (!context.KeepOriginals)
                {
                    foreach (var file in previewFiles)
                    {
                        if (!processedFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                        {
                            try
                            {
                                File.Delete(file);
                                Logger.LogInfo($"Deleted original: {Path.GetFileName(file)}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Failed to delete original file: {file}", ex);
                            }
                        }
                    }
                }
                
                Logger.LogInfo($"Mod preview processing (Full mode) completed for: {modDir}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process mod preview images (Full mode) in {modDir}", ex);
            }
        }

        /// <summary>
        /// Process mod preview images in Lite mode - quality conversion without resizing or cropping
        /// </summary>
        private static async Task ProcessModPreviewImagesLiteAsync(string modDir, OptimizationContext context)
        {
            try
            {
                Logger.LogInfo($"Processing mod preview images (Lite mode) in: {modDir}");
                
                // Create backup if enabled
                if (context.CreateBackups)
                {
                    var filesToBackup = Directory.GetFiles(modDir)
                        .Where(f =>
                        {
                            var fileName = Path.GetFileName(f).ToLower();
                            return fileName.StartsWith("preview") &&
                                   (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                        })
                        .ToList();
                    
                    if (filesToBackup.Count > 0)
                    {
                        CreateBackup(modDir, filesToBackup);
                    }
                }
                
                // Find all preview image files
                var previewFiles = Directory.GetFiles(modDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("preview") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .OrderBy(f => f)
                    .ToList();
                
                if (previewFiles.Count == 0)
                {
                    Logger.LogInfo($"No preview files found in: {modDir}");
                    return;
                }
                
                // Process each preview file - convert to JPEG without resizing
                var processedFiles = new List<string>();
                for (int i = 0; i < previewFiles.Count && i < AppConstants.MAX_PREVIEW_IMAGES; i++)
                {
                    var sourceFile = previewFiles[i];
                    string targetFileName = i == 0 ? "preview.jpg" : $"preview-{i:D2}.jpg";
                    var targetPath = Path.Combine(modDir, targetFileName);
                    
                    try
                    {
                        using (var img = Image.FromFile(sourceFile))
                        {
                            // Save as JPEG with quality setting, preserving original dimensions
                            var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                                .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                            if (jpegEncoder != null)
                            {
                                var jpegParams = new EncoderParameters(1);
                                jpegParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)context.JpegQuality);
                                img.Save(targetPath, jpegEncoder, jpegParams);
                                processedFiles.Add(targetPath);
                                Logger.LogInfo($"Converted (Lite): {Path.GetFileName(sourceFile)} -> {targetFileName}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to process preview file: {sourceFile}", ex);
                    }
                }
                
                // Generate minitile.jpg (600x722 thumbnail) from first preview
                if (processedFiles.Count > 0)
                {
                    await GenerateMinitileAsync(modDir, processedFiles[0], context);
                }
                
                // Clean up original files if not keeping originals
                if (!context.KeepOriginals)
                {
                    foreach (var file in previewFiles)
                    {
                        if (!processedFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                        {
                            try
                            {
                                File.Delete(file);
                                Logger.LogInfo($"Deleted original: {Path.GetFileName(file)}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Failed to delete original file: {file}", ex);
                            }
                        }
                    }
                }
                
                Logger.LogInfo($"Mod preview processing (Lite mode) completed for: {modDir}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process mod preview images (Lite mode) in {modDir}", ex);
            }
        }

        /// <summary>
        /// Process mod preview images in Rename mode - generate thumbnails and rename to standard names
        /// </summary>
        private static async Task ProcessModPreviewImagesRenameAsync(string modDir, OptimizationContext context)
        {
            try
            {
                Logger.LogInfo($"Processing mod preview images (Rename mode) in: {modDir}");
                
                // Find all preview image files
                var previewFiles = Directory.GetFiles(modDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("preview") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .OrderBy(f => f)
                    .ToList();
                
                if (previewFiles.Count == 0)
                {
                    Logger.LogInfo($"No preview files found in: {modDir}");
                    return;
                }
                
                // FIRST: Generate minitile.jpg from first preview (BEFORE renaming)
                var firstPreviewPath = previewFiles[0];
                Logger.LogInfo($"Generating minitile from: {Path.GetFileName(firstPreviewPath)}");
                await GenerateMinitileAsync(modDir, firstPreviewPath, context);
                
                // SECOND: Rename files to standard names
                for (int i = 0; i < previewFiles.Count && i < AppConstants.MAX_PREVIEW_IMAGES; i++)
                {
                    var sourceFile = previewFiles[i];
                    var ext = Path.GetExtension(sourceFile);
                    string targetFileName = i == 0 ? $"preview{ext}" : $"preview-{i:D2}{ext}";
                    var targetPath = Path.Combine(modDir, targetFileName);
                    
                    if (!sourceFile.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            // Use temp name to avoid conflicts
                            var tempPath = Path.Combine(modDir, $"_temp_{Guid.NewGuid()}{ext}");
                            File.Move(sourceFile, tempPath);
                            File.Move(tempPath, targetPath);
                            Logger.LogInfo($"Renamed: {Path.GetFileName(sourceFile)} -> {targetFileName}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to rename file: {sourceFile}", ex);
                        }
                    }
                }
                
                Logger.LogInfo($"Mod preview processing (Rename mode) completed for: {modDir}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process mod preview images (Rename mode) in {modDir}", ex);
            }
        }

        /// <summary>
        /// Process mod preview images in RenameOnly mode - only rename files without any processing
        /// </summary>
        private static void ProcessModPreviewImagesRenameOnly(string modDir)
        {
            try
            {
                Logger.LogInfo($"Processing mod preview images (RenameOnly mode) in: {modDir}");
                
                // Find all preview image files
                var previewFiles = Directory.GetFiles(modDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("preview") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .OrderBy(f => f)
                    .ToList();
                
                if (previewFiles.Count == 0)
                {
                    Logger.LogInfo($"No preview files found in: {modDir}");
                    return;
                }
                
                // Rename files to standard names only
                for (int i = 0; i < previewFiles.Count && i < AppConstants.MAX_PREVIEW_IMAGES; i++)
                {
                    var sourceFile = previewFiles[i];
                    var ext = Path.GetExtension(sourceFile);
                    string targetFileName = i == 0 ? $"preview{ext}" : $"preview-{i:D2}{ext}";
                    var targetPath = Path.Combine(modDir, targetFileName);
                    
                    if (!sourceFile.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            // Use temp name to avoid conflicts
                            var tempPath = Path.Combine(modDir, $"_temp_{Guid.NewGuid()}{ext}");
                            File.Move(sourceFile, tempPath);
                            File.Move(tempPath, targetPath);
                            Logger.LogInfo($"Renamed: {Path.GetFileName(sourceFile)} -> {targetFileName}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to rename file: {sourceFile}", ex);
                        }
                    }
                }
                
                // Do NOT generate thumbnails in RenameOnly mode
                
                Logger.LogInfo($"Mod preview processing (RenameOnly mode) completed for: {modDir}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process mod preview images (RenameOnly mode) in {modDir}", ex);
            }
        }

        /// <summary>
        /// Generate minitile.jpg thumbnail (600x722) from preview image
        /// </summary>
        private static async Task GenerateMinitileAsync(string modDir, string previewPath, OptimizationContext context)
        {
            try
            {
                Logger.LogInfo($"Generating minitile for: {modDir}");
                
                var minitilePath = Path.Combine(modDir, "minitile.jpg");
                
                using (var img = Image.FromFile(previewPath))
                {
                    // Get crop rectangle with optional inspection
                    var srcRect = await GetCropRectangleWithInspectionAsync(
                        img, 600, 722, context, "minitile.jpg");
                    
                    if (srcRect == null)
                    {
                        Logger.LogInfo($"Skipped minitile generation for: {modDir}");
                        return; // User skipped
                    }
                    
                    using (var minitile = new Bitmap(600, 722))
                    using (var g = Graphics.FromImage(minitile))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        
                        var destRect = new Rectangle(0, 0, 600, 722);
                        g.DrawImage(img, destRect, srcRect.Value, GraphicsUnit.Pixel);
                        
                        // Save as JPEG with quality setting
                        var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                            .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                        if (jpegEncoder != null)
                        {
                            var jpegParams = new EncoderParameters(1);
                            jpegParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)context.JpegQuality);
                            minitile.Save(minitilePath, jpegEncoder, jpegParams);
                            Logger.LogInfo($"Minitile generated: {minitilePath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to generate minitile for {modDir}", ex);
            }
        }
        
        /// <summary>
        /// Synchronous wrapper for backward compatibility
        /// </summary>
        private static void GenerateMinitile(string modDir, string previewPath, OptimizationContext context)
        {
            GenerateMinitileAsync(modDir, previewPath, context).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Convert CropStrategy enum to CropType enum
        /// </summary>
        private static CropType ConvertCropStrategy(CropStrategy strategy)
        {
            return strategy switch
            {
                CropStrategy.Center => CropType.Center,
                CropStrategy.Smart => CropType.Smart,
                CropStrategy.Entropy => CropType.Entropy,
                CropStrategy.Attention => CropType.Attention,
                CropStrategy.ManualOnly => CropType.ManualOnly,
                _ => CropType.Center
            };
        }

        /// <summary>
        /// Get crop rectangle with optional inspection
        /// </summary>
        public static async Task<Rectangle?> GetCropRectangleWithInspectionAsync(
            Image image, 
            int targetWidth, 
            int targetHeight, 
            OptimizationContext context,
            string imageType)
        {
            var cropType = ConvertCropStrategy(context.CropStrategy);
            var suggestedCrop = ImageCropService.CalculateCropRectangle(image, targetWidth, targetHeight, cropType);

            // Show inspection panel ONLY IF:
            // 1. UI interaction is allowed (not background processing)
            // 2. AND (ManualOnly mode OR Inspect&Edit is enabled)
            bool needsInspection = context.AllowUIInteraction && 
                                  (context.CropStrategy == CropStrategy.ManualOnly || 
                                   context.InspectAndEditEnabled);

            if (needsInspection && CropInspectionRequested != null)
            {
                try
                {
                    Logger.LogInfo($"Requesting crop inspection for {imageType}");
                    var result = await CropInspectionRequested(image, suggestedCrop, targetWidth, targetHeight, imageType);
                    if (result != null)
                    {
                        if (result.Confirmed)
                        {
                            Logger.LogInfo($"User confirmed crop for {imageType}");
                            return result.CropRectangle;
                        }
                        else
                        {
                            // User skipped - return null to skip this image
                            Logger.LogInfo($"User skipped crop for {imageType}");
                            return null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Crop inspection failed for {imageType}", ex);
                    // Fall back to automatic crop
                }
            }

            // No inspection needed, UI not allowed, or failed - use automatic crop
            return suggestedCrop;
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

        /// <summary>
        /// Optimize all preview images in the mod library (for manual optimization)
        /// </summary>
        public static async Task OptimizeAllPreviewsAsync()
        {
            try
            {
                Logger.LogInfo("Starting manual preview optimization");
                
                var modLibraryPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modLibraryPath) || !Directory.Exists(modLibraryPath))
                {
                    Logger.LogWarning("Mod library path not found for optimization");
                    return;
                }

                var context = GetOptimizationContext(OptimizationTrigger.Manual);
                
                // Check if we need sequential processing for crop inspection
                bool needsSequentialProcessing = context.InspectAndEditEnabled || context.CropStrategy == CropStrategy.ManualOnly;
                
                if (needsSequentialProcessing)
                {
                    // Sequential processing with potential user interaction
                    Logger.LogInfo("Using sequential processing (crop inspection enabled)");
                    var categoryDirs = Directory.GetDirectories(modLibraryPath);
                    
                    foreach (var categoryDir in categoryDirs)
                    {
                        if (!Directory.Exists(categoryDir)) continue;
                        
                        // Process category preview
                        await ProcessCategoryPreviewAsync(categoryDir, context);
                        
                        // Process all mods in category
                        var modDirs = Directory.GetDirectories(categoryDir);
                        foreach (var modDir in modDirs)
                        {
                            await ProcessModPreviewImagesAsync(modDir, context);
                        }
                    }
                }
                else
                {
                    // Parallel processing without user interaction
                    Logger.LogInfo("Using parallel processing");
                    await Task.Run(() =>
                    {
                        var threadCount = context.ThreadCount;
                        if (threadCount <= 0)
                        {
                            threadCount = Math.Max(1, Environment.ProcessorCount - 1);
                        }
                        
                        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = threadCount };
                        var categoryDirs = Directory.GetDirectories(modLibraryPath);
                        
                        Parallel.ForEach(categoryDirs, parallelOptions, categoryDir =>
                        {
                            if (!Directory.Exists(categoryDir)) return;
                            
                            // Process category preview
                            ProcessCategoryPreview(categoryDir, context);
                            
                            // Process all mods in category
                            var modDirs = Directory.GetDirectories(categoryDir);
                            Parallel.ForEach(modDirs, parallelOptions, modDir =>
                            {
                                ProcessModPreviewImages(modDir, context);
                            });
                        });
                    });
                }
                
                Logger.LogInfo("Manual preview optimization completed");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during manual preview optimization", ex);
                throw;
            }
        }

        /// <summary>
        /// Get optimization context from settings for specified trigger
        /// </summary>
        public static OptimizationContext GetOptimizationContext(OptimizationTrigger trigger)
        {
            var modeString = trigger switch
            {
                OptimizationTrigger.Manual => SettingsManager.Current.ImageOptimizerManualMode,
                OptimizationTrigger.DragDropMod => SettingsManager.Current.ImageOptimizerDragDropModMode,
                OptimizationTrigger.DragDropCategory => SettingsManager.Current.ImageOptimizerDragDropCategoryMode,
                OptimizationTrigger.GameBananaDownload => SettingsManager.Current.ImageOptimizerAutoDownloadMode,
                _ => "Full"
            };

            var mode = Enum.TryParse<OptimizationMode>(modeString, out var parsedMode) 
                ? parsedMode 
                : OptimizationMode.Full;

            var cropStrategyString = SettingsManager.Current.ImageCropType ?? "Center";
            var cropStrategy = Enum.TryParse<CropStrategy>(cropStrategyString, out var parsedStrategy)
                ? parsedStrategy
                : CropStrategy.Center;

            // Determine if UI interaction is allowed based on user preferences and crop strategy
            // Manual and Drag&Drop operations should allow UI interaction if inspection is enabled
            // GameBanana downloads in background typically shouldn't allow UI (but user can override if they want)
            bool allowUIInteraction = true; // Default: allow UI interaction for user-initiated operations
            
            // For now, all triggers allow UI interaction if the user has enabled inspection
            // This respects PreviewBeforeCrop setting for all contexts

            return new OptimizationContext
            {
                Mode = mode,
                JpegQuality = SettingsManager.Current.ImageOptimizerJpegQuality,
                ThreadCount = SettingsManager.Current.ImageOptimizerThreadCount,
                CreateBackups = SettingsManager.Current.ImageOptimizerCreateBackups,
                KeepOriginals = SettingsManager.Current.ImageOptimizerKeepOriginals,
                CropStrategy = cropStrategy,
                InspectAndEditEnabled = SettingsManager.Current.PreviewBeforeCrop,
                Trigger = trigger,
                AllowUIInteraction = allowUIInteraction
            };
        }
    }

    /// <summary>
    /// Context containing all optimization settings
    /// </summary>
    public class OptimizationContext
    {
        public OptimizationMode Mode { get; set; }
        public int JpegQuality { get; set; }
        public int ThreadCount { get; set; }
        public bool CreateBackups { get; set; }
        public bool KeepOriginals { get; set; }
        public CropStrategy CropStrategy { get; set; }
        public bool InspectAndEditEnabled { get; set; }
        public OptimizationTrigger Trigger { get; set; }
        public bool AllowUIInteraction { get; set; } = true; // Can show crop inspection UI
    }

    /// <summary>
    /// Optimization trigger context
    /// </summary>
    public enum OptimizationTrigger
    {
        Manual,
        DragDropMod,
        DragDropCategory,
        GameBananaDownload
    }

    /// <summary>
    /// Cropping strategy for image processing
    /// </summary>
    public enum CropStrategy
    {
        Center,
        Smart,
        Entropy,
        Attention,
        ManualOnly
    }
}
