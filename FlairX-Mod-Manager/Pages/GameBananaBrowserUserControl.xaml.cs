using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
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
        private string _currentSort = "date_added";
        private string? _currentFeedType = null;
        private System.Collections.Generic.List<string>? _includeSections = null;
        private System.Collections.Generic.List<string>? _excludeSections = null;
        private System.Collections.Generic.List<string>? _includeTags = null;
        private System.Collections.Generic.List<string>? _excludeTags = null;
        private ObservableCollection<ModViewModel> _mods = new();
        private System.Collections.Generic.Dictionary<string, string> _lang = new();
        private System.Collections.Generic.List<string>? _availableSections = null;
        private GameBananaService.ModDetailsResponse? _currentModDetails;
        private ObservableCollection<Models.GameBananaFileViewModel> _detailFiles = new();
        
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
            
            public string DownloadCountFormatted => FormatCount(DownloadCount);
            public string LikeCountFormatted => FormatCount(LikeCount);
            public string ViewCountFormatted => FormatCount(ViewCount);
            public string DateAddedFormatted => FormatDate(DateAdded);
            public string DateModifiedFormatted => FormatDate(DateModified);

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
                var now = DateTime.Now;
                var diff = now - date;
                
                if (diff.TotalDays < 1)
                    return "Today";
                if (diff.TotalDays < 2)
                    return "Yesterday";
                if (diff.TotalDays < 7)
                    return $"{(int)diff.TotalDays}d ago";
                if (diff.TotalDays < 30)
                    return $"{(int)(diff.TotalDays / 7)}w ago";
                if (diff.TotalDays < 365)
                    return $"{(int)(diff.TotalDays / 30)}mo ago";
                return $"{(int)(diff.TotalDays / 365)}y ago";
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
            IncludeSectionsLabel.Text = SharedUtilities.GetTranslation(_lang, "IncludeSections");
            ExcludeSectionsLabel.Text = SharedUtilities.GetTranslation(_lang, "ExcludeSections");
            IncludeTagsLabel.Text = SharedUtilities.GetTranslation(_lang, "IncludeTags");
            ExcludeTagsLabel.Text = SharedUtilities.GetTranslation(_lang, "ExcludeTags");
            PageLabel.Text = SharedUtilities.GetTranslation(_lang, "Page");
            FiltersExpanderHeader.Text = SharedUtilities.GetTranslation(_lang, "AdvancedFilters");
            
            ModsGridView.ItemsSource = _mods;
            
            // Load sections
            _ = LoadSectionsAsync();
            
            // Load mods
            _ = LoadModsAsync();
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

                var response = await GameBananaService.GetModsAsync(
                    _gameTag, 
                    _currentPage, 
                    _currentSearch, 
                    _currentSort,
                    _currentFeedType,
                    _includeSections,
                    _excludeSections,
                    _includeTags,
                    _excludeTags);

                if (response?.Records == null || response.Records.Count == 0)
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    EmptyPanel.Visibility = Visibility.Visible;
                    EmptyText.Text = string.IsNullOrEmpty(_currentSearch) 
                        ? SharedUtilities.GetTranslation(_lang, "NoModsFound")
                        : SharedUtilities.GetTranslation(_lang, "NoModsMatchSearch");
                    PrevPageButton.IsEnabled = _currentPage > 1;
                    NextPageButton.IsEnabled = false;
                    return;
                }

                _mods.Clear();
                foreach (var record in response.Records)
                {
                    var viewModel = new ModViewModel
                    {
                        Id = record.Id,
                        Name = record.Name,
                        AuthorName = record.Submitter?.Name ?? "Unknown",
                        ProfileUrl = record.ProfileUrl,
                        LikeCount = record.GetLikeCount(),
                        ViewCount = record.GetViewCount(),
                        DateAdded = record.DateAdded,
                        DateModified = record.DateModified
                    };

                    // Get preview image
                    var image = record.PreviewMedia?.Images?.FirstOrDefault();
                    if (image != null)
                    {
                        viewModel.ImageUrl = $"{image.BaseUrl}/{image.File220 ?? image.File100 ?? image.File}";
                    }

                    _mods.Add(viewModel);
                }

                // Load images asynchronously
                _ = LoadImagesAsync();

                LoadingPanel.Visibility = Visibility.Collapsed;
                ModsGridView.Visibility = Visibility.Visible;

                // Update pagination
                PageNumberTextBox.Text = _currentPage.ToString();
                PrevPageButton.IsEnabled = _currentPage > 1;
                NextPageButton.IsEnabled = response.Records.Count >= response.PerPage;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load mods from GameBanana", ex);
                LoadingPanel.Visibility = Visibility.Collapsed;
                EmptyPanel.Visibility = Visibility.Visible;
                EmptyText.Text = SharedUtilities.GetTranslation(_lang, "FailedToLoadMods");
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

        private void SortComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            // Set ComboBox item text from language file
            SortMostRecentItem.Content = SharedUtilities.GetTranslation(_lang, "SortMostRecent");
            SortLastUpdatedItem.Content = SharedUtilities.GetTranslation(_lang, "SortLastUpdated");
            SortMostDownloadedItem.Content = SharedUtilities.GetTranslation(_lang, "SortMostDownloaded");
            SortMostLikedItem.Content = SharedUtilities.GetTranslation(_lang, "SortMostLiked");
            SortMostViewedItem.Content = SharedUtilities.GetTranslation(_lang, "SortMostViewed");
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SortComboBox.SelectedItem is ComboBoxItem item && item.Tag is string sort)
            {
                _currentSort = sort;
                _currentPage = 1;
                _ = LoadModsAsync();
            }
        }

        private void RefreshButton_Loaded(object sender, RoutedEventArgs e)
        {
            ToolTipService.SetToolTip(RefreshButton, SharedUtilities.GetTranslation(_lang, "RefreshTooltip"));
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
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
                DetailPreviewPanel.Visibility = Visibility.Collapsed;
                DetailDescription.Visibility = Visibility.Collapsed;
                DetailDescriptionTitle.Visibility = Visibility.Collapsed;
                DetailFilesTitle.Visibility = Visibility.Collapsed;
                DetailFilesList.Visibility = Visibility.Collapsed;
                DetailDownloadButton.Visibility = Visibility.Collapsed;
                DetailOpenBrowserButton.Visibility = Visibility.Collapsed;
                
                // Animate transition to details
                AnimateContentSwitch(ModsListGrid, DetailsPanel);
                _currentState = NavigationState.ModDetails;

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
                DetailDescription.Text = string.IsNullOrWhiteSpace(_currentModDetails.Description) 
                    ? "No description available." 
                    : _currentModDetails.Description;

                // Load preview images
                DetailPreviewImages.Children.Clear();
                if (_currentModDetails.PreviewMedia?.Images != null && _currentModDetails.PreviewMedia.Images.Count > 0)
                {
                    DetailPreviewPanel.Visibility = Visibility.Visible;
                    foreach (var image in _currentModDetails.PreviewMedia.Images.Take(5))
                    {
                        var imageUrl = $"{image.BaseUrl}/{image.File530 ?? image.File}";
                        var img = new Image
                        {
                            Width = 400,
                            Height = 300,
                            Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
                            Source = new BitmapImage(new Uri(imageUrl))
                        };
                        DetailPreviewImages.Children.Add(img);
                    }
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
                            DownloadCount = file.DownloadCount
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

                // Show content
                DetailLoadingPanel.Visibility = Visibility.Collapsed;
                DetailAuthor.Visibility = Visibility.Visible;
                DetailDescription.Visibility = Visibility.Visible;
                DetailDescriptionTitle.Visibility = Visibility.Visible;
                DetailFilesTitle.Visibility = Visibility.Visible;
                DetailFilesList.Visibility = Visibility.Visible;
                DetailDownloadButton.Visibility = Visibility.Visible;
                DetailOpenBrowserButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load mod details", ex);
                CloseDetailsPanel();
            }
        }

        private void CloseDetailsPanel()
        {
            // Animate transition back to list
            AnimateContentSwitch(DetailsPanel, ModsListGrid);
            _currentModDetails = null;
            _detailFiles.Clear();
            _currentState = NavigationState.ModsList;
            
            // Restore title
            var gameName = GetGameName(_gameTag);
            var titleFormat = SharedUtilities.GetTranslation(_lang, "BrowseTitle");
            TitleText.Text = string.Format(titleFormat, gameName);
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

        private async Task LoadSectionsAsync()
        {
            try
            {
                _availableSections = await GameBananaService.GetGameSectionsAsync(_gameTag);
                
                if (_availableSections != null && _availableSections.Count > 0)
                {
                    IncludeSectionsComboBox.Items.Clear();
                    ExcludeSectionsComboBox.Items.Clear();
                    
                    IncludeSectionsComboBox.Items.Add(new ComboBoxItem { Content = "None", Tag = null });
                    ExcludeSectionsComboBox.Items.Add(new ComboBoxItem { Content = "None", Tag = null });
                    
                    foreach (var section in _availableSections)
                    {
                        IncludeSectionsComboBox.Items.Add(new ComboBoxItem { Content = section, Tag = section });
                        ExcludeSectionsComboBox.Items.Add(new ComboBoxItem { Content = section, Tag = section });
                    }
                    
                    IncludeSectionsComboBox.SelectedIndex = 0;
                    ExcludeSectionsComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load sections", ex);
            }
        }

        private void FeedTypeComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            FeedAllItem.Content = SharedUtilities.GetTranslation(_lang, "FeedAll");
            FeedRipeItem.Content = SharedUtilities.GetTranslation(_lang, "FeedRipe");
            FeedNewItem.Content = SharedUtilities.GetTranslation(_lang, "FeedNew");
            FeedUpdatedItem.Content = SharedUtilities.GetTranslation(_lang, "FeedUpdated");
        }

        private void FeedTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FeedTypeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string feedType)
            {
                _currentFeedType = string.IsNullOrEmpty(feedType) ? null : feedType;
                _currentPage = 1;
                _ = LoadModsAsync();
            }
        }

        private void FiltersButton_Loaded(object sender, RoutedEventArgs e)
        {
            ToolTipService.SetToolTip(FiltersButton, SharedUtilities.GetTranslation(_lang, "AdvancedFilters"));
        }

        private void FiltersButton_Click(object sender, RoutedEventArgs e)
        {
            FiltersExpander.IsExpanded = !FiltersExpander.IsExpanded;
        }

        private void IncludeSectionsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IncludeSectionsComboBox.SelectedItem is ComboBoxItem item)
            {
                _includeSections = item.Tag as string != null ? new System.Collections.Generic.List<string> { (string)item.Tag } : null;
                _currentPage = 1;
                _ = LoadModsAsync();
            }
        }

        private void ExcludeSectionsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExcludeSectionsComboBox.SelectedItem is ComboBoxItem item)
            {
                _excludeSections = item.Tag as string != null ? new System.Collections.Generic.List<string> { (string)item.Tag } : null;
                _currentPage = 1;
                _ = LoadModsAsync();
            }
        }

        private System.Threading.CancellationTokenSource? _tagsDebounceToken;

        private async void TagsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Debounce - wait 500ms after user stops typing
            _tagsDebounceToken?.Cancel();
            _tagsDebounceToken = new System.Threading.CancellationTokenSource();
            
            try
            {
                await Task.Delay(500, _tagsDebounceToken.Token);
                
                // Parse tags
                var includeText = IncludeTagsTextBox.Text?.Trim();
                var excludeText = ExcludeTagsTextBox.Text?.Trim();
                
                _includeTags = string.IsNullOrEmpty(includeText) 
                    ? null 
                    : includeText.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList() as System.Collections.Generic.List<string>;
                    
                _excludeTags = string.IsNullOrEmpty(excludeText) 
                    ? null 
                    : excludeText.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList() as System.Collections.Generic.List<string>;
                
                _currentPage = 1;
                await LoadModsAsync();
            }
            catch (TaskCanceledException)
            {
                // Debounce cancelled, ignore
            }
        }

        private void PageNumberTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (int.TryParse(PageNumberTextBox.Text, out var page) && page > 0)
                {
                    _currentPage = page;
                    _ = LoadModsAsync();
                }
                else
                {
                    PageNumberTextBox.Text = _currentPage.ToString();
                }
            }
        }
    }
}
