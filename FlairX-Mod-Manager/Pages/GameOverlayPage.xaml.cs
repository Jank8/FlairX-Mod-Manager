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
            FilterActiveHotkeyTextBox.Text = settings.FilterActiveHotkey ?? "Alt+A";
            
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
            // Ensure gamepad is available
            if (_testGamepad == null)
            {
                _testGamepad = new GamepadManager();
            }
            
            if (!_testGamepad.CheckConnection())
            {
                if (_isRecordingFilterCombo)
                    FilterActiveComboTextBox.Text = "No controller!";
                else
                    GamepadComboTextBox.Text = "No controller!";
                return;
            }

            _isRecordingCombo = true;
            _recordedButtons.Clear();
            
            if (_isRecordingFilterCombo)
            {
                FilterActiveComboTextBox.Text = "Press buttons...";
                FilterActiveComboRecordButton.Content = new FontIcon { Glyph = "\uE71A", FontSize = 14 };
            }
            else
            {
                GamepadComboTextBox.Text = "Press buttons...";
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
            if (_testGamepad == null)
            {
                _testGamepad = new GamepadManager();
            }
            
            if (!_testGamepad.CheckConnection())
            {
                textBox.Text = "No controller!";
                return;
            }

            _isRecordingSingleButton = true;
            _recordingButtonType = buttonType;
            _currentRecordingTextBox = textBox;
            _currentRecordingButton = recordButton;
            
            textBox.Text = "Press button...";
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
            if (_currentRecordingTextBox?.Text == "Press button...")
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
