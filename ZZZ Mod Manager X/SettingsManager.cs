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
        public bool DisableAllModsView { get; set; } = false;
        public bool ShowOrangeAnimation { get; set; } = true;
        public int SelectedPresetIndex { get; set; } = 0; // 0 = default
        
        // StatusKeeper settings
        public string StatusKeeperD3dxUserIniPath { get; set; } = AppConstants.DEFAULT_D3DX_USER_INI_PATH;
        public bool StatusKeeperDynamicSyncEnabled { get; set; } = false;
        public bool StatusKeeperLoggingEnabled { get; set; } = false;
        public bool StatusKeeperBackupConfirmed { get; set; } = false; // User confirms they made backups
        public bool StatusKeeperBackupOverride1Enabled { get; set; } = false;
        public bool StatusKeeperBackupOverride2Enabled { get; set; } = false;
        public bool StatusKeeperBackupOverride3Enabled { get; set; } = false;
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
