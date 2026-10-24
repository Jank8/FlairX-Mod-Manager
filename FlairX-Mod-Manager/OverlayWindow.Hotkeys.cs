using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using WinRT;
using WinRT.Interop;

namespace FlairX_Mod_Manager
{
    public sealed partial class OverlayWindow
    {

        private void UpdateHotkeyHint()
        {
            var lang = SharedUtilities.LoadLanguageDictionary("Overlay");
            var settings = SettingsManager.Current;
            
            // Keyboard hotkey hint
            var hotkey = settings.ToggleOverlayHotkey ?? "Alt+W";
            var gamepadCombo = settings.GamepadToggleOverlayCombo ?? "Back+Start";
            var gamepadHideHint = settings.GamepadEnabled ? $" | {gamepadCombo}" : "";
            
            // Format: "{hotkey} | {gamepadCombo} to hide" or "{hotkey} to hide"
            var hotkeyString = $"{hotkey}{gamepadHideHint}";
            var hintTemplate = SharedUtilities.GetTranslation(lang, "HotkeyHint");
            
            if (HotkeyHintText != null)
            {
                HotkeyHintText.Text = string.Format(hintTemplate, hotkeyString);
            }
            
            if (ClickToToggleText != null)
            {
                ClickToToggleText.Text = SharedUtilities.GetTranslation(lang, "ClickToToggle");
            }
            
            // Gamepad controls hint (only shown when gamepad is enabled)
            if (settings.GamepadEnabled)
            {
                var selectBtn = settings.GamepadSelectButton ?? "A";
                var prevCatBtn = settings.GamepadPrevCategoryButton ?? "LB";
                var nextCatBtn = settings.GamepadNextCategoryButton ?? "RB";
                var backBtn = settings.GamepadBackButton ?? "B";
                var categoryFavBtn = settings.GamepadCategoryFavoriteButton ?? "XB X";
                var modFavBtn = settings.GamepadModFavoriteButton ?? "XB Y";
                
                var gamepadHintTemplate = SharedUtilities.GetTranslation(lang, "GamepadHintWithFavorites");
                
                if (GamepadHintText != null)
                {
                    GamepadHintText.Text = string.Format(gamepadHintTemplate, selectBtn, prevCatBtn, nextCatBtn, backBtn, categoryFavBtn, modFavBtn);
                    GamepadHintText.Visibility = Visibility.Visible;
                }
                if (GamepadSeparator != null)
                    GamepadSeparator.Visibility = Visibility.Visible;

                if (ClickFocusHintText != null)
                    ClickFocusHintText.Visibility = Visibility.Visible;
                if (ClickHintSeparator != null)
                    ClickHintSeparator.Visibility = Visibility.Visible;
            }
            else
            {
                if (GamepadHintText != null)
                    GamepadHintText.Visibility = Visibility.Collapsed;
                if (GamepadSeparator != null)
                    GamepadSeparator.Visibility = Visibility.Collapsed;
                if (ClickFocusHintText != null)
                    ClickFocusHintText.Visibility = Visibility.Collapsed;
                if (ClickHintSeparator != null)
                    ClickHintSeparator.Visibility = Visibility.Collapsed;
            }
        }



        /// <summary>
        /// Load hotkeys for a specific mod
        /// </summary>
        private async Task<List<(string Key, string Description, string IniFile)>> LoadModHotkeysAsync(string modDirectory)
        {
            var hotkeys = new List<(string Key, string Description, string IniFile)>();
            
            try
            {
                var hotkeysJsonPath = Path.Combine(modDirectory, "hotkeys.json");
                
                if (!File.Exists(hotkeysJsonPath))
                    return hotkeys;
                
                var json = await Services.FileAccessQueue.ReadAllTextAsync(hotkeysJsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("hotkeys", out var hotkeysProp) && hotkeysProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var hotkey in hotkeysProp.EnumerateArray())
                    {
                        var key = hotkey.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : null;
                        var desc = hotkey.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
                        var iniFile = hotkey.TryGetProperty("iniFile", out var iniProp) ? iniProp.GetString() ?? "" : "";
                        
                        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(desc))
                            hotkeys.Add((key, desc, iniFile));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load hotkeys for mod: {modDirectory}", ex);
            }
            
            return hotkeys;
        }

