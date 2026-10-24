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
                        ? Path.Combine(modDir, $"_temp_{Guid.NewGuid()}{SettingsManager.GetImageExtension()}")
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
                                    ? Path.Combine(modDir, $"_temp_minitile_{Guid.NewGuid()}{SettingsManager.GetImageExtension()}")
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
                        ? Path.Combine(modDir, $"_temp_preview_{Guid.NewGuid()}{SettingsManager.GetImageExtension()}")
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
                        // Delete the file
                        try
                        {
                            if (File.Exists(result.FilePath))
                            {
                                File.Delete(result.FilePath);
                                Logger.LogInfo($"Deleted file marked for deletion: {Path.GetFileName(result.FilePath)}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to delete file: {result.FilePath}", ex);
                        }
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
                            ? Path.Combine(modDir, $"_temp_{Guid.NewGuid()}{SettingsManager.GetImageExtension()}")
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
                
                // Check if minitile exists
                var minitilePath = Path.Combine(modDir, GetMinitileFilename());
                var minitileExists = File.Exists(minitilePath);
                
                // FIRST: Generate minitile.jpg from selected preview (BEFORE renaming)
                // Generate if minitile doesn't exist
                if (!minitileExists)
                {
                    var minitileSource = await SelectMinitileSourceAsync(previewFiles, modDir, context);
                    if (!string.IsNullOrEmpty(minitileSource))
                    {
                        Logger.LogInfo($"Generating minitile from: {Path.GetFileName(minitileSource)}");
                        await GenerateMinitileAsync(modDir, minitileSource, context, previewFiles);
                    }
                }
                else
                {
                    Logger.LogInfo($"Minitile already exists, skipping generation: {minitilePath}");
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
    }
}



