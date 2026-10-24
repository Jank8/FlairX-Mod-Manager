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
        Cancel,     // Cancel entire optimization process
        ChooseDifferentFile // Go back to file selection panel
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
    public delegate Task<MinitileSourceResult?> MinitileSourceSelectionHandler(List<string> availableFiles, string modDirectory, string? titleOverride = null, string? subtitleOverride = null);

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
    public static partial class ImageOptimizationService
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
        
        // Semaphore to ensure only one UI dialog at a time (for minitile selection)
        private static readonly SemaphoreSlim _uiSemaphore = new SemaphoreSlim(1, 1);
        
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
                JpegQuality = GetImageQuality(), // Returns WebP quality for WebP, JPEG quality for JPEG
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
        /// Check if a mod directory already has optimized files IN CURRENT FORMAT
        /// </summary>
        public static bool IsModAlreadyOptimized(string modDir)
        {
            // Get current format extension
            var currentExtension = SettingsManager.GetImageExtension();
            
            // Check if minitile exists in current format
            var minitilePath = Path.Combine(modDir, $"minitile{currentExtension}");
            if (!File.Exists(minitilePath))
                return false;
            
            // Also check if minitile-wide exists (wide format)
            var minitileWidePath = Path.Combine(modDir, $"minitile-wide{currentExtension}");
            if (!File.Exists(minitileWidePath))
                return false;  // Wide minitile missing - needs regeneration
            
            // Check if preview files exist in current format
            var previewFiles = Directory.GetFiles(modDir)
                .Where(f =>
                {
                    var fileName = Path.GetFileName(f).ToLower();
                    return fileName.StartsWith("preview") &&
                           f.EndsWith(currentExtension, StringComparison.OrdinalIgnoreCase);
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
        /// Check if a category directory already has optimized files IN CURRENT FORMAT
        /// </summary>
        public static bool IsCategoryAlreadyOptimized(string categoryDir)
        {
            // Get current format extension
            var currentExtension = SettingsManager.GetImageExtension();
            
            // Check if catprev exists in current format
            var catprevPath = Path.Combine(categoryDir, $"catprev{currentExtension}");
            if (!File.Exists(catprevPath))
                return false;
            
            // Check if catmini exists in current format
            var catminiPath = Path.Combine(categoryDir, $"catmini{currentExtension}");
            if (!File.Exists(catminiPath))
                return false;
            
            // Check if catprev-wide exists in current format
            var catprevWidePath = Path.Combine(categoryDir, $"catprev-wide{currentExtension}");
            if (!File.Exists(catprevWidePath))
                return false;

            // Check if catmini-wide exists in current format
            var catminiWidePath = Path.Combine(categoryDir, $"catmini-wide{currentExtension}");
            if (!File.Exists(catminiWidePath))
                return false;
            
            // Both catprev and catmini exist in current format - category is fully optimized
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




