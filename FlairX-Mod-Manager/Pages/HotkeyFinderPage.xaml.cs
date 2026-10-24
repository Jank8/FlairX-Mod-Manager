using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class HotkeyFinderPage : Page
    {
        public HotkeyFinderPage()
        {
            this.InitializeComponent();
        }

        // Helper classes
        public class HotkeyInfo
        {
            public string Key { get; set; } = "";
            public string Description { get; set; } = "";
            public string IniFile { get; set; } = "";
            public string Section { get; set; } = "";
            public string KeyName { get; set; } = "";
            public string OriginalValue { get; set; } = "";
        }

    }
}

