using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager.Dialogs
{
    public sealed partial class GameBananaModDetailsWindow : Window
    {
        private int _modId;
        private string _gameTag;
        private GameBananaService.ModDetailsResponse? _modDetails;
        private ObservableCollection<FileViewModel> _files = new();

        public class FileViewModel : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string FileName { get; set; } = "";
            public long FileSize { get; set; }
            public string? Description { get; set; }
            public string DownloadUrl { get; set; } = "";
            public int DownloadCount { get; set; }

            public string FileSizeFormatted => FormatFileSize(FileSize);
            public string DownloadCountFormatted => FormatCount(DownloadCount);
            public Visibility HasDescription => string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;

            private static string FormatFileSize(long bytes)
            {
                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = bytes;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }

            private static string FormatCount(int count)
            {
                if (count >= 1000000)
                    return $"{count / 1000000.0:F1}M";
                if (count >= 1000)
                    return $"{count / 1000.0:F1}K";
                return count.ToString();
            }

            #pragma warning disable CS0067
            public event PropertyChangedEventHandler? PropertyChanged;
            #pragma warning restore CS0067
        }

        public GameBananaModDetailsWindow(int modId, string gameTag)
        {
            InitializeComponent();
            _modId = modId;
            _gameTag = gameTag;

            // Set window size
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(900, 700));
            }

            FilesListView.ItemsSource = _files;

            _ = LoadModDetailsAsync();
        }

        private async Task LoadModDetailsAsync()
        {
            try
            {
                _modDetails = await GameBananaService.GetModDetailsAsync(_modId);

                if (_modDetails == null)
                {
                    await ShowErrorDialog("Failed to load mod details. Please try again.");
                    Close();
                    return;
                }

                // Update UI
                ModNameText.Text = _modDetails.Name;
                AuthorText.Text = _modDetails.Submitter?.Name ?? "Unknown";
                DescriptionText.Text = string.IsNullOrWhiteSpace(_modDetails.Description) 
                    ? "No description available." 
                    : _modDetails.Description;

                // Load preview images
                if (_modDetails.PreviewMedia?.Images != null && _modDetails.PreviewMedia.Images.Count > 0)
                {
                    PreviewGrid.Visibility = Visibility.Visible;
                    foreach (var image in _modDetails.PreviewMedia.Images.Take(5))
                    {
                        var imageUrl = $"{image.BaseUrl}/{image.File530 ?? image.File}";
                        var img = new Image
                        {
                            Width = 400,
                            Height = 300,
                            Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
                            Source = new BitmapImage(new Uri(imageUrl))
                        };
                        PreviewImagesPanel.Children.Add(img);
                    }
                }

                // Load files
                if (_modDetails.Files != null && _modDetails.Files.Count > 0)
                {
                    foreach (var file in _modDetails.Files)
                    {
                        _files.Add(new FileViewModel
                        {
                            Id = file.Id,
                            FileName = file.FileName,
                            FileSize = file.FileSize,
                            Description = file.Description,
                            DownloadUrl = file.DownloadUrl,
                            DownloadCount = file.DownloadCount
                        });
                    }

                    // Auto-select first file
                    if (_files.Count > 0)
                    {
                        FilesListView.SelectedIndex = 0;
                    }
                }
                else
                {
                    DownloadButton.IsEnabled = false;
                    DownloadButton.Content = "No files available";
                }

                LoadingPanel.Visibility = Visibility.Collapsed;
                ContentGrid.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load mod details", ex);
                await ShowErrorDialog("Failed to load mod details. Please try again.");
                Close();
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFiles = FilesListView.SelectedItems.Cast<FileViewModel>().ToList();

            if (selectedFiles.Count == 0)
            {
                await ShowErrorDialog("Please select at least one file to download.");
                return;
            }

            // Show file extraction dialog
            var extractDialog = new GameBananaFileExtractionDialog(selectedFiles, _modDetails!.Name, _gameTag, _modDetails.ProfileUrl);
            extractDialog.XamlRoot = Content.XamlRoot;
            var result = await extractDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                Close();
            }
        }

        private async void OpenInBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            if (_modDetails != null && !string.IsNullOrEmpty(_modDetails.ProfileUrl))
            {
                await Launcher.LaunchUriAsync(new Uri(_modDetails.ProfileUrl));
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async Task ShowErrorDialog(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
