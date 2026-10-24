using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using System.Runtime.InteropServices.WindowsRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using XamlImage = Microsoft.UI.Xaml.Controls.Image;
using SharpImage = SixLabors.ImageSharp.Image;

namespace FlairX_Mod_Manager.Controls
{
    public sealed partial class ImageCropInspectionPanel
    {
        private TaskCompletionSource<List<BatchCropResult>?>? _batchCompletionSource;
        
        // Batch mode
        private bool _isBatchMode = false;
        private ObservableCollection<BatchCropItem> _batchItems = new();
        private int _currentBatchIndex = 0;

        /// <summary>
        /// Batch mode - process multiple images at once
        /// </summary>
        public Task<List<BatchCropResult>?> ShowForBatchAsync(List<BatchCropItem> items)
        {
            Logger.LogInfo($"[CROP_PANEL] ShowForBatchAsync called with {items.Count} items");
            
            _isBatchMode = true;
            _batchItems = new ObservableCollection<BatchCropItem>(items);
            _currentBatchIndex = 0;
            _batchCompletionSource = new TaskCompletionSource<List<BatchCropResult>?>();

            // Load translations
            var lang = SharedUtilities.LoadLanguageDictionary();
            
            // Update button texts
            StopButtonText.Text = SharedUtilities.GetTranslation(lang, "Stop") ?? "Stop";
            ResetButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Reset") ?? "Crop Reset";
            DeleteButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Delete") ?? "Delete";
            SkipButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Skip") ?? "Skip";
            NextButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Next") ?? "Next";
            FinalizeButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Done") ?? "Done";
            HintText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Hint") ?? "Drag to move • Corners maintain ratio • Edges change ratio";
            BatchListHeader.Text = SharedUtilities.GetTranslation(lang, "CropPanel_FilesToProcess") ?? "Files to Process";
            
            // Show batch mode UI
            BatchListPanel.Visibility = Visibility.Visible;
            BatchListColumn.Width = new GridLength(240);
            BatchCounterText.Visibility = Visibility.Visible;
            NextButton.Visibility = Visibility.Visible;
            FinalizeButton.Visibility = Visibility.Visible;
            ConfirmButton.Visibility = Visibility.Collapsed;
            
            // Setup items repeater - force update by setting to null first
            BatchItemsRepeater.ItemsSource = null;
            BatchItemsRepeater.ItemsSource = _batchItems;
            
            // Force layout update to ensure all items are rendered
            BatchItemsRepeater.UpdateLayout();
            
            // Load first item (this will set up the image and crop rect)
            if (_batchItems.Count > 0)
            {
                // Don't call LoadBatchItem here - it adds its own LayoutUpdated handler
                // Instead, manually set up the first item
                var item = _batchItems[0];
                item.IsSelected = true;
                
                _sourceImage = item.SourceImage;
                _cropRect = item.CropRect ?? item.InitialCropRect;
                _initialCropRect = item.InitialCropRect;
                _aspectRatio = (double)item.TargetWidth / item.TargetHeight;
                _maintainAspectRatio = true;
                
                // Update UI
                TitleText.Text = $"{SharedUtilities.GetTranslation(lang, "CropPanel_Title") ?? "Adjust Crop Area"} - {item.ImageType}";
                
                // Show aspect ratio info only for specific image types
                bool showAspectRatio = item.ImageType.StartsWith("minitile", StringComparison.OrdinalIgnoreCase) ||
                                       item.ImageType.StartsWith("catprev", StringComparison.OrdinalIgnoreCase) ||
                                       item.ImageType.StartsWith("catmini", StringComparison.OrdinalIgnoreCase);
                
                if (showAspectRatio)
                {
                    SubtitleText.Text = $"{SharedUtilities.GetTranslation(lang, "CropPanel_AspectLocked") ?? "Aspect ratio locked to"} {_aspectRatio:F2}:1";
                }
                else
                {
                    SubtitleText.Text = "Drag to reposition, use handles to resize";
                }
                
                BatchCounterText.Text = $"(1/{_batchItems.Count})";
                
                DeleteButton.IsEnabled = !item.IsProtected;
                NextButton.IsEnabled = _batchItems.Count > 1;
                
                LoadImage();
                UpdateInfoText();
            }
            
            // Update overlay after layout is ready
            _isInitialized = false;
            this.LayoutUpdated += OnLayoutUpdated;

            return _batchCompletionSource.Task;
        }

