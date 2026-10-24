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

                // Leaving the page mid-edit used to leave global hotkeys and the gamepad
                // disabled until the process restarted.
                RestoreGlobalInputAfterEditing();
            };
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
    }
}
