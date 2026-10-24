using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlairX_Mod_Manager.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace FlairX_Mod_Manager.Dialogs
{
    public partial class GameBananaFileExtractionDialog
    {
        
        private async Task<string> FetchVersionFromGameBanana(string url)
        {
            try
            {
                // Parse GameBanana URL to extract item type and ID
                var urlPattern = new System.Text.RegularExpressions.Regex(@"gamebanana\.com/(\w+)/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var match = urlPattern.Match(url);
                if (!match.Success)
                {
                    return "";
                }

                string itemType = match.Groups[1].Value; // e.g., "mods", "tools"
                string itemId = match.Groups[2].Value;   // e.g., "574763"

                // Capitalize first letter for API (Mod, Tool, etc.)
                itemType = char.ToUpper(itemType[0]) + itemType.Substring(1).TrimEnd('s');

                // Build API URL to get updates
                string apiUrl = $"https://gamebanana.com/apiv11/{itemType}/{itemId}/Updates?_nPage=1&_nPerpage=1&_csvProperties=_sVersion";

                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                var response = await httpClient.GetStringAsync(apiUrl);
                using var doc = System.Text.Json.JsonDocument.Parse(response);
                var root = doc.RootElement;

                // Get the first update's version
                if (root.ValueKind == System.Text.Json.JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var firstUpdate = root[0];
                    if (firstUpdate.TryGetProperty("_sVersion", out var versionProp))
                    {
                        return versionProp.GetString() ?? "";
                    }
                }

                return "";
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to fetch version from GameBanana API for URL: {url}", ex);
                return "";
            }
        }

        private bool IsArchiveFile(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".tar" || 
                   ext == ".gz" || ext == ".bz2" || ext == ".xz" || ext == ".tar.gz" || 
                   ext == ".tar.bz2" || ext == ".tar.xz";
        }

        /// <summary>
        /// Shows a red error under the password box that fades out after 3 seconds.
        /// </summary>
        private async Task ShowPasswordErrorAsync(string message)
        {
            _passwordErrorText.Text = message;
            _passwordErrorText.Opacity = 1;

            await Task.Delay(3000);

            var fadeAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase
                    { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn }
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeAnimation, _passwordErrorText);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeAnimation, "Opacity");
            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            sb.Children.Add(fadeAnimation);
            sb.Begin();
        }

        private async Task ShowError(string message)
        {
            if (App.Current is App _a && _a.MainWindow is MainWindow _mw) _mw.ShowErrorInfo(message);
        }
        
        /// <summary>
        /// Find existing mod path by mod ID extracted from URL (handles renamed folders)
        /// </summary>
        private string? FindExistingModPathByModId(string categoryPath, string? url)
        {
            if (string.IsNullOrEmpty(url) || !Directory.Exists(categoryPath))
                return null;
            
            // Extract mod ID from the URL we're looking for
            var targetModId = GameBananaService.ExtractModIdFromUrl(url);
            if (targetModId == null)
                return null;
            
            try
            {
                foreach (var modDir in Directory.GetDirectories(categoryPath))
                {
                    var modJsonPath = Path.Combine(modDir, "mod.json");
                    if (!File.Exists(modJsonPath))
                        continue;
                    
                    try
                    {
                        var modJsonContent = Services.FileAccessQueue.ReadAllText(modJsonPath);
                        var modJson = System.Text.Json.JsonDocument.Parse(modJsonContent);
                        if (modJson.RootElement.TryGetProperty("url", out var urlProp))
                        {
                            var existingUrl = urlProp.GetString();
                            var existingModId = GameBananaService.ExtractModIdFromUrl(existingUrl);
                            
                            if (existingModId != null && existingModId == targetModId)
                            {
                                Logger.LogInfo($"Found existing mod by ID {targetModId}: {modDir}");
                                return modDir;
                            }
                        }
                    }
                    catch
                    {
                        // Skip invalid mod.json files
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error searching for mod by ID", ex);
            }
            
            return null;
        }

        /// <summary>
        /// Find existing mod path (with or without DISABLED_ prefix)
        /// </summary>
        private string? FindExistingModPath(string categoryPath, string modName)
        {
            // Check for exact match
            var exactPath = Path.Combine(categoryPath, modName);
            if (Directory.Exists(exactPath))
                return exactPath;
            
            // Check for DISABLED_ version
            var disabledPath = Path.Combine(categoryPath, "DISABLED_" + modName);
            if (Directory.Exists(disabledPath))
                return disabledPath;
            
            return null;
        }

        private void ModNameTextBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            // Check if the new text contains any invalid characters
            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            if (args.NewText.Any(c => invalidChars.Contains(c)))
            {
                args.Cancel = true;
                
                // Sanitize the text by replacing invalid characters with dashes
                var sanitizedText = SanitizeFileName(args.NewText);
                
                // Set the sanitized text
                sender.Text = sanitizedText;
                sender.SelectionStart = sender.Text.Length;
                
                // Validate after sanitization
                ValidateInputs();
                return;
            }

            // Validate and update button state for valid input
            ValidateInputs();
        }

        private void ValidateInputs()
        {
            // Check if mod name or category is a reserved Windows name
            var modName = _modNameTextBox.Text.Trim();
            var category = _categoryComboBox.Text.Trim();

            bool isValid = !string.IsNullOrWhiteSpace(modName) &&
                          !string.IsNullOrWhiteSpace(category) &&
                          !IsReservedWindowsName(modName) && 
                          !IsReservedWindowsName(category);
            
            // Check if at least one file is selected (if files are available)
            if (_selectedFiles != null && _selectedFiles.Count > 0)
            {
                bool hasSelectedFile = _selectedFiles.Any(f => f.IsSelected);
                isValid = isValid && hasSelectedFile;
            }
            
            IsPrimaryButtonEnabled = isValid;
        }
    }
}


