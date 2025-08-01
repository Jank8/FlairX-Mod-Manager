using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;

namespace ZZZ_Mod_Manager_X.Pages
{
    public sealed partial class SettingsFunctionPage : Page
    {
        public SettingsFunctionPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string functionFolderName)
            {
                ShowFunctionUI(functionFolderName);
            }
        }

        private string? ExtractFunctionName(string filePath)
        {
            var lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("// Function:", StringComparison.OrdinalIgnoreCase))
                {
                    var name = trimmed.Substring("// Function:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                        return name;
                }
            }
            var regex = new System.Text.RegularExpressions.Regex("FunctionName\\s*=>\\s*\"([^\"]+)\"");
            foreach (var line in lines)
            {
                var match = regex.Match(line);
                if (match.Success)
                    return match.Groups[1].Value;
            }
            return Path.GetFileNameWithoutExtension(filePath);
        }

        private void ShowFunctionUI(string functionFileName)
        {
            FunctionLangPanel.Children.Clear();
            var functionName = System.IO.Path.GetFileNameWithoutExtension(functionFileName);
            var functionFolder = Path.Combine(AppContext.BaseDirectory, "Functions", functionName);
            var uiXamlFile = Path.Combine(functionFolder, functionName + ".UI.xaml");
            var settingsFile = Path.Combine(functionFolder, functionName + ".settings.json");
            Dictionary<string, object>? settings = null;
            if (File.Exists(settingsFile))
            {
                var json = File.ReadAllText(settingsFile);
                settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (settings != null)
                {
                    var keys = new List<string>(settings.Keys);
                    foreach (var key in keys)
                    {
                        if (settings[key] is JsonElement je && je.ValueKind == JsonValueKind.True)
                            settings[key] = true;
                        else if (settings[key] is JsonElement je2 && je2.ValueKind == JsonValueKind.False)
                            settings[key] = false;
                    }
                }
            }
            if (settings == null)
                settings = new Dictionary<string, object>();
            // ...rest of UI generation logic, update all FunctionLangPanel references, etc.
        }


    }
}
