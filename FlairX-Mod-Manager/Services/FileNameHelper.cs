using System;
using System.IO;
using System.Linq;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Windows-safe folder/file names shared by the installer and the author updater.
    /// </summary>
    public static class FileNameHelper
    {
        private static readonly string[] ReservedNames =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public static bool IsReservedWindowsName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var nameWithoutExt = name.Split('.')[0].ToUpperInvariant();
            return ReservedNames.Contains(nameWithoutExt);
        }

        public static string SanitizeFileName(string fileName)
        {
            return Sanitize(fileName, "Unnamed", 80, "_mod");
        }

        public static string SanitizeCategoryName(string categoryName)
        {
            return Sanitize(categoryName, "Other", 50, "_category");
        }

        private static string Sanitize(string value, string fallback, int maxLength, string reservedSuffix)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = string.Join(" ", value.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
            sanitized = new string(sanitized.Where(c => c >= 32).ToArray());

            while (sanitized.Contains("  "))
            {
                sanitized = sanitized.Replace("  ", " ");
            }

            sanitized = sanitized.Trim().TrimEnd('.');
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                return fallback;
            }

            if (sanitized.Length > maxLength)
            {
                sanitized = sanitized.Substring(0, maxLength).TrimEnd('.', ' ');
            }

            if (IsReservedWindowsName(sanitized))
            {
                sanitized += reservedSuffix;
            }

            return sanitized;
        }
    }
}
