using System;

namespace FlairX_Mod_Manager.Services
{
    public static class ModNameHelper
    {
        private const string DisabledPrefix = "DISABLED_";

        public static string GetCleanModName(string modFolderName)
        {
            if (modFolderName.StartsWith(DisabledPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return modFolderName.Substring(DisabledPrefix.Length);
            }

            return modFolderName;
        }
    }
}
