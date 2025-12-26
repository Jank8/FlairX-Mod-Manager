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
        Accent,
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
                "Accent" => PreviewEffectType.Accent,
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

            // Apply elastic animation to the container
            ApplyElasticAnimationToElement(borderContainer);

            return borderContainer;
        }

        /// <summary>
        /// Apply accent effect - same as border but with system accent color
        /// </summary>
        public static Grid? ApplyAccentEffect(Border? border, ImageSource? imageSource)
        {
            if (border == null || imageSource == null || GetCurrentEffect() != PreviewEffectType.Accent)
            {
                return null;
            }

            // Create container for layered effect
            var accentContainer = new Grid
            {
                Name = "AccentContainer"
            };

            // Get system accent color
            var accentColor = (Color)Application.Current.Resources["SystemAccentColor"];
            
            // Accent background layer using system accent color
            var accentLayer = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(accentColor)
            };

            // Top image layer (smaller, creates the accent border effect by covering center)
            var topImageBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(15), // 15px margin creates the accent border
                Child = new Image
                {
                    Source = imageSource,
                    Stretch = Stretch.Uniform
                }
            };

            // Add layers to container (accent layer, top image)
            accentContainer.Children.Add(accentLayer);
            accentContainer.Children.Add(topImageBorder);

            // Apply elastic animation to the container
            ApplyElasticAnimationToElement(accentContainer);

            return accentContainer;
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

            // Apply elastic animation to the container
            ApplyElasticAnimationToElement(parallaxContainer);

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
        /// Check if border effect is enabled
        /// </summary>
        public static bool IsBorderEnabled => GetCurrentEffect() == PreviewEffectType.Border;
        
        /// <summary>
        /// Check if accent effect is enabled
        /// </summary>
        public static bool IsAccentEnabled => GetCurrentEffect() == PreviewEffectType.Accent;
        
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

            // Apply elastic animation to the container
            ApplyElasticAnimationToElement(glassmorphismContainer);

            return glassmorphismContainer;
        }
        /// <summary>
        /// Apply elastic scale animation to the appropriate element based on current effect
        /// </summary>
        public static void ApplyElasticAnimation(Border? border, Microsoft.UI.Xaml.Media.ImageSource? imageSource)
        {
            if (border == null) return;

            var currentEffect = GetCurrentEffect();
            FrameworkElement? targetElement = null;

            switch (currentEffect)
            {
                case PreviewEffectType.None:
                    // For None effect, animate the image directly
                    var innerBorder = border.Child as Border;
                    targetElement = innerBorder?.Child as Image;
                    break;

                case PreviewEffectType.Border:
                case PreviewEffectType.Accent:
                    // For Border/Accent effects, animate the container
                    var borderInner = border.Child as Border;
                    targetElement = borderInner?.Child as Grid;
                    break;

                case PreviewEffectType.Parallax:
                    // For Parallax effect, animate the parallax container
                    var parallaxInner = border.Child as Border;
                    targetElement = parallaxInner?.Child as Grid;
                    break;

                case PreviewEffectType.Glassmorphism:
                    // For Glassmorphism effect, animate the glassmorphism container
                    var glassInner = border.Child as Border;
                    targetElement = glassInner?.Child as Grid;
                    break;
            }

            if (targetElement != null)
            {
                ApplyElasticAnimationToElement(targetElement);
            }
        }

        /// <summary>
        /// Apply elastic animation to a specific element
        /// </summary>
        private static void ApplyElasticAnimationToElement(FrameworkElement targetElement)
        {
            if (targetElement == null) return;

            // Ensure the element has a ScaleTransform
            if (targetElement.RenderTransform == null || !(targetElement.RenderTransform is ScaleTransform))
            {
                targetElement.RenderTransform = new ScaleTransform();
                targetElement.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            }

            var scaleTransform = (ScaleTransform)targetElement.RenderTransform;

            // Create elastic scale animation
            var elasticScaleX = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0.8,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(600)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.ElasticEase 
                { 
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
                    Oscillations = 1,
                    Springiness = 6
                }
            };

            var elasticScaleY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0.8,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(600)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.ElasticEase 
                { 
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
                    Oscillations = 1,
                    Springiness = 6
                }
            };

            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(elasticScaleX, scaleTransform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(elasticScaleX, "ScaleX");
            
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(elasticScaleY, scaleTransform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(elasticScaleY, "ScaleY");
            
            storyboard.Children.Add(elasticScaleX);
            storyboard.Children.Add(elasticScaleY);
            
            storyboard.Begin();
        }
    }
}
