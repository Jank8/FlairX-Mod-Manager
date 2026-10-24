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

        private string _gameTag = "";
        private int _currentPage = 1;
        private int _loadGeneration;
        private string? _currentSearch = null;
        private CategoryFilter _currentCategoryFilter = CategoryFilter.AllMods;
        private GameBananaService.CategorySortOrder _currentSortOrder = GameBananaService.CategorySortOrder.LatestUpdated;
        private ObservableCollection<ModViewModel> _mods = new();
        private HashSet<int> _loadedModIds = new(); // Track loaded mod IDs to prevent duplicates
        
        // Infinite scroll
        private bool _isLoadingMore = false;
        private bool _hasMorePages = true;
        private ScrollViewer? _modsScrollViewer = null;
        private DateTime _lastScrollTime = DateTime.MinValue;

        internal static ModViewModel CreateModViewModel(
            GameBananaService.ModRecord record,
            string installedText,
            bool isInstalled)
        {
            var image = record.PreviewMedia?.Images?.FirstOrDefault();
            return new ModViewModel
            {
                Id = record.Id,
                Name = record.Name ?? "",
                AuthorName = record.Submitter?.Name ?? "Unknown",
                ProfileUrl = record.ProfileUrl ?? "",
                ImageUrl = image is null ? null : GameBananaService.GetListPreviewImageUrl(image),
                DownloadCount = record.GetDownloadCount(),
                LikeCount = record.GetLikeCount(),
                CommentCount = record.GetPostCount(),
                ViewCount = record.GetViewCount(),
                DateAdded = record.DateAdded,
                DateModified = record.DateModified,
                DateUpdated = record.DateUpdated,
                IsRated = record.HasAnyContentWarning,
                IsInstalled = isInstalled,
                InstalledText = installedText
            };
        }

        private bool ShouldIncludeMod(GameBananaService.ModRecord record)
        {
            if (record.HasAnyContentWarning && SettingsManager.Current.HideNSFWMods)
            {
                return false;
            }

            return !IsModBlacklisted(record.Submitter?.Name ?? "", record.Name ?? "");
        }

        private void GameBananaBrowserUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= GameBananaBrowserUserControl_Loaded;
            Logger.LogInfo($"[GBBrowser] Loaded event fired, _pendingModUrl={((_pendingModUrl != null) ? _pendingModUrl : "null")}");
            if (_pendingModUrl != null)
            {
                var url = _pendingModUrl;
                _pendingModUrl = null;
                Logger.LogInfo($"[GBBrowser] Firing LoadModDetailsFromUrlAsync for: {url}");
                _ = LoadModDetailsFromUrlAsync(url);
            }
        }

        private async void ModsScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            try
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
                await LoadMoreModsIfNeeded();
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.ModsScrollViewer_ViewChanged failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.ModsScrollViewer_ViewChanged failed", ex);
            }
        }

        private async Task LoadMoreModsIfNeeded()
        {
            if (_modsScrollViewer == null)
            {
                Logger.LogWarning("ScrollViewer is null");
                return;
            }
            
            // Check if we're in author mods view
            if (_currentState == NavigationState.AuthorMods)
            {
                if (_authorIsLoadingMore)
                {
                    Logger.LogInfo("Already loading more author mods");
                    return;
                }
                
                if (!_authorHasMorePages)
                {
                    Logger.LogInfo("No more author pages available");
                    return;
                }
                
                // Check if we're near the bottom
                var authorScrollableHeight = _modsScrollViewer.ScrollableHeight;
                var authorCurrentVerticalOffset = _modsScrollViewer.VerticalOffset;
                var authorViewportHeight = _modsScrollViewer.ViewportHeight;
                
                // Load more when we're within 2 viewport heights of the bottom
                var authorLoadMoreThreshold = authorScrollableHeight - (authorViewportHeight * 2);
                
                Logger.LogInfo($"Checking author load threshold: {authorCurrentVerticalOffset} >= {authorLoadMoreThreshold} (scrollable: {authorScrollableHeight}, viewport: {authorViewportHeight})");
                
                if (authorCurrentVerticalOffset >= authorLoadMoreThreshold)
                {
                    Logger.LogInfo("Loading more author mods via scroll...");
                    
                    // Show progress bar and hide button while loading
                    LoadMoreAuthorModsButton.Visibility = Visibility.Collapsed;
                    LoadMoreProgressBar.Visibility = Visibility.Visible;
                    
                    await LoadAuthorModsAsync(loadMore: true);
                    
                    // Hide progress bar and show button again
                    LoadMoreProgressBar.Visibility = Visibility.Collapsed;
                    LoadMoreAuthorModsButton.Visibility = Visibility.Visible;
                }
                return;
            }
            
            // Original logic for regular mods
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

        public void SetInitialSearch(string searchQuery)
        {
            if (string.IsNullOrWhiteSpace(searchQuery)) return;
            
            // Set search box text
            SearchBox.Text = searchQuery;
            
            // Trigger search
            _currentSearch = searchQuery;
            UpdateRecentWindowAvailability();
            _currentPage = 1;
            _ = LoadModsAsync();
        }

        private Task<GameBananaService.ModListResponse?> FetchModsPageAsync(int page)
        {
            if (_currentCategoryFilter == CategoryFilter.CharacterSkins)
            {
                return GameBananaService.GetModsByCategoryAsync(
                    _gameTag,
                    GetActiveCharacterCategoryId(),
                    page,
                    _currentSearch,
                    _currentSortOrder);
            }

            return GameBananaService.GetModsAsync(
                _gameTag, page, _currentSearch, null, null, null, null, null, null);
        }

        private bool IsRecentWindowEligible() =>
            GameBananaRecentSecondarySorter.IsEligible(
                _currentCategoryFilter == CategoryFilter.CharacterSkins,
                _currentSortOrder,
                _currentSearch);

        private async Task LoadModsAsync()
        {
            var loadGeneration = Interlocked.Increment(ref _loadGeneration);

            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                EmptyPanel.Visibility = Visibility.Collapsed;
                ModsGridView.Visibility = Visibility.Collapsed;
                ConnectionErrorBar.Severity = InfoBarSeverity.Error;
                ConnectionErrorBar.IsOpen = false;

                // Reset loading UI
                LoadingProgressBar.IsIndeterminate = true;
                LoadingProgressBar.Value = 0;
                LoadingText.Text = GetTranslationOrDefault("Loading", "Loading...");

                _mods.Clear();
                _loadedModIds.Clear();

                var windowEligible = IsRecentWindowEligible();
                var selection = GameBananaRecentSecondarySorter.Normalize(
                    _currentCategoryFilter == CategoryFilter.CharacterSkins,
                    _currentSortOrder,
                    _currentSearch,
                    new GameBananaRecentSelection(_currentTimeRange, _currentSecondarySort));
                var firstPage = windowEligible ? 1 : _currentPage;
                var response = await FetchModsPageAsync(firstPage);
                if (loadGeneration != _loadGeneration) return;

                if (response == null)
                {
                    Logger.LogInfo("Response is null, attempting Cloudflare bypass...");
                    var (cookies, _) = await Services.CloudflareBypassService.BypassCloudflareAsync();
                    if (loadGeneration != _loadGeneration) return;

                    if (!string.IsNullOrEmpty(cookies))
                    {
                        response = await FetchModsPageAsync(firstPage);
                        if (loadGeneration != _loadGeneration) return;
                    }

                    if (response == null)
                    {
                        LoadingPanel.Visibility = Visibility.Collapsed;
                        ConnectionErrorBar.Severity = InfoBarSeverity.Error;
                        ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "ConnectionErrorTitle");
                        ConnectionErrorBar.Message = SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage");
                        ConnectionErrorBar.IsOpen = true;
                        _hasMorePages = false;
                        return;
                    }
                }

                var firstPageRecords = response.Records ?? [];
                IReadOnlyList<GameBananaService.ModRecord> records;
                var partialWindow = false;
                var cappedWindow = false;

                if (windowEligible)
                {
                    // Reset progress bar to determinate mode and 0%
                    LoadingProgressBar.IsIndeterminate = false;
                    LoadingProgressBar.Value = 0;

                    var collector = new GameBananaRecentWindowCollector(
                        selection.TimeRange,
                        DateTimeOffset.UtcNow,
                        (currentPage, totalPages) =>
                        {
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                LoadingProgressBar.Value = (currentPage * 100.0) / totalPages;
                                var loadingText = GetTranslationOrDefault("Loading", "Loading...");
                                LoadingText.Text = $"{loadingText} ({currentPage}/{totalPages})";
                            });
                        });
                    var sourceComplete = response.Metadata?.IsComplete ?? firstPageRecords.Count < 50;
                    var decision = collector.AddPage(firstPageRecords, sourceComplete);

                    while (decision.ShouldContinue)
                    {
                        var nextPage = collector.PagesProcessed + 1;
                        var nextResponse = await FetchModsPageAsync(nextPage);
                        if (loadGeneration != _loadGeneration) return;

                        if (nextResponse is null)
                        {
                            partialWindow = true;
                            Logger.LogWarning(
                                $"GameBanana recent window page {nextPage} failed after " +
                                $"{collector.PagesProcessed} successful page(s).");
                            break;
                        }

                        var nextRecords = nextResponse.Records ?? [];
                        sourceComplete = nextResponse.Metadata?.IsComplete ?? nextRecords.Count < 50;
                        decision = collector.AddPage(nextRecords, sourceComplete);
                    }

                    records = GameBananaRecentSecondarySorter.FilterAndOrder(
                        collector.Records,
                        ShouldIncludeMod,
                        selection.Sort);
                    cappedWindow = collector.StopReason is
                        GameBananaRecentStopReason.PageLimit or
                        GameBananaRecentStopReason.RecordLimit;
                    Logger.LogInfo(
                        $"GameBanana recent window stopped with {collector.StopReason}: " +
                        $"pages={collector.PagesProcessed}, raw={collector.RawRecordsProcessed}, " +
                        $"uniqueInRange={collector.Records.Count}, displayed={records.Count}.");
                    _hasMorePages = false;
                }
                else
                {
                    records = firstPageRecords;
                    _hasMorePages = response.Metadata != null
                        ? !response.Metadata.IsComplete
                        : firstPageRecords.Count >= 50;
                }

                if (partialWindow)
                {
                    ConnectionErrorBar.Severity = InfoBarSeverity.Warning;
                    ConnectionErrorBar.Title = GetTranslationOrDefault(
                        "RecentWindow_PartialTitle",
                        "Partial results");
                    ConnectionErrorBar.Message = GetTranslationOrDefault(
                        "RecentWindow_PartialWarning",
                        "A later GameBanana page could not be loaded; the available results are shown.");
                    ConnectionErrorBar.IsOpen = true;
                }
                else if (cappedWindow)
                {
                    ConnectionErrorBar.Severity = InfoBarSeverity.Warning;
                    ConnectionErrorBar.Title = GetTranslationOrDefault(
                        "RecentWindow_CappedTitle",
                        "Results limited");
                    ConnectionErrorBar.Message = GetTranslationOrDefault(
                        "RecentWindow_CappedWarning",
                        "Results reached the safety limit of 10 pages or 500 records.");
                    ConnectionErrorBar.IsOpen = true;
                }

                if (records.Count == 0)
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    EmptyPanel.Visibility = Visibility.Visible;
                    EmptyText.Text = string.IsNullOrEmpty(_currentSearch)
                        ? SharedUtilities.GetTranslation(_lang, "NoModsFound")
                        : SharedUtilities.GetTranslation(_lang, "NoModsMatchSearch");
                    _hasMorePages = false;
                    return;
                }

                var installedText = SharedUtilities.GetTranslation(_lang, "Installed");
                foreach (var record in records)
                {
                    if (_loadedModIds.Contains(record.Id) ||
                        (!windowEligible && !ShouldIncludeMod(record)))
                    {
                        continue;
                    }

                    var viewModel = CreateModViewModel(
                        record,
                        installedText,
                        IsModInstalled(record.ProfileUrl ?? ""));
                    _mods.Add(viewModel);
                    _loadedModIds.Add(record.Id);
                }

                if (!windowEligible && _mods.Count < 20 && _hasMorePages)
                {
                    Logger.LogInfo($"Only {_mods.Count} mods loaded after NSFW filtering, auto-loading more pages...");
                    await AutoLoadMorePagesAsync(loadGeneration);
                    if (loadGeneration != _loadGeneration) return;
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

                _ = LoadImagesAsync();
                LoadingPanel.Visibility = Visibility.Collapsed;
                ModsGridView.Visibility = Visibility.Visible;
                LoadMoreMainModsButton.Visibility = !windowEligible && _hasMorePages
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                LoadMoreAuthorModsButton.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                if (loadGeneration != _loadGeneration) return;

                Logger.LogError("Failed to load mods from GameBanana", ex);
                LoadingPanel.Visibility = Visibility.Collapsed;
                ConnectionErrorBar.Severity = InfoBarSeverity.Error;
                ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "ConnectionErrorTitle");
                ConnectionErrorBar.Message = SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage");
                ConnectionErrorBar.IsOpen = true;
                LoadMoreMainModsButton.Visibility = Visibility.Collapsed;
                LoadMoreAuthorModsButton.Visibility = Visibility.Collapsed;
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

        private async Task AutoLoadMorePagesAsync(int loadGeneration)
        {
            // Auto-load up to 5 more pages if we have too few mods (handles heavy NSFW filtering)
            int pagesLoaded = 0;
            while (loadGeneration == _loadGeneration &&
                   _mods.Count < 20 &&
                   _hasMorePages &&
                   pagesLoaded < 5)
            {
                await LoadMoreModsAsync(loadGeneration);
                if (loadGeneration != _loadGeneration) return;
                pagesLoaded++;
                await Task.Delay(100); // Small delay between requests
            }
            Logger.LogInfo($"AutoLoadMorePages finished: {_mods.Count} mods after {pagesLoaded} extra pages");
        }

        private async Task LoadMoreModsAsync(int? expectedLoadGeneration = null)
        {
            // Capture the generation even when the caller did not pass one (scroll and auto-load
            // paths). Those calls previously skipped the staleness check entirely, so a page
            // requested before a new search could append its results to the newer result set.
            var generation = expectedLoadGeneration ?? _loadGeneration;

            if (generation != _loadGeneration ||
                _isLoadingMore ||
                IsRecentWindowEligible() ||
                !_hasMorePages)
            {
                return;
            }
            
            _isLoadingMore = true;
            
            try
            {
                _currentPage++;
                
                var response = await FetchModsPageAsync(_currentPage);
                if (generation != _loadGeneration) return;

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
                var newViewModels = new List<ModViewModel>();

                foreach (var record in response.Records)
                {
                    // Skip duplicates (important for infinite scroll and auto-load)
                    if (_loadedModIds.Contains(record.Id))
                        continue;

                    if (!ShouldIncludeMod(record))
                        continue;

                    var viewModel = CreateModViewModel(
                        record,
                        installedText,
                        isInstalled: false);

                    _mods.Add(viewModel);
                    _loadedModIds.Add(record.Id);
                    newViewModels.Add(viewModel);
                }

                // Check installed status in background to avoid blocking UI during load-more
                _ = Task.Run(async () =>
                {
                    foreach (var vm in newViewModels)
                    {
                        if (string.IsNullOrEmpty(vm.ProfileUrl)) continue;
                        var installed = !string.IsNullOrEmpty(await GetInstalledModPathAsync(vm.ProfileUrl));
                        if (installed)
                            DispatcherQueue.TryEnqueue(() => vm.IsInstalled = true);
                    }
                });

                // Load images for new mods
                _ = LoadImagesAsync();
                
                // Update Load More button visibility
                LoadMoreMainModsButton.Visibility = !IsRecentWindowEligible() && _hasMorePages
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                LoadMoreAuthorModsButton.Visibility = Visibility.Collapsed;
                
                // Check if we still need more content after loading
                await Task.Delay(200); // Wait for layout to update
                if (expectedLoadGeneration.HasValue && expectedLoadGeneration.Value != _loadGeneration) return;
                CheckIfNeedMoreContent();
            }
            catch (Exception ex)
            {
                if (expectedLoadGeneration.HasValue && expectedLoadGeneration.Value != _loadGeneration) return;

                Logger.LogError("Failed to load more mods from GameBanana", ex);
                _hasMorePages = false;
                LoadMoreMainModsButton.Visibility = Visibility.Collapsed;
                LoadMoreAuthorModsButton.Visibility = Visibility.Collapsed;
            }
            finally
            {
                _isLoadingMore = false;
            }
        }

        private void CheckIfNeedMoreContent()
        {
            // If ScrollViewer doesn't have enough content to scroll, load more
            if (_modsScrollViewer != null &&
                !IsRecentWindowEligible() &&
                _hasMorePages &&
                !_isLoadingMore)
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
            // Snapshot first: a new page can append to _mods while this loop is running,
            // which threw "Collection was modified" and aborted the whole image pass.
            var mods = _mods.Where(m => !string.IsNullOrEmpty(m.ImageUrl)).ToList();
            foreach (var mod in mods)
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.ImageOpened += async (s, e) =>
                    {
                        try
                        {
                            // Fade in animation
                            for (double opacity = 0; opacity <= 1; opacity += 0.1)
                            {
                                mod.ImageOpacity = opacity;
                                await Task.Delay(20);
                            }
                            mod.ImageOpacity = 1;
                        }
                        catch (System.OperationCanceledException)
                        {
                            Logger.LogDebug("GameBananaBrowserUserControl.LoadImagesAsync failed: canceled");
                        }
                        catch (System.Exception ex)
                        {
                            Logger.LogError("GameBananaBrowserUserControl.LoadImagesAsync failed", ex);
                        }
                    };
                    bitmap.UriSource = new Uri(mod.ImageUrl!);
                    mod.ImageSource = bitmap;
                }
                catch
                {
                    // Image loading failed, skip
                }
                
                await Task.Delay(10);
            }
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
        
        private async void LoadMoreMainModsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show progress bar and hide button while loading
                LoadMoreMainModsButton.Visibility = Visibility.Collapsed;
                LoadMoreProgressBar.Visibility = Visibility.Visible;

                await LoadMoreModsAsync();

                // Hide progress bar and show button again
                LoadMoreProgressBar.Visibility = Visibility.Collapsed;
                LoadMoreMainModsButton.Visibility = _hasMorePages ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.LoadMoreMainModsButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.LoadMoreMainModsButton_Click failed", ex);
            }
        }
    }
}
