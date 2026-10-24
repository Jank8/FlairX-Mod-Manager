using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using WinRT;
using WinRT.Interop;

namespace FlairX_Mod_Manager
{
    public sealed partial class OverlayWindow
    {



        private void CategoryFavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is OverlayCategoryItem category)
                {
                    var gameTag = SettingsManager.CurrentSelectedGame;
                    if (string.IsNullOrEmpty(gameTag)) return;

                    // Toggle favorite status
                    SettingsManager.ToggleCategoryFavorite(gameTag, category.Name);
                    category.IsFavorite = SettingsManager.IsCategoryFavorite(gameTag, category.Name);
                    
                    Logger.LogInfo($"Overlay: Toggled favorite for category: {category.Name}, IsFavorite: {category.IsFavorite}");
                    
                    // Refresh categories to update sorting
                    LoadCurrentCategoryMods();
                    
                    // Update main window menu if it exists
                    if (_mainWindow != null)
                    {
                        _mainWindow.UpdateMenuStarForCategoryAnimated(category.Name, category.IsFavorite);
                        
                        // Update ModGridPage if it's showing categories
                        if (_mainWindow.ContentFrame?.Content is Pages.ModGridPage modGridPage && 
                            modGridPage.CurrentViewMode == Pages.ModGridPage.ViewMode.Categories)
                        {
                            modGridPage.RefreshCategoryFavoritesAnimated();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to toggle category favorite in overlay", ex);
            }
        }

        private void ModFavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is OverlayModItem mod)
                {
                    var gameTag = SettingsManager.CurrentSelectedGame;
                    if (string.IsNullOrEmpty(gameTag)) return;

                    // Toggle favorite status
                    SettingsManager.ToggleModFavorite(gameTag, mod.Category ?? "Other", mod.Name);
                    mod.IsFavorite = SettingsManager.IsModFavorite(gameTag, mod.Category ?? "Other", mod.Name);
                    
                    Logger.LogInfo($"Overlay: Toggled favorite for mod: {mod.Name}, IsFavorite: {mod.IsFavorite}");
                    
                    // Refresh current category to update sorting
                    if (!string.IsNullOrEmpty(_selectedCategoryPath))
                    {
                        LoadModsFromCategory(_selectedCategoryPath, _showActiveOnly);
                    }
                    
                    // Update ModGridPage if it's showing mods
                    if (_mainWindow != null)
                    {
                        if (_mainWindow.ContentFrame?.Content is Pages.ModGridPage modGridPage && 
                            modGridPage.CurrentViewMode == Pages.ModGridPage.ViewMode.Mods)
                        {
                            modGridPage.RefreshModFavorites();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to toggle mod favorite in overlay", ex);
            }
        }

        private async void ModBrokenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is OverlayModItem mod)
                {
                    var newBroken = !mod.IsBroken;
                    var modJsonPath = Path.Combine(mod.Directory, "mod.json");
                    if (!File.Exists(modJsonPath)) return;

                    await Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                    {
                        var json = await File.ReadAllTextAsync(modJsonPath);
                        var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                        dict["modBroken"] = newBroken;
                        await File.WriteAllTextAsync(modJsonPath,
                            System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    });

                    mod.IsBroken = newBroken;
                    Logger.LogInfo($"Overlay: Toggled broken for mod: {mod.Name}, IsBroken: {newBroken}");

                    // Refresh ModListManager and main grid
                    ModListManager.RebuildAllLists();
                    if (_mainWindow?.ContentFrame?.Content is Pages.ModGridPage modGridPage)
                        modGridPage.RefreshModTile(Path.GetFileName(mod.Directory), newBroken);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to toggle mod broken in overlay", ex);
            }
        }

        

        private void ToggleCategoryFavorite()
        {
            try
            {
                // Find currently selected category
                var selectedCategory = OverlayCategories.FirstOrDefault(c => c.IsSelected);
                if (selectedCategory == null) return;

                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag)) return;

                // Toggle favorite status
                SettingsManager.ToggleCategoryFavorite(gameTag, selectedCategory.Name);
                selectedCategory.IsFavorite = SettingsManager.IsCategoryFavorite(gameTag, selectedCategory.Name);
                
                Logger.LogInfo($"Overlay Gamepad: Toggled favorite for category: {selectedCategory.Name}, IsFavorite: {selectedCategory.IsFavorite}");
                
                // Vibrate to confirm action
                _gamepadManager?.Vibrate((ushort)(selectedCategory.IsFavorite ? 40000 : 20000), (ushort)0, 200);
                
                // Refresh categories to update sorting
                LoadCurrentCategoryMods();
                
                // Update main window menu if it exists
                if (_mainWindow != null)
                {
                    _mainWindow.UpdateMenuStarForCategoryAnimated(selectedCategory.Name, selectedCategory.IsFavorite);
                    
                    // Update ModGridPage if it's showing categories
                    if (_mainWindow != null)
                    {
                        if (_mainWindow.ContentFrame?.Content is Pages.ModGridPage modGridPage && 
                            modGridPage.CurrentViewMode == Pages.ModGridPage.ViewMode.Categories)
                        {
                            modGridPage.RefreshCategoryFavoritesAnimated();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to toggle category favorite via gamepad in overlay", ex);
            }
        }

        private void ToggleModFavorite()
        {
            try
            {
                // Find currently selected mod
                if (_selectedMod == null) return;

                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag)) return;

                // Toggle favorite status
                SettingsManager.ToggleModFavorite(gameTag, _selectedMod.Category ?? "Other", _selectedMod.Name);
                _selectedMod.IsFavorite = SettingsManager.IsModFavorite(gameTag, _selectedMod.Category ?? "Other", _selectedMod.Name);
                
                Logger.LogInfo($"Overlay Gamepad: Toggled favorite for mod: {_selectedMod.Name}, IsFavorite: {_selectedMod.IsFavorite}");
                
                // Vibrate to confirm action
                _gamepadManager?.Vibrate((ushort)(_selectedMod.IsFavorite ? 40000 : 20000), (ushort)0, 200);
                
                // Refresh current category to update sorting
                if (!string.IsNullOrEmpty(_selectedCategoryPath))
                {
                    LoadModsFromCategory(_selectedCategoryPath, _showActiveOnly);
                }
                
                // Update ModGridPage if it's showing mods
                if (_mainWindow != null)
                {
                    if (_mainWindow.ContentFrame?.Content is Pages.ModGridPage modGridPage && 
                        modGridPage.CurrentViewMode == Pages.ModGridPage.ViewMode.Mods)
                    {
                        modGridPage.RefreshModFavorites();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to toggle mod favorite via gamepad in overlay", ex);
            }
        }
    }
}
