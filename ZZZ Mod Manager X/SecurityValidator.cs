using System;
using System.IO;
using System.Linq;

namespace ZZZ_Mod_Manager_X
{
    /// <summary>
    /// Security validation utilities
    /// </summary>
    public static class SecurityValidator
    {
        private static readonly string[] ReservedNames = 
        {
            "CON", "PRN", "AUX", "NUL", 
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };
        
        /// <summary>
        /// Validates that a directory name is safe for mod operations
        /// </summary>
        public static bool IsValidModDirectoryName(string? directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName))
            {
                Logger.LogWarning("Directory name is null or empty");
                return false;
            }

            try
            {
                // Check for path traversal attempts
                if (directoryName.Contains("..") || directoryName.Contains("/") || directoryName.Contains("\\"))
                {
                    Logger.LogWarning($"Directory name contains path traversal: {directoryName}");
                    return false;
                }

                // Check for absolute path attempts
                if (Path.IsPathRooted(directoryName))
                {
                    Logger.LogWarning($"Directory name is rooted path: {directoryName}");
                    return false;
                }

                // Check for invalid filename characters
                var invalidChars = Path.GetInvalidFileNameChars();
                if (directoryName.IndexOfAny(invalidChars) >= 0)
                {
                    Logger.LogWarning($"Directory name contains invalid characters: {directoryName}");
                    return false;
                }

                // Check for reserved names (Windows)
                if (ReservedNames.Contains(directoryName.ToUpperInvariant()))
                {
                    Logger.LogWarning($"Directory name is reserved: {directoryName}");
                    return false;
                }

                // Check length limits
                if (directoryName.Length > 255)
                {
                    Logger.LogWarning($"Directory name too long: {directoryName}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error validating directory name: {directoryName}", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Validates URL for safety
        /// </summary>
        public static bool IsValidUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;
                
            try
            {
                var uri = new Uri(url);
                return uri.Scheme == "http" || uri.Scheme == "https";
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Invalid URL: {url} - {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Sanitizes user input for safe file operations
        /// </summary>
        public static string SanitizeInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;
                
            try
            {
                // Remove dangerous characters
                var invalidChars = Path.GetInvalidFileNameChars();
                var sanitized = input;
                
                foreach (var invalidChar in invalidChars)
                {
                    sanitized = sanitized.Replace(invalidChar, '_');
                }
                
                // Remove path traversal attempts
                sanitized = sanitized.Replace("..", "_");
                sanitized = sanitized.Replace("~", "_");
                
                // Limit length
                if (sanitized.Length > 255)
                {
                    sanitized = sanitized.Substring(0, 255);
                }
                
                return sanitized;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error sanitizing input: {input}", ex);
                return "sanitized_input";
            }
        }
    }
}