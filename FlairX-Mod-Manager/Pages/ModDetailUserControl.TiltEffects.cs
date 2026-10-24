using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModDetailUserControl
    {

        // Animation throttling
        private DateTime _lastAnimationUpdate = DateTime.MinValue;
        private const int ANIMATION_THROTTLE_MS = 16; // ~60 FPS
        private Microsoft.UI.Xaml.DispatcherTimer? _animationTimer;
        private double _targetTiltX = 0;
        private double _targetTiltY = 0;
        
        private void ModImageCoordinateField_PointerMoved_Parallax(object sender, PointerRoutedEventArgs e)
        {
            if (PreviewEffectHelper.IsParallaxEnabled)
            {
                var position = e.GetCurrentPoint(ModImageCoordinateField);
                var innerBorder = ModImageBorder.Child as Border;
                var parallaxContainer = innerBorder?.Child as Grid;
                
                PreviewEffectHelper.UpdateParallaxEffect(
                    parallaxContainer, 
                    position.Position.X, 
                    position.Position.Y,
                    ModImageCoordinateField.ActualWidth,
                    ModImageCoordinateField.ActualHeight);
            }
        }
        
        private void ModImageCoordinateField_PointerExited_Parallax(object sender, PointerRoutedEventArgs e)
        {
            if (PreviewEffectHelper.IsParallaxEnabled)
            {
                var innerBorder = ModImageBorder.Child as Border;
                var parallaxContainer = innerBorder?.Child as Grid;
                
                PreviewEffectHelper.ResetParallaxEffect(parallaxContainer);
            }
        }

        private void ModImageCoordinateField_PointerMoved_Glassmorphism(object sender, PointerRoutedEventArgs e)
        {
            if (PreviewEffectHelper.IsGlassmorphismEnabled)
            {
                var position = e.GetCurrentPoint(ModImageCoordinateField);
                var innerBorder = ModImageBorder.Child as Border;
                var glassmorphismContainer = innerBorder?.Child as Grid;
                
                PreviewEffectHelper.UpdateGlassmorphismEffect(
                    glassmorphismContainer, 
                    position.Position.X, 
                    position.Position.Y,
                    ModImageCoordinateField.ActualWidth,
                    ModImageCoordinateField.ActualHeight);
            }
        }
        
        private void ModImageCoordinateField_PointerExited_Glassmorphism(object sender, PointerRoutedEventArgs e)
        {
            if (PreviewEffectHelper.IsGlassmorphismEnabled)
            {
                var innerBorder = ModImageBorder.Child as Border;
                var glassmorphismContainer = innerBorder?.Child as Grid;
                
                PreviewEffectHelper.ResetGlassmorphismEffect(glassmorphismContainer);
            }
        }

        // MAIN HOVER TILT EFFECT - 6 degrees for entire preview area
        private void ModImageCoordinateField_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                // Only process if sender is the main coordinate field, ignore buttons
                if (!ReferenceEquals(sender, ModImageCoordinateField)) return;
                
                // Subscribe to pointer moved events for dynamic tilt
                ModImageCoordinateField.PointerMoved += ModImageCoordinateField_PointerMoved;
                
                // Calculate initial target tilt
                CalculateTargetTilt(e);
                
                // Apply initial tilt with smooth animation
                AnimateMainTilt(_targetTiltX, _targetTiltY);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ModImageCoordinateField_PointerEntered", ex);
            }
        }

        private void ModImageCoordinateField_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                // Only process if sender is the main coordinate field, ignore buttons
                if (!ReferenceEquals(sender, ModImageCoordinateField)) return;
                
                // Check if mouse is still within the entire preview area (including buttons)
                var position = e.GetCurrentPoint(ModImageCoordinateField.Parent as FrameworkElement);
                var container = ModImageCoordinateField.Parent as FrameworkElement;
                
                if (container != null)
                {
                    var bounds = new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight);
                    
                    // Only reset tilt if mouse truly left the entire preview area
                    if (!bounds.Contains(position.Position))
                    {
                        // Unsubscribe from pointer moved events
                        ModImageCoordinateField.PointerMoved -= ModImageCoordinateField_PointerMoved;
                        
                        // Reset target values
                        _targetTiltX = 0;
                        _targetTiltY = 0;
                        
                        // Reset tilt projection with smooth animation - reset entire container
                        ResetMainTiltEffect();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ModImageCoordinateField_PointerExited", ex);
            }
        }

        private void ModImageCoordinateField_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                // Only process if sender is the main coordinate field, ignore buttons
                if (!ReferenceEquals(sender, ModImageCoordinateField)) return;
                
                // Throttle animation updates for smoother performance
                var now = DateTime.Now;
                if ((now - _lastAnimationUpdate).TotalMilliseconds < ANIMATION_THROTTLE_MS)
                    return;
                
                _lastAnimationUpdate = now;
                
                // Calculate target tilt values
                CalculateTargetTilt(e);
                
                // Use smooth interpolation instead of instant response
                UpdateMainTiltEffectSmooth();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ModImageCoordinateField_PointerMoved", ex);
            }
        }

        private void CalculateTargetTilt(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                // Get the pointer position relative to the coordinate field
                var position = e.GetCurrentPoint(ModImageCoordinateField);
                var fieldWidth = ModImageCoordinateField.ActualWidth;
                var fieldHeight = ModImageCoordinateField.ActualHeight;
                
                if (fieldWidth > 0 && fieldHeight > 0)
                {
                    // Calculate tilt angles based on pointer position
                    var centerX = fieldWidth / 2;
                    var centerY = fieldHeight / 2;
                    var offsetX = (position.Position.X - centerX) / centerX; // -1 to 1
                    var offsetY = (position.Position.Y - centerY) / centerY; // -1 to 1
                    
                    // Main tilt for entire area (max 6 degrees)
                    var maxTilt = 6.0;
                    _targetTiltX = offsetY * maxTilt; // Y offset affects X rotation
                    _targetTiltY = -offsetX * maxTilt; // X offset affects Y rotation (inverted)
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in CalculateTargetTilt", ex);
            }
        }

        private void UpdateMainTiltEffectSmooth()
        {
            try
            {
                var container = ModImageCoordinateField.Parent as Grid;
                var projection = GetOrCreateProjection(container);
                if (projection == null) return;
                
                // Use smooth interpolation instead of instant change
                var currentTiltX = projection.RotationX;
                var currentTiltY = projection.RotationY;
                
                // Interpolation factor (0.2 = 20% towards target each frame)
                var lerpFactor = 0.2;
                var newTiltX = currentTiltX + ((_targetTiltX - currentTiltX) * lerpFactor);
                var newTiltY = currentTiltY + ((_targetTiltY - currentTiltY) * lerpFactor);
                
                // Set new values directly (no animation needed as we're interpolating)
                projection.RotationX = newTiltX;
                projection.RotationY = newTiltY;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in UpdateMainTiltEffectSmooth", ex);
            }
        }

        private void UpdateMainTiltEffect(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e, bool useAnimation = false)
        {
            try
            {
                // Get the pointer position relative to the coordinate field
                var position = e.GetCurrentPoint(ModImageCoordinateField);
                var fieldWidth = ModImageCoordinateField.ActualWidth;
                var fieldHeight = ModImageCoordinateField.ActualHeight;
                
                if (fieldWidth > 0 && fieldHeight > 0)
                {
                    // Calculate tilt angles based on pointer position
                    var centerX = fieldWidth / 2;
                    var centerY = fieldHeight / 2;
                    var offsetX = (position.Position.X - centerX) / centerX; // -1 to 1
                    var offsetY = (position.Position.Y - centerY) / centerY; // -1 to 1
                    
                    // Main tilt for entire area (max 6 degrees)
                    var maxTilt = 6.0;
                    var tiltX = offsetY * maxTilt; // Y offset affects X rotation
                    var tiltY = -offsetX * maxTilt; // X offset affects Y rotation (inverted)
                    
                    if (useAnimation)
                    {
                        // Smooth animation for entry
                        AnimateMainTilt(tiltX, tiltY);
                    }
                    else
                    {
                        // Instant response for movement
                        SetMainTilt(tiltX, tiltY);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in UpdateMainTiltEffect", ex);
            }
        }

        private void AnimateMainTilt(double tiltX, double tiltY)
        {
            try
            {
                var container = ModImageCoordinateField.Parent as Grid;
                var projection = GetOrCreateProjection(container);
                if (projection == null) return;
                
                // Create optimized storyboard
                var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                var easing = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase 
                { 
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut 
                };
                var duration = TimeSpan.FromMilliseconds(150);
                
                // X rotation animation
                var rotXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = tiltX,
                    Duration = duration,
                    EasingFunction = easing
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotXAnim, projection);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotXAnim, "RotationX");
                storyboard.Children.Add(rotXAnim);
                
                // Y rotation animation
                var rotYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = tiltY,
                    Duration = duration,
                    EasingFunction = easing
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotYAnim, projection);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotYAnim, "RotationY");
                storyboard.Children.Add(rotYAnim);
                
                storyboard.Begin();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in AnimateMainTilt", ex);
            }
        }

        private void SetMainTilt(double tiltX, double tiltY)
        {
            try
            {
                var container = ModImageCoordinateField.Parent as Grid;
                var projection = GetOrCreateProjection(container);
                if (projection == null) return;
                
                // Set immediately - no animation
                projection.RotationX = tiltX;
                projection.RotationY = tiltY;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in SetMainTilt", ex);
            }
        }

        private void ResetMainTiltEffect()
        {
            try
            {
                var container = ModImageCoordinateField.Parent as Grid;
                if (container?.Projection is not Microsoft.UI.Xaml.Media.PlaneProjection projection) return;
                
                var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                var easing = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase();
                var duration = TimeSpan.FromMilliseconds(250);
                
                // X rotation reset
                var rotXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = 0,
                    Duration = duration,
                    EasingFunction = easing
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotXAnim, projection);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotXAnim, "RotationX");
                storyboard.Children.Add(rotXAnim);
                
                // Y rotation reset
                var rotYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = 0,
                    Duration = duration,
                    EasingFunction = easing
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotYAnim, projection);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotYAnim, "RotationY");
                storyboard.Children.Add(rotYAnim);
                
                storyboard.Begin();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ResetMainTiltEffect", ex);
            }
        }

        // Helper method to get or create PlaneProjection
        private Microsoft.UI.Xaml.Media.PlaneProjection? GetOrCreateProjection(Grid? container)
        {
            if (container == null) return null;
            
            if (container.Projection is not Microsoft.UI.Xaml.Media.PlaneProjection projection)
            {
                projection = new Microsoft.UI.Xaml.Media.PlaneProjection
                {
                    CenterOfRotationX = 0.5,
                    CenterOfRotationY = 0.5
                };
                container.Projection = projection;
            }
            
            return projection;
        }
    }
}
