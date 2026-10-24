using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.Text;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class StatusKeeperSyncPage
    {

        // Public static version for use from other classes
        public static string TStatic(string key)
        {
            var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
            return SharedUtilities.GetTranslation(lang, key);
        }

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
            
            // File path card
            D3dxFilePathLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxFilePath_Label");
            D3dxFilePathOpenButtonText.Text = SharedUtilities.GetTranslation(lang, "Browse");
            
            // Backup confirmation card
            BackupConfirmationLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_BackupConfirmation_Label");
            BackupConfirmationDescription.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_BackupConfirmation_Description");
            
            // Dynamic sync card
            DynamicSyncLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_DynamicSync_Label");
            DynamicSyncDescription.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_DynamicSync_Description");
            ToolTipService.SetToolTip(DynamicSyncToggle, SharedUtilities.GetTranslation(lang, "StatusKeeper_Tooltip_DynamicSync"));
            
            // Manual sync card
            ManualSyncLabel.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_ManualSync_Label");
            ManualSyncDescription.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_ManualSync_Description");
            ManualSyncButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_ManualSync_Button");
            ToolTipService.SetToolTip(ManualSyncButton, SharedUtilities.GetTranslation(lang, "StatusKeeper_Tooltip_ManualSync"));
        }
        
        private void UpdateToggleLabels()
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            var onText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_On");
            var offText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_Off");
            
            if (BackupConfirmationToggleLabel != null && BackupConfirmationToggle != null)
                BackupConfirmationToggleLabel.Text = BackupConfirmationToggle.IsOn ? onText : offText;
            if (DynamicSyncToggleLabel != null && DynamicSyncToggle != null)
                DynamicSyncToggleLabel.Text = DynamicSyncToggle.IsOn ? onText : offText;
        }



        // Method for saving the logging setting, call it where you want to change StatusKeeperLoggingEnabled
        private void SetLoggingEnabled(bool enabled)
        {
            SettingsManager.Current.StatusKeeperLoggingEnabled = enabled;
            SettingsManager.Save();
        }

        private void InitializeBreadcrumbBar()
        {
            var currentGame = SettingsManager.CurrentSelectedGame ?? "ZZMI";
            var defaultPath = string.IsNullOrEmpty(SettingsManager.Current.StatusKeeperD3dxUserIniPath) 
                ? Path.Combine(".", "XXMI", currentGame, "d3dx_user.ini")
                : SettingsManager.Current.StatusKeeperD3dxUserIniPath;
            
            if (string.IsNullOrEmpty(SettingsManager.Current.StatusKeeperD3dxUserIniPath))
            {
                SettingsManager.Current.StatusKeeperD3dxUserIniPath = defaultPath;
                SettingsManager.Save();
            }
            SetBreadcrumbBar(D3dxFilePathBreadcrumb, SettingsManager.Current.StatusKeeperD3dxUserIniPath);
        }

        private void SetBreadcrumbBar(BreadcrumbBar bar, string path)
        {
            SharedUtilities.SetBreadcrumbBarPath(bar, path);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            // Fast UI initialization - only read from settings (no I/O operations)
            BackupConfirmationToggle.IsOn = SettingsManager.Current.StatusKeeperBackupConfirmed;
            DynamicSyncToggle.IsOn = SettingsManager.Current.StatusKeeperDynamicSyncEnabled;
            InitializeBreadcrumbBar();
            
            // Refresh translations when navigating to this page
            UpdateTexts();
            UpdateToggleLabels();
            
            // Update button states
            DynamicSyncToggle.IsEnabled = BackupConfirmationToggle.IsOn;
            ManualSyncButton.IsEnabled = BackupConfirmationToggle.IsOn;
            
            // Check d3dx_user.ini in background
            _ = Task.Run(() =>
            {
                var d3dxUserPath = GetD3dxUserPathStatic();
                if (string.IsNullOrEmpty(d3dxUserPath) && DynamicSyncToggle.IsOn)
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        DynamicSyncToggle.IsOn = false;
                        SettingsManager.Current.StatusKeeperDynamicSyncEnabled = false;
                        SettingsManager.Save();
                    });
                    
                    this.DispatcherQueue.TryEnqueue(async () =>
                    {
                        try
                        {
                            var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                            await ShowInfoDialog(SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Title"), SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Message"));
                        }
                        catch (System.OperationCanceledException)
                        {
                            Logger.LogDebug("StatusKeeperSyncPage.OnNavigatedTo failed: canceled");
                        }
                        catch (System.Exception ex)
                        {
                            Logger.LogError("StatusKeeperSyncPage.OnNavigatedTo failed", ex);
                        }
                    });
                }
            });
            
            // Move heavy operations to the background to avoid blocking the UI
            _ = Task.Run(() =>
            {
                try
                {
                    
                    // Check backup status in the background (only if sync is enabled)
                    if (DynamicSyncToggle.IsOn)
                    {
                        ValidateAndHandleSyncState();
                    }
                }
                catch (Exception ex)
                {
                    LogStatic($"Background validation error: {ex.Message}", "ERROR");
                }
            });
        }
        
        private void ValidateAndHandleSyncState()
        {
            // Check d3dx_user.ini
            var d3dxUserPath = GetD3dxUserPathStatic();
            if (string.IsNullOrEmpty(d3dxUserPath))
            {
                // Disable sync on UI thread
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    DynamicSyncToggle.IsOn = false;
                    SettingsManager.Current.StatusKeeperDynamicSyncEnabled = false;
                    SettingsManager.Save();
                });
                
                // Show message
                this.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                        await ShowInfoDialog(SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Title"), SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Message"));
                    }
                    catch (System.OperationCanceledException)
                    {
                        Logger.LogDebug("StatusKeeperSyncPage.ValidateAndHandleSyncState failed: canceled");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("StatusKeeperSyncPage.ValidateAndHandleSyncState failed", ex);
                    }
                });
                return;
            }
            
            // Perform automatic synchronization in the background
            if (DynamicSyncToggle.IsOn)
            {
                _ = SyncPersistentVariables();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            
            // Clear any heavy operations or timers to improve navigation performance
            // Note: We don't stop the static watcher/timer here as they should persist across navigation
            LogStatic("OnNavigatedFrom called - cleaning up page resources", "DEBUG");
        }



        private void D3dxFilePathOpenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var d3dxUserPath = SettingsManager.Current.StatusKeeperD3dxUserIniPath;
                if (!string.IsNullOrEmpty(d3dxUserPath))
                {
                    var directoryPath = Path.GetDirectoryName(d3dxUserPath);
                    if (!string.IsNullOrEmpty(directoryPath) && Directory.Exists(directoryPath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = directoryPath,
                            UseShellExecute = true
                        });
                        LogStatic($"Opened d3dx_user.ini directory: {directoryPath}");
                    }
                    else
                    {
                        LogStatic($"Directory does not exist: {directoryPath}", "WARNING");
                    }
                }
            }
            catch (Exception ex)
            {
                LogStatic($"Failed to open d3dx_user.ini directory: {ex.Message}", "ERROR");
            }
        }

        private void BackupConfirmationToggle_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateToggleLabels();
            SettingsManager.Current.StatusKeeperBackupConfirmed = BackupConfirmationToggle.IsOn;
            SettingsManager.Save();
            
            // Update button states based on backup confirmation
            UpdateSyncButtonStates();
            
            // If backup is not confirmed and sync is enabled, disable it
            if (!BackupConfirmationToggle.IsOn && DynamicSyncToggle.IsOn)
            {
                DynamicSyncToggle.IsOn = false;
                SettingsManager.Current.StatusKeeperDynamicSyncEnabled = false;
                SettingsManager.Save();
                StopWatcher();
                StopPeriodicSync();
                LogStatic("Dynamic sync disabled because backup confirmation was turned off");
            }
        }

        private void UpdateSyncButtonStates()
        {
            bool backupConfirmed = BackupConfirmationToggle.IsOn;
            
            // Disable sync functionality if backup is not confirmed
            DynamicSyncToggle.IsEnabled = backupConfirmed;
            ManualSyncButton.IsEnabled = backupConfirmed;
        }

        private void DynamicSyncToggle_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateToggleLabels();
            // Check if backup is confirmed before allowing sync
            if (DynamicSyncToggle.IsOn && !BackupConfirmationToggle.IsOn)
            {
                DynamicSyncToggle.IsOn = false;
                LogStatic("Cannot enable dynamic sync: Backup not confirmed", "WARNING");
                return;
            }
            
            SettingsManager.Current.StatusKeeperDynamicSyncEnabled = DynamicSyncToggle.IsOn;
            SettingsManager.Save();

            if (DynamicSyncToggle.IsOn)
            {
                var d3dxUserPath = GetD3dxUserPathStatic();
                if (string.IsNullOrEmpty(d3dxUserPath))
                {
                    DynamicSyncToggle.IsOn = false;
                    SettingsManager.Current.StatusKeeperDynamicSyncEnabled = false;
                    SettingsManager.Save();
                    LogStatic("Cannot enable auto-sync: d3dx_user.ini path not set or file not found", "ERROR");
                    var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                    _ = ShowInfoDialog(SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Title"), SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Message"));
                    return;
                }

                StartWatcher();
                StartPeriodicSync();
                LogStatic("Auto-updater enabled. File monitoring active.");
                // Perform automatic synchronization immediately after enabling
                _ = SyncPersistentVariables();
            }
            else
            {
                StopWatcher();
                StopPeriodicSync();
                LogStatic("Auto-updater disabled. Background monitoring stopped.");
            }
        }

        private async void ManualSyncButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if backup is confirmed before allowing manual sync
                if (!BackupConfirmationToggle.IsOn)
                {
                    LogStatic("Cannot sync: Backup not confirmed", "WARNING");
                    return;
                }

                // Check if the d3dx_user.ini file exists
                var d3dxUserPath = GetD3dxUserPathStatic();
                if (string.IsNullOrEmpty(d3dxUserPath))
                {
                    LogStatic("Cannot sync: d3dx_user.ini path not set or file not found", "ERROR");
                    var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                    await ShowInfoDialog(SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Title"), SharedUtilities.GetTranslation(lang, "StatusKeeper_D3dxMissing_Message"));
                    return;
                }

                try
                {
                    var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                    ManualSyncButton.IsEnabled = false;
                    ManualSyncProgressBar.Visibility = Visibility.Visible;
                    ManualSyncButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_Syncing");

                    LogStatic("Syncing persistent variables...");

                    var result = await SyncPersistentVariables();

                    var message = $"Sync complete! Updated {result.updateCount} variables in {result.fileCount} files";
                    if (result.lodSyncCount > 0)
                    {
                        message += $" and synced [Constants] to {result.lodSyncCount} LOD files";
                    }

                    LogStatic(message);

                    // Show completion dialog to user
                    await ShowInfoDialog(SharedUtilities.GetTranslation(lang, "StatusKeeper_ManualSync_Complete_Title"), message);
                }
                catch (Exception error)
                {
                    LogStatic($"Sync failed: {error.Message}", "ERROR");
                }
                finally
                {
                    var lang = SharedUtilities.LoadLanguageDictionary("StatusKeeper");
                    ManualSyncProgressBar.Visibility = Visibility.Collapsed;
                    ManualSyncButton.IsEnabled = true;
                    ManualSyncButtonText.Text = SharedUtilities.GetTranslation(lang, "StatusKeeper_ManualSync_Button");
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("StatusKeeperSyncPage.ManualSyncButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("StatusKeeperSyncPage.ManualSyncButton_Click failed", ex);
            }
        }



        // Simple info dialog using SharedUtilities
        private async Task ShowInfoDialog(string title, string message)
        {
            // Add logging before showing the dialog
            LogStatic($"Showing dialog: {title} - {message}", "DEBUG");
            
            try
            {
                await SharedUtilities.ShowInfoDialog(title, message, this.XamlRoot);
                // Add logging after closing the dialog
                Logger.LogStatusKeeper($"Dialog closed: {title}", "DEBUG");
            }
            catch (Exception ex)
            {
                Logger.LogStatusKeeperError($"Error showing dialog: {ex.Message}");
            }
        }

        private static void LogStatic(string message, string level = "INFO")
        {
            // Delegate to centralized Logger
            if (level == "ERROR")
            {
                Logger.LogStatusKeeperError(message);
            }
            else
            {
                Logger.LogStatusKeeper(message, level);
            }
        }
    }
}
