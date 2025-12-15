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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using System.Runtime.InteropServices.WindowsRuntime;

namespace FlairX_Mod_Manager.Controls
{
    /// <summary>
    /// Item for batch crop processing
    /// </summary>
    public class BatchCropItem : INotifyPropertyChanged
    {
        public string FilePath { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string ImageType { get; set; } = "";
        public BitmapImage? Thumbnail { get; set; }
        public System.Drawing.Image? SourceImage { get; set; }
        public Rectangle InitialCropRect { get; set; }
        public Rectangle? CropRect { get; set; }
        public int TargetWidth { get; set; }
        public int TargetHeight { get; set; }
        public bool IsProtected { get; set; }
        public CropAction Action { get; set; } = CropAction.Confirm;
        
        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); OnPropertyChanged(nameof(StatusIcon)); OnPropertyChanged(nameof(StatusColor)); }
        }
        
        private bool _isEdited;
        public bool IsEdited 
        { 
            get => _isEdited;
            set { _isEdited = value; OnPropertyChanged(nameof(IsEdited)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusIcon)); OnPropertyChanged(nameof(StatusColor)); }
        }
        
        public string StatusText => Action == CropAction.Delete ? "Will be deleted" : 
                                    Action == CropAction.Skip ? "Skip optimization" :
                                    IsEdited ? "Edited" : "Pending";
        public string StatusIcon => Action == CropAction.Delete ? "\uE74D" : 
                                    Action == CropAction.Skip ? "\uE8BB" :
                                    IsEdited ? "\uE73E" : "\uE8B7";
        public SolidColorBrush StatusColor => Action == CropAction.Delete ? new SolidColorBrush(Microsoft.UI.Colors.Red) :
                                              Action == CropAction.Skip ? new SolidColorBrush(Microsoft.UI.Colors.Orange) :
                                              IsEdited ? new SolidColorBrush(Microsoft.UI.Colors.Green) : 
                                              new SolidColorBrush(Microsoft.UI.Colors.Gray);
        
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed partial class ImageCropInspectionPanel : UserControl
    {
        private System.Drawing.Image? _sourceImage;
        private Rectangle _cropRect;
        private Rectangle _initialCropRect;
        private double _aspectRatio;
        private bool _maintainAspectRatio;
        private string? _dragHandle;
        private Windows.Foundation.Point _dragStartPoint;
        private Rectangle _dragStartRect;
        private bool _isDragging;
        private TaskCompletionSource<CropResult>? _completionSource;
        private TaskCompletionSource<List<BatchCropResult>>? _batchCompletionSource;

        private bool _isInitialized = false;
        
        // Batch mode
        private bool _isBatchMode = false;
        private ObservableCollection<BatchCropItem> _batchItems = new();
        private int _currentBatchIndex = 0;

        public event EventHandler? CloseRequested;

        public ImageCropInspectionPanel()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Single image mode - original behavior
        /// </summary>
        public Task<CropResult> ShowForImageAsync(System.Drawing.Image sourceImage, Rectangle initialCropRect, double targetAspectRatio, bool maintainAspectRatio, string imageType, bool isProtected = false)
        {
            _isBatchMode = false;
            _sourceImage = sourceImage;
            _cropRect = initialCropRect;
            _initialCropRect = initialCropRect;
            _aspectRatio = targetAspectRatio;
            _maintainAspectRatio = maintainAspectRatio;
            _completionSource = new TaskCompletionSource<CropResult>();

            // Load translations
            var lang = SharedUtilities.LoadLanguageDictionary();
            
            // Update title based on image type
            TitleText.Text = $"{SharedUtilities.GetTranslation(lang, "CropPanel_Title") ?? "Adjust Crop Area"} - {imageType}";
            SubtitleText.Text = maintainAspectRatio 
                ? $"{SharedUtilities.GetTranslation(lang, "CropPanel_AspectLocked") ?? "Aspect ratio locked to"} {targetAspectRatio:F2}:1" 
                : SharedUtilities.GetTranslation(lang, "CropPanel_FreeAspect") ?? "Free aspect ratio - drag handles to resize";
            
            // Update button texts
            ResetButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Reset") ?? "Crop Reset";
            DeleteButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Delete") ?? "Delete";
            SkipButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Skip") ?? "Skip";
            ConfirmButton.Content = SharedUtilities.GetTranslation(lang, "Confirm") ?? "Confirm";
            HintText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Hint") ?? "Drag to move • Corners maintain ratio • Edges change ratio";
            
            // Disable Delete button if this file is protected
            DeleteButton.IsEnabled = !isProtected;
            
            // Hide batch mode UI
            BatchListPanel.Visibility = Visibility.Collapsed;
            BatchListColumn.Width = new GridLength(0);
            BatchCounterText.Visibility = Visibility.Collapsed;
            NextButton.Visibility = Visibility.Collapsed;
            FinalizeButton.Visibility = Visibility.Collapsed;
            ConfirmButton.Visibility = Visibility.Visible;

            // Load image
            LoadImage();
            UpdateInfoText();
            
            // Update overlay after layout is ready
            _isInitialized = false;
            this.LayoutUpdated += OnLayoutUpdated;

            return _completionSource.Task;
        }

        /// <summary>
        /// Batch mode - process multiple images at once
        /// </summary>
        public Task<List<BatchCropResult>> ShowForBatchAsync(List<BatchCropItem> items)
        {
            _isBatchMode = true;
            _batchItems = new ObservableCollection<BatchCropItem>(items);
            _currentBatchIndex = 0;
            _batchCompletionSource = new TaskCompletionSource<List<BatchCropResult>>();

            // Load translations
            var lang = SharedUtilities.LoadLanguageDictionary();
            
            // Update button texts
            ResetButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Reset") ?? "Crop Reset";
            DeleteButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Delete") ?? "Delete";
            SkipButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Skip") ?? "Skip";
            NextButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Next") ?? "Next";
            FinalizeButtonText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_FinalizeAll") ?? "Finalize All";
            HintText.Text = SharedUtilities.GetTranslation(lang, "CropPanel_Hint") ?? "Drag to move • Corners maintain ratio • Edges change ratio";
            BatchListHeader.Text = SharedUtilities.GetTranslation(lang, "CropPanel_FilesToProcess") ?? "Files to Process";
            
            // Show batch mode UI
            BatchListPanel.Visibility = Visibility.Visible;
            BatchListColumn.Width = new GridLength(220);
            BatchCounterText.Visibility = Visibility.Visible;
            NextButton.Visibility = Visibility.Visible;
            FinalizeButton.Visibility = Visibility.Visible;
            ConfirmButton.Visibility = Visibility.Collapsed;
            
            // Setup items repeater
            BatchItemsRepeater.ItemsSource = _batchItems;
            
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
                SubtitleText.Text = $"{SharedUtilities.GetTranslation(lang, "CropPanel_AspectLocked") ?? "Aspect ratio locked to"} {_aspectRatio:F2}:1";
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
            SubtitleText.Text = $"{SharedUtilities.GetTranslation(lang, "CropPanel_AspectLocked") ?? "Aspect ratio locked to"} {_aspectRatio:F2}:1";
            BatchCounterText.Text = $"({index + 1}/{_batchItems.Count})";
            
            // Update delete button state
            DeleteButton.IsEnabled = !item.IsProtected;
            
            // Update Next button state
            NextButton.IsEnabled = index < _batchItems.Count - 1;
            
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

        private void OnLayoutUpdated(object? sender, object e)
        {
            if (!_isInitialized && ImageCanvas.ActualWidth > 0 && ImageCanvas.ActualHeight > 0)
            {
                _isInitialized = true;
                // Unsubscribe immediately to prevent multiple calls
                try { this.LayoutUpdated -= OnLayoutUpdated; } catch { }
                
                // Delay slightly to ensure transforms are ready
                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateCropOverlay();
                });
            }
        }

        private void LoadImage()
        {
            if (_sourceImage == null) return;

            try
            {
                // Convert System.Drawing.Image to BitmapImage
                using var ms = new System.IO.MemoryStream();
                _sourceImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.SetSource(ms.AsRandomAccessStream());
                SourceImage.Source = bitmapImage;

                // Set canvas size to image size
                ImageCanvas.Width = _sourceImage.Width;
                ImageCanvas.Height = _sourceImage.Height;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load image for crop inspection", ex);
            }
        }

        private void UpdateCropOverlay()
        {
            if (_sourceImage == null) return;
            
            // Check if layout is ready
            if (ImageCanvas.ActualWidth <= 0 || ImageCanvas.ActualHeight <= 0 ||
                CropOverlayCanvas.ActualWidth <= 0 || CropOverlayCanvas.ActualHeight <= 0)
            {
                return; // Layout not ready yet
            }

            try
            {
                // Update dimmed areas in image space
                DimTop.Width = _sourceImage.Width;
                DimTop.Height = Math.Max(0, _cropRect.Y);
                Canvas.SetLeft(DimTop, 0);
                Canvas.SetTop(DimTop, 0);

                DimLeft.Width = Math.Max(0, _cropRect.X);
                DimLeft.Height = _cropRect.Height;
                Canvas.SetLeft(DimLeft, 0);
                Canvas.SetTop(DimLeft, _cropRect.Y);

                DimRight.Width = Math.Max(0, _sourceImage.Width - (_cropRect.X + _cropRect.Width));
                DimRight.Height = _cropRect.Height;
                Canvas.SetLeft(DimRight, _cropRect.X + _cropRect.Width);
                Canvas.SetTop(DimRight, _cropRect.Y);

                DimBottom.Width = _sourceImage.Width;
                DimBottom.Height = Math.Max(0, _sourceImage.Height - (_cropRect.Y + _cropRect.Height));
                Canvas.SetLeft(DimBottom, 0);
                Canvas.SetTop(DimBottom, _cropRect.Y + _cropRect.Height);

                // Convert crop rect from image space to screen space
                var topLeft = ImageCanvas.TransformToVisual(CropOverlayCanvas).TransformPoint(new Windows.Foundation.Point(_cropRect.X, _cropRect.Y));
                var bottomRight = ImageCanvas.TransformToVisual(CropOverlayCanvas).TransformPoint(new Windows.Foundation.Point(_cropRect.X + _cropRect.Width, _cropRect.Y + _cropRect.Height));
                
                // Validate transform results
                if (double.IsNaN(topLeft.X) || double.IsNaN(topLeft.Y) || 
                    double.IsNaN(bottomRight.X) || double.IsNaN(bottomRight.Y))
                {
                    Logger.LogWarning("Invalid transform results in UpdateCropOverlay");
                    return;
                }
                
                double screenX = topLeft.X;
                double screenY = topLeft.Y;
                double screenWidth = bottomRight.X - topLeft.X;
                double screenHeight = bottomRight.Y - topLeft.Y;

                // Update crop rectangle in screen space
                Canvas.SetLeft(CropRectangle, screenX);
                Canvas.SetTop(CropRectangle, screenY);
                CropRectangle.Width = screenWidth;
                CropRectangle.Height = screenHeight;

                // Update corner handles in screen space
                Canvas.SetLeft(HandleTopLeft, screenX - 10);
                Canvas.SetTop(HandleTopLeft, screenY - 10);

                Canvas.SetLeft(HandleTopRight, screenX + screenWidth - 10);
                Canvas.SetTop(HandleTopRight, screenY - 10);

                Canvas.SetLeft(HandleBottomLeft, screenX - 10);
                Canvas.SetTop(HandleBottomLeft, screenY + screenHeight - 10);

                Canvas.SetLeft(HandleBottomRight, screenX + screenWidth - 10);
                Canvas.SetTop(HandleBottomRight, screenY + screenHeight - 10);

                // Update edge handles in screen space
                Canvas.SetLeft(HandleTop, screenX + screenWidth / 2 - 25);
                Canvas.SetTop(HandleTop, screenY - 5);

                Canvas.SetLeft(HandleBottom, screenX + screenWidth / 2 - 25);
                Canvas.SetTop(HandleBottom, screenY + screenHeight - 5);

                Canvas.SetLeft(HandleLeft, screenX - 5);
                Canvas.SetTop(HandleLeft, screenY + screenHeight / 2 - 25);

                Canvas.SetLeft(HandleRight, screenX + screenWidth - 5);
                Canvas.SetTop(HandleRight, screenY + screenHeight / 2 - 25);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error updating crop overlay", ex);
            }
        }

        private void UpdateInfoText()
        {
            if (_sourceImage == null) return;

            CropInfoText.Text = $"Crop: {_cropRect.Width} × {_cropRect.Height}";
            ImageInfoText.Text = $"Image: {_sourceImage.Width} × {_sourceImage.Height}";
        }

        private void Handle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string handle)
            {
                _dragHandle = handle;
                var screenPoint = e.GetCurrentPoint(ImageContainer).Position;
                _dragStartPoint = ImageContainer.TransformToVisual(ImageCanvas).TransformPoint(screenPoint);
                _dragStartRect = _cropRect;
                _isDragging = true;
                element.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }

        private void CropBody_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _dragHandle = "Body";
            var screenPoint = e.GetCurrentPoint(ImageContainer).Position;
            _dragStartPoint = ImageContainer.TransformToVisual(ImageCanvas).TransformPoint(screenPoint);
            _dragStartRect = _cropRect;
            _isDragging = true;
            CropRectangle.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Clicking on canvas (not on handles) does nothing
        }

        private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging || _sourceImage == null || string.IsNullOrEmpty(_dragHandle)) return;

            try
            {
                // Get pointer position in image space
                var currentPoint = e.GetCurrentPoint(ImageContainer).Position;
                var imagePoint = ImageContainer.TransformToVisual(ImageCanvas).TransformPoint(currentPoint);
                var deltaX = (int)(imagePoint.X - _dragStartPoint.X);
                var deltaY = (int)(imagePoint.Y - _dragStartPoint.Y);

                var newRect = _dragStartRect;

            switch (_dragHandle)
            {
                case "Body":
                    // Move entire crop area
                    newRect.X = _dragStartRect.X + deltaX;
                    newRect.Y = _dragStartRect.Y + deltaY;
                    break;

                case "TopLeft":
                    // Corner handles ALWAYS maintain aspect ratio
                    newRect.X = _dragStartRect.X + deltaX;
                    newRect.Y = _dragStartRect.Y + deltaY;
                    newRect.Width = _dragStartRect.Width - deltaX;
                    newRect.Height = _dragStartRect.Height - deltaY;
                    AdjustForAspectRatio(ref newRect, "TopLeft");
                    break;

                case "TopRight":
                    // Corner handles ALWAYS maintain aspect ratio
                    newRect.Y = _dragStartRect.Y + deltaY;
                    newRect.Width = _dragStartRect.Width + deltaX;
                    newRect.Height = _dragStartRect.Height - deltaY;
                    AdjustForAspectRatio(ref newRect, "TopRight");
                    break;

                case "BottomLeft":
                    // Corner handles ALWAYS maintain aspect ratio
                    newRect.X = _dragStartRect.X + deltaX;
                    newRect.Width = _dragStartRect.Width - deltaX;
                    newRect.Height = _dragStartRect.Height + deltaY;
                    AdjustForAspectRatio(ref newRect, "BottomLeft");
                    break;

                case "BottomRight":
                    // Corner handles ALWAYS maintain aspect ratio
                    newRect.Width = _dragStartRect.Width + deltaX;
                    newRect.Height = _dragStartRect.Height + deltaY;
                    AdjustForAspectRatio(ref newRect, "BottomRight");
                    break;

                case "Top":
                    // Edge handles allow free aspect ratio change
                    newRect.Y = _dragStartRect.Y + deltaY;
                    newRect.Height = _dragStartRect.Height - deltaY;
                    break;

                case "Bottom":
                    // Edge handles allow free aspect ratio change
                    newRect.Height = _dragStartRect.Height + deltaY;
                    break;

                case "Left":
                    // Edge handles allow free aspect ratio change
                    newRect.X = _dragStartRect.X + deltaX;
                    newRect.Width = _dragStartRect.Width - deltaX;
                    break;

                case "Right":
                    // Edge handles allow free aspect ratio change
                    newRect.Width = _dragStartRect.Width + deltaX;
                    break;
            }

                // Minimum size check before constraining
                if (newRect.Width < 50)
                    newRect.Width = 50;
                if (newRect.Height < 50)
                    newRect.Height = 50;

                // Constrain to image bounds (only affects position, not size)
                newRect = ConstrainToImageBounds(newRect);

                _cropRect = newRect;
                UpdateCropOverlay();
                UpdateInfoText();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during crop adjustment", ex);
            }
        }

        private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                _dragHandle = null;
            }
        }

        private void Canvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // Don't stop dragging when pointer exits - user might drag outside
        }

        private void AdjustForAspectRatio(ref Rectangle rect, string corner)
        {
            // Use CURRENT crop aspect ratio (not initial _aspectRatio)
            // This allows user to change aspect with edge handles, then maintain it with corners
            double targetAspect = (double)_dragStartRect.Width / _dragStartRect.Height;
            double currentAspect = (double)rect.Width / rect.Height;
            
            if (Math.Abs(currentAspect - targetAspect) > 0.01)
            {
                // Adjust based on which dimension changed more
                int newWidth = (int)(rect.Height * targetAspect);
                int newHeight = (int)(rect.Width / targetAspect);
                
                // Choose adjustment that keeps crop area larger
                if (newWidth >= rect.Width)
                {
                    rect.Width = newWidth;
                }
                else
                {
                    rect.Height = newHeight;
                }

                // Adjust position for top/left corners
                if (corner == "TopLeft")
                {
                    rect.X = _dragStartRect.X + _dragStartRect.Width - rect.Width;
                    rect.Y = _dragStartRect.Y + _dragStartRect.Height - rect.Height;
                }
                else if (corner == "TopRight")
                {
                    rect.Y = _dragStartRect.Y + _dragStartRect.Height - rect.Height;
                }
                else if (corner == "BottomLeft")
                {
                    rect.X = _dragStartRect.X + _dragStartRect.Width - rect.Width;
                }
            }
        }

        private Rectangle ConstrainToImageBounds(Rectangle rect)
        {
            if (_sourceImage == null) return rect;

            // Only constrain position when moving, not when resizing
            // This prevents size changes when dragging to edges
            
            // Ensure rect doesn't go outside image bounds
            // First constrain size if needed (only if it's bigger than image)
            if (rect.Width > _sourceImage.Width)
                rect.Width = _sourceImage.Width;
            if (rect.Height > _sourceImage.Height)
                rect.Height = _sourceImage.Height;

            // Then constrain position (clamp to valid range)
            if (rect.X < 0)
            {
                // Only move position, don't change size
                rect.X = 0;
            }
            if (rect.Y < 0)
            {
                rect.Y = 0;
            }
            if (rect.X + rect.Width > _sourceImage.Width)
            {
                // Move left to fit, don't shrink
                rect.X = Math.Max(0, _sourceImage.Width - rect.Width);
            }
            if (rect.Y + rect.Height > _sourceImage.Height)
            {
                // Move up to fit, don't shrink
                rect.Y = Math.Max(0, _sourceImage.Height - rect.Height);
            }

            return rect;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset crop to initial suggested crop
            _cropRect = _initialCropRect;
            UpdateCropOverlay();
            UpdateInfoText();
            Logger.LogInfo("Crop area reset to initial suggestion");
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            _completionSource?.SetResult(new CropResult { Action = CropAction.Confirm, CropRectangle = _cropRect });
            CloseRequested?.Invoke(this, EventArgs.Empty);
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
                // Mark current item as delete and move to next
                if (_currentBatchIndex >= 0 && _currentBatchIndex < _batchItems.Count)
                {
                    var item = _batchItems[_currentBatchIndex];
                    item.Action = CropAction.Delete;
                    item.CropRect = _cropRect;
                    item.IsEdited = true;
                }
                MoveToNextBatchItem();
            }
            else
            {
                _completionSource?.SetResult(new CropResult { Action = CropAction.Delete, CropRectangle = _cropRect });
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            // Save current and move to next
            SaveCurrentBatchItem();
            if (_currentBatchIndex >= 0 && _currentBatchIndex < _batchItems.Count)
            {
                _batchItems[_currentBatchIndex].Action = CropAction.Confirm;
            }
            MoveToNextBatchItem();
        }

        private void MoveToNextBatchItem()
        {
            if (_currentBatchIndex < _batchItems.Count - 1)
            {
                LoadBatchItem(_currentBatchIndex + 1);
            }
        }

        private void FinalizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Save current item
            SaveCurrentBatchItem();
            if (_currentBatchIndex >= 0 && _currentBatchIndex < _batchItems.Count)
            {
                var currentItem = _batchItems[_currentBatchIndex];
                if (!currentItem.IsEdited)
                {
                    currentItem.Action = CropAction.Confirm;
                    currentItem.IsEdited = true;
                }
            }
            
            // Build results
            var results = _batchItems.Select(item => new BatchCropResult
            {
                FilePath = item.FilePath,
                ImageType = item.ImageType,
                Action = item.Action,
                CropRectangle = item.CropRect ?? item.InitialCropRect,
                TargetWidth = item.TargetWidth,
                TargetHeight = item.TargetHeight
            }).ToList();
            
            _batchCompletionSource?.SetResult(results);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        // Batch list item handlers
        private void BatchItem_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is BatchCropItem item)
            {
                int index = _batchItems.IndexOf(item);
                if (index >= 0 && index != _currentBatchIndex)
                {
                    SaveCurrentBatchItem();
                    if (_currentBatchIndex >= 0 && _currentBatchIndex < _batchItems.Count)
                    {
                        var currentItem = _batchItems[_currentBatchIndex];
                        if (!currentItem.IsEdited)
                        {
                            currentItem.Action = CropAction.Confirm;
                            currentItem.IsEdited = true;
                        }
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
    }

    public enum CropAction
    {
        Confirm,    // Proceed with cropping and optimization
        Skip,       // Skip optimization, only rename file
        Delete      // Delete/remove file completely
    }

    public class CropResult
    {
        public CropAction Action { get; set; }
        public Rectangle CropRectangle { get; set; }
    }

    public class BatchCropResult
    {
        public string FilePath { get; set; } = "";
        public string ImageType { get; set; } = "";
        public CropAction Action { get; set; }
        public Rectangle CropRectangle { get; set; }
        public int TargetWidth { get; set; }
        public int TargetHeight { get; set; }
    }
}
