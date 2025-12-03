using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class GameOverlayPage : Page
    {
        private bool _isInitializing = true;

        public GameOverlayPage()
        {
            InitializeComponent();
            LoadSettings();
            _isInitializing = false;
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Current;
            
            // Load hotkey
            OverlayHotkeyTextBox.Text = settings.ToggleOverlayHotkey ?? "Alt+W";
            
            // Load always on top (default true)
            AlwaysOnTopToggle.IsOn = true;
            
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
                    Logger.LogInfo($"Overlay hotkey set to: {newHotkey}");
                }
            }
        }

        private void OverlayHotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Hotkey is now saved in KeyDown handler
            // This handler is kept for manual text entry compatibility
        }

        private void AlwaysOnTopToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            Logger.LogInfo($"Overlay always on top: {AlwaysOnTopToggle.IsOn}");
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
