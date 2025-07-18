using System;
using System.IO;
using System.Linq;

namespace ZZZ_Mod_Manager_X
{
    public static class PathValidator
    {
        private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
        private static readonly string[] DangerousPatterns = { "..", "~", "$" };

        /// <summary>
        /// Validates that a path is safe and doesn't contain path traversal attempts
        /// </summary>
        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                // Check for invalid characters
                if (path.Any(c => InvalidPathChars.Contains(c)))
                    return false;

                // Check for dangerous patterns
                if (DangerousPatterns.Any(pattern => path.Contains(pattern)))
                    return false;

                // Try to get full path - this will throw if path is invalid
                var fullPath = Path.GetFullPath(path);
                
                // Ensure the resolved path is within expected boundaries
                var basePath = Path.GetFullPath(AppContext.BaseDirectory);
                return fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates that a filename is safe
        /// </summary>
        public static bool IsValidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            return !fileName.Any(c => InvalidFileNameChars.Contains(c)) &&
                   !DangerousPatterns.Any(pattern => fileName.Contains(pattern));
        }

        /// <summary>
        /// Sanitizes a path by removing or replacing invalid characters
        /// </summary>
        public static string SanitizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            // Remove invalid characters
            foreach (var invalidChar in InvalidPathChars)
            {
                path = path.Replace(invalidChar, '_');
            }

            // Remove dangerous patterns
            foreach (var pattern in DangerousPatterns)
            {
                path = path.Replace(pattern, "_");
            }

            return path;
        }

        /// <summary>
        /// Safely combines paths and validates the result
        /// </summary>
        public static string? SafeCombine(string basePath, string relativePath)
        {
            try
            {
                if (!IsValidPath(basePath) || string.IsNullOrWhiteSpace(relativePath))
                    return null;

                var sanitizedRelativePath = SanitizePath(relativePath);
                var combinedPath = Path.Combine(basePath, sanitizedRelativePath);
                
                return IsValidPath(combinedPath) ? combinedPath : null;
            }
            catch
            {
                return null;
            }
        }
    }
}