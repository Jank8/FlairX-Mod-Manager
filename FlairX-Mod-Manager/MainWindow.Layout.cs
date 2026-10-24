using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace FlairX_Mod_Manager
{
    public sealed partial class MainWindow
    {
        private const int MIN_WIDTH = 1300;
        private const int MIN_HEIGHT = 720;
        private const int MAX_WIDTH = 20000;
        private const int MAX_HEIGHT = 15000;

        // Coalesces window-state saves: AppWindow.Changed fires on every resize / drag tick.
        private Microsoft.UI.Dispatching.DispatcherQueueTimer? _windowStateSaveTimer;

        /// <summary>
        /// Debounces <see cref="SaveWindowState"/> so dragging or resizing the window does not
        /// rewrite the whole settings file dozens of times per second (each save is now
        /// temp file + replace + backup). The final state is still flushed by the Closed handler.
        /// </summary>
        private void ScheduleWindowStateSave()
        {
            if (_windowStateSaveTimer == null)
            {
                _windowStateSaveTimer = DispatcherQueue.CreateTimer();
                _windowStateSaveTimer.Interval = TimeSpan.FromMilliseconds(500);
                _windowStateSaveTimer.IsRepeating = false;
                _windowStateSaveTimer.Tick += (_, _) => SaveWindowState();
            }
            else
            {
                _windowStateSaveTimer.Stop();
            }

            _windowStateSaveTimer.Start();
        }

        // Hotkey methods moved to MainWindow.Hotkeys.cs partial class

        // UI Management methods moved to MainWindow.UIManagement.cs

        private void SetSearchBoxPlaceholder()
        {
            if (SearchBox != null)
                SearchBox.PlaceholderText = SharedUtilities.GetTranslation(_lang, "Search_Placeholder");
        }

        private void SetFooterMenuTranslations()
        {
            if (SettingsPageItem is NavigationViewItem settings)
                settings.Content = SharedUtilities.GetTranslation(_lang, "SettingsPage_Title");
            
            var presetsItem = nvSample.FooterMenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag as string == "PresetsUserControl");
            if (presetsItem != null)
                presetsItem.Content = SharedUtilities.GetTranslation(_lang, "Presets");
            if (AllModsButton != null)
                AllModsButton.Content = SharedUtilities.GetTranslation(_lang, "All_Mods");
            if (ReloadModsButton != null)
                ToolTipService.SetToolTip(ReloadModsButton, SharedUtilities.GetTranslation(_lang, "Reload_Mods_Tooltip"));
            if (BrowseGameBananaButton != null)
                ToolTipService.SetToolTip(BrowseGameBananaButton, SharedUtilities.GetTranslation(_lang, "Browse_GameBanana_Tooltip"));
            if (LauncherFabBorder != null)
                ToolTipService.SetToolTip(LauncherFabBorder, SharedUtilities.GetTranslation(_lang, "Launcher_Tooltip"));
            if (RestartAppButton != null)
                ToolTipService.SetToolTip(RestartAppButton, SharedUtilities.GetTranslation(_lang, "SettingsPage_RestartApp_Tooltip"));

            // Set tile format toggle button texts
            if (ClassicFormatButton != null)
                ClassicFormatButton.Content = SharedUtilities.GetTranslation(_lang, "TileFormat_Classic");
            if (WideFormatButton != null)
                WideFormatButton.Content = SharedUtilities.GetTranslation(_lang, "TileFormat_Wide");
            
            // Update view mode tooltip based on current state
            if (ViewModeToggleButton?.Content is FontIcon icon)
            {
                bool isCategoriesView = icon.Glyph == "\uE8B3";
                UpdateViewModeTooltip(isCategoriesView);
            }
        }



        // Navigation methods moved to MainWindow.Navigation.cs partial class

        // SearchBox methods moved to MainWindow.Navigation.cs

        // Window control button methods moved to MainWindow.WindowManagement.cs

        // Event handler methods moved to MainWindow.EventHandlers.cs

        private FlairX_Mod_Manager.Pages.ModGridPage.ViewMode GetCurrentViewMode()
        {
            if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
            {
                return modGridPage.CurrentViewMode;
            }
            
            // Default to mods view
            return FlairX_Mod_Manager.Pages.ModGridPage.ViewMode.Mods;
        }

        private string GetCurrentViewModeString()
        {
            // Use SettingsManager ViewMode instead of ModGridPage CurrentViewMode
            // because ModGridPage CurrentViewMode can be temporarily changed
            return SettingsManager.Current.ViewMode;
        }

        private void SaveCurrentPositionBeforeReload()
        {
            // Save current position before reload to ensure we return to the same place
            if (contentFrame.Content is FlairX_Mod_Manager.Pages.ModGridPage modGridPage)
            {
                var currentViewMode = GetCurrentViewModeString();
                
                // Get current category from CategoryTitle or check if we're showing categories
                var categoryTitle = modGridPage.GetCategoryTitleText();
                var langDict = SharedUtilities.LoadLanguageDictionary();
                var allModsText = SharedUtilities.GetTranslation(langDict, "Category_All_Mods");
                var allCategoriesText = SharedUtilities.GetTranslation(langDict, "All_Categories");
                var activeModsText = SharedUtilities.GetTranslation(langDict, "Category_Active_Mods");
                var brokenModsText = SharedUtilities.GetTranslation(langDict, "Category_Broken_Mods");
                var outdatedModsText = SharedUtilities.GetTranslation(langDict, "Category_Outdated_Mods");
                
                // Check if we're in special views (Active, Broken, Outdated)
                if (categoryTitle == activeModsText)
                {
                    SettingsManager.SaveLastPosition("Active", "ModGridPage", currentViewMode);
                }
                else if (categoryTitle == brokenModsText)
                {
                    SettingsManager.SaveLastPosition("Broken", "ModGridPage", currentViewMode);
                }
                else if (categoryTitle == outdatedModsText)
                {
                    SettingsManager.SaveLastPosition("Outdated", "ModGridPage", currentViewMode);
                }
                // Check if we're in a specific category (not "All Mods" or "All Categories")
                else if (!string.IsNullOrEmpty(categoryTitle) && 
                    categoryTitle != allModsText && 
                    categoryTitle != allCategoriesText)
                {
                    // We're in a specific category - save it
                    SettingsManager.SaveLastPosition(categoryTitle, "ModGridPage", currentViewMode);
                }
                else if (modGridPage.CurrentViewMode == FlairX_Mod_Manager.Pages.ModGridPage.ViewMode.Categories)
                {
                    // We're in category mode but showing all categories
                    SettingsManager.SaveLastPosition(null, "Categories", "Categories");
                }
                else
                {
                    // We're in default mode showing all mods
                    SettingsManager.SaveLastPosition(null, "ModGridPage", "Mods");
                }
            }
        }

        private bool DoesCategoryExist(string category)
        {
            try
            {
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
                    return false;
                
                var categoryPath = Path.Combine(modsPath, category);
                return Directory.Exists(categoryPath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking if category exists: {category}", ex);
                return false;
            }
        }

        private void RestoreLastPosition()
        {
            // Check if a game is selected first
            bool gameSelected = SettingsManager.Current.SelectedGameIndex > 0;
            
            if (!gameSelected)
            {
                // No game selected - show welcome page
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.WelcomePage));
                return;
            }
            
            var (lastCategory, lastPage, lastViewMode) = SettingsManager.GetLastPosition();
            
            if (!string.IsNullOrEmpty(lastCategory))
            {
                if (lastCategory == "Active")
                {
                    // Navigate to Active mods
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Active", new DrillInNavigationTransitionInfo());
                    // Don't select any menu item for Active mods
                }
                else if (lastCategory == "Outdated")
                {
                    // Navigate to Outdated mods
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Outdated", new DrillInNavigationTransitionInfo());
                    // Don't select any menu item for Outdated mods
                }
                else if (lastCategory == "Broken")
                {
                    // Navigate to Broken mods
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Broken", new DrillInNavigationTransitionInfo());
                    // Don't select any menu item for Broken mods
                }
                else
                {
                    // Check if the category still exists before navigating to it
                    bool categoryExists = DoesCategoryExist(lastCategory);
                    
                    if (categoryExists)
                    {
                        // Navigate to specific category with view mode consideration
                        if (lastViewMode == "Categories")
                        {
                            // In category mode, navigate with CategoryInCategoryMode parameter
                            contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), $"CategoryInCategoryMode:{lastCategory}", new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            // In default mode, navigate with Category parameter
                            contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), $"Category:{lastCategory}", new DrillInNavigationTransitionInfo());
                        }
                        
                        // Try to select the corresponding menu item
                        var categoryMenuItem = nvSample.MenuItems.OfType<NavigationViewItem>()
                            .FirstOrDefault(item => item.Tag?.ToString() == $"Category_{lastCategory}");
                        if (categoryMenuItem != null)
                        {
                            nvSample.SelectedItem = categoryMenuItem;
                        }
                        else if (lastCategory == "Other")
                        {
                            // Navigate to Other category
                            contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Other", new DrillInNavigationTransitionInfo());
                        }
                    }
                    else
                    {
                        // Category doesn't exist anymore - use fallback based on view mode
                        var currentViewMode = SettingsManager.Current.ViewMode;
                        if (currentViewMode == "Categories" || lastViewMode == "Categories")
                        {
                            // Fallback to All Categories in category mode
                            contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Categories", new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            // Fallback to All Mods in default mode
                            contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
                        }
                        
                        // Clear the invalid position
                        SettingsManager.ClearLastPosition();
                    }
                }
            }
            else if (lastPage == "Categories")
            {
                // Navigate to All Categories view
                contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Categories", new DrillInNavigationTransitionInfo());
            }
            else if (lastPage == "SettingsUserControl")
            {
                // Don't start on Settings page to avoid category list duplicates
                // Instead, navigate to All Mods based on current view mode
                var currentViewMode = SettingsManager.Current.ViewMode;
                if (currentViewMode == "Categories")
                {
                    // Navigate to All Categories in category mode
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Categories", new DrillInNavigationTransitionInfo());
                }
                else
                {
                    // Navigate to All Mods in default mode
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
                }
            }
            else
            {
                // Default based on view mode - use current ViewMode from settings
                var currentViewMode = SettingsManager.Current.ViewMode;
                if (currentViewMode == "Categories" || lastViewMode == "Categories")
                {
                    // Default to All Categories in category mode
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), "Categories", new DrillInNavigationTransitionInfo());
                }
                else
                {
                    // Default to All Mods in default mode
                    contentFrame.Navigate(typeof(FlairX_Mod_Manager.Pages.ModGridPage), null, new DrillInNavigationTransitionInfo());
                }
            }
        }

        // ViewMode and GameSelection methods moved to MainWindow.EventHandlers.cs



        public void UpdateViewModeButtonIcon(bool isCategoriesView)
        {
            if (ViewModeToggleButton?.Content is FontIcon icon)
            {
                icon.Glyph = isCategoriesView ? "\uE8B3" : "\uE8A9";
            }
            
            // Update the All Mods button text
            UpdateAllModsButtonText(isCategoriesView);
            
            // Update the tooltip
            UpdateViewModeTooltip(isCategoriesView);
        }

        public bool IsCurrentlyInCategoryMode()
        {
            if (ViewModeToggleButton?.Content is FontIcon icon)
            {
                return icon.Glyph == "\uE8B3";
            }
            return false;
        }

        private void InitializeViewModeFromSettings()
        {
            // Initialize view mode button from settings
            bool isCategoriesView = SettingsManager.Current.ViewMode == "Categories";
            
            if (ViewModeToggleButton?.Content is FontIcon icon)
            {
                icon.Glyph = isCategoriesView ? "\uE8B3" : "\uE8A9";
            }
            
            UpdateAllModsButtonText(isCategoriesView);
            UpdateViewModeTooltip(isCategoriesView);
            UpdateMenuItemsEnabledState(isCategoriesView);
        }

        public void RestoreViewModeButtonFromSettings()
        {
            // Restore view mode button from settings (call this when button gets corrupted)
            InitializeViewModeFromSettings();
        }

        private void UpdateViewModeTooltip(bool isCategoriesView)
        {
            if (ViewModeToggleButton != null)
            {
                var tooltipKey = isCategoriesView ? "ViewModeToggle_CategoryMode_Tooltip" : "ViewModeToggle_DefaultMode_Tooltip";
                var tooltip = SharedUtilities.GetTranslation(_lang, tooltipKey);
                ToolTipService.SetToolTip(ViewModeToggleButton, tooltip);
            }
        }

        private void UpdateMenuItemsEnabledState(bool isCategoriesView)
        {
            // Hide/show scrollable menu entries based on view mode
            foreach (var item in nvSample.MenuItems.OfType<NavigationViewItem>())
            {
                if (item.Tag is string tag && tag.StartsWith("Category_"))
                {
                    item.Visibility = isCategoriesView ? Visibility.Collapsed : Visibility.Visible;
                }
            }
        }

        private void UpdateAllModsButtonText(bool isCategoriesView)
        {
            if (AllModsButton != null)
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                if (isCategoriesView)
                {
                    AllModsButton.Content = SharedUtilities.GetTranslation(lang, "All_Categories");
                }
                else
                {
                    AllModsButton.Content = SharedUtilities.GetTranslation(lang, "All_Mods");
                }
            }
        }

        public void UpdateAllModsButtonState()
        {
            if (AllModsButton != null)
            {
                // Enable button only if a game is selected
                bool gameSelected = SettingsManager.Current.SelectedGameIndex > 0;
                AllModsButton.IsEnabled = gameSelected;
                
                // Restore button text based on current view mode
                bool isCategoriesView = SettingsManager.Current.ViewMode == "Categories";
                UpdateAllModsButtonText(isCategoriesView);
            }
        }
        
        /// <summary>
        /// Public method to reload manager - same as clicking Reload button
        /// Used after data changes (like GameBanana auto-update)
        /// </summary>
        public async Task ReloadManagerView()
        {
            await ReloadModsAsync();
        }

        // Enhanced InfoBar methods for better user feedback
        public void ShowInfoBar(string title, string message, Microsoft.UI.Xaml.Controls.InfoBarSeverity severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational, int autoCloseDelayMs = 6000)
        {
            Logger.LogInfo($"Showing notification: {severity} - {title}: {message}");

            if (MainNotificationPopup != null)
            {
                MainNotificationPopup.AttachToWindow(this);
                var popupSeverity = severity switch
                {
                    Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success => Controls.NotificationSeverity.Success,
                    Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning => Controls.NotificationSeverity.Warning,
                    Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error   => Controls.NotificationSeverity.Error,
                    _                                                   => Controls.NotificationSeverity.Info
                };
                // Show message (prepend title if non-empty and different from message)
                var displayMessage = !string.IsNullOrEmpty(title) && title != "Success" && title != "Warning" && title != "Error" && title != "Info"
                    ? $"{title}: {message}"
                    : message;
                MainNotificationPopup.Show(displayMessage, popupSeverity, autoCloseDelayMs);
            }
        }

        public void ShowSuccessInfo(string message, int autoCloseDelayMs = 6000)
        {
            ShowInfoBar("", message, Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success, autoCloseDelayMs);
        }

        public void ShowWarningInfo(string message, int autoCloseDelayMs = 12000)
        {
            ShowInfoBar("", message, Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning, autoCloseDelayMs);
        }

        public void ShowErrorInfo(string message, int autoCloseDelayMs = 0)
        {
            ShowInfoBar("", message, Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error, autoCloseDelayMs);
        }
    }
}
