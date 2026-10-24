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
        /// Process mod preview images in Standard mode - handles both with and without crop inspection
        /// </summary>
        private static async Task ProcessModPreviewImagesLiteAsync(string modDir, OptimizationContext context)
        {
            var processedFiles = new List<string>();
            var newFilesToCleanup = new List<string>(); // Track new files for cleanup on cancel
            
            try
            {
                Logger.LogInfo($"Processing mod preview images (Standard) in: {modDir}");
                
                // FIRST: Convert existing files to new format if needed (format change)
                var currentExtension = SettingsManager.GetImageExtension();
                var oldExtension = currentExtension == ".webp" ? ".jpg" : ".webp";
                
                // Convert minitile if it exists in old format
                var oldMinitilePath = Path.Combine(modDir, $"minitile{oldExtension}");
                var newMinitilePath = Path.Combine(modDir, $"minitile{currentExtension}");
                if (File.Exists(oldMinitilePath) && !File.Exists(newMinitilePath))
                {
                    Logger.LogInfo($"Converting minitile{oldExtension} to minitile{currentExtension}");
                    using (var img = Image.Load<Rgba32>(oldMinitilePath))
                    {
                        SaveImage(img, newMinitilePath, context.JpegQuality);
                    }
                    
                    // Handle old file based on KeepOriginals
                    if (context.KeepOriginals)
                    {
                        var originalPath = Path.Combine(modDir, $"minitile_original{oldExtension}");
                        if (File.Exists(originalPath)) File.Delete(originalPath);
                        File.Move(oldMinitilePath, originalPath);
                        Logger.LogInfo($"Kept original: minitile{oldExtension} -> minitile_original{oldExtension}");
                    }
                    else
                    {
                        File.Delete(oldMinitilePath);
                        Logger.LogInfo($"Deleted original: minitile{oldExtension}");
                    }
                }
                
                // Convert minitile-wide if it exists in old format
                var oldMinitileWidePath = Path.Combine(modDir, $"minitile-wide{oldExtension}");
                var newMinitileWidePath = Path.Combine(modDir, $"minitile-wide{currentExtension}");
                if (File.Exists(oldMinitileWidePath) && !File.Exists(newMinitileWidePath))
                {
                    Logger.LogInfo($"Converting minitile-wide{oldExtension} to minitile-wide{currentExtension}");
                    using (var img = Image.Load<Rgba32>(oldMinitileWidePath))
                    {
                        SaveImage(img, newMinitileWidePath, context.JpegQuality);
                    }
                    
                    // Handle old file based on KeepOriginals
                    if (context.KeepOriginals)
                    {
                        var originalPath = Path.Combine(modDir, $"minitile-wide_original{oldExtension}");
                        if (File.Exists(originalPath)) File.Delete(originalPath);
                        File.Move(oldMinitileWidePath, originalPath);
                        Logger.LogInfo($"Kept original: minitile-wide{oldExtension} -> minitile-wide_original{oldExtension}");
                    }
                    else
                    {
                        File.Delete(oldMinitileWidePath);
                        Logger.LogInfo($"Deleted original: minitile-wide{oldExtension}");
                    }
                }
                
                // Convert preview files if they exist in old format
                var oldPreviewFiles = Directory.GetFiles(modDir)
                    .Where(f =>
                    {
                        var fileName = Path.GetFileName(f).ToLower();
                        return fileName.StartsWith("preview") && 
                               !fileName.Contains("_original") &&
                               f.EndsWith(oldExtension, StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();
                
                foreach (var oldFile in oldPreviewFiles)
                {
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(oldFile);
                    var newFile = Path.Combine(modDir, $"{fileNameWithoutExt}{currentExtension}");
                    
                    if (!File.Exists(newFile))
                    {
                        Logger.LogInfo($"Converting {Path.GetFileName(oldFile)} to {Path.GetFileName(newFile)}");
                        using (var img = Image.Load<Rgba32>(oldFile))
                        {
                            SaveImage(img, newFile, context.JpegQuality);
                        }
                        
                        // Handle old file based on KeepOriginals
                        if (context.KeepOriginals)
                        {
                            var originalPath = Path.Combine(modDir, $"{fileNameWithoutExt}_original{oldExtension}");
                            if (File.Exists(originalPath)) File.Delete(originalPath);
                            File.Move(oldFile, originalPath);
                            Logger.LogInfo($"Kept original: {Path.GetFileName(oldFile)} -> {Path.GetFileName(originalPath)}");
                        }
                        else
                        {
                            File.Delete(oldFile);
                            Logger.LogInfo($"Deleted original: {Path.GetFileName(oldFile)}");
                        }
                    }
                }
                
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
                        return fileName.StartsWith("preview") && IsImageFile(f);
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
                            // preview.jpg or preview-XX.jpg pattern (with dash and exactly 2 digits)
                            if (fileName == "preview") return true;
                            if (fileName.StartsWith("preview-") && fileName.Length == 10 && 
                                char.IsDigit(fileName[8]) && char.IsDigit(fileName[9])) return true; // preview-XX
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
                            if (fileName.StartsWith("preview-") && fileName.Length == 10 && 
                                char.IsDigit(fileName[8]) && char.IsDigit(fileName[9])) return false; // preview-XX
                            // Everything else is new (preview001, preview002, preview1, etc.)
                            return true;
                        })
                        .OrderBy(f => PreviewSortHelper.GetSortOrder(Path.GetFileName(f)))
                        .ToList();
                }
                
                Logger.LogInfo($"Already optimized: {alreadyOptimizedFiles.Count}, New to process: {newFilesToProcess.Count}");
                
                // Log file names for debugging
                if (alreadyOptimizedFiles.Count > 0)
                {
                    Logger.LogInfo($"Already optimized files: {string.Join(", ", alreadyOptimizedFiles.Select(f => Path.GetFileName(f)))}");
                }
                if (newFilesToProcess.Count > 0)
                {
                    Logger.LogInfo($"New files to process: {string.Join(", ", newFilesToProcess.Select(f => Path.GetFileName(f)))}");
                }
                
                // Track new files for cleanup on cancel
                newFilesToCleanup.AddRange(newFilesToProcess);
                
                // Check if we need to handle minitile even when no new files
                var minitileFilePath = Path.Combine(modDir, GetMinitileFilename());
                var minitileFileExists = File.Exists(minitileFilePath);
                var minitileWideFilePath = Path.Combine(modDir, GetMinitileWideFilename());
                var minitileWideFileExists = File.Exists(minitileWideFilePath);
                bool hasPreviewFiles = alreadyOptimizedFiles.Count > 0 || newFilesToProcess.Count > 0;
                
                // If no new files to process AND both minitile variants exist (or no preview files for minitile), nothing to do
                if (newFilesToProcess.Count == 0 && ((minitileFileExists && minitileWideFileExists) || !hasPreviewFiles))
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
                        RecycleBinHelper.MoveToRecycleBin(file);
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
                        RecycleBinHelper.MoveToRecycleBin(file);
                    }
                    
                    // Update the list to only include files we'll process
                    newFilesToProcess = filesToKeep;
                    newFilesToCleanup.Clear();
                    newFilesToCleanup.AddRange(newFilesToProcess);
                }
                
                // Check if minitile already exists
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
                // Show selection if: 1) there are new files to process, OR 2) minitile is missing
                string? selectedMinitileSource = null;
                bool skipMinitileOnly = false;
                List<string> filesToChooseFrom = new();
                
                Logger.LogInfo($"[PREVIEW_LITE] Minitile check - Path: {minitileFilePath}, Exists: {minitileFileExists}, WideExists: {minitileWideFileExists}");
                Logger.LogInfo($"[PREVIEW_LITE] Files count - newFilesToProcess: {newFilesToProcess.Count}, alreadyOptimizedFiles: {alreadyOptimizedFiles.Count}");
                Logger.LogInfo($"[PREVIEW_LITE] Context - CreateMinitile: {context.CreateMinitile}, AllowUIInteraction: {context.AllowUIInteraction}");
                
                if (context.CreateMinitile && (!minitileFileExists || !minitileWideFileExists || newFilesToProcess.Count > 0))
                {
                    // Get list of files to choose from - prefer new files, fallback to already optimized
                    filesToChooseFrom = newFilesToProcess.Count > 0 
                        ? newFilesToProcess 
                        : alreadyOptimizedFiles;
                    
                    Logger.LogInfo($"[PREVIEW_LITE] Files to choose from: {filesToChooseFrom.Count}");
                    if (filesToChooseFrom.Count > 0)
                    {
                        Logger.LogInfo($"[PREVIEW_LITE] Files list: {string.Join(", ", filesToChooseFrom.Select(f => Path.GetFileName(f)))}");
                    }
                    
                    if (filesToChooseFrom.Count > 0)
                    {
                        Logger.LogInfo($"[PREVIEW_LITE] Asking user to select minitile source from {filesToChooseFrom.Count} files (minitileExists: {minitileFileExists})");
                        selectedMinitileSource = await SelectMinitileSourceAsync(filesToChooseFrom, modDir, context);
                        Logger.LogInfo($"[PREVIEW_LITE] Minitile source selection returned: {(selectedMinitileSource == null ? "NULL (skipped)" : Path.GetFileName(selectedMinitileSource))}");
                        
                        if (selectedMinitileSource == null)
                        {
                            // User clicked Skip - skip minitile but continue processing files
                            Logger.LogInfo($"[PREVIEW_LITE] User skipped minitile source selection, continuing with file processing");
                            skipMinitileOnly = true;
                        }
                    }
                    else
                    {
                        Logger.LogInfo($"[PREVIEW_LITE] No files to choose from - skipping minitile selection");
                        skipMinitileOnly = true;
                    }
                }
                else if (!context.CreateMinitile)
                {
                    Logger.LogInfo("[PREVIEW_LITE] Minitile creation disabled - skipping minitile source selection");
                    skipMinitileOnly = true;
                }
                else if (minitileFileExists && minitileWideFileExists)
                {
                    Logger.LogInfo("[PREVIEW_LITE] Minitile already exists and no new files - skipping minitile source selection");
                    skipMinitileOnly = true;
                }
                
                Logger.LogInfo($"[PREVIEW_LITE] Determining processing mode...");
                Logger.LogInfo($"[PREVIEW_LITE] AllowUIInteraction: {context.AllowUIInteraction}");
                Logger.LogInfo($"[PREVIEW_LITE] InspectAndEditEnabled: {context.InspectAndEditEnabled}");
                Logger.LogInfo($"[PREVIEW_LITE] newFilesToProcess.Count: {newFilesToProcess.Count}");
                Logger.LogInfo($"[PREVIEW_LITE] BatchCropInspectionRequested != null: {BatchCropInspectionRequested != null}");
                Logger.LogInfo($"[PREVIEW_LITE] CreateMinitile: {context.CreateMinitile}");
                Logger.LogInfo($"[PREVIEW_LITE] selectedMinitileSource: {(selectedMinitileSource == null ? "NULL" : Path.GetFileName(selectedMinitileSource))}");
                Logger.LogInfo($"[PREVIEW_LITE] skipMinitileOnly: {skipMinitileOnly}");
                
                // Determine if we should use batch mode (list of files on the right side)
                bool useBatchMode = context.AllowUIInteraction && 
                                   context.InspectAndEditEnabled && 
                                   newFilesToProcess.Count > 1 &&
                                   BatchCropInspectionRequested != null;
                
                Logger.LogInfo($"[PREVIEW_LITE] Processing mode determined: {(useBatchMode ? "BATCH" : context.InspectAndEditEnabled && context.AllowUIInteraction ? "SINGLE" : "STANDARD")}");
                
                // Generate minitile BEFORE processing preview files:
                // Step 1: Select minitile source (already done above)
                // Step 2: Crop minitile (classic 600x722)
                // Step 3: Select minitile-wide source (inside GenerateMinitileAsync)
                // Step 4: Crop minitile-wide (1280x720)
                // Step 5: Process preview files (below)
                if (!skipMinitileOnly && !string.IsNullOrEmpty(selectedMinitileSource) && context.CreateMinitile)
                {
                    Logger.LogInfo($"[PREVIEW_LITE] Generating minitile BEFORE preview file processing");
                    await GenerateMinitileAsync(modDir, selectedMinitileSource, context, filesToChooseFrom);
                    Logger.LogInfo($"[PREVIEW_LITE] Minitile generation completed");
                }
                
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
                    
                    // Show progress dialog for image conversion (only if UI interaction is allowed)
                    Dialogs.ProgressDialog? progressDialog = null;
                    
                    // Show per-file progress dialog only for non-manual triggers (drag&drop, GameBanana)
                    // Manual optimization has its own progress bar - no popup needed
                    bool showPerFileDialog = context.AllowUIInteraction && 
                                           context.Trigger != OptimizationTrigger.Manual;
                    
                    if (showPerFileDialog)
                    {
                        var lang = SharedUtilities.LoadLanguageDictionary();
                        var progressTitle = SharedUtilities.GetTranslation(lang, "ProcessingImages") ?? "Processing Images";
                        var progressMessage = SharedUtilities.GetTranslation(lang, "ConvertingImages") ?? "Converting images, please wait...";
                        
                        if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                        {
                            Logger.LogInfo($"[PREVIEW_LITE] Creating progress dialog on UI thread");
                            
                            try
                            {
                                // Create and show dialog on UI thread
                                await mainWindow.DispatcherQueue.EnqueueAsync(() =>
                                {
                                    try
                                    {
                                        if (mainWindow.Content?.XamlRoot == null)
                                        {
                                            Logger.LogWarning("[PREVIEW_LITE] XamlRoot is null, cannot show progress dialog");
                                            return;
                                        }
                                        
                                        progressDialog = new Dialogs.ProgressDialog(progressTitle, progressMessage);
                                        progressDialog.XamlRoot = mainWindow.Content.XamlRoot;
                                        _ = progressDialog.ShowAsync();
                                        Logger.LogInfo($"[PREVIEW_LITE] Progress dialog created and shown");
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.LogError("[PREVIEW_LITE] Failed to create progress dialog", ex);
                                    }
                                });
                                
                                // Small delay to ensure dialog is visible
                                await Task.Delay(100);
                                Logger.LogInfo($"[PREVIEW_LITE] Progress dialog initialization complete");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError("[PREVIEW_LITE] Failed to initialize progress dialog", ex);
                            }
                        }
                    }
                    else
                    {
                        Logger.LogInfo($"[PREVIEW_LITE] Skipping progress dialog (AllowUIInteraction is false)");
                    }
                    
                    try
                    {
                        // Process images on background thread to keep UI responsive
                        await Task.Run(() =>
                        {
                            // Standard mode without inspection - just convert to target format without resizing
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
                                        ? Path.Combine(modDir, $"_temp_{Guid.NewGuid()}{SettingsManager.GetImageExtension()}")
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
                // Sequential processing is needed when:
                // 1. InspectAndEditEnabled - user wants to see/edit each crop
                // 2. AllowUIInteraction AND NOT AutoCreateModThumbnails - minitile source selection panel will be shown
                bool needsSequentialProcessing = context.InspectAndEditEnabled || 
                    (context.AllowUIInteraction && !SettingsManager.Current.AutoCreateModThumbnails);
                
                bool wasCancelled = false;
                
                if (needsSequentialProcessing)
                {
                    // Sequential processing - all images need user interaction
                    Logger.LogInfo("Using sequential processing (crop inspection enabled)");
                    
                    foreach (var categoryDir in categoryDirs)
                    {
                        if (_cancellationRequested) { wasCancelled = true; break; }
                        if (!Directory.Exists(categoryDir)) continue;
                        
                        // Process category preview without dialog (main progress bar handles progress)
                        await ProcessCategoryPreviewLiteAsync(categoryDir, context, showProgressDialog: false);
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
                            
                            try
                            {
                                await ProcessModPreviewImagesAsync(modDir, context);
                            }
                            catch (OperationCanceledException)
                            {
                                wasCancelled = true;
                                break;
                            }
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
                            
                            // Process category preview - no dialog in parallel mode (background thread)
                            ProcessCategoryPreviewLite(categoryDir, context, showProgressDialog: false);
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
    }
}



