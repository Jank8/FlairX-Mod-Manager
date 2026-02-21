using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ImagePreviewUserControl : UserControl
    {
        private List<string> _availablePreviewImages = new List<string>();
        private int _currentImageIndex = 0;
        public event EventHandler? CloseRequested;
        
        // Image cache for URLs
        private static readonly HttpClient _imageHttpClient = new();
        private readonly Dictionary<string, BitmapImage> _imageCache = new();
        
        // Animation throttling
        private DateTime _lastAnimationUpdate = DateTime.MinValue;
        private const int ANIMATION_THROTTLE_MS = 16;
        private double _targetTiltX = 0;
        private double _targetTiltY = 0;

        public ImagePreviewUserControl()
        {
            this.InitializeComponent();
            this.Loaded += ImagePreviewUserControl_Loaded;
            this.ActualThemeChanged += ImagePreviewUserControl_ActualThemeChanged;
            this.Unloaded += ImagePreviewUserControl_Unloaded;
            
            // Set close button tooltip
            var lang = SharedUtilities.LoadLanguageDictionary();
            ToolTipService.SetToolTip(CloseButton, SharedUtilities.GetTranslation(lang, "ImagePreview_Close_Tooltip"));
        }

        private void ImagePreviewUserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ImageCoordinateField != null)
            {
                ImageCoordinateField.PointerMoved -= ImageCoordinateField_PointerMoved;
                ImageCoordinateField.PointerMoved -= ImageCoordinateField_PointerMoved_Parallax;
                ImageCoordinateField.PointerExited -= ImageCoordinateField_PointerExited_Parallax;
                ImageCoordinateField.PointerMoved -= ImageCoordinateField_PointerMoved_Glassmorphism;
                ImageCoordinateField.PointerExited -= ImageCoordinateField_PointerExited_Glassmorphism;
            }
            // Clear cache on unload
            _imageCache.Clear();
        }

        public void LoadImages(List<string> imagePaths, int startIndex = 0, string title = "Preview")
        {
            _availablePreviewImages = imagePaths ?? new List<string>();
            _currentImageIndex = Math.Max(0, Math.Min(startIndex, _availablePreviewImages.Count - 1));
            
            UpdateImageNavigation();
            LoadCurrentImage();
        }

        private async void LoadCurrentImage()
        {
            try
            {
                if (_availablePreviewImages.Count > 0 && _currentImageIndex >= 0 && _currentImageIndex < _availablePreviewImages.Count)
                {
                    var imagePath = _availablePreviewImages[_currentImageIndex];
                    
                    // Check if it's a URL or local file
                    if (imagePath.StartsWith("http://") || imagePath.StartsWith("https://"))
                    {
                        await LoadImageFromUrlAsync(imagePath);
                    }
                    else
                    {
                        LoadImageFromFile(imagePath);
                    }
                }
                else
                {
                    PreviewImage.Source = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error loading preview image", ex);
                PreviewImage.Source = null;
                ImageLoadingBar.Visibility = Visibility.Collapsed;
                ImageBorder.Opacity = 1;
            }
        }

        private void LoadImageFromFile(string imagePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                byte[] imageData = File.ReadAllBytes(imagePath);
                using (var memStream = new MemoryStream(imageData))
                {
                    bitmap.SetSource(memStream.AsRandomAccessStream());
                }
                PreviewImage.Source = bitmap;
                ImageLoadingBar.Visibility = Visibility.Collapsed;
                ImageBorder.Opacity = 1;
                
                // Apply preview effects
                UpdatePreviewEffects(bitmap);
                
                PlayElasticScaleAnimation();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading image from file: {imagePath}", ex);
                PreviewImage.Source = null;
            }
        }

        private async Task LoadImageFromUrlAsync(string imageUrl)
        {
            // Check cache first
            if (_imageCache.TryGetValue(imageUrl, out var cachedBitmap))
            {
                PreviewImage.Source = cachedBitmap;
                ImageLoadingBar.Visibility = Visibility.Collapsed;
                ImageBorder.Opacity = 1;
                
                // Apply preview effects
                UpdatePreviewEffects(cachedBitmap);
                
                PlayElasticScaleAnimation();
                return;
            }
            
            // Show loading bar and hide image
            ImageLoadingBar.Value = 0;
            ImageLoadingBar.Visibility = Visibility.Visible;
            ImageBorder.Opacity = 0;
            
            try
            {
                using var response = await _imageHttpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var memoryStream = new MemoryStream();
                
                var buffer = new byte[8192];
                long bytesRead = 0;
                int read;
                
                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    memoryStream.Write(buffer, 0, read);
                    bytesRead += read;
                    
                    if (totalBytes > 0)
                    {
                        var progress = (double)bytesRead / totalBytes * 100;
                        ImageLoadingBar.Value = progress;
                    }
                }
                
                memoryStream.Position = 0;
                var bitmap = new BitmapImage();
                // Use UriSource for WebP support - save to temp file first
                var tempPath = Path.GetTempFileName();
                File.WriteAllBytes(tempPath, memoryStream.ToArray());
                // IMPORTANT: Must use absolute path for WebP to work
                var absolutePath = Path.GetFullPath(tempPath);
                bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
                
                // Cache the bitmap
                _imageCache[imageUrl] = bitmap;
                
                PreviewImage.Source = bitmap;
                ImageLoadingBar.Visibility = Visibility.Collapsed;
                
                // Apply preview effects
                UpdatePreviewEffects(bitmap);
                
                // Animate in
                PlayFadeInWithElasticScale();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading image from URL: {imageUrl}", ex);
                ImageLoadingBar.Visibility = Visibility.Collapsed;
                ImageBorder.Opacity = 1;
            }
        }

        private void PlayElasticScaleAnimation()
        {
            // Use PreviewEffectHelper for consistent elastic animation across all effects
            PreviewEffectHelper.ApplyElasticAnimation(ImageBorder, PreviewImage.Source);
        }

        private void PlayFadeInWithElasticScale()
        {
            // Use PreviewEffectHelper for consistent elastic animation across all effects
            PreviewEffectHelper.ApplyElasticAnimation(ImageBorder, PreviewImage.Source);
            
            // Fade in border
            var fadeIn = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };
            
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeIn, ImageBorder);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeIn, "Opacity");
            storyboard.Children.Add(fadeIn);
            storyboard.Begin();
        }

        private void UpdateImageNavigation()
        {
            bool hasMultipleImages = _availablePreviewImages.Count > 1;
            
            PrevImageButton.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            NextImageButton.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            ImageCounterBorder.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            
            if (hasMultipleImages)
            {
                ImageCounterText.Text = $"{_currentImageIndex + 1} / {_availablePreviewImages.Count}";
                // Infinite carousel - buttons always active
            }
        }

        private void ImagePreviewUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateBorderColor();
        }

        private void ImagePreviewUserControl_ActualThemeChanged(FrameworkElement sender, object args)
        {
            UpdateBorderColor();
        }

        private void UpdateBorderColor()
        {
            if (ImageBorder != null)
            {
                var currentTheme = this.ActualTheme;
                if (currentTheme == ElementTheme.Dark)
                    ImageBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 70, 70, 70));
                else
                    ImageBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 200, 200, 200));
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CloseButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            AnimateScale(CloseIconScale, 1.1);
        }

        private void CloseButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            AnimateScale(CloseIconScale, 1.0);
        }

        private void AnimateScale(ScaleTransform transform, double scale)
        {
            var animation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = scale,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, transform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, "ScaleX");
            storyboard.Children.Add(animation);
            
            var animationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = scale,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animationY, transform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animationY, "ScaleY");
            storyboard.Children.Add(animationY);
            storyboard.Begin();
        }

        private void PrevImageButton_Click(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;
            if (_availablePreviewImages.Count == 0) return;
            
            // Infinite carousel - wrap to last image
            _currentImageIndex = _currentImageIndex > 0 
                ? _currentImageIndex - 1 
                : _availablePreviewImages.Count - 1;
            LoadCurrentImage();
            UpdateImageNavigation();
        }

        private void NextImageButton_Click(object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;
            if (_availablePreviewImages.Count == 0) return;
            
            // Infinite carousel - wrap to first image
            _currentImageIndex = _currentImageIndex < _availablePreviewImages.Count - 1 
                ? _currentImageIndex + 1 
                : 0;
            LoadCurrentImage();
            UpdateImageNavigation();
        }


        // Tilt effect
        private void ImageCoordinateField_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                ImageCoordinateField.PointerMoved += ImageCoordinateField_PointerMoved;
                CalculateTargetTilt(e);
                AnimateMainTilt(_targetTiltX, _targetTiltY);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ImageCoordinateField_PointerEntered", ex);
            }
        }

        private void ImageCoordinateField_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                var position = e.GetCurrentPoint(ImageCoordinateField.Parent as FrameworkElement);
                var container = ImageCoordinateField.Parent as FrameworkElement;
                
                if (container != null)
                {
                    var bounds = new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight);
                    if (!bounds.Contains(position.Position))
                    {
                        ImageCoordinateField.PointerMoved -= ImageCoordinateField_PointerMoved;
                        _targetTiltX = 0;
                        _targetTiltY = 0;
                        ResetMainTiltEffect();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ImageCoordinateField_PointerExited", ex);
            }
        }

        private void ImageCoordinateField_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                if ((now - _lastAnimationUpdate).TotalMilliseconds < ANIMATION_THROTTLE_MS)
                    return;
                
                _lastAnimationUpdate = now;
                CalculateTargetTilt(e);
                UpdateMainTiltEffectSmooth();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ImageCoordinateField_PointerMoved", ex);
            }
        }

        private void CalculateTargetTilt(PointerRoutedEventArgs e)
        {
            var position = e.GetCurrentPoint(ImageCoordinateField);
            var fieldWidth = ImageCoordinateField.ActualWidth;
            var fieldHeight = ImageCoordinateField.ActualHeight;
            
            if (fieldWidth > 0 && fieldHeight > 0)
            {
                var centerX = fieldWidth / 2;
                var centerY = fieldHeight / 2;
                var offsetX = (position.Position.X - centerX) / centerX;
                var offsetY = (position.Position.Y - centerY) / centerY;
                
                var maxTilt = 6.0;
                _targetTiltX = offsetY * maxTilt;
                _targetTiltY = -offsetX * maxTilt;
            }
        }

        private void UpdateMainTiltEffectSmooth()
        {
            var projection = GetOrCreateProjection(ImageContentRoot);
            if (projection == null) return;
            
            var lerpFactor = 0.2;
            projection.RotationX += (_targetTiltX - projection.RotationX) * lerpFactor;
            projection.RotationY += (_targetTiltY - projection.RotationY) * lerpFactor;
        }

        private void AnimateMainTilt(double tiltX, double tiltY)
        {
            var projection = GetOrCreateProjection(ImageContentRoot);
            if (projection == null) return;
            
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var easing = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(150);
            
            var rotXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = tiltX, Duration = duration, EasingFunction = easing };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotXAnim, projection);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotXAnim, "RotationX");
            storyboard.Children.Add(rotXAnim);
            
            var rotYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = tiltY, Duration = duration, EasingFunction = easing };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotYAnim, projection);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotYAnim, "RotationY");
            storyboard.Children.Add(rotYAnim);
            
            storyboard.Begin();
        }

        private void ResetMainTiltEffect()
        {
            if (ImageContentRoot?.Projection is not PlaneProjection projection) return;
            
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var easing = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase();
            var duration = TimeSpan.FromMilliseconds(250);
            
            var rotXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 0, Duration = duration, EasingFunction = easing };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotXAnim, projection);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotXAnim, "RotationX");
            storyboard.Children.Add(rotXAnim);
            
            var rotYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = 0, Duration = duration, EasingFunction = easing };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotYAnim, projection);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotYAnim, "RotationY");
            storyboard.Children.Add(rotYAnim);
            
            storyboard.Begin();
        }

        private PlaneProjection? GetOrCreateProjection(Grid? container)
        {
            if (container == null) return null;
            
            if (container.Projection is not PlaneProjection projection)
            {
                projection = new PlaneProjection { CenterOfRotationX = 0.5, CenterOfRotationY = 0.5 };
                container.Projection = projection;
            }
            return projection;
        }

        // Preview effects for image preview
        private void UpdatePreviewEffects(Microsoft.UI.Xaml.Media.ImageSource? imageSource)
        {
            // Handle border effect
            if (PreviewEffectHelper.IsBorderEnabled && imageSource != null)
            {
                // Apply border effect and replace the image
                var borderContainer = PreviewEffectHelper.ApplyBorderEffect(ImageBorder, imageSource);
                if (borderContainer != null)
                {
                    // Find the inner border that contains the image
                    var innerBorder = ImageBorder.Child as Border;
                    if (innerBorder != null)
                    {
                        // Replace the image with border container
                        innerBorder.Child = borderContainer;
                    }
                }
            }
            // Handle accent effect
            else if (PreviewEffectHelper.IsAccentEnabled && imageSource != null)
            {
                // Apply accent effect and replace the image
                var accentContainer = PreviewEffectHelper.ApplyAccentEffect(ImageBorder, imageSource);
                if (accentContainer != null)
                {
                    // Find the inner border that contains the image
                    var innerBorder = ImageBorder.Child as Border;
                    if (innerBorder != null)
                    {
                        // Replace the image with accent container
                        innerBorder.Child = accentContainer;
                    }
                }
            }
            // Handle parallax effect
            else if (PreviewEffectHelper.IsParallaxEnabled && imageSource != null)
            {
                // Create parallax container and replace the image
                var parallaxContainer = PreviewEffectHelper.CreateParallaxContainer(imageSource);
                if (parallaxContainer != null)
                {
                    // Find the inner border that contains the image
                    var innerBorder = ImageBorder.Child as Border;
                    if (innerBorder != null)
                    {
                        // Replace the image with parallax container
                        innerBorder.Child = parallaxContainer;
                        
                        // Add mouse move handler to coordinate field for parallax
                        ImageCoordinateField.PointerMoved -= ImageCoordinateField_PointerMoved_Parallax;
                        ImageCoordinateField.PointerMoved += ImageCoordinateField_PointerMoved_Parallax;
                        ImageCoordinateField.PointerExited -= ImageCoordinateField_PointerExited_Parallax;
                        ImageCoordinateField.PointerExited += ImageCoordinateField_PointerExited_Parallax;
                    }
                }
            }
            // Handle glassmorphism effect
            else if (PreviewEffectHelper.IsGlassmorphismEnabled && imageSource != null)
            {
                // Apply glassmorphism frame and replace the image
                var glassmorphismContainer = PreviewEffectHelper.ApplyGlassmorphismEffect(ImageBorder, imageSource);
                if (glassmorphismContainer != null)
                {
                    // Find the inner border that contains the image
                    var innerBorder = ImageBorder.Child as Border;
                    if (innerBorder != null)
                    {
                        // Replace the image with glassmorphism container
                        innerBorder.Child = glassmorphismContainer;
                        
                        // Add mouse move handler to coordinate field for glassmorphism
                        ImageCoordinateField.PointerMoved -= ImageCoordinateField_PointerMoved_Glassmorphism;
                        ImageCoordinateField.PointerMoved += ImageCoordinateField_PointerMoved_Glassmorphism;
                        ImageCoordinateField.PointerExited -= ImageCoordinateField_PointerExited_Glassmorphism;
                        ImageCoordinateField.PointerExited += ImageCoordinateField_PointerExited_Glassmorphism;
                    }
                }
            }
            else
            {
                // Remove parallax and glassmorphism handlers and restore normal image
                ImageCoordinateField.PointerMoved -= ImageCoordinateField_PointerMoved_Parallax;
                ImageCoordinateField.PointerExited -= ImageCoordinateField_PointerExited_Parallax;
                ImageCoordinateField.PointerMoved -= ImageCoordinateField_PointerMoved_Glassmorphism;
                ImageCoordinateField.PointerExited -= ImageCoordinateField_PointerExited_Glassmorphism;
                
                var innerBorder = ImageBorder.Child as Border;
                if (innerBorder != null && (innerBorder.Child is Grid || innerBorder.Child is Border))
                {
                    // Restore normal image
                    innerBorder.Child = PreviewImage;
                }
            }
        }
        
        private void ImageCoordinateField_PointerMoved_Parallax(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (PreviewEffectHelper.IsParallaxEnabled)
            {
                var position = e.GetCurrentPoint(ImageCoordinateField);
                var innerBorder = ImageBorder.Child as Border;
                var parallaxContainer = innerBorder?.Child as Grid;
                
                PreviewEffectHelper.UpdateParallaxEffect(
                    parallaxContainer, 
                    position.Position.X, 
                    position.Position.Y,
                    ImageCoordinateField.ActualWidth,
                    ImageCoordinateField.ActualHeight);
            }
        }
        
        private void ImageCoordinateField_PointerExited_Parallax(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (PreviewEffectHelper.IsParallaxEnabled)
            {
                var innerBorder = ImageBorder.Child as Border;
                var parallaxContainer = innerBorder?.Child as Grid;
                
                PreviewEffectHelper.ResetParallaxEffect(parallaxContainer);
            }
        }

        private void ImageCoordinateField_PointerMoved_Glassmorphism(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (PreviewEffectHelper.IsGlassmorphismEnabled)
            {
                var position = e.GetCurrentPoint(ImageCoordinateField);
                var innerBorder = ImageBorder.Child as Border;
                var glassmorphismContainer = innerBorder?.Child as Grid;
                
                PreviewEffectHelper.UpdateGlassmorphismEffect(
                    glassmorphismContainer, 
                    position.Position.X, 
                    position.Position.Y,
                    ImageCoordinateField.ActualWidth,
                    ImageCoordinateField.ActualHeight);
            }
        }
        
        private void ImageCoordinateField_PointerExited_Glassmorphism(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (PreviewEffectHelper.IsGlassmorphismEnabled)
            {
                var innerBorder = ImageBorder.Child as Border;
                var glassmorphismContainer = innerBorder?.Child as Grid;
                
                PreviewEffectHelper.ResetGlassmorphismEffect(glassmorphismContainer);
            }
        }
    }
}
