using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

internal class Program
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    private const int SW_HIDE = 0;

    static void Main()
    {
        ShowWindow(GetConsoleWindow(), SW_HIDE);
        try
        {
            var exePath = Path.GetFullPath(@"app\FlairX Mod Manager.exe");
            var workingDir = Path.GetDirectoryName(exePath);
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                WorkingDirectory = workingDir
            });
        }
        catch (Exception)
        {
            // Jeśli chcesz, możesz dodać logowanie do pliku lub MessageBox
        }
    }
}
