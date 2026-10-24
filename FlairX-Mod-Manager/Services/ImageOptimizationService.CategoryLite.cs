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
        /// Process category preview in Standard mode - quality conversion without resizing or cropping
        /// </summary>
        private static async Task ProcessCategoryPreviewLiteAsync(string categoryDir, OptimizationContext context, bool showProgressDialog = true)
        {
            Dialogs.ProgressDialog? progressDialog = null;
            
            try
            {
                Logger.LogInfo($"Processing category preview (Standard) in: {categoryDir}");
                
                // FIRST: Convert existing catprev/catmini to new format if needed (format change)
                var currentExtension = SettingsManager.GetImageExtension();
                var oldExtension = currentExtension == ".webp" ? ".jpg" : ".webp";
                
                // Convert catprev if it exists in old format
                var oldCatprevPath = Path.Combine(categoryDir, $"catprev{oldExtension}");
                var newCatprevPath = Path.Combine(categoryDir, $"catprev{currentExtension}");
                if (File.Exists(oldCatprevPath) && !File.Exists(newCatprevPath))
                {
                    Logger.LogInfo($"Converting catprev{oldExtension} to catprev{currentExtension}");
                    using (var img = Image.Load<Rgba32>(oldCatprevPath))
                    {
                        SaveImage(img, newCatprevPath, context.JpegQuality);
                    }
                    
                    // Handle old file based on KeepOriginals
                    if (context.KeepOriginals)
                    {
                        var originalPath = Path.Combine(categoryDir, $"catprev_original{oldExtension}");
                        if (File.Exists(originalPath)) File.Delete(originalPath);
                        File.Move(oldCatprevPath, originalPath);
                        Logger.LogInfo($"Kept original: catprev{oldExtension} -> catprev_original{oldExtension}");
                    }
                    else
                    {
                        File.Delete(oldCatprevPath);
                        Logger.LogInfo($"Deleted original: catprev{oldExtension}");
                    }
                }
                
                // Convert catprev-wide if it exists in old format
                var oldCatprevWidePath = Path.Combine(categoryDir, $"catprev-wide{oldExtension}");
                var newCatprevWidePath = Path.Combine(categoryDir, $"catprev-wide{currentExtension}");
                if (File.Exists(oldCatprevWidePath) && !File.Exists(newCatprevWidePath))
                {
                    Logger.LogInfo($"Converting catprev-wide{oldExtension} to catprev-wide{currentExtension}");
                    using (var img = Image.Load<Rgba32>(oldCatprevWidePath))
                    {
                        SaveImage(img, newCatprevWidePath, context.JpegQuality);
                    }
                    
                    // Handle old file based on KeepOriginals
                    if (context.KeepOriginals)
                    {
                        var originalPath = Path.Combine(categoryDir, $"catprev-wide_original{oldExtension}");
                        if (File.Exists(originalPath)) File.Delete(originalPath);
                        File.Move(oldCatprevWidePath, originalPath);
                        Logger.LogInfo($"Kept original: catprev-wide{oldExtension} -> catprev-wide_original{oldExtension}");
                    }
                    else
                    {
                        File.Delete(oldCatprevWidePath);
                        Logger.LogInfo($"Deleted original: catprev-wide{oldExtension}");
                    }
                }
                
                // Convert catmini if it exists in old format
                var oldCatminiPath = Path.Combine(categoryDir, $"catmini{oldExtension}");
                var newCatminiPath = Path.Combine(categoryDir, $"catmini{currentExtension}");
                if (File.Exists(oldCatminiPath) && !File.Exists(newCatminiPath))
                {
                    Logger.LogInfo($"Converting catmini{oldExtension} to catmini{currentExtension}");
                    using (var img = Image.Load<Rgba32>(oldCatminiPath))
                    {
                        SaveImage(img, newCatminiPath, context.JpegQuality);
                    }
                    
                    // Handle old file based on KeepOriginals
                    if (context.KeepOriginals)
                    {
                        var originalPath = Path.Combine(categoryDir, $"catmini_original{oldExtension}");
                        if (File.Exists(originalPath)) File.Delete(originalPath);
                        File.Move(oldCatminiPath, originalPath);
                        Logger.LogInfo($"Kept original: catmini{oldExtension} -> catmini_original{oldExtension}");
                    }
                    else
                    {
                        File.Delete(oldCatminiPath);
                        Logger.LogInfo($"Deleted original: catmini{oldExtension}");
                    }
                }
                
                // Convert catmini-wide if it exists in old format
                var oldCatminiWidePath = Path.Combine(categoryDir, $"catmini-wide{oldExtension}");
                var newCatminiWidePath = Path.Combine(categoryDir, $"catmini-wide{currentExtension}");
                if (File.Exists(oldCatminiWidePath) && !File.Exists(newCatminiWidePath))
                {
                    Logger.LogInfo($"Converting catmini-wide{oldExtension} to catmini-wide{currentExtension}");
                    using (var img = Image.Load<Rgba32>(oldCatminiWidePath))
                    {
                        SaveImage(img, newCatminiWidePath, context.JpegQuality);
                    }
                    
                    // Handle old file based on KeepOriginals
                    if (context.KeepOriginals)
                    {
                        var originalPath = Path.Combine(categoryDir, $"catmini-wide_original{oldExtension}");
                        if (File.Exists(originalPath)) File.Delete(originalPath);
                        File.Move(oldCatminiWidePath, originalPath);
                        Logger.LogInfo($"Kept original: catmini-wide{oldExtension} -> catmini-wide_original{oldExtension}");
                    }
                    else
                    {
                        File.Delete(oldCatminiWidePath);
                        Logger.LogInfo($"Deleted original: catmini-wide{oldExtension}");
                    }
                }
                
                // Check if catprev exists but catmini is missing - only generate catmini
                var existingCatprevPath = Path.Combine(categoryDir, GetCatprevFilename());
                var existingCatminiPath = Path.Combine(categoryDir, GetCatminiFilename());
                var existingCatminiWidePath = Path.Combine(categoryDir, GetCatminiWideFilename());
                
                bool catminiMissing = !File.Exists(existingCatminiPath);
                bool catminiWideMissing = !File.Exists(existingCatminiWidePath);
                
                if (File.Exists(existingCatprevPath) && (catminiMissing || catminiWideMissing) && !context.Reoptimize)
                {
                    Logger.LogInfo($"catprev exists but catmini/catmini-wide missing - generating missing thumbnails (Lite)");
                    GenerateCatminiFromCatprevLite(categoryDir, context);
                    return;
                }
                
                // Process gbicon separately (PNG/WebP only - needs alpha channel)
                // When showProgressDialog=false (drag&drop), process BEFORE dialog
                // When showProgressDialog=true (manual optimization), process INSIDE Task.Run with dialog
                if (!showProgressDialog)
                {
                    ProcessGbIcon(categoryDir, context);
                }
                
                // Show progress dialog for image conversion (only if requested)
                if (showProgressDialog)
                {
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    var progressTitle = SharedUtilities.GetTranslation(lang, "ProcessingImages") ?? "Processing Images";
                    var progressMessage = SharedUtilities.GetTranslation(lang, "ConvertingImages") ?? "Converting images, please wait...";
                    
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                    {
                        Logger.LogInfo("Creating progress dialog for category preview conversion");
                        progressDialog = new Dialogs.ProgressDialog(progressTitle, progressMessage);
                        progressDialog.XamlRoot = mainWindow.Content.XamlRoot;
                        
                        // Show dialog asynchronously
                        _ = progressDialog.ShowAsync();
                        
                        // Small delay to ensure dialog is visible
                        await Task.Delay(100);
                        Logger.LogInfo("Progress dialog shown");
                    }
                }
                
                // Run conversion on background thread
                await Task.Run(() =>
                {
                    // Process gbicon inside dialog for manual optimization
                    if (showProgressDialog)
                    {
                        ProcessGbIcon(categoryDir, context);
                    }
                    
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
                    
                    Logger.LogInfo($"Found {allFiles.Count} preview files in {categoryDir}: {string.Join(", ", allFiles.Select(Path.GetFileName))}");
                    
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
                    
                    // Update progress
                    progressDialog?.UpdateProgress(1, 2, "Converting category preview...");
                    
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
                        ? Path.Combine(categoryDir, $"_temp_catprev_{Guid.NewGuid()}{SettingsManager.GetImageExtension()}")
                        : catprevPath;
                    
                    using (var img = Image.Load<Rgba32>(sourceFile))
                    {
                        // Save catprev without resizing (just convert to target format)
                        SaveImage(img, actualCatprevPath, context.JpegQuality);
                        Logger.LogInfo($"Converted (Lite): {Path.GetFileName(sourceFile)} -> {GetCatprevFilename()}");
                        
                        // Update progress
                        progressDialog?.UpdateProgress(2, 2, "Generating thumbnail...");
                        
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
                        
                        // Generate catmini-wide (1280x720) - wide cropped thumbnail
                        var catminiWidePath = Path.Combine(categoryDir, GetCatminiWideFilename());
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
                            Logger.LogInfo($"Generated {GetCatminiWideFilename()} (Standard)");
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
                });
                
                Logger.LogInfo($"Category preview processing (Standard) completed for: {categoryDir}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to process category preview (Standard) in {categoryDir}", ex);
            }
            finally
            {
                // Close progress dialog
                progressDialog?.Hide();
            }
        }
        
        /// <summary>
        /// Synchronous wrapper for ProcessCategoryPreviewLiteAsync
        /// </summary>
        private static void ProcessCategoryPreviewLite(string categoryDir, OptimizationContext context, bool showProgressDialog = true)
        {
            ProcessCategoryPreviewLiteAsync(categoryDir, context, showProgressDialog).GetAwaiter().GetResult();
        }
    }
}



