using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Single implementation of "delete with Undo" used by the optimizer (silent, falls
    /// back to permanent delete) and the mod grid (interactive; reports failure).
    /// </summary>
    public static class RecycleBinHelper
    {
        private const uint FO_DELETE = 0x0003;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOCONFIRMATION = 0x0010;
        private const ushort FOF_SILENT = 0x0004;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        public static bool MoveToRecycleBin(string path, bool silent = true, bool fallbackToDelete = true)
        {
            var flags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION);
            if (silent)
            {
                flags |= FOF_SILENT;
            }

            var shf = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = path + '\0' + '\0',
                fFlags = flags
            };

            int result = SHFileOperation(ref shf);
            if (result == 0)
            {
                Logger.LogInfo($"Moved to recycle bin: {Path.GetFileName(path)}");
                return true;
            }

            if (fallbackToDelete)
            {
                Logger.LogWarning($"Failed to move to recycle bin (error {result}), deleting permanently: {path}");
                File.Delete(path);
                return true;
            }

            throw new IOException($"Failed to move to recycle bin. Error code: {result}");
        }
    }
}
