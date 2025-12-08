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
        
        // Progress tracking for manual optimization
        private static readonly object _progressLock = new();
        private static volatile bool _isOptimizing = false;
        private static volatile bool _cancellationRequested = false;
        private static int _totalFiles = 0;
        private static int _processedFiles = 0;
        private static double _progressValue = 0;
        
        /// <summary>
        /// Event raised when optimization progress changes
        /// </summary>
        public static event Action? OptimizationProgressChanged;
        
        /// <summary>
        /// Whether manual optimization is currently running
        /// </summary>
        public static bool IsOptimizing => _isOptimizing;
        
        /// <summary>
        /// Request cancellation of current optimization (will finish current tasks)
        /// </summary>
        public static void RequestCancellation()
        {
            _cancellationRequested = true;
        }
        
        /// <summary>
        /// Current progress value (0.0 to 1.0)
        /// </summary>
        public static double ProgressValue
        {
            get { lock (_progressLock) { return _progressValue; } }
        }
        
        /// <summary>
        /// Total files to process
        /// </summary>
        public static int TotalFiles
        {
            get { lock (_progressLock) { return _totalFiles; } }
        }
        
        /// <summary>
        /// Files processed so far
        /// </summary>
        public static int ProcessedFiles
        {
            get { lock (_progressLock) { return _processedFiles; } }
        }
        
        private static void NotifyProgressChanged()
        {
            OptimizationProgressChanged?.Invoke();
        }
        
        private static void IncrementProcessed()
        {
            lock (_progressLock)
            {
                _processedFiles++;
                _progressValue = _totalFiles > 0 ? (double)_processedFiles / _totalFiles : 0;
            }
            NotifyProgressChanged();
        }
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
            
            // Check if already optimized and skip if reoptimize is disabled
            if (!context.Reoptimize && OptimizationHelper.IsCategoryAlreadyOptimized(categoryDir))
            {
                Logger.LogInfo($"Skipping already optimized category: {categoryDir}");
                return;
            }

            // Handle different optimization modes
            switch (context.Mode)
            {
                case OptimizationMode.Full:
                    await ProcessCategoryPreviewFullAsync(categoryDir, context);
                    break;
                    
                case OptimizationMode.Lite:
                    // Lite mode not applicable for categories - skip
                    Logger.LogInfo($"Skipping category (Lite mode not applicable): {categoryDir}");
                    break;
                    
                case OptimizationMode.Rename:
                    // Rename mode not applicable for categories - skip
                    Logger.LogInfo($"Skipping category (Rename mode not applicable): {categoryDir}");
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
                
                // If reoptimizing, prepare by renaming source to _original and deleting generated files
                if (context.Reoptimize)
                {
                    OptimizationHelper.PrepareForReoptimization(categoryDir, isCategory: true);
                }
                
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
                
                // Separate original files from regular files
                var originalFiles = allPreviewFiles.Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("_original", StringComparison.OrdinalIgnoreCase)).ToArray();
                var regularFiles = allPreviewFiles.Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith("_original", StringComparison.OrdinalIgnoreCase)).ToArray();
                
                // Use original file if it exists, otherwise use regular file
                var sourceFiles = originalFiles.Length > 0 ? originalFiles : regularFiles;
                
                if (sourceFiles.Length == 0)
                {
                    Logger.LogInfo($"No preview files found in category: {categoryDir}");
                    return;
                }
                
                // If using regular file and KeepOriginals is enabled, create _original copy first
                bool needsOriginalCreation = (originalFiles.Length == 0 && context.KeepOriginals && regularFiles.Length > 0);
                if (needsOriginalCreation)
                {
                    Logger.LogInfo("Creating original copy before optimization");
                    var file = regularFiles[0];
                    try
                    {
                        var directory = Path.GetDirectoryName(file);
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                        var extension = Path.GetExtension(file);
                        var originalPath = Path.Combine(directory!, $"{fileNameWithoutExt}_original{extension}");
                        
                        if (!File.Exists(originalPath))
                        {
                            File.Copy(file, originalPath);
                            Logger.LogInfo($"Created original: {Path.GetFileName(originalPath)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to create original copy: {file}", ex);
                    }
                }
                
                var previewPath = sourceFiles[0];
                var catprevPath = Path.Combine(categoryDir, "catprev.jpg");
                var catminiPath = Path.Combine(categoryDir, "catmini.jpg");
                
                using (var img = Image.FromFile(previewPath))
                {
                    // Get crop rectangle with optional inspection for catprev
                    var catprevCropRect = await GetCropRectangleWithInspectionAsync(
                        img, 600, 600, context, "catprev.jpg");
                    
                    if (catprevCropRect == null)
                    {
                        Logger.LogInfo($"Skipped catprev.jpg generation");
                        return; // User skipped
                    }
                    
                    // Generate catprev.jpg (600x600)
                    using (var catprev = new Bitmap(600, 600))
                    using (var g = Graphics.FromImage(catprev))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        
                        var destRect = new Rectangle(0, 0, 600, 600);
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
                    
                    // Generate catmini.jpg (600x722)
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
                

                
                // Handle original files based on KeepOriginals setting
                if (context.KeepOriginals)
                {
                    // Rename original files with _original suffix
                    foreach (var file in allPreviewFiles)
                    {
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                        // Skip if already _original or is output file
                        if (fileNameWithoutExt.EndsWith("_original", StringComparison.OrdinalIgnoreCase) ||
                            file.Equals(catprevPath, StringComparison.OrdinalIgnoreCase) ||
                            file.Equals(catminiPath, StringComparison.OrdinalIgnoreCase))
                            continue;
                            
                        try
                        {
                            var directory = Path.GetDirectoryName(file);
                            var extension = Path.GetExtension(file);
                            var originalPath = Path.Combine(directory!, $"{fileNameWithoutExt}_original{extension}");
                            
                            // If _original already exists, delete it first
                            if (File.Exists(originalPath))
                                File.Delete(originalPath);
                            
                            File.Move(file, originalPath);
                            Logger.LogInfo($"Kept original: {Path.GetFileName(file)} -> {Path.GetFileName(originalPath)}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to keep original file: {file}", ex);
                        }
                    }
                }
                else
                {
                    // Delete original files (but keep _original files as they are source)
                    foreach (var file in allPreviewFiles)
                    {
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                        // Skip _original files and output files
                        if (fileNameWithoutExt.EndsWith("_original", StringComparison.OrdinalIgnoreCase) ||
                            file.Equals(catprevPath, StringComparison.OrdinalIgnoreCase) ||
                            file.Equals(catminiPath, StringComparison.OrdinalIgnoreCase))
                            continue;
                            
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
                
                // Find preview files (prefer _original files if they exist)
                var allFiles = Directory.GetFiles(categoryDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return (fileName.StartsWith("catprev") || fileName.StartsWith("preview")) &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .ToList();
                
                // Separate original files from regular files
                var originalFiles = allFiles.Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("_original", StringComparison.OrdinalIgnoreCase)).ToList();
                var regularFiles = allFiles.Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith("_original", StringComparison.OrdinalIgnoreCase)).ToList();
                
                // Use original file if it exists, otherwise use regular file
                var previewFiles = originalFiles.Count > 0 ? originalFiles : regularFiles;
                
                if (previewFiles.Count == 0)
                {
                    Logger.LogInfo($"No preview files found in category: {categoryDir}");
                    return;
                }
                
                // If using regular file and KeepOriginals is enabled, create _original copy first
                bool needsOriginalCreation = (originalFiles.Count == 0 && context.KeepOriginals && regularFiles.Count > 0);
                if (needsOriginalCreation)
                {
                    Logger.LogInfo("Creating original copy before optimization");
                    var file = regularFiles[0];
                    try
                    {
                        var directory = Path.GetDirectoryName(file);
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                        var extension = Path.GetExtension(file);
                        var originalPath = Path.Combine(directory!, $"{fileNameWithoutExt}_original{extension}");
                        
                        if (!File.Exists(originalPath))
                        {
                            File.Copy(file, originalPath);
                            Logger.LogInfo($"Created original: {Path.GetFileName(originalPath)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to create original copy: {file}", ex);
                    }
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
                
                // Handle original file based on KeepOriginals setting
                var sourceFileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFile);
                // Skip if already _original or is output file
                if (!sourceFileNameWithoutExt.EndsWith("_original", StringComparison.OrdinalIgnoreCase) &&
                    !sourceFile.Equals(catprevPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (context.KeepOriginals)
                    {
                        // Rename original file with _original suffix
                        try
                        {
                            var directory = Path.GetDirectoryName(sourceFile);
                            var extension = Path.GetExtension(sourceFile);
                            var originalPath = Path.Combine(directory!, $"{sourceFileNameWithoutExt}_original{extension}");
                            
                            // If _original already exists, delete it first
                            if (File.Exists(originalPath))
                                File.Delete(originalPath);
                            
                            File.Move(sourceFile, originalPath);
                            Logger.LogInfo($"Kept original: {Path.GetFileName(sourceFile)} -> {Path.GetFileName(originalPath)}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to keep original file: {sourceFile}", ex);
                        }
                    }
                    else
                    {
                        // Delete original file
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
            
            // Check if already optimized and skip if reoptimize is disabled
            if (!context.Reoptimize && OptimizationHelper.IsModAlreadyOptimized(modDir))
            {
                Logger.LogInfo($"Skipping already optimized mod: {modDir}");
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
                
                // If reoptimizing, prepare by renaming preview files to _original
                if (context.Reoptimize)
                {
                    OptimizationHelper.PrepareForReoptimization(modDir, isCategory: false);
                }
                
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
                
                // Find all preview image files (prefer _original files if they exist)
                var allFiles = Directory.GetFiles(modDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("preview") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .ToList();
                
                // Separate original files from regular files
                var originalFiles = allFiles.Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("_original", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f).ToList();
                var regularFiles = allFiles.Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith("_original", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f).ToList();
                
                // Use original files if they exist, otherwise use regular files
                var previewFiles = originalFiles.Count > 0 ? originalFiles : regularFiles;
                
                if (previewFiles.Count == 0)
                {
                    Logger.LogInfo($"No preview files found in: {modDir}");
                    return;
                }
                
                // If using regular files and KeepOriginals is enabled, create _original copies first
                bool needsOriginalCreation = (originalFiles.Count == 0 && context.KeepOriginals && regularFiles.Count > 0);
                if (needsOriginalCreation)
                {
                    Logger.LogInfo("Creating original copies before optimization");
                    foreach (var file in regularFiles)
                    {
                        try
                        {
                            var directory = Path.GetDirectoryName(file);
                            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                            var extension = Path.GetExtension(file);
                            var originalPath = Path.Combine(directory!, $"{fileNameWithoutExt}_original{extension}");
                            
                            if (!File.Exists(originalPath))
                            {
                                File.Copy(file, originalPath);
                                Logger.LogInfo($"Created original: {Path.GetFileName(originalPath)}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to create original copy: {file}", ex);
                        }
                    }
                }
                
                // Process each preview file
                var processedFiles = new List<string>();
                for (int i = 0; i < previewFiles.Count && i < AppConstants.MAX_PREVIEW_IMAGES; i++)
                {
                    var sourceFile = previewFiles[i];
                    string targetFileName = i == 0 ? "preview.jpg" : $"preview-{i:D2}.jpg";
                    var targetPath = Path.Combine(modDir, targetFileName);
                    
                    bool savedSuccessfully = false;
                    try
                    {
                        using (var img = Image.FromFile(sourceFile))
                        {
                            // For Full mode, we optimize the image to 1000x1000 (crop to square and resize)
                            const int targetSize = 1000;
                            
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
                                
                                // Crop to square and resize to 1000x1000
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
                                    savedSuccessfully = true;
                                    Logger.LogInfo($"Optimized: {Path.GetFileName(sourceFile)} -> {targetFileName}");
                                }
                            }
                        }
                        
                        if (savedSuccessfully)
                        {
                            processedFiles.Add(targetPath);
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
                
                // Handle original files based on KeepOriginals setting
                if (context.KeepOriginals)
                {
                    // Rename original files with _original suffix
                    foreach (var file in previewFiles)
                    {
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                        // Skip if already _original or is processed output file
                        if (fileNameWithoutExt.EndsWith("_original", StringComparison.OrdinalIgnoreCase) ||
                            processedFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                            continue;
                            
                        try
                        {
                            var directory = Path.GetDirectoryName(file);
                            var extension = Path.GetExtension(file);
                            var originalPath = Path.Combine(directory!, $"{fileNameWithoutExt}_original{extension}");
                            
                            // If _original already exists, delete it first
                            if (File.Exists(originalPath))
                                File.Delete(originalPath);
                            
                            File.Move(file, originalPath);
                            Logger.LogInfo($"Kept original: {Path.GetFileName(file)} -> {Path.GetFileName(originalPath)}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to keep original file: {file}", ex);
                        }
                    }
                }
                else
                {
                    // Delete original files (but keep _original files as they are source)
                    foreach (var file in previewFiles)
                    {
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                        // Skip _original files and processed output files
                        if (fileNameWithoutExt.EndsWith("_original", StringComparison.OrdinalIgnoreCase) ||
                            processedFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                            continue;
                            
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
                
                // Find all preview image files (prefer _original files if they exist)
                var allFiles = Directory.GetFiles(modDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("preview") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .ToList();
                
                // Separate original files from regular files
                var originalFiles = allFiles.Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("_original", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f).ToList();
                var regularFiles = allFiles.Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith("_original", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f).ToList();
                
                // Use original files if they exist, otherwise use regular files
                var previewFiles = originalFiles.Count > 0 ? originalFiles : regularFiles;
                
                if (previewFiles.Count == 0)
                {
                    Logger.LogInfo($"No preview files found in: {modDir}");
                    return;
                }
                
                // If using regular files and KeepOriginals is enabled, create _original copies first
                bool needsOriginalCreation = (originalFiles.Count == 0 && context.KeepOriginals && regularFiles.Count > 0);
                if (needsOriginalCreation)
                {
                    Logger.LogInfo("Creating original copies before optimization");
                    foreach (var file in regularFiles)
                    {
                        try
                        {
                            var directory = Path.GetDirectoryName(file);
                            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                            var extension = Path.GetExtension(file);
                            var originalPath = Path.Combine(directory!, $"{fileNameWithoutExt}_original{extension}");
                            
                            if (!File.Exists(originalPath))
                            {
                                File.Copy(file, originalPath);
                                Logger.LogInfo($"Created original: {Path.GetFileName(originalPath)}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to create original copy: {file}", ex);
                        }
                    }
                }
                
                // Process each preview file - convert to JPEG without resizing
                var processedFiles = new List<string>();
                for (int i = 0; i < previewFiles.Count && i < AppConstants.MAX_PREVIEW_IMAGES; i++)
                {
                    var sourceFile = previewFiles[i];
                    string targetFileName = i == 0 ? "preview.jpg" : $"preview-{i:D2}.jpg";
                    var targetPath = Path.Combine(modDir, targetFileName);
                    
                    bool savedSuccessfully = false;
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
                                savedSuccessfully = true;
                                Logger.LogInfo($"Converted (Lite): {Path.GetFileName(sourceFile)} -> {targetFileName}");
                            }
                        }
                        
                        if (savedSuccessfully)
                        {
                            processedFiles.Add(targetPath);
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
                
                // Handle original files based on KeepOriginals setting
                if (context.KeepOriginals)
                {
                    // Rename original files with _original suffix
                    foreach (var file in previewFiles)
                    {
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                        // Skip if already _original or is processed output file
                        if (fileNameWithoutExt.EndsWith("_original", StringComparison.OrdinalIgnoreCase) ||
                            processedFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                            continue;
                            
                        try
                        {
                            var directory = Path.GetDirectoryName(file);
                            var extension = Path.GetExtension(file);
                            var originalPath = Path.Combine(directory!, $"{fileNameWithoutExt}_original{extension}");
                            
                            // If _original already exists, delete it first
                            if (File.Exists(originalPath))
                                File.Delete(originalPath);
                            
                            File.Move(file, originalPath);
                            Logger.LogInfo($"Kept original: {Path.GetFileName(file)} -> {Path.GetFileName(originalPath)}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to keep original file: {file}", ex);
                        }
                    }
                }
                else
                {
                    // Delete original files (but keep _original files as they are source)
                    foreach (var file in previewFiles)
                    {
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                        // Skip _original files and processed output files
                        if (fileNameWithoutExt.EndsWith("_original", StringComparison.OrdinalIgnoreCase) ||
                            processedFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                            continue;
                            
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
                        
                        // Save as JPEG with quality setting (overwrites existing)
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

                // Reset progress tracking and cancellation flag
                lock (_progressLock)
                {
                    _isOptimizing = true;
                    _cancellationRequested = false;
                    _totalFiles = 0;
                    _processedFiles = 0;
                    _progressValue = 0;
                }
                NotifyProgressChanged();

                var context = GetOptimizationContext(OptimizationTrigger.Manual);
                
                // Count total files first
                var categoryDirs = Directory.GetDirectories(modLibraryPath);
                int totalCount = 0;
                foreach (var categoryDir in categoryDirs)
                {
                    if (!Directory.Exists(categoryDir)) continue;
                    totalCount++; // category preview
                    var modDirs = Directory.GetDirectories(categoryDir);
                    totalCount += modDirs.Length; // each mod
                }
                
                lock (_progressLock)
                {
                    _totalFiles = totalCount;
                }
                NotifyProgressChanged();
                
                // Check if we need sequential processing for crop inspection
                bool needsSequentialProcessing = context.InspectAndEditEnabled || context.CropStrategy == CropStrategy.ManualOnly;
                bool wasCancelled = false;
                
                if (needsSequentialProcessing)
                {
                    // Sequential processing with potential user interaction
                    Logger.LogInfo("Using sequential processing (crop inspection enabled)");
                    
                    foreach (var categoryDir in categoryDirs)
                    {
                        if (_cancellationRequested) { wasCancelled = true; break; }
                        if (!Directory.Exists(categoryDir)) continue;
                        
                        // Process category preview
                        await ProcessCategoryPreviewAsync(categoryDir, context);
                        IncrementProcessed();
                        
                        // Process all mods in category
                        var modDirs = Directory.GetDirectories(categoryDir);
                        foreach (var modDir in modDirs)
                        {
                            if (_cancellationRequested) { wasCancelled = true; break; }
                            await ProcessModPreviewImagesAsync(modDir, context);
                            IncrementProcessed();
                        }
                        
                        if (wasCancelled) break;
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
                        
                        Parallel.ForEach(categoryDirs, parallelOptions, (categoryDir, loopState) =>
                        {
                            if (_cancellationRequested) { loopState.Break(); return; }
                            if (!Directory.Exists(categoryDir)) return;
                            
                            // Process category preview
                            ProcessCategoryPreview(categoryDir, context);
                            IncrementProcessed();
                            
                            if (_cancellationRequested) { loopState.Break(); return; }
                            
                            // Process all mods in category
                            var modDirs = Directory.GetDirectories(categoryDir);
                            Parallel.ForEach(modDirs, parallelOptions, (modDir, innerLoopState) =>
                            {
                                if (_cancellationRequested) { innerLoopState.Break(); return; }
                                ProcessModPreviewImages(modDir, context);
                                IncrementProcessed();
                            });
                        });
                        
                        wasCancelled = _cancellationRequested;
                    });
                }
                
                if (wasCancelled)
                {
                    Logger.LogInfo("Manual preview optimization cancelled by user");
                    throw new OperationCanceledException("Optimization cancelled by user");
                }
                
                Logger.LogInfo("Manual preview optimization completed");
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during manual preview optimization", ex);
                throw;
            }
            finally
            {
                lock (_progressLock)
                {
                    _isOptimizing = false;
                    _cancellationRequested = false;
                }
                NotifyProgressChanged();
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
                AllowUIInteraction = allowUIInteraction,
                Reoptimize = SettingsManager.Current.ImageOptimizerReoptimize
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
        public bool Reoptimize { get; set; } = false; // Re-optimize already optimized files
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
    
    /// <summary>
    /// Helper methods for checking if files are already optimized
    /// </summary>
    public static class OptimizationHelper
    {
        /// <summary>
        /// Check if a mod directory already has optimized files
        /// </summary>
        public static bool IsModAlreadyOptimized(string modDir)
        {
            // Check if minitile.jpg exists
            var minitilePath = Path.Combine(modDir, "minitile.jpg");
            if (!File.Exists(minitilePath))
                return false;
            
            // Check if preview files have correct names (preview.jpg, preview-01.jpg, preview-02.jpg, etc.)
            var previewFiles = Directory.GetFiles(modDir)
                .Where(f =>
                {
                    var fileName = Path.GetFileName(f).ToLower();
                    return fileName.StartsWith("preview") &&
                           (f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                })
                .ToList();
            
            if (previewFiles.Count == 0)
                return false;
            
            // Check if all preview files have correct naming pattern
            foreach (var file in previewFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file).ToLower();
                if (fileName == "preview")
                    continue;
                if (fileName.StartsWith("preview-") && fileName.Length >= 10) // preview-XX
                    continue;
                    
                // Found a file with incorrect name
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if a category directory already has optimized files
        /// </summary>
        public static bool IsCategoryAlreadyOptimized(string categoryDir)
        {
            // Check if catprev.jpg exists (optimized format)
            var catprevPath = Path.Combine(categoryDir, "catprev.jpg");
            if (!File.Exists(catprevPath))
                return false;
            
            // Check if catmini.jpg exists (required for Full mode optimization)
            var catminiPath = Path.Combine(categoryDir, "catmini.jpg");
            if (!File.Exists(catminiPath))
                return false;
            
            // Both catprev.jpg and catmini.jpg exist - category is fully optimized
            return true;
        }

        /// <summary>
        /// Prepare directory for reoptimization by deleting generated files (keeps _original files as source)
        /// </summary>
        public static void PrepareForReoptimization(string directory, bool isCategory)
        {
            try
            {
                if (isCategory)
                {
                    // For categories: rename catprev.jpg to catprev_original.jpg
                    // catmini.jpg will be overwritten by optimizer
                    var catprevPath = Path.Combine(directory, "catprev.jpg");
                    var catprevOriginalPath = Path.Combine(directory, "catprev_original.jpg");
                    
                    if (File.Exists(catprevPath) && !File.Exists(catprevOriginalPath))
                    {
                        File.Move(catprevPath, catprevOriginalPath);
                        Logger.LogInfo($"Renamed catprev.jpg to catprev_original.jpg for reoptimization");
                    }
                }
                else
                {
                    // For mods: rename preview.jpg/preview-XX.jpg to _original
                    // minitile.jpg will be overwritten by optimizer
                    var previewFiles = Directory.GetFiles(directory)
                        .Where(f =>
                        {
                            var fileName = Path.GetFileName(f).ToLower();
                            return fileName.StartsWith("preview") &&
                                   !fileName.Contains("_original") &&
                                   (f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                        })
                        .ToList();
                    
                    foreach (var file in previewFiles)
                    {
                        var dir = Path.GetDirectoryName(file)!;
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
                        var ext = Path.GetExtension(file);
                        var originalPath = Path.Combine(dir, $"{nameWithoutExt}_original{ext}");
                        
                        if (!File.Exists(originalPath))
                        {
                            File.Move(file, originalPath);
                            Logger.LogInfo($"Renamed {Path.GetFileName(file)} to {Path.GetFileName(originalPath)} for reoptimization");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to prepare for reoptimization: {directory}", ex);
            }
        }
    }
}
