using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Runtime.InteropServices;
using System.Threading;
using FlairX_Mod_Manager;
using FlairX_Mod_Manager.Models;
using FlairX_Mod_Manager.Services;
using XamlImage = Microsoft.UI.Xaml.Controls.Image;
using SharpImage = SixLabors.ImageSharp.Image;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class SettingsUserControl
    {
        
        // Hotkey management fields
        private TextBox? _activeHotkeyEditBox;
        private bool _previousHotkeysEnabled = true;
        
        // Hotkey definitions
        private readonly List<(string Key, string LabelKey, string DescKey, string Icon)> _hotkeyDefinitions = new()
        {
            ("ReloadManagerHotkey", "Reload_Mods_Tooltip", "SettingsPage_ReloadManagerHotkey_Description", "\uE72C"),
            ("ShuffleActiveModsHotkey", "SettingsPage_ShuffleActiveMods_Label", "SettingsPage_ShuffleActiveModsHotkey_Description", "\uE8B1"),
            ("DeactivateAllModsHotkey", "SettingsPage_DeactivateAllMods_Label", "SettingsPage_DeactivateAllModsHotkey_Description", "\uE711")
        };

        private void HotkeysEnabledToggle_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsManager.Current.HotkeysEnabled = HotkeysEnabledToggle.IsOn;
            SettingsManager.Save();
            UpdateToggleLabels();
            
            // Update hotkey section UI state
            UpdateHotkeysSectionState(HotkeysEnabledToggle.IsOn);
            
            // Refresh global hotkeys in MainWindow
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshGlobalHotkeys();
            }
        }



        private void UpdateHotkeysSectionState(bool enabled)
        {
            try
            {
                // Update hotkeys header opacity
                if (HotkeysHeader != null) HotkeysHeader.Opacity = enabled ? 1.0 : 0.5;
                
                // Update the entire SettingsHotkeysPanel opacity and disable/enable all children
                if (SettingsHotkeysPanel != null)
                {
                    SettingsHotkeysPanel.Opacity = enabled ? 1.0 : 0.5;
                    
                    // Enable/disable all child controls in the hotkeys panel
                    foreach (var child in SettingsHotkeysPanel.Children)
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
                Logger.LogError("Error updating hotkeys section state", ex);
            }
        }

        /// <summary>
        
        private void LoadSettingsHotkeys()
        {
            try
            {
                SettingsHotkeysPanel.Children.Clear();
                
                var lang = SharedUtilities.LoadLanguageDictionary();
                
                foreach (var (key, labelKey, descKey, icon) in _hotkeyDefinitions)
                {
                    var hotkeyValue = GetHotkeyValue(key);
                    var label = SharedUtilities.GetTranslation(lang, labelKey);
                    var description = SharedUtilities.GetTranslation(lang, descKey);
                    
                    var hotkeyRow = CreateSettingsHotkeyRow(key, hotkeyValue, label, description, icon);
                    SettingsHotkeysPanel.Children.Add(hotkeyRow);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load settings hotkeys", ex);
            }
        }
        
        private string GetHotkeyValue(string key)
        {
            return key switch
            {
                "ReloadManagerHotkey" => SettingsManager.Current.ReloadManagerHotkey,
                "ShuffleActiveModsHotkey" => SettingsManager.Current.ShuffleActiveModsHotkey,
                "DeactivateAllModsHotkey" => SettingsManager.Current.DeactivateAllModsHotkey,
                _ => ""
            };
        }
        
        private void SetHotkeyValue(string key, string value)
        {
            switch (key)
            {
                case "ReloadManagerHotkey":
                    SettingsManager.Current.ReloadManagerHotkey = value;
                    break;
                case "ShuffleActiveModsHotkey":
                    SettingsManager.Current.ShuffleActiveModsHotkey = value;
                    break;
                case "DeactivateAllModsHotkey":
                    SettingsManager.Current.DeactivateAllModsHotkey = value;
                    break;
            }
            // While recording a shortcut, global hotkeys are temporarily disabled. Persist the
            // user's actual preference instead of the temporary state - otherwise the next
            // launch starts with every main hotkey switched off.
            bool isRecordingHotkey = _activeHotkeyEditBox != null;
            if (isRecordingHotkey)
                SettingsManager.Current.HotkeysEnabled = _previousHotkeysEnabled;

            SettingsManager.Save();
            
            // Refresh global hotkeys
            if (App.Current is App app && app.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshGlobalHotkeys();
            }

            // Keep them disabled in memory until the editor closes.
            if (isRecordingHotkey)
                SettingsManager.Current.HotkeysEnabled = false;
        }
        
        private Border CreateSettingsHotkeyRow(string key, string keyCombo, string label, string description, string iconGlyph)
        {
            var keyBackground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HotkeyKeyBackground"];
            
            var rowBorder = new Border
            {
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 0, 2),
                Height = 60,
                Tag = key
            };
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Keys
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Spacer
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Description
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Buttons
            
            // Create keys panel
            var keysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(keyCombo, keyBackground, 64);
            Grid.SetColumn(keysPanel, 0);
            grid.Children.Add(keysPanel);
            
            // Description
            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.9
            };
            Grid.SetColumn(labelText, 2);
            grid.Children.Add(labelText);
            
            // Buttons panel
            var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            var defaultBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.6 };
            var accentColor = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            var accentBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(accentColor.A, accentColor.R, accentColor.G, accentColor.B));
            
            // Edit button
            var editBtn = CreateHotkeyActionButton("\uE70F", defaultBrush);
            editBtn.Tag = key;
            editBtn.PointerPressed += SettingsEditButton_PointerPressed;
            var lang = SharedUtilities.LoadLanguageDictionary();
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(editBtn, SharedUtilities.GetTranslation(lang, "ModDetailPage_EditHotkey_Tooltip"));
            buttonsPanel.Children.Add(editBtn);
            
            // Save button (initially hidden)
            var saveBtn = CreateHotkeyActionButton("\uE73E", accentBrush);
            saveBtn.Tag = key;
            saveBtn.Visibility = Visibility.Collapsed;
            saveBtn.PointerPressed += SettingsSaveButton_PointerPressed;
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(saveBtn, SharedUtilities.GetTranslation(lang, "ModDetailPage_SaveHotkey_Tooltip"));
            buttonsPanel.Children.Add(saveBtn);
            
            // Restore default button
            var restoreBtn = CreateHotkeyActionButton("\uE777", defaultBrush);
            restoreBtn.Tag = key;
            restoreBtn.PointerPressed += SettingsRestoreButton_PointerPressed;
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(restoreBtn, SharedUtilities.GetTranslation(lang, "ModDetailPage_RestoreDefaultHotkey_Tooltip"));
            buttonsPanel.Children.Add(restoreBtn);
            
            Grid.SetColumn(buttonsPanel, 5);
            grid.Children.Add(buttonsPanel);
            
            rowBorder.Child = grid;
            return rowBorder;
        }
        
        private Border CreateHotkeyActionButton(string glyph, Microsoft.UI.Xaml.Media.Brush foreground)
        {
            var border = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            
            var icon = new FontIcon
            {
                Glyph = glyph,
                FontSize = 16,
                Foreground = foreground,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                RenderTransform = new Microsoft.UI.Xaml.Media.ScaleTransform { ScaleX = 1, ScaleY = 1 },
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5)
            };
            
            border.Child = icon;
            
            border.PointerEntered += (s, e) => {
                border.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
                if (icon.RenderTransform is Microsoft.UI.Xaml.Media.ScaleTransform st) { st.ScaleX = 1.1; st.ScaleY = 1.1; }
            };
            border.PointerExited += (s, e) => {
                border.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                if (icon.RenderTransform is Microsoft.UI.Xaml.Media.ScaleTransform st) { st.ScaleX = 1.0; st.ScaleY = 1.0; }
            };
            
            return border;
        }
        
        private void SettingsEditButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_activeHotkeyEditBox != null) return; // Block if already editing
            
            if (sender is Border editBorder && editBorder.Tag is string key)
            {
                var parent = editBorder.Parent as StackPanel;
                if (parent == null) return;
                
                var grid = parent.Parent as Grid;
                if (grid == null) return;
                
                // Disable global hotkeys during editing
                _previousHotkeysEnabled = SettingsManager.Current.HotkeysEnabled;
                SettingsManager.Current.HotkeysEnabled = false;
                
                // Show save button, hide edit button
                editBorder.Visibility = Visibility.Collapsed;
                if (parent.Children.Count > 1 && parent.Children[1] is Border saveBtn)
                    saveBtn.Visibility = Visibility.Visible;
                
                // Replace keys panel with editable TextBox
                var keysPanel = grid.Children.OfType<StackPanel>().FirstOrDefault();
                if (keysPanel != null)
                {
                    var currentValue = GetHotkeyValue(key);
                    var editBox = new TextBox
                    {
                        Text = currentValue,
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 14,
                        MinWidth = 120,
                        Height = 32,
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = key,
                        PlaceholderText = "Press keys..."
                    };
                    editBox.PreviewKeyDown += SettingsHotkeyEditBox_PreviewKeyDown;
                    
                    int idx = grid.Children.IndexOf(keysPanel);
                    Grid.SetColumn(editBox, 0);
                    grid.Children.RemoveAt(idx);
                    grid.Children.Insert(idx, editBox);
                    editBox.Focus(FocusState.Programmatic);
                    
                    _activeHotkeyEditBox = editBox;
                }
            }
        }
        
        private async void SettingsSaveButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                if (sender is Border saveBorder && saveBorder.Tag is string key)
                {
                    var parent = saveBorder.Parent as StackPanel;
                    if (parent == null) return;

                    var grid = parent.Parent as Grid;
                    if (grid == null) return;

                    // Find edit TextBox
                    var editBox = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Tag as string == key);
                    if (editBox == null) return;

                    string newKeyCombo = editBox.Text.Trim();
                    if (string.IsNullOrEmpty(newKeyCombo)) newKeyCombo = GetHotkeyValue(key);

                    // Check for conflicts
                    var conflict = SharedUtilities.GetConflictingKeyboardHotkey(newKeyCombo, key);
                    if (conflict != null)
                    {
                        var lang = SharedUtilities.LoadLanguageDictionary();
                        if (App.Current is App _app && _app.MainWindow is MainWindow _mw) _mw.ShowWarningInfo(SharedUtilities.GetTranslation(lang, "HotkeyConflict_Message"));
                        return;
                    }

                    // Update settings
                    SetHotkeyValue(key, newKeyCombo);

                    // Replace TextBox with keys panel
                    var keyBackground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HotkeyKeyBackground"];
                    var keysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(newKeyCombo, keyBackground, 64);

                    int idx = grid.Children.IndexOf(editBox);
                    Grid.SetColumn(keysPanel, 0);
                    grid.Children.RemoveAt(idx);
                    grid.Children.Insert(idx, keysPanel);

                    // Hide save, show edit
                    saveBorder.Visibility = Visibility.Collapsed;
                    if (parent.Children.Count > 0 && parent.Children[0] is Border editBtn)
                        editBtn.Visibility = Visibility.Visible;

                    _activeHotkeyEditBox = null;

                    // Restore global hotkeys
                    SettingsManager.Current.HotkeysEnabled = _previousHotkeysEnabled;
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("SettingsUserControl.SettingsSaveButton_PointerPressed failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("SettingsUserControl.SettingsSaveButton_PointerPressed failed", ex);
            }
        }
        
        private void SettingsRestoreButton_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_activeHotkeyEditBox != null) return; // Block if editing
            
            if (sender is Border restoreBorder && restoreBorder.Tag is string key)
            {
                var defaultValue = GetDefaultHotkeyValue(key);
                if (string.IsNullOrEmpty(defaultValue)) return;
                
                var parent = restoreBorder.Parent as StackPanel;
                if (parent == null) return;
                
                var grid = parent.Parent as Grid;
                if (grid == null) return;
                
                // Update settings
                SetHotkeyValue(key, defaultValue);
                
                // Update keys panel
                var keyBackground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HotkeyKeyBackground"];
                var keysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(defaultValue, keyBackground, 64);
                
                var existingKeysPanel = grid.Children.OfType<StackPanel>().FirstOrDefault();
                if (existingKeysPanel != null)
                {
                    int idx = grid.Children.IndexOf(existingKeysPanel);
                    Grid.SetColumn(keysPanel, 0);
                    grid.Children.RemoveAt(idx);
                    grid.Children.Insert(idx, keysPanel);
                }
            }
        }
        
        private string GetDefaultHotkeyValue(string key)
        {
            return key switch
            {
                "ReloadManagerHotkey" => "Ctrl+R",
                "ShuffleActiveModsHotkey" => "Ctrl+S",
                "DeactivateAllModsHotkey" => "Ctrl+D",
                _ => ""
            };
        }
        
        private void SettingsHotkeyEditBox_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (sender is not TextBox editBox) return;
            
            e.Handled = true;
            
            var modifiers = new List<string>();
            
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                modifiers.Add("Ctrl");
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                modifiers.Add("Shift");
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                modifiers.Add("Alt");
            
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
