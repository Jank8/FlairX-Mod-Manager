using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Dialogs
{
    public class ManagerUpdateDialog : ContentDialog
    {
        private ProgressBar _progressBar;
        private TextBlock _statusText;
        private string _downloadUrl;
        private string _latestVersion;

        public ManagerUpdateDialog(string latestVersion, string downloadUrl)
        {
            _latestVersion = latestVersion;
            _downloadUrl = downloadUrl;

            var lang = SharedUtilities.LoadLanguageDictionary();

            Title = SharedUtilities.GetTranslation(lang, "UpdateAvailable") ?? "Update Available";
            PrimaryButtonText = SharedUtilities.GetTranslation(lang, "DownloadAndInstall") ?? "Download and Install";
            CloseButtonText = SharedUtilities.GetTranslation(lang, "Cancel") ?? "Cancel";
            DefaultButton = ContentDialogButton.Primary;

            var stackPanel = new StackPanel { Spacing = 16 };

            var infoText = new TextBlock
            {
                Text = $"{SharedUtilities.GetTranslation(lang, "NewVersionAvailable") ?? "New version available"}: v{latestVersion}\n\n{SharedUtilities.GetTranslation(lang, "CurrentVersion") ?? "Current version"}: v{UpdateChecker.GetCurrentVersion()}",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stackPanel.Children.Add(infoText);

            _statusText = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(lang, "ReadyToDownload") ?? "Ready to download and install update.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.7,
                Margin = new Thickness(0, 8, 0, 4)
            };
            stackPanel.Children.Add(_statusText);

            _progressBar = new ProgressBar
            {
                Visibility = Visibility.Collapsed,
                Value = 0,
                Maximum = 100
            };
            stackPanel.Children.Add(_progressBar);

            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 400
            };

            PrimaryButtonClick += OnPrimaryButtonClick;
        }

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            
            var lang = SharedUtilities.LoadLanguageDictionary();

            try
            {
                IsPrimaryButtonEnabled = false;
                CloseButtonText = "";

                _statusText.Text = SharedUtilities.GetTranslation(lang, "DownloadingUpdate");
                _progressBar.Visibility = Visibility.Visible;
                _progressBar.IsIndeterminate = false;
                _progressBar.Value = 0;

                var progress = new Progress<int>(percent =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _progressBar.Value = percent;
                    });
                });

                var success = await UpdateChecker.DownloadAndInstallUpdateAsync(_downloadUrl, progress);

                if (!success)
                {
                    _statusText.Text = SharedUtilities.GetTranslation(lang, "UpdateDownloadFailed");
                    _progressBar.Visibility = Visibility.Collapsed;
                    IsPrimaryButtonEnabled = true;
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "Close");
                }
                // If successful, app will close and restart
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to download update", ex);
                _statusText.Text = $"{SharedUtilities.GetTranslation(lang, "UpdateFailed")}: {ex.Message}";
                _progressBar.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = true;
                CloseButtonText = SharedUtilities.GetTranslation(lang, "Close");
            }
        }
    }
}