        private void LoadBatchItem(int index)
        {
            if (index < 0 || index >= _batchItems.Count) return;
            
            // Save current item's crop rect before switching
            if (_currentBatchIndex >= 0 && _currentBatchIndex < _batchItems.Count)
            {
                var currentItem = _batchItems[_currentBatchIndex];
                currentItem.CropRect = _cropRect;
                currentItem.IsSelected = false;
            }
            
            _currentBatchIndex = index;
            var item = _batchItems[index];
            item.IsSelected = true;
            
            _sourceImage = item.SourceImage;
            _cropRect = item.CropRect ?? item.InitialCropRect;
            _initialCropRect = item.InitialCropRect;
            _aspectRatio = (double)item.TargetWidth / item.TargetHeight;
            _maintainAspectRatio = true;
            
            // Update UI
            var lang = SharedUtilities.LoadLanguageDictionary();
            TitleText.Text = $"{SharedUtilities.GetTranslation(lang, "CropPanel_Title") ?? "Adjust Crop Area"} - {item.ImageType}";
            
            // Show aspect ratio info only for specific image types
            bool showAspectRatio = item.ImageType.StartsWith("minitile", StringComparison.OrdinalIgnoreCase) ||
                                   item.ImageType.StartsWith("catprev", StringComparison.OrdinalIgnoreCase) ||
                                   item.ImageType.StartsWith("catmini", StringComparison.OrdinalIgnoreCase);
            
            if (showAspectRatio)
            {
                SubtitleText.Text = $"{SharedUtilities.GetTranslation(lang, "CropPanel_AspectLocked") ?? "Aspect ratio locked to"} {_aspectRatio:F2}:1";
            }
            else
            {
                SubtitleText.Text = "Drag to reposition, use handles to resize";
            }
            
            BatchCounterText.Text = $"({index + 1}/{_batchItems.Count})";
            
            // Update delete button state
            DeleteButton.IsEnabled = !item.IsProtected;
            
            // Next button is always enabled (wraps around to first item)
            NextButton.IsEnabled = true;
            
            // Load image
            LoadImage();
            UpdateInfoText();
            
            // Need to wait for layout to update before updating crop overlay
            _isInitialized = false;
            this.LayoutUpdated += OnLayoutUpdated;
        }

