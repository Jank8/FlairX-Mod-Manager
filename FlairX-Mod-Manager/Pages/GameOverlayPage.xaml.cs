using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class GameOverlayPage : Page
    {
        private bool _isInitializing = true;
        private GamepadManager? _testGamepad;

        public GameOverlayPage()
        {
            InitializeComponent();
            LoadSettings();
            UpdateTexts();
            _isInitializing = false;
            
            // Check gamepad status on load
            CheckGamepadStatus();
            
            this.Unloaded += (s, e) =>
            {
                _testGamepad?.Dispose();
                _testGamepad = null;
            };
        }

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
            if (OverlayHotkeyDescription != null) OverlayHotkeyDescription.Text = SharedUtilities.GetTranslation(lang, "ToggleHotkey_Description");
            
            // Filter Active Hotkey
            if (FilterActiveHotkeyLabel != null) FilterActiveHotkeyLabel.Text = SharedUtilities.GetTranslation(lang, "FilterActiveHotkey_Label");
            if (FilterActiveHotkeyDescription != null) FilterActiveHotkeyDescription.Text = SharedUtilities.GetTranslation(lang, "FilterActiveHotkey_Description");
            
            // Send F10 on Overlay Close
            if (SendF10OnOverlayCloseLabel != null) SendF10OnOverlayCloseLabel.Text = SharedUtilities.GetTranslation(lang, "SendF10OnOverlayClose_Label");
            if (SendF10OnOverlayCloseDescription != null) SendF10OnOverlayCloseDescription.Text = SharedUtilities.GetTranslation(lang, "SendF10OnOverlayClose_Description");
            
            // Gamepad Enabled
            if (GamepadEnabledLabel != null) GamepadEnabledLabel.Text = SharedUtilities.GetTranslation(lang, "GamepadEnabled_Label");
            if (GamepadEnabledDescription != null) GamepadEnabledDescription.Text = SharedUtilities.GetTranslation(lang, "GamepadEnabled_Description");
            
            // Use Left Stick
            if (UseLeftStickLabel != null) UseLeftStickLabel.Text = SharedUtilities.GetTranslation(lang, "UseLeftStick_Label");
            if (UseLeftStickDescription != null) UseLeftStickDescription.Text = SharedUtilities.GetTranslation(lang, "UseLeftStick_Description");
            
            // Vibrate on Navigation
            if (VibrateOnNavLabel != null) VibrateOnNavLabel.Text = SharedUtilities.GetTranslation(lang, "VibrateOnNav_Label");
            if (VibrateOnNavDescription != null) VibrateOnNavDescription.Text = SharedUtilities.GetTranslation(lang, "VibrateOnNav_Description");
            
            // Controller Status
            if (GamepadStatusLabel != null) GamepadStatusLabel.Text = SharedUtilities.GetTranslation(lang, "ControllerStatus_Label");
            if (TestButtonText != null) TestButtonText.Text = SharedUtilities.GetTranslation(lang, "ControllerStatus_Test");
            
            // Select Button
            if (SelectButtonLabel != null) SelectButtonLabel.Text = SharedUtilities.GetTranslation(lang, "SelectButton_Label");
            if (SelectButtonDescription != null) SelectButtonDescription.Text = SharedUtilities.GetTranslation(lang, "SelectButton_Description");
            
            // Back Button
            if (BackButtonLabel != null) BackButtonLabel.Text = SharedUtilities.GetTranslation(lang, "BackButton_Label");
            if (BackButtonDescription != null) BackButtonDescription.Text = SharedUtilities.GetTranslation(lang, "BackButton_Description");
            
            // Next Category
            if (NextCategoryButtonLabel != null) NextCategoryButtonLabel.Text = SharedUtilities.GetTranslation(lang, "NextCategory_Label");
            if (NextCategoryButtonDescription != null) NextCategoryButtonDescription.Text = SharedUtilities.GetTranslation(lang, "NextCategory_Description");
            
            // Previous Category
            if (PrevCategoryButtonLabel != null) PrevCategoryButtonLabel.Text = SharedUtilities.GetTranslation(lang, "PrevCategory_Label");
            if (PrevCategoryButtonDescription != null) PrevCategoryButtonDescription.Text = SharedUtilities.GetTranslation(lang, "PrevCategory_Description");
            
            // Toggle Overlay Combo
            if (GamepadComboLabel != null) GamepadComboLabel.Text = SharedUtilities.GetTranslation(lang, "ToggleOverlayCombo_Label");
            if (GamepadComboDescription != null) GamepadComboDescription.Text = SharedUtilities.GetTranslation(lang, "ToggleOverlayCombo_Description");
            
            // Filter Active Combo
            if (FilterActiveComboLabel != null) FilterActiveComboLabel.Text = SharedUtilities.GetTranslation(lang, "FilterActiveCombo_Label");
            if (FilterActiveComboDescription != null) FilterActiveComboDescription.Text = SharedUtilities.GetTranslation(lang, "FilterActiveCombo_Description");
            
            // Toggle switch labels
            UpdateToggleSwitchLabels(lang);
        }

        private void UpdateToggleSwitchLabels(Dictionary<string, string> lang)
        {
            var onText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_On");
            var offText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_Off");
            
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
            
            // Load hotkey
            OverlayHotkeyTextBox.Text = settings.ToggleOverlayHotkey ?? "Alt+W";
            FilterActiveHotkeyTextBox.Text = settings.FilterActiveHotkey ?? "Alt+A";
            
            // Load Send F10 on overlay close
            SendF10OnOverlayCloseToggle.IsOn = settings.SendF10OnOverlayClose;
            
            // Ensure background_keypress.ini exists if setting is enabled
            if (settings.SendF10OnOverlayClose)
            {
                EnsureBackgroundKeypressIni(true);
            }
            
            // Load gamepad enabled
            GamepadEnabledToggle.IsOn = settings.GamepadEnabled;
            
            // Load gamepad combos
            GamepadComboTextBox.Text = settings.GamepadToggleOverlayCombo ?? "Back+Start";
            FilterActiveComboTextBox.Text = settings.GamepadFilterActiveCombo ?? "Back+A";
            
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
            VibrateOnNavToggle.IsOn = settings.GamepadVibrateOnNavigation;
            
            // Load gamepad hotkeys
            SelectButtonTextBox.Text = settings.GamepadSelectButton ?? "A";
            BackButtonTextBox.Text = settings.GamepadBackButton ?? "B";
            NextCategoryButtonTextBox.Text = settings.GamepadNextCategoryButton ?? "RB";
            PrevCategoryButtonTextBox.Text = settings.GamepadPrevCategoryButton ?? "LB";
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

        private async void SendF10OnOverlayCloseToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            // If enabling and not already running as admin, show admin requirement dialog
            if (SendF10OnOverlayCloseToggle.IsOn && !IsRunningAsAdmin())
            {
                var lang = SharedUtilities.LoadLanguageDictionary("Overlay");
                
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "AutoReload_AdminRequired_Title"),
                    Content = SharedUtilities.GetTranslation(lang, "AutoReload_AdminRequired_Content"),
                    PrimaryButtonText = SharedUtilities.GetTranslation(lang, "AutoReload_AdminRequired_Yes"),
                    CloseButtonText = SharedUtilities.GetTranslation(lang, "AutoReload_AdminRequired_Cancel"),
                    XamlRoot = this.XamlRoot
                };
                
                var result = await dialog.ShowAsync();
                
                if (result == ContentDialogResult.Primary)
                {
                    // Save settings before restart - enable both auto-reload and run as admin
                    SettingsManager.Current.SendF10OnOverlayClose = true;
                    SettingsManager.Current.RunAsAdminEnabled = true;
                    SettingsManager.Save();
                    
                    // Create background_keypress.ini
                    EnsureBackgroundKeypressIni(true);
                    
                    Logger.LogInfo("Auto-reload enabled, RunAsAdminEnabled set to true, restarting as admin...");
                    
                    // Restart application as administrator
                    RestartAsAdmin();
                }
                else
                {
                    // User cancelled - revert toggle
                    _isInitializing = true;
                    SendF10OnOverlayCloseToggle.IsOn = false;
                    _isInitializing = false;
                    return;
                }
            }
            else
            {
                SettingsManager.Current.SendF10OnOverlayClose = SendF10OnOverlayCloseToggle.IsOn;
                
                // If disabling auto-reload, also disable run as admin
                if (!SendF10OnOverlayCloseToggle.IsOn)
                {
                    SettingsManager.Current.RunAsAdminEnabled = false;
                }
                
                SettingsManager.Save();
                
                // Create or remove background_keypress.ini file
                EnsureBackgroundKeypressIni(SendF10OnOverlayCloseToggle.IsOn);
                
                Logger.LogInfo($"Auto-reload mods: {SendF10OnOverlayCloseToggle.IsOn}, RunAsAdminEnabled: {SettingsManager.Current.RunAsAdminEnabled}");
            }
        }
        
        /// <summary>
        /// Check if the application is running with administrator privileges
        /// </summary>
        private static bool IsRunningAsAdmin()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Restart the application with administrator privileges
        /// </summary>
        private static void RestartAsAdmin()
        {
            try
            {
                var exePath = System.Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;
                
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = System.IO.Path.GetDirectoryName(exePath)
                };
                
                System.Diagnostics.Process.Start(startInfo);
                
                // Close current application
                System.Environment.Exit(0);
            }
            catch (System.Exception ex)
            {
                Logger.LogError("Failed to restart as admin", ex);
            }
        }
        
        /// <summary>
        /// Creates or removes background_keypress.ini file in Mods folder.
        /// This file tells 3DMigoto to accept keypresses even when game is not in foreground.
        /// </summary>
        private void EnsureBackgroundKeypressIni(bool enabled)
        {
            try
            {
                var modsPath = SettingsManager.GetCurrentXXMIModsDirectory();
                if (string.IsNullOrEmpty(modsPath) || !System.IO.Directory.Exists(modsPath))
                {
                    Logger.LogWarning("Cannot create background_keypress.ini - Mods directory not found");
                    return;
                }
                
                var iniPath = System.IO.Path.Combine(modsPath, "background_keypress.ini");
                
                if (enabled)
                {
                    // Create the ini file with setting to accept background keypresses
                    var content = "[System]\ncheck_foreground_window = 0\n";
                    System.IO.File.WriteAllText(iniPath, content);
                    Logger.LogInfo($"Created background_keypress.ini at: {iniPath}");
                }
                else
                {
                    // Remove the ini file if it exists
                    if (System.IO.File.Exists(iniPath))
                    {
                        System.IO.File.Delete(iniPath);
                        Logger.LogInfo($"Removed background_keypress.ini from: {iniPath}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError("Failed to manage background_keypress.ini", ex);
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

        private void VibrateOnNavToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            SettingsManager.Current.GamepadVibrateOnNavigation = VibrateOnNavToggle.IsOn;
            SettingsManager.Save();
            
            Logger.LogInfo($"Vibrate on navigation: {VibrateOnNavToggle.IsOn}");
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

        #region Gamepad Combo Recording

        private bool _isRecordingCombo = false;
        private bool _isRecordingFilterCombo = false; // true = filter active, false = toggle overlay
        private HashSet<string> _recordedButtons = new();
        private DispatcherTimer? _comboRecordTimer;

        private void GamepadComboTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Show current combo
            GamepadComboTextBox.Text = SettingsManager.Current.GamepadToggleOverlayCombo ?? "Back+Start";
        }

        private void FilterActiveComboTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Show current combo
            FilterActiveComboTextBox.Text = SettingsManager.Current.GamepadFilterActiveCombo ?? "Back+A";
        }

        private void GamepadComboRecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecordingCombo)
            {
                StopRecordingCombo();
            }
            else
            {
                _isRecordingFilterCombo = false;
                StartRecordingCombo();
            }
        }

        private void FilterActiveComboRecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecordingCombo)
            {
                StopRecordingCombo();
            }
            else
            {
                _isRecordingFilterCombo = true;
                StartRecordingCombo();
            }
        }

        private void StartRecordingCombo()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("Overlay");
            
            // Ensure gamepad is available
            if (_testGamepad == null)
            {
                _testGamepad = new GamepadManager();
            }
            
            if (!_testGamepad.CheckConnection())
            {
                var noControllerText = SharedUtilities.GetTranslation(lang, "Recording_NoController");
                if (_isRecordingFilterCombo)
                    FilterActiveComboTextBox.Text = noControllerText;
                else
                    GamepadComboTextBox.Text = noControllerText;
                return;
            }

            _isRecordingCombo = true;
            _recordedButtons.Clear();
            
            var pressButtonsText = SharedUtilities.GetTranslation(lang, "Recording_PressButtons");
            if (_isRecordingFilterCombo)
            {
                FilterActiveComboTextBox.Text = pressButtonsText;
                FilterActiveComboRecordButton.Content = new FontIcon { Glyph = "\uE71A", FontSize = 14 };
            }
            else
            {
                GamepadComboTextBox.Text = pressButtonsText;
                GamepadComboRecordButton.Content = new FontIcon { Glyph = "\uE71A", FontSize = 14 };
            }
            
            // Subscribe to button events
            _testGamepad.ButtonPressed += OnComboButtonPressed;
            _testGamepad.StartPolling();
            
            // Auto-stop after 5 seconds
            _comboRecordTimer = new DispatcherTimer();
            _comboRecordTimer.Interval = System.TimeSpan.FromSeconds(5);
            _comboRecordTimer.Tick += (s, e) => StopRecordingCombo();
            _comboRecordTimer.Start();
        }

        private void StopRecordingCombo()
        {
            _isRecordingCombo = false;
            _comboRecordTimer?.Stop();
            _comboRecordTimer = null;
            
            if (_testGamepad != null)
            {
                _testGamepad.ButtonPressed -= OnComboButtonPressed;
                _testGamepad.StopPolling();
            }
            
            // Reset button icons
            GamepadComboRecordButton.Content = new FontIcon { Glyph = "\uE7C8", FontSize = 14 };
            FilterActiveComboRecordButton.Content = new FontIcon { Glyph = "\uE7C8", FontSize = 14 };
            
            // Save combo if we recorded something
            if (_recordedButtons.Count > 0)
            {
                var combo = string.Join("+", _recordedButtons);
                
                if (_isRecordingFilterCombo)
                {
                    FilterActiveComboTextBox.Text = combo;
                    SettingsManager.Current.GamepadFilterActiveCombo = combo;
                    Logger.LogInfo($"Gamepad filter active combo set to: {combo}");
                }
                else
                {
                    GamepadComboTextBox.Text = combo;
                    SettingsManager.Current.GamepadToggleOverlayCombo = combo;
                    Logger.LogInfo($"Gamepad overlay combo set to: {combo}");
                }
                
                SettingsManager.Save();
                
                // Refresh global gamepad
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                mainWindow?.RefreshGlobalGamepad();
                
                // Vibrate to confirm
                _testGamepad?.Vibrate(30000, 30000, 100);
            }
            else
            {
                if (_isRecordingFilterCombo)
                    FilterActiveComboTextBox.Text = SettingsManager.Current.GamepadFilterActiveCombo ?? "Back+A";
                else
                    GamepadComboTextBox.Text = SettingsManager.Current.GamepadToggleOverlayCombo ?? "Back+Start";
            }
            
            _recordedButtons.Clear();
        }

        private void OnComboButtonPressed(object? sender, GamepadButtonEventArgs e)
        {
            if (!_isRecordingCombo) return;
            
            var buttonName = e.GetButtonDisplayName();
            
            // Use display names directly for readability
            _recordedButtons.Add(buttonName);
            
            DispatcherQueue.TryEnqueue(() =>
            {
                var comboText = string.Join("+", _recordedButtons);
                if (_isRecordingFilterCombo)
                    FilterActiveComboTextBox.Text = comboText;
                else
                    GamepadComboTextBox.Text = comboText;
            });
        }

        #endregion

        private void HotkeyTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                e.Handled = true;
                
                var key = e.Key;
                var modifiers = new List<string>();
                
                // Check for modifier keys
                if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    modifiers.Add("Ctrl");
                if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    modifiers.Add("Shift");
                if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    modifiers.Add("Alt");
                
                // Skip modifier-only keys
                if (key == Windows.System.VirtualKey.Control || key == Windows.System.VirtualKey.Shift || 
                    key == Windows.System.VirtualKey.Menu || key == Windows.System.VirtualKey.LeftWindows || 
                    key == Windows.System.VirtualKey.RightWindows)
                    return;
                
                // Build hotkey string
                var hotkeyParts = new List<string>(modifiers);
                hotkeyParts.Add(key.ToString());
                
                var newHotkey = string.Join("+", hotkeyParts);
                textBox.Text = newHotkey;
            }
        }

        private void OverlayHotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            var newHotkey = OverlayHotkeyTextBox.Text;
            if (!string.IsNullOrWhiteSpace(newHotkey))
            {
                SettingsManager.Current.ToggleOverlayHotkey = newHotkey;
                SettingsManager.Save();
                
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                mainWindow?.RefreshGlobalHotkeys();
                
                Logger.LogInfo($"Overlay hotkey set to: {newHotkey}");
            }
        }

        private void FilterActiveHotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            var newHotkey = FilterActiveHotkeyTextBox.Text;
            if (!string.IsNullOrWhiteSpace(newHotkey))
            {
                SettingsManager.Current.FilterActiveHotkey = newHotkey;
                SettingsManager.Save();
                
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                mainWindow?.RefreshGlobalHotkeys();
                
                Logger.LogInfo($"Filter active hotkey set to: {newHotkey}");
            }
        }

        private void OverlayHotkeyConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // Remove focus from hotkey textbox
            if (sender is Button button)
            {
                button.Focus(FocusState.Programmatic);
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

        #region Single Button Recording

        private bool _isRecordingSingleButton = false;
        private string _recordingButtonType = ""; // "select", "back", "next", "prev"

        private void SelectButtonTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            SelectButtonTextBox.Text = SettingsManager.Current.GamepadSelectButton ?? "A";
        }

        private void BackButtonTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            BackButtonTextBox.Text = SettingsManager.Current.GamepadBackButton ?? "B";
        }

        private void NextCategoryButtonTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            NextCategoryButtonTextBox.Text = SettingsManager.Current.GamepadNextCategoryButton ?? "RB";
        }

        private void PrevCategoryButtonTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            PrevCategoryButtonTextBox.Text = SettingsManager.Current.GamepadPrevCategoryButton ?? "LB";
        }

        private void SelectButtonRecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecordingSingleButton) StopRecordingSingleButton();
            else StartRecordingSingleButton("select", SelectButtonTextBox, SelectButtonRecordButton);
        }

        private void BackButtonRecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecordingSingleButton) StopRecordingSingleButton();
            else StartRecordingSingleButton("back", BackButtonTextBox, BackButtonRecordButton);
        }

        private void NextCategoryButtonRecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecordingSingleButton) StopRecordingSingleButton();
            else StartRecordingSingleButton("next", NextCategoryButtonTextBox, NextCategoryButtonRecordButton);
        }

        private void PrevCategoryButtonRecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecordingSingleButton) StopRecordingSingleButton();
            else StartRecordingSingleButton("prev", PrevCategoryButtonTextBox, PrevCategoryButtonRecordButton);
        }

        private TextBox? _currentRecordingTextBox;
        private Button? _currentRecordingButton;

        private void StartRecordingSingleButton(string buttonType, TextBox textBox, Button recordButton)
        {
            var lang = SharedUtilities.LoadLanguageDictionary("Overlay");
            
            if (_testGamepad == null)
            {
                _testGamepad = new GamepadManager();
            }
            
            if (!_testGamepad.CheckConnection())
            {
                textBox.Text = SharedUtilities.GetTranslation(lang, "Recording_NoController");
                return;
            }

            _isRecordingSingleButton = true;
            _recordingButtonType = buttonType;
            _currentRecordingTextBox = textBox;
            _currentRecordingButton = recordButton;
            
            textBox.Text = SharedUtilities.GetTranslation(lang, "Recording_PressButton");
            recordButton.Content = new FontIcon { Glyph = "\uE71A", FontSize = 14 };
            
            _testGamepad.ButtonPressed += OnSingleButtonPressed;
            _testGamepad.StartPolling();
        }

        private void StopRecordingSingleButton()
        {
            _isRecordingSingleButton = false;
            
            if (_testGamepad != null)
            {
                _testGamepad.ButtonPressed -= OnSingleButtonPressed;
                _testGamepad.StopPolling();
            }
            
            // Reset button icon
            if (_currentRecordingButton != null)
            {
                _currentRecordingButton.Content = new FontIcon { Glyph = "\uE7C8", FontSize = 14 };
            }
            
            // Restore text if nothing recorded
            var lang = SharedUtilities.LoadLanguageDictionary("Overlay");
            var pressButtonText = SharedUtilities.GetTranslation(lang, "Recording_PressButton");
            if (_currentRecordingTextBox?.Text == pressButtonText)
            {
                switch (_recordingButtonType)
                {
                    case "select": _currentRecordingTextBox.Text = SettingsManager.Current.GamepadSelectButton ?? "A"; break;
                    case "back": _currentRecordingTextBox.Text = SettingsManager.Current.GamepadBackButton ?? "B"; break;
                    case "next": _currentRecordingTextBox.Text = SettingsManager.Current.GamepadNextCategoryButton ?? "RB"; break;
                    case "prev": _currentRecordingTextBox.Text = SettingsManager.Current.GamepadPrevCategoryButton ?? "LB"; break;
                }
            }
            
            _currentRecordingTextBox = null;
            _currentRecordingButton = null;
            _recordingButtonType = "";
        }

        private void OnSingleButtonPressed(object? sender, GamepadButtonEventArgs e)
        {
            if (!_isRecordingSingleButton) return;
            
            var buttonName = e.GetButtonDisplayName();
            
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_currentRecordingTextBox != null)
                {
                    _currentRecordingTextBox.Text = buttonName;
                }
                
                // Save to settings
                switch (_recordingButtonType)
                {
                    case "select":
                        SettingsManager.Current.GamepadSelectButton = buttonName;
                        break;
                    case "back":
                        SettingsManager.Current.GamepadBackButton = buttonName;
                        break;
                    case "next":
                        SettingsManager.Current.GamepadNextCategoryButton = buttonName;
                        break;
                    case "prev":
                        SettingsManager.Current.GamepadPrevCategoryButton = buttonName;
                        break;
                }
                
                SettingsManager.Save();
                Logger.LogInfo($"Gamepad {_recordingButtonType} button set to: {buttonName}");
                
                // Vibrate to confirm
                _testGamepad?.Vibrate(30000, 30000, 100);
                
                StopRecordingSingleButton();
            });
        }

        #endregion
    }
}
