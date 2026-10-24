using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager
{
    public sealed partial class MainWindow
    {
        
        private async Task CheckRuntimeVersionsAsync()
        {
            try
            {
                // Let the window settle before any optional prompt.
                await Task.Delay(1000);

                var langDict = SharedUtilities.LoadLanguageDictionary();

                // .NET Runtime - compare only against versions that were actually found.
                // A failed scan returns null and must not be reported as "outdated".
                var requiredDotNetVersion = new Version(10, 0, 7);
                var installedDotNetVersion = GetHighestInstalledDotNetVersion();
                bool dotNetNeedsUpdate = installedDotNetVersion != null &&
                                         installedDotNetVersion < requiredDotNetVersion;

                // Windows App Runtime - same rule: unknown is not outdated. The old code
                // treated a failed Appx query as "needs update", which produced a blocking
                // dialog on every start and could not be dismissed for good.
                var requiredWinAppVersion = new Version(2, 0, 1);
                bool winAppMissing = App.IsWindowsAppRuntimeMissing;
                Version? installedWinAppVersion = null;
                if (!winAppMissing)
                {
                    installedWinAppVersion = await Task.Run(GetInstalledWindowsAppRuntimeVersion);
                }

                bool winAppNeedsUpdate = !winAppMissing &&
                                         installedWinAppVersion != null &&
                                         installedWinAppVersion < requiredWinAppVersion;

                Logger.LogInfo(
                    "Runtime check - .NET: " + (installedDotNetVersion?.ToString() ?? "unknown") +
                    " (required " + requiredDotNetVersion + ", update=" + dotNetNeedsUpdate + "); " +
                    "Windows App Runtime: " + (installedWinAppVersion?.ToString() ?? "unknown") +
                    " (required " + requiredWinAppVersion + ", update=" + winAppNeedsUpdate +
                    ", missing=" + winAppMissing + ")");

                if (!dotNetNeedsUpdate && !winAppNeedsUpdate && !winAppMissing)
                {
                    Logger.LogInfo("All runtime versions OK");
                    return;
                }

                // One prompt per application version. Clicking a button used to re-show the
                // dialog in a do/while loop, so it looked like the dialog could not be closed.
                if (string.Equals(SettingsManager.Current.RuntimeCheckDismissedVersion,
                                  AppConstants.APP_VERSION, StringComparison.Ordinal))
                {
                    Logger.LogInfo("Runtime prompt already shown for this version - skipping");
                    return;
                }

                SettingsManager.Current.RuntimeCheckDismissedVersion = AppConstants.APP_VERSION;
                SettingsManager.Save();

                var contentBuilder = new System.Text.StringBuilder();

                if (dotNetNeedsUpdate)
                {
                    contentBuilder.AppendLine("--------------------------------");
                    contentBuilder.AppendLine(".NET Runtime");
                    contentBuilder.AppendLine("--------------------------------");
                    contentBuilder.AppendLine(string.Format(
                        SharedUtilities.GetTranslation(langDict, "RuntimeCheck_DotNet_Update_Content"),
                        installedDotNetVersion, requiredDotNetVersion));
                    contentBuilder.AppendLine();
                }

                if (winAppMissing || winAppNeedsUpdate)
                {
                    contentBuilder.AppendLine("--------------------------------");
                    contentBuilder.AppendLine("Windows App Runtime");
                    contentBuilder.AppendLine("--------------------------------");

                    if (winAppMissing)
                    {
                        contentBuilder.AppendLine(SharedUtilities.GetTranslation(langDict, "RuntimeCheck_WinAppRuntime_Missing_Content"));
                    }
                    else
                    {
                        contentBuilder.AppendLine(string.Format(
                            SharedUtilities.GetTranslation(langDict, "RuntimeCheck_WinAppRuntime_Update_Content_WithVersion"),
                            installedWinAppVersion, requiredWinAppVersion));
                    }
                    contentBuilder.AppendLine();
                }

                var dialog = new ContentDialog
                {
                    // The translated title is .NET-specific. When only the Windows App Runtime
                    // section is present, leave it empty instead of claiming .NET is outdated.
                    Title = dotNetNeedsUpdate
                        ? SharedUtilities.GetTranslation(langDict, "RuntimeCheck_DotNet_Update_Title")
                        : null,
                    Content = contentBuilder.ToString().TrimEnd(),
                    CloseButtonText = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_MaybeLater_Button"),
                    XamlRoot = this.Content.XamlRoot
                };

                if (dotNetNeedsUpdate)
                {
                    dialog.PrimaryButtonText = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_Download_Button") + " .NET";
                }
                if (winAppMissing || winAppNeedsUpdate)
                {
                    dialog.SecondaryButtonText = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_Download_Button") + " Windows App Runtime";
                }

                var result = await ShowDialogAsync(dialog);

                if (result == ContentDialogResult.Primary)
                {
                    await OpenDownloadPageAsync("https://dotnet.microsoft.com/download/dotnet/10.0");
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    await OpenDownloadPageAsync("https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to check runtime versions", ex);
            }
        }

        /// <summary>
        /// Get the highest installed .NET 10 patch version by scanning the runtime folder on disk.
        /// Returns null if the folder cannot be read.
        /// </summary>
        private static Version? GetHighestInstalledDotNetVersion()
        {
            try
            {
                var candidates = new System.Collections.Generic.List<string>();

                // Standard install locations
                candidates.Add(@"C:\Program Files\dotnet\shared\Microsoft.NETCore.App");
                candidates.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared", "Microsoft.NETCore.App"));

                // Also check DOTNET_ROOT env var
                var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
                if (!string.IsNullOrEmpty(dotnetRoot))
                    candidates.Add(System.IO.Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App"));

                // Check where dotnet.exe lives
                var dotnetExe = System.Diagnostics.Process.GetProcessesByName("dotnet").FirstOrDefault()?.MainModule?.FileName;
                if (!string.IsNullOrEmpty(dotnetExe))
                    candidates.Add(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dotnetExe)!, "shared", "Microsoft.NETCore.App"));

                Version? highest = null;
                foreach (var runtimeDir in candidates)
                {
                    Logger.LogInfo($"GetHighestInstalledDotNetVersion: checking {runtimeDir} (exists={System.IO.Directory.Exists(runtimeDir)})");
                    if (!System.IO.Directory.Exists(runtimeDir)) continue;
                    foreach (var dir in System.IO.Directory.GetDirectories(runtimeDir))
                    {
                        var name = System.IO.Path.GetFileName(dir);
                        if (Version.TryParse(name, out var v) && v.Major == 10)
                        {
                            Logger.LogInfo($"GetHighestInstalledDotNetVersion: found {v}");
                            if (highest == null || v > highest)
                                highest = v;
                        }
                    }
                    if (highest != null) break;
                }

                Logger.LogInfo($"GetHighestInstalledDotNetVersion: result={highest?.ToString() ?? "not found"}");
                return highest;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to scan installed .NET versions", ex);
                return null;
            }
        }

        /// <summary>
        /// Reads the highest installed Windows App Runtime package version.
        /// Returns null when the version cannot be determined - callers must treat that as
        /// "unknown", not as "outdated".
        /// </summary>
        private static Version? GetInstalledWindowsAppRuntimeVersion()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -Command \"Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime.*' | Select-Object -ExpandProperty Version\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return null;

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();

                if (!process.WaitForExit(10000))
                {
                    Logger.LogWarning("Windows App Runtime version check timed out");
                    try { process.Kill(true); } catch { }
                    return null;
                }

                if (process.ExitCode != 0)
                {
                    Logger.LogWarning("Windows App Runtime version check failed: " + error.Trim());
                    return null;
                }

                Version? best = null;
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!Version.TryParse(line.Trim(), out var parsed))
                        continue;

                    var normalized = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
                    if (best == null || normalized > best)
                        best = normalized;
                }

                if (best == null)
                {
                    Logger.LogWarning("Windows App Runtime version not detected (treated as unknown, not outdated)");
                }

                return best;
            }
            catch (Exception ex)
            {
                Logger.LogDebug("Failed to read Windows App Runtime version: " + ex.Message);
                return null;
            }
        }

        private static async Task OpenDownloadPageAsync(string url)
        {
            try
            {
                if (await Windows.System.Launcher.LaunchUriAsync(new Uri(url)))
                {
                    Logger.LogInfo("Opened download page: " + url);
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Launcher failed for " + url + ": " + ex.Message);
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                Logger.LogInfo("Opened download page via shell: " + url);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to open download page: " + url, ex);
            }
        }

        private async Task ShowXXMIDownloadDialogAsync(string expectedPath)
        {
            var progressBar = new ProgressBar
            {
                IsIndeterminate = false,
                Value = 0,
                Minimum = 0,
                Maximum = 100,
                Width = 300,
                Visibility = Visibility.Collapsed
            };
            
            var statusText = new TextBlock
            {
                Text = SharedUtilities.GetTranslation(_lang, "LauncherNotFoundDescription"),
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            
            var contentPanel = new StackPanel { Spacing = 8 };
            contentPanel.Children.Add(statusText);
            contentPanel.Children.Add(progressBar);

            var dialog = new ContentDialog
            {
                Title = SharedUtilities.GetTranslation(_lang, "LauncherNotFound"),
                Content = contentPanel,
                PrimaryButtonText = SharedUtilities.GetTranslation(_lang, "Download_XXMI"),
                CloseButtonText = SharedUtilities.GetTranslation(_lang, "Cancel"),
                XamlRoot = this.Content.XamlRoot
            };

            bool isDownloading = false;
            bool downloadSuccess = false;

            dialog.CloseButtonClick += (s, e) =>
            {
                if (isDownloading)
                {
                    XXMIDownloader.CancelDownload();
                }
            };

            dialog.PrimaryButtonClick += async (s, e) =>
            {
                try
                {
                    var deferral = e.GetDeferral();

                    try
                    {
                        isDownloading = true;

                        dialog.IsPrimaryButtonEnabled = false;
                        progressBar.Visibility = Visibility.Visible;
                        progressBar.IsIndeterminate = true;
                        statusText.Text = SharedUtilities.GetTranslation(_lang, "XXMI_Download_Starting") ?? "Starting download...";

                        var progress = new Progress<(int percent, string status)>(p =>
                        {
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                progressBar.IsIndeterminate = false;
                                progressBar.Value = p.percent;
                                statusText.Text = p.status;
                            });
                        });

                        downloadSuccess = await XXMIDownloader.DownloadAndInstallAsync(progress, _lang);

                        if (downloadSuccess)
                        {
                            statusText.Text = SharedUtilities.GetTranslation(_lang, "XXMI_Download_Success") ?? "XXMI Launcher installed successfully!";
                            progressBar.Value = 100;
                        }
                        else
                        {
                            statusText.Text = SharedUtilities.GetTranslation(_lang, "XXMI_Download_Failed") ?? "Failed to download XXMI Launcher";
                            dialog.IsPrimaryButtonEnabled = true;
                        }
                    }
                    finally
                    {
                        isDownloading = false;
                        deferral.Complete();
                    }
                }
                catch (System.OperationCanceledException)
                {
                    Logger.LogDebug("MainWindow.ShowXXMIDownloadDialogAsync failed: canceled");
                }
                catch (System.Exception ex)
                {
                    Logger.LogError("MainWindow.ShowXXMIDownloadDialogAsync failed", ex);
                }
            };

            await MainWindow.ShowDialogAsync(dialog);
            
            if (downloadSuccess)
            {
                ShowSuccessInfo(SharedUtilities.GetTranslation(_lang, "XXMI_Download_Success") ?? "XXMI Launcher installed successfully!", 3000);
            }
        }

        // Symlink cleanup removed - no longer needed with DISABLED_ prefix system

        /// <summary>
        /// Show Starter Pack dialog if available for the game and not dismissed
        /// </summary>
        public async Task ShowStarterPackIfNeeded(string gameTag)
        {
            await ShowStarterPackDialogIfNeededAsync(gameTag);
        }
        
        private async Task ShowStarterPackDialogIfNeededAsync(string gameTag)
        {
            try
            {
                Logger.LogDebug($"ShowStarterPackDialogIfNeededAsync called for {gameTag}");
                
                // Check if Starter Pack is available for this game
                if (!Dialogs.StarterPackDialog.IsStarterPackAvailable(gameTag))
                {
                    Logger.LogDebug($"No Starter Pack available for {gameTag}");
                    return;
                }

                // Check if user has dismissed the dialog for this game
                if (Dialogs.StarterPackDialog.IsStarterPackDismissed(gameTag))
                {
                    Logger.LogDebug($"Starter Pack dialog dismissed for {gameTag}");
                    return;
                }

                // Check if mods folder already has content (excluding Other category)
                if (!Dialogs.StarterPackDialog.IsModsFolderEmpty(gameTag))
                {
                    Logger.LogDebug($"Mods folder not empty for {gameTag}, skipping Starter Pack dialog");
                    return;
                }

                Logger.LogDebug($"All conditions met, showing Starter Pack dialog for {gameTag}");

                // Small delay to let the UI settle
                await Task.Delay(500);

                // Show the dialog
                var dialog = new Dialogs.StarterPackDialog(gameTag);
                dialog.XamlRoot = this.Content.XamlRoot;
                
                // Subscribe to installation complete event to reload mods
                dialog.InstallationComplete += async (s, e) =>
                {
                    try
                    {
                        await ReloadModsAsync();
                    }
                    catch (System.OperationCanceledException)
                    {
                        Logger.LogDebug("MainWindow.ShowStarterPackDialogIfNeededAsync failed: canceled");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("MainWindow.ShowStarterPackDialogIfNeededAsync failed", ex);
                    }
                };
                
                var result = await MainWindow.ShowDialogAsync(dialog);
                Logger.LogDebug($"Starter Pack dialog result: {result}");
                
                // If user clicked "No thanks" with checkbox checked, it's already handled in the dialog
                // If user clicked "No thanks" without checkbox, we don't dismiss permanently
                if (result == ContentDialogResult.None && dialog.DontShowAgain)
                {
                    Dialogs.StarterPackDialog.DismissStarterPack(gameTag);
                    Logger.LogDebug($"Starter Pack dismissed permanently for {gameTag}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show Starter Pack dialog for {gameTag}", ex);
            }
        }
    }
}
