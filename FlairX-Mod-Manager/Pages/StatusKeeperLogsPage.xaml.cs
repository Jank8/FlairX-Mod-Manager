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

        public StatusKeeperLogsPage()
        {
            this.InitializeComponent();
            UpdateTexts();
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
