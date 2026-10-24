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
        // Guards SyncPersistentVariables: serialises watcher, timer and manual passes.
        private static readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1);
        private static FileSystemWatcher? _fileWatcher;
        private static Timer? _periodicSyncTimer;
        private static Timer? _debounceTimer; // Debounce timer for file watcher
        private static readonly object _debounceLock = new object();
        private const int DEBOUNCE_DELAY_MS = 1000; // Wait 1 second after last file change

        // ==================== FILE WATCHER ====================
        
        public static void StartWatcherStatic()
        {
            if (_fileWatcher != null) return;

            var d3dxUserPath = GetD3dxUserPathStatic();
            if (string.IsNullOrEmpty(d3dxUserPath))
            {
                LogStatic("Cannot start watcher: d3dx_user.ini path not found", "ERROR");
                return;
            }

            LogStatic($"Starting file watcher for: {d3dxUserPath}");

            if (!File.Exists(d3dxUserPath))
            {
                LogStatic($"d3dx_user.ini not found for watcher at: {d3dxUserPath}", "ERROR");
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(d3dxUserPath);
                var fileName = Path.GetFileName(d3dxUserPath);

                _fileWatcher = new FileSystemWatcher(directory ?? "", fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Changed += async (sender, e) =>
                {
                    try
                    {
                        LogStatic("d3dx_user.ini changed, debouncing...");

                        // Debounce - reset timer on each change
                        lock (_debounceLock)
                        {
                            _debounceTimer?.Dispose();
                            _debounceTimer = new Timer(async _ =>
                            {
                                LogStatic("Debounce timer fired - starting auto-sync...");

                                try
                                {
                                    var result = await SyncPersistentVariables();
                                    var logMessage = $"Auto-sync completed: {result.updateCount} variables in {result.fileCount} files";
                                    if (result.lodSyncCount > 0)
                                    {
                                        logMessage += $", {result.lodSyncCount} LOD files synced";
                                    }
                                    LogStatic(logMessage);
                                }
                                catch (Exception error)
                                {
                                    LogStatic($"Auto-sync failed: {error.Message}", "ERROR");
                                }
                            }, null, DEBOUNCE_DELAY_MS, Timeout.Infinite);
                        }
                    }
                    catch (System.OperationCanceledException)
                    {
                        Logger.LogDebug("StatusKeeperSyncPage.StartWatcherStatic failed: canceled");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("StatusKeeperSyncPage.StartWatcherStatic failed", ex);
                    }
                };

                LogStatic($"✅ File watcher started successfully for: {d3dxUserPath}");
            }
            catch (Exception error)
            {
                LogStatic($"Failed to start file watcher: {error.Message}", "ERROR");
            }
        }

        public static void StopWatcherStatic()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
                LogStatic("✅ File watcher stopped");
            }
            
            // Stop debounce timer
            lock (_debounceLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }
        }
        
        private static void StopWatcher()
        {
            StopWatcherStatic();
        }

        public static void StartPeriodicSyncStatic()
        {
            if (_periodicSyncTimer != null) return;

            LogStatic("Starting periodic sync timer (every 10 seconds)...");

            _periodicSyncTimer = new Timer(async _ =>
            {
                try
                {
                    var d3dxUserPath = GetD3dxUserPathStatic();
                    if (string.IsNullOrEmpty(d3dxUserPath) || !File.Exists(d3dxUserPath))
                    {
                        return;
                    }

                    var result = await SyncPersistentVariables();

                    if (result.updateCount > 0 || result.lodSyncCount > 0)
                    {
                        var logMessage = $"Periodic sync completed: {result.updateCount} variables updated in {result.fileCount} files";
                        if (result.lodSyncCount > 0)
                        {
                            logMessage += $", {result.lodSyncCount} LOD files synced";
                        }
                        LogStatic(logMessage);
                    }
                }
                catch (Exception error)
                {
                    LogStatic($"Periodic sync failed: {error.Message}", "ERROR");
                }
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

            LogStatic("✅ Periodic sync timer started successfully");
        }

        public static void StopPeriodicSyncStatic()
        {
            if (_periodicSyncTimer != null)
            {
                _periodicSyncTimer.Dispose();
                _periodicSyncTimer = null;
                LogStatic("✅ Periodic sync timer stopped");
            }
        }
        
        private static void StopPeriodicSync()
        {
            StopPeriodicSyncStatic();
        }

        // Helper methods to call static watcher/sync methods from instance context
        private void StartWatcher()
        {
            StartWatcherStatic();
        }

        private void StartPeriodicSync()
        {
            StartPeriodicSyncStatic();
        }
    }
}
