using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlairX_Mod_Manager.Models;
using System.Collections.ObjectModel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Service for capturing screenshots and processing them through crop panel
    /// </summary>
    public class ScreenshotCaptureService
    {
        private FileSystemWatcher? _watcher;
        private string? _modDirectory;
        private string? _screenshotDirectory;
        private List<string> _capturedFiles = new();
        private bool _isCapturing = false;

        /// <summary>
        /// Event raised when a new file is captured
        /// </summary>
        public event EventHandler<string>? FileCaptured;

        /// <summary>
        /// Start screenshot capture mode - monitors directory and opens crop panel
        /// </summary>
        public async Task StartCaptureMode(string modDirectory, string screenshotDirectory)
        {
            if (_isCapturing)
            {
                Logger.LogWarning("Screenshot capture already in progress");
                return;
            }

            _modDirectory = modDirectory;
            _screenshotDirectory = screenshotDirectory;
            _capturedFiles.Clear();
            _isCapturing = true;

            try
            {
                // Start monitoring screenshot directory
                StartFileWatcher();
                Logger.LogInfo($"Screenshot capture mode started - monitoring {screenshotDirectory}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error starting screenshot capture mode", ex);
                StopCapture();
                throw;
            }
        }

        private void StartFileWatcher()
        {
            if (string.IsNullOrEmpty(_screenshotDirectory))
                return;

            _watcher = new FileSystemWatcher(_screenshotDirectory)
            {
                Filter = "*.*",
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileCreated;
            _watcher.Renamed += OnFileRenamed;

            Logger.LogInfo($"Started monitoring screenshot directory: {_screenshotDirectory}");
        }

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                await ProcessNewFile(e.FullPath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unhandled error in OnFileCreated for {e.FullPath}", ex);
            }
        }

        private async void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                await ProcessNewFile(e.FullPath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unhandled error in OnFileRenamed for {e.FullPath}", ex);
            }
        }

        private async Task ProcessNewFile(string filePath)
        {
            try
            {
                // Check if capture is still active
                if (!_isCapturing || string.IsNullOrEmpty(_modDirectory))
                {
                    Logger.LogWarning($"Capture stopped or mod directory not set, ignoring file: {filePath}");
                    return;
                }

                // Check if it's an image file
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (!IsImageFile(extension))
                    return;

                // Wait a bit for file to be fully written
                await Task.Delay(500);

                // Check if file still exists and is accessible
                if (!File.Exists(filePath))
                    return;

                // Try to open the file to ensure it's not locked
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        using var fs = File.OpenRead(filePath);
                        break;
                    }
                    catch (IOException)
                    {
                        if (i == 4) return; // Give up after 5 attempts
                        await Task.Delay(200);
                    }
                }

                // Copy file to mod directory with sequential naming
                await CopyFileToModDirectory(filePath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error processing new screenshot file: {filePath}", ex);
            }
        }

        private async Task CopyFileToModDirectory(string sourceFilePath)
        {
            if (string.IsNullOrEmpty(_modDirectory))
            {
                Logger.LogWarning("Mod directory is null or empty, cannot copy file");
                return;
            }

            try
            {
                // Verify mod directory still exists
                if (!Directory.Exists(_modDirectory))
                {
                    Logger.LogError($"Mod directory no longer exists: {_modDirectory}");
                    return;
                }

                // Find next available number
                int nextNumber = GetNextAvailableNumber();
                var sourceExtension = Path.GetExtension(sourceFilePath); // Keep original extension (PNG, JPG, etc.)
                string targetFileName = $"Preview{nextNumber:D3}{sourceExtension}"; // Preview001.png, Preview001.jpg, etc.
                string targetPath = System.IO.Path.Combine(_modDirectory, targetFileName);

                // Copy file without conversion - keep original quality
                // Conversion will happen during optimization
                File.Copy(sourceFilePath, targetPath, overwrite: true);

                _capturedFiles.Add(targetPath);
                Logger.LogInfo($"Captured screenshot: {targetFileName}");

                // Notify UI that new file was captured
                FileCaptured?.Invoke(this, targetPath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error copying screenshot to mod directory: {sourceFilePath}", ex);
            }
        }

        private int GetNextAvailableNumber()
        {
            if (string.IsNullOrEmpty(_modDirectory))
                return 1;

            var existingFiles = Directory.GetFiles(_modDirectory, "Preview*.*")
                .Where(f => IsImageFile(System.IO.Path.GetExtension(f)))
                .Select(System.IO.Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrEmpty(name) && name.StartsWith("Preview") && name.Length == 10) // Preview001 = 10 chars
                .Select(name => name!.Substring(7)) // Remove "Preview" prefix - name is guaranteed not null by Where clause
                .Where(numberPart => !string.IsNullOrEmpty(numberPart) && int.TryParse(numberPart, out _))
                .Select(numberPart => int.Parse(numberPart))
                .ToList();

            // Find first available number starting from 1
            for (int i = 1; i <= 999; i++)
            {
                if (!existingFiles.Contains(i))
                    return i;
            }

            return 1; // Fallback
        }

        private bool IsImageFile(string extension)
        {
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };
            return imageExtensions.Contains(extension);
        }

        public void StopCapture()
        {
            _isCapturing = false;
            
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            Logger.LogInfo("Screenshot capture stopped");
        }

        /// <summary>
        /// Stop capture and delete all captured files
        /// </summary>
        public async void StopCaptureAndCleanup()
        {
            try
            {
                Logger.LogInfo($"StopCaptureAndCleanup called - {_capturedFiles.Count} files to clean up");
                
                StopCapture();
                
                // Delete all captured files
                if (_capturedFiles.Count > 0)
                {
                    Logger.LogInfo($"Cleaning up {_capturedFiles.Count} captured files");
                    var deletedCount = 0;
                    
                    // Force garbage collection to release any image handles
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    
                    foreach (var file in _capturedFiles.ToList())
                    {
                        var deleted = await TryDeleteFileWithRetry(file, maxRetries: 5, delayMs: 200);
                        if (deleted)
                        {
                            deletedCount++;
                            Logger.LogInfo($"Deleted captured file: {Path.GetFileName(file)}");
                        }
                        else
                        {
                            Logger.LogError($"Failed to delete captured file after retries: {file}");
                        }
                    }
                    
                    _capturedFiles.Clear();
                    Logger.LogInfo($"Cleanup completed - deleted {deletedCount} files");
                }
                else
                {
                    Logger.LogInfo("No files to clean up");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Unhandled error in StopCaptureAndCleanup", ex);
            }
        }
        
        /// <summary>
        /// Try to delete a file with retry logic for locked files
        /// </summary>
        private async Task<bool> TryDeleteFileWithRetry(string filePath, int maxRetries = 5, int delayMs = 200)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        Logger.LogWarning($"File not found for deletion: {filePath}");
                        return true; // Consider it "deleted" if it doesn't exist
                    }
                    
                    // Try to delete the file
                    File.Delete(filePath);
                    return true;
                }
                catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                {
                    Logger.LogWarning($"File locked on attempt {attempt}/{maxRetries}: {Path.GetFileName(filePath)}");
                    
                    if (attempt < maxRetries)
                    {
                        // Force garbage collection before retry
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        
                        await Task.Delay(delayMs);
                        delayMs *= 2; // Exponential backoff
                    }
                    else
                    {
                        Logger.LogError($"File remains locked after {maxRetries} attempts: {filePath}", ex);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Unexpected error deleting file: {filePath}", ex);
                    return false;
                }
            }
            
            return false;
        }

        public List<string> GetCapturedFiles()
        {
            return new List<string>(_capturedFiles);
        }

        public bool IsCapturing => _isCapturing;
    }
}