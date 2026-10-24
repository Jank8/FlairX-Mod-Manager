using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class GameOverlayPage
    {
        private bool _isInitializing = true;
        private GamepadManager? _testGamepad;

        /// <summary>
        /// Updates all UI texts with translations from language file
        /// </summary>
        public void UpdateTexts()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("Overlay");
            var mainLang = SharedUtilities.LoadLanguageDictionary();
            
            // Section headers
            if (AppearanceSectionHeader != null) AppearanceSectionHeader.Text = SharedUtilities.GetTranslation(lang, "Section_Appearance");
            if (PreviewSectionHeader != null) PreviewSectionHeader.Text = SharedUtilities.GetTranslation(lang, "Section_Preview");
            if (ControlsSectionHeader != null) ControlsSectionHeader.Text = SharedUtilities.GetTranslation(lang, "Section_Controls");
            if (GamepadSectionHeader != null) GamepadSectionHeader.Text = SharedUtilities.GetTranslation(lang, "Section_Gamepad");
            if (GamepadHotkeysSectionHeader != null) GamepadHotkeysSectionHeader.Text = SharedUtilities.GetTranslation(lang, "Section_GamepadHotkeys");
            
            // Theme
            if (ThemeLabel != null) ThemeLabel.Text = SharedUtilities.GetTranslation(lang, "Theme_Label");
            if (ThemeDescription != null) ThemeDescription.Text = SharedUtilities.GetTranslation(lang, "Theme_Description");
            if (ThemeAutoText != null) ThemeAutoText.Text = SharedUtilities.GetTranslation(lang, "Theme_Auto");
            if (ThemeLightText != null) ThemeLightText.Text = SharedUtilities.GetTranslation(lang, "Theme_Light");
            if (ThemeDarkText != null) ThemeDarkText.Text = SharedUtilities.GetTranslation(lang, "Theme_Dark");
            
            // Backdrop
            if (BackdropLabel != null) BackdropLabel.Text = SharedUtilities.GetTranslation(lang, "Backdrop_Label");
            if (BackdropDescription != null) BackdropDescription.Text = SharedUtilities.GetTranslation(lang, "Backdrop_Description");
            if (BackdropThinText != null) BackdropThinText.Text = SharedUtilities.GetTranslation(lang, "Backdrop_Thin");
            if (BackdropNoneText != null) BackdropNoneText.Text = SharedUtilities.GetTranslation(lang, "Backdrop_None");
            
            // Test Overlay
            if (TestOverlayLabel != null) TestOverlayLabel.Text = SharedUtilities.GetTranslation(lang, "TestOverlay_Label");
            if (TestOverlayDescription != null) TestOverlayDescription.Text = SharedUtilities.GetTranslation(lang, "TestOverlay_Description");
            if (OpenOverlayButtonText != null) OpenOverlayButtonText.Text = SharedUtilities.GetTranslation(lang, "TestOverlay_Button");
            
            // Toggle Hotkey
            if (OverlayHotkeyLabel != null) OverlayHotkeyLabel.Text = SharedUtilities.GetTranslation(lang, "ToggleHotkey_Label");
            
            // Filter Active Hotkey
            if (FilterActiveHotkeyLabel != null) FilterActiveHotkeyLabel.Text = SharedUtilities.GetTranslation(lang, "FilterActiveHotkey_Label");
            
            // Gamepad Enabled
            if (GamepadEnabledLabel != null) GamepadEnabledLabel.Text = SharedUtilities.GetTranslation(lang, "GamepadEnabled_Label");
            if (GamepadEnabledDescription != null) GamepadEnabledDescription.Text = SharedUtilities.GetTranslation(lang, "GamepadEnabled_Description");
            
            // Use Left Stick
            if (UseLeftStickLabel != null) UseLeftStickLabel.Text = SharedUtilities.GetTranslation(lang, "UseLeftStick_Label");
            if (UseLeftStickDescription != null) UseLeftStickDescription.Text = SharedUtilities.GetTranslation(lang, "UseLeftStick_Description");
            
            // Use Right Stick for Hotkeys
            if (UseRightStickForHotkeysLabel != null) UseRightStickForHotkeysLabel.Text = SharedUtilities.GetTranslation(lang, "UseRightStickForHotkeys_Label");
            if (UseRightStickForHotkeysDescription != null) UseRightStickForHotkeysDescription.Text = SharedUtilities.GetTranslation(lang, "UseRightStickForHotkeys_Description");
            
            // Vibrate on Navigation
            if (VibrateOnNavLabel != null) VibrateOnNavLabel.Text = SharedUtilities.GetTranslation(lang, "VibrateOnNav_Label");
            if (VibrateOnNavDescription != null) VibrateOnNavDescription.Text = SharedUtilities.GetTranslation(lang, "VibrateOnNav_Description");
            
            // Controller Status
            if (GamepadStatusLabel != null) GamepadStatusLabel.Text = SharedUtilities.GetTranslation(lang, "ControllerStatus_Label");
            if (TestButtonText != null) TestButtonText.Text = SharedUtilities.GetTranslation(lang, "ControllerStatus_Test");
            
            // Steam Warning
            if (SteamWarningLabel != null) SteamWarningLabel.Text = SharedUtilities.GetTranslation(lang, "SteamWarning_Label");
            if (SteamWarningDescription != null) SteamWarningDescription.Text = SharedUtilities.GetTranslation(lang, "SteamWarning_Description");
            
            // Select Button
            if (SelectButtonLabel != null) SelectButtonLabel.Text = SharedUtilities.GetTranslation(lang, "SelectButton_Label");
            
            // Back Button
            if (BackButtonLabel != null) BackButtonLabel.Text = SharedUtilities.GetTranslation(lang, "BackButton_Label");
            
            // Next Category
            if (NextCategoryButtonLabel != null) NextCategoryButtonLabel.Text = SharedUtilities.GetTranslation(lang, "NextCategory_Label");
            
            // Previous Category
            if (PrevCategoryButtonLabel != null) PrevCategoryButtonLabel.Text = SharedUtilities.GetTranslation(lang, "PrevCategory_Label");
            
            // Toggle Overlay Combo
            if (GamepadComboLabel != null) GamepadComboLabel.Text = SharedUtilities.GetTranslation(lang, "ToggleOverlayCombo_Label");
            
            // Filter Active Combo
            if (FilterActiveComboLabel != null) FilterActiveComboLabel.Text = SharedUtilities.GetTranslation(lang, "FilterActiveCombo_Label");
            
            // Favorites section
            if (FavoritesSectionHeader != null) FavoritesSectionHeader.Text = SharedUtilities.GetTranslation(lang, "Favorites_Section");
            if (CategoryFavoriteLabel != null) CategoryFavoriteLabel.Text = SharedUtilities.GetTranslation(lang, "CategoryFavorite_Label");
            if (ModFavoriteLabel != null) ModFavoriteLabel.Text = SharedUtilities.GetTranslation(lang, "ModFavorite_Label");
            
            // Navigation labels
            if (NavigationSectionHeader != null) NavigationSectionHeader.Text = SharedUtilities.GetTranslation(lang, "Section_Navigation");
            if (DPadUpLabel != null) DPadUpLabel.Text = SharedUtilities.GetTranslation(lang, "DPadUp_Label");
            if (DPadDownLabel != null) DPadDownLabel.Text = SharedUtilities.GetTranslation(lang, "DPadDown_Label");
            if (DPadLeftLabel != null) DPadLeftLabel.Text = SharedUtilities.GetTranslation(lang, "DPadLeft_Label");
            if (DPadRightLabel != null) DPadRightLabel.Text = SharedUtilities.GetTranslation(lang, "DPadRight_Label");
            if (LeftStickUpLabel != null) LeftStickUpLabel.Text = SharedUtilities.GetTranslation(lang, "LeftStickUp_Label");
            if (LeftStickDownLabel != null) LeftStickDownLabel.Text = SharedUtilities.GetTranslation(lang, "LeftStickDown_Label");
            if (LeftStickLeftLabel != null) LeftStickLeftLabel.Text = SharedUtilities.GetTranslation(lang, "LeftStickLeft_Label");
            if (LeftStickRightLabel != null) LeftStickRightLabel.Text = SharedUtilities.GetTranslation(lang, "LeftStickRight_Label");
            if (RightStickUpLabel != null) RightStickUpLabel.Text = SharedUtilities.GetTranslation(lang, "RightStickUp_Label");
            if (RightStickDownLabel != null) RightStickDownLabel.Text = SharedUtilities.GetTranslation(lang, "RightStickDown_Label");
            if (RightStickSectionHeader != null) RightStickSectionHeader.Text = SharedUtilities.GetTranslation(lang, "Section_RightStick") ?? "Right Stick (Hotkeys Scrolling)";
            
            // Toggle switch labels
            UpdateToggleSwitchLabels(lang);
        }

        private void UpdateToggleSwitchLabels(Dictionary<string, string> lang)
        {
            var onText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_On");
            var offText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_Off");
            
            if (OverlayHotkeysEnabledToggleLabel != null && OverlayHotkeysEnabledToggle != null)
                OverlayHotkeysEnabledToggleLabel.Text = OverlayHotkeysEnabledToggle.IsOn ? onText : offText;
            if (GamepadEnabledToggle != null)
            {
                GamepadEnabledToggle.OnContent = onText;
                GamepadEnabledToggle.OffContent = offText;
            }
            if (UseLeftStickToggle != null)
            {
                UseLeftStickToggle.OnContent = onText;
                UseLeftStickToggle.OffContent = offText;
            }
            if (VibrateOnNavToggle != null)
            {
                VibrateOnNavToggle.OnContent = onText;
                VibrateOnNavToggle.OffContent = offText;
            }
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Current;
            
            // Load overlay hotkeys enabled
            OverlayHotkeysEnabledToggle.IsOn = settings.OverlayHotkeysEnabled;
            
            // Load hotkey
            var overlayHotkey = settings.ToggleOverlayHotkey ?? "Alt+W";
            var filterActiveHotkey = settings.FilterActiveHotkey ?? "Alt+A";
            
            // Initialize hotkey visual panels
            InitializeHotkeyPanel(OverlayHotkeyKeysPanel, overlayHotkey);
            InitializeHotkeyPanel(FilterActiveHotkeyKeysPanel, filterActiveHotkey);
            
            // Load gamepad enabled
            GamepadEnabledToggle.IsOn = settings.GamepadEnabled;
            
            // Load theme
            var theme = settings.OverlayTheme ?? "Auto";
            ThemeSelectorBar.SelectedItem = theme switch
            {
                "Light" => ThemeSelectorLight,
                "Dark" => ThemeSelectorDark,
                _ => ThemeSelectorAuto
            };
            
            // Load backdrop
            var backdrop = settings.OverlayBackdrop ?? "AcrylicThin";
            BackdropSelectorBar.SelectedItem = backdrop switch
            {
                "Mica" => BackdropSelectorMica,
                "MicaAlt" => BackdropSelectorMicaAlt,
                "Acrylic" => BackdropSelectorAcrylic,
                "None" => BackdropSelectorNone,
                _ => BackdropSelectorAcrylicThin
            };
            
            // Load gamepad options
            UseLeftStickToggle.IsOn = settings.GamepadUseLeftStick;
            UseRightStickForHotkeysToggle.IsOn = settings.GamepadUseRightStickForHotkeys;
            VibrateOnNavToggle.IsOn = settings.GamepadVibrateOnNavigation;
            
            // Initialize gamepad button keys panels (convert to Kenney format if needed)
            InitializeHotkeyPanel(SelectButtonKeysPanel, ConvertToKenneyFormat(settings.GamepadSelectButton ?? "A"));
            InitializeHotkeyPanel(BackButtonKeysPanel, ConvertToKenneyFormat(settings.GamepadBackButton ?? "B"));
            InitializeHotkeyPanel(NextCategoryButtonKeysPanel, ConvertToKenneyFormat(settings.GamepadNextCategoryButton ?? "RB"));
            InitializeHotkeyPanel(PrevCategoryButtonKeysPanel, ConvertToKenneyFormat(settings.GamepadPrevCategoryButton ?? "LB"));
            InitializeHotkeyPanel(CategoryFavoriteButtonKeysPanel, ConvertToKenneyFormat(settings.GamepadCategoryFavoriteButton ?? "XB X"));
            InitializeHotkeyPanel(ModFavoriteButtonKeysPanel, ConvertToKenneyFormat(settings.GamepadModFavoriteButton ?? "XB Y"));
            
            // Initialize combo panels
            var toggleCombo = settings.GamepadToggleOverlayCombo ?? "Back+Start";
            var filterCombo = settings.GamepadFilterActiveCombo ?? "Back+A";
            InitializeHotkeyPanel(GamepadComboKeysPanel, ConvertComboToKenneyFormat(toggleCombo));
            InitializeHotkeyPanel(FilterActiveComboKeysPanel, ConvertComboToKenneyFormat(filterCombo));
            
            // Initialize navigation panels
            InitializeHotkeyPanel(DPadUpKeysPanel, ConvertToKenneyFormat(settings.GamepadDPadUp ?? "XB ↑"));
            InitializeHotkeyPanel(DPadDownKeysPanel, ConvertToKenneyFormat(settings.GamepadDPadDown ?? "XB ↓"));
            InitializeHotkeyPanel(DPadLeftKeysPanel, ConvertToKenneyFormat(settings.GamepadDPadLeft ?? "XB ←"));
            InitializeHotkeyPanel(DPadRightKeysPanel, ConvertToKenneyFormat(settings.GamepadDPadRight ?? "XB →"));
            InitializeHotkeyPanel(LeftStickUpKeysPanel, ConvertToKenneyFormat(settings.GamepadLeftStickUp ?? "XB L↑"));
            InitializeHotkeyPanel(LeftStickDownKeysPanel, ConvertToKenneyFormat(settings.GamepadLeftStickDown ?? "XB L↓"));
            InitializeHotkeyPanel(LeftStickLeftKeysPanel, ConvertToKenneyFormat(settings.GamepadLeftStickLeft ?? "XB L←"));
            InitializeHotkeyPanel(LeftStickRightKeysPanel, ConvertToKenneyFormat(settings.GamepadLeftStickRight ?? "XB L→"));
            InitializeHotkeyPanel(RightStickUpKeysPanel, ConvertToKenneyFormat(settings.GamepadRightStickUp ?? "XB R↑"));
            InitializeHotkeyPanel(RightStickDownKeysPanel, ConvertToKenneyFormat(settings.GamepadRightStickDown ?? "XB R↓"));
            
            // Update overlay hotkeys section state
            UpdateOverlayHotkeysSectionState(settings.OverlayHotkeysEnabled);
        }

        private void CheckGamepadStatus()
        {
            try
            {
                _testGamepad = new GamepadManager();
                bool isConnected = _testGamepad.CheckConnection();
                
                UpdateGamepadStatusUI(isConnected);
                
                if (!isConnected)
                {
                    _testGamepad.Dispose();
                    _testGamepad = null;
                }
            }
            catch
            {
                UpdateGamepadStatusUI(false);
            }
        }

        private void UpdateGamepadStatusUI(bool isConnected)
        {
            var lang = SharedUtilities.LoadLanguageDictionary("Overlay");
            
            if (isConnected)
            {
                GamepadStatusIcon.Glyph = "\uE73E"; // Checkmark
                GamepadStatusText.Text = SharedUtilities.GetTranslation(lang, "ControllerStatus_Connected");
                GamepadStatusText.Opacity = 1.0;
            }
            else
            {
                GamepadStatusIcon.Glyph = "\uE711"; // X
                GamepadStatusText.Text = SharedUtilities.GetTranslation(lang, "ControllerStatus_NotConnected");
                GamepadStatusText.Opacity = 0.7;
            }
        }

        /// <summary>
        /// Modifies d3dx.ini to enable/disable background keypresses.
        /// This tells 3DMigoto to accept keypresses even when game is not in foreground.
        /// Can be called from anywhere (static) or from GameOverlayPage instance.
        /// </summary>
        public static void EnsureBackgroundKeypressIni(bool enabled, string? modsPathOverride = null)
        {
            try
            {
                var modsPath = modsPathOverride ?? SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !System.IO.Directory.Exists(modsPath))
                {
                    Logger.LogWarning("Cannot modify d3dx.ini - Mods directory not found");
                    return;
                }
                
                // d3dx.ini is in the parent directory of Mods folder
                var basePath = System.IO.Path.GetDirectoryName(modsPath);
                if (string.IsNullOrEmpty(basePath))
                {
                    Logger.LogWarning("Cannot determine base path for d3dx.ini");
                    return;
                }
                
                var d3dxIniPath = System.IO.Path.Combine(basePath, "d3dx.ini");
                
                if (!System.IO.File.Exists(d3dxIniPath))
                {
                    Logger.LogWarning($"d3dx.ini not found at: {d3dxIniPath}");
                    return;
                }
                
                // Read existing content
                var lines = System.IO.File.ReadAllLines(d3dxIniPath).ToList();
                
                // Find or create [System] section
                int systemSectionIndex = -1;
                int checkForegroundIndex = -1;
                
                for (int i = 0; i < lines.Count; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.Equals("[System]", StringComparison.OrdinalIgnoreCase))
                    {
                        systemSectionIndex = i;
                    }
                    else if (systemSectionIndex != -1 && trimmed.StartsWith("check_foreground_window", StringComparison.OrdinalIgnoreCase))
                    {
                        checkForegroundIndex = i;
                        break;
                    }
                    else if (systemSectionIndex != -1 && trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        // Reached next section without finding check_foreground_window
                        break;
                    }
                }
                
                if (enabled)
                {
                    if (checkForegroundIndex != -1)
                    {
                        // Update existing line
                        lines[checkForegroundIndex] = "check_foreground_window = 0";
                        Logger.LogInfo("Updated check_foreground_window = 0 in d3dx.ini");
                    }
                    else if (systemSectionIndex != -1)
                    {
                        // Add to existing [System] section
                        lines.Insert(systemSectionIndex + 1, "check_foreground_window = 0");
                        Logger.LogInfo("Added check_foreground_window = 0 to existing [System] section in d3dx.ini");
                    }
                    else
                    {
                        // Create [System] section at the beginning
                        lines.Insert(0, "");
                        lines.Insert(0, "check_foreground_window = 0");
                        lines.Insert(0, "[System]");
                        Logger.LogInfo("Created [System] section with check_foreground_window = 0 in d3dx.ini");
                    }
                }
                else
                {
                    if (checkForegroundIndex != -1)
                    {
                        // Remove or comment out the line
                        lines[checkForegroundIndex] = "; check_foreground_window = 0  ; Disabled by FlairX";
                        Logger.LogInfo("Disabled check_foreground_window in d3dx.ini");
                    }
                }
                
                // Write back to file
                System.IO.File.WriteAllLines(d3dxIniPath, lines);
                Logger.LogInfo($"Successfully modified d3dx.ini at: {d3dxIniPath}");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("Failed to modify d3dx.ini", ex);
            }
        }

        private void GamepadEnabledToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            SettingsManager.Current.GamepadEnabled = GamepadEnabledToggle.IsOn;
            SettingsManager.Save();
            
            // Refresh global gamepad manager
            var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
            mainWindow?.RefreshGlobalGamepad();
            
            Logger.LogInfo($"Gamepad enabled: {GamepadEnabledToggle.IsOn}");
        }

        private void UseLeftStickToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            SettingsManager.Current.GamepadUseLeftStick = UseLeftStickToggle.IsOn;
            SettingsManager.Save();
            
            Logger.LogInfo($"Use left stick: {UseLeftStickToggle.IsOn}");
        }

        private void UseRightStickForHotkeysToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            SettingsManager.Current.GamepadUseRightStickForHotkeys = UseRightStickForHotkeysToggle.IsOn;
            SettingsManager.Save();
            
            Logger.LogInfo($"Use right stick for hotkeys: {UseRightStickForHotkeysToggle.IsOn}");
        }

        private void VibrateOnNavToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            SettingsManager.Current.GamepadVibrateOnNavigation = VibrateOnNavToggle.IsOn;
            SettingsManager.Save();
            
            Logger.LogInfo($"Vibrate on navigation: {VibrateOnNavToggle.IsOn}");
        }

        private void UpdateOverlayHotkeysSectionState(bool enabled)
        {
            try
            {
                // Update hotkeys header opacity
                if (ControlsSectionHeader != null) ControlsSectionHeader.Opacity = enabled ? 1.0 : 0.5;
                
                // Update the entire OverlayHotkeysPanel opacity and disable/enable all children
                if (OverlayHotkeysPanel != null)
                {
                    OverlayHotkeysPanel.Opacity = enabled ? 1.0 : 0.5;
                    
                    // Enable/disable all child controls in the hotkeys panel
                    foreach (var child in OverlayHotkeysPanel.Children)
                    {
                        if (child is Control control)
                        {
                            control.IsEnabled = enabled;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error updating overlay hotkeys section state", ex);
            }
        }

        private void GamepadTestButton_Click(object sender, RoutedEventArgs e)
        {
            CheckGamepadStatus();
            
            // Vibrate if connected
            if (_testGamepad != null && _testGamepad.CheckConnection())
            {
                _testGamepad.Vibrate(40000, 40000, 200);
            }
        }

        private void ThemeSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            if (_isInitializing) return;
            
            if (sender.SelectedItem is SelectorBarItem selectedItem && selectedItem.Tag is string theme)
            {
                SettingsManager.Current.OverlayTheme = theme;
                SettingsManager.Save();
                
                // Apply theme to overlay window if open
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                mainWindow?.UpdateOverlayTheme();
                
                Logger.LogInfo($"Overlay theme changed to: {theme}");
            }
        }

        private void BackdropSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            if (_isInitializing) return;
            
            if (sender.SelectedItem is SelectorBarItem selectedItem && selectedItem.Tag is string backdrop)
            {
                SettingsManager.Current.OverlayBackdrop = backdrop;
                SettingsManager.Save();
                
                // Apply backdrop to overlay window if open
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                mainWindow?.UpdateOverlayBackdrop(backdrop);
                
                Logger.LogInfo($"Overlay backdrop changed to: {backdrop}");
            }
        }

        private void TestOverlayButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
            mainWindow?.ToggleOverlayWindow();
        }
    }
}