        /// <summary>
        /// Update hotkeys panel for selected mod with directional animations
        /// </summary>
        private void UpdateHotkeysPanel(OverlayModItem? selectedMod)
        {
            try
            {
                // Cancel any pending hide timer
                _hideTimer?.Stop();
                _hideTimer = null;

                if (selectedMod == null || selectedMod.Hotkeys.Count == 0)
                {
                    // Hide hotkeys panel with animation
                    HideHotkeysPanel();
                    _previousSelectedMod = null;
                    _previousSelectedIndex = -1;
                    return;
                }
                
                // Determine animation direction based on navigation
                var direction = DetermineAnimationDirection(selectedMod);
                
                // Update tracking for next time
                _previousSelectedMod = selectedMod;
                _previousSelectedIndex = OverlayMods.IndexOf(selectedMod);
                
                // Always show hotkeys panel with directional animation
                ShowHotkeysPanelWithDirection(selectedMod, direction);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to update hotkeys panel", ex);
                HideHotkeysPanel();
            }
        }

        /// <summary>
        /// Determine animation direction based on navigation pattern
        /// </summary>
        private AnimationDirection DetermineAnimationDirection(OverlayModItem selectedMod)
        {
            try
            {
                // If no previous selection, default to right (slide in from right)
                if (_previousSelectedMod == null || _previousSelectedIndex < 0)
                {
                    return AnimationDirection.Right;
                }
                
                var currentIndex = OverlayMods.IndexOf(selectedMod);
                if (currentIndex < 0) return AnimationDirection.Right;
                
                var itemsPerRow = GetItemsPerRow();
                if (itemsPerRow <= 0) itemsPerRow = 1;
                
                var prevRow = _previousSelectedIndex / itemsPerRow;
                var prevCol = _previousSelectedIndex % itemsPerRow;
                var currentRow = currentIndex / itemsPerRow;
                var currentCol = currentIndex % itemsPerRow;
                
                // Determine primary direction of movement
                var rowDiff = currentRow - prevRow;
                var colDiff = currentCol - prevCol;
                
                // Prioritize row movement (up/down) over column movement
                if (Math.Abs(rowDiff) > Math.Abs(colDiff))
                {
                    return rowDiff > 0 ? AnimationDirection.Down : AnimationDirection.Up;
                }
                else if (colDiff != 0)
                {
                    return colDiff > 0 ? AnimationDirection.Right : AnimationDirection.Left;
                }
                
                // Fallback: if same position or can't determine, slide from right
                return AnimationDirection.Right;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to determine animation direction", ex);
                return AnimationDirection.Right;
            }
        }

        /// <summary>
        /// Show hotkeys panel with directional slide-in animation
        /// </summary>
        private void ShowHotkeysPanelWithDirection(OverlayModItem selectedMod, AnimationDirection direction)
        {
            try
            {
                // Cancel any ongoing hide animation
                if (_isAnimatingOut)
                {
                    _isAnimatingOut = false;
                }
                
                // If already animating in, cancel and start fresh
                if (_isAnimatingIn)
                {
                    _isAnimatingIn = false;
                }
                
                _isAnimatingIn = true;
                
                // Clear existing hotkeys
                HotkeysList.Children.Clear();
                
                // Update header
                var lang = SharedUtilities.LoadLanguageDictionary("Overlay");
                HotkeysHeader.Text = SharedUtilities.GetTranslation(lang, "Hotkeys");
                
                // Show column and separator - separator is always visible, just change opacity
                HotkeysColumn.Width = new GridLength(280, GridUnitType.Pixel);
                HotkeysScrollViewer.Visibility = Visibility.Visible;
                HotkeysSeparator.Opacity = 1.0; // Full opacity when hotkeys are shown
                
                // Calculate slide direction based on navigation
                var slideOffset = GetSlideOffset(direction);
                
                // Animate panel in with directional slide and fade
                HotkeysScrollViewer.Translation = slideOffset;
                HotkeysScrollViewer.Opacity = 0;
                
                // Animate to final position
                HotkeysScrollViewer.Translation = new System.Numerics.Vector3(0, 0, 0);
                HotkeysScrollViewer.Opacity = 1;
                
                // Always create and animate hotkey rows with wave effect
                AnimateHotkeyRowsInWithDirection(selectedMod.Hotkeys, direction);
                
                // Mark animation as complete after main panel animation
                var completeTimer = new DispatcherTimer();
                completeTimer.Interval = TimeSpan.FromMilliseconds(300);
                completeTimer.Tick += (s, e) =>
                {
                    completeTimer.Stop();
                    _isAnimatingIn = false;
                };
                completeTimer.Start();
                
                Logger.LogInfo($"Showing hotkeys panel with {direction} animation");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to show hotkeys panel with direction", ex);
                _isAnimatingIn = false;
            }
        }

