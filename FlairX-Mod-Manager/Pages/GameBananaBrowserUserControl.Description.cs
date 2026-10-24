using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class GameBananaBrowserUserControl
    {
        
        // Lazy image loading for description markdown
        private string? _fullDescriptionMarkdown = null;
        private bool _descriptionImagesLoaded = false;
        
        // LayoutUpdated fires on every layout pass - many times per second while scrolling or
        // resizing - and UpdateMarkdownImages walks the visual tree. Coalesce those passes.
        private DateTime _lastMarkdownImageUpdate = DateTime.MinValue;

        private void DetailDescriptionMarkdown_LayoutUpdated(object? sender, object e)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastMarkdownImageUpdate).TotalMilliseconds < 150)
                return;

            _lastMarkdownImageUpdate = now;

            // Update images when the markdown layout is updated
            UpdateMarkdownImages();
        }

        private void LoadDescriptionInMarkdown(string? htmlContent)
        {
            try
            {
                // Reset lazy-load state for the new mod
                _fullDescriptionMarkdown = null;
                _descriptionImagesLoaded = false;
                LoadImagesButton.Visibility = Visibility.Collapsed;

                if (string.IsNullOrWhiteSpace(htmlContent))
                {
                    DetailDescriptionMarkdown.Text = SharedUtilities.GetTranslation(_lang, "GB_NoDescription") ?? "No description available.";
                    return;
                }
                
                // Convert HTML to Markdown with custom config
                var config = new ReverseMarkdown.Config
                {
                    Tags = { Unknown = ReverseMarkdown.Config.UnknownTagsOption.PassThrough },
                    GithubFlavored = true,
                    Formatting = { RemoveComments = true },
                    Links = { SmartHref = true }
                };
                
                // 🆕 Preprocessing pipeline - clean HTML before conversion
                config.Preprocess
                    .RemoveScripts()              // Remove <script> tags and event handlers
                    .RemoveStyles()               // Remove style attributes and <style> tags
                    .RemoveHidden()               // Remove hidden elements (display:none, visibility:hidden)
                    .Unwrap("span, font")         // Keep text, remove wrapper elements
                    .RemoveEmptyElements();       // Clean up leftover empty tags
                
                var converter = new ReverseMarkdown.Converter(config);
                var markdown = converter.Convert(htmlContent);
                
                // Clean up excessive whitespace (still needed for final polish)
                markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"\n{3,}", "\n\n");
                markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"\n+!\[", "\n\n![");
                markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"\)\n+", ")\n\n");
                markdown = markdown.Trim();
                
                Logger.LogInfo($"Description converted to Markdown ({markdown.Length} chars)");

                // Check if the description contains any inline images
                bool hasImages = System.Text.RegularExpressions.Regex.IsMatch(markdown, @"!\[.*?\]\(.+?\)");

                if (hasImages)
                {
                    // Save full markdown for on-demand loading
                    _fullDescriptionMarkdown = markdown;

                    // Strip image lines so only text loads immediately
                    var strippedMarkdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"!\[.*?\]\(.+?\)", "");
                    // Clean up blank lines left by removed images
                    strippedMarkdown = System.Text.RegularExpressions.Regex.Replace(strippedMarkdown, @"\n{3,}", "\n\n").Trim();

                    DetailDescriptionMarkdown.Text = strippedMarkdown;

                    // Show the load-images button with translated label
                    LoadImagesButtonText.Text = SharedUtilities.GetTranslation(_lang, "GB_LoadImages") ?? "Load Images";
                    LoadImagesButton.Visibility = Visibility.Visible;
                }
                else
                {
                    // No images — set text directly, no button needed
                    DetailDescriptionMarkdown.Text = markdown;
                }
                
                // Update image sizes after a small delay to ensure the visual tree is ready
                _ = UpdateMarkdownImagesWithRetry();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to convert HTML to Markdown", ex);
                DetailDescriptionMarkdown.Text = "Failed to load description.";
            }
        }
        
        private async Task UpdateMarkdownImagesWithRetry()
        {
            // Try to update images immediately
            UpdateMarkdownImages();
            
            // Retry several times with increasing delays to ensure the visual tree is fully loaded
            for (int i = 0; i < 8; i++)
            {
                await Task.Delay(25 * (i + 1)); // 25ms, 50ms, 75ms, 100ms, 125ms, 150ms, 175ms, 200ms
                UpdateMarkdownImages();
            }
            
            // One final update with normal priority to ensure everything is properly sized
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                UpdateMarkdownImages();
            });
        }

        private void LoadImagesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_fullDescriptionMarkdown == null || _descriptionImagesLoaded) return;

            _descriptionImagesLoaded = true;
            LoadImagesButton.Visibility = Visibility.Collapsed;

            DetailDescriptionMarkdown.Text = _fullDescriptionMarkdown;
            _ = UpdateMarkdownImagesWithRetry();
        }

        private void DetailDescriptionMarkdown_Loaded(object sender, RoutedEventArgs e)
        {
            // Images will be resized on SizeChanged event, but also trigger an update here
            UpdateMarkdownImages();
        }

        private void UpdateMarkdownImages()
        {
            try
            {
                var availableWidth = DetailDescriptionScrollViewer.ActualWidth;
                if (availableWidth <= 0) 
                {
                    // If the scroll viewer width is not available yet, try to get it from the parent
                    availableWidth = DetailsPanel.ActualWidth;
                    if (availableWidth <= 0)
                    {
                        // If still not available, use a default width
                        availableWidth = 600;
                    }
                }

                var maxWidth = Math.Max(100, availableWidth - 20); // Ensure minimum width of 100px
                UpdateImagesInPanel(DetailDescriptionMarkdown, maxWidth);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error updating markdown images", ex);
            }
        }

        private void UpdateImagesInPanel(DependencyObject parent, double maxWidth)
        {
            try
            {
                int childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < childCount; i++)
                {
                    var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                    
                    if (child is Image img)
                    {
                        // Force resize all images to fit the container width
                        img.Width = maxWidth;
                        img.MaxWidth = maxWidth;
                        img.Height = double.NaN;
                        img.Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform;
                        img.HorizontalAlignment = HorizontalAlignment.Left;
                        img.VerticalAlignment = VerticalAlignment.Top;
                        img.Margin = new Thickness(0, 4, 0, 4);
                        
                        // Log the image resize for debugging
                        Logger.LogInfo($"Resized image to width: {maxWidth}");
                        
                        // Ensure the parent container also allows proper sizing
                        if (img.Parent is FrameworkElement parentElement)
                        {
                            parentElement.Height = double.NaN;
                            parentElement.MaxWidth = maxWidth;
                            parentElement.Margin = new Thickness(0, 4, 0, 4);
                            parentElement.VerticalAlignment = VerticalAlignment.Top;
                        }
                    }
                    else if (child is Microsoft.UI.Xaml.Documents.Paragraph paragraph)
                    {
                        paragraph.Margin = new Thickness(0, 4, 0, 4);
                    }
                    else if (child is Microsoft.UI.Xaml.Documents.InlineUIContainer container)
                    {
                        // Handle inline UI containers that might contain images
                        var containerChildCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(container);
                        for (int j = 0; j < containerChildCount; j++)
                        {
                            var containerChild = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(container, j);
                            if (containerChild is Image containerImg)
                            {
                                containerImg.Width = maxWidth;
                                containerImg.MaxWidth = maxWidth;
                                containerImg.Height = double.NaN;
                                containerImg.Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform;
                                containerImg.HorizontalAlignment = HorizontalAlignment.Left;
                                containerImg.VerticalAlignment = VerticalAlignment.Top;
                                containerImg.Margin = new Thickness(0, 4, 0, 4);
                                
                                Logger.LogInfo($"Resized container image to width: {maxWidth}");
                            }
                        }
                    }
                    else if (child is Panel panel)
                    {
                        // Handle panels that might contain images
                        var panelChildCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(panel);
                        for (int j = 0; j < panelChildCount; j++)
                        {
                            var panelChild = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(panel, j);
                            if (panelChild is Image panelImg)
                            {
                                panelImg.Width = maxWidth;
                                panelImg.MaxWidth = maxWidth;
                                panelImg.Height = double.NaN;
                                panelImg.Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform;
                                panelImg.HorizontalAlignment = HorizontalAlignment.Left;
                                panelImg.VerticalAlignment = VerticalAlignment.Top;
                                panelImg.Margin = new Thickness(0, 4, 0, 4);
                                
                                Logger.LogInfo($"Resized panel image to width: {maxWidth}");
                            }
                        }
                    }
                    
                    // Recursively process child elements
                    UpdateImagesInPanel(child, maxWidth);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error updating images in panel", ex);
            }
        }
    }
}
