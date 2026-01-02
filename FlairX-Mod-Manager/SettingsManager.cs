using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FlairX_Mod_Manager
{
    public class Settings
    {
        public string? LanguageFile { get; set; }
        public string? XXMIRootDirectory { get; set; } = @".\XXMI"; // Root XXMI directory
        
        // Per-game path settings
        public Dictionary<string, string> GameXXMIRootPaths { get; set; } = new Dictionary<string, string>();
        public string? Theme { get; set; } = "Auto";
        public string? BackdropEffect { get; set; } = "Mica";
        public string PreviewEffect { get; set; } = "None"; // None, GlassReflection
        public bool DynamicModSearchEnabled { get; set; } = true;
        public bool GridLoggingEnabled { get; set; } = false;
        public bool ErrorOnlyLogging { get; set; } = false; // Log only errors to file
        public bool MinimizeToTrayEnabled { get; set; } = false;
        public bool HotkeysEnabled { get; set; } = true;
        public bool ShowOrangeAnimation { get; set; } = true;
        public int SelectedPresetIndex { get; set; } = 0; // 0 = default
        
        // Game Selection
        public int SelectedGameIndex { get; set; } = 0; // 0 = no game selected, 1-5 = game indices
        
        // Window settings
        public double WindowWidth { get; set; } = 1300;
        public double WindowHeight { get; set; } = 720;
        public double WindowX { get; set; } = -1; // -1 means center on screen
        public double WindowY { get; set; } = -1; // -1 means center on screen
        public bool WindowMaximized { get; set; } = false;
        
        // Default resolution on start settings
        public bool UseDefaultResolutionOnStart { get; set; } = false;
        public int DefaultStartWidth { get; set; } = 1650;
        public int DefaultStartHeight { get; set; } = 820;
        
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
        
        // XXMI Launcher settings
        public bool SkipXXMILauncherEnabled { get; set; } = false;
        
        // View mode settings
        public string ViewMode { get; set; } = "Mods"; // "Mods" or "Categories"
        
        // Hotkey settings
        public string OptimizePreviewsHotkey { get; set; } = "Ctrl+O";
        public string ReloadManagerHotkey { get; set; } = "Ctrl+R";
        public string ShuffleActiveModsHotkey { get; set; } = "Ctrl+S";
        public string DeactivateAllModsHotkey { get; set; } = "Ctrl+D";
        public string ToggleOverlayHotkey { get; set; } = "Alt+W";
        
        // Overlay settings
        public string OverlayTheme { get; set; } = "Auto";
        public string OverlayBackdrop { get; set; } = "AcrylicThin";
        public int OverlayWidth { get; set; } = 1200;
        public int OverlayHeight { get; set; } = 720;
        public int OverlayX { get; set; } = -1; // -1 means center on screen
        public int OverlayY { get; set; } = -1;
        public bool SendF10OnOverlayClose { get; set; } = false; // Send F10 to reload mods when mod is toggled in overlay (requires admin)
        public bool RunAsAdminEnabled { get; set; } = false; // Run FlairX as administrator on startup
        
        // Gamepad settings
        public bool GamepadEnabled { get; set; } = false;
        public string GamepadToggleOverlayCombo { get; set; } = "XB Back+XB Start"; // Combo to toggle overlay (uses display names)
        public string GamepadFilterActiveCombo { get; set; } = "XB Back+XB A"; // Combo to filter active mods only
        public string FilterActiveHotkey { get; set; } = "Alt+A"; // Keyboard shortcut to filter active mods
        public bool GamepadUseLeftStick { get; set; } = false; // Use left analog stick for navigation
        public bool GamepadVibrateOnNavigation { get; set; } = false; // Vibrate when navigating
        public string GamepadSelectButton { get; set; } = "XB A";
        public string GamepadBackButton { get; set; } = "XB B";
        public string GamepadNextCategoryButton { get; set; } = "XB RB";
        public string GamepadPrevCategoryButton { get; set; } = "XB LB";
        
        // Gamepad D-Pad navigation
        public string GamepadDPadUp { get; set; } = "XB ↑";
        public string GamepadDPadDown { get; set; } = "XB ↓";
        public string GamepadDPadLeft { get; set; } = "XB ←";
        public string GamepadDPadRight { get; set; } = "XB →";
        
        // Gamepad stick navigation
        public string GamepadLeftStickUp { get; set; } = "XB L↑";
        public string GamepadLeftStickDown { get; set; } = "XB L↓";
        public string GamepadLeftStickLeft { get; set; } = "XB L←";
        public string GamepadLeftStickRight { get; set; } = "XB L→";
        
        // Navigation state persistence
        public string? LastSelectedCategory { get; set; }
        public string? LastSelectedPage { get; set; } = "ModGridPage";
        public string? LastViewMode { get; set; } // "Mods" or "Categories"
        public bool RememberLastPosition { get; set; } = true;
        
        // GameBanana settings
        public bool BlurNSFWThumbnails { get; set; } = true;
        
        // Image Optimizer settings
        public int ImageOptimizerJpegQuality { get; set; } = 80;
        public int ImageOptimizerThreadCount { get; set; } = 0; // 0 = auto-detect based on CPU cores
        public bool ImageOptimizerCreateBackups { get; set; } = false;
        public bool ImageOptimizerKeepOriginals { get; set; } = false;
        public string ImageCropType { get; set; } = "Center"; // Center, Smart, Entropy, Attention
        public bool PreviewBeforeCrop { get; set; } = false; // Show preview dialog before each crop
        public bool InspectThumbnailsOnly { get; set; } = false; // Only show crop inspection for minitile/catmini thumbnails
        public bool AutoCreateModThumbnails { get; set; } = false; // Auto-create mod thumbnails without manual selection/cropping
        public bool ImageOptimizerReoptimize { get; set; } = false; // Re-optimize already optimized files
        
        // Starter Pack settings (per-game, stored as comma-separated game tags that dismissed the dialog)
        public string StarterPackDismissedGames { get; set; } = ""; // e.g. "ZZMI,GIMI" - games where user clicked "No thanks" or checkbox
    }

    public static class SettingsManager
    {
        private static readonly string SettingsPath = PathManager.GetSettingsPath();
        public static Settings Current { get; private set; } = new Settings();

        public static void Load()
        {
            Logger.LogInfo($"Loading settings from: {PathManager.GetRelativePath(SettingsPath)}");
            
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsPath);
                    Logger.LogDebug($"Settings file size: {json.Length} characters");
                    
                    Current = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                    Logger.LogInfo($"Settings loaded successfully - Game: {GetGameTagFromIndex(Current.SelectedGameIndex)}, Theme: {Current.Theme}");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to deserialize settings file, using defaults", ex);
                    Current = new Settings();
                }
            }
            else
            {
                Logger.LogInfo("Settings file does not exist, creating with default values");
                Current = new Settings();
                Save(); // Create the file with defaults
            }
        }

        public static void Save()
        {
            Logger.LogDebug("Saving settings to file");
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    Logger.LogInfo($"Created settings directory: {PathManager.GetRelativePath(dir)}");
                }
                
                var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
                Logger.LogDebug($"Settings saved successfully - {json.Length} characters written");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save settings file", ex);
            }
        }

        public static void RestoreDefaults()
        {
            // Get current game tag to restore game-specific defaults
            string gameTag = GetGameTagFromIndex(Current.SelectedGameIndex);
            
            // Remove custom paths for current game to use defaults
            if (!string.IsNullOrEmpty(gameTag))
            {
                Current.GameXXMIRootPaths.Remove(gameTag);
            }
            
            Current.StatusKeeperD3dxUserIniPath = GetCurrentD3dxUserIniPath();
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
            Logger.LogInfo($"Switching to game: {gameTag} (index: {gameIndex})");
            
            Current.SelectedGameIndex = gameIndex;
            Current.StatusKeeperD3dxUserIniPath = GetCurrentD3dxUserIniPath();
            
            // Only create directories if a game is selected (index > 0)
            if (gameIndex > 0)
            {
                try
                {
                    string xxmiModsDir = GetCurrentXXMIModsDirectory();
                    
                    Logger.LogInfo($"Creating game directories - XXMI Mods: {xxmiModsDir}");
                    
                    if (!string.IsNullOrEmpty(xxmiModsDir))
                    {
                        System.IO.Directory.CreateDirectory(xxmiModsDir);
                        Logger.LogInfo("XXMI mods directory created");
                    }
                    
                    // Create presets directory for the selected game
                    string presetsPath = AppConstants.GameConfig.GetPresetsPath(gameTag);
                    if (!string.IsNullOrEmpty(presetsPath))
                    {
                        string fullPresetsPath = PathManager.GetAbsolutePath(presetsPath);
                        System.IO.Directory.CreateDirectory(fullPresetsPath);
                        Logger.LogInfo($"Presets directory created: {PathManager.GetRelativePath(fullPresetsPath)}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to create directories for game {gameTag}", ex);
                }
            }
            else
            {
                Logger.LogInfo("No game selected, skipping directory creation");
            }
            
            Save();
            Logger.LogInfo($"Game switch completed successfully to {gameTag}");
        }

        public static string XXMIModsDirectorySafe => GetCurrentXXMIModsDirectory();
        public static string CurrentSelectedGame => GetGameTagFromIndex(Current.SelectedGameIndex);
        
        // Get current XXMI mods directory with per-game support and fallback
        public static string GetCurrentXXMIModsDirectory()
        {
            string gameTag = GetGameTagFromIndex(Current.SelectedGameIndex);
            
            // If no game selected, return empty
            if (string.IsNullOrEmpty(gameTag))
                return string.Empty;
            
            // Check if we have a custom XXMI root path for this game
            if (Current.GameXXMIRootPaths.TryGetValue(gameTag, out string? customXXMIRoot) && 
                !string.IsNullOrEmpty(customXXMIRoot) && Directory.Exists(customXXMIRoot))
            {
                // Build mods path from custom XXMI root
                return Path.Combine(customXXMIRoot, gameTag, "Mods");
            }
            
            // Fallback to default game-specific path
            return AppConstants.GameConfig.GetModsPath(gameTag);
        }
        
        // Get current d3dx_user.ini path with per-game support and fallback
        public static string GetCurrentD3dxUserIniPath()
        {
            string gameTag = GetGameTagFromIndex(Current.SelectedGameIndex);
            
            // If no game selected, return empty
            if (string.IsNullOrEmpty(gameTag))
                return string.Empty;
            
            // Check if we have a custom XXMI root path for this game
            if (Current.GameXXMIRootPaths.TryGetValue(gameTag, out string? customXXMIRoot) && 
                !string.IsNullOrEmpty(customXXMIRoot) && Directory.Exists(customXXMIRoot))
            {
                // Build d3dx_user.ini path from custom XXMI root
                return Path.Combine(customXXMIRoot, gameTag, "d3dx_user.ini");
            }
            
            // Fallback to default game-specific path
            return AppConstants.GameConfig.GetD3dxUserIniPath(gameTag);
        }
        
        // Set XXMI root directory for current game
        public static void SetCurrentGameXXMIRoot(string xxmiRootPath)
        {
            string gameTag = GetGameTagFromIndex(Current.SelectedGameIndex);
            if (!string.IsNullOrEmpty(gameTag))
            {
                Current.GameXXMIRootPaths[gameTag] = xxmiRootPath;
                Save();
            }
        }
        
        // Get XXMI root directory for current game (for display purposes)
        public static string GetCurrentGameXXMIRoot()
        {
            string gameTag = GetGameTagFromIndex(Current.SelectedGameIndex);
            if (string.IsNullOrEmpty(gameTag))
                return Current.XXMIRootDirectory ?? @".\XXMI";
            
            if (Current.GameXXMIRootPaths.TryGetValue(gameTag, out string? customPath) && 
                !string.IsNullOrEmpty(customPath))
            {
                return customPath;
            }
            
            // Return default XXMI root
            return @".\XXMI";
        }
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
        
        // Navigation state management
        public static void SaveLastPosition(string? category, string? page, string? viewMode = null)
        {
            if (Current.RememberLastPosition)
            {
                Current.LastSelectedCategory = category;
                Current.LastSelectedPage = page;
                Current.LastViewMode = viewMode ?? Current.ViewMode;
                Save();
            }
        }
        
        public static (string? category, string? page, string? viewMode) GetLastPosition()
        {
            if (Current.RememberLastPosition)
            {
                // Use Current.ViewMode as fallback if LastViewMode is null
                var viewMode = Current.LastViewMode ?? Current.ViewMode;
                return (Current.LastSelectedCategory, Current.LastSelectedPage, viewMode);
            }
            return (null, "ModGridPage", Current.ViewMode);
        }
        
        public static void ClearLastPosition()
        {
            Current.LastSelectedCategory = null;
            Current.LastSelectedPage = "ModGridPage";
            Current.LastViewMode = null;
            Save();
        }
    }
}
