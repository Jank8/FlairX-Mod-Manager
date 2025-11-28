using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ImageCropUserControl : UserControl
    {
        private BitmapImage? _bitmapImage;
        private System.Drawing.Image? _sourceImage;
        private double _aspectRatio;
        private string _cropType = "";
        
        // Crop rectangle in image coordinates
        private Rectangle _cropRectangle;
        
        // Dragging state
        private bool _isDragging = false;
        private bool _isResizing = false;
        private string _resizeHandle = "";
        private Windows.Foundation.Point _dragStartPoint;
        private Rectangle _dragStartRect;
        
        // Completion source for async result
        private TaskCompletionSource<Rectangle?>? _completionSource;

        public event EventHandler? CloseRequested;

        public ImageCropUserControl()
        {
            this.InitializeComponent();
            this.Loaded += ImageCropUserControl_Loaded;
        }

        public Task<Rectangle?> ShowAsync(System.Drawing.Image sourceImage, string cropType, int targetWidth, int targetHeight)
        {
            _sourceImage = sourceImage;
            _cropType = cropType;
            _aspectRatio = (double)targetWidth / targetHeight;
            _completionSource = new TaskCompletionSource<Rectangle?>();
            
            // Load translations
            var lang = SharedUtilities.LoadLanguageDictionary();
            
            // Set title based on crop type
            TitleText.Text = cropType switch
            {
                "minitile" => SharedUtilities.GetTranslation(lang, "CropDialog_Minitile_Title") ?? "Crop Mod Thumbnail",
                "catprev" => SharedUtilities.GetTranslation(lang, "CropDialog_Catprev_Title") ?? "Crop Category Preview",
                "catmini" => SharedUtilities.GetTranslation(lang, "CropDialog_Catmini_Title") ?? "Crop Category Icon",
                _ => SharedUtilities.GetTranslation(lang, "CropDialog_Title") ?? "Crop Image"
            };
            
            DescriptionText.Text = SharedUtilities.GetTranslation(lang, "CropDialog_Description") ?? 
                "Adjust the crop area for optimal framing";
            
            InfoText.Text = SharedUtilities.GetTranslation(lang, "CropDialog_Info") ?? 
                "Drag to move • Drag corners to resize";
            
            TargetSizeText.Text = $"Target: {targetWidth}×{targetHeight}";
            
            ApplyButton.Content = SharedUtilities.GetTranslation(lang, "Apply") ?? "Apply";
            SkipButton.Content = SharedUtilities.GetTranslation(lang, "Skip") ?? "Skip";
            
            return _completionSource.Task;
        }

        private void ImageCropUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_sourceImage == null) return;
                
                // Convert System.Drawing.Image to BitmapImage for display
                using (var ms = new MemoryStream())
                {
                    _sourceImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    
                    _bitmapImage = new BitmapImage();
                    _bitmapImage.SetSource(ms.AsRandomAccessStream());
                    SourceImage.Source = _bitmapImage;
                }
                
                // Wait for image to load and layout to complete
                SourceImage.ImageOpened += (s, args) =>
                {
                    try
                    {
                        InitializeCropRectangle();
                        UpdateCropOverlay();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to initialize crop rectangle", ex);
                        HandleCropError();
                    }
                };
                
                // Update overlay when canvas size changes
                ImageCanvas.SizeChanged += (s, args) =>
                {
                    try
                    {
                        UpdateCropOverlay();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to update crop overlay", ex);
                    }
                };
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load crop control", ex);
                HandleCropError();
            }
        }
        
        private void HandleCropError()
        {
            // Return null to indicate error/skip
            _completionSource?.TrySetResult(null);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void InitializeCropRectangle()
        {
            if (_sourceImage == null) return;
            
            // Calculate initial crop rectangle (centered, maximum size with correct aspect ratio)
            int sourceWidth = _sourceImage.Width;
            int sourceHeight = _sourceImage.Height;
            double sourceRatio = (double)sourceWidth / sourceHeight;
            
            int cropWidth, cropHeight;
            
            if (sourceRatio > _aspectRatio)
            {
                // Source is wider - fit to height
                cropHeight = sourceHeight;
                cropWidth = (int)(cropHeight * _aspectRatio);
            }
            else
            {
                // Source is taller - fit to width
                cropWidth = sourceWidth;
                cropHeight = (int)(cropWidth / _aspectRatio);
            }
            
            int cropX = (sourceWidth - cropWidth) / 2;
            int cropY = (sourceHeight - cropHeight) / 2;
            
            _cropRectangle = new Rectangle(cropX, cropY, cropWidth, cropHeight);
        }

        private void UpdateCropOverlay()
        {
            if (_sourceImage == null || SourceImage.ActualWidth == 0 || SourceImage.ActualHeight == 0)
                return;
            
            // Calculate scale factor from image coordinates to canvas coordinates
            double scaleX = SourceImage.ActualWidth / _sourceImage.Width;
            double scaleY = SourceImage.ActualHeight / _sourceImage.Height;
            double scale = Math.Min(scaleX, scaleY);
            
            // Calculate image position on canvas (centered)
            double imageWidth = _sourceImage.Width * scale;
            double imageHeight = _sourceImage.Height * scale;
            double imageLeft = (ImageCanvas.ActualWidth - imageWidth) / 2;
            double imageTop = (ImageCanvas.ActualHeight - imageHeight) / 2;
            
            // Convert crop rectangle to canvas coordinates
            double cropLeft = imageLeft + (_cropRectangle.X * scale);
            double cropTop = imageTop + (_cropRectangle.Y * scale);
            double cropWidth = _cropRectangle.Width * scale;
            double cropHeight = _cropRectangle.Height * scale;
            
            // Update crop rectangle
            Canvas.SetLeft(CropRect, cropLeft);
            Canvas.SetTop(CropRect, cropTop);
            CropRect.Width = cropWidth;
            CropRect.Height = cropHeight;
            
            // Update dimmed areas
            DimTop.Width = ImageCanvas.ActualWidth;
            DimTop.Height = cropTop;
            Canvas.SetLeft(DimTop, 0);
            Canvas.SetTop(DimTop, 0);
            
            DimLeft.Width = cropLeft;
            DimLeft.Height = cropHeight;
            Canvas.SetLeft(DimLeft, 0);
            Canvas.SetTop(DimLeft, cropTop);
            
            DimRight.Width = ImageCanvas.ActualWidth - (cropLeft + cropWidth);
            DimRight.Height = cropHeight;
            Canvas.SetLeft(DimRight, cropLeft + cropWidth);
            Canvas.SetTop(DimRight, cropTop);
            
            DimBottom.Width = ImageCanvas.ActualWidth;
            DimBottom.Height = ImageCanvas.ActualHeight - (cropTop + cropHeight);
            Canvas.SetLeft(DimBottom, 0);
            Canvas.SetTop(DimBottom, cropTop + cropHeight);
            
            // Update corner handles
            Canvas.SetLeft(HandleTopLeft, cropLeft - 6);
            Canvas.SetTop(HandleTopLeft, cropTop - 6);
            
            Canvas.SetLeft(HandleTopRight, cropLeft + cropWidth - 6);
            Canvas.SetTop(HandleTopRight, cropTop - 6);
            
            Canvas.SetLeft(HandleBottomLeft, cropLeft - 6);
            Canvas.SetTop(HandleBottomLeft, cropTop + cropHeight - 6);
            
            Canvas.SetLeft(HandleBottomRight, cropLeft + cropWidth - 6);
            Canvas.SetTop(HandleBottomRight, cropTop + cropHeight - 6);
            
            // Update info text
            CropSizeText.Text = $"Crop: {_cropRectangle.Width}×{_cropRectangle.Height}";
        }

        private void ImageCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_sourceImage == null) return;
            
            var point = e.GetCurrentPoint(ImageCanvas).Position;
            _dragStartPoint = point;
            _dragStartRect = _cropRectangle;
            
            // Check if clicking on a handle
            if (IsPointNearHandle(point, HandleTopLeft))
            {
                _isResizing = true;
                _resizeHandle = "TopLeft";
                ImageCanvas.CapturePointer(e.Pointer);
            }
            else if (IsPointNearHandle(point, HandleTopRight))
            {
                _isResizing = true;
                _resizeHandle = "TopRight";
                ImageCanvas.CapturePointer(e.Pointer);
            }
            else if (IsPointNearHandle(point, HandleBottomLeft))
            {
                _isResizing = true;
                _resizeHandle = "BottomLeft";
                ImageCanvas.CapturePointer(e.Pointer);
            }
            else if (IsPointNearHandle(point, HandleBottomRight))
            {
                _isResizing = true;
                _resizeHandle = "BottomRight";
                ImageCanvas.CapturePointer(e.Pointer);
            }
            else if (IsPointInCropRect(point))
            {
                _isDragging = true;
                ImageCanvas.CapturePointer(e.Pointer);
            }
        }

        private void ImageCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_sourceImage == null) return;
            
            var point = e.GetCurrentPoint(ImageCanvas).Position;
            
            if (_isDragging)
            {
                // Move crop rectangle
                double deltaX = point.X - _dragStartPoint.X;
                double deltaY = point.Y - _dragStartPoint.Y;
                
                // Convert delta to image coordinates
                double scale = GetImageScale();
                int imageDeltaX = (int)(deltaX / scale);
                int imageDeltaY = (int)(deltaY / scale);
                
                int newX = _dragStartRect.X + imageDeltaX;
                int newY = _dragStartRect.Y + imageDeltaY;
                
                // Constrain to image bounds
                newX = Math.Max(0, Math.Min(newX, _sourceImage.Width - _cropRectangle.Width));
                newY = Math.Max(0, Math.Min(newY, _sourceImage.Height - _cropRectangle.Height));
                
                _cropRectangle.X = newX;
                _cropRectangle.Y = newY;
                
                UpdateCropOverlay();
            }
            else if (_isResizing)
            {
                // Resize crop rectangle while maintaining aspect ratio
                double deltaX = point.X - _dragStartPoint.X;
                double deltaY = point.Y - _dragStartPoint.Y;
                
                double scale = GetImageScale();
                int imageDeltaX = (int)(deltaX / scale);
                int imageDeltaY = (int)(deltaY / scale);
                
                ResizeCropRectangle(imageDeltaX, imageDeltaY);
                UpdateCropOverlay();
            }
        }

        private void ImageCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = false;
            _isResizing = false;
            _resizeHandle = "";
            ImageCanvas.ReleasePointerCapture(e.Pointer);
        }

        private bool IsPointNearHandle(Windows.Foundation.Point point, FrameworkElement handle)
        {
            double handleX = Canvas.GetLeft(handle) + 6;
            double handleY = Canvas.GetTop(handle) + 6;
            double distance = Math.Sqrt(Math.Pow(point.X - handleX, 2) + Math.Pow(point.Y - handleY, 2));
            return distance < 20; // 20 pixel tolerance
        }

        private bool IsPointInCropRect(Windows.Foundation.Point point)
        {
            double left = Canvas.GetLeft(CropRect);
            double top = Canvas.GetTop(CropRect);
            double right = left + CropRect.Width;
            double bottom = top + CropRect.Height;
            
            return point.X >= left && point.X <= right && point.Y >= top && point.Y <= bottom;
        }

        private double GetImageScale()
        {
            if (_sourceImage == null) return 1.0;
            
            double scaleX = SourceImage.ActualWidth / _sourceImage.Width;
            double scaleY = SourceImage.ActualHeight / _sourceImage.Height;
            return Math.Min(scaleX, scaleY);
        }

        private void ResizeCropRectangle(int deltaX, int deltaY)
        {
            if (_sourceImage == null) return;
            
            Rectangle newRect = _dragStartRect;
            
            // Calculate new dimensions based on handle being dragged
            switch (_resizeHandle)
            {
                case "TopLeft":
                    newRect.X = _dragStartRect.X + deltaX;
                    newRect.Y = _dragStartRect.Y + deltaY;
                    newRect.Width = _dragStartRect.Width - deltaX;
                    newRect.Height = _dragStartRect.Height - deltaY;
                    break;
                    
                case "TopRight":
                    newRect.Y = _dragStartRect.Y + deltaY;
                    newRect.Width = _dragStartRect.Width + deltaX;
                    newRect.Height = _dragStartRect.Height - deltaY;
                    break;
                    
                case "BottomLeft":
                    newRect.X = _dragStartRect.X + deltaX;
                    newRect.Width = _dragStartRect.Width - deltaX;
                    newRect.Height = _dragStartRect.Height + deltaY;
                    break;
                    
                case "BottomRight":
                    newRect.Width = _dragStartRect.Width + deltaX;
                    newRect.Height = _dragStartRect.Height + deltaY;
                    break;
            }
            
            // Maintain aspect ratio - use width as primary dimension
            newRect.Height = (int)(newRect.Width / _aspectRatio);
            
            // Adjust position for top/left handles
            if (_resizeHandle.Contains("Top"))
            {
                newRect.Y = _dragStartRect.Y + _dragStartRect.Height - newRect.Height;
            }
            if (_resizeHandle.Contains("Left"))
            {
                newRect.X = _dragStartRect.X + _dragStartRect.Width - newRect.Width;
            }
            
            // Constrain to minimum size (50x50)
            if (newRect.Width < 50 || newRect.Height < 50)
                return;
            
            // Constrain to image bounds
            if (newRect.X < 0 || newRect.Y < 0 || 
                newRect.X + newRect.Width > _sourceImage.Width || 
                newRect.Y + newRect.Height > _sourceImage.Height)
                return;
            
            _cropRectangle = newRect;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            _completionSource?.SetResult(_cropRectangle);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            _completionSource?.SetResult(null);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _completionSource?.SetResult(null);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
