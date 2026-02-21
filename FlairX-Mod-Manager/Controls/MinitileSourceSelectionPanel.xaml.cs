using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager.Controls
{
    public sealed partial class MinitileSourceSelectionPanel : UserControl
    {
        private List<string> _availableFiles = new();
        private string? _selectedFilePath;
        private TaskCompletionSource<MinitileSourceResult>? _completionSource;

        public event EventHandler? CloseRequested;

        public MinitileSourceSelectionPanel()
        {
            this.InitializeComponent();
            this.Unloaded += OnUnloaded;
        }

        /// <summary>
        /// Called when panel is unloaded - cancel any pending operation
        /// </summary>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CancelOperation();
        }

        /// <summary>
        /// Cancel the current operation (called when panel is closed without explicit action)
        /// </summary>
        public void CancelOperation()
        {
            // Only set result if not already completed
            // When panel is closed externally (Escape, click outside), treat as Stop
            if (_completionSource != null && !_completionSource.Task.IsCompleted)
            {
                _completionSource.SetResult(new MinitileSourceResult { Stopped = true });
            }
        }

        public Task<MinitileSourceResult> ShowForSelectionAsync(List<string> availableFiles, string modDirectory)
        {
            _availableFiles = availableFiles;
            _completionSource = new TaskCompletionSource<MinitileSourceResult>();

            // Load translations
            var lang = SharedUtilities.LoadLanguageDictionary();
            TitleText.Text = SharedUtilities.GetTranslation(lang, "MinitileSelection_Title") ?? "Select Minitile Source";
            SubtitleText.Text = SharedUtilities.GetTranslation(lang, "MinitileSelection_Subtitle") ?? "Choose which image to use for the thumbnail";
            StopButtonText.Text = SharedUtilities.GetTranslation(lang, "Stop") ?? "Stop";
            SkipButtonText.Text = SharedUtilities.GetTranslation(lang, "Skip") ?? "Skip";
            ConfirmButtonText.Text = SharedUtilities.GetTranslation(lang, "Confirm") ?? "Confirm";
            SelectedInfoText.Text = SharedUtilities.GetTranslation(lang, "MinitileSelection_NoSelection") ?? "No image selected";

            // Load images into grid
            LoadImages();

            return _completionSource.Task;
        }

        private void LoadImages()
        {
            ImageGridView.Items.Clear();

            foreach (var filePath in _availableFiles)
            {
                try
                {
                    var item = CreateImageItem(filePath);
                    ImageGridView.Items.Add(item);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to load image for selection: {filePath}", ex);
                }
            }
        }

        private async Task LoadImageAsync(Image imageControl, string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;

                await Task.Run(() =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        var bitmap = new BitmapImage();
                        // Use UriSource for WebP support via Windows codecs
                        // IMPORTANT: Must use absolute path for WebP to work
                        var absolutePath = Path.GetFullPath(filePath);
                        bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
                        imageControl.Source = bitmap;
                    });
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load image: {filePath}", ex);
            }
        }

        private Grid CreateImageItem(string filePath)
        {
            var grid = new Grid
            {
                Width = 200,
                Height = 240,
                Margin = new Thickness(8),
                Tag = filePath
            };

            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Image border
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(2),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]
            };

            var image = new Image
            {
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4)
            };

            // Load image asynchronously
            _ = LoadImageAsync(image, filePath);

            border.Child = image;
            Grid.SetRow(border, 0);
            grid.Children.Add(border);

            // File name label
            var fileName = Path.GetFileName(filePath);
            var label = new TextBlock
            {
                Text = fileName,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 190,
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 12
            };
            Grid.SetRow(label, 1);
            grid.Children.Add(label);

            return grid;
        }

        private void ImageGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ImageGridView.SelectedItem is Grid selectedGrid && selectedGrid.Tag is string filePath)
            {
                _selectedFilePath = filePath;
                ConfirmButton.IsEnabled = true;

                var lang = SharedUtilities.LoadLanguageDictionary();
                var selectedText = SharedUtilities.GetTranslation(lang, "MinitileSelection_Selected") ?? "Selected: {0}";
                SelectedInfoText.Text = string.Format(selectedText, Path.GetFileName(filePath));

                // Update visual selection
                foreach (var item in ImageGridView.Items)
                {
                    if (item is Grid grid)
                    {
                        var border = grid.Children[0] as Border;
                        if (border != null)
                        {
                            border.BorderBrush = grid == selectedGrid
                                ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                        }
                    }
                }
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // Close panel immediately, then set result
            CloseRequested?.Invoke(this, EventArgs.Empty);
            
            _completionSource?.SetResult(new MinitileSourceResult
            {
                SelectedFilePath = _selectedFilePath,
                Skipped = false,
                Stopped = false
            });
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            // Close panel immediately, then set result
            CloseRequested?.Invoke(this, EventArgs.Empty);
            
            _completionSource?.SetResult(new MinitileSourceResult
            {
                SelectedFilePath = null,
                Skipped = true,
                Stopped = false
            });
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // Close panel immediately, then set result
            CloseRequested?.Invoke(this, EventArgs.Empty);
            
            _completionSource?.SetResult(new MinitileSourceResult
            {
                SelectedFilePath = null,
                Skipped = false,
                Stopped = true
            });
        }
    }
}
