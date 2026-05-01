using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using FlairX_Mod_Manager.Services;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// MainWindow partial class - UI event handlers and button clicks
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private void NvSample_Loaded(object sender, RoutedEventArgs e)
        {
            _allMenuItems = nvSample.MenuItems.OfType<NavigationViewItem>().ToList();
            _allFooterItems = nvSample.FooterMenuItems.OfType<NavigationViewItem>().ToList();
            SetFooterMenuTranslations();
        }

        private void MainRoot_Loaded(object sender, RoutedEventArgs e)
        {
            // Restore saved game selection first
            RestoreGameSelection();
            
            // Mark initialization as complete after game selection is restored
            _isInitializationComplete = true;
            
            // Check both .NET and Windows App Runtime versions in one dialog
            _ = CheckRuntimeVersionsAsync();
            
            // Auto-detect hotkeys for all mods in background on startup
            _ = Task.Run(async () =>
            {
                try
                {
                    Logger.LogInfo("Starting full hotkey detection on startup");
                    await Pages.HotkeyFinderPage.RefreshAllModsHotkeysStaticAsync();
                    Logger.LogInfo("Completed full hotkey detection on startup");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to auto-detect hotkeys on startup", ex);
                }
            });
        }
        
        private async Task CheckRuntimeVersionsAsync()
        {
            try
            {
                // Small delay to let the window fully load
                await Task.Delay(1000);
                
                var langDict = SharedUtilities.LoadLanguageDictionary();
                
                // Check .NET Runtime
                var requiredDotNetVersion = new Version(10, 0, 7);
                var currentDotNetVersion = Environment.Version;
                bool dotNetNeedsUpdate = currentDotNetVersion < requiredDotNetVersion;
                
                Logger.LogInfo($".NET Runtime version: {currentDotNetVersion}");
                
                // Check Windows App Runtime
                // SDK 2.0+ uses simple SemVer - required version is 2.0.1
                var requiredWinAppVersion = new Version("2.0.1");
                Version? installedWinAppVersion = null;
                bool winAppNeedsUpdate = false;
                bool winAppMissing = App.IsWindowsAppRuntimeMissing;
                
                if (!winAppMissing)
                {
                    installedWinAppVersion = await Task.Run(() =>
                    {
                        try
                        {
                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "powershell.exe",
                                Arguments = "-NoProfile -Command \"Get-AppxPackage | Where-Object {$_.Name -eq 'Microsoft.WindowsAppRuntime.2' -and $_.Architecture -eq 'X64'} | Select-Object -First 1 -ExpandProperty Version\"",
                                RedirectStandardOutput = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            
                            using var process = System.Diagnostics.Process.Start(psi);
                            if (process != null)
                            {
                                var output = process.StandardOutput.ReadToEnd().Trim();
                                process.WaitForExit();
                                
                                if (!string.IsNullOrWhiteSpace(output) && Version.TryParse(output, out var fullVersion))
                                {
                                    // Return simple SemVer format: Major.Minor.Build
                                    return new Version(fullVersion.Major, fullVersion.Minor, fullVersion.Build);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogDebug($"Failed to check Windows App Runtime version: {ex.Message}");
                        }
                        return null;
                    });
                    
                    if (installedWinAppVersion != null)
                    {
                        // Simple SemVer comparison (2.0.1 vs 2.0.0, etc.)
                        winAppNeedsUpdate = installedWinAppVersion < requiredWinAppVersion;
                        Logger.LogInfo($"Windows App Runtime version: {installedWinAppVersion} (required: {requiredWinAppVersion})");
                    }
                    else
                    {
                        winAppNeedsUpdate = true;
                    }
                }
                
                // Show dialog only if something needs update
                if (dotNetNeedsUpdate || winAppNeedsUpdate || winAppMissing)
                {
                    var contentBuilder = new System.Text.StringBuilder();
                    
                    // .NET section
                    if (dotNetNeedsUpdate)
                    {
                        contentBuilder.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        contentBuilder.AppendLine(".NET Runtime");
                        contentBuilder.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        
                        var dotNetContent = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_DotNet_Update_Content");
                        contentBuilder.AppendLine(string.Format(dotNetContent, currentDotNetVersion, requiredDotNetVersion));
                        contentBuilder.AppendLine();
                    }
                    
                    // Windows App Runtime section
                    if (winAppMissing || winAppNeedsUpdate)
                    {
                        contentBuilder.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        contentBuilder.AppendLine("Windows App Runtime");
                        contentBuilder.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        
                        if (winAppMissing)
                        {
                            var missingContent = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_WinAppRuntime_Missing_Content");
                            contentBuilder.AppendLine(missingContent);
                        }
                        else if (installedWinAppVersion != null)
                        {
                            var updateContent = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_WinAppRuntime_Update_Content_WithVersion");
                            contentBuilder.AppendLine(string.Format(updateContent, installedWinAppVersion, requiredWinAppVersion));
                        }
                        else
                        {
                            var noVersionContent = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_WinAppRuntime_Update_Content_NoVersion");
                            contentBuilder.AppendLine(noVersionContent);
                        }
                    }
                    
                    var dialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_DotNet_Update_Title"),
                        Content = contentBuilder.ToString(),
                        PrimaryButtonText = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_Download_Button") + " .NET",
                        SecondaryButtonText = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_Download_Button") + " Windows App Runtime",
                        CloseButtonText = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_MaybeLater_Button"),
                        XamlRoot = this.Content.XamlRoot
                    };
                    
                    ContentDialogResult result;
                    do
                    {
                        result = await dialog.ShowAsync();
                        
                        if (result == ContentDialogResult.Primary)
                        {
                            // Install .NET via winget or open browser
                            await InstallDotNetRuntimeAsync();
                            // Don't close dialog, show it again
                        }
                        else if (result == ContentDialogResult.Secondary)
                        {
                            // Install Windows App Runtime via winget or open browser
                            await InstallWindowsAppRuntimeAsync();
                            // Don't close dialog, show it again
                        }
                    } while (result != ContentDialogResult.None); // None = CloseButton clicked
                }
                else
                {
                    Logger.LogInfo("All runtime versions OK");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to check runtime versions", ex);
            }
        }
        
        private async Task CheckDotNetRuntimeVersionAsync()
        {
            try
            {
                // Small delay to let the window fully load
                await Task.Delay(500);
                
                // Required .NET version: 10.0.7 (latest as of April 21, 2026)
                var requiredVersion = new Version(10, 0, 7);
                var currentVersion = Environment.Version;
                
                Logger.LogInfo($".NET Runtime version: {currentVersion}");
                
                if (currentVersion < requiredVersion)
                {
                    Logger.LogWarning($"Outdated .NET Runtime detected: {currentVersion} (required: {requiredVersion})");
                    
                    var langDict = SharedUtilities.LoadLanguageDictionary();
                    var contentTemplate = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_DotNet_Update_Content");
                    var content = string.Format(contentTemplate, currentVersion, requiredVersion);
                    
                    var dialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_DotNet_Update_Title"),
                        Content = content,
                        PrimaryButtonText = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_Download_Button"),
                        CloseButtonText = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_MaybeLater_Button"),
                        XamlRoot = this.Content.XamlRoot
                    };
                    
                    var result = await dialog.ShowAsync();
                    
                    if (result == ContentDialogResult.Primary)
                    {
                        // Install .NET Runtime via winget or open browser
                        await InstallDotNetRuntimeAsync();
                    }
                }
                else
                {
                    Logger.LogInfo($".NET Runtime version OK: {currentVersion}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to check .NET Runtime version", ex);
            }
        }
        
        private async Task ShowWindowsAppRuntimeWarningAsync()
        {
            try
            {
                // Small delay to let the window fully load
                await Task.Delay(1000);
                
                var langDict = SharedUtilities.LoadLanguageDictionary();
                
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_WinAppRuntime_Missing_Title"),
                    Content = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_WinAppRuntime_Missing_Content"),
                    PrimaryButtonText = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_Download_Button"),
                    CloseButtonText = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_ContinueAnyway_Button"),
                    XamlRoot = this.Content.XamlRoot
                };
                
                var result = await dialog.ShowAsync();
                
                if (result == ContentDialogResult.Primary)
                {
                    // Install Windows App Runtime via winget or open browser
                    await InstallWindowsAppRuntimeAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to show Windows App Runtime warning", ex);
            }
        }
        private async Task CheckWindowsAppRuntimeVersionAsync()
        {
            try
            {
                // Small delay to let the window fully load
                await Task.Delay(1500);
                
                // SDK 2.0+ uses simple SemVer - required version is 2.0.1
                const string requiredVersionString = "2.0.1";
                var requiredVersion = new Version(requiredVersionString);
                
                // Check installed runtime 2.0 version
                var installedVersion = await Task.Run(() =>
                {
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = "-NoProfile -Command \"Get-AppxPackage | Where-Object {$_.Name -eq 'Microsoft.WindowsAppRuntime.2' -and $_.Architecture -eq 'X64'} | Select-Object -First 1 -ExpandProperty Version\"",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        
                        using var process = System.Diagnostics.Process.Start(psi);
                        if (process != null)
                        {
                            var output = process.StandardOutput.ReadToEnd().Trim();
                            process.WaitForExit();
                            
                            if (!string.IsNullOrWhiteSpace(output) && Version.TryParse(output, out var fullVersion))
                            {
                                // Return simple SemVer format: Major.Minor.Build
                                return new Version(fullVersion.Major, fullVersion.Minor, fullVersion.Build);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogDebug($"Failed to check runtime version: {ex.Message}");
                    }
                    return null;
                });
                
                if (installedVersion == null)
                {
                    // No runtime 2.0 found at all
                    var langDict = SharedUtilities.LoadLanguageDictionary();
                    
                    var dialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_WinAppRuntime_Update_Title"),
                        Content = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_WinAppRuntime_Update_Content_NoVersion"),
                        PrimaryButtonText = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_Download_Button"),
                        CloseButtonText = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_MaybeLater_Button"),
                        XamlRoot = this.Content.XamlRoot
                    };
                    
                    var result = await dialog.ShowAsync();
                    
                    if (result == ContentDialogResult.Primary)
                    {
                        // Install Windows App Runtime via winget or open browser
                        await InstallWindowsAppRuntimeAsync();
                    }
                }
                else if (installedVersion < requiredVersion)
                {
                    // Runtime 2.0 found but outdated
                    Logger.LogInfo($"Outdated Windows App Runtime 2.0 detected: {installedVersion} (required: {requiredVersion})");
                    
                    var langDict = SharedUtilities.LoadLanguageDictionary();
                    var contentTemplate = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_WinAppRuntime_Update_Content_WithVersion");
                    var content = string.Format(contentTemplate, installedVersion, requiredVersion);
                    
                    var dialog = new ContentDialog
                    {
                        Title = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_WinAppRuntime_Update_Title"),
                        Content = content,
                        PrimaryButtonText = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_Download_Button"),
                        CloseButtonText = SharedUtilities.GetTranslation(langDict, "RuntimeCheck_MaybeLater_Button"),
                        XamlRoot = this.Content.XamlRoot
                    };
                    
                    var result = await dialog.ShowAsync();
                    
                    if (result == ContentDialogResult.Primary)
                    {
                        // Install Windows App Runtime via winget or open browser
                        await InstallWindowsAppRuntimeAsync();
                    }
                }
                else
                {
                    Logger.LogInfo($"Windows App Runtime 2.0 version OK: {installedVersion}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to check Windows App Runtime version", ex);
            }
        }

        /// <summary>
        /// Check if winget is available on the system
        /// </summary>
        private async Task<bool> IsWingetAvailableAsync()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c winget --version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Winget not available: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Find the best available package version in winget that satisfies the required version
        /// </summary>
        private async Task<string?> FindBestWingetPackageAsync(string packagePrefix, Version requiredVersion)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c winget search {packagePrefix}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode != 0)
                    {
                        return null;
                    }

                    // Parse winget output to find matching packages
                    var lines = output.Split('\n');
                    var bestPackageId = (string?)null;
                    var bestVersion = (Version?)null;

                    foreach (var line in lines)
                    {
                        // Skip header lines
                        if (line.Contains("Name") || line.Contains("---") || string.IsNullOrWhiteSpace(line))
                            continue;

                        // Parse line format: "Name    Id    Version    Source"
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 3)
                            continue;

                        // Find the package ID (starts with packagePrefix)
                        string? packageId = null;
                        string? versionStr = null;

                        for (int i = 0; i < parts.Length - 1; i++)
                        {
                            if (parts[i].StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase))
                            {
                                packageId = parts[i];
                                // Version is typically the next part
                                if (i + 1 < parts.Length)
                                {
                                    versionStr = parts[i + 1];
                                }
                                break;
                            }
                        }

                        if (packageId == null || versionStr == null)
                            continue;

                        // Try to parse version
                        if (Version.TryParse(versionStr, out var packageVersion))
                        {
                            // Check if this version satisfies the requirement
                            if (packageVersion >= requiredVersion)
                            {
                                // Keep track of the best (highest) version found
                                if (bestVersion == null || packageVersion > bestVersion)
                                {
                                    bestVersion = packageVersion;
                                    bestPackageId = packageId;
                                }
                            }
                        }
                    }

                    if (bestPackageId != null && bestVersion != null)
                    {
                        Logger.LogInfo($"Found package {bestPackageId} version {bestVersion} (required: {requiredVersion})");
                        return bestPackageId;
                    }
                    else
                    {
                        Logger.LogInfo($"No package found matching {packagePrefix} with version >= {requiredVersion}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to search for package {packagePrefix}: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// Install .NET Runtime via winget or open browser
        /// </summary>
        private async Task InstallDotNetRuntimeAsync()
        {
            try
            {
                if (await IsWingetAvailableAsync())
                {
                    // Find the best available .NET Runtime package (10.0.7 or higher)
                    var packageId = await FindBestWingetPackageAsync("Microsoft.DotNet.Runtime", new Version(10, 0, 7));
                    
                    if (packageId != null)
                    {
                        Logger.LogInfo($"Installing .NET Runtime via winget: {packageId}");
                        
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c start cmd /k \"winget install {packageId} && pause\"",
                            UseShellExecute = true,
                            CreateNoWindow = false
                        };
                        System.Diagnostics.Process.Start(psi);
                        return;
                    }
                    else
                    {
                        Logger.LogInfo(".NET Runtime 10.0.7+ not found in winget, falling back to browser");
                    }
                }
                else
                {
                    Logger.LogInfo("Winget not available, opening browser for .NET download");
                }
                
                // Fallback to browser
                var browserPsi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://dotnet.microsoft.com/download/dotnet/10.0",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(browserPsi);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to install .NET Runtime", ex);
            }
        }

        /// <summary>
        /// Install Windows App Runtime via winget or open browser
        /// </summary>
        private async Task InstallWindowsAppRuntimeAsync()
        {
            try
            {
                if (await IsWingetAvailableAsync())
                {
                    // Find the best available Windows App Runtime package (2.0.1 or higher)
                    var packageId = await FindBestWingetPackageAsync("Microsoft.WindowsAppRuntime", new Version(2, 0, 1));
                    
                    if (packageId != null)
                    {
                        Logger.LogInfo($"Installing Windows App Runtime via winget: {packageId}");
                        
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c start cmd /k \"winget install {packageId} && pause\"",
                            UseShellExecute = true,
                            CreateNoWindow = false
                        };
                        System.Diagnostics.Process.Start(psi);
                        return;
                    }
                    else
                    {
                        Logger.LogInfo("Windows App Runtime 2.0.1+ not found in winget, falling back to browser");
                    }
                }
                else
                {
                    Logger.LogInfo("Winget not available, opening browser for Windows App SDK download");
                }
                
                // Fallback to browser
                var browserPsi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(browserPsi);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to install Windows App Runtime", ex);
            }
        }

        private async void ReloadModsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ReloadModsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ReloadModsButton_Click", ex);
            }
        }

        public async Task ReloadModsAsync()
        {
            // Show loading window during refresh
            var loadingWindow = new LoadingWindow();
            loadingWindow.Activate();
            
            await Task.Run(async () =>
            {
                try
                {
                    loadingWindow.UpdateStatus("Refreshing manager...");
                    await Task.Delay(100);
                    
                    // CRITICAL: Rebuild mod lists first to pick up new/changed mods
                    loadingWindow.UpdateStatus("Rebuilding mod lists...");
                    LogToGridLog("REFRESH: Rebuilding mod lists");
                    ModListManager.RebuildAllLists();
                    LogToGridLog("REFRESH: Mod lists rebuilt");
                    await Task.Delay(100);
                    
                    // Update mod.json files with namespace info (for new/updated mods)
                    loadingWindow.UpdateStatus("Scanning mod configurations...");
                    LogToGridLog("REFRESH: Updating mod.json files with namespace info");
                    var app = App.Current as App;
                    if (app != null)
                    {
                        await app.EnsureModJsonInXXMIMods();
                    }
                    LogToGridLog("REFRESH: Mod.json files updated");
                    await Task.Delay(100);
                    
                    // Preload images again
                    loadingWindow.UpdateStatus("Reloading images...");
                    await PreloadModImages(loadingWindow);
                    
                    loadingWindow.UpdateStatus("Finalizing refresh...");
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error during refresh", ex);
                }
            });
            
            // Update UI on main thread
            this.DispatcherQueue.TryEnqueue(() =>
            {
                SetSearchBoxPlaceholder();
                SetFooterMenuTranslations();
                UpdateGameSelectionComboBoxTexts();
                
                // Force menu regeneration even if suppressed (for reload after category creation)
                var wasSuppressed = _suppressMenuRegeneration;
                _suppressMenuRegeneration = false;
                _ = GenerateModCharacterMenuAsync();
                _suppressMenuRegeneration = wasSuppressed;
                
                // Refresh ModGridPage if it's currently loaded
                if (contentFrame.Content is Pages.ModGridPage modGridPage)
                {
                    // Reload current view to show updated mods
                    if (modGridPage.CurrentViewMode == Pages.ModGridPage.ViewMode.Categories)
                    {
                        Logger.LogInfo("Refreshing categories view after reload");
                        modGridPage.LoadCategories();
                    }
                    else
                    {
                        Logger.LogInfo("Refreshing mods view after reload");
                        modGridPage.LoadAllMods();
                    }
                }
                
                // No symlink recreation needed - using DISABLED_ prefix system
                Logger.LogInfo("Mod state maintained during manager reload");
                
                // Update All Mods button state after reload
                UpdateAllModsButtonState();
                
                nvSample.SelectedItem = null; // Unselect active button
                
                // Save current position before restoring (to handle category mode properly)
                SaveCurrentPositionBeforeReload();
                
                // Restore last position instead of always going to All Mods
                RestoreLastPosition();
                
                // Auto-detect hotkeys for all mods in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        Logger.LogInfo("Starting full hotkey detection after reload");
                        await Pages.HotkeyFinderPage.RefreshAllModsHotkeysStaticAsync();
                        Logger.LogInfo("Completed full hotkey detection after reload");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to auto-detect hotkeys after reload", ex);
                    }
                });
                
                loadingWindow.Close();
            });
        }

        private void AllModsButton_Click(object sender, RoutedEventArgs e)
        {
            // Unselect selected menu item
            nvSample.SelectedItem = null;
            
            // Use ViewMode from settings to determine navigation
            bool isCategoriesView = SettingsManager.Current.ViewMode == "Categories";
            
            // Save position for reload
            if (isCategoriesView)
            {
                SettingsManager.SaveLastPosition(null, "Categories", "Categories");
            }
            else
            {
                SettingsManager.SaveLastPosition(null, "ModGridPage", "Mods");
            }
            
            // Always navigate to ModGridPage with appropriate parameter
            if (isCategoriesView)
            {
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Categories", new DrillInNavigationTransitionInfo());
            }
            else
            {
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
            }
            
            // Update context menu after navigation (will be handled by contentFrame.Navigated event)
        }

        private void ViewModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle view mode and update icon
            if (ViewModeToggleButton?.Content is FontIcon icon)
            {
                bool isCurrentlyCategories = icon.Glyph == "\uE8B3";
                
                if (isCurrentlyCategories)
                {
                    // Switch to mods view
                    icon.Glyph = "\uE8A9";
                    
                    // Save view mode to settings
                    SettingsManager.Current.ViewMode = "Mods";
                    SettingsManager.Save();
                    SettingsManager.Load(); // Force reload
                    
                    // Update UI based on settings
                    bool isCategoriesView = SettingsManager.Current.ViewMode == "Categories";
                    UpdateAllModsButtonText(isCategoriesView);
                    UpdateViewModeTooltip(isCategoriesView);
                    
                    // Refresh navigation menu to reload category icons and hover previews
                    _ = GenerateModCharacterMenuAsync();
                    
                    // Update menu items enabled state with a small delay to ensure menu is fully loaded
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(100); // Small delay to ensure menu items are loaded
                        UpdateMenuItemsEnabledState(isCategoriesView);
                    });
                    
                    // Navigate to ModGridPage if not already there, or change mode if already there
                    if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                    {
                        modGridPage.CurrentViewMode = FlairX_Mod_Manager.Pages.ModGridPage.ViewMode.Mods;
                    }
                    else
                    {
                        // Navigate to ModGridPage with all mods
                        contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
                    }
                }
                else
                {
                    // Switch to categories view
                    icon.Glyph = "\uE8B3";
                    
                    // Save view mode to settings
                    SettingsManager.Current.ViewMode = "Categories";
                    SettingsManager.Save();
                    SettingsManager.Load(); // Force reload
                    
                    // Update UI based on settings
                    bool isCategoriesView = SettingsManager.Current.ViewMode == "Categories";
                    UpdateAllModsButtonText(isCategoriesView);
                    UpdateViewModeTooltip(isCategoriesView);
                    
                    // Refresh navigation menu to reload category icons and hover previews
                    _ = GenerateModCharacterMenuAsync();
                    
                    UpdateMenuItemsEnabledState(isCategoriesView);
                    
                    // Navigate to ModGridPage if not already there, or change mode if already there
                    if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
                    {
                        modGridPage.CurrentViewMode = FlairX_Mod_Manager.Pages.ModGridPage.ViewMode.Categories;
                    }
                    else
                    {
                        // Navigate to ModGridPage with categories
                        contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Categories", new DrillInNavigationTransitionInfo());
                    }
                }
                
                // Unselect menu item since we're going to all mods/categories
                nvSample.SelectedItem = null;
            }
        }

        private void GameSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ignore selection changes during initialization
            if (!_isInitializationComplete)
            {
                Logger.LogDebug("GameSelectionComboBox_SelectionChanged: Ignoring event during initialization");
                return;
            }
            
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                // Get game tag from the selected item's Tag property
                string gameTag = selectedItem.Tag?.ToString() ?? "";
                int selectedIndex = SettingsManager.GetIndexFromGameTag(gameTag);
                
                // Skip if same game index
                if (selectedIndex == SettingsManager.Current.SelectedGameIndex)
                    return;
                
                bool gameSelected = selectedIndex > 0;
                
                if (!gameSelected)
                {
                    Logger.LogDebug("Switching to no game selected - clearing paths and disabling UI");
                    UpdateUIForGameSelection(false); // Disable UI first
                }
                else
                {
                    Logger.LogDebug($"Switching to game index {selectedIndex} (tag: {gameTag})");
                    UpdateUIForGameSelection(true); // Enable UI
                }
                
                // No symlink cleanup needed - using DISABLED_ prefix system
                Logger.LogDebug("Switching games - no cleanup needed...");
                
                // Switch game paths using index
                SettingsManager.SwitchGame(selectedIndex);
                
                // Only restart StatusKeeper watcher if a game is selected
                if (gameSelected && SettingsManager.Current.StatusKeeperDynamicSyncEnabled)
                {
                    Logger.LogDebug("Restarting StatusKeeper watcher for new game...");
                    FlairX_Mod_Manager.Pages.StatusKeeperSyncPage.StopWatcherStatic();
                    FlairX_Mod_Manager.Pages.StatusKeeperSyncPage.StartWatcherStatic();
                }
                else if (!gameSelected)
                {
                    // Stop StatusKeeper watcher when no game is selected
                    FlairX_Mod_Manager.Pages.StatusKeeperSyncPage.StopWatcherStatic();
                }
                
                // Handle navigation based on game selection
                if (gameSelected)
                {
                    // Ensure mod.json files exist in the new game's directory
                    _ = (App.Current as App)?.EnsureModJsonInXXMIMods();
                    
                    // Create default preset for the new game if it doesn't exist
                    var gridPage = new FlairX_Mod_Manager.Pages.ModGridPage();
                    gridPage.SaveDefaultPresetAllInactive();
                    
                    // Regenerate character menu for the new game
                    Logger.LogDebug("Regenerating character menu for new game...");
                    _ = GenerateModCharacterMenuAsync();
                    
                    // No symlink recreation needed - using DISABLED_ prefix system
                    Logger.LogDebug("Mod state maintained for new game...");
                    
                    // Always navigate to All Mods when a game is selected
                    // This handles all cases including ModDetailPage (since the current mod might not exist in new game)
                    Logger.LogDebug("Game selected - navigating to All Mods view");
                    AllModsButton_Click(AllModsButton, new RoutedEventArgs());
                    
                    // Show Starter Pack dialog if available and not dismissed
                    _ = ShowStarterPackDialogIfNeededAsync(gameTag);
                }
                else
                {
                    // No game selected - navigate to welcome page
                    Logger.LogDebug("No game selected - navigating to welcome page");
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.WelcomePage));
                    
                    // Clear navigation selection
                    nvSample.SelectedItem = null;
                }
                
                Logger.LogDebug($"Game switched successfully. New paths:");
                Logger.LogDebug($"  Mods: '{SettingsManager.GetCurrentXXMIModsDirectory()}'");
                Logger.LogDebug($"  D3DX INI: '{SettingsManager.Current.StatusKeeperD3dxUserIniPath}'");
            }
        }



        // OpenModLibraryButton removed - mods are now in XXMI/Mods directly
        
        private void OpenGameBananaBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var gameTag = SettingsManager.CurrentSelectedGame;
                if (string.IsNullOrEmpty(gameTag))
                {
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    ShowInfoBar(
                        SharedUtilities.GetTranslation(lang, "Error"),
                        "Please select a game first.",
                        InfoBarSeverity.Warning
                    );
                    return;
                }

                ShowGameBananaBrowserPanel(gameTag);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error opening GameBanana browser", ex);
            }
        }

        private async void RestartAppButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "SettingsPage_RestartApp_Title"),
                    Content = SharedUtilities.GetTranslation(lang, "SettingsPage_RestartApp_Content"),
                    PrimaryButtonText = SharedUtilities.GetTranslation(lang, "SettingsPage_RestartApp_Confirm"),
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "Cancel"),
                    XamlRoot = this.Content.XamlRoot
                };
                
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    RestartApplication();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in RestartAppButton_Click", ex);
            }
        }

        private void RestartApplication()
        {
            try
            {
                // Try multiple methods to get the executable path
                string? exePath = null;
                
                // Method 1: Get from current process
                try
                {
                    exePath = Process.GetCurrentProcess().MainModule?.FileName;
                }
                catch { }
                
                // Method 2: Get from Environment
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Environment.ProcessPath;
                }
                
                // Method 3: Get from AppDomain
                if (string.IsNullOrEmpty(exePath))
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var possibleExe = Path.Combine(baseDir, "FlairX Mod Manager.exe");
                    if (File.Exists(possibleExe))
                    {
                        exePath = possibleExe;
                    }
                }
                
                Logger.LogInfo($"Restart: Executable path = {exePath}");
                
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    // Get current process ID
                    var currentProcessId = Process.GetCurrentProcess().Id;
                    
                    // Use PowerShell to restart - kill immediately and start new instance
                    var psCommand = $"Stop-Process -Id {currentProcessId} -Force; Start-Process -FilePath '{exePath}'";
                    
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{psCommand}\"",
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    
                    Logger.LogInfo($"Restart: Starting restart script (PID: {currentProcessId})...");
                    Process.Start(psi);
                    
                    Logger.LogInfo($"Restart: PowerShell script started, restarting now...");
                }
                else
                {
                    Logger.LogError($"Restart: Could not find executable at path: {exePath}");
                    
                    // Show error to user
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    ShowErrorInfo("Could not restart - executable not found", 3000);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error restarting application", ex);
                
                // Try fallback with cmd
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var exePath = Path.Combine(baseDir, "FlairX Mod Manager.exe");
                    
                    if (File.Exists(exePath))
                    {
                        // Use cmd to start the app after a delay
                        var psi = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c timeout /t 1 /nobreak >nul && start \"\" \"{exePath}\"",
                            UseShellExecute = true,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        Process.Start(psi);
                        Application.Current.Exit();
                    }
                }
                catch (Exception ex2)
                {
                    Logger.LogError("Fallback restart also failed", ex2);
                }
            }
        }

        private async void LauncherFabBorder_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Logger.LogInfo("LauncherFabBorder_PointerPressed called");
            try
            {
                var exePath = GetXXMILauncherPath();
                if (File.Exists(exePath))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(exePath) ?? PathManager.GetAbsolutePath(".")
                    };

                    // Check if Skip XXMI Launcher is enabled
                    Logger.LogInfo($"Skip XXMI Launcher setting: {SettingsManager.Current.SkipXXMILauncherEnabled}");
                    Logger.LogInfo($"Selected game index: {SettingsManager.Current.SelectedGameIndex}");
                    
                    if (SettingsManager.Current.SkipXXMILauncherEnabled)
                    {
                        // Get current game tag (button is only active when game is selected)
                        string gameTag = SettingsManager.GetGameTagFromIndex(SettingsManager.Current.SelectedGameIndex);
                        psi.Arguments = $"--nogui --xxmi {gameTag}";
                        Logger.LogInfo($"Launching XXMI Launcher with arguments: {psi.Arguments}");
                    }
                    else
                    {
                        Logger.LogInfo("Launching XXMI Launcher without arguments (normal GUI mode)");
                    }

                    using var process = System.Diagnostics.Process.Start(psi);
                }
                else
                {
                    await ShowXXMIDownloadDialogAsync(exePath);
                }
            }
            catch (Exception ex)
            {
                ShowErrorInfo(ex.Message);
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
            };

            await dialog.ShowAsync();
            
            if (downloadSuccess)
            {
                ShowSuccessInfo(SharedUtilities.GetTranslation(_lang, "XXMI_Download_Success") ?? "XXMI Launcher installed successfully!", 3000);
            }
        }

        private void LauncherFabBorder_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            LauncherFabIcon.Glyph = "\uF5B0";
        }

        private void LauncherFabBorder_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            LauncherFabIcon.Glyph = "\uE768";
        }

        private void LauncherFabBorder_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Don't handle wheel events - let them bubble through to the page content below
            e.Handled = false;
        }

        private void ZoomIndicatorBorder_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Don't handle wheel events - let them bubble through to the page content below
            e.Handled = false;
        }

        public void UpdateZoomIndicator(double zoomLevel)
        {
            if (ZoomIndicatorText != null && ZoomIndicatorBorder != null)
            {
                ZoomIndicatorText.Text = $"{(int)(zoomLevel * 100)}%";
                
                // Only show zoom indicator if zoom is enabled in settings
                bool zoomEnabled = SettingsManager.Current.ModGridZoomEnabled;
                bool gameSelected = SettingsManager.Current.SelectedGameIndex > 0;
                
                // Hide indicator if zoom is disabled, no game selected, or at 100% zoom
                bool shouldShow = zoomEnabled && gameSelected && Math.Abs(zoomLevel - 1.0) >= 0.001;
                
                ZoomIndicatorBorder.Visibility = shouldShow ? 
                    Microsoft.UI.Xaml.Visibility.Visible : 
                    Microsoft.UI.Xaml.Visibility.Collapsed;
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
                    await ReloadModsAsync();
                };
                
                var result = await dialog.ShowAsync();
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