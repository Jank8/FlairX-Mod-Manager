using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class GBAuthorUpdatePage : Page
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        
        // Regex pattern to parse GameBanana URLs
        private static readonly Regex _urlPattern = new Regex(@"gamebanana\.com/(\w+)/(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public GBAuthorUpdatePage()
        {
            this.InitializeComponent();
            UpdateTexts();
            UpdateToggleLabels();
            LoadSettings(); // Load auto-update settings
            ProgressChanged += OnProgressChanged;
            UpdateAuthorsSwitch.Toggled += UpdateAuthorsSwitch_Toggled;
            SmartUpdateSwitch.Toggled += SmartUpdateSwitch_Toggled;
        }

        ~GBAuthorUpdatePage()
        {
            ProgressChanged -= OnProgressChanged;
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ProgressChanged += OnProgressChanged;
            // Restore switch states according to mode
            UpdateAuthorsSwitch.IsOn = (CurrentUpdateMode == UpdateMode.Full);
            SmartUpdateSwitch.IsOn = (CurrentUpdateMode == UpdateMode.Smart);
            UpdateProgressBarUI();
            // Refresh translations when navigating to this page
            UpdateTexts();
            UpdateToggleLabels();
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ProgressChanged -= OnProgressChanged;
        }
    }
}