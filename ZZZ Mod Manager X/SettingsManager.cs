using System;
using System.IO;
using System.Text.Json;

namespace ZZZ_Mod_Manager_X
{
    public class Settings
    {
        public string? LanguageFile { get; set; }
        public string? XXMIModsDirectory { get; set; } = AppConstants.DEFAULT_XXMI_MODS_PATH;
        public string? ModLibraryDirectory { get; set; } = AppConstants.DEFAULT_MOD_LIBRARY_PATH;
        public string? Theme { get; set; } = "Auto";
        public bool DynamicModSearchEnabled { get; set; } = true;
        public bool GridLoggingEnabled { get; set; } = false;
        public bool ShowOrangeAnimation { get; set; } = true;
        public int SelectedPresetIndex { get; set; } = 0; // 0 = default
        
        // Game Selection
        public string? SelectedGame { get; set; } = "ZZMI"; // Default to ZZZ
        
        // StatusKeeper settings
        public string StatusKeeperD3dxUserIniPath { get; set; } = AppConstants.DEFAULT_D3DX_USER_INI_PATH;
        public bool StatusKeeperDynamicSyncEnabled { get; set; } = false;
        public bool StatusKeeperLoggingEnabled { get; set; } = false;
        public bool StatusKeeperBackupConfirmed { get; set; } = false; // User confirms they made backups
        public bool StatusKeeperBackupOverride1Enabled { get; set; } = false;
        public bool StatusKeeperBackupOverride2Enabled { get; set; } = false;
        public bool StatusKeeperBackupOverride3Enabled { get; set; } = false;
        
        // Zoom settings
        public double ZoomLevel { get; set; } = 1.0;
        public bool ModGridZoomEnabled { get; set; } = false;
    }

    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(AppContext.BaseDirectory, AppConstants.SETTINGS_FOLDER, AppConstants.SETTINGS_FILE);
        public static Settings Current { get; private set; } = new Settings();

        public static void Load()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsPath);
                    Current = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
                    Current = new Settings();
                }
            }
        }

        public static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings save failed: {ex.Message}");
                // Settings save failed - not critical for app functionality
            }
        }

        public static void RestoreDefaults()
        {
            Current.XXMIModsDirectory = AppConstants.DEFAULT_XXMI_MODS_PATH;
            Current.ModLibraryDirectory = AppConstants.DEFAULT_MOD_LIBRARY_PATH;
            Current.StatusKeeperD3dxUserIniPath = AppConstants.DEFAULT_D3DX_USER_INI_PATH;
            Current.Theme = "Auto";
            Current.ShowOrangeAnimation = true;
            Current.SelectedPresetIndex = 0;
            Current.SelectedGame = "ZZMI";
            Save();
        }
        
        public static void SwitchGame(string gameTag)
        {
            Current.SelectedGame = gameTag;
            Current.XXMIModsDirectory = AppConstants.GameConfig.GetModsPath(gameTag);
            Current.ModLibraryDirectory = AppConstants.GameConfig.GetModLibraryPath(gameTag);
            Current.StatusKeeperD3dxUserIniPath = AppConstants.GameConfig.GetD3dxUserIniPath(gameTag);
            
            // Only create directories if a game is selected (not empty)
            if (!string.IsNullOrEmpty(gameTag))
            {
                try
                {
                    if (!string.IsNullOrEmpty(Current.XXMIModsDirectory))
                        System.IO.Directory.CreateDirectory(Current.XXMIModsDirectory);
                    if (!string.IsNullOrEmpty(Current.ModLibraryDirectory))
                        System.IO.Directory.CreateDirectory(Current.ModLibraryDirectory);
                    
                    // Create presets directory for the selected game
                    string presetsPath = AppConstants.GameConfig.GetPresetsPath(gameTag);
                    if (!string.IsNullOrEmpty(presetsPath))
                    {
                        string fullPresetsPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, presetsPath);
                        System.IO.Directory.CreateDirectory(fullPresetsPath);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create game directories: {ex.Message}");
                }
            }
            
            Save();
        }

        public static string XXMIModsDirectorySafe => Current.XXMIModsDirectory ?? string.Empty;
        public static string ModLibraryDirectorySafe => Current.ModLibraryDirectory ?? string.Empty;
        public static bool ShowOrangeAnimation
        {
            get => Current?.ShowOrangeAnimation ?? true;
            set
            {
                if (Current != null)
                {
                    Current.ShowOrangeAnimation = value;
                }
            }
        }
    }
}
