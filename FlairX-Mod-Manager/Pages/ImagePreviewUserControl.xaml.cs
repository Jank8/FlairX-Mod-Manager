using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ImagePreviewUserControl : UserControl
    {
        private List<string> _availablePreviewImages = new List<string>();
        private int _currentImageIndex = 0;
        public event EventHandler? CloseRequested;
        
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
            }
        }

        public void LoadImages(List<string> imagePaths, int startIndex = 0, string title = "Preview")
        {
            _availablePreviewImages = imagePaths ?? new List<string>();
            _currentImageIndex = Math.Max(0, Math.Min(startIndex, _availablePreviewImages.Count - 1));
            
            UpdateImageNavigation();
            LoadCurrentImage();
        }

        private void LoadCurrentImage()
        {
            try
            {
                if (_availablePreviewImages.Count > 0 && _currentImageIndex >= 0 && _currentImageIndex < _availablePreviewImages.Count)
                {
                    var imagePath = _availablePreviewImages[_currentImageIndex];
                    var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    
                    // Check if it's a URL or local file
                    if (imagePath.StartsWith("http://") || imagePath.StartsWith("https://"))
                    {
                        // Load from URL
                        bitmap.UriSource = new Uri(imagePath);
                    }
                    else
                    {
                        // Load from local file
                        byte[] imageData = File.ReadAllBytes(imagePath);
                        using (var memStream = new MemoryStream(imageData))
                        {
                            bitmap.SetSource(memStream.AsRandomAccessStream());
                        }
                    }
                    PreviewImage.Source = bitmap;

                    // Elastic scale animation
                    var elasticScaleX = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        From = 0.9,
                        To = 1.0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.ElasticEase 
                        { 
                            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
                            Oscillations = 1,
                            Springiness = 6
                        }
                    };
                    
                    if (PreviewImage.RenderTransform == null || !(PreviewImage.RenderTransform is ScaleTransform))
                    {
                        PreviewImage.RenderTransform = new ScaleTransform();
                        PreviewImage.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                    }
                    
                    var scaleTransform = (ScaleTransform)PreviewImage.RenderTransform;
                    var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(elasticScaleX, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(elasticScaleX, "ScaleX");
                    storyboard.Children.Add(elasticScaleX);
                    
                    var elasticScaleY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        From = 0.9,
                        To = 1.0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.ElasticEase 
                        { 
                            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
                            Oscillations = 1,
                            Springiness = 6
                        }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(elasticScaleY, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(elasticScaleY, "ScaleY");
                    storyboard.Children.Add(elasticScaleY);
                    storyboard.Begin();
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
            }
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
    }
}
