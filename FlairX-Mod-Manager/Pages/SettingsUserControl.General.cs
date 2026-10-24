using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Runtime.InteropServices;
using System.Threading;
using FlairX_Mod_Manager;
using FlairX_Mod_Manager.Models;
using FlairX_Mod_Manager.Services;
using XamlImage = Microsoft.UI.Xaml.Controls.Image;
using SharpImage = SixLabors.ImageSharp.Image;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class SettingsUserControl
    {

        private async Task XXMIRootDirectoryPickButton_ClickAsync(Button senderButton)
        {
            senderButton.IsEnabled = false;
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                var hwnd = SharedUtilities.GetMainWindowHandle();
                var folderPath = await SharedUtilities.PickFolderAsync(hwnd, SharedUtilities.GetTranslation(lang, "PickFolderDialog_Title"));
                if (!string.IsNullOrEmpty(folderPath))
                {
                    // Clean up from current mods directory before switching

                    
                    // Set XXMI root for current game
                    SettingsManager.SetCurrentGameXXMIRoot(folderPath);
                    SetBreadcrumbBar(XXMIRootDirectoryBreadcrumb, folderPath);
                    // RecreateSymlinksFromActiveMods removed - no longer needed
                    
                    FlairX_Mod_Manager.Logger.LogInfo($"Changed XXMI root directory to: {folderPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to change XXMI root directory", ex);

                if (App.Current is App rootApp && rootApp.MainWindow is MainWindow rootWindow)
                {
                    var rootLang = SharedUtilities.LoadLanguageDictionary();
                    rootWindow.ShowErrorInfo(SharedUtilities.GetTranslation(rootLang, "Notify_XXMIRootChangeFailed"));
                }
            }
            senderButton.IsEnabled = true;
        }

        private async void XXMIRootDirectoryPickButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button senderButton)
                    await XXMIRootDirectoryPickButton_ClickAsync(senderButton);
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("SettingsUserControl.XXMIRootDirectoryPickButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("SettingsUserControl.XXMIRootDirectoryPickButton_Click failed", ex);
            }
        }

        private void XXMIRootDirectoryDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            string gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
            if (string.IsNullOrEmpty(gameTag))
                return;

            var currentModsPath = SettingsManager.GetCurrentXXMIModsDirectory();
            var defaultXXMIRoot = @".\XXMI";
            var defaultModsPath = AppConstants.GameConfig.GetModsPath(gameTag);
            
            // If already using default, do nothing
            if (string.Equals(Path.GetFullPath(currentModsPath), Path.GetFullPath(defaultModsPath), StringComparison.OrdinalIgnoreCase))
                return;



            // Remove custom XXMI root for current game to use default
            SettingsManager.Current.GameXXMIRootPaths.Remove(gameTag);
            SettingsManager.Save();
            
            SetBreadcrumbBar(XXMIRootDirectoryBreadcrumb, defaultXXMIRoot);
            // RecreateSymlinksFromActiveMods removed - no longer needed
            
            FlairX_Mod_Manager.Logger.LogInfo($"Restored XXMI root to default: {defaultXXMIRoot}");
        }

        private void XXMIRootDirectoryBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            var path = GetBreadcrumbPath(sender);
            string gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
            if (!string.IsNullOrEmpty(gameTag))
            {
                SettingsManager.Current.GameXXMIRootPaths[gameTag] = path;
                SettingsManager.Save();
            }
        }



        private void DefaultResolutionOnStartToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.UseDefaultResolutionOnStart = DefaultResolutionOnStartToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();
            
            // Enable/disable the resolution input boxes
            DefaultStartWidthTextBox.IsEnabled = DefaultResolutionOnStartToggle.IsOn;
            DefaultStartHeightTextBox.IsEnabled = DefaultResolutionOnStartToggle.IsOn;
        }

        private void DefaultStartWidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && int.TryParse(textBox.Text, out int width))
            {
                // Only validate if the textbox is enabled (user is editing)
                if (textBox.IsEnabled)
                {
                    // Get full screen resolution (not just work area)
                    var bounds = Microsoft.UI.Windowing.DisplayArea.Primary.OuterBounds;
                    int maxWidth = bounds.Width;
                    int minWidth = 1300; // Use MainWindow MIN_WIDTH constant
                    
                    if (width >= minWidth && width <= maxWidth)
                    {
                        SettingsManager.Current.DefaultStartWidth = width;
                        SettingsManager.Save();
                        textBox.BorderBrush = null; // Reset border color
                    }
                    else
                    {
                        // Set red border to indicate invalid value
                        textBox.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                    }
                }
                else
                {
                    // When disabled (showing current size), don't show red border
                    textBox.BorderBrush = null;
                }
            }
        }

        private void DefaultStartHeightTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && int.TryParse(textBox.Text, out int height))
            {
                // Only validate if the textbox is enabled (user is editing)
                if (textBox.IsEnabled)
                {
                    // Get full screen resolution (not just work area)
                    var bounds = Microsoft.UI.Windowing.DisplayArea.Primary.OuterBounds;
                    int maxHeight = bounds.Height;
                    int minHeight = 720; // Use MainWindow MIN_HEIGHT constant
                    
                    if (height >= minHeight && height <= maxHeight)
                    {
                        SettingsManager.Current.DefaultStartHeight = height;
                        SettingsManager.Save();
                        textBox.BorderBrush = null; // Reset border color
                    }
                    else
                    {
                        // Set red border to indicate invalid value
                        textBox.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                    }
                }
                else
                {
                    // When disabled (showing current size), don't show red border
                    textBox.BorderBrush = null;
                }
            }
        }

        private void OnWindowSizeChanged(object? sender, EventArgs e)
        {
            // Restart the timer - this will delay the update by 200ms
            _windowSizeUpdateTimer?.Stop();
            _windowSizeUpdateTimer?.Start();
        }

        private void WindowSizeUpdateTimer_Tick(object? sender, object e)
        {
            // Stop the timer and update the resolution fields
            _windowSizeUpdateTimer?.Stop();
            UpdateResolutionFields();
        }

        private void UpdateResolutionFields()
        {
            // Only update if toggle is OFF (when it's ON, user controls the values)
            if (!SettingsManager.Current.UseDefaultResolutionOnStart)
            {
                var mainWindow = (App.Current as App)?.MainWindow;
                if (mainWindow != null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
                    var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    
                    if (appWindow != null)
                    {
                        // Get the client area size (content area without title bar and borders)
                        // by using the actual content size instead of window size
                        if (mainWindow.Content is FrameworkElement content)
                        {
                            DefaultStartWidthTextBox.Text = ((int)content.ActualWidth).ToString();
                            DefaultStartHeightTextBox.Text = ((int)content.ActualHeight).ToString();
                        }
                        else
                        {
                            // Fallback to window size if content is not available
                            DefaultStartWidthTextBox.Text = appWindow.Size.Width.ToString();
                            DefaultStartHeightTextBox.Text = appWindow.Size.Height.ToString();
                        }
                    }
                }
            }
        }
        

        
        private void FastDownloadToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.SetFastDownloadEnabled(FastDownloadToggle.IsOn);
            UpdateToggleLabels();
        }
        
        private void MaxConnectionsNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (sender.Value >= 1 && sender.Value <= 8)
            {
                SettingsManager.SetMaxDownloadConnections((int)sender.Value);
            }
        }
    }
}
