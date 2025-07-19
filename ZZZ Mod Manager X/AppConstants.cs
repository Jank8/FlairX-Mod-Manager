namespace ZZZ_Mod_Manager_X
{
    public static class AppConstants
    {
        // File Extensions
        public const string MOD_JSON_FILENAME = "mod.json";
        public const string BACKUP_EXTENSION = ".msk";
        public const string INI_EXTENSION = ".ini";
        public const string JSON_EXTENSION = ".json";
        
        // Directory Names
        public const string SETTINGS_FOLDER = "Settings";
        public const string LANGUAGE_FOLDER = "Language";
        public const string MOD_LIBRARY_FOLDER = "ModLibrary";
        public const string XXMI_FOLDER = "XXMI";
        public const string ASSETS_FOLDER = "Assets";
        
        // File Names
        public const string SETTINGS_FILE = "Settings.json";
        public const string ACTIVE_MODS_FILE = "ActiveMods.json";
        public const string SYMLINK_STATE_FILE = "SymlinkState.json";
        public const string STATUS_KEEPER_LOG_FILE = "StatusKeeper.log";
        public const string APPLICATION_LOG_FILE = "Application.log";
        public const string D3DX_USER_INI_FILE = "d3dx_user.ini";
        
        // Default Paths
        public const string DEFAULT_XXMI_MODS_PATH = @".\XXMI\ZZMI\Mods";
        public const string DEFAULT_MOD_LIBRARY_PATH = @".\ModLibrary";
        public const string DEFAULT_D3DX_USER_INI_PATH = @".\XXMI\ZZMI\d3dx_user.ini";
        
        // UI Constants
        public const int MIN_WINDOW_WIDTH = 1280;
        public const int MIN_WINDOW_HEIGHT = 720;
        public const int MAX_WINDOW_WIDTH = 20000;
        public const int MAX_WINDOW_HEIGHT = 15000;
        public const int DEFAULT_WINDOW_WIDTH = 1650;
        public const int DEFAULT_WINDOW_HEIGHT = 820;
        
        // Cache Limits
        public const int MAX_IMAGE_CACHE_SIZE = 100;
        public const int MAX_RAM_IMAGE_CACHE_SIZE = 50;
        
        // Timing Constants
        public const int LOG_REFRESH_INTERVAL_MS = 3000;
        public const int PERIODIC_SYNC_INTERVAL_SECONDS = 10;
        public const int FILE_WATCHER_DELAY_MS = 100;
        
        // Regex Patterns
        public const string PERSISTENT_VARIABLE_PATTERN = @"^\$\\mods\\(.+\.ini)\\([^=]+?)\s*=\s*(.+)$";
        public const string VARIABLE_ASSIGNMENT_PATTERN = @"^(.*?\$([^=\s]+))\s*=\s*(.*)$";
        public const string CONSTANTS_VARIABLE_PATTERN = @"^(.+?)\s*=\s*(.*)$";
        
        // Section Names
        public const string CONSTANTS_SECTION = "constants";
        
        // Default JSON Content
        public const string DEFAULT_MOD_JSON = "{\n    \"author\": \"unknown\",\n    \"character\": \"!unknown!\",\n    \"url\": \"https://\",\n    \"hotkeys\": []\n}";
    }
}