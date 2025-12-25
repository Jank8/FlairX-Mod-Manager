using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using Microsoft.UI;
using System.Linq;
using System;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Available preview effects
    /// </summary>
    public enum PreviewEffectType
    {
        None,
        Border,
        Glow,
        Parallax,
        Glassmorphism
    }

    /// <summary>
    /// Helper class for managing preview image effects
    /// </summary>
    public static class PreviewEffectHelper
    {
        /// <summary>
        /// Get current effect type from settings
        /// </summary>
        public static PreviewEffectType GetCurrentEffect()
        {
            var effectString = SettingsManager.Current.PreviewEffect ?? "None";
            return effectString switch
            {
                "Border" => PreviewEffectType.Border,
                "Glow" => PreviewEffectType.Glow,
                "Parallax" => PreviewEffectType.Parallax,
                "Glassmorphism" => PreviewEffectType.Glassmorphism,
                _ => PreviewEffectType.None
            };
        }

        /// <summary>
        /// Apply border effect - copy glassmorphism but with border color and no parallax
        /// </summary>
        public static Grid? ApplyBorderEffect(Border? border, ImageSource? imageSource)
        {
            if (border == null || imageSource == null || GetCurrentEffect() != PreviewEffectType.Border)
            {
                return null;
            }

            // Create container for layered effect
            var borderContainer = new Grid
            {
                Name = "BorderContainer"
            };

            // Border background layer using CardStrokeColorDefaultBrush
            var borderLayer = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"]
            };

            // Top image layer (smaller, creates the border effect by covering center)
            var topImageBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(15), // 15px margin creates the border
                Child = new Image
                {
                    Source = imageSource,
                    Stretch = Stretch.Uniform
                }
            };

            // Add layers to container (border layer, top image)
            borderContainer.Children.Add(borderLayer);
            borderContainer.Children.Add(topImageBorder);

            return borderContainer;
        }

        /// <summary>
        /// Apply glow effect to a Border element
        /// </summary>
        public static void ApplyGlowEffect(Border? border)
        {
            if (border == null || GetCurrentEffect() != PreviewEffectType.Glow)
            {
                if (border != null)
                {
                    // Remove glow effect but don't clear border if Border effect is active
                    if (GetCurrentEffect() != PreviewEffectType.Border)
                    {
                        border.Background = null;
                        border.BorderBrush = null;
                        border.BorderThickness = new Thickness(0);
                    }
                    
                    // Remove any existing glow layers
                    var parentGrid = border.Parent as Grid;
                    if (parentGrid != null)
                    {
                        var glowLayers = parentGrid.Children.OfType<Border>().Where(b => b.Name?.StartsWith("GlowLayer") == true).ToList();
                        foreach (var layer in glowLayers)
                        {
                            parentGrid.Children.Remove(layer);
                        }
                    }
                }
                return;
            }

            // Get system accent color - make it brighter
            var accentColor = (Color)Application.Current.Resources["SystemAccentColor"];
            
            // Create multiple glow layers behind the main border
            var containerGrid = border.Parent as Grid;
            if (containerGrid != null)
            {
                // Remove existing glow layers first
                var existingGlowLayers = containerGrid.Children.OfType<Border>().Where(b => b.Name?.StartsWith("GlowLayer") == true).ToList();
                foreach (var layer in existingGlowLayers)
                {
                    containerGrid.Children.Remove(layer);
                }
                
                // Find the index of the main border
                int borderIndex = containerGrid.Children.IndexOf(border);
                
                // Create 3 glow layers with increasing size and decreasing opacity
                for (int i = 2; i >= 0; i--)
                {
                    var glowLayer = new Border
                    {
                        Name = $"GlowLayer{i}",
                        CornerRadius = new CornerRadius(border.CornerRadius.TopLeft + (i + 1) * 2),
                        Background = new SolidColorBrush(Color.FromArgb((byte)(80 - i * 20), accentColor.R, accentColor.G, accentColor.B)),
                        Margin = new Thickness(-(i + 1) * 3),
                        IsHitTestVisible = false
                    };
                    
                    containerGrid.Children.Insert(borderIndex, glowLayer);
                }
            }
        }

        /// <summary>
        /// Create parallax background for an image
        /// </summary>
        public static Grid? CreateParallaxContainer(ImageSource? imageSource)
        {
            if (GetCurrentEffect() != PreviewEffectType.Parallax || imageSource == null)
                return null;

            // Create container for parallax effect
            var parallaxContainer = new Grid();

            // Create background layer (blurred and scaled version of the same image)
            var backgroundImage = new Image
            {
                Source = imageSource,
                Stretch = Stretch.UniformToFill,
                Opacity = 0.3,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = new CompositeTransform
                {
                    ScaleX = 1.5,
                    ScaleY = 1.5
                },
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5)
            };

            // Add blur effect to background (simulated with opacity and scaling)
            var backgroundOverlay = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0))
            };

            // Create foreground image layer with border for rounded corners and clipping
            var foregroundBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = new CompositeTransform
                {
                    ScaleX = 0.9,
                    ScaleY = 0.9
                },
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5)
            };

            var foregroundImage = new Image
            {
                Source = imageSource,
                Stretch = Stretch.Uniform
            };

            foregroundBorder.Child = foregroundImage;

            // Set up clipping for rounded corners when border loads
            foregroundBorder.Loaded += (s, e) =>
            {
                var border = s as Border;
                if (border != null)
                {
                    var clip = new RectangleGeometry
                    {
                        Rect = new Windows.Foundation.Rect(0, 0, border.ActualWidth, border.ActualHeight)
                    };
                    border.Clip = clip;
                }
            };

            parallaxContainer.Children.Add(backgroundImage);
            parallaxContainer.Children.Add(backgroundOverlay);
            parallaxContainer.Children.Add(foregroundBorder);

            return parallaxContainer;
        }

        /// <summary>
        /// Update parallax effect based on mouse position with smooth animation
        /// </summary>
        public static void UpdateParallaxEffect(Grid? parallaxContainer, double mouseX, double mouseY, double containerWidth, double containerHeight)
        {
            if (parallaxContainer?.Children.Count >= 3)
            {
                // Calculate offset based on mouse position (normalized to -1 to 1)
                double offsetX = ((mouseX / containerWidth) - 0.5) * 2;
                double offsetY = ((mouseY / containerHeight) - 0.5) * 2;

                // Apply different movement speeds to create parallax effect with animation
                if (parallaxContainer.Children[0] is Image backgroundImage && 
                    backgroundImage.RenderTransform is CompositeTransform backgroundTransform)
                {
                    // Background moves slower (0.3x speed) with smooth animation
                    AnimateTransform(backgroundTransform, "TranslateX", offsetX * 6, 250);
                    AnimateTransform(backgroundTransform, "TranslateY", offsetY * 6, 250);
                }

                if (parallaxContainer.Children[2] is Border foregroundBorder && 
                    foregroundBorder.RenderTransform is CompositeTransform foregroundTransform)
                {
                    // Foreground moves instantly for smooth mouse tracking
                    foregroundTransform.TranslateX = offsetX * -20;
                    foregroundTransform.TranslateY = offsetY * -20;
                }
            }
        }

        /// <summary>
        /// Reset parallax effect to center position with smooth animation
        /// </summary>
        public static void ResetParallaxEffect(Grid? parallaxContainer)
        {
            if (parallaxContainer?.Children.Count >= 3)
            {
                if (parallaxContainer.Children[0] is Image backgroundImage && 
                    backgroundImage.RenderTransform is CompositeTransform backgroundTransform)
                {
                    AnimateTransform(backgroundTransform, "TranslateX", 0, 400);
                    AnimateTransform(backgroundTransform, "TranslateY", 0, 400);
                }

                if (parallaxContainer.Children[2] is Border foregroundBorder && 
                    foregroundBorder.RenderTransform is CompositeTransform foregroundTransform)
                {
                    AnimateTransform(foregroundTransform, "TranslateX", 0, 300);
                    AnimateTransform(foregroundTransform, "TranslateY", 0, 300);
                }
            }
        }

        /// <summary>
        /// Animate a transform property smoothly
        /// </summary>
        private static void AnimateTransform(CompositeTransform transform, string property, double toValue, int durationMs)
        {
            var animation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = toValue,
                Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase
                {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut
                }
            };

            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, transform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, property);
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        /// <summary>
        /// Check if glow effect is enabled
        /// </summary>
        public static bool IsGlowEnabled => GetCurrentEffect() == PreviewEffectType.Glow;
        
        /// <summary>
        /// Check if border effect is enabled
        /// </summary>
        public static bool IsBorderEnabled => GetCurrentEffect() == PreviewEffectType.Border;
        
        /// <summary>
        /// Check if parallax effect is enabled
        /// </summary>
        public static bool IsParallaxEnabled => GetCurrentEffect() == PreviewEffectType.Parallax;
        
        /// <summary>
        /// Check if glassmorphism effect is enabled
        /// </summary>
        public static bool IsGlassmorphismEnabled => GetCurrentEffect() == PreviewEffectType.Glassmorphism;

        /// <summary>
        /// Update glassmorphism effect - move background image with parallax
        /// </summary>
        public static void UpdateGlassmorphismEffect(Grid? glassmorphismContainer, double mouseX, double mouseY, double containerWidth, double containerHeight)
        {
            if (glassmorphismContainer?.Children.Count >= 3)
            {
                // Calculate offset based on mouse position
                double offsetX = ((mouseX / containerWidth) - 0.5) * 2;
                double offsetY = ((mouseY / containerHeight) - 0.5) * 2;

                // Background image moves instantly with larger movement (150% scale gives more room)
                if (glassmorphismContainer.Children[0] is Image backgroundImage && 
                    backgroundImage.RenderTransform is CompositeTransform backgroundTransform)
                {
                    backgroundTransform.TranslateX = offsetX * 25; // Increased from 10 to 25
                    backgroundTransform.TranslateY = offsetY * 25;
                }
            }
        }

        /// <summary>
        /// Reset glassmorphism effect - center background image
        /// </summary>
        public static void ResetGlassmorphismEffect(Grid? glassmorphismContainer)
        {
            if (glassmorphismContainer?.Children.Count >= 3)
            {
                if (glassmorphismContainer.Children[0] is Image backgroundImage && 
                    backgroundImage.RenderTransform is CompositeTransform backgroundTransform)
                {
                    backgroundTransform.TranslateX = 0;
                    backgroundTransform.TranslateY = 0;
                }
            }
        }

        /// <summary>
        /// Apply glassmorphism effect with acrylic frame as mask over background image
        /// </summary>
        public static Grid? ApplyGlassmorphismEffect(Border? border, ImageSource? imageSource)
        {
            if (border == null || imageSource == null || GetCurrentEffect() != PreviewEffectType.Glassmorphism)
            {
                return null;
            }

            // Create container for layered effect
            var glassmorphismContainer = new Grid
            {
                Name = "GlassmorphismContainer"
            };

            // Background image layer (150% scaled, will move with parallax - more room to move)
            var backgroundImage = new Image
            {
                Source = imageSource,
                Stretch = Stretch.Uniform,
                RenderTransform = new CompositeTransform 
                { 
                    ScaleX = 1.5, 
                    ScaleY = 1.5,
                    CenterX = 0.5,
                    CenterY = 0.5
                },
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5)
            };

            // Acrylic layer using LauncherAcrylicBrush (same as start button)
            var blurLayer = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = (Brush)Application.Current.Resources["LauncherAcrylicBrush"]
            };

            // Top image layer (smaller, creates the "hole" effect by covering center)
            var topImageBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(15), // 15px margin creates the frame
                Child = new Image
                {
                    Source = imageSource,
                    Stretch = Stretch.Uniform
                }
            };

            // Add layers to container (background, blur layer, top image)
            glassmorphismContainer.Children.Add(backgroundImage);
            glassmorphismContainer.Children.Add(blurLayer);
            glassmorphismContainer.Children.Add(topImageBorder);

            return glassmorphismContainer;
        }
    }
}
