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
        private List<string> _availablePreviewImages = new List<string>();
        private int _currentImageIndex = 0;

        private void CheckDownloadPreviewsButtonVisibility(string url)
        {
            try
            {
                // Show Download Previews button only if:
                // 1. Mod has a valid GameBanana URL
                // 2. Mod is installed (we're viewing it from mod library)
                bool hasGameBananaUrl = !string.IsNullOrEmpty(url) && 
                                       url != "https://" && 
                                       url.Contains("gamebanana.com/mods/");

                bool isModInstalled = !string.IsNullOrEmpty(_currentModDirectory) && 
                                    Directory.Exists(_currentModDirectory);

                if (hasGameBananaUrl && isModInstalled)
                {
                    DownloadPreviewsBorder.Visibility = Visibility.Visible;
                    
                    // Load language translations for the button
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    DownloadPreviewsLabel.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_DownloadPreviews_Label");
                    DownloadPreviewsDesc.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_DownloadPreviews_Desc");
                    DownloadPreviewsButtonText.Text = SharedUtilities.GetTranslation(lang, "ModDetailPage_DownloadPreviews_Button");
                    
                    // Set tooltip
                    ToolTipService.SetToolTip(DownloadPreviewsButton, 
                        SharedUtilities.GetTranslation(lang, "ModDetailPage_DownloadPreviews_Tooltip"));
                }
                else
                {
                    DownloadPreviewsBorder.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error checking download previews button visibility", ex);
                DownloadPreviewsBorder.Visibility = Visibility.Collapsed;
            }
        }

        private async void LoadPreviewImages(string fullModDir)
        {
            try
            {
                _availablePreviewImages.Clear();
                _currentImageIndex = 0;

                // Load preview images on background thread to avoid blocking UI
                var previewImages = await Task.Run(() =>
                {
                    var images = new List<string>();
                    
                    // Check for main preview (both formats)
                    var mainPreviewJpg = Path.Combine(fullModDir, "preview.jpg");
                    var mainPreviewWebp = Path.Combine(fullModDir, "preview.webp");
                    if (File.Exists(mainPreviewWebp))
                    {
                        images.Add(mainPreviewWebp);
                    }
                    else if (File.Exists(mainPreviewJpg))
                    {
                        images.Add(mainPreviewJpg);
                    }

                    // Check for preview-01 through preview-99 (both formats)
                    for (int i = 1; i <= 99; i++)
                    {
                        var previewJpg = Path.Combine(fullModDir, $"preview-{i:D2}.jpg");
                        var previewWebp = Path.Combine(fullModDir, $"preview-{i:D2}.webp");
                        if (File.Exists(previewWebp))
                        {
                            images.Add(previewWebp);
                        }
                        else if (File.Exists(previewJpg))
                        {
                            images.Add(previewJpg);
                        }
                    }
                    
                    return images;
                });
                
                // Update UI on UI thread
                _availablePreviewImages = previewImages;
                UpdateImageNavigation();
                LoadCurrentImage();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading preview images from {fullModDir}", ex);
                ModImage.Source = null;
                UpdateImageNavigation();
                // Apply effects even when there's an error
                UpdatePreviewEffects(null);
            }
        }

        private async void LoadCurrentImage()
        {
            try
            {
                if (_availablePreviewImages.Count > 0 && _currentImageIndex >= 0 && _currentImageIndex < _availablePreviewImages.Count)
                {
                    var imagePath = _availablePreviewImages[_currentImageIndex];
                    var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    
                    // Use UriSource for direct file loading - this uses Windows codecs including WebP
                    // IMPORTANT: Must use absolute path for WebP to work
                    var absolutePath = Path.GetFullPath(imagePath);
                    bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
                    
                    ModImage.Source = bitmap;
                    
                    // Apply preview effects
                    UpdatePreviewEffects(bitmap);
                    
                    // Apply elastic animation to the appropriate element
                    PreviewEffectHelper.ApplyElasticAnimation(ModImageBorder, bitmap);
                    
                    // Fade in animation for border
                    var fadeIn = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = new Duration(TimeSpan.FromMilliseconds(200))
                    };
                    
                    var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeIn, ModImageBorder);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeIn, "Opacity");
                    storyboard.Children.Add(fadeIn);
                    storyboard.Begin();
                }
                else
                {
                    ModImage.Source = null;
                    UpdatePreviewEffects(null);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading current image", ex);
                ModImage.Source = null;
                UpdatePreviewEffects(null);
            }
        }
        
        private void UpdatePreviewEffects(Microsoft.UI.Xaml.Media.ImageSource? imageSource)
        {
            // Handle border effect
            if (PreviewEffectHelper.IsBorderEnabled && imageSource != null)
            {
                // Apply border effect and replace the image
                var borderContainer = PreviewEffectHelper.ApplyBorderEffect(ModImageBorder, imageSource);
                if (borderContainer != null)
                {
                    // Find the inner border that contains the image
                    var innerBorder = ModImageBorder.Child as Border;
                    if (innerBorder != null)
                    {
                        // Replace the image with border container
                        innerBorder.Child = borderContainer;
                    }
                }
            }
            // Handle accent effect
            else if (PreviewEffectHelper.IsAccentEnabled && imageSource != null)
            {
                // Apply accent effect and replace the image
                var accentContainer = PreviewEffectHelper.ApplyAccentEffect(ModImageBorder, imageSource);
                if (accentContainer != null)
                {
                    // Find the inner border that contains the image
                    var innerBorder = ModImageBorder.Child as Border;
                    if (innerBorder != null)
                    {
                        // Replace the image with accent container
                        innerBorder.Child = accentContainer;
                    }
                }
            }
            // Handle parallax effect
            else if (PreviewEffectHelper.IsParallaxEnabled && imageSource != null)
            {
                // Create parallax container and replace the image
                var parallaxContainer = PreviewEffectHelper.CreateParallaxContainer(imageSource);
                if (parallaxContainer != null)
                {
                    // Find the inner border that contains the image
                    var innerBorder = ModImageBorder.Child as Border;
                    if (innerBorder != null)
                    {
                        // Replace the image with parallax container
                        innerBorder.Child = parallaxContainer;
                        
                        // Add mouse move handler to coordinate field for parallax
                        ModImageCoordinateField.PointerMoved -= ModImageCoordinateField_PointerMoved_Parallax;
                        ModImageCoordinateField.PointerMoved += ModImageCoordinateField_PointerMoved_Parallax;
                        ModImageCoordinateField.PointerExited -= ModImageCoordinateField_PointerExited_Parallax;
                        ModImageCoordinateField.PointerExited += ModImageCoordinateField_PointerExited_Parallax;
                    }
                }
            }
            // Handle glassmorphism effect
            else if (PreviewEffectHelper.IsGlassmorphismEnabled && imageSource != null)
            {
                // Apply glassmorphism frame and replace the image
                var glassmorphismContainer = PreviewEffectHelper.ApplyGlassmorphismEffect(ModImageBorder, imageSource);
                if (glassmorphismContainer != null)
                {
                    // Find the inner border that contains the image
                    var innerBorder = ModImageBorder.Child as Border;
                    if (innerBorder != null)
                    {
                        // Replace the image with glassmorphism container
                        innerBorder.Child = glassmorphismContainer;
                        
                        // Add mouse move handler to coordinate field for glassmorphism
                        ModImageCoordinateField.PointerMoved -= ModImageCoordinateField_PointerMoved_Glassmorphism;
                        ModImageCoordinateField.PointerMoved += ModImageCoordinateField_PointerMoved_Glassmorphism;
                        ModImageCoordinateField.PointerExited -= ModImageCoordinateField_PointerExited_Glassmorphism;
                        ModImageCoordinateField.PointerExited += ModImageCoordinateField_PointerExited_Glassmorphism;
                    }
                }
            }
            else
            {
                // Remove parallax and glassmorphism handlers and restore normal image
                ModImageCoordinateField.PointerMoved -= ModImageCoordinateField_PointerMoved_Parallax;
                ModImageCoordinateField.PointerExited -= ModImageCoordinateField_PointerExited_Parallax;
                ModImageCoordinateField.PointerMoved -= ModImageCoordinateField_PointerMoved_Glassmorphism;
                ModImageCoordinateField.PointerExited -= ModImageCoordinateField_PointerExited_Glassmorphism;
                
                var innerBorder = ModImageBorder.Child as Border;
                if (innerBorder != null && (innerBorder.Child is Grid || innerBorder.Child is Border))
                {
                    // Restore normal image
                    innerBorder.Child = ModImage;
                }
            }
        }



        private void UpdateImageNavigation()
        {
            bool hasMultipleImages = _availablePreviewImages.Count > 1;
            bool hasAnyImages = _availablePreviewImages.Count > 0;
            
            // Show/hide navigation buttons
            PrevImageButton.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            NextImageButton.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            
            // Show/hide image counter
            ImageCounterBorder.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            
            // Show/hide delete preview button (only when there's at least one image)
            DeletePreviewButton.Visibility = hasAnyImages ? Visibility.Visible : Visibility.Collapsed;
            
            // Show/hide capture screenshot button (always visible when mod is loaded)
            CaptureScreenshotButton.Visibility = !string.IsNullOrEmpty(_currentModDirectory) ? Visibility.Visible : Visibility.Collapsed;
            
            if (hasMultipleImages)
            {
                ImageCounterText.Text = $"{_currentImageIndex + 1} / {_availablePreviewImages.Count}";
                // Infinite carousel - buttons always active
            }
        }



        private void SetDefaultValues()
        {
            ModImage.Source = null;
            ModHotkeysPanel.Children.Clear();
            HotkeysSection.Visibility = Visibility.Collapsed;
            ModAuthorTextBox.Text = "";
            ModUrlTextBox.Text = "";
            ModUrlTextBox.PlaceholderText = "https://";
            ModUrlTextBox.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray);
            ModVersionTextBox.Text = "";
            ModDateCheckedPicker.Date = null;
            ModDateUpdatedPicker.Date = null;
            
            // Reset image navigation
            _availablePreviewImages.Clear();
            _currentImageIndex = 0;
            UpdateImageNavigation();
        }

        private void PrevImageButton_Click(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            e.Handled = true;
            if (_availablePreviewImages.Count == 0) return;
            
            // Infinite carousel - wrap to last image
            _currentImageIndex = _currentImageIndex > 0 
                ? _currentImageIndex - 1 
                : _availablePreviewImages.Count - 1;
            LoadCurrentImage();
            UpdateImageNavigation();
        }

        private void NextImageButton_Click(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            e.Handled = true;
            if (_availablePreviewImages.Count == 0) return;
            
            // Infinite carousel - wrap to first image
            _currentImageIndex = _currentImageIndex < _availablePreviewImages.Count - 1 
                ? _currentImageIndex + 1 
                : 0;
            LoadCurrentImage();
            UpdateImageNavigation();
        }
        
        // Image Preview - opens sliding panel
        private void ModImage_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(ModImageCoordinateField);
            if (point.Properties.IsLeftButtonPressed && _availablePreviewImages.Count > 0)
            {
                // Open image preview in sliding panel
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                var modName = ModDetailTitle.Text ?? "Preview";
                mainWindow?.ShowImagePreviewPanel(_availablePreviewImages, _currentImageIndex, modName);
            }
        }

        // Download Previews button click handler
        private async void DownloadPreviewsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get mod.json data to extract GameBanana URL
                if (string.IsNullOrEmpty(_modJsonPath) || !File.Exists(_modJsonPath))
                {
                    Logger.LogError("Cannot download previews: mod.json not found");
                    return;
                }

                var jsonContent = Services.FileAccessQueue.ReadAllText(_modJsonPath);
                var modData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(jsonContent);
                
                if (modData == null || !modData.TryGetValue("url", out var urlElement))
                {
                    Logger.LogError("Cannot download previews: No URL found in mod.json");
                    return;
                }

                var modUrl = urlElement.GetString();
                if (string.IsNullOrEmpty(modUrl) || modUrl == "https://")
                {
                    Logger.LogError("Cannot download previews: Invalid URL in mod.json");
                    return;
                }

                // Extract mod ID from URL
                var modIdMatch = System.Text.RegularExpressions.Regex.Match(modUrl, @"gamebanana\.com/mods/(\d+)");
                if (!modIdMatch.Success)
                {
                    Logger.LogError("Cannot download previews: Invalid GameBanana URL format");
                    return;
                }

                var modId = int.Parse(modIdMatch.Groups[1].Value);
                Logger.LogInfo($"Downloading previews for mod ID: {modId}");

                // Get mod details from GameBanana API to fetch preview media
                var modDetails = await Services.GameBananaService.GetModDetailsAsync(modId);
                if (modDetails == null || !modDetails.IsAvailable)
                {
                    Logger.LogError("Cannot download previews: Failed to get mod details from GameBanana API");
                    return;
                }

                // Check if previews are available
                if (modDetails.PreviewMedia?.Images == null || !modDetails.PreviewMedia.Images.Any(img => img.Type == "screenshot"))
                {
                    Logger.LogInfo("No preview images available for this mod");
                    return;
                }

                // Get mod name and other details from mod.json
                var modName = modData.TryGetValue("name", out var nameElement) ? nameElement.GetString() : "Unknown Mod";
                var category = modData.TryGetValue("category", out var categoryElement) ? categoryElement.GetString() : "Unknown";
                var author = modData.TryGetValue("author", out var authorElement) ? authorElement.GetString() : "Unknown";
                var version = modData.TryGetValue("version", out var versionElement) ? versionElement.GetString() : null;

                // Create installation dialog in preview-only mode
                var extractDialog = new Dialogs.GameBananaFileExtractionDialog(
                    new List<Models.GameBananaFileViewModel>(), // Empty file list for preview-only mode
                    modName ?? "Unknown Mod",
                    SettingsManager.CurrentSelectedGame ?? "tekken8", // Use current game
                    modUrl,
                    author,
                    modId,
                    modDetails.DateUpdated ?? modDetails.DateAdded ?? 0,
                    category,
                    modDetails.PreviewMedia,
                    false, // isNSFW - we don't have this info in mod.json
                    version,
                    _currentModDirectory); // Pass current mod directory for preview-only installation

                extractDialog.XamlRoot = XamlRoot;
                
                // Show dialog - in preview-only mode, secondary button is the main action
                var result = await extractDialog.ShowAsync();

                if (result == ContentDialogResult.Secondary) // Secondary button is "Download Previews Only"
                {
                    // Reload preview images after download
                    if (!string.IsNullOrEmpty(_currentModDirectory))
                    {
                        LoadPreviewImages(_currentModDirectory);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error downloading previews", ex);
            }
        }

        // Download Previews button hover effects
        private void DownloadPreviewsButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = 1.05,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
                };

                var scaleTransform = new Microsoft.UI.Xaml.Media.ScaleTransform
                {
                    CenterX = 0.5,
                    CenterY = 0.5
                };
                button.RenderTransform = scaleTransform;

                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, scaleTransform);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
                storyboard.Children.Add(scaleAnimation);

                var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = 1.05,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, scaleTransform);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
                storyboard.Children.Add(scaleAnimationY);

                storyboard.Begin();
            }
        }

        private void DownloadPreviewsButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button && button.RenderTransform is Microsoft.UI.Xaml.Media.ScaleTransform scaleTransform)
            {
                var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
                };

                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, scaleTransform);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
                storyboard.Children.Add(scaleAnimation);

                var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase()
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, scaleTransform);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
                storyboard.Children.Add(scaleAnimationY);

                storyboard.Begin();
            }
        }
    }
}
