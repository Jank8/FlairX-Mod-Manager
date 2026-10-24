using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Anonymizes user-specific information in log messages and error reports.
    /// </summary>
    /// <remarks>
    /// Redaction used to run <c>message.Replace(Environment.UserName, ...)</c> for every
    /// character sequence, which corrupted normal words whenever the account name was short
    /// (an account called "G" turned "Loading" into "Loadin&lt;USER&gt;"). The account and machine
    /// names are now replaced only as standalone tokens, so short names are still hidden without
    /// damaging the surrounding text.
    /// </remarks>
    public static class PathAnonymizer
    {
        private static readonly string _userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private static readonly string _userName = Environment.UserName;
        private static readonly string _machineName = Environment.MachineName;

        /// <summary>
        /// Minimum length for free-text identifier redaction.
        /// </summary>
        /// <remarks>
        /// Short account names were previously skipped so they could not garble ordinary words,
        /// which meant a one-letter name was left unredacted in log text. The token boundaries in
        /// <see cref="BuildIdentifierRegex"/> are what prevent the corruption, so even a
        /// one-character name can be redacted safely.
        /// </remarks>
        private const int MinFreeTextIdentifierLength = 1;

        private const string UserPathReplacement = "\\Users\\<USER>";

        /// <summary>Matches any <c>X:\Users\name</c> or <c>\Users\name</c> segment.</summary>
        private static readonly Regex _userPathRegex = new Regex(
            @"([A-Za-z]:)?\\Users\\[^\\/:*?""<>|\r\n]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Matches the well-known per-user folders in a single pass.</summary>
        private static readonly Regex _commonUserDirRegex = new Regex(
            @"\\(AppData|Documents|Desktop|Downloads|Pictures|Videos|Music|OneDrive)\\",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex? _userNameRegex = BuildIdentifierRegex(_userName);
        private static readonly Regex? _machineNameRegex = BuildIdentifierRegex(_machineName);

        private static Regex? BuildIdentifierRegex(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < MinFreeTextIdentifierLength)
                return null;

            // Whole token only: never inside a longer word, never a drive letter.
            return new Regex(
                $@"(?<![A-Za-z0-9]){Regex.Escape(value)}(?![A-Za-z0-9:])",
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// Anonymizes user-specific information in a message.
        /// </summary>
        public static string Anonymize(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            var anonymized = message;

            // 1. The current user's profile folder, exact match (cheapest, most common).
            if (!string.IsNullOrEmpty(_userProfile))
            {
                anonymized = anonymized.Replace(_userProfile, "<USER_PROFILE>", StringComparison.OrdinalIgnoreCase);
            }

            // 2. Any remaining user folder, preserving the drive letter.
            if (anonymized.Contains("\\Users\\", StringComparison.OrdinalIgnoreCase))
            {
                anonymized = _userPathRegex.Replace(anonymized, static m =>
                    (m.Groups[1].Success ? m.Groups[1].Value : string.Empty) + UserPathReplacement);
            }

            // 3. Well-known per-user folders (single regex pass instead of seven Replace calls).
            anonymized = _commonUserDirRegex.Replace(anonymized,
                static m => "\\<" + m.Groups[1].Value.ToUpperInvariant() + ">\\");

            // 4. Standalone occurrences of the account/machine name (only for meaningful lengths).
            if (_userNameRegex != null && anonymized.Contains(_userName, StringComparison.OrdinalIgnoreCase))
            {
                anonymized = _userNameRegex.Replace(anonymized, "<USER>");
            }

            if (_machineNameRegex != null && anonymized.Contains(_machineName, StringComparison.OrdinalIgnoreCase))
            {
                anonymized = _machineNameRegex.Replace(anonymized, "<MACHINE>");
            }

            return anonymized;
        }

        /// <summary>
        /// Anonymizes an exception and all its inner exceptions.
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
        /// Gets anonymized app base directory for display.
        /// </summary>
        public static string GetAnonymizedBaseDirectory()
        {
            return Anonymize(AppContext.BaseDirectory);
        }
    }
}