        private void SaveCurrentBatchItem()
        {
            if (!_isBatchMode || _currentBatchIndex < 0 || _currentBatchIndex >= _batchItems.Count) return;
            
            var item = _batchItems[_currentBatchIndex];
            item.CropRect = _cropRect;
            item.IsEdited = true;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset crop to initial suggested crop
            _cropRect = _initialCropRect;
            UpdateCropOverlay();
            UpdateInfoText();
            Logger.LogInfo("Crop area reset to initial suggestion");
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchMode)
            {
                // Mark current item as skip and move to next
                if (_currentBatchIndex >= 0 && _currentBatchIndex < _batchItems.Count)
                {
                    var item = _batchItems[_currentBatchIndex];
                    item.Action = CropAction.Skip;
                    item.CropRect = _cropRect;
                    item.IsEdited = true;
                }
                MoveToNextBatchItem();
            }
            else
            {
                _completionSource?.SetResult(new CropResult { Action = CropAction.Skip, CropRectangle = _cropRect });
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchMode)
            {
                // Delete file immediately and remove from list
                if (_currentBatchIndex >= 0 && _currentBatchIndex < _batchItems.Count)
                {
                    var item = _batchItems[_currentBatchIndex];
                    
                    // Delete the file immediately
                    try
                    {
                        if (File.Exists(item.FilePath))
                        {
                            // Dispose the image first to release file handle
                            item.SourceImage?.Dispose();
                            item.Thumbnail = null;
                            
                            File.Delete(item.FilePath);
                            Logger.LogInfo($"Deleted file: {Path.GetFileName(item.FilePath)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to delete file: {item.FilePath}", ex);
                    }
                    
                    // Remove from list
                    int indexToRemove = _currentBatchIndex;
                    _batchItems.RemoveAt(indexToRemove);
                    
                    // Update counter
                    if (_batchItems.Count > 0)
                    {
                        BatchCounterText.Text = $"{_batchItems.Count} file(s)";
                        
                        // Adjust current index after removal
                        if (indexToRemove >= _batchItems.Count)
                        {
                            _currentBatchIndex = _batchItems.Count - 1;
                        }
                        else
                        {
                            _currentBatchIndex = indexToRemove;
                        }
                        
                        // Load the item at current index
                        if (_currentBatchIndex >= 0)
                        {
                            LoadBatchItem(_currentBatchIndex);
                        }
                    }
                    else
                    {
                        // No more items - clear UI but don't close (for screenshot capture mode)
                        _currentBatchIndex = -1;
                        ImageContainer.Visibility = Visibility.Collapsed;
                        CropInfoText.Text = "All files processed. Waiting for more screenshots...";
                        BatchCounterText.Visibility = Visibility.Collapsed;
                        NextButton.IsEnabled = false;
                    }
                }
            }
            else
            {
                _completionSource?.SetResult(new CropResult { Action = CropAction.Delete, CropRectangle = _cropRect });
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            // Save current and mark as confirmed, then move to next
            SaveCurrentBatchItem();
            if (_currentBatchIndex >= 0 && _currentBatchIndex < _batchItems.Count)
            {
                _batchItems[_currentBatchIndex].Action = CropAction.Confirm;
            }
            MoveToNextBatchItem();
        }

        private void MoveToNextBatchItem()
        {
            // Move to next item, or wrap around to first
            int nextIndex = (_currentBatchIndex + 1) % _batchItems.Count;
            LoadBatchItem(nextIndex);
        }

        private async void FinalizeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable buttons to prevent double-click
                FinalizeButton.IsEnabled = false;
                NextButton.IsEnabled = false;
                StopButton.IsEnabled = false;

                // Save current item's crop rect and mark all pending items as "no crop"
                if (_isBatchMode && _currentBatchIndex >= 0 && _currentBatchIndex < _batchItems.Count)
                {
                    _batchItems[_currentBatchIndex].CropRect = _cropRect;
                }

                // Mark all pending items as "no crop" (user can delete them later if needed)
                var pendingItems = _batchItems.Where(item => !item.IsEdited).ToList();
                foreach (var item in pendingItems)
                {
                    item.Action = CropAction.Skip; // No crop
                    item.IsEdited = true;
                }

                // Build results
                var results = _batchItems.Select(item => new BatchCropResult
                {
                    FilePath = item.FilePath,
                    ImageType = item.ImageType,
                    Action = item.Action,
                    CropRectangle = item.CropRect ?? item.InitialCropRect,
                    TargetWidth = item.TargetWidth,
                    TargetHeight = item.TargetHeight,
                    TargetIndex = item.TargetIndex
                }).ToList();

                _batchCompletionSource?.SetResult(results);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ImageCropInspectionPanel.FinalizeButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ImageCropInspectionPanel.FinalizeButton_Click failed", ex);
            }
        }

