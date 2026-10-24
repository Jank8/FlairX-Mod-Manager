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
        
        private void AnimateHotkeyToFavorites(string keyCombo)
        {
            // Find the border with this key
            Border? targetBorder = null;
            int currentIndex = -1;
            
            for (int i = 0; i < ModHotkeysPanel.Children.Count; i++)
            {
                if (ModHotkeysPanel.Children[i] is Border border && border.Tag is HotkeyRowData data && data.Key == keyCombo)
                {
                    targetBorder = border;
                    currentIndex = i;
                    break;
                }
            }
            
            if (targetBorder == null || currentIndex <= 0)
            {
                // Already at top or not found - just reorder
                ReorderHotkeys();
                return;
            }
            
            // Create slide-up animation for the target
            var transform = targetBorder.RenderTransform as CompositeTransform ?? new CompositeTransform();
            targetBorder.RenderTransform = transform;
            
            // Find target position (after existing favorites, maintaining original order)
            int targetPosition = 0;
            int targetOriginalIndex = -1;
            if (targetBorder.Tag is HotkeyRowData targetData)
            {
                targetOriginalIndex = targetData.OriginalIndex;
            }
            
            // Count how many favorites with lower original index exist
            for (int i = 0; i < ModHotkeysPanel.Children.Count; i++)
            {
                if (ModHotkeysPanel.Children[i] is Border border && border.Tag is HotkeyRowData data)
                {
                    if (_favoriteHotkeys.Contains(data.Key) && data.Key != keyCombo && data.OriginalIndex < targetOriginalIndex)
                    {
                        targetPosition++;
                    }
                }
            }
            
            // Calculate distance to move to target position
            double totalHeight = 0;
            for (int i = targetPosition; i < currentIndex; i++)
            {
                if (ModHotkeysPanel.Children[i] is FrameworkElement elem)
                {
                    totalHeight += elem.ActualHeight + 6; // 6 is margin
                }
            }
            
            if (totalHeight <= 0)
            {
                ReorderHotkeys();
                return;
            }
            
            var slideUp = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = -totalHeight,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase 
                { 
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut 
                }
            };
            
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideUp, transform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideUp, "TranslateY");
            storyboard.Children.Add(slideUp);
            
            // Animate items between target position and current position sliding down
            for (int i = targetPosition; i < currentIndex; i++)
            {
                if (ModHotkeysPanel.Children[i] is Border otherBorder)
                {
                    var otherTransform = otherBorder.RenderTransform as CompositeTransform ?? new CompositeTransform();
                    otherBorder.RenderTransform = otherTransform;
                    
                    var slideDown = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        From = 0,
                        To = targetBorder.ActualHeight + 6,
                        Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase 
                        { 
                            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut 
                        }
                    };
                    
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideDown, otherTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideDown, "TranslateY");
                    storyboard.Children.Add(slideDown);
                }
            }
            
            storyboard.Completed += (s, args) =>
            {
                // Reset transforms and reorder
                foreach (var child in ModHotkeysPanel.Children)
                {
                    if (child is Border border && border.RenderTransform is CompositeTransform ct)
                    {
                        ct.TranslateY = 0;
                    }
                }
                ReorderHotkeys();
            };
            
            storyboard.Begin();
        }
        
        private void AnimateHotkeyFromFavorites(string keyCombo)
        {
            // Find the border with this key
            Border? targetBorder = null;
            int currentIndex = -1;
            int targetOriginalIndex = -1;
            
            for (int i = 0; i < ModHotkeysPanel.Children.Count; i++)
            {
                if (ModHotkeysPanel.Children[i] is Border border && border.Tag is HotkeyRowData data && data.Key == keyCombo)
                {
                    targetBorder = border;
                    currentIndex = i;
                    targetOriginalIndex = data.OriginalIndex;
                    break;
                }
            }
            
            if (targetBorder == null)
            {
                ReorderHotkeys();
                return;
            }
            
            // Calculate target position (where it should go based on original index, after all favorites)
            int favoritesCount = 0;
            int targetPosition = 0;
            
            foreach (var child in ModHotkeysPanel.Children)
            {
                if (child is Border border && border.Tag is HotkeyRowData data)
                {
                    if (_favoriteHotkeys.Contains(data.Key))
                    {
                        favoritesCount++;
                    }
                }
            }
            
            // Find position among non-favorites based on original index
            targetPosition = favoritesCount;
            foreach (var child in ModHotkeysPanel.Children)
            {
                if (child is Border border && border.Tag is HotkeyRowData data)
                {
                    if (!_favoriteHotkeys.Contains(data.Key) && data.Key != keyCombo && data.OriginalIndex < targetOriginalIndex)
                    {
                        targetPosition++;
                    }
                }
            }
            
            if (targetPosition <= currentIndex)
            {
                // Already in correct position or moving up (shouldn't happen when removing from favorites)
                ReorderHotkeys();
                return;
            }
            
            // Calculate distance to move down
            double totalHeight = 0;
            for (int i = currentIndex + 1; i <= targetPosition && i < ModHotkeysPanel.Children.Count; i++)
            {
                if (ModHotkeysPanel.Children[i] is FrameworkElement elem)
                {
                    totalHeight += elem.ActualHeight + 6;
                }
            }
            
            if (totalHeight <= 0)
            {
                ReorderHotkeys();
                return;
            }
            
            var transform = targetBorder.RenderTransform as CompositeTransform ?? new CompositeTransform();
            targetBorder.RenderTransform = transform;
            
            var slideDown = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = totalHeight,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase 
                { 
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut 
                }
            };
            
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideDown, transform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideDown, "TranslateY");
            storyboard.Children.Add(slideDown);
            
            // Animate items between current position and target position sliding up
            for (int i = currentIndex + 1; i <= targetPosition && i < ModHotkeysPanel.Children.Count; i++)
            {
                if (ModHotkeysPanel.Children[i] is Border otherBorder)
                {
                    var otherTransform = otherBorder.RenderTransform as CompositeTransform ?? new CompositeTransform();
                    otherBorder.RenderTransform = otherTransform;
                    
                    var slideUp = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        From = 0,
                        To = -(targetBorder.ActualHeight + 6),
                        Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase 
                        { 
                            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut 
                        }
                    };
                    
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideUp, otherTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideUp, "TranslateY");
                    storyboard.Children.Add(slideUp);
                }
            }
            
            storyboard.Completed += (s, args) =>
            {
                // Reset transforms and reorder
                foreach (var child in ModHotkeysPanel.Children)
                {
                    if (child is Border border && border.RenderTransform is CompositeTransform ct)
                    {
                        ct.TranslateY = 0;
                    }
                }
                ReorderHotkeys();
            };
            
            storyboard.Begin();
        }
        
        private void ReorderHotkeys()
        {
            // Collect all hotkey borders with their data
            var hotkeyItems = new List<(Border border, string key, int originalIndex, bool isFavorite)>();
            
            foreach (var child in ModHotkeysPanel.Children)
            {
                if (child is Border border && border.Tag is HotkeyRowData data)
                {
                    hotkeyItems.Add((border, data.Key, data.OriginalIndex, _favoriteHotkeys.Contains(data.Key)));
                }
            }
            
            // Sort: favorites first (maintaining original order within favorites), then non-favorites (maintaining original order)
            var sorted = hotkeyItems
                .OrderByDescending(x => x.isFavorite)
                .ThenBy(x => x.originalIndex)
                .Select(x => x.border)
                .ToList();
            
            // Clear and re-add in sorted order
            ModHotkeysPanel.Children.Clear();
            foreach (var border in sorted)
            {
                ModHotkeysPanel.Children.Add(border);
            }
        }
    }
}
