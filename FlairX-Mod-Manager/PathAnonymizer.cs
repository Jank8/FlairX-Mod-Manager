using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Anonymizes user-specific paths in logs and error messages
    /// </summary>
    public static class PathAnonymizer
    {
        private static readonly string _userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private static readonly string _userName = Environment.UserName;
        private static readonly string _machineName = Environment.MachineName;
        
        // Regex patterns for common user paths
        private static readonly Regex _windowsUserPathRegex = new Regex(
            @"C:\\Users\\[^\\]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        private static readonly Regex _driveLetterPathRegex = new Regex(
            @"[A-Z]:\\Users\\[^\\]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        /// <summary>
        /// Anonymizes user-specific information in a message
        /// </summary>
        public static string Anonymize(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;
                
            var anonymized = message;
            
            // Replace user profile path
            if (!string.IsNullOrEmpty(_userProfile))
            {
                anonymized = anonymized.Replace(_userProfile, "<USER_PROFILE>", StringComparison.OrdinalIgnoreCase);
            }
            
            // Replace username
            if (!string.IsNullOrEmpty(_userName))
            {
                anonymized = anonymized.Replace(_userName, "<USER>", StringComparison.OrdinalIgnoreCase);
            }
            
            // Replace machine name
            if (!string.IsNullOrEmpty(_machineName))
            {
                anonymized = anonymized.Replace(_machineName, "<MACHINE>", StringComparison.OrdinalIgnoreCase);
            }
            
            // Replace any remaining C:\Users\<username> patterns
            anonymized = _windowsUserPathRegex.Replace(anonymized, "C:\\Users\\<USER>");
            
            // Replace any drive letter user paths (D:\Users\<username>, etc)
            anonymized = _driveLetterPathRegex.Replace(anonymized, "<DRIVE>:\\Users\\<USER>");
            
            // Replace common user directories
            anonymized = anonymized
                .Replace("\\AppData\\", "\\<APPDATA>\\", StringComparison.OrdinalIgnoreCase)
                .Replace("\\Documents\\", "\\<DOCUMENTS>\\", StringComparison.OrdinalIgnoreCase)
                .Replace("\\Desktop\\", "\\<DESKTOP>\\", StringComparison.OrdinalIgnoreCase)
                .Replace("\\Downloads\\", "\\<DOWNLOADS>\\", StringComparison.OrdinalIgnoreCase)
                .Replace("\\Pictures\\", "\\<PICTURES>\\", StringComparison.OrdinalIgnoreCase)
                .Replace("\\Videos\\", "\\<VIDEOS>\\", StringComparison.OrdinalIgnoreCase)
                .Replace("\\Music\\", "\\<MUSIC>\\", StringComparison.OrdinalIgnoreCase);
            
            return anonymized;
        }
        
        /// <summary>
        /// Anonymizes an exception and all its inner exceptions
        /// </summary>
        public static string AnonymizeException(Exception ex)
        {
            if (ex == null)
                return string.Empty;
                
            var message = Anonymize(ex.Message);
            var stackTrace = Anonymize(ex.StackTrace ?? string.Empty);
            var result = $"{message}\nStack Trace:\n{stackTrace}";
            
            if (ex.InnerException != null)
            {
                result += $"\n\nInner Exception:\n{AnonymizeException(ex.InnerException)}";
            }
            
            return result;
        }
        
        /// <summary>
        /// Gets anonymized app base directory for display
        /// </summary>
        public static string GetAnonymizedBaseDirectory()
        {
            return Anonymize(AppContext.BaseDirectory);
        }
    }
}
