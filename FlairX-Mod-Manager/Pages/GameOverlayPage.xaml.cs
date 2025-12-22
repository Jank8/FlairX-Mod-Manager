using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class GameOverlayPage : Page
    {
        private bool _isInitializing = true;
        private GamepadManager? _testGamepad;
        private bool _previousHotkeysEnabled = true;
        
        // Hotkey editing state
        private TextBox? _activeHotkeyEditBox;

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
            
            // Filter Active Hotkey
            if (FilterActiveHotkeyLabel != null) FilterActiveHotkeyLabel.Text = SharedUtilities.GetTranslation(lang, "FilterActiveHotkey_Label");
            
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
            var overlayHotkey = settings.ToggleOverlayHotkey ?? "Alt+W";
            var filterActiveHotkey = settings.FilterActiveHotkey ?? "Alt+A";
            
            // Initialize hotkey visual panels
            InitializeHotkeyPanel(OverlayHotkeyKeysPanel, overlayHotkey);
            InitializeHotkeyPanel(FilterActiveHotkeyKeysPanel, filterActiveHotkey);
            
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
            
            // Initialize gamepad button keys panels (convert to Kenney format if needed)
            InitializeHotkeyPanel(SelectButtonKeysPanel, ConvertToKenneyFormat(settings.GamepadSelectButton ?? "A"));
            InitializeHotkeyPanel(BackButtonKeysPanel, ConvertToKenneyFormat(settings.GamepadBackButton ?? "B"));
            InitializeHotkeyPanel(NextCategoryButtonKeysPanel, ConvertToKenneyFormat(settings.GamepadNextCategoryButton ?? "RB"));
            InitializeHotkeyPanel(PrevCategoryButtonKeysPanel, ConvertToKenneyFormat(settings.GamepadPrevCategoryButton ?? "LB"));
            
            // Initialize combo panels
            var toggleCombo = settings.GamepadToggleOverlayCombo ?? "Back+Start";
            var filterCombo = settings.GamepadFilterActiveCombo ?? "Back+A";
            InitializeHotkeyPanel(GamepadComboKeysPanel, ConvertComboToKenneyFormat(toggleCombo));
            InitializeHotkeyPanel(FilterActiveComboKeysPanel, ConvertComboToKenneyFormat(filterCombo));
        }
        
        private string ConvertToKenneyFormat(string button)
        {
            if (button.StartsWith("XB ")) return button;
            return "XB " + button;
        }
        
        private string ConvertComboToKenneyFormat(string combo)
        {
            return string.Join("+", combo.Split('+').Select(b => ConvertToKenneyFormat(b.Trim())));
        }
        
        private void InitializeHotkeyPanel(StackPanel panel, string keyCombo)
        {
            panel.Children.Clear();
            var keyBackground = (Brush)Application.Current.Resources["HotkeyKeyBackground"];
            var keysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(keyCombo, keyBackground, 64);
            
            // Copy children from keysPanel to our panel
            while (keysPanel.Children.Count > 0)
            {
                var child = keysPanel.Children[0];
                keysPanel.Children.RemoveAt(0);
                panel.Children.Add(child);
            }
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
        private bool _isRecordingFilterCombo = false;
        private HashSet<string> _recordedComboButtons = new();
        private DispatcherTimer? _comboHoldTimer;
        private int _comboHoldCountdown = 3;
        private string _pendingCombo = "";
        private TextBlock? _comboCountdownTextBlock;
        private bool _isSettingComboText = false;

        private void GamepadComboTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            GamepadComboTextBox.Text = SettingsManager.Current.GamepadToggleOverlayCombo ?? "Back+Start";
        }

        private void FilterActiveComboTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            FilterActiveComboTextBox.Text = SettingsManager.Current.GamepadFilterActiveCombo ?? "Back+A";
        }

        private void GamepadComboRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isRecordingCombo)
            {
                StopComboRecording();
                return;
            }
            _isRecordingFilterCombo = false;
            StartComboRecording(GamepadComboTextBox, GamepadComboRecordButton, GamepadComboKeysPanel, GamepadComboSaveButton);
        }

        private void FilterActiveComboRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isRecordingCombo)
            {
                StopComboRecording();
                return;
            }
            _isRecordingFilterCombo = true;
            StartComboRecording(FilterActiveComboTextBox, FilterActiveComboRecordButton, FilterActiveComboKeysPanel, FilterActiveComboSaveButton);
        }

        private TextBox? _activeComboTextBox;
        private Border? _activeComboRecButton;
        private StackPanel? _activeComboKeysPanel;
        private Border? _activeComboSaveButton;

        private void StartComboRecording(TextBox textBox, Border recButton, StackPanel keysPanel, Border saveButton)
        {
            var lang = SharedUtilities.LoadLanguageDictionary("Overlay");
            
            if (_testGamepad == null)
                _testGamepad = new GamepadManager();
            
            if (!_testGamepad.CheckConnection())
            {
                textBox.Text = SharedUtilities.GetTranslation(lang, "Recording_NoController");
                textBox.Visibility = Visibility.Visible;
                keysPanel.Visibility = Visibility.Collapsed;
                return;
            }

            _activeComboTextBox = textBox;
            _activeComboRecButton = recButton;
            _activeComboKeysPanel = keysPanel;
            _activeComboSaveButton = saveButton;
            _recordedComboButtons.Clear();
            _pendingCombo = "";
            _comboHoldCountdown = 3;

            _isRecordingCombo = true;
            
            // Show TextBox and Save button, hide KeysPanel
            textBox.Visibility = Visibility.Visible;
            textBox.Text = "";
            textBox.PlaceholderText = SharedUtilities.GetTranslation(lang, "Recording_PressButtons");
            keysPanel.Visibility = Visibility.Collapsed;
            saveButton.Visibility = Visibility.Visible;
            
            if (recButton.Child is FontIcon icon)
                icon.Glyph = "\uE71A"; // Stop icon
            
            _testGamepad.ButtonPressed += OnComboButtonPressed;
            _testGamepad.ButtonReleased += OnComboButtonReleased;
            _testGamepad.StartPolling();
        }

        private void StopComboRecording()
        {
            _isRecordingCombo = false;
            _comboHoldTimer?.Stop();
            _comboHoldTimer = null;
            _comboHoldCountdown = 3;
            _pendingCombo = "";
            
            if (_testGamepad != null)
            {
                _testGamepad.ButtonPressed -= OnComboButtonPressed;
                _testGamepad.ButtonReleased -= OnComboButtonReleased;
                _testGamepad.StopPolling();
            }
            
            if (_activeComboRecButton?.Child is FontIcon icon)
                icon.Glyph = "\uE7C8"; // Rec icon
            
            if (_activeComboTextBox != null)
                _activeComboTextBox.PlaceholderText = "Press buttons...";
            
            // Hide countdown
            if (_comboCountdownTextBlock != null)
                _comboCountdownTextBlock.Visibility = Visibility.Collapsed;
            
            _activeComboRecButton = null;
            _recordedComboButtons.Clear();
        }

        private void OnComboButtonReleased(object? sender, GamepadButtonEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _comboHoldTimer?.Stop();
                _comboHoldTimer = null;
                _comboHoldCountdown = 3;
                
                if (_activeComboTextBox != null)
                {
                    _isSettingComboText = true;
                    
                    if (_recordedComboButtons.Count <= 1)
                    {
                        // Single button - keep it
                        _activeComboTextBox.Text = _pendingCombo;
                    }
                    else
                    {
                        // Combo released before 3s - clear it
                        _activeComboTextBox.Text = "";
                        _pendingCombo = "";
                    }
                    
                    _recordedComboButtons.Clear();
                    _isSettingComboText = false;
                }
                
                // Hide countdown
                if (_comboCountdownTextBlock != null)
                    _comboCountdownTextBlock.Visibility = Visibility.Collapsed;
            });
        }

        private void OnComboButtonPressed(object? sender, GamepadButtonEventArgs e)
        {
            if (!_isRecordingCombo) return;
            
            var buttonName = e.GetButtonDisplayName();
            
            if (!_recordedComboButtons.Contains(buttonName))
                _recordedComboButtons.Add(buttonName);
            
            _pendingCombo = string.Join("+", _recordedComboButtons);
            _comboHoldCountdown = 3;
            
            DispatcherQueue.TryEnqueue(() =>
            {
                _comboHoldTimer?.Stop();
                
                if (_activeComboTextBox != null)
                {
                    _isSettingComboText = true;
                    _activeComboTextBox.Text = _pendingCombo;
                    
                    if (_recordedComboButtons.Count == 1)
                    {
                        // Single button - no countdown
                        if (_comboCountdownTextBlock != null)
                            _comboCountdownTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        // Combo - start countdown timer
                        _comboHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                        _comboHoldTimer.Tick += ComboHoldTimer_Tick;
                        _comboHoldTimer.Start();
                        
                        // Show countdown (create if needed)
                        if (_comboCountdownTextBlock == null)
                        {
                            _comboCountdownTextBlock = new TextBlock
                            {
                                FontFamily = new FontFamily("ms-appx:///Assets/KenneyFonts/kenney_input_keyboard_mouse.ttf#Kenney Input Keyboard & Mouse"),
                                FontSize = 32,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = new Thickness(8, 0, 0, 0)
                            };
                            // Add to parent of TextBox
                            if (_activeComboTextBox?.Parent is StackPanel parent)
                            {
                                var idx = parent.Children.IndexOf(_activeComboTextBox);
                                if (idx >= 0 && idx < parent.Children.Count - 1)
                                    parent.Children.Insert(idx + 1, _comboCountdownTextBlock);
                                else
                                    parent.Children.Add(_comboCountdownTextBlock);
                            }
                        }
                        
                        var countdownGlyph = _comboHoldCountdown switch
                        {
                            3 => "\uE007",
                            2 => "\uE005",
                            1 => "\uE003",
                            _ => _comboHoldCountdown.ToString()
                        };
                        _comboCountdownTextBlock.Text = countdownGlyph;
                        _comboCountdownTextBlock.Visibility = Visibility.Visible;
                    }
                    
                    _isSettingComboText = false;
                }
            });
        }

        private void ComboHoldTimer_Tick(object? sender, object e)
        {
            _comboHoldCountdown--;
            
            if (_comboHoldCountdown <= 0)
            {
                _comboHoldTimer?.Stop();
                _comboHoldTimer = null;
                
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_activeComboTextBox != null)
                    {
                        _isSettingComboText = true;
                        _activeComboTextBox.Text = _pendingCombo;
                        _isSettingComboText = false;
                    }
                    
                    // Hide countdown
                    if (_comboCountdownTextBlock != null)
                        _comboCountdownTextBlock.Visibility = Visibility.Collapsed;
                    
                    // Vibrate to confirm combo locked
                    _testGamepad?.Vibrate(30000, 30000, 100);
                    
                    StopComboRecording();
                });
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_comboCountdownTextBlock != null)
                    {
                        var countdownGlyph = _comboHoldCountdown switch
                        {
                            3 => "\uE007",
                            2 => "\uE005",
                            1 => "\uE003",
                            _ => _comboHoldCountdown.ToString()
                        };
                        _comboCountdownTextBlock.Text = countdownGlyph;
                    }
                });
            }
        }

        // Save button handlers for combos
        private void GamepadComboSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SaveCombo(false);
        }

        private void FilterActiveComboSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SaveCombo(true);
        }

        private void SaveCombo(bool isFilterActive)
        {
            StopComboRecording();
            
            var textBox = isFilterActive ? FilterActiveComboTextBox : GamepadComboTextBox;
            var keysPanel = isFilterActive ? FilterActiveComboKeysPanel : GamepadComboKeysPanel;
            var saveButton = isFilterActive ? FilterActiveComboSaveButton : GamepadComboSaveButton;
            
            var combo = textBox.Text.Trim();
            if (string.IsNullOrEmpty(combo)) return;
            
            var kenneyCombo = ConvertComboToKenneyFormat(combo);
            
            if (isFilterActive)
            {
                SettingsManager.Current.GamepadFilterActiveCombo = combo;
                Logger.LogInfo($"Gamepad filter active combo set to: {combo}");
            }
            else
            {
                SettingsManager.Current.GamepadToggleOverlayCombo = combo;
                Logger.LogInfo($"Gamepad overlay combo set to: {combo}");
            }
            
            SettingsManager.Save();
            
            // Refresh global gamepad
            var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
            mainWindow?.RefreshGlobalGamepad();
            
            // Update keys panel
            InitializeHotkeyPanel(keysPanel, kenneyCombo);
            
            // Hide TextBox and Save, show KeysPanel
            textBox.Visibility = Visibility.Collapsed;
            saveButton.Visibility = Visibility.Collapsed;
            keysPanel.Visibility = Visibility.Visible;
            
            // Remove countdown if exists
            if (_comboCountdownTextBlock != null)
            {
                if (_comboCountdownTextBlock.Parent is StackPanel parent)
                    parent.Children.Remove(_comboCountdownTextBlock);
                _comboCountdownTextBlock = null;
            }
            
            _activeComboTextBox = null;
            _activeComboKeysPanel = null;
            _activeComboSaveButton = null;
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
                    modifiers.Add("CTRL");
                if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    modifiers.Add("SHIFT");
                if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    modifiers.Add("ALT");
                
                // Skip modifier-only keys
                if (key == Windows.System.VirtualKey.Control || key == Windows.System.VirtualKey.Shift || 
                    key == Windows.System.VirtualKey.Menu || key == Windows.System.VirtualKey.LeftWindows || 
                    key == Windows.System.VirtualKey.RightWindows || key == Windows.System.VirtualKey.LeftControl ||
                    key == Windows.System.VirtualKey.RightControl || key == Windows.System.VirtualKey.LeftShift ||
                    key == Windows.System.VirtualKey.RightShift || key == Windows.System.VirtualKey.LeftMenu ||
                    key == Windows.System.VirtualKey.RightMenu)
                    return;
                
                // Convert key to display string
                string keyStr = ConvertVirtualKeyToString(key);
                
                // Build hotkey string
                if (modifiers.Count > 0)
                    textBox.Text = string.Join("+", modifiers) + "+" + keyStr;
                else
                    textBox.Text = keyStr;
            }
        }
        
        private string ConvertVirtualKeyToString(Windows.System.VirtualKey key)
        {
            return key switch
            {
                >= Windows.System.VirtualKey.A and <= Windows.System.VirtualKey.Z => key.ToString(),
                >= Windows.System.VirtualKey.Number0 and <= Windows.System.VirtualKey.Number9 => ((int)key - (int)Windows.System.VirtualKey.Number0).ToString(),
                >= Windows.System.VirtualKey.NumberPad0 and <= Windows.System.VirtualKey.NumberPad9 => "NUM " + ((int)key - (int)Windows.System.VirtualKey.NumberPad0),
                >= Windows.System.VirtualKey.F1 and <= Windows.System.VirtualKey.F12 => "F" + ((int)key - (int)Windows.System.VirtualKey.F1 + 1),
                Windows.System.VirtualKey.Up => "↑",
                Windows.System.VirtualKey.Down => "↓",
                Windows.System.VirtualKey.Left => "←",
                Windows.System.VirtualKey.Right => "→",
                Windows.System.VirtualKey.Space => "SPACE",
                Windows.System.VirtualKey.Enter => "ENTER",
                Windows.System.VirtualKey.Tab => "TAB",
                Windows.System.VirtualKey.Escape => "ESC",
                Windows.System.VirtualKey.Back => "BACKSPACE",
                Windows.System.VirtualKey.Delete => "DEL",
                Windows.System.VirtualKey.Insert => "INS",
                Windows.System.VirtualKey.Home => "HOME",
                Windows.System.VirtualKey.End => "END",
                Windows.System.VirtualKey.PageUp => "PAGE UP",
                Windows.System.VirtualKey.PageDown => "PAGE DOWN",
                _ => key.ToString().ToUpper()
            };
        }

        private void OverlayHotkeyConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // This method is no longer used with the new UI
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

        private void SelectButtonRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isRecordingSingleButton) StopRecordingSingleButton();
            else StartRecordingSingleButton("select", SelectButtonTextBox, SelectButtonRecordButton, SelectButtonKeysPanel, SelectButtonSaveButton);
        }

        private void BackButtonRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isRecordingSingleButton) StopRecordingSingleButton();
            else StartRecordingSingleButton("back", BackButtonTextBox, BackButtonRecordButton, BackButtonKeysPanel, BackButtonSaveButton);
        }

        private void NextCategoryButtonRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isRecordingSingleButton) StopRecordingSingleButton();
            else StartRecordingSingleButton("next", NextCategoryButtonTextBox, NextCategoryButtonRecordButton, NextCategoryButtonKeysPanel, NextCategoryButtonSaveButton);
        }

        private void PrevCategoryButtonRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isRecordingSingleButton) StopRecordingSingleButton();
            else StartRecordingSingleButton("prev", PrevCategoryButtonTextBox, PrevCategoryButtonRecordButton, PrevCategoryButtonKeysPanel, PrevCategoryButtonSaveButton);
        }

        private TextBox? _currentRecordingTextBox;
        private Border? _currentRecordingButton;
        private Border? _currentRecordingSaveButton;
        private StackPanel? _currentRecordingKeysPanel;

        private void StartRecordingSingleButton(string buttonType, TextBox textBox, Border recordButton, StackPanel keysPanel, Border saveButton)
        {
            var lang = SharedUtilities.LoadLanguageDictionary("Overlay");
            
            if (_testGamepad == null)
            {
                _testGamepad = new GamepadManager();
            }
            
            if (!_testGamepad.CheckConnection())
            {
                textBox.Text = SharedUtilities.GetTranslation(lang, "Recording_NoController");
                textBox.Visibility = Visibility.Visible;
                keysPanel.Visibility = Visibility.Collapsed;
                return;
            }

            _isRecordingSingleButton = true;
            _recordingButtonType = buttonType;
            _currentRecordingTextBox = textBox;
            _currentRecordingButton = recordButton;
            _currentRecordingSaveButton = saveButton;
            _currentRecordingKeysPanel = keysPanel;
            
            // Show TextBox and Save button, hide KeysPanel
            textBox.Visibility = Visibility.Visible;
            keysPanel.Visibility = Visibility.Collapsed;
            saveButton.Visibility = Visibility.Visible;
            
            textBox.Text = SharedUtilities.GetTranslation(lang, "Recording_PressButton");
            if (recordButton.Child is FontIcon icon)
                icon.Glyph = "\uE71A"; // Stop icon
            
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
            if (_currentRecordingButton?.Child is FontIcon icon)
            {
                icon.Glyph = "\uE7C8"; // Record icon
            }
            
            // Hide TextBox and Save button, show KeysPanel
            if (_currentRecordingTextBox != null)
                _currentRecordingTextBox.Visibility = Visibility.Collapsed;
            if (_currentRecordingSaveButton != null)
                _currentRecordingSaveButton.Visibility = Visibility.Collapsed;
            if (_currentRecordingKeysPanel != null)
                _currentRecordingKeysPanel.Visibility = Visibility.Visible;
            
            _currentRecordingTextBox = null;
            _currentRecordingButton = null;
            _currentRecordingSaveButton = null;
            _currentRecordingKeysPanel = null;
            _recordingButtonType = "";
        }

        private void OnSingleButtonPressed(object? sender, GamepadButtonEventArgs e)
        {
            if (!_isRecordingSingleButton) return;
            
            var buttonName = e.GetButtonDisplayName();
            var kenneyButtonName = ConvertToKenneyFormat(buttonName);
            
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_currentRecordingTextBox != null)
                {
                    _currentRecordingTextBox.Text = buttonName;
                }
                
                // Stop polling but keep UI in edit mode - user must click Save
                if (_testGamepad != null)
                {
                    _testGamepad.ButtonPressed -= OnSingleButtonPressed;
                    _testGamepad.StopPolling();
                }
                _isRecordingSingleButton = false;
                
                // Reset record button icon
                if (_currentRecordingButton?.Child is FontIcon icon)
                    icon.Glyph = "\uE7C8";
                
                // Vibrate to confirm button was captured
                _testGamepad?.Vibrate(30000, 30000, 100);
            });
        }
        
        // Save button handlers for single buttons
        private void SelectButtonSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SaveSingleButton("select", SelectButtonTextBox, SelectButtonKeysPanel, SelectButtonSaveButton);
        }
        
        private void BackButtonSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SaveSingleButton("back", BackButtonTextBox, BackButtonKeysPanel, BackButtonSaveButton);
        }
        
        private void NextCategoryButtonSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SaveSingleButton("next", NextCategoryButtonTextBox, NextCategoryButtonKeysPanel, NextCategoryButtonSaveButton);
        }
        
        private void PrevCategoryButtonSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SaveSingleButton("prev", PrevCategoryButtonTextBox, PrevCategoryButtonKeysPanel, PrevCategoryButtonSaveButton);
        }
        
        private void SaveSingleButton(string buttonType, TextBox textBox, StackPanel keysPanel, Border saveButton)
        {
            var buttonName = textBox.Text.Trim();
            if (string.IsNullOrEmpty(buttonName)) return;
            
            var kenneyButtonName = ConvertToKenneyFormat(buttonName);
            
            // Save to settings
            switch (buttonType)
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
            Logger.LogInfo($"Gamepad {buttonType} button set to: {buttonName}");
            
            // Update keys panel
            InitializeHotkeyPanel(keysPanel, kenneyButtonName);
            
            // Hide TextBox and Save, show KeysPanel
            textBox.Visibility = Visibility.Collapsed;
            saveButton.Visibility = Visibility.Collapsed;
            keysPanel.Visibility = Visibility.Visible;
            
            // Clear state
            _currentRecordingTextBox = null;
            _currentRecordingButton = null;
            _currentRecordingSaveButton = null;
            _currentRecordingKeysPanel = null;
            _recordingButtonType = "";
        }

        #endregion

        // Additional methods for new XAML event handlers
        private void GamepadComboTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // Handle keyboard input like regular hotkey
            HotkeyTextBox_KeyDown(sender, e);
            
            // Save the combo
            if (sender is TextBox textBox && !string.IsNullOrEmpty(textBox.Text))
            {
                SettingsManager.Current.GamepadToggleOverlayCombo = textBox.Text;
                SettingsManager.Save();
                
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                mainWindow?.RefreshGlobalGamepad();
                
                Logger.LogInfo($"Gamepad overlay combo set to: {textBox.Text}");
            }
        }

        private void FilterActiveComboTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // Handle keyboard input like regular hotkey
            HotkeyTextBox_KeyDown(sender, e);
            
            // Save the combo
            if (sender is TextBox textBox && !string.IsNullOrEmpty(textBox.Text))
            {
                SettingsManager.Current.GamepadFilterActiveCombo = textBox.Text;
                SettingsManager.Save();
                
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                mainWindow?.RefreshGlobalGamepad();
                
                Logger.LogInfo($"Gamepad filter active combo set to: {textBox.Text}");
            }
        }

        private void GamepadComboTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Nothing special needed for combo textboxes on lost focus
        }

        private void FilterActiveComboTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Nothing special needed for combo textboxes on lost focus
        }

        private void SelectButtonTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Nothing special needed for single button textboxes on lost focus
        }

        private void BackButtonTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Nothing special needed for single button textboxes on lost focus
        }

        private void NextCategoryButtonTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Nothing special needed for single button textboxes on lost focus
        }

        private void PrevCategoryButtonTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Nothing special needed for single button textboxes on lost focus
        }
        
        // Hotkey action button hover effects
        private void HotkeyActionButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
                if (border.Child is FontIcon icon && icon.RenderTransform is ScaleTransform st)
                {
                    st.ScaleX = 1.1;
                    st.ScaleY = 1.1;
                }
                else if (border.Child is FontIcon iconNoTransform)
                {
                    iconNoTransform.RenderTransform = new ScaleTransform { ScaleX = 1.1, ScaleY = 1.1 };
                    iconNoTransform.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                }
            }
        }

        private void HotkeyActionButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                if (border.Child is FontIcon icon && icon.RenderTransform is ScaleTransform st)
                {
                    st.ScaleX = 1.0;
                    st.ScaleY = 1.0;
                }
            }
        }
        
        // Overlay Hotkey action buttons
        private void OverlayHotkeyEditButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_activeHotkeyEditBox != null) return;
            
            StartHotkeyEdit(OverlayHotkeyKeysPanel, OverlayHotkeyEditButton, OverlayHotkeySaveButton, 
                           SettingsManager.Current.ToggleOverlayHotkey ?? "Alt+W");
        }

        private void OverlayHotkeySaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SaveHotkeyEdit(OverlayHotkeyKeysPanel, OverlayHotkeyEditButton, OverlayHotkeySaveButton, "overlay");
        }

        private void OverlayHotkeyRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_activeHotkeyEditBox != null) return;
            
            var defaultHotkey = "Alt+W";
            SettingsManager.Current.ToggleOverlayHotkey = defaultHotkey;
            SettingsManager.Save();
            
            InitializeHotkeyPanel(OverlayHotkeyKeysPanel, defaultHotkey);
            
            var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
            mainWindow?.RefreshGlobalHotkeys();
            
            Logger.LogInfo($"Overlay hotkey restored to default: {defaultHotkey}");
        }

        // Filter Active Hotkey action buttons
        private void FilterActiveHotkeyEditButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_activeHotkeyEditBox != null) return;
            
            StartHotkeyEdit(FilterActiveHotkeyKeysPanel, FilterActiveHotkeyEditButton, FilterActiveHotkeySaveButton, 
                           SettingsManager.Current.FilterActiveHotkey ?? "Alt+A");
        }

        private void FilterActiveHotkeySaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SaveHotkeyEdit(FilterActiveHotkeyKeysPanel, FilterActiveHotkeyEditButton, FilterActiveHotkeySaveButton, "filter");
        }

        private void FilterActiveHotkeyRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_activeHotkeyEditBox != null) return;
            
            var defaultHotkey = "Alt+A";
            SettingsManager.Current.FilterActiveHotkey = defaultHotkey;
            SettingsManager.Save();
            
            InitializeHotkeyPanel(FilterActiveHotkeyKeysPanel, defaultHotkey);
            
            var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
            mainWindow?.RefreshGlobalHotkeys();
            
            Logger.LogInfo($"Filter active hotkey restored to default: {defaultHotkey}");
        }
        
        // Helper methods for hotkey editing
        private void StartHotkeyEdit(StackPanel keysPanel, Border editButton, Border saveButton, string currentHotkey)
        {
            // Hide edit button, show save button
            editButton.Visibility = Visibility.Collapsed;
            saveButton.Visibility = Visibility.Visible;
            
            // Replace keys panel with TextBox
            keysPanel.Children.Clear();
            
            var editBox = new TextBox
            {
                Text = currentHotkey,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 14,
                Height = 32,
                MinWidth = 120,
                VerticalAlignment = VerticalAlignment.Center,
                PlaceholderText = "Press keys..."
            };
            
            editBox.PreviewKeyDown += HotkeyEditBox_PreviewKeyDown;
            keysPanel.Children.Add(editBox);
            editBox.Focus(FocusState.Programmatic);
            
            _activeHotkeyEditBox = editBox;
            
            // Disable global hotkeys during editing
            _previousHotkeysEnabled = SettingsManager.Current.HotkeysEnabled;
            SettingsManager.Current.HotkeysEnabled = false;
        }

        private void SaveHotkeyEdit(StackPanel keysPanel, Border editButton, Border saveButton, string hotkeyType)
        {
            if (_activeHotkeyEditBox == null) return;
            
            var newHotkey = _activeHotkeyEditBox.Text.Trim();
            if (string.IsNullOrEmpty(newHotkey))
            {
                // Restore original hotkey if empty
                newHotkey = hotkeyType == "overlay" 
                    ? SettingsManager.Current.ToggleOverlayHotkey ?? "Alt+W"
                    : SettingsManager.Current.FilterActiveHotkey ?? "Alt+A";
            }
            
            // Save to settings
            if (hotkeyType == "overlay")
            {
                SettingsManager.Current.ToggleOverlayHotkey = newHotkey;
            }
            else
            {
                SettingsManager.Current.FilterActiveHotkey = newHotkey;
            }
            SettingsManager.Save();
            
            // Restore visual keys panel
            InitializeHotkeyPanel(keysPanel, newHotkey);
            
            // Show edit button, hide save button
            editButton.Visibility = Visibility.Visible;
            saveButton.Visibility = Visibility.Collapsed;
            
            // Restore global hotkeys
            SettingsManager.Current.HotkeysEnabled = _previousHotkeysEnabled;
            
            // Refresh global hotkeys
            var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
            mainWindow?.RefreshGlobalHotkeys();
            
            _activeHotkeyEditBox = null;
            
            Logger.LogInfo($"{hotkeyType} hotkey set to: {newHotkey}");
        }

        private void HotkeyEditBox_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (sender is not TextBox editBox) return;
            
            e.Handled = true;
            
            var modifiers = new List<string>();
            
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                modifiers.Add("CTRL");
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                modifiers.Add("SHIFT");
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                modifiers.Add("ALT");
            
            var key = e.Key;
            
            // Skip modifier-only keys
            if (key == Windows.System.VirtualKey.Control || key == Windows.System.VirtualKey.Shift ||
                key == Windows.System.VirtualKey.Menu || key == Windows.System.VirtualKey.LeftWindows ||
                key == Windows.System.VirtualKey.RightWindows || key == Windows.System.VirtualKey.LeftControl ||
                key == Windows.System.VirtualKey.RightControl || key == Windows.System.VirtualKey.LeftShift ||
                key == Windows.System.VirtualKey.RightShift || key == Windows.System.VirtualKey.LeftMenu ||
                key == Windows.System.VirtualKey.RightMenu)
                return;
            
            // Convert key to display string
            string keyStr = ConvertVirtualKeyToString(key);
            
            if (modifiers.Count > 0)
                editBox.Text = string.Join("+", modifiers) + "+" + keyStr;
            else
                editBox.Text = keyStr;
        }
    }
}