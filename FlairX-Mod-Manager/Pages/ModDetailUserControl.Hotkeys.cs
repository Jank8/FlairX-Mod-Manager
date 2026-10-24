using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager.Pages
{
    public sealed partial class ModDetailUserControl
    {
        public class HotkeyDisplay
        {
            public string Key { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }
        
        // Hotkey editing with gamepad support
        private GamepadManager? _hotkeyGamepad;
        private TextBox? _activeHotkeyEditBox;
        private List<string> _recordedGamepadButtons = new();
        private bool _isSettingHotkeyText = false;
        private bool _previousHotkeysEnabled = true;
        
        private HashSet<string> _favoriteHotkeys = new HashSet<string>();
        private List<string> _originalHotkeyOrder = new List<string>(); // Preserve original order
        
        private Border CreateHotkeyRow(string keyCombo, string description, int originalIndex)
        {
            var keyBackground = (Brush)Application.Current.Resources["HotkeyKeyBackground"];
            bool isFavorite = _favoriteHotkeys.Contains(keyCombo);
            
            var rowBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 0, 6),
                Height = 60,
                Tag = new HotkeyRowData { Key = keyCombo, Description = description, OriginalIndex = originalIndex },
                RenderTransform = new CompositeTransform()
            };
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Keys - auto width
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Spacer
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Description
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Buttons
            
            // Create keys panel
            var keysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(keyCombo, keyBackground, 64);
            Grid.SetColumn(keysPanel, 0);
            grid.Children.Add(keysPanel);
            
            // Description
            var descText = new TextBlock
            {
                Text = description,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.9
            };
            Grid.SetColumn(descText, 2);
            grid.Children.Add(descText);
            
            // Buttons panel (3 buttons in same style)
            var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            
            var accentColor = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            var accentBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(accentColor.A, accentColor.R, accentColor.G, accentColor.B));
            var defaultBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.6 };
            
            // Favorite button
            var favoriteBtn = CreateHotkeyActionButton(isFavorite ? "\uE735" : "\uE734", isFavorite ? accentBrush : defaultBrush);
            favoriteBtn.Tag = new HotkeyButtonData { KeyCombo = keyCombo, Description = description, OriginalIndex = originalIndex };
            favoriteBtn.PointerPressed += FavoriteButton_PointerPressed;
            
            // Set tooltip for favorite button
            var lang = SharedUtilities.LoadLanguageDictionary();
            ToolTipService.SetToolTip(favoriteBtn, SharedUtilities.GetTranslation(lang, isFavorite ? "ModDetailPage_UnfavoriteHotkey_Tooltip" : "ModDetailPage_FavoriteHotkey_Tooltip"));
            
            buttonsPanel.Children.Add(favoriteBtn);
            
            // Edit button
            var editBtn = CreateHotkeyActionButton("\uE70F", defaultBrush);
            editBtn.Tag = new HotkeyButtonData { KeyCombo = keyCombo, Description = description, OriginalIndex = originalIndex };
            editBtn.PointerPressed += EditButton_PointerPressed;
            
            // Set tooltip for edit button
            ToolTipService.SetToolTip(editBtn, SharedUtilities.GetTranslation(lang, "ModDetailPage_EditHotkey_Tooltip"));
            
            buttonsPanel.Children.Add(editBtn);
            
            // Rec button (always visible - for gamepad recording)
            var recBtn = CreateHotkeyActionButton("\uE7C8", defaultBrush);
            recBtn.Tag = new HotkeyButtonData { KeyCombo = keyCombo, Description = description, OriginalIndex = originalIndex };
            recBtn.PointerPressed += RecButton_PointerPressed;
            
            // Set tooltip for rec button
            ToolTipService.SetToolTip(recBtn, SharedUtilities.GetTranslation(lang, "ModDetailPage_RecordHotkey_Tooltip"));
            
            buttonsPanel.Children.Add(recBtn);
            
            // Save button (initially hidden, shown during edit)
            var saveBtn = CreateHotkeyActionButton("\uE73E", accentBrush);
            saveBtn.Tag = new HotkeyButtonData { KeyCombo = keyCombo, Description = description, OriginalIndex = originalIndex };
            saveBtn.Visibility = Visibility.Collapsed;
            saveBtn.PointerPressed += SaveButton_PointerPressed;
            
            // Set tooltip for save button
            ToolTipService.SetToolTip(saveBtn, SharedUtilities.GetTranslation(lang, "ModDetailPage_SaveHotkey_Tooltip"));
            
            buttonsPanel.Children.Add(saveBtn);
            
            // Restore default button (always visible)
            var restoreBtn = CreateHotkeyActionButton("\uE777", defaultBrush);
            restoreBtn.Tag = new HotkeyButtonData { KeyCombo = keyCombo, Description = description, OriginalIndex = originalIndex };
            restoreBtn.PointerPressed += RestoreDefaultButton_PointerPressed;
            
            // Set tooltip for restore button
            ToolTipService.SetToolTip(restoreBtn, SharedUtilities.GetTranslation(lang, "ModDetailPage_RestoreDefaultHotkey_Tooltip"));
            buttonsPanel.Children.Add(restoreBtn);
            
            Grid.SetColumn(buttonsPanel, 3);
            grid.Children.Add(buttonsPanel);
            
            rowBorder.Child = grid;
            return rowBorder;
        }
        
        private Border CreateHotkeyActionButton(string glyph, Brush foreground)
        {
            var border = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            
            var icon = new FontIcon
            {
                Glyph = glyph,
                FontSize = 16,
                Foreground = foreground,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                RenderTransform = new ScaleTransform { ScaleX = 1, ScaleY = 1 },
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5)
            };
            
            border.Child = icon;
            
            border.PointerEntered += (s, e) => {
                border.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
                if (icon.RenderTransform is ScaleTransform st) { st.ScaleX = 1.1; st.ScaleY = 1.1; }
            };
            border.PointerExited += (s, e) => {
                border.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                if (icon.RenderTransform is ScaleTransform st) { st.ScaleX = 1.0; st.ScaleY = 1.0; }
            };
            
            return border;
        }
        
        private class HotkeyButtonData
        {
            public string KeyCombo { get; set; } = "";
            public string Description { get; set; } = "";
            public int OriginalIndex { get; set; }
        }
        
        private async void FavoriteButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                // Block if in edit mode or recording gamepad
                if (_activeHotkeyEditBox != null || _isRecordingGamepad)
                    return;

                if (sender is Border border && border.Tag is HotkeyButtonData data && border.Child is FontIcon icon)
                {
                    var accentColor = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
                    var accentBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(accentColor.A, accentColor.R, accentColor.G, accentColor.B));
                    var defaultBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.6 };

                    bool wasFavorite = _favoriteHotkeys.Contains(data.KeyCombo);

                    var lang = SharedUtilities.LoadLanguageDictionary();

                    if (wasFavorite)
                    {
                        _favoriteHotkeys.Remove(data.KeyCombo);
                        icon.Glyph = "\uE734";
                        icon.Foreground = defaultBrush;
                        ToolTipService.SetToolTip(border, SharedUtilities.GetTranslation(lang, "ModDetailPage_FavoriteHotkey_Tooltip"));
                        await SaveFavoriteHotkeys();
                        AnimateHotkeyFromFavorites(data.KeyCombo);
                    }
                    else
                    {
                        _favoriteHotkeys.Add(data.KeyCombo);
                        icon.Glyph = "\uE735";
                        icon.Foreground = accentBrush;
                        ToolTipService.SetToolTip(border, SharedUtilities.GetTranslation(lang, "ModDetailPage_UnfavoriteHotkey_Tooltip"));
                        await SaveFavoriteHotkeys();
                        AnimateHotkeyToFavorites(data.KeyCombo);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                Logger.LogDebug("ModDetailUserControl.FavoriteButton_PointerPressed failed: canceled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("ModDetailUserControl.FavoriteButton_PointerPressed failed", ex);
            }
        }
        
        private void EditButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Block if already in edit mode or recording gamepad
            if (_activeHotkeyEditBox != null || _isRecordingGamepad)
                return;
                
            if (sender is Border editBorder && editBorder.Tag is HotkeyButtonData data)
            {
                var parent = editBorder.Parent as StackPanel;
                if (parent == null) return;
                
                var grid = parent.Parent as Grid;
                if (grid == null) return;
                
                // Show rec and save buttons, hide edit button (buttons: fav[0], edit[1], rec[2], save[3], restore[4])
                editBorder.Visibility = Visibility.Collapsed;
                if (parent.Children.Count > 2 && parent.Children[2] is Border recBtn)
                    recBtn.Visibility = Visibility.Visible;
                if (parent.Children.Count > 3 && parent.Children[3] is Border saveBtn)
                    saveBtn.Visibility = Visibility.Visible;
                
                // Replace keys panel with editable TextBox
                var keysPanel = grid.Children.OfType<StackPanel>().FirstOrDefault(sp => sp.Tag as string != "EditContainer");
                if (keysPanel != null)
                {
                    var editBox = new TextBox
                    {
                        Text = data.KeyCombo,
                        FontFamily = new FontFamily("Consolas"),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 14,
                        Height = 32,
                        MinWidth = 120,
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = "HotkeyEditBox",
                        PlaceholderText = "Press keys..."
                    };
                    editBox.PreviewKeyDown += HotkeyEditBox_PreviewKeyDown;
                    editBox.TextChanged += HotkeyEditBox_TextChanged;
                    
                    int idx = grid.Children.IndexOf(keysPanel);
                    Grid.SetColumn(editBox, 0);
                    grid.Children.RemoveAt(idx);
                    grid.Children.Insert(idx, editBox);
                    editBox.Focus(FocusState.Programmatic);
                    
                    // Set active edit box to block other buttons
                    _activeHotkeyEditBox = editBox;
                    
                    // Disable restore all button during edit
                    RestoreDefaultHotkeysButton.IsEnabled = false;
                }
            }
        }
        
        private void RecButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Border recBorder || recBorder.Tag is not HotkeyButtonData data) return;
            
            // Block if ANY hotkey is in edit mode or recording (one operation at a time)
            if (_activeHotkeyEditBox != null || _isRecordingGamepad)
                return;
            
            var parent = recBorder.Parent as StackPanel;
            if (parent == null) return;
            
            var grid = parent.Parent as Grid;
            if (grid == null) return;
            
            // Check if this is the same hotkey row or different one
            var editBox = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Tag as string == "HotkeyEditBox");
            
            if (editBox == null)
            {
                // Enter edit mode first - show save button, hide edit button (buttons: fav[0], edit[1], rec[2], save[3], restore[4])
                if (parent.Children.Count > 1 && parent.Children[1] is Border editBtn)
                    editBtn.Visibility = Visibility.Collapsed;
                if (parent.Children.Count > 3 && parent.Children[3] is Border saveBtn)
                    saveBtn.Visibility = Visibility.Visible;
                
                // Replace keys panel with TextBox + countdown
                var keysPanel = grid.Children.OfType<StackPanel>().FirstOrDefault(sp => sp.Tag as string != "EditContainer");
                if (keysPanel != null)
                {
                    // Create container for TextBox and countdown
                    var editContainer = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = "EditContainer"
                    };
                    
                    editBox = new TextBox
                    {
                        Text = data.KeyCombo,
                        FontFamily = new FontFamily("Consolas"),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 14,
                        Height = 32,
                        MinWidth = 120,
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = "HotkeyEditBox",
                        PlaceholderText = "Press keys..."
                    };
                    editBox.PreviewKeyDown += HotkeyEditBox_PreviewKeyDown;
                    editBox.TextChanged += HotkeyEditBox_TextChanged;
                    editContainer.Children.Add(editBox);
                    
                    // Countdown TextBlock (hidden initially)
                    _countdownTextBlock = new TextBlock
                    {
                        FontFamily = new FontFamily("ms-appx:///Assets/KenneyFonts/kenney_input_keyboard_mouse.ttf#Kenney Input Keyboard & Mouse"),
                        FontSize = 32,
                        VerticalAlignment = VerticalAlignment.Center,
                        Visibility = Visibility.Collapsed
                    };
                    editContainer.Children.Add(_countdownTextBlock);
                    
                    int idx = grid.Children.IndexOf(keysPanel);
                    Grid.SetColumn(editContainer, 0);
                    grid.Children.RemoveAt(idx);
                    grid.Children.Insert(idx, editContainer);
                    
                    // Set active edit box and disable restore all button
                    _activeHotkeyEditBox = editBox;
                    RestoreDefaultHotkeysButton.IsEnabled = false;
                }
            }
            
            if (editBox != null)
            {
                StartGamepadListening(editBox, recBorder);
            }
        }
        
        private bool _isRecordingGamepad = false;
        private Border? _activeRecBorder;
        private DispatcherTimer? _holdTimer;
        private int _holdCountdown = 3;
        private string _pendingCombo = "";
        private TextBlock? _countdownTextBlock;
        
        private void StartGamepadListening(TextBox editBox, Border recBorder)
        {
            _activeHotkeyEditBox = editBox;
            _activeRecBorder = recBorder;
            _recordedGamepadButtons.Clear();
            _pendingCombo = "";
            _holdCountdown = 3;
            
            if (_hotkeyGamepad == null)
                _hotkeyGamepad = new GamepadManager();
            
            if (!_hotkeyGamepad.CheckConnection())
            {
                editBox.PlaceholderText = "No controller";
                return;
            }
            
            _isRecordingGamepad = true;
            _previousHotkeysEnabled = SettingsManager.Current.HotkeysEnabled;
            SettingsManager.Current.HotkeysEnabled = false;
            
            editBox.Text = "";
            editBox.PlaceholderText = "Press & hold gamepad...";
            
            _hotkeyGamepad.ButtonPressed += OnHotkeyGamepadButtonPressed;
            _hotkeyGamepad.ButtonReleased += OnHotkeyGamepadButtonReleased;
            _hotkeyGamepad.StartPolling();
        }
        
        private void StopGamepadListening()
        {
            _isRecordingGamepad = false;
            _holdTimer?.Stop();
            _holdTimer = null;
            _holdCountdown = 3;
            _pendingCombo = "";
            
            SettingsManager.Current.HotkeysEnabled = _previousHotkeysEnabled;
            
            if (_hotkeyGamepad != null)
            {
                _hotkeyGamepad.ButtonPressed -= OnHotkeyGamepadButtonPressed;
                _hotkeyGamepad.ButtonReleased -= OnHotkeyGamepadButtonReleased;
                // Stop any ongoing vibration before stopping polling
                _hotkeyGamepad.Vibrate(0, 0, 0);
                _hotkeyGamepad.StopPolling();
            }
            
            if (_activeHotkeyEditBox != null)
                _activeHotkeyEditBox.PlaceholderText = "Press keys...";
            
            _activeRecBorder = null;
            _recordedGamepadButtons.Clear();
        }
        
        private void OnHotkeyGamepadButtonReleased(object? sender, GamepadButtonEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _holdTimer?.Stop();
                _holdTimer = null;
                _holdCountdown = 3;
                
                if (_activeHotkeyEditBox != null)
                {
                    _isSettingHotkeyText = true;
                    
                    if (_recordedGamepadButtons.Count <= 1)
                    {
                        // Single button - keep it, clear for next input
                        _activeHotkeyEditBox.Text = _pendingCombo;
                    }
                    else
                    {
                        // Combo released before 3s - clear it
                        _activeHotkeyEditBox.Text = "";
                        _pendingCombo = "";
                    }
                    
                    // Always clear recorded buttons after release - next press starts fresh
                    _recordedGamepadButtons.Clear();
                    
                    _isSettingHotkeyText = false;
                }
            });
        }
        
        private void HotkeyEditBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSettingHotkeyText) return;
            if (sender is TextBox editBox && string.IsNullOrEmpty(editBox.Text))
            {
                _recordedGamepadButtons.Clear();
            }
        }
        
        private void OnHotkeyGamepadButtonPressed(object? sender, GamepadButtonEventArgs e)
        {
            var buttonName = e.GetButtonDisplayName();
            
            // buttonName already contains the controller prefix (e.g., "PS △", "XB A", "NIN B")
            if (!_recordedGamepadButtons.Contains(buttonName))
                _recordedGamepadButtons.Add(buttonName);
            
            _pendingCombo = string.Join("+", _recordedGamepadButtons);
            _holdCountdown = 3;
            
            DispatcherQueue.TryEnqueue(() =>
            {
                _holdTimer?.Stop();
                
                if (_activeHotkeyEditBox != null)
                {
                    _isSettingHotkeyText = true;
                    _activeHotkeyEditBox.Text = _pendingCombo;
                    
                    if (_recordedGamepadButtons.Count == 1)
                    {
                        // Single button - no countdown
                        if (_countdownTextBlock != null)
                            _countdownTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        // Combo - start countdown timer
                        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                        _holdTimer.Tick += HoldTimer_Tick;
                        _holdTimer.Start();
                        
                        // Show countdown with Kenney font
                        if (_countdownTextBlock != null)
                        {
                            var countdownGlyph = _holdCountdown switch
                            {
                                3 => "\uE007", // "3" in Kenney Keyboard
                                2 => "\uE005", // "2" in Kenney Keyboard
                                1 => "\uE003", // "1" in Kenney Keyboard
                                _ => _holdCountdown.ToString()
                            };
                            _countdownTextBlock.Text = countdownGlyph;
                            _countdownTextBlock.Visibility = Visibility.Visible;
                        }
                    }
                    
                    _isSettingHotkeyText = false;
                }
            });
        }
        
        private void HoldTimer_Tick(object? sender, object e)
        {
            _holdCountdown--;
            
            if (_holdCountdown <= 0)
            {
                _holdTimer?.Stop();
                _holdTimer = null;
                
                DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        if (_activeHotkeyEditBox != null)
                        {
                            _isSettingHotkeyText = true;
                            _activeHotkeyEditBox.Text = _pendingCombo;
                            _isSettingHotkeyText = false;
                        }

                        // Hide countdown when done
                        if (_countdownTextBlock != null)
                        {
                            _countdownTextBlock.Visibility = Visibility.Collapsed;
                        }

                        // Confirmation vibration 400ms
                        _hotkeyGamepad?.Vibrate(30000, 30000, 400);
                        // Wait for vibration to finish before stopping
                        await Task.Delay(450);
                        StopGamepadListening();
                    }
                    catch (System.OperationCanceledException)
                    {
                        Logger.LogDebug("ModDetailUserControl.HoldTimer_Tick failed: canceled");
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError("ModDetailUserControl.HoldTimer_Tick failed", ex);
                    }
                });
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_activeHotkeyEditBox != null)
                    {
                        _isSettingHotkeyText = true;
                        _activeHotkeyEditBox.Text = _pendingCombo;
                        _isSettingHotkeyText = false;
                    }
                    
                    // Update countdown TextBlock
                    if (_countdownTextBlock != null)
                    {
                        var countdownGlyph = _holdCountdown switch
                        {
                            3 => "\uE007", // "3" in Kenney Keyboard
                            2 => "\uE005", // "2" in Kenney Keyboard
                            1 => "\uE003", // "1" in Kenney Keyboard
                            _ => _holdCountdown.ToString()
                        };
                        _countdownTextBlock.Text = countdownGlyph;
                    }
                });
            }
        }
        
        private void HotkeyEditBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (sender is not TextBox editBox) return;
            
            // Ignore keyboard during gamepad recording
            if (_isRecordingGamepad)
            {
                e.Handled = true;
                return;
            }
            
            e.Handled = true;
            _recordedGamepadButtons.Clear();
            
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
        
        // Helper class to store hotkey row data
        private class HotkeyRowData
        {
            public string Key { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int OriginalIndex { get; set; }
        }
    }
}
