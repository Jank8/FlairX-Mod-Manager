using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using System.Runtime.InteropServices.WindowsRuntime;

namespace FlairX_Mod_Manager.Controls
{
    public sealed partial class ImageCropInspectionPanel : UserControl
    {
        private System.Drawing.Image? _sourceImage;
        private Rectangle _cropRect;
        private Rectangle _initialCropRect; // Store initial crop for reset
        private double _aspectRatio;
        private bool _maintainAspectRatio;
        private string? _dragHandle;
        private Windows.Foundation.Point _dragStartPoint;
        private Rectangle _dragStartRect;
        private bool _isDragging;
        private TaskCompletionSource<CropResult>? _completionSource;

        private bool _isInitialized = false;

        public event EventHandler? CloseRequested;

        public ImageCropInspectionPanel()
        {
            this.InitializeComponent();
        }

        public Task<CropResult> ShowForImageAsync(System.Drawing.Image sourceImage, Rectangle initialCropRect, double targetAspectRatio, bool maintainAspectRatio, string imageType, bool isProtected = false)
        {
            _sourceImage = sourceImage;
            _cropRect = initialCropRect;
            _initialCropRect = initialCropRect; // Store for reset
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
            
            // Disable Delete button if this file is protected (selected as minitile source)
            DeleteButton.IsEnabled = !isProtected;

            // Load image
            LoadImage();
            UpdateInfoText();
            
            // Update overlay after layout is ready
            _isInitialized = false;
            this.LayoutUpdated += OnLayoutUpdated;

            return _completionSource.Task;
        }

        private void OnLayoutUpdated(object? sender, object e)
        {
            if (!_isInitialized && ImageCanvas.ActualWidth > 0 && ImageCanvas.ActualHeight > 0)
            {
                _isInitialized = true;
                this.LayoutUpdated -= OnLayoutUpdated;
                UpdateCropOverlay();
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

            try
            {
                // Update dimmed areas in image space
                DimTop.Width = _sourceImage.Width;
                DimTop.Height = _cropRect.Y;
                Canvas.SetLeft(DimTop, 0);
                Canvas.SetTop(DimTop, 0);

                DimLeft.Width = _cropRect.X;
                DimLeft.Height = _cropRect.Height;
                Canvas.SetLeft(DimLeft, 0);
                Canvas.SetTop(DimLeft, _cropRect.Y);

                DimRight.Width = _sourceImage.Width - (_cropRect.X + _cropRect.Width);
                DimRight.Height = _cropRect.Height;
                Canvas.SetLeft(DimRight, _cropRect.X + _cropRect.Width);
                Canvas.SetTop(DimRight, _cropRect.Y);

                DimBottom.Width = _sourceImage.Width;
                DimBottom.Height = _sourceImage.Height - (_cropRect.Y + _cropRect.Height);
                Canvas.SetLeft(DimBottom, 0);
                Canvas.SetTop(DimBottom, _cropRect.Y + _cropRect.Height);

                // Convert crop rect from image space to screen space
                var topLeft = ImageCanvas.TransformToVisual(CropOverlayCanvas).TransformPoint(new Windows.Foundation.Point(_cropRect.X, _cropRect.Y));
                var bottomRight = ImageCanvas.TransformToVisual(CropOverlayCanvas).TransformPoint(new Windows.Foundation.Point(_cropRect.X + _cropRect.Width, _cropRect.Y + _cropRect.Height));
                
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
            // Skip optimization, only rename file
            _completionSource?.SetResult(new CropResult { Action = CropAction.Skip, CropRectangle = _cropRect });
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // Delete/remove file completely (old Skip behavior)
            _completionSource?.SetResult(new CropResult { Action = CropAction.Delete, CropRectangle = _cropRect });
            CloseRequested?.Invoke(this, EventArgs.Empty);
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
}
