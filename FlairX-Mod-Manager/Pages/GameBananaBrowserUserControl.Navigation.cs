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
        
        private enum NavigationState
        {
            ModsList,
            ModDetails,
            AuthorMods
        }

        // Navigation history stack (browser-like)
        private record NavigationEntry(
            NavigationState State,
            int? ModId = null,
            string? Search = null,
            int? AuthorId = null,
            string? AuthorName = null,
            double ScrollOffset = 0,
            int Page = 1,
            CategoryFilter Filter = CategoryFilter.AllMods,
            GameBananaService.CategorySortOrder SortOrder = GameBananaService.CategorySortOrder.LatestUpdated,
            int? CharacterCategoryId = null,
            GameBananaUpdatedTimeRange TimeRange = GameBananaUpdatedTimeRange.Last90Days,
            GameBananaRecentSecondarySort SecondarySort = GameBananaRecentSecondarySort.LatestUpdated
        );

        private readonly Stack<NavigationEntry> _navigationStack = new();
        private NavigationState _currentState = NavigationState.ModsList;
        private bool _suppressNavigationPush = false;
        private bool _isNavigating = false; // Prevents race condition on rapid back clicks

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _ = NavigateBackAsync();
        }

        private async Task NavigateBackAsync()
        {
            if (_isNavigating) return;

            if (_navigationStack.Count > 0)
            {
                var previous = _navigationStack.Pop();
                _isNavigating = true; // Set synchronously before async call to prevent race condition
                await RestoreNavigationEntryAsync(previous);
            }
            else
            {
                CloseRequested?.Invoke(this, new BrowserClosedEventArgs { ModWasInstalled = _modWasInstalled });
            }
        }

        private async Task RestoreNavigationEntryAsync(NavigationEntry entry)
        {
            _isNavigating = true;
            try
            {
                switch (entry.State)
                {
                    case NavigationState.ModsList:
                        if (_currentState == NavigationState.AuthorMods)
                        {
                            // AuthorMods uses ModsListGrid - already visible, just reset state
                            _authorMods.Clear();
                            _currentAuthorId = null;
                            _currentAuthorName = null;
                            _authorCurrentPage = 1;
                            _authorHasMorePages = true;
                            _authorIsLoadingMore = false;
                            LoadMoreAuthorModsButton.Visibility = Visibility.Collapsed;
                            LoadMoreAuthorModsButton.IsEnabled = true;
                            LoadMoreProgressBar.Visibility = Visibility.Collapsed;
                            ModsGridView.ItemsSource = _mods;
                            LoadMoreMainModsButton.Visibility = _hasMorePages ? Visibility.Visible : Visibility.Collapsed;
                            _currentState = NavigationState.ModsList;
                        }
                        else if (_currentState == NavigationState.ModDetails)
                        {
                            CloseDetailsPanelNoHistory();
                        }
                        // else already on ModsList - nothing to close

                        // Reload if search, page, filter or sort changed — or if list is empty
                        if (entry.Search != _currentSearch || entry.Page != _currentPage || 
                            entry.Filter != _currentCategoryFilter || entry.SortOrder != _currentSortOrder ||
                            entry.CharacterCategoryId != _selectedCharacterCategoryId ||
                            entry.TimeRange != _currentTimeRange ||
                            entry.SecondarySort != _currentSecondarySort ||
                            _mods.Count == 0)
                        {
                            _currentSearch = entry.Search;
                            _currentPage = entry.Page;
                            SearchBox.Text = entry.Search ?? "";

                            // Restore filter without triggering SelectionChanged reload
                            _currentCategoryFilter = entry.Filter;
                            CategoryFilterComboBox.SelectionChanged -= CategoryFilterComboBox_SelectionChanged;
                            CategoryFilterComboBox.SelectedIndex = entry.Filter == CategoryFilter.CharacterSkins ? 1 : 0;
                            var characterFilterVisibility = entry.Filter == CategoryFilter.CharacterSkins
                                ? Visibility.Visible : Visibility.Collapsed;
                            CharacterFilterComboBox.Visibility = characterFilterVisibility;
                            SortOrderComboBox.Visibility = characterFilterVisibility;
                            CategoryFilterComboBox.SelectionChanged += CategoryFilterComboBox_SelectionChanged;

                            // Restore sort order without triggering SelectionChanged reload
                            _currentSortOrder = entry.SortOrder;
                            SortOrderComboBox.SelectionChanged -= SortOrderComboBox_SelectionChanged;
                            SortOrderComboBox.SelectedIndex = entry.SortOrder switch
                            {
                                GameBananaService.CategorySortOrder.Newest => 1,
                                GameBananaService.CategorySortOrder.Oldest => 2,
                                GameBananaService.CategorySortOrder.MostLiked => 3,
                                GameBananaService.CategorySortOrder.MostViewed => 4,
                                GameBananaService.CategorySortOrder.MostDownloaded => 5,
                                GameBananaService.CategorySortOrder.MostCommented => 6,
                                _ => 0
                            };
                            SortOrderComboBox.SelectionChanged += SortOrderComboBox_SelectionChanged;

                            var recentSelection = GameBananaRecentSecondarySorter.Normalize(
                                entry.Filter == CategoryFilter.CharacterSkins,
                                entry.SortOrder,
                                entry.Search,
                                new GameBananaRecentSelection(entry.TimeRange, entry.SecondarySort));
                            _currentTimeRange = recentSelection.TimeRange;
                            _currentSecondarySort = recentSelection.Sort;
                            _isUpdatingRecentWindowControls = true;
                            try
                            {
                                TimeRangeComboBox.SelectedIndex = _currentTimeRange switch
                                {
                                    GameBananaUpdatedTimeRange.Last30Days => 0,
                                    GameBananaUpdatedTimeRange.Last180Days => 2,
                                    GameBananaUpdatedTimeRange.Unlimited => 3,
                                    _ => 1
                                };
                                SecondarySortComboBox.SelectedIndex = _currentSecondarySort switch
                                {
                                    GameBananaRecentSecondarySort.MostLiked => 1,
                                    GameBananaRecentSecondarySort.MostDownloaded => 2,
                                    GameBananaRecentSecondarySort.MostCommented => 3,
                                    _ => 0
                                };
                            }
                            finally
                            {
                                _isUpdatingRecentWindowControls = false;
                            }
                            UpdateRecentWindowAvailability(resetIfIneligible: false);

                            if (entry.Filter == CategoryFilter.CharacterSkins)
                            {
                                await LoadCharacterOptionsAsync(entry.CharacterCategoryId);
                            }
                            else
                            {
                                _selectedCharacterCategoryId = null;
                            }

                            await LoadModsAsync();
                        }
                        _currentState = NavigationState.ModsList;
                        TitleText.Text = string.Format(SharedUtilities.GetTranslation(_lang, "BrowseTitle"), GetGameName(_gameTag));

                        // Restore scroll position after layout
                        if (entry.ScrollOffset > 0 && _modsScrollViewer != null)
                        {
                            await Task.Delay(50);
                            _modsScrollViewer.ScrollToVerticalOffset(entry.ScrollOffset);
                        }
                        break;

                    case NavigationState.ModDetails:
                        if (entry.ModId.HasValue)
                        {
                            if (_currentState == NavigationState.AuthorMods)
                            {
                                CloseAuthorModsPanelNoHistory();
                            }
                            else if (_currentState == NavigationState.ModsList)
                            {
                                AnimateContentSwitch(ModsListGrid, DetailsPanel);
                                _currentState = NavigationState.ModDetails;
                            }
                            // If _currentState == ModDetails already, DetailsPanel is visible - no animation needed

                            if (_currentModDetails?.Id != entry.ModId.Value)
                            {
                                await ShowModDetailsAsyncNoHistory(entry.ModId.Value);
                            }
                            else
                            {
                                DetailsScrollViewer.ScrollToVerticalOffset(0);
                            }
                        }
                        break;

                    case NavigationState.AuthorMods:
                        if (entry.AuthorId.HasValue)
                        {
                            _currentAuthorId = entry.AuthorId;
                            _currentAuthorName = entry.AuthorName;

                            // If we already have data for this author in memory, don't reload from API
                            bool alreadyLoaded = _authorMods.Count > 0 && _currentAuthorId == entry.AuthorId;

                            if (alreadyLoaded)
                            {
                                // Just restore the UI state without API call
                                _suppressNavigationPush = true;
                                try
                                {
                                    var authorModsTitle = SharedUtilities.GetTranslation(_lang, "AuthorModsTitle");
                                    TitleText.Text = $"{_currentAuthorName} - {authorModsTitle}";
                                    UpdateBackButtonIcon();

                                    if (_currentState == NavigationState.ModDetails)
                                        AnimateContentSwitch(DetailsPanel, ModsListGrid);

                                    _currentState = NavigationState.AuthorMods;
                                    ModsGridView.ItemsSource = _authorMods;
                                    LoadMoreMainModsButton.Visibility = Visibility.Collapsed;
                                    LoadMoreAuthorModsButton.Visibility = Visibility.Visible;
                                }
                                finally { _suppressNavigationPush = false; }

                                // Restore scroll position
                                if (entry.ScrollOffset > 0 && _modsScrollViewer != null)
                                {
                                    await Task.Delay(50);
                                    _modsScrollViewer.ScrollToVerticalOffset(entry.ScrollOffset);
                                }

                                // Refresh installation status for all author mods
                                foreach (var mod in _authorMods)
                                    mod.IsInstalled = IsModInstalled(mod.ProfileUrl);

                                Logger.LogInfo($"Restored author mods from cache for {_currentAuthorName}, scroll: {entry.ScrollOffset}");
                            }
                            else
                            {
                                await ShowAuthorModsAsyncNoHistory();
                            }
                        }
                        break;
                }
            }
            finally
            {
                _isNavigating = false;
                UpdateBackButtonIcon();
            }
        }

        private void UpdateBackButtonIcon()
        {
            BackIcon.Glyph = _navigationStack.Count > 0 ? "\uE72B" : "\uE711"; // Left arrow : Close (X)
        }

        private void CloseDetailsPanelNoHistory()
        {
            DetailInstalledBadge.Visibility = Visibility.Collapsed;
            DetailVersionBadge.Visibility = Visibility.Collapsed;
            AnimateContentSwitch(DetailsPanel, ModsListGrid);
            _currentModDetails = null;
            _detailFiles.Clear();
            _currentState = NavigationState.ModsList;
            _imageCache.Clear();
            // Reset comments
            _commentsLoaded = false;
            _commentsHasMore = false;
            _commentsLoadingMore = false;
            _comments.Clear();
            CommentsSection.Visibility = Visibility.Collapsed;
            ShowCommentsButton.Visibility = Visibility.Collapsed;
            // Close WebView2 used for comments
            try { _commentsWebView?.Close(); } catch { }
            _commentsWebView = null;
            var gameName = GetGameName(_gameTag);
            var titleFormat = SharedUtilities.GetTranslation(_lang, "BrowseTitle");
            TitleText.Text = string.Format(titleFormat, gameName);
            if (_isAttachedToSizeChanged)
            {
                DetailDescriptionScrollViewer.SizeChanged -= DetailDescriptionScrollViewer_SizeChanged;
                _isAttachedToSizeChanged = false;
            }
            DetailDescriptionMarkdown.LayoutUpdated -= DetailDescriptionMarkdown_LayoutUpdated;
        }

        private void CloseAuthorModsPanelNoHistory()
        {
            AnimateContentSwitch(ModsListGrid, DetailsPanel);
            _currentState = NavigationState.ModDetails;
            ModsGridView.ItemsSource = _mods;
            LoadMoreAuthorModsButton.Visibility = Visibility.Collapsed;
            LoadMoreAuthorModsButton.IsEnabled = true;
            LoadMoreMainModsButton.Visibility = Visibility.Collapsed;
            LoadMoreProgressBar.Visibility = Visibility.Collapsed;
            _authorMods.Clear();
            _currentAuthorId = null;
            _currentAuthorName = null;
            _authorCurrentPage = 1;
            _authorHasMorePages = true;
            _authorIsLoadingMore = false;
            if (_currentModDetails != null)
                TitleText.Text = _currentModDetails.Name;
        }

        private async Task ShowModDetailsAsyncNoHistory(int modId)
        {
            _suppressNavigationPush = true;
            try { await ShowModDetailsAsync(modId); }
            finally { _suppressNavigationPush = false; }
        }

        private async Task ShowAuthorModsAsyncNoHistory()
        {
            _suppressNavigationPush = true;
            try { await ShowAuthorModsAsync(); }
            finally { _suppressNavigationPush = false; }
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

        private void CloseDetailsPanel()
        {
            _navigationStack.Clear();
            CloseDetailsPanelNoHistory();
            UpdateBackButtonIcon();
        }

        private void CloseAuthorModsPanel()
        {
            _navigationStack.Clear();
            CloseAuthorModsPanelNoHistory();
            UpdateBackButtonIcon();
        }

        private void CloseAuthorModsToMainPanel()
        {
            _navigationStack.Clear();
            // No animation needed since we're already in ModsListGrid view, just update state
            _currentState = NavigationState.ModsList;
            ModsGridView.ItemsSource = _mods;
            LoadMoreAuthorModsButton.Visibility = Visibility.Collapsed;
            LoadMoreAuthorModsButton.IsEnabled = true;
            LoadMoreMainModsButton.Visibility = _hasMorePages ? Visibility.Visible : Visibility.Collapsed;
            LoadMoreProgressBar.Visibility = Visibility.Collapsed;
            _authorMods.Clear();
            _currentAuthorId = null;
            _currentAuthorName = null;
            _authorCurrentPage = 1;
            _authorHasMorePages = true;
            _authorIsLoadingMore = false;
            var gameName = GetGameName(_gameTag);
            var titleFormat = SharedUtilities.GetTranslation(_lang, "BrowseTitle");
            TitleText.Text = string.Format(titleFormat, gameName);
            UpdateBackButtonIcon();
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
    }
}
