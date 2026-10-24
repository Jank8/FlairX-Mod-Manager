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
                case OptimizationMode.CategoryFull:
                    // CategoryFull: full optimization with manual crop inspection (drag&drop)
                    await ProcessCategoryPreviewFullAsync(categoryDir, context);
                    break;
                    
                case OptimizationMode.Standard:
                default:
                    // Standard: optimize quality, generate thumbnails with auto crop
                    await ProcessCategoryPreviewLiteAsync(categoryDir, context);
                    break;
            }
        }

        /// <summary>
        /// Synchronous wrapper for backward compatibility
        /// </summary>
        public static void ProcessCategoryPreview(string categoryDir, OptimizationContext context)
        {
            ProcessCategoryPreviewLiteAsync(categoryDir, context, showProgressDialog: false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Process category preview in CategoryFull mode - complete optimization with cropping and thumbnails
        /// </summary>
        private static async Task ProcessCategoryPreviewFullAsync(string categoryDir, OptimizationContext context)
        {
            // Track which files existed before we started (for cleanup on cancel)
            var catprevPath = Path.Combine(categoryDir, GetCatprevFilename());
            var catminiPath = Path.Combine(categoryDir, GetCatminiFilename());
            bool catprevExistedBefore = File.Exists(catprevPath);
            bool catminiExistedBefore = File.Exists(catminiPath);
            
            try
            {
                Logger.LogInfo($"Processing category preview (CategoryFull) in: {categoryDir}");
                
                // Check if catprev exists but catmini is missing - only generate catmini
                var existingCatprevPath = Path.Combine(categoryDir, GetCatprevFilename());
                var existingCatminiPath = Path.Combine(categoryDir, GetCatminiFilename());
                
                if (File.Exists(existingCatprevPath) && !File.Exists(existingCatminiPath) && !context.Reoptimize)
                {
                    Logger.LogInfo($"{GetCatprevFilename()} exists but {GetCatminiFilename()} missing - generating catmini only");
                    await GenerateCatminiFromCatprevAsync(categoryDir, context);
                    return;
                }
                
                // If reoptimizing, prepare by renaming source to _original and deleting generated files
                if (context.Reoptimize)
                {
                    OptimizationHelper.PrepareForReoptimization(categoryDir, isCategory: true);
                    // After reoptimization prep, files are deleted so they didn't "exist before" for cleanup purposes
                    catprevExistedBefore = false;
                    catminiExistedBefore = false;
                }
                
                // Create backup if enabled
                if (context.CreateBackups)
                {
                    var filesToBackup = Directory.GetFiles(categoryDir)
                        .Where(f =>
                        {
                            var fileName = Path.GetFileName(f).ToLower();
                            return (fileName.StartsWith("preview") || fileName.StartsWith("catprev") || fileName.StartsWith("catmini")) &&
                                   IsImageFile(f);
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
                               IsImageFile(f);
                    })
                    .ToArray();
                
                var otherPreviewFiles = Directory.GetFiles(categoryDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return (fileName.StartsWith("catpreview") || fileName.StartsWith("preview")) &&
                               !fileName.StartsWith("catprev") &&
                               IsImageFile(f);
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
                // catprevPath and catminiPath already declared at method start
                
                // Check if source and target are the same file - need to use temp file
                bool isSameFileAsCatprev = previewPath.Equals(catprevPath, StringComparison.OrdinalIgnoreCase);
                string actualCatprevPath = isSameFileAsCatprev 
                    ? Path.Combine(categoryDir, $"_temp_catprev_{Guid.NewGuid()}{SettingsManager.GetImageExtension()}")
                    : catprevPath;
                
                var catprevWidePath = Path.Combine(categoryDir, GetCatprevWideFilename());
                var catminiWidePath = Path.Combine(categoryDir, GetCatminiWideFilename());
                
                using (var img = Image.Load<Rgba32>(previewPath))
                {
                    // ========== CATPREV (722x722 - квадратний для списку) ==========
                    var catprevCropRect = await GetCropRectangleWithInspectionAsync(
                        img, 722, 722, context, "catprev (722x722 for list)");
                    
                    if (catprevCropRect == null)
                    {
                        Logger.LogInfo($"Deleted catprev generation");
                        return; // User chose to delete - abort all
                    }
                    
                    if (catprevCropRect.Value.X == -1 && catprevCropRect.Value.Y == -1)
                    {
                        Logger.LogInfo($"Skipping catprev generation (user chose skip)");
                        return;
                    }
                    
                    // Generate catprev (722x722)
                    using (var catprev = img.Clone(ctx => ctx
                        .Crop(catprevCropRect.Value)
                        .Resize(new ResizeOptions
                        {
                            Size = new Size(722, 722),
                            Mode = ResizeMode.Stretch,
                            Sampler = KnownResamplers.Bicubic
                        })))
                    {
                        SaveImage(catprev, actualCatprevPath, context.JpegQuality);
                        Logger.LogInfo($"Generated {GetCatprevFilename()}");
                    }
                    
                    // ========== CATPREV-WIDE (1280x720) ==========
                    var catprevWideCropRect = await GetCropRectangleWithInspectionAsync(
                        img, 1280, 720, context, "catprev-wide (1280x720)", isProtected: false, isThumbnail: false);
                    
                    if (catprevWideCropRect == null)
                    {
                        Logger.LogInfo($"Deleted catprev-wide generation");
                        return;
                    }
                    
                    if (catprevWideCropRect.Value.X == -1 && catprevWideCropRect.Value.Y == -1)
                    {
                        Logger.LogInfo($"Skipping catprev-wide generation");
                        return;
                    }
                    
                    // Generate catprev-wide (1280x720)
                    using (var catprevWide = img.Clone(ctx => ctx
                        .Crop(catprevWideCropRect.Value)
                        .Resize(new ResizeOptions
                        {
                            Size = new Size(1280, 720),
                            Mode = ResizeMode.Stretch,
                            Sampler = KnownResamplers.Bicubic
                        })))
                    {
                        SaveImage(catprevWide, catprevWidePath, context.JpegQuality);
                        Logger.LogInfo($"Generated {GetCatprevWideFilename()}");
                    }
                    
                    // ========== CATMINI (600x722 - classic thumbnail) ==========
                    var catminiCropRect = await GetCropRectangleWithInspectionAsync(
                        img, 600, 722, context, "catmini (600x722 classic)", isProtected: false, isThumbnail: true);
                    
                    if (catminiCropRect == null)
                    {
                        Logger.LogInfo($"Deleted catmini generation");
                        return;
                    }
                    
                    if (catminiCropRect.Value.X == -1 && catminiCropRect.Value.Y == -1)
                    {
                        Logger.LogInfo($"Skipping catmini generation");
                        return;
                    }
                    
                    // Generate catmini (600x722)
                    using (var catmini = img.Clone(ctx => ctx
                        .Crop(catminiCropRect.Value)
                        .Resize(new ResizeOptions
                        {
                            Size = new Size(600, 722),
                            Mode = ResizeMode.Stretch,
                            Sampler = KnownResamplers.Bicubic
                        })))
                    {
                        SaveImage(catmini, catminiPath, context.JpegQuality);
                        Logger.LogInfo($"Generated {GetCatminiFilename()}");
                    }
                    
                    // ========== CATMINI-WIDE (1280x720 - wide thumbnail) ==========
                    var catminiWideCropRect = await GetCropRectangleWithInspectionAsync(
                        img, 1280, 720, context, "catmini-wide (1280x720)", isProtected: false, isThumbnail: true);
                    
                    if (catminiWideCropRect == null)
                    {
                        Logger.LogInfo($"Deleted catmini-wide generation");
                        return;
                    }
                    
                    if (catminiWideCropRect.Value.X == -1 && catminiWideCropRect.Value.Y == -1)
                    {
                        Logger.LogInfo($"Skipping catmini-wide generation");
                        return;
                    }
                    
                    // Generate catmini-wide (1280x720)
                    using (var catminiWide = img.Clone(ctx => ctx
                        .Crop(catminiWideCropRect.Value)
                        .Resize(new ResizeOptions
                        {
                            Size = new Size(1280, 720),
                            Mode = ResizeMode.Stretch,
                            Sampler = KnownResamplers.Bicubic
                        })))
                    {
                        SaveImage(catminiWide, catminiWidePath, context.JpegQuality);
                        Logger.LogInfo($"Generated {GetCatminiWideFilename()}");
                    }
                }
                
                // If we used a temp file for catprev, move it to the actual target after closing the source
                if (isSameFileAsCatprev && File.Exists(actualCatprevPath))
                {
                    try
                    {
                        if (File.Exists(catprevPath))
                            File.Delete(catprevPath);
                        File.Move(actualCatprevPath, catprevPath);
                        Logger.LogInfo($"Moved temp catprev to final location");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to move temp catprev file", ex);
                        // Clean up temp file
                        try { if (File.Exists(actualCatprevPath)) File.Delete(actualCatprevPath); } catch { }
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
                            file.Equals(catprevWidePath, StringComparison.OrdinalIgnoreCase) ||
                            file.Equals(catminiPath, StringComparison.OrdinalIgnoreCase) ||
                            file.Equals(catminiWidePath, StringComparison.OrdinalIgnoreCase))
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
                            file.Equals(catprevWidePath, StringComparison.OrdinalIgnoreCase) ||
                            file.Equals(catminiPath, StringComparison.OrdinalIgnoreCase) ||
                            file.Equals(catminiWidePath, StringComparison.OrdinalIgnoreCase))
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
                
                Logger.LogInfo($"Category preview processing (CategoryFull) completed for: {categoryDir}");
            }
            catch (OperationCanceledException)
            {
                // Clean up only NEW files on cancellation (don't delete files that existed before)
                try
                {
                    if (!catprevExistedBefore && File.Exists(catprevPath))
                    {
                        File.Delete(catprevPath);
                        Logger.LogInfo($"Cleaned up new {GetCatprevFilename()}");
                    }
                    if (!catminiExistedBefore && File.Exists(catminiPath))
                    {
                        File.Delete(catminiPath);
                        Logger.LogInfo($"Cleaned up new {GetCatminiFilename()}");
                    }
                    
                    // Restore _original files back to their original names
                    RestoreOriginalFiles(categoryDir);
                }
                catch (Exception cleanupEx)
                {
                    Logger.LogError($"Failed to clean up category preview files", cleanupEx);
                }
                
                throw; // Re-throw cancellation to stop optimization
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process category preview (CategoryFull) in {categoryDir}", ex);
            }
        }

        /// <summary>
        /// Process gbicon (GameBanana icon) - convert between PNG and WebP only (needs alpha channel)
        /// </summary>
        private static void ProcessGbIcon(string categoryDir, OptimizationContext context)
        {
            try
            {
                var currentFormat = SettingsManager.Current.ImageFormat ?? "WebP";
                var targetExtension = currentFormat.Equals("WebP", StringComparison.OrdinalIgnoreCase) ? ".webp" : ".png";
                var sourceExtension = targetExtension == ".webp" ? ".png" : ".webp";
                
                var targetPath = Path.Combine(categoryDir, $"gbicon{targetExtension}");
                var sourcePath = Path.Combine(categoryDir, $"gbicon{sourceExtension}");
                
                // If target already exists, nothing to do
                if (File.Exists(targetPath))
                {
                    Logger.LogInfo($"gbicon{targetExtension} already exists, skipping conversion");
                    return;
                }
                
                // If source doesn't exist, nothing to convert
                if (!File.Exists(sourcePath))
                {
                    Logger.LogInfo($"No gbicon found to convert in: {categoryDir}");
                    return;
                }
                
                Logger.LogInfo($"Converting gbicon{sourceExtension} to gbicon{targetExtension}");
                
                // Load and save in target format
                using (var img = Image.Load<Rgba32>(sourcePath))
                {
                    if (targetExtension == ".webp")
                    {
                        // Save as WebP with alpha channel - use lossless mode (quality 101)
                        SaveImage(img, targetPath, 101); // 101 = lossless
                    }
                    else
                    {
                        // Save as PNG (always lossless with alpha)
                        img.SaveAsPng(targetPath);
                    }
                    
                    Logger.LogInfo($"Converted gbicon{sourceExtension} -> gbicon{targetExtension}");
                }
                
                // Handle original file based on KeepOriginals setting
                if (context.KeepOriginals)
                {
                    // Rename to _original
                    try
                    {
                        var originalPath = Path.Combine(categoryDir, $"gbicon_original{sourceExtension}");
                        if (File.Exists(originalPath))
                            File.Delete(originalPath);
                        File.Move(sourcePath, originalPath);
                        Logger.LogInfo($"Kept original: gbicon{sourceExtension} -> gbicon_original{sourceExtension}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to keep original gbicon", ex);
                    }
                }
                else
                {
                    // Delete original
                    try
                    {
                        File.Delete(sourcePath);
                        Logger.LogInfo($"Deleted original: gbicon{sourceExtension}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to delete original gbicon", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process gbicon in {categoryDir}", ex);
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
                               IsImageFile(f);
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
        /// Generate catmini from existing catprev (CategoryFull mode with crop inspection)
        /// </summary>
        private static async Task GenerateCatminiFromCatprevAsync(string categoryDir, OptimizationContext context)
        {
            try
            {
                var catprevPath = Path.Combine(categoryDir, GetCatprevFilename());
                var catminiPath = Path.Combine(categoryDir, GetCatminiFilename());
                
                using (var img = Image.Load<Rgba32>(catprevPath))
                {
                    // Get crop rectangle with optional inspection for catmini
                    var catminiCropRect = await GetCropRectangleWithInspectionAsync(
                        img, 600, 722, context, GetCatminiFilename(), isProtected: false, isThumbnail: true);
                    
                    if (catminiCropRect == null)
                    {
                        Logger.LogInfo($"Skipped {GetCatminiFilename()} generation (user chose delete)");
                        return;
                    }
                    
                    // Skip not allowed for category - crop is required
                    if (catminiCropRect.Value.X == -1 && catminiCropRect.Value.Y == -1)
                    {
                        Logger.LogInfo($"Skipped {GetCatminiFilename()} generation (user chose skip)");
                        return;
                    }
                    
                    // Generate catmini.jpg (600x722)
                    using (var catmini = img.Clone(ctx => ctx
                        .Crop(catminiCropRect.Value)
                        .Resize(new ResizeOptions
                        {
                            Size = new Size(600, 722),
                            Mode = ResizeMode.Stretch,
                            Sampler = KnownResamplers.Bicubic
                        })))
                    {
                        SaveImage(catmini, catminiPath, context.JpegQuality);
                        Logger.LogInfo($"Generated {GetCatminiFilename()} from existing {GetCatprevFilename()}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to generate catmini from catprev in {categoryDir}", ex);
            }
        }

        /// <summary>
        /// Generate catmini from existing catprev (Standard mode with auto crop)
        /// </summary>
        private static void GenerateCatminiFromCatprevLite(string categoryDir, OptimizationContext context)
        {
            try
            {
                var catprevPath = Path.Combine(categoryDir, GetCatprevFilename());
                var catminiPath = Path.Combine(categoryDir, GetCatminiFilename());
                var catminiWidePath = Path.Combine(categoryDir, GetCatminiWideFilename());
                
                using (var img = Image.Load<Rgba32>(catprevPath))
                {
                    // Generate catmini (600x722) with auto crop
                    if (!File.Exists(catminiPath))
                    {
                        var catminiCropRect = ImageCropService.CalculateCropRectangle(img, 600, 722, CropType.Center);
                        using (var catmini = img.Clone(ctx => ctx
                            .Crop(catminiCropRect)
                            .Resize(new ResizeOptions
                            {
                                Size = new Size(600, 722),
                                Mode = ResizeMode.Stretch,
                                Sampler = KnownResamplers.Bicubic
                            })))
                        {
                            SaveImage(catmini, catminiPath, context.JpegQuality);
                            Logger.LogInfo($"Generated {GetCatminiFilename()} from existing {GetCatprevFilename()} (Standard)");
                        }
                    }
                    
                    // Generate catmini-wide (1280x720) with auto crop
                    if (!File.Exists(catminiWidePath))
                    {
                        var catminiWideCropRect = ImageCropService.CalculateCropRectangle(img, 1280, 720, CropType.Center);
                        using (var catminiWide = img.Clone(ctx => ctx
                            .Crop(catminiWideCropRect)
                            .Resize(new ResizeOptions
                            {
                                Size = new Size(1280, 720),
                                Mode = ResizeMode.Stretch,
                                Sampler = KnownResamplers.Bicubic
                            })))
                        {
                            SaveImage(catminiWide, catminiWidePath, context.JpegQuality);
                            Logger.LogInfo($"Generated {GetCatminiWideFilename()} from existing {GetCatprevFilename()} (Standard)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to generate catmini from catprev (Lite) in {categoryDir}", ex);
            }
        }
    }
}



