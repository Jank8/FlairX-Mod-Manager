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
    public sealed partial class GBAuthorUpdatePage
    {

        private void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("GBAuthorUpdate");
            var mainLang = SharedUtilities.LoadLanguageDictionary();
            
            // Update mode cards
            UpdateAuthorsLabel.Text = SharedUtilities.GetTranslation(lang, "UpdateAuthorsLabel");
            UpdateAuthorsDescription.Text = SharedUtilities.GetTranslation(lang, "UpdateAuthorsDescription");
            SmartUpdateLabel.Text = SharedUtilities.GetTranslation(lang, "SmartUpdateLabel");
            SmartUpdateDescription.Text = SharedUtilities.GetTranslation(lang, "SmartUpdateDescription");
            
            // Actions header
            ActionsHeader.Text = SharedUtilities.GetTranslation(lang, "ActionsHeader");
            
            // Fetch all data card (formerly authors)
            FetchAuthorsTitle.Text = SharedUtilities.GetTranslation(lang, "FetchAuthorsButton");
            FetchAuthorsDescription.Text = SharedUtilities.GetTranslation(lang, "FetchAuthorsDescription");
            // Button text is now handled by UpdateButtonStates()
            

            
            // Auto-Update settings card
            AutoUpdateSettingsHeader.Text = SharedUtilities.GetTranslation(lang, "AutoUpdateSettingsHeader") ?? "Auto-Update Settings";
            AutoUpdateEnabledLabel.Text = SharedUtilities.GetTranslation(lang, "AutoUpdateEnabledLabel") ?? "Enable Auto-Update";
            AutoUpdateEnabledDescription.Text = SharedUtilities.GetTranslation(lang, "AutoUpdateEnabledDescription") ?? "Automatically fetch versions and dates on startup";
            AutoUpdateIntervalLabel.Text = SharedUtilities.GetTranslation(lang, "AutoUpdateIntervalLabel") ?? "Update Interval";
            AutoUpdateIntervalDescription.Text = SharedUtilities.GetTranslation(lang, "AutoUpdateIntervalDescription") ?? "How often to check for updates";
            
            // Fetch all previews card
            FetchAllPreviewsTitle.Text = SharedUtilities.GetTranslation(lang, "FetchAllPreviewsButton");
            FetchAllPreviewsDescription.Text = SharedUtilities.GetTranslation(lang, "FetchAllPreviewsButton_Tooltip");
            // Button text is now handled by UpdateButtonStates()
            
            // Fetch missing previews card
            FetchMissingPreviewsTitle.Text = SharedUtilities.GetTranslation(lang, "FetchMissingPreviewsButton");
            FetchMissingPreviewsDescription.Text = SharedUtilities.GetTranslation(lang, "FetchMissingPreviewsButton_Tooltip");
            // Button text is now handled by UpdateButtonStates()
            
            // Create category folders card
            CreateCategoryFoldersTitle.Text = SharedUtilities.GetTranslation(lang, "CreateCategoryFoldersButton");
            CreateCategoryFoldersDescription.Text = SharedUtilities.GetTranslation(lang, "CreateCategoryFoldersButton_Tooltip");
            // Button text is now handled by UpdateButtonStates()
        }
        
        private void UpdateToggleLabels()
        {
            var lang = SharedUtilities.LoadLanguageDictionary();
            var onText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_On");
            var offText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_Off");
            
            if (UpdateAuthorsToggleLabel != null && UpdateAuthorsSwitch != null)
                UpdateAuthorsToggleLabel.Text = UpdateAuthorsSwitch.IsOn ? onText : offText;
            if (SmartUpdateToggleLabel != null && SmartUpdateSwitch != null)
                SmartUpdateToggleLabel.Text = SmartUpdateSwitch.IsOn ? onText : offText;
        }

        private void LoadSettings()
        {
            AutoUpdateEnabledSwitch.IsOn = SettingsManager.Current.GameBananaAutoUpdateEnabled;
            
            // Set interval TextBox
            var intervalDays = SettingsManager.Current.GameBananaAutoUpdateIntervalDays;
            AutoUpdateIntervalTextBox.Text = intervalDays.ToString();
            
            // Show last auto-update run date
            UpdateLastRunText();
        }

        private void UpdateLastRunText()
        {
            var lastRun = SettingsManager.Current.GameBananaLastAutoUpdate;
            AutoUpdateLastRunText.Text = lastRun == DateTime.MinValue
                ? ""
                : lastRun.ToString("g");
        }

        private void AutoUpdateEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.GameBananaAutoUpdateEnabled = AutoUpdateEnabledSwitch.IsOn;
            SettingsManager.Save();
        }

        private void AutoUpdateIntervalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Only validate, don't save automatically
            if (int.TryParse(AutoUpdateIntervalTextBox.Text, out int days) && days >= 1 && days <= 365)
            {
                // Remove any error styling
                AutoUpdateIntervalTextBox.BorderBrush = null;
                AutoUpdateIntervalConfirmButton.IsEnabled = true;
            }
            else if (!string.IsNullOrEmpty(AutoUpdateIntervalTextBox.Text))
            {
                // Show error styling for invalid values
                AutoUpdateIntervalTextBox.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                AutoUpdateIntervalConfirmButton.IsEnabled = false;
            }
            else
            {
                // Empty text - disable confirm button
                AutoUpdateIntervalTextBox.BorderBrush = null;
                AutoUpdateIntervalConfirmButton.IsEnabled = false;
            }
        }

        private void AutoUpdateIntervalConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(AutoUpdateIntervalTextBox.Text, out int days) && days >= 1 && days <= 365)
            {
                SettingsManager.Current.GameBananaAutoUpdateIntervalDays = days;
                SettingsManager.Save();
                
                // Visual feedback - briefly change button appearance
                var button = sender as Button;
                if (button?.Content is TextBlock textBlock)
                {
                    var originalText = textBlock.Text;
                    textBlock.Text = "✓"; // Keep checkmark
                    
                    // Reset after short delay
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
                    timer.Tick += (s, args) =>
                    {
                        textBlock.Text = originalText;
                        timer.Stop();
                    };
                    timer.Start();
                }
            }
        }
    }
}
