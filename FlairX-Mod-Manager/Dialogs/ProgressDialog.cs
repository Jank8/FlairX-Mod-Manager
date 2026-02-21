using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;

namespace FlairX_Mod_Manager.Dialogs
{
    public class ProgressDialog : ContentDialog
    {
        private ProgressBar _progressBar;
        private TextBlock _statusText;
        private TextBlock _detailText;

        public ProgressDialog(string title, string message)
        {
            Title = title;
            
            var stackPanel = new StackPanel { Spacing = 12, MinWidth = 400 };

            _statusText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stackPanel.Children.Add(_statusText);

            _progressBar = new ProgressBar
            {
                IsIndeterminate = false,
                Value = 0,
                Minimum = 0,
                Maximum = 100,
                Height = 4
            };
            stackPanel.Children.Add(_progressBar);

            _detailText = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Opacity = 0.7,
                Margin = new Thickness(0, 4, 0, 0)
            };
            stackPanel.Children.Add(_detailText);

            Content = stackPanel;
            IsPrimaryButtonEnabled = false;
            IsSecondaryButtonEnabled = false;
            DefaultButton = ContentDialogButton.None;
        }

        public void UpdateProgress(int current, int total, string detail = "")
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _progressBar.Value = total > 0 ? (double)current / total * 100 : 0;
                _detailText.Text = $"{current}/{total} - {detail}";
            });
        }

        public void UpdateStatus(string status)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _statusText.Text = status;
            });
        }
    }
}