        /// <summary>
        /// Get slide offset vector based on animation direction
        /// </summary>
        private System.Numerics.Vector3 GetSlideOffset(AnimationDirection direction)
        {
            return direction switch
            {
                AnimationDirection.Left => new System.Numerics.Vector3(-30, 0, 0),  // Slide from left
                AnimationDirection.Right => new System.Numerics.Vector3(30, 0, 0),  // Slide from right
                AnimationDirection.Up => new System.Numerics.Vector3(0, -20, 0),    // Slide from top
                AnimationDirection.Down => new System.Numerics.Vector3(0, 20, 0),   // Slide from bottom
                _ => new System.Numerics.Vector3(20, 0, 0)                          // Default: slide from right
            };
        }

        /// <summary>
        /// Animate hotkey rows in with directional wave effect
        /// </summary>
        private void AnimateHotkeyRowsInWithDirection(List<(string Key, string Description, string IniFile)> hotkeys, AnimationDirection direction)
        {
            try
            {
                if (hotkeys.Count == 0) return;

                // Group hotkeys by sub-mod folder (first segment of iniFile path)
                static string GetSubModGroup(string iniFile)
                {
                    if (string.IsNullOrEmpty(iniFile)) return "";
                    var parts = iniFile.Replace('\\', '/').Split('/');
                    return parts.Length > 1 ? parts[0] : "";
                }

                var groups = hotkeys.GroupBy(h => GetSubModGroup(h.IniFile)).ToList();
                bool showHeaders = groups.Count > 1 || (groups.Count == 1 && !string.IsNullOrEmpty(groups[0].Key));

                // Build flat list of UI elements (headers + rows) for wave animation
                var elements = new List<UIElement>();

                foreach (var group in groups)
                {
                    if (showHeaders && !string.IsNullOrEmpty(group.Key))
                    {
                        var header = new TextBlock
                        {
                            Text = group.Key,
                            FontSize = 11,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Opacity = 0,
                            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                            Margin = new Thickness(4, 6, 0, 2)
                        };
                        header.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(200) };
                        HotkeysList.Children.Add(header);
                        elements.Add(header);
                    }

                    foreach (var (key, description, _) in group)
                    {
                        var hotkeyRow = CreateSimpleHotkeyRow(key, description);
                        hotkeyRow.Opacity = 0;
                        hotkeyRow.Translation = GetRowSlideOffset(direction);
                        HotkeysList.Children.Add(hotkeyRow);
                        elements.Add(hotkeyRow);
                    }
                }

                // Wave animation for all elements
                var baseDelay = 50;
                var delayPerItem = Math.Max(20, Math.Min(40, elements.Count > 0 ? 250 / elements.Count : 40));
                var maxTotalTime = 600;
                if (baseDelay + elements.Count * delayPerItem > maxTotalTime)
                    delayPerItem = Math.Max(20, (maxTotalTime - baseDelay) / Math.Max(1, elements.Count));

                for (int i = 0; i < elements.Count; i++)
                {
                    var itemDelay = baseDelay + i * delayPerItem;
                    var currentElement = elements[i];

                    var timer = new DispatcherTimer();
                    timer.Interval = TimeSpan.FromMilliseconds(itemDelay);
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        if (HotkeysList.Children.Contains(currentElement))
                        {
                            currentElement.Opacity = 1;
                            if (currentElement is Border border)
                                border.Translation = new System.Numerics.Vector3(0, 0, 0);
                        }
                    };
                    timer.Start();
                }

