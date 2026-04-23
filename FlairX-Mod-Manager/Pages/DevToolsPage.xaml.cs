using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class DevToolsPage : Page
    {
        public DevToolsPage()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => RefreshDiagnostics();
        }

        private void RefreshDiagnostics()
        {
            try
            {
                DiagVersion.Text = AppConstants.APP_VERSION;
                DiagGame.Text = SettingsManager.CurrentSelectedGame ?? "None";

                var allMods = ModListManager.GetAllMods();
                DiagModCount.Text = allMods.Count.ToString();
                DiagActiveCount.Text = allMods.Count(m => m.IsActive).ToString();

                var (imgCount, ramCount, imgMB, ramMB) = ImageCacheManager.GetCacheSizes();
                DiagImageCache.Text = $"{imgCount} items ({imgMB} MB)";
                DiagRamCache.Text = $"{ramCount} items ({ramMB} MB)";

                var memMB = GC.GetTotalMemory(false) / (1024 * 1024);
                DiagMemory.Text = $"{memMB} MB";

                // Paths
                var baseDir = System.IO.Path.GetFullPath(AppContext.BaseDirectory);
                PathAppDir.Text = baseDir;
                PathSettings.Text = System.IO.Path.GetFullPath(PathManager.GetSettingsPath(""));
                PathMods.Text = SettingsManager.GetCurrentXXMIModsDirectory() ?? "Not set";
                PathLog.Text = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "Settings", AppConstants.APPLICATION_LOG_FILE));
                PathLanguage.Text = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(AppContext.BaseDirectory, AppConstants.LANGUAGE_FOLDER));

                // Hotkeys
                HotkeysList.Children.Clear();
                if (App.Current is App app && app.MainWindow is MainWindow mw)
                {
                    var hotkeys = mw.GetRegisteredHotkeyInfo();
                    if (hotkeys.Count == 0)
                    {
                        HotkeysList.Children.Add(new TextBlock { Text = "No hotkeys registered", Opacity = 0.5 });
                    }
                    else
                    {
                        foreach (var (name, hotkey) in hotkeys)
                        {
                            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                            row.Children.Add(new TextBlock { Text = name, Width = 200, Opacity = 0.7 });
                            row.Children.Add(new TextBlock { Text = hotkey, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                            HotkeysList.Children.Add(row);
                        }
                    }
                }

                // Runtimes
                RefreshRuntimes();

                // GameBanana status
                var cookies = Services.CloudflareBypassService.GetCachedCookies();
                GBCookieStatus.Text = string.IsNullOrEmpty(cookies)
                    ? "None"
                    : $"Present ({cookies.Length} chars)";
                GBLoginPromptStatus.Text = SettingsManager.Current.CommentsLoginPromptDismissed
                    ? "Dismissed (won't show)"
                    : "Active (will show on first comments load)";
            }
            catch (Exception ex)
            {
                Logger.LogError("DevToolsPage: Failed to refresh diagnostics", ex);
            }
        }

        private void RefreshRuntimes()
        {
            RuntimeList.Children.Clear();

            void AddRow(string name, string version, bool ok)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                row.Children.Add(new FontIcon
                {
                    Glyph = ok ? "\uE73E" : "\uEA39",
                    FontSize = 14,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        ok ? Windows.UI.Color.FromArgb(255, 76, 175, 80)
                           : Windows.UI.Color.FromArgb(255, 244, 67, 54)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Children.Add(new TextBlock { Text = name, Width = 200, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(new TextBlock
                {
                    Text = version,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                });
                RuntimeList.Children.Add(row);
            }

            // .NET Runtime
            AddRow(".NET Runtime", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, true);

            // OS - get info quickly from registry and WMI
            try
            {
                var osArch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;
                string osInfo = "";
                
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                    if (key != null)
                    {
                        var productName = key.GetValue("ProductName")?.ToString() ?? "";
                        var displayVersion = key.GetValue("DisplayVersion")?.ToString() ?? "";
                        var currentBuild = key.GetValue("CurrentBuild")?.ToString() ?? "";
                        var ubr = key.GetValue("UBR")?.ToString() ?? "";
                        
                        if (!string.IsNullOrEmpty(productName))
                        {
                            osInfo = productName;
                            
                            if (!string.IsNullOrEmpty(displayVersion))
                            {
                                osInfo += $" {displayVersion}";
                            }
                            
                            if (!string.IsNullOrEmpty(currentBuild))
                            {
                                osInfo += $" (Build {currentBuild}";
                                if (!string.IsNullOrEmpty(ubr))
                                {
                                    osInfo += $".{ubr}";
                                }
                                osInfo += ")";
                            }
                            
                            osInfo += $" ({osArch})";
                        }
                    }
                }
                catch { }
                
                // Fallback to RuntimeInformation
                if (string.IsNullOrEmpty(osInfo))
                {
                    osInfo = $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} ({osArch})";
                }
                
                AddRow("OS", osInfo, true);
            }
            catch
            {
                AddRow("OS", $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} ({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture})", true);
            }

            // CPU
            try
            {
                var cpuName = GetCpuName();
                if (!string.IsNullOrEmpty(cpuName))
                {
                    AddRow("CPU", cpuName, true);
                }
            }
            catch { }

            // RAM
            try
            {
                var ramInfo = GetRamInfo();
                if (!string.IsNullOrEmpty(ramInfo))
                {
                    AddRow("RAM", ramInfo, true);
                }
            }
            catch { }

            // GPU
            try
            {
                var gpuName = GetGpuName();
                if (!string.IsNullOrEmpty(gpuName))
                {
                    AddRow("GPU", gpuName, true);
                }
            }
            catch { }

            // WebView2
            try
            {
                var wv2 = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
                AddRow("WebView2 Runtime", wv2, true);
            }
            catch
            {
                AddRow("WebView2 Runtime", "Not installed", false);
            }

            // Windows App Runtime
            try
            {
                var winAppVersion = GetWindowsAppRuntimeVersion();
                if (!string.IsNullOrEmpty(winAppVersion))
                {
                    AddRow("Windows App Runtime", winAppVersion, true);
                }
                else
                {
                    AddRow("Windows App Runtime", App.IsWindowsAppRuntimeMissing ? "Not installed" : "Packaged (unknown version)", !App.IsWindowsAppRuntimeMissing);
                }
            }
            catch
            {
                AddRow("Windows App Runtime", App.IsWindowsAppRuntimeMissing ? "Not installed" : "Packaged (unknown version)", !App.IsWindowsAppRuntimeMissing);
            }

            // 7-Zip (SharpSevenZip)
            try
            {
                // Check both x64 and x86 folders
                var baseDir = AppContext.BaseDirectory;
                var sevenZipPathX64 = System.IO.Path.Combine(baseDir, "x64", "7z.dll");
                var sevenZipPathX86 = System.IO.Path.Combine(baseDir, "x86", "7z.dll");
                
                // Check x64 version
                if (System.IO.File.Exists(sevenZipPathX64))
                {
                    var ver = System.Diagnostics.FileVersionInfo.GetVersionInfo(sevenZipPathX64);
                    AddRow("7-Zip (7z.dll) [x64]", ver.FileVersion ?? "Present", true);
                }
                else
                {
                    AddRow("7-Zip (7z.dll) [x64]", "Not found", false);
                }
                
                // Check x86 version
                if (System.IO.File.Exists(sevenZipPathX86))
                {
                    var ver = System.Diagnostics.FileVersionInfo.GetVersionInfo(sevenZipPathX86);
                    AddRow("7-Zip (7z.dll) [x86]", ver.FileVersion ?? "Present", true);
                }
                else
                {
                    AddRow("7-Zip (7z.dll) [x86]", "Not found", false);
                }
            }
            catch
            {
                AddRow("7-Zip (7z.dll)", "Error", false);
            }

            // NuGet packages - read from package-versions.json file
            RuntimeList.Children.Add(new TextBlock { Text = "NuGet Packages", Opacity = 0.5, FontSize = 11, Margin = new Thickness(0, 8, 0, 0) });

            var packageVersions = GetNuGetPackageVersionsFromJson();
            
            // Special handling for WindowsAppSDK - show both project and installed versions
            if (packageVersions.TryGetValue("Microsoft.WindowsAppSDK", out var projectVersion))
            {
                AddRow("Project App SDK", projectVersion, true);
            }
            else
            {
                AddRow("Project App SDK", "Not found", false);
            }
            
            var installedVersion = GetInstalledWindowsAppSDKVersion();
            if (!string.IsNullOrEmpty(installedVersion))
            {
                AddRow("Installed App SDK", installedVersion, true);
            }
            else
            {
                AddRow("Installed App SDK", "Not found", false);
            }
            
            // Other packages
            var otherPackages = new[]
            {
                "SharpSevenZip",
                "NLua",
                "CommunityToolkit.WinUI.Media"
            };

            foreach (var packageName in otherPackages)
            {
                if (packageVersions.TryGetValue(packageName, out var version))
                {
                    AddRow(packageName, version, true);
                }
                else
                {
                    AddRow(packageName, "Not found", false);
                }
            }
        }

        private Dictionary<string, string> GetNuGetPackageVersionsFromJson()
        {
            var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            try
            {
                var jsonPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Settings", "package-versions.json");
                
                if (!System.IO.File.Exists(jsonPath))
                {
                    Logger.LogWarning($"Could not find package-versions.json at: {jsonPath}");
                    return versions;
                }
                
                var jsonContent = System.IO.File.ReadAllText(jsonPath);
                var packageDict = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);
                
                if (packageDict != null)
                {
                    foreach (var kvp in packageDict)
                    {
                        versions[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to read NuGet package versions from package-versions.json", ex);
            }
            
            return versions;
        }

        private string? GetWindowsAppRuntimeVersion()
        {
            try
            {
                // Use PackageManager API instead of PowerShell (much faster)
                var packageManager = new Windows.Management.Deployment.PackageManager();
                var packages = packageManager.FindPackagesForUser(string.Empty, "Microsoft.WindowsAppRuntime.1.8");
                
                foreach (var package in packages)
                {
                    if (package.Id.Architecture == Windows.System.ProcessorArchitecture.X64)
                    {
                        var ver = package.Id.Version;
                        return $"{ver.Major}.{ver.Minor}.{ver.Build}.{ver.Revision}";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to check Windows App Runtime version via PackageManager: {ex.Message}");
            }
            
            // Fallback to PowerShell if PackageManager fails
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -Command \"Get-AppxPackage | Where-Object {$_.Name -eq 'Microsoft.WindowsAppRuntime.1.8' -and $_.Architecture -eq 'X64'} | Select-Object -First 1 -ExpandProperty Version\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = System.Diagnostics.Process.Start(psi);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        return output;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to check Windows App Runtime version: {ex.Message}");
            }
            
            return null;
        }

        private string? GetInstalledWindowsAppSDKVersion()
        {
            try
            {
                var nugetPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".nuget", "packages", "microsoft.windowsappsdk");
                
                if (!System.IO.Directory.Exists(nugetPath))
                {
                    return null;
                }
                
                // Get all version folders and find the latest
                var versions = System.IO.Directory.GetDirectories(nugetPath)
                    .Select(d => System.IO.Path.GetFileName(d))
                    .Where(v => !string.IsNullOrEmpty(v))
                    .OrderByDescending(v => v)
                    .FirstOrDefault();
                
                return versions;
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to check installed WindowsAppSDK version: {ex.Message}");
            }
            
            return null;
        }

        private string? GetCpuName()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                return key?.GetValue("ProcessorNameString")?.ToString()?.Trim();
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to get CPU name: {ex.Message}");
            }
            return null;
        }

        private string? GetRamInfo()
        {
            try
            {
                // Use Windows API to get memory info (fast, no PowerShell)
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    var totalGB = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    return $"{totalGB:F2} GB";
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to get RAM info: {ex.Message}");
            }
            return null;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] MEMORYSTATUSEX lpBuffer);

        private string? GetGpuName()
        {
            try
            {
                string? gpuName = null;
                string? vramSize = null;
                
                // Try to get GPU info from registry (faster)
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000");
                if (key != null)
                {
                    var driverDesc = key.GetValue("DriverDesc")?.ToString();
                    if (!string.IsNullOrEmpty(driverDesc))
                    {
                        gpuName = driverDesc.Trim();
                    }
                    
                    // Try to get VRAM size
                    var vramBytes = key.GetValue("HardwareInformation.qwMemorySize");
                    if (vramBytes != null && long.TryParse(vramBytes.ToString(), out var vramBytesValue))
                    {
                        var vramGB = vramBytesValue / (1024.0 * 1024.0 * 1024.0);
                        vramSize = $"{vramGB:F2} GB";
                    }
                }
                
                // Combine GPU name and VRAM
                if (!string.IsNullOrEmpty(gpuName))
                {
                    if (!string.IsNullOrEmpty(vramSize))
                    {
                        return $"{gpuName} ({vramSize})";
                    }
                    return gpuName;
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to get GPU name from registry: {ex.Message}");
            }
            
            // Fallback to PowerShell
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -Command \"(Get-CimInstance Win32_VideoController | Select-Object -First 1).Name\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = System.Diagnostics.Process.Start(psi);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        return output;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Failed to get GPU name: {ex.Message}");
            }
            return null;
        }

        private void RefreshDiag_Click(object sender, RoutedEventArgs e) => RefreshDiagnostics();

        private void ClearGBCookies_Click(object sender, RoutedEventArgs e)
        {
            Services.CloudflareBypassService.ClearCookies();
            SettingsManager.Current.CommentsLoginPromptDismissed = false;
            SettingsManager.Save();

            // Also clear WebView2 cache so session is fully gone
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var cacheDir = exePath + ".WebView2";
                    if (System.IO.Directory.Exists(cacheDir))
                    {
                        System.IO.Directory.Delete(cacheDir, recursive: true);
                        Logger.LogInfo($"WebView2 cache cleared alongside GB cookies: {cacheDir}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to clear WebView2 cache during cookie clear", ex);
            }

            RefreshDiagnostics();
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowSuccessInfo("GameBanana cookies & WebView2 cache cleared");
        }

        private void ResetGBLoginPrompt_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.CommentsLoginPromptDismissed = false;
            SettingsManager.Save();
            RefreshDiagnostics();
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowSuccessInfo("Login prompt reset — will show on next comments load");
        }

        private void ClearImageCache_Click(object sender, RoutedEventArgs e)
        {
            ImageCacheManager.ClearAllCaches();
            RefreshDiagnostics();
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowSuccessInfo("Image cache cleared");
        }

        private void ClearCategoryCache_Click(object sender, RoutedEventArgs e)
        {
            Services.GameBananaService.ClearCategoryTreeCache();
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowSuccessInfo("Category tree cache cleared");
        }

        private void RebuildModLists_Click(object sender, RoutedEventArgs e)
        {
            ModListManager.RebuildAllLists();
            RefreshDiagnostics();
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowSuccessInfo("Mod lists rebuilt");
        }

        private void OpenAppDir_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("explorer.exe", System.IO.Path.GetFullPath(AppContext.BaseDirectory));
        }

        private void OpenSettingsDir_Click(object sender, RoutedEventArgs e)
        {
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "Settings"));
            if (System.IO.Directory.Exists(path))
                System.Diagnostics.Process.Start("explorer.exe", path);
        }

        private void OpenModsDir_Click(object sender, RoutedEventArgs e)
        {
            var path = SettingsManager.GetCurrentXXMIModsDirectory();
            if (!string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path))
                System.Diagnostics.Process.Start("explorer.exe", path);
        }

        private void SendF10_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app && app.MainWindow is MainWindow mw)
            {
                mw.SendF10KeyPress();
                mw.ShowSuccessInfo("F10 sent");
            }
        }

        private void ForceGC_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            RefreshDiagnostics();
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowSuccessInfo("Garbage Collection completed");
        }

        private void DevTestSuccess_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowSuccessInfo("Mod installed successfully!");
        }

        private void DevTestWarning_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowWarningInfo("Something might be wrong with this operation.");
        }

        private void DevTestError_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowErrorInfo("Operation failed: could not complete the requested action.");
        }

        private void DevTestInfo_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowInfoBar("", "This is an informational notification.");
        }

        private void DevTestLong_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app && app.MainWindow is MainWindow mw)
                mw.ShowWarningInfo("This is a very long notification message that should wrap to multiple lines because it contains a lot of text that exceeds the maximum width of the popup notification bar.");
        }
    }
}
