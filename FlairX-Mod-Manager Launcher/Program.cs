using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

// Hide console window
if (OperatingSystem.IsWindows())
{
    var handle = GetConsoleWindow();
    if (handle != IntPtr.Zero)
    {
        ShowWindow(handle, 0); // SW_HIDE = 0
    }
}

try
{
    // Get launcher directory (use ProcessPath for single-file exe)
    var launcherExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
    if (string.IsNullOrEmpty(launcherExePath))
    {
        return;
    }
    
    var launcherDir = Path.GetDirectoryName(launcherExePath);
    if (string.IsNullOrEmpty(launcherDir))
    {
        return;
    }
    
    var exePath = Path.Combine(launcherDir, "app", "FlairX Mod Manager.exe");
    
    // Check if main exe exists
    if (!File.Exists(exePath))
    {
        return;
    }
    
    var workingDir = Path.GetDirectoryName(exePath);
    if (string.IsNullOrEmpty(workingDir))
    {
        return;
    }

    // Launch main application
    var startInfo = new ProcessStartInfo
    {
        FileName = exePath,
        UseShellExecute = true,
        WorkingDirectory = workingDir,
        CreateNoWindow = true
    };
    
    var process = Process.Start(startInfo);
    
    // Wait a moment to ensure main app started successfully
    if (process != null)
    {
        System.Threading.Thread.Sleep(500);
        
        // Check if main process is still running
        if (!process.HasExited)
        {
            // Main app is running, launcher can exit
            return;
        }
    }
}
catch
{
    // Silent fail
}

// Win32 API imports
[DllImport("kernel32.dll")]
static extern IntPtr GetConsoleWindow();

[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
