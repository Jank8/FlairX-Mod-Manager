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

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // If search box is cleared, reset to main page
            if (string.IsNullOrWhiteSpace(sender.Text) && !string.IsNullOrEmpty(_currentSearch))
            {
                // Push current search state before clearing
                if (_currentState == NavigationState.ModsList)
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
                    UpdateBackButtonIcon();
                }
                _currentSearch = null;
                UpdateRecentWindowAvailability();
                _currentPage = 1;
                _ = LoadModsAsync();
            }
        }

        private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            try
            {
                var queryText = args.QueryText?.Trim();

                // Check if the query is for author search
                if (!string.IsNullOrEmpty(queryText) && IsAuthorSearch(queryText))
                {
                    // Clear the search box and search for author
                    sender.Text = "";
                    await SearchAuthorModsAsync(queryText);
                    return;
                }

                // Check if the query looks like a GameBanana URL
                if (!string.IsNullOrEmpty(queryText) && IsGameBananaUrl(queryText))
                {
                    // Clear the search box and open the URL
                    sender.Text = "";
                    await OpenGameBananaUrlAsync(queryText);
                    return;
                }

                // Regular search
                var newSearch = string.IsNullOrWhiteSpace(queryText) ? null : queryText;
                // Push current ModsList state only if search is actually changing
                if (_currentState == NavigationState.ModsList && newSearch != _currentSearch)
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
                    UpdateBackButtonIcon();
                }
                _currentSearch = newSearch;
                UpdateRecentWindowAvailability();
                _currentPage = 1;
                _ = LoadModsAsync();
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameBananaBrowserUserControl.SearchBox_QuerySubmitted failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameBananaBrowserUserControl.SearchBox_QuerySubmitted failed", ex);
            }
        }

        private async Task PerformSearchAsync(string searchText)
        {
            var newSearch = string.IsNullOrWhiteSpace(searchText) ? null : searchText;
            // Push current ModsList state only if search is actually changing
            if (_currentState == NavigationState.ModsList && newSearch != _currentSearch)
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
                UpdateBackButtonIcon();
            }
            _currentSearch = newSearch;
            UpdateRecentWindowAvailability();
            _currentPage = 1;
            await LoadModsAsync();
        }

        private bool IsAuthorSearch(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            
            // Check for user: prefix only
            return text.StartsWith("user:", StringComparison.OrdinalIgnoreCase);
        }

        private async Task SearchAuthorModsAsync(string query)
        {
            try
            {
                // Extract username from query (remove user: prefix)
                if (!query.StartsWith("user:", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                
                string username = query.Substring(5).Trim();
                
                if (string.IsNullOrWhiteSpace(username))
                {
                    ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "Error");
                    ConnectionErrorBar.Message = "Please provide a username after 'user:'";
                    ConnectionErrorBar.IsOpen = true;
                    return;
                }
                
                Logger.LogInfo($"Searching for mods by user: {username}");
                
                // Get author ID
                var authorId = await GameBananaService.GetAuthorIdByUsernameAsync(username);
                
                if (authorId == null)
                {
                    ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "Error");
                    ConnectionErrorBar.Message = $"User '{username}' not found. Username must be exact (case-sensitive).";
                    ConnectionErrorBar.IsOpen = true;
                    return;
                }
                
                // Set up author search and load mods
                _currentAuthorId = authorId;
                _currentAuthorName = username;
                
                // Navigate to author mods view
                await ShowAuthorModsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to search for author mods: {query}", ex);
                ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "ConnectionErrorTitle");
                ConnectionErrorBar.Message = SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage");
                ConnectionErrorBar.IsOpen = true;
            }
        }

        private bool IsGameBananaUrl(string text)
        {
            try
            {
                // Check if it looks like a GameBanana URL
                if (text.Contains("gamebanana.com") && text.Contains("/mods/"))
                {
                    return true;
                }
                
                // Check if it's just a mod ID (numbers only)
                if (text.All(char.IsDigit) && text.Length >= 4)
                {
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task OpenGameBananaUrlAsync(string input)
        {
            try
            {
                string modUrl;
                
                // If it's just a mod ID (numbers only), construct the full URL
                if (input.All(char.IsDigit) && input.Length >= 4)
                {
                    modUrl = $"https://gamebanana.com/mods/{input}";
                }
                // If it's a partial URL without https, add it
                else if (input.StartsWith("gamebanana.com"))
                {
                    modUrl = $"https://{input}";
                }
                // Otherwise use as-is
                else
                {
                    modUrl = input;
                }
                
                Logger.LogInfo($"Opening GameBanana URL: {modUrl}");
                await LoadModDetailsFromUrlAsync(modUrl);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to open GameBanana URL: {input}", ex);
                ConnectionErrorBar.Title = SharedUtilities.GetTranslation(_lang, "ConnectionErrorTitle");
                ConnectionErrorBar.Message = SharedUtilities.GetTranslation(_lang, "ConnectionErrorMessage");
                ConnectionErrorBar.IsOpen = true;
            }
        }
    }
}
