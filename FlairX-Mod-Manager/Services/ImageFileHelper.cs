using System;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Shared image-file predicates used by the optimizer, installer and author updater.
    /// Kept in one place so the extension list cannot drift between features.
    /// </summary>
    public static class ImageFileHelper
    {
        public static bool IsImageFile(string path)
        {
            return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMinitileFile(string fileName)
        {
            var name = System.IO.Path.GetFileName(fileName).ToLowerInvariant();
            return name == "minitile.jpg" || name == "minitile.webp";
        }
    }
}

