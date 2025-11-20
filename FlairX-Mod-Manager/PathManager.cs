using System;
using System.IO;

namespace FlairX_Mod_Manager
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
        public const string XXMI_DIR = "XXMI";
        public const string ASSETS_DIR = "Assets";
        
        /// <summary>
        /// Gets absolute path from relative path
        /// </summary>
        public static string GetAbsolutePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return GetSafeBaseDirectory();
                
            if (Path.IsPathRooted(relativePath))
                return relativePath; // Already absolute
                
            return Path.Combine(GetSafeBaseDirectory(), relativePath);
        }
        
        /// <summary>
        /// Gets a safe base directory, avoiding system directories
        /// </summary>
        private static string GetSafeBaseDirectory()
        {
            var baseDir = _baseDirectory;
            
            // Check if base directory is in a system location
            var systemDirs = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            
            foreach (var systemDir in systemDirs)
            {
                if (!string.IsNullOrEmpty(systemDir) && 
                    baseDir.StartsWith(systemDir, StringComparison.OrdinalIgnoreCase))
                {
                    // If we're in a system directory, use Documents/FlairX-Mod-Manager instead
                    var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    return Path.Combine(documentsPath, "FlairX-Mod-Manager");
                }
            }
            
            return baseDir;
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
        /// Gets mods path (XXMI/Mods)
        /// </summary>
        public static string GetModsPath(string? subPath = null)
        {
            var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
            
            if (string.IsNullOrEmpty(subPath))
                return GetAbsolutePath(modsPath);
            else
                return GetAbsolutePath(CombinePath(modsPath, subPath));
        }
        
        // GetCategoryModPath removed - mods stored directly in XXMI/Mods
        
        /// <summary>
        /// Gets category from mod directory path
        /// </summary>
        public static string GetCategoryFromModPath(string modPath)
        {
            try
            {
                var modLibraryPath = GetModsPath();
                var relativePath = Path.GetRelativePath(modLibraryPath, modPath);
                var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                
                // Expected structure: [Category]/[ModName]
                if (pathParts.Length >= 2)
                {
                    return pathParts[0]; // Category is the first part
                }
                
                return "Other"; // Default category
            }
            catch
            {
                return "Other"; // Default category on error
            }
        }
        
        /// <summary>
        /// Gets XXMI mods path
        /// </summary>
        public static string GetXXMIModsPath(string? subPath = null)
        {
            var xxmiPath = SettingsManager.GetCurrentXXMIModsDirectory();
            
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