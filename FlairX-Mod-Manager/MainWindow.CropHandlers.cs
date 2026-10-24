using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace FlairX_Mod_Manager
{
    public sealed partial class MainWindow
    {
        
        /// <summary>
        /// Setup crop inspection event handler
        /// </summary>
        private void SetupCropInspectionHandler()
        {
            var window = this; // Capture window reference for lambda
            
            Services.ImageOptimizationService.CropInspectionRequested += async (sourceImage, suggestedCrop, targetWidth, targetHeight, imageType, isProtected) =>
            {
                var tcs = new TaskCompletionSource<Services.CropInspectionResult?>();
                
                window.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        // Create crop inspection panel
                        var cropPanel = new Controls.ImageCropInspectionPanel();
                        
                        // Calculate aspect ratio
                        double aspectRatio = (double)targetWidth / targetHeight;
                        bool maintainAspectRatio = true; // Always maintain aspect ratio for consistency
                        
                        // Show as sliding panel
                        ShowSlidingPanel(cropPanel, $"Crop Image - {imageType}");
                        
                        // Start showing the crop panel and wait for result
                        var cropResult = await cropPanel.ShowForImageAsync(sourceImage, suggestedCrop, aspectRatio, maintainAspectRatio, imageType, isProtected);
                        
                        // Convert result
                        var result = new Services.CropInspectionResult
                        {
                            Action = cropResult.Action switch
                            {
                                Controls.CropAction.Confirm => Services.CropInspectionAction.Confirm,
                                Controls.CropAction.Skip => Services.CropInspectionAction.Skip,
                                Controls.CropAction.Delete => Services.CropInspectionAction.Delete,
                                Controls.CropAction.Cancel => Services.CropInspectionAction.Cancel,
                                Controls.CropAction.ChooseDifferentFile => Services.CropInspectionAction.ChooseDifferentFile,
                                _ => Services.CropInspectionAction.Cancel
                            },
                            CropRectangle = cropResult.CropRectangle
                        };
                        
                        tcs.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error showing crop inspection panel", ex);
                        tcs.SetResult(null);
                    }
                });
                
                return await tcs.Task;
            };
            
            Logger.LogInfo("Crop inspection handler setup complete");
            
            // Setup batch crop inspection handler
            SetupBatchCropInspectionHandler();
            
            // Also setup minitile source selection handler
            SetupMinitileSourceSelectionHandler();
        }

        /// <summary>
        /// Setup handler for batch crop inspection events from ImageOptimizationService
        /// </summary>
        private void SetupBatchCropInspectionHandler()
        {
            var window = this;
            
            Services.ImageOptimizationService.BatchCropInspectionRequested += async (items) =>
            {
                Logger.LogInfo($"[BATCH_CROP] Handler invoked with {items.Count} items");
                var tcs = new TaskCompletionSource<List<Services.BatchCropInspectionResult>?>();
                
                window.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        var cropPanel = new Controls.ImageCropInspectionPanel();
                        
                        // Create thumbnails in background to avoid UI freeze
                        var controlItems = await Task.Run(() =>
                        {
                            return items.Select(item => new Controls.BatchCropItem
                            {
                                FilePath = item.FilePath,
                                DisplayName = Path.GetFileName(item.FilePath),
                                ImageType = item.ImageType,
                                SourceImage = item.SourceImage,
                                InitialCropRect = item.InitialCropRect,
                                TargetWidth = item.TargetWidth,
                                TargetHeight = item.TargetHeight,
                                IsProtected = item.IsProtected,
                                Thumbnail = CreateThumbnailFromImage(item.SourceImage)
                            }).ToList();
                        });
                        
                        var lang = SharedUtilities.LoadLanguageDictionary();
                        var title = SharedUtilities.GetTranslation(lang, "CropPanel_Title") ?? "Adjust Crop Area";
                        ShowSlidingPanel(cropPanel, $"{title} ({items.Count} files)");
                        
                        var batchResults = await cropPanel.ShowForBatchAsync(controlItems);
                        
                        // Check if cancelled (null result)
                        if (batchResults == null)
                        {
                            Logger.LogInfo($"[BATCH_CROP] User cancelled batch crop");
                            tcs.SetResult(null);
                            return;
                        }
                        
                        // Convert results back to service format
                        var results = batchResults.Select(r => new Services.BatchCropInspectionResult
                        {
                            FilePath = r.FilePath,
                            ImageType = r.ImageType,
                            Action = r.Action switch
                            {
                                Controls.CropAction.Confirm => Services.CropInspectionAction.Confirm,
                                Controls.CropAction.Skip => Services.CropInspectionAction.Skip,
                                Controls.CropAction.Delete => Services.CropInspectionAction.Delete,
                                Controls.CropAction.Cancel => Services.CropInspectionAction.Cancel,
                                Controls.CropAction.ChooseDifferentFile => Services.CropInspectionAction.ChooseDifferentFile,
                                _ => Services.CropInspectionAction.Cancel
                            },
                            CropRectangle = r.CropRectangle,
                            TargetWidth = r.TargetWidth,
                            TargetHeight = r.TargetHeight
                        }).ToList();
                        
                        tcs.SetResult(results);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("[BATCH_CROP] Error showing batch crop inspection panel", ex);
                        tcs.SetResult(null);
                    }
                });
                
                return await tcs.Task;
            };
            
            Logger.LogInfo("Batch crop inspection handler setup complete");
        }
        
        /// <summary>
        /// Setup handler for minitile source selection events from ImageOptimizationService
        /// </summary>
        private void SetupMinitileSourceSelectionHandler()
        {
            var window = this;
            
            Services.ImageOptimizationService.MinitileSourceSelectionRequested += async (availableFiles, modDirectory, titleOverride, subtitleOverride) =>
            {
                Logger.LogInfo($"[MINITILE_HANDLER] Event triggered with {availableFiles.Count} files for: {modDirectory}");
                var tcs = new TaskCompletionSource<Services.MinitileSourceResult?>();
                
                Logger.LogInfo($"[MINITILE_HANDLER] Attempting to enqueue to UI thread");
                bool enqueued = window.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        Logger.LogInfo($"[MINITILE_HANDLER] Executing on UI thread");
                        try
                        {
                            Logger.LogInfo($"[MINITILE_HANDLER] Creating MinitileSourceSelectionPanel");
                            var selectionPanel = new Controls.MinitileSourceSelectionPanel();

                            Logger.LogInfo($"[MINITILE_HANDLER] Loading language dictionary");
                            var lang = SharedUtilities.LoadLanguageDictionary();
                            var title = titleOverride ?? SharedUtilities.GetTranslation(lang, "MinitileSelection_Title") ?? "Select Minitile Source";

                            Logger.LogInfo($"[MINITILE_HANDLER] Showing sliding panel with title: {title}");
                            ShowSlidingPanel(selectionPanel, title);
                            Logger.LogInfo($"[MINITILE_HANDLER] Sliding panel shown");

                            Logger.LogInfo($"[MINITILE_HANDLER] Starting ShowForSelectionAsync");
                            var selectionTask = selectionPanel.ShowForSelectionAsync(availableFiles, modDirectory, subtitleOverride);

                            // Wait for the result - panel will close itself when button is clicked
                            var selectionResult = await selectionTask;
                            Logger.LogInfo($"[MINITILE_HANDLER] ShowForSelectionAsync returned");

                            var result = new Services.MinitileSourceResult
                            {
                                SelectedFilePath = selectionResult.SelectedFilePath,
                                Skipped = selectionResult.Skipped,
                                Stopped = selectionResult.Stopped
                            };

                            Logger.LogInfo($"[MINITILE_HANDLER] Setting result");
                            tcs.SetResult(result);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("[MINITILE_HANDLER] Error showing minitile source selection panel", ex);
                            tcs.SetResult(null);
                        }
                    }
                    catch (System.OperationCanceledException)
                    {
                        Logger.LogDebug("MainWindow.SetupMinitileSourceSelectionHandler failed: canceled");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("MainWindow.SetupMinitileSourceSelectionHandler failed", ex);
                    }
                });
                
                Logger.LogInfo($"[MINITILE_HANDLER] TryEnqueue returned: {enqueued}");
                
                if (!enqueued)
                {
                    Logger.LogError("[MINITILE_HANDLER] Failed to enqueue to UI thread!");
                    tcs.SetResult(null);
                }
                
                Logger.LogInfo($"[MINITILE_HANDLER] Waiting for task completion");
                return await tcs.Task;
            };
            
            Logger.LogInfo("Minitile source selection handler setup complete");
        }

        /// <summary>
        /// Open crop panel in screenshot capture mode
        /// </summary>
        public async Task<List<string>?> OpenScreenshotCaptureCropPanel(string modDirectory, string screenshotDirectory)
        {
            try
            {
                var captureService = new Services.ScreenshotCaptureService();
                
                // Start monitoring screenshot directory
                await captureService.StartCaptureMode(modDirectory, screenshotDirectory);
                
                // Create crop panel in capture mode
                var cropPanel = new Controls.ImageCropInspectionPanel();
                
                var lang = SharedUtilities.LoadLanguageDictionary();
                var title = SharedUtilities.GetTranslation(lang, "CropPanel_Title") ?? "Adjust Crop Area";
                ShowSlidingPanel(cropPanel, $"{title} - Screenshot Capture");
                
                // Show panel in capture mode (this would need to be implemented in the panel)
                // For now, we'll use a simplified approach
                var results = await cropPanel.ShowForScreenshotCaptureAsync(captureService);
                
                // Stop capture service
                captureService.StopCapture();
                
                // Return results WITHOUT running optimization here
                // Optimization will be done by the caller after all dialogs are closed
                return results;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error opening screenshot capture crop panel", ex);
                return null;
            }
        }

        /// <summary>
        /// Optimize captured screenshots using manual mode without crop and without minitile creation
        /// </summary>
        private async Task OptimizeCapturedScreenshots(string modDirectory, List<string> capturedFiles)
        {
            try
            {
                // Create optimization context for screenshot capture
                var context = Services.ImageOptimizationService.GetOptimizationContext(Services.OptimizationTrigger.Manual);
                
                // Override settings for screenshot capture
                context.AllowUIInteraction = true; // Allow UI interaction for minitile selection
                context.InspectAndEditEnabled = false; // No crop inspection for preview files
                context.Mode = FlairX_Mod_Manager.Models.OptimizationMode.Standard; // Use Standard mode (quality conversion)
                context.CreateMinitile = true; // Create minitile (will show selection dialog unless AutoCreateModThumbnails is enabled)

                Logger.LogInfo($"Starting optimization of {capturedFiles.Count} captured screenshots in mod: {modDirectory}");

                // Run optimization on the mod directory
                await Services.ImageOptimizationService.ProcessModPreviewImagesAsync(modDirectory, context);

                Logger.LogInfo("Screenshot optimization completed successfully");

                // A new minitile may have been written at the same path; refresh the tile so
                // it does not keep the pre-capture bitmap or stay blank.
                CurrentModGridPage?.RefreshModTileImage(modDirectory);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error optimizing captured screenshots in {modDirectory}", ex);
                
                // Show error to user
                DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        var lang = SharedUtilities.LoadLanguageDictionary();
                        if (App.Current is App _a && _a.MainWindow is MainWindow _mw)
                            _mw.ShowErrorInfo(string.Format(
                                SharedUtilities.GetTranslation(lang, "Notify_ScreenshotOptimizeFailed"), ex.Message));
                    }
                    catch (System.OperationCanceledException)
                    {
                        Logger.LogDebug("MainWindow.OptimizeCapturedScreenshots failed: canceled");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("MainWindow.OptimizeCapturedScreenshots failed", ex);
                    }
                });
            }
        }
    }
}
