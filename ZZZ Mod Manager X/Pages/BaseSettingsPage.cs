using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZZZ_Mod_Manager_X.Pages
{
    /// <summary>
    /// Base class for settings pages that provides common functionality
    /// </summary>
    public abstract class BaseSettingsPage : Page
    {
        protected Dictionary<string, string> _lang = new();

        protected BaseSettingsPage()
        {
            LoadLanguage();
        }

        /// <summary>
        /// Loads language strings for the page
        /// </summary>
        /// <param name="subfolder">Optional subfolder in Language directory (e.g., "StatusKeeper")</param>
        protected virtual void LoadLanguage(string subfolder = "")
        {
            try
            {
                var langFile = SettingsManager.Current?.LanguageFile ?? "en.json";
                
                string langPath;
                if (string.IsNullOrEmpty(subfolder))
                {
                    langPath = Path.Combine(AppContext.BaseDirectory, AppConstants.LANGUAGE_FOLDER, langFile);
                }
                else
                {
                    langPath = Path.Combine(AppContext.BaseDirectory, AppConstants.LANGUAGE_FOLDER, subfolder, langFile);
                    // Fallback to English if specific language not found
                    if (!File.Exists(langPath))
                        langPath = Path.Combine(AppContext.BaseDirectory, AppConstants.LANGUAGE_FOLDER, subfolder, "en.json");
                }

                if (File.Exists(langPath))
                {
                    var json = File.ReadAllText(langPath, System.Text.Encoding.UTF8);
                    _lang = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
                else
                {
                    Logger.LogWarning($"Language file not found: {langPath}");
                    _lang = new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load language file", ex);
                _lang = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Gets a translated string for the given key
        /// </summary>
        /// <param name="key">Translation key</param>
        /// <returns>Translated string or the key itself if not found</returns>
        protected string T(string key)
        {
            return _lang.TryGetValue(key, out var value) ? value : key;
        }

        /// <summary>
        /// Abstract method that derived classes must implement to update UI text
        /// </summary>
        protected abstract void UpdateTexts();

        /// <summary>
        /// Refreshes the page after language change
        /// </summary>
        public virtual void RefreshAfterLanguageChange()
        {
            LoadLanguage();
            UpdateTexts();
        }

        /// <summary>
        /// Shows an information dialog
        /// </summary>
        protected async Task ShowInfoDialog(string title, string message)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = T("OK"),
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show info dialog: {title}", ex);
            }
        }

        /// <summary>
        /// Shows an error dialog
        /// </summary>
        protected async Task ShowErrorDialog(string message, Exception? exception = null)
        {
            try
            {
                var content = exception != null ? $"{message}\n\nDetails: {exception.Message}" : message;
                var dialog = new ContentDialog
                {
                    Title = T("Error_Generic"),
                    Content = content,
                    CloseButtonText = T("OK"),
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show error dialog: {message}", ex);
            }
        }
    }
}