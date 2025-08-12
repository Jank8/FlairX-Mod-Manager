using System;
using System.IO;
using System.Text.Json;

namespace FlairX_Mod_Manager
{
    public class Settings
    {
        public string? LanguageFile { get; set; }
        public string? XXMIModsDirectory { get; set; } = AppConstants.DEFAULT_XXMI_MODS_PATH;
        public string? ModLibraryDirectory { get; set; } = AppConstants.DEFAULT_MOD_LIBRARY_PATH;
        public string? Theme { get; set; } = "Auto";
        public string? BackdropEffect { get; set; } = "AcrylicThin";
        public bool DynamicModSearchEnabled { get; set; } = true;
        public bool GridLoggingEnabled { get; set; } = false;
        public bool ShowOrangeAnimation { get; set; } = true;
        public int SelectedPresetIndex { get; set; } = 0; // 0 = default
        
        // Game Selection
        public int SelectedGameIndex { get; set; } = 0; // 0 = no game selected, 1-5 = game indices
        
        // Window settings
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public double WindowX { get; set; } = -1; // -1 means center on screen
        public double WindowY { get; set; } = -1; // -1 means center on screen
        public bool WindowMaximized { get; set; } = false;
        
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
        
        // Mod sorting settings
        public bool ActiveModsToTopEnabled { get; set; } = true;
        
        // View mode settings
        public string ViewMode { get; set; } = "Mods"; // "Mods" or "Categories"
    }

    public static class SettingsManager
    {
        private static readonly string SettingsPath = PathManager.GetSettingsPath();
        public static Settings Current { get; private set; } = new Settings();

        public static void Load()
        {
            System.Diagnostics.Debug.WriteLine($"SettingsManager.Load() called. Settings file path: {SettingsPath}");
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsPath);
                    System.Diagnostics.Debug.WriteLine($"Settings JSON content: {json}");
                    Current = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                    System.Diagnostics.Debug.WriteLine($"Settings loaded: SelectedGameIndex = {Current.SelectedGameIndex}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
                    Current = new Settings();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Settings file does not exist, using defaults");
                Current = new Settings();
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
            // Get current game tag to restore game-specific defaults
            string gameTag = GetGameTagFromIndex(Current.SelectedGameIndex);
            
            Current.XXMIModsDirectory = AppConstants.GameConfig.GetModsPath(gameTag);
            Current.ModLibraryDirectory = AppConstants.GameConfig.GetModLibraryPath(gameTag);
            Current.StatusKeeperD3dxUserIniPath = AppConstants.GameConfig.GetD3dxUserIniPath(gameTag);
            Current.Theme = "Auto";
            Current.ShowOrangeAnimation = true;
            Current.SelectedPresetIndex = 0;
            // Reset window state to defaults
            Current.WindowWidth = 1200;
            Current.WindowHeight = 800;
            Current.WindowX = -1; // Center on screen
            Current.WindowY = -1; // Center on screen
            Current.WindowMaximized = false;
            // Don't change SelectedGameIndex when restoring defaults - keep current game
            Save();
        }
        
        public static string GetGameTagFromIndex(int index)
        {
            return index switch
            {
                0 => "", // No game selected
                1 => "GIMI", // Genshin Impact
                2 => "HIMI", // Honkai Impact
                3 => "SRMI", // Star Rail
                4 => "WWMI", // Wuthering Waves
                5 => "ZZMI", // Zenless Zone Zero
                _ => ""
            };
        }

        public static int GetIndexFromGameTag(string gameTag)
        {
            return gameTag switch
            {
                "GIMI" => 1,
                "HIMI" => 2,
                "SRMI" => 3,
                "WWMI" => 4,
                "ZZMI" => 5,
                _ => 0
            };
        }

        public static void SwitchGame(int gameIndex)
        {
            string gameTag = GetGameTagFromIndex(gameIndex);
            System.Diagnostics.Debug.WriteLine($"SwitchGame: Setting SelectedGameIndex to {gameIndex} (tag: '{gameTag}')");
            Current.SelectedGameIndex = gameIndex;
            Current.XXMIModsDirectory = AppConstants.GameConfig.GetModsPath(gameTag);
            Current.ModLibraryDirectory = AppConstants.GameConfig.GetModLibraryPath(gameTag);
            Current.StatusKeeperD3dxUserIniPath = AppConstants.GameConfig.GetD3dxUserIniPath(gameTag);
            
            // Only create directories if a game is selected (index > 0)
            if (gameIndex > 0)
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
                        string fullPresetsPath = PathManager.GetAbsolutePath(presetsPath);
                        System.IO.Directory.CreateDirectory(fullPresetsPath);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create game directories: {ex.Message}");
                }
            }
            
            Save();
            System.Diagnostics.Debug.WriteLine($"SwitchGame: Saved settings with SelectedGameIndex = {Current.SelectedGameIndex}");
        }

        public static string XXMIModsDirectorySafe => Current.XXMIModsDirectory ?? string.Empty;
        public static string ModLibraryDirectorySafe => Current.ModLibraryDirectory ?? string.Empty;
        public static string CurrentSelectedGame => GetGameTagFromIndex(Current.SelectedGameIndex);
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
