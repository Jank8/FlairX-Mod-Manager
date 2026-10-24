using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModGridPage
    {

        private void LoadActiveModsOnly()
        {
            LogToGridLog("LoadActiveModsOnly() called");
            
            // Get active mods from ModListManager (fast - no file I/O)
            var activeMods = ModListManager.GetActiveMods();
            
            // Apply filters based on settings
            bool hideBroken = SettingsManager.Current.HideBrokenMods;
            bool hideNSFW = SettingsManager.Current.HideNSFWMods;
            
            var filteredMods = activeMods.Where(mod =>
            {
                if (hideBroken && mod.IsBroken) return false;
                if (hideNSFW && mod.IsNSFW) return false;
                return true;
            }).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
            
            // Convert to lightweight ModData for lazy loading
            _allModData.Clear();
            _lastLoadedModDataIndex = 0;
            
            foreach (var modInfo in filteredMods)
            {
                _allModData.Add(new ModData
                {
                    Name = modInfo.Name,
                    Directory = modInfo.Directory,
                    ImagePath = modInfo.ImagePath,
                    IsActive = modInfo.IsActive,
                    Category = modInfo.Category,
                    Author = modInfo.Author,
                    Url = modInfo.Url,
                    LastChecked = modInfo.LastChecked,
                    LastUpdated = modInfo.LastUpdated,
                    HasUpdate = modInfo.HasUpdate,
                    IsNSFW = modInfo.IsNSFW,
                    IsBroken = modInfo.IsBroken,
                    IsFavorite = modInfo.IsFavorite,
                    Character = modInfo.Character
                });
            }
            
            // Load only first batch of tiles (lazy loading)
            const int initialLoadCount = 30;
            var initialTiles = new List<ModTile>();
            
            for (int i = 0; i < Math.Min(initialLoadCount, _allModData.Count); i++)
            {
                var modData = _allModData[i];
                initialTiles.Add(new ModTile
                {
                    Name = modData.Name,
                    Directory = modData.Directory,
                    ImagePath = modData.ImagePath,
                    IsActive = modData.IsActive,
                    Category = modData.Category,
                    Author = modData.Author,
                    Url = modData.Url,
                    LastChecked = modData.LastChecked,
                    LastUpdated = modData.LastUpdated,
                    HasUpdate = modData.HasUpdate,
                    IsVisible = true,
                    IsBroken = modData.IsBroken,
                    IsNSFW = modData.IsNSFW,
                    IsFavorite = SettingsManager.IsModFavorite(SettingsManager.CurrentSelectedGame ?? "", modData.Category ?? "Other", modData.Name),
                    ImageSource = null
                });
                _lastLoadedModDataIndex = i + 1;
            }
            
            LogToGridLog($"Loaded {initialTiles.Count} initial tiles from {_allModData.Count} active mods");
            
            // Update UI
            DispatcherQueue.TryEnqueue(() =>
            {
                _allMods.Clear();
                foreach (var mod in initialTiles)
                {
                    _allMods.Add(mod);
                }
                
                UpdateEmptyState();
            });
            
            // Load visible images
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
        }

        private void LoadBrokenModsOnly()
        {
            LogToGridLog("LoadBrokenModsOnly() called");
            
            // Get broken mods from ModListManager (fast - no file I/O)
            var brokenMods = ModListManager.GetBrokenMods();
            
            // Sort alphabetically
            var sortedMods = brokenMods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
            
            // Convert to lightweight ModData for lazy loading
            _allModData.Clear();
            _lastLoadedModDataIndex = 0;
            
            foreach (var modInfo in sortedMods)
            {
                _allModData.Add(new ModData
                {
                    Name = modInfo.Name,
                    Directory = modInfo.Directory,
                    ImagePath = modInfo.ImagePath,
                    IsActive = modInfo.IsActive,
                    Category = modInfo.Category,
                    Author = modInfo.Author,
                    Url = modInfo.Url,
                    LastChecked = modInfo.LastChecked,
                    LastUpdated = modInfo.LastUpdated,
                    HasUpdate = modInfo.HasUpdate,
                    IsNSFW = modInfo.IsNSFW,
                    IsBroken = true,
                    Character = modInfo.Character
                });
            }
            
            // Load only first batch of tiles (lazy loading)
            const int initialLoadCount = 30;
            var initialTiles = new List<ModTile>();
            
            for (int i = 0; i < Math.Min(initialLoadCount, _allModData.Count); i++)
            {
                var modData = _allModData[i];
                initialTiles.Add(new ModTile
                {
                    Name = modData.Name,
                    Directory = modData.Directory,
                    ImagePath = modData.ImagePath,
                    IsActive = modData.IsActive,
                    Category = modData.Category,
                    Author = modData.Author,
                    Url = modData.Url,
                    LastChecked = modData.LastChecked,
                    LastUpdated = modData.LastUpdated,
                    HasUpdate = modData.HasUpdate,
                    IsVisible = true,
                    IsBroken = true,
                    IsNSFW = modData.IsNSFW,
                    IsFavorite = SettingsManager.IsModFavorite(SettingsManager.CurrentSelectedGame ?? "", modData.Category ?? "Other", modData.Name),
                    ImageSource = null
                });
                _lastLoadedModDataIndex = i + 1;
            }
            
            LogToGridLog($"Loaded {initialTiles.Count} initial tiles from {_allModData.Count} broken mods");
            
            // Update UI
            DispatcherQueue.TryEnqueue(() =>
            {
                _allMods.Clear();
                foreach (var mod in initialTiles)
                {
                    _allMods.Add(mod);
                }
                
                UpdateEmptyState();
            });
            
            // Load visible images
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
        }

        private void LoadNoPreviewModsOnly()
        {
            LogToGridLog("LoadNoPreviewModsOnly() called");

            // Get all mods and filter to those with no preview image
            var allMods = ModListManager.GetAllMods();

            bool hideBroken = SettingsManager.Current.HideBrokenMods;
            bool hideNSFW = SettingsManager.Current.HideNSFWMods;

            var filteredMods = allMods.Where(mod =>
            {
                if (hideBroken && mod.IsBroken) return false;
                if (hideNSFW && mod.IsNSFW) return false;
                return string.IsNullOrEmpty(mod.ImagePath);
            }).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();

            _allModData.Clear();
            _lastLoadedModDataIndex = 0;

            foreach (var modInfo in filteredMods)
            {
                _allModData.Add(new ModData
                {
                    Name = modInfo.Name,
                    Directory = modInfo.Directory,
                    ImagePath = modInfo.ImagePath,
                    IsActive = modInfo.IsActive,
                    Category = modInfo.Category,
                    Author = modInfo.Author,
                    Url = modInfo.Url,
                    LastChecked = modInfo.LastChecked,
                    LastUpdated = modInfo.LastUpdated,
                    HasUpdate = modInfo.HasUpdate,
                    IsNSFW = modInfo.IsNSFW,
                    IsBroken = modInfo.IsBroken,
                    IsFavorite = modInfo.IsFavorite,
                    Character = modInfo.Character
                });
            }

            const int initialLoadCount = 30;
            var initialTiles = new List<ModTile>();

            for (int i = 0; i < Math.Min(initialLoadCount, _allModData.Count); i++)
            {
                var modData = _allModData[i];
                initialTiles.Add(new ModTile
                {
                    Name = modData.Name,
                    Directory = modData.Directory,
                    ImagePath = modData.ImagePath,
                    IsActive = modData.IsActive,
                    Category = modData.Category,
                    Author = modData.Author,
                    Url = modData.Url,
                    LastChecked = modData.LastChecked,
                    LastUpdated = modData.LastUpdated,
                    HasUpdate = modData.HasUpdate,
                    IsVisible = true,
                    IsBroken = modData.IsBroken,
                    IsNSFW = modData.IsNSFW,
                    IsFavorite = SettingsManager.IsModFavorite(SettingsManager.CurrentSelectedGame ?? "", modData.Category ?? "Other", modData.Name),
                    ImageSource = null
                });
                _lastLoadedModDataIndex = i + 1;
            }

            LogToGridLog($"Loaded {initialTiles.Count} initial tiles from {_allModData.Count} mods with no previews");

            DispatcherQueue.TryEnqueue(() =>
            {
                _allMods.Clear();
                foreach (var tile in initialTiles)
                    _allMods.Add(tile);
                UpdateEmptyState();
            });

            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
        }

        private void LoadOutdatedModsOnly()
        {
            LogToGridLog("LoadOutdatedModsOnly() called");
            
            // If _allModData is empty, load all mods first (like LoadAllMods does)
            if (_allModData.Count == 0)
            {
                LogToGridLog("Loading all mods first for outdated filtering");
                LoadAllMods();
            }
            
            // Filter to only outdated mods (those with HasUpdate = true)
            bool hideBroken = SettingsManager.Current.HideBrokenMods;
            bool hideNSFW = SettingsManager.Current.HideNSFWMods;
            
            var filteredMods = _allModData.Where(mod =>
            {
                if (hideBroken && mod.IsBroken) return false;
                if (hideNSFW && mod.IsNSFW) return false;
                return mod.HasUpdate; // Only mods with updates
            }).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
            
            LogToGridLog($"Filtered to {filteredMods.Count} outdated mods from {_allModData.Count} total mods");
            
            // Replace _allModData with filtered results
            _allModData.Clear();
            _lastLoadedModDataIndex = 0;
            
            foreach (var modInfo in filteredMods)
            {
                _allModData.Add(modInfo);
            }
            
            // Load only first batch of tiles (lazy loading)
            const int initialLoadCount = 30;
            var initialTiles = new List<ModTile>();
            
            for (int i = 0; i < Math.Min(initialLoadCount, _allModData.Count); i++)
            {
                var modData = _allModData[i];
                initialTiles.Add(new ModTile
                {
                    Name = modData.Name,
                    Directory = modData.Directory,
                    ImagePath = modData.ImagePath,
                    IsActive = modData.IsActive,
                    Category = modData.Category,
                    Author = modData.Author,
                    Url = modData.Url,
                    LastChecked = modData.LastChecked,
                    LastUpdated = modData.LastUpdated,
                    HasUpdate = modData.HasUpdate,
                    IsVisible = true,
                    IsBroken = modData.IsBroken,
                    IsNSFW = modData.IsNSFW,
                    IsFavorite = SettingsManager.IsModFavorite(SettingsManager.CurrentSelectedGame ?? "", modData.Category ?? "Other", modData.Name),
                    ImageSource = null
                });
                _lastLoadedModDataIndex = i + 1;
            }
            
            LogToGridLog($"Loaded {initialTiles.Count} initial tiles from {_allModData.Count} outdated mods");
            
            // Update UI
            DispatcherQueue.TryEnqueue(() =>
            {
                _allMods.Clear();
                foreach (var tile in initialTiles)
                {
                    _allMods.Add(tile);
                }
                
                UpdateEmptyState();
            });
            
            // Load visible images
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
        }

        private void LoadDuplicateModsOnly()
        {
            LogToGridLog("LoadDuplicateModsOnly() called");

            // Get all mods
            var allMods = ModListManager.GetAllMods();

            bool hideBroken = SettingsManager.Current.HideBrokenMods;
            bool hideNSFW = SettingsManager.Current.HideNSFWMods;

            // Helper function to get base name without DISABLED_ and _1, _2, etc.
            string GetBaseName(string directory)
            {
                // Remove DISABLED_ prefix
                var clean = directory.Replace("DISABLED_", "", StringComparison.OrdinalIgnoreCase);
                
                // Remove _1, _2, etc. suffix
                var lastUnderscore = clean.LastIndexOf('_');
                if (lastUnderscore > 0)
                {
                    var suffix = clean.Substring(lastUnderscore + 1);
                    // Check if suffix is a number
                    if (int.TryParse(suffix, out _))
                    {
                        return clean.Substring(0, lastUnderscore);
                    }
                }
                
                return clean;
            }

            // Group mods by their base name (without DISABLED_ and _1, _2, etc.)
            var modGroups = allMods
                .GroupBy(mod => new { BaseName = GetBaseName(mod.Directory), mod.Category })
                .Where(g => g.Count() > 1) // Only groups with more than 1 mod (duplicates)
                .ToList();

            // Flatten into list of mods that have duplicates
            var duplicateMods = new List<ModListManager.ModInfo>();
            foreach (var group in modGroups)
            {
                // Sort within group: base name first, then _1, _2, etc.
                var sortedGroup = group.OrderBy(m =>
                {
                    var clean = m.Directory.Replace("DISABLED_", "", StringComparison.OrdinalIgnoreCase);
                    var lastUnderscore = clean.LastIndexOf('_');
                    if (lastUnderscore > 0)
                    {
                        var suffix = clean.Substring(lastUnderscore + 1);
                        if (int.TryParse(suffix, out int num))
                            return num;
                    }
                    return -1; // Base name comes first
                }).ToList();
                
                duplicateMods.AddRange(sortedGroup);
            }

            // Apply filters
            var filteredMods = duplicateMods.Where(mod =>
            {
                if (hideBroken && mod.IsBroken) return false;
                if (hideNSFW && mod.IsNSFW) return false;
                return true;
            }).ToList();

            _allModData.Clear();
            _lastLoadedModDataIndex = 0;

            foreach (var modInfo in filteredMods)
            {
                _allModData.Add(new ModData
                {
                    Name = modInfo.Name,
                    Directory = modInfo.Directory,
                    ImagePath = modInfo.ImagePath,
                    IsActive = modInfo.IsActive,
                    Category = modInfo.Category,
                    Author = modInfo.Author,
                    Url = modInfo.Url,
                    LastChecked = modInfo.LastChecked,
                    LastUpdated = modInfo.LastUpdated,
                    HasUpdate = modInfo.HasUpdate,
                    IsNSFW = modInfo.IsNSFW,
                    IsBroken = modInfo.IsBroken,
                    IsFavorite = modInfo.IsFavorite,
                    Character = modInfo.Character
                });
            }

            const int initialLoadCount = 30;
            var initialTiles = new List<ModTile>();

            for (int i = 0; i < Math.Min(initialLoadCount, _allModData.Count); i++)
            {
                var modData = _allModData[i];
                initialTiles.Add(new ModTile
                {
                    Name = modData.Name,
                    Directory = modData.Directory,
                    ImagePath = modData.ImagePath,
                    IsActive = modData.IsActive,
                    Category = modData.Category,
                    Author = modData.Author,
                    Url = modData.Url,
                    LastChecked = modData.LastChecked,
                    LastUpdated = modData.LastUpdated,
                    HasUpdate = modData.HasUpdate,
                    IsVisible = true,
                    IsBroken = modData.IsBroken,
                    IsNSFW = modData.IsNSFW,
                    IsFavorite = SettingsManager.IsModFavorite(SettingsManager.CurrentSelectedGame ?? "", modData.Category ?? "Other", modData.Name),
                    ImageSource = null
                });
                _lastLoadedModDataIndex = i + 1;
            }

            LogToGridLog($"Loaded {initialTiles.Count} initial tiles from {_allModData.Count} duplicate mods ({modGroups.Count} mod groups)");

            DispatcherQueue.TryEnqueue(() =>
            {
                _allMods.Clear();
                foreach (var tile in initialTiles)
                    _allMods.Add(tile);
                UpdateEmptyState();
            });

            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                DispatcherQueue.TryEnqueue(() => LoadVisibleImages());
            });
        }
    }
}
