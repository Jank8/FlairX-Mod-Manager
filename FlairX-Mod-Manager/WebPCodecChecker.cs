using Microsoft.Win32;
using System;

namespace FlairX_Mod_Manager;

/// <summary>
/// Helper class to check if WebP Image Extensions codec is installed on Windows
/// </summary>
public static class WebPCodecChecker
{
    private const string WEBP_CODEC_PACKAGE_NAME = "Microsoft.WebpImageExtension";
    
    /// <summary>
    /// Check if WebP codec is installed by checking Windows registry
    /// </summary>
    public static bool IsWebPCodecInstalled()
    {
        try
        {
            // Check if WebP Image Extensions package is installed
            // Registry path for installed packages
            string registryPath = @"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
            
            using (var key = Registry.LocalMachine.OpenSubKey(registryPath))
            {
                if (key != null)
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        if (subKeyName.Contains(WEBP_CODEC_PACKAGE_NAME, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            
            // Also check current user registry
            using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
            {
                if (key != null)
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        if (subKeyName.Contains(WEBP_CODEC_PACKAGE_NAME, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError("Error checking WebP codec installation", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Get the Microsoft Store link to install WebP Image Extensions
    /// </summary>
    public static string GetWebPCodecStoreLink()
    {
        return "ms-windows-store://pdp/?ProductId=9PG2DK419DRG";
    }
}
