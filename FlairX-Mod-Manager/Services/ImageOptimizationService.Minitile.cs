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
        /// Select source file for minitile generation with optional UI selection
        /// </summary>
        private static async Task<string?> SelectMinitileSourceAsync(List<string> availableFiles, string modDir, OptimizationContext context, string? titleOverride = null, string? subtitleOverride = null)
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
            
            // Multiple files - ask user to select (with semaphore to ensure sequential UI)
            if (MinitileSourceSelectionRequested != null)
            {
                // Check if cancellation was requested before showing UI
                if (_cancellationRequested)
                {
                    throw new OperationCanceledException("Optimization cancelled by user");
                }
                
                // Wait for semaphore to ensure only one UI dialog at a time
                await _uiSemaphore.WaitAsync();
                try
                {
                    // Check cancellation again after acquiring semaphore
                    if (_cancellationRequested)
                    {
                        throw new OperationCanceledException("Optimization cancelled by user");
                    }
                    
                    Logger.LogInfo($"Requesting minitile source selection for: {modDir}");
                    var result = await MinitileSourceSelectionRequested(availableFiles, modDir, titleOverride, subtitleOverride);
                    
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
                finally
                {
                    // Always release semaphore
                    _uiSemaphore.Release();
                }
            }
            
            // Fallback to first file
            return availableFiles.FirstOrDefault();
        }

        /// <summary>
        /// Generate both minitile.jpg (600x722) and minitile-wide.jpg (1280x720) from preview images
        /// User can select different source files for each format
        /// </summary>
        private static async Task GenerateMinitileAsync(string modDir, string previewPath, OptimizationContext context, List<string>? originalFiles = null)
        {
            try
            {
                Logger.LogInfo($"Generating minitiles (classic + wide) for: {modDir}");
                Logger.LogInfo($"Minitile generation context: InspectAndEditEnabled={context.InspectAndEditEnabled}, AllowUIInteraction={context.AllowUIInteraction}, AutoCreateModThumbnails={SettingsManager.Current.AutoCreateModThumbnails}");
                
                // Prepare file list for source selection
                var filesToShow = originalFiles?.Count > 0
                    ? originalFiles
                    : Directory.GetFiles(modDir)
                        .Where(f => {
                            var name = Path.GetFileNameWithoutExtension(f).ToLower();
                            return IsImageFile(f) && (name == "preview" || System.Text.RegularExpressions.Regex.IsMatch(name, @"^preview-\d+$"));
                        })
                        .OrderBy(f => PreviewSortHelper.GetSortOrder(Path.GetFileName(f)))
                        .ToList();
                
                if (filesToShow.Count == 0)
                    filesToShow = new List<string> { previewPath };
                
                // ========== CLASSIC FORMAT (600x722) ==========
                var minitilePath = Path.Combine(modDir, GetMinitileFilename());
                var currentClassicPath = previewPath;
                
                while (true)
                {
                    using (var img = Image.Load<Rgba32>(currentClassicPath))
                    {
                        // Get crop rectangle with optional inspection for classic format
                        var srcRect = await GetCropRectangleWithInspectionAsync(
                            img, 600, 722, context, "minitile (classic 600x722)", isProtected: false, isThumbnail: true);
                        
                        if (srcRect == null)
                        {
                            Logger.LogInfo($"Deleted classic minitile generation for: {modDir}");
                            return; // User chose to delete - abort both formats
                        }
                        
                        // Check if user chose to skip optimization
                        if (srcRect.Value.X == -1 && srcRect.Value.Y == -1)
                        {
                            Logger.LogInfo($"Skipping classic minitile optimization for: {modDir}");
                            return; // Skip both formats
                        }
                        
                        // Check if user wants to choose a different file for classic
                        if (srcRect.Value.X == -2 && srcRect.Value.Y == -2)
                        {
                            Logger.LogInfo($"User wants to choose a different file for classic minitile in: {modDir}");
                            var lang = SharedUtilities.LoadLanguageDictionary();
                            var titleClassic = SharedUtilities.GetTranslation(lang, "MinitileSelection_Title_Classic") ?? "Select Minitile Source";
                            var subtitleClassic = SharedUtilities.GetTranslation(lang, "MinitileSelection_Subtitle") ?? "Choose which image to use for the thumbnail";
                            var newSource = await SelectMinitileSourceAsync(filesToShow, modDir, context, titleClassic, subtitleClassic);
                            if (string.IsNullOrEmpty(newSource))
                                return; // Skipped or stopped
                            currentClassicPath = newSource;
                            continue; // Loop back with new file
                        }
                        
                        // Generate classic minitile (600x722)
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
                            Logger.LogInfo($"Classic minitile generated: {minitilePath}");
                            ImageCacheManager.InvalidateImage(minitilePath);
                        }
                        break; // Exit classic loop
                    }
                }
                
                // ========== SELECT SOURCE FOR WIDE FORMAT ==========
                // isAutoMode: true only when AutoCreateModThumbnails is explicitly enabled
                // When disabled, always show source selection and crop inspection (if UI available)
                bool isAutoMode = SettingsManager.Current.AutoCreateModThumbnails;
                var currentWidePath = previewPath; // default: same source as classic
                
                if (!isAutoMode && context.AllowUIInteraction && filesToShow.Count > 1)
                {
                    Logger.LogInfo($"Asking user to select source for minitile-wide in: {modDir}");
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    var titleWide = SharedUtilities.GetTranslation(lang, "MinitileSelection_Title_Wide") ?? "Select Minitile-Wide Source";
                    var subtitleWide = SharedUtilities.GetTranslation(lang, "MinitileSelection_Subtitle_Wide") ?? "Choose which image to use for the wide thumbnail (1280×720)";
                    var wideSource = await SelectMinitileSourceAsync(filesToShow, modDir, context, titleWide, subtitleWide);
                    if (wideSource == null)
                    {
                        Logger.LogInfo($"User skipped minitile-wide source selection for: {modDir}");
                        return; // User skipped or stopped
                    }
                    currentWidePath = wideSource;
                }
                
                // ========== WIDE FORMAT (1280x720) ==========
                var minitileWidePath = Path.Combine(modDir, GetMinitileWideFilename());
                
                if (isAutoMode && filesToShow.Count > 1)
                {
                    // Pick the file with aspect ratio closest to 16:9 (1.7778) as source for wide format
                    const double target169 = 16.0 / 9.0;
                    var bestFile = filesToShow
                        .Select(f => {
                            try {
                                using var img = Image.Load<Rgba32>(f);
                                return (path: f, diff: Math.Abs((double)img.Width / img.Height - target169));
                            } catch { return (path: f, diff: double.MaxValue); }
                        })
                        .OrderBy(x => x.diff)
                        .First().path;
                    
                    if (bestFile != previewPath)
                    {
                        currentWidePath = bestFile;
                        Logger.LogInfo($"Auto mode: selected closest-to-16:9 source for wide minitile: {Path.GetFileName(bestFile)}");
                    }
                }
                
                while (true)
                {
                    using (var img = Image.Load<Rgba32>(currentWidePath))
                    {
                        // In auto mode, force no inspection for wide - just use auto-crop
                        // Otherwise respect the context's InspectAndEditEnabled setting
                        var wideContext = isAutoMode
                            ? new OptimizationContext
                            {
                                Mode = context.Mode,
                                JpegQuality = context.JpegQuality,
                                ThreadCount = context.ThreadCount,
                                CreateBackups = context.CreateBackups,
                                KeepOriginals = context.KeepOriginals,
                                CropStrategy = context.CropStrategy,
                                InspectAndEditEnabled = false, // force no inspection in auto mode
                                Trigger = context.Trigger,
                                AllowUIInteraction = context.AllowUIInteraction,
                                Reoptimize = context.Reoptimize,
                                CreateMinitile = context.CreateMinitile
                            }
                            : context; // respect InspectAndEditEnabled when not in auto mode
                        
                        // Get crop rectangle with optional inspection for wide format
                        var srcRect = await GetCropRectangleWithInspectionAsync(
                            img, 1280, 720, wideContext, "minitile-wide (1280x720)", isProtected: false, isThumbnail: true);
                        
                        if (srcRect == null)
                        {
                            Logger.LogInfo($"Deleted wide minitile generation for: {modDir}");
                            return; // User chose to delete
                        }
                        
                        // Check if user chose to skip wide format
                        if (srcRect.Value.X == -1 && srcRect.Value.Y == -1)
                        {
                            Logger.LogInfo($"Skipping wide minitile optimization for: {modDir}");
                            return;
                        }
                        
                        // Check if user wants to choose a different file for wide
                        if (srcRect.Value.X == -2 && srcRect.Value.Y == -2)
                        {
                            Logger.LogInfo($"User wants to choose a different file for wide minitile in: {modDir}");
                            var lang = SharedUtilities.LoadLanguageDictionary();
                            var titleWide = SharedUtilities.GetTranslation(lang, "MinitileSelection_Title_Wide") ?? "Select Minitile-Wide Source";
                            var subtitleWide = SharedUtilities.GetTranslation(lang, "MinitileSelection_Subtitle_Wide") ?? "Choose which image to use for the wide thumbnail (1280×720)";
                            var newSource = await SelectMinitileSourceAsync(filesToShow, modDir, context, titleWide, subtitleWide);
                            if (string.IsNullOrEmpty(newSource))
                                return; // Skipped or stopped
                            currentWidePath = newSource;
                            continue; // Loop back with new file
                        }
                        
                        // Generate wide minitile (1280x720)
                        using (var minitileWide = img.Clone(ctx => ctx
                            .Crop(srcRect.Value)
                            .Resize(new ResizeOptions
                            {
                                Size = new Size(1280, 720),
                                Mode = ResizeMode.Stretch,
                                Sampler = KnownResamplers.Bicubic
                            })))
                        {
                            SaveImage(minitileWide, minitileWidePath, context.JpegQuality);
                            Logger.LogInfo($"Wide minitile generated: {minitileWidePath}");
                            ImageCacheManager.InvalidateImage(minitileWidePath);
                        }
                        break; // Exit wide loop
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation to stop optimization
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to generate minitiles for {modDir}", ex);
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
            // 3. Critical thumbnails (catprev, catmini, minitile, minitile-wide) always need crop inspection
            //    Exception: minitile only skipped if AutoCreateModThumbnails is enabled (automatic mode)
            bool isCriticalThumbnail = imageType.ToLower().Contains("catprev") || 
                                      imageType.ToLower().Contains("catmini");
            bool isMinitile = imageType.ToLower().Contains("minitile");
            bool skipMinitileDueToAutoCreate = isMinitile && SettingsManager.Current.AutoCreateModThumbnails;
            
            bool needsInspection = context.AllowUIInteraction && 
                                  (context.InspectAndEditEnabled || isCriticalThumbnail || isMinitile) &&
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
                                
                            case CropInspectionAction.ChooseDifferentFile:
                                Logger.LogInfo($"User wants to choose a different file for {imageType}");
                                return new Rectangle(-2, -2, -2, -2); // Special marker for "choose different file"
                                
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
    }
}



