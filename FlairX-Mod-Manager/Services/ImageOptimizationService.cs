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
    /// <summary>
    /// Image format for optimization
    /// </summary>
    public enum ImageFormat
    {
        JPEG,
        WebP
    }
    
    /// <summary>
    /// Delegate for crop inspection callback
    /// </summary>
    public delegate Task<CropInspectionResult?> CropInspectionHandler(Image<Rgba32> sourceImage, Rectangle suggestedCrop, int targetWidth, int targetHeight, string imageType, bool isProtected);

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
        public Image<Rgba32>? SourceImage { get; set; }
        public Rectangle InitialCropRect { get; set; }
        public int TargetWidth { get; set; }
        public int TargetHeight { get; set; }
        public bool IsProtected { get; set; }
        public int TargetIndex { get; set; } // Index for output file naming (preview.jpg = 0, preview-01.jpg = 1, etc.)
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
        public int TargetIndex { get; set; } // Index for output file naming
    }

    /// <summary>
    /// Service for image optimization operations across all contexts (Manual, Drag&Drop, Auto)
    /// </summary>
    public static class ImageOptimizationService
    {
        // Constants and structures for MoveToRecycleBin
        private const int FO_DELETE = 0x0003;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOCONFIRMATION = 0x0010;
        private const ushort FOF_SILENT = 0x0004;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public int wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);
        
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
                        var json = FileAccessQueue.ReadAllText(modJsonPath);
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
                    ? Path.Combine(categoryDir, $"_temp_catprev_{Guid.NewGuid()}{GetImageExtension()}")
                    : catprevPath;
                
                using (var img = Image.Load<Rgba32>(previewPath))
                {
                    // Get crop rectangle with optional inspection for catprev
                    var catprevCropRect = await GetCropRectangleWithInspectionAsync(
                        img, 722, 722, context, GetCatprevFilename());
                    
                    if (catprevCropRect == null)
                    {
                        Logger.LogInfo($"Deleted {GetCatprevFilename()} generation");
                        return; // User chose to delete
                    }
                    
                    // Skip not allowed for category - crop is required
                    if (catprevCropRect.Value.X == -1 && catprevCropRect.Value.Y == -1)
                    {
                        Logger.LogInfo($"Skipping {GetCatprevFilename()} generation (user chose skip)");
                        return;
                    }
                    
                    // Generate catprev (722x722) - crop and resize
                    using (var catprev = img.Clone(ctx => ctx
                        .Crop(catprevCropRect.Value)
                        .Resize(new ResizeOptions
                        {
                            Size = new Size(722, 722),
                            Mode = ResizeMode.Stretch,
                            Sampler = KnownResamplers.Bicubic  // = HighQualityBicubic
                        })))
                    {
                        SaveImage(catprev, actualCatprevPath, context.JpegQuality);
                        Logger.LogInfo($"Generated {GetCatprevFilename()}");
                    }
                    
                    // Get crop rectangle with optional inspection for catmini (thumbnail)
                    var catminiCropRect = await GetCropRectangleWithInspectionAsync(
                        img, 600, 722, context, GetCatminiFilename(), isProtected: false, isThumbnail: true);
                    
                    if (catminiCropRect == null)
                    {
                        Logger.LogInfo($"Deleted {GetCatminiFilename()} generation");
                        return; // User chose to delete
                    }
                    
                    // Skip not allowed for category - crop is required
                    if (catminiCropRect.Value.X == -1 && catminiCropRect.Value.Y == -1)
                    {
                        Logger.LogInfo($"Skipping {GetCatminiFilename()} generation (user chose skip)");
                        return;
                    }
                    
                    // Generate catmini (600x722) - crop and resize
                    using (var catmini = img.Clone(ctx => ctx
                        .Crop(catminiCropRect.Value)
                        .Resize(new ResizeOptions
                        {
                            Size = new Size(600, 722),
                            Mode = ResizeMode.Stretch,
                            Sampler = KnownResamplers.Bicubic  // = HighQualityBicubic
                        })))
                    {
                        SaveImage(catmini, catminiPath, context.JpegQuality);
                        Logger.LogInfo($"Generated {GetCatminiFilename()}");
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
        /// Process category preview in Standard mode - quality conversion without resizing or cropping
        /// </summary>
        private static void ProcessCategoryPreviewLite(string categoryDir, OptimizationContext context)
        {
            try
            {
                Logger.LogInfo($"Processing category preview (Standard) in: {categoryDir}");
                
                // Check if catprev exists but catmini is missing - only generate catmini
                var existingCatprevPath = Path.Combine(categoryDir, GetCatprevFilename());
                var existingCatminiPath = Path.Combine(categoryDir, GetCatminiFilename());
                
                if (File.Exists(existingCatprevPath) && !File.Exists(existingCatminiPath) && !context.Reoptimize)
                {
                    Logger.LogInfo($"{GetCatprevFilename()} exists but {GetCatminiFilename()} missing - generating catmini only (Lite)");
                    GenerateCatminiFromCatprevLite(categoryDir, context);
                    return;
                }
                
                // Process gbicon separately (PNG/WebP only - needs alpha channel)
                ProcessGbIcon(categoryDir, context);
                
                // Create backup if enabled
                if (context.CreateBackups)
                {
                    var filesToBackup = Directory.GetFiles(categoryDir)
                        .Where(f =>
                        {
                            var fileName = Path.GetFileName(f).ToLower();
                            return (fileName.StartsWith("preview") || fileName.StartsWith("catprev")) &&
                                   IsImageFile(f);
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
                               IsImageFile(f);
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
                var catprevPath = Path.Combine(categoryDir, GetCatprevFilename());
                var catminiPath = Path.Combine(categoryDir, GetCatminiFilename());
                
                // Check if source and target are the same file - need to use temp file
                bool isSameFile = sourceFile.Equals(catprevPath, StringComparison.OrdinalIgnoreCase);
                string actualCatprevPath = isSameFile 
                    ? Path.Combine(categoryDir, $"_temp_catprev_{Guid.NewGuid()}{GetImageExtension()}")
                    : catprevPath;
                
                using (var img = Image.Load<Rgba32>(sourceFile))
                {
                    // Save catprev without resizing (just convert to target format)
                    SaveImage(img, actualCatprevPath, context.JpegQuality);
                    Logger.LogInfo($"Converted (Lite): {Path.GetFileName(sourceFile)} -> {GetCatprevFilename()}");
                    
                    // Generate catmini (600x722) - cropped thumbnail
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
                        Logger.LogInfo($"Generated {GetCatminiFilename()} (Standard)");
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
                
                Logger.LogInfo($"Category preview processing (Standard) completed for: {categoryDir}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process category preview (Standard) in {categoryDir}", ex);
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
                
                using (var img = Image.Load<Rgba32>(catprevPath))
                {
                    // Generate catmini (600x722) with auto crop
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

            // Always use Standard mode - it handles InspectAndEditEnabled internally for crop inspection
            await ProcessModPreviewImagesLiteAsync(modDir, context);
        }

        /// <summary>
        /// Synchronous wrapper for backward compatibility
        /// </summary>
        public static void ProcessModPreviewImages(string modDir, OptimizationContext context)
        {
            ProcessModPreviewImagesAsync(modDir, context).GetAwaiter().GetResult();
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
        /// Move file to recycle bin instead of permanent deletion
        /// </summary>
        private static void MoveToRecycleBin(string path)
        {
            try
            {
                var shf = new SHFILEOPSTRUCT
                {
                    wFunc = FO_DELETE,
                    pFrom = path + '\0' + '\0',
                    fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
                };
                
                int result = SHFileOperation(ref shf);
                if (result != 0)
                {
                    Logger.LogWarning($"Failed to move file to recycle bin (error {result}), falling back to permanent deletion: {path}");
                    File.Delete(path);
                }
                else
                {
                    Logger.LogInfo($"Moved to recycle bin: {Path.GetFileName(path)}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to move file to recycle bin, falling back to permanent deletion: {path}. Error: {ex.Message}");
                try
                {
                    File.Delete(path);
                }
                catch { }
            }
        }

        /// <summary>
        /// Process preview files in single mode (one by one with individual crop inspection)
        /// </summary>
        private static async Task ProcessPreviewFilesSingleAsync(string modDir, List<string> previewFiles, string? selectedMinitileSource, OptimizationContext context, List<string> processedFiles, int startIndex = 0)
        {
            const int targetSize = 1000;
            
            for (int i = 0; i < previewFiles.Count && (startIndex + i) < AppConstants.MAX_PREVIEW_IMAGES; i++)
            {
                var sourceFile = previewFiles[i];
                int targetIndex = startIndex + i;
                string targetFileName = GetPreviewFilename(targetIndex);
                var targetPath = Path.Combine(modDir, targetFileName);
                
                bool savedSuccessfully = false;
                string? tempFileToMove = null;
                string? tempFileTarget = null;
                
                try
                {
                    // Check if source and target are the same file - need to use temp file
                    bool isSameFile = sourceFile.Equals(targetPath, StringComparison.OrdinalIgnoreCase);
                    string actualTargetPath = isSameFile 
                        ? Path.Combine(modDir, $"_temp_{Guid.NewGuid()}{GetImageExtension()}")
                        : targetPath;
                    
                    using (var img = Image.Load<Rgba32>(sourceFile))
                    {
                        bool isMinitileSource = sourceFile.Equals(selectedMinitileSource, StringComparison.OrdinalIgnoreCase);
                        var squareCropRect = await GetCropRectangleWithInspectionAsync(
                            img, targetSize, targetSize, context, $"preview.jpg #{i + 1}", isMinitileSource);
                        
                        if (squareCropRect == null)
                        {
                            // If this is the minitile source, process it as No Crop (save as preview.jpg at index 0)
                            if (isMinitileSource)
                            {
                                Logger.LogInfo($"Processing minitile source as No Crop: {Path.GetFileName(sourceFile)}");
                                string minitileTargetPath = Path.Combine(modDir, GetPreviewFilename(0));
                                
                                // Check if source and target are the same file - use temp file
                                bool isSameFileForMinitile = sourceFile.Equals(minitileTargetPath, StringComparison.OrdinalIgnoreCase);
                                string actualMinitileTargetPath = isSameFileForMinitile 
                                    ? Path.Combine(modDir, $"_temp_minitile_{Guid.NewGuid()}{GetImageExtension()}")
                                    : minitileTargetPath;
                                
                                if (await SaveAsJpegOnlyAsync(img, actualMinitileTargetPath, context.JpegQuality))
                                {
                                    processedFiles.Add(minitileTargetPath);
                                    
                                    // If we used a temp file, store it for moving after the using block
                                    if (isSameFileForMinitile)
                                    {
                                        tempFileToMove = actualMinitileTargetPath;
                                        tempFileTarget = minitileTargetPath;
                                    }
                                }
                            }
                            else
                            {
                                Logger.LogInfo($"Marked for deletion: {Path.GetFileName(sourceFile)}");
                            }
                            continue;
                        }
                        
                        // No Crop selected - save as JPEG without cropping/resizing
                        bool isNoCrop = squareCropRect.Value.X == -1 && squareCropRect.Value.Y == -1;
                        if (isNoCrop)
                        {
                            Logger.LogInfo($"No crop for: {Path.GetFileName(sourceFile)} - saving as JPEG only");
                            savedSuccessfully = await SaveAsJpegOnlyAsync(img, actualTargetPath, context.JpegQuality);
                        }
                        else
                        {
                            savedSuccessfully = await SaveOptimizedImageAsync(img, actualTargetPath, squareCropRect.Value, context.JpegQuality);
                        }
                    }
                    
                    // Move temp file for minitile source if needed (after image is closed)
                    if (tempFileToMove != null && tempFileTarget != null)
                    {
                        try
                        {
                            if (File.Exists(tempFileTarget))
                                File.Delete(tempFileTarget);
                            File.Move(tempFileToMove, tempFileTarget);
                        }
                        catch (Exception moveEx)
                        {
                            Logger.LogError($"Failed to move temp minitile file", moveEx);
                            try { if (File.Exists(tempFileToMove)) File.Delete(tempFileToMove); } catch { }
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
        private static async Task ProcessPreviewFilesBatchAsync(string modDir, List<string> previewFiles, string? selectedMinitileSource, OptimizationContext context, List<string> processedFiles, int startIndex = 0)
        {
            Logger.LogInfo($"[BATCH_PROCESS] Starting batch processing for {previewFiles.Count} files");
            const int targetSize = 1000;
            
            // Collect all files with their suggested crop rectangles
            var batchItems = new List<BatchCropItem>();
            var loadedImages = new Dictionary<string, Image<Rgba32>>();
            
            try
            {
                for (int i = 0; i < previewFiles.Count && (startIndex + i) < AppConstants.MAX_PREVIEW_IMAGES; i++)
                {
                    var sourceFile = previewFiles[i];
                    int targetIndex = startIndex + i;
                    
                    try
                    {
                        var img = Image.Load<Rgba32>(sourceFile);
                        loadedImages[sourceFile] = img;
                        
                        var suggestedCrop = GetSuggestedCropRectangle(img, targetSize, targetSize, context.CropStrategy);
                        bool isMinitileSource = sourceFile.Equals(selectedMinitileSource, StringComparison.OrdinalIgnoreCase);
                        
                        batchItems.Add(new BatchCropItem
                        {
                            FilePath = sourceFile,
                            ImageType = $"preview.jpg #{targetIndex + 1}",
                            SourceImage = img,
                            InitialCropRect = suggestedCrop,
                            TargetWidth = targetSize,
                            TargetHeight = targetSize,
                            IsProtected = isMinitileSource,
                            TargetIndex = targetIndex // Store target index for later use
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[BATCH_PROCESS] Failed to load image for batch: {sourceFile}", ex);
                    }
                }
                
                if (batchItems.Count == 0)
                {
                    Logger.LogWarning("[BATCH_PROCESS] No images loaded for batch processing");
                    return;
                }
                
                Logger.LogInfo($"[BATCH_PROCESS] All {batchItems.Count} items prepared, requesting batch crop inspection");
                // Show batch crop inspection panel
                var results = await BatchCropInspectionRequested!(batchItems);
                
                if (results == null || results.Count == 0)
                {
                    Logger.LogInfo("[BATCH_PROCESS] Batch crop inspection cancelled or returned no results");
                    return;
                }
                
                Logger.LogInfo($"[BATCH_PROCESS] Processing {results.Count} results - showing progress dialog");
                
                // Show progress dialog for conversion (especially important for WebP which is slower)
                Dialogs.ProgressDialog? progressDialog = null;
                var lang = SharedUtilities.LoadLanguageDictionary();
                var progressTitle = SharedUtilities.GetTranslation(lang, "ProcessingImages") ?? "Processing Images";
                var progressMessage = SharedUtilities.GetTranslation(lang, "ConvertingImages") ?? "Converting images, please wait...";
                
                Logger.LogInfo($"[BATCH_PROCESS] Checking if App.Current is available");
                if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                {
                    Logger.LogInfo($"[BATCH_PROCESS] MainWindow found, creating progress dialog");
                    progressDialog = new Dialogs.ProgressDialog(progressTitle, progressMessage);
                    progressDialog.XamlRoot = mainWindow.Content.XamlRoot;
                    
                    // Show dialog asynchronously (don't await - we want to process while it's showing)
                    _ = progressDialog.ShowAsync();
                    
                    // Small delay to ensure dialog is visible
                    await Task.Delay(200);
                    Logger.LogInfo($"[BATCH_PROCESS] Progress dialog shown, starting image processing");
                }
                else
                {
                    Logger.LogWarning($"[BATCH_PROCESS] MainWindow not available, skipping progress dialog");
                }
                
                // Separate minitile source from other files
                var minitileSourceResult = results.FirstOrDefault(r => 
                    !string.IsNullOrEmpty(selectedMinitileSource) && 
                    r.FilePath.Equals(selectedMinitileSource, StringComparison.OrdinalIgnoreCase));
                var otherResults = results.Where(r => 
                    string.IsNullOrEmpty(selectedMinitileSource) || 
                    !r.FilePath.Equals(selectedMinitileSource, StringComparison.OrdinalIgnoreCase)).ToList();
                
                int currentIndex = startIndex;
                int processedCount = 0;
                int totalToProcess = (minitileSourceResult != null ? 1 : 0) + otherResults.Count(r => r.Action != CropInspectionAction.Delete);
                
                // Process minitile source FIRST as next preview file
                if (minitileSourceResult != null && loadedImages.TryGetValue(minitileSourceResult.FilePath, out var minitileImg))
                {
                    processedCount++;
                    progressDialog?.UpdateProgress(processedCount, totalToProcess, $"{Path.GetFileName(minitileSourceResult.FilePath)}");
                    
                    string targetFileName = GetPreviewFilename(currentIndex);
                    string targetPath = Path.Combine(modDir, targetFileName);
                    bool savedSuccessfully = false;
                    
                    // Check if source and target are the same file - need to use temp file
                    bool isSameFile = minitileSourceResult.FilePath.Equals(targetPath, StringComparison.OrdinalIgnoreCase);
                    string actualTargetPath = isSameFile 
                        ? Path.Combine(modDir, $"_temp_preview_{Guid.NewGuid()}{GetImageExtension()}")
                        : targetPath;
                    
                    if (minitileSourceResult.Action == CropInspectionAction.Delete)
                    {
                        // Delete means No Crop for minitile source
                        savedSuccessfully = await SaveAsJpegOnlyAsync(minitileImg, actualTargetPath, context.JpegQuality);
                    }
                    else if (minitileSourceResult.Action == CropInspectionAction.Skip)
                    {
                        savedSuccessfully = await SaveAsJpegOnlyAsync(minitileImg, actualTargetPath, context.JpegQuality);
                    }
                    else
                    {
                        savedSuccessfully = await SaveOptimizedImageAsync(minitileImg, actualTargetPath, minitileSourceResult.CropRectangle, context.JpegQuality);
                    }
                    
                    // Yield to allow UI updates
                    await Task.Yield();
                    
                    // If we used a temp file, move it to the actual target
                    if (savedSuccessfully && isSameFile)
                    {
                        // Need to dispose the image first before moving
                        minitileImg.Dispose();
                        loadedImages.Remove(minitileSourceResult.FilePath);
                        
                        try
                        {
                            if (File.Exists(targetPath))
                                File.Delete(targetPath);
                            File.Move(actualTargetPath, targetPath);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"[BATCH_PROCESS] Failed to move temp preview file", ex);
                            try { if (File.Exists(actualTargetPath)) File.Delete(actualTargetPath); } catch { }
                            savedSuccessfully = false;
                        }
                    }
                    
                    if (savedSuccessfully)
                    {
                        Logger.LogInfo($"Optimized: {Path.GetFileName(minitileSourceResult.FilePath)} -> {targetFileName}");
                        processedFiles.Add(targetPath);
                        currentIndex++;
                    }
                }
                
                // Process other files with sequential indices
                foreach (var result in otherResults)
                {
                    if (result.Action == CropInspectionAction.Delete)
                    {
                        continue;
                    }
                    
                    processedCount++;
                    progressDialog?.UpdateProgress(processedCount, totalToProcess, $"{Path.GetFileName(result.FilePath)}");
                    
                    string targetFileName = GetPreviewFilename(currentIndex);
                    var targetPath = Path.Combine(modDir, targetFileName);
                    
                    if (loadedImages.TryGetValue(result.FilePath, out var img))
                    {
                        // Check if source and target are the same file - need to use temp file
                        bool isSameFile = result.FilePath.Equals(targetPath, StringComparison.OrdinalIgnoreCase);
                        string actualTargetPath = isSameFile 
                            ? Path.Combine(modDir, $"_temp_{Guid.NewGuid()}{GetImageExtension()}")
                            : targetPath;
                        
                        bool savedSuccessfully;
                        if (result.Action == CropInspectionAction.Skip)
                        {
                            savedSuccessfully = await SaveAsJpegOnlyAsync(img, actualTargetPath, context.JpegQuality);
                        }
                        else
                        {
                            savedSuccessfully = await SaveOptimizedImageAsync(img, actualTargetPath, result.CropRectangle, context.JpegQuality);
                        }
                        
                        // Yield to allow UI updates
                        await Task.Yield();
                        
                        // If we used a temp file, move it to the actual target
                        if (savedSuccessfully && isSameFile)
                        {
                            // Need to dispose the image first before moving
                            img.Dispose();
                            loadedImages.Remove(result.FilePath);
                            
                            try
                            {
                                if (File.Exists(targetPath))
                                    File.Delete(targetPath);
                                File.Move(actualTargetPath, targetPath);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"[BATCH_PROCESS] Failed to move temp file: {targetPath}", ex);
                                try { if (File.Exists(actualTargetPath)) File.Delete(actualTargetPath); } catch { }
                                savedSuccessfully = false;
                            }
                        }
                        
                        if (savedSuccessfully)
                        {
                            Logger.LogInfo($"Optimized: {Path.GetFileName(result.FilePath)} -> {targetFileName}");
                            processedFiles.Add(targetPath);
                            currentIndex++;
                        }
                    }
                }
                
                // Close progress dialog
                if (progressDialog != null)
                {
                    if (App.Current is App currentApp && currentApp.MainWindow is MainWindow currentMainWindow)
                    {
                        await currentMainWindow.DispatcherQueue.EnqueueAsync(() =>
                        {
                            progressDialog.Hide();
                        });
                    }
                }
                
                Logger.LogInfo($"[BATCH_PROCESS] Batch processing complete");
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
        /// Get file extension for current image format setting
        /// </summary>
        private static string GetImageExtension()
        {
            var format = SettingsManager.Current.ImageFormat ?? "JPEG";
            return format.Equals("WebP", StringComparison.OrdinalIgnoreCase) ? ".webp" : ".jpg";
        }
        
        /// <summary>
        /// Check if file is a supported image format (jpg, jpeg, png, webp)
        /// </summary>
        private static bool IsImageFile(string filePath)
        {
            return filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Check if filename is a minitile file (minitile.jpg or minitile.webp)
        /// </summary>
        private static bool IsMinitileFile(string fileName)
        {
            var name = Path.GetFileName(fileName).ToLower();
            return name == "minitile.jpg" || name == "minitile.webp";
        }
        
        /// <summary>
        /// Get standard filename for category preview
        /// </summary>
        private static string GetCatprevFilename() => $"catprev{GetImageExtension()}";
        
        /// <summary>
        /// Get standard filename for category mini thumbnail
        /// </summary>
        private static string GetCatminiFilename() => $"catmini{GetImageExtension()}";
        
        /// <summary>
        /// Get standard filename for mod preview (index 0 = preview, 1+ = preview-01, preview-02, etc.)
        /// </summary>
        private static string GetPreviewFilename(int index) => index == 0 ? $"preview{GetImageExtension()}" : $"preview-{index:D2}{GetImageExtension()}";
        
        /// <summary>
        /// Get standard filename for mod minitile thumbnail
        /// </summary>
        private static string GetMinitileFilename() => $"minitile{GetImageExtension()}";
        
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
            try
            {
                // Crop the image using ImageSharp
                using (var cropped = sourceImage.Clone(ctx => ctx.Crop(cropRect)))
                {
                    await SaveImageAsync(cropped, targetPath, quality);
                    return true;
                }
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
        /// Process mod preview images in Standard mode - handles both with and without crop inspection
        /// </summary>
        private static async Task ProcessModPreviewImagesLiteAsync(string modDir, OptimizationContext context)
        {
            var processedFiles = new List<string>();
            var newFilesToCleanup = new List<string>(); // Track new files for cleanup on cancel
            
            try
            {
                Logger.LogInfo($"Processing mod preview images (Standard) in: {modDir}");
                
                // Create backup if enabled
                if (context.CreateBackups)
                {
                    var filesToBackup = Directory.GetFiles(modDir)
                        .Where(f =>
                        {
                            var fileName = Path.GetFileName(f).ToLower();
                            return fileName.StartsWith("preview") &&
                                   IsImageFile(f);
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
                
                // Track new files for cleanup on cancel
                newFilesToCleanup.AddRange(newFilesToProcess);
                
                // If no new files to process, nothing to do
                if (newFilesToProcess.Count == 0)
                {
                    Logger.LogInfo($"No new preview files to process in: {modDir}");
                    return;
                }
                
                // Calculate starting index for new files
                int startIndex = alreadyOptimizedFiles.Count;
                
                // Check how many files can be processed within the limit
                int availableSlots = AppConstants.MAX_PREVIEW_IMAGES - startIndex;
                if (availableSlots <= 0)
                {
                    Logger.LogWarning($"Preview image limit reached ({AppConstants.MAX_PREVIEW_IMAGES}), moving all new files to recycle bin");
                    foreach (var file in newFilesToProcess)
                    {
                        MoveToRecycleBin(file);
                    }
                    return;
                }
                
                // If there are more files than available slots, move excess to recycle bin
                if (newFilesToProcess.Count > availableSlots)
                {
                    var filesToKeep = newFilesToProcess.Take(availableSlots).ToList();
                    var filesToRecycle = newFilesToProcess.Skip(availableSlots).ToList();
                    
                    Logger.LogWarning($"Preview image limit ({AppConstants.MAX_PREVIEW_IMAGES}) exceeded, moving {filesToRecycle.Count} excess files to recycle bin");
                    foreach (var file in filesToRecycle)
                    {
                        MoveToRecycleBin(file);
                    }
                    
                    // Update the list to only include files we'll process
                    newFilesToProcess = filesToKeep;
                    newFilesToCleanup.Clear();
                    newFilesToCleanup.AddRange(newFilesToProcess);
                }
                
                // Check if minitile already exists
                var minitilePath = Path.Combine(modDir, GetMinitileFilename());
                bool minitileExists = File.Exists(minitilePath);
                
                // If using new files and KeepOriginals is enabled, create _original copies first
                if (context.KeepOriginals && newFilesToProcess.Count > 0)
                {
                    Logger.LogInfo("Creating original copies before optimization (Standard)");
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
                if (newFilesToProcess.Count > 0 && context.CreateMinitile)
                {
                    Logger.LogInfo($"[PREVIEW_LITE] Asking user to select minitile source from {newFilesToProcess.Count} files");
                    // Ask user to select minitile source from new files
                    selectedMinitileSource = await SelectMinitileSourceAsync(newFilesToProcess, modDir, context);
                    Logger.LogInfo($"[PREVIEW_LITE] Minitile source selection returned: {(selectedMinitileSource == null ? "NULL (skipped)" : Path.GetFileName(selectedMinitileSource))}");
                    
                    if (selectedMinitileSource == null)
                    {
                        // User clicked Skip - skip minitile but continue processing files
                        Logger.LogInfo($"[PREVIEW_LITE] User skipped minitile source selection, continuing with file processing");
                        skipMinitileOnly = true;
                    }
                }
                else if (!context.CreateMinitile)
                {
                    Logger.LogInfo("[PREVIEW_LITE] Minitile creation disabled - skipping minitile source selection");
                    skipMinitileOnly = true;
                }
                
                Logger.LogInfo($"[PREVIEW_LITE] Determining processing mode...");
                Logger.LogInfo($"[PREVIEW_LITE] AllowUIInteraction: {context.AllowUIInteraction}");
                Logger.LogInfo($"[PREVIEW_LITE] InspectAndEditEnabled: {context.InspectAndEditEnabled}");
                Logger.LogInfo($"[PREVIEW_LITE] newFilesToProcess.Count: {newFilesToProcess.Count}");
                Logger.LogInfo($"[PREVIEW_LITE] BatchCropInspectionRequested != null: {BatchCropInspectionRequested != null}");
                
                // Determine if we should use batch mode (list of files on the right side)
                bool useBatchMode = context.AllowUIInteraction && 
                                   context.InspectAndEditEnabled && 
                                   newFilesToProcess.Count > 1 &&
                                   BatchCropInspectionRequested != null;
                
                Logger.LogInfo($"[PREVIEW_LITE] Processing mode determined: {(useBatchMode ? "BATCH" : context.InspectAndEditEnabled && context.AllowUIInteraction ? "SINGLE" : "STANDARD")}");
                
                if (useBatchMode)
                {
                    Logger.LogInfo($"[PREVIEW_LITE] Starting BATCH MODE processing");
                    // BATCH MODE: Show all files in panel with list on the right
                    await ProcessPreviewFilesBatchAsync(modDir, newFilesToProcess, selectedMinitileSource, context, processedFiles, startIndex);
                    Logger.LogInfo($"[PREVIEW_LITE] BATCH MODE processing completed");
                }
                else if (context.InspectAndEditEnabled && context.AllowUIInteraction)
                {
                    Logger.LogInfo($"[PREVIEW_LITE] Starting SINGLE MODE processing");
                    // SINGLE MODE with inspection: Process one by one with crop panel
                    await ProcessPreviewFilesSingleAsync(modDir, newFilesToProcess, selectedMinitileSource, context, processedFiles, startIndex);
                    Logger.LogInfo($"[PREVIEW_LITE] SINGLE MODE processing completed");
                }
                else
                {
                    Logger.LogInfo($"[PREVIEW_LITE] Starting STANDARD MODE processing (no inspection)");
                    
                    // Show progress dialog for image conversion
                    Dialogs.ProgressDialog? progressDialog = null;
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    var progressTitle = SharedUtilities.GetTranslation(lang, "ProcessingImages") ?? "Processing Images";
                    var progressMessage = SharedUtilities.GetTranslation(lang, "ConvertingImages") ?? "Converting images, please wait...";
                    
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                    {
                        Logger.LogInfo($"[PREVIEW_LITE] Creating progress dialog");
                        progressDialog = new Dialogs.ProgressDialog(progressTitle, progressMessage);
                        progressDialog.XamlRoot = mainWindow.Content.XamlRoot;
                        
                        // Show dialog asynchronously
                        _ = progressDialog.ShowAsync();
                        
                        // Small delay to ensure dialog is visible
                        await Task.Delay(100);
                        Logger.LogInfo($"[PREVIEW_LITE] Progress dialog shown");
                    }
                    
                    try
                    {
                        // Process images on background thread to keep UI responsive
                        await Task.Run(() =>
                        {
                            // Standard mode without inspection - just convert to JPEG without resizing
                            for (int i = 0; i < newFilesToProcess.Count && (startIndex + i) < AppConstants.MAX_PREVIEW_IMAGES; i++)
                            {
                                var sourceFile = newFilesToProcess[i];
                                int targetIndex = startIndex + i;
                                string targetFileName = GetPreviewFilename(targetIndex);
                                var targetPath = Path.Combine(modDir, targetFileName);
                                
                                // Update progress
                                progressDialog?.UpdateProgress(i + 1, newFilesToProcess.Count, Path.GetFileName(sourceFile));
                                
                                bool savedSuccessfully = false;
                                try
                                {
                                    // Check if source and target are the same file - need to use temp file
                                    bool isSameFile = sourceFile.Equals(targetPath, StringComparison.OrdinalIgnoreCase);
                                    string actualTargetPath = isSameFile 
                                        ? Path.Combine(modDir, $"_temp_{Guid.NewGuid()}{GetImageExtension()}")
                                        : targetPath;
                                    
                                    using (var img = Image.Load<Rgba32>(sourceFile))
                                    {
                                        SaveImage(img, actualTargetPath, context.JpegQuality);
                                        savedSuccessfully = true;
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
                        });
                        
                        Logger.LogInfo($"[PREVIEW_LITE] STANDARD MODE processing completed");
                    }
                    finally
                    {
                        // Close progress dialog
                        if (progressDialog != null)
                        {
                            if (App.Current is App currentApp && currentApp.MainWindow is MainWindow currentMainWindow)
                            {
                                await currentMainWindow.DispatcherQueue.EnqueueAsync(() =>
                                {
                                    progressDialog.Hide();
                                });
                            }
                        }
                    }
                }
                
                // Generate minitile.jpg (600x722 thumbnail) from the correct source file
                // Find the actual position of selected minitile source in the processed files
                if (!skipMinitileOnly && !string.IsNullOrEmpty(selectedMinitileSource) && context.CreateMinitile)
                {
                    // First try to use the original file if it still exists
                    if (File.Exists(selectedMinitileSource))
                    {
                        await GenerateMinitileAsync(modDir, selectedMinitileSource, context);
                    }
                    else
                    {
                        // Find the index of selected source in the newFilesToProcess list
                        int selectedIndex = newFilesToProcess.FindIndex(f => f.Equals(selectedMinitileSource, StringComparison.OrdinalIgnoreCase));
                        if (selectedIndex >= 0)
                        {
                            // Calculate the actual target index for this file
                            int actualTargetIndex = startIndex + selectedIndex;
                            string minitileSourceFileName = GetPreviewFilename(actualTargetIndex);
                            string minitileSourceProcessed = Path.Combine(modDir, minitileSourceFileName);
                            if (File.Exists(minitileSourceProcessed))
                            {
                                await GenerateMinitileAsync(modDir, minitileSourceProcessed, context);
                            }
                            else
                            {
                                Logger.LogWarning($"Processed minitile source not found: {minitileSourceProcessed}");
                            }
                        }
                        else
                        {
                            Logger.LogWarning($"Selected minitile source not found in processed files list: {selectedMinitileSource}");
                        }
                    }
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
                
                Logger.LogInfo($"Mod preview processing (Standard) completed for: {modDir}");
            }
            catch (OperationCanceledException)
            {
                // Clean up only NEW files on cancellation (don't touch already optimized files)
                CleanupNewFilesOnly(newFilesToCleanup, processedFiles, modDir);
                throw; // Re-throw cancellation to stop optimization
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process mod preview images (Standard) in {modDir}", ex);
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
                               IsImageFile(f);
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
            
            // If UI interaction not allowed, use first file
            if (!context.AllowUIInteraction)
            {
                return availableFiles.FirstOrDefault();
            }
            
            // Multiple files - ask user to select
            if (MinitileSourceSelectionRequested != null)
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
                Logger.LogInfo($"Generating minitile for: {modDir} from source: {Path.GetFileName(previewPath)}");
                Logger.LogInfo($"Minitile generation context: InspectAndEditEnabled={context.InspectAndEditEnabled}, AllowUIInteraction={context.AllowUIInteraction}, AutoCreateModThumbnails={SettingsManager.Current.AutoCreateModThumbnails}");
                
                var minitilePath = Path.Combine(modDir, GetMinitileFilename());
                
                using (var img = Image.Load<Rgba32>(previewPath))
                {
                    // Get crop rectangle with optional inspection (minitile is a thumbnail)
                    var srcRect = await GetCropRectangleWithInspectionAsync(
                        img, 600, 722, context, GetMinitileFilename(), isProtected: false, isThumbnail: true);
                    
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
                    
                    // Generate minitile.jpg (600x722)
                    using (var minitile = img.Clone(ctx => ctx
                        .Crop(srcRect.Value)
                        .Resize(new ResizeOptions
                        {
                            Size = new Size(600, 722),
                            Mode = ResizeMode.Stretch,
                            Sampler = KnownResamplers.Bicubic
                        })))
                    {
                        SaveImage(minitile, minitilePath, context.JpegQuality);
                        Logger.LogInfo($"Minitile generated: {minitilePath}");
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
        private static Rectangle GetSuggestedCropRectangle(Image<Rgba32> image, int targetWidth, int targetHeight, CropStrategy strategy)
        {
            var cropType = ConvertCropStrategy(strategy);
            return ImageCropService.CalculateCropRectangle(image, targetWidth, targetHeight, cropType);
        }

        /// <summary>
        /// Get crop rectangle with optional inspection
        /// </summary>
        public static async Task<Rectangle?> GetCropRectangleWithInspectionAsync(
            Image<Rgba32> image, 
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
            // 2. Inspect&Edit is enabled (PreviewBeforeCrop setting) OR this is a critical thumbnail
            // 3. Critical thumbnails (catprev, catmini, minitile) always need crop inspection
            //    Exception: minitile only skipped if AutoCreateModThumbnails is enabled (automatic mode)
            bool isCriticalThumbnail = imageType.ToLower().Contains("catprev") || 
                                      imageType.ToLower().Contains("catmini") || 
                                      imageType.ToLower().Contains("minitile");
            bool isMinitile = imageType.ToLower().Contains("minitile");
            bool skipMinitileDueToAutoCreate = isMinitile && SettingsManager.Current.AutoCreateModThumbnails;
            
            bool needsInspection = context.AllowUIInteraction && 
                                  (context.InspectAndEditEnabled || isCriticalThumbnail) &&
                                  !skipMinitileDueToAutoCreate;

            Logger.LogInfo($"Crop inspection check for {imageType}: AllowUIInteraction={context.AllowUIInteraction}, InspectAndEditEnabled={context.InspectAndEditEnabled}, isCriticalThumbnail={isCriticalThumbnail}, isMinitile={isMinitile}, AutoCreateModThumbnails={SettingsManager.Current.AutoCreateModThumbnails}, skipMinitileDueToAutoCreate={skipMinitileDueToAutoCreate}, needsInspection={needsInspection}");

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
                
                // Check if we need sequential processing for crop inspection
                // Sequential processing is needed when InspectAndEditEnabled (preview before crop)
                bool needsSequentialProcessing = context.InspectAndEditEnabled;
                
                bool wasCancelled = false;
                
                if (needsSequentialProcessing)
                {
                    // Sequential processing - all images need user interaction
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
                JpegQuality = GetImageQuality(), // Use helper that returns 100 for WebP, user setting for JPEG
                ThreadCount = SettingsManager.Current.ImageOptimizerThreadCount,
                CreateBackups = SettingsManager.Current.ImageOptimizerCreateBackups,
                KeepOriginals = SettingsManager.Current.ImageOptimizerKeepOriginals,
                CropStrategy = cropStrategy,
                InspectAndEditEnabled = inspectAndEdit,
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
        public OptimizationTrigger Trigger { get; set; }
        public bool AllowUIInteraction { get; set; } = true; // Can show crop inspection UI
        public bool Reoptimize { get; set; } = false; // Re-optimize already optimized files
        public bool CreateMinitile { get; set; } = true; // Create minitile.jpg thumbnail
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
        /// Check if file is a supported image format (jpg, jpeg, png, webp)
        /// </summary>
        private static bool IsImageFile(string filePath)
        {
            return filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Check if a mod directory already has optimized files
        /// </summary>
        public static bool IsModAlreadyOptimized(string modDir)
        {
            // Check if minitile exists (either .jpg or .webp)
            var minitileJpg = Path.Combine(modDir, "minitile.jpg");
            var minitileWebp = Path.Combine(modDir, "minitile.webp");
            if (!File.Exists(minitileJpg) && !File.Exists(minitileWebp))
                return false;
            
            // Check if preview files have correct names (preview.jpg/webp, preview-01.jpg/webp, etc.)
            var previewFiles = Directory.GetFiles(modDir)
                .Where(f =>
                {
                    var fileName = Path.GetFileName(f).ToLower();
                    return fileName.StartsWith("preview") &&
                           IsImageFile(f);
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
            // Check if catprev exists (either .jpg or .webp)
            var catprevJpg = Path.Combine(categoryDir, "catprev.jpg");
            var catprevWebp = Path.Combine(categoryDir, "catprev.webp");
            if (!File.Exists(catprevJpg) && !File.Exists(catprevWebp))
                return false;
            
            // Check if catmini exists (either .jpg or .webp)
            var catminiJpg = Path.Combine(categoryDir, "catmini.jpg");
            var catminiWebp = Path.Combine(categoryDir, "catmini.webp");
            if (!File.Exists(catminiJpg) && !File.Exists(catminiWebp))
                return false;
            
            // Both catprev and catmini exist - category is fully optimized
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
                    // For categories: rename catprev to catprev_original (both formats)
                    // catmini will be overwritten by optimizer
                    var format = SettingsManager.Current.ImageFormat ?? "JPEG";
                    var extension = format.Equals("WebP", StringComparison.OrdinalIgnoreCase) ? ".webp" : ".jpg";
                    var catprevPath = Path.Combine(directory, $"catprev{extension}");
                    var catprevOriginalPath = Path.Combine(directory, $"catprev_original{extension}");
                    
                    if (File.Exists(catprevPath) && !File.Exists(catprevOriginalPath))
                    {
                        File.Move(catprevPath, catprevOriginalPath);
                        Logger.LogInfo($"Renamed catprev{extension} to catprev_original{extension} for reoptimization");
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
                                   IsImageFile(f);
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
