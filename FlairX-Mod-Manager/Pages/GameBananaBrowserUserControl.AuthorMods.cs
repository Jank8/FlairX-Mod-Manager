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
        
        // Author mods functionality
        private int? _currentAuthorId = null;
        private string? _currentAuthorName = null;
        private ObservableCollection<ModViewModel> _authorMods = new();
        private int _authorCurrentPage = 1;
        private bool _authorHasMorePages = true;
        private bool _authorIsLoadingMore = false;

        private async Task LoadAuthorModsAsync(bool loadMore = false)
        {
            if (_currentAuthorId == null) return;
            
            try
            {
                if (!loadMore)
                {
                    _authorCurrentPage = 1;
                    _authorHasMorePages = true;
                    _authorIsLoadingMore = false;
                    
                    // Re-enable the button for new search
                    LoadMoreAuthorModsButton.IsEnabled = true;
                }
                
                if (_authorIsLoadingMore || !_authorHasMorePages) return;
                _authorIsLoadingMore = true;
                
                if (!loadMore)
                {
                    LoadingPanel.Visibility = Visibility.Visible;
                    EmptyPanel.Visibility = Visibility.Collapsed;
                    ModsGridView.Visibility = Visibility.Collapsed;
                }
                
                ConnectionErrorBar.IsOpen = false;

                if (!loadMore)
                {
                    _authorMods.Clear();
                }
                
                // Get current page of author's mods
                var url = $"https://api.gamebanana.com/Core/List/New?page={_authorCurrentPage}&userid={_currentAuthorId}&itemtype=Mod";
                Logger.LogInfo($"Fetching author mods page {_authorCurrentPage}: {url}");
                
                var response = await GameBananaService.MakeApiCallAsync<List<List<object>>>(url);
                
                if (response == null || response.Count == 0)
                {
                    Logger.LogInfo($"API returned 0 results for page {_authorCurrentPage}, no more pages available");
                    _authorHasMorePages = false;
                    _authorIsLoadingMore = false;
                    
                    // Disable the button instead of hiding it
                    LoadMoreAuthorModsButton.IsEnabled = false;
                    
                    if (!loadMore && _authorMods.Count == 0)
                    {
                        LoadingPanel.Visibility = Visibility.Collapsed;
                        EmptyPanel.Visibility = Visibility.Visible;
                        EmptyText.Text = SharedUtilities.GetTranslation(_lang, "NoModsFound");
                    }
                    return;
                }
                
                // Extract mod IDs from response
                var modIds = new List<int>();
                foreach (var item in response)
                {
                    if (item.Count >= 2 && item[0].ToString() == "Mod" && int.TryParse(item[1].ToString(), out int modId))
                    {
                        modIds.Add(modId);
                    }
                }
                
                Logger.LogInfo($"Found {modIds.Count} mod IDs on page {_authorCurrentPage}");
                
                if (modIds.Count == 0)
                {
                    _authorHasMorePages = false;
                    _authorIsLoadingMore = false;
                    return;
                }
                
                // Fetch details for each mod in parallel batches
                const int batchSize = 10;
                var modViewModels = new List<ModViewModel>();
                var currentGameId = GameBananaService.GetGameId(_gameTag);
                var installedText = SharedUtilities.GetTranslation(_lang, "Installed");
                
                Logger.LogInfo($"Filtering mods for game ID {currentGameId} ({_gameTag})");
                
                for (int i = 0; i < modIds.Count; i += batchSize)
                {
                    var batch = modIds.Skip(i).Take(batchSize);
                    var batchTasks = batch.Select(async modId =>
                    {
                        try
                        {
                            var modDetails = await GameBananaService.GetModDetailsAsync(modId);
                            if (modDetails != null)
                            {
                                Logger.LogInfo($"Mod {modId} ({modDetails.Name}) belongs to game {modDetails.Game?.Id} ({modDetails.Game?.Name}), current game: {currentGameId} ({_gameTag})");
                                
                                // Filter by game ID - only show mods for current game
                                if (modDetails.Game?.Id != currentGameId)
                                {
                                    Logger.LogDebug($"Skipping mod {modId} - belongs to game {modDetails.Game?.Id} ({modDetails.Game?.Name}), not {currentGameId} ({_gameTag})");
                                    return null;
                                }
                                
                                var viewModel = new ModViewModel
                                {
                                    Id = modDetails.Id,
                                    Name = modDetails.Name,
                                    AuthorName = modDetails.Submitter?.Name ?? "Unknown",
                                    AuthorProfileUrl = modDetails.Submitter?.ProfileUrl ?? "",
                                    AuthorAvatarUrl = modDetails.Submitter?.AvatarUrl,
                                    ProfileUrl = modDetails.ProfileUrl,
                                    CategoryName = modDetails.Category?.Name ?? "",
                                    CategoryUrl = modDetails.Category?.ProfileUrl ?? "",
                                    LikeCount = modDetails.GetLikeCount(),
                                    ViewCount = modDetails.GetViewCount(),
                                    DownloadCount = modDetails.GetDownloadCount(),
                                    DateAdded = modDetails.DateAdded ?? 0,
                                    DateUpdated = modDetails.DateUpdated ?? 0,
                                    IsRated = false, // Not available in details API, will be set to false
                                    IsNSFW = false, // Not available in details API
                                    IsInstalled = IsModInstalled(modDetails.ProfileUrl),
                                    InstalledText = installedText
                                };
                                
                                // Set preview image
                                if (modDetails.PreviewMedia?.Images?.Any() == true)
                                {
                                    var image = modDetails.PreviewMedia.Images.First();
                                    viewModel.ImageUrl = GameBananaService.GetListPreviewImageUrl(image);
                                }
                                
                                return viewModel;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Error fetching details for mod {modId}", ex);
                        }
                        return null;
                    });
                    
                    var batchResults = await Task.WhenAll(batchTasks);
                    var validResults = batchResults.Where(vm => vm != null).Cast<ModViewModel>();
                    modViewModels.AddRange(validResults);
                    
                    // Add results to UI as we get them for better UX
                    foreach (var viewModel in validResults)
                    {
                        _authorMods.Add(viewModel);
                    }
                    
                    Logger.LogInfo($"Processed batch {i / batchSize + 1}/{(modIds.Count + batchSize - 1) / batchSize}, loaded {validResults.Count()} mods for {_gameTag}");
                }
                
                Logger.LogInfo($"Successfully loaded {modViewModels.Count} {_gameTag} mods for author {_currentAuthorName} page {_authorCurrentPage}");
                Logger.LogInfo($"Total author mods in collection: {_authorMods.Count}");
                Logger.LogInfo($"API returned {response.Count} total items on this page");
                
                // Always keep trying - never assume we've reached the end
                Logger.LogInfo($"Button will remain visible for manual loading");
                
                // Increment page for next load
                _authorCurrentPage++;
                Logger.LogInfo($"Next page will be: {_authorCurrentPage}");
                
                if (!loadMore)
                {
                    // Check if we have any mods after filtering
                    if (_authorMods.Count == 0)
                    {
                        // If no mods found on first page, try loading next page automatically
                        Logger.LogInfo($"No mods found for {_gameTag} on page {_authorCurrentPage - 1}, trying next page automatically...");
                        _authorIsLoadingMore = false;
                        await LoadAuthorModsAsync(loadMore: true);
                        return;
                    }
                    
                    // Load images asynchronously
                    _ = LoadAuthorModImagesAsync();

                    LoadingPanel.Visibility = Visibility.Collapsed;
                    ModsGridView.Visibility = Visibility.Visible;
                    
                    // Always show load more button
                    Logger.LogInfo($"Setting LoadMoreButton visibility to: Visible (first load with {_authorMods.Count} mods)");
                    LoadMoreAuthorModsButton.Visibility = Visibility.Visible;
                    LoadMoreMainModsButton.Visibility = Visibility.Collapsed;
                    
                    // Check if we need to load more content to fill the screen
                    _ = CheckIfNeedMoreAuthorContent();
                }
                else
                {
                    // When loading more, check if we got any mods for this game
                    var modsLoadedThisPage = modViewModels.Count;
                    Logger.LogInfo($"Loaded {modsLoadedThisPage} mods for {_gameTag} on page {_authorCurrentPage - 1}");
                    
                    // If loading more pages and no mods found, try next page automatically
                    if (modsLoadedThisPage == 0)
                    {
                        Logger.LogInfo($"No mods found for {_gameTag} on page {_authorCurrentPage - 1}, trying next page automatically...");
                        _authorIsLoadingMore = false;
                        await LoadAuthorModsAsync(loadMore: true);
                        return;
                    }
                    
                    // Load images for newly added mods
                    Logger.LogInfo($"Loading images for {modsLoadedThisPage} newly added mods");
                    _ = LoadAuthorModImagesForNewModsAsync(modViewModels);
                    
                    // Always keep button visible
                    Logger.LogInfo($"Keeping LoadMoreButton visible (loaded {modsLoadedThisPage} mods this page)");
                    LoadMoreAuthorModsButton.Visibility = Visibility.Visible;
                    LoadMoreMainModsButton.Visibility = Visibility.Collapsed;
                    
                    // Check if we need to load more content to fill the screen
                    _ = CheckIfNeedMoreAuthorContent();
                }
                
                _authorIsLoadingMore = false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load author mods for {_currentAuthorName}", ex);
                _authorIsLoadingMore = false;
                
                if (!loadMore)
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "ConnectionErrorTitle");
                    ConnectionErrorBar.Message = SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage");
                    ConnectionErrorBar.IsOpen = true;
                }
            }
        }
        
        private async Task CheckIfNeedMoreAuthorContent()
        {
            // Wait for layout to update
            await Task.Delay(200);
            
            // If ScrollViewer doesn't have enough content to scroll, load more
            if (_modsScrollViewer != null && _authorHasMorePages && !_authorIsLoadingMore)
            {
                var scrollableHeight = _modsScrollViewer.ScrollableHeight;
                
                // If there's no scrollable content (everything fits on screen), load more
                if (scrollableHeight <= 0)
                {
                    Logger.LogInfo("No scrollable content for author mods, loading more automatically");
                    
                    // Show progress bar
                    LoadMoreAuthorModsButton.Visibility = Visibility.Collapsed;
                    LoadMoreProgressBar.Visibility = Visibility.Visible;
                    
                    await LoadAuthorModsAsync(loadMore: true);
                    
                    // Hide progress bar and show button
                    LoadMoreProgressBar.Visibility = Visibility.Collapsed;
                    LoadMoreAuthorModsButton.Visibility = Visibility.Visible;
                }
            }
        }

        private async Task LoadAuthorModImagesAsync()
        {
            var mods = _authorMods.Where(m => !string.IsNullOrEmpty(m.ImageUrl)).ToList();
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
                            Logger.LogDebug("GameBananaBrowserUserControl.LoadAuthorModImagesAsync failed: canceled");
                        }
                        catch (System.Exception ex)
                        {
                            Logger.LogError("GameBananaBrowserUserControl.LoadAuthorModImagesAsync failed", ex);
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
        
        private async Task LoadAuthorModImagesForNewModsAsync(List<ModViewModel> newMods)
        {
            foreach (var mod in newMods.Where(m => !string.IsNullOrEmpty(m.ImageUrl)))
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
                            Logger.LogDebug("GameBananaBrowserUserControl.LoadAuthorModImagesForNewModsAsync failed: canceled");
                        }
                        catch (System.Exception ex)
                        {
                            Logger.LogError("GameBananaBrowserUserControl.LoadAuthorModImagesForNewModsAsync failed", ex);
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
        
        private async void LoadMoreAuthorModsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show progress bar and hide button while loading
                LoadMoreAuthorModsButton.Visibility = Visibility.Collapsed;
                LoadMoreProgressBar.Visibility = Visibility.Visible;

                await LoadAuthorModsAsync(loadMore: true);

                // Hide progress bar and show button again
                LoadMoreProgressBar.Visibility = Visibility.Collapsed;
                LoadMoreAuthorModsButton.Visibility = Visibility.Visible;
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.LoadMoreAuthorModsButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.LoadMoreAuthorModsButton_Click failed", ex);
            }
        }
        
        private async Task ShowAuthorModsAsync()
        {
            try
            {
                // Push current state to navigation stack before navigating (unless suppressed)
                if (!_suppressNavigationPush)
                {
                    if (_currentState == NavigationState.ModDetails && _currentModDetails != null)
                    {
                        _navigationStack.Push(new NavigationEntry(
                            NavigationState.ModDetails,
                            ModId: _currentModDetails.Id));
                    }
                    else if (_currentState == NavigationState.ModsList)
                    {
                        _navigationStack.Push(new NavigationEntry(
                            NavigationState.ModsList,
                            Search: _currentSearch,
                            ScrollOffset: _modsScrollViewer?.VerticalOffset ?? 0,
                            Page: _currentPage,
                            Filter: _currentCategoryFilter,
                            SortOrder: _currentSortOrder,
                            CharacterCategoryId: _selectedCharacterCategoryId,
                            TimeRange: _currentTimeRange,
                            SecondarySort: _currentSecondarySort));
                    }
                    else if (_currentState == NavigationState.AuthorMods && _currentAuthorId.HasValue)
                    {
                        _navigationStack.Push(new NavigationEntry(
                            NavigationState.AuthorMods,
                            AuthorId: _currentAuthorId,
                            AuthorName: _currentAuthorName,
                            ScrollOffset: _modsScrollViewer?.VerticalOffset ?? 0));
                    }
                }

                // Update title to show author name
                var authorModsTitle = SharedUtilities.GetTranslation(_lang, "AuthorModsTitle");
                var newTitle = $"{_currentAuthorName} - {authorModsTitle}";
                Logger.LogInfo($"Setting author mods title to: {newTitle}");
                TitleText.Text = newTitle;
                
                // Update back button icon
                UpdateBackButtonIcon();
                
                // Animate transition to author mods only if coming from details panel
                if (_currentState == NavigationState.ModDetails)
                {
                    AnimateContentSwitch(DetailsPanel, ModsListGrid);
                }
                // If coming from ModsList, ModsListGrid is already visible - no animation needed
                _currentState = NavigationState.AuthorMods;
                
                // Bind author mods to the grid
                ModsGridView.ItemsSource = _authorMods;
                
                // Hide main view button, show author button
                LoadMoreMainModsButton.Visibility = Visibility.Collapsed;
                
                // Load author mods
                await LoadAuthorModsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show author mods for {_currentAuthorName}", ex);
                ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "ConnectionErrorTitle");
                ConnectionErrorBar.Message = SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage");
                ConnectionErrorBar.IsOpen = true;
            }
        }
    }
}
