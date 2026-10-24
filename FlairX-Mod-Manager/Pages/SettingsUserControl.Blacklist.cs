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




        private void LoadBlacklistSettings()
        {
            // Load blacklisted authors
            var authors = SettingsManager.Current.GameBananaBlacklistedAuthors ?? new List<string>();
            BlacklistedAuthorsListView.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<string>(authors);
            
            // Load blacklisted tags/keywords
            var tags = SettingsManager.Current.GameBananaBlacklistedTags ?? new List<string>();
            BlacklistedTagsListView.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<string>(tags);
        }

        private void AddBlacklistAuthor_Click(object sender, RoutedEventArgs e)
        {
            var authorName = BlacklistAuthorTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(authorName))
                return;
            
            // Limit to 20 characters
            if (authorName.Length > 20)
            {
                authorName = authorName.Substring(0, 20);
            }
            
            // Check if already exists
            if (SettingsManager.Current.GameBananaBlacklistedAuthors.Contains(authorName))
            {
                BlacklistAuthorTextBox.Text = string.Empty;
                return;
            }
            
            // Add to settings
            SettingsManager.Current.GameBananaBlacklistedAuthors.Add(authorName);
            SettingsManager.Save();
            
            // Update UI
            var collection = BlacklistedAuthorsListView.ItemsSource as System.Collections.ObjectModel.ObservableCollection<string>;
            collection?.Add(authorName);
            
            // Clear input
            BlacklistAuthorTextBox.Text = string.Empty;
        }

        private void BlacklistAuthorTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                AddBlacklistAuthor_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void RemoveBlacklistAuthor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string authorName)
            {
                // Remove from settings
                SettingsManager.Current.GameBananaBlacklistedAuthors.Remove(authorName);
                SettingsManager.Save();
                
                // Update UI
                var collection = BlacklistedAuthorsListView.ItemsSource as System.Collections.ObjectModel.ObservableCollection<string>;
                collection?.Remove(authorName);
            }
        }

        private void AddBlacklistTag_Click(object sender, RoutedEventArgs e)
        {
            var tag = BlacklistTagTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(tag))
                return;
            
            // Limit to 20 characters
            if (tag.Length > 20)
            {
                tag = tag.Substring(0, 20);
            }
            
            // Check if already exists
            if (SettingsManager.Current.GameBananaBlacklistedTags.Contains(tag))
            {
                BlacklistTagTextBox.Text = string.Empty;
                return;
            }
            
            // Add to settings
            SettingsManager.Current.GameBananaBlacklistedTags.Add(tag);
            SettingsManager.Save();
            
            // Update UI
            var collection = BlacklistedTagsListView.ItemsSource as System.Collections.ObjectModel.ObservableCollection<string>;
            collection?.Add(tag);
            
            // Clear input
            BlacklistTagTextBox.Text = string.Empty;
        }

        private void BlacklistTagTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                AddBlacklistTag_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void RemoveBlacklistTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                // Remove from settings
                SettingsManager.Current.GameBananaBlacklistedTags.Remove(tag);
                SettingsManager.Save();
                
                // Update UI
                var collection = BlacklistedTagsListView.ItemsSource as System.Collections.ObjectModel.ObservableCollection<string>;
                collection?.Remove(tag);
            }
        }
    }
}
