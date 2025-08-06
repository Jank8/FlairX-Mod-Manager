using System;
using System.IO;

namespace ZZZ_Mod_Manager_X
{
    /// <summary>
    /// Centralized path management using relative paths by default
    /// </summary>
    public static class PathManager
    {
        private static readonly string _baseDirectory = AppContext.BaseDirectory;
        
        // Core directories (relative paths)
        public const string SETTINGS_DIR = "Settings";
        public const string LANGUAGE_DIR = "Language";
        public const string MOD_LIBRARY_DIR = "ModLibrary";
        public const string XXMI_DIR = "XXMI";
        public const string ASSETS_DIR = "Assets";
        
        /// <summary>
        /// Gets absolute path from relative path
        /// </summary>
        public static string GetAbsolutePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return _baseDirectory;
                
            if (Path.IsPathRooted(relativePath))
                return relativePath; // Already absolute
                
            return Path.Combine(_baseDirectory, relativePath);
        }
        
        /// <summary>
        /// Gets relative path from absolute path
        /// </summary>
        public static string GetRelativePath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
                return string.Empty;
                
            if (!Path.IsPathRooted(absolutePath))
                return absolutePath; // Already relative
                
            try
            {
                return Path.GetRelativePath(_baseDirectory, absolutePath);
            }
            catch
            {
                return absolutePath; // Fallback to original
            }
        }
        
        /// <summary>
        /// Safely combines paths
        /// </summary>
        public static string CombinePath(params string[] paths)
        {
            if (paths == null || paths.Length == 0)
                return string.Empty;
                
            try
            {
                return Path.Combine(paths);
            }
            catch
            {
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Gets settings file path
        /// </summary>
        public static string GetSettingsPath(string fileName = AppConstants.SETTINGS_FILE)
        {
            return GetAbsolutePath(CombinePath(SETTINGS_DIR, fileName));
        }
        
        /// <summary>
        /// Gets language file path
        /// </summary>
        public static string GetLanguagePath(string fileName, string? subfolder = null)
        {
            if (string.IsNullOrEmpty(subfolder))
                return GetAbsolutePath(CombinePath(LANGUAGE_DIR, fileName));
            else
                return GetAbsolutePath(CombinePath(LANGUAGE_DIR, subfolder, fileName));
        }
        
        /// <summary>
        /// Gets mod library path
        /// </summary>
        public static string GetModLibraryPath(string? subPath = null)
        {
            var modLibPath = SettingsManager.Current?.ModLibraryDirectory ?? MOD_LIBRARY_DIR;
            
            if (string.IsNullOrEmpty(subPath))
                return GetAbsolutePath(modLibPath);
            else
                return GetAbsolutePath(CombinePath(modLibPath, subPath));
        }
        
        /// <summary>
        /// Gets XXMI mods path
        /// </summary>
        public static string GetXXMIModsPath(string? subPath = null)
        {
            var xxmiPath = SettingsManager.Current?.XXMIModsDirectory ?? AppConstants.DEFAULT_XXMI_MODS_PATH;
            
            if (string.IsNullOrEmpty(subPath))
                return GetAbsolutePath(xxmiPath);
            else
                return GetAbsolutePath(CombinePath(xxmiPath, subPath));
        }
        
        /// <summary>
        /// Gets game-specific ActiveMods file path
        /// </summary>
        public static string GetActiveModsPath()
        {
            var gameTag = SettingsManager.CurrentSelectedGame;
            var activeModsFileName = AppConstants.GameConfig.GetActiveModsFilename(gameTag);
            return GetSettingsPath(activeModsFileName);
        }
        
        /// <summary>
        /// Validates path for security
        /// </summary>
        public static bool IsPathSafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
                
            try
            {
                // Check for path traversal attempts
                if (path.Contains("..") || path.Contains("~"))
                    return false;
                    
                // Check for invalid characters
                var invalidChars = Path.GetInvalidPathChars();
                if (path.IndexOfAny(invalidChars) >= 0)
                    return false;
                    
                // Ensure resolved path is within base directory
                var fullPath = GetAbsolutePath(path);
                return fullPath.StartsWith(_baseDirectory, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Creates directory if it doesn't exist
        /// </summary>
        public static bool EnsureDirectoryExists(string path)
        {
            try
            {
                var absolutePath = GetAbsolutePath(path);
                if (!Directory.Exists(absolutePath))
                {
                    Directory.CreateDirectory(absolutePath);
                    Logger.LogInfo($"Created directory: {GetRelativePath(absolutePath)}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create directory: {path}", ex);
                return false;
            }
        }
    }
}