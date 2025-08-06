using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class StatusKeeperLogsPage : Page
    {
        private StatusKeeperSettings _settings = new();
        private Microsoft.UI.Xaml.DispatcherTimer? _logRefreshTimer;
        private const int LogRefreshIntervalSeconds = 0; // zmieniamy na 0 sekund

        public StatusKeeperLogsPage()
        {
            this.InitializeComponent();
            UpdateTexts();
            InitLogRefreshTimer();
        }

        private void InitLogRefreshTimer()
        {
            _logRefreshTimer = new Microsoft.UI.Xaml.DispatcherTimer();
            _logRefreshTimer.Interval = TimeSpan.FromMilliseconds(AppConstants.LOG_REFRESH_INTERVAL_MS);
            _logRefreshTimer.Tick += (s, e) => RefreshLogContent();
        }

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
            LoggingLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_Logging_Label");
            OpenLogButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_OpenLog_Button");
            ClearLogButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_ClearLog_Button");
        }

        private void LoadSettingsToUI()
        {
            LoggingToggle.IsOn = SettingsManager.Current.StatusKeeperLoggingEnabled;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is StatusKeeperSettings settings)
            {
                _settings = settings;
                LoadSettingsToUI();
            }
            RefreshLogContent();
            _logRefreshTimer?.Start();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _logRefreshTimer?.Stop();
        }

        private void RefreshLogContent()
        {
            var logPath = GetLogPath();
            if (File.Exists(logPath))
            {
                try
                {
                    string logContent = File.ReadAllText(logPath, System.Text.Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(logContent))
                    {
                        // Najnowsze wpisy na g�rze
                        var lines = logContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        Array.Reverse(lines);
                        LogsTextBlock.Text = string.Join("\n", lines);
                        
                        // Przewi� na g�r�, aby pokaza� najnowsze wpisy
                        LogsScrollViewer.ScrollToVerticalOffset(0);
                    }
                    else
                    {
                        var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                        LogsTextBlock.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_Log_Empty");
                    }
                }
                catch (Exception ex)
                {
                    LogsTextBlock.Text = $"Error reading log file: {ex.Message}";
                }
            }
            else
            {
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                LogsTextBlock.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_Log_NotFound");
            }
        }

        private string GetLogPath()
        {
            return PathManager.GetSettingsPath("StatusKeeper.log");
        }

        private void InitFileLogging(string logPath)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd | HH:mm:ss");
            File.WriteAllText(logPath, $"=== ModStatusKeeper Log Started at {timestamp} ===\n", System.Text.Encoding.UTF8);
        }

        private void LoggingToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.StatusKeeperLoggingEnabled = LoggingToggle.IsOn;
            SettingsManager.Save();
            var logPath = GetLogPath();
            if (LoggingToggle.IsOn)
            {
                InitFileLogging(logPath);
                Debug.WriteLine("File logging enabled");
            }
            else
            {
                Debug.WriteLine("File logging disabled - console only");
            }
            RefreshLogContent();
        }

        private async void OpenLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logPath = GetLogPath();
                
                if (File.Exists(logPath))
                {
                    // Use SharedUtilities to open the file with default application
                    if (SharedUtilities.OpenWithDefaultApp(logPath))
                    {
                        Debug.WriteLine("Log file opened in default text editor");
                    }
                    else
                    {
                        var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                        var dialog = new ContentDialog
                        {
                            Title = SharedUtilities.GetTranslation(lang, "Error_Generic"),
                            Content = "Failed to open log file with default application",
                            CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                            XamlRoot = this.XamlRoot
                        };
                        await dialog.ShowAsync();
                    }
                }
                else
                {
                    var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                    await SharedUtilities.ShowInfoDialog(SharedUtilities.GetTranslation(lang, "StatusKeeper_Info"), SharedUtilities.GetTranslation(lang, "StatusKeeper_LogNotFound_Message"), this.XamlRoot);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open log file: {ex.Message}");
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "Error_Generic"),
                    Content = $"Failed to open log file: {ex.Message}",
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "OK"),
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private async void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logPath = GetLogPath();
                
                if (File.Exists(logPath))
                {
                    // Delete the current log file
                    File.Delete(logPath);
                    
                    // Reinitialize logging if it's enabled
                    if (SettingsManager.Current.StatusKeeperLoggingEnabled)
                    {
                        InitFileLogging(logPath);
                        Debug.WriteLine("Log file cleared and reinitialized");
                    }
                    
                    RefreshLogContent();
                    
                    var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                    await SharedUtilities.ShowInfoDialog(SharedUtilities.GetTranslation(lang, "StatusKeeper_Success"), SharedUtilities.GetTranslation(lang, "StatusKeeper_LogCleared_Success"), this.XamlRoot);
                }
                else
                {
                    var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                    await SharedUtilities.ShowInfoDialog(SharedUtilities.GetTranslation(lang, "StatusKeeper_Info"), SharedUtilities.GetTranslation(lang, "StatusKeeper_LogNotFound_Clear"), this.XamlRoot);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to clear log file: {ex.Message}");
                var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                await SharedUtilities.ShowErrorDialog(SharedUtilities.GetTranslation(lang, "Error_Generic"), $"Failed to clear log file: {ex.Message}", this.XamlRoot);
            }
        }
    }
}
