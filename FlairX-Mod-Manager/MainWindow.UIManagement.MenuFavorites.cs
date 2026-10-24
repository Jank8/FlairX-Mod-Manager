using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Storage.Streams;

namespace FlairX_Mod_Manager
{
    public sealed partial class MainWindow
    {
        
        // Dictionary to store star buttons, presenters, and grid columns for each category
        private readonly Dictionary<string, (Button button, FontIcon icon, ContentPresenter? presenter, ColumnDefinition? column)> _categoryStarButtons = new Dictionary<string, (Button, FontIcon, ContentPresenter?, ColumnDefinition?)>();

        private void SortMenuItemsByFavorites()
        {
            try
            {
                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag)) return;
                
                var menuItems = nvSample.MenuItems.OfType<NavigationViewItem>().ToList();
                
                // Sort: favorites first, then alphabetically
                // Get category name from Tag (format: "Category_Name")
                var sortedItems = menuItems
                    .OrderByDescending(item => 
                    {
                        var tag = item.Tag?.ToString() ?? "";
                        var catName = tag.StartsWith("Category_") ? tag.Substring(9) : item.Content?.ToString() ?? "";
                        return SettingsManager.IsCategoryFavorite(gameTag, catName);
                    })
                    .ThenBy(item => 
                    {
                        var tag = item.Tag?.ToString() ?? "";
                        var catName = tag.StartsWith("Category_") ? tag.Substring(9) : item.Content?.ToString() ?? "";
                        return catName;
                    }, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                
                // Clear and re-add
                nvSample.MenuItems.Clear();
                foreach (var item in sortedItems)
                {
                    nvSample.MenuItems.Add(item);
                }
                
                Logger.LogInfo("Menu items sorted by favorites");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sorting menu items by favorites", ex);
            }
        }
        
        private async void SortMenuItemsByFavoritesAnimated()
        {
            try
            {
                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag)) return;
                
                var menuItems = nvSample.MenuItems.OfType<NavigationViewItem>().ToList();
                if (menuItems.Count == 0) return;
                
