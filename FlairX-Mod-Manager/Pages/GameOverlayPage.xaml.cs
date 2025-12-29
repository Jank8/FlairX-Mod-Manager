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
    public sealed partial class GameOverlayPage : Page
    {
        private bool _isInitializing = true;
        private GamepadManager? _testGamepad;
        private bool _previousHotkeysEnabled = true;
        
        // Hotkey editing state
        private TextBox? _activeHotkeyEditBox;
        
        /// <summary>
        /// Returns true if any editing is in progress (keyboard hotkey or gamepad recording)
        /// </summary>
        private bool IsAnyEditInProgress()
        {
            return _activeHotkeyEditBox != null || 
                   _activeComboEditBox != null ||
                   _isRecordingCombo;
        }

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
            
            // Initialize navigation panels
            InitializeHotkeyPanel(DPadUpKeysPanel, ConvertToKenneyFormat(settings.GamepadDPadUp ?? "XB ↑"));
            InitializeHotkeyPanel(DPadDownKeysPanel, ConvertToKenneyFormat(settings.GamepadDPadDown ?? "XB ↓"));
            InitializeHotkeyPanel(DPadLeftKeysPanel, ConvertToKenneyFormat(settings.GamepadDPadLeft ?? "XB ←"));
            InitializeHotkeyPanel(DPadRightKeysPanel, ConvertToKenneyFormat(settings.GamepadDPadRight ?? "XB →"));
            InitializeHotkeyPanel(LeftStickUpKeysPanel, ConvertToKenneyFormat(settings.GamepadLeftStickUp ?? "XB L↑"));
            InitializeHotkeyPanel(LeftStickDownKeysPanel, ConvertToKenneyFormat(settings.GamepadLeftStickDown ?? "XB L↓"));
            InitializeHotkeyPanel(LeftStickLeftKeysPanel, ConvertToKenneyFormat(settings.GamepadLeftStickLeft ?? "XB L←"));
            InitializeHotkeyPanel(LeftStickRightKeysPanel, ConvertToKenneyFormat(settings.GamepadLeftStickRight ?? "XB L→"));
        }
        
        private string ConvertToKenneyFormat(string button)
        {
            // Check if button already has a controller prefix (XB, PS, NIN)
            if (button.StartsWith("XB ") || button.StartsWith("PS ") || button.StartsWith("NIN ")) 
                return button;
            return "XB " + button; // Only add prefix for legacy values without prefix
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

        #region Gamepad Recording
        
        // Unified gamepad recording for all 6 fields (single buttons AND combos)
        private bool _isRecordingCombo = false;
        private bool _singleInputOnly = false;
        private string _recordingSettingType = ""; // "select", "back", "next", "prev", "toggleCombo", "filterCombo", "dpadUp", etc.
        private HashSet<string> _recordedComboButtons = new();
        private DispatcherTimer? _comboHoldTimer;
        private int _comboHoldCountdown = 3;
        private string _pendingCombo = "";
        private TextBlock? _comboCountdownTextBlock;
        private TextBox? _activeComboEditBox;
        private Border? _activeComboRecButton;
        private StackPanel? _activeComboKeysPanel;
        private Border? _activeComboSaveButton;

        private void GamepadComboRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(GamepadComboKeysPanel, GamepadComboRecordButton, GamepadComboSaveButton,
                SettingsManager.Current.GamepadToggleOverlayCombo ?? "Back+Start", "toggleCombo");
        }

        private void FilterActiveComboRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(FilterActiveComboKeysPanel, FilterActiveComboRecordButton, FilterActiveComboSaveButton,
                SettingsManager.Current.GamepadFilterActiveCombo ?? "Back+A", "filterCombo");
        }

        private void StartComboRecording(StackPanel keysPanel, Border recButton, Border saveButton, string currentValue, string settingType, bool singleInputOnly = false)
        {
            _activeComboRecButton = recButton;
            _activeComboKeysPanel = keysPanel;
            _activeComboSaveButton = saveButton;
            _recordingSettingType = settingType;
            _singleInputOnly = singleInputOnly;
            _recordedComboButtons.Clear();
            _pendingCombo = "";
            _comboHoldCountdown = 3;
            
            // Disable global hotkeys and gamepad during recording
            _previousHotkeysEnabled = SettingsManager.Current.HotkeysEnabled;
            SettingsManager.Current.HotkeysEnabled = false;
            var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
            mainWindow?.StopGlobalGamepad();
            
            saveButton.Visibility = Visibility.Visible;
            keysPanel.Children.Clear();
            
            var editBox = new TextBox
            {
                Text = currentValue,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 14,
                Height = 32,
                MinWidth = 100,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            _comboCountdownTextBlock = new TextBlock
            {
                FontFamily = new FontFamily("ms-appx:///Assets/KenneyFonts/kenney_input_keyboard_mouse.ttf#Kenney Input Keyboard & Mouse"),
                FontSize = 32,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Visibility = Visibility.Collapsed
            };
            
            keysPanel.Children.Add(editBox);
            keysPanel.Children.Add(_comboCountdownTextBlock);
            _activeComboEditBox = editBox;
            
            // Always create fresh gamepad for recording
            _testGamepad?.Dispose();
            _testGamepad = new GamepadManager();
            
            _isRecordingCombo = true;
            
            if (!_testGamepad.CheckConnection())
            {
                editBox.PlaceholderText = "No controller";
                return;
            }

            editBox.Text = "";
            editBox.PlaceholderText = "Press & hold gamepad...";
            
            _testGamepad.ButtonPressed += OnComboButtonPressed;
            _testGamepad.ButtonReleased += OnComboButtonReleased;
            _testGamepad.StickMoved += OnComboStickMoved;
            _testGamepad.StartPolling();
        }

        private void StopComboRecording()
        {
            _isRecordingCombo = false;
            _singleInputOnly = false;
            _comboHoldTimer?.Stop();
            _comboHoldTimer = null;
            _comboHoldCountdown = 3;
            _pendingCombo = "";
            
            if (_testGamepad != null)
            {
                _testGamepad.ButtonPressed -= OnComboButtonPressed;
                _testGamepad.ButtonReleased -= OnComboButtonReleased;
                _testGamepad.StickMoved -= OnComboStickMoved;
                // Stop any ongoing vibration before stopping polling
                _testGamepad.Vibrate(0, 0, 0);
                _testGamepad.StopPolling();
            }
            
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
                
                if (_activeComboEditBox != null)
                {
                    if (_recordedComboButtons.Count <= 1)
                        _activeComboEditBox.Text = _pendingCombo;
                    else
                    {
                        _activeComboEditBox.Text = "";
                        _pendingCombo = "";
                    }
                    _recordedComboButtons.Clear();
                }
                
                if (_comboCountdownTextBlock != null)
                    _comboCountdownTextBlock.Visibility = Visibility.Collapsed;
            });
        }

        private void OnComboButtonPressed(object? sender, GamepadButtonEventArgs e)
        {
            if (!_isRecordingCombo) return;
            
            var buttonName = e.GetButtonDisplayName();
            
            if (_singleInputOnly)
            {
                // For single input fields, replace any existing input
                _recordedComboButtons.Clear();
                _recordedComboButtons.Add(buttonName);
                _pendingCombo = buttonName;
            }
            else
            {
                // For combo fields, add to existing inputs
                if (!_recordedComboButtons.Contains(buttonName))
                    _recordedComboButtons.Add(buttonName);
                _pendingCombo = string.Join("+", _recordedComboButtons);
            }
            
            _comboHoldCountdown = 3;
            
            DispatcherQueue.TryEnqueue(() =>
            {
                _comboHoldTimer?.Stop();
                
                if (_activeComboEditBox != null)
                {
                    _activeComboEditBox.Text = _pendingCombo;
                    
                    // For single input fields, no countdown needed
                    if (_singleInputOnly)
                    {
                        if (_comboCountdownTextBlock != null)
                            _comboCountdownTextBlock.Visibility = Visibility.Collapsed;
                        return;
                    }
                    
                    // For combo fields, show countdown if multiple buttons
                    if (_recordedComboButtons.Count == 1)
                    {
                        if (_comboCountdownTextBlock != null)
                            _comboCountdownTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        _comboHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                        _comboHoldTimer.Tick += ComboHoldTimer_Tick;
                        _comboHoldTimer.Start();
                        
                        if (_comboCountdownTextBlock != null)
                        {
                            _comboCountdownTextBlock.Text = _comboHoldCountdown switch { 3 => "\uE007", 2 => "\uE005", 1 => "\uE003", _ => _comboHoldCountdown.ToString() };
                            _comboCountdownTextBlock.Visibility = Visibility.Visible;
                        }
                    }
                }
            });
        }

        private void OnComboStickMoved(object? sender, GamepadButtonEventArgs e)
        {
            if (!_isRecordingCombo) return;
            
            var stickName = e.GetButtonDisplayName();
            
            if (_singleInputOnly)
            {
                // For single input fields, replace any existing input
                _recordedComboButtons.Clear();
                _recordedComboButtons.Add(stickName);
                _pendingCombo = stickName;
            }
            else
            {
                // For combo fields, add to existing inputs
                if (!_recordedComboButtons.Contains(stickName))
                    _recordedComboButtons.Add(stickName);
                _pendingCombo = string.Join("+", _recordedComboButtons);
            }
            
            _comboHoldCountdown = 3;
            
            DispatcherQueue.TryEnqueue(() =>
            {
                _comboHoldTimer?.Stop();
                
                if (_activeComboEditBox != null)
                {
                    _activeComboEditBox.Text = _pendingCombo;
                    
                    // For single input fields, no countdown needed
                    if (_singleInputOnly)
                    {
                        if (_comboCountdownTextBlock != null)
                            _comboCountdownTextBlock.Visibility = Visibility.Collapsed;
                        return;
                    }
                    
                    // For combo fields, show countdown if multiple buttons
                    if (_recordedComboButtons.Count == 1)
                    {
                        if (_comboCountdownTextBlock != null)
                            _comboCountdownTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        _comboHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                        _comboHoldTimer.Tick += ComboHoldTimer_Tick;
                        _comboHoldTimer.Start();
                        
                        if (_comboCountdownTextBlock != null)
                        {
                            _comboCountdownTextBlock.Text = _comboHoldCountdown switch { 3 => "\uE007", 2 => "\uE005", 1 => "\uE003", _ => _comboHoldCountdown.ToString() };
                            _comboCountdownTextBlock.Visibility = Visibility.Visible;
                        }
                    }
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
                
                DispatcherQueue.TryEnqueue(async () =>
                {
                    if (_activeComboEditBox != null)
                        _activeComboEditBox.Text = _pendingCombo;
                    if (_comboCountdownTextBlock != null)
                        _comboCountdownTextBlock.Visibility = Visibility.Collapsed;
                    // Confirmation vibration 400ms
                    _testGamepad?.Vibrate(30000, 30000, 400);
                    // Wait for vibration to finish before stopping
                    await Task.Delay(450);
                    StopComboRecording();
                });
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_comboCountdownTextBlock != null)
                        _comboCountdownTextBlock.Text = _comboHoldCountdown switch { 3 => "\uE007", 2 => "\uE005", 1 => "\uE003", _ => _comboHoldCountdown.ToString() };
                });
            }
        }

        // Save button handlers
        private void GamepadComboSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("toggleCombo", GamepadComboKeysPanel, GamepadComboSaveButton, "Back+Start");
        private void FilterActiveComboSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("filterCombo", FilterActiveComboKeysPanel, FilterActiveComboSaveButton, "Back+A");

        private async void SaveGamepadSetting(string settingType, StackPanel keysPanel, Border saveButton, string defaultValue)
        {
            StopComboRecording();
            
            // Restore global hotkeys and gamepad
            SettingsManager.Current.HotkeysEnabled = _previousHotkeysEnabled;
            var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
            mainWindow?.RefreshGlobalGamepad();
            
            var value = _activeComboEditBox?.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(value))
            {
                value = settingType switch
                {
                    "select" => SettingsManager.Current.GamepadSelectButton ?? defaultValue,
                    "back" => SettingsManager.Current.GamepadBackButton ?? defaultValue,
                    "next" => SettingsManager.Current.GamepadNextCategoryButton ?? defaultValue,
                    "prev" => SettingsManager.Current.GamepadPrevCategoryButton ?? defaultValue,
                    "toggleCombo" => SettingsManager.Current.GamepadToggleOverlayCombo ?? defaultValue,
                    "filterCombo" => SettingsManager.Current.GamepadFilterActiveCombo ?? defaultValue,
                    "dpadUp" => SettingsManager.Current.GamepadDPadUp ?? defaultValue,
                    "dpadDown" => SettingsManager.Current.GamepadDPadDown ?? defaultValue,
                    "dpadLeft" => SettingsManager.Current.GamepadDPadLeft ?? defaultValue,
                    "dpadRight" => SettingsManager.Current.GamepadDPadRight ?? defaultValue,
                    "leftStickUp" => SettingsManager.Current.GamepadLeftStickUp ?? defaultValue,
                    "leftStickDown" => SettingsManager.Current.GamepadLeftStickDown ?? defaultValue,
                    "leftStickLeft" => SettingsManager.Current.GamepadLeftStickLeft ?? defaultValue,
                    "leftStickRight" => SettingsManager.Current.GamepadLeftStickRight ?? defaultValue,
                    _ => defaultValue
                };
            }
            
            // Check for conflicts
            var settingKey = settingType switch
            {
                "select" => "GamepadSelectButton",
                "back" => "GamepadBackButton",
                "next" => "GamepadNextCategoryButton",
                "prev" => "GamepadPrevCategoryButton",
                "toggleCombo" => "GamepadToggleOverlayCombo",
                "filterCombo" => "GamepadFilterActiveCombo",
                "dpadUp" => "GamepadDPadUp",
                "dpadDown" => "GamepadDPadDown",
                "dpadLeft" => "GamepadDPadLeft",
                "dpadRight" => "GamepadDPadRight",
                "leftStickUp" => "GamepadLeftStickUp",
                "leftStickDown" => "GamepadLeftStickDown",
                "leftStickLeft" => "GamepadLeftStickLeft",
                "leftStickRight" => "GamepadLeftStickRight",
                _ => ""
            };
            var conflict = SharedUtilities.GetConflictingGamepadCombo(value, settingKey);
            if (conflict != null)
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "HotkeyConflict_Title"),
                    Content = SharedUtilities.GetTranslation(lang, "HotkeyConflict_Message"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                
                // Restore original value in panel
                var originalValue = settingType switch
                {
                    "select" => SettingsManager.Current.GamepadSelectButton ?? defaultValue,
                    "back" => SettingsManager.Current.GamepadBackButton ?? defaultValue,
                    "next" => SettingsManager.Current.GamepadNextCategoryButton ?? defaultValue,
                    "prev" => SettingsManager.Current.GamepadPrevCategoryButton ?? defaultValue,
                    "toggleCombo" => SettingsManager.Current.GamepadToggleOverlayCombo ?? defaultValue,
                    "filterCombo" => SettingsManager.Current.GamepadFilterActiveCombo ?? defaultValue,
                    "dpadUp" => SettingsManager.Current.GamepadDPadUp ?? defaultValue,
                    "dpadDown" => SettingsManager.Current.GamepadDPadDown ?? defaultValue,
                    "dpadLeft" => SettingsManager.Current.GamepadDPadLeft ?? defaultValue,
                    "dpadRight" => SettingsManager.Current.GamepadDPadRight ?? defaultValue,
                    "leftStickUp" => SettingsManager.Current.GamepadLeftStickUp ?? defaultValue,
                    "leftStickDown" => SettingsManager.Current.GamepadLeftStickDown ?? defaultValue,
                    "leftStickLeft" => SettingsManager.Current.GamepadLeftStickLeft ?? defaultValue,
                    "leftStickRight" => SettingsManager.Current.GamepadLeftStickRight ?? defaultValue,
                    _ => defaultValue
                };
                var kenneyOriginal = originalValue.Contains("+") ? ConvertComboToKenneyFormat(originalValue) : ConvertToKenneyFormat(originalValue);
                InitializeHotkeyPanel(keysPanel, kenneyOriginal);
                saveButton.Visibility = Visibility.Collapsed;
                
                _comboCountdownTextBlock = null;
                _activeComboEditBox = null;
                _activeComboKeysPanel = null;
                _activeComboSaveButton = null;
                _recordingSettingType = "";
                return;
            }
            
            switch (settingType)
            {
                case "select": SettingsManager.Current.GamepadSelectButton = value; break;
                case "back": SettingsManager.Current.GamepadBackButton = value; break;
                case "next": SettingsManager.Current.GamepadNextCategoryButton = value; break;
                case "prev": SettingsManager.Current.GamepadPrevCategoryButton = value; break;
                case "toggleCombo": SettingsManager.Current.GamepadToggleOverlayCombo = value; break;
                case "filterCombo": SettingsManager.Current.GamepadFilterActiveCombo = value; break;
                case "dpadUp": SettingsManager.Current.GamepadDPadUp = value; break;
                case "dpadDown": SettingsManager.Current.GamepadDPadDown = value; break;
                case "dpadLeft": SettingsManager.Current.GamepadDPadLeft = value; break;
                case "dpadRight": SettingsManager.Current.GamepadDPadRight = value; break;
                case "leftStickUp": SettingsManager.Current.GamepadLeftStickUp = value; break;
                case "leftStickDown": SettingsManager.Current.GamepadLeftStickDown = value; break;
                case "leftStickLeft": SettingsManager.Current.GamepadLeftStickLeft = value; break;
                case "leftStickRight": SettingsManager.Current.GamepadLeftStickRight = value; break;
            }
            SettingsManager.Save();
            Logger.LogInfo($"Gamepad {settingType} set to: {value}");
            
            // Convert to Kenney format - use combo format if contains +
            var kenneyValue = value.Contains("+") ? ConvertComboToKenneyFormat(value) : ConvertToKenneyFormat(value);
            InitializeHotkeyPanel(keysPanel, kenneyValue);
            saveButton.Visibility = Visibility.Collapsed;
            
            _comboCountdownTextBlock = null;
            _activeComboEditBox = null;
            _activeComboKeysPanel = null;
            _activeComboSaveButton = null;
            _recordingSettingType = "";
        }
        
        // Restore button handlers
        private void GamepadComboRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("toggleCombo", GamepadComboKeysPanel, "Back+Start"); }
        private void FilterActiveComboRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("filterCombo", FilterActiveComboKeysPanel, "Back+A"); }

        private void RestoreGamepadSetting(string settingType, StackPanel keysPanel, string defaultValue)
        {
            switch (settingType)
            {
                case "select": SettingsManager.Current.GamepadSelectButton = defaultValue; break;
                case "back": SettingsManager.Current.GamepadBackButton = defaultValue; break;
                case "next": SettingsManager.Current.GamepadNextCategoryButton = defaultValue; break;
                case "prev": SettingsManager.Current.GamepadPrevCategoryButton = defaultValue; break;
                case "toggleCombo": SettingsManager.Current.GamepadToggleOverlayCombo = defaultValue; break;
                case "filterCombo": SettingsManager.Current.GamepadFilterActiveCombo = defaultValue; break;
                case "dpadUp": SettingsManager.Current.GamepadDPadUp = defaultValue; break;
                case "dpadDown": SettingsManager.Current.GamepadDPadDown = defaultValue; break;
                case "dpadLeft": SettingsManager.Current.GamepadDPadLeft = defaultValue; break;
                case "dpadRight": SettingsManager.Current.GamepadDPadRight = defaultValue; break;
                case "leftStickUp": SettingsManager.Current.GamepadLeftStickUp = defaultValue; break;
                case "leftStickDown": SettingsManager.Current.GamepadLeftStickDown = defaultValue; break;
                case "leftStickLeft": SettingsManager.Current.GamepadLeftStickLeft = defaultValue; break;
                case "leftStickRight": SettingsManager.Current.GamepadLeftStickRight = defaultValue; break;
            }
            SettingsManager.Save();
            Logger.LogInfo($"Gamepad {settingType} restored to: {defaultValue}");
            
            var kenneyValue = defaultValue.Contains("+") ? ConvertComboToKenneyFormat(defaultValue) : ConvertToKenneyFormat(defaultValue);
            InitializeHotkeyPanel(keysPanel, kenneyValue);
            
            if (settingType == "toggleCombo" || settingType == "filterCombo")
            {
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                mainWindow?.RefreshGlobalGamepad();
            }
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

        // Single button Record/Save/Restore handlers - delegate to unified gamepad recording
        private void SelectButtonRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(SelectButtonKeysPanel, SelectButtonRecordButton, SelectButtonSaveButton,
                SettingsManager.Current.GamepadSelectButton ?? "A", "select");
        }

        private void BackButtonRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(BackButtonKeysPanel, BackButtonRecordButton, BackButtonSaveButton,
                SettingsManager.Current.GamepadBackButton ?? "B", "back");
        }

        private void NextCategoryButtonRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(NextCategoryButtonKeysPanel, NextCategoryButtonRecordButton, NextCategoryButtonSaveButton,
                SettingsManager.Current.GamepadNextCategoryButton ?? "RB", "next");
        }

        private void PrevCategoryButtonRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(PrevCategoryButtonKeysPanel, PrevCategoryButtonRecordButton, PrevCategoryButtonSaveButton,
                SettingsManager.Current.GamepadPrevCategoryButton ?? "LB", "prev");
        }

        private void SelectButtonSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("select", SelectButtonKeysPanel, SelectButtonSaveButton, "A");
        private void BackButtonSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("back", BackButtonKeysPanel, BackButtonSaveButton, "B");
        private void NextCategoryButtonSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("next", NextCategoryButtonKeysPanel, NextCategoryButtonSaveButton, "RB");
        private void PrevCategoryButtonSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("prev", PrevCategoryButtonKeysPanel, PrevCategoryButtonSaveButton, "LB");

        // Navigation Save button handlers
        private void DPadUpSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("dpadUp", DPadUpKeysPanel, DPadUpSaveButton, "XB ↑");
        private void DPadDownSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("dpadDown", DPadDownKeysPanel, DPadDownSaveButton, "XB ↓");
        private void DPadLeftSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("dpadLeft", DPadLeftKeysPanel, DPadLeftSaveButton, "XB ←");
        private void DPadRightSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("dpadRight", DPadRightKeysPanel, DPadRightSaveButton, "XB →");
        private void LeftStickUpSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("leftStickUp", LeftStickUpKeysPanel, LeftStickUpSaveButton, "XB L↑");
        private void LeftStickDownSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("leftStickDown", LeftStickDownKeysPanel, LeftStickDownSaveButton, "XB L↓");
        private void LeftStickLeftSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("leftStickLeft", LeftStickLeftKeysPanel, LeftStickLeftSaveButton, "XB L←");
        private void LeftStickRightSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("leftStickRight", LeftStickRightKeysPanel, LeftStickRightSaveButton, "XB L→");

        private void SelectButtonRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("select", SelectButtonKeysPanel, "A"); }
        private void BackButtonRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("back", BackButtonKeysPanel, "B"); }
        private void NextCategoryButtonRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("next", NextCategoryButtonKeysPanel, "RB"); }
        private void PrevCategoryButtonRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("prev", PrevCategoryButtonKeysPanel, "LB"); }
        
        // Navigation Record button handlers
        private void DPadUpRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(DPadUpKeysPanel, DPadUpRecordButton, DPadUpSaveButton,
                SettingsManager.Current.GamepadDPadUp ?? "XB ↑", "dpadUp", singleInputOnly: true);
        }

        private void DPadDownRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(DPadDownKeysPanel, DPadDownRecordButton, DPadDownSaveButton,
                SettingsManager.Current.GamepadDPadDown ?? "XB ↓", "dpadDown", singleInputOnly: true);
        }

        private void DPadLeftRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(DPadLeftKeysPanel, DPadLeftRecordButton, DPadLeftSaveButton,
                SettingsManager.Current.GamepadDPadLeft ?? "XB ←", "dpadLeft", singleInputOnly: true);
        }

        private void DPadRightRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(DPadRightKeysPanel, DPadRightRecordButton, DPadRightSaveButton,
                SettingsManager.Current.GamepadDPadRight ?? "XB →", "dpadRight", singleInputOnly: true);
        }

        private void LeftStickUpRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(LeftStickUpKeysPanel, LeftStickUpRecordButton, LeftStickUpSaveButton,
                SettingsManager.Current.GamepadLeftStickUp ?? "XB L↑", "leftStickUp", singleInputOnly: true);
        }

        private void LeftStickDownRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(LeftStickDownKeysPanel, LeftStickDownRecordButton, LeftStickDownSaveButton,
                SettingsManager.Current.GamepadLeftStickDown ?? "XB L↓", "leftStickDown", singleInputOnly: true);
        }

        private void LeftStickLeftRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(LeftStickLeftKeysPanel, LeftStickLeftRecordButton, LeftStickLeftSaveButton,
                SettingsManager.Current.GamepadLeftStickLeft ?? "XB L←", "leftStickLeft", singleInputOnly: true);
        }

        private void LeftStickRightRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(LeftStickRightKeysPanel, LeftStickRightRecordButton, LeftStickRightSaveButton,
                SettingsManager.Current.GamepadLeftStickRight ?? "XB L→", "leftStickRight", singleInputOnly: true);
        }

        // Navigation Restore button handlers
        private void DPadUpRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("dpadUp", DPadUpKeysPanel, "XB ↑"); }
        private void DPadDownRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("dpadDown", DPadDownKeysPanel, "XB ↓"); }
        private void DPadLeftRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("dpadLeft", DPadLeftKeysPanel, "XB ←"); }
        private void DPadRightRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("dpadRight", DPadRightKeysPanel, "XB →"); }
        private void LeftStickUpRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("leftStickUp", LeftStickUpKeysPanel, "XB L↑"); }
        private void LeftStickDownRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("leftStickDown", LeftStickDownKeysPanel, "XB L↓"); }
        private void LeftStickLeftRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("leftStickLeft", LeftStickLeftKeysPanel, "XB L←"); }
        private void LeftStickRightRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("leftStickRight", LeftStickRightKeysPanel, "XB L→"); }
        
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
            if (IsAnyEditInProgress()) return;
            
            StartHotkeyEdit(OverlayHotkeyKeysPanel, OverlayHotkeyEditButton, OverlayHotkeySaveButton, 
                           SettingsManager.Current.ToggleOverlayHotkey ?? "Alt+W");
        }

        private void OverlayHotkeySaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SaveHotkeyEdit(OverlayHotkeyKeysPanel, OverlayHotkeyEditButton, OverlayHotkeySaveButton, "overlay");
        }

        private void OverlayHotkeyRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            
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
            if (IsAnyEditInProgress()) return;
            
            StartHotkeyEdit(FilterActiveHotkeyKeysPanel, FilterActiveHotkeyEditButton, FilterActiveHotkeySaveButton, 
                           SettingsManager.Current.FilterActiveHotkey ?? "Alt+A");
        }

        private void FilterActiveHotkeySaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SaveHotkeyEdit(FilterActiveHotkeyKeysPanel, FilterActiveHotkeyEditButton, FilterActiveHotkeySaveButton, "filter");
        }

        private void FilterActiveHotkeyRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            
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
            
            // Disable global hotkeys and gamepad during editing
            _previousHotkeysEnabled = SettingsManager.Current.HotkeysEnabled;
            SettingsManager.Current.HotkeysEnabled = false;
            var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
            mainWindow?.StopGlobalGamepad();
        }

        private async void SaveHotkeyEdit(StackPanel keysPanel, Border editButton, Border saveButton, string hotkeyType)
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
            
            // Check for conflicts
            var settingKey = hotkeyType == "overlay" ? "ToggleOverlayHotkey" : "FilterActiveHotkey";
            var conflict = SharedUtilities.GetConflictingKeyboardHotkey(newHotkey, settingKey);
            if (conflict != null)
            {
                var lang = SharedUtilities.LoadLanguageDictionary();
                var dialog = new ContentDialog
                {
                    Title = SharedUtilities.GetTranslation(lang, "HotkeyConflict_Title"),
                    Content = SharedUtilities.GetTranslation(lang, "HotkeyConflict_Message"),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                return;
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
            
            // Restore global hotkeys and gamepad
            SettingsManager.Current.HotkeysEnabled = _previousHotkeysEnabled;
            var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
            mainWindow?.RefreshGlobalHotkeys();
            mainWindow?.RefreshGlobalGamepad();
            
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