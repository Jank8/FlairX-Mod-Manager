using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Microsoft.UI;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.UI.Xaml.Data;
using Windows.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;
using FlairX_Mod_Manager.Models;
using Windows.Graphics.Imaging;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModGridPage
    {

        // Drag & Drop Event Handlers
        private async Task HandleCategoryImageDrop(Button btn, ModTile tile, List<StorageFile> imageFiles)
        {
            var dropOverlay = FindVisualChild<Border>(btn, "DragDropOverlay");
            var dropIcon = dropOverlay != null ? FindVisualChild<FontIcon>(dropOverlay, "DropIcon") : null;
            var dropText = dropOverlay != null ? FindVisualChild<TextBlock>(dropOverlay, "DropText") : null;
            var lang = SharedUtilities.LoadLanguageDictionary();
            
            try
            {
                // For categories, only allow 1 image
                if (imageFiles.Count > 1)
                {
                    Logger.LogInfo($"Multiple images dropped on category ({imageFiles.Count}), rejecting");
                    if (dropText != null)
                    {
                        dropText.Text = string.Format(SharedUtilities.GetTranslation(lang, "DragDrop_LimitReached"), 1);
                    }
                    if (dropIcon != null)
                    {
                        dropIcon.Glyph = "\uE711"; // Cross/Cancel icon
                        dropIcon.FontSize = 48;
                    }
                    
                    await Task.Delay(2000);
                    
                    // Fade out overlay
                    if (dropOverlay != null)
                    {
                        var fadeOut = new DoubleAnimation
                        {
                            From = 1,
                            To = 0,
                            Duration = new Duration(TimeSpan.FromMilliseconds(300))
                        };
                        var storyboard = new Storyboard();
                        Storyboard.SetTarget(fadeOut, dropOverlay);
                        Storyboard.SetTargetProperty(fadeOut, "Opacity");
                        storyboard.Children.Add(fadeOut);
                        storyboard.Completed += (s, args) =>
                        {
                            dropOverlay.Visibility = Visibility.Collapsed;
                            dropOverlay.Opacity = 0;
                            if (dropText != null) dropText.Text = SharedUtilities.GetTranslation(lang, "DragDrop_DropImagesHere");
                            if (dropIcon != null)
                            {
                                dropIcon.Glyph = "\uE896";
                                dropIcon.FontSize = 48;
                            }
                        };
                        storyboard.Begin();
                    }
                    return;
                }
                
                var imageFile = imageFiles[0];
                Logger.LogInfo($"Processing category image drop: {imageFile.Name}");
                Logger.LogInfo($"Category tile.Directory: {tile.Directory}");
                Logger.LogInfo($"Category tile.Name: {tile.Name}");
                
                // Get category folder path
                var gameTag = SettingsManager.CurrentSelectedGame;
                var gameModsPath = AppConstants.GameConfig.GetModsPath(gameTag);
                string modsPath = PathManager.GetAbsolutePath(gameModsPath);
                
                Logger.LogInfo($"Mods path: {modsPath}");
                
                // For categories, the Directory is just the category name, not a full path
                string categoryFolderPath = Path.Combine(modsPath, tile.Directory);
                
                Logger.LogInfo($"Category folder path: {categoryFolderPath}");
                Logger.LogInfo($"Category folder exists: {Directory.Exists(categoryFolderPath)}");
                
                if (!Directory.Exists(categoryFolderPath))
                {
                    Logger.LogError($"Category folder does not exist: {categoryFolderPath}");
                    if (dropOverlay != null)
                    {
                        dropOverlay.Visibility = Visibility.Collapsed;
                        dropOverlay.Opacity = 0;
                    }
                    return;
                }
                
                // Show processing state
                if (dropText != null)
                {
                    dropText.Text = string.Format(SharedUtilities.GetTranslation(lang, "DragDrop_Copying"), 1);
                }
                if (dropIcon != null)
                {
                    dropIcon.Glyph = "\uE895"; // Sync icon
                }
                
                // Copy image to category folder as preview (overwriting if exists)
                string targetPath = Path.Combine(categoryFolderPath, "preview" + SettingsManager.GetImageExtension());
                
                // Delete existing preview files (preview.*, catprev.*, catmini.*)
                var filesToDelete = new List<string>();
                
                // Add preview.* files
                filesToDelete.AddRange(Directory.GetFiles(categoryFolderPath, "preview.*")
                    .Where(f => 
                    {
                        var ext = Path.GetExtension(f).ToLower();
                        return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp";
                    }));
                
                // Add catprev and catmini if they exist (both formats, classic and wide)
                var catprevJpgPath = Path.Combine(categoryFolderPath, "catprev.jpg");
                var catprevWebpPath = Path.Combine(categoryFolderPath, "catprev.webp");
                var catminiJpgPath = Path.Combine(categoryFolderPath, "catmini.jpg");
                var catminiWebpPath = Path.Combine(categoryFolderPath, "catmini.webp");
                var catprevWideJpgPath = Path.Combine(categoryFolderPath, "catprev-wide.jpg");
                var catprevWideWebpPath = Path.Combine(categoryFolderPath, "catprev-wide.webp");
                var catminiWideJpgPath = Path.Combine(categoryFolderPath, "catmini-wide.jpg");
                var catminiWideWebpPath = Path.Combine(categoryFolderPath, "catmini-wide.webp");
                
                if (File.Exists(catprevJpgPath)) filesToDelete.Add(catprevJpgPath);
                if (File.Exists(catprevWebpPath)) filesToDelete.Add(catprevWebpPath);
                if (File.Exists(catminiJpgPath)) filesToDelete.Add(catminiJpgPath);
                if (File.Exists(catminiWebpPath)) filesToDelete.Add(catminiWebpPath);
                if (File.Exists(catprevWideJpgPath)) filesToDelete.Add(catprevWideJpgPath);
                if (File.Exists(catprevWideWebpPath)) filesToDelete.Add(catprevWideWebpPath);
                if (File.Exists(catminiWideJpgPath)) filesToDelete.Add(catminiWideJpgPath);
                if (File.Exists(catminiWideWebpPath)) filesToDelete.Add(catminiWideWebpPath);
                
                foreach (var existingFile in filesToDelete)
                {
                    try
                    {
                        File.Delete(existingFile);
                        Logger.LogInfo($"Deleted existing file: {existingFile}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to delete existing file: {existingFile}", ex);
                    }
                }
                
                // Copy new image using File.Copy (more reliable than StorageFolder)
                File.Copy(imageFile.Path, targetPath, overwrite: true);
                Logger.LogInfo($"Copied image to: {targetPath}");
                
                // Run image optimizer in category mode using DragDropCategoryMode setting
                var context = Services.ImageOptimizationService.GetOptimizationContext(
                    Services.OptimizationTrigger.DragDropCategory);
                Logger.LogInfo($"Running image optimizer in category mode (Mode: {context.Mode}, InspectAndEdit: {context.InspectAndEditEnabled})");
                bool wasCancelled = false;
                
                try
                {
                    Logger.LogInfo($"Calling ProcessCategoryPreviewAsync for: {categoryFolderPath}");
                    await Services.ImageOptimizationService.ProcessCategoryPreviewAsync(categoryFolderPath, context);
                    Logger.LogInfo("Category optimization complete");
                    
                    // Delete preview.jpg after optimization (keep only catprev.jpg and catmini.jpg) - if KeepOriginals is disabled
                    if (!context.KeepOriginals && File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                        Logger.LogInfo($"Deleted original preview.jpg after optimization");
                    }
                    else if (context.KeepOriginals)
                    {
                        Logger.LogInfo($"Keeping original preview.jpg (KeepOriginals enabled)");
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.LogInfo("Category optimization cancelled by user");
                    wasCancelled = true;
                    
                    // Cleanup: delete copied file
                    try
                    {
                        if (File.Exists(targetPath))
                        {
                            File.Delete(targetPath);
                            Logger.LogInfo($"Cleanup: deleted {Path.GetFileName(targetPath)}");
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        Logger.LogWarning($"Cleanup: failed to delete {Path.GetFileName(targetPath)}: {cleanupEx.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Category optimization failed", ex);
                }
                
                // Show result based on whether cancelled or successful
                if (dropText != null)
                {
                    dropText.Text = wasCancelled 
                        ? (SharedUtilities.GetTranslation(lang, "DragDrop_Cancelled") ?? "Cancelled")
                        : SharedUtilities.GetTranslation(lang, "DragDrop_ImagesAdded");
                }
                if (dropIcon != null)
                {
                    dropIcon.Glyph = wasCancelled ? "\uE711" : "\uE73E"; // Cross or Checkmark
                    dropIcon.FontSize = 48;
                }
                
                await Task.Delay(2000);
                
                // Fade out overlay
                if (dropOverlay != null)
                {
                    var fadeOut = new DoubleAnimation
                    {
                        From = 1,
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(300))
                    };
                    var storyboard = new Storyboard();
                    Storyboard.SetTarget(fadeOut, dropOverlay);
                    Storyboard.SetTargetProperty(fadeOut, "Opacity");
                    storyboard.Children.Add(fadeOut);
                    storyboard.Completed += (s, args) =>
                    {
                        dropOverlay.Visibility = Visibility.Collapsed;
                        dropOverlay.Opacity = 0;
                        if (dropText != null) dropText.Text = SharedUtilities.GetTranslation(lang, "DragDrop_DropImagesHere");
                        if (dropIcon != null) dropIcon.Glyph = "\uE896";
                    };
                    storyboard.Begin();
                }
                
                // Reload the category image
                await Task.Delay(500); // Wait for animation
                DispatcherQueue.TryEnqueue(() =>
                {
                    tile.ImageSource = null;
                    LoadVisibleImages();
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in category drag & drop handler", ex);
                if (dropOverlay != null)
                {
                    dropOverlay.Visibility = Visibility.Collapsed;
                    dropOverlay.Opacity = 0;
                }
            }
        }
        
        private OptimizationMode ParseOptimizationMode(string modeString)
        {
            if (Enum.TryParse<OptimizationMode>(modeString, out var mode))
                return mode;
            return OptimizationMode.Standard;
        }
        

        private void Tile_DragEnter(object sender, DragEventArgs e)
        {
            Logger.LogInfo("Tile_DragEnter called");
            
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                Logger.LogInfo("Contains storage items");
                e.AcceptedOperation = DataPackageOperation.Copy;
                
                if (sender is Button btn && btn.DataContext is ModTile tile)
                {
                    Logger.LogInfo($"Drag enter on {(tile.IsCategory ? "category" : "mod")}: {tile.Name}");
                    var overlay = FindVisualChild<Border>(btn, "DragDropOverlay");
                    if (overlay != null)
                    {
                        Logger.LogInfo("Found overlay, showing it");
                        overlay.Visibility = Visibility.Visible;
                        
                        // Update text for category vs mod
                        var dropText = FindVisualChild<TextBlock>(overlay, "DropText");
                        if (dropText != null && tile.IsCategory)
                        {
                            var lang = SharedUtilities.LoadLanguageDictionary();
                            dropText.Text = SharedUtilities.GetTranslation(lang, "DragDrop_DropImagesHere");
                        }
                        
                        var fadeIn = new DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = new Duration(TimeSpan.FromMilliseconds(200))
                        };
                        var storyboard = new Storyboard();
                        Storyboard.SetTarget(fadeIn, overlay);
                        Storyboard.SetTargetProperty(fadeIn, "Opacity");
                        storyboard.Children.Add(fadeIn);
                        storyboard.Begin();
                    }
                    else
                    {
                        Logger.LogError("Overlay not found!");
                    }
                }
                else
                {
                    Logger.LogInfo($"Sender type: {sender?.GetType().Name}");
                }
            }
            else
            {
                Logger.LogInfo("Does not contain storage items");
            }
        }
        
        private void Tile_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Button btn)
            {
                var overlay = FindVisualChild<Border>(btn, "DragDropOverlay");
                if (overlay != null && overlay.Visibility == Visibility.Visible)
                {
                    var fadeOut = new DoubleAnimation
                    {
                        From = overlay.Opacity,
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(200))
                    };
                    var storyboard = new Storyboard();
                    Storyboard.SetTarget(fadeOut, overlay);
                    Storyboard.SetTargetProperty(fadeOut, "Opacity");
                    storyboard.Children.Add(fadeOut);
                    storyboard.Completed += (s, args) => overlay.Visibility = Visibility.Collapsed;
                    storyboard.Begin();
                }
            }
        }
        
        private void Tile_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
        }
        
        private async void Tile_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (sender is not Button btn || btn.DataContext is not ModTile tile)
                    return;

                if (!e.DataView.Contains(StandardDataFormats.StorageItems))
                    return;

                Border? dropOverlay = null;
                FontIcon? dropIcon = null;
                TextBlock? dropText = null;

                try
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    var imageFiles = items.OfType<StorageFile>()
                        .Where(f => f.FileType.ToLower() is ".jpg" or ".jpeg" or ".png")
                        .ToList();

                    if (!imageFiles.Any())
                    {
                        // Hide overlay
                        var overlay = FindVisualChild<Border>(btn, "DragDropOverlay");
                        if (overlay != null)
                        {
                            overlay.Visibility = Visibility.Collapsed;
                            overlay.Opacity = 0;
                        }
                        return;
                    }

                    Logger.LogInfo($"Drop received: {imageFiles.Count} image files");

                    // Handle category drop differently
                    if (tile.IsCategory)
                    {
                        await HandleCategoryImageDrop(btn, tile, imageFiles);
                        return;
                    }

                    // Get UI elements
                    dropOverlay = FindVisualChild<Border>(btn, "DragDropOverlay");
                    dropIcon = dropOverlay != null ? FindVisualChild<FontIcon>(dropOverlay, "DropIcon") : null;
                    dropText = dropOverlay != null ? FindVisualChild<TextBlock>(dropOverlay, "DropText") : null;

                    // Get mod folder path first to check existing images
                    var gameTag = SettingsManager.CurrentSelectedGame;
                    var gameModsPath = AppConstants.GameConfig.GetModsPath(gameTag);
                    string modsPath = PathManager.GetAbsolutePath(gameModsPath);

                    string? modFolderPath = FindModFolderPath(modsPath, tile.Directory);
                    if (string.IsNullOrEmpty(modFolderPath))
                    {
                        Logger.LogError($"Could not find mod folder for: {tile.Directory}");
                        if (dropOverlay != null)
                        {
                            dropOverlay.Visibility = Visibility.Collapsed;
                            dropOverlay.Opacity = 0;
                        }
                        return;
                    }

                    // Check existing preview images count
                    const int MAX_IMAGES = AppConstants.MAX_PREVIEW_IMAGES;
                    var existingPreviews = Directory.Exists(modFolderPath)
                        ? Directory.GetFiles(modFolderPath, "preview*.*")
                            .Where(f =>
                            {
                                var ext = Path.GetExtension(f).ToLower();
                                return ext == ".jpg" || ext == ".jpeg" || ext == ".png";
                            })
                            .Count()
                        : 0;

                    Logger.LogInfo($"Existing preview images: {existingPreviews}/{MAX_IMAGES}");

                    // Load language dictionary once for all feedback messages
                    var lang = SharedUtilities.LoadLanguageDictionary();

                    // Check if limit reached
                    if (existingPreviews >= MAX_IMAGES)
                    {
                        Logger.LogInfo("Image limit reached, showing error feedback");
                        if (dropText != null)
                        {
                            dropText.Text = string.Format(SharedUtilities.GetTranslation(lang, "DragDrop_LimitReached"), AppConstants.MAX_PREVIEW_IMAGES);
                        }
                        if (dropIcon != null)
                        {
                            dropIcon.Glyph = "\uE711"; // Cross/Cancel icon
                            dropIcon.FontSize = 48; // Same size as checkmark
                        }

                        await Task.Delay(2000);

                        // Fade out overlay
                        if (dropOverlay != null)
                        {
                            var fadeOut = new DoubleAnimation
                            {
                                From = 1,
                                To = 0,
                                Duration = new Duration(TimeSpan.FromMilliseconds(300))
                            };
                            var storyboard = new Storyboard();
                            Storyboard.SetTarget(fadeOut, dropOverlay);
                            Storyboard.SetTargetProperty(fadeOut, "Opacity");
                            storyboard.Children.Add(fadeOut);
                            storyboard.Completed += (s, args) =>
                            {
                                dropOverlay.Visibility = Visibility.Collapsed;
                                dropOverlay.Opacity = 0;
                                if (dropText != null)
                                {
                                    var lang = SharedUtilities.LoadLanguageDictionary();
                                    dropText.Text = SharedUtilities.GetTranslation(lang, "Drop_Images_Here");
                                }
                                if (dropIcon != null)
                                {
                                    dropIcon.Glyph = "\uE896";
                                    dropIcon.FontSize = 48;
                                }
                            };
                            storyboard.Begin();
                        }
                        return;
                    }

                    // Calculate how many images we can add
                    int availableSlots = MAX_IMAGES - existingPreviews;
                    int filesToCopy = Math.Min(imageFiles.Count, availableSlots);
                    int skippedCount = imageFiles.Count - filesToCopy;

                    Logger.LogInfo($"Available slots: {availableSlots}, Files to copy: {filesToCopy}, Skipped: {skippedCount}");

                    // Show processing state
                    if (dropText != null)
                    {
                        dropText.Text = string.Format(SharedUtilities.GetTranslation(lang, "DragDrop_Copying"), filesToCopy);
                        Logger.LogInfo("Updated drop text to copying");
                    }
                    if (dropIcon != null)
                    {
                        dropIcon.Glyph = "\uE895"; // Sync icon
                    }

                    // modFolderPath already retrieved above
                    Logger.LogInfo($"Target folder: {modFolderPath}");

                    // Copy images with preview naming (only up to available slots)
                    int copiedCount = 0;
                    Logger.LogInfo($"Starting copy of {filesToCopy} files (out of {imageFiles.Count})");
                    for (int i = 0; i < filesToCopy; i++)
                    {
                        var imageFile = imageFiles[i];
                        try
                        {
                            Logger.LogInfo($"Processing file: {imageFile.Name} (Path: {imageFile.Path})");

                            // Generate preview name: preview001.jpg, preview002.jpg, etc.
                            var previewName = $"preview{copiedCount + 1:D3}{imageFile.FileType}";
                            var targetPath = Path.Combine(modFolderPath, previewName);
                            Logger.LogInfo($"Target path: {targetPath} (from original: {imageFile.Name})");

                            // Check if source file exists
                            if (!File.Exists(imageFile.Path))
                            {
                                Logger.LogError($"Source file does not exist: {imageFile.Path}");
                                continue;
                            }

                            // Use simple file copy with preview naming
                            Logger.LogInfo("Using File.Copy with preview naming");
                            File.Copy(imageFile.Path, targetPath, true);

                            copiedCount++;
                            Logger.LogInfo($"Successfully copied {imageFile.Name} as {previewName}");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to copy {imageFile?.Name ?? "unknown"}", ex);
                        }
                    }

                    Logger.LogInfo($"Copy complete. Copied {copiedCount}/{imageFiles.Count} files with preview naming");

                    // Track copied files for potential cleanup (only the raw copied files, not processed ones)
                    var copiedFiles = new List<string>();
                    for (int i = 0; i < copiedCount; i++)
                    {
                        var previewName = $"preview{i + 1:D3}{imageFiles[i].FileType}";
                        copiedFiles.Add(Path.Combine(modFolderPath, previewName));
                    }

                    // Optimize using DragDropModMode setting (async to allow crop inspection UI)
                    var context = Services.ImageOptimizationService.GetOptimizationContext(
                        Services.OptimizationTrigger.DragDropMod);
                    Logger.LogInfo($"Starting optimization (Mode: {context.Mode}, InspectAndEdit: {context.InspectAndEditEnabled})");
                    bool wasCancelled = false;

                    try
                    {
                        Logger.LogInfo($"Calling ProcessModPreviewImagesAsync for: {modFolderPath}");
                        await Services.ImageOptimizationService.ProcessModPreviewImagesAsync(modFolderPath, context);
                        Logger.LogInfo("Optimization complete");

                        // Log what files exist after optimization
                        try
                        {
                            var files = Directory.GetFiles(modFolderPath)
                                .Select(f => Path.GetFileName(f))
                                .Where(f => f.StartsWith("preview", StringComparison.OrdinalIgnoreCase) ||
                                           f.Equals("minitile.jpg", StringComparison.OrdinalIgnoreCase) ||
                                           f.Equals("minitile.webp", StringComparison.OrdinalIgnoreCase))
                                .ToList();
                            Logger.LogInfo($"Files after optimization: {string.Join(", ", files)}");
                        }
                        catch (Exception filesEx)
                        {
                            Logger.LogError("Failed to list files after optimization", filesEx);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.LogInfo("Optimization cancelled by user");
                        wasCancelled = true;

                        // Cleanup: delete any remaining raw copied files (preview001, preview002, etc.)
                        // The service's CleanupProcessedFiles handles cleanup of processed files
                        foreach (var file in copiedFiles)
                        {
                            try
                            {
                                if (File.Exists(file))
                                {
                                    File.Delete(file);
                                    Logger.LogInfo($"Cleanup: deleted {Path.GetFileName(file)}");
                                }
                            }
                            catch (Exception cleanupEx)
                            {
                                Logger.LogWarning($"Cleanup: failed to delete {Path.GetFileName(file)}: {cleanupEx.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Optimization failed", ex);
                        wasCancelled = true; // Treat errors as cancellation for UI feedback
                    }

                    Logger.LogInfo("Optimization task completed");

                    // Show result based on whether cancelled or successful
                    if (dropText != null)
                    {
                        if (wasCancelled)
                        {
                            dropText.Text = SharedUtilities.GetTranslation(lang, "DragDrop_Cancelled") ?? "Cancelled";
                            Logger.LogInfo("Updated drop text to cancelled");
                        }
                        else if (skippedCount > 0)
                        {
                            dropText.Text = string.Format(SharedUtilities.GetTranslation(lang, "DragDrop_AddedSkipped"), copiedCount, skippedCount);
                            Logger.LogInfo($"Updated drop text to partial success: {copiedCount} added, {skippedCount} skipped");
                        }
                        else
                        {
                            dropText.Text = SharedUtilities.GetTranslation(lang, "DragDrop_ImagesAdded");
                            Logger.LogInfo("Updated drop text to success");
                        }
                    }
                    if (dropIcon != null)
                    {
                        dropIcon.Glyph = wasCancelled ? "\uE711" : "\uE73E"; // Cross or Checkmark
                        dropIcon.FontSize = 48;
                    }

                    await Task.Delay(1500);

                    // Fade out overlay
                    if (dropOverlay != null)
                    {
                        var fadeOut = new DoubleAnimation
                        {
                            From = 1,
                            To = 0,
                            Duration = new Duration(TimeSpan.FromMilliseconds(300))
                        };
                        var storyboard = new Storyboard();
                        Storyboard.SetTarget(fadeOut, dropOverlay);
                        Storyboard.SetTargetProperty(fadeOut, "Opacity");
                        storyboard.Children.Add(fadeOut);
                        storyboard.Completed += (s, args) =>
                        {
                            dropOverlay.Visibility = Visibility.Collapsed;
                            dropOverlay.Opacity = 0;
                            if (dropText != null)
                            {
                                var resetLang = SharedUtilities.LoadLanguageDictionary();
                                dropText.Text = SharedUtilities.GetTranslation(resetLang, "DragDrop_DropImagesHere");
                            }
                            if (dropIcon != null) dropIcon.Glyph = "\uE896";
                        };
                        storyboard.Begin();
                        Logger.LogInfo("Started fade out animation");
                    }

                    // Refresh images
                    if (!string.IsNullOrEmpty(modFolderPath))
                    {
                        RefreshModTileImage(modFolderPath);
                    }
                    else
                    {
                        LoadVisibleImages();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in drag & drop handler", ex);
                    if (dropOverlay != null)
                    {
                        dropOverlay.Visibility = Visibility.Collapsed;
                        dropOverlay.Opacity = 0;
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModGridPage.Tile_Drop failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModGridPage.Tile_Drop failed", ex);
            }
        }
    }
}
