using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModGridPage
    {

        private void FavoriteStarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTile tile)
            {
                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag)) return;
                
                if (tile.IsCategory)
                {
                    // Handle category favorites
                    SettingsManager.ToggleCategoryFavorite(gameTag, tile.Name);
                    tile.IsFavorite = SettingsManager.IsCategoryFavorite(gameTag, tile.Name);
                    
                    Logger.LogInfo($"Toggled favorite for category: {tile.Name}, IsFavorite: {tile.IsFavorite}");
                    
                    // Re-sort categories with animation to move favorites to top
                    if (_currentViewMode == ViewMode.Categories)
                    {
                        SortCategoriesByFavoritesAnimated();
                    }
                    
                    // Update menu star in MainWindow with animation
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.UpdateMenuStarForCategoryAnimated(tile.Name, tile.IsFavorite);
                        
                        // Refresh overlay if it exists
                        if (mainWindow.OverlayWindow != null)
                        {
                            mainWindow.OverlayWindow.RefreshOverlayData();
                        }
                    }
                }
                else
                {
                    // Handle mod favorites
                    SettingsManager.ToggleModFavorite(gameTag, tile.Category ?? "Other", tile.Name);
                    tile.IsFavorite = SettingsManager.IsModFavorite(gameTag, tile.Category ?? "Other", tile.Name);
                    
                    // Sync the new favorite state into ModListManager cache
                    // so that re-entering the category shows the correct order
                    ModListManager.UpdateModFavorite(tile.Name, tile.IsFavorite);
                    
                    Logger.LogInfo($"Toggled favorite for mod: {tile.Name}, IsFavorite: {tile.IsFavorite}");
                    
                    // Re-sort mods with animation to move favorites to top
                    if (_currentViewMode == ViewMode.Mods)
                    {
                        SortModsByFavoritesAnimated();
                    }
                    
                    // Refresh overlay if it exists
                    if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
                    {
                        if (mainWindow.OverlayWindow != null)
                        {
                            mainWindow.OverlayWindow.RefreshOverlayData();
                        }
                    }
                }
            }
        }
        
        private async void SortCategoriesByFavoritesAnimated()
        {
            try
            {
                var items = ModsGrid.ItemsSource as ObservableCollection<ModTile>;
                if (items == null || items.Count == 0) return;
                
                // Get current order
                var currentOrder = items.ToList();
                
                // Calculate new order
                var sortedItems = items.OrderByDescending(m => m.IsFavorite)
                                      .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                                      .ToList();
                
                // Check if order changed
                bool orderChanged = false;
                for (int i = 0; i < currentOrder.Count; i++)
                {
                    if (currentOrder[i] != sortedItems[i])
                    {
                        orderChanged = true;
                        break;
                    }
                }
                if (!orderChanged) return;
                
                // Fade out the grid
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                var fadeOutStoryboard = new Storyboard();
                Storyboard.SetTarget(fadeOut, ModsGrid);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");
                fadeOutStoryboard.Children.Add(fadeOut);
                fadeOutStoryboard.Begin();
                
                // Wait for fade out to complete
                await Task.Delay(150);
                
                // Now do the refresh while invisible
                for (int targetIndex = 0; targetIndex < sortedItems.Count; targetIndex++)
                {
                    var item = sortedItems[targetIndex];
                    int currentIndex = items.IndexOf(item);
                    if (currentIndex != targetIndex && currentIndex >= 0)
                    {
                        items.Move(currentIndex, targetIndex);
                    }
                }
                
                // Small delay to ensure refresh is complete
                await Task.Delay(50);
                
                // Fade in the grid
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                var fadeInStoryboard = new Storyboard();
                Storyboard.SetTarget(fadeIn, ModsGrid);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");
                fadeInStoryboard.Children.Add(fadeIn);
                fadeInStoryboard.Begin();
                
                Logger.LogInfo("Categories sorted by favorites with animation");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sorting categories by favorites with animation", ex);
                // Reset opacity and fallback
                ModsGrid.Opacity = 1;
                SortCategoriesByFavorites();
            }
        }

        private void SortCategoriesByFavorites()
        {
            try
            {
                var items = ModsGrid.ItemsSource as ObservableCollection<ModTile>;
                if (items == null) return;
                
                // Sort: favorites first, then alphabetically
                var sortedItems = items.OrderByDescending(m => m.IsFavorite)
                                      .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                                      .ToList();
                
                // Clear and re-add in sorted order
                items.Clear();
                foreach (var item in sortedItems)
                {
                    items.Add(item);
                }
                
                Logger.LogInfo("Categories sorted by favorites");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sorting categories by favorites", ex);
            }
        }
        
        public void RefreshCategoryFavorites()
        {
            try
            {
                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag)) return;
                
                // Update favorite status for all category tiles
                var items = ModsGrid.ItemsSource as ObservableCollection<ModTile>;
                if (items != null)
                {
                    foreach (var item in items.Where(i => i.IsCategory))
                    {
                        item.IsFavorite = SettingsManager.IsCategoryFavorite(gameTag, item.Name);
                    }
                    
                    // Re-sort
                    SortCategoriesByFavorites();
                }
                
                Logger.LogInfo("Refreshed category favorites from menu");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error refreshing category favorites", ex);
            }
        }
        
        public async void RefreshCategoryFavoritesAnimated()
        {
            try
            {
                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag)) return;
                
                var items = ModsGrid.ItemsSource as ObservableCollection<ModTile>;
                if (items == null || items.Count == 0) return;
                
                // Update favorite status for all category tiles
                foreach (var item in items.Where(i => i.IsCategory))
                {
                    item.IsFavorite = SettingsManager.IsCategoryFavorite(gameTag, item.Name);
                }
                
                // Get current order
                var currentOrder = items.ToList();
                
                // Calculate new order
                var sortedItems = items.OrderByDescending(m => m.IsFavorite)
                                      .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                                      .ToList();
                
                // Check if order changed
                bool orderChanged = false;
                for (int i = 0; i < currentOrder.Count; i++)
                {
                    if (currentOrder[i] != sortedItems[i])
                    {
                        orderChanged = true;
                        break;
                    }
                }
                if (!orderChanged) return;
                
                // Fade out the grid
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                var fadeOutStoryboard = new Storyboard();
                Storyboard.SetTarget(fadeOut, ModsGrid);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");
                fadeOutStoryboard.Children.Add(fadeOut);
                fadeOutStoryboard.Begin();
                
                // Wait for fade out to complete
                await Task.Delay(150);
                
                // Now do the refresh while invisible
                for (int targetIndex = 0; targetIndex < sortedItems.Count; targetIndex++)
                {
                    var item = sortedItems[targetIndex];
                    int currentIndex = items.IndexOf(item);
                    if (currentIndex != targetIndex && currentIndex >= 0)
                    {
                        items.Move(currentIndex, targetIndex);
                    }
                }
                
                // Small delay to ensure refresh is complete
                await Task.Delay(50);
                
                // Fade in the grid
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                var fadeInStoryboard = new Storyboard();
                Storyboard.SetTarget(fadeIn, ModsGrid);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");
                fadeInStoryboard.Children.Add(fadeIn);
                fadeInStoryboard.Begin();
                
                Logger.LogInfo("Refreshed category favorites with animation");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error refreshing category favorites with animation", ex);
                // Reset opacity and fallback
                ModsGrid.Opacity = 1;
                RefreshCategoryFavorites();
            }
        }
        
        private async void SortModsByFavoritesAnimated()
        {
            try
            {
                var items = ModsGrid.ItemsSource as ObservableCollection<ModTile>;
                if (items == null || items.Count == 0) return;
                
                // Get current order
                var currentOrder = items.ToList();
                
                // Calculate new order - favorites first, then by current sort mode
                var sortedItems = items.OrderByDescending(m => m.IsFavorite)
                                      .ThenBy(m => GetSortKey(m))
                                      .ToList();
                
                // Check if order changed
                bool orderChanged = false;
                for (int i = 0; i < currentOrder.Count; i++)
                {
                    if (currentOrder[i] != sortedItems[i])
                    {
                        orderChanged = true;
                        break;
                    }
                }
                if (!orderChanged) return;
                
                // Fade out the grid
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                var fadeOutStoryboard = new Storyboard();
                Storyboard.SetTarget(fadeOut, ModsGrid);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");
                fadeOutStoryboard.Children.Add(fadeOut);
                fadeOutStoryboard.Begin();
                
                // Wait for fade out to complete
                await Task.Delay(150);
                
                // Now do the refresh while invisible
                for (int targetIndex = 0; targetIndex < sortedItems.Count; targetIndex++)
                {
                    var item = sortedItems[targetIndex];
                    int currentIndex = items.IndexOf(item);
                    if (currentIndex != targetIndex && currentIndex >= 0)
                    {
                        items.Move(currentIndex, targetIndex);
                    }
                }
                
                // Small delay to ensure refresh is complete
                await Task.Delay(50);
                
                // Fade in the grid
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                var fadeInStoryboard = new Storyboard();
                Storyboard.SetTarget(fadeIn, ModsGrid);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");
                fadeInStoryboard.Children.Add(fadeIn);
                fadeInStoryboard.Begin();
                
                Logger.LogInfo("Mods sorted by favorites with animation");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sorting mods by favorites with animation", ex);
                // Reset opacity and fallback
                ModsGrid.Opacity = 1;
                SortModsByFavorites();
            }
        }
        
        private void SortModsByFavorites()
        {
            try
            {
                var items = ModsGrid.ItemsSource as ObservableCollection<ModTile>;
                if (items == null) return;
                
                // Sort: favorites first, then by current sort mode
                var sortedItems = items.OrderByDescending(m => m.IsFavorite)
                                      .ThenBy(m => GetSortKey(m))
                                      .ToList();
                
                // Clear and re-add in sorted order
                items.Clear();
                foreach (var item in sortedItems)
                {
                    items.Add(item);
                }
                
                Logger.LogInfo("Mods sorted by favorites");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error sorting mods by favorites", ex);
            }
        }
        
        private object GetSortKey(ModTile mod)
        {
            // Use current sort mode to determine secondary sort key
            return _currentSortMode switch
            {
                SortMode.NameAZ => mod.Name,
                SortMode.NameZA => mod.Name,
                SortMode.CategoryAZ => mod.Category,
                SortMode.CategoryZA => mod.Category,
                SortMode.LastCheckedNewest => mod.LastChecked,
                SortMode.LastCheckedOldest => mod.LastChecked,
                SortMode.LastUpdatedNewest => mod.LastUpdated,
                SortMode.LastUpdatedOldest => mod.LastUpdated,
                SortMode.ActiveFirst => mod.IsActive ? 0 : 1, // Active first
                SortMode.InactiveFirst => mod.IsActive ? 1 : 0, // Inactive first
                _ => mod.Name
            };
        }
        
        public void RefreshModFavorites()
        {
            try
            {
                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag)) return;
                
                // Update favorite status for all mod tiles
                var items = ModsGrid.ItemsSource as ObservableCollection<ModTile>;
                if (items != null)
                {
                    foreach (var item in items.Where(i => !i.IsCategory))
                    {
                        item.IsFavorite = SettingsManager.IsModFavorite(gameTag, item.Category ?? "Other", item.Name);
                    }
                    
                    // Re-sort
                    SortModsByFavorites();
                }
                
                Logger.LogInfo("Refreshed mod favorites");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error refreshing mod favorites", ex);
            }
        }
        
        private void SetFavoriteTooltips()
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                
                // Find all favorite star buttons in the current view and set their tooltips
                if (ModsGrid?.ItemsSource is IEnumerable<ModTile> items)
                {
                    foreach (var item in items)
                    {
                        // The tooltip will be set when the button is created in the UI
                        // We'll handle this in the DataTemplate or when the button is loaded
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error setting favorite tooltips", ex);
            }
        }
    }
}
