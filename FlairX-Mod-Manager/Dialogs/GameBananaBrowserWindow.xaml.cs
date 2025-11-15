using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager.Dialogs
{
    public sealed partial class GameBananaBrowserWindow : Window
    {
        private string _gameTag = "";
        private int _currentPage = 1;
        private string? _currentSearch = null;
        private string _currentSort = "date_added";
        private ObservableCollection<ModViewModel> _mods = new();

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
            
            public string DownloadCountFormatted => FormatCount(DownloadCount);
            public string LikeCountFormatted => FormatCount(LikeCount);
            public string ViewCountFormatted => FormatCount(ViewCount);

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

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public GameBananaBrowserWindow(string gameTag)
        {
            InitializeComponent();
            _gameTag = gameTag;
            
            // Set window size
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
            }
            
            // Set window title based on game
            var gameName = GetGameName(gameTag);
            TitleText.Text = $"Browse {gameName} Mods - GameBanana";
            Title = $"Browse {gameName} Mods";

            ModsGridView.ItemsSource = _mods;
            
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

                var response = await GameBananaService.GetModsAsync(_gameTag, _currentPage, _currentSearch, _currentSort);

                if (response?.Records == null || response.Records.Count == 0)
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    EmptyPanel.Visibility = Visibility.Visible;
                    EmptyText.Text = string.IsNullOrEmpty(_currentSearch) ? "No mods found" : "No mods match your search";
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
                        DownloadCount = record.DownloadCount,
                        LikeCount = record.LikeCount,
                        ViewCount = record.ViewCount
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
                PageText.Text = $"Page {_currentPage}";
                PrevPageButton.IsEnabled = _currentPage > 1;
                NextPageButton.IsEnabled = response.Records.Count >= response.PerPage;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load mods from GameBanana", ex);
                LoadingPanel.Visibility = Visibility.Collapsed;
                EmptyPanel.Visibility = Visibility.Visible;
                EmptyText.Text = "Failed to load mods. Please try again.";
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
                
                await Task.Delay(10); // Small delay to avoid overwhelming the UI thread
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Only search when user types (not when programmatically changed)
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                // Wait for user to finish typing (debounce)
            }
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            _currentSearch = string.IsNullOrWhiteSpace(args.QueryText) ? null : args.QueryText;
            _currentPage = 1;
            _ = LoadModsAsync();
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
                // Open mod details window
                var detailsWindow = new GameBananaModDetailsWindow(mod.Id, _gameTag);
                detailsWindow.Activate();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
