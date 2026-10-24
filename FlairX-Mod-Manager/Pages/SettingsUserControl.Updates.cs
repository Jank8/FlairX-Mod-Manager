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

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary();

                CheckUpdatesButton.IsEnabled = false;

                var result = await UpdateChecker.CheckForUpdatesAsync();

                if (result == null)
                {
                    if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(SharedUtilities.GetTranslation(lang, "UpdateCheckFailed"));
                    CheckUpdatesButton.IsEnabled = true;
                    return;
                }

                if (result.Value.updateAvailable)
                {
                    var updateDialog = new FlairX_Mod_Manager.Dialogs.ManagerUpdateDialog(
                        result.Value.latestVersion,
                        result.Value.downloadUrl,
                        result.Value.changelog
                    );
                    updateDialog.XamlRoot = this.XamlRoot;
                    await updateDialog.ShowAsync();
                }
                else
                {
                    var forceCheckBox = new CheckBox
                    {
                        Content = SharedUtilities.GetTranslation(lang, "ForceUpdate"),
                        IsChecked = false,
                        Margin = new Thickness(0, 8, 0, 0)
                    };

                    var clearSettingsCheckBox = new CheckBox
                    {
                        Content = SharedUtilities.GetTranslation(lang, "ClearSettingsFolder"),
                        IsChecked = false,
                        IsEnabled = false,
                        Margin = new Thickness(0, 4, 0, 0)
                    };

                    forceCheckBox.Checked += (s, ev) => clearSettingsCheckBox.IsEnabled = true;
                    forceCheckBox.Unchecked += (s, ev) =>
                    {
                        clearSettingsCheckBox.IsEnabled = false;
                        clearSettingsCheckBox.IsChecked = false;
                    };

                    var panel = new StackPanel { Spacing = 4 };
                    panel.Children.Add(new TextBlock
                    {
                        Text = $"{SharedUtilities.GetTranslation(lang, "LatestVersion")} ({UpdateChecker.GetCurrentVersion()})",
                        TextWrapping = TextWrapping.Wrap
                    });
                    panel.Children.Add(forceCheckBox);
                    panel.Children.Add(clearSettingsCheckBox);

                    var infoDialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(lang, "Information"),
                        Content = panel,
                        PrimaryButtonText = SharedUtilities.GetTranslation(lang, "DownloadAndInstall") ?? "Download and Install",
                        CloseButtonText = "OK",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };

                    // Only enable primary button when force is checked
                    infoDialog.Opened += (s, ev) => infoDialog.IsPrimaryButtonEnabled = false;
                    forceCheckBox.Checked += (s, ev) => infoDialog.IsPrimaryButtonEnabled = true;
                    forceCheckBox.Unchecked += (s, ev) => infoDialog.IsPrimaryButtonEnabled = false;

                    var infoResult = await infoDialog.ShowAsync();

                    if (infoResult == ContentDialogResult.Primary && forceCheckBox.IsChecked == true)
                    {
                        var updateDialog = new FlairX_Mod_Manager.Dialogs.ManagerUpdateDialog(
                            result.Value.latestVersion,
                            result.Value.downloadUrl,
                            result.Value.changelog,
                            forceUpdate: true,
                            clearSettings: clearSettingsCheckBox.IsChecked == true
                        );
                        updateDialog.XamlRoot = this.XamlRoot;
                        await updateDialog.ShowAsync();
                    }
                }

                CheckUpdatesButton.IsEnabled = true;
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("SettingsUserControl.CheckUpdatesButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("SettingsUserControl.CheckUpdatesButton_Click failed", ex);
            }
        }
        
        private void ReportIssueButton_Click(object sender, RoutedEventArgs e)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/Jank8/FlairX-Mod-Manager/issues/new",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo);
        }

        private async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                var currentVersion = UpdateChecker.GetCurrentVersion();

                var dialog = new ContentDialog
                {
                    PrimaryButtonText = "OK",
                    XamlRoot = this.XamlRoot,
                };
                var stackPanel = new StackPanel();

                // Add title first
                var titleBlock = new TextBlock
                {
                    Text = SharedUtilities.GetTranslation(lang, "App_Name"),
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 22,
                    Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 16)
                };
                stackPanel.Children.Add(titleBlock);

                // Add avatar image under title
                try
                {
                    var avatarPath = Path.Combine(PathManager.GetAbsolutePath("Assets"), "avatar.png");

                    if (File.Exists(avatarPath))
                    {
                        var avatarBorder = new Microsoft.UI.Xaml.Controls.Border
                        {
                            Width = 88,
                            Height = 88,
                            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(44),
                            BorderThickness = new Microsoft.UI.Xaml.Thickness(2),
                            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 12),
                        };

                        var avatarImage = new Microsoft.UI.Xaml.Controls.Image
                        {
                            Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
                        };

                        var avatarBitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                        avatarBitmap.UriSource = new Uri(Path.GetFullPath(avatarPath), UriKind.Absolute);
                        avatarImage.Source = avatarBitmap;

                        avatarBorder.Child = avatarImage;
                        stackPanel.Children.Add(avatarBorder);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to load avatar image: {ex.Message}");
                }

                // Author link
                stackPanel.Children.Add(new HyperlinkButton
                {
                    Content = "Jank8",
                    NavigateUri = new Uri("https://github.com/Jank8"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 15,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 4)
                });

                // Version
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"v{currentVersion}",
                    FontSize = 13,
                    Opacity = 0.6,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 20)
                });

                // Separator
                stackPanel.Children.Add(new Microsoft.UI.Xaml.Controls.Border
                {
                    Height = 1,
                    Opacity = 0.15,
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                    Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 12)
                });

                // License link
                var gplPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                gplPanel.Children.Add(new HyperlinkButton
                {
                    Content = SharedUtilities.GetTranslation(lang, "AboutDialog_License"),
                    NavigateUri = new Uri("https://www.gnu.org/licenses/gpl-3.0.html#license-text"),
                    FontSize = 12,
                    Opacity = 0.7
                });
                stackPanel.Children.Add(gplPanel);

                dialog.Content = stackPanel;
                await dialog.ShowAsync();
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("SettingsUserControl.AboutButton_Click failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("SettingsUserControl.AboutButton_Click failed", ex);
            }
        }
    }
}
