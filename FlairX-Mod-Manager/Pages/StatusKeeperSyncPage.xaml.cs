using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.Text;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class StatusKeeperSyncPage : Page
    {
        


        public StatusKeeperSyncPage()
        {
            this.InitializeComponent();
            UpdateTexts();
            
            // Initialize logging if enabled
            if (SettingsManager.Current.StatusKeeperLoggingEnabled)
            {
                InitFileLogging(GetLogPath());
            }
            
            // Sync disabled message is now displayed in App.xaml.cs
        }
    }
}