                // Calculate new order
                var sortedItems = menuItems
                    .OrderByDescending(item => 
                    {
                        var tag = item.Tag?.ToString() ?? "";
                        var catName = tag.StartsWith("Category_") ? tag.Substring(9) : item.Content?.ToString() ?? "";
                        return SettingsManager.IsCategoryFavorite(gameTag, catName);
                    })
                    .ThenBy(item => 
                    {
                        var tag = item.Tag?.ToString() ?? "";
                        var catName = tag.StartsWith("Category_") ? tag.Substring(9) : item.Content?.ToString() ?? "";
                        return catName;
                    }, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                
                // Check if order changed
                bool orderChanged = false;
                for (int i = 0; i < menuItems.Count; i++)
                {
                    if (menuItems[i] != sortedItems[i])
                    {
                        orderChanged = true;
                        break;
                    }
                }
                if (!orderChanged) return;
                
                // Fade out animation
                var fadeOutStoryboard = new Storyboard();
                foreach (var item in menuItems)
                {
                    var fadeOut = new DoubleAnimation
                    {
                        From = 1,
                        To = 0.3,
                        Duration = new Duration(TimeSpan.FromMilliseconds(100))
                    };
                    Storyboard.SetTarget(fadeOut, item);
                    Storyboard.SetTargetProperty(fadeOut, "Opacity");
                    fadeOutStoryboard.Children.Add(fadeOut);
                }
                
                fadeOutStoryboard.Begin();
                await Task.Delay(100);
                
                // Reorder
                nvSample.MenuItems.Clear();
                foreach (var item in sortedItems)
                {
                    item.Opacity = 0.3;
                    nvSample.MenuItems.Add(item);
                }
                
                // Fade in animation
                await Task.Delay(50);
                var fadeInStoryboard = new Storyboard();
                foreach (var item in sortedItems)
                {
                    var fadeIn = new DoubleAnimation
                    {
                        From = 0.3,
                        To = 1,
                        Duration = new Duration(TimeSpan.FromMilliseconds(150))
                    };
                    Storyboard.SetTarget(fadeIn, item);
                    Storyboard.SetTargetProperty(fadeIn, "Opacity");
                    fadeInStoryboard.Children.Add(fadeIn);
                }
                
                fadeInStoryboard.Begin();
                
                Logger.LogInfo("Menu items sorted by favorites with animation");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sorting menu items by favorites with animation", ex);
                SortMenuItemsByFavorites();
            }
        }
        
        public void UpdateMenuStarForCategory(string categoryName, bool isFavorite)
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    // Use dictionary to find star button
                    if (_categoryStarButtons.TryGetValue(categoryName, out var starData))
                    {
                        starData.icon.Glyph = isFavorite ? "\uE735" : "\uE734";
                        starData.icon.Foreground = isFavorite 
                            ? new SolidColorBrush(Microsoft.UI.Colors.Gold) 
                            : new SolidColorBrush(Microsoft.UI.Colors.White);
                        
                        // Re-sort menu
                        SortMenuItemsByFavorites();
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error updating menu star for category: {categoryName}", ex);
            }
        }
        
        public void UpdateMenuStarForCategoryAnimated(string categoryName, bool isFavorite)
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    // Use dictionary to find star button
                    if (_categoryStarButtons.TryGetValue(categoryName, out var starData))
                    {
                        starData.icon.Glyph = isFavorite ? "\uE735" : "\uE734";
                        starData.icon.Foreground = isFavorite 
                            ? new SolidColorBrush(Microsoft.UI.Colors.Gold) 
                            : new SolidColorBrush(Microsoft.UI.Colors.White);
                        
                        // Re-sort menu with animation
                        SortMenuItemsByFavoritesAnimated();
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error updating menu star for category with animation: {categoryName}", ex);
            }
        }
        
        private bool _starVisibilityUpdateQueued;

        /// <summary>
        /// Update star button visibility based on NavigationView pane display mode.
        /// </summary>
        /// <remarks>
        /// This walks every star button and changes Grid columns and visibility. Running that
        /// inside a layout pass - which is exactly what happens while the pane collapses - makes
        /// XAML fail with an empty-stack "Unspecified error" and takes the process down.
        /// Coalesce repeated calls into one deferred update instead.
        /// </remarks>
        private void UpdateStarButtonsVisibility()
        {
            if (_starVisibilityUpdateQueued)
            {
                return;
            }

            _starVisibilityUpdateQueued = true;

            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                _starVisibilityUpdateQueued = false;

                try
                {
                    UpdateStarButtonsVisibilityCore();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to update star button visibility", ex);
                }
            });
        }

        private void UpdateStarButtonsVisibilityCore()
        {
            if (nvSample == null) return;
            
            Logger.LogInfo($"UpdateStarButtonsVisibility called - IsPaneOpen: {nvSample.IsPaneOpen}");
            
            // Hide stars when pane is closed (collapsed/compact mode)
            var isVisible = nvSample.IsPaneOpen;
            
            Logger.LogInfo($"Star visibility: {isVisible}, Star buttons count: {_categoryStarButtons.Count}");
            
            // Update all star columns - set width to 0 to remove the space
            foreach (var starData in _categoryStarButtons.Values)
            {
                if (starData.column != null)
                {
                    if (isVisible)
                    {
                        // Restore original width (Auto)
                        starData.column.Width = new GridLength(1, GridUnitType.Auto);
                    }
                    else
                    {
                        // Hide column by setting width to 0
                        starData.column.Width = new GridLength(0);
                    }
                }
                
                // Also update presenter visibility as backup
                if (starData.presenter != null)
                {
                    starData.presenter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            
            Logger.LogInfo($"Star columns updated - visible: {isVisible}");
        }
    }
}
