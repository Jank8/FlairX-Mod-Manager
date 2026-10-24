using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Microsoft.UI;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.UI.Xaml.Data;
using Windows.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;
using FlairX_Mod_Manager.Models;
using Windows.Graphics.Imaging;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModGridPage
    {



        // Dynamic NSFW filtering
        public void FilterNSFWMods(bool hideNSFW)
        {
            Logger.LogGrid($"FilterNSFWMods called: hideNSFW={hideNSFW} (reloading view)");
            
            // Simply reload the current view to apply the new NSFW filter setting
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_currentCategory == null)
                {
                    // Reload all mods
                    LoadAllMods();
                }
                else if (_currentCategory == "Active")
                {
                    LoadActiveModsOnly();
                }
                else if (_currentCategory == "Broken")
                {
                    LoadBrokenModsOnly();
                }
                else if (_currentCategory == "Outdated")
                {
                    LoadOutdatedModsOnly();
                }
                else
                {
                    // Reload specific category
                    LoadModsByCategory(_currentCategory);
                }
            });
        }

        public void ReloadIfInCategoryView()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (CurrentViewMode == ViewMode.Categories && _currentCategory == null)
                {
                    LoadCategories();
                }
            });
        }
        
        /// <summary>
        /// Refresh current view - reload images with updated tile width
        /// </summary>
        public async Task RefreshCurrentViewAsync()
        {
            await Task.Run(() =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (CurrentViewMode == ViewMode.Categories)
                    {
                        if (_currentCategory == null)
                        {
                            LoadCategories();
                        }
                        else
                        {
                            LoadModsByCategory(_currentCategory);
                        }
                    }
                    else if (!string.IsNullOrEmpty(_currentCategory))
                    {
                        // In a specific category (Mods view inside a category)
                        LoadModsByCategory(_currentCategory);
                    }
                    else
                    {
                        LoadAllMods();
                    }
                    
                    // Ensure grid sizes are updated after reload completes
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                    {
                        UpdateGridItemSizes();
                    });
                });
            });
        }
        
        /// <summary>
        /// Update TileWidth for all existing tiles without reloading
        /// </summary>
        public void UpdateAllTileWidths()
        {
            bool isWide = SettingsManager.Current.UseWideTileFormat;
            double newWidth = isWide ? 592 : 277;
            var newMargin = new Microsoft.UI.Xaml.Thickness(isWide ? 4 : 12);
            
            if (ModsGrid?.ItemsSource is System.Collections.ObjectModel.ObservableCollection<ModTile> mods)
            {
                foreach (var mod in mods)
                {
                    mod.TileWidth = newWidth;
                    mod.TileMargin = newMargin;
                }
                Logger.LogInfo($"Updated TileWidth to {newWidth}px, TileMargin to {newMargin.Left}px for {mods.Count} tiles");
            }
        }
        
        // DISABLED: Blur mechanism not used - mods are hidden instead
        /*
        private void UpdateAllNSFWOverlays()
        {
            // NSFW overlay is now controlled by binding, no manual update needed
            // This method is kept for compatibility but does nothing
        }
        */

        // Refresh specific mod tile when modBroken status changes
        public void RefreshModTile(string modDirectoryName, bool isBroken)
        {
            if (ModsGrid?.ItemsSource is not ObservableCollection<ModTile> mods) return;
            if (string.IsNullOrEmpty(modDirectoryName)) return;
            
            Logger.LogGrid($"RefreshModTile called: modDirectoryName={modDirectoryName}, isBroken={isBroken}");
            
            // Find the mod tile in current view by directory name
            var modTile = mods.FirstOrDefault(m => m.Directory == modDirectoryName);
            if (modTile != null)
            {
                Logger.LogGrid($"Found mod tile: {modTile.Name}, current IsBroken: {modTile.IsBroken}");
                
                // Update the IsBroken property which will trigger UI refresh via PropertyChanged
                modTile.IsBroken = isBroken;
                Logger.LogGrid($"Updated mod tile IsBroken property: {modDirectoryName} -> {isBroken}");
            }
            else
            {
                Logger.LogGrid($"Mod tile not found in current view: {modDirectoryName}");
                Logger.LogGrid($"Available tiles: {string.Join(", ", mods.Select(m => m.Directory))}");
            }
            
            // Also update the mod data in cache if it exists
            var modData = _allModData.FirstOrDefault(m => m.Directory == modDirectoryName);
            if (modData != null)
            {
                modData.IsBroken = isBroken;
                Logger.LogGrid($"Updated mod data IsBroken property: {modDirectoryName} -> {isBroken}");
            }
            else
            {
                Logger.LogGrid($"Mod data not found in cache: {modDirectoryName}");
            }
        }
        
        // Refresh mod tile status (HasUpdate, dates, etc.) by re-reading mod.json
        public void RefreshModTileStatus(string modPath)
        {
            try
            {
                if (ModsGrid?.ItemsSource is not ObservableCollection<ModTile> mods) return;
                
                var modDirName = Path.GetFileName(modPath);
                var modJsonPath = Path.Combine(modPath, "mod.json");
                
                if (!File.Exists(modJsonPath))
                {
                    Logger.LogGrid($"mod.json not found for: {modDirName}");
                    return;
                }
                
                // Read mod.json
                var jsonText = File.ReadAllText(modJsonPath);
                using var doc = System.Text.Json.JsonDocument.Parse(jsonText);
                var root = doc.RootElement;
                
                // Parse dates
                DateTime lastUpdated = DateTime.MinValue;
                if (root.TryGetProperty("dateUpdated", out var dateUpdatedProp) && dateUpdatedProp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    DateTime.TryParse(dateUpdatedProp.GetString(), out lastUpdated);
                }
                
                // Calculate HasUpdate
                bool hasUpdate = false;
                if (root.TryGetProperty("gbChangeDate", out var gbChangeProp) && gbChangeProp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var gbChangeDateStr = gbChangeProp.GetString();
                    if (!string.IsNullOrEmpty(gbChangeDateStr) && lastUpdated != DateTime.MinValue)
                    {
                        if (DateTime.TryParse(gbChangeDateStr, out var gbDate))
                        {
                            hasUpdate = gbDate > lastUpdated;
                        }
                    }
                }
                
                Logger.LogGrid($"RefreshModTileStatus: {modDirName}, HasUpdate={hasUpdate}");
                
                // Update mod tile in UI
                DispatcherQueue.TryEnqueue(() =>
                {
                    var modTile = mods.FirstOrDefault(m => 
                        m.Directory.Equals(modDirName, StringComparison.OrdinalIgnoreCase) ||
                        m.Directory.EndsWith("\\" + modDirName, StringComparison.OrdinalIgnoreCase) ||
                        m.Directory.EndsWith("/" + modDirName, StringComparison.OrdinalIgnoreCase));
                    
                    if (modTile != null)
                    {
                        modTile.HasUpdate = hasUpdate;
                        modTile.LastUpdated = lastUpdated;
                        Logger.LogGrid($"Updated mod tile status: {modDirName} -> HasUpdate={hasUpdate}");
                    }
                });
                
                // Update cached data
                var modData = _allModData.FirstOrDefault(m => m.Directory == modDirName);
                if (modData != null)
                {
                    modData.HasUpdate = hasUpdate;
                    modData.LastUpdated = lastUpdated;
                }
                
                // Update persistent lists
                var categoryName = modData?.Category ?? "Other";
                if (hasUpdate)
                {
                    ModListManager.AddToOutdatedList(categoryName, modData?.Name ?? modDirName);
                }
                else
                {
                    ModListManager.RemoveFromOutdatedList(categoryName, modData?.Name ?? modDirName);
                }
                
                // Update cached list
                if (_cachedOutdatedMods != null)
                {
                    var cacheKey = $"{categoryName}|{modData?.Name ?? modDirName}";
                    if (hasUpdate)
                        _cachedOutdatedMods.Add(cacheKey);
                    else
                        _cachedOutdatedMods.Remove(cacheKey);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to refresh mod tile status: {modPath}", ex);
            }
        }
        
        // Check if currently showing outdated mods view
        public bool IsOutdatedView()
        {
            return _currentCategory == "Outdated";
        }
        
        // Reload outdated mods data (always reloads, even if not currently viewing)
        public void RefreshOutdatedView()
        {
            try
            {
                Logger.LogGrid("Reloading all mods data to refresh outdated status");
                
                // Clear all mod data to force full reload from disk
                _allModData.Clear();
                _lastLoadedModDataIndex = 0;
                
                // If currently viewing outdated mods, reload the view
                if (_currentCategory == "Outdated")
                {
                    Logger.LogGrid("Refreshing outdated view (currently visible)");
                    LoadOutdatedModsOnly();
                }
                else
                {
                    Logger.LogGrid("Outdated data refreshed (will be reloaded when user navigates to Outdated)");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to refresh outdated view", ex);
            }
        }
    }
}
