using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class GameBananaBrowserUserControl : UserControl
    {
        private string _gameTag = "";
        private int _currentPage = 1;
        private string? _currentSearch = null;
        private ObservableCollection<ModViewModel> _mods = new();
        private System.Collections.Generic.Dictionary<string, string> _lang = new();
        private GameBananaService.ModDetailsResponse? _currentModDetails;
        private ObservableCollection<Models.GameBananaFileViewModel> _detailFiles = new();
        
        // Infinite scroll
        private bool _isLoadingMore = false;
        private bool _hasMorePages = true;
        private ScrollViewer? _modsScrollViewer = null;
        private DateTime _lastScrollTime = DateTime.MinValue;
        
        // Tilt animation system
        private readonly System.Collections.Generic.Dictionary<Button, (double tiltX, double tiltY)> _tileTiltTargets = new();
        private readonly System.Collections.Generic.Dictionary<Button, DateTime> _lastTileAnimationUpdate = new();
        
        // Image slider for details
        private System.Collections.Generic.List<string> _detailPreviewImages = new();
        private int _currentDetailImageIndex = 0;
        
        // Markdown image resizing
        private bool _isAttachedToSizeChanged = false;
        
        private enum NavigationState
        {
            ModsList,
            ModDetails
        }
        
        private NavigationState _currentState = NavigationState.ModsList;

        public class ModViewModel : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string AuthorName { get; set; } = "";
            public string ProfileUrl { get; set; } = "";
            public string? ImageUrl { get; set; }
            public int DownloadCount { get; set; }
            public int LikeCount { get; set; }
            public int ViewCount { get; set; }
            public long DateAdded { get; set; }
            public long DateModified { get; set; }
            public long DateUpdated { get; set; }
            public bool IsRated { get; set; } = false;
            
            public string DownloadCountFormatted => FormatCount(DownloadCount);
            public string LikeCountFormatted => FormatCount(LikeCount);
            public string ViewCountFormatted => FormatCount(ViewCount);
            public string DateAddedFormatted => FormatDate(DateAdded);
            public string DateModifiedFormatted => FormatDate(DateModified);
            public string DateUpdatedFormatted => FormatDate(DateUpdated);
            public Visibility HasUpdate => (DateUpdated > 0 && DateUpdated != DateAdded) ? Visibility.Visible : Visibility.Collapsed;

            private BitmapImage? _imageSource;
            public BitmapImage? ImageSource
            {
                get => _imageSource;
                set
                {
                    if (_imageSource != value)
                    {
                        _imageSource = value;
                        OnPropertyChanged(nameof(ImageSource));
                    }
                }
            }

            private static string FormatCount(int count)
            {
                if (count >= 1000000)
                    return $"{count / 1000000.0:F1}M";
                if (count >= 1000)
                    return $"{count / 1000.0:F1}K";
                return count.ToString();
            }

            private static string FormatDate(long timestamp)
            {
                if (timestamp == 0) return "";
                
                var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                // Return date in local format (e.g., "16.11.2024" or "11/16/2024" depending on locale)
                return date.ToShortDateString();
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // Event for closing the panel
        public event EventHandler? CloseRequested;

        public GameBananaBrowserUserControl(string gameTag)
        {
            InitializeComponent();
            _gameTag = gameTag;
            
            // Load language
            _lang = SharedUtilities.LoadLanguageDictionary("GameBananaBrowser");
            
            // Set window title based on game
            var gameName = GetGameName(gameTag);
            var titleFormat = SharedUtilities.GetTranslation(_lang, "BrowseTitle");
            TitleText.Text = string.Format(titleFormat, gameName);

            // Set UI text
            SearchBox.PlaceholderText = SharedUtilities.GetTranslation(_lang, "SearchPlaceholder");
            
            ModsGridView.ItemsSource = _mods;
            
            // Attach to the named ScrollViewer directly
            ModsScrollViewer.ViewChanged += ModsScrollViewer_ViewChanged;
            _modsScrollViewer = ModsScrollViewer;
            Logger.LogInfo("ScrollViewer attached directly from XAML");
            
            // Load mods (sections will be extracted from loaded mods)
            _ = LoadModsAsync();
        }

        private void ModsScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            var now = DateTime.Now;
            
            // Throttle during rapid scrolling - only process every 100ms
            if ((now - _lastScrollTime).TotalMilliseconds < 100)
            {
                return;
            }
            
            _lastScrollTime = now;
            
            Logger.LogInfo($"Scroll detected - Offset: {_modsScrollViewer?.VerticalOffset}, Scrollable: {_modsScrollViewer?.ScrollableHeight}");
            
            // Load more mods if user is scrolling near the end
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                LoadMoreModsIfNeeded();
            });
        }

        private void LoadMoreModsIfNeeded()
        {
            if (_modsScrollViewer == null)
            {
                Logger.LogWarning("ScrollViewer is null");
                return;
            }
            
            if (_isLoadingMore)
            {
                Logger.LogInfo("Already loading more mods");
                return;
            }
            
            if (!_hasMorePages)
            {
                Logger.LogInfo("No more pages available");
                return;
            }
            
            // Check if we're near the bottom
            var scrollableHeight = _modsScrollViewer.ScrollableHeight;
            var currentVerticalOffset = _modsScrollViewer.VerticalOffset;
            var viewportHeight = _modsScrollViewer.ViewportHeight;
            
            // Load more when we're within 2 viewport heights of the bottom
            var loadMoreThreshold = scrollableHeight - (viewportHeight * 2);
            
            Logger.LogInfo($"Checking load threshold: {currentVerticalOffset} >= {loadMoreThreshold} (scrollable: {scrollableHeight}, viewport: {viewportHeight})");
            
            if (currentVerticalOffset >= loadMoreThreshold)
            {
                Logger.LogInfo("Loading more mods...");
                _ = LoadMoreModsAsync();
            }
        }

        private string GetGameName(string gameTag)
        {
            return gameTag switch
            {
                "ZZMI" => "Zenless Zone Zero",
                "GIMI" => "Genshin Impact",
                "HIMI" => "Honkai Impact 3rd",
                "WWMI" => "Wuthering Waves",
                "SRMI" => "Honkai Star Rail",
                _ => "Game"
            };
        }

        private async Task LoadModsAsync()
        {
            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                EmptyPanel.Visibility = Visibility.Collapsed;
                ModsGridView.Visibility = Visibility.Collapsed;

                _mods.Clear();
                
                // Fetch single page from API
                var response = await GameBananaService.GetModsAsync(
                    _gameTag, 
                    _currentPage, 
                    _currentSearch, 
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);

                if (response?.Records == null || response.Records.Count == 0)
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    EmptyPanel.Visibility = Visibility.Visible;
                    EmptyText.Text = string.IsNullOrEmpty(_currentSearch) 
                        ? SharedUtilities.GetTranslation(_lang, "NoModsFound")
                        : SharedUtilities.GetTranslation(_lang, "NoModsMatchSearch");
                    _hasMorePages = false;
                    return;
                }

                // Check if there are more pages
                if (response.Metadata != null)
                {
                    _hasMorePages = !response.Metadata.IsComplete;
                }
                else
                {
                    _hasMorePages = response.Records.Count >= 50;
                }

                foreach (var record in response.Records)
                {
                    // Skip NSFW content if setting is enabled
                    if (record.HasContentRatings && SettingsManager.Current.BlurNSFWThumbnails)
                    {
                        continue;
                    }

                    var viewModel = new ModViewModel
                    {
                        Id = record.Id,
                        Name = record.Name,
                        AuthorName = record.Submitter?.Name ?? "Unknown",
                        ProfileUrl = record.ProfileUrl,
                        LikeCount = record.GetLikeCount(),
                        ViewCount = record.GetViewCount(),
                        DateAdded = record.DateAdded,
                        DateModified = record.DateModified,
                        DateUpdated = record.DateUpdated,
                        IsRated = record.HasContentRatings
                    };

                    // Get preview image
                    var image = record.PreviewMedia?.Images?.FirstOrDefault();
                    if (image != null)
                    {
                        viewModel.ImageUrl = $"{image.BaseUrl}/{image.File220 ?? image.File100 ?? image.File}";
                    }

                    _mods.Add(viewModel);
                }

                if (_mods.Count == 0)
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    EmptyPanel.Visibility = Visibility.Visible;
                    EmptyText.Text = string.IsNullOrEmpty(_currentSearch) 
                        ? SharedUtilities.GetTranslation(_lang, "NoModsFound")
                        : SharedUtilities.GetTranslation(_lang, "NoModsMatchSearch");
                    return;
                }

                // Load images asynchronously
                _ = LoadImagesAsync();

                LoadingPanel.Visibility = Visibility.Collapsed;
                ModsGridView.Visibility = Visibility.Visible;
                
                // If we have very few mods (likely due to NSFW filtering), load more automatically
                if (_mods.Count < 20 && _hasMorePages)
                {
                    Logger.LogInfo($"Only {_mods.Count} mods loaded, auto-loading more pages...");
                    _ = AutoLoadMorePagesAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load mods from GameBanana", ex);
                LoadingPanel.Visibility = Visibility.Collapsed;
                EmptyPanel.Visibility = Visibility.Visible;
                EmptyText.Text = SharedUtilities.GetTranslation(_lang, "FailedToLoadMods");
            }
        }

        private async Task AutoLoadMorePagesAsync()
        {
            // Auto-load up to 3 more pages if we have too few mods
            int pagesLoaded = 0;
            while (_mods.Count < 30 && _hasMorePages && pagesLoaded < 3)
            {
                await LoadMoreModsAsync();
                pagesLoaded++;
                await Task.Delay(100); // Small delay between requests
            }
        }

        private async Task LoadMoreModsAsync()
        {
            if (_isLoadingMore || !_hasMorePages) return;
            
            _isLoadingMore = true;
            LoadingMorePanel.Visibility = Visibility.Visible;
            
            try
            {
                _currentPage++;
                
                var response = await GameBananaService.GetModsAsync(
                    _gameTag, 
                    _currentPage, 
                    _currentSearch, 
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);

                if (response?.Records == null || response.Records.Count == 0)
                {
                    _hasMorePages = false;
                    return;
                }

                // Check if there are more pages
                if (response.Metadata != null)
                {
                    _hasMorePages = !response.Metadata.IsComplete;
                }
                else
                {
                    _hasMorePages = response.Records.Count >= 50;
                }

                foreach (var record in response.Records)
                {
                    // Skip NSFW content if setting is enabled
                    if (record.HasContentRatings && SettingsManager.Current.BlurNSFWThumbnails)
                    {
                        continue;
                    }

                    var viewModel = new ModViewModel
                    {
                        Id = record.Id,
                        Name = record.Name,
                        AuthorName = record.Submitter?.Name ?? "Unknown",
                        ProfileUrl = record.ProfileUrl,
                        LikeCount = record.GetLikeCount(),
                        ViewCount = record.GetViewCount(),
                        DateAdded = record.DateAdded,
                        DateModified = record.DateModified,
                        DateUpdated = record.DateUpdated,
                        IsRated = record.HasContentRatings
                    };

                    // Get preview image
                    var image = record.PreviewMedia?.Images?.FirstOrDefault();
                    if (image != null)
                    {
                        viewModel.ImageUrl = $"{image.BaseUrl}/{image.File220 ?? image.File100 ?? image.File}";
                    }

                    _mods.Add(viewModel);
                }

                // Load images for new mods
                _ = LoadImagesAsync();
                
                // Check if we still need more content after loading
                await Task.Delay(200); // Wait for layout to update
                CheckIfNeedMoreContent();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load more mods from GameBanana", ex);
                _hasMorePages = false;
            }
            finally
            {
                _isLoadingMore = false;
                LoadingMorePanel.Visibility = Visibility.Collapsed;
            }
        }

        private void CheckIfNeedMoreContent()
        {
            // If ScrollViewer doesn't have enough content to scroll, load more
            if (_modsScrollViewer != null && _hasMorePages && !_isLoadingMore)
            {
                var scrollableHeight = _modsScrollViewer.ScrollableHeight;
                
                // If there's no scrollable content (everything fits on screen), load more
                if (scrollableHeight <= 0)
                {
                    Logger.LogInfo("No scrollable content, loading more mods automatically");
                    _ = LoadMoreModsAsync();
                }
            }
        }

        private async Task LoadImagesAsync()
        {
            foreach (var mod in _mods.Where(m => !string.IsNullOrEmpty(m.ImageUrl)))
            {
                try
                {
                    var bitmap = new BitmapImage(new Uri(mod.ImageUrl!));
                    mod.ImageSource = bitmap;
                }
                catch
                {
                    // Image loading failed, skip
                }
                
                await Task.Delay(10);
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Wait for user to finish typing (debounce)
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            _currentSearch = string.IsNullOrWhiteSpace(args.QueryText) ? null : args.QueryText;
            _currentPage = 1;
            _ = LoadModsAsync();
        }

        private void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                _ = LoadModsAsync();
            }
        }

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            _currentPage++;
            _ = LoadModsAsync();
        }

        private void ModsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ModViewModel mod)
            {
                // Show details panel
                _ = ShowModDetailsAsync(mod.Id);
            }
        }

        private void ModTile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModViewModel mod)
            {
                // Show details panel
                _ = ShowModDetailsAsync(mod.Id);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Handle back navigation based on current state
            if (_currentState == NavigationState.ModDetails)
            {
                // Go back to mods list
                CloseDetailsPanel();
            }
            else
            {
                // Close the entire panel
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void BackButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.2,
                Duration = new Duration(TimeSpan.FromMilliseconds(100))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            storyboard.Children.Add(scaleAnimation);

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.2,
                Duration = new Duration(TimeSpan.FromMilliseconds(100))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
            storyboard.Children.Add(scaleAnimationY);

            storyboard.Begin();
        }

        private void BackButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(100))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            storyboard.Children.Add(scaleAnimation);

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(100))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, BackIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
            storyboard.Children.Add(scaleAnimationY);

            storyboard.Begin();
        }

        private async Task ShowModDetailsAsync(int modId)
        {
            try
            {
                // Show loading state in details panel first
                DetailLoadingPanel.Visibility = Visibility.Visible;
                DetailAuthor.Visibility = Visibility.Collapsed;
                DetailImage.Visibility = Visibility.Collapsed;
                DetailDescriptionTitle.Visibility = Visibility.Collapsed;
                DetailFilesTitle.Visibility = Visibility.Collapsed;
                DetailFilesList.Visibility = Visibility.Collapsed;
                DetailDownloadButton.Visibility = Visibility.Collapsed;
                DetailOpenBrowserButton.Visibility = Visibility.Collapsed;
                
                // Animate transition to details
                AnimateContentSwitch(ModsListGrid, DetailsPanel);
                _currentState = NavigationState.ModDetails;
                
                // Change back button icon to left arrow
                BackIcon.Glyph = "\uE72B"; // Left arrow

                // Load mod details
                _currentModDetails = await GameBananaService.GetModDetailsAsync(modId);

                if (_currentModDetails == null)
                {
                    CloseDetailsPanel();
                    return;
                }

                // Update UI
                TitleText.Text = _currentModDetails.Name;
                DetailAuthor.Text = _currentModDetails.Submitter?.Name ?? "Unknown";
                
                // Load description in MarkdownTextBlock
                LoadDescriptionInMarkdown(_currentModDetails.Description);
                
                // Load author avatar
                if (!string.IsNullOrEmpty(_currentModDetails.Submitter?.AvatarUrl))
                {
                    try
                    {
                        DetailAuthorAvatar.Source = new BitmapImage(new Uri(_currentModDetails.Submitter.AvatarUrl));
                    }
                    catch
                    {
                        DetailAuthorAvatar.Source = null;
                    }
                }
                else
                {
                    DetailAuthorAvatar.Source = null;
                }
                
                // Set profile link
                DetailAuthorProfileLink.Tag = _currentModDetails.Submitter?.ProfileUrl;

                // Load preview images into slider
                _detailPreviewImages.Clear();
                _currentDetailImageIndex = 0;
                
                if (_currentModDetails.PreviewMedia?.Images != null && _currentModDetails.PreviewMedia.Images.Count > 0)
                {
                    foreach (var image in _currentModDetails.PreviewMedia.Images)
                    {
                        var imageUrl = $"{image.BaseUrl}/{image.File530 ?? image.File}";
                        _detailPreviewImages.Add(imageUrl);
                    }
                    
                    LoadCurrentDetailImage();
                    UpdateDetailImageNavigation();
                }

                // Load files
                _detailFiles.Clear();
                if (_currentModDetails.Files != null && _currentModDetails.Files.Count > 0)
                {
                    foreach (var file in _currentModDetails.Files)
                    {
                        _detailFiles.Add(new Models.GameBananaFileViewModel
                        {
                            Id = file.Id,
                            FileName = file.FileName,
                            FileSize = file.FileSize,
                            Description = file.Description,
                            DownloadUrl = file.DownloadUrl,
                            DownloadCount = file.DownloadCount,
                            DateAdded = file.DateAdded
                        });
                    }

                    DetailFilesList.ItemsSource = _detailFiles;
                    
                    // Auto-select first file
                    if (_detailFiles.Count > 0)
                    {
                        DetailFilesList.SelectedIndex = 0;
                    }
                    
                    DetailDownloadButton.IsEnabled = true;
                }
                else
                {
                    DetailDownloadButton.IsEnabled = false;
                    DetailDownloadButton.Content = SharedUtilities.GetTranslation(_lang, "NoFilesAvailable");
                }

                // Show content (WebView/TextBlock visibility is set in LoadDescriptionInWebView)
                DetailLoadingPanel.Visibility = Visibility.Collapsed;
                DetailAuthor.Visibility = Visibility.Visible;
                DetailImage.Visibility = Visibility.Visible;
                DetailDescriptionTitle.Visibility = Visibility.Visible;
                DetailFilesTitle.Visibility = Visibility.Visible;
                DetailFilesList.Visibility = Visibility.Visible;
                DetailDownloadButton.Visibility = Visibility.Visible;
                DetailOpenBrowserButton.Visibility = Visibility.Visible;
                
                // Ensure size changed event is attached for markdown images
                if (!_isAttachedToSizeChanged)
                {
                    DetailDescriptionScrollViewer.SizeChanged += DetailDescriptionScrollViewer_SizeChanged;
                    _isAttachedToSizeChanged = true;
                }
                
                // Attach to the markdown text block layout updated event
                DetailDescriptionMarkdown.LayoutUpdated += DetailDescriptionMarkdown_LayoutUpdated;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load mod details", ex);
                CloseDetailsPanel();
            }
        }
        
        private void DetailDescriptionMarkdown_LayoutUpdated(object? sender, object e)
        {
            // Update images when the markdown layout is updated
            UpdateMarkdownImages();
        }

        private void CloseDetailsPanel()
        {
            // Animate transition back to list
            AnimateContentSwitch(DetailsPanel, ModsListGrid);
            _currentModDetails = null;
            _detailFiles.Clear();
            _currentState = NavigationState.ModsList;
            
            // Change back button icon to X (close)
            BackIcon.Glyph = "\uE711"; // Cancel/Close icon
            
            // Restore title
            var gameName = GetGameName(_gameTag);
            var titleFormat = SharedUtilities.GetTranslation(_lang, "BrowseTitle");
            TitleText.Text = string.Format(titleFormat, gameName);
            
            // Clean up event handlers to prevent memory leaks
            if (_isAttachedToSizeChanged)
            {
                DetailDescriptionScrollViewer.SizeChanged -= DetailDescriptionScrollViewer_SizeChanged;
                _isAttachedToSizeChanged = false;
            }
            
            // Clean up markdown layout updated event handler
            DetailDescriptionMarkdown.LayoutUpdated -= DetailDescriptionMarkdown_LayoutUpdated;
        }

        private void AnimateContentSwitch(UIElement hideElement, UIElement showElement)
        {
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            
            // Fade out current content
            var fadeOut = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeOut, hideElement);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeOut, "Opacity");
            storyboard.Children.Add(fadeOut);
            
            storyboard.Completed += (s, e) =>
            {
                hideElement.Visibility = Visibility.Collapsed;
                showElement.Visibility = Visibility.Visible;
                showElement.Opacity = 0;
                
                // Fade in new content
                var fadeInStoryboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                var fadeIn = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeIn, showElement);
                Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeIn, "Opacity");
                fadeInStoryboard.Children.Add(fadeIn);
                fadeInStoryboard.Begin();
            };
            
            storyboard.Begin();
        }

        private async void DetailDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentModDetails == null) return;

            var selectedFiles = DetailFilesList.SelectedItems.Cast<Models.GameBananaFileViewModel>().ToList();

            if (selectedFiles.Count == 0)
            {
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(_lang, "Error"),
                    Content = SharedUtilities.GetTranslation(_lang, "SelectAtLeastOneFile"),
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }

            // Show file extraction dialog
            var extractDialog = new Dialogs.GameBananaFileExtractionDialog(selectedFiles, _currentModDetails.Name, _gameTag, _currentModDetails.ProfileUrl);
            extractDialog.XamlRoot = XamlRoot;
            var result = await extractDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                CloseDetailsPanel();
            }
        }

        private async void DetailOpenBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentModDetails != null && !string.IsNullOrEmpty(_currentModDetails.ProfileUrl))
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(_currentModDetails.ProfileUrl));
            }
        }








        // Tilt animation methods
        private void ModTile_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                try
                {
                    button.PointerMoved += ModTile_PointerMoved;
                    CalculateTileTiltTarget(button, e);
                    UpdateTileTiltSmooth(button);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in ModTile_PointerEntered", ex);
                }
            }
        }

        private void ModTile_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                try
                {
                    button.PointerMoved -= ModTile_PointerMoved;
                    
                    var tileBorder = FindTileBorder(button);
                    if (tileBorder?.Projection is Microsoft.UI.Xaml.Media.PlaneProjection projection)
                    {
                        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                        var easing = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase();
                        
                        var rotXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                        {
                            To = 0,
                            Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                            EasingFunction = easing
                        };
                        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotXAnim, projection);
                        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotXAnim, "RotationX");
                        storyboard.Children.Add(rotXAnim);
                        
                        var rotYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                        {
                            To = 0,
                            Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                            EasingFunction = easing
                        };
                        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotYAnim, projection);
                        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotYAnim, "RotationY");
                        storyboard.Children.Add(rotYAnim);
                        
                        storyboard.Begin();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in ModTile_PointerExited", ex);
                }
            }
        }

        private void ModTile_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                try
                {
                    var now = DateTime.Now;
                    if (_lastTileAnimationUpdate.TryGetValue(button, out var lastUpdate) && 
                        (now - lastUpdate).TotalMilliseconds < 16) return;
                    
                    _lastTileAnimationUpdate[button] = now;
                    CalculateTileTiltTarget(button, e);
                    UpdateTileTiltSmooth(button);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in ModTile_PointerMoved", ex);
                }
            }
        }

        private void CalculateTileTiltTarget(Button btn, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                var position = e.GetCurrentPoint(btn);
                var buttonWidth = btn.ActualWidth;
                var buttonHeight = btn.ActualHeight;
                
                if (buttonWidth > 0 && buttonHeight > 0)
                {
                    var centerX = buttonWidth / 2;
                    var centerY = buttonHeight / 2;
                    var offsetX = (position.Position.X - centerX) / centerX;
                    var offsetY = (position.Position.Y - centerY) / centerY;
                    
                    var maxTilt = 8.0;
                    var targetTiltX = offsetY * maxTilt;
                    var targetTiltY = -offsetX * maxTilt;
                    
                    _tileTiltTargets[btn] = (targetTiltX, targetTiltY);
                }
            }
            catch { }
        }

        private void UpdateTileTiltSmooth(Button btn)
        {
            try
            {
                var tileBorder = FindTileBorder(btn);
                if (tileBorder == null) return;
                
                var projection = GetOrCreateTileProjection(tileBorder);
                if (projection == null) return;
                
                var currentTiltX = projection.RotationX;
                var currentTiltY = projection.RotationY;
                
                var (targetTiltX, targetTiltY) = _tileTiltTargets.GetValueOrDefault(btn, (0, 0));
                
                var lerpFactor = 0.2;
                var newTiltX = currentTiltX + ((targetTiltX - currentTiltX) * lerpFactor);
                var newTiltY = currentTiltY + ((targetTiltY - currentTiltY) * lerpFactor);
                
                projection.RotationX = newTiltX;
                projection.RotationY = newTiltY;
            }
            catch { }
        }

        private Microsoft.UI.Xaml.Media.PlaneProjection? GetOrCreateTileProjection(Border tileBorder)
        {
            if (tileBorder.Projection is not Microsoft.UI.Xaml.Media.PlaneProjection projection)
            {
                projection = new Microsoft.UI.Xaml.Media.PlaneProjection
                {
                    CenterOfRotationX = 0.5,
                    CenterOfRotationY = 0.5
                };
                tileBorder.Projection = projection;
            }
            return projection;
        }

        private Border? FindTileBorder(Button btn)
        {
            try
            {
                if (btn.Content is Border directBorder)
                    return directBorder;

                return FindChildBorderByName(btn, "TileBorder");
            }
            catch
            {
                return null;
            }
        }

        private Border? FindChildBorderByName(DependencyObject parent, string name)
        {
            int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is Border b && b.Name == name)
                    return b;
                var result = FindChildBorderByName(child, name);
                if (result != null) return result;
            }
            return null;
        }

        // Detail image slider methods
        private void LoadCurrentDetailImage()
        {
            try
            {
                if (_detailPreviewImages.Count > 0 && _currentDetailImageIndex >= 0 && _currentDetailImageIndex < _detailPreviewImages.Count)
                {
                    var imageUrl = _detailPreviewImages[_currentDetailImageIndex];
                    DetailImage.Source = new BitmapImage(new Uri(imageUrl));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error loading detail image", ex);
            }
        }

        private void UpdateDetailImageNavigation()
        {
            bool hasMultipleImages = _detailPreviewImages.Count > 1;
            
            DetailPrevImageButton.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            DetailNextImageButton.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            DetailImageCounterBorder.Visibility = hasMultipleImages ? Visibility.Visible : Visibility.Collapsed;
            
            if (hasMultipleImages)
            {
                DetailImageCounterText.Text = $"{_currentDetailImageIndex + 1} / {_detailPreviewImages.Count}";
            }
        }

        private void DetailPrevImageButton_Click(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_currentDetailImageIndex > 0)
            {
                _currentDetailImageIndex--;
                LoadCurrentDetailImage();
                UpdateDetailImageNavigation();
            }
        }

        private void DetailNextImageButton_Click(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_currentDetailImageIndex < _detailPreviewImages.Count - 1)
            {
                _currentDetailImageIndex++;
                LoadCurrentDetailImage();
                UpdateDetailImageNavigation();
            }
        }

        // Detail image tilt animation - DISABLED for performance
        private void DetailImageCoordinateField_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Disabled for performance
        }

        private void DetailImageCoordinateField_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Disabled for performance
        }

        private void DetailImageCoordinateField_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Disabled for performance
        }

        // Author avatar hover effect
        private void AuthorAvatar_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.1,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, DetailAuthorAvatarScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            storyboard.Children.Add(scaleAnimation);

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.1,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, DetailAuthorAvatarScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
            storyboard.Children.Add(scaleAnimationY);

            storyboard.Begin();
        }

        private void AuthorAvatar_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, DetailAuthorAvatarScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            storyboard.Children.Add(scaleAnimation);

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, DetailAuthorAvatarScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
            storyboard.Children.Add(scaleAnimationY);

            storyboard.Begin();
        }

        private async void DetailAuthorProfileLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is HyperlinkButton button && button.Tag is string profileUrl && !string.IsNullOrEmpty(profileUrl))
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(profileUrl));
            }
        }

        private void LoadDescriptionInMarkdown(string? htmlContent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(htmlContent))
                {
                    DetailDescriptionMarkdown.Text = "No description available.";
                    return;
                }
                
                // Convert HTML to Markdown with custom config
                var config = new ReverseMarkdown.Config
                {
                    UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.PassThrough,
                    GithubFlavored = true,
                    RemoveComments = true,
                    SmartHrefHandling = true
                };
                var converter = new ReverseMarkdown.Converter(config);
                var markdown = converter.Convert(htmlContent);
                
                // Clean up excessive whitespace
                // Remove more than 2 consecutive newlines
                markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"\n{3,}", "\n\n");
                // Remove whitespace before/after images
                markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"\n+!\[", "\n\n![");
                markdown = System.Text.RegularExpressions.Regex.Replace(markdown, @"\)\n+", ")\n\n");
                
                // Trim each line
                var lines = markdown.Split('\n');
                markdown = string.Join('\n', lines.Select(l => l.Trim()));
                
                Logger.LogInfo($"Description converted to Markdown ({markdown.Length} chars)");
                
                // Just set the markdown text directly
                DetailDescriptionMarkdown.Text = markdown;
                
                // Update images after a small delay to ensure the visual tree is ready
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

        private void DetailDescriptionScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateMarkdownImages();
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