                Logger.LogInfo($"Started {direction} wave animation for {elements.Count} hotkey elements");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to animate hotkey rows with direction", ex);
                HotkeysList.Children.Clear();
                foreach (var (key, description, _) in hotkeys)
                {
                    var hotkeyRow = CreateSimpleHotkeyRow(key, description);
                    HotkeysList.Children.Add(hotkeyRow);
                }
            }
        }

        /// <summary>
        /// Get slide offset for individual hotkey rows
        /// </summary>
        private System.Numerics.Vector3 GetRowSlideOffset(AnimationDirection direction)
        {
            return direction switch
            {
                AnimationDirection.Left => new System.Numerics.Vector3(-20, 0, 0),   // Slide from left
                AnimationDirection.Right => new System.Numerics.Vector3(20, 0, 0),   // Slide from right  
                AnimationDirection.Up => new System.Numerics.Vector3(0, -15, 0),     // Slide from top
                AnimationDirection.Down => new System.Numerics.Vector3(0, 15, 0),    // Slide from bottom
                _ => new System.Numerics.Vector3(15, 0, 0)                           // Default: slide from right
            };
        }

        /// <summary>
        /// Hide hotkeys panel with smooth fade-out animation
        /// </summary>
        private void HideHotkeysPanel()
        {
            try
            {
                // Cancel any ongoing show animation
                if (_isAnimatingIn)
                {
                    _isAnimatingIn = false;
                }
                
                // If already animating out or already hidden, don't start another animation
                if (_isAnimatingOut || HotkeysScrollViewer.Visibility == Visibility.Collapsed) return;
                
                _isAnimatingOut = true;
                
                // Cancel any existing hide timer
                _hideTimer?.Stop();
                
                // Animate out with slide and fade
                HotkeysScrollViewer.Translation = new System.Numerics.Vector3(20, 0, 0);
                HotkeysScrollViewer.Opacity = 0;
                HotkeysSeparator.Opacity = 0.3; // Dim separator when no hotkeys
                
                // Use a timer to hide after animation completes
                _hideTimer = new DispatcherTimer();
                _hideTimer.Interval = TimeSpan.FromMilliseconds(300);
                _hideTimer.Tick += (s, e) =>
                {
                    _hideTimer.Stop();
                    _hideTimer = null;
                    
                    // Only hide if we're still supposed to be animating out
                    if (_isAnimatingOut)
                    {
                        HotkeysScrollViewer.Visibility = Visibility.Collapsed;
                        HotkeysColumn.Width = new GridLength(0);
                        HotkeysList.Children.Clear();
                        _isAnimatingOut = false;
                        // Keep separator visible but dimmed
                    }
                };
                _hideTimer.Start();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to hide hotkeys panel", ex);
                // Fallback to immediate hide
                HotkeysScrollViewer.Visibility = Visibility.Collapsed;
                HotkeysColumn.Width = new GridLength(0);
                HotkeysList.Children.Clear();
                HotkeysSeparator.Opacity = 0.3;
                _isAnimatingOut = false;
            }
        }

        /// <summary>
        /// Create simplified hotkey row for overlay (no edit buttons) with animation support
        /// </summary>
        private Border CreateSimpleHotkeyRow(string keyCombo, string description)
        {
            var keyBackground = (Brush)Application.Current.Resources["HotkeyKeyBackground"];
            
            var rowBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 4),
                MinHeight = 48
            };
            
            // Add smooth transitions for animations
            rowBorder.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(200) };
            rowBorder.TranslationTransition = new Vector3Transition { Duration = TimeSpan.FromMilliseconds(250) };
            
            // Add hover effects
            rowBorder.PointerEntered += (s, e) =>
            {
                rowBorder.Translation = new System.Numerics.Vector3(-2, 0, 0); // Slight left movement
                if (rowBorder.Background is SolidColorBrush brush)
                {
                    // Slightly brighten on hover
                    var color = brush.Color;
                    var hoverColor = Windows.UI.Color.FromArgb(
                        color.A,
                        (byte)Math.Min(255, color.R + 10),
                        (byte)Math.Min(255, color.G + 10),
                        (byte)Math.Min(255, color.B + 10)
                    );
                    rowBorder.Background = new SolidColorBrush(hoverColor);
                }
            };
            
            rowBorder.PointerExited += (s, e) =>
            {
                rowBorder.Translation = new System.Numerics.Vector3(0, 0, 0); // Return to original position
                rowBorder.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];
            };
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Keys
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) }); // Spacer
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Description
            
            // Create keys panel (smaller size for overlay)
            var keysPanel = HotkeyIconHelper.CreateKeysPanelFromCombo(keyCombo, keyBackground, 48);
            Grid.SetColumn(keysPanel, 0);
            grid.Children.Add(keysPanel);
            
            // Description
            var descText = new TextBlock
            {
                Text = description,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.9,
                Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
            };
            Grid.SetColumn(descText, 2);
            grid.Children.Add(descText);
            
            rowBorder.Child = grid;
            return rowBorder;
        }
    }
}
