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
        
        // State restoration flag
        private bool _isRestoringState = false;
        private bool _isFirstShow = true;

        
        
        /// <summary>
        /// Save current overlay state to settings
        /// </summary>
        private void SaveOverlayState()
        {
            try
            {
                SettingsManager.Current.OverlayLastCategoryPath = _selectedCategoryPath;
                SettingsManager.Current.OverlayLastModIndex = _selectedModIndex;
                SettingsManager.Current.OverlayLastModDirectory = _selectedMod?.Directory;
                SettingsManager.Current.OverlayLastShowActiveOnly = _showActiveOnly;
                
                // Save scroll position if ModsScrollViewer exists
                if (ModsScrollViewer != null)
                {
                    SettingsManager.Current.OverlayLastScrollPosition = ModsScrollViewer.VerticalOffset;
                }
                
                SettingsManager.Save();
                Logger.LogInfo($"Overlay state saved: Category={_selectedCategoryPath}, ModIndex={_selectedModIndex}, ShowActiveOnly={_showActiveOnly}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save overlay state", ex);
            }
        }
        
        /// <summary>
        /// Restore overlay state from settings
        /// </summary>
        private async void RestoreOverlayState()
        {
            try
            {
                _isRestoringState = true;
                
                var lastCategoryPath = SettingsManager.Current.OverlayLastCategoryPath;
                var lastModIndex = SettingsManager.Current.OverlayLastModIndex;
                var lastModDirectory = SettingsManager.Current.OverlayLastModDirectory;
                var lastShowActiveOnly = SettingsManager.Current.OverlayLastShowActiveOnly;
                var lastScrollPosition = SettingsManager.Current.OverlayLastScrollPosition;
                
                Logger.LogInfo($"Restoring overlay state: Category={lastCategoryPath}, ModIndex={lastModIndex}, ModDir={lastModDirectory}, ShowActiveOnly={lastShowActiveOnly}");
                
                // Restore active-only filter BEFORE loading categories
                if (lastShowActiveOnly != _showActiveOnly)
                {
                    _showActiveOnly = lastShowActiveOnly;
                }
                
                // Load categories
                await LoadCategoriesAsync();
                await Task.Delay(50);
                
                // Restore category and mod selection
                if (!string.IsNullOrEmpty(lastCategoryPath) && Directory.Exists(lastCategoryPath))
                {
                    // Find and select the category
                    var category = OverlayCategories.FirstOrDefault(c => c.Directory == lastCategoryPath);
                    if (category != null)
                    {
                        SelectCategory(category);
                        
                        // Wait for mods to load
                        await Task.Delay(150);
                        
                        // NOW restore mod selection after mods are loaded
                        if (!string.IsNullOrEmpty(lastModDirectory))
                        {
                            // Try to find mod by directory first (more reliable)
                            var mod = OverlayMods.FirstOrDefault(m => m.Directory == lastModDirectory);
                            if (mod != null)
                            {
                                _selectedModIndex = OverlayMods.IndexOf(mod);
                                _selectedMod = mod;
                                UpdateSelectedMod(mod);
                                
                                // Wait for UI to update and scroll to selected mod
                                await Task.Delay(100);
                                await ScrollModIntoView(_selectedModIndex);
                                Logger.LogInfo($"Restored mod selection: {mod.Name} at index {_selectedModIndex}");
                            }
                            else
                            {
                                Logger.LogWarning($"Could not find mod with directory: {lastModDirectory}");
                            }
                        }
                        else if (lastModIndex >= 0 && lastModIndex < OverlayMods.Count)
                        {
                            // Fallback to index-based selection
                            var mod = OverlayMods[lastModIndex];
                            _selectedModIndex = lastModIndex;
                            _selectedMod = mod;
                            UpdateSelectedMod(mod);
                            
                            // Wait for UI to update and scroll to selected mod
                            await Task.Delay(100);
                            await ScrollModIntoView(_selectedModIndex);
                            Logger.LogInfo($"Restored mod selection by index: {mod.Name} at index {_selectedModIndex}");
                        }
                        
                        // Restore scroll position if no mod was selected
                        if (_selectedModIndex < 0 && lastScrollPosition > 0 && ModsScrollViewer != null)
                        {
                            await Task.Delay(100);
                            ModsScrollViewer.ChangeView(null, lastScrollPosition, null, true);
                        }
                    }
                }
                else if (_showActiveOnly)
                {
                    // If no category was saved but active-only filter is on, load all active mods
                    LoadAllActiveMods();
                }
                else if (OverlayCategories.Count > 0)
                {
                    // Default to first category
                    SelectCategory(OverlayCategories[0]);
                }
                
                _isRestoringState = false;
                Logger.LogInfo("Overlay state restored successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to restore overlay state", ex);
                _isRestoringState = false;
            }
        }
    }
}
