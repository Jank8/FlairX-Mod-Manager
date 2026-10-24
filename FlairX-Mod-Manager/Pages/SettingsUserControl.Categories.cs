using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Runtime.InteropServices;
using System.Threading;
using FlairX_Mod_Manager;
using FlairX_Mod_Manager.Models;
using FlairX_Mod_Manager.Services;
using XamlImage = Microsoft.UI.Xaml.Controls.Image;
using SharpImage = SixLabors.ImageSharp.Image;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class SettingsUserControl
    {
        
        
        
        private void LoadCategoryManagement()
        {
            var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
            if (string.IsNullOrEmpty(gameTag))
                return;
                
            // Load pinned categories
            var pinnedCategories = SettingsManager.GetPinnedCategories(gameTag);
            PinnedCategoriesItemsControl.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<string>(pinnedCategories);
            
            // Load hidden categories
            var hiddenCategories = SettingsManager.GetHiddenCategories(gameTag);
            HiddenCategoriesItemsControl.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<string>(hiddenCategories);
            
            // Load shuffle excluded categories
            var shuffleExcludedCategories = SettingsManager.GetShuffleExcludedCategories(gameTag);
            ShuffleExcludedCategoriesItemsControl.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<string>(shuffleExcludedCategories);
            
            // Load conflict excluded categories
            var conflictExcludedCategories = SettingsManager.GetConflictExcludedCategories(gameTag);
            ConflictExcludedCategoriesItemsControl.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<string>(conflictExcludedCategories);
            
            // Load available categories for ComboBoxes
            RefreshAvailableCategories();
        }
        
        private void RefreshAvailableCategories()
        {
            var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
            if (string.IsNullOrEmpty(gameTag))
                return;
                
            var modsPath = PathManager.GetModsPath();
            if (!Directory.Exists(modsPath))
                return;
                
            var allCategories = Directory.GetDirectories(modsPath)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name)
                .ToList();
                
            var pinnedCategories = SettingsManager.GetPinnedCategories(gameTag);
            var hiddenCategories = SettingsManager.GetHiddenCategories(gameTag);
            var shuffleExcludedCategories = SettingsManager.GetShuffleExcludedCategories(gameTag);
            var conflictExcludedCategories = SettingsManager.GetConflictExcludedCategories(gameTag);
            
            // Available categories for pinning (exclude already pinned and hidden)
            var availableForPinning = allCategories
                .Where(c => !string.IsNullOrEmpty(c) && !pinnedCategories.Contains(c) && !hiddenCategories.Contains(c))
                .ToList();
            AddPinnedCategoryComboBox.ItemsSource = availableForPinning;
            
            // Available categories for hiding (exclude already hidden and pinned)
            var availableForHiding = allCategories
                .Where(c => !string.IsNullOrEmpty(c) && !hiddenCategories.Contains(c) && !pinnedCategories.Contains(c))
                .ToList();
            AddHiddenCategoryComboBox.ItemsSource = availableForHiding;
            
            // Available categories for shuffle exclusion (exclude already excluded)
            var availableForShuffleExclusion = allCategories
                .Where(c => !string.IsNullOrEmpty(c) && !shuffleExcludedCategories.Contains(c))
                .ToList();
            AddShuffleExcludedCategoryComboBox.ItemsSource = availableForShuffleExclusion;
            
            // Available categories for conflict exclusion (exclude already excluded)
            var availableForConflictExclusion = allCategories
                .Where(c => !string.IsNullOrEmpty(c) && !conflictExcludedCategories.Contains(c))
                .ToList();
            AddConflictExcludedCategoryComboBox.ItemsSource = availableForConflictExclusion;
        }
        
        private void AddPinnedCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AddPinnedCategoryComboBox.SelectedItem is string categoryName)
            {
                var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                SettingsManager.AddPinnedCategory(gameTag, categoryName);
                
                // Mark that category management changed
                _categoryManagementChanged = true;
                
                // Clear selection first
                AddPinnedCategoryComboBox.SelectedItem = null;
                
                // Refresh UI - this will update both lists and ComboBoxes
                LoadCategoryManagement();
            }
        }
        
        private void RemovePinnedCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string categoryName)
            {
                var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                SettingsManager.RemovePinnedCategory(gameTag, categoryName);
                
                // Mark that category management changed
                _categoryManagementChanged = true;
                
                // Refresh UI - this will update both lists and ComboBoxes
                LoadCategoryManagement();
            }
        }
        
        private void AddHiddenCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AddHiddenCategoryComboBox.SelectedItem is string categoryName)
            {
                var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                SettingsManager.AddHiddenCategory(gameTag, categoryName);
                
                // Mark that category management changed
                _categoryManagementChanged = true;
                
                // Clear selection first
                AddHiddenCategoryComboBox.SelectedItem = null;
                
                // Refresh UI - this will update both lists and ComboBoxes
                LoadCategoryManagement();
            }
        }
        
        private void RemoveHiddenCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string categoryName)
            {
                var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                SettingsManager.RemoveHiddenCategory(gameTag, categoryName);
                
                // Mark that category management changed
                _categoryManagementChanged = true;
                
                // Refresh UI - this will update both lists and ComboBoxes
                LoadCategoryManagement();
            }
        }
        
        private void AddShuffleExcludedCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AddShuffleExcludedCategoryComboBox.SelectedItem is string categoryName)
            {
                var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                SettingsManager.AddShuffleExcludedCategory(gameTag, categoryName);
                
                // Clear selection first
                AddShuffleExcludedCategoryComboBox.SelectedItem = null;
                
                // Refresh UI - this will update both lists and ComboBoxes
                LoadCategoryManagement();
            }
        }
        
        private void RemoveShuffleExcludedCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string categoryName)
            {
                var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                SettingsManager.RemoveShuffleExcludedCategory(gameTag, categoryName);
                
                // Refresh UI - this will update both lists and ComboBoxes
                LoadCategoryManagement();
            }
        }
        
        private void AddConflictExcludedCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AddConflictExcludedCategoryComboBox.SelectedItem is string categoryName)
            {
                var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                SettingsManager.AddConflictExcludedCategory(gameTag, categoryName);
                
                // Clear selection first
                AddConflictExcludedCategoryComboBox.SelectedItem = null;
                
                // Refresh UI - this will update both lists and ComboBoxes
                LoadCategoryManagement();
            }
        }
        
        private void RemoveConflictExcludedCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string categoryName)
            {
                var gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                SettingsManager.RemoveConflictExcludedCategory(gameTag, categoryName);
                
                // Refresh UI - this will update both lists and ComboBoxes
                LoadCategoryManagement();
            }
        }
    }
}
