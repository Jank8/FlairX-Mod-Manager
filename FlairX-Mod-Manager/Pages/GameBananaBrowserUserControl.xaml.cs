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
        private bool _currentModIsNSFW = false;
        
        // Infinite scroll
        private bool _isLoadingMore = false;
        private bool _hasMorePages = true;
        private ScrollViewer? _modsScrollViewer = null;
        private DateTime _lastScrollTime = DateTime.MinValue;
        

        
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
        private bool _openedDirectlyToModDetails = false;
        private int? _returnToModId = null; // Remember mod ID when navigating to category search

        public class ModViewModel : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string AuthorName { get; set; } = "";
            public string AuthorProfileUrl { get; set; } = "";
            public string? AuthorAvatarUrl { get; set; }
            public string ProfileUrl { get; set; } = "";
            public string? ImageUrl { get; set; }
            public string CategoryName { get; set; } = "";
            public string CategoryUrl { get; set; } = "";
            public string? CategoryIconUrl { get; set; }
            public int DownloadCount { get; set; }
            public int LikeCount { get; set; }
            public int ViewCount { get; set; }
            public long DateAdded { get; set; }
            public long DateModified { get; set; }
            public long DateUpdated { get; set; }
            public bool IsRated { get; set; } = false;
            public bool IsInstalled { get; set; } = false;
            public Visibility IsInstalledVisibility => IsInstalled ? Visibility.Visible : Visibility.Collapsed;
            public string InstalledText { get; set; } = "Installed";
            
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

        public GameBananaBrowserUserControl(string gameTag, string? modUrl = null)
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
            DetailInstalledBadgeText.Text = SharedUtilities.GetTranslation(_lang, "Installed");
            DetailAuthorLabel.Text = SharedUtilities.GetTranslation(_lang, "Author");
            DetailViewProfileText.Text = SharedUtilities.GetTranslation(_lang, "ViewProfile");
            DetailCategoryLabel.Text = SharedUtilities.GetTranslation(_lang, "Category");
            DetailCategoryViewText.Text = SharedUtilities.GetTranslation(_lang, "ViewCategory");
            DetailPreviewHeader.Text = SharedUtilities.GetTranslation(_lang, "Preview");
            DetailDescriptionTitle.Text = SharedUtilities.GetTranslation(_lang, "Description");
            DetailFilesTitle.Text = SharedUtilities.GetTranslation(_lang, "Files");
            DetailOpenBrowserButtonText.Text = SharedUtilities.GetTranslation(_lang, "OpenInBrowser");
            RetryButton.Content = SharedUtilities.GetTranslation(_lang, "Retry");
            
            ModsGridView.ItemsSource = _mods;
            
            // Attach to the named ScrollViewer directly
            ModsScrollViewer.ViewChanged += ModsScrollViewer_ViewChanged;
            _modsScrollViewer = ModsScrollViewer;
            Logger.LogInfo("ScrollViewer attached directly from XAML");
            
            // If modUrl is provided, load mod details directly
            if (!string.IsNullOrEmpty(modUrl))
            {
                _openedDirectlyToModDetails = true;
                _ = LoadModDetailsFromUrlAsync(modUrl);
            }
            else
            {
                // Load mods (sections will be extracted from loaded mods)
                _ = LoadModsAsync();
            }
        }

        private bool IsModInstalled(string profileUrl)
        {
            try
            {
                var modLibraryPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modLibraryPath) || !System.IO.Directory.Exists(modLibraryPath))
                    return false;

                // Extract mod ID from profile URL
                var profileModId = ExtractModIdFromUrl(profileUrl);
                if (string.IsNullOrEmpty(profileModId))
                    return false;

                // Search all mod.json files in mod library
                foreach (var categoryDir in System.IO.Directory.GetDirectories(modLibraryPath))
                {
                    foreach (var modDir in System.IO.Directory.GetDirectories(categoryDir))
                    {
                        var modJsonPath = System.IO.Path.Combine(modDir, "mod.json");
                        if (System.IO.File.Exists(modJsonPath))
                        {
                            try
                            {
                                var jsonContent = System.IO.File.ReadAllText(modJsonPath);
                                var modData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(jsonContent);
                                
                                if (modData != null && modData.TryGetValue("url", out var urlElement))
                                {
                                    var url = urlElement.GetString();
                                    if (!string.IsNullOrEmpty(url) && url != "https://")
                                    {
                                        // Extract mod ID from stored URL and compare
                                        var storedModId = ExtractModIdFromUrl(url);
                                        if (!string.IsNullOrEmpty(storedModId) && storedModId == profileModId)
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Skip invalid mod.json files
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error checking if mod is installed", ex);
            }

            return false;
        }

        private string? ExtractModIdFromUrl(string url)
        {
            try
            {
                // GameBanana URL format: https://gamebanana.com/mods/574763
                var pattern = new System.Text.RegularExpressions.Regex(@"gamebanana\.com/mods/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var match = pattern.Match(url);
                
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            
            return null;
        }
        
        private async Task UpdateDateCheckedForMod(string profileUrl)
        {
            try
            {
                var modLibraryPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modLibraryPath) || !System.IO.Directory.Exists(modLibraryPath))
                    return;

                // Search for the mod by URL
                foreach (var categoryDir in System.IO.Directory.GetDirectories(modLibraryPath))
                {
                    foreach (var modDir in System.IO.Directory.GetDirectories(categoryDir))
                    {
                        var modJsonPath = System.IO.Path.Combine(modDir, "mod.json");
                        if (System.IO.File.Exists(modJsonPath))
                        {
                            try
                            {
                                // First check if this is the right mod (read-only check)
                                var jsonContent = await Services.FileAccessQueue.ReadAllTextAsync(modJsonPath);
                                var checkData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
                                
                                if (checkData != null && checkData.TryGetValue("url", out var urlObj))
                                {
                                    var url = urlObj?.ToString();
                                    if (!string.IsNullOrEmpty(url) && url.Equals(profileUrl, StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Found the mod - do atomic read-modify-write
                                        await Services.FileAccessQueue.ExecuteAsync(modJsonPath, async () =>
                                        {
                                            var currentJson = await System.IO.File.ReadAllTextAsync(modJsonPath);
                                            var modData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(currentJson);
                                            if (modData != null)
                                            {
                                                modData["dateChecked"] = DateTime.Now.ToString("yyyy-MM-dd");
                                                var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                                                var updatedJson = System.Text.Json.JsonSerializer.Serialize(modData, jsonOptions);
                                                await System.IO.File.WriteAllTextAsync(modJsonPath, updatedJson);
                                            }
                                        });
                                        
                                        Logger.LogInfo($"Updated dateChecked for mod at {modJsonPath}");
                                        return;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Error updating dateChecked for mod at {modJsonPath}", ex);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error updating dateChecked", ex);
            }
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
                ConnectionErrorBar.IsOpen = false;

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

                if (response == null)
                {
                    // Try Cloudflare bypass
                    Logger.LogInfo("Response is null, attempting Cloudflare bypass...");
                    var (cookies, userAgent) = await Services.CloudflareBypassService.BypassCloudflareAsync(this.XamlRoot);
                    
                    if (!string.IsNullOrEmpty(cookies))
                    {
                        // Retry the request
                        response = await GameBananaService.GetModsAsync(
                            _gameTag, 
                            _currentPage, 
                            _currentSearch, 
                            null,
                            null,
                            null,
                            null,
                            null,
                            null);
                    }
                    
                    if (response == null)
                    {
                        // Still failed - show error
                        LoadingPanel.Visibility = Visibility.Collapsed;
                        ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "ConnectionErrorTitle");
                        ConnectionErrorBar.Message = SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage");
                        ConnectionErrorBar.IsOpen = true;
                        _hasMorePages = false;
                        return;
                    }
                }

                if (response.Records == null || response.Records.Count == 0)
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

                var installedText = SharedUtilities.GetTranslation(_lang, "Installed");
                
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
                        IsRated = record.HasContentRatings,
                        IsInstalled = IsModInstalled(record.ProfileUrl),
                        InstalledText = installedText
                    };

                    // Get preview image
                    var image = record.PreviewMedia?.Images?.FirstOrDefault();
                    if (image != null)
                    {
                        viewModel.ImageUrl = $"{image.BaseUrl}/{image.File220 ?? image.File100 ?? image.File}";
                    }

                    _mods.Add(viewModel);
                }

                // If we have very few mods (likely due to NSFW filtering), load more automatically
                if (_mods.Count < 20 && _hasMorePages)
                {
                    Logger.LogInfo($"Only {_mods.Count} mods loaded after NSFW filtering, auto-loading more pages...");
                    await AutoLoadMorePagesAsync();
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
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load mods from GameBanana", ex);
                LoadingPanel.Visibility = Visibility.Collapsed;
                ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "ConnectionErrorTitle");
                ConnectionErrorBar.Message = SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage");
                ConnectionErrorBar.IsOpen = true;
            }
        }
        
        private void ConnectionErrorBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
        {
            // User closed the error notification
        }
        
        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            ConnectionErrorBar.IsOpen = false;
            _currentPage = 1;
            _ = LoadModsAsync();
        }

        private async Task AutoLoadMorePagesAsync()
        {
            // Auto-load up to 5 more pages if we have too few mods (handles heavy NSFW filtering)
            int pagesLoaded = 0;
            while (_mods.Count < 20 && _hasMorePages && pagesLoaded < 5)
            {
                await LoadMoreModsAsync();
                pagesLoaded++;
                await Task.Delay(100); // Small delay between requests
            }
            Logger.LogInfo($"AutoLoadMorePages finished: {_mods.Count} mods after {pagesLoaded} extra pages");
        }

        private async Task LoadMoreModsAsync()
        {
            if (_isLoadingMore || !_hasMorePages) return;
            
            _isLoadingMore = true;
            
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

                var installedText = SharedUtilities.GetTranslation(_lang, "Installed");
                
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
                        IsRated = record.HasContentRatings,
                        IsInstalled = IsModInstalled(record.ProfileUrl),
                        InstalledText = installedText
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
            // If search box is cleared, reset to main page
            if (string.IsNullOrWhiteSpace(sender.Text) && !string.IsNullOrEmpty(_currentSearch))
            {
                _currentSearch = null;
                _currentPage = 1;
                _ = LoadModsAsync();
            }
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
                _currentModIsNSFW = mod.IsRated;
                _ = ShowModDetailsAsync(mod.Id);
            }
        }

        private void ModTile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModViewModel mod)
            {
                // Show details panel
                _currentModIsNSFW = mod.IsRated;
                _ = ShowModDetailsAsync(mod.Id);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Handle back navigation based on current state
            if (_currentState == NavigationState.ModDetails)
            {
                // If opened directly to mod details, close the entire panel
                if (_openedDirectlyToModDetails)
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    // Go back to mods list
                    CloseDetailsPanel();
                }
            }
            else if (_returnToModId.HasValue)
            {
                // Return to mod details after category search
                var modId = _returnToModId.Value;
                _returnToModId = null;
                _ = ShowModDetailsAsync(modId);
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
                
                // Change back button icon - close (X) if opened directly, left arrow if navigated from list
                if (_openedDirectlyToModDetails)
                {
                    BackIcon.Glyph = "\uE711"; // Close (X)
                }
                else
                {
                    BackIcon.Glyph = "\uE72B"; // Left arrow
                }

                // Load mod details
                _currentModDetails = await GameBananaService.GetModDetailsAsync(modId);

                if (_currentModDetails == null)
                {
                    // API error occurred - show error and go back to list
                    CloseDetailsPanel();
                    ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "ConnectionErrorTitle");
                    ConnectionErrorBar.Message = SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage");
                    ConnectionErrorBar.IsOpen = true;
                    return;
                }
                
                // Check if mod is installed and update UI
                bool isInstalled = false;
                if (!string.IsNullOrEmpty(_currentModDetails.ProfileUrl))
                {
                    isInstalled = IsModInstalled(_currentModDetails.ProfileUrl);
                    
                    // Update dateChecked in mod.json if this mod is installed
                    if (isInstalled)
                    {
                        _ = UpdateDateCheckedForMod(_currentModDetails.ProfileUrl);
                    }
                }
                
                // Show/hide installed badge
                DetailInstalledBadge.Visibility = isInstalled ? Visibility.Visible : Visibility.Collapsed;
                
                // Update download button text based on installation status
                DetailDownloadButtonText.Text = isInstalled 
                    ? SharedUtilities.GetTranslation(_lang, "DownloadAndUpdate")
                    : SharedUtilities.GetTranslation(_lang, "DownloadAndInstall");

                // Update UI
                TitleText.Text = _currentModDetails.Name;
                
                // Show version if available
                if (!string.IsNullOrEmpty(_currentModDetails.Version))
                {
                    DetailVersionText.Text = _currentModDetails.Version;
                    DetailVersionBadge.Visibility = Visibility.Visible;
                }
                else
                {
                    DetailVersionBadge.Visibility = Visibility.Collapsed;
                }
                
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
                
                // Load category info
                if (_currentModDetails.Category != null)
                {
                    DetailCategory.Text = _currentModDetails.Category.Name ?? "Unknown";
                    
                    // Load category icon
                    if (!string.IsNullOrEmpty(_currentModDetails.Category.IconUrl))
                    {
                        try
                        {
                            DetailCategoryIcon.Source = new BitmapImage(new Uri(_currentModDetails.Category.IconUrl));
                        }
                        catch
                        {
                            DetailCategoryIcon.Source = null;
                        }
                    }
                    else
                    {
                        DetailCategoryIcon.Source = null;
                    }
                }
                else
                {
                    DetailCategory.Text = "Unknown";
                    DetailCategoryIcon.Source = null;
                }

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
                    var sizeLabel = SharedUtilities.GetTranslation(_lang, "Size");
                    var downloadsLabel = SharedUtilities.GetTranslation(_lang, "Downloads");
                    var addedLabel = SharedUtilities.GetTranslation(_lang, "Added");
                    
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
                            DateAdded = file.DateAdded,
                            SizeLabel = sizeLabel,
                            DownloadsLabel = downloadsLabel,
                            AddedLabel = addedLabel
                        });
                    }

                    DetailFilesList.ItemsSource = _detailFiles;
                    
                    // Files are unselected by default
                    foreach (var file in _detailFiles)
                    {
                        file.IsSelected = false;
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
                ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "ConnectionErrorTitle");
                ConnectionErrorBar.Message = SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage");
                ConnectionErrorBar.IsOpen = true;
            }
        }
        
        private void DetailDescriptionMarkdown_LayoutUpdated(object? sender, object e)
        {
            // Update images when the markdown layout is updated
            UpdateMarkdownImages();
        }

        private void CloseDetailsPanel()
        {
            // Hide installed badge and version badge when leaving details
            DetailInstalledBadge.Visibility = Visibility.Collapsed;
            DetailVersionBadge.Visibility = Visibility.Collapsed;
            
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

            var selectedFiles = _detailFiles.Where(f => f.IsSelected).ToList();

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
            var authorName = _currentModDetails.Submitter?.Name ?? "unknown";
            var dateUpdated = _currentModDetails.DateUpdated > 0 ? _currentModDetails.DateUpdated : _currentModDetails.DateAdded;
            var categoryName = _currentModDetails.Category?.Name; // Get category from API
            
            var extractDialog = new Dialogs.GameBananaFileExtractionDialog(
                selectedFiles, 
                _currentModDetails.Name, 
                _gameTag, 
                _currentModDetails.ProfileUrl,
                authorName,
                _currentModDetails.Id,
                dateUpdated,
                categoryName,
                _currentModDetails.PreviewMedia,
                _currentModIsNSFW, // Pass NSFW status from mod list
                _currentModDetails.Version); // Pass version from API (_sVersion)
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







        // Detail image slider methods
        private void LoadCurrentDetailImage()
        {
            try
            {
                if (_detailPreviewImages.Count > 0 && _currentDetailImageIndex >= 0 && _currentDetailImageIndex < _detailPreviewImages.Count)
                {
                    var imageUrl = _detailPreviewImages[_currentDetailImageIndex];
                    
                    // Change the image source first
                    DetailImage.Source = new BitmapImage(new Uri(imageUrl));
                    
                    // Create elastic scale animation
                    var elasticScaleX = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        From = 0.8,
                        To = 1.0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(600)),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.ElasticEase 
                        { 
                            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
                            Oscillations = 2,
                            Springiness = 8
                        }
                    };
                    
                    var elasticScaleY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        From = 0.8,
                        To = 1.0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(600)),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.ElasticEase 
                        { 
                            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
                            Oscillations = 2,
                            Springiness = 8
                        }
                    };
                    
                    // Ensure the image has a ScaleTransform
                    if (DetailImage.RenderTransform == null || !(DetailImage.RenderTransform is Microsoft.UI.Xaml.Media.ScaleTransform))
                    {
                        DetailImage.RenderTransform = new Microsoft.UI.Xaml.Media.ScaleTransform();
                        DetailImage.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5); // Center the scaling
                    }
                    
                    var scaleTransform = (Microsoft.UI.Xaml.Media.ScaleTransform)DetailImage.RenderTransform;
                    
                    // Create storyboard and apply animations
                    var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(elasticScaleX, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(elasticScaleX, "ScaleX");
                    
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(elasticScaleY, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(elasticScaleY, "ScaleY");
                    
                    storyboard.Children.Add(elasticScaleX);
                    storyboard.Children.Add(elasticScaleY);
                    
                    storyboard.Begin();
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
            e.Handled = true;
            if (_detailPreviewImages.Count == 0) return;
            
            // Infinite carousel - wrap to last image
            _currentDetailImageIndex = _currentDetailImageIndex > 0 
                ? _currentDetailImageIndex - 1 
                : _detailPreviewImages.Count - 1;
            LoadCurrentDetailImage();
            UpdateDetailImageNavigation();
        }

        private void DetailNextImageButton_Click(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            e.Handled = true;
            if (_detailPreviewImages.Count == 0) return;
            
            // Infinite carousel - wrap to first image
            _currentDetailImageIndex = _currentDetailImageIndex < _detailPreviewImages.Count - 1 
                ? _currentDetailImageIndex + 1 
                : 0;
            LoadCurrentDetailImage();
            UpdateDetailImageNavigation();
        }

        // Detail image tilt animation
        private void DetailImageCoordinateField_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Grid grid)
            {
                grid.PointerMoved += DetailImageCoordinateField_PointerMoved;
            }
        }

        private void DetailImageCoordinateField_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Grid grid)
            {
                grid.PointerMoved -= DetailImageCoordinateField_PointerMoved;
                
                // Reset tilt with animation
                if (DetailImageBorder?.Projection is Microsoft.UI.Xaml.Media.PlaneProjection projection)
                {
                    var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    var easing = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase();
                    
                    var rotXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                        EasingFunction = easing
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotXAnim, projection);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotXAnim, "RotationX");
                    storyboard.Children.Add(rotXAnim);
                    
                    var rotYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                        EasingFunction = easing
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rotYAnim, projection);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rotYAnim, "RotationY");
                    storyboard.Children.Add(rotYAnim);
                    
                    storyboard.Begin();
                }
            }
        }

        private void DetailImageCoordinateField_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Grid grid && DetailImageBorder != null)
            {
                try
                {
                    var position = e.GetCurrentPoint(grid);
                    var width = grid.ActualWidth;
                    var height = grid.ActualHeight;
                    
                    if (width > 0 && height > 0)
                    {
                        var centerX = width / 2;
                        var centerY = height / 2;
                        var offsetX = (position.Position.X - centerX) / centerX;
                        var offsetY = (position.Position.Y - centerY) / centerY;
                        
                        var maxTilt = 10.0;
                        var targetTiltX = -offsetY * maxTilt;
                        var targetTiltY = offsetX * maxTilt;
                        
                        // Get or create projection
                        if (DetailImageBorder.Projection is not Microsoft.UI.Xaml.Media.PlaneProjection projection)
                        {
                            projection = new Microsoft.UI.Xaml.Media.PlaneProjection
                            {
                                CenterOfRotationX = 0.5,
                                CenterOfRotationY = 0.5
                            };
                            DetailImageBorder.Projection = projection;
                        }
                        
                        // Smooth interpolation
                        var lerpFactor = 0.15;
                        projection.RotationX = projection.RotationX + ((targetTiltX - projection.RotationX) * lerpFactor);
                        projection.RotationY = projection.RotationY + ((targetTiltY - projection.RotationY) * lerpFactor);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in DetailImageCoordinateField_PointerMoved", ex);
                }
            }
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

        // Category icon hover effect
        private void CategoryIcon_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.1,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, DetailCategoryIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            storyboard.Children.Add(scaleAnimation);

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.1,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, DetailCategoryIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimationY, "ScaleY");
            storyboard.Children.Add(scaleAnimationY);

            storyboard.Begin();
        }

        private void CategoryIcon_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimation, DetailCategoryIconScale);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleAnimation, "ScaleX");
            storyboard.Children.Add(scaleAnimation);

            var scaleAnimationY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleAnimationY, DetailCategoryIconScale);
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

        private async Task LoadModDetailsFromUrlAsync(string modUrl)
        {
            try
            {
                // Parse GameBanana URL to extract mod ID
                // Example: https://gamebanana.com/mods/574763
                var urlPattern = new System.Text.RegularExpressions.Regex(@"gamebanana\.com/mods/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var match = urlPattern.Match(modUrl);
                
                if (match.Success && int.TryParse(match.Groups[1].Value, out int modId))
                {
                    Logger.LogInfo($"Loading mod details from URL: {modUrl}, ID: {modId}");
                    await ShowModDetailsAsync(modId);
                }
                else
                {
                    Logger.LogWarning($"Could not parse mod ID from URL: {modUrl}");
                    // Fallback to loading mods list
                    await LoadModsAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load mod details from URL: {modUrl}", ex);
                // Fallback to loading mods list and show error
                await LoadModsAsync();
                ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "ConnectionErrorTitle");
                ConnectionErrorBar.Message = SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage");
                ConnectionErrorBar.IsOpen = true;
            }
        }

        private void DetailCategoryLink_Click(object sender, RoutedEventArgs e)
        {
            if (_currentModDetails?.Category != null && !string.IsNullOrEmpty(_currentModDetails.Category.Name))
            {
                try
                {
                    // Set search to category name BEFORE navigating back
                    var categoryName = _currentModDetails.Category.Name;
                    
                    // Remember current mod ID so we can return to it
                    _returnToModId = _currentModDetails.Id;
                    
                    // If opened directly to mod details, we need to show the list first
                    if (_openedDirectlyToModDetails)
                    {
                        _openedDirectlyToModDetails = false; // Reset flag so we don't close the panel
                    }
                    
                    // Navigate back to mods list
                    CloseDetailsPanel();
                    
                    // Set search and reload mods (needs to happen after UI is visible)
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                    {
                        SearchBox.Text = categoryName;
                        _currentSearch = categoryName;
                        _currentPage = 1;
                        _ = LoadModsAsync();
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to filter by category", ex);
                }
            }
        }

        // Tile hover effects - scale only the image
        private void ModTile_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                try
                {
                    // Find the Image element (it's in Grid.Row="0" Grid.RowSpan="2")
                    var image = FindVisualChild<Image>(button);
                    if (image == null) return;
                    
                    // Create scale transform if it doesn't exist
                    if (image.RenderTransform is not ScaleTransform)
                    {
                        image.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                        image.RenderTransform = new ScaleTransform();
                    }
                    
                    var scaleTransform = (ScaleTransform)image.RenderTransform;
                    
                    // Animate scale to 1.10 (10% larger)
                    var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    
                    var scaleXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 1.10,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleXAnim, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
                    storyboard.Children.Add(scaleXAnim);
                    
                    var scaleYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 1.10,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleYAnim, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");
                    storyboard.Children.Add(scaleYAnim);
                    
                    storyboard.Begin();
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
                    var image = FindVisualChild<Image>(button);
                    if (image?.RenderTransform is not ScaleTransform scaleTransform) return;
                    
                    // Animate scale back to 1.0
                    var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    
                    var scaleXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleXAnim, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
                    storyboard.Children.Add(scaleXAnim);
                    
                    var scaleYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
                    };
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleYAnim, scaleTransform);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");
                    storyboard.Children.Add(scaleYAnim);
                    
                    storyboard.Begin();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in ModTile_PointerExited", ex);
                }
            }
        }

        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;
                
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
