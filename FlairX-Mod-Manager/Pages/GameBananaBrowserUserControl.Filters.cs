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
        private IReadOnlyList<GameBananaCharacterOption> _characterOptions = Array.Empty<GameBananaCharacterOption>();
        private int? _selectedCharacterCategoryId;
        private bool _isUpdatingCharacterSelection;
        private bool _suppressCategoryFilterEvents;
        private ComboBox CharacterFilterComboBox = null!;
        private ComboBox TimeRangeComboBox = null!;
        private ComboBox SecondarySortComboBox = null!;
        private GameBananaUpdatedTimeRange _currentTimeRange = GameBananaUpdatedTimeRange.Last90Days;
        private GameBananaRecentSecondarySort _currentSecondarySort = GameBananaRecentSecondarySort.LatestUpdated;
        private bool _isUpdatingRecentWindowControls;

        private void CreateCharacterFilterComboBox()
        {
            CharacterFilterComboBox = new ComboBox
            {
                MinWidth = 180,
                MaxWidth = 260,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(CharacterFilterComboBox, 1);
            CharacterFilterComboBox.SelectionChanged += CharacterFilterComboBox_SelectionChanged;
            FiltersToolbarGrid.Children.Add(CharacterFilterComboBox);
        }

        private void CreateRecentWindowComboBoxes()
        {
            FiltersToolbarGrid.ColumnDefinitions.Insert(3, new ColumnDefinition { Width = GridLength.Auto });
            FiltersToolbarGrid.ColumnDefinitions.Insert(4, new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(SearchBox, 5);
            Grid.SetColumn(StarterPackButton, 6);

            TimeRangeComboBox = new ComboBox
            {
                MinWidth = 150,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(TimeRangeComboBox, 3);
            TimeRangeComboBox.SelectionChanged += TimeRangeComboBox_SelectionChanged;
            FiltersToolbarGrid.Children.Add(TimeRangeComboBox);

            SecondarySortComboBox = new ComboBox
            {
                MinWidth = 160,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(SecondarySortComboBox, 4);
            SecondarySortComboBox.SelectionChanged += SecondarySortComboBox_SelectionChanged;
            FiltersToolbarGrid.Children.Add(SecondarySortComboBox);
        }

        private string GetTranslationOrDefault(string key, string fallback)
        {
            var value = SharedUtilities.GetTranslation(_lang, key);
            return string.IsNullOrWhiteSpace(value) || value.StartsWith("[MISSING:", StringComparison.Ordinal)
                ? fallback
                : value;
        }

        private int GetActiveCharacterCategoryId()
        {
            var parentCategoryId = GameBananaService.GetCharacterCategoryId(_gameTag);
            return GameBananaCharacterFilter.ResolveCategoryId(
                parentCategoryId,
                _selectedCharacterCategoryId,
                _characterOptions);
        }

        private void ApplyCharacterOptions(
            IReadOnlyList<GameBananaCharacterOption> options,
            int? preferredCategoryId,
            bool enableSelection)
        {
            var parentCategoryId = GameBananaService.GetCharacterCategoryId(_gameTag);
            var resolvedCategoryId = GameBananaCharacterFilter.ResolveCategoryId(
                parentCategoryId,
                preferredCategoryId,
                options);

            _isUpdatingCharacterSelection = true;
            try
            {
                _characterOptions = options;
                CharacterFilterComboBox.Items.Clear();
                foreach (var option in _characterOptions)
                {
                    CharacterFilterComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = option.DisplayName,
                        Tag = option
                    });
                }

                CharacterFilterComboBox.SelectedItem = CharacterFilterComboBox.Items
                    .OfType<ComboBoxItem>()
                    .FirstOrDefault(item =>
                        item.Tag is GameBananaCharacterOption option &&
                        option.CategoryId == resolvedCategoryId);
                CharacterFilterComboBox.IsEnabled = enableSelection && _characterOptions.Count > 1;
                _selectedCharacterCategoryId = resolvedCategoryId;
            }
            finally
            {
                _isUpdatingCharacterSelection = false;
            }
        }

        private async Task LoadCharacterOptionsAsync(int? preferredCategoryId = null)
        {
            var parentCategoryId = GameBananaService.GetCharacterCategoryId(_gameTag);
            var allCharactersLabel = GetTranslationOrDefault("Filter_AllCharacters", "All Characters");
            var fallbackOptions = GameBananaCharacterFilter.BuildOptions(
                Array.Empty<GameBananaService.CategoryRecord>(),
                parentCategoryId,
                allCharactersLabel);

            ApplyCharacterOptions(fallbackOptions, preferredCategoryId ?? parentCategoryId, false);

            try
            {
                var categories = await GameBananaService.GetCharacterCategoriesAsync(_gameTag);
                if (categories is not { Count: > 0 })
                {
                    Logger.LogWarning($"No GameBanana character categories were returned for {_gameTag}; using the parent category.");
                    return;
                }

                var options = GameBananaCharacterFilter.BuildOptions(
                    categories,
                    parentCategoryId,
                    allCharactersLabel);
                ApplyCharacterOptions(options, preferredCategoryId ?? parentCategoryId, true);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load GameBanana character options for {_gameTag}; using the parent category.", ex);
            }
        }

        private void CategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressCategoryFilterEvents)
            {
                return;
            }

            if (sender is ComboBox cb)
            {
                _currentCategoryFilter = cb.SelectedIndex == 1
                    ? CategoryFilter.CharacterSkins
                    : CategoryFilter.AllMods;

                var characterFilterVisibility = _currentCategoryFilter == CategoryFilter.CharacterSkins
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                CharacterFilterComboBox.Visibility = characterFilterVisibility;
                SortOrderComboBox.Visibility = characterFilterVisibility;

                if (_currentCategoryFilter == CategoryFilter.CharacterSkins)
                {
                    _selectedCharacterCategoryId = GameBananaService.GetCharacterCategoryId(_gameTag);
                    _ = LoadCharacterOptionsAsync();
                }
                else
                {
                    _selectedCharacterCategoryId = null;
                }

                _currentSortOrder = GameBananaService.CategorySortOrder.LatestUpdated;
                SortOrderComboBox.SelectedIndex = 0;

                _currentSearch = null;
                SearchBox.Text = "";
                UpdateRecentWindowAvailability();
                _currentPage = 1;
                _ = LoadModsAsync();
            }
        }

        private void CharacterFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingCharacterSelection ||
                _currentCategoryFilter != CategoryFilter.CharacterSkins ||
                sender is not ComboBox { SelectedItem: ComboBoxItem { Tag: GameBananaCharacterOption option } })
            {
                return;
            }

            _selectedCharacterCategoryId = option.CategoryId;
            _currentPage = 1;
            _ = LoadModsAsync();
        }

        private void SortOrderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressCategoryFilterEvents)
            {
                return;
            }

            if (sender is ComboBox cb && _currentCategoryFilter == CategoryFilter.CharacterSkins)
            {
                _currentSortOrder = cb.SelectedIndex switch
                {
                    1 => GameBananaService.CategorySortOrder.Newest,
                    2 => GameBananaService.CategorySortOrder.Oldest,
                    3 => GameBananaService.CategorySortOrder.MostLiked,
                    4 => GameBananaService.CategorySortOrder.MostViewed,
                    5 => GameBananaService.CategorySortOrder.MostDownloaded,
                    6 => GameBananaService.CategorySortOrder.MostCommented,
                    _ => GameBananaService.CategorySortOrder.LatestUpdated,
                };

                UpdateRecentWindowAvailability();
                _currentPage = 1;
                _ = LoadModsAsync();
            }
        }

        private void TimeRangeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingRecentWindowControls || sender is not ComboBox comboBox)
            {
                return;
            }

            _currentTimeRange = comboBox.SelectedIndex switch
            {
                0 => GameBananaUpdatedTimeRange.Last30Days,
                2 => GameBananaUpdatedTimeRange.Last180Days,
                3 => GameBananaUpdatedTimeRange.Unlimited,
                _ => GameBananaUpdatedTimeRange.Last90Days
            };

            var selection = GameBananaRecentSecondarySorter.Normalize(
                _currentCategoryFilter == CategoryFilter.CharacterSkins,
                _currentSortOrder,
                _currentSearch,
                new GameBananaRecentSelection(_currentTimeRange, _currentSecondarySort));
            _currentTimeRange = selection.TimeRange;
            _currentSecondarySort = selection.Sort;

            _currentPage = 1;
            _ = LoadModsAsync();
        }

        private void SecondarySortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingRecentWindowControls || sender is not ComboBox comboBox)
            {
                return;
            }

            _currentSecondarySort = comboBox.SelectedIndex switch
            {
                1 => GameBananaRecentSecondarySort.MostLiked,
                2 => GameBananaRecentSecondarySort.MostDownloaded,
                3 => GameBananaRecentSecondarySort.MostCommented,
                _ => GameBananaRecentSecondarySort.LatestUpdated
            };

            var selection = GameBananaRecentSecondarySorter.Normalize(
                _currentCategoryFilter == CategoryFilter.CharacterSkins,
                _currentSortOrder,
                _currentSearch,
                new GameBananaRecentSelection(_currentTimeRange, _currentSecondarySort));
            _currentTimeRange = selection.TimeRange;
            _currentSecondarySort = selection.Sort;

            _currentPage = 1;
            _ = LoadModsAsync();
        }

        private void UpdateRecentWindowAvailability(bool resetIfIneligible = true)
        {
            var isEligible = GameBananaRecentSecondarySorter.IsEligible(
                _currentCategoryFilter == CategoryFilter.CharacterSkins,
                _currentSortOrder,
                _currentSearch);

            if (resetIfIneligible && !isEligible)
            {
                var selection = GameBananaRecentSelection.Default;
                _currentTimeRange = selection.TimeRange;
                _currentSecondarySort = selection.Sort;
                _isUpdatingRecentWindowControls = true;
                try
                {
                    TimeRangeComboBox.SelectedIndex = 1;
                    SecondarySortComboBox.SelectedIndex = 0;
                }
                finally
                {
                    _isUpdatingRecentWindowControls = false;
                }
            }

            TimeRangeComboBox.Visibility = isEligible ? Visibility.Visible : Visibility.Collapsed;
            SecondarySortComboBox.Visibility = isEligible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