        // Batch list item handlers
        private void BatchItem_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is BatchCropItem item)
            {
                int index = _batchItems.IndexOf(item);
                if (index >= 0 && index != _currentBatchIndex)
                {
                    // Just save crop rect without marking as edited - user is just browsing
                    if (_currentBatchIndex >= 0 && _currentBatchIndex < _batchItems.Count)
                    {
                        _batchItems[_currentBatchIndex].CropRect = _cropRect;
                    }
                    LoadBatchItem(index);
                }
            }
        }

        private void BatchItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = (SolidColorBrush)Application.Current.Resources["SubtleFillColorTertiaryBrush"];
            }
        }

        private void BatchItem_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = (SolidColorBrush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
            }
        }

        /// <summary>
        /// Screenshot capture mode - monitors directory and processes files as they arrive
        /// </summary>
        public async Task<List<string>?> ShowForScreenshotCaptureAsync(Services.ScreenshotCaptureService captureService)
        {
            try
            {
                _isBatchMode = true;
                _batchItems = new ObservableCollection<BatchCropItem>();
                _currentBatchIndex = -1; // No item selected initially
                
                var lang = SharedUtilities.LoadLanguageDictionary();
                
                // Update UI for capture mode
                TitleText.Text = SharedUtilities.GetTranslation(lang, "ScreenshotCapture_Title") ?? "Screenshot Capture";
                SubtitleText.Text = ""; // Empty subtitle for screenshot capture mode
                BatchCounterText.Visibility = Visibility.Collapsed; // Hide initially, will show when files are added
                
                // Show batch mode UI
                BatchListPanel.Visibility = Visibility.Visible;
                BatchListColumn.Width = new GridLength(240);
                BatchItemsRepeater.ItemsSource = _batchItems;
                BatchListHeader.Text = SharedUtilities.GetTranslation(lang, "CropPanel_FilesToProcess") ?? "Captured Files";
                
                // Show capture mode buttons
                NextButton.Visibility = Visibility.Visible; // Show Next button for navigation
                FinalizeButton.Visibility = Visibility.Visible;
                ConfirmButton.Visibility = Visibility.Collapsed;
                
                // Update button texts
                StopButtonText.Text = SharedUtilities.GetTranslation(lang, "Stop") ?? "Stop";
                ResetButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Reset") ?? "Reset";
                DeleteButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Delete") ?? "Delete";
                SkipButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Skip") ?? "Skip";
                NextButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Next") ?? "Next";
                FinalizeButtonText.Text = SharedUtilities.GetTranslation(lang, "ScreenshotCapture_FinishCapture") ?? "Finish Capture";
                HintText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Hint") ?? "Drag to move • Corners maintain ratio • Edges change ratio";
                
                // Initially disable Next button
                NextButton.IsEnabled = false;
                
                // Hide image area initially
                ImageContainer.Visibility = Visibility.Collapsed;
                
                // Show instructions
                CropInfoText.Text = SharedUtilities.GetTranslation(lang, "ScreenshotCapture_WaitingForFiles") ?? "Waiting for screenshots...";
                ImageInfoText.Text = SharedUtilities.GetTranslation(lang, "ScreenshotCapture_TakeScreenshots") ?? "Take screenshots in your game/application";
                HintText.Text = SharedUtilities.GetTranslation(lang, "ScreenshotCapture_FilesWillBeCopied") ?? "Files will be automatically copied and numbered (Preview001.jpg, Preview002.jpg, etc.)";
                
                // Subscribe to file capture events
                captureService.FileCaptured += OnFileCaptured;
                
                // Wait for user to finish capture
                var tcs = new TaskCompletionSource<List<string>?>();
                
                // Store original handlers to restore them later
                var originalFinalizeHandlers = new List<RoutedEventHandler>();
                var originalStopHandlers = new List<RoutedEventHandler>();
                
                // Override finalize button to complete capture
                RoutedEventHandler finalizeHandler = (s, e) =>
                {
                    try
                    {
                        // Mark all pending items as "no crop" (user can delete them later if needed)
                        var pendingItems = _batchItems.Where(item => !item.IsEdited).ToList();
                        foreach (var item in pendingItems)
                        {
                            item.Action = CropAction.Skip; // No crop
                            item.IsEdited = true;
                        }
                        
                        // Unsubscribe from capture service
                        captureService.FileCaptured -= OnFileCaptured;
                        
                        var capturedFiles = captureService.GetCapturedFiles();
                        tcs.TrySetResult(capturedFiles);
                        CloseRequested?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error in finalize handler", ex);
                        tcs.TrySetResult(null);
                    }
                };
                FinalizeButton.Click += finalizeHandler;
                
                // Override stop button to cancel capture
                RoutedEventHandler stopHandler = async (s, e) =>
                {
                    try
                    {
                        Logger.LogInfo("Stop button clicked - cancelling screenshot capture");
                        
                        // Unsubscribe from capture service
                        captureService.FileCaptured -= OnFileCaptured;
                        
                        // Clear all loaded images to release file handles
                        ClearAllLoadedImages();
                        
                        // Use the service's cleanup method to delete captured files
                        captureService.StopCaptureAndCleanup();
                        
                        Logger.LogInfo("Screenshot capture cancelled and files cleaned up");
                        
                        tcs.TrySetResult(null);
                        CloseRequested?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error in stop handler", ex);
                        tcs.TrySetResult(null);
                    }
                };
                StopButton.Click += stopHandler;
                
                var result = await tcs.Task;
                
                // Clean up event handlers
                try
                {
                    FinalizeButton.Click -= finalizeHandler;
                    StopButton.Click -= stopHandler;
                    captureService.FileCaptured -= OnFileCaptured;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error cleaning up event handlers", ex);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in screenshot capture mode", ex);
                return null;
            }
        }

        private async void OnFileCaptured(object? sender, string filePath)
        {
            try
            {
                // Run on UI thread
                DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        await AddCapturedFileToList(filePath);
                    }
                    catch (System.OperationCanceledException)
                    {
                        Logger.LogDebug("ImageCropInspectionPanel.OnFileCaptured failed: canceled");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("ImageCropInspectionPanel.OnFileCaptured failed", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error handling captured file: {filePath}", ex);
            }
        }

        private async Task AddCapturedFileToList(string filePath)
        {
            try
            {
                // Load the image
                using var sourceImage = SharpImage.Load<Rgba32>(filePath);
                
                // Create batch item
                var batchItem = new BatchCropItem
                {
                    FilePath = filePath,
                    DisplayName = System.IO.Path.GetFileName(filePath),
                    ImageType = "preview",
                    SourceImage = sourceImage.Clone(ctx => { }),
                    InitialCropRect = new SixLabors.ImageSharp.Rectangle(0, 0, sourceImage.Width, sourceImage.Height),
                    TargetWidth = 1000,
                    TargetHeight = 1000,
                    IsProtected = false,
                    Thumbnail = CreateThumbnailFromImage(sourceImage),
                    TargetIndex = _batchItems.Count
                };
                
                // Add to list
                _batchItems.Add(batchItem);
                
                // If this is the first file, load it for editing
                if (_batchItems.Count == 1)
                {
                    LoadBatchItem(0);
                    ImageContainer.Visibility = Visibility.Visible;
                    
                    // Update instructions
                    CropInfoText.Text = $"Crop: {sourceImage.Width} × {sourceImage.Height}";
                    ImageInfoText.Text = $"Image: {sourceImage.Width} × {sourceImage.Height}";
                    HintText.Text = SharedUtilities.GetTranslation(SharedUtilities.LoadLanguageDictionary(), "ScreenshotCapture_EditAndContinue") ?? "Edit the crop area, then continue taking screenshots or click 'Finish Capture'";
                }
                
                // Update counter to show current/total
                UpdateBatchCounter();
                
                // Enable Next button if we have multiple files
                if (_batchItems.Count > 1)
                {
                    NextButton.IsEnabled = true;
                }
                
                Logger.LogInfo($"Added captured file to crop panel: {System.IO.Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding captured file to list: {filePath}", ex);
            }
        }

        private void UpdateBatchCounter()
        {
            if (_batchItems.Count == 0)
            {
                BatchCounterText.Visibility = Visibility.Collapsed;
                return;
            }
            
            BatchCounterText.Visibility = Visibility.Visible;
            BatchCounterText.Text = $"({_currentBatchIndex + 1}/{_batchItems.Count})";
        }
        
        /// <summary>
        /// Clear all loaded images to release file handles
        /// </summary>
        private void ClearAllLoadedImages()
        {
            try
            {
                Logger.LogInfo("Clearing all loaded images to release file handles");
                
                // Clear current source image
                if (_sourceImage != null)
                {
                    _sourceImage.Dispose();
                    _sourceImage = null;
                }
                
                // Clear all batch item images
                if (_batchItems != null)
                {
                    foreach (var item in _batchItems)
                    {
                        if (item.SourceImage != null)
                        {
                            item.SourceImage.Dispose();
                            item.SourceImage = null;
                        }
                        
                        // Note: We don't dispose Thumbnail as it's a BitmapImage managed by WinUI
                    }
                }
                
                // Clear UI image source
                if (SourceImage.Source != null)
                {
                    SourceImage.Source = null;
                }
                
                // Force garbage collection to ensure file handles are released
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                Logger.LogInfo("All loaded images cleared");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error clearing loaded images", ex);
            }
        }
    }
}
