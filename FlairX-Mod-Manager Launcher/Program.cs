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

string? logPath = null;

try
{
    // Get launcher directory (use ProcessPath for single-file exe)
    var launcherExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
    if (string.IsNullOrEmpty(launcherExePath))
    {
        Log(logPath, "ERROR: Could not determine launcher exe path");
        return;
    }
    
    var launcherDir = Path.GetDirectoryName(launcherExePath);
    if (string.IsNullOrEmpty(launcherDir))
    {
        Log(logPath, "ERROR: Could not determine launcher directory");
        return;
    }
    
    // Setup log file path
    var settingsDir = Path.Combine(launcherDir, "app", "settings");
    Directory.CreateDirectory(settingsDir);
    logPath = Path.Combine(settingsDir, "launcher.log");
    
    Log(logPath, "=== Launcher started ===");
    Log(logPath, $"Launcher path: {launcherExePath}");
    Log(logPath, $"Launcher directory: {launcherDir}");
    Log(logPath, $"OS: {Environment.OSVersion}");
    Log(logPath, $".NET version: {Environment.Version}");
    
    var exePath = Path.Combine(launcherDir, "app", "FlairX Mod Manager.exe");
    Log(logPath, $"Target exe path: {exePath}");
    
    // Check if main exe exists
    if (!File.Exists(exePath))
    {
        Log(logPath, "ERROR: Main exe not found!");
        return;
    }
    
    Log(logPath, "Main exe found");
    
    var workingDir = Path.GetDirectoryName(exePath);
    if (string.IsNullOrEmpty(workingDir))
    {
        Log(logPath, "ERROR: Could not determine working directory");
        return;
    }
    
    Log(logPath, $"Working directory: {workingDir}");

    // Launch main application
    var startInfo = new ProcessStartInfo
    {
        FileName = exePath,
        UseShellExecute = true,
        WorkingDirectory = workingDir,
        CreateNoWindow = true
    };
    
    Log(logPath, "Starting main application...");
    var process = Process.Start(startInfo);
    
    // Wait a moment to ensure main app started successfully
    if (process != null)
    {
        Log(logPath, $"Process started with PID: {process.Id}");
        System.Threading.Thread.Sleep(500);
        
        // Check if main process is still running
        if (!process.HasExited)
        {
            Log(logPath, "Main app is running, launcher exiting successfully");
            return;
        }
        else
        {
            Log(logPath, $"ERROR: Main app exited immediately with code: {process.ExitCode}");
        }
    }
    else
    {
        Log(logPath, "ERROR: Process.Start returned null");
    }
}
catch (Exception ex)
{
    Log(logPath, $"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
    Log(logPath, $"Stack trace: {ex.StackTrace}");
}

// Logging function with anonymization
static void Log(string? logPath, string message)
{
    try
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var anonymizedMessage = AnonymizePath(message);
        var logLine = $"[{timestamp}] {anonymizedMessage}";
        
        if (!string.IsNullOrEmpty(logPath))
        {
            File.AppendAllText(logPath, logLine + Environment.NewLine);
        }
    }
    catch
    {
        // Silent fail for logging
    }
}

// Anonymize user-specific paths in log messages
static string AnonymizePath(string message)
{
    if (string.IsNullOrEmpty(message))
        return message;
    
    var anonymized = message;
    
    // Replace user profile path
    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    if (!string.IsNullOrEmpty(userProfile))
    {
        anonymized = anonymized.Replace(userProfile, "<USER_PROFILE>", StringComparison.OrdinalIgnoreCase);
    }
    
    // Replace username
    var userName = Environment.UserName;
    if (!string.IsNullOrEmpty(userName))
    {
        anonymized = anonymized.Replace(userName, "<USER>", StringComparison.OrdinalIgnoreCase);
    }
    
    // Replace machine name
    var machineName = Environment.MachineName;
    if (!string.IsNullOrEmpty(machineName))
    {
        anonymized = anonymized.Replace(machineName, "<MACHINE>", StringComparison.OrdinalIgnoreCase);
    }
    
    // Replace common C:\Users\<name> patterns
    anonymized = System.Text.RegularExpressions.Regex.Replace(
        anonymized, 
        @"C:\\Users\\[^\\]+", 
        "C:\\Users\\<USER>", 
        System.Text.RegularExpressions.RegexOptions.IgnoreCase
    );
    
    // Replace common user directories
    anonymized = anonymized
        .Replace("\\AppData\\", "\\<APPDATA>\\", StringComparison.OrdinalIgnoreCase)
        .Replace("\\Documents\\", "\\<DOCUMENTS>\\", StringComparison.OrdinalIgnoreCase)
        .Replace("\\Desktop\\", "\\<DESKTOP>\\", StringComparison.OrdinalIgnoreCase)
        .Replace("\\Downloads\\", "\\<DOWNLOADS>\\", StringComparison.OrdinalIgnoreCase);
    
    return anonymized;
}

// Win32 API imports
[DllImport("kernel32.dll")]
static extern IntPtr GetConsoleWindow();

[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
