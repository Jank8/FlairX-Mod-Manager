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
            _globalInputDisabled = true;
            
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
                    try
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
                    }
                    catch (System.OperationCanceledException)
                    {
                        Logger.LogDebug("GameOverlayPage.ComboHoldTimer_Tick failed: canceled");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("GameOverlayPage.ComboHoldTimer_Tick failed", ex);
                    }
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
            try
            {
                StopComboRecording();

                // Restore global hotkeys and gamepad
                SettingsManager.Current.HotkeysEnabled = _previousHotkeysEnabled;
                var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
                mainWindow?.RefreshGlobalGamepad();
                _globalInputDisabled = false;

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
                        "categoryFavorite" => SettingsManager.Current.GamepadCategoryFavoriteButton ?? defaultValue,
                        "modFavorite" => SettingsManager.Current.GamepadModFavoriteButton ?? defaultValue,
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
                    "categoryFavorite" => "GamepadCategoryFavoriteButton",
                    "modFavorite" => "GamepadModFavoriteButton",
                    _ => ""
                };
                var conflict = SharedUtilities.GetConflictingGamepadCombo(value, settingKey);
                if (conflict != null)
                {
                    var lang = SharedUtilities.LoadLanguageDictionary();
                    if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "HotkeyConflict_Message"));

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
                        "categoryFavorite" => SettingsManager.Current.GamepadCategoryFavoriteButton ?? defaultValue,
                        "modFavorite" => SettingsManager.Current.GamepadModFavoriteButton ?? defaultValue,
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
                    case "rightStickUp": SettingsManager.Current.GamepadRightStickUp = value; break;
                    case "rightStickDown": SettingsManager.Current.GamepadRightStickDown = value; break;
                    case "categoryFavorite": SettingsManager.Current.GamepadCategoryFavoriteButton = value; break;
                    case "modFavorite": SettingsManager.Current.GamepadModFavoriteButton = value; break;
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
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameOverlayPage.SaveGamepadSetting failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameOverlayPage.SaveGamepadSetting failed", ex);
            }
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
                case "rightStickUp": SettingsManager.Current.GamepadRightStickUp = defaultValue; break;
                case "rightStickDown": SettingsManager.Current.GamepadRightStickDown = defaultValue; break;
                case "categoryFavorite": SettingsManager.Current.GamepadCategoryFavoriteButton = defaultValue; break;
                case "modFavorite": SettingsManager.Current.GamepadModFavoriteButton = defaultValue; break;
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

        // Right Stick methods
        private void RightStickUpRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(RightStickUpKeysPanel, RightStickUpRecordButton, RightStickUpSaveButton,
                SettingsManager.Current.GamepadRightStickUp ?? "XB R↑", "rightStickUp", singleInputOnly: true);
        }

        private void RightStickDownRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(RightStickDownKeysPanel, RightStickDownRecordButton, RightStickDownSaveButton,
                SettingsManager.Current.GamepadRightStickDown ?? "XB R↓", "rightStickDown", singleInputOnly: true);
        }

        private void RightStickUpSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("rightStickUp", RightStickUpKeysPanel, RightStickUpSaveButton, "XB R↑");
        private void RightStickDownSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("rightStickDown", RightStickDownKeysPanel, RightStickDownSaveButton, "XB R↓");

        private void RightStickUpRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("rightStickUp", RightStickUpKeysPanel, "XB R↑"); }
        private void RightStickDownRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("rightStickDown", RightStickDownKeysPanel, "XB R↓"); }

        // Favorite button handlers
        private void CategoryFavoriteButtonRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(CategoryFavoriteButtonKeysPanel, CategoryFavoriteButtonRecordButton, CategoryFavoriteButtonSaveButton,
                SettingsManager.Current.GamepadCategoryFavoriteButton ?? "XB X", "categoryFavorite");
        }

        private void ModFavoriteButtonRecordButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsAnyEditInProgress()) return;
            StartComboRecording(ModFavoriteButtonKeysPanel, ModFavoriteButtonRecordButton, ModFavoriteButtonSaveButton,
                SettingsManager.Current.GamepadModFavoriteButton ?? "XB Y", "modFavorite");
        }

        private void CategoryFavoriteButtonSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("categoryFavorite", CategoryFavoriteButtonKeysPanel, CategoryFavoriteButtonSaveButton, "XB X");

        private void ModFavoriteButtonSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
            => SaveGamepadSetting("modFavorite", ModFavoriteButtonKeysPanel, ModFavoriteButtonSaveButton, "XB Y");

        private void CategoryFavoriteButtonRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("categoryFavorite", CategoryFavoriteButtonKeysPanel, "XB X"); }

        private void ModFavoriteButtonRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        { if (!IsAnyEditInProgress()) RestoreGamepadSetting("modFavorite", ModFavoriteButtonKeysPanel, "XB Y"); }
    }
}
