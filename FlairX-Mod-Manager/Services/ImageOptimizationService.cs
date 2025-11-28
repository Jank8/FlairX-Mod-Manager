using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using FlairX_Mod_Manager.Models;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Service for image optimization operations across all contexts (Manual, Drag&Drop, Auto)
    /// </summary>
    public static class ImageOptimizationService
    {
        /// <summary>
        /// Process category preview image with specified optimization mode
        /// </summary>
        public static void ProcessCategoryPreview(string categoryDir, OptimizationMode mode)
        {
            // Delegate to existing implementation in SettingsUserControl
            // TODO: Move implementation here in future refactoring
            Pages.SettingsUserControl.ProcessCategoryPreviewStatic(categoryDir, mode);
        }

        /// <summary>
        /// Process mod preview images with specified optimization mode
        /// </summary>
        public static void ProcessModPreviewImages(string modDir, OptimizationMode mode)
        {
            // Delegate to existing implementation in SettingsUserControl
            // TODO: Move implementation here in future refactoring
            Pages.SettingsUserControl.ProcessModPreviewImagesStatic(modDir, mode);
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
        /// Get optimization context from settings for specified trigger
        /// </summary>
        public static OptimizationContext GetOptimizationContext(OptimizationTrigger trigger)
        {
            var modeString = trigger switch
            {
                OptimizationTrigger.Manual => SettingsManager.Current.ImageOptimizerManualMode,
                OptimizationTrigger.DragDropMod => SettingsManager.Current.ImageOptimizerDragDropModMode,
                OptimizationTrigger.DragDropCategory => SettingsManager.Current.ImageOptimizerDragDropCategoryMode,
                OptimizationTrigger.GameBananaDownload => SettingsManager.Current.ImageOptimizerAutoDownloadMode,
                _ => "Full"
            };

            var mode = Enum.TryParse<OptimizationMode>(modeString, out var parsedMode) 
                ? parsedMode 
                : OptimizationMode.Full;

            var cropStrategyString = SettingsManager.Current.ImageCropType ?? "Center";
            var cropStrategy = Enum.TryParse<CropStrategy>(cropStrategyString, out var parsedStrategy)
                ? parsedStrategy
                : CropStrategy.Center;

            return new OptimizationContext
            {
                Mode = mode,
                JpegQuality = SettingsManager.Current.ImageOptimizerJpegQuality,
                ThreadCount = SettingsManager.Current.ImageOptimizerThreadCount,
                CreateBackups = SettingsManager.Current.ImageOptimizerCreateBackups,
                KeepOriginals = SettingsManager.Current.ImageOptimizerKeepOriginals,
                CropStrategy = cropStrategy,
                InspectAndEditEnabled = SettingsManager.Current.PreviewBeforeCrop,
                Trigger = trigger
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
        Attention,
        ManualOnly
    }
}
