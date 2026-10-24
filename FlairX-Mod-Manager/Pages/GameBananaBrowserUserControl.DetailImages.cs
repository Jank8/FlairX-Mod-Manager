using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class GameBananaBrowserUserControl
    {
        private static readonly HttpClient _imageHttpClient = CreateImageHttpClient();

        /// <summary>
        /// Image host sits behind the same Cloudflare protection as the API. A bare
        /// HttpClient (no browser User-Agent, no Referer, no cf_clearance cookie) gets
        /// rejected, which left the detail preview area blank while the mod list still
        /// worked - the list thumbnails are decoded by XAML itself.
        /// </summary>
        private static HttpClient CreateImageHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
                                       | System.Net.DecompressionMethods.Deflate
                                       | System.Net.DecompressionMethods.Brotli,
                UseCookies = true,
                AllowAutoRedirect = true
            };

            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Accept.ParseAdd("image/avif,image/webp,image/apng,image/*,*/*;q=0.8");
            client.DefaultRequestHeaders.Referrer = new Uri("https://gamebanana.com/");
            return client;
        }
        private readonly Dictionary<string, BitmapImage> _imageCache = new();

        // Image slider for details
        private System.Collections.Generic.List<string> _detailPreviewImages = new();
        private int _currentDetailImageIndex = 0;

        // Detail image slider methods
        private async void LoadCurrentDetailImage()
        {
            try
            {
                if (_detailPreviewImages.Count > 0 && _currentDetailImageIndex >= 0 && _currentDetailImageIndex < _detailPreviewImages.Count)
                {
                    var imageUrl = _detailPreviewImages[_currentDetailImageIndex];
                    
                    // Check cache first
                    if (_imageCache.TryGetValue(imageUrl, out var cachedBitmap))
                    {
                        DetailImage.Source = cachedBitmap;
                        DetailImageLoadingBar.Visibility = Visibility.Collapsed;
                        DetailImageBorder.Opacity = 1;
                        
                        // Apply preview effects
                        UpdateDetailPreviewEffects(cachedBitmap);
                        
                        // Apply elastic animation to the appropriate element
                        PreviewEffectHelper.ApplyElasticAnimation(DetailImageBorder, cachedBitmap);
                        return;
                    }
                    
                    // Show loading bar and hide border with image
                    DetailImageLoadingBar.Value = 0;
                    DetailImageLoadingBar.Visibility = Visibility.Visible;
                    DetailImageBorder.Opacity = 0;
                    
                    try
                    {
                        // Download image with progress tracking
                        using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);

                        var cachedUserAgent = CloudflareBypassService.GetCachedUserAgent();
                        request.Headers.TryAddWithoutValidation("User-Agent",
                            string.IsNullOrEmpty(cachedUserAgent)
                                ? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
                                : cachedUserAgent);

                        var cachedCookies = CloudflareBypassService.GetCachedCookies();
                        if (!string.IsNullOrEmpty(cachedCookies))
                            request.Headers.TryAddWithoutValidation("Cookie", cachedCookies);

                        using var response = await _imageHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();
                        
                        var totalBytes = response.Content.Headers.ContentLength ?? -1;
                        var buffer = new byte[8192];
                        var bytesRead = 0L;
                        
                        using var contentStream = await response.Content.ReadAsStreamAsync();
                        using var memoryStream = new MemoryStream();
                        
                        int read;
                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            memoryStream.Write(buffer, 0, read);
                            bytesRead += read;
                            
                            if (totalBytes > 0)
                            {
                                var progress = (double)bytesRead / totalBytes * 100;
                                DetailImageLoadingBar.Value = progress;
                            }
                        }
                        
                        // Create bitmap from downloaded data
                        memoryStream.Position = 0;
                        var bitmap = new BitmapImage();
                        // Use UriSource for WebP support - save to temp file first
                        var tempPath = Path.Combine(PathManager.GetTempDirectory("GbPreview"), $"gbpreview_{Guid.NewGuid():N}.tmp");
                        File.WriteAllBytes(tempPath, memoryStream.ToArray());
                        // IMPORTANT: Must use absolute path for WebP to work
                        var absolutePath = Path.GetFullPath(tempPath);
                        bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
                        
                        // Cache the bitmap
                        _imageCache[imageUrl] = bitmap;
                        
                        DetailImage.Source = bitmap;
                        DetailImageLoadingBar.Visibility = Visibility.Collapsed;
                        
                        // Apply preview effects
                        UpdateDetailPreviewEffects(bitmap);
                        
                        // Apply elastic animation to the appropriate element
                        PreviewEffectHelper.ApplyElasticAnimation(DetailImageBorder, bitmap);
                        
                        // Fade in animation for border
                        var fadeIn = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = new Duration(TimeSpan.FromMilliseconds(200))
                        };
                        
                        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeIn, DetailImageBorder);
                        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeIn, "Opacity");
                        storyboard.Children.Add(fadeIn);
                        storyboard.Begin();
                    }
                    catch (Exception ex)
                    {
                        // Download failed, hide loading bar and show border. Log the URL and
                        // reason - this used to fail silently with a blank preview area.
                        Logger.LogWarning($"Detail preview failed: {imageUrl} - {ex.Message}");

                        // Last resort: let the XAML decoder fetch the URL directly. The mod
                        // list already loads its thumbnails that way, so it uses a different
                        // network stack than HttpClient and can succeed when the manual
                        // download is blocked.
                        try
                        {
                            DetailImage.Source = new BitmapImage(new Uri(imageUrl, UriKind.Absolute));
                        }
                        catch (Exception fallbackEx)
                        {
                            Logger.LogWarning($"Detail preview fallback failed: {imageUrl} - {fallbackEx.Message}");
                        }

                        DetailImageLoadingBar.Visibility = Visibility.Collapsed;
                        DetailImageBorder.Opacity = 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error loading detail image", ex);
            }
        }

        private void UpdateDetailImageNavigation()
        {
            bool hasMultipleImages = _detailPreviewImages.Count > 1;
            
            DetailPrevImageButton.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            DetailNextImageButton.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            DetailImageCounterBorder.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            
            if (hasMultipleImages)
            {
                DetailImageCounterText.Text = $"{_currentDetailImageIndex + 1} / {_detailPreviewImages.Count}";
            }
        }

        private void DetailPrevImageButton_Click(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            e.Handled = true;
            if (_detailPreviewImages.Count == 0) return;
            
            // Infinite carousel - wrap to last image
            _currentDetailImageIndex = _currentDetailImageIndex > 0 
                ? _currentDetailImageIndex - 1 
                : _detailPreviewImages.Count - 1;
            LoadCurrentDetailImage();
            UpdateDetailImageNavigation();
        }

        private void DetailNextImageButton_Click(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            e.Handled = true;
            if (_detailPreviewImages.Count == 0) return;
            
            // Infinite carousel - wrap to first image
            _currentDetailImageIndex = _currentDetailImageIndex < _detailPreviewImages.Count - 1 
                ? _currentDetailImageIndex + 1 
                : 0;
            LoadCurrentDetailImage();
            UpdateDetailImageNavigation();
        }

        // Detail image tilt animation
        private void DetailImageCoordinateField_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Grid grid)
            {
                grid.PointerMoved += DetailImageCoordinateField_PointerMoved;
            }
        }

        private void DetailImageCoordinateField_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Grid grid)
            {
                grid.PointerMoved -= DetailImageCoordinateField_PointerMoved;
                
                // Reset tilt with animation
                if (DetailImageBorder?.Projection is Microsoft.UI.Xaml.Media.PlaneProjection projection)
                {
                    var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    var easing = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase();
                    
                    var rotXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                        EasingFunction = easing
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotXAnim, projection);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotXAnim, "RotationX");
                    storyboard.Children.Add(rotXAnim);
                    
                    var rotYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                        EasingFunction = easing
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotYAnim, projection);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotYAnim, "RotationY");
                    storyboard.Children.Add(rotYAnim);
                    
                    storyboard.Begin();
                }
            }
        }

        private void DetailImageCoordinateField_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Grid grid && DetailImageBorder != null)
            {
                try
                {
                    var position = e.GetCurrentPoint(grid);
                    var width = grid.ActualWidth;
                    var height = grid.ActualHeight;
                    
                    if (width > 0 && height > 0)
                    {
                        var centerX = width / 2;
                        var centerY = height / 2;
                        var offsetX = (position.Position.X - centerX) / centerX;
                        var offsetY = (position.Position.Y - centerY) / centerY;
                        
                        var maxTilt = 10.0;
                        var targetTiltX = -offsetY * maxTilt;
                        var targetTiltY = offsetX * maxTilt;
                        
                        // Get or create projection
                        if (DetailImageBorder.Projection is not Microsoft.UI.Xaml.Media.PlaneProjection projection)
                        {
                            projection = new Microsoft.UI.Xaml.Media.PlaneProjection
                            {
                                CenterOfRotationX = 0.5,
                                CenterOfRotationY = 0.5
                            };
                            DetailImageBorder.Projection = projection;
                        }
                        
                        // Smooth interpolation
                        var lerpFactor = 0.15;
                        projection.RotationX = projection.RotationX + ((targetTiltX - projection.RotationX) * lerpFactor);
                        projection.RotationY = projection.RotationY + ((targetTiltY - projection.RotationY) * lerpFactor);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in DetailImageCoordinateField_PointerMoved", ex);
                }
            }
        }

        // Image Preview - opens sliding panel
        private void DetailImageCoordinateField_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(DetailImageCoordinateField);
            if (point.Properties.IsLeftButtonPressed && _detailPreviewImages.Count > 0)
            {
                // Open image preview in sliding panel
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                var modName = _currentModDetails?.Name ?? "Preview";
                mainWindow?.ShowImagePreviewPanel(_detailPreviewImages, _currentDetailImageIndex, modName);
            }
        }

        // Preview effects for detail image
        private void UpdateDetailPreviewEffects(Microsoft.UI.Xaml.Media.ImageSource? imageSource)
        {
            // Handle border effect
            if (PreviewEffectHelper.IsBorderEnabled && imageSource != null)
            {
                // Apply border effect and replace the image
                var borderContainer = PreviewEffectHelper.ApplyBorderEffect(DetailImageBorder, imageSource);
                if (borderContainer != null)
                {
                    // Find the inner border that contains the image
                    var innerBorder = DetailImageBorder.Child as Border;
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
                var accentContainer = PreviewEffectHelper.ApplyAccentEffect(DetailImageBorder, imageSource);
                if (accentContainer != null)
                {
                    // Find the inner border that contains the image
                    var innerBorder = DetailImageBorder.Child as Border;
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
                    var innerBorder = DetailImageBorder.Child as Border;
                    if (innerBorder != null)
                    {
                        // Replace the image with parallax container
                        innerBorder.Child = parallaxContainer;
                        
                        // Add mouse move handler to coordinate field for parallax
                        DetailImageCoordinateField.PointerMoved -= DetailImageCoordinateField_PointerMoved_Parallax;
                        DetailImageCoordinateField.PointerMoved += DetailImageCoordinateField_PointerMoved_Parallax;
                        DetailImageCoordinateField.PointerExited -= DetailImageCoordinateField_PointerExited_Parallax;
                        DetailImageCoordinateField.PointerExited += DetailImageCoordinateField_PointerExited_Parallax;
                    }
                }
            }
            // Handle glassmorphism effect
            else if (PreviewEffectHelper.IsGlassmorphismEnabled && imageSource != null)
            {
                // Apply glassmorphism frame and replace the image
                var glassmorphismContainer = PreviewEffectHelper.ApplyGlassmorphismEffect(DetailImageBorder, imageSource);
                if (glassmorphismContainer != null)
                {
                    // Find the inner border that contains the image
                    var innerBorder = DetailImageBorder.Child as Border;
                    if (innerBorder != null)
                    {
                        // Replace the image with glassmorphism container
                        innerBorder.Child = glassmorphismContainer;
                        
                        // Add mouse move handler to coordinate field for glassmorphism
                        DetailImageCoordinateField.PointerMoved -= DetailImageCoordinateField_PointerMoved_Glassmorphism;
                        DetailImageCoordinateField.PointerMoved += DetailImageCoordinateField_PointerMoved_Glassmorphism;
                        DetailImageCoordinateField.PointerExited -= DetailImageCoordinateField_PointerExited_Glassmorphism;
                        DetailImageCoordinateField.PointerExited += DetailImageCoordinateField_PointerExited_Glassmorphism;
                    }
                }
            }
            else
            {
                // Remove parallax and glassmorphism handlers and restore normal image
                DetailImageCoordinateField.PointerMoved -= DetailImageCoordinateField_PointerMoved_Parallax;
                DetailImageCoordinateField.PointerExited -= DetailImageCoordinateField_PointerExited_Parallax;
                DetailImageCoordinateField.PointerMoved -= DetailImageCoordinateField_PointerMoved_Glassmorphism;
                DetailImageCoordinateField.PointerExited -= DetailImageCoordinateField_PointerExited_Glassmorphism;
                
                var innerBorder = DetailImageBorder.Child as Border;
                if (innerBorder != null && (innerBorder.Child is Grid || innerBorder.Child is Border))
                {
                    // Restore normal image
                    innerBorder.Child = DetailImage;
                }
            }
        }
        
        private void DetailImageCoordinateField_PointerMoved_Parallax(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (PreviewEffectHelper.IsParallaxEnabled)
            {
                var position = e.GetCurrentPoint(DetailImageCoordinateField);
                var innerBorder = DetailImageBorder.Child as Border;
                var parallaxContainer = innerBorder?.Child as Grid;
                
                PreviewEffectHelper.UpdateParallaxEffect(
                    parallaxContainer, 
                    position.Position.X, 
                    position.Position.Y,
                    DetailImageCoordinateField.ActualWidth,
                    DetailImageCoordinateField.ActualHeight);
            }
        }
        
        private void DetailImageCoordinateField_PointerExited_Parallax(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (PreviewEffectHelper.IsParallaxEnabled)
            {
                var innerBorder = DetailImageBorder.Child as Border;
                var parallaxContainer = innerBorder?.Child as Grid;
                
                PreviewEffectHelper.ResetParallaxEffect(parallaxContainer);
            }
        }

        private void DetailImageCoordinateField_PointerMoved_Glassmorphism(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (PreviewEffectHelper.IsGlassmorphismEnabled)
            {
                var position = e.GetCurrentPoint(DetailImageCoordinateField);
                var innerBorder = DetailImageBorder.Child as Border;
                var glassmorphismContainer = innerBorder?.Child as Grid;
                
                PreviewEffectHelper.UpdateGlassmorphismEffect(
                    glassmorphismContainer, 
                    position.Position.X, 
                    position.Position.Y,
                    DetailImageCoordinateField.ActualWidth,
                    DetailImageCoordinateField.ActualHeight);
            }
        }
        
        private void DetailImageCoordinateField_PointerExited_Glassmorphism(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (PreviewEffectHelper.IsGlassmorphismEnabled)
            {
                var innerBorder = DetailImageBorder.Child as Border;
                var glassmorphismContainer = innerBorder?.Child as Grid;
                
                PreviewEffectHelper.ResetGlassmorphismEffect(glassmorphismContainer);
            }
        }
    }
}
