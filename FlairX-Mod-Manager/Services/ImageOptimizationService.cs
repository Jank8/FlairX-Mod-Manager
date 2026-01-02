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
    public delegate Task<CropInspectionResult?> CropInspectionHandler(Image sourceImage, Rectangle suggestedCrop, int targetWidth, int targetHeight, string imageType, bool isProtected);

    /// <summary>
    /// Action to take with the image
    /// </summary>
    public enum CropInspectionAction
    {
        Confirm,    // Proceed with cropping and optimization
        Skip,       // Skip optimization, only rename file
        Delete,     // Delete/remove file completely
        Cancel      // Cancel entire optimization process
    }

    /// <summary>
    /// Result from crop inspection
    /// </summary>
    public class CropInspectionResult
    {
        public CropInspectionAction Action { get; set; }
        public Rectangle CropRectangle { get; set; }
        
        // Backward compatibility property
        [Obsolete("Use Action property instead")]
        public bool Confirmed => Action == CropInspectionAction.Confirm;
    }

    /// <summary>
    /// Delegate for minitile source selection callback
    /// </summary>
    public delegate Task<MinitileSourceResult?> MinitileSourceSelectionHandler(List<string> availableFiles, string modDirectory);

    /// <summary>
    /// Result from minitile source selection
    /// </summary>
    public class MinitileSourceResult
    {
        public string? SelectedFilePath { get; set; }
        public bool Skipped { get; set; }  // Skip this mod, continue with others
        public bool Stopped { get; set; }  // Stop entire optimization process
        
        // Backward compatibility
        [Obsolete("Use Skipped or Stopped instead")]
        public bool Cancelled { get => Skipped || Stopped; set => Skipped = value; }
    }

    /// <summary>
    /// Delegate for batch crop inspection callback
    /// </summary>
    public delegate Task<List<BatchCropInspectionResult>?> BatchCropInspectionHandler(List<BatchCropItem> items);

    /// <summary>
    /// Item for batch crop inspection
    /// </summary>
    public class BatchCropItem
    {
        public string FilePath { get; set; } = "";
        public string ImageType { get; set; } = "";
        public Image? SourceImage { get; set; }
        public Rectangle InitialCropRect { get; set; }
        public int TargetWidth { get; set; }
        public int TargetHeight { get; set; }
        public bool IsProtected { get; set; }
    }

    /// <summary>
    /// Result from batch crop inspection
    /// </summary>
    public class BatchCropInspectionResult
    {
        public string FilePath { get; set; } = "";
        public string ImageType { get; set; } = "";
        public CropInspectionAction Action { get; set; }
        public Rectangle CropRectangle { get; set; }
        public int TargetWidth { get; set; }
        public int TargetHeight { get; set; }
    }

    /// <summary>
    /// Service for image optimization operations across all contexts (Manual, Drag&Drop, Auto)
    /// </summary>
    public static class ImageOptimizationService
    {
        /// <summary>
        /// Event raised when crop inspection is needed (single image)
        /// </summary>
        public static event CropInspectionHandler? CropInspectionRequested;
        
        /// <summary>
        /// Event raised when batch crop inspection is needed (multiple images)
        /// </summary>
        public static event BatchCropInspectionHandler? BatchCropInspectionRequested;
        
        /// <summary>
        /// Event raised when minitile source selection is needed (for mods with multiple preview images)
        /// </summary>
        public static event MinitileSourceSelectionHandler? MinitileSourceSelectionRequested;
        
        // Progress tracking for manual optimization
        private static readonly object _progressLock = new();
        private static volatile bool _isOptimizing = false;
        private static volatile bool _cancellationRequested = false;
        private static int _totalFiles = 0;
        private static int _processedFiles = 0;
        private static double _progressValue = 0;
        private static string _currentProcessingMod = "";
        
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
            
            // Immediately update UI to show cancellation
            lock (_progressLock)
            {
                _isOptimizing = false;
                _currentProcessingMod = "";
            }
            NotifyProgressChanged();
        }
        
        /// <summary>
        /// Reset cancellation flag - call before starting a new optimization process
        /// </summary>
        public static void ResetCancellation()
        {
            _cancellationRequested = false;
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
        
        /// <summary>
        /// Get current processing mod name
        /// </summary>
        public static string CurrentProcessingMod
        {
            get { lock (_progressLock) { return _currentProcessingMod; } }
        }
        
        /// <summary>
        /// Set current processing mod name (thread-safe)
        /// </summary>
        private static void SetCurrentProcessingMod(string modName)
        {
            lock (_progressLock)
            {
                _currentProcessingMod = modName;
            }
        }
        
        /// <summary>
        /// Get display name for mod (removes DISABLED_ prefix and reads from mod.json if available)
        /// </summary>
        private static string GetModDisplayName(string modDir)
        {
            try
            {
                var modFolderName = Path.GetFileName(modDir);
                
                // Remove DISABLED_ prefix from folder name for display
                var displayFolderName = modFolderName.StartsWith("DISABLED_") 
                    ? modFolderName.Substring("DISABLED_".Length) 
                    : modFolderName;
                
                // Try to read name from mod.json
                var modJsonPath = Path.Combine(modDir, "mod.json");
                if (File.Exists(modJsonPath))
                {
                    try
                    {
                        var json = File.ReadAllText(modJsonPath);
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        
                        if (root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString()))
                        {
                            return nameProp.GetString()!;
                        }
                    }
                    catch
                    {
                        // Ignore JSON parsing errors, fall back to folder name
                    }
                }
                
                return displayFolderName;
            }
            catch
            {
                return Path.GetFileName(modDir);
            }
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
                case OptimizationMode.CategoryFull:
                    // CategoryFull: full optimization with manual crop inspection (drag&drop)
                    await ProcessCategoryPreviewFullAsync(categoryDir, context);
                    break;
                    
                case OptimizationMode.Standard:
                default:
                    // Standard: optimize quality, generate thumbnails with auto crop
                    ProcessCategoryPreviewLite(categoryDir, context);
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
                
                // Check if catprev exists but catmini is missing - only generate catmini
                var existingCatprevPath = Path.Combine(categoryDir, "catprev.jpg");
                var existingCatminiPath = Path.Combine(categoryDir, "catmini.jpg");
                
                if (File.Exists(existingCatprevPath) && !File.Exists(existingCatminiPath) && !context.Reoptimize)
                {
                    Logger.LogInfo($"catprev.jpg exists but catmini.jpg missing - generating catmini only");
                    await GenerateCatminiFromCatprevAsync(categoryDir, context);
                    return;
                }
                
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
                
                // Check if source and target are the same file - need to use temp file
                bool isSameFileAsCatprev = previewPath.Equals(catprevPath, StringComparison.OrdinalIgnoreCase);
                string actualCatprevPath = isSameFileAsCatprev 
                    ? Path.Combine(categoryDir, $"_temp_catprev_{Guid.NewGuid()}.jpg")
                    : catprevPath;
                
                using (var img = Image.FromFile(previewPath))
                {
                    // Get crop rectangle with optional inspection for catprev
                    var catprevCropRect = await GetCropRectangleWithInspectionAsync(
                        img, 722, 722, context, "catprev.jpg");
                    
                    if (catprevCropRect == null)
                    {
                        Logger.LogInfo($"Deleted catprev.jpg generation");
                        return; // User chose to delete
                    }
                    
                    // Check if user chose to skip optimization (rename only)
                    if (catprevCropRect.Value.X == -1 && catprevCropRect.Value.Y == -1)
                    {
                        Logger.LogInfo($"Skipping catprev.jpg optimization, rename only");
                        // Just rename existing file to catprev.jpg if needed
                        ProcessCategoryPreviewRenameOnly(categoryDir);
                        return;
                    }
                    
                    // Generate catprev.jpg (722x722)
                    using (var catprev = new Bitmap(722, 722))
                    using (var g = Graphics.FromImage(catprev))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        
                        var destRect = new Rectangle(0, 0, 722, 722);
                        g.DrawImage(img, destRect, catprevCropRect.Value, GraphicsUnit.Pixel);
                        
                        var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                            .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                        if (jpegEncoder != null)
                        {
                            var jpegParams = new EncoderParameters(1);
                            jpegParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)context.JpegQuality);
                            catprev.Save(actualCatprevPath, jpegEncoder, jpegParams);
                            Logger.LogInfo($"Generated catprev.jpg");
                        }
                    }
                    
                    // Get crop rectangle with optional inspection for catmini (thumbnail)
                    var catminiCropRect = await GetCropRectangleWithInspectionAsync(
                        img, 600, 722, context, "catmini.jpg", isProtected: false, isThumbnail: true);
                    
                    if (catminiCropRect == null)
                    {
                        Logger.LogInfo($"Deleted catmini.jpg generation");
                        return; // User chose to delete
                    }
                    
                    // Check if user chose to skip optimization (rename only)
                    if (catminiCropRect.Value.X == -1 && catminiCropRect.Value.Y == -1)
                    {
                        Logger.LogInfo($"Skipping catmini.jpg optimization, rename only");
                        // Just rename existing file to catprev.jpg if needed (no catmini in rename mode)
                        ProcessCategoryPreviewRenameOnly(categoryDir);
                        return;
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
            catch (OperationCanceledException)
            {
                // Clean up partially created files on cancellation
                var catprevPath = Path.Combine(categoryDir, "catprev.jpg");
                var catminiPath = Path.Combine(categoryDir, "catmini.jpg");
                
                try
                {
                    if (File.Exists(catprevPath)) File.Delete(catprevPath);
                    if (File.Exists(catminiPath)) File.Delete(catminiPath);
                    Logger.LogInfo($"Cleaned up category preview files in: {categoryDir}");
                    
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
                
                // Check if catprev exists but catmini is missing - only generate catmini
                var existingCatprevPath = Path.Combine(categoryDir, "catprev.jpg");
                var existingCatminiPath = Path.Combine(categoryDir, "catmini.jpg");
                
                if (File.Exists(existingCatprevPath) && !File.Exists(existingCatminiPath) && !context.Reoptimize)
                {
                    Logger.LogInfo($"catprev.jpg exists but catmini.jpg missing - generating catmini only (Lite)");
                    GenerateCatminiFromCatprevLite(categoryDir, context);
                    return;
                }
                
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
                var catminiPath = Path.Combine(categoryDir, "catmini.jpg");
                
                // Check if source and target are the same file - need to use temp file
                bool isSameFile = sourceFile.Equals(catprevPath, StringComparison.OrdinalIgnoreCase);
                string actualCatprevPath = isSameFile 
                    ? Path.Combine(categoryDir, $"_temp_catprev_{Guid.NewGuid()}.jpg")
                    : catprevPath;
                
                using (var img = Image.FromFile(sourceFile))
                {
                    var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                        .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    
                    if (jpegEncoder != null)
                    {
                        var jpegParams = new EncoderParameters(1);
                        jpegParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)context.JpegQuality);
                        
                        // Generate catprev.jpg - preserve original dimensions
                        img.Save(actualCatprevPath, jpegEncoder, jpegParams);
                        Logger.LogInfo($"Converted (Lite): {Path.GetFileName(sourceFile)} -> catprev.jpg");
                        
                        // Generate catmini.jpg (600x722) - cropped thumbnail
                        var catminiCropRect = ImageCropService.CalculateCropRectangle(img, 600, 722, CropType.Center);
                        using (var catmini = new Bitmap(600, 722))
                        using (var g = Graphics.FromImage(catmini))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.CompositingQuality = CompositingQuality.HighQuality;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            
                            var destRect = new Rectangle(0, 0, 600, 722);
                            g.DrawImage(img, destRect, catminiCropRect, GraphicsUnit.Pixel);
                            catmini.Save(catminiPath, jpegEncoder, jpegParams);
                            Logger.LogInfo($"Generated catmini.jpg (Lite mode)");
                        }
                    }
                }
                
                // If we used a temp file for catprev, move it to the actual target after closing the source
                if (isSameFile && File.Exists(actualCatprevPath))
                {
                    try
                    {
                        if (File.Exists(catprevPath))
                            File.Delete(catprevPath);
                        File.Move(actualCatprevPath, catprevPath);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to move temp catprev file", ex);
                        try { if (File.Exists(actualCatprevPath)) File.Delete(actualCatprevPath); } catch { }
                    }
                }
                
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
        /// Generate catmini.jpg from existing catprev.jpg (Full mode with crop inspection)
        /// </summary>
        private static async Task GenerateCatminiFromCatprevAsync(string categoryDir, OptimizationContext context)
        {
            try
            {
                var catprevPath = Path.Combine(categoryDir, "catprev.jpg");
                var catminiPath = Path.Combine(categoryDir, "catmini.jpg");
                
                using (var img = Image.FromFile(catprevPath))
                {
                    // Get crop rectangle with optional inspection for catmini
                    var catminiCropRect = await GetCropRectangleWithInspectionAsync(
                        img, 600, 722, context, "catmini.jpg", isProtected: false, isThumbnail: true);
                    
                    if (catminiCropRect == null)
                    {
                        Logger.LogInfo($"Skipped catmini.jpg generation (user chose delete)");
                        return;
                    }
                    
                    if (catminiCropRect.Value.X == -1 && catminiCropRect.Value.Y == -1)
                    {
                        Logger.LogInfo($"Skipped catmini.jpg generation (user chose skip)");
                        return;
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
                            Logger.LogInfo($"Generated catmini.jpg from existing catprev.jpg");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to generate catmini from catprev in {categoryDir}", ex);
            }
        }

        /// <summary>
        /// Generate catmini.jpg from existing catprev.jpg (Lite mode with auto crop)
        /// </summary>
        private static void GenerateCatminiFromCatprevLite(string categoryDir, OptimizationContext context)
        {
            try
            {
                var catprevPath = Path.Combine(categoryDir, "catprev.jpg");
                var catminiPath = Path.Combine(categoryDir, "catmini.jpg");
                
                using (var img = Image.FromFile(catprevPath))
                {
                    var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                        .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    
                    if (jpegEncoder != null)
                    {
                        var jpegParams = new EncoderParameters(1);
                        jpegParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)context.JpegQuality);
                        
                        // Generate catmini.jpg (600x722) with auto center crop
                        var catminiCropRect = ImageCropService.CalculateCropRectangle(img, 600, 722, CropType.Center);
                        using (var catmini = new Bitmap(600, 722))
                        using (var g = Graphics.FromImage(catmini))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.CompositingQuality = CompositingQuality.HighQuality;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            
                            var destRect = new Rectangle(0, 0, 600, 722);
                            g.DrawImage(img, destRect, catminiCropRect, GraphicsUnit.Pixel);
                            catmini.Save(catminiPath, jpegEncoder, jpegParams);
                            Logger.LogInfo($"Generated catmini.jpg from existing catprev.jpg (Lite mode)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to generate catmini from catprev (Lite) in {categoryDir}", ex);
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
            // Check for cancellation at the start
            if (_cancellationRequested)
            {
                throw new OperationCanceledException("Optimization cancelled by user");
            }
            
            if (string.IsNullOrEmpty(modDir) || !Directory.Exists(modDir))
            {
                Logger.LogWarning($"Mod directory not found: {modDir}");
                return;
            }
            
            // Check if already optimized and skip if reoptimize is disabled
            // Skip this check for GameBanana downloads, DragDropMod, and Manual - they should always process new files
            if (!context.Reoptimize && context.Trigger != OptimizationTrigger.GameBananaDownload && 
                context.Trigger != OptimizationTrigger.DragDropMod && context.Trigger != OptimizationTrigger.Manual &&
                OptimizationHelper.IsModAlreadyOptimized(modDir))
            {
                Logger.LogInfo($"Skipping already optimized mod: {modDir}");
                return;
            }

            // Handle different optimization modes
            switch (context.Mode)
            {
                case OptimizationMode.CategoryFull:
                    // CategoryFull is only for categories, use Standard for mods
                    await ProcessModPreviewImagesLiteAsync(modDir, context);
                    break;
                    
                case OptimizationMode.Standard:
                default:
                    // Standard: optimize quality, generate thumbnails with auto crop
                    await ProcessModPreviewImagesLiteAsync(modDir, context);
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
            var processedFiles = new List<string>();
            
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
                // Sort so that "preview" (without number) comes first, then "preview-01", "preview-02", etc.
                var originalFiles = allFiles
                    .Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("_original", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => PreviewSortHelper.GetSortOrder(Path.GetFileName(f)))
                    .ToList();
                var regularFiles = allFiles
                    .Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith("_original", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => PreviewSortHelper.GetSortOrder(Path.GetFileName(f)))
                    .ToList();
                
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
                    Logger.LogInfo("Creating original copies before optimization (Full mode)");
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
                
                // Select minitile source BEFORE processing (so user sees original images)
                string? selectedMinitileSource = null;
                if (previewFiles.Count > 0)
                {
                    selectedMinitileSource = await SelectMinitileSourceAsync(previewFiles, modDir, context);
                    if (selectedMinitileSource == null)
                    {
                        Logger.LogInfo($"User cancelled minitile source selection, skipping mod: {modDir}");
                        return;
                    }
                }
                
                // Determine if we should use batch mode
                bool useBatchMode = context.AllowUIInteraction && 
                                   context.InspectAndEditEnabled && 
                                   previewFiles.Count > 1 &&
                                   BatchCropInspectionRequested != null;
                
                if (useBatchMode)
                {
                    // BATCH MODE: Collect all files, show batch panel, then process
                    await ProcessPreviewFilesBatchAsync(modDir, previewFiles, selectedMinitileSource, context, processedFiles);
                }
                else
                {
                    // SINGLE MODE: Process each file one by one (original behavior)
                    await ProcessPreviewFilesSingleAsync(modDir, previewFiles, selectedMinitileSource, context, processedFiles);
                }
                
                // Generate minitile.jpg (600x722 thumbnail) from selected source
                if (!string.IsNullOrEmpty(selectedMinitileSource) && File.Exists(selectedMinitileSource))
                {
                    await GenerateMinitileAsync(modDir, selectedMinitileSource, context);
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
            catch (OperationCanceledException)
            {
                // Clean up partially created files on cancellation
                CleanupProcessedFiles(processedFiles, modDir);
                throw; // Re-throw cancellation to stop optimization
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process mod preview images (Full mode) in {modDir}", ex);
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
            
            // Also clean up minitile.jpg if it was created
            var minitilePath = Path.Combine(directory, "minitile.jpg");
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
        /// Process preview files in single mode (one by one with individual crop inspection)
        /// </summary>
        private static async Task ProcessPreviewFilesSingleAsync(string modDir, List<string> previewFiles, string? selectedMinitileSource, OptimizationContext context, List<string> processedFiles)
        {
            const int targetSize = 1000;
            
            for (int i = 0; i < previewFiles.Count && i < AppConstants.MAX_PREVIEW_IMAGES; i++)
            {
                var sourceFile = previewFiles[i];
                string targetFileName = i == 0 ? "preview.jpg" : $"preview-{i:D2}.jpg";
                var targetPath = Path.Combine(modDir, targetFileName);
                
                bool savedSuccessfully = false;
                try
                {
                    // Check if source and target are the same file - need to use temp file
                    bool isSameFile = sourceFile.Equals(targetPath, StringComparison.OrdinalIgnoreCase);
                    string actualTargetPath = isSameFile 
                        ? Path.Combine(modDir, $"_temp_{Guid.NewGuid()}.jpg")
                        : targetPath;
                    
                    using (var img = Image.FromFile(sourceFile))
                    {
                        bool isMinitileSource = sourceFile.Equals(selectedMinitileSource, StringComparison.OrdinalIgnoreCase);
                        var squareCropRect = await GetCropRectangleWithInspectionAsync(
                            img, targetSize, targetSize, context, $"preview.jpg #{i + 1}", isMinitileSource);
                        
                        if (squareCropRect == null)
                        {
                            Logger.LogInfo($"Deleted: {Path.GetFileName(sourceFile)}");
                            continue;
                        }
                        
                        if (squareCropRect.Value.X == -1 && squareCropRect.Value.Y == -1)
                        {
                            Logger.LogInfo($"Skipping optimization for: {Path.GetFileName(sourceFile)}, rename only");
                            var ext = Path.GetExtension(sourceFile);
                            var renameTarget = Path.Combine(modDir, Path.GetFileNameWithoutExtension(targetFileName) + ext);
                            if (!sourceFile.Equals(renameTarget, StringComparison.OrdinalIgnoreCase))
                            {
                                File.Copy(sourceFile, renameTarget, true);
                                Logger.LogInfo($"Renamed: {Path.GetFileName(sourceFile)} -> {Path.GetFileName(renameTarget)}");
                            }
                            processedFiles.Add(renameTarget);
                            continue;
                        }
                        
                        savedSuccessfully = SaveOptimizedImage(img, actualTargetPath, squareCropRect.Value, targetSize, context.JpegQuality);
                    }
                    
                    // If we used a temp file, move it to the actual target after closing the source
                    if (savedSuccessfully && isSameFile)
                    {
                        try
                        {
                            if (File.Exists(targetPath))
                                File.Delete(targetPath);
                            File.Move(actualTargetPath, targetPath);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to move temp file to target: {targetPath}", ex);
                            // Clean up temp file
                            try { if (File.Exists(actualTargetPath)) File.Delete(actualTargetPath); } catch { }
                            savedSuccessfully = false;
                        }
                    }
                    
                    if (savedSuccessfully)
                    {
                        Logger.LogInfo($"Optimized: {Path.GetFileName(sourceFile)} -> {targetFileName}");
                        processedFiles.Add(targetPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to process preview file: {sourceFile}", ex);
                }
            }
        }

        /// <summary>
        /// Process preview files in batch mode (collect all, show batch panel, then process)
        /// </summary>
        private static async Task ProcessPreviewFilesBatchAsync(string modDir, List<string> previewFiles, string? selectedMinitileSource, OptimizationContext context, List<string> processedFiles)
        {
            const int targetSize = 1000;
            
            // Collect all files with their suggested crop rectangles
            var batchItems = new List<BatchCropItem>();
            var loadedImages = new Dictionary<string, Image>();
            
            try
            {
                for (int i = 0; i < previewFiles.Count && i < AppConstants.MAX_PREVIEW_IMAGES; i++)
                {
                    var sourceFile = previewFiles[i];
                    try
                    {
                        var img = Image.FromFile(sourceFile);
                        loadedImages[sourceFile] = img;
                        
                        var suggestedCrop = GetSuggestedCropRectangle(img, targetSize, targetSize, context.CropStrategy);
                        bool isMinitileSource = sourceFile.Equals(selectedMinitileSource, StringComparison.OrdinalIgnoreCase);
                        
                        batchItems.Add(new BatchCropItem
                        {
                            FilePath = sourceFile,
                            ImageType = $"preview.jpg #{i + 1}",
                            SourceImage = img,
                            InitialCropRect = suggestedCrop,
                            TargetWidth = targetSize,
                            TargetHeight = targetSize,
                            IsProtected = isMinitileSource
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to load image for batch: {sourceFile}", ex);
                    }
                }
                
                if (batchItems.Count == 0)
                {
                    Logger.LogWarning("No images loaded for batch processing");
                    return;
                }
                
                // Show batch crop inspection panel
                Logger.LogInfo($"Requesting batch crop inspection for {batchItems.Count} files");
                var results = await BatchCropInspectionRequested!(batchItems);
                
                if (results == null || results.Count == 0)
                {
                    Logger.LogInfo("Batch crop inspection cancelled or returned no results");
                    return;
                }
                
                // Process results
                for (int i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    string targetFileName = i == 0 ? "preview.jpg" : $"preview-{i:D2}.jpg";
                    var targetPath = Path.Combine(modDir, targetFileName);
                    
                    if (result.Action == CropInspectionAction.Delete)
                    {
                        Logger.LogInfo($"Deleted: {Path.GetFileName(result.FilePath)}");
                        continue;
                    }
                    
                    if (result.Action == CropInspectionAction.Skip)
                    {
                        Logger.LogInfo($"Skipping optimization for: {Path.GetFileName(result.FilePath)}, rename only");
                        var ext = Path.GetExtension(result.FilePath);
                        var renameTarget = Path.Combine(modDir, Path.GetFileNameWithoutExtension(targetFileName) + ext);
                        if (!result.FilePath.Equals(renameTarget, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(result.FilePath, renameTarget, true);
                            Logger.LogInfo($"Renamed: {Path.GetFileName(result.FilePath)} -> {Path.GetFileName(renameTarget)}");
                        }
                        processedFiles.Add(renameTarget);
                        continue;
                    }
                    
                    // Confirm action - optimize the image
                    if (loadedImages.TryGetValue(result.FilePath, out var img))
                    {
                        bool savedSuccessfully = SaveOptimizedImage(img, targetPath, result.CropRectangle, targetSize, context.JpegQuality);
                        if (savedSuccessfully)
                        {
                            Logger.LogInfo($"Optimized: {Path.GetFileName(result.FilePath)} -> {targetFileName}");
                            processedFiles.Add(targetPath);
                        }
                    }
                }
            }
            finally
            {
                // Dispose all loaded images
                foreach (var img in loadedImages.Values)
                {
                    img.Dispose();
                }
            }
        }

        /// <summary>
        /// Save optimized image with crop and resize
        /// </summary>
        private static bool SaveOptimizedImage(Image sourceImage, string targetPath, Rectangle cropRect, int targetSize, int jpegQuality)
        {
            try
            {
                using (var optimized = new Bitmap(targetSize, targetSize))
                using (var g = Graphics.FromImage(optimized))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    
                    var destRect = new Rectangle(0, 0, targetSize, targetSize);
                    g.DrawImage(sourceImage, destRect, cropRect, GraphicsUnit.Pixel);
                    
                    var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                        .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    if (jpegEncoder != null)
                    {
                        var jpegParams = new EncoderParameters(1);
                        jpegParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)jpegQuality);
                        optimized.Save(targetPath, jpegEncoder, jpegParams);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save optimized image: {targetPath}", ex);
            }
            return false;
        }

        /// <summary>
        /// Process mod preview images in Lite mode - quality conversion without resizing or cropping
        /// </summary>
        private static async Task ProcessModPreviewImagesLiteAsync(string modDir, OptimizationContext context)
        {
            var processedFiles = new List<string>();
            
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
                
                // Separate files into categories:
                // 1. Already optimized files (preview.jpg, preview-01.jpg, etc.)
                // 2. New files to process (preview001.jpg, preview002.jpg, etc.)
                // 3. _original files
                var originalFiles = allFiles
                    .Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("_original", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                List<string> alreadyOptimizedFiles;
                List<string> newFilesToProcess;
                
                // If Reoptimize is enabled, treat ALL files as new (process everything from scratch)
                if (context.Reoptimize)
                {
                    Logger.LogInfo("Reoptimize enabled - processing all files from scratch");
                    alreadyOptimizedFiles = new List<string>();
                    
                    // Prefer _original files if they exist, otherwise use all non-original files
                    newFilesToProcess = originalFiles.Count > 0 
                        ? originalFiles.OrderBy(f => PreviewSortHelper.GetSortOrder(Path.GetFileName(f))).ToList()
                        : allFiles
                            .Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith("_original", StringComparison.OrdinalIgnoreCase))
                            .OrderBy(f => PreviewSortHelper.GetSortOrder(Path.GetFileName(f)))
                            .ToList();
                }
                else
                {
                    // Normal mode - only process new files, keep already optimized
                    alreadyOptimizedFiles = allFiles
                        .Where(f => 
                        {
                            var fileName = Path.GetFileNameWithoutExtension(f).ToLower();
                            if (fileName.EndsWith("_original")) return false;
                            // preview.jpg or preview-XX.jpg pattern
                            if (fileName == "preview") return true;
                            if (fileName.StartsWith("preview-") && fileName.Length == 10) return true; // preview-XX
                            return false;
                        })
                        .OrderBy(f => PreviewSortHelper.GetSortOrder(Path.GetFileName(f)))
                        .ToList();
                    
                    newFilesToProcess = allFiles
                        .Where(f => 
                        {
                            var fileName = Path.GetFileNameWithoutExtension(f).ToLower();
                            if (fileName.EndsWith("_original")) return false;
                            if (fileName == "preview") return false;
                            if (fileName.StartsWith("preview-") && fileName.Length == 10) return false; // preview-XX
                            // Everything else is new (preview001, preview002, preview1, etc.)
                            return true;
                        })
                        .OrderBy(f => PreviewSortHelper.GetSortOrder(Path.GetFileName(f)))
                        .ToList();
                }
                
                Logger.LogInfo($"Already optimized: {alreadyOptimizedFiles.Count}, New to process: {newFilesToProcess.Count}");
                
                // If no new files to process, nothing to do
                if (newFilesToProcess.Count == 0)
                {
                    Logger.LogInfo($"No new preview files to process in: {modDir}");
                    return;
                }
                
                // Calculate starting index for new files
                int startIndex = alreadyOptimizedFiles.Count;
                
                // Check if minitile already exists
                var minitilePath = Path.Combine(modDir, "minitile.jpg");
                bool minitileExists = File.Exists(minitilePath);
                
                // If using new files and KeepOriginals is enabled, create _original copies first
                if (context.KeepOriginals && newFilesToProcess.Count > 0)
                {
                    Logger.LogInfo("Creating original copies before optimization (Lite mode)");
                    foreach (var file in newFilesToProcess)
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
                
                // Select minitile source BEFORE processing (so user sees original images)
                // Always show selection - user can decide to create new minitile even if one exists
                string? selectedMinitileSource = null;
                bool skipMinitileOnly = false;
                if (newFilesToProcess.Count > 0)
                {
                    // Combine all files for selection (existing + new)
                    var allForSelection = alreadyOptimizedFiles.Concat(newFilesToProcess).ToList();
                    selectedMinitileSource = await SelectMinitileSourceAsync(allForSelection, modDir, context);
                    if (selectedMinitileSource == null)
                    {
                        // User clicked Skip - skip minitile but continue processing files
                        Logger.LogInfo($"User skipped minitile source selection, continuing with file processing");
                        skipMinitileOnly = true;
                    }
                }
                
                // Process each NEW preview file - convert to JPEG without resizing
                // Use indices starting from existing count
                for (int i = 0; i < newFilesToProcess.Count && (startIndex + i) < AppConstants.MAX_PREVIEW_IMAGES; i++)
                {
                    var sourceFile = newFilesToProcess[i];
                    int targetIndex = startIndex + i;
                    string targetFileName = targetIndex == 0 ? "preview.jpg" : $"preview-{targetIndex:D2}.jpg";
                    var targetPath = Path.Combine(modDir, targetFileName);
                    
                    bool savedSuccessfully = false;
                    try
                    {
                        // Check if source and target are the same file - need to use temp file
                        bool isSameFile = sourceFile.Equals(targetPath, StringComparison.OrdinalIgnoreCase);
                        string actualTargetPath = isSameFile 
                            ? Path.Combine(modDir, $"_temp_{Guid.NewGuid()}.jpg")
                            : targetPath;
                        
                        using (var img = Image.FromFile(sourceFile))
                        {
                            // Save as JPEG with quality setting, preserving original dimensions
                            var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                                .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                            if (jpegEncoder != null)
                            {
                                var jpegParams = new EncoderParameters(1);
                                jpegParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)context.JpegQuality);
                                img.Save(actualTargetPath, jpegEncoder, jpegParams);
                                savedSuccessfully = true;
                            }
                        }
                        
                        // If we used a temp file, move it to the actual target after closing the source
                        if (savedSuccessfully && isSameFile)
                        {
                            try
                            {
                                if (File.Exists(targetPath))
                                    File.Delete(targetPath);
                                File.Move(actualTargetPath, targetPath);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Failed to move temp file to target: {targetPath}", ex);
                                // Clean up temp file
                                try { if (File.Exists(actualTargetPath)) File.Delete(actualTargetPath); } catch { }
                                savedSuccessfully = false;
                            }
                        }
                        
                        if (savedSuccessfully)
                        {
                            Logger.LogInfo($"Converted (Lite): {Path.GetFileName(sourceFile)} -> {targetFileName}");
                            processedFiles.Add(targetPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to process preview file: {sourceFile}", ex);
                    }
                }
                
                // Generate minitile.jpg (600x722 thumbnail) from selected source
                // Only if not skipping minitile
                if (!skipMinitileOnly && !string.IsNullOrEmpty(selectedMinitileSource) && File.Exists(selectedMinitileSource))
                {
                    await GenerateMinitileAsync(modDir, selectedMinitileSource, context);
                }
                
                // Handle original files based on KeepOriginals setting
                // Only process new files, not already optimized ones
                if (context.KeepOriginals)
                {
                    // Rename original files with _original suffix
                    foreach (var file in newFilesToProcess)
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
                    // Delete original source files (the new ones we just processed)
                    foreach (var file in newFilesToProcess)
                    {
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                        // Skip _original files and processed output files
                        if (fileNameWithoutExt.EndsWith("_original", StringComparison.OrdinalIgnoreCase) ||
                            processedFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                            continue;
                            
                        try
                        {
                            File.Delete(file);
                            Logger.LogInfo($"Deleted source: {Path.GetFileName(file)}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to delete source file: {file}", ex);
                        }
                    }
                }
                
                Logger.LogInfo($"Mod preview processing (Lite mode) completed for: {modDir}");
            }
            catch (OperationCanceledException)
            {
                // Clean up partially created files on cancellation
                CleanupProcessedFiles(processedFiles, modDir);
                throw; // Re-throw cancellation to stop optimization
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
                // Sort so that "preview" (without number) comes first, then "preview-01", "preview-02", etc.
                var previewFiles = Directory.GetFiles(modDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("preview") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .OrderBy(f => PreviewSortHelper.GetSortOrder(Path.GetFileName(f)))
                    .ToList();
                
                if (previewFiles.Count == 0)
                {
                    Logger.LogInfo($"No preview files found in: {modDir}");
                    return;
                }
                
                // FIRST: Generate minitile.jpg from selected preview (BEFORE renaming)
                var minitileSource = await SelectMinitileSourceAsync(previewFiles, modDir, context);
                if (!string.IsNullOrEmpty(minitileSource))
                {
                    Logger.LogInfo($"Generating minitile from: {Path.GetFileName(minitileSource)}");
                    await GenerateMinitileAsync(modDir, minitileSource, context);
                }
                
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
                // Sort so that "preview" (without number) comes first, then "preview-01", "preview-02", etc.
                var previewFiles = Directory.GetFiles(modDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("preview") &&
                               (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                    })
                    .OrderBy(f => PreviewSortHelper.GetSortOrder(Path.GetFileName(f)))
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
        /// Select source file for minitile generation with optional UI selection
        /// </summary>
        private static async Task<string?> SelectMinitileSourceAsync(List<string> availableFiles, string modDir, OptimizationContext context)
        {
            // If only one file, use it directly
            if (availableFiles.Count <= 1)
            {
                return availableFiles.FirstOrDefault();
            }
            
            // If AutoCreateModThumbnails is enabled, skip UI and use first file
            if (SettingsManager.Current.AutoCreateModThumbnails)
            {
                Logger.LogInfo($"AutoCreateModThumbnails enabled - using first preview for minitile: {Path.GetFileName(availableFiles.First())}");
                return availableFiles.FirstOrDefault();
            }
            
            // If UI interaction not allowed and InspectThumbnailsOnly is not enabled, use first file
            if (!context.AllowUIInteraction && !context.InspectThumbnailsOnly)
            {
                return availableFiles.FirstOrDefault();
            }
            
            // Multiple files - ask user to select if:
            // - UI interaction is allowed (always show selection panel)
            // - OR InspectThumbnailsOnly is enabled (show selection even without full inspection)
            if (MinitileSourceSelectionRequested != null && (context.AllowUIInteraction || context.InspectThumbnailsOnly))
            {
                // Check if cancellation was requested before showing UI
                if (_cancellationRequested)
                {
                    throw new OperationCanceledException("Optimization cancelled by user");
                }
                
                try
                {
                    Logger.LogInfo($"Requesting minitile source selection for: {modDir}");
                    var result = await MinitileSourceSelectionRequested(availableFiles, modDir);
                    
                    // Check if cancellation was requested while waiting for UI
                    if (_cancellationRequested)
                    {
                        throw new OperationCanceledException("Optimization cancelled by user");
                    }
                    
                    if (result != null)
                    {
                        if (result.Stopped)
                        {
                            Logger.LogInfo($"User stopped optimization from minitile source selection");
                            throw new OperationCanceledException("User stopped optimization");
                        }
                        
                        if (result.Skipped)
                        {
                            Logger.LogInfo($"User skipped minitile source selection for: {modDir}");
                            return null; // Skip this mod, continue with others
                        }
                        
                        if (!string.IsNullOrEmpty(result.SelectedFilePath))
                        {
                            Logger.LogInfo($"User selected minitile source: {Path.GetFileName(result.SelectedFilePath)}");
                            return result.SelectedFilePath;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // Re-throw cancellation exceptions
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Minitile source selection failed", ex);
                }
            }
            
            // Fallback to first file
            return availableFiles.FirstOrDefault();
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
                    // Get crop rectangle with optional inspection (minitile is a thumbnail)
                    var srcRect = await GetCropRectangleWithInspectionAsync(
                        img, 600, 722, context, "minitile.jpg", isProtected: false, isThumbnail: true);
                    
                    if (srcRect == null)
                    {
                        Logger.LogInfo($"Deleted minitile generation for: {modDir}");
                        return; // User chose to delete
                    }
                    
                    // Check if user chose to skip optimization (rename only)
                    if (srcRect.Value.X == -1 && srcRect.Value.Y == -1)
                    {
                        Logger.LogInfo($"Skipping minitile optimization for: {modDir}, rename only");
                        // For minitile, skip means don't generate it at all (since it's a thumbnail)
                        return;
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
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation to stop optimization
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
                _ => CropType.Center
            };
        }

        /// <summary>
        /// Get suggested crop rectangle without inspection (for batch mode)
        /// </summary>
        private static Rectangle GetSuggestedCropRectangle(Image image, int targetWidth, int targetHeight, CropStrategy strategy)
        {
            var cropType = ConvertCropStrategy(strategy);
            return ImageCropService.CalculateCropRectangle(image, targetWidth, targetHeight, cropType);
        }

        /// <summary>
        /// Get crop rectangle with optional inspection
        /// </summary>
        public static async Task<Rectangle?> GetCropRectangleWithInspectionAsync(
            Image image, 
            int targetWidth, 
            int targetHeight, 
            OptimizationContext context,
            string imageType,
            bool isProtected = false,
            bool isThumbnail = false)
        {
            var cropType = ConvertCropStrategy(context.CropStrategy);
            var suggestedCrop = ImageCropService.CalculateCropRectangle(image, targetWidth, targetHeight, cropType);

            // Show inspection panel ONLY IF:
            // 1. UI interaction is allowed (not background processing)
            // 2. AND one of:
            //    a) Inspect&Edit is enabled (for all images)
            //    b) InspectThumbnailsOnly is enabled AND this is a thumbnail (minitile/catmini)
            // 3. BUT NOT if AutoCreateModThumbnails is enabled AND this is a minitile
            bool isMinitile = imageType.ToLower().Contains("minitile");
            bool skipDueToAutoCreate = SettingsManager.Current.AutoCreateModThumbnails && isMinitile;
            
            bool needsInspection = context.AllowUIInteraction && 
                                  !skipDueToAutoCreate &&
                                  (context.InspectAndEditEnabled ||
                                   (context.InspectThumbnailsOnly && isThumbnail));

            if (needsInspection && CropInspectionRequested != null)
            {
                // Check if cancellation was requested before showing UI
                if (_cancellationRequested)
                {
                    throw new OperationCanceledException("Optimization cancelled by user");
                }
                
                try
                {
                    Logger.LogInfo($"Requesting crop inspection for {imageType}" + (isProtected ? " (protected - minitile source)" : ""));
                    var result = await CropInspectionRequested(image, suggestedCrop, targetWidth, targetHeight, imageType, isProtected);
                    
                    // Check if cancellation was requested while waiting for UI
                    if (_cancellationRequested)
                    {
                        throw new OperationCanceledException("Optimization cancelled by user");
                    }
                    
                    if (result != null)
                    {
                        switch (result.Action)
                        {
                            case CropInspectionAction.Confirm:
                                Logger.LogInfo($"User confirmed crop for {imageType}");
                                return result.CropRectangle;
                                
                            case CropInspectionAction.Skip:
                                Logger.LogInfo($"User chose to skip optimization for {imageType} (rename only)");
                                // Return special marker to indicate skip optimization but keep file
                                return new Rectangle(-1, -1, -1, -1); // Special marker for skip
                                
                            case CropInspectionAction.Delete:
                                Logger.LogInfo($"User chose to delete {imageType}");
                                return null; // Return null to skip/delete this image
                                
                            case CropInspectionAction.Cancel:
                                Logger.LogInfo($"User cancelled optimization");
                                throw new OperationCanceledException("User cancelled crop inspection");
                                
                            default:
                                Logger.LogWarning($"Unknown crop action for {imageType}: {result.Action}");
                                return null;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // Re-throw cancellation exceptions
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
                
                // Check if we need fully sequential processing for crop inspection
                // Full sequential processing is needed when:
                // - InspectAndEditEnabled (preview before crop for ALL images)
                bool needsFullSequentialProcessing = context.InspectAndEditEnabled;
                
                // Hybrid mode: parallel preview processing, sequential thumbnail inspection
                // Used when InspectThumbnailsOnly is enabled
                bool needsHybridProcessing = context.InspectThumbnailsOnly && !needsFullSequentialProcessing;
                
                bool wasCancelled = false;
                
                if (needsFullSequentialProcessing)
                {
                    // Full sequential processing - all images need user interaction
                    Logger.LogInfo("Using full sequential processing (crop inspection for all images)");
                    
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
                            
                            // Update current processing mod status
                            var modName = GetModDisplayName(modDir);
                            SetCurrentProcessingMod(modName);
                            NotifyProgressChanged();
                            
                            await ProcessModPreviewImagesAsync(modDir, context);
                            IncrementProcessed();
                        }
                        
                        if (wasCancelled) break;
                    }
                }
                else if (needsHybridProcessing)
                {
                    // Hybrid mode: process previews in parallel, but thumbnails sequentially
                    // This allows fast preview processing while showing thumbnail inspection one by one
                    Logger.LogInfo("Using hybrid processing (parallel previews, sequential thumbnails)");
                    
                    foreach (var categoryDir in categoryDirs)
                    {
                        if (_cancellationRequested) { wasCancelled = true; break; }
                        if (!Directory.Exists(categoryDir)) continue;
                        
                        // Process category preview (includes catmini thumbnail - needs sequential)
                        await ProcessCategoryPreviewAsync(categoryDir, context);
                        IncrementProcessed();
                        
                        // Process all mods in category - one by one for thumbnail inspection
                        var modDirs = Directory.GetDirectories(categoryDir);
                        foreach (var modDir in modDirs)
                        {
                            if (_cancellationRequested) { wasCancelled = true; break; }
                            
                            // Update current processing mod status
                            var modName = GetModDisplayName(modDir);
                            SetCurrentProcessingMod(modName);
                            NotifyProgressChanged();
                            
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
                    _currentProcessingMod = ""; // Clear current mod status
                }
                NotifyProgressChanged();
            }
        }

        /// <summary>
        /// Get optimization context from settings for specified trigger
        /// </summary>
        public static OptimizationContext GetOptimizationContext(OptimizationTrigger trigger)
        {
            // Standard mode for everything, CategoryFull only for category operations
            var mode = trigger == OptimizationTrigger.DragDropCategory 
                ? OptimizationMode.CategoryFull 
                : OptimizationMode.Standard;

            var cropStrategyString = SettingsManager.Current.ImageCropType ?? "Center";
            var cropStrategy = Enum.TryParse<CropStrategy>(cropStrategyString, out var parsedStrategy)
                ? parsedStrategy
                : CropStrategy.Center;

            // DragDropCategory always forces InspectAndEdit because catprev (722x722) and catmini (600x722) 
            // require different crop areas
            bool inspectAndEdit = trigger == OptimizationTrigger.DragDropCategory 
                ? true 
                : SettingsManager.Current.PreviewBeforeCrop;

            return new OptimizationContext
            {
                Mode = mode,
                JpegQuality = SettingsManager.Current.ImageOptimizerJpegQuality,
                ThreadCount = SettingsManager.Current.ImageOptimizerThreadCount,
                CreateBackups = SettingsManager.Current.ImageOptimizerCreateBackups,
                KeepOriginals = SettingsManager.Current.ImageOptimizerKeepOriginals,
                CropStrategy = cropStrategy,
                InspectAndEditEnabled = inspectAndEdit,
                InspectThumbnailsOnly = true,
                Trigger = trigger,
                AllowUIInteraction = true,
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
        public bool InspectThumbnailsOnly { get; set; } = true; // Always show crop inspection for minitile/catmini
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
        Attention
    }
    
    /// <summary>
    /// Helper methods for preview file sorting
    /// </summary>
    public static class PreviewSortHelper
    {
        /// <summary>
        /// Get sort order for preview files so that "preview" (without number) comes first,
        /// then "preview-01", "preview-02", etc.
        /// </summary>
        public static int GetSortOrder(string fileName)
        {
            // Remove extension and _original suffix for comparison
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName).ToLower();
            if (nameWithoutExt.EndsWith("_original"))
                nameWithoutExt = nameWithoutExt.Substring(0, nameWithoutExt.Length - "_original".Length);
            
            // "preview" without number should be first (order 0)
            if (nameWithoutExt == "preview")
                return 0;
            
            // Try to extract number from "preview-XX" or "previewXX"
            var match = System.Text.RegularExpressions.Regex.Match(nameWithoutExt, @"preview-?(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                return num;
            
            // Unknown format, put at end
            return int.MaxValue;
        }
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
