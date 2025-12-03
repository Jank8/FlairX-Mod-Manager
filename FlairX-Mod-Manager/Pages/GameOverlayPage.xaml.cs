using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

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
            _isInitializing = false;
            
            // Check gamepad status on load
            CheckGamepadStatus();
            
            this.Unloaded += (s, e) =>
            {
                _testGamepad?.Dispose();
                _testGamepad = null;
            };
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Current;
            
            // Load hotkey
            OverlayHotkeyTextBox.Text = settings.ToggleOverlayHotkey ?? "Alt+W";
            
            // Load gamepad enabled
            GamepadEnabledToggle.IsOn = settings.GamepadEnabled;
            
            // Load gamepad combo
            GamepadComboTextBox.Text = settings.GamepadToggleOverlayCombo ?? "Back+Start";
            
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
            if (isConnected)
            {
                GamepadStatusIcon.Glyph = "\uE73E"; // Checkmark
                GamepadStatusText.Text = "Controller connected";
                GamepadStatusText.Opacity = 1.0;
            }
            else
            {
                GamepadStatusIcon.Glyph = "\uE711"; // X
                GamepadStatusText.Text = "No controller detected";
                GamepadStatusText.Opacity = 0.7;
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
        private HashSet<string> _recordedButtons = new();
        private DispatcherTimer? _comboRecordTimer;

        private void GamepadComboTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Show current combo
            GamepadComboTextBox.Text = SettingsManager.Current.GamepadToggleOverlayCombo ?? "Back+Start";
        }

        private void GamepadComboRecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecordingCombo)
            {
                StopRecordingCombo();
            }
            else
            {
                StartRecordingCombo();
            }
        }

        private void StartRecordingCombo()
        {
            // Ensure gamepad is available
            if (_testGamepad == null)
            {
                _testGamepad = new GamepadManager();
            }
            
            if (!_testGamepad.CheckConnection())
            {
                GamepadComboTextBox.Text = "No controller!";
                return;
            }

            _isRecordingCombo = true;
            _recordedButtons.Clear();
            GamepadComboTextBox.Text = "Press buttons...";
            GamepadComboRecordButton.Content = new FontIcon { Glyph = "\uE71A", FontSize = 14 }; // Stop icon
            
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
            
            GamepadComboRecordButton.Content = new FontIcon { Glyph = "\uE7C8", FontSize = 14 }; // Record icon
            
            // Save combo if we recorded something
            if (_recordedButtons.Count > 0)
            {
                var combo = string.Join("+", _recordedButtons);
                GamepadComboTextBox.Text = combo;
                SettingsManager.Current.GamepadToggleOverlayCombo = combo;
                SettingsManager.Save();
                
                // Refresh global gamepad
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                mainWindow?.RefreshGlobalGamepad();
                
                Logger.LogInfo($"Gamepad overlay combo set to: {combo}");
                
                // Vibrate to confirm
                _testGamepad?.Vibrate(30000, 30000, 100);
            }
            else
            {
                GamepadComboTextBox.Text = SettingsManager.Current.GamepadToggleOverlayCombo ?? "Back+Start";
            }
            
            _recordedButtons.Clear();
        }

        private void OnComboButtonPressed(object? sender, GamepadButtonEventArgs e)
        {
            if (!_isRecordingCombo) return;
            
            var buttonName = e.GetButtonDisplayName();
            
            // Map display names to setting names
            var settingName = buttonName switch
            {
                "D-Pad Up" => "DPadUp",
                "D-Pad Down" => "DPadDown",
                "D-Pad Left" => "DPadLeft",
                "D-Pad Right" => "DPadRight",
                "Left Stick" => "LeftThumb",
                "Right Stick" => "RightThumb",
                "LB" => "LeftShoulder",
                "RB" => "RightShoulder",
                _ => buttonName
            };
            
            _recordedButtons.Add(settingName);
            
            DispatcherQueue.TryEnqueue(() =>
            {
                GamepadComboTextBox.Text = string.Join("+", _recordedButtons);
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
                
                // Save hotkey directly (even if text didn't change)
                if (!_isInitializing && !string.IsNullOrWhiteSpace(newHotkey))
                {
                    SettingsManager.Current.ToggleOverlayHotkey = newHotkey;
                    SettingsManager.Save();
                    
                    // Refresh global hotkeys so the new hotkey works immediately
                    var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                    mainWindow?.RefreshGlobalHotkeys();
                    
                    Logger.LogInfo($"Overlay hotkey set to: {newHotkey}");
                }
            }
        }

        private void OverlayHotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Hotkey is now saved in KeyDown handler
            // This handler is kept for manual text entry compatibility
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
