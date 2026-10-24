using System;
using System.Diagnostics;
using System.IO;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Opens a folder with the same command line the manager used before the refactor.
    /// The earlier COM/ShellExecute experiments changed the launch context and made
    /// explorer.exe fail with 0xc0000142, so keep the original direct launch.
    /// </summary>
    public static class FolderLaunchHelper
    {
        public static bool OpenFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return false;
            }

            try
            {
                // Paths built from the relative .\XXMI setting keep a ".\" segment.
                // Explorer is fine when the app is moved to a simple folder but can fail
                // to start when that relative segment is passed through the build tree.
                var fullPath = Path.GetFullPath(path);
                Logger.LogInfo($"Opening folder in explorer: {fullPath}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{fullPath}\"",
                    UseShellExecute = true
                });
                Logger.LogInfo($"Successfully opened folder: {fullPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to open folder: {path}", ex);
                return false;
            }
        }
    }
}
