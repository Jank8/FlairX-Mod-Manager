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
        private bool _previousHotkeysEnabled = true;

        // True while a hotkey/combo edit session has global input switched off, so every exit
        // path (save, conflict, navigation) can restore it exactly once.
        private bool _globalInputDisabled;
        
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

        private void OverlayHotkeysEnabledToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            SettingsManager.Current.OverlayHotkeysEnabled = OverlayHotkeysEnabledToggle.IsOn;
            SettingsManager.Save();
            
            // Update toggle label
            var lang = SharedUtilities.LoadLanguageDictionary("Overlay");
            var onText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_On");
            var offText = SharedUtilities.GetTranslation(lang, "ToggleSwitch_Off");
            if (OverlayHotkeysEnabledToggleLabel != null)
                OverlayHotkeysEnabledToggleLabel.Text = OverlayHotkeysEnabledToggle.IsOn ? onText : offText;
            
            // Update hotkeys section state
            UpdateOverlayHotkeysSectionState(OverlayHotkeysEnabledToggle.IsOn);
            
            // Refresh global hotkeys to apply the change
            var mainWindow = (App.Current as App)?.MainWindow as MainWindow;
            mainWindow?.RefreshGlobalHotkeys();
            
            Logger.LogInfo($"Overlay hotkeys enabled: {OverlayHotkeysEnabledToggle.IsOn}");
        }


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

        private void OverlayHotkeyConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // This method is no longer used with the new UI
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
            _globalInputDisabled = true;
        }

        /// <summary>
        /// Re-enables global hotkeys and the gamepad after an edit/record session ends.
        /// </summary>
        /// <remarks>
        /// Starting an edit turns global input off so the user can press any combination. Only
        /// the successful save path turned it back on, so cancelling, hitting a hotkey conflict,
        /// or navigating away left every global shortcut and the gamepad dead until restart.
        /// </remarks>
        private void RestoreGlobalInputAfterEditing()
        {
            if (!_globalInputDisabled)
                return;

            _globalInputDisabled = false;
            SettingsManager.Current.HotkeysEnabled = _previousHotkeysEnabled;

            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshGlobalHotkeys();
                mainWindow.RefreshGlobalGamepad();
            }
        }

        private async void SaveHotkeyEdit(StackPanel keysPanel, Border editButton, Border saveButton, string hotkeyType)
        {
            try
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
                    if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "HotkeyConflict_Message"));

                    // Returning here used to leave global input disabled for the rest of the session.
                    RestoreGlobalInputAfterEditing();
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
                // Restore the user's real preference BEFORE saving: the editor temporarily sets
                // this flag to false, and saving that value would disable all main hotkeys on the
                // next start.
                SettingsManager.Current.HotkeysEnabled = _previousHotkeysEnabled;
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
                _globalInputDisabled = false;

                _activeHotkeyEditBox = null;

                Logger.LogInfo($"{hotkeyType} hotkey set to: {newHotkey}");
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("GameOverlayPage.SaveHotkeyEdit failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("GameOverlayPage.SaveHotkeyEdit failed", ex);
            }
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